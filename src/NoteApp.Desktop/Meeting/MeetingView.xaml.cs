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

                var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        StartKnap.IsEnabled = mik is not null;
        if (mik is null) Status.Text = "Ingen mikrofon fundet";

        VisGenvej(null, "endnu ikke registreret");

        VisType();
    }

    public bool IsRecording => _session?.IsRecording == true;
    public bool IsPaused => _session?.IsPaused == true;
    public TimeSpan Elapsed => _session?.Elapsed ?? TimeSpan.Zero;
    public IReadOnlyList<NoteVisning> Noter => _noter;
    public string Titel => FeltTitel.Text.Trim();

    /// <summary>Rejses når en optagelse begynder — så appen kan blive til mødet.</summary>
    public event Action? Startet;

    /// <summary>Rejses ved hver ændring under optagelsen: ur, pause, ny note.</summary>
    public event Action? Opdateret;

    /// <summary>Tilføjer en note udefra — fra mødeskærmen.</summary>
    public void TilføjNoteUdefra(string tekst) => TilføjNote(tekst);

    /// <summary>Pause og fortsæt udefra.</summary>
    public void SkiftPause() => Pause_Click(this, new RoutedEventArgs());

    /// <summary>Stop udefra.</summary>
    public void StopUdefra() => Stop();

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
        // Bjaelken har ikke plads til en linje om genvejen. Den staar som
        // tooltip paa knappen, hvor man kigger hen, naar man er i tvivl.
        StartKnap.ToolTip = tast is null
            ? $"Genvejstasten virker ikke: {bemærkning ?? "ukendt årsag"}. Vælg en anden under Indstillinger."
            : bemærkning is null
                ? $"Eller tryk {tast} — virker også, når appen er skjult bag andre vinduer."
                : $"Eller tryk {tast}. {bemærkning}";
    }

    // ---------------------------------------------------------- pladsholdere

    /// <summary>
    /// Pladsholderteksten skjules, så snart der står noget i feltet.
    ///
    /// Begge felter går gennem den SAMME metode. Første udgave havde to
    /// forskellige veje — titlen blev skjult ved TextChanged, noten kun når man
    /// havde trykket Enter. Resultatet var, at man skrev en note oven i den grå
    /// tekst og ikke kunne læse, hvad man skrev.
    ///
    /// To felter, der gør det samme, skal ikke have hver sin kode.
    /// </summary>
    private void Titel_Changed(object sender, TextChangedEventArgs e) => VisPladsholdere();

    private void Note_Changed(object sender, TextChangedEventArgs e) => VisPladsholdere();

    private void VisPladsholdere()
    {
        if (TitelPladsholder is not null && FeltTitel is not null)
            TitelPladsholder.Visibility = FeltTitel.Text.Length == 0
                ? Visibility.Visible : Visibility.Collapsed;

        if (NotePladsholder is not null && FeltNote is not null)
            NotePladsholder.Visibility = FeltNote.Text.Length == 0
                ? Visibility.Visible : Visibility.Collapsed;
    }

    // ------------------------------------------------------------- mødetype

    private void Type_Valgt(object sender, RoutedEventArgs e) => VisType();

    private void VisType()
    {
        if (Status is null || IsRecording) return;

        Status.Text = TypeOnline.IsChecked == true
            ? "Online: mikrofon + højttaler, så de andre deltagere kommer med"
            : "Fysisk: kun mikrofonen. Sæt den midt på bordet, hvis I er flere";
    }

    // ------------------------------------------------------------- optagelse

    private void Start_Click(object sender, RoutedEventArgs e) => Start();

    /// <summary>
    /// Lynstart fra genvejstasten: find selv ud af, om det er et onlinemøde.
    ///
    /// Reglen er den enkle og den rigtige: kommer der lyd ud af højttaleren,
    /// er der nogen i den anden ende, og så skal det spor med. Er der stille,
    /// er det et fysisk møde, og så optages kun mikrofonen.
    ///
    /// Der måles i 700 ms. Længere ville udskyde optagelsen, og det, der
    /// bliver sagt i de første sekunder, er tit dagsordenen. Er højttaleren
    /// tavs netop dér — ingen taler lige nu — bliver det optaget som et fysisk
    /// møde, og DET SKAL SIGES, så man kan stoppe og starte forfra.
    /// </summary>
    public void Lynstart()
    {
        if (IsRecording) return;

        var højttaler = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out _);
        var online = false;
        var måltNiveau = 0f;

        if (højttaler is not null)
        {
            try
            {
                måltNiveau = AudioDevices.MeasureLoopbackPeak(højttaler.Id, TimeSpan.FromMilliseconds(700));
                online = måltNiveau > AudioDevices.SilenceThreshold;
            }
            catch (Exception)
            {
                // Kan der ikke maales, optages der som fysisk moede. Det er den
                // sikre fejl: mikrofonen kommer altid med.
            }
        }

        TypeOnline.IsChecked = online;
        TypeFysisk.IsChecked = !online;

        Start();

        if (!IsRecording) return;

        Status.Text = online
            ? $"Der kom lyd fra {højttaler!.FriendlyName} — optager som onlinemøde med begge spor. Skift til «Fysisk møde» og start forfra, hvis det er forkert."
            : (højttaler is null
                ? "Ingen afspilningsenhed — optager kun mikrofonen."
                : "Der var stille i højttaleren, så det optages som fysisk møde. Er du på et onlinemøde, hvor ingen talte netop nu, så stop, vælg «Onlinemøde» og start forfra.");
    }

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

        _session.Start();
        _ur.Start();

        KlarFelter.Visibility = Visibility.Collapsed;
        OptagFelter.Visibility = Visibility.Visible;
        StartKnap.Visibility = Visibility.Collapsed;
        UrPanel.Visibility = Visibility.Visible;
        NoteTaeller.Text = "";
        PauseKnap.Visibility = Visibility.Visible;
        StopKnap.Visibility = Visibility.Visible;
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        // Kun ÉT budskab. Foerste udgave satte Status to gange, og stien til
        // mappen overskrev det, der faktisk betoed noget: hvad der optages.
        Status.Text = online ? "Optager mikrofon + højttaler" : "Optager kun mikrofonen";
        Status.ToolTip = $"Gemmes i {_session.SessionDir}";

        VisPladsholdere();

        Startet?.Invoke();
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

        KlarFelter.Visibility = Visibility.Visible;
        OptagFelter.Visibility = Visibility.Collapsed;
        StartKnap.Visibility = Visibility.Visible;
        UrPanel.Visibility = Visibility.Collapsed;
        PauseKnap.Visibility = Visibility.Collapsed;
        PauseKnap.Content = "❚❚ Pause";
        StopKnap.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");
        Ur.Text = "00:00:00";
        UrUnder.Text = "";


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

        // Kvitteringen: tidspunktet, noten fik. Uden den ved man ikke, om der
        // skete noget, da man trykkede Enter — feltet bliver jo bare tomt igen.
        NoteTaeller.Text = $"✓ {_noter[0].Tid} · {_noter.Count}";
        NoteTaeller.ToolTip = string.Join("\n", _noter.Take(12).Select(n => $"{n.Tid}  {n.Tekst}"));

        VisPladsholdere();
    }

    // ------------------------------------------------------------------- ur

    private void Opdater()
    {
        if (_session is null) return;

        Ur.Text = _session.Elapsed.ToString(@"hh\:mm\:ss");
        UrUnder.Text = _session.IsPaused ? "på pause" : "optager";
        Opdateret?.Invoke();
    }
}
