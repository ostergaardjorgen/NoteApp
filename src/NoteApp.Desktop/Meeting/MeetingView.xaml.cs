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

        // Flaget paa sprogknappen. Det skal ogsaa foelge med, naar sproget
        // skiftes fra et andet vindue - derfor lyttes der, og der kobles af
        // igen, naar bjaelken forsvinder.
        VisSprogflag();
        NoteApp.Core.Sprog.Aendret += VisSprogflag;
        Unloaded += (_, _) => NoteApp.Core.Sprog.Aendret -= VisSprogflag;

        // Maanen eller solen paa temaknappen. Den skal foelge med, naar temaet
        // skiftes fra Indstillinger - eller af Windows selv ved solnedgang,
        // hvis man foelger systemet. Hjaelpeteksten er ogsaa en oversat
        // streng, saa den skal med, naar sproget skifter.
        //
        // DER MELDES TIL I LOADED OG IKKE HER. Unloaded fyrer i WPF, ogsaa
        // naar et element kun kortvarigt er ude af traeet - og saa var der
        // ingen, der lyttede mere. Set 28-08-2026: foerste tryk paa knappen
        // skiftede ikonet, andet tryk gjorde ikke.
        Loaded += (_, _) =>
        {
            Temaskift.Skiftet -= VisTemaikon;
            Temaskift.Skiftet += VisTemaikon;
            NoteApp.Core.Sprog.Aendret -= VisTemaikon;
            NoteApp.Core.Sprog.Aendret += VisTemaikon;
            VisTemaikon();
        };

        Unloaded += (_, _) =>
        {
            Temaskift.Skiftet -= VisTemaikon;
            NoteApp.Core.Sprog.Aendret -= VisTemaikon;
        };

        VisTemaikon();

        _ur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
        _ur.Tick += (_, _) => Opdater();

        // Klokken. Den lyttter paa Notifikationer, saa tallet opdaterer sig,
        // uden at nogen skal huske at kalde noget — ogsaa naar man staar paa en
        // helt anden skaerm.
        Notifikationer.Nyt += () => Dispatcher.Invoke(VisKlokke);
        Loaded += (_, _) => VisKlokke();

                var mik = Mikrofon.Valgt();
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
    /// <summary>
    /// Kører der en optagelse lige nu?
    /// </summary>
    /// <remarks>
    /// Uret er fremme, netop mens der optages — det er dét, der SKIFTER, og
    /// det er derfor svaret læses dér frem for i et flag, der kunne komme ud
    /// af trit med skærmen.
    /// </remarks>
    public bool OptagerNu => UrPanel.Visibility == Visibility.Visible;

    /// <summary>
    /// Optages MIKROFONEN lige nu?
    /// </summary>
    /// <remarks>
    /// ET WEBINAR OPTAGER IKKE DIN MIKROFON. Det står i Start: enheden slås
    /// op, fordi navnet skal med i mødedataene, men den optages ikke — et
    /// webinar er dét, de andre siger.
    ///
    /// Forskellen betyder noget for dikteringen. Under et møde er mikrofonen
    /// i brug til noget vigtigere, og det, man siger, hører til referatet.
    /// Under et webinar sidder man og lytter, og dét er netop, når man kommer
    /// i tanker om noget, der skal skrives ned. En diktering forstyrrer
    /// ingenting: sporet er højttalerens.
    /// </remarks>
    public bool OptagerMikrofonen => OptagerNu && !_erWebinar;

    public void VisGenvej(string? tast, string? bemærkning)
    {
        if (tast is null)
        {
            // Ingen af mulighederne kunne registreres. At skjule maerkatet
            // ville vaere at lade som ingenting - saa staar der, at den ikke
            // virker, og hvor man goer noget ved det.
            GenvejTast.Text = NoteApp.Core.Sprog.T("topbar.ingen_genvej");
            GenvejTast.Foreground = (System.Windows.Media.Brush)FindResource("Advarsel");
            GenvejPanel.ToolTip =
                $"Genvejstasten virker ikke: {bemærkning ?? "ukendt årsag"}. " +
                "Vælg en anden under Indstillinger.";

            StartKnap.ToolTip = GenvejPanel.ToolTip;
        }
        else
        {
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

        VisDiktatgenvej(tast);
    }

    /// <summary>
    /// Viser, at den samme tast HOLDT NEDE giver en diktering.
    /// </summary>
    /// <remarks>
    /// DEN VISES KUN, NÅR DEN VIRKER. Dikteringen skal være slået til, og der
    /// skal være en nøgle til leverandøren — uden en af delene sker der
    /// ingenting, når man holder tasten nede.
    ///
    /// En besked om en genvej, der ikke gør noget, er værre end ingen besked:
    /// man holder tasten, der sker intet, og så er det appen, der er i
    /// stykker — ikke opsætningen, der mangler.
    /// </remarks>
    private void VisDiktatgenvej(string? tast)
    {
        var virker = tast is not null
                     && NoteApp.Core.AppSettings.Current.DikteringTil
                     && NoteApp.Core.Llm.SkyNoegle.Hent() is not null
                     && UrPanel.Visibility != Visibility.Visible;

        DiktatPanel.Visibility = virker ? Visibility.Visible : Visibility.Collapsed;

        if (!virker) return;

        DiktatTekst.Text = NoteApp.Core.Sprog.T("topbar.diktering_hold", tast!);
        DiktatPanel.ToolTip = NoteApp.Core.Sprog.T("topbar.diktering_hold_tip");
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

    /// <summary>
    /// Optageknappen: der spørges om mappe, mødetype og sprog, og så optages
    /// der.
    ///
    /// Fortryder man dialogen, sker der ingenting. Det er forskellen på
    /// knappen og genvejstasten: knappen trykker man på, fordi man er klar til
    /// at tage stilling, og genvejstasten fordi mødet allerede er begyndt.
    /// </summary>
    /// <summary>
    /// Rejses, når der trykkes optag. Sandt betyder: en anden har taget over.
    /// </summary>
    /// <remarks>
    /// KNAPPEN SPØRGER KALENDEREN FØRST.
    ///
    /// Den åbnede før opstartsdialogen med TOMME felter og spurgte om mappe,
    /// mødetype og sprog — også når man stod midt i en aftale, hvor alle tre
    /// stod skrevet. Man havde udfyldt dem i går og blev spurgt igen, mens
    /// mødet gik i gang.
    ///
    /// Det var ikke dialogens skyld. Den fik bare aldrig at vide, hvilket
    /// møde det var. MainWindow ejer kalenderen og OptagAftale, så det er
    /// den, der svarer — og OptagAftale spørger kun om det, der MANGLER.
    ///
    /// Er der ingen aftale netop nu, sker der som før: dialogen åbner, og der
    /// spørges om det hele. Det er stadig det rigtige, når man optager noget,
    /// der ikke står i kalenderen.
    /// </remarks>
    public Func<bool>? OptagAftalenNu;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (IsRecording) return;

        // Er der en aftale i gang, tager den over - med alt, hvad der staar
        // paa den.
        if (OptagAftalenNu?.Invoke() == true) return;

        var vindue = new OpstartWindow(OpstartWindow.Slags.Moede)
        { Owner = Window.GetWindow(this) };

        if (vindue.ShowDialog() != true) return;

        Start(new Opstart(vindue.Sprog, vindue.Kilde, vindue.Mappe, vindue.Moedetype, ErWebinar: false));
    }

    /// <summary>
    /// Optager et webinar: ét spor, sproget valgt på forhånd, og den stopper
    /// af sig selv.
    ///
    /// Dialogen kommer FØR optagelsen. Et webinar begynder på slaget, og et
    /// spørgsmål, der skal besvares, mens oplægsholderen går i gang, koster de
    /// første minutter — dem, hvor dagsordenen bliver ridset op.
    /// </summary>
    /// <summary>
    /// Starter en webinaroptagelse udefra — fra en talt kommando.
    /// </summary>
    /// <remarks>
    /// Den kalder knappens egen handler frem for at gentage dens indhold. To
    /// veje ind i det samme skal gå gennem den samme kode; ellers retter man
    /// den ene og glemmer den anden.
    /// </remarks>
    public void StartWebinar() => Webinar_Click(this, new RoutedEventArgs());

    private void Webinar_Click(object sender, RoutedEventArgs e)
    {
        if (IsRecording) return;

        var vindue = new OpstartWindow(OpstartWindow.Slags.Webinar)
        { Owner = Window.GetWindow(this) };

        if (vindue.ShowDialog() != true) return;

        Start(new Opstart(vindue.Sprog, vindue.Kilde, vindue.Mappe, vindue.Moedetype, ErWebinar: true));
    }

    /// <summary>
    /// Lynstart fra genvejstasten. Den gør nu nøjagtig det samme som knappen.
    ///
    /// Før målte den højttaleren i 700 ms for at gætte, om mødet var online.
    /// Det gæt er væk: er ingen begyndt at tale i netop det øjeblik, ser et
    /// onlinemøde ud som et fysisk, og så mangler alle de andre deltagere.
    /// Højttalersporet tages altid med, og er det tavst hele vejen igennem,
    /// slettes det, når optagelsen stoppes.
    /// </summary>
    public void Lynstart()
    {
        if (IsRecording) return;

        // OPTAGELSEN FØRST. SPØRGSMÅLENE BAGEFTER.
        //
        // Knappen spørger om mappe, mødetype og sprog, FØR der optages. Det er
        // rigtigt dér — man trykker på den, fordi man er klar.
        //
        // Genvejstasten er det modsatte: den bruges, fordi mødet allerede er
        // gået i gang, og fordi man ikke nåede at forberede sig. Lagde man den
        // samme dialog foran, ville de første replikker gå tabt, mens man
        // valgte en mappe — og genvejen ville holde op med at være en genvej.
        //
        // Derfor starter den, og dialogen kommer ovenpå den kørende optagelse.
        // Der optages, mens man svarer, og lukker man den bare, sker der
        // ingenting: optagelsen kører videre uden mappe og uden type.
        Start();

        if (!IsRecording) return;

        // STAAR SVARENE PAA EN AFTALE, SKAL DER IKKE SPOERGES.
        //
        // Genvejen bruges midt i et moede. Er moedet i kalenderen med mappe,
        // moedetype og sprog paa, er der ikke noget at svare paa - og en
        // dialog oven paa en koerende optagelse er saa bare noget, der staar
        // i vejen.
        //
        // Mangler sproget, spoerges der alligevel. Det er det ene, der ikke
        // kan gaettes: rammer det forkert, bliver hele transskriptionen
        // vroevl, og det opdages foerst i referatet.
        if (AftalensSvar?.Invoke() is { } fraAftalen)
        {
            Skriv_Opstart(fraAftalen);

            if (fraAftalen.Sprog.Length > 0) return;
        }

        Spoerg_MensDerOptages();
    }

    /// <summary>
    /// Optager et telefonopkald — uden spørgsmål.
    /// </summary>
    /// <param name="sprog">Sproget, valgt i beskeden: «da» eller «en».</param>
    /// <remarks>
    /// ET OPKALD HAR INGEN MAPPE OG INGEN MØDETYPE. Det lander altid under
    /// Opkald, og navnet er dato og klokkeslæt. Det eneste, der ikke
    /// kan gættes, er sproget — og det blev valgt, før der blev trykket.
    ///
    /// Opkaldet MARKERES her og overlades ikke til målingen i
    /// <see cref="RecordingSession"/>. Brugeren har lige sagt, at det er et
    /// opkald; det svar skal ikke afhænge af, at målingen rammer i samme
    /// sekund.
    /// </remarks>
    public void LynstartOpkald(string sprog)
    {
        if (IsRecording) return;

        Start(new Opstart(sprog, null, null, null, ErWebinar: false));

        if (_session is null || !IsRecording) return;

        var m = _session.Meta;
        m.Opkald = true;

        if (string.IsNullOrWhiteSpace(m.Title))
            m.Title = RecordingSession.Opkaldsnavn(m.StartedAt);

        try { MeetingStore.Save(_session.SessionDir, m); }
        catch (IOException)
        {
            // Skrives igen, naar optagelsen stoppes.
        }
    }

    /// <summary>
    /// Svarene fra den aftale, der kører nu — hvis der er en. Sat af MainWindow.
    /// </summary>
    public Func<Opstart?>? AftalensSvar;

    /// <summary>
    /// Stiller de tre spørgsmål oven på en optagelse, der allerede kører.
    ///
    /// Båndet ligger over alt andet, så det skal lægges ned imens — ellers
    /// havner dialogen BAG det, og appen ser låst ud. Præcis samme greb som
    /// ved «kassér» på båndet.
    /// </summary>
    private void Spoerg_MensDerOptages()
    {
        var ejer = (Window?)_baand ?? Window.GetWindow(this);
        if (_baand is not null) _baand.Topmost = false;

        try
        {
            var vindue = new OpstartWindow(OpstartWindow.Slags.Igang) { Owner = ejer };
            if (vindue.ShowDialog() != true) return;

            Skriv_Opstart(new Opstart(vindue.Sprog, vindue.Kilde, vindue.Mappe, vindue.Moedetype,
                                      ErWebinar: false));
        }
        finally
        {
            if (_baand is not null) _baand.Topmost = true;
        }
    }

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
            "Navnet følger med til transkriptionen og til de dokumenter, du laver af mødet.",
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

    /// <summary>
    /// Boksen står klar til et booket møde — eller står ned igen.
    /// </summary>
    /// <param name="besked">
    /// Hvad der skal stå. Null sætter hjælpeteksten tilbage, som den var.
    /// </param>
    /// <remarks>
    /// KLAR SKAL SES, ELLERS ER DEN INGENTING. Hele grunden til at stille
    /// boksen klar to minutter før er, at man kan kigge på skærmen og vide, at
    /// den er der. Står der den samme grå hjælpetekst som altid, har
    /// klargøringen ingen værdi — så kunne den lige så godt køre usynligt.
    ///
    /// Derfor skifter både farve og vægt. Grøn og halvfed er «det her er i
    /// orden, og det er nyt» — ikke rød, for der optages ikke endnu, og rødt
    /// betyder optagelse alle andre steder i appen.
    /// </remarks>
    public void StaaKlar(string? besked)
    {
        if (besked is null)
        {
            KlarTekst.SetResourceReference(TextBlock.ForegroundProperty, "TekstMeget");
            KlarTekst.FontWeight = FontWeights.Normal;
            KlarTekst.Text = NoteApp.Core.Sprog.T("topbar.hjaelp");
            return;
        }

        KlarTekst.SetResourceReference(TextBlock.ForegroundProperty, "Godkendt");
        KlarTekst.FontWeight = FontWeights.SemiBold;
        KlarTekst.Text = besked;
    }

    /// <summary>Starter uden svar på noget. Genvejstasten spørger bagefter.</summary>
    public void Start() => Start(null);

    /// <summary>
    /// Starter en optagelse — møde eller webinar.
    /// </summary>
    /// <param name="opstart">
    /// Svarene fra opstartsdialogen. Null betyder et almindeligt møde, hvor
    /// der ikke er svaret på noget.
    /// </param>
    public void Start(Opstart? opstart)
    {
        var webinar = opstart is { ErWebinar: true } ? opstart : null;

        if (IsRecording) return;

        var mik = Mikrofon.Valgt(out var fallback);

        // MIKROFONEN SKAL FINDES — OGSÅ TIL ET WEBINAR.
        //
        // Den optages ikke, men enheden slås stadig op: appen skriver dens
        // navn i mødedataene, og en optagelse uden nogen form for lydopsætning
        // er værd at stoppe, før den begynder.
        if (mik is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke optage", "Ingen mikrofon fundet.", Dialogs.Slags.Pas_paa);
            return;
        }

        // Højttaleren tages med, hvis der er en. Er der ingen, optages kun
        // mikrofonen — og det er ikke en fejl, det er et fysisk møde.
        var højttaler = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out _);
        _varOnline = højttaler is not null;

        // ET WEBINAR UDEN HØJTTALERSPOR ER INGEN OPTAGELSE.
        //
        // For et møde er en manglende højttaler bare et fysisk møde. For et
        // webinar er det ENESTE spor væk, og så optages der ingenting. Det
        // skal siges nu og ikke opdages bagefter som en tom fil.
        if (webinar is not null && højttaler is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Der er ingen højttaler",
                "Et webinar optages fra det, computeren afspiller — og der er ingen " +
                "afspilningsenhed at optage fra.\n\n" +
                "Tilslut høretelefoner eller højttalere, og prøv igen.",
                Dialogs.Slags.Pas_paa);
            return;
        }

        if (fallback && webinar is null)
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
            var type = webinar is not null
                ? MeetingType.Webinar
                : højttaler is not null ? MeetingType.Online : MeetingType.Physical;

            _session = RecordingSession.Create(type, null, mik, højttaler);

            if (opstart is not null) Skriv_Opstart(opstart);

            _erWebinar = webinar is not null;
            _stilhedFra = null;

            StartMedskrivning();
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
        WebinarKnap.Visibility = Visibility.Collapsed;
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
    // ikke på HeyPia. Et helt programvindue ovenpå bliver skjult, og så kan
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
        _baand.SkiftAppVisning += SkiftAppVisning;

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
            _baand.SaetAppSynlig(false);
        }
    }

    /// <summary>
    /// Kontakten paa baandet: rul appen ud, fold den sammen igen.
    ///
    /// HVORFOR DEN SKAL KUNNE BEGGE VEJE MIDT I EN OPTAGELSE:
    /// appen bliver vist frem for andre MENS der optages. Man skal kunne tage
    /// den frem, pege paa den, og laegge den vaek igen - uden at lede efter et
    /// vindue i proceslinjen, og uden at optagelsen maerker det.
    ///
    /// BAANDET BLIVER LIGGENDE, OGSAA NAAR APPEN ER FREMME.
    /// Det er Topmost, saa det ligger oven paa hovedvinduet. Skjulte vi det,
    /// mens appen var fremme, ville beviset for, at der optages, vaere vaek
    /// praecis mens der bliver kigget med - og vejen tilbage med.
    ///
    /// DER SKJULES, DER LUKKES IKKE. Samme grund som ved start: optagelsen maa
    /// ikke haenge paa, at et vindue overlever.
    ///
    /// HELE METODEN ER PAKKET IND. En optagelse maa aldrig kunne rives ned af,
    /// at et vindue ikke ville frem. Gaar det galt, bliver baandet - og
    /// optagelsen - staaende, og teksten paa knappen roeres ikke, saa den
    /// bliver ved at sige sandheden om, hvor vinduet er.
    /// </summary>
    private void SkiftAppVisning()
    {
        try
        {
            // _skjultVindue foerst: er vinduet skjult, kan Window.GetWindow
            // stadig finde det, men vi ved allerede hvilket det er.
            var vindue = _skjultVindue ?? Window.GetWindow(this);
            if (vindue is null) return;

            // Minimeret taeller som «ikke fremme» - ellers ville det foerste
            // klik efter en minimering skjule et vindue, brugeren ikke kan se,
            // og kontakten ville staa forkert resten af moedet.
            var erFremme = vindue.IsVisible && vindue.WindowState != WindowState.Minimized;

            if (erFremme)
            {
                vindue.Hide();
            }
            else
            {
                // Samme vej frem som genvejstasten og spaerren mod to
                // instanser bruger. Show() alene raekker ikke, naar et andet
                // program har fokus.
                App.HentFrem(vindue);
            }

            // Vinduet er stadig vores at rydde op i, naar moedet slutter -
            // ogsaa naar brugeren selv har hentet det frem imellemtiden.
            _skjultVindue = vindue;

            _baand?.SaetAppSynlig(!erFremme);
        }
        catch (Exception)
        {
            // Optagelsen koerer videre. Et vindue, der ikke ville frem, maa
            // ikke koste moedet.
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

        // Samme vej frem som kontakten paa baandet og genvejstasten. Vinduet
        // kan vaere fremme i forvejen - brugeren kan selv have hentet det -
        // og saa er det her uden virkning.
        App.HentFrem(_skjultVindue);
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

    /// <summary>
    /// Under det her er en optagelse et fejltryk, og der spørges ikke.
    /// </summary>
    /// <remarks>
    /// FEMTEN SEKUNDER. Det er kort nok til, at intet møde kan nå at begynde,
    /// og langt nok til, at man kan nå at fortryde et tryk uden at have travlt.
    /// </remarks>
    private static readonly TimeSpan ForKort = TimeSpan.FromSeconds(15);

    /// <summary>Stopper og gemmer. Sikker at kalde, når der ikke optages.</summary>
    public string? Stop() => Stop(spørgOmNavn: true);

    /// <summary>
    /// Stopper optagelsen.
    ///
    /// <paramref name="spørgOmNavn"/> er falsk, når optagelsen kasseres fra
    /// båndet. Der er spurgt dér — og et navn på noget, der skal slettes om
    /// et øjeblik, er et spørgsmål uden formål.
    /// </summary>
    /// <param name="navn">
    /// Et navn, der bruges i stedet for at spørge. Sat, når et webinar stopper
    /// af sig selv — hele pointen dér er, at ingen er til stede at spørge.
    /// </param>
    private string? Stop(bool spørgOmNavn, string? navn = null)
    {
        if (_session is null) return null;

        // Baandet vaek FOERST. Ellers ligger navnedialogen bag et vindue,
        // der altid er oeverst, og appen ser laast ud.
        SkjulBaand();

        _ur.Stop();
        _erWebinar = false;
        _stilhedFra = null;

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
        //
        // Et webinar, der stopper af sig selv, har fået et navn med — dér er
        // der ingen at spørge, og optagelsen skal gemmes, ikke kasseres.
        // ============ ET KVART MINUT ER ET FEJLTRYK ============
        //
        // Under femten sekunder er der ikke noget moede. Der er trykket
        // forkert, eller nogen har proevet knappen. At spoerge «hvad skal
        // moedet hedde?» og «er du sikker paa, du vil kassere?» om fire
        // sekunders lyd er to spoergsmaal uden svar - man kan ikke navngive
        // det, og man vil ikke gemme det.
        //
        // Der ryddes op med det samme, og der staar hvorfor. Historikken faar
        // sin linje som ved enhver anden kassering, saa det ikke bare
        // forsvinder.
        //
        // Et webinar, der stopper sig selv, har et navn med og roeres ikke -
        // dér er der ingen at spoerge, og laengden er ikke et fejltryk.
        var forKort = navn is null && spørgOmNavn && længde < ForKort;

        // ============ ET OPKALD HAR SIT NAVN I FORVEJEN ============
        //
        // Dato og klokkeslaet, sat da optagelsen begyndte. At spoerge «hvad
        // skal moedet hedde?» efter hvert opkald er et spoergsmaal, der er
        // besvaret - og det var dét, der blev bedt om at slippe for.
        //
        // Kasseres opkaldet fra baandet, er spoergOmNavn falsk, og saa
        // kasseres det stadig: navnet maa ikke redde noget, man har sagt nej til.
        var opkaldsnavn = _session.Meta.Opkald ? _session.Meta.Title : null;

        var titel = forKort ? null
            : navn ?? (!spørgOmNavn ? null
                : !string.IsNullOrWhiteSpace(opkaldsnavn) ? opkaldsnavn
                : SpørgOmNavn());

        // ============ SEGMENTERNE REDDES, HALEN KOERER BAGEFTER ============
        //
        // _session.Stop() SAMLER segmenterne og SLETTER dem bagefter. Resten
        // af mødet ligger i dem, så de skal reddes FØR stoppet — det er en
        // filkopiering på nogle få megabyte og tager et øjeblik.
        //
        // SELVE UDSKRIVNINGEN MAA IKKE SKE HER. Halen er op til fem minutters
        // lyd, og det er op mod et minuts arbejde. Gjordes det her, ville
        // skærmen stå stille imens — og et program, der fryser, når man
        // trykker stop, ser ud som et, der har mistet mødet.
        //
        // Derfor: red segmenterne nu, luk optagelsen, og skriv resten ud
        // bagefter med en status, man kan følge.
        var reddet = RedResten();

        _session.Stop();
        _session.Dispose();
        _session = null;

        // KOERER VIDERE AF SIG SELV - MEN DET SIGES HOEJT.
        //
        // Den automatiske udskrivning gaar i gang i samme oejeblik og kigger
        // efter, om der allerede ligger en udskrift. Er halen ikke skrevet ned
        // endnu, finder den ingenting og laver hele moedet om.
        //
        // Maalt 27-08-2026: otte bidders arbejde blev gentaget, fordi de to
        // ting skete med et sekunds mellemrum. Se Jobs.Medskrivning.
        var hale = FaerdiggoerMedskrivning(mappe, reddet);

        if (reddet.Count > 0) Jobs.Medskrivning.Meld(mappe, hale);

        // null betyder «kassér». Det er brugerens svar paa navnedialogen, og
        // der er ikke noget at gemme — de trykkede optag for at proeve noget.
        var kasseret = titel is null;

        if (kasseret) Kasser(mappe);
        else
        {
            // ============ BRUGERENS NAVN VINDER ============
            //
            // HER STOD «kun hvis titlen er tom», OG SAA BLEV NAVNET ALDRIG
            // GEMT.
            //
            // Optagelsen faar et foreslaaet navn - «Moede 2. september» - i det
            // oejeblik den begynder, saa den kan findes, mens den koerer. Naar
            // moedet er slut, spoerges der om et rigtigt navn. Men titlen var
            // jo ikke tom laengere, saa svaret blev kasseret, og forslaget
            // blev staaende.
            //
            // Set 02-09-2026: brugeren skrev «Moede med Ibrar den 2.
            // september» og fik «Moede 2. september».
            //
            // DER SPOERGES KUN, FORDI SVARET SKAL BRUGES. Et spoergsmaal, hvis
            // svar bliver smidt vaek, er vaerre end intet spoergsmaal - man
            // opdager det foerst, naar man leder efter moedet i morgen.
            var meta = MeetingStore.Load(mappe);
            if (meta is not null && !string.IsNullOrWhiteSpace(titel))
            {
                meta.Title = titel.Trim();
                MeetingStore.Save(mappe, meta);
            }
        }

        KlarFelter.Visibility = Visibility.Visible;
        GenvejPanel.Visibility = GenvejTast.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        OptagFelter.Visibility = Visibility.Collapsed;
        StartKnap.Visibility = Visibility.Visible;
        WebinarKnap.Visibility = Visibility.Visible;
        UrPanel.Visibility = Visibility.Collapsed;
        PauseKnap.Visibility = Visibility.Collapsed;
        PauseKnap.Content = "❚❚ Pause";
        StopKnap.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");
        Ur.Text = "00:00:00";
        UrUnder.Text = "";


        if (kasseret)
        {
            Status.Text = forKort
                ? $"Optagelsen var under {ForKort.TotalSeconds:0} sekunder og er slettet."
                : $"Kasseret. {længde:hh\\:mm\\:ss} lyd er slettet.";

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

    /// <summary>
    /// «Skrevet ud til 16:40» — eller tom tekst, hvis der ikke skrives med.
    /// </summary>
    /// <remarks>
    /// DET SKAL KUNNE SES, MENS DET SKER. Medskrivningen var usynlig, og saa
    /// tvivler man paa, at den virker - med god grund: der er ingen maade at
    /// vide det paa. Set 27-08-2026, hvor brugeren spurgte, om den havde
    /// forberedt noget, og den havde skrevet syv bidder.
    ///
    /// Der staar et tidspunkt og ikke en procent. Et tidspunkt kan holdes op
    /// mod uret oeverst i baandet.
    /// </remarks>
    private string Medskrivningsstatus()
    {
        if (_medskrivere.Count == 0) return "";

        // Er der to spor, er det det mindst faerdige, der siger noget om,
        // hvor langt man reelt er.
        var naaet = _medskrivere.Values
            .Select(m => m.Naaet)
            .Where(t => t.Length > 0)
            .OrderBy(t => t, StringComparer.Ordinal)
            .FirstOrDefault();

        return naaet is null ? "" : $"Skrevet ud til {naaet}";
    }

    // ==================== MEDSKRIVNING ====================

    /// <summary>
    /// Sætter medskrivningen i gang — ét job pr. spor.
    /// </summary>
    /// <remarks>
    /// KUN NÅR SPROGET ER KENDT. Undervejs er der ingen at spørge, og en
    /// transskription på det forkerte sprog er vrøvl, der først opdages i
    /// referatet. Er sproget tomt, springes sporet over, og mødet skrives ud
    /// bagefter som altid.
    ///
    /// GÅR NOGET GALT, SKER DER INGENTING. Medskriveren giver op af sig selv
    /// og rører aldrig noget igen — optagelse må aldrig kunne blokeres.
    /// </remarks>
    private void StartMedskrivning()
    {
        _medskrivere.Clear();

        if (_session is null) return;

        try
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
            if (!install.IsComplete) return;

            var meta = MeetingStore.Load(_session.SessionDir);
            if (meta is null) return;

            // ============ ET TOMT SPROG ER IKKE ET FRAVALG ============
            //
            // MEDSKRIVNINGEN KOERTE ALDRIG, OG DET KOSTEDE EN HEL TIME.
            //
            // Den starter kun for et spor, der har et sprog. Blev der ikke
            // valgt et, da moedet begyndte, stod der en TOM streng i
            // moedefilen - ikke null, ikke «auto», bare tom - og saa sprang
            // den over. Begge spor.
            //
            // Maalt 02-09-2026 paa et moede paa 62 minutter: ValgtSprogMik og
            // ValgtSprogLoop stod begge tomme, mappen «segmenter» var tom, og
            // moedet stod som «ikke skrevet ud». Lyden var der - to spor, 119
            // MB hver, med tale paa begge - men intet var skrevet ned undervejs.
            //
            // Det er den SAMME faelde som i dikteringen samme dag: «MitSprog
            // ?? "da"» fangede kun null, og en tom streng slap igennem.
            //
            // Nu falder begge spor tilbage paa indstillingerne, hvor dansk er
            // standarden. Et sprog, der ikke er valgt, er ikke et fravalg -
            // det er et sprog, ingen har taget stilling til.
            var v = AppSettings.Current;

            var sprog = new Dictionary<string, string?>
            {
                ["mikrofon"] = _erWebinar
                    ? null
                    : string.IsNullOrWhiteSpace(meta.ValgtSprogMik)
                        ? v.Talesprog
                        : meta.ValgtSprogMik,

                ["loopback"] = string.IsNullOrWhiteSpace(meta.ValgtSprogLoop)
                    ? v.Deresprog
                    : meta.ValgtSprogLoop,
            };

            foreach (var (spor, kode) in sprog)
            {
                if (string.IsNullOrWhiteSpace(kode)) continue;

                var m = new Jobs.Medskriver(_session.SessionDir, spor, kode!, install);
                m.Start();
                _medskrivere[spor] = m;
            }
        }
        catch (Exception ex)
        {
            _medskrivere.Clear();

            // TAVSHEDEN VAR FEJLEN. Uden det her kan "medskrivningen koerte
            // ikke" ikke skelnes fra "den gik i stykker ved opstart", og det
            // kostede en time at finde ud af 27-08-2026.
            try
            {
                Historik.Skriv(HaendelseType.Transskription,
                    "Medskrivning kunne ikke startes", ex.Message, Udfald.SeEfter);
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// Redder de segmenter, medskrivningen ikke nåede — før de bliver slettet.
    /// </summary>
    /// <returns>Spor → medskriveren, der nu peger på en kopi. Tom = ingenting.</returns>
    private Dictionary<string, Jobs.Medskriver> RedResten()
    {
        var ud = new Dictionary<string, Jobs.Medskriver>();

        foreach (var (spor, m) in _medskrivere)
        {
            try
            {
                if (m.RedResten()) ud[spor] = m;
                else m.Dispose();
            }
            catch (Exception)
            {
                m.Dispose();
            }
        }

        _medskrivere.Clear();
        return ud;
    }

    /// <summary>
    /// Skriver halen ud og lægger den færdige udskrift ned.
    /// </summary>
    /// <remarks>
    /// KØRER EFTER, AT MØDET ER LUKKET. Skærmen er fri imens, og bjælken
    /// nederst siger, hvad der sker og hvor langt der er igen.
    ///
    /// Går noget galt, sker der ingenting: filerne findes ikke, og mødet
    /// skrives ud bagefter på den almindelige måde. Det er hele sikkerheden i
    /// medskrivningen — den kan altid fravælges, og resultatet er det samme.
    /// </remarks>
    private static async Task FaerdiggoerMedskrivning(
        string mappe, Dictionary<string, Jobs.Medskriver> reddet)
    {
        if (reddet.Count == 0) return;

        var faerdige = new Dictionary<string, string>();

        // ============ MAN SKAL KUNNE SE, AT DEN ER I GANG ============
        //
        // Hele pointen med medskrivning er, at det meste er gjort, naar mødet
        // slutter. Det er ingenting værd, hvis skærmen ikke siger det — så
        // sidder man og venter på noget, der allerede er ved at være færdigt.
        //
        // Tallet er REGNET, ikke gættet: det er den lyd, der er tilbage, delt
        // med den hastighed, de foregående bidder faktisk kørte med. Et gæt
        // ville være værre end intet tal.
        var tilbage = reddet.Values.Sum(m => m.SekunderTilbage());

        Jobs.Udskriftsvagt.Start(mappe, "Transskription",
            tilbage > 90
                ? $"Skrevet med undervejs — der mangler cirka {Math.Ceiling(tilbage / 60.0):0} min"
                : "Skrevet med undervejs — gør resten færdig nu");

        try
        {
            foreach (var (spor, m) in reddet)
            {
                try
                {
                    if (await m.AfslutAsync() is { } json) faerdige[spor] = json;
                }
                catch (Exception)
                {
                    // Glem det spor. Det skrives ud bagefter.
                }
                finally
                {
                    m.Dispose();
                }
            }

            LaegMedskrevetNed(mappe, faerdige);
        }
        catch (Exception)
        {
            // Intet tabt: uden filerne koerer den almindelige vej.
        }
        finally
        {
            Jobs.Udskriftsvagt.Slut();
            Jobs.Medskrivning.Faerdig(mappe);
        }
    }

    /// <summary>
    /// Lægger den medskrevne udskrift ved siden af lyden.
    /// </summary>
    /// <remarks>
    /// FILERNE HEDDER DET SAMME, SOM DEN ALMINDELIGE VEJ VILLE KALDE DEM.
    /// Derfor opdager genbrugsreglen dem af sig selv, og der er ikke rørt ved
    /// transskriptionsskærmen overhovedet — se TranscribeView.KanGenbruges.
    ///
    /// Går skrivningen galt, sker der ingenting: filerne findes ikke, og
    /// mødet skrives ud bagefter som altid.
    /// </remarks>
    private static void LaegMedskrevetNed(string mappe, Dictionary<string, string> medskrevet)
    {
        if (medskrevet.Count == 0) return;

        try
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
            var model = install.ModelFileName is { } f
                ? System.IO.Path.GetFileNameWithoutExtension(f).Replace("ggml-", "")
                : null;

            if (model is null) return;

            foreach (var (spor, json) in medskrevet)
            {
                var udbase = System.IO.Path.Combine(mappe, $"{spor}_{model}");

                System.IO.File.WriteAllText(udbase + ".json", json, System.Text.Encoding.UTF8);
                System.IO.File.WriteAllText(udbase + ".txt",
                    NoteApp.Core.Medskrift.SomTekst(json), System.Text.Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // Kan den ikke laegges ned, skrives moedet ud bagefter. Intet tabt.
        }
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

        HoldOejeMedStilheden();

        Opdateret?.Invoke();
    }

    // ------------------------------------------------- webinaret stopper selv

    /// <summary>
    /// Svarene fra opstartsdialogen: hvad optagelsen er, hvor den hører til,
    /// og hvad der bliver talt.
    /// </summary>
    public sealed record Opstart(string Sprog, string? Kilde, string? Mappe, string? Moedetype,
                                 bool ErWebinar);

    /// <summary>
    /// Skriver svarene ind på optagelsen — også hvis den allerede kører.
    ///
    /// ALT GEMMES MED DET SAMME. Filen skrives, mens der optages, og ikke
    /// først når mødet stoppes: dør maskinen undervejs, er mappen og sproget
    /// det, der gør en genoprettet optagelse til andet end en lydfil uden
    /// ophav.
    ///
    /// Sproget lægges på begge spor ved et møde. De to sider taler som regel
    /// det samme, og kan de ikke det, rettes det, når der skrives ud — dér
    /// spørges der om hvert spor for sig. Ved et webinar er der kun ét spor,
    /// højttalerens, og mikrofonens felt skal blive stående tomt: et sprog på
    /// et spor, der ikke findes, ville forvirre genbruget senere.
    /// </summary>
    private void Skriv_Opstart(Opstart o)
    {
        if (_session is null) return;

        var m = _session.Meta;

        m.Mappe = o.Mappe;
        m.Moedetype = o.Moedetype;
        m.Kilde = o.Kilde;

        m.ValgtSprogLoop = o.Sprog;
        if (!o.ErWebinar) m.ValgtSprogMik = o.Sprog;

        try { MeetingStore.Save(_session.SessionDir, m); }
        catch (IOException)
        {
            // En optagelse maa ikke falde over en fil, der ikke kunne skrives.
            // Den skrives igen, naar optagelsen stoppes.
        }
    }

    /// <summary>
    /// Hvor længe der skal være stille, før et webinar regnes for slut.
    ///
    /// FEM MINUTTER ER IKKE ET TILFÆLDIGT TAL.
    ///
    /// Et webinar har pauser: en oplægsholder, der tier mens et videoklip
    /// loader, et spørgsmål ingen svarer på, en teknisk afbrydelse. En
    /// optagelse, der stopper efter tredive sekunders stilhed, ville skære
    /// midt i — og man opdager det først, når udskriften mangler den sidste
    /// halvdel.
    ///
    /// Prisen for at vente for længe er fem minutters tomhed i filen, og den
    /// bliver klippet af igen. Prisen for at stoppe for tidligt er et halvt
    /// webinar. De to fejl er ikke lige store.
    /// </summary>
    private static readonly TimeSpan Stilhedsgraense = TimeSpan.FromMinutes(5);

    private DateTime? _stilhedFra;
    private bool _erWebinar;

    /// <summary>
    /// De medskrivere, der kører nu — ét pr. spor. Tom, når der ikke optages.
    /// </summary>
    /// <remarks>
    /// Se <see cref="Jobs.Medskriver"/>. De starter først, når sproget er
    /// KENDT: en transskription på det forkerte sprog er vrøvl, og undervejs
    /// er der ingen at spørge.
    /// </remarks>
    private readonly Dictionary<string, Jobs.Medskriver> _medskrivere = new();

    /// <summary>
    /// Stopper et webinar, når der ikke har været lyd i fem minutter.
    ///
    /// Kun for webinarer. Et møde kan sagtens have fem minutters stilhed —
    /// nogen deler en skærm, alle læser. At stoppe dét ville være at afbryde
    /// mødet.
    /// </summary>
    private void HoldOejeMedStilheden()
    {
        if (!_erWebinar || _session is null || _session.IsPaused) return;
        if (_session.Loopback is not { } spor) return;

        // ReadPeak nulstiller maaleren, saa den skal kun laeses eet sted.
        // Ingen andre i appen laeser den under optagelse.
        var niveau = spor.ReadPeak();

        if (niveau > AudioDevices.SilenceThreshold)
        {
            _stilhedFra = null;

            // ER DER LYD, ER DER INTET AT ADVARE OM - saa er pladsen ledig til
            // at vise, hvor langt medskrivningen er naaet. Stilhedsadvarslen
            // vinder, naar den er der: den handler om, at optagelsen kan vaere
            // tom, og det er vaerre end alt andet.
            _baand?.Meld(Medskrivningsstatus());
            return;
        }

        _stilhedFra ??= DateTime.Now;

        var stille = DateTime.Now - _stilhedFra.Value;

        if (stille < Stilhedsgraense)
        {
            // ============ DER SIGES TIL TO GANGE ============
            //
            // FØRSTE MELDING KOMMER EFTER 45 SEKUNDER, og den peger på den
            // fejl, der faktisk sker. Målt paa et rigtigt webinar 21-08-2026:
            // afspilleren var sat paa mute, og saa afleverer programmet ingen
            // lyd — der er intet for Windows at kopiere. Optagelsen loeb i tre
            // minutter uden indhold, og det blev opdaget ved et tilfaelde.
            //
            // Foer stod der intet foer efter to en halv minut, og saa hed det
            // «stopper om 2 min» — en nedtaelling, ikke et raad. Den fortalte
            // hverken, hvad der var galt, eller hvad man kunne goere.
            //
            // 45 sekunder er valgt, fordi et webinar sjaeldent er helt tavst
            // saa laenge, og fordi meldingen forsvinder af sig selv i samme
            // sekund, der kommer lyd. En melding for meget koster ingenting;
            // en for lidt koster optagelsen.
            if (stille >= TimeSpan.FromMinutes(2.5))
            {
                var igen = Stilhedsgraense - stille;
                _baand?.Meld($"Ingen lyd i {stille.Minutes} min — stopper om {Math.Max(1, (int)igen.TotalMinutes)} min");
            }
            else if (stille >= TimeSpan.FromSeconds(45))
            {
                _baand?.Meld("Ingen lyd — er webinaret sat på mute?");
            }

            return;
        }

        _stilhedFra = null;
        StopWebinaret();
    }

    /// <summary>
    /// Slutter webinaret af sig selv og klipper tavsheden af.
    ///
    /// Der spørges ikke om et navn. Hele pointen er, at man er et andet sted —
    /// en dialog, der venter på et svar, ville efterlade optagelsen ugemt,
    /// indtil nogen kom tilbage.
    /// </summary>
    private void StopWebinaret()
    {
        if (_session is null) return;

        var mappe = _session.SessionDir;
        var tid = DateTime.Now.ToString("d. MMMM 'kl.' HH:mm");

        Stop(spørgOmNavn: false, navn: $"Webinar {tid}");

        // Klippet sker EFTER, at sporene er samlet. Foer det findes filen ikke.
        try
        {
            var wav = Path.Combine(mappe, "loopback.wav");
            var klippet = Stilhedsklip.KlipHalen(wav);

            Status.Text = klippet > 0
                ? $"Webinaret er slut. {klippet / 60:0.0} minutters stilhed er klippet af."
                : "Webinaret er slut.";
        }
        catch (Exception)
        {
            // Kan halen ikke klippes, er optagelsen der stadig - bare laengere.
            Status.Text = "Webinaret er slut.";
        }
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

    // ========================= SPROGET =========================
    //
    // Selve mekanikken ligger i NoteApp.Core.Sprog og i Sprogbinding.cs.
    // Her er kun knappen og flaget paa den.

    private void Sprog_Click(object sender, RoutedEventArgs e) => Sprogmenu.Vis(SprogKnap);

    /// <summary>
    /// Åbner hjælpen. Den er ikke modal — man slår noget op MENS man
    /// arbejder, og et vindue, der spærrer for appen, tvinger en til at lukke
    /// hjælpen for at prøve det, man lige har læst.
    /// </summary>
    private void Hjaelp_Click(object sender, RoutedEventArgs e) =>
        Help.HjaelpWindow.Aabn(Window.GetWindow(this));

    // ------------------------------------------------------ lyst og mørkt

    /// <summary>
    /// Skifter mellem lyst og mørkt.
    /// </summary>
    /// <remarks>
    /// KNAPPEN SÆTTER ET UDTRYKKELIGT VALG og efterlader «følg Windows».
    /// Alternativet — at et tryk kun gjaldt indtil Windows skiftede — ville
    /// betyde, at valget blev rullet tilbage af sig selv ved solnedgang, uden
    /// at nogen havde rørt noget. Vil man tilbage til at følge systemet, står
    /// den mulighed under Indstillinger.
    /// </remarks>
    private void Tema_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.Tema = Temaskift.ErLyst ? Temavalg.Moerkt : Temavalg.Lyst;
        AppSettings.Current.Save();

        Temaskift.Anvend();
    }

    /// <summary>
    /// Sætter månen eller solen på knappen efter, hvad et tryk ville give.
    /// </summary>
    private void VisTemaikon()
    {
        var lyst = Temaskift.ErLyst;

        // U+E708 QuietHours (maane), U+E706 Brightness (sol). Begge
        // efterproevet mod cmap-tabellen i segmdl2.ttf.
        TemaIkon.Text = lyst ? "\uE708" : "\uE706";

        var tekst = NoteApp.Core.Sprog.T(lyst ? "topbar.tilmoerkt" : "topbar.tillyst");
        TemaKnap.ToolTip = tekst;
        System.Windows.Automation.AutomationProperties.SetName(TemaKnap, tekst);
    }

    /// <summary>
    /// Sætter flaget på knappen efter det sprog, der vises nu.
    ///
    /// Kaldes ved opstart og hver gang sproget skifter. Flaget er ikke bundet
    /// som teksterne er, fordi det ikke er en streng — det er en tegning, der
    /// skal vælges.
    /// </summary>
    private void VisSprogflag()
    {
        var nu = NoteApp.Core.Sprog.Kode;

        var valgt = NoteApp.Core.Sprog.Tilgaengelige()
            .FirstOrDefault(s => string.Equals(s.Kode, nu, StringComparison.OrdinalIgnoreCase));

        SprogFlag.Kode = valgt?.Flag ?? nu.ToUpperInvariant();
    }

    /// <summary>
    /// Klokkens liste, så længe den er fremme.
    /// </summary>
    /// <remarks>
    /// ET ANDET KLIK PAA KLOKKEN SKAL LUKKE LISTEN, og det er kun ligetil,
    /// hvis man ved, at listen ER fremme.
    ///
    /// Her stod foer et tidsstempel og en spaerre paa et kvart sekund. Den
    /// fandtes, fordi listen lukkede sig selv i det oejeblik, der blev
    /// klikket ved siden af — og klokken er ved siden af. Naar
    /// Klokke_Click saa koerte, var listen vaek, og den blev aabnet igen.
    ///
    /// Nu lukker listen ikke oejeblikkeligt; den glider ud, og IsOpen staar
    /// stadig sandt imens. Saa er svaret det enkle: er den fremme, lukkes
    /// den, og der aabnes ikke en ny. Spaerren paa tid er vaek.
    /// </remarks>
    private System.Windows.Controls.Primitives.Popup? _klokkerude;

    private void Klokke_Click(object sender, RoutedEventArgs e)
    {
        if (_klokkerude is { IsOpen: true } fremme)
        {
            Glid.LukRude(fremme);
            return;
        }

        var panel = new Notifications.NotificationPopup();

        var pop = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = KlokkeKnap,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            HorizontalOffset = -400,
            VerticalOffset = 6,
            StaysOpen = false,
            Child = panel
        };

        // Glidningen skal saettes op, FOER ruden aabnes. Den overtager
        // lukningen, og den kan ikke overtage noget, der allerede er sket.
        Glid.Rude(pop);
        _klokkerude = pop;

        panel.HistorikOenskes += () =>
        {
            Glid.LukRude(pop);
            if (Window.GetWindow(this) is MainWindow hoved) hoved.GaaTilHistorik();
        };

        pop.IsOpen = true;

        // ============ HER STOD «MARKER ALT SOM SET» ============
        //
        // Beskederne blev laest, fordi man AABNEDE klokken. Saa forsvandt tre,
        // fordi man kiggede efter den ene, man ventede paa - og de to andre
        // havde man aldrig set.
        //
        // Nu bliver en besked kun laest af, at man trykker paa den, eller af
        // knappen «Alle laest». Fjernet 28-08-2026.
    }
}
