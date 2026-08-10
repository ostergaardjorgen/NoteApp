using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>
/// En løbende niveaumåler på én lydenhed.
///
/// Findes, så man kan vælge en enhed og med det samme se, om der kommer lyd
/// fra den. Det er den eneste kontrol, der reelt forhindrer et spildt møde:
/// enhedens navn siger intet om, hvorvidt lyden faktisk går derigennem.
///
/// Måleren skriver ikke til disk og gemmer ingen lyd. Den holder kun det
/// højeste niveau siden sidste aflæsning.
/// </summary>
public sealed class WasapiLevelProbe : IDisposable
{
    private readonly IWaveIn? _capture;
    private float _peak;
    private readonly object _gate = new();

    private WasapiLevelProbe(IWaveIn? capture, string? error = null)
    {
        _capture = capture;
        Error = error;

        if (capture is null) return;

        capture.DataAvailable += (_, e) =>
        {
            var p = PeakMeter.Peak(e.Buffer, e.BytesRecorded, capture.WaveFormat);
            lock (_gate) if (p > _peak) _peak = p;
        };
    }

    /// <summary>Sat hvis enheden ikke kunne åbnes. Så viser UI'et det frem for at lade måleren stå død.</summary>
    public string? Error { get; }

    public static WasapiLevelProbe Start(string deviceId, bool loopback)
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            var device = e.GetDevice(deviceId);

            IWaveIn capture = loopback
                ? new WasapiLoopbackCapture(device)
                : new WasapiCapture(device);

            var probe = new WasapiLevelProbe(capture);
            capture.StartRecording();
            return probe;
        }
        catch (Exception ex)
        {
            // En enhed, der ikke kan aabnes, maa ikke vaelte skaermen. Fejlen
            // vises ved maaleren, saa man kan vaelge en anden enhed.
            return new WasapiLevelProbe(null, ex.Message);
        }
    }

    /// <summary>Højeste niveau siden sidste kald. Nulstiller, så måleren falder igen.</summary>
    public float ReadPeak()
    {
        lock (_gate)
        {
            var p = _peak;
            _peak = 0f;
            return p;
        }
    }

    public void Dispose()
    {
        if (_capture is null) return;
        try { _capture.StopRecording(); } catch (Exception) { }
        _capture.Dispose();
    }
}
