using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NoteApp.Core;

public sealed record DeviceInfo(string Id, string FriendlyName, bool IsDefault = false)
{
    /// <summary>
    /// Navnet, som det står på skærmen.
    /// </summary>
    /// <remarks>
    /// Windows har TO standardmikrofoner: «Standardenhed» og
    /// «Standardkommunikationsenhed». Appen læser den FØRSTE — den, der
    /// sættes i Indstillinger → System → Lyd, og som brugeren derfor kan se.
    ///
    /// Etiketten her betyder altså det samme som fluebenet i Windows' egen
    /// lydskærm. Stod der noget andet, ville de to modsige hinanden, og det
    /// gjorde de: se <see cref="AudioDevices.Microphones"/>.
    /// </remarks>
    public string Display => IsDefault
        ? $"{FriendlyName}  (Windows' standard)"
        : FriendlyName;
}

/// <summary>
/// Lydenheder.
///
/// Oprindeligt tog appen altid Windows' standardenheder og tilbød ingen
/// dropdown (speccen afsnit 2). Den beslutning er omgjort 10. august 2026:
/// med en Jabra-højttaler, en laptopmikrofon og et headset tilsluttet er
/// Windows' standard ofte den forkerte, og uden et valg i appen skal man ud
/// i Windows' lydindstillinger midt i en mødeforberedelse.
///
/// Valget gemmes som enheds-ID, ikke som navn: to headset af samme model
/// hedder det samme. Er den valgte enhed væk — headsettet er taget ud —
/// falder appen tilbage på Windows' standard frem for at fejle, og siger det.
/// </summary>
public static class AudioDevices
{
    /// <summary>Alle mikrofoner, Windows kender. Standarden står først.</summary>
    /// <remarks>
    /// MULTIMEDIA OG IKKE COMMUNICATIONS — OG DET ER EN RETTELSE.
    ///
    /// Windows har to standardmikrofoner. «Standardenhed» sættes i
    /// Indstillinger → System → Lyd, som er dér, folk går hen.
    /// «Standardkommunikationsenhed» sættes kun i det gamle kontrolpanel,
    /// som Windows 11 ikke længere viser vej til.
    ///
    /// Appen læste kommunikationsenheden. Set 30-08-2026: brugeren havde sat
    /// sit headset som standardenhed og fik alligevel et helt andet apparat
    /// vist som standard. Han troede, appen ikke kunne huske hans valg — og
    /// der var ingen måde at se, at den kiggede et andet sted hen.
    ///
    /// Nu læses den, brugeren faktisk kan se og sætte. Højttalerne har hele
    /// tiden brugt den samme.
    /// </remarks>
    public static IReadOnlyList<DeviceInfo> Microphones() => Enumerate(DataFlow.Capture, Role.Multimedia);

    /// <summary>Alle afspilningsenheder. Loopback optages fra den, lyden faktisk går til.</summary>
    public static IReadOnlyList<DeviceInfo> Speakers() => Enumerate(DataFlow.Render, Role.Multimedia);

    private static IReadOnlyList<DeviceInfo> Enumerate(DataFlow flow, Role role)
    {
        try
        {
            using var e = new MMDeviceEnumerator();

            string? standardId = null;
            try { standardId = e.GetDefaultAudioEndpoint(flow, role).ID; } catch (Exception) { }

            return e.EnumerateAudioEndPoints(flow, DeviceState.Active)
                .Select(d => new DeviceInfo(d.ID, d.FriendlyName, d.ID == standardId))
                .OrderByDescending(d => d.IsDefault)
                .ThenBy(d => d.FriendlyName)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<DeviceInfo>();
        }
    }

    /// <summary>
    /// Finder den valgte enhed, eller falder tilbage på Windows' standard,
    /// hvis den er væk. <paramref name="wasFallback"/> siger, om der blev
    /// faldet tilbage — brugeren skal have det at vide FØR optagelsen, ikke
    /// opdage det bagefter.
    /// </summary>
    public static DeviceInfo? ResolveMicrophone(string? preferredId, out bool wasFallback)
        => Resolve(Microphones(), preferredId, DefaultMicrophone(), out wasFallback);

    public static DeviceInfo? ResolveSpeaker(string? preferredId, out bool wasFallback)
        => Resolve(Speakers(), preferredId, DefaultRenderDevice(), out wasFallback);

    private static DeviceInfo? Resolve(IReadOnlyList<DeviceInfo> alle, string? preferredId,
                                       DeviceInfo? standard, out bool wasFallback)
    {
        wasFallback = false;

        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var valgt = alle.FirstOrDefault(d => d.Id == preferredId);
            if (valgt is not null) return valgt;
            wasFallback = true;
        }

        return standard;
    }

    /// <summary>Øjebliksniveau på en mikrofon. Bruges til måleren, når enheden vælges.</summary>
    public static float MeasureCapturePeak(string deviceId, TimeSpan window, CancellationToken ct = default)
    {
        var peak = 0f;
        using var device = Resolve(deviceId);
        using var probe = new WasapiCapture(device);

        probe.DataAvailable += (_, e) =>
        {
            var p = PeakMeter.Peak(e.Buffer, e.BytesRecorded, probe.WaveFormat);
            if (p > peak) peak = p;
        };

        probe.StartRecording();
        try { ct.WaitHandle.WaitOne(window); }
        finally { probe.StopRecording(); }

        return peak;
    }

    public static DeviceInfo? DefaultMicrophone()
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            // Samme rolle som Microphones() - se forklaringen dér.
            var d = e.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
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

    /// <summary>
    /// Den højttaler, Windows sender OPKALD til. Kan være en anden end den
    /// almindelige standard.
    /// </summary>
    /// <remarks>
    /// ============ WINDOWS HAR TO STANDARDER, IKKE ÉN ============
    ///
    /// «Standardenhed» er den, musik og video bruger. «Standardkommunikations-
    /// enhed» er den, opkald bruger — Teams, Telefonlink, Zoom. De kan pege
    /// hvert sit sted, og det gør de tit: skærmens højttalere til musik,
    /// headsettet til møder.
    ///
    /// DET ER DEN FORSKEL, DER KOSTER DEN ANDEN HALVDEL AF SAMTALEN.
    ///
    /// Optager appen loopback fra den almindelige standard, mens opkaldet
    /// kører i headsettet, optages din egen stemme fint — og modparten
    /// findes ikke. Udskriften ser hel ud. Den er det ikke, og man opdager det
    /// først, når man leder efter noget, der blev sagt.
    ///
    /// Målt 08-09-2026: en Jabra SPEAK 510 stod som standardkommunikations-
    /// enhed, mens skærmen stod som almindelig standard.
    /// </remarks>
    public static DeviceInfo? DefaultOpkaldshoejttaler() => Standard(DataFlow.Render, Role.Communications);

    /// <summary>Den mikrofon, Windows bruger til opkald. Se <see cref="DefaultOpkaldshoejttaler"/>.</summary>
    public static DeviceInfo? DefaultOpkaldsmikrofon() => Standard(DataFlow.Capture, Role.Communications);

    private static DeviceInfo? Standard(DataFlow flow, Role role)
    {
        try
        {
            using var e = new MMDeviceEnumerator();
            var d = e.GetDefaultAudioEndpoint(flow, role);
            return new DeviceInfo(d.ID, d.FriendlyName);
        }
        catch (Exception)
        {
            // Ingen enhed af den slags. Kalderen viser det; her er null nok.
            return null;
        }
    }

    /// <summary>
    /// Vil et opkald blive optaget helt med de enheder, der er valgt?
    /// </summary>
    /// <param name="valgtHoejttaler">Den højttaler, appen optager loopback fra.</param>
    /// <param name="valgtMikrofon">Den mikrofon, appen optager fra.</param>
    /// <returns>
    /// Null når alt er, som det skal være. Ellers navnet på den enhed,
    /// opkaldet faktisk kører på — dét, brugeren skal vælge.
    /// </returns>
    /// <remarks>
    /// DER SAMMENLIGNES PÅ ID OG IKKE PÅ NAVN. To enheder kan hedde det samme
    /// («Mikrofon»), og en enhed kan skifte navn, når en driver opdateres.
    ///
    /// FINDES DER INGEN KOMMUNIKATIONSENHED, ER DER INTET AT ADVARE OM. Så er
    /// de to standarder den samme, og Windows svarer med den.
    /// </remarks>
    public static (string? Hoejttaler, string? Mikrofon) Opkaldsafvigelse(
        string? valgtHoejttaler, string? valgtMikrofon)
    {
        var h = DefaultOpkaldshoejttaler();
        var m = DefaultOpkaldsmikrofon();

        return (
            h is not null && !string.Equals(h.Id, valgtHoejttaler, StringComparison.OrdinalIgnoreCase)
                ? h.FriendlyName : null,
            m is not null && !string.Equals(m.Id, valgtMikrofon, StringComparison.OrdinalIgnoreCase)
                ? m.FriendlyName : null);
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

