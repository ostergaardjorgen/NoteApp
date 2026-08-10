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

    // Live-lytningen der skifter afsnit af sig selv. Den er en hjaelper:
    // fejler den, eller taber den traaden, virker mellemrum praecis som foer.
    private LiveListener? _lytter;
    private readonly ScriptFollower _foelger = new();

    public ReadAloudView()
    {
        InitializeComponent();

        _script = ScriptDocument.Load();

        Indledning.Text =
            $"Teksten er på {_script.TotalWords} ord og tager cirka " +
            $"{_script.EstimatedDuration.TotalMinutes:0} minutter at læse i almindeligt taletempo. " +
            "Den handler om et opdigtet møde — indholdet betyder ikke noget, og navnene i den er " +
            "opfundet. Det er din stemme og dine fagord, appen skal lære at kende.";

        // Tallene er talt op i facitlisten, ikke skoennet. Skiftes teksten ud,
        // skal de taelles igen — et forkert tal her ville vaere en paastand om,
        // at oplaesningen maaler noget, den ikke maaler.
        var sidsteBlok = _script.Blocks.LastOrDefault();
        HvorforLaengde.Text =
            "Stopper du efter 5 minutter, er du midt i anden blok. Så har du læst 4 af de 17 " +
            "negationer op — og ingen af de 6 beslutninger eller 7 opgaver med ejer, for de " +
            "ligger alle sammen efter minut 10. To af opgaverne bliver kun nævnt i forbifarten, " +
            "og netop dem er de sværeste at fange. " +
            (sidsteBlok is null
                ? ""
                : $"Sidste blok begynder først {sidsteBlok.TargetStart:mm\\:ss}.");

        Praktisk.Text =
            "Læs som om du taler til en kollega, ikke som en oplæsning. Pauser, vejrtrækning og små " +
            "tøvelyde hører med — det er dem, der gør optagelsen realistisk. Læs ikke overskrifterne højt; " +
            "de er kun til dig. Sæt dig i normal afstand fra mikrofonen, og læn dig ikke ind mod den.";

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        MikrofonNavn.Text = mik?.FriendlyName ?? "ingen mikrofon fundet";
        OptagKnap.IsEnabled = mik is not null;
        Status.Text = mik is null
            ? "Der er ingen mikrofon. Tilslut en, og genstart appen."
            : $"{_script.Blocks.Count} blokke, {_script.Paragraphs.Count} afsnit. Optagelsen gemmes i {UserDataPaths.Meetings}";

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Opdater();

        FoelgMed.IsChecked = AppSettings.Current.AutoAdvance;
        VisFoelgStatus();

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
        PauseKnap.Visibility = Visibility.Visible;
        PauseKnap.Content = "❚❚ Pause";
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        VisAfsnit();
        _timer.Start();

        if (AppSettings.Current.AutoAdvance) StartLytning();

        Focus();
    }

    private void StopOptagelse()
    {
        if (_session is null) return;

        _timer.Stop();
        StopLytning();

        var filer = _session.Stop();
        var mappe = _session.SessionDir;
        var længde = _session.Elapsed;
        var noter = _session.Notebook.Notes.Count;

        _session.Dispose();
        _session = null;

        OptagKnap.Content = "● Start optagelse";
        NaesteKnap.IsEnabled = false;
        ForrigeKnap.IsEnabled = false;
        PauseKnap.Visibility = Visibility.Collapsed;
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

    // ------------------------------------------------------------ auto-skift

    /// <summary>
    /// Fortæller, om automatisk skift kan lade sig gøre — og hvorfor ikke,
    /// hvis det ikke kan. Et afkrydsningsfelt, der stille intet gør, er
    /// værre end et, der er slået fra med en begrundelse.
    /// </summary>
    private void VisFoelgStatus()
    {
        var install = WhisperInstall.Locate();
        var stream = LiveListener.FindStreamExe(install.WhisperCli);
        var model = LiveModelPath();

        if (stream is null)
        {
            FoelgMed.IsEnabled = false;
            FoelgStatus.Text = "kræver whisper-stream, som ikke findes i din motor-mappe";
        }
        else if (model is null)
        {
            FoelgMed.IsEnabled = false;
            FoelgStatus.Text = $"hent modellen «{AppSettings.Current.LiveModel}» under Motor og model først";
        }
        else
        {
            FoelgMed.IsEnabled = true;
            FoelgStatus.Text = FoelgMed.IsChecked == true
                ? $"lytter med {AppSettings.Current.LiveModel} — mellemrum virker stadig"
                : "";
        }
    }

    /// <summary>
    /// Stien til live-modellen. Den er bevidst en anden end den store: den
    /// skal svare hvert andet sekund, ikke skrive det bedste resultat.
    /// </summary>
    private static string? LiveModelPath()
    {
        var id = AppSettings.Current.LiveModel;
        var model = WhisperInstall.Model(id);
        if (model is null) return null;

        var sti = WhisperInstall.Locate(id).ModelPath;
        return sti is not null && Path.GetFileName(sti).Equals(model.FileName, StringComparison.OrdinalIgnoreCase)
            ? sti
            : null;
    }

    private void FoelgMed_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.AutoAdvance = FoelgMed.IsChecked == true;
        AppSettings.Current.Save();
        VisFoelgStatus();

        if (_session is null) return;

        if (AppSettings.Current.AutoAdvance) StartLytning();
        else StopLytning();
    }

    private void StartLytning()
    {
        var install = WhisperInstall.Locate();
        var stream = LiveListener.FindStreamExe(install.WhisperCli);
        var model = LiveModelPath();
        if (stream is null || model is null) return;

        // Lyt paa den mikrofon, brugeren har valgt — ikke paa Windows'
        // standard. Ellers foelger appen en anden lyd end den, den optager.
        var enheder = LiveListener.ListCaptureDevices(stream);
        var valgt = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        var sdlId = LiveListener.MatchDevice(enheder, valgt?.FriendlyName);

        _lytter = new LiveListener(stream);
        _lytter.Heard += tekst => Dispatcher.BeginInvoke(() => Hoert(tekst));
        _lytter.Failed += fejl => Dispatcher.BeginInvoke(() =>
        {
            FoelgStatus.Text = $"kunne ikke lytte med: {fejl} — brug mellemrum";
        });

        _foelger.SetParagraph(_script.Paragraphs[_afsnitIndex].Text);
        _lytter.Start(model, sdlId);

        FoelgStatus.Text = sdlId >= 0
            ? $"lytter med {AppSettings.Current.LiveModel} — mellemrum virker stadig"
            : $"lytter på Windows' standardmikrofon — mellemrum virker stadig";
    }

    private void StopLytning()
    {
        _lytter?.Dispose();
        _lytter = null;
    }

    /// <summary>
    /// Kaldes for hver linje, live-lytningen producerer. Skifter afsnit, når
    /// slutningen af det aktuelle er hørt.
    /// </summary>
    private void Hoert(string tekst)
    {
        if (_session is null || _session.IsPaused) return;
        if (_afsnitIndex + 1 >= _script.Paragraphs.Count) return;

        if (_foelger.Feed(tekst)) Flyt(1);
    }

    /// <summary>
    /// Pause og fortsæt. Under pausen optages der intet — mikrofonen slippes,
    /// og uret står stille. Det er dét, der gør det trygt at trykke start:
    /// kommer der nogen ind ad døren, skal man ikke først finde ud af, om
    /// samtalen bliver optaget.
    /// </summary>
    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;

        if (_session.IsPaused)
        {
            _session.Resume();
            PauseKnap.Content = "❚❚ Pause";
            OptagerPrik.Fill = (Brush)FindResource("Optager");
            Status.Text = "Optager igen.";
            _timer.Start();
            if (AppSettings.Current.AutoAdvance) StartLytning();
        }
        else
        {
            _session.Pause();
            _timer.Stop();

            // Lytningen stoppes ogsaa. Ellers ville den blive ved med at
            // hoere efter under en pause, hvor der netop ikke skal optages.
            StopLytning();
            PauseKnap.Content = "▶ Fortsæt";
            OptagerPrik.Fill = (Brush)FindResource("Advarsel");
            Niveau.Width = 0;
            Ur.Text = _session.Elapsed.ToString(@"mm\:ss");
            Status.Text = "PÅ PAUSE — der optages ikke. Tryk Fortsæt, når du er klar.";
        }

        Focus();
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

        // Foelgeren skal vide, hvad den nu skal lytte efter — ogsaa naar man
        // selv trykker mellemrum. Ellers ville den blive ved med at vente paa
        // slutningen af et afsnit, brugeren allerede har forladt.
        _foelger.SetParagraph(_script.Paragraphs[_afsnitIndex].Text);
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

        var blok = _script.Blocks[p.BlockIndex];
        BlokTitel.Text = blok.Title;
        var sidste = _afsnitIndex + 1 >= _script.Paragraphs.Count;
        BlokUnder.Text = sidste
            ? $"Sidste afsnit af {_script.Paragraphs.Count} — tryk «Stop og gem», når du har læst det"
            : $"Afsnit {_afsnitIndex + 1} af {_script.Paragraphs.Count}  ·  mål for blokken {blok.TargetStart:mm\\:ss}–{blok.TargetEnd:mm\\:ss}";

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
