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
        if (type == MeetingType.Online && renderDevice is null)
            throw new ArgumentException("Onlinemøde kræver en afspilningsenhed at optage loopback fra.", nameof(renderDevice));

        UserDataPaths.EnsureCreated();

        var startedAt = DateTimeOffset.Now;
        var dir = MeetingStore.CreateSessionDirectory(title, startedAt);

        var meta = new MeetingMetadata
        {
            Type = type,
            StartedAt = startedAt,
            Title = title,
            MicDeviceName = microphone.FriendlyName,
            LoopbackDeviceName = type == MeetingType.Online ? renderDevice!.FriendlyName : null
        };

        // Skriv metadata FØR optagelsen begynder. Crasher maskinen, er der
        // stadig noget genopretningen kan læse.
        MeetingStore.Save(dir, meta);

        var session = new RecordingSession(dir, meta);

        session.Microphone = new TrackRecorder(TrackKind.Microphone, microphone.Id, dir,
                                               () => session._clock.Elapsed.TotalSeconds);
        session._tracks.Add(session.Microphone);

        if (type == MeetingType.Online)
        {
            session.Loopback = new TrackRecorder(TrackKind.Loopback, renderDevice!.Id, dir,
                                                 () => session._clock.Elapsed.TotalSeconds);
            session._tracks.Add(session.Loopback);
        }

        foreach (var t in session._tracks)
            t.IncidentOccurred += i => session.IncidentOccurred?.Invoke(i);

        return session;
    }

    public void Start()
    {
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

        Meta.EndedAt = DateTimeOffset.Now;
        Meta.DurationSeconds = Math.Round(_clock.Elapsed.TotalSeconds, 1);
        MeetingStore.Save(SessionDir, Meta);

        return filer;
    }

    public IReadOnlyList<TrackIncident> AllIncidents =>
        _tracks.SelectMany(t => t.Incidents).OrderBy(i => i.AtSeconds).ToList();

    public void Dispose()
    {
        foreach (var t in _tracks) t.Dispose();
        Notebook.Dispose();
    }
}
