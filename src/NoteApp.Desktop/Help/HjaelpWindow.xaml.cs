using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Help;

/// <summary>Én række i listen — enten et afsnit eller et søgetræf.</summary>
public sealed record Hjaelperaekke(string Id, string Titel, string Under);

/// <summary>
/// Hjælpen. Afsnittene til venstre, teksten til højre, søgning øverst.
///
/// DEN FØLGER SPROGET. Skiftes sproget, mens vinduet er åbent, bygges både
/// listen og teksten om — det er den samme hjælp, bare på det andet sprog.
///
/// DEN ER IKKE MODAL. Man slår noget op MENS man arbejder, og et vindue, der
/// spærrer for appen, tvinger en til at lukke hjælpen for at prøve det, man
/// lige har læst.
/// </summary>
public partial class HjaelpWindow : Window
{
    private static HjaelpWindow? _aabent;

    private readonly DispatcherTimer _pause;
    private string _valgt = "";

    private HjaelpWindow()
    {
        InitializeComponent();

        // SAMME PAUSE SOM I SOEGNINGEN I COCKPITTET. Uden den soeges der forfra
        // for hvert tastetryk - og ved «optagelse» er det ni soegninger, der
        // bliver smidt vaek.
        _pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _pause.Tick += (_, _) => { _pause.Stop(); Vis(); };

        Sprog.Aendret += Sprogskiftet;
        Closed += (_, _) => { Sprog.Aendret -= Sprogskiftet; _aabent = null; };

        Loaded += (_, _) => { Vis(); Felt.Focus(); };
    }

    /// <summary>
    /// Åbner hjælpen — eller henter den frem, hvis den allerede er åben.
    ///
    /// To hjælpevinduer ved siden af hinanden er ikke to ting, man kan bruge
    /// til noget. Den ene ville stå med et andet afsnit og se ud som en fejl.
    /// </summary>
    public static void Aabn(Window? ejer, string? afsnit = null)
    {
        if (_aabent is not null)
        {
            if (_aabent.WindowState == WindowState.Minimized)
                _aabent.WindowState = WindowState.Normal;

            _aabent.Activate();

            if (afsnit is not null) _aabent.Gaa(afsnit);
            return;
        }

        _aabent = new HjaelpWindow { Owner = ejer };

        if (afsnit is not null) _aabent.Loaded += (_, _) => _aabent.Gaa(afsnit);

        _aabent.Show();
    }

    /// <summary>Springer til et bestemt afsnit — til «hjælp om det her»-knapper senere.</summary>
    private void Gaa(string id)
    {
        Felt.Text = "";
        Vis();

        var raekke = (Liste.ItemsSource as List<Hjaelperaekke>)?
            .FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

        if (raekke is not null) Liste.SelectedItem = raekke;
    }

    private void Sprogskiftet()
    {
        // Hjaelpen laeses forfra: det er andre filer, der skal vises.
        Hjaelp.Genindlaes();

        var stod = _valgt;
        Vis();

        var raekke = (Liste.ItemsSource as List<Hjaelperaekke>)?
            .FirstOrDefault(r => string.Equals(r.Id, stod, StringComparison.OrdinalIgnoreCase));

        if (raekke is not null) Liste.SelectedItem = raekke;
    }

    // ===================== LISTEN =====================

    private void Vis()
    {
        var soeg = Felt.Text.Trim();

        Pladsholder.Visibility = soeg.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RydKnap.Visibility = soeg.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        List<Hjaelperaekke> raekker;

        if (soeg.Length == 0)
        {
            raekker = Hjaelp.Alle()
                .Select(a => new Hjaelperaekke(a.Id, a.Titel, a.Undertitel))
                .ToList();

            Traeftal.Visibility = Visibility.Collapsed;
        }
        else
        {
            var traef = Hjaelp.Soeg(soeg);

            raekker = traef
                .Select(t => new Hjaelperaekke(t.Afsnit.Id, t.Afsnit.Titel, t.Uddrag))
                .ToList();

            Traeftal.Text = traef.Count switch
            {
                0 => Sprog.T("hjaelp.ingentraef"),
                1 => Sprog.T("hjaelp.ettraef"),
                _ => Sprog.T("hjaelp.traef", traef.Count)
            };

            Traeftal.Visibility = Visibility.Visible;
        }

        // Var der noget valgt, og er det stadig paa listen, bliver det valgt.
        // Ellers vaelges det foerste - man skal ikke selv klikke for at komme
        // videre efter en soegning.
        var beholdt = raekker.FirstOrDefault(
            r => string.Equals(r.Id, _valgt, StringComparison.OrdinalIgnoreCase));

        Liste.ItemsSource = raekker;
        Liste.SelectedItem = beholdt ?? raekker.FirstOrDefault();

        if (raekker.Count == 0) VisIntet(soeg);
    }

    private void VisIntet(string soeg)
    {
        Visning.Visibility = Visibility.Collapsed;
        IntetFundet.Text = Sprog.T("hjaelp.intet", soeg);
        IntetFundet.Visibility = Visibility.Visible;
    }

    private void Liste_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Liste.SelectedItem is not Hjaelperaekke raekke) return;

        _valgt = raekke.Id;

        var afsnit = Hjaelp.Find(raekke.Id);
        if (afsnit is null) return;

        // Titlen kommer med i selve teksten, saa den ogsaa staar der, naar man
        // ruller ned - og saa listen ikke er det eneste sted, man kan se,
        // hvad man laeser.
        var markdown = "# " + afsnit.Titel + "\n\n"
                     + (afsnit.Undertitel.Length > 0 ? "*" + afsnit.Undertitel + "*\n\n" : "")
                     + afsnit.Tekst;

        Visning.Document = Markdownvisning.Byg(markdown, Application.Current.Resources);
        Visning.Visibility = Visibility.Visible;
        IntetFundet.Visibility = Visibility.Collapsed;

        Rude.ScrollToTop();
    }

    // ===================== SØGEFELTET =====================

    private void Felt_Aendret(object sender, TextChangedEventArgs e)
    {
        _pause.Stop();
        _pause.Start();
    }

    private void Felt_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            // Escape rydder soegningen. Er der ingenting at rydde, lukker den
            // vinduet - det er den samme tast, man ellers ville lede efter.
            if (Felt.Text.Length > 0) { Ryd(); e.Handled = true; }
            else Close();
            return;
        }

        // Pil ned flytter ned i listen uden at slippe tastaturet.
        if (e.Key is Key.Down or Key.Enter && Liste.Items.Count > 0)
        {
            _pause.Stop();
            Vis();

            Liste.Focus();
            if (Liste.SelectedIndex < 0) Liste.SelectedIndex = 0;

            e.Handled = true;
        }
    }

    private void Ryd_Klik(object sender, RoutedEventArgs e) => Ryd();

    private void Ryd()
    {
        Felt.Text = "";
        _pause.Stop();
        Vis();
        Felt.Focus();
    }
}
