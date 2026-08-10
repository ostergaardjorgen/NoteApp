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

        Loaded += (_, _) => { Indlaes(); _timer.Start(); };
        Unloaded += (_, _) => { _timer.Stop(); StopProber(); };
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

        StartProber();
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
        StartProber();
    }

    private void Hoejttaler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Hoejttalere.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.SpeakerId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Højttaler: {d.FriendlyName}";
        StartProber();
    }

    private void Genindlaes_Click(object sender, RoutedEventArgs e)
    {
        StopProber();
        Indlaes();
        Status.Text = "Enhedslisten er opdateret.";
    }

    // ---------------------------------------------------------------- målere

    private void StartProber()
    {
        StopProber();

        if (Mikrofoner.SelectedItem is DeviceInfo m)
            _mikProbe = WasapiLevelProbe.Start(m.Id, loopback: false);

        if (Hoejttalere.SelectedItem is DeviceInfo h)
            _hoejtProbe = WasapiLevelProbe.Start(h.Id, loopback: true);
    }

    private void StopProber()
    {
        _mikProbe?.Dispose();
        _hoejtProbe?.Dispose();
        _mikProbe = null;
        _hoejtProbe = null;
    }

    private void OpdaterMaalere()
    {
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
