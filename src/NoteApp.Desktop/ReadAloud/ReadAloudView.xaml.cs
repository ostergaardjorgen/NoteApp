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
    private ScriptDocument _script;
    private readonly DispatcherTimer _timer;

    private RecordingSession? _session;
    private int _afsnitIndex;
    private int _sidsteBlok = -1;


    public ReadAloudView()
    {
        InitializeComponent();

        _script = ScriptDocument.Load();

        var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
        MikrofonNavn.Text = mik?.FriendlyName ?? "ingen mikrofon fundet";
        OptagKnap.IsEnabled = mik is not null;

        // Knappen nederst er stopknappen. Den vises foerst, naar der er noget
        // at stoppe; paa forsiden ligger handlingen i detaljen under boksene.
        OptagKnap.Visibility = Visibility.Collapsed;

        // VisTekst EFTER mikrofonen er slaaet op: startknappen i detaljen
        // spejler OptagKnap.IsEnabled, og koerte den foer, stod den aktiv paa
        // en maskine uden mikrofon.
        VisTekst();

        if (mik is null)
            Status.Text = "Der er ingen mikrofon. Tilslut en, og genstart appen.";

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Opdater();

        Loaded += (_, _) => Focus();
    }

    public bool IsRecording => _session?.IsRecording == true;

    // ------------------------------------------------------------ tekstvalg

    /// <summary>
    /// Skifter oplæsningstekst. Kan ikke ske under en optagelse — teksten er
    /// det, transskriptionen holdes op imod, og skiftes den midtvejs, måler
    /// optagelsen ingenting.
    /// </summary>
    /// <summary>Hvilket af de tre kort der er valgt.</summary>
    private int ValgtIndex =>
        Valg2.IsChecked == true ? 2 : Valg1.IsChecked == true ? 1 : 0;

    private void Vaelg(int index)
    {
        var knap = index switch { 2 => Valg2, 1 => Valg1, _ => Valg0 };
        knap.IsChecked = true;    // Tekst_Valgt henter teksten
    }

    /// <summary>
    /// Knappen på et kort: vælg teksten OG gå i gang. Det er hele pointen med
    /// at have den dér — at man ikke skal videre til et andet hjørne af
    /// skærmen for at gøre det, boksen handler om.
    /// </summary>
    private void KortStart_Click(object sender, RoutedEventArgs e)
    {
        if (IsRecording) return;   // knapperne er skjult under optagelse
        if (sender is not Button b || !int.TryParse(b.Tag?.ToString(), out var index)) return;

        Vaelg(index);
        StartOptagelse();
    }

    private void Tekst_Valgt(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        if (IsRecording)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Optagelsen kører", "Teksten kan ikke skiftes, mens der optages.\n\n" +
                "Stop optagelsen først — det, du har læst, bliver gemt.", Dialogs.Slags.Pas_paa);
            return;
        }

        var valgt = ScriptDocument.Available[ValgtIndex];
        try
        {
            _script = ScriptDocument.Load(valgt.File);
        }
        catch (Exception ex)
        {
            // Teksten kan mangle, hvis repo-mappen er flyttet OG appen er bygget
            // uden den indlejrede kopi. Sig hvad der mangler frem for at falde.
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Teksten kunne ikke hentes", ex.Message, Dialogs.Slags.Pas_paa);
            return;
        }

        _afsnitIndex = 0;
        _sidsteBlok = -1;

        // Et tekstskift lukker kvitteringen: den hoerer til den forrige
        // oplaesning, og at lade den staa ville vise tal for noget andet end
        // det, der nu er valgt.
        KvitteringPanel.Visibility = Visibility.Collapsed;
        VejledningPanel.Visibility = Visibility.Visible;

        VisTekst();
    }

    /// <summary>Alt, der afhænger af hvilken tekst der er valgt, ét sted.</summary>
    private void VisTekst()
    {
        var valgt = ScriptDocument.Available[ValgtIndex];
        var minutter = (int)Math.Round(_script.EstimatedDuration.TotalMinutes);

        Overskrift.Text = "Lær appen dine ord at kende";
        Indledning.Text =
            "Læs en prøvetekst højt. Så kender appen facit og kan måle præcis, hvor mange af " +
            "dine ord den rammer — det kan den ikke på et rigtigt møde. Det er frivilligt; " +
            "appen virker uden.";

        VisKort();
        VisDetalje(valgt, minutter);

        if (_session is null && OptagKnap.IsEnabled)
            Status.Text = $"{_script.Blocks.Count} blokke, {_script.Paragraphs.Count} afsnit.";
    }

    /// <summary>
    /// De tre bokse: hvor står jeg med hver enkelt tekst.
    ///
    /// Tallet står PÅ boksen, fordi det er dét, man kigger efter. En boks, der
    /// bare siger «indtalt», tvinger et klik for at få det at vide, man kom
    /// efter.
    /// </summary>
    private void VisKort()
    {
        var status = new[] { Status0, Status1, Status2 };
        var tal = new[] { Tal0, Tal1, Tal2 };
        var forvent = new[] { Forvent0, Forvent1, Forvent2 };

        for (var i = 0; i < ScriptDocument.Available.Count && i < 3; i++)
        {
            var t = Tilstand(i);

            tal.ElementAt(i).Text = t.Maaling is null ? "" : $"{t.Maaling.Procent:0.0} %";
            tal.ElementAt(i).Foreground = t.Maaling is null
                ? (Brush)FindResource("TekstMeget")
                : (Brush)FindResource(Traeningsmaaling.Farve(t.Maaling.Procent));

            (status[i].Text, var farve) =
                  t.Maaling is not null ? ("målt", "Godkendt")
                : t.Mappe is not null   ? ("læst op — mangler at blive skrevet ud", "Advarsel")
                : i == 0                ? ("anbefalet · 15-20 min", "Accent")
                                        : ("valgfri · 5 min", "TekstMeget");

            status[i].Foreground = (Brush)FindResource(farve);

            forvent[i].Text = Forventning(i, t.Maaling);
        }
    }

    /// <summary>
    /// Hvad man kan forvente — FØR man læser op.
    ///
    /// HVORFOR DET SKAL STÅ DÉR
    ///
    /// Uden et pejlemærke er 92 % skuffende. Man har lige brugt tyve minutter
    /// på at læse tydeligt op, og så mangler der stadig 180 ord — det ligner
    /// noget, der gik galt. Med tallet på forhånd er 92 % dét, der skulle ske.
    ///
    /// HVORFOR TALLENE ER OMTRENTLIGE
    ///
    /// De kommer fra faktiske målinger: dansk 92,3 %, engelsk 90,5 %, blandet
    /// 83,1 %. Men det er MÅLT PÅ ÉN STEMME, én mikrofon og én maskine. Et
    /// præcist tal ville være en påstand om noget, der ikke er målt på
    /// brugerens stemme — derfor et interval, og derfor ordet «typisk».
    ///
    /// Er teksten allerede målt, står forklaringen i stedet: dér er der ikke
    /// længere brug for et skøn, for tallet står ved siden af.
    /// </summary>
    private static string Forventning(int index, Traeningsmaaling? målt)
    {
        if (målt is not null)
        {
            return målt.Procent >= 90 ? "som forventet — så godt bliver det"
                 : målt.Procent >= 80 ? "lidt under det typiske — se sætningerne"
                 : "under det forventede — tjek mikrofonen";
        }

        return index switch
        {
            0 => "typisk 90-93 %",
            1 => "typisk 80-85 % — sprogskiftet koster",
            _ => "typisk 88-92 %"
        };
    }

    /// <summary>Optagelsen og målingen for en af de tre tekster, hvis de findes.</summary>
    private static (string? Mappe, string? Titel, Traeningsmaaling? Maaling) Tilstand(int index)
    {
        if (index >= ScriptDocument.Available.Count) return (null, null, null);

        var nøgle = ScriptDocument.Available[index].Key;
        if (!Directory.Exists(UserDataPaths.Meetings)) return (null, null, null);

        foreach (var mappe in Directory.GetDirectories(UserDataPaths.Meetings))
        {
            var meta = MeetingStore.Load(mappe);
            if (Transcribe.Optagelsesgruppe.Traeningsnoegle(mappe, meta) != nøgle) continue;

            var titel = meta?.Title ?? Path.GetFileName(mappe);
            var fil = Transcribe.Optagelsesgruppe.Manuskriptfil(nøgle);

            Traeningsmaaling? m = null;
            if (fil is not null)
            {
                try { m = Traeningsmaaling.Laes(mappe, ScriptDocument.RåTekst(fil)); }
                catch (Exception) { }
            }

            return (mappe, titel, m);
        }

        return (null, null, null);
    }

    /// <summary>
    /// Indholdet under boksene. Præcis ét af de tre trin vises.
    /// </summary>
    private void VisDetalje(
        (string File, string Key, string Name, string Why, string Next) valgt, int minutter)
    {
        var (mappe, titel, maaling) = Tilstand(ValgtIndex);

        _detalje?.Luk();

        TrinLaes.Visibility = Visibility.Collapsed;
        TrinSkrivUd.Visibility = Visibility.Collapsed;
        TrinResultat.Visibility = Visibility.Collapsed;

        // 3 · Målt. Der er noget at se på, og noget at gøre ved det.
        if (mappe is not null && maaling is not null)
        {
            if (_detalje is null)
            {
                _detalje = new Training.TrainingDetail();

                // Er der rettet noget i vinduet, skal boksene og tallet passe
                // bagefter. Uden det stod der stadig det gamle, indtil man
                // skiftede skærm og tilbage igen.
                _detalje.Aendret += VisTekst;
            }

            TrinResultat.Content = _detalje;
            TrinResultat.Visibility = Visibility.Visible;

            _detalje.Vis(mappe, titel ?? valgt.Name, maaling);
            return;
        }

        // 2 · Læst op, men ikke skrevet ud. Ét skridt mangler, og der er én knap.
        if (mappe is not null)
        {
            TrinSkrivUd.Visibility = Visibility.Visible;
            SkrivUdTekst.Text =
                $"Du har læst «{valgt.Name}» op, men lyden er ikke blevet til tekst endnu. " +
                "Først dér kan appen sammenligne med manuskriptet og sige, hvor mange ord den ramte. " +
                "Det tager nogle minutter og kører på denne pc.";
            _venterMappe = mappe;
            return;
        }

        // 1 · Ikke læst op endnu. Hvorfor er den værd at læse, og en knap.
        TrinLaes.Visibility = Visibility.Visible;
        HvorforOverskrift.Text = $"Hvorfor «{valgt.Name}»";
        HvorforLaengde.Text = valgt.Why;

        // FORVENTNINGEN, saa tallet ikke kommer som en skuffelse.
        //
        // 92 % lyder som en daarlig karakter, hvis man tror, 100 er maalet. Det
        // er det ikke: to mennesker, der skriver den samme optagelse ned, er
        // heller ikke enige om hvert ord. Staar det FOER man laeser op, er 92 %
        // en bekraeftelse i stedet for et nederlag.
        HvorforFuld.Text = valgt.Next + "\n\n" +
            $"Hvad du kan forvente: {Forventning(ValgtIndex, null)}. " +
            "Hundrede procent findes ikke — heller ikke mellem to mennesker, der skriver " +
            "den samme optagelse ned. Det, der flytter tallet, er mikrofonen og afstanden " +
            "til den, ikke hvor tydeligt du artikulerer.";

        StartKnap.IsEnabled = OptagKnap.IsEnabled;
        StartUnder.Text = $"{_script.TotalWords} ord · cirka {minutter} minutter";
    }

    private Training.TrainingDetail? _detalje;
    private string? _venterMappe;

    private void StartValgte_Click(object sender, RoutedEventArgs e) => StartOptagelse();

    private void SkrivUd_Click(object sender, RoutedEventArgs e)
    {
        if (_venterMappe is null) return;
        TranskriptionØnskes?.Invoke(_venterMappe);
    }

    /// <summary>
    /// Hvor meget der er indtalt indtil nu, og hvilke af de tre tekster der er
    /// dækket. Læses af mapperne frem for at blive gemt et sted for sig — så
    /// kan tallet ikke komme ud af trit med det, der faktisk ligger på disken.
    /// </summary>
    private static (double Minutter, HashSet<string> Laest) OptagetIndtilNu()
    {
        var læst = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double minutter = 0;

        if (!Directory.Exists(UserDataPaths.Meetings)) return (0, læst);

        foreach (var mappe in Directory.GetDirectories(UserDataPaths.Meetings))
        {
            var meta = MeetingStore.Load(mappe);
            if (meta is null) continue;

            // Tre kilder, i den rækkefølge de er til at stole på.
            //
            //   1. Feltet. Det er appens eget og kan ikke ændres af en omdøbning.
            //   2. Titlen. Sådan blev det gemt indtil 13. august.
            //   3. MAPPENAVNET. Det ændrer sig ikke, når en optagelse omdøbes.
            //
            // Punkt 3 er ikke pynt: den første oplæsning hed «Fase0-oplaesning»
            // og blev omdøbt til «Første oplæsning». Så forsvandt den fra
            // tællingen — den var der, men appen kunne ikke længere se det.
            // Titlen er brugerens og skal kunne hedde hvad som helst.
            var kilde = meta.ReadAloudScript;

            if (kilde is null)
            {
                var navn = (meta.Title ?? "") + " " + Path.GetFileName(mappe);

                var erOplæsning =
                    navn.Contains("Oplæsning", StringComparison.OrdinalIgnoreCase) ||
                    navn.Contains("Oplaesning", StringComparison.OrdinalIgnoreCase);

                if (!erOplæsning) continue;

                // Findes ingen nøgle i navnet, er det den gamle enkelttekst —
                // dengang fandtes kun den danske.
                kilde = ScriptDocument.Available
                    .Where(t => navn.Contains(t.Key, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.Key)
                    .FirstOrDefault() ?? "dansk";
            }

            minutter += meta.DurationSeconds / 60.0;
            læst.Add(kilde);
        }

        return (minutter, læst);
    }

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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke optage", "Ingen mikrofon fundet.", Dialogs.Slags.Pas_paa);
            return;
        }

        if (fallback)
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Mikrofonen er skiftet",
                "Den mikrofon, du havde valgt under Indstillinger, er ikke tilsluttet. " +
                "Der optages i stedet fra:\n\n" + mik.FriendlyName,
                godkend: "Læs op med den", annuller: "Stop — jeg retter det",
                slags: Dialogs.Slags.Pas_paa);

            if (!ja) return;
        }

        // Oplæsning er per definition et fysisk møde: ét spor, kun mikrofonen.
        // Der er ingen anden part, og derfor intet loopback-spor at fejle på.
        // Titlen baerer, HVILKEN tekst der blev laest. Uden den kan skaermen
        // ikke vise, hvad der mangler — og saa er de tre kort bare tre knapper.
        _session = RecordingSession.Create(
            MeetingType.Physical,
            // Titlen er det, brugeren SER i listen over optagelser, og derfor
            // staves den med æ. At den samtidig bruges til at genkende, hvilken
            // tekst der blev læst, er en teknisk detalje — den må ikke koste
            // en stavefejl på skærmen.
            "Oplæsning — " + ScriptDocument.Available[ValgtIndex].Name,
            mik, null);

        // NØGLEN gemmes i sit eget felt. Titlen er brugerens og må hedde hvad
        // som helst; feltet her er appens og overlever en omdøbning.
        var metaNy = MeetingStore.Load(_session.SessionDir);
        if (metaNy is not null)
        {
            metaNy.ReadAloudScript = ScriptDocument.Available[ValgtIndex].Key;
            MeetingStore.Save(_session.SessionDir, metaNy);
        }
        _session.IncidentOccurred += i => Dispatcher.Invoke(() =>
            Status.Text = $"Hændelse ved {TimeSpan.FromSeconds(i.AtSeconds):mm\\:ss}: {i.What}");

        _session.Start();

        _afsnitIndex = 0;
        _sidsteBlok = -1;
        VejledningPanel.Visibility = Visibility.Collapsed;
        KvitteringPanel.Visibility = Visibility.Collapsed;
        LaesePanel.Visibility = Visibility.Visible;

        // Nu — og kun nu — er knappen nederst relevant: den er stopknappen.
        // Paa forsiden stod den som en anden vej til det samme, og saa er der
        // to knapper til een handling.
        OptagKnap.Visibility = Visibility.Visible;
        OptagKnap.Content = "■ Stop og gem";
        NaesteKnap.IsEnabled = true;
        ForrigeKnap.IsEnabled = false;
        PauseKnap.Visibility = Visibility.Visible;
        PauseKnap.Content = "❚❚ Pause";
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
        OptagKnap.Visibility = Visibility.Collapsed;   // handlingen bor paa kortene
        NaesteKnap.IsEnabled = false;
        ForrigeKnap.IsEnabled = false;
        PauseKnap.Visibility = Visibility.Collapsed;
        LaesePanel.Visibility = Visibility.Collapsed;
        OptagerPrik.Fill = (Brush)FindResource("TekstMeget");

        VisKvittering(mappe, længde, noter);
    }

    // ---------------------------------------------------------- kvitteringen

    private string? _sidsteOptagelse;

    /// <summary>
    /// Kvitteringen efter en oplæsning.
    ///
    /// Den findes, fordi den forrige udgave sluttede med ingenting: en dialog
    /// spurgte, om mappen skulle åbnes, og så stod man i en mappe med tre filer
    /// uden at vide, hvad der skulle ske nu. Tyve minutters oplæsning er et
    /// stykke arbejde, og et stykke arbejde skal kvitteres og pege videre.
    /// </summary>
    private void VisKvittering(string mappe, TimeSpan længde, int blokke)
    {
        _sidsteOptagelse = mappe;

        VejledningPanel.Visibility = Visibility.Collapsed;
        KvitteringPanel.Visibility = Visibility.Visible;

        var læste = _afsnitIndex + 1;
        var helt = læste >= _script.Paragraphs.Count;
        var ord = _script.Paragraphs.Take(læste).Sum(p => p.WordCount);
        var tempo = længde.TotalMinutes > 0 ? ord / længde.TotalMinutes : 0;

        BlokTitel.Text = "Optagelsen er gemt";
        BlokUnder.Text = $"{længde:hh\\:mm\\:ss} lyd, {blokke} blokmærker";

        KvitOverskrift.Text = helt ? "Godt læst op — hele teksten er i hus" : "Optagelsen er gemt";
        KvitUnder.Text = helt
            ? "Appen har nu din stemme på det materiale, den kan måles på."
            : $"Du nåede {læste} af {_script.Paragraphs.Count} afsnit. Det er gemt, og du kan læse resten en anden gang — det bliver bare en ny optagelse.";

        KvitLaengde.Text = længde.ToString(@"mm\:ss");
        KvitAfsnit.Text = $"{læste}/{_script.Paragraphs.Count}";
        KvitTempo.Text = $"{tempo:0}";

        // Vurderingen skal sige, hvad tallene BETYDER. "05:11" er ikke en
        // vurdering; "hele teksten er med" er.
        //
        // Maalestokken er TEKSTEN, ikke et fast antal minutter. Foerste udgave
        // sammenlignede med 15 minutter uanset hvad, og saa fik en fuldt laest
        // 5-minutters tekst besked om, at den var for kort. Den var praecis saa
        // lang, som den skulle vaere.
        var nok = helt;
        KvitVurdering.Text = helt
            ? $"Hele teksten er læst — {længde:mm\\:ss} lyd. Læste du hurtigere end de 120 ord i minuttet, teksten er sat efter, betyder det ikke noget: det er ordene, der tælles, ikke minutterne."
            : $"Du nåede {læste} af {_script.Paragraphs.Count} afsnit. Det er gemt og kan godt transskriberes, men resten af teksten er ikke målt. Læs den færdig en anden gang — det bliver en ny optagelse.";
        KvitVurdering.Foreground = (Brush)FindResource(nok ? "Godkendt" : "Advarsel");

        Status.Text = helt
            ? $"{længde:mm\\:ss} optaget — hele «{ScriptDocument.Available[ValgtIndex].Name}» er i hus."
            : $"{længde:mm\\:ss} optaget, {læste} af {_script.Paragraphs.Count} afsnit.";

        // Naeste tekst: den foerste af de tre, der IKKE er indtalt endnu. Er de
        // alle taget, peges der paa den naeste i raekken — en tekst kan laeses
        // igen, og mere materiale er ikke spildt.
        var (_, alleredeLæst) = OptagetIndtilNu();
        var næste = -1;
        for (var i = 0; i < ScriptDocument.Available.Count; i++)
            if (!alleredeLæst.Contains(ScriptDocument.Available[i].Key)) { næste = i; break; }

        var alleTaget = næste < 0;
        if (alleTaget) næste = (ValgtIndex + 1) % ScriptDocument.Available.Count;

        KvitNaesteKnapIndex = næste;
        var n = ScriptDocument.Available[næste];
        KvitNaesteTekst.Content = alleTaget ? $"Læs «{n.Name}» igen" : $"Næste: {n.Name}";
        KvitNaesteHvad.Text = alleTaget
            ? "Alle tre tekster er indtalt. Næste skridt er at skrive dem ud og rette de ord, der blev hørt forkert. " +
              "Det er rettelserne, appen lærer af, og de flytter mere end en indtaling mere."
            : n.Why;

        VisKort();
    }

    private int KvitNaesteKnapIndex;

    private void KvitTransskriber_Click(object sender, RoutedEventArgs e)
    {
        if (_sidsteOptagelse is null) return;
        TranskriptionØnskes?.Invoke(_sidsteOptagelse);
    }

    /// <summary>
    /// Bedt om at komme videre til transskription af en BESTEMT optagelse.
    ///
    /// Stien følger med. Uden den landede man bare på listen over alle
    /// optagelser og skulle selv finde den, man lige havde lavet — og det er
    /// ikke «videre», det er «start forfra et andet sted».
    ///
    /// Skærmskiftet ligger i MainWindow, som er den eneste, der kender
    /// navigationen.
    /// </summary>
    public event Action<string>? TranskriptionØnskes;

    private void KvitNaesteTekst_Click(object sender, RoutedEventArgs e)
    {
        KvitteringPanel.Visibility = Visibility.Collapsed;
        VejledningPanel.Visibility = Visibility.Visible;
        BlokTitel.Text = "Klar til oplæsning";
        BlokUnder.Text = string.Empty;
        Vaelg(KvitNaesteKnapIndex);
    }

    private void KvitAabnMappe_Click(object sender, RoutedEventArgs e)
    {
        if (_sidsteOptagelse is null) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_sidsteOptagelse}\"") { UseShellExecute = true });
    }

    // ------------------------------------------------------------ auto-skift

    // ---------------------------------------------- automatisk afsnitsskift: væk
    //
    // Her lå VisFoelgStatus, LiveModelPath, FoelgMed_Klik, StartLytning,
    // StopLytning og Hoert — sammen med LiveListener (244 linjer) og
    // ScriptFollower (140).
    //
    // Den lyttede med, mens man læste op, og skiftede afsnit af sig selv, når
    // slutningen af det aktuelle var hørt. Den havde en vagthund, fordi den
    // kunne køre videre uden at høre noget og se ud som om den virkede. Den
    // havde en Failed-hændelse, fordi den kunne dø undervejs. Den krævede en
    // ekstra model og en ekstra exe.
    //
    // Alt det for at spare et tryk på mellemrum.
    //
    // Og den brød løftet om, at appen ikke lytter, før man trykker optag: den
    // åbnede mikrofonen ved siden af optagelsen og kørte sin egen genkendelse
    // hele vejen igennem. Afsnit skiftes nu med mellemrum, og det er hele
    // historien.

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
        }
        else
        {
            _session.Pause();
            _timer.Stop();

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

        // Tre udgange, og «bliv» er den, der har fokus: at lukke ved et uheld
        // og miste en oplaesning er vaerre end et ekstra klik.
        var valg = Dialogs.AppDialog.SpoergTre(Window.GetWindow(this),
            "Der er en oplæsning i gang",
            "Du har læst op i et stykke tid. Stopper du nu, bliver det, du har nået, gemt som en optagelse.",
            godkend: "Stop og gem",
            tredje: "Luk uden at gemme",
            annuller: "Bliv her",
            slags: Dialogs.Slags.Pas_paa);

        if (valg < 0) return false;          // bliv
        if (valg == 0) StopOptagelse();      // stop og gem
        return true;
    }
}
