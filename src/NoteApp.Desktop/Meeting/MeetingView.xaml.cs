using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Meeting;

/// <summary>En note på skærmen under mødet.</summary>
public sealed record NoteVisning(string Tid, string Tekst);

/// <summary>
/// Optagelse af et rigtigt møde.
///
/// Denne skærm er hele produktet. Alt det andet — oplæsning, ordbog,
/// skabeloner — findes for at gøre DEN her bedre. Derfor er den første i
/// menuen, og derfor er der én stor knap frem for en formular: et møde
/// begynder om et øjeblik, og der skal ikke udfyldes noget først.
///
/// Titlen kan skrives bagefter. Et krav om at navngive mødet, før man må
/// trykke, koster de første tredive sekunder af det.
/// </summary>
public partial class MeetingView : UserControl
{
    private RecordingSession? _session;
    private readonly DispatcherTimer _ur;
    private readonly List<NoteVisning> _noter = new();

    public MeetingView()
    {
        InitializeComponent();

        _ur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
        _ur.Tick += (_, _) => Opdater();

        Noter.ItemsSource = _noter;

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        MikrofonNavn.Text = mik is null
            ? "Ingen mikrofon fundet — tilslut en og genstart appen"
            : $"Mikrofon: {mik.FriendlyName}";
        StartKnap.IsEnabled = mik is not null;

        VisGenvej(null, "endnu ikke registreret");

        VisType();
        Loaded += (_, _) => Focus();
    }

    public bool IsRecording => _session?.IsRecording == true;

    /// <summary>
    /// Fortæller om genvejstasten virker. <paramref name="fejl"/> er null, når
    /// den gør.
    ///
    /// Virker den ikke, SKAL det stå. En genvej, der stille er død, opdages
    /// først den dag, man trykker på den før et møde og bagefter finder ud af,
    /// at der ikke blev optaget noget.
    /// </summary>
    public void VisGenvej(string? tast, string? bemærkning)
    {
        if (tast is null)
        {
            GenvejTekst.Text = bemærkning is null
                ? "Genvejstasten kunne ikke registreres"
                : $"Ingen genvejstast: {bemærkning}. Vælg en anden under Indstillinger.";
            return;
        }

        GenvejTekst.Text = bemærkning is null
            ? $"eller tryk {tast} — virker også, når appen er skjult"
            : $"eller tryk {tast} ({bemærkning})";
    }

    // ------------------------------------------------------------- mødetype

    private void Type_Valgt(object sender, RoutedEventArgs e) => VisType();

    private void VisType()
    {
        if (TypeForklaring is null) return;

        TypeForklaring.Text = TypeOnline.IsChecked == true
            ? "Onlinemøde: der optages både fra din mikrofon og fra det, højttaleren afspiller — altså også de andre deltagere. De to spor gemmes hver for sig, så de kan skilles ad bagefter."
            : "Fysisk møde: der optages kun fra mikrofonen. Sæt den midt på bordet, hvis I er flere.";
    }

    // ------------------------------------------------------------- optagelse

    private void Start_Click(object sender, RoutedEventArgs e) => Start();

    /// <summary>Kaldes både fra knappen og fra genvejstasten.</summary>
    public void Start()
    {
        if (IsRecording) return;

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var fallback);
        if (mik is null)
        {
            MessageBox.Show("Ingen mikrofon fundet.", "Kan ikke optage",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var online = TypeOnline.IsChecked == true;
        DeviceInfo? højttaler = null;

        if (online)
        {
            højttaler = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out _);
            if (højttaler is null)
            {
                MessageBox.Show(
                    "Der er ingen afspilningsenhed at optage fra.\n\n" +
                    "Et onlinemøde optages fra det, højttaleren afspiller. Vælg en enhed under Indstillinger, " +
                    "eller optag som fysisk møde — så optages kun din mikrofon.",
                    "Kan ikke optage onlinemøde", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (fallback)
        {
            var svar = MessageBox.Show(
                $"Den mikrofon, du havde valgt, er ikke tilsluttet.\n\nDer optages i stedet fra:\n{mik.FriendlyName}\n\nFortsæt?",
                "Mikrofonen er skiftet", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (svar != MessageBoxResult.OK) return;
        }

        var titel = string.IsNullOrWhiteSpace(FeltTitel.Text) ? null : FeltTitel.Text.Trim();

        try
        {
            _session = RecordingSession.Create(
                online ? MeetingType.Online : MeetingType.Physical, titel, mik, højttaler);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Optagelsen kunne ikke startes.\n\n{ex.Message}", "Kunne ikke optage",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _session.IncidentOccurred += i => Dispatcher.Invoke(() =>
            Status.Text = $"Hændelse ved {TimeSpan.FromSeconds(i.AtSeconds):hh\\:mm\\:ss}: {i.What}");

        _noter.Clear();
        Noter.Items.Refresh();

        _session.Start();
        _ur.Start();

        KlarPanel.Visibility = Visibility.Collapsed;
        OptagPanel.Visibility = Visibility.Visible;
        PauseKnap.Visibility = Visibility.Visible;
        StopKnap.Visibility = Visibility.Visible;
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        Overskrift.Text = titel ?? "Mødet optages";
        Underskrift.Text = online
            ? "Fysisk møde · mikrofon + højttaler"
            : "Fysisk møde · kun mikrofon";
        Underskrift.Text = online ? "Onlinemøde · mikrofon + højttaler" : "Fysisk møde · kun mikrofon";

        Status.Text = $"Gemmes i {_session.SessionDir}";
        FeltNote.Focus();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null) return;

        if (_session.IsPaused)
        {
            _session.Resume();
            PauseKnap.Content = "❚❚ Pause";
            OptagerPrik.Fill = (Brush)FindResource("Optager");
            Status.Text = "Optager igen.";
        }
        else
        {
            _session.Pause();
            PauseKnap.Content = "● Fortsæt";
            OptagerPrik.Fill = (Brush)FindResource("Advarsel");
            Status.Text = "På pause — der optages intet, før du fortsætter.";
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => Stop();

    /// <summary>Stopper og gemmer. Sikker at kalde, når der ikke optages.</summary>
    public string? Stop()
    {
        if (_session is null) return null;

        _ur.Stop();

        var mappe = _session.SessionDir;
        var længde = _session.Elapsed;
        var noter = _session.Notebook.Notes.Count;

        // Titlen kan vaere skrevet, mens moedet koerte. Den gemmes med, saa
        // optagelsen ikke hedder et klokkeslet i listen bagefter.
        var titel = string.IsNullOrWhiteSpace(FeltTitel.Text) ? null : FeltTitel.Text.Trim();

        _session.Stop();
        _session.Dispose();
        _session = null;

        if (titel is not null)
        {
            var meta = MeetingStore.Load(mappe);
            if (meta is not null && string.IsNullOrWhiteSpace(meta.Title))
            {
                meta.Title = titel;
                MeetingStore.Save(mappe, meta);
            }
        }

        KlarPanel.Visibility = Visibility.Visible;
        OptagPanel.Visibility = Visibility.Collapsed;
        PauseKnap.Visibility = Visibility.Collapsed;
        PauseKnap.Content = "❚❚ Pause";
        StopKnap.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");
        Ur.Text = "00:00:00";
        UrUnder.Text = "";

        Overskrift.Text = "Optag møde";
        Underskrift.Text = "Alt bliver på denne pc. Ingen lyd forlader maskinen.";
        Status.Text = $"Gemt: {længde:hh\\:mm\\:ss} lyd, {noter} noter.";

        FærdigMedMøde?.Invoke(mappe);
        return mappe;
    }

    /// <summary>Rejses når et møde er gemt — så resten af appen kan pege videre.</summary>
    public event Action<string>? FærdigMedMøde;

    /// <summary>Stopper en optagelse, hvis der kører en. Falsk = brugeren fortrød.</summary>
    public bool StopHvisIGang()
    {
        if (_session is null) return true;

        var svar = MessageBox.Show(
            $"Der optages lige nu ({_session.Elapsed:hh\\:mm\\:ss}).\n\nStop og gem, før du lukker?",
            "Optagelsen kører", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

        if (svar == MessageBoxResult.Cancel) return false;
        if (svar == MessageBoxResult.Yes) Stop();
        return true;
    }

    // ----------------------------------------------------------------- noter

    private void Note_KeyDown(object sender, KeyEventArgs e)
    {
        if (_session is null) return;

        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TilføjNote("(bogmærke)");
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(FeltNote.Text)) return;

        TilføjNote(FeltNote.Text.Trim());
        FeltNote.Clear();
        e.Handled = true;
    }

    private void TilføjNote(string tekst)
    {
        if (_session is null) return;

        _session.Notebook.Add(tekst);
        _noter.Insert(0, new NoteVisning(_session.Elapsed.ToString(@"hh\:mm\:ss"), tekst));
        Noter.Items.Refresh();
    }

    // ------------------------------------------------------------------- ur

    private void Opdater()
    {
        if (_session is null) return;

        Ur.Text = _session.Elapsed.ToString(@"hh\:mm\:ss");
        UrUnder.Text = _session.IsPaused ? "på pause" : "optager";
    }
}
