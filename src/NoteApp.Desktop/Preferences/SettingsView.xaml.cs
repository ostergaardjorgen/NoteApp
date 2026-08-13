using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Preferences;

/// <summary>
/// Valg af mikrofon og højttaler.
///
/// Speccens afsnit 2 sagde oprindeligt, at der ikke skulle være dropdowns —
/// enheder skulle tages fra Windows' standard, og appen skulle kun vise hvad
/// der blev valgt. Den beslutning er omgjort 10. august 2026: med en
/// Jabra-højttaler, en laptopmikrofon og et headset tilsluttet er Windows'
/// standard ofte den forkerte, og uden et valg her skal man ud i Windows'
/// lydindstillinger midt i en mødeforberedelse.
///
/// Måleren er ikke pynt. Den er hele grunden til, at valget kan træffes
/// rigtigt: man vælger en enhed, siger noget, og ser om den rører sig.
/// </summary>
public partial class SettingsView : UserControl
{
    private readonly DispatcherTimer _timer;

    private WasapiLevelProbe? _mikProbe;
    private WasapiLevelProbe? _hoejtProbe;

    public SettingsView()
    {
        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => OpdaterMaalere();

        Loaded += (_, _) => { Indlaes(); VisAutostart(); };
        Unloaded += (_, _) => { _timer.Stop(); StopProber(); };
    }

    // ---------------------------------------------------------- klar til møde

    private void VisAutostart()
    {
        StartMedWindows.IsChecked = Autostart.ErSlaaetTil();

        AutostartStatus.Text = Autostart.ErForaeldet()
            ? "Bemærk: den gemte opstart peger på en anden placering, end appen kører fra nu. " +
              "Slå den fra og til igen, så bliver stien rettet."
            : "Appen åbner ikke et vindue ved opstart — den ligger klar, indtil du trykker genvejstasten.";

        VisGenveje();
    }

    private bool _indlæserGenveje;

    /// <summary>
    /// Listen over genvejskombinationer, med hvad der faktisk er ledigt lige
    /// nu. Ledigheden PRØVES af frem for at blive gættet — det er forskelligt
    /// fra maskine til maskine, og et valg, der ikke kan lade sig gøre, skal
    /// ikke stå som om det kan.
    /// </summary>
    private void VisGenveje()
    {
        var vindue = Window.GetWindow(this);
        if (vindue is null) return;

        _indlæserGenveje = true;

        var valgt = AppSettings.Current.HotkeyId;
        var rækker = GlobalHotkey.Muligheder.Select(m =>
        {
            var ledig = m.Id == valgt || GlobalHotkey.ErLedig(m, vindue);
            return new
            {
                m.Id,
                m.Hvorfor,
                Visning = ledig ? m.Navn : $"{m.Navn}  —  optaget af et andet program",
                Ledig = ledig
            };
        }).ToList();

        Genveje.ItemsSource = rækker;
        Genveje.SelectedItem = rækker.FirstOrDefault(r => r.Id == valgt)
                            ?? rækker.FirstOrDefault(r => r.Ledig);

        _indlæserGenveje = false;

        var v = Genveje.SelectedItem as dynamic;
        GenvejForklaring.Text = v is null ? "" : (string)v.Hvorfor;
    }

    private void Genvej_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_indlæserGenveje || Genveje.SelectedItem is null) return;

        var valg = (dynamic)Genveje.SelectedItem!;
        GenvejForklaring.Text = (string)valg.Hvorfor;

        if (!(bool)valg.Ledig)
        {
            Status.Text = "Den kombination er taget af et andet program. Vælg en anden.";
            return;
        }

        AppSettings.Current.HotkeyId = (string)valg.Id;
        AppSettings.Current.Save();

        // Registreringen skal ske med det samme. Et valg, der foerst virker
        // efter en genstart, er et valg, man tror er i kraft.
        if (Window.GetWindow(this) is MainWindow hoved) hoved.TilslutGenvej();

        Status.Text = $"Lynstart er nu {(string)valg.Visning}.";
    }

    private void Autostart_Klik(object sender, RoutedEventArgs e)
    {
        var til = StartMedWindows.IsChecked == true;
        var fejl = Autostart.Saet(til);

        if (fejl is not null)
        {
            // Feltet skal vise virkeligheden, ikke oensket. Et haevet flueben,
            // der ikke gjorde noget, er en loegn, man opdager en morgen.
            StartMedWindows.IsChecked = Autostart.ErSlaaetTil();
            Status.Text = $"Kunne ikke ændre opstarten: {fejl}";
            return;
        }

        Status.Text = til
            ? "NoteApp starter nu med Windows og ligger klar til genvejstasten."
            : "NoteApp starter ikke længere med Windows.";

        VisAutostart();
    }

    private void Indlaes()
    {
        var mikrofoner = AudioDevices.Microphones();
        var hoejttalere = AudioDevices.Speakers();

        // Visningen styres af ItemTemplate i XAML. DisplayMemberPath maa IKKE
        // ogsaa saettes — WPF kaster paa at have begge, og skaermen ville
        // vaelte i det oejeblik den aabnes.
        Mikrofoner.ItemsSource = mikrofoner;
        Hoejttalere.ItemsSource = hoejttalere;

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var mikFallback);
        var hoejt = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out var hoejtFallback);

        Mikrofoner.SelectedItem = mikrofoner.FirstOrDefault(d => d.Id == mik?.Id);
        Hoejttalere.SelectedItem = hoejttalere.FirstOrDefault(d => d.Id == hoejt?.Id);

        // Er den valgte enhed vaek — headsettet er taget ud — skal det staa
        // her, ikke opdages naar optagelsen er slut.
        Vis(MikAdvarsel, mikFallback,
            "Den mikrofon, du havde valgt, er ikke tilsluttet længere. Appen bruger Windows' standard i stedet.");
        Vis(HoejtAdvarsel, hoejtFallback,
            "Den højttaler, du havde valgt, er ikke tilsluttet længere. Appen bruger Windows' standard i stedet.");

        if (mikrofoner.Count == 0) MikStatus.Text = "ingen mikrofon fundet";
        if (hoejttalere.Count == 0) HoejtStatus.Text = "ingen afspilningsenhed fundet";

    }

    private static void Vis(TextBlock felt, bool synlig, string tekst)
    {
        felt.Visibility = synlig ? Visibility.Visible : Visibility.Collapsed;
        felt.Text = synlig ? tekst : "";
    }

    // ------------------------------------------------------------------ valg

    private void Mikrofon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Mikrofoner.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.MicrophoneId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Mikrofon: {d.FriendlyName}";
    }

    private void Hoejttaler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Hoejttalere.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.SpeakerId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Højttaler: {d.FriendlyName}";
    }

    private void Genindlaes_Click(object sender, RoutedEventArgs e)
    {
        StopProber();
        Indlaes();
        Status.Text = "Enhedslisten er opdateret.";
    }

    // ---------------------------------------------------------------- målere

    /// <summary>
    /// Åbner mikrofon og højttaler for at MÅLE niveauet — kun når brugeren
    /// har bedt om det.
    ///
    /// Før kørte målingen, så snart man åbnede Indstillinger, og blev ved,
    /// så længe man var der. Appen lyttede altså uden at nogen havde trykket
    /// optag. Det er forkert i en app, hvis hele løfte er, at man ved præcis,
    /// hvornår der bliver optaget — og det er også skidt for optagelser: to
    /// programmer om den samme enhed er sjældent gratis.
    ///
    /// Måleren slukker af sig selv efter et stykke tid. En prøve, man skal
    /// huske at stoppe, bliver ikke stoppet.
    /// </summary>
    private void StartProber()
    {
        StopProber();

        if (Mikrofoner.SelectedItem is DeviceInfo m)
            _mikProbe = WasapiLevelProbe.Start(m.Id, loopback: false);

        if (Hoejttalere.SelectedItem is DeviceInfo h)
            _hoejtProbe = WasapiLevelProbe.Start(h.Id, loopback: true);

        _timer.Start();

        _proeveSlut = DateTime.Now.AddSeconds(20);
        ProeveKnap.Content = "■ Stop prøven";
        MikStatus.Text = "Sig noget — måleren skal røre sig";
    }

    private DateTime _proeveSlut = DateTime.MinValue;

    private void Proeve_Click(object sender, RoutedEventArgs e)
    {
        if (_mikProbe is null && _hoejtProbe is null) StartProber();
        else StopProber();
    }

    private void StopProber()
    {
        _mikProbe?.Dispose();
        _hoejtProbe?.Dispose();
        _mikProbe = null;
        _hoejtProbe = null;

        _timer.Stop();
        _proeveSlut = DateTime.MinValue;

        if (ProeveKnap is not null) ProeveKnap.Content = "▶ Prøv mikrofon og højttaler";
        if (MikNiveau is not null) MikNiveau.Width = 0;
        if (HoejtNiveau is not null) HoejtNiveau.Width = 0;
        if (MikStatus is not null) MikStatus.Text = "Måleren kører kun, mens du prøver";
        if (HoejtStatus is not null) HoejtStatus.Text = "";
    }

    private void OpdaterMaalere()
    {
        // Proeven slukker af sig selv. En maaler, der koerer videre, er en
        // mikrofon, der staar aaben uden grund.
        if (_proeveSlut != DateTime.MinValue && DateTime.Now > _proeveSlut) { StopProber(); return; }

        Tegn(_mikProbe, MikNiveau, MikStatus, "Sig noget — måleren skal røre sig");
        Tegn(_hoejtProbe, HoejtNiveau, HoejtStatus, "Afspil lyd — måleren skal røre sig");
    }

    private void Tegn(WasapiLevelProbe? probe, Border bjaelke, TextBlock status, string naarTavs)
    {
        if (probe is null) return;

        if (probe.Error is not null)
        {
            status.Text = "kan ikke aflæses";
            bjaelke.Width = 0;
            return;
        }

        var top = probe.ReadPeak();
        var bredde = bjaelke.Parent is FrameworkElement f && f.ActualWidth > 0 ? f.ActualWidth : 300;
        bjaelke.Width = bredde * PeakMeter.ToMeterScale(top);

        var tavs = top < AudioDevices.SilenceThreshold;
        bjaelke.Background = tavs
            ? (Brush)FindResource("TekstMeget")
            : (Brush)FindResource("Godkendt");
        status.Text = tavs ? naarTavs : $"{PeakMeter.ToDb(top):0.0} dBFS";
    }
}
