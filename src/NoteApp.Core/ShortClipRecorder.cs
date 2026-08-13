using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace NoteApp.Core;

/// <summary>
/// Optager et kort klip fra mikrofonen — få sekunder, én fil, ingen segmenter.
///
/// Bruges til at tale et ord ind i ordbogen: man siger ordet, Whisper skriver
/// det ned, og så kan man se, HVORDAN det bliver hørt forkert. Det er langt
/// hurtigere end at vente på, at fejlen dukker op i et rigtigt møde.
///
/// Den er med vilje ikke <see cref="TrackRecorder"/>. Den skriver segmenter,
/// håndterer pause, genopretning efter nedbrud og enheder, der rives ud — alt
/// sammen rigtigt for et møde på en time, og alt sammen unødvendigt for fire
/// sekunder.
/// </summary>
public sealed class ShortClipRecorder : IDisposable
{
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _buffer;
    private WaveFileWriter? _writer;
    private IWaveProvider? _converted;
    private Thread? _pumpe;
    private volatile bool _kører;

    public string Path { get; }

    public ShortClipRecorder(string path) => Path = path;

    /// <summary>Højeste niveau set indtil nu (0-1). Til en måler, så man kan se, der er lyd.</summary>
    public float Niveau { get; private set; }

    public void Start(string? mikrofonId)
    {
        var enhed = AudioDevices.Resolve(mikrofonId ?? "");
        _capture = new WasapiCapture(enhed);

        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(30),

            // Som i TrackRecorder: uden dette fylder Read() op med stilhed, og
            // pumpen skriver tomme samples saa hurtigt CPU'en kan.
            DiscardOnBufferOverflow = true,
            ReadFully = false
        };

        ISampleProvider samples = _buffer.ToSampleProvider();
        if (samples.WaveFormat.Channels > 1) samples = new StereoToMonoSampleProvider(samples);
        samples = new WdlResamplingSampleProvider(samples, AudioFormat.SampleRate);
        _converted = samples.ToWaveProvider16();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer = new WaveFileWriter(Path,
            new WaveFormat(AudioFormat.SampleRate, AudioFormat.BitsPerSample, AudioFormat.Channels));

        _capture.DataAvailable += (_, e) =>
        {
            _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // Niveauet aflaeses paa raa 16-bit samples. Det er kun til en
            // maaler; praecisionen betyder intet, men det goer det, at man kan
            // SE, at mikrofonen virker, foer man taler.
            var top = 0f;
            for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
                top = Math.Max(top, Math.Abs(BitConverter.ToInt16(e.Buffer, i)) / 32768f);
            Niveau = top;
        };

        _kører = true;
        _capture.StartRecording();

        _pumpe = new Thread(Pump) { IsBackground = true, Name = "kort-klip" };
        _pumpe.Start();
    }

    private void Pump()
    {
        var buffer = new byte[8192];

        while (_kører)
        {
            var læst = _converted!.Read(buffer, 0, buffer.Length);
            if (læst > 0) _writer!.Write(buffer, 0, læst);
            else Thread.Sleep(20);
        }
    }

    /// <summary>Stopper og lukker filen. Returnerer længden i sekunder.</summary>
    public double Stop()
    {
        _kører = false;

        try { _capture?.StopRecording(); } catch (Exception) { }
        _pumpe?.Join(1000);

        // Rest i bufferen med. Fire sekunder er kort nok til, at et halvt
        // sekund paa gulvet ville vaere et halvt ord.
        var buffer = new byte[8192];
        int læst;
        while (_converted is not null && (læst = _converted.Read(buffer, 0, buffer.Length)) > 0)
            _writer!.Write(buffer, 0, læst);

        var sekunder = _writer is null ? 0 : _writer.TotalTime.TotalSeconds;

        _writer?.Dispose();
        _writer = null;

        return sekunder;
    }

    public void Dispose()
    {
        _kører = false;
        try { _capture?.Dispose(); } catch (Exception) { }
        _writer?.Dispose();
    }
}
