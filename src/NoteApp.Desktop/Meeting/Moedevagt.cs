using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Holder øje med, om der er startet et møde — og spørger, om det skal optages.
///
/// HVORFOR MIKROFONEN OG IKKE HØJTTALEREN
///
/// Et møde og et webinar lyder ens i højttaleren. De kan ikke skilles ad dér.
/// Men et møde bruger DIN MIKROFON, og et webinar, en YouTube-video og en
/// streamingtjeneste rører den aldrig. Se <see cref="Mikrofonvagt"/> for
/// målingen.
///
/// TRE REGLER, OG HVER AF DEM ER DER FOR AT UNDGÅ ET SPØRGSMÅL FOR MEGET
///
/// 1. Der spørges først efter et halvt minut. Diktering, stemmestyring og
///    «test din mikrofon» åbner den i få sekunder. Et møde varer længere.
///
/// 2. Der spørges én gang pr. gang, programmet åbner mikrofonen. Siger man
///    nej, kommer der ikke et spørgsmål mere ti sekunder senere — men lukker
///    man mødet og åbner et nyt, spørges der igen.
///
/// 3. Der spørges aldrig om et program, man har frabedt sig. Discord og et
///    spil bruger mikrofonen uden at være et møde, og et spørgsmål, man
///    afviser hver dag, er værre end intet spørgsmål.
///
/// DER OPTAGES ALDRIG AF SIG SELV. Vagten kan kun spørge.
/// </summary>
public sealed class Moedevagt
{
    /// <summary>
    /// Hvor længe mikrofonen skal have været i brug, før der spørges.
    ///
    /// Tredive sekunder er valgt, fordi det er langt nok til at lade en
    /// diktering, en mikrofonprøve og et «kan I høre mig» passere, og kort nok
    /// til, at man stadig er i begyndelsen af mødet, når beskeden kommer.
    /// </summary>
    private static readonly TimeSpan Foer = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _ur = new() { Interval = TimeSpan.FromSeconds(5) };

    /// <summary>Hvad der allerede er spurgt om — nøgle og starttidspunkt.</summary>
    private readonly HashSet<string> _spurgt = new();

    private readonly Func<bool> _optagerAllerede;
    private readonly Action _startOptagelse;
    private readonly Func<System.Windows.Window?> _ejer;

    private MoedevagtPopup? _aaben;

    public Moedevagt(Func<bool> optagerAllerede, Action startOptagelse,
                     Func<System.Windows.Window?> ejer)
    {
        _optagerAllerede = optagerAllerede;
        _startOptagelse = startOptagelse;
        _ejer = ejer;

        _ur.Tick += (_, _) => Kig();
    }

    /// <summary>
    /// Starter eller stopper vagten efter indstillingen.
    ///
    /// Kaldes både ved opstart og hver gang indstillingen ændres, så et hak,
    /// der bliver sat, virker med det samme — ikke først efter en genstart.
    /// </summary>
    public void Opdater()
    {
        if (AppSettings.Current.MoedevagtTil)
        {
            if (!_ur.IsEnabled) _ur.Start();
            return;
        }

        _ur.Stop();

        // En besked, der staar paa skaermen, naar vagten slaas fra, skal vaek.
        // Ellers ser det ud, som om indstillingen ikke virkede.
        _aaben?.Close();
        _aaben = null;
    }

    private void Kig()
    {
        if (_aaben is not null) return;
        if (_optagerAllerede()) return;

        var s = AppSettings.Current;
        var nu = DateTime.Now;

        foreach (var b in Mikrofonvagt.IBrug())
        {
            if (s.MoedevagtAldrig.Any(a => a.Equals(b.Noegle, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (nu - b.Startet < Foer) continue;

            // Starttidspunktet er en del af noeglen. Saadan spoerges der én
            // gang pr. gang, programmet aabner mikrofonen — ikke én gang pr.
            // program, hvilket ville betyde, at et nej i formiddags gjaldt
            // resten af dagen.
            var id = $"{b.Noegle}|{b.Startet:O}";
            if (!_spurgt.Add(id)) continue;

            Spoerg(b);
            return;
        }
    }

    private void Spoerg(Mikrofonbruger b)
    {
        var p = new MoedevagtPopup(b);

        p.Optag += () => _startOptagelse();

        p.Aldrig += noegle =>
        {
            var s = AppSettings.Current;
            if (!s.MoedevagtAldrig.Contains(noegle)) s.MoedevagtAldrig.Add(noegle);
            s.Save();
        };

        p.Closed += (_, _) => _aaben = null;

        _aaben = p;

        // Ejeren saettes, hvis hovedvinduet er fremme. Er det skjult — det er
        // det under en optagelse — ville en ejer, der ikke vises, tage
        // beskeden med sig ned.
        var ejer = _ejer();
        if (ejer is { IsVisible: true }) p.Owner = ejer;

        p.Show();
    }
}
