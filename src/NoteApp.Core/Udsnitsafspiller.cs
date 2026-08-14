using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>
/// Spiller ET UDSNIT af en lydfil — fra et tidspunkt til et andet.
///
/// HVORFOR DEN FINDES
///
/// Når et ord står forkert i udskriften, er der to mulige forklaringer, og de
/// kræver hver sin handling: enten blev ordet sagt tydeligt, og appen hørte
/// forkert (så skal det rettes i ordbogen) — eller det blev sagt utydeligt (så
/// skal sætningen læses op igen). Teksten alene kan ikke skelne. Lyden kan.
///
/// Uden en afspiller ville brugeren skulle finde sin egen lydafspiller, åbne
/// en wav-fil på 28 MB og selv spole til 7 minutter og 12 sekunder. Det gør
/// ingen, og så bliver alle fejl rettet på må og få.
/// </summary>
public sealed class Udsnitsafspiller : IDisposable
{
    private WaveOutEvent? _ud;
    private AudioFileReader? _fil;
    private TimeSpan _slut;

    /// <summary>Kaldes, når udsnittet er spillet færdigt — også når det blev stoppet.</summary>
    public event Action? Faerdig;

    public bool Spiller => _ud?.PlaybackState == PlaybackState.Playing;

    /// <summary>Filen, der spilles fra lige nu. Null når der er stille.</summary>
    public string? Fil { get; private set; }

    /// <summary>Udsnittet, der spilles lige nu.</summary>
    public TimeSpan Start { get; private set; }

    /// <summary>
    /// Starter et udsnit. Et udsnit, der allerede kører, stoppes først — der
    /// skal aldrig kunne køre to ad gangen, for så kan man ikke høre nogen af
    /// dem.
    /// </summary>
    public void Spil(string fil, TimeSpan fra, TimeSpan til)
    {
        Stop();

        if (!File.Exists(fil)) throw new FileNotFoundException("Lydfilen findes ikke.", fil);

        _fil = new AudioFileReader(fil);

        // Et udsnit, der starter efter filens slutning, er en fejl i
        // tidsmærkerne — ikke noget, brugeren skal se en undtagelse for.
        if (fra >= _fil.TotalTime) { Ryd(); return; }

        // Lidt luft i begge ender. Whispers tidsmærker rammer tæt på, men ikke
        // på millisekundet, og et ord, der bliver klippet halvt over, lyder
        // utydeligt uanset hvor tydeligt det blev sagt — så ville afspilningen
        // give det forkerte svar på præcis det spørgsmål, den skal afgøre.
        var luft = TimeSpan.FromMilliseconds(250);

        Start = fra - luft < TimeSpan.Zero ? TimeSpan.Zero : fra - luft;
        _slut = til + luft > _fil.TotalTime ? _fil.TotalTime : til + luft;

        if (_slut <= Start) _slut = _fil.TotalTime;

        _fil.CurrentTime = Start;
        Fil = fil;

        _ud = new WaveOutEvent();
        _ud.Init(_fil);
        _ud.PlaybackStopped += (_, _) => Ryd();
        _ud.Play();

        // Slutningen holdes af en tråd frem for af en timer i UI'et: en timer,
        // der bliver hængende, når skærmen skiftes, spiller resten af filen.
        _vagt = new System.Threading.Timer(_ =>
        {
            if (_fil is null || _ud is null) return;
            if (_fil.CurrentTime >= _slut) Stop();
        }, null, 50, 50);
    }

    private System.Threading.Timer? _vagt;

    public void Stop()
    {
        try { _ud?.Stop(); } catch (Exception) { }
        Ryd();
    }

    private void Ryd()
    {
        _vagt?.Dispose();
        _vagt = null;

        var kørte = _ud is not null;

        try { _ud?.Dispose(); } catch (Exception) { }
        try { _fil?.Dispose(); } catch (Exception) { }

        _ud = null;
        _fil = null;
        Fil = null;

        if (kørte) Faerdig?.Invoke();
    }

    public void Dispose() => Stop();
}
