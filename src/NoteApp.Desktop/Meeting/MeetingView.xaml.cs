using System.IO;
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

    /// <summary>Om den koerende optagelse ogsaa tager hoejttalersporet med.</summary>
    private bool _varOnline;

    public MeetingView()
    {
        InitializeComponent();

        _ur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
        _ur.Tick += (_, _) => Opdater();

        // Klokken. Den lyttter paa Notifikationer, saa tallet opdaterer sig,
        // uden at nogen skal huske at kalde noget — ogsaa naar man staar paa en
        // helt anden skaerm.
        Notifikationer.Nyt += () => Dispatcher.Invoke(VisKlokke);
        Loaded += (_, _) => VisKlokke();

                var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        StartKnap.IsEnabled = mik is not null;
        if (mik is null) Status.Text = "Ingen mikrofon fundet";

        VisGenvej(null, "endnu ikke registreret");

    }

    public bool IsRecording => _session?.IsRecording == true;
    public bool IsPaused => _session?.IsPaused == true;
    public TimeSpan Elapsed => _session?.Elapsed ?? TimeSpan.Zero;
    public IReadOnlyList<NoteVisning> Noter => _noter;
    public string Titel => _session is null ? "" : "Mødet optages";

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
    /// <summary>
    /// Viser genvejstasten — både øverst til højre og som tooltip på knappen.
    ///
    /// Kombinationen kan skifte uden at brugeren har valgt noget: er den
    /// ønskede taget af et andet program, tager appen den næste ledige. Derfor
    /// er teksten ikke en fast streng i XAML, men den tast, der FAKTISK blev
    /// registreret.
    /// </summary>
    public void VisGenvej(string? tast, string? bemærkning)
    {
        if (tast is null)
        {
            // Ingen af mulighederne kunne registreres. At skjule maerkatet
            // ville vaere at lade som ingenting - saa staar der, at den ikke
            // virker, og hvor man goer noget ved det.
            GenvejMaerkat.Text = "OPTAG MED";
            GenvejTast.Text = "ingen genvej";
            GenvejTast.Foreground = (System.Windows.Media.Brush)FindResource("Advarsel");
            GenvejPanel.ToolTip =
                $"Genvejstasten virker ikke: {bemærkning ?? "ukendt årsag"}. " +
                "Vælg en anden under Indstillinger.";

            StartKnap.ToolTip = GenvejPanel.ToolTip;
        }
        else
        {
            GenvejMaerkat.Text = "OPTAG MED";
            GenvejTast.Text = tast;
            GenvejTast.Foreground = (System.Windows.Media.Brush)FindResource("Tekst");
            GenvejPanel.ToolTip = bemærkning is null
                ? $"Tryk {tast} for at starte en optagelse — virker også, når appen er skjult bag andre vinduer."
                : $"Tryk {tast} for at starte en optagelse. {bemærkning}";

            StartKnap.ToolTip = GenvejPanel.ToolTip;
        }

        // Under en optagelse er genvejen brugt, og Pause og Stop skal have
        // pladsen. Sker kun, hvis genvejen saettes op midt i en optagelse.
        GenvejPanel.Visibility = UrPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
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
    private void Note_Changed(object sender, TextChangedEventArgs e) => VisPladsholdere();

    private void VisPladsholdere()
    {
        if (NotePladsholder is not null && FeltNote is not null)
            NotePladsholder.Visibility = FeltNote.Text.Length == 0
                ? Visibility.Visible : Visibility.Collapsed;
    }


    // ------------------------------------------------------------- optagelse

    private void Start_Click(object sender, RoutedEventArgs e) => Start();

    /// <summary>
    /// Lynstart fra genvejstasten. Den gør nu nøjagtig det samme som knappen.
    ///
    /// Før målte den højttaleren i 700 ms for at gætte, om mødet var online.
    /// Det gæt er væk: er ingen begyndt at tale i netop det øjeblik, ser et
    /// onlinemøde ud som et fysisk, og så mangler alle de andre deltagere.
    /// Højttalersporet tages altid med, og er det tavst hele vejen igennem,
    /// slettes det, når optagelsen stoppes.
    /// </summary>
    public void Lynstart() => Start();

    /// <summary>
    /// Spørger, hvad mødet skal hedde — bagefter, hvor man ved det.
    ///
    /// Forslaget bygges af det, der er kendt: den første note og datoen. Er
    /// der ingen noter, er datoen alt, vi har, og så siger forslaget det frem
    /// for at finde på noget.
    ///
    /// Trykker man Annullér, får optagelsen datoen som navn. Den bliver
    /// gemt uanset hvad — et møde, der forsvinder, fordi man lukkede en
    /// dialog, ville være den værste fejl i hele appen.
    /// </summary>
    private string? SpørgOmNavn()
    {
        var dato = DateTime.Now.ToString("d. MMMM");

        // Foerste note er tit emnet eller hvem man taler med. Er den lang,
        // klippes den — den skal vaere et udgangspunkt, ikke en titel.
        var førsteNote = _noter.LastOrDefault()?.Tekst ?? "";
        if (førsteNote == "(bogmærke)") førsteNote = "";
        if (førsteNote.Length > 45) førsteNote = førsteNote[..45].TrimEnd() + "…";

        var forslag = førsteNote.Length > 0 ? $"{førsteNote} — {dato}" : $"Møde {dato}";

        var vindue = new Transcribe.RenameWindow(
            forslag,
            "Hvad skal mødet hedde?",
            _noter.Count > 0
                ? "Forslaget er bygget af din første note og dagens dato. Skriv henover, hvis noget andet passer bedre — typisk hvem mødet var med."
                : "Der er ingen noter at bygge et forslag på, så her er bare datoen. Skriv typisk hvem mødet var med.",
            "Navnet følger med til udskriften og til de dokumenter, du laver af mødet.",
            "Gem optagelsen",
            "Mødet er slut")
        { Owner = Window.GetWindow(this) };

        vindue.TilladKasser();

        var svar = vindue.ShowDialog();

        if (svar == true && vindue.Kasseret) return null;   // null = kassér

        return svar == true && vindue.NytNavn.Length > 0 ? vindue.NytNavn : $"Møde {dato}";
    }

    /// <summary>
    /// Sletter en optagelse, der blev kasseret.
    ///
    /// Hele mappen ryger: lyd, noter og oplysninger. Der er ikke noget at
    /// beholde — brugeren har netop sagt, at det var en prøve.
    /// </summary>
    private static void Kasser(string mappe)
    {
        try
        {
            if (Directory.Exists(mappe)) Directory.Delete(mappe, recursive: true);
        }
        catch (Exception)
        {
            // Kan mappen ikke slettes — en fil kan vaere aaben — bliver den
            // liggende. Det er spildplads, ikke et tab.
        }
    }

    /// <summary>Kaldes både fra knappen og fra genvejstasten.</summary>
    public void Start()
    {
        if (IsRecording) return;

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var fallback);
        if (mik is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke optage", "Ingen mikrofon fundet.", Dialogs.Slags.Pas_paa);
            return;
        }

        // Højttaleren tages med, hvis der er en. Er der ingen, optages kun
        // mikrofonen — og det er ikke en fejl, det er et fysisk møde.
        var højttaler = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out _);
        _varOnline = højttaler is not null;

        if (fallback)
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Mikrofonen er skiftet",
                "Den mikrofon, du havde valgt, er ikke tilsluttet. Der optages i stedet fra:\n\n" +
                mik.FriendlyName,
                godkend: "Optag med den", annuller: "Stop — jeg retter det",
                slags: Dialogs.Slags.Pas_paa);

            if (!ja) return;
        }


        try
        {
            _session = RecordingSession.Create(
                højttaler is not null ? MeetingType.Online : MeetingType.Physical, null, mik, højttaler);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke optage", $"Optagelsen kunne ikke startes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
            return;
        }

        _session.IncidentOccurred += i => Dispatcher.Invoke(() =>
            Status.Text = $"Hændelse ved {TimeSpan.FromSeconds(i.AtSeconds):hh\\:mm\\:ss}: {i.What}");

        _noter.Clear();

        _session.Start();
        _ur.Start();

        KlarFelter.Visibility = Visibility.Collapsed;
        GenvejPanel.Visibility = Visibility.Collapsed;
        OptagFelter.Visibility = Visibility.Visible;
        StartKnap.Visibility = Visibility.Collapsed;
        UrPanel.Visibility = Visibility.Visible;
        NoteTaeller.Text = "";
        PauseKnap.Visibility = Visibility.Visible;
        StopKnap.Visibility = Visibility.Visible;
        OptagerPrik.Fill = (Brush)FindResource("Optager");

        // Kun ÉT budskab. Foerste udgave satte Status to gange, og stien til
        // mappen overskrev det, der faktisk betoed noget: hvad der optages.
        Status.Text = højttaler is not null ? "Optager · mikrofon og højttaler" : "Optager · kun mikrofon";
        Status.ToolTip = $"Gemmes i {_session.SessionDir}";

        VisPladsholdere();

        VisBaand();

        Startet?.Invoke();
    }

    // ============================ OPTAGEBÅNDET ============================
    //
    // MENS DER OPTAGES, ER APPEN I VEJEN.
    //
    // Man kigger på mødet — et videoopkald, en dagsorden, en andens skærm —
    // ikke på NoteApp. Et helt programvindue ovenpå bliver skjult, og så kan
    // man ikke længere se, om der overhovedet stadig optages.
    //
    // Vinduet trækkes derfor helt væk, og båndet bliver tilbage: at der
    // optages, hvor længe, notefeltet, og de to udveje.

    private OptageBaand? _baand;
    private Window? _skjultVindue;

    private void VisBaand()
    {
        if (_baand is not null) return;

        var ejer = Window.GetWindow(this);

        _baand = new OptageBaand();
        _baand.Pause += () => Pause_Click(this, new RoutedEventArgs());
        _baand.Stop += () => Stop();
        _baand.Annuller += Kassér_FraBaand;
        _baand.Note_Skrevet += TilføjNote;

        _baand.SaetPause(false);
        _baand.SaetNoter(0);
        _baand.Show();
        _baand.Placer(ejer);

        // Vinduet SKJULES, det lukkes ikke. En optagelse i gang maa ikke
        // haenge paa, at et vindue overlever - og appen skal staa praecis,
        // hvor den stod, naar moedet er slut.
        if (ejer is not null)
        {
            _skjultVindue = ejer;
            ejer.Hide();
        }
    }

    private void SkjulBaand()
    {
        if (_baand is not null)
        {
            _baand.LukForAlvor();
            _baand = null;
        }

        if (_skjultVindue is null) return;

        _skjultVindue.Show();
        _skjultVindue.Activate();
        _skjultVindue = null;
    }

    /// <summary>
    /// Kassér fra båndet. Der er allerede spurgt dér, så der spørges ikke igen.
    /// </summary>
    private void Kassér_FraBaand()
    {
        SkjulBaand();
        Stop(spørgOmNavn: false);
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
            _baand?.SaetPause(false);
        }
        else
        {
            _session.Pause();
            PauseKnap.Content = "● Fortsæt";
            OptagerPrik.Fill = (Brush)FindResource("Advarsel");
            Status.Text = "På pause — der optages intet, før du fortsætter.";
            _baand?.SaetPause(true);
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => Stop();

    /// <summary>Stopper og gemmer. Sikker at kalde, når der ikke optages.</summary>
    public string? Stop() => Stop(spørgOmNavn: true);

    /// <summary>
    /// Stopper optagelsen.
    ///
    /// <paramref name="spørgOmNavn"/> er falsk, når optagelsen kasseres fra
    /// båndet. Der er spurgt dér — og et navn på noget, der skal slettes om
    /// et øjeblik, er et spørgsmål uden formål.
    /// </summary>
    private string? Stop(bool spørgOmNavn)
    {
        if (_session is null) return null;

        // Baandet vaek FOERST. Ellers ligger navnedialogen bag et vindue,
        // der altid er oeverst, og appen ser laast ud.
        SkjulBaand();

        _ur.Stop();

        var mappe = _session.SessionDir;
        var længde = _session.Elapsed;
        var noter = _session.Notebook.Notes.Count;

        // NAVNGIVNINGEN SKER HER, NÅR MØDET ER SLUT.
        //
        // Før stod titelfeltet før knappen, og det er det forkerte tidspunkt:
        // et møde begynder om et øjeblik, man aner ikke hvad det kommer til at
        // handle om, og feltet blev derfor stående tomt. Bagefter VED man det.
        //
        // Forslaget bygges af det, der faktisk er kendt, når mødet er slut —
        // den første note og datoen. Ikke klokkeslættet: man husker «mødet med
        // Espen den 13.», ikke «mødet 09:57».
        //
        // Kasseres optagelsen fra båndet, springes der over. Der ER spurgt, og
        // et navn på noget, der bliver slettet om et øjeblik, er et spørgsmål
        // uden formål. null betyder «kassér» længere nede.
        var titel = spørgOmNavn ? SpørgOmNavn() : null;

        _session.Stop();
        _session.Dispose();
        _session = null;

        // null betyder «kassér». Det er brugerens svar paa navnedialogen, og
        // der er ikke noget at gemme — de trykkede optag for at proeve noget.
        var kasseret = titel is null;

        if (kasseret) Kasser(mappe);
        else
        {
            var meta = MeetingStore.Load(mappe);
            if (meta is not null && string.IsNullOrWhiteSpace(meta.Title))
            {
                meta.Title = titel;
                MeetingStore.Save(mappe, meta);
            }
        }

        KlarFelter.Visibility = Visibility.Visible;
        GenvejPanel.Visibility = GenvejTast.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        OptagFelter.Visibility = Visibility.Collapsed;
        StartKnap.Visibility = Visibility.Visible;
        UrPanel.Visibility = Visibility.Collapsed;
        PauseKnap.Visibility = Visibility.Collapsed;
        PauseKnap.Content = "❚❚ Pause";
        StopKnap.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");
        Ur.Text = "00:00:00";
        UrUnder.Text = "";


        if (kasseret)
        {
            Status.Text = $"Kasseret. {længde:hh\\:mm\\:ss} lyd er slettet.";

            Historik.Skriv(HaendelseType.Optagelse, "Optagelse kasseret",
                $"{længde:hh\\:mm\\:ss} lyd og {noter} noter slettet efter brugerens valg",
                Udfald.Afbrudt, sekunder: længde.TotalSeconds);

            return null;
        }

        Status.Text = $"Gemt: {længde:hh\\:mm\\:ss} lyd, {noter} noter.";

        Historik.Skriv(HaendelseType.Optagelse, $"Møde optaget: {titel}",
            $"{længde:hh\\:mm\\:ss} lyd · {noter} noter · " + (_varOnline ? "begge spor" : "kun mikrofon"),
            // En kort optagelse stod foer som «se efter». Den er gemt og
            // faerdig; laengden staar i linjen ovenfor, og der er ikke noget
            // at goere ved den bagefter. Se Udfald.SeEfter.
            Udfald.Fuldført,
            sti: mappe, sekunder: længde.TotalSeconds,
            kilde: MeetingStore.Load(mappe)?.Id.ToString() ?? "");

        FærdigMedMøde?.Invoke(mappe);
        return mappe;
    }

    /// <summary>Rejses når et møde er gemt — så resten af appen kan pege videre.</summary>
    public event Action<string>? FærdigMedMøde;

    /// <summary>Stopper en optagelse, hvis der kører en. Falsk = brugeren fortrød.</summary>
    public bool StopHvisIGang()
    {
        if (_session is null) return true;

        // Tre udgange, og «bliv her» har fokus: at lukke ved et uheld og miste
        // et møde er værre end et ekstra klik.
        var valg = Dialogs.AppDialog.SpoergTre(Window.GetWindow(this),
            "Der optages lige nu",
            $"Mødet har kørt i {_session.Elapsed:hh\\:mm\\:ss}. Stopper du nu, bliver det, der er " +
            "optaget indtil videre, gemt.",
            godkend: "Stop og gem", tredje: "Luk uden at gemme", annuller: "Bliv her",
            slags: Dialogs.Slags.Pas_paa);

        if (valg < 0) return false;
        if (valg == 0) Stop();
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

        _baand?.SaetNoter(_noter.Count);

        VisPladsholdere();
    }

    // ------------------------------------------------------------------- ur

    private void Opdater()
    {
        if (_session is null) return;

        var tid = _session.Elapsed.ToString(@"hh\:mm\:ss");

        Ur.Text = tid;
        UrUnder.Text = _session.IsPaused ? "på pause" : "optager";

        // Baandet er det, man kan SE, mens der optages - appens eget ur staar
        // bag et skjult vindue. Det er altsaa ikke en kopi, det er visningen.
        _baand?.SaetTid(tid);

        Opdateret?.Invoke();
    }

    // ------------------------------------------------------------- klokken

    /// <summary>
    /// Tallet paa klokken. Kun ULAESTE taeller — en klokke, der viser hvor
    /// mange beskeder der findes i alt, holder aldrig op med at raabe, og saa
    /// holder man op med at kigge.
    /// </summary>
    private void VisKlokke()
    {
        if (KlokkeBadge is null) return;

        int nye;
        try { nye = Notifikationer.Ulaeste(); }
        catch (Exception) { return; }

        KlokkeBadge.Visibility = nye == 0 ? Visibility.Collapsed : Visibility.Visible;
        KlokkeTal.Text = nye > 9 ? "9+" : nye.ToString();
    }

    private void Klokke_Click(object sender, RoutedEventArgs e)
    {
        var panel = new Notifications.NotificationPopup();

        var pop = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = KlokkeKnap,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            HorizontalOffset = -400,
            VerticalOffset = 6,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
            Child = panel
        };

        panel.HistorikOenskes += () =>
        {
            pop.IsOpen = false;
            if (Window.GetWindow(this) is MainWindow hoved) hoved.GaaTilHistorik();
        };

        pop.IsOpen = true;

        // Markeres som set, NAAR den aabnes — ikke naar appen starter. En
        // besked, man aldrig naaede at se, skal ikke forsvinde, fordi man
        // genstartede.
        Notifikationer.MarkerSet();
    }
}
