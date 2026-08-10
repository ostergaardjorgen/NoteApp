using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NoteApp.Core;

public sealed record DeviceInfo(string Id, string FriendlyName);

/// <summary>
/// Enhedsvalg tages fra Windows' standardenheder. Appen viser hvad der blev
/// valgt, men tilbyder ikke en dropdown — jf. speccen afsnit 2. Vælger man
/// forkert enhed midt i et møde, er skaden allerede sket; at vise navnet
/// tydeligt er den kontrol der faktisk hjælper.
/// </summary>
public static class AudioDevices
{
    public static DeviceInfo? DefaultMicrophone()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            var d = e.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return new DeviceInfo(d.ID, d.FriendlyName);
        }
        catch (Exception)
        {
            // Ingen mikrofon tilsluttet. Kalderen viser fejlen; her er null nok.
            return null;
        }
    }

    public static DeviceInfo? DefaultRenderDevice()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            var d = e.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return new DeviceInfo(d.ID, d.FriendlyName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static MMDevice Resolve(string id)
    {
        var e = new MMDeviceEnumerator();
        return e.GetDevice(id);
    }

    /// <summary>
    /// Måler loopback-niveauet i et kort vindue. Et onlinemøde med tavst
    /// loopback-spor er den fejl der koster et helt møde, og den kan kun
    /// fanges FØR optagelsen begynder.
    /// </summary>
    public static float MeasureLoopbackPeak(string renderDeviceId, TimeSpan window, CancellationToken ct = default)
    {
        var peak = 0f;
        using var device = Resolve(renderDeviceId);
        using var probe = new WasapiLoopbackCapture(device);

        probe.DataAvailable += (_, e) =>
        {
            var p = PeakMeter.Peak(e.Buffer, e.BytesRecorded, probe.WaveFormat);
            if (p > peak) peak = p;
        };

        probe.StartRecording();
        try
        {
            ct.WaitHandle.WaitOne(window);
        }
        finally
        {
            probe.StopRecording();
        }

        return peak;
    }

    /// <summary>Under dette niveau regnes sporet som tavst. -60 dBFS.</summary>
    public const float SilenceThreshold = 0.001f;
}

public static class PeakMeter
{
    public static float Peak(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        var peak = 0f;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var i = 0; i + 4 <= bytesRecorded; i += 4)
            {
                var v = Math.Abs(BitConverter.ToSingle(buffer, i));
                if (v > peak) peak = v;
            }
        }
        else if (format.BitsPerSample == 16)
        {
            for (var i = 0; i + 2 <= bytesRecorded; i += 2)
            {
                var v = Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f);
                if (v > peak) peak = v;
            }
        }

        return peak;
    }

    public static float ToDb(float peak) => peak <= 0f ? -100f : 20f * MathF.Log10(peak);

    /// <summary>0..1 til en niveaumåler, hvor -60 dBFS er bunden.</summary>
    public static float ToMeterScale(float peak) => Math.Clamp((ToDb(peak) + 60f) / 60f, 0f, 1f);
}
