using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace NoteApp.Core;

public sealed record TrackIncident(DateTimeOffset At, double AtSeconds, string What);

/// <summary>
/// Ét lydspor, skrevet som en række 30-sekunders WAV-segmenter i stedet for
/// én stor fil.
///
/// Segmenteringen er der for crash-sikkerheden (feature 4), men giver en
/// sidegevinst: fordi hvert segment er en selvstændig fil i det samme
/// udgangsformat, kan lydkæden bygges helt om midt i optagelsen uden at
/// ødelægge noget. Skifter du headset midt i et møde, starter vi bare et nyt
/// segment på den nye enhed — også selv om den har et andet samplerate eller
/// kanalantal end den gamle.
/// </summary>
public sealed class TrackRecorder : IDisposable
{
    private const int MaxRestarts = 5;

    private readonly TrackKind _kind;
    private readonly string _segmentDir;
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _stopping = new(false);
    private readonly byte[] _pumpBuffer = new byte[AudioFormat.BytesPerSecond];
    private readonly List<TrackIncident> _incidents = new();
    private readonly Func<double> _elapsedSeconds;

    private string _deviceId;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _incoming;
    private IWaveProvider? _converted;
    private WaveFileWriter? _writer;
    private Thread? _pump;

    private int _segmentIndex = -1;
    private long _bytesInSegment;
    private long _bytesTotal;
    private int _restarts;
    private float _peak;
    private volatile bool _running;

    public TrackRecorder(TrackKind kind, string deviceId, string sessionDir, Func<double> elapsedSeconds)
    {
        _kind = kind;
        _deviceId = deviceId;
        _elapsedSeconds = elapsedSeconds;
        _segmentDir = Path.Combine(sessionDir, "segmenter", Name);
        Directory.CreateDirectory(_segmentDir);
    }

    public string Name => _kind == TrackKind.Microphone ? "mikrofon" : "loopback";

    public IReadOnlyList<TrackIncident> Incidents
    {
        get { lock (_gate) return _incidents.ToList(); }
    }

    public double RecordedSeconds => Interlocked.Read(ref _bytesTotal) / (double)AudioFormat.BytesPerSecond;

    /// <summary>Højeste niveau siden sidste kald. Nulstiller måleren.</summary>
    public float ReadPeak()
    {
        var p = _peak;
        _peak = 0f;
        return p;
    }

    /// <summary>Rejses når sporet mistede sin enhed, uanset om genstarten lykkedes.</summary>
    public event Action<TrackIncident>? IncidentOccurred;

    public void Start()
    {
        BuildChain();
        _running = true;
        _pump = new Thread(Pump) { IsBackground = true, Name = $"pump:{Name}" };
        _pump.Start();
        _capture!.StartRecording();
    }

    public void Stop()
    {
        _running = false;

        try { _capture?.StopRecording(); } catch (Exception) { /* enheden kan allerede være væk */ }

        _stopping.Set();
        _pump?.Join(TimeSpan.FromSeconds(10));

        lock (_gate)
        {
            DrainLocked();
            CloseSegmentLocked();
        }
    }

    // ---------------------------------------------------------------- kæden

    private void BuildChain()
    {
        using var device = AudioDevices.Resolve(_deviceId);

        _capture = _kind == TrackKind.Loopback
            ? new WasapiLoopbackCapture(device)
            : new WasapiCapture(device);

        _incoming = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(30),
            DiscardOnBufferOverflow = true,

            // AFGØRENDE: default er true, hvilket får Read() til at fylde op
            // med stilhed i stedet for at returnere 0 når bufferen er tom.
            // Pumpen ville så aldrig gå i dvale, men skrive tomme samples så
            // hurtigt CPU'en kunne — 9 sekunders optagelse blev til 192 MB.
            ReadFully = false
        };

        ISampleProvider samples = _incoming.ToSampleProvider();
        samples = ToMono(samples);
        samples = new WdlResamplingSampleProvider(samples, AudioFormat.SampleRate);
        _converted = samples.ToWaveProvider16();

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    private void TearDownChain()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            try { _capture.Dispose(); } catch (Exception) { /* enheden kan være revet ud */ }
            _capture = null;
        }

        _incoming = null;
        _converted = null;
    }

    private static ISampleProvider ToMono(ISampleProvider source) => source.WaveFormat.Channels switch
    {
        1 => source,
        2 => new StereoToMonoSampleProvider(source) { LeftVolume = 0.5f, RightVolume = 0.5f },
        // Flerkanals-enheder (fx et headset der melder sig som 4 kanaler):
        // tag første kanal frem for at gætte på en nedmixning.
        _ => new MultiplexingSampleProvider(new[] { source }, 1)
    };

    // ------------------------------------------------------------ hændelser

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (capture is null) return;

        var p = PeakMeter.Peak(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        if (p > _peak) _peak = p;

        _incoming?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Et rent stop går også gennem denne hændelse. Kun uventede stop skal
        // føre til genstart.
        if (!_running) return;

        var årsag = e.Exception?.Message ?? "enheden stoppede uventet";
        Record($"Sporet mistede enheden: {årsag}");

        if (_restarts >= MaxRestarts)
        {
            Record($"Opgiver efter {MaxRestarts} forsøg. Sporet er stoppet.");
            _running = false;
            return;
        }

        _restarts++;

        // Genstart på en baggrundstråd: vi står i NAudio's egen hændelse, og
        // at rive kæden ned herfra ville låse.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(1_000);
            if (!_running) return;

            try
            {
                lock (_gate)
                {
                    DrainLocked();
                    // Nyt segment: den nye enhed kan have et andet format, og
                    // et WAV-segment må kun indeholde ét.
                    CloseSegmentLocked();
                    TearDownChain();

                    var nu = _kind == TrackKind.Loopback
                        ? AudioDevices.DefaultRenderDevice()
                        : AudioDevices.DefaultMicrophone();

                    if (nu is null)
                    {
                        Record("Ingen enhed at falde tilbage på. Sporet er stoppet.");
                        _running = false;
                        return;
                    }

                    if (nu.Id != _deviceId)
                        Record($"Skifter til '{nu.FriendlyName}'.");

                    _deviceId = nu.Id;
                    BuildChain();
                }

                _capture!.StartRecording();
                Record($"Optagelsen fortsatte (forsøg {_restarts}).");
            }
            catch (Exception ex)
            {
                Record($"Genstart mislykkedes: {ex.Message}");
            }
        });
    }

    private void Record(string what)
    {
        var incident = new TrackIncident(DateTimeOffset.Now, Math.Round(_elapsedSeconds(), 1), what);
        lock (_gate) _incidents.Add(incident);
        IncidentOccurred?.Invoke(incident);
    }

    // ---------------------------------------------------------------- pumpe

    private void Pump()
    {
        while (!_stopping.IsSet)
        {
            int skrevet;
            lock (_gate) skrevet = DrainLocked();

            if (skrevet == 0)
                _stopping.Wait(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>Kaldes altid under _gate.</summary>
    private int DrainLocked()
    {
        var converted = _converted;
        if (converted is null) return 0;

        var total = 0;
        int read;

        while ((read = converted.Read(_pumpBuffer, 0, _pumpBuffer.Length)) > 0)
        {
            var offset = 0;
            while (offset < read)
            {
                if (_writer is null) OpenSegmentLocked();

                var plads = ChunkBytes - _bytesInSegment;
                var tag = (int)Math.Min(read - offset, plads);

                _writer!.Write(_pumpBuffer, offset, tag);
                offset += tag;
                _bytesInSegment += tag;
                Interlocked.Add(ref _bytesTotal, tag);

                if (_bytesInSegment >= ChunkBytes) CloseSegmentLocked();
            }

            total += read;
            if (total >= _pumpBuffer.Length * 4) break;   // giv måleren luft
        }

        // Flush efter hver runde: det er dét, der gør et efterladt segment
        // læsbart efter et crash.
        _writer?.Flush();
        return total;
    }

    private static long ChunkBytes => (long)AudioFormat.ChunkDuration.TotalSeconds * AudioFormat.BytesPerSecond;

    private void OpenSegmentLocked()
    {
        _segmentIndex++;
        var path = Path.Combine(_segmentDir, $"seg_{_segmentIndex:D5}.wav");
        _writer = new WaveFileWriter(path, new WaveFormat(AudioFormat.SampleRate, AudioFormat.BitsPerSample, AudioFormat.Channels));
        _bytesInSegment = 0;
    }

    private void CloseSegmentLocked()
    {
        if (_writer is null) return;
        _writer.Flush();
        _writer.Dispose();
        _writer = null;
        _bytesInSegment = 0;
    }

    /// <summary>
    /// Samler segmenterne til én WAV og fjerner dem. Kaldes efter Stop.
    /// Fejler den, bliver segmenterne liggende — så er der stadig noget at
    /// redde, og genopretningen kan tage over.
    /// </summary>
    public string Assemble(string sessionDir)
    {
        var mål = Path.Combine(sessionDir, $"{Name}.wav");
        SegmentAssembler.Assemble(_segmentDir, mål);

        try { Directory.Delete(_segmentDir, recursive: true); }
        catch (IOException) { /* lad dem ligge; de gør ingen skade */ }

        return mål;
    }

    public void Dispose()
    {
        TearDownChain();
        _writer?.Dispose();
        _stopping.Dispose();
    }
}
