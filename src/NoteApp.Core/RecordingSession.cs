using System.Diagnostics;

namespace NoteApp.Core;

public sealed record LoopbackCheck(bool HasSignal, float Peak)
{
    public string Description => HasSignal
        ? $"OK (top {PeakMeter.ToDb(Peak):0.0} dBFS)"
        : "STILHED — Teams kører måske ikke endnu, eller lyden går til en anden enhed";
}

/// <summary>
/// Ét møde under optagelse: sporene, noterne, uret og metadata.
///
/// Klassen ejer rækkefølgen af tingene — hvornår meeting.json skrives,
/// hvornår segmenter samles, hvad der sker hvis samlingen fejler. Den
/// rækkefølge er ikke ligegyldig: meeting.json skrives FØR optagelsen
/// begynder, så et crash efterlader noget, genopretningen kan læse.
/// </summary>
public sealed class RecordingSession : IDisposable
{
    private readonly Stopwatch _clock = new();
    private readonly List<TrackRecorder> _tracks = new();

    private RecordingSession(string sessionDir, MeetingMetadata meta)
    {
        SessionDir = sessionDir;
        Meta = meta;
        Notebook = new MeetingNotebook(sessionDir, () => _clock.Elapsed.TotalSeconds);
    }

    public string SessionDir { get; }
    public MeetingMetadata Meta { get; }
    public MeetingNotebook Notebook { get; }

    public TrackRecorder? Microphone { get; private set; }
    public TrackRecorder? Loopback { get; private set; }

    public TimeSpan Elapsed => _clock.Elapsed;
    public bool IsRecording => _clock.IsRunning;

    public event Action<TrackIncident>? IncidentOccurred;

    /// <summary>
    /// Måler loopback-niveauet før optagelsen. Et onlinemøde med tavst
    /// loopback-spor er den fejl der koster et helt møde, og den kan kun
    /// fanges her.
    /// </summary>
    public static LoopbackCheck CheckLoopback(string renderDeviceId, TimeSpan? window = null)
    {
        var peak = AudioDevices.MeasureLoopbackPeak(renderDeviceId, window ?? TimeSpan.FromSeconds(3));
        return new LoopbackCheck(peak > AudioDevices.SilenceThreshold, peak);
    }

    public static RecordingSession Create(MeetingType type, string? title,
                                          DeviceInfo microphone, DeviceInfo? renderDevice)
    {
        // Ingen afspilningsenhed er ikke en fejl laengere: saa optages der kun
        // mikrofonen, og det er praecis, hvad et fysisk moede er.

        UserDataPaths.EnsureCreated();

        var startedAt = DateTimeOffset.Now;
        var dir = MeetingStore.CreateSessionDirectory(title, startedAt);

        var meta = new MeetingMetadata
        {
            Type = type,
            StartedAt = startedAt,
            Title = title,
            MicDeviceName = microphone.FriendlyName,
            LoopbackDeviceName = type is MeetingType.Online or MeetingType.Webinar
                ? renderDevice!.FriendlyName
                : null,

            // ============ ER DET TELEFONEN? ============
            //
            // Der spoerges HER og huskes ikke fra andetsteds. Telefonen er
            // lagt paa, laenge foer nogen kigger paa listen; svaret findes kun
            // i det oejeblik, der optages. Se Opkaldsprogrammer.IGang.
            Opkald = Opkaldsprogrammer.IGang(),
        };

        // Skriv metadata FØR optagelsen begynder. Crasher maskinen, er der
        // stadig noget genopretningen kan læse.
        MeetingStore.Save(dir, meta);

        var session = new RecordingSession(dir, meta);

        // ET WEBINAR OPTAGER IKKE DIN MIKROFON.
        //
        // Det er hele pointen med typen. Du lytter; du taler ikke. Uden dit
        // spor er der intet ekko at fjerne, ingen overlappende tale, og
        // optagelsen fylder og koster det halve at skrive ud.
        //
        // Det er samtidig det ENESTE, der skiller et webinar fra et onlinemøde
        // — lyden er den samme. Derfor er det et valg, brugeren træffer, og
        // ikke noget, der kan måles bagefter.
        if (type != MeetingType.Webinar)
        {
            session.Microphone = new TrackRecorder(TrackKind.Microphone, microphone.Id, dir,
                                                   () => session._clock.Elapsed.TotalSeconds);
            session._tracks.Add(session.Microphone);
        }

        // Højttalersporet tages med, hver gang der ER en afspilningsenhed —
        // ikke kun når nogen har sagt, at mødet er online.
        //
        // Grunden er, at valget ikke kan træffes rigtigt på forhånd. Et
        // onlinemøde, hvor ingen taler i det øjeblik, man trykker optag, ser
        // ud som et fysisk møde; vælger man forkert, mangler alle de andre
        // deltagere, og det opdages først bagefter.
        //
        // Er sporet tavst hele vejen igennem, bliver det slettet i Stop() —
        // dér findes hele optagelsen, og svaret kan måles i stedet for gættes.
        if (renderDevice is not null)
        {
            session.Loopback = new TrackRecorder(TrackKind.Loopback, renderDevice.Id, dir,
                                                 () => session._clock.Elapsed.TotalSeconds);
            session._tracks.Add(session.Loopback);
        }

        foreach (var t in session._tracks)
            t.IncidentOccurred += i => session.IncidentOccurred?.Invoke(i);

        return session;
    }

    private static int _ilive;

    /// <summary>Har DEN her session talt sig med? Se <see cref="NogenOptager"/>.</summary>
    private bool _talt;

    /// <summary>
    /// Optager appen lige nu? Bruges af de vagter, der ellers ville tage
    /// maskinen fra optagelsen.
    /// </summary>
    /// <remarks>
    /// TALLET OG IKKE ET FLAG. Der kan være to sessioner i gang på én gang —
    /// en lydprøve ved siden af et møde — og et flag ville blive slukket af
    /// den første, der stoppede, mens den anden stadig optog.
    ///
    /// Optagelsen er den ene ting, der aldrig kan tages om. Alt andet kan
    /// vente.
    /// </remarks>
    public static bool NogenOptager => Volatile.Read(ref _ilive) > 0;

    public void Start()
    {
        if (!_talt) { Interlocked.Increment(ref _ilive); _talt = true; }

        _clock.Start();
        foreach (var t in _tracks) t.Start();
    }

    public bool IsPaused { get; private set; }

    /// <summary>
    /// Holder pause. Uret standser sammen med lyden, så noternes tidsstempler
    /// bliver ved med at passe til det, der faktisk er optaget — ellers ville
    /// en note lande et forkert sted i transskriptionen efter hver pause.
    ///
    /// Der optages intet imens. Det er ikke en dæmpning: mikrofonen slippes.
    /// </summary>
    public void Pause()
    {
        if (IsPaused || !_clock.IsRunning) return;

        foreach (var t in _tracks) t.Pause();
        _clock.Stop();
        IsPaused = true;
    }

    public void Resume()
    {
        if (!IsPaused) return;

        _clock.Start();
        foreach (var t in _tracks) t.Resume();
        IsPaused = false;
    }

    /// <summary>
    /// Stopper og samler segmenterne. Fejler samlingen for ét spor, fortsætter
    /// vi med det andet — og segmenterne bliver liggende, så genopretningen
    /// kan tage over. Delvist tab slår totalt tab.
    /// </summary>
    public IReadOnlyDictionary<string, string> Stop()
    {
        foreach (var t in _tracks) t.Stop();
        _clock.Stop();

        var filer = new Dictionary<string, string>();

        foreach (var t in _tracks)
        {
            try
            {
                filer[t.Name] = t.Assemble(SessionDir);
                Meta.Tracks[t.Name] = Path.GetFileName(filer[t.Name]);
            }
            catch (Exception ex)
            {
                Meta.Tracks[t.Name] = $"IKKE SAMLET: {ex.Message} — segmenterne ligger stadig i segmenter\\{t.Name}";
            }
        }

        // Var der ingen i den anden ende, var det et fysisk møde — og så skal
        // et tomt spor ikke ligge og fylde. Det afgøres HER, hvor hele
        // optagelsen findes, ikke ved en stikprøve før den begyndte.
        //
        // Det er dét, der gør, at brugeren ikke skal vælge mellem «fysisk» og
        // «online»: gættet er væk, og svaret er målt på det hele.
        //
        // ET WEBINAR OMKLASSIFICERES ALDRIG.
        //
        // For et møde er et tavst højttalerspor svaret på «var der nogen i den
        // anden ende» — nej, altså et fysisk møde, og sporet er spildplads.
        //
        // For et webinar er det den modsatte oplysning: det ENESTE spor er
        // tavst, og så er der ingen optagelse. Slettes filen og typen laves om
        // til «fysisk», står der en optagelse uden lyd og uden en forklaring
        // på hvorfor. Den skal blive liggende, så det kan ses, at der blev
        // optaget — og at der ikke kom noget.
        if (Meta.Type != MeetingType.Webinar
            && (filer.TryGetValue(TrackKind.Loopback.ToString().ToLowerInvariant(), out var loopbackFil)
                || filer.TryGetValue("loopback", out loopbackFil)))
        {
            if (ErTavs(loopbackFil))
            {
                try
                {
                    File.Delete(loopbackFil);
                    filer.Remove("loopback");
                    Meta.Tracks.Remove("loopback");
                    Meta.LoopbackDeviceName = null;
                    Meta.Type = MeetingType.Physical;
                }
                catch (IOException)
                {
                    // Kan filen ikke slettes, bliver den liggende. Et tavst
                    // spor er spildplads, ikke en fejl.
                }
            }
        }

        if (_talt) { Interlocked.Decrement(ref _ilive); _talt = false; }

        Meta.EndedAt = DateTimeOffset.Now;
        Meta.DurationSeconds = Math.Round(_clock.Elapsed.TotalSeconds, 1);
        MeetingStore.Save(SessionDir, Meta);

        return filer;
    }

    /// <summary>
    /// Er sporet tavst hele vejen igennem?
    ///
    /// Der læses i spring frem for hele filen: en times lyd er 111 MB, og
    /// spørgsmålet er kun, om der NOGENSINDE var lyd. Findes én prøve over
    /// tærsklen, er svaret nej, og resten er ligegyldig.
    ///
    /// Tærsklen er den samme som alle andre steder i appen. To forskellige
    /// grænser for «stilhed» ville før eller siden være uenige.
    /// </summary>
    private static bool ErTavs(string wav)
    {
        try
        {
            using var fs = File.OpenRead(wav);
            if (fs.Length < 4096) return true;

            var buffer = new byte[8192];
            var spring = Math.Max(buffer.Length, (fs.Length - 46) / 400);   // ~400 stikproever
            var graense = (short)(AudioDevices.SilenceThreshold * short.MaxValue);

            for (var pos = 46L; pos < fs.Length - buffer.Length; pos += spring)
            {
                fs.Position = pos;
                var læst = fs.Read(buffer, 0, buffer.Length);

                for (var i = 0; i + 1 < læst; i += 2)
                {
                    var prøve = Math.Abs(BitConverter.ToInt16(buffer, i));
                    if (prøve > graense) return false;
                }
            }

            return true;
        }
        catch (IOException)
        {
            // Kan filen ikke laeses, roeres den ikke.
            return false;
        }
    }

    public IReadOnlyList<TrackIncident> AllIncidents =>
        _tracks.SelectMany(t => t.Incidents).OrderBy(i => i.AtSeconds).ToList();

    public void Dispose()
    {
        foreach (var t in _tracks) t.Dispose();
        Notebook.Dispose();
    }
}
