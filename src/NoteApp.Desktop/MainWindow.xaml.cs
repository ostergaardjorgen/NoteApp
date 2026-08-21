using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NoteApp.Core;
using NoteApp.Desktop.Engine;
using NoteApp.Desktop.Documents;
using NoteApp.Desktop.Jobs;
using NoteApp.Desktop.Meeting;
using NoteApp.Desktop.Preferences;
using NoteApp.Desktop.Templates;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop;

public partial class MainWindow : Window
{
    private readonly MeetingView _moede = new();
    private readonly GlobalHotkey _genvej = new();

    /// <summary>Optagelsen, Optagelser-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnOptagelse;

    /// <summary>
    /// Skal skærmen spørge «skal den skrives ud nu?» ved næste skift?
    ///
    /// Kun sandt lige efter en optagelse. Kommer man fra et søgeresultat eller
    /// fra historikken, vil man se optagelsen — ikke have et tilbud om tyve
    /// minutters arbejde, man ikke bad om.
    /// </summary>
    private bool _spoergOmUdskrift;

    /// <summary>Dokumentet, Dokumenter-skærmen skal åbne på. Bruges én gang.</summary>
    private string? _aabnDokument;

    /// <summary>Id på det dokument, en kørsel netop har lavet.</summary>
    private string? _færdigtDokument;


    // ------------------------------------------------------------ kørselsbjælken

    /// <summary>
    /// Viser, hvad der kører. Bjælken bliver stående, når kørslen er færdig,
    /// indtil man selv skjuler den eller går hen og ser dokumentet.
    ///
    /// Det er hele pointen: en kvittering, der forsvinder af sig selv, når man
    /// står et andet sted i appen, er ikke en kvittering.
    /// </summary>
    private void VisJob(JobStatus s)
    {
        Dispatcher.Invoke(() =>
        {
            JobBjaelke.Visibility = Visibility.Visible;
            JobHvad.Text = s.Kører ? $"{s.Hvad} laves …" : s.Hvad + " færdigt";
            JobBesked.Text = s.Besked;

            JobDetaljer.Text = s.Detaljer;
            JobDetaljer.Visibility = s.Detaljer.Length == 0
                ? Visibility.Collapsed : Visibility.Visible;

            // Bjaelken vises kun, naar der ER noget at maale. Under
            // modelindlaesningen staar der ingen procent, og en bjaelke paa nul
            // i et halvt minut ligner en, der har haengt sig — dér er en
            // ubestemt bjaelke det aerlige svar.
            if (!s.Kører)
            {
                JobBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                JobBar.Visibility = Visibility.Visible;
                JobBar.IsIndeterminate = s.Procent < 0;
                if (s.Procent >= 0) JobBar.Value = s.Procent;
            }

            JobPrik.Fill = (System.Windows.Media.Brush)FindResource(s.Kører ? "Accent" : "Godkendt");
            JobAfbrydKnap.Visibility = s.Kører ? Visibility.Visible : Visibility.Collapsed;
            JobLukKnap.Visibility = s.Kører ? Visibility.Collapsed : Visibility.Visible;
            JobVisKnap.Visibility = !s.Kører && _færdigtDokument is not null
                ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void JobVis_Click(object sender, RoutedEventArgs e)
    {
        _aabnDokument = _færdigtDokument;
        JobBjaelke.Visibility = Visibility.Collapsed;

        if (NavDokumenter.IsChecked == true) Nav_Changed(this, new RoutedEventArgs());
        else NavDokumenter.IsChecked = true;
    }

    private void JobLuk_Click(object sender, RoutedEventArgs e) =>
        JobBjaelke.Visibility = Visibility.Collapsed;

    private void JobAfbryd_Click(object sender, RoutedEventArgs e)
    {
        var ja = Dialogs.AppDialog.Spoerg(this, "Afbryd kørslen?",
            "Der bliver ikke gemt noget dokument, og arbejdet skal gøres om.",
            godkend: "Afbryd", annuller: "Lad den køre færdig",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (ja) BackgroundJobs.Afbryd();
    }

    public MainWindow()
    {
        InitializeComponent();

        // Versionen paa appen kender vi — den staar i assemblyen. Det er kun
        // Whispers version, vi ikke kan aflaese; se WhisperInstall.
        //
        // TRE LED, IKKE TO. ToString(2) klippede tredje led vaek, og det er
        // netop dét led, udgivelserne taeller paa: 1.0.1, 1.0.2, 1.0.3 stod
        // alle sammen som "v1.0". Saa kunne man ikke se paa skaermen, hvilken
        // udgave man koerte - og det er hele grunden til, at nummeret staar
        // der. Fjerde led er altid 0 og siger ingenting.
        Version.Text = "v" + (Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "0.0.0");
        DataSti.Text = UserDataPaths.Root;
        OpdaterOpsaetningsmaerkat();

        // En ny installation kan optage og skrive ud, men ikke lave
        // dokumenter. Uden en besked opdager man det foerst den dag, man staar
        // med et moede og skal bruge referatet nu. Skrives een gang.
        NoteApp.Core.Notifikationer.MeldManglendeOpsaetning(
            NoteApp.Core.Llm.SkyNoegle.Hent() is null);

        // Efter en oplæsning peger kvitteringen videre til transskription.
        // Skærmskiftet skal ske her, fordi det er MainWindow, der ejer
        // navigationen — og fordi menupunktet skal markeres med, ellers
        // skifter indholdet uden at menuen følger med.
        //
        // Stien gemmes, så Optagelser-skærmen kan åbne PÅ den optagelse, der
        // lige er lavet, og spørge, om den skal skrives ud.
        OptagBjaelke.Content = _moede;

        // NAAR EN OPTAGELSE ER SLUT, SKAL DER SPOERGES MED DET SAMME.
        //
        // FaerdigMedMoede blev rejst, men INGEN lyttede. Efter et stop skete
        // der derfor ingenting: optagelsen laa i listen, og man skulle selv
        // finde den og trykke. Det er det oejeblik, man staar med moedet i
        // hovedet og gerne vil have teksten - og det oejeblik gik tabt.
        //
        // Der navigeres til optagelsen og spoerges dér. Svarer man nej, staar
        // man samme sted med optagelsen markeret, saa "senere" er lige saa
        // nemt som "nu": man trykker bare paa knappen, naar man vil.
        _moede.FærdigMedMøde += mappe =>
        {
            _aabnOptagelse = mappe;
            _spoergOmUdskrift = true;
            NavTransskriber.IsChecked = true;

            // Er man allerede paa skaermen, fyrer Checked ikke. Saa bygges
            // den her - ellers ville dialogen udeblive netop naar man
            // optager to moeder i traek.
            if (Indhold.Content is Transcribe.TranscribeView)
                Indhold.Content = NyOptagelsesskaerm();
        };

        // Startskærmen sættes HER, ikke af Nav_Changed. Menupunktet er markeret
        // fra XAML'en, men Checked fyrer under InitializeComponent, hvor
        // Indhold endnu er null — så handleren returnerer, og skærmen stod tom,
        // indtil man klikkede på et andet punkt og tilbage igen.
        //
        // DEN SKAL SVARE TIL DET MARKEREDE MENUPUNKT.
        //
        // De to steder kan komme ud af trit, og det gjorde de: 21-08-2026 blev
        // «Cockpit» markeret i XAML'en, mens denne linje stadig byggede
        // optagelsesskærmen. Menuen sagde Cockpit, skærmen viste Optagelser, og
        // man skulle klikke væk og tilbage for at få den rigtige frem.
        //
        // Kommentaren ovenfor advarede mod nøjagtig den fælde, og den blev
        // alligevel gået i — fordi man ændrer XAML'en og ikke leder efter den
        // linje her. Derfor står de nu sammen i én sætning: cockpit er markeret,
        // cockpit bygges.
        Indhold.Content = new Search.SearchView(null);

        // Genvejstasten kobles på, når vinduet findes. Virker den ikke, skal
        // det siges — en genvej, der stille er død, opdages først den dag, man
        // trykker på den under et møde.
        Loaded += (_, _) =>
        {
            _genvej.Trykket += LynstartOptagelse;
            TilslutGenvej();

            // Mødevagten. Den gør intet, før den er slået til under
            // Indstillinger — se AppSettings.MoedevagtTil for hvorfor.
            Moedevagten.Opdater();
        };
    }

    /// <summary>
    /// Holder øje med, om et andet program åbner mikrofonen — altså om der er
    /// startet et møde.
    ///
    /// Den ligger HER og ikke i MeetingView, fordi den skal virke, mens man er
    /// et hvilket som helst sted i appen. Den starter ikke selv en optagelse;
    /// den beder <see cref="LynstartOptagelse"/> om det, præcis som
    /// genvejstasten gør — så der kun er ÉN vej ind i en optagelse.
    /// </summary>
    public Moedevagt Moedevagten => _moedevagt ??= new Moedevagt(
        optagerAllerede: () => _moede.IsRecording,
        startOptagelse: LynstartOptagelse,
        ejer: () => this);

    private Moedevagt? _moedevagt;

    /// <summary>
    /// Siger, at dokumentet er klart, og tilbyder at åbne det.
    ///
    /// HVORFOR DET IKKE ER NOK MED BJÆLKEN NEDERST
    ///
    /// Et referat tager fra tyve sekunder til en halv time. Man laver noget
    /// andet imens — også noget uden for appen. En knap, der dukker op i en
    /// bjælke, man ikke kigger på, bliver fundet en time senere, hvis den
    /// bliver fundet.
    ///
    /// Vinduet kommer derfor til brugeren. Det er det ene sted i appen, hvor
    /// en afbrydelse er rigtig: man har selv sat noget i gang og ventet på det.
    /// </summary>
    private void TilbydAtAabne(string id)
    {
        // Fyres fra en baggrundstraad. Uden Dispatcher rejser WPF en
        // InvalidOperationException i stedet for at vise noget.
        Dispatcher.Invoke(() =>
        {
            var doc = NoteApp.Core.Documents.DocumentStore.LoadAll().FirstOrDefault(d => d.Id == id);
            if (doc is null) return;

            var sti = NoteApp.Core.Documents.DocumentStore.Path_(doc);
            if (!System.IO.File.Exists(sti)) return;

            var aabn = Dialogs.AppDialog.Spoerg(this,
                "Dokumentet er klar",
                $"«{doc.Title}» er lavet og gemt under Dokumenter.",
                godkend: "Åbn dokumentet",
                annuller: "Senere",
                slags: Dialogs.Slags.Valg);

            if (!aabn) return;

            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sti) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Dialogs.AppDialog.Vis(this, "Kunne ikke åbne dokumentet",
                    $"{ex.Message}\n\nFilen ligger her:\n{sti}", Dialogs.Slags.Valg);
            }
        });
    }

    /// <summary>Går til historikken — hele listen bag klokkens beskeder.</summary>
    public void GaaTilHistorik() => NavHistorik.IsChecked = true;

    /// <summary>
    /// Springer til «Dokumenter» og markerer det dokument, der er sat i gang.
    ///
    /// Kaldes fra optagelsesskærmen, naar en koersel er startet: arbejdet
    /// sker et andet sted, end man staar, og en besked om det uden en vej
    /// derhen er en halv besked.
    /// </summary>
    /// <param name="position">
    /// Tegnnummeret i dokumentets tekst, der skal springes til. Nul betyder
    /// «vis bare dokumentet». Kommer fra en søgning, hvor man klikkede på ét
    /// bestemt sted frem for på dokumentet som helhed.
    /// </param>
    public void GaaTilDokumenter(string? dokumentId = null, int position = 0)
    {
        _aabnDokument = dokumentId;
        _aabnPosition = position;

        if (NavDokumenter.IsChecked == true)
            Indhold.Content = new Documents.DocumentsView(_aabnDokument, Brug());
        else NavDokumenter.IsChecked = true;
    }

    /// <summary>Positionen, der skal springes til. Bruges ÉN gang.</summary>
    private int _aabnPosition;

    /// <summary>
    /// Det, der blev søgt på, da man klikkede sig videre herfra.
    ///
    /// Null betyder, at man ikke kom fra en søgning — og så er der ingen
    /// tilbage-linje at vise.
    /// </summary>
    private string? _soegeord;

    /// <summary>
    /// Går til «Søg» med et ord skrevet ind, så listen står, som den gjorde.
    ///
    /// Kaldes både af tilbage-linjen og af søgeskærmen selv, når den sender
    /// én videre — det er det samme ord, der skal bruges begge veje.
    /// </summary>
    public void GaaTilSoeg(string ord)
    {
        _soegeord = null;
        VisTilbagelinje();

        _startSoegning = ord;

        if (NavCockpit.IsChecked == true) Indhold.Content = new Search.SearchView(ord);
        else NavCockpit.IsChecked = true;
    }

    private string? _startSoegning;

    /// <summary>
    /// Husker søgeordet, så man kan komme tilbage til listen.
    ///
    /// Kaldes af søgeskærmen LIGE FØR den sender én videre. Rækkefølgen
    /// betyder noget: navigationen udløser Nav_Changed, som rydder linjen —
    /// derfor sættes den bagefter.
    /// </summary>
    public void HuskSoegning(string ord, int steder = 0)
    {
        _soegeord = ord;
        _soegesteder = steder;
        VisTilbagelinje();
    }

    private int _soegesteder;

    private void VisTilbagelinje()
    {
        if (TilbageLinje is null) return;

        TilbageLinje.Visibility = _soegeord is null ? Visibility.Collapsed : Visibility.Visible;

        if (_soegeord is null) return;

        // Antallet staar der, saa man kan se, hvor mange steder der er
        // tilbage at kontrollere - det er hele grunden til at gaa tilbage.
        TilbageKnap.Content = _soegesteder > 1
            ? $"Tilbage til de {_soegesteder} steder for «{_soegeord}»"
            : $"Tilbage til søgningen på «{_soegeord}»";
    }

    private void Tilbage_Click(object sender, RoutedEventArgs e)
    {
        if (_soegeord is { } ord) GaaTilSoeg(ord);
    }

    private int Brug()
    {
        var p = _aabnPosition;
        _aabnPosition = 0;
        return p;
    }

    /// <summary>
    /// Springer til «Optagelser» og markerer den optagelse, id'et peger på.
    ///
    /// Tager et ID og ikke en mappe. Mappen slås op HER, i det øjeblik der
    /// klikkes — en optagelse kan være flyttet eller omdøbt, siden linjen i
    /// historikken blev skrevet, og så peger en gemt sti på ingenting.
    ///
    /// Returnerer falsk, hvis optagelsen ikke findes mere. Så bliver man
    /// stående, hvor man er, frem for at skifte skærm og vise en tom liste.
    /// </summary>
    public bool GaaTilOptagelse(string id, int position = 0)
    {
        if (MeetingStore.FindById(id) is not { } fundet) return false;

        _aabnOptagelse = fundet.Mappe;
        _aabnPosition = position;

        if (NavTransskriber.IsChecked == true) Indhold.Content = NyOptagelsesskaerm();
        else NavTransskriber.IsChecked = true;

        return true;
    }

    /// <summary>
    /// Registrerer genvejen og fortæller mødeskærmen, hvad der blev til noget.
    /// Kaldes igen, når valget ændres under Indstillinger.
    /// </summary>
    public void TilslutGenvej()
    {
        var ok = _genvej.Tilslut(this, AppSettings.Current.HotkeyId);
        _moede.VisGenvej(ok ? _genvej.Aktiv!.Navn : null, _genvej.Bemærkning);

        // HER BLEV NOEDLOESNINGEN GEMT SOM ET VALG.
        //
        // Var den oenskede tast optaget, tog appen den naeste ledige og
        // skrev den i indstillingerne. Begrundelsen var, at den ellers ville
        // proeve den optagede igen ved hver opstart.
        //
        // Men det er praecis, hvad den SKAL. Konflikten er som regel
        // midlertidig - det var en anden NoteApp, der laa og holdt tasten -
        // og naar den er vaek, skal man have sin egen tast tilbage. Med det
        // gemte valg sad man fast paa Ctrl+Shift+1 for altid, uden nogensinde
        // at have valgt den.
        //
        // HotkeyId er nu OENSKET og intet andet: enten det, brugeren har
        // valgt under Indstillinger, eller null for standarden. Hvad der
        // faktisk blev registreret, staar oeverst til hoejre og i
        // bemaerkningen - dér hoerer en midlertidig tilstand hjemme.
    }

    /// <summary>
    /// Genvejstasten er trykket. Vinduet hentes frem, og optagelsen går i gang
    /// med det samme — man skal ikke først finde den rigtige skærm.
    /// </summary>
    private void LynstartOptagelse()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();

        _moede.Lynstart();
    }

    // HER LAA OpdaterDataLoefte, som satte linjen nederst i sidebjaelken:
    // "Optagelser og udskrifter bliver paa denne pc. Referater bearbejdes
    // i EU."
    //
    // Linjen var sand, men overfloedig. Det samme staar under AI-modeller,
    // hvor man vaelger det, og under Compliance, hvor det hoerer juridisk
    // hjemme. En paastand, der gentages tre steder, bliver ikke tre gange
    // saa troevaerdig - den bliver til noget, man laeser forbi.
    //
    // Maerkatet ved menupunktet opdateres nu direkte, hvor noeglen kan have
    // aendret sig. Fjernet 19-08-2026.

    /// <summary>
    /// Viser eller skjuler 1-tallet ved «AI-modeller».
    ///
    /// Kaldes ved opstart og hver gang noeglen kan have aendret sig. Den
    /// laeser tilstanden frem for at faa den fortalt: et maerkat, der bliver
    /// sat af den, der aendrer noget, staar tilbage den dag nogen glemmer at
    /// kalde det.
    /// </summary>
    public void OpdaterOpsaetningsmaerkat()
    {
        if (MotorMaerkat is null) return;

        MotorMaerkat.Visibility = NoteApp.Core.Llm.SkyNoegle.Hent() is null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        // Konstruktøren kører før felterne er sat op; RadioButton.Checked
        // fyrer under InitializeComponent.
        if (Indhold is null) return;

        // VAELGER MAN SELV ET MENUPUNKT, ER MAN IKKE LAENGERE PAA VEJ TILBAGE.
        // En linje, der bliver staaende, efter man er gaaet et helt andet
        // sted hen, peger tilbage til noget, man for laengst er faerdig med.
        _soegeord = null;
        VisTilbagelinje();

        // Skærmene bygges først, når de vises. Motorskærmen leder efter filer
        // på disken, og det skal ikke ske, mens man bare vil optage.
        if (NavTransskriber.IsChecked == true)
        {
            // Bygges hver gang: listen over optagelser skal vise den, der
            // netop er lavet, uden at nogen skal genstarte appen.
            Indhold.Content = NyOptagelsesskaerm();
        }
        else if (NavDokumenter.IsChecked == true)
        {
            var id = _aabnDokument;
            _aabnDokument = null;
            Indhold.Content = new DocumentsView(id, Brug());
        }
        else if (NavCockpit.IsChecked == true)
        {
            // Bygges hver gang: soegningen skal se det, der blev skrevet ud
            // for et oejeblik siden, uden at nogen genstarter appen.
            var ord = _startSoegning;
            _startSoegning = null;
            Indhold.Content = new Search.SearchView(ord);
        }
        else if (NavSkabeloner.IsChecked == true)
        {
            // Bygges hver gang: skabelonerne er filer, og de kan være rettet i
            // en editor siden sidst.
            Indhold.Content = new TemplatesView();
        }
        else if (NavMotor.IsChecked == true)
        {
            Indhold.Content = new EngineView();
        }
        else if (NavCompliance.IsChecked == true)
        {
            // Bygges hver gang: linjen om, hvor teksten sendes hen, skal
            // foelge, om noeglen er sat op NU.
            Indhold.Content = new Compliance.ComplianceView();
        }
        else if (NavHistorik.IsChecked == true)
        {
            // Bygges hver gang: historikken skal vise det, der lige er sket.
            Indhold.Content = new History.HistoryView();
        }
        else if (NavIndstillinger.IsChecked == true)
        {
            // Bygges hver gang: enhedslisten skal vise det, der er tilsluttet
            // NU, ikke da appen startede.
            Indhold.Content = new SettingsView();
        }
        else
        {
            // Alt andet lander paa optagelserne. De er det, appen er til.
            Indhold.Content = NyOptagelsesskaerm();
        }
    }

    /// <summary>
    /// Optagelsesskærmen, bygget forfra.
    ///
    /// Den bygges hver gang frem for at blive genbrugt: listen skal vise den
    /// optagelse, der netop er lavet, uden at nogen skal genstarte appen.
    /// </summary>
    private Transcribe.TranscribeView NyOptagelsesskaerm()
    {
        var aabn = _aabnOptagelse;
        var spoerg = _spoergOmUdskrift;

        _aabnOptagelse = null;              // gælder kun dette skift
        _spoergOmUdskrift = false;

        return new Transcribe.TranscribeView(aabn, spoerg, Brug());
    }

    /// <summary>
    /// En optagelse i gang må ikke tabes, fordi vinduet lukkes. Det er
    /// 20 minutters oplæsning, og der er ingen fortrydelse.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_moede.StopHvisIGang()) { e.Cancel = true; return; }

        _genvej.Dispose();
        base.OnClosing(e);
    }
}
