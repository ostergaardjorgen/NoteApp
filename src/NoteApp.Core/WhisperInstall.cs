using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>Hvilke sprog en model overhovedet kan. Ikke en anbefaling — en grænse.</summary>
public enum ModelLanguages
{
    /// <summary>Kan dansk. Alle flersprogede modeller kan også engelsk.</summary>
    Multilingual,

    /// <summary>KUN engelsk. Vælges den til et dansk møde, kommer der volapyk ud.</summary>
    EnglishOnly
}

public sealed record WhisperModel(
    string Id,
    string FileName,
    long Bytes,
    string Url,
    ModelLanguages Languages,
    string Summary,
    string Pros,
    string Cons)
{
    public double GigaBytes => Bytes / 1024.0 / 1024.0 / 1024.0;

    public string SizeText => Bytes >= 1_000_000_000
        ? $"{GigaBytes:0.0} GB"
        : $"{Bytes / 1024.0 / 1024.0:0} MB";

    public bool SupportsDanish => Languages == ModelLanguages.Multilingual;
}

public sealed record InstallState(
    string? WhisperCli,
    string? ModelPath,
    bool HasCuda,
    string? EngineVersion,
    DateTimeOffset? EngineInstalled)
{
    public bool IsComplete => WhisperCli is not null && ModelPath is not null;

    public string Engine => HasCuda ? "GPU (CUDA)" : "CPU";

    public string? ModelFileName => ModelPath is null ? null : Path.GetFileName(ModelPath);

    /// <summary>
    /// Hvad der skal stå om motorens alder.
    ///
    /// whisper.cpp stempler hverken sin exe eller sit output med en version,
    /// så den kendes kun, hvis appen selv har installeret motoren. Ellers
    /// vises datoen på binæren i stedet — den kan vi kontrollere. Et felt,
    /// der siger "ukendt", er værre end ingenting: det ligner en fejl.
    /// </summary>
    public (string Label, string Value) AgeLine => EngineVersion is not null
        ? ("Version", EngineVersion)
        : ("Sidst opdateret", EngineInstalled?.ToLocalTime().ToString("d. MMMM yyyy") ?? "—");
}

/// <summary>
/// Finder Whisper-motoren og modellen, kender hvilke modeller der kan hentes,
/// og kan aflæse hvilken version af motoren der faktisk kører.
///
/// Rækkefølgen i <see cref="Locate"/> er ikke tilfældig: en lokal CUDA-build
/// vinder over en hentet CPU-build, fordi forskellen på GPU og CPU er omkring
/// en faktor ti på en 20-minutters optagelse.
/// </summary>
public static class WhisperInstall
{
    /// <summary>Motor og model lander i datamappen — de er brugerens, ikke kodens.</summary>
    public static string Root => Path.Combine(UserDataPaths.Root, "motor");

    /// <summary>
    /// DET ENESTE STED, MODELLER LIGGER: {datamappe}\motor\modeller.
    ///
    /// Der var to steder. Ud over det her ledte koden ogsaa i
    /// C:\NoteApp\models — en haardkodet sti ind i KODELAGERET. Den var
    /// bekvem paa udviklingsmaskinen og forkert alle andre steder: paa en
    /// kundes maskine findes mappen ikke, saa den lagde et opslag ind, der
    /// aldrig kunne give noget.
    ///
    /// Vaerre var, at den virkede. Modellerne endte spredt — large-v3 og
    /// medium i kodelageret, small i datamappen — og saa kan man ikke svare
    /// paa, hvad maskinen har, uden at lede to steder og huske begge.
    /// Filerne er op til 2,9 GB stykket; de hoerer i datamappen, som ligger
    /// uden for git, og ingen andre steder. Samlet 25-08-2026.
    /// </summary>
    public static string ModelDirectory => Path.Combine(Root, "modeller");

    public static string EngineDirectory => Path.Combine(Root, "whisper");

    /// <summary>
    /// Modellerne, appen kan hente.
    ///
    /// HER LAA SEKS. NU LIGGER DER TO.
    ///
    /// Katalogget havde large-v3, large-v3-turbo, medium, small, small.en og
    /// base.en med fordele og ulemper ved hver. Det saa ud som et oplyst valg
    /// og var en faelde: de smaa er maerkbart daarligere paa dansk, og de to
    /// engelske kan slet ikke dansk - de giver volapyk frem for en fejl. Et
    /// valg, hvor hvert alternativ goer loesningen ringere, er ikke et valg.
    /// Fjernet 18-08-2026.
    ///
    /// ROEST V3 BLEV PROEVET OG ER IKKE MED. Se doc/maaling-whisper.md.
    /// Modellen er en dansk finjustering af large-v3, og CoRal maaler den
    /// klart bedre end originalen - men den er finjusteret til at koere UDEN
    /// tidsstempler (forced_decoder_ids peger paa notimestamps, og
    /// suppress_tokens er tom). whisper.cpp beder altid om tidsstempler, og
    /// saa loeber afkodningen i ring. Slaar man dem fra med -nt, holder den
    /// op med at loebe i ring og begynder i stedet at klippe hvert vindue
    /// over paa midten. Maalt paa den samme oplaesning: 48,5 % ordfejlrate
    /// mod large-v3's 10,0 %, og 12 af 17 negationer tabt.
    ///
    /// Det er ikke modellen, der er daarlig. Det er GGML-vejen ind i
    /// whisper.cpp, der ikke baerer dens afkodningsopsaetning med. Vil man
    /// bruge den, skal det vaere gennem faster-whisper (CTranslate2), og det
    /// er en anden motor end den, appen har.
    ///
    /// Stoerrelserne er de faktiske filstoerrelser, maalt med et HEAD-kald.
    /// Tallet er ikke pynt: Downloader bruger det til at afgoere, om en fil,
    /// der allerede ligger der, er hel. Er tallet forkert, hentes modellen
    /// igen hver eneste gang.
    /// </summary>
    public static readonly IReadOnlyList<WhisperModel> Models = new[]
    {
        new WhisperModel(
            "large-v3", "ggml-large-v3.bin", 3_095_033_483L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
            ModelLanguages.Multilingual,
            "Standardvalget. Målt på dansk to gange — på oplæsning og på et rigtigt møde.",
            "Målt 25-08-2026 på et rigtigt møde: 32,7 minutter dansk tale skrevet ud på 1 minut 57 sekunder med stilhedsmodel — omkring sytten gange hurtigere end lyden er lang. En times møde tager altså cirka fire minutter. På oplæsning tidligere: 10,0 % ordfejlrate på 2.365 ord, 12 af 19 fagtermer og 11 af 17 negationer bevaret.",
            "Fylder 2,9 GB og kræver godt 3 GB på grafikkortet. På ren CPU er den for langsom til daglig brug."),

        new WhisperModel(
            "large-v3-turbo", "ggml-large-v3-turbo.bin", 1_624_555_275L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin",
            ModelLanguages.Multilingual,
            "Halv størrelse. Til en maskine, der ikke kan holde large-v3 — men den taber tale.",
            "Målt 25-08-2026 på det samme møde som large-v3: 1,76 gange hurtigere, ikke dobbelt. Lige så god på negationer (17 mod 18 steder, enige om 16). Ét sted skrev den rigtigere dansk end large-v3.",
            "MEN DEN TABER TALE, HVOR LYDEN BLIVER SVÆR. På det samme møde droppede den en hel sætning, og de sidste tredive sekunder skiftede den til islandsk. Slutningen af et møde er dér, aftaler og næste skridt bliver sagt — og en sætning, der ikke blev skrevet, kan ingen sprogmodel genskabe bagefter. Vælg den, hvis grafikkortet ikke kan holde large-v3; ikke for hastigheden alene."),
    };

    /// <summary>
    /// Den model, appen bruger, hvis brugeren ikke har valgt andet.
    ///
    /// Staar ET sted, saa den kan skiftes ET sted. Baade opsaetningen,
    /// AI-modeller-skaermen og reserveopslaget i <see cref="FindModel"/>
    /// spoerger her.
    /// </summary>
    public static WhisperModel Standard => Models[0];

    public static WhisperModel? Model(string id) =>
        Models.FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Modeller, der overhovedet kan bruges til danske møder.</summary>
    public static IEnumerable<WhisperModel> DanishCapable => Models.Where(m => m.SupportsDanish);

    public static InstallState Locate(string? preferredModelId = null)
    {
        var cli = FindCli();
        var cuda = cli is not null && File.Exists(Path.Combine(Path.GetDirectoryName(cli)!, "ggml-cuda.dll"));
        var model = FindModel(preferredModelId);

        // Manifestet foerst: det er det eneste sted, versionen faktisk staar.
        // whisper.cpp stempler hverken sin exe eller sit output, saa er motoren
        // lagt der i haanden, ER versionen ukendt — og det skal siges, ikke gaettes.
        var version = cli is null ? null : (EngineManifest.Load(cli)?.Version ?? ReadVersion(cli));

        // Filens dato er det, vi altid kan svare paa. Manifestets dato vinder,
        // naar appen selv har hentet motoren.
        DateTimeOffset? installeret = null;
        if (cli is not null)
        {
            installeret = EngineManifest.Load(cli)?.InstalledAt;
            if (installeret is null)
            {
                try { installeret = new FileInfo(cli).LastWriteTime; }
                catch (IOException) { }
            }
        }

        return new InstallState(cli, model, cuda, version, installeret);
    }

    /// <summary>
    /// Aflæser motorens version ved at spørge binæren selv. whisper.cpp
    /// skriver sin version til stderr sammen med resten af fremdriften, så
    /// begge strømme læses.
    ///
    /// Kan versionen ikke aflæses, er svaret null frem for et gæt: en forkert
    /// version i UI'et er værre end ingen, fordi den bruges til at afgøre, om
    /// der skal opdateres.
    /// </summary>
    public static string? ReadVersion(string whisperCli)
    {
        try
        {
            var psi = new ProcessStartInfo(whisperCli, "--help")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return null;

            // Begge stroemme skal laeses SAMTIDIG. Laeser man stdout til ende
            // foerst, gaar processen i staa naar stderr-bufferen loeber fuld —
            // og whisper skriver det meste af sin hjaelpetekst til stderr.
            // Det var praecis den doedvande, foerste udgave af denne metode gik i.
            var sb = new StringBuilder();
            p.OutputDataReceived += (_, e) => { lock (sb) if (e.Data is not null) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { lock (sb) if (e.Data is not null) sb.AppendLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // Luk stdin, saa binaeren ikke kan blive staaende og vente paa input.
            try { p.StandardInput.Close(); } catch { }

            if (!p.WaitForExit(5000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            string tekst;
            lock (sb) tekst = sb.ToString();

            var m = Regex.Match(tekst, @"whisper\.cpp\s+version\s*[:=]?\s*(v?[\d.]+)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;

            m = Regex.Match(tekst, @"\bv(\d+\.\d+\.\d+)\b");
            return m.Success ? "v" + m.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindCli()
    {
        foreach (var mappe in EngineDirectories())
        {
            if (!Directory.Exists(mappe)) continue;

            // whisper-cli.exe FØRST: main.exe findes stadig i udgivelsen, men
            // er forældet og skriver kun en advarsel uden at transskribere.
            var fundet = Directory.EnumerateFiles(mappe, "whisper-cli.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (fundet is not null) return fundet;
        }
        return null;
    }

    private static IEnumerable<string> EngineDirectories()
    {
        // RAEKKEFOELGEN ER VENDT 19-08-2026, OG GRUNDEN ER EN FEJL.
        //
        // Repoets build stod foerst, fordi den paa udviklingsmaskinen er en
        // CUDA-build, og GPU slaar CPU med omkring en faktor ti. Det virkede,
        // indtil "Opdatér motoren" kom til: knappen hentede v1.9.2 til
        // datamappen, skrev et manifest og meldte succes - og appen blev ved
        // med at koere repoets kopi. Opdateringen aendrede ingenting, og
        // versionen stod tom, fordi manifestet laa ved siden af en anden exe
        // end den, der blev brugt.
        //
        // Den, brugeren SELV har hentet, vinder nu. En knap, der siger den
        // opdaterede noget, skal have opdateret dét, der koerer.

        // 1. Hentet af appen selv.
        yield return EngineDirectory;

        // 2. Repoets build. Stadig med, saa en udviklingsmaskine uden hentet
        //    motor virker ud af boksen.
        yield return Path.Combine("C:", "NoteApp", "tools", "whisper");

        // 3. Ved siden af den installerede exe.
        yield return Path.Combine(AppContext.BaseDirectory, "whisper");
    }

    private static string? FindModel(string? preferredId)
    {
        // ÉT STED. Se ModelDirectory for, hvorfor der ikke længere ledes
        // ved siden af exe'en eller i kodelageret.
        var mapper = new[] { ModelDirectory };

        // Er der ønsket en bestemt model, gælder kun den. Ellers tages den
        // bedste, der findes — rækkefølgen i Models er kvalitetsrækkefølgen.
        var ønskede = preferredId is null
            ? Models
            : Models.Where(m => m.Id.Equals(preferredId, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var model in ønskede)
        foreach (var mappe in mapper)
        {
            var sti = Path.Combine(mappe, model.FileName);
            if (File.Exists(sti) && new FileInfo(sti).Length > 50_000_000) return sti;
        }

        return null;
    }

    /// <summary>Modeller, der allerede ligger på disken.</summary>
    public static IEnumerable<WhisperModel> Installed()
    {
        foreach (var m in Models)
        {
            var sti = Path.Combine(ModelDirectory, m.FileName);
            if (File.Exists(sti)) yield return m;
        }
    }

    public static string ModelDestination(WhisperModel model) =>
        Path.Combine(ModelDirectory, model.FileName);

    /// <summary>Stilhedsmodellen — Silero, under 1 MB. Ligger sammen med de andre.</summary>
    public const string VadFilnavn = "ggml-silero-v5.1.2.bin";

    public const long VadStoerrelse = 885_098L;

    public const string VadKilde =
        "https://huggingface.co/ggml-org/whisper-vad/resolve/main/ggml-silero-v5.1.2.bin";

    /// <summary>
    /// Stien til stilhedsmodellen — null hvis den ikke er hentet.
    ///
    /// HVAD DEN LØSER
    ///
    /// Whisper er trænet på undertekster. Får den tredive sekunder uden tale,
    /// finder den ikke ingenting — den finder det, der plejer at stå, hvor der
    /// ikke bliver sagt noget: en underteksterkredit. Målt 25-08-2026 på et
    /// møde på 32 minutter kom der to af slagsen, «Danske tekster af Jesper
    /// Buhl Scandinavian Text Service 2018» og «Danske tekster af Nicolai
    /// Winther», plus 66 segmenter, der kun var «Ja».
    ///
    /// Ingen af delene blev sagt. De står i transskriptionen som alt andet, og
    /// derfra går de videre til opsummeringen som noget, der ligner indhold.
    ///
    /// AT DELE LYDEN OP HJÆLPER IKKE. Efterprøvet: den samme lyd i syv
    /// uafhængige bidder af fem minutter gav NØJAGTIG lige så mange
    /// opdigtninger — to — og den ene på samme absolutte sted. Det er ikke
    /// længden, der gør det; whisper.cpp arbejder i forvejen i 30-sekunders
    /// vinduer, og med -mc 0 bæres der intet med videre. Det er vinduer UDEN
    /// TALE, og de ligger spredt: de tætteste i den her optagelse lå mellem
    /// 2 og 8 minutter, altså i begyndelsen.
    ///
    /// HVAD DET KOSTER, OG HVAD DET GIVER
    ///
    /// Målt på samme lyd, samme model, samme øvrige argumenter:
    ///
    ///   uden stilhedsmodel   7 min 27 s   2 opdigtninger   66 tomme «Ja»
    ///   med, 200 ms luft     1 min 57 s   0                0
    ///
    /// Næsten fire gange hurtigere, fordi stilhed slet ikke sendes til
    /// modellen. Antallet af negationer var det samme (19 mod 20), så der
    /// forsvinder ikke betydning — og det var dét, der skulle efterprøves,
    /// før den kunne slås til.
    /// </summary>
    public static string? VadModel()
    {
        var sti = Path.Combine(ModelDirectory, VadFilnavn);
        return File.Exists(sti) && new FileInfo(sti).Length > 100_000 ? sti : null;
    }

    /// <summary>
    /// Henter stilhedsmodellen, hvis den mangler. Gør intet, hvis den er der.
    /// </summary>
    /// <remarks>
    /// DEN SPØRGES DER IKKE OM, og det er med vilje.
    ///
    /// De store modeller er et valg: de fylder gigabyte, og forskellen mellem
    /// dem er noget, man skal kunne tage stilling til. Den her fylder 885 KB
    /// og har ikke noget alternativ — den er en del af motoren, ikke en smag.
    /// Et spørgsmål om den ville være et spørgsmål, ingen har forudsætning for
    /// at svare på.
    ///
    /// DEN MÅ ALDRIG STÅ I VEJEN. Går hentningen galt — der er ikke net, eller
    /// filen er flyttet — sker der ingenting, og transskriptionen kører som
    /// før. Se Transcriber: mangler modellen, udelades tilvalget.
    ///
    /// Halvt hentede filer skrives til .delvis og flyttes først på plads, når
    /// størrelsen passer. Ellers ville et afbrudt net efterlade en fil, der
    /// ligner en model og ikke er det — og så ville motoren fejle ved hver
    /// transskription, indtil nogen slettede den i hånden.
    /// </remarks>
    public static async Task HentVadAsync(CancellationToken ct = default)
    {
        if (VadModel() is not null) return;

        var maal = Path.Combine(ModelDirectory, VadFilnavn);
        var delvis = maal + ".delvis";

        try
        {
            Directory.CreateDirectory(ModelDirectory);

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var data = await http.GetByteArrayAsync(VadKilde, ct);

            if (data.Length != VadStoerrelse) return;

            await File.WriteAllBytesAsync(delvis, data, ct);
            File.Move(delvis, maal, overwrite: true);
        }
        catch (Exception)
        {
            // I stilhed. Uden modellen koeres der som foer.
            try { if (File.Exists(delvis)) File.Delete(delvis); } catch (Exception) { }
        }
    }
}
