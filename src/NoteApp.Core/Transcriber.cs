using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

public sealed record TranscriptionRequest(
    string WavPath,
    string ModelPath,
    string OutputBase,
    /// <summary>
    /// Sprogkode, eller "auto" for at lade Whisper finde det selv.
    ///
    /// "auto" er standard, fordi møder ikke altid holdes på dansk. Låses det
    /// til dansk, bliver et engelsk møde transskriberet som var det dansk, og
    /// resultatet er volapyk frem for en fejlmeddelelse — den værste slags
    /// fejl, fordi den ligner et resultat.
    /// </summary>
    string Language = "auto",
    string? Prompt = null,
    bool ForceCpu = false);

public sealed record TranscriptionResult(
    string TextPath,
    string JsonPath,
    string LogPath,
    double AudioSeconds,
    double ElapsedSeconds,
    string EngineId,
    /// <summary>
    /// Det sprog, Whisper landede på. Enten det, brugeren valgte, eller det,
    /// modellen selv fandt. Skrives med i referatet, så skabelonerne kan
    /// forholde sig til sproget frem for at gætte.
    /// </summary>
    string DetectedLanguage = "da",
    /// <summary>
    /// Hvor sikker detekteringen var (0-1). Null, når sproget var valgt på
    /// forhånd og der altså ikke blev detekteret noget.
    ///
    /// Tallet betyder noget i praksis: dansk, norsk og svensk ligner hinanden
    /// nok til, at en detektering kan lande forkert. Er den usikker, skal det
    /// kunne ses frem for at blive skjult bag et flag.
    /// </summary>
    double? LanguageProbability = null)
{
    /// <summary>
    /// Realtidsfaktoren: transskriptionstid delt med lydens længde. Over 1,0
    /// betyder, at transskription skal planlægges som natjob frem for noget,
    /// man venter på — og det ændrer, hvordan resten af appen skal se ud.
    /// </summary>
    public double RealTimeFactor => AudioSeconds <= 0 ? 0 : ElapsedSeconds / AudioSeconds;

    public string Text => File.Exists(TextPath) ? File.ReadAllText(TextPath, Encoding.UTF8) : "";
}

public sealed record TranscriptionProgress(double Percent, string Message);

/// <summary>
/// Kører whisper.cpp og måler, hvor lang tid det tog.
///
/// Dette er den kode, der afløser PowerShell-scriptet: appen skal kunne det
/// hele selv, ellers kan den ikke installeres af andre end den, der har
/// repoet. Flagene er de samme, som scriptet allerede har bevist virker —
/// særligt at det er whisper-cli.exe og ikke main.exe, og at al fremdrift
/// kommer på stderr.
/// </summary>
public sealed class Transcriber
{
    private readonly string _whisperCli;

    public Transcriber(string whisperCli) => _whisperCli = whisperCli;

    /// <summary>
    /// Længden af en 16 kHz mono PCM16 WAV, læst af headeren. Bruges til RTF,
    /// og skal derfor kunne stole på: er tallet forkert, er hele målingen det.
    /// </summary>
    public static double WavSeconds(string path)
    {
        using var fs = File.OpenRead(path);
        var h = new byte[64];
        if (fs.Read(h, 0, 64) < 44) return 0;

        var bytesPerSecond = BitConverter.ToUInt32(h, 28);
        return bytesPerSecond == 0 ? 0 : Math.Round((fs.Length - 44) / (double)bytesPerSecond, 1);
    }

    public async Task<TranscriptionResult> RunAsync(
        TranscriptionRequest request,
        IProgress<TranscriptionProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(request.WavPath)) throw new FileNotFoundException("Lydfilen findes ikke", request.WavPath);
        if (!File.Exists(request.ModelPath)) throw new FileNotFoundException("Modellen findes ikke", request.ModelPath);

        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputBase)!);

        var lyd = WavSeconds(request.WavPath);

        var args = new List<string>
        {
            "-m", request.ModelPath,
            "-f", request.WavPath,
            "-l", request.Language,
            "-otxt",
            "-oj",
            "-of", request.OutputBase,
            "-pp"                       // fremdrift, saa UI'et kan vise noget
        };

        if (!string.IsNullOrWhiteSpace(request.Prompt)) { args.Add("--prompt"); args.Add(request.Prompt); }
        if (request.ForceCpu) args.Add("-ng");

        var psi = new ProcessStartInfo(_whisperCli)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var log = new StringBuilder();
        var ur = Stopwatch.StartNew();

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        // whisper.cpp skriver AL fremdrift til stderr — ogsaa det, der ikke er
        // fejl. Begge stroemme samles i loggen, og fremdriften laeses ud af
        // stderr. Behandles stderr som "fejl", stopper alt ved foerste linje.
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            log.AppendLine(e.Data);

            var m = Regex.Match(e.Data, @"progress\s*=\s*(\d+)%");
            if (m.Success && progress is not null)
            {
                var pct = double.Parse(m.Groups[1].Value);
                progress.Report(new TranscriptionProgress(pct, $"Transskriberer … {pct:0}%"));
            }
            else if (e.Data.Contains("load time", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new TranscriptionProgress(100, "Samler resultatet"));
            }
        };

        progress?.Report(new TranscriptionProgress(0, "Indlæser modellen …"));

        p.Start();
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

        var logPath = request.OutputBase + ".log";
        await File.WriteAllTextAsync(logPath, log.ToString(), new UTF8Encoding(false), ct);

        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"whisper-cli afsluttede med kode {p.ExitCode}. Se loggen: {logPath}");

        var engine = BuildEngineId(request.ModelPath, log.ToString());
        var (sprog, sikkerhed) = ReadLanguage(log.ToString(), request.Language);

        return new TranscriptionResult(
            request.OutputBase + ".txt",
            request.OutputBase + ".json",
            logPath,
            lyd,
            Math.Round(ur.Elapsed.TotalSeconds, 1),
            engine,
            sprog,
            sikkerhed);
    }

    /// <summary>
    /// Hvilket sprog blev det, og hvor sikkert var det.
    ///
    /// Whisper skriver ved detektering en linje i stil med:
    ///   auto-detected language: da (p = 0.98342)
    ///
    /// VIGTIGT om rækkevidden: detekteringen sker ÉN gang, ud fra de første
    /// tredive sekunder af lyden. Skifter mødet sprog undervejs — og det gør
    /// et dansk møde med en engelsk gæst — opdager Whisper det ikke. Sproget
    /// her er altså mødets hovedsprog, ikke en markering pr. afsnit. Det skal
    /// stå, som det er; et felt, der lover mere end det holder, er værre end
    /// intet felt.
    /// </summary>
    private static (string Sprog, double? Sikkerhed) ReadLanguage(string log, string ønsket)
    {
        var m = Regex.Match(log,
            @"auto-detected language:\s*(?<kode>[a-z]{2,3})\s*(?:\(\s*p\s*=\s*(?<p>[\d.]+))?",
            RegexOptions.IgnoreCase);

        if (m.Success)
        {
            double? p = double.TryParse(m.Groups["p"].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
            return (m.Groups["kode"].Value.ToLowerInvariant(), p);
        }

        // Ingen detektering i loggen. Enten fordi sproget var valgt paa
        // forhaand, eller fordi Whisper skrev det anderledes end ventet. I
        // begge tilfaelde er det oenskede sprog det aerlige svar — men "auto"
        // er ikke et sprog, og maa ikke ende i et referat som om det var.
        return ønsket.Equals("auto", StringComparison.OrdinalIgnoreCase)
            ? ("ukendt", null)
            : (ønsket.ToLowerInvariant(), null);
    }

    /// <summary>Sprogkode til noget, der kan staa i et referat.</summary>
    public static string LanguageName(string kode) => kode.ToLowerInvariant() switch
    {
        "da" => "dansk",
        "en" => "engelsk",
        "no" or "nn" or "nb" => "norsk",
        "sv" => "svensk",
        "de" => "tysk",
        "nl" => "hollandsk",
        "fr" => "fransk",
        "es" => "spansk",
        "ukendt" => "ukendt",
        _ => kode
    };

    /// <summary>
    /// Proveniens: hvilken motor og model lavede denne tekst. Gemmes med
    /// rettelser, saa en senere Whisper-opdatering kan maales paa, om den
    /// faktisk hjalp — men bruges ALDRIG til at afgoere, om en rettelse maa
    /// anvendes. Se doc\laering-og-vedligehold.md.
    /// </summary>
    private static string BuildEngineId(string modelPath, string log)
    {
        var model = Path.GetFileNameWithoutExtension(modelPath).Replace("ggml-", "");
        var m = Regex.Match(log, @"whisper\.cpp\s+version\s*[:=]?\s*(v?[\d.]+)", RegexOptions.IgnoreCase);
        var version = m.Success ? m.Groups[1].Value : "ukendt";
        return $"whisper.cpp/{model}/{version}";
    }
}
