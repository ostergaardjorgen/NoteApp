using System.Diagnostics;
using System.Text;

namespace NoteApp.Core.Llm;

public sealed record LlmResult(
    string Text,
    TimeSpan Elapsed,
    string ModelFile,
    int PromptTokens,
    int ResponseTokens)
{
    public double TokensPerSecond => Elapsed.TotalSeconds <= 0 ? 0 : ResponseTokens / Elapsed.TotalSeconds;
}

/// <summary>
/// Fremdrift under en kørsel.
///
/// <paramref name="Percent"/> er ord skrevet i forhold til skabelonens loft.
/// Det er et LOFT, ikke et maal: modellen stopper som regel af sig selv, foer
/// den naar det, og saa springer bjaelken fra fx 60 til faerdig. Derfor staar
/// de faktiske tal ved siden af — «312 ord · 84 sek» kan man forholde sig til,
/// selv naar procenten ikke naar hele vejen.
///
/// Alternativet var en snurrende cirkel, og den kan ikke svare paa det, man
/// spoerger om: koerer den stadig, eller er den gaaet i staa?
/// </summary>
public sealed record LlmProgress(string Message, string? PartialText = null,
                                 int Ord = 0, double Percent = 0, TimeSpan Forloebet = default);

/// <summary>
/// Kører en lokal sprogmodel gennem llama.cpp.
///
/// Der bruges engangskørsler med llama-cli frem for llama-server. Serveren
/// ville lytte på en port, også når man ikke bruger den — og selvom 127.0.0.1
/// ikke er "ud af maskinen", er "der lytter ingenting" en nemmere påstand at
/// stå på. Et referat laves én gang pr. møde; der er ingen grund til en server.
///
/// Modellen ligger på disken, og der er intet netværkskald i denne klasse.
/// </summary>
public sealed class LlmRunner
{
    private readonly string _cli;

    /// <summary>
    /// Sidste linje i prompten. Den er en instruktion til modellen OG den
    /// graense, svaret klippes fra — se RensSvar.
    /// </summary>
    private const string Skaeringsmaerke = "Skriv nu resultatet, og kun det.";

    public LlmRunner(string llamaCli) => _cli = llamaCli;

    /// <summary>
    /// Finder den binær, der laver ÉT svar og slutter.
    ///
    /// llama-cli.exe er chat-klienten og den, der skal bruges: den anvender
    /// modellens chat-skabelon, så systemprompten faktisk virker.
    ///
    /// llama-completion.exe er derimod en FORTSÆTTELSES-motor. Den anvender
    /// ikke chat-skabelonen — heller ikke med --jinja — og ignorerer -sys.
    /// Målt: den fortsatte mødereferatet i stedet for at skrive det, og kørte
    /// fem gange langsommere. Den står kun som nødløsning, hvis llama-cli
    /// mangler i en fremtidig udgivelse.
    /// </summary>
    public static string? FindCli()
    {
        var rod = Path.Combine(UserDataPaths.Root, "motor", "llama");
        if (!Directory.Exists(rod)) return null;

        foreach (var navn in new[] { "llama-cli.exe", "llama-completion.exe" })
        {
            var fundet = Directory.EnumerateFiles(rod, navn, SearchOption.AllDirectories).FirstOrDefault();
            if (fundet is not null) return fundet;
        }
        return null;
    }

    public static string ModelDirectory => Path.Combine(UserDataPaths.Root, "motor", "sprogmodeller");

    public static IReadOnlyList<string> InstalledModels()
    {
        if (!Directory.Exists(ModelDirectory)) return Array.Empty<string>();
        return Directory.GetFiles(ModelDirectory, "*.gguf").OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Kører én prompt og venter på hele svaret.
    ///
    /// Konteksten sættes efter promptens længde frem for til et fast tal: et
    /// 20-minutters møde fylder omkring 4.000 tokens, og at reservere plads
    /// til 32.000 ville koste hukommelse, der ikke er på et 6 GB-kort.
    /// </summary>
    public async Task<LlmResult> RunAsync(
        string modelPath,
        PromptTemplate template,
        string userPrompt,
        IProgress<LlmProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(modelPath)) throw new FileNotFoundException("Sprogmodellen findes ikke", modelPath);

        // Er der plads i den almindelige hukommelse? Er der ikke, swapper
        // Windows, og saa holder HELE maskinen op med at svare — ikke kun
        // denne koersel. Maalt 12. august: en 12,4 GB model tog 18,9 GB RAM og
        // efterlod 3,6 GB af 32,5; appen maatte lukkes haardt.
        //
        // Der kastes frem for at advare og fortsaette: naar maskinen er gaaet
        // i staa, er der ingen, der kan naa at trykke "afbryd".
        var modelStørrelse = new FileInfo(modelPath).Length;
        var (plads, krævet, fri) = Arbejdshukommelse.HarPlads(modelStørrelse);
        if (!plads)
            throw new InvalidOperationException(
                $"Der er ikke hukommelse nok til {Path.GetFileName(modelPath)}.\n\n" +
                $"Modellen fylder {Arbejdshukommelse.Gigabyte(modelStørrelse)} og har brug for cirka " +
                $"{Arbejdshukommelse.Gigabyte(krævet)} arbejdshukommelse. Der er " +
                $"{Arbejdshukommelse.Gigabyte(fri)} fri.\n\n" +
                "Luk nogle programmer, eller vælg en mindre model. Startes den alligevel, " +
                "begynder Windows at bruge disken som hukommelse, og så holder hele maskinen " +
                "op med at svare — ikke kun NoteApp.");

        // Groft skoen: dansk tekst lander omkring 3 tegn pr. token. Bevidst
        // rundhaandet, saa prompten ikke bliver klippet over.
        var anslaaetPrompt = (template.RenderSystem().Length + userPrompt.Length) / 3 + 200;
        var kontekst = Naermeste2Potens(anslaaetPrompt + template.MaxTokens + 512);

        // Prompten sendes i en FIL, ikke paa kommandolinjen. Windows knaekker
        // ved ca. 32.000 tegn, og et 90-minutters moede fylder let mere end
        // det. Filen ligger i systemets midlertidige mappe og slettes bagefter.
        // Sidste linje er baade en instruktion og et skaeringsmaerke.
        // llama.cpp skriver prompten ud trods --no-display-prompt, og hvordan
        // den ombrydes undervejs kan ikke forudsiges — derfor laegges
        // graensen et sted, VI bestemmer, frem for at gaette paa udgangen.
        var medMaerke = userPrompt.TrimEnd() + "\n\n" + Skaeringsmaerke;

        var promptFil = Path.Combine(Path.GetTempPath(), $"noteapp-prompt-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(promptFil, medMaerke, new UTF8Encoding(false), ct);

        var args = new List<string>
        {
            "-m", modelPath,

            // «/no_think» SLUKKER MODELLENS HØJTTÆNKNING.
            //
            // «--reasoning-budget 0» nedenfor gør det ikke i dette byg af
            // llama.cpp. Målt 20-08-2026: Qwen3-4B skrev et helt afsnit på
            // ENGELSK om, hvordan den ville gribe opgaven an, før den svarede
            // — og ved 50 tokens i sekundet er det dér, tiden går. Med
            // «/no_think» i systemprompten faldt den samme kørsel fra 63 til
            // 9 sekunder.
            //
            // Det er Qwens egen kontakt. Andre modeller ser det bare som et
            // ord i prompten og tager ikke skade af det.
            "-sys", template.RenderSystem().TrimEnd() + " /no_think",

            "-f", promptFil,
            "-n", template.MaxTokens.ToString(),
            "-c", kontekst.ToString(),
            "--temp", template.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-ngl", "999",              // laeg alt paa GPU'en; llama.cpp falder selv tilbage
            "--no-warmup",

            // KV-CACHEN KVANTISERES.
            //
            // Den er den egentlige begrænsning på et 6 GB-kort — ikke vægtene.
            // Målt på et møde på en time: udskriften fylder 23.345 tokens, og
            // Qwen3 bruger omkring 147 KB pr. token i fuld opløsning. Det er
            // 3,4 GB oven i modellen, og så er kortet fyldt.
            //
            // Med q8_0 halveres det. Det er forskellen på, at en model på 4B
            // kan læse et helt møde på én gang — og at den ikke kan.
            "--cache-type-k", "q8_0",
            "--cache-type-v", "q8_0",

            // De fire her hoerer sammen, og hver af dem kostede en fejlkoersel:
            //
            // --jinja            Uden den bruges modellens chat-skabelon ikke.
            //                    Saa ignoreres systemprompten, og teksten
            //                    behandles som noget der skal FORTSAETTES.
            //                    Foerste koersel gentog hele moedet ordret.
            // -st                Een tur, saa slut. Uden den bliver llama-cli
            //                    staaende som chat og afslutter med kode 130,
            //                    naar stdin lukkes.
            // --no-display-prompt Ellers skrives hele prompten ud foer svaret.
            // --reasoning-budget 0 Qwen3 taenker hoejt paa ENGELSK foerst og
            //                    kan gaa i ring i det. Virker kun sammen med
            //                    --jinja.
            "--jinja",
            "-st",
            "--no-display-prompt",
            "--reasoning-budget", "0"
        };

        var psi = new ProcessStartInfo(_cli)
        {
            // Stdin skal omdirigeres OG lukkes med det samme. Arver llama-cli
            // konsollens input, opfatter den det som et afbryd og afslutter
            // med kode 130, midt i svaret. Samme faelde som whisper-stream.
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var svar = new StringBuilder();
        var log = new StringBuilder();
        var ur = Stopwatch.StartNew();

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // llama-cli skriver svaret paa stdout og alt om modelindlaesning,
        // hastighed og hukommelse paa stderr. Begge skal laeses, ellers
        // fyldes bufferen og processen gaar i staa.
        // Ord skrevet indtil nu. Tokens kan ikke taelles herfra — llama-cli
        // oplyser dem foerst til sidst — men ord kan, og et ord er cirka 1,3
        // token paa dansk. Det er godt nok til en bjaelke, og tallet ved siden
        // af er det rigtige ord-tal, ikke et skoen.
        var ordSkrevet = 0;
        var maksOrd = Math.Max(1, (int)(template.MaxTokens / 1.3));

        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            svar.AppendLine(e.Data);

            ordSkrevet += e.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            progress?.Report(new LlmProgress(
                "Skriver …", e.Data,
                ordSkrevet,
                Math.Min(100, ordSkrevet * 100.0 / maksOrd),
                ur.Elapsed));
        };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            log.AppendLine(e.Data);
            if (e.Data.Contains("llama_model_loader", StringComparison.Ordinal))
                progress?.Report(new LlmProgress("Indlæser modellen …"));
        };

        progress?.Report(new LlmProgress("Starter modellen …"));

        p.Start();
        try { p.StandardInput.Close(); } catch (IOException) { }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        ur.Stop();
        try { File.Delete(promptFil); } catch (IOException) { }

        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"llama-cli afsluttede med kode {p.ExitCode}.\n{Sidste(log.ToString(), 8)}");

        var raat = svar.ToString();
        var tekst = RensSvar(raat, userPrompt);
        var (prompt, respons) = LaesTokental(log + raat, tekst);

        return new LlmResult(tekst, ur.Elapsed, Path.GetFileName(modelPath), prompt, respons);
    }

    /// <summary>
    /// Skræller llama.cpp's egen pynt fra svaret.
    ///
    /// llama-cli skriver et banner med ASCII-kunst, en liste over
    /// chat-kommandoer, en linje med prompten efter «&gt; », og til sidst en
    /// statuslinje med hastigheder. Intet af det hører til i et mødereferat.
    /// Grænserne findes ved faste markører frem for ved linjetælling, så en
    /// ny udgave med et andet banner ikke stille begynder at klippe i teksten.
    /// </summary>
    private static string RensSvar(string raa, string userPrompt)
    {
        var tekst = raa.Replace("\r\n", "\n");

        // Prompten bliver vist trods --no-display-prompt. At klippe ved
        // "sidste linje der begynder med >" raekker ikke: kun promptens
        // FOERSTE linje faar det praefiks, saa resten af den blev staaende i
        // referatet. Derfor klippes der efter promptens egen slutning, som vi
        // kender ordret.
        var linjer = tekst.Split('\n').ToList();

        // Bannerlinjer indtil den foerste "> ".
        var forstePrompt = linjer.FindIndex(l => l.StartsWith("> ", StringComparison.Ordinal));
        if (forstePrompt >= 0) linjer = linjer.Skip(forstePrompt).ToList();

        tekst = string.Join('\n', linjer);
        if (tekst.StartsWith("> ", StringComparison.Ordinal)) tekst = tekst[2..];

        // llama.cpp gengiver prompten ordret trods --no-display-prompt, og
        // svaret foelger lige efter. Lange linjer bliver ombrudt undervejs,
        // saa hverken et skaeringsmaerke eller en tekstsammenligning holder.
        // Derfor springes prompten over TEGN FOR TEGN, hvor mellemrum og
        // linjeskift ignoreres paa begge sider — saa er ombrydning ligegyldig.
        tekst = SpringPromptOver(tekst, userPrompt.TrimEnd() + "\n\n" + Skaeringsmaerke);

        linjer = tekst.Split('\n').ToList();

        // Statuslinjen og afskeden hoerer heller ikke med.
        var stop = linjer.FindIndex(l => l.TrimStart().StartsWith("[ Prompt:", StringComparison.Ordinal)
                                      || l.Trim() == "Exiting...");
        if (stop >= 0) linjer = linjer.Take(stop).ToList();

        tekst = string.Join('\n', linjer).Trim();

        // llama.cpp markerer selv, hvor den har afkortet gengivelsen.
        if (tekst.StartsWith("... (truncated)", StringComparison.Ordinal))
            tekst = tekst["... (truncated)".Length..].TrimStart();

        // ANSI-farvekoder fra terminaludgangen.
        tekst = System.Text.RegularExpressions.Regex.Replace(tekst, @"\x1B\[[0-9;]*[A-Za-z]", "");

        return tekst.Trim();
    }

    /// <summary>
    /// Springer promptens gengivelse over i udgangen. Mellemrum og linjeskift
    /// ignoreres på begge sider, så llama.cpp's ombrydning af lange linjer
    /// ikke ødelægger sammenligningen.
    ///
    /// Gengivelsen bliver AFKORTET ved lange prompter. Netop derfor klippes
    /// der ved afvigelsen: det sted, hvor de to tekster holder op med at være
    /// ens, er dér hvor gengivelsen stopper og svaret begynder.
    ///
    /// Er der ikke tegnmæssigt overlap af betydning, returneres teksten
    /// uændret. Så står prompten med i udkastet, hvilket er grimt — men det
    /// er bedre end at klippe i selve referatet.
    /// </summary>
    private static string SpringPromptOver(string udgang, string prompt)
    {
        int i = 0, j = 0, ens = 0;

        while (i < udgang.Length && j < prompt.Length)
        {
            if (char.IsWhiteSpace(udgang[i])) { i++; continue; }
            if (char.IsWhiteSpace(prompt[j])) { j++; continue; }

            if (udgang[i] != prompt[j]) break;

            i++; j++; ens++;
        }

        // Kraev et rimeligt overlap, foer der klippes. Ellers ville et
        // tilfaeldigt sammenfald paa de foerste tegn kunne aede svaret.
        return ens > 40 ? udgang[i..].TrimStart() : udgang;
    }

    /// <summary>
    /// Tokentallene står i llama.cpp's egen opsummering på stderr. De bruges
    /// til at måle, hvad en kørsel koster — ikke til noget funktionelt.
    /// </summary>
    private static (int Prompt, int Respons) LaesTokental(string log, string svar)
    {
        var prompt = System.Text.RegularExpressions.Regex.Match(log, @"prompt eval time.*?/\s*(\d+) tokens");
        var respons = System.Text.RegularExpressions.Regex.Match(log, @"\beval time.*?/\s*(\d+) runs");

        if (prompt.Success || respons.Success)
            return (prompt.Success ? int.Parse(prompt.Groups[1].Value) : 0,
                    respons.Success ? int.Parse(respons.Groups[1].Value) : 0);

        // Nyere udgaver skriver kun hastigheder:
        // "[ Prompt: 212.8 t/s | Generation: 36.7 t/s ]". Saa kendes antallet
        // ikke, men svarets laengde kan omregnes — dansk tekst lander omkring
        // 3 tegn pr. token. Det er et skoen og maerkes som saadan i navnet.
        var skoenRespons = svar.Length / 3;
        return (0, skoenRespons);
    }

    private static string Sidste(string tekst, int linjer) =>
        string.Join('\n', tekst.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(linjer));

    /// <summary>
    /// Konteksten, afrundet OPAD til nærmeste 1024.
    ///
    /// HVORFOR IKKE NÆRMESTE TOPOTENS
    ///
    /// Den rundede før op til 2048, 4096, 8192 … Det er pænt, men det spilder
    /// hukommelse i store spring: et møde, der har brug for 17.300 tokens, fik
    /// 32.768 — næsten det dobbelte. Hver token koster plads i KV-cachen, og
    /// på et 6 GB-kort er det forskellen på at ligge på grafikkortet og at
    /// blive skubbet over på processoren, hvor det tager ti gange så lang tid.
    ///
    /// Målt 14. august 2026 med et 61-minutters møde: 5.795 MiB af 6.144 brugt
    /// — 94 %. Der var ikke plads til at være rundhåndet.
    ///
    /// 1024 er stadig et rundt tal for llama.cpp, og forskellen mellem 17.408
    /// og 17.300 betyder ingenting.
    /// </summary>
    private static int Naermeste2Potens(int n)
    {
        const int trin = 1024;
        const int mindst = 2048;
        const int mest = 131072;

        if (n <= mindst) return mindst;

        var v = (n + trin - 1) / trin * trin;
        return Math.Min(v, mest);
    }
}
