using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

public sealed record CaptureDevice(int SdlId, string Name);

/// <summary>
/// Live-lytning under oplæsningen: kører whisper-stream ved siden af
/// optagelsen og melder, hvad den hører.
///
/// Den bruges KUN til at følge med i, hvor langt man er nået i en tekst,
/// appen kender i forvejen. Den rigtige transskription laves bagefter på den
/// optagede fil med den store model — det her er en hjælper, ikke en kilde.
/// Derfor er kvalitetskravet også lavt: den skal genkende nok ord til et
/// match, ikke skrive et referat.
///
/// Lyden forlader ikke maskinen. whisper-stream kører lokalt på samme model,
/// som ligger på disken.
/// </summary>
public sealed class LiveListener : IDisposable
{
    private readonly string _streamExe;
    private Process? _proc;

    /// <summary>
    /// De sidste linjer fra stderr. De blev kasseret i første udgave, og da
    /// lytningen så døde ved opstart, stod der stadig «lytter med small» på
    /// skærmen — resten af oplæsningen. Grunden til at den døde stod i den
    /// tekst, der blev smidt væk.
    /// </summary>
    private readonly Queue<string> _sidsteFejllinjer = new();

    /// <summary>Sat, når vi selv stopper — så et exit ikke meldes som en fejl.</summary>
    private bool _stopperSelv;

    private bool _harHørtNoget;

    public LiveListener(string streamExe) => _streamExe = streamExe;

    /// <summary>Rejses for hver linje tekst, lytningen producerer.</summary>
    public event Action<string>? Heard;

    /// <summary>Rejses hvis lytningen ikke kunne startes eller døde undervejs.</summary>
    public event Action<string>? Failed;

    public bool IsRunning => _proc is { HasExited: false };

    /// <summary>
    /// Finder whisper-stream ved siden af whisper-cli. Er den der ikke, kan
    /// der ikke følges med — og det skal siges, ikke skjules.
    /// </summary>
    public static string? FindStreamExe(string? whisperCli)
    {
        if (whisperCli is null) return null;
        var kandidat = Path.Combine(Path.GetDirectoryName(whisperCli)!, "whisper-stream.exe");
        return File.Exists(kandidat) ? kandidat : null;
    }

    /// <summary>
    /// Lister SDL's optageenheder. whisper-stream skriver listen FØR den
    /// indlæser modellen, så et bevidst forkert modelnavn giver os listen på
    /// under et sekund i stedet for at vente på 2,9 GB.
    /// </summary>
    public static IReadOnlyList<CaptureDevice> ListCaptureDevices(string streamExe)
    {
        var liste = new List<CaptureDevice>();

        try
        {
            var psi = new ProcessStartInfo(streamExe)
            {
                Arguments = "-m __findes_ikke__.bin",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return liste;

            var moenster = new Regex(@"Capture device #(?<id>\d+):\s*'(?<navn>[^']+)'");

            var opsamlet = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(8000)) { try { p.Kill(true); } catch { } }

            foreach (Match m in moenster.Matches(opsamlet.Result))
                liste.Add(new CaptureDevice(int.Parse(m.Groups["id"].Value), m.Groups["navn"].Value));
        }
        catch (Exception)
        {
            // Kan listen ikke hentes, koeres der paa standardenheden. Det er
            // en daarligere oplevelse, ikke en fejl der skal vaelte noget.
        }

        return liste;
    }

    /// <summary>
    /// Finder den SDL-enhed, der bedst svarer til et Windows-enhedsnavn.
    /// SDL og Windows skriver navnene lidt forskelligt, så der sammenlignes
    /// på de første ord frem for på hele strengen.
    /// </summary>
    public static int MatchDevice(IReadOnlyList<CaptureDevice> devices, string? windowsName)
    {
        if (devices.Count == 0 || string.IsNullOrWhiteSpace(windowsName)) return -1;

        var noegle = ScriptFollower.Normalize(windowsName);

        foreach (var d in devices)
        {
            if (ScriptFollower.Normalize(d.Name) == noegle) return d.SdlId;
        }

        // Delvist match: fx "Mikrofon (Jabra SPEAK 510 USB)" mod
        // "Mikrofon (Jabra SPEAK 510 USB) - 2".
        foreach (var d in devices)
        {
            var dn = ScriptFollower.Normalize(d.Name);
            if (dn.StartsWith(noegle, StringComparison.Ordinal) ||
                noegle.StartsWith(dn, StringComparison.Ordinal)) return d.SdlId;
        }

        return -1;
    }

    /// <summary>
    /// Starter lytningen. <paramref name="captureId"/> er SDL-indekset;
    /// -1 betyder Windows' standardenhed.
    /// </summary>
    public void Start(string modelPath, int captureId = -1, string language = "da")
    {
        Stop();

        // step/length er sat lavere end whisper-streams standard: vi skal
        // reagere paa, at et afsnit er slut, ikke skrive en flydende tekst.
        // 2 sekunders skridt giver et svar cirka hvert andet sekund.
        var psi = new ProcessStartInfo(_streamExe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(modelPath);
        psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(language);
        psi.ArgumentList.Add("--step"); psi.ArgumentList.Add("2000");
        psi.ArgumentList.Add("--length"); psi.ArgumentList.Add("6000");
        psi.ArgumentList.Add("-t"); psi.ArgumentList.Add("4");
        if (captureId >= 0) { psi.ArgumentList.Add("-c"); psi.ArgumentList.Add(captureId.ToString()); }

        _stopperSelv = false;
        _harHørtNoget = false;
        _sidsteFejllinjer.Clear();

        try
        {
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _proc.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                _harHørtNoget = true;
                Heard?.Invoke(e.Data);
            };

            // stderr er fremdrift og modelindlaesning, ikke tekst — men det er
            // ogsaa dér, en fejl staar. Linjerne gemmes, saa de kan vises, hvis
            // processen doer, frem for at blive kasseret.
            _proc.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                lock (_sidsteFejllinjer)
                {
                    _sidsteFejllinjer.Enqueue(e.Data.Trim());
                    while (_sidsteFejllinjer.Count > 8) _sidsteFejllinjer.Dequeue();
                }
            };

            // Doer processen, mens vi tror, vi lytter, SKAL det siges. Uden
            // dette blev der ikke skiftet afsnit i tyve minutter, mens skaermen
            // paastod, at der blev lyttet med.
            _proc.Exited += (_, _) =>
            {
                if (_stopperSelv) return;
                Failed?.Invoke(Fejlaarsag());
            };

            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _proc = null;
            Failed?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Hvorfor lytningen holdt op. Det, brugeren skal se, er hvad der kan
    /// gøres ved det — ikke en stakspor.
    /// </summary>
    private string Fejlaarsag()
    {
        string[] linjer;
        lock (_sidsteFejllinjer) linjer = _sidsteFejllinjer.ToArray();

        var samlet = string.Join(" ", linjer);

        // Den hyppigste aarsag paa et 6 GB-kort: en sprogmodel eller en
        // transskription har taget hukommelsen. Det er ikke en fejl i appen,
        // og raadet er et andet end ved alle andre fejl.
        if (Regex.IsMatch(samlet, @"out of memory|CUDA error|failed to allocate|cudaMalloc",
                RegexOptions.IgnoreCase))
            return "der var ikke plads på grafikkortet — kører der en transskription eller et udkast samtidig?";

        if (Regex.IsMatch(samlet, @"failed to open|no such device|SDL", RegexOptions.IgnoreCase))
            return "mikrofonen kunne ikke åbnes til medlytning";

        if (!_harHørtNoget)
            return "lytningen stoppede, før den nåede at høre noget";

        var sidste = linjer.LastOrDefault(l => l.Length > 0);
        return sidste is null ? "lytningen stoppede" : $"lytningen stoppede: {sidste}";
    }

    public void Stop()
    {
        if (_proc is null) return;

        _stopperSelv = true;   // et exit herfra er ikke en fejl

        try { if (!_proc.HasExited) _proc.Kill(entireProcessTree: true); }
        catch (Exception) { /* den kan allerede vaere doed */ }

        try { _proc.Dispose(); } catch (Exception) { }
        _proc = null;
    }

    public void Dispose() => Stop();
}
