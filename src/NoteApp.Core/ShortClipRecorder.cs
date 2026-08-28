namespace NoteApp.Core;

/// <summary>
/// Optager et kort klip fra mikrofonen — få sekunder, én fil.
///
/// HVORFOR DEN BRUGER TrackRecorder
///
/// Første udgave havde sin egen optagelseskæde. Den så rigtig ud og var det
/// ikke: fem sekunders tale blev til halvandet sekunds fil. Målt undervejs —
/// enheden leverede 633.464 byte, hvilket ved 48 kHz, to kanaler og 32 bit
/// er 1,65 sekunder, ikke fem. Lyden forsvandt i selve opsamlingen, ikke i
/// skrivningen.
///
/// <see cref="TrackRecorder"/> har optaget en time uden at tabe noget. Den
/// håndterer genstart, når enheden rives ud, den skriver i segmenter, så et
/// nedbrud højst koster tredive sekunder, og den har været kørt igennem på
/// rigtige møder.
///
/// Derfor er den her nu en indpakning frem for en kopi. Det koster en
/// midlertidig mappe med et segment i; det er billigt for at bruge noget,
/// der virker.
/// </summary>
public sealed class ShortClipRecorder : IDisposable
{
    private readonly string _arbejdsmappe;
    private TrackRecorder? _spor;
    private DateTime _start;

    public string Path { get; }

    public ShortClipRecorder(string path)
    {
        Path = path;
        _arbejdsmappe = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "heypia-klip-" + Guid.NewGuid().ToString("N")[..8]);
    }

    /// <summary>Højeste niveau (0-1), så måleren kan vise, at mikrofonen hører noget.</summary>
    public float Niveau => _spor?.ReadPeak() ?? 0;

    public void Start(string? mikrofonId)
    {
        Directory.CreateDirectory(_arbejdsmappe);
        _start = DateTime.Now;

        _spor = new TrackRecorder(TrackKind.Microphone, mikrofonId ?? "", _arbejdsmappe,
                                  () => (DateTime.Now - _start).TotalSeconds);
        _spor.Start();
    }

    /// <summary>Stopper, samler klippet og returnerer længden i sekunder.</summary>
    public double Stop()
    {
        if (_spor is null) return 0;

        _spor.Stop();

        try
        {
            var samlet = _spor.Assemble(_arbejdsmappe);

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.Copy(samlet, Path, overwrite: true);

            return Transcriber.WavSeconds(Path);
        }
        catch (Exception)
        {
            // Kunne klippet ikke samles, er der ingenting at give tilbage.
            // Kalderen ser nul sekunder og siger det.
            return 0;
        }
    }

    public void Dispose()
    {
        try { _spor?.Dispose(); } catch (Exception) { }

        // Arbejdsmappen er et spor med tale i. Den maa ikke blive liggende.
        try { if (Directory.Exists(_arbejdsmappe)) Directory.Delete(_arbejdsmappe, recursive: true); }
        catch (IOException) { }
    }
}
