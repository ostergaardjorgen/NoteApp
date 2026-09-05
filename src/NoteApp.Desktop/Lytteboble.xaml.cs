using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

    /// <summary>Der blev trykket «Aftale» i boblen.</summary>
    public Action? Aftale;

    /// <summary>Der blev trykket «Opgave» i boblen.</summary>
    public Action? Opgave;

    /// <summary>Der blev trykket «Kopiér» i boblen.</summary>
    public Action? Kopier;

    /// <summary>
    /// Viser eller skjuler de tre genveje.
    /// </summary>
    /// <remarks>
    /// DE ER DER KUN, NÅR DER ER EN TEKST. Boblen står også, mens der lyttes,
    /// og mens der skrives ud — tre knapper til noget, der ikke findes endnu,
    /// er tre knapper, der ikke virker.
    ///
    /// Boblen ændrer bredde, når de kommer og går. Den er sat til
    /// SizeToContent, så det sker af sig selv, men den skal placeres igen:
    /// den ligger centreret forneden, og en boble, der bliver bredere uden at
    /// flytte sig, står ikke længere på midten.
    /// </remarks>
    public void VisGenveje(bool vis)
    {
        var nyt = vis ? Visibility.Visible : Visibility.Collapsed;

        if (Genveje.Visibility == nyt) return;

        Genveje.Visibility = nyt;

        if (IsVisible)
        {
            UpdateLayout();
            Placer();
        }
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
        else
        {
            // OGSAA NAAR DEN ALLEREDE STAAR. Man kan have flyttet sig til en
            // anden skaerm, siden den kom frem, og en boble, der bliver
            // liggende paa den forrige, er lige saa vaek som ingen boble.
            Placer();
        }

        // Se Oeverst: et andet program, der ogsaa vil ligge oeverst, kan
        // have skubbet os ned - og en boble bag et browservindue er det samme
        // som ingen boble.
        Oeverst();

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
    /// <summary>
    /// Lægger boblen midt for neden — på den skærm, man arbejder på.
    /// </summary>
    /// <remarks>
    /// DEN LAA PAA DEN PRIMAERE SKAERM. <c>SystemParameters.WorkArea</c>
    /// kender kun én skærm, og sad man på den anden, dukkede boblen op ovre
    /// på den første — altså uden for det, man kiggede på. På to skærme
    /// betød det i praksis, at der «ikke skete noget».
    ///
    /// Nu findes skærmen ud fra det vindue, man står i. Er der ingen — er
    /// forgrundsvinduet lukket i samme øjeblik — bruges den primære, som før.
    ///
    /// Der regnes i PIXELS og tegnes i WPF-enheder. På en skærm med skalering
    /// er de ikke det samme, og uden omregningen ville boblen ligge for langt
    /// til højre og for langt nede, jo højere skaleringen var.
    /// </remarks>
    private void Placer()
    {
        UpdateLayout();

        var (venstre, top, bredde, hoejde) = Arbejdsomraade();

        Left = venstre + (bredde - ActualWidth) / 2;
        Top = top + hoejde - ActualHeight - 48;
    }

    private (double Venstre, double Top, double Bredde, double Hoejde) Arbejdsomraade()
    {
        try
        {
            var vindue = GetForegroundWindow();
            var skaerm = MonitorFromWindow(vindue, MONITOR_DEFAULTTONEAREST);

            var oplysning = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

            if (skaerm != IntPtr.Zero && GetMonitorInfo(skaerm, ref oplysning))
            {
                var m = PresentationSource.FromVisual(this)?.CompositionTarget?
                    .TransformFromDevice ?? Matrix.Identity;

                var oe = oplysning.rcWork;

                var hjoerne = m.Transform(new Point(oe.Left, oe.Top));
                var slut = m.Transform(new Point(oe.Right, oe.Bottom));

                return (hjoerne.X, hjoerne.Y, slut.X - hjoerne.X, slut.Y - hjoerne.Y);
            }
        }
        catch (Exception)
        {
            // Kan skaermen ikke findes, bruges den primaere. En boble det
            // forkerte sted er bedre end ingen boble.
        }

        var a = SystemParameters.WorkArea;
        return (a.Left, a.Top, a.Width, a.Height);
    }

    /// <summary>
    /// Løfter boblen op over alt andet — uden at tage fokus.
    /// </summary>
    /// <remarks>
    /// <c>Topmost</c> ALENE RAEKKER IKKE. Et andet program, der også ligger
    /// øverst — en videoafspiller, et delingsværktøj, en anden boble — kan
    /// have lagt sig oven på os, og WPF sætter kun flaget, når det ændrer
    /// sig. At slå det fra og til igen hjælper som regel, men ikke altid.
    ///
    /// SetWindowPos med HWND_TOPMOST flytter os forrest i rækken af
    /// topmost-vinduer HVER gang. SWP_NOACTIVATE er det, der gør, at
    /// markøren bliver i det felt, man dikterer ind i — uden det ville
    /// teksten lande et andet sted.
    /// </remarks>
    private void Oeverst()
    {
        Topmost = false;
        Topmost = true;

        try
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero) return;

            SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
        catch (Exception)
        {
            // Topmost ovenfor er sat. Resten er en ekstra sikring.
        }
    }

    private const int MONITOR_DEFAULTTONEAREST = 2;

    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr skaerm, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr efter,
        int x, int y, int cx, int cy, uint flag);

    private void Aftale_Klik(object sender, RoutedEventArgs e) => Aftale?.Invoke();

    private void Opgave_Klik(object sender, RoutedEventArgs e) => Opgave?.Invoke();

    private void Kopier_Klik(object sender, RoutedEventArgs e) => Kopier?.Invoke();

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
