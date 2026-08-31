using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>
/// Holder mikrofonen åben og de sidste sekunder i hukommelsen — så en
/// diktering kan begynde FØR den blev bedt om det.
/// </summary>
/// <remarks>
/// HVORFOR VENTETIDEN VAR DER
///
/// Vågeordet høres af whisper-command. Først når den havde meldt ordet, blev
/// mikrofonen åbnet til selve optagelsen. Der lå derfor to ventetider efter
/// hinanden:
///
///   1. Motoren skal høre ordet FÆRDIGT og afgøre det. Målt 30-08-2026:
///      mellem 60 og 555 millisekunder.
///   2. Lydenheden skal åbnes. Det tager sin egen tid.
///
/// I den tid talte man ud i ingenting, og de første ord blev klippet. Man
/// skulle vente på boblen for at være sikker — og så var det ikke længere
/// hurtigere end at trykke på en tast.
///
/// HVORDAN DEN LØSES
///
/// Mikrofonen er åben, mens der lyttes, og de sidste sekunder ligger i en
/// ring, der hele tiden overskriver sig selv. Når ordet høres, tages de med
/// tilbage i tiden — inklusive «Hej Pia» selv, som skæres væk af teksten
/// bagefter.
///
/// DER SKRIVES ALDRIG NOGET NED, FØR ORDET ER HØRT. Ringen er hukommelse og
/// kun hukommelse; det, der er faldet ud, findes ikke nogen steder. Det er
/// den samme aftale som før — den flytter bare fra whisper-command ind i
/// appen, hvor den kan efterprøves.
/// </remarks>
public sealed class Foroptager : IDisposable
{
    /// <summary>
    /// Hvor langt tilbage der huskes.
    /// </summary>
    /// <remarks>
    /// Fire sekunder. Det dækker «Hej Pia» plus den tid, motoren bruger på at
    /// afgøre det — med rigelig margen. Længere ville koste hukommelse for
    /// lyd, ingen kommer til at bruge: 16 kHz i 16 bit er 32 kB pr. sekund,
    /// så fire sekunder er 128 kB.
    /// </remarks>
    public const double Sekunder = 4.0;

    private WasapiCapture? _optager;
    private Lydring? _ring;
    private List<short>? _beholdt;
    private readonly object _laas = new();

    /// <summary>Prøver pr. sekund på den enhed, der er åben.</summary>
    public int Frekvens { get; private set; }

    /// <summary>Er mikrofonen åben?</summary>
    public bool Koerer => _optager is not null;

    /// <summary>Højeste niveau siden sidst — så en måler kan vise, at der høres noget.</summary>
    public float Niveau { get; private set; }

    /// <summary>
    /// Åbner mikrofonen og begynder at fylde ringen.
    /// </summary>
    public bool Start(string? mikrofonId)
    {
        lock (_laas)
        {
            if (_optager is not null) return true;

            try
            {
                var enhed = AudioDevices.ResolveMicrophone(mikrofonId, out _);
                if (enhed is null) return false;

                using var e = new MMDeviceEnumerator();
                var apparat = e.GetDevice(enhed.Id);

                _optager = new WasapiCapture(apparat);
                Frekvens = _optager.WaveFormat.SampleRate;

                _ring = Lydring.Til(Sekunder, Frekvens);
                _beholdt = null;

                _optager.DataAvailable += Data;
                _optager.StartRecording();

                return true;
            }
            catch (Exception)
            {
                Ryd();
                return false;
            }
        }
    }

    /// <summary>Lukker mikrofonen og glemmer alt.</summary>
    public void Stop()
    {
        lock (_laas) Ryd();
    }

    private void Ryd()
    {
        try { _optager?.StopRecording(); } catch (Exception) { }
        try { _optager?.Dispose(); } catch (Exception) { }

        _optager = null;
        _ring = null;
        _beholdt = null;
        Niveau = 0;
    }

    /// <summary>
    /// Vågeordet er hørt: behold det, der ligger i ringen, og alt hvad der
    /// kommer nu.
    /// </summary>
    public void Behold()
    {
        lock (_laas)
        {
            if (_ring is null) return;

            _beholdt = new List<short>(_ring.Laes());
        }
    }

    /// <summary>Er der noget på vej i hus lige nu?</summary>
    public bool Beholder
    {
        get { lock (_laas) return _beholdt is not null; }
    }

    /// <summary>
    /// Skriver det beholdte som en WAV og holder op med at beholde.
    /// Returnerer længden i sekunder — nul, hvis der intet var.
    /// </summary>
    public double Gem(string sti)
    {
        short[] proever;
        int frekvens;

        lock (_laas)
        {
            if (_beholdt is null || _beholdt.Count == 0) { _beholdt = null; return 0; }

            proever = _beholdt.ToArray();
            frekvens = Frekvens;
            _beholdt = null;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

            // 16 bit, én kanal. Det er dét, baade Whisper og Voxtral vil have,
            // og saa er der ikke en omregning mere, der kan gaa galt.
            var format = new WaveFormat(frekvens, 16, 1);

            using (var skriver = new WaveFileWriter(sti, format))
            {
                var bytes = new byte[proever.Length * 2];
                Buffer.BlockCopy(proever, 0, bytes, 0, bytes.Length);
                skriver.Write(bytes, 0, bytes.Length);
            }

            return (double)proever.Length / frekvens;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Tager imod lyden fra enheden: ned til én kanal, om til 16 bit, ind i
    /// ringen.
    /// </summary>
    /// <remarks>
    /// KALDET SKAL VÆRE HURTIGT. Det kommer mange gange i sekundet fra
    /// lydlaget, og bliver det langsomt, taber enheden data — og så mangler
    /// der stavelser midt i en sætning.
    /// </remarks>
    private void Data(object? afsender, WaveInEventArgs e)
    {
        var format = _optager?.WaveFormat;
        if (format is null || e.BytesRecorded == 0) return;

        var kanaler = Math.Max(1, format.Channels);
        short[] mono;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            var antal = e.BytesRecorded / 4 / kanaler;
            mono = new short[antal];

            for (var i = 0; i < antal; i++)
            {
                // Kun foerste kanal. En blanding af to ville daempe en stemme,
                // der kun er paa den ene - og en mikrofon har som regel én.
                var v = BitConverter.ToSingle(e.Buffer, (i * kanaler) * 4);
                mono[i] = (short)Math.Clamp(v * short.MaxValue, short.MinValue, short.MaxValue);
            }
        }
        else if (format.BitsPerSample == 16)
        {
            var antal = e.BytesRecorded / 2 / kanaler;
            mono = new short[antal];

            for (var i = 0; i < antal; i++)
                mono[i] = BitConverter.ToInt16(e.Buffer, (i * kanaler) * 2);
        }
        else
        {
            // Et format, vi ikke kender. Bedre ingenting end stoej.
            return;
        }

        var top = 0f;
        foreach (var p in mono)
        {
            var a = Math.Abs(p / (float)short.MaxValue);
            if (a > top) top = a;
        }

        lock (_laas)
        {
            Niveau = top;
            _ring?.Skriv(mono);
            _beholdt?.AddRange(mono);
        }
    }

    public void Dispose() => Stop();
}
