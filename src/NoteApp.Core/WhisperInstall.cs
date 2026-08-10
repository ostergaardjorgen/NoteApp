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

public sealed record InstallState(string? WhisperCli, string? ModelPath, bool HasCuda, string? EngineVersion)
{
    public bool IsComplete => WhisperCli is not null && ModelPath is not null;

    public string Engine => HasCuda ? "GPU (CUDA)" : "CPU";

    public string? ModelFileName => ModelPath is null ? null : Path.GetFileName(ModelPath);
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
    /// Modellerne, appen kan hente. Fordele og ulemper står ved hver enkelt,
    /// fordi valget ikke kan træffes fornuftigt uden dem: den mindste model
    /// er ti gange hurtigere OG mærkbart dårligere, og hvad der er det rigtige
    /// afhænger af, om man holder møder på dansk eller engelsk, og om der er
    /// et NVIDIA-kort i maskinen.
    ///
    /// Størrelserne er de faktiske filstørrelser fra Hugging Face, målt med
    /// et HEAD-kald 10. august 2026. De vises, FØR brugeren siger ja til at
    /// hente — ikke bagefter.
    ///
    /// Tallet er ikke pynt: Downloader bruger det til at afgøre, om en fil,
    /// der allerede ligger der, er hel. Er tallet forkert, hentes modellen
    /// igen hver eneste gang. To af dem var gættet forkert i første udgave,
    /// og det viste sig kun, fordi hentningen blev afprøvet.
    /// </summary>
    public static readonly IReadOnlyList<WhisperModel> Models = new[]
    {
        new WhisperModel(
            "large-v3", "ggml-large-v3.bin", 3_095_033_483L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin",
            ModelLanguages.Multilingual,
            "Bedst på dansk. Standardvalget, hvis maskinen har et NVIDIA-kort.",
            "Klart bedst til danske navne, fagtermer og negationer. Den eneste, der er god nok til at sende et referat videre uden at læse lyden efter.",
            "Fylder 2,9 GB og kræver knap 3,1 GB VRAM. På ren CPU er den for langsom til daglig brug — regn med flere timer for et langt møde."),

        new WhisperModel(
            "large-v3-turbo", "ggml-large-v3-turbo.bin", 1_624_555_275L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin",
            ModelLanguages.Multilingual,
            "Næsten large-v3's kvalitet på omtrent det halve af tiden.",
            "Markant hurtigere end large-v3 og halvt så stor. Bedste kompromis, hvis du venter på transskriptionen i stedet for at lade den køre om natten.",
            "En smule ringere end large-v3 på svær lyd — flere talere i munden på hinanden, kraftig dialekt, dårlig mikrofon."),

        new WhisperModel(
            "medium", "ggml-medium.bin", 1_533_763_059L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin",
            ModelLanguages.Multilingual,
            "Mellemvejen. Brugbar på dansk, kører på en pc uden GPU.",
            "Halv størrelse af large-v3 og mærkbart hurtigere på CPU. God nok til at forstå, hvad der blev sagt.",
            "Taber navne og fagtermer, som large-v3 fanger. Ordlisten betyder mere her, ikke mindre."),

        new WhisperModel(
            "small", "ggml-small.bin", 487_601_967L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            ModelLanguages.Multilingual,
            "Til en pc uden GPU og uden tålmodighed.",
            "Under 500 MB og hurtig selv på CPU. Fin til at finde ud af, hvad et møde handlede om.",
            "Mærkbart dårligere dansk. Negationer forsvinder, og det er den fejl, der vender betydningen om."),

        new WhisperModel(
            "small.en", "ggml-small.en.bin", 487_614_201L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin",
            ModelLanguages.EnglishOnly,
            "Kun engelsk. Bedre engelsk end small til samme størrelse.",
            "Al kapacitet er brugt på ét sprog, så den slår den flersprogede small klart på engelske møder. Lille og hurtig.",
            "KAN IKKE DANSK. Vælges den til et dansk møde, kommer der volapyk ud — ikke en fejlmeddelelse."),

        new WhisperModel(
            "base.en", "ggml-base.en.bin", 147_964_211L,
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
            ModelLanguages.EnglishOnly,
            "Kun engelsk, letvægt. 148 MB.",
            "Henter på sekunder og kører på hvad som helst. Nok til engelske møder, hvor man bare skal kunne søge bagefter.",
            "KAN IKKE DANSK. Og selv på engelsk taber den navne og tal, som de større modeller rammer.")
    };

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

        return new InstallState(cli, model, cuda, version);
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
