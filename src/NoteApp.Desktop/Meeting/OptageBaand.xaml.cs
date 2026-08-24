using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Båndet, der bliver stående, mens der optages.
///
/// Det ejer ingenting. Optagelsen hører til <see cref="MeetingView"/>, og
/// båndet siger kun, hvad der blev trykket på. Ellers ville der være to
/// steder, der kunne stoppe et møde, og de to kunne blive uenige.
/// </summary>
public partial class OptageBaand : Window
{
    public event Action? Pause;
    public event Action? Stop;
    public event Action? Annuller;
    public event Action<string>? Note_Skrevet;

    /// <summary>
    /// Der blev trykket paa kontakten «Vis appen» / «Skjul appen».
    ///
    /// Baandet ved ikke selv, hvilket vindue der skal frem - det ejer
    /// ingenting. Den, der lytter, skifter og melder tilbage med
    /// <see cref="SaetAppSynlig"/>, saa knappen aldrig kan komme til at vise
    /// en anden tilstand end den, vinduet faktisk staar i.
    /// </summary>
    public event Action? SkiftAppVisning;

    private readonly Storyboard _blink;

    public OptageBaand()
    {
        InitializeComponent();

        // Prikken blinker. En stillestående prik kan betyde hvad som helst -
        // ogsaa at programmet er gaaet i staa. Et blink betyder, at der
        // stadig sker noget.
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.25,
            Duration = TimeSpan.FromSeconds(0.9),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(anim, Prik);
        Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));

        _blink = new Storyboard();
        _blink.Children.Add(anim);

        Loaded += (_, _) =>
        {
            _blink.Begin();
            Note.TextChanged += (_, _) =>
                NotePladsholder.Visibility = Note.Text.Length == 0
                    ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    // ------------------------------------------------------------ placering

    /// <summary>
    /// Lægger båndet, hvor det stod sidst — eller øverst midt på den skærm,
    /// vinduet <paramref name="nær"/> står på.
    ///
    /// DER KONTROLLERES, AT PLADSEN FINDES.
    ///
    /// En gemt placering kan pege på en skærm, der ikke er tilsluttet mere —
    /// en bærbar, der har været i dok. Uden kontrollen ville båndet ligge
    /// uden for billedet, og så ser det ud, som om optagelsen ikke startede.
    /// </summary>
    public void Placer(Window? nær)
    {
        var s = AppSettings.Current;

        if (s.BaandX is { } x && s.BaandY is { } y && PaaEnSkaerm(x, y))
        {
            Left = x;
            Top = y;
            return;
        }

        // SizeToContent giver foerst en bredde, naar vinduet er maalt.
        UpdateLayout();

        var skaerm = nær is null
            ? SystemParameters.WorkArea
            : new Rect(nær.Left, nær.Top, nær.Width, nær.Height);

        Left = skaerm.Left + (skaerm.Width - ActualWidth) / 2;
        Top = skaerm.Top + 28;
    }

    /// <summary>
    /// Er punktet inden for et skærmbillede, der findes lige nu?
    ///
    /// Der regnes med et hjørne og ikke hele båndet: flyttes en skærm, kan
    /// kanterne have rykket sig lidt, og et bånd, der stikker tyve pixel ud,
    /// er ikke et problem — et bånd på en skærm, der er væk, er.
    /// </summary>
    private static bool PaaEnSkaerm(double x, double y)
    {
        var virt = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return virt.Contains(new Point(x + 40, y + 20));
    }

    private void Gem_Placering()
    {
        try
        {
            var s = AppSettings.Current;
            s.BaandX = Left;
            s.BaandY = Top;
            s.Save();
        }
        catch (Exception)
        {
            // En placering, der ikke kunne gemmes, maa ikke koste et moede.
        }
    }

    /// <summary>
    /// Træk i fladen flytter båndet.
    ///
    /// Felter og knapper markerer selv klikket som håndteret, så de bliver
    /// ved at virke — det her er kun det, ingen andre tog.
    /// </summary>
    private void Flade_Traek(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try { DragMove(); }
        catch (InvalidOperationException) { return; }

        Gem_Placering();
    }

    // ------------------------------------------------------------- tilstand

    public void SaetTid(string tid) => Ur.Text = tid;

    /// <summary>
    /// En kort melding ved siden af uret — fx at et webinar er ved at være
    /// stille længe nok til at stoppe.
    ///
    /// Den står, hvor tilstanden står, og ikke i en ny rude: båndet er dét,
    /// der er tilbage af appen, mens der optages, og det skal ikke vokse.
    /// </summary>
    public void Meld(string tekst)
    {
        if (tekst.Length == 0) { SaetPause(Tilstand.Text == "PÅ PAUSE"); return; }

        Tilstand.Text = tekst;
    }

    public void SaetNoter(int antal) =>
        NoteTaeller.Text = antal == 0 ? "" : antal == 1 ? "1 note" : $"{antal} noter";

    /// <summary>Skifter mellem «optager» og «på pause» — i tekst, farve og blink.</summary>
    public void SaetPause(bool paaPause)
    {
        if (paaPause)
        {
            _blink.Stop();
            Prik.Opacity = 1.0;
            Prik.Fill = new SolidColorBrush(Color.FromRgb(0xE8, 0xA3, 0x3D));
            Tilstand.Text = "PÅ PAUSE";
            PauseKnap.Content = "▶";
            PauseKnap.ToolTip = "Fortsæt optagelsen.";
        }
        else
        {
            Prik.Fill = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
            Tilstand.Text = "OPTAGER";
            PauseKnap.Content = "❚❚";
            PauseKnap.ToolTip = "Hold pause. Der optages intet, før du fortsætter.";
            _blink.Begin();
        }
    }

    /// <summary>
    /// Skriver paa kontakten, hvad et klik goer NU.
    ///
    /// Kaldes af den, der rent faktisk skjulte eller viste vinduet - aldrig af
    /// klikket selv. Gik visningen galt, staar knappen dermed stadig og siger
    /// sandheden om, hvor vinduet er.
    /// </summary>
    public void SaetAppSynlig(bool synlig)
    {
        if (synlig)
        {
            VisAppKnap.Content = "▲ Skjul appen";
            VisAppKnap.ToolTip = "Skjul appen igen. Optagelsen fortsætter, og båndet bliver liggende.";
        }
        else
        {
            VisAppKnap.Content = "▼ Vis appen";
            VisAppKnap.ToolTip = "Vis hele appen frem. Optagelsen fortsætter, og båndet bliver liggende.";
        }
    }

    // ------------------------------------------------------------- knapper

    private void VisApp_Klik(object sender, RoutedEventArgs e) => SkiftAppVisning?.Invoke();

    private void Pause_Klik(object sender, RoutedEventArgs e) => Pause?.Invoke();

    private void Stop_Klik(object sender, RoutedEventArgs e) => Stop?.Invoke();

    /// <summary>
    /// Kassér uden at gemme.
    ///
    /// DER SPØRGES FØRST, OG «BEHOLD» ER STANDARDVALGET.
    ///
    /// Knappen sidder ved siden af «Stop og gem» og sletter lyd, der ikke kan
    /// skaffes igen. Et fejlklik her er den værste fejl, appen kan lave, og
    /// den kan ikke fortrydes.
    /// </summary>
    private void Annuller_Klik(object sender, RoutedEventArgs e)
    {
        // Baandet ligger over alt andet. Uden det her ville spoergsmaalet
        // kunne havne BAG baandet, og saa ser appen laast ud.
        Topmost = false;

        var kasser = Dialogs.AppDialog.Spoerg(this,
            "Kassér optagelsen?",
            $"Optagelsen er {Ur.Text} lang og bliver slettet. Den kan ikke hentes frem igen.\n\n" +
            "Vil du beholde den, så tryk «Stop og gem» i stedet — du kan altid slette " +
            "mødet bagefter, når du har set, hvad der står i det.",
            godkend: "Kassér og slet",
            annuller: "Behold optagelsen",
            slags: Dialogs.Slags.Pas_paa,
            godkendErStandard: false);

        Topmost = true;

        if (kasser) Annuller?.Invoke();
    }

    private void Note_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var tekst = Note.Text.Trim();
        if (tekst.Length == 0) return;

        Note_Skrevet?.Invoke(tekst);
        Note.Clear();
        e.Handled = true;
    }

    /// <summary>
    /// Båndet lukkes kun af appen, ikke af Windows.
    ///
    /// Alt+F4 på båndet ville ellers stoppe optagelsen uden at gemme og uden
    /// at spørge — og der er ingen titellinje, så der er intet, der fortæller,
    /// at vinduet overhovedet kan lukkes.
    /// </summary>
    public void LukForAlvor()
    {
        _blink.Stop();
        _luk = true;
        Close();
    }

    private bool _luk;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_luk) e.Cancel = true;
        base.OnClosing(e);
    }
}
