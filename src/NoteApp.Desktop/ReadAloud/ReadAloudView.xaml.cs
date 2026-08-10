using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.ReadAloud;

/// <summary>
/// Oplæsningsstudiet: teleprompter plus optager.
///
/// Formålet er ikke at være pæn, men at gøre det svært at ødelægge testen.
/// De to fejl, der koster hele optagelsen, er at læse for hurtigt (så der
/// ikke er 15-20 minutters lyd at måle på) og at glemme, hvilken blok man er
/// i (så transskriptionen ikke kan holdes op mod facitlisten). Begge dele
/// vises hele tiden, og blokskift skrives som bogmærker i notes.jsonl.
/// </summary>
public partial class ReadAloudView : UserControl
{
    private readonly ScriptDocument _script;
    private readonly DispatcherTimer _timer;

    private RecordingSession? _session;
    private int _afsnitIndex;
    private int _sidsteBlok = -1;

    public ReadAloudView()
    {
        InitializeComponent();

        _script = ScriptDocument.Load();

        Vejledning.ItemsSource = _script.Instructions;
        NavneAdvarsel.Text =
            $"Teksten er på {_script.TotalWords} ord — cirka {_script.EstimatedDuration.TotalMinutes:0} minutter " +
            "ved 120 ord i minuttet. Har du ikke skiftet de opdigtede navne ud med rigtige kollegaer og kunder, " +
            "så gør det først: det er navnene, testen skal afsløre.";

        var kilde = ScriptDocument.DiskPath();
        TekstKilde.Text = kilde is null
            ? "Teksten læses fra den kopi, der er indlejret i appen."
            : $"Teksten læses fra {kilde} — ret filen, og genstart appen for at se ændringen.";

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        MikrofonNavn.Text = mik?.FriendlyName ?? "ingen mikrofon fundet";
        OptagKnap.IsEnabled = mik is not null;
        Status.Text = mik is null
            ? "Der er ingen mikrofon. Tilslut en, og genstart appen."
            : $"{_script.Blocks.Count} blokke, {_script.Paragraphs.Count} afsnit. Optagelsen gemmes i {UserDataPaths.Meetings}";

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Opdater();

        Loaded += (_, _) => Focus();
    }

    public bool IsRecording => _session?.IsRecording == true;

    // ---------------------------------------------------------------- optag

    private void Optag_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) StartOptagelse();
        else StopOptagelse();
    }

    private void StartOptagelse()
    {
        // Den valgte mikrofon fra Indstillinger. Er den taget ud siden sidst,
        // falder vi tilbage paa Windows' standard — men siger det foerst, saa
        // man ikke opdager det efter tyve minutters oplaesning.
        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var fallback);
        if (mik is null)
        {
            MessageBox.Show("Ingen mikrofon fundet.", "Kan ikke optage", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (fallback)
        {
            var svar = MessageBox.Show(
                $"Den mikrofon, du havde valgt under Indstillinger, er ikke tilsluttet.\n\n" +
                $"Der optages i stedet fra: {mik.FriendlyName}\n\nFortsæt?",
                "Mikrofonen er skiftet", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (svar != MessageBoxResult.OK) return;
        }

        // Oplæsning er per definition et fysisk møde: ét spor, kun mikrofonen.
        // Der er ingen anden part, og derfor intet loopback-spor at fejle på.
        _session = RecordingSession.Create(MeetingType.Physical, "Fase0-oplaesning", mik, null);
        _session.IncidentOccurred += i => Dispatcher.Invoke(() =>
            Status.Text = $"Hændelse ved {TimeSpan.FromSeconds(i.AtSeconds):mm\\:ss}: {i.What}");

        _session.Start();

        _afsnitIndex = 0;
        _sidsteBlok = -1;
        VejledningPanel.Visibility = Visibility.Collapsed;
        LaesePanel.Visibility = Visibility.Visible;
        OptagKnap.Content = "■ Stop og gem";
        NaesteKnap.IsEnabled = true;
        ForrigeKnap.IsEnabled = false;
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        VisAfsnit();
        _timer.Start();
        Focus();
    }

    private void StopOptagelse()
    {
        if (_session is null) return;

        _timer.Stop();

        var filer = _session.Stop();
        var mappe = _session.SessionDir;
        var længde = _session.Elapsed;
        var noter = _session.Notebook.Notes.Count;

        _session.Dispose();
        _session = null;

        OptagKnap.Content = "● Start optagelse";
        NaesteKnap.IsEnabled = false;
        ForrigeKnap.IsEnabled = false;
        LaesePanel.Visibility = Visibility.Collapsed;
        VejledningPanel.Visibility = Visibility.Visible;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");
        BlokTitel.Text = "Optagelsen er gemt";
        BlokUnder.Text = $"{længde:hh\\:mm\\:ss} lyd, {noter} blokmærker";

        var kort = længde < TimeSpan.FromMinutes(15);
        Status.Text = kort
            ? $"ADVARSEL: kun {længde:mm\\:ss}. Gaten har brug for 15-20 minutter for at give brugbare tal."
            : $"{længde:mm\\:ss} optaget — det er nok til gaten.";

        var spor = filer.Count > 0 ? string.Join(", ", filer.Values.Select(Path.GetFileName)) : "ingen";

        var svar = MessageBox.Show(
            $"Optagelsen ligger i:\n{mappe}\n\nSpor: {spor}\nVarighed: {længde:hh\\:mm\\:ss}\n\n" +
            (kort ? "Bemærk: under 15 minutter giver upålidelige RTF-tal.\n\n" : "") +
            "Åbn mappen nu?",
            "Optagelse gemt", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (svar == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mappe}\"") { UseShellExecute = true });
    }

    // ------------------------------------------------------------ navigation

    private void Naeste_Click(object sender, RoutedEventArgs e) => Flyt(1);

    private void Forrige_Click(object sender, RoutedEventArgs e) => Flyt(-1);

    private void Flyt(int retning)
    {
        if (_session is null) return;

        var ny = _afsnitIndex + retning;
        if (ny < 0 || ny >= _script.Paragraphs.Count) return;

        _afsnitIndex = ny;
        VisAfsnit();
    }

    /// <summary>
    /// Mellemrum og pil frem går videre. Tasterne fanges her frem for som
    /// knap-genveje, så de virker uanset hvad der har fokus — man skal kunne
    /// læse videre uden at se på skærmen.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_session is not null)
        {
            switch (e.Key)
            {
                case Key.Space:
                case Key.Right:
                case Key.PageDown:
                case Key.Down:
                    Flyt(1);
                    e.Handled = true;
                    return;
                case Key.Left:
                case Key.PageUp:
                case Key.Up:
                    Flyt(-1);
                    e.Handled = true;
                    return;
            }
        }
        base.OnPreviewKeyDown(e);
    }

    private void VisAfsnit()
    {
        var p = _script.Paragraphs[_afsnitIndex];
        Afsnit.Text = p.Text;
        NaesteAfsnit.Text = _afsnitIndex + 1 < _script.Paragraphs.Count
            ? _script.Paragraphs[_afsnitIndex + 1].Text
            : "— sidste afsnit. Stop optagelsen, når du har læst det.";

        var blok = _script.Blocks[p.BlockIndex];
        BlokTitel.Text = blok.Title;
        BlokUnder.Text = $"Afsnit {_afsnitIndex + 1} af {_script.Paragraphs.Count}  ·  mål for blokken {blok.TargetStart:mm\\:ss}–{blok.TargetEnd:mm\\:ss}";

        // Blokskiftet skrives som bogmærke. Uden det kan transskriptionen ikke
        // holdes op mod facitlisten blok for blok, og så skal hele teksten
        // læses igennem manuelt for at score den.
        if (p.BlockIndex != _sidsteBlok)
        {
            _sidsteBlok = p.BlockIndex;
            _session?.Notebook.Add($"[blok {p.BlockIndex + 1}] {blok.Title}");
        }

        ForrigeKnap.IsEnabled = _afsnitIndex > 0;
        NaesteKnap.IsEnabled = _afsnitIndex + 1 < _script.Paragraphs.Count;
        Fremdrift.Value = (_afsnitIndex + 1) * 100.0 / _script.Paragraphs.Count;
    }

    // ---------------------------------------------------------------- timer

    private void Opdater()
    {
        if (_session is null) return;

        var gået = _session.Elapsed;
        Ur.Text = gået.ToString(@"mm\:ss");

        var top = _session.Microphone?.ReadPeak() ?? 0f;
        Niveau.Width = 220 * PeakMeter.ToMeterScale(top);

        // Tavshed under en optagelse er den fejl, man opdager bagefter.
        Niveau.Background = top < AudioDevices.SilenceThreshold
            ? (Brush)FindResource("Optager")
            : (Brush)FindResource("Godkendt");

        // Hvor burde jeg være nu? Målet regnes ud fra, hvor langt gennem
        // teksten jeg er — ikke fra blokkens starttid alene, for så ville
        // tallet hoppe ved hvert blokskift.
        var andel = (_afsnitIndex + 1.0) / _script.Paragraphs.Count;
        var mål = TimeSpan.FromSeconds(_script.EstimatedDuration.TotalSeconds * andel);
        UrMaal.Text = $"mål {mål:mm\\:ss}";

        var afvigelse = gået - mål;
        if (Math.Abs(afvigelse.TotalSeconds) < 45)
        {
            Tempo.Text = "i takt";
            Tempo.Foreground = (Brush)FindResource("Godkendt");
        }
        else if (afvigelse < TimeSpan.Zero)
        {
            Tempo.Text = $"{Math.Abs(afvigelse.TotalSeconds):0} sek for hurtigt";
            Tempo.Foreground = (Brush)FindResource("Advarsel");
        }
        else
        {
            Tempo.Text = $"{afvigelse.TotalSeconds:0} sek bagud";
            Tempo.Foreground = (Brush)FindResource("TekstSvag");
        }

        OptagerPrik.Opacity = gået.Milliseconds < 500 ? 1.0 : 0.35;
    }

    /// <summary>Kaldes når vinduet lukkes, så en optagelse ikke tabes ved et uheld.</summary>
    public bool StopHvisIGang()
    {
        if (_session is null) return true;

        var svar = MessageBox.Show(
            "Der er en optagelse i gang. Vil du stoppe og gemme den, før appen lukkes?",
            "Optagelse i gang", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (svar == MessageBoxResult.Cancel) return false;
        if (svar == MessageBoxResult.Yes) StopOptagelse();
        return true;
    }
}
