using System.IO.Compression;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

public enum EngineKind
{
    /// <summary>Ren CPU. Virker overalt, langsomst.</summary>
    Cpu,

    /// <summary>CPU med BLAS. Mærkbart hurtigere end ren CPU, stadig ingen GPU-krav.</summary>
    Blas,

    /// <summary>NVIDIA-GPU. Ti gange hurtigere, men kun på maskiner med et NVIDIA-kort.</summary>
    Cuda
}

public sealed record EngineBuild(
    string FileName,
    long Bytes,
    string Url,
    EngineKind Kind,
    string Summary,
    string Pros,
    string Cons)
{
    public string SizeText => Bytes >= 1_000_000_000
        ? $"{Bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB"
        : $"{Bytes / 1024.0 / 1024.0:0} MB";

    public bool RequiresNvidia => Kind == EngineKind.Cuda;
}

public sealed record EngineRelease(string Version, IReadOnlyList<EngineBuild> Builds);

/// <summary>
/// Henter og udpakker selve Whisper-motoren.
///
/// Uden den kan appen ikke transskribere noget som helst på en frisk maskine,
/// og en installationspakke, der efterlader brugeren med en app der ikke
/// virker, er ikke en installation.
///
/// Størrelser og adresser hentes fra GitHubs udgivelsesliste i stedet for at
/// stå hardkodet. Det er ikke pænhed: hardkodede størrelser på modellerne var
/// gættet forkert to steder, og en forkert størrelse får hentningen til at
/// starte forfra hver gang. Det, serveren selv siger, kan ikke blive forældet.
/// </summary>
public static class EngineInstaller
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest";

    /// <summary>
    /// Er der et NVIDIA-kort i maskinen? Afgør, om CUDA-udgaven overhovedet
    /// giver mening at tilbyde — den fylder 80 gange mere end CPU-udgaven og
    /// er ubrugelig uden kortet.
    /// </summary>
    public static bool HasNvidiaGpu()
    {
        try
        {
            using var søger = new System.Management.ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");

            foreach (var enhed in søger.Get())
            {
                var navn = enhed["Name"]?.ToString() ?? "";
                if (navn.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch (Exception)
        {
            // Kan det ikke afgoeres, antages ingen GPU. Saa faar brugeren
            // CPU-udgaven anbefalet, hvilket altid virker.
        }

        return false;
    }

    /// <summary>
    /// Spørger GitHub, hvilke Windows-udgaver der findes i den nyeste udgivelse.
    /// Beskrivelserne er vores egne; navn, størrelse og adresse er serverens.
    /// </summary>
    public static async Task<EngineRelease> FetchLatestAsync(CancellationToken ct = default)
    {
        var json = await new Downloader().FetchTextAsync(LatestReleaseUrl, ct);

        var version = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"(?<tag>[^\"]+)\"").Groups["tag"].Value;
        if (string.IsNullOrWhiteSpace(version)) version = "ukendt";

        var builds = new List<EngineBuild>();

        // Der parses med ét udtryk frem for en model af hele GitHub-svaret:
        // jo mindre af det svar vi binder os til, jo mindre kan gaa i stykker,
        // naar de aendrer noget andet.
        var aktiver = Regex.Matches(json,
            "\"name\"\\s*:\\s*\"(?<navn>[^\"]+\\.zip)\".*?\"size\"\\s*:\\s*(?<stoerrelse>\\d+).*?\"browser_download_url\"\\s*:\\s*\"(?<url>[^\"]+)\"",
            RegexOptions.Singleline);

        foreach (Match m in aktiver)
        {
            var navn = m.Groups["navn"].Value;
            var bytes = long.Parse(m.Groups["stoerrelse"].Value);
            var url = m.Groups["url"].Value;

            var build = Beskriv(navn, bytes, url);
            if (build is not null) builds.Add(build);
        }

        // Bedste foerst: CUDA, saa BLAS, saa ren CPU.
        builds.Sort((a, b) => a.Kind.CompareTo(b.Kind) * -1);

        return new EngineRelease(version, builds);
    }

    /// <summary>
    /// Fordele og ulemper ved hver udgave. De skal stå ved valget, ikke i en
    /// vejledning — forskellen er en faktor ti i hastighed og en faktor firs
    /// i størrelse, og det kan ingen gætte.
    /// </summary>
    private static EngineBuild? Beskriv(string navn, long bytes, string url)
    {
        // Kun 64-bit Windows. 32-bit findes stadig i udgivelsen, men appen er
        // x64, og et 32-bit whisper ville ikke kunne bruge maskinens hukommelse.
        if (!navn.Contains("x64", StringComparison.OrdinalIgnoreCase)) return null;

        if (navn.Contains("cublas-12", StringComparison.OrdinalIgnoreCase))
            return new EngineBuild(navn, bytes, url, EngineKind.Cuda,
                "Til maskiner med et NVIDIA-kort. Klart det hurtigste.",
                "Omkring ti gange hurtigere end CPU. Et 90-minutters møde bliver til minutter i stedet for timer.",
                "Fylder mest af alle udgaver og kræver et NVIDIA-kort med en nyere driver. Uden kortet starter den slet ikke.");

        if (navn.Contains("cublas-11", StringComparison.OrdinalIgnoreCase))
            return new EngineBuild(navn, bytes, url, EngineKind.Cuda,
                "NVIDIA-udgave til ældre drivere (CUDA 11).",
                "Samme hastighed som CUDA 12 og under halv størrelse. Vælg den, hvis driveren er for gammel til CUDA 12.",
                "Kræver stadig et NVIDIA-kort. Er driveren ny nok, er der ingen grund til at vælge denne frem for CUDA 12.");

        if (navn.Contains("blas", StringComparison.OrdinalIgnoreCase))
            return new EngineBuild(navn, bytes, url, EngineKind.Blas,
                "Uden GPU, men optimeret. Anbefales på en pc uden NVIDIA-kort.",
                "Mærkbart hurtigere end den rene CPU-udgave og fylder stadig kun lidt. Virker på enhver maskine.",
                "Langsommere end GPU. Regn med, at et langt møde tager længere tid end mødet selv — planlæg det som natjob.");

        if (navn.Contains("whisper-bin", StringComparison.OrdinalIgnoreCase))
            return new EngineBuild(navn, bytes, url, EngineKind.Cpu,
                "Den mindste udgave. Ren CPU, ingen optimering.",
                "Under 10 MB og hentet på få sekunder. Virker overalt.",
                "Langsomst af alle. Vælg BLAS-udgaven i stedet, med mindre pladsen er helt afgørende.");

        return null;
    }

    /// <summary>
    /// Anbefaler den udgave, der passer til maskinen: CUDA hvis der er et
    /// NVIDIA-kort, ellers BLAS.
    /// </summary>
    public static EngineBuild? Recommend(EngineRelease release)
    {
        var harNvidia = HasNvidiaGpu();

        if (harNvidia)
        {
            var cuda = release.Builds.FirstOrDefault(b => b.Kind == EngineKind.Cuda && b.FileName.Contains("12."));
            if (cuda is not null) return cuda;
        }

        return release.Builds.FirstOrDefault(b => b.Kind == EngineKind.Blas)
            ?? release.Builds.FirstOrDefault(b => b.Kind == EngineKind.Cpu);
    }

    /// <summary>
    /// Henter og udpakker en udgave. Skriver til sidst et manifest med
    /// versionen — det er det ENESTE sted, motorens version kan komme fra,
    /// fordi whisper.cpp ikke stempler sine filer.
    /// </summary>
    public static async Task InstallAsync(
        EngineBuild build,
        string version,
        IProgress<DownloadProgress>? downloadProgress = null,
        IProgress<string>? status = null,
        CancellationToken ct = default)
    {
        var mappe = WhisperInstall.EngineDirectory;
        Directory.CreateDirectory(mappe);

        var zip = Path.Combine(mappe, build.FileName);

        status?.Report($"Henter {build.FileName} ({build.SizeText}) …");
        await new Downloader().DownloadAsync(build.Url, zip, build.Bytes, downloadProgress, ct);

        status?.Report("Pakker ud …");

        // Udpakning sker til en frisk undermappe. Ligger der en tidligere
        // udgave, skal den vaek foerst — ellers kan gamle DLL'er blive
        // liggende og blande sig med de nye.
        var udpakket = Path.Combine(mappe, "bin");
        if (Directory.Exists(udpakket))
        {
            try { Directory.Delete(udpakket, recursive: true); }
            catch (IOException) { /* en fil i brug — udpakningen overskriver */ }
        }
        Directory.CreateDirectory(udpakket);

        await Task.Run(() => ZipFile.ExtractToDirectory(zip, udpakket, overwriteFiles: true), ct);

        // Zip-filen fylder lige saa meget som det udpakkede. Den er brugt nu.
        try { File.Delete(zip); } catch (IOException) { }

        var cli = Directory.EnumerateFiles(udpakket, "whisper-cli.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (cli is null)
            throw new InvalidOperationException(
                $"Udpakningen indeholdt ingen whisper-cli.exe. Arkivet {build.FileName} ser ikke ud som forventet.");

        new EngineManifest(version, build.Url, DateTimeOffset.Now, null).Save(mappe);

        status?.Report($"Motoren er klar: {Path.GetFileName(cli)} ({version})");
    }
}
