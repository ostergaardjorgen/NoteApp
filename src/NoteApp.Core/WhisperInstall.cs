using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>Hvilke sprog en model overhovedet kan. Ikke en anbefaling — en grænse.</summary>
public enum ModelLanguages
{
    /// <summary>Kan dansk. Alle flersprogede modeller kan også engelsk.</summary>
    Multilingual,

    /// <summary>KUN engelsk. Vælges den til et dansk møde, kommer der volapyk ud.</summary>
    EnglishOnly,

    /// <summary>
    /// KUN dansk. En model, der er finjusteret på ét sprog, kan i praksis
    /// kun det ene — resten af Whispers sprog er trænet væk igen.
    /// </summary>
    Danish
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

    public bool SupportsDanish => Languages is ModelLanguages.Multilingual or ModelLanguages.Danish;
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

    public static string ModelDirectory => Path.Combine(Root, "modeller");

    public static string EngineDirectory => Path.Combine(Root, "whisper");

    /// <summary>
    /// Modellerne, appen kan hente.
    ///
    /// HER LAA SEKS. NU LIGGER DER TO.
    ///
    /// Katalogget havde large-v3, large-v3-turbo, medium, small, small.en og
    /// base.en med fordele og ulemper ved hver. Det saa ud som et oplyst valg
    /// og var en faelde: alle de smaa er maerkbart daarligere paa dansk, og de
    /// to engelske kan slet ikke dansk — de giver volapyk frem for en fejl.
    /// Et valg, hvor hvert alternativ goer loesningen ringere, er ikke et
    /// valg. Fjernet 18-08-2026.
    ///
    /// Tilbage staar de to, der er vaerd at have:
    ///
    ///   · Roest v3 — en Whisper large-v3, der er finjusteret paa dansk tale
    ///     af CoRal-projektet. Standardvalget.
    ///   · large-v3-turbo — OpenAI's egen, flersproget. Reserven, naar moedet
    ///     ikke holdes paa dansk.
    ///
    /// Stoerrelserne er de faktiske filstoerrelser, maalt med et HEAD-kald.
    /// Tallet er ikke pynt: Downloader bruger det til at afgoere, om en fil,
    /// der allerede ligger der, er hel. Er tallet forkert, hentes modellen
    /// igen hver eneste gang.
    /// </summary>
    public static readonly IReadOnlyList<WhisperModel> Models = new[]
    {
        new WhisperModel(
            "roest-v3", "roest-v3-q8_0.bin", 1_656_538_283L,
            "https://huggingface.co/alfanova/roest-v3-whisper-ggml/resolve/main/roest-v3-q8_0.bin",
            ModelLanguages.Danish,
            "Whisper large-v3, finjusteret paa dansk tale. Standardvalget.",
            "Trænet på CoRal-v3: dansk samtale og oplæsning på tværs af aldre, køn og dialekter. Bygger på den samme large-v3, som ellers ville være valget — men med dansk oveni.",
            "Kun dansk. Holdes mødet på engelsk, skal der skiftes til large-v3-turbo. Licensen er OpenRAIL-M med brugsbegrænsninger, der skal følge med videre."),

        new WhisperModel(
            "large-v3-turbo", "ggml-large-v3-turbo.bin", 1_624_555_275L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin",
            ModelLanguages.Multilingual,
            "OpenAI's egen, flersproget. Til møder, der ikke holdes på dansk.",
            "Kan alle de sprog, Whisper kan, og finder selv ud af hvilket. Næsten large-v3's kvalitet på omtrent det halve af tiden.",
            "Ringere på dansk end Roest, som er trænet netop på det. Vælg den, når mødet ikke er dansk.")
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
        // 1. Repoets CUDA-build vinder, hvis den er der — GPU slår CPU med
        //    omkring en faktor ti, og på udviklingsmaskinen ligger den der.
        yield return Path.Combine("C:", "NoteApp", "tools", "whisper");

        // 2. Ved siden af den installerede exe.
        yield return Path.Combine(AppContext.BaseDirectory, "whisper");

        // 3. Hentet af appen selv.
        yield return EngineDirectory;
    }

    private static string? FindModel(string? preferredId)
    {
        var mapper = new[]
        {
            ModelDirectory,
            Path.Combine("C:", "NoteApp", "models"),
            Path.Combine(AppContext.BaseDirectory, "models")
        };

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
            var iRepo = Path.Combine("C:", "NoteApp", "models", m.FileName);
            if (File.Exists(sti) || File.Exists(iRepo)) yield return m;
        }
    }

    public static string ModelDestination(WhisperModel model) =>
        Path.Combine(ModelDirectory, model.FileName);
}
