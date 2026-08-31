using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// En lille boble nederst på skærmen, der viser, at der lyttes.
/// </summary>
/// <remarks>
/// VÅGEORDET BRUGES, MENS MAN STÅR I ET ANDET PROGRAM.
///
/// Bjælken nederst i appens eget vindue kunne derfor ikke ses af den, der
/// brugte den: man sagde «Hej Pia» og havde ingen måde at vide, om den hørte
/// efter, eller om der skete noget bagefter.
///
/// Boblen ligger over alt andet, tager ikke fokus, og forsvinder af sig selv.
/// Herfra kan lytningen også slås fra — at skulle finde appen frem for at få
/// ro er for meget besvær til, at nogen gør det.
/// </remarks>
public partial class Lytteboble : Window
{
    /// <summary>Kaldes, når brugeren slår lytningen til eller fra herfra.</summary>
    public Action<bool>? Skiftet { get; set; }

    private DispatcherTimer? _skjulUr;
    private Storyboard? _puls;

    public Lytteboble()
    {
        InitializeComponent();

        // Prikken puster. Et stillestaaende ikon siger ikke, om noget er i
        // gang - og det er praecis dét, boblen findes for at vise.
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.25,
            Duration = TimeSpan.FromMilliseconds(900),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };

        Storyboard.SetTarget(anim, Prik);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));

        _puls = new Storyboard();
        _puls.Children.Add(anim);

        // Boblen maa kunne flyttes. Staar den i vejen for netop det felt, man
        // dikterer i, er den et problem frem for en hjaelp.
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        };
    }

    /// <summary>
    /// Viser boblen med en besked.
    /// </summary>
    /// <param name="besked">Hvad der sker lige nu.</param>
    /// <param name="pulser">Puster prikken? Sandt, mens der lyttes eller optages.</param>
    /// <param name="skjulEfter">Skjul igen efter så lang tid. Null = bliv stående.</param>
    public void Vis(string besked, bool pulser, TimeSpan? skjulEfter)
    {
        Besked.Text = besked;

        if (!IsVisible)
        {
            Placer();
            Show();
        }

        // Topmost skal saettes IGEN ved hver visning. Et andet program, der
        // ogsaa vil ligge oeverst, kan have skubbet os ned - og en boble bag
        // et browservindue er det samme som ingen boble.
        Topmost = false;
        Topmost = true;

        if (pulser) _puls?.Begin();
        else { _puls?.Stop(); Prik.Opacity = 1.0; }

        _skjulUr?.Stop();
        _skjulUr = null;

        if (skjulEfter is not { } tid) return;

        _skjulUr = new DispatcherTimer { Interval = tid };
        _skjulUr.Tick += (_, _) => { _skjulUr?.Stop(); _skjulUr = null; Skjul(); };
        _skjulUr.Start();
    }

    public void Skjul()
    {
        _puls?.Stop();
        _skjulUr?.Stop();
        _skjulUr = null;
        Hide();
    }

    /// <summary>
    /// Sætter knappens udseende efter, om der lyttes.
    /// </summary>
    public void VisLytning(bool til)
    {
        // IKONET VISER, HVAD KNAPPEN GOER - ikke hvilken tilstand man staar
        // i. Det modsatte er den klassiske forveksling, og den koster et
        // fejlklik hver gang. Lyttes der, goer knappen pause; er der pause,
        // starter den igen.
        //
        // TEGNET OG IKKE SAT MED EN SKRIFTTYPE. Her stod to kodepunkter fra
        // Segoe MDL2, og paa skaermen kom der to TOMME FIRKANTER. En figur,
        // der er tegnet, kan ikke mangle - uanset hvilke skrifttyper der er
        // installeret paa maskinen.
        Pausetegn.Visibility = til ? Visibility.Visible : Visibility.Collapsed;
        Starttegn.Visibility = til ? Visibility.Collapsed : Visibility.Visible;

        var navn = Sprog.T(til ? "lytteboble.pause" : "lytteboble.start");
        PauseKnap.ToolTip = navn;
        System.Windows.Automation.AutomationProperties.SetName(PauseKnap, navn);
    }

    /// <summary>
    /// Nederst i midten på den skærm, musen er på.
    /// </summary>
    /// <remarks>
    /// Nederst, fordi det er dér, den er mindst i vejen: man skriver foroven.
    /// Og på musens skærm, ikke på den primære — den, der sidder med to
    /// skærme, arbejder på den ene ad gangen.
    /// </remarks>
    private void Placer()
    {
        UpdateLayout();

        var arbejde = SystemParameters.WorkArea;

        Left = arbejde.Left + (arbejde.Width - ActualWidth) / 2;
        Top = arbejde.Bottom - ActualHeight - 48;
    }

    private void Pause_Klik(object sender, RoutedEventArgs e)
    {
        var v = AppSettings.Current;
        var nyTilstand = !v.VaageordTil;

        v.VaageordTil = nyTilstand;
        v.Save();

        VisLytning(nyTilstand);
        Skiftet?.Invoke(nyTilstand);

        Vis(Sprog.T(nyTilstand ? "lytteboble.lytter_igen" : "lytteboble.sat_paa_pause"),
            pulser: nyTilstand,
            skjulEfter: TimeSpan.FromSeconds(nyTilstand ? 3 : 6));
    }

    /// <summary>Viser bølgerne i stedet for prikken — eller omvendt.</summary>
    /// <remarks>
    /// Kun ÉN af dem ad gangen. To ting, der begge fortæller om lyd, læses
    /// som støj i stedet for som et svar.
    /// </remarks>
    public void VisBoelger(bool til)
    {
        try
        {
            Boelger.Visibility = til ? Visibility.Visible : Visibility.Collapsed;
            Prik.Visibility = til ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception)
        {
            // En boble, der ikke kan tegnes, maa ikke kunne stoppe en
            // diktering. Bjaelken i vinduet siger det samme.
        }
    }

    /// <summary>Sætter søjlernes højde. Nyeste værdi står yderst til højre.</summary>
    public void SaetBoelger(IReadOnlyList<double> niveauer)
    {
        try
        {
            var felter = new[] { B0, B1, B2, B3, B4, B5, B6 };

            for (var i = 0; i < felter.Length && i < niveauer.Count; i++)
                felter[i].Height = 4.0 + niveauer[i] * 14.0;
        }
        catch (Exception)
        {
            // Se VisBoelger.
        }
    }

    /// <summary>Siger til, når brugeren lukker boblen med krydset.</summary>
    public Action? Lukket { get; set; }

    /// <summary>
    /// Krydset lukker BOKSEN — ikke lytningen.
    /// </summary>
    /// <remarks>
    /// Lytningen fortsætter, og den grønne bjælke nederst i appen bliver
    /// stående og siger det. Derfra kan boblen hentes tilbage.
    ///
    /// Skal lytningen stoppe, er svaret pause ved siden af — og så stopper
    /// den også rigtigt, i stedet for at fortsætte usynligt.
    /// </remarks>
    private void Luk_Klik(object sender, RoutedEventArgs e)
    {
        Skjul();
        Lukket?.Invoke();
    }
}
