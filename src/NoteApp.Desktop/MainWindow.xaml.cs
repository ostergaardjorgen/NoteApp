using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
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
    private readonly Tastehook _genvej = new();

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

    // ------------------------------------------------ transskription i gang

    /// <summary>
    /// Viser i bjælken nederst, at en optagelse bliver skrevet ud.
    ///
    /// DEN SAMME BJÆLKE SOM DOKUMENTER, og af samme grund: der skal være ÉT
    /// sted, hvor man kan se, at noget kører. Fremdriften stod før kun på
    /// Optagelser-skærmen, og skiftede man til Cockpittet, var der intet spor
    /// af, at noget var i gang — på en kørsel, der tager fire et halvt minut
    /// for hvert kvarters lyd.
    ///
    /// DOKUMENTER HAR FORTRINSRET. Kører der en dokumentkørsel, bliver den
    /// stående; den kan afbrydes fra bjælken, og en transskription, der
    /// overtog den, ville fjerne den knap midt i noget.
    ///
    /// DER ER INGEN AFBRYD-KNAP HER. Transskriptionen afbrydes på den skærm,
    /// den kører på. To steder at stoppe det samme er to steder at lede efter
    /// en fejl.
    /// </summary>
    private void VisUdskrift()
    {
        Dispatcher.Invoke(() =>
        {
            if (BackgroundJobs.Kører) return;

            if (!Jobs.Udskriftsvagt.Koerer)
            {
                JobBjaelke.Visibility = Visibility.Collapsed;
                return;
            }

            JobBjaelke.Visibility = Visibility.Visible;

            JobHvad.Text = Jobs.Udskriftsvagt.Navn;
            JobBesked.Text = Jobs.Udskriftsvagt.Besked;
            JobDetaljer.Visibility = Visibility.Collapsed;

            JobBar.Visibility = Visibility.Visible;
            JobBar.IsIndeterminate = Jobs.Udskriftsvagt.Procent < 0;
            if (Jobs.Udskriftsvagt.Procent >= 0) JobBar.Value = Jobs.Udskriftsvagt.Procent;

            JobPrik.Fill = (System.Windows.Media.Brush)FindResource("Accent");

            JobAfbrydKnap.Visibility = Visibility.Collapsed;
            JobVisKnap.Visibility = Visibility.Collapsed;
            JobLukKnap.Visibility = Visibility.Collapsed;
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
        // TREDJE LED SKRIVES MED TO CIFRE: v1.1.07, ikke v1.1.7.
        //
        // 99 er hoejeste tal i hvert led. Tredje led naaede 108, foer det
        // blev opdaget - og 1.0.108 sorterer FOER 1.0.99 i alt, der
        // sammenligner tekst, saa udgivelserne kom i forkert raekkefoelge
        // overalt, hvor de blev stillet op.
        //
        // .NET kender ikke foranstillede nuller i en Version: skriver man
        // 1.1.07 i csproj, bliver det til 1.1.7 uden at sige det. Derfor
        // gemmes det rigtige tal, og nullet saettes paa her. Forskellen
        // mellem, hvad der gemmes, og hvad der laeses, hoerer hjemme i
        // visningen.
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        Version.Text = v is null ? "v0.0.00" : $"v{v.Major}.{v.Minor}.{v.Build:00}";
        DataSti.Text = UserDataPaths.Root;
        OpdaterOpsaetningsmaerkat();

        // En ny installation kan optage og skrive ud, men ikke lave
        // dokumenter. Uden en besked opdager man det foerst den dag, man staar
        // med et moede og skal bruge referatet nu. Skrives een gang.
        NoteApp.Core.Notifikationer.MeldManglendeOpsaetning(
            NoteApp.Core.Llm.SkyNoegle.Hent() is null);

        // Vinduets maal, foer det tegnes. Saettes de senere, ser man vinduet
        // springe fra standardstoerrelsen til den gemte.
        HentVinduesstoerrelse();

        // Efter en oplæsning peger kvitteringen videre til transskription.
        // Skærmskiftet skal ske her, fordi det er MainWindow, der ejer
        // navigationen — og fordi menupunktet skal markeres med, ellers
        // skifter indholdet uden at menuen følger med.
        //
        // Stien gemmes, så Optagelser-skærmen kan åbne PÅ den optagelse, der
        // lige er lavet, og spørge, om den skal skrives ud.
        OptagBjaelke.Content = _moede;

        // ============ OPTAGEKNAPPEN SPOERGER KALENDEREN FOERST ============
        //
        // Trykker man optag, mens en aftale koerer, skal den aftales mappe,
        // moedetype og sprog bruges - ikke spoerges om igen. Se
        // Kalender.IGangNu for, hvad der taeller som "nu", og hvorfor et
        // moede, der allerede er optaget, ikke taeller.
        //
        // OptagAftale spoerger kun om det, der MANGLER. Staar sproget paa
        // aftalen, kommer der ingen dialog overhovedet.
        // Genvejstasten optager FOERST og spoerger bagefter. Staar svarene paa
        // en aftale, skal der ikke spoerges - se MeetingView.Lynstart.
        _moede.AftalensSvar = () =>
        {
            try
            {
                if (Kalender.IGangNu(DateTimeOffset.Now) is not { } a) return null;

                _venterAftale = a;

                return new Meeting.MeetingView.Opstart(
                    a.Sprog,
                    a.Link.Length > 0 ? a.Link : null,
                    a.Mappe.Length > 0 ? a.Mappe : null,
                    a.Moedetype.Length > 0 ? a.Moedetype : null,
                    a.ErWebinar);
            }
            catch (Exception)
            {
                return null;
            }
        };

        _moede.OptagAftalenNu = () =>
        {
            try
            {
                if (Kalender.IGangNu(DateTimeOffset.Now) is not { } a) return false;

                OptagAftale(a, spoerg: true);
                return true;
            }
            catch (Exception)
            {
                // Kan kalenderen ikke laeses, skal knappen stadig virke.
                // Optagelse maa aldrig kunne blokeres - saa aabner dialogen
                // som foer.
                return false;
            }
        };

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
            // AFTALEN FÅR OPTAGELSENS ID. Uden den forbindelse er kalenderen
            // og arkivet to lister, man selv skal sammenholde.
            if (_venterAftale is { } aftale)
            {
                try
                {
                    if (MeetingStore.Load(mappe) is { } meta)
                    {
                        aftale.MoedeId = meta.Id.ToString();
                        Kalender.Gem(aftale);
                    }
                }
                catch (Exception)
                {
                    // Kan forbindelsen ikke gemmes, staar optagelsen der
                    // stadig. Den er det vaerdifulde; linket er en bekvemmelighed.
                }

                _venterAftale = null;
            }

            _aabnOptagelse = mappe;

            // DER SPOERGES IKKE LAENGERE - der TRANSSKRIBERES.
            //
            // Flaget hed foer _spoergOmUdskrift og udloeste en dialog. Nu
            // saetter det transskriptionen i gang, hvis sproget er kendt; er
            // det ikke, spoerges der EEN gang og svaret huskes. Se
            // TranscribeView.StartAutomatisk og Core.Udskriftsvalg.
            _spoergOmUdskrift = AppSettings.Current.SkrivUdAutomatisk;

            // ============ SKAERMEN SKAL BYGGES OM, OGSAA NAAR MAN STAAR PAA DEN ============
            //
            // Her stod kun «NavTransskriber.IsChecked = true». Stod man
            // ALLEREDE paa Optagelser - og det goer man tit, for det er den
            // skaerm, man arbejder paa - saetter man en RadioButton, der
            // allerede er sat. Saa rejses Checked ikke, skaermen bygges ikke
            // om, optagelsen markeres ikke, og transskriptionen gaar aldrig i
            // gang.
            //
            // Man stod tilbage paa den tomme hjaelpeskaerm med «Opret
            // transskription» graa og ingen maade at se, hvad der skete.
            // Set 25-08-2026 paa et moede paa 32 minutter, hvor historikken
            // kun fik linjen «Moede optaget» og intet andet.
            //
            // Moenstret findes i forvejen i GaaTilOptagelse laengere nede;
            // det var her, det manglede.
            // ============ SKAERMEN BYGGES EEN GANG - IKKE TO ============
            //
            // HER STOD DER TO BLOKKE, OG DEN ANDEN OEDELAGDE DEN FOERSTE.
            //
            // Den anden lyder: «er man allerede paa skaermen, fyrer Checked
            // ikke - saa bygges den her». Det var rigtigt, indtil blokken
            // ovenfor blev tilfoejet 25-08-2026; nu goer de det samme.
            //
            // Og de goer det ikke uskadeligt. NyOptagelsesskaerm TOEMMER
            // _aabnOptagelse og _spoergOmUdskrift, naar den laeser dem - de
            // gaelder kun det ene skift. Anden gang er de altsaa tomme, og
            // skaermen bliver bygget UDEN besked om, hvilken optagelse der
            // skal markeres, og uden at transskriptionen skal starte. Den
            // rigtige skaerm blev smidt vaek og erstattet af en tom.
            //
            // Foelgen: efter et stop skete der ingenting. Set paa et webinar
            // paa 42 minutter natten til 26-08-2026 - det stoppede selv kl.
            // 21:48, historikken fik linjen «Moede optaget», og saa stod
            // maskinen stille i fire en halv time. Ingen transskription,
            // ingen fejl, ingen besked.
            //
            // EEN BLOK KLARER BEGGE TILFAELDE:
            //   staar man paa skaermen  -> byg den om her
            //   staar man et andet sted -> Checked fyrer, og Nav_Changed
            //                              bygger den med de samme flag
            if (NavTransskriber.IsChecked == true) Indhold.Content = NyOptagelsesskaerm();
            else NavTransskriber.IsChecked = true;
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
            SaetStandardvalg();

            // ============ EN GANG, OGSAA NAAR LOADED FYRER IGEN ============
            //
            // Loaded fyrer i WPF, hver gang vinduet kommer tilbage i traeet -
            // ikke kun én gang. Uden «-=» foerst laa abonnementet oven i sig
            // selv, og ét tastetryk kaldte LynstartOptagelse flere gange.
            //
            // Det gik som regel godt, fordi Lynstart svarer med det samme, hvis
            // der allerede optages. Men de to kald ligger mikrosekunder fra
            // hinanden, og «som regel» er ikke godt nok til den knap, der
            // starter et moede. Set som moenster 28-08-2026, samme fejl som i
            // MeetingViews temaabonnement.
            _genvej.Trykket -= LynstartOptagelse;
            _genvej.Trykket += LynstartOptagelse;

            // ============ HOLD PAA SAMME TAST = DIKTERING ============
            //
            // Samme moenster som ovenfor: «-=» foerst, fordi Loaded fyrer
            // igen, hver gang vinduet kommer tilbage i traeet. To abonnementer
            // ville starte to optagere paa det samme hold.
            _genvej.HoldBegyndt -= DiktatBegynd;
            _genvej.HoldBegyndt += DiktatBegynd;
            _genvej.HoldSluttet -= DiktatSlut;
            _genvej.HoldSluttet += DiktatSlut;
            _genvej.HoldAfbrudt -= DiktatAfbrudt;
            _genvej.HoldAfbrudt += DiktatAfbrudt;

            // Genvejen skal vide det med det samme, naar fanen aendrer det -
            // ellers skulle appen genstartes, foer et hold betoed noget.
            Preferences.SettingsView.Dikteringsskift = _ =>
                Dispatcher.BeginInvoke(SaetDiktering);

            // Klappen skal saettes EFTER skabelonen er bygget: pilen findes
            // ikke i traeet foer.
            Klap.ApplyTemplate();

            // VED OPSTART SAETTES STANDEN UDEN AT GLIDE. En menu, der folder
            // sig ud, hver gang appen aabnes, er en bevaegelse ingen har bedt
            // om - og den staar i vejen for det, man kom efter.
            var inde = Core.AppSettings.Current.MenuSammenklappet;

            MenuSammenklappet = inde;
            SaetMenubredde(inde ? 0.0 : 1.0);

            Logotekst.Visibility = inde ? Visibility.Collapsed : Visibility.Visible;
            Datafod.Visibility = inde ? Visibility.Collapsed : Visibility.Visible;
            Logolinje.Margin = new Thickness(inde ? 0 : 4, 0, 0, 2);
            Logolinje.HorizontalAlignment = inde
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Left;

            VisKlap(inde);

            foreach (var knap in Menupunkter())
                knap.ToolTip = inde ? knap.Content as string : null;

            SaetDiktering();

            // ============ VAAGEORDET ============
            _vaage.Hoert -= VaageordHoert;
            _vaage.Hoert += VaageordHoert;
            _vaage.Melder -= VisDiktat;
            _vaage.Melder += VisDiktat;

            // Fanen skal kunne taende og slukke med det samme. Ellers skal
            // appen genstartes, foer vaageordet begynder at virke.
            Diktering.KommandoerView.Aendret = () => Dispatcher.BeginInvoke(SaetVaageord);

            // Fanen viser det MAALTE forbrug. «Det bruger naesten ingenting»
            // er en paastand, ingen kan efterproeve - og den slags skal en app
            // ikke komme med om sig selv, naar den har mikrofonen aaben.
            Diktering.KommandoerView.Maaler = () => _vaage.Forbrug;

            _vaageur?.Stop();
            _vaageur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _vaageur.Tick += (_, _) => SaetVaageord();
            _vaageur.Start();

            SaetVaageord();

            // HER LAA ET ABONNEMENT PAA «AENDRET». Registreringen kunne skifte
            // af sig selv, fordi den oenskede tast blev ledig igen - og saa
            // skulle bjaelken sige noget nyt.
            //
            // Det kan ikke ske mere. Grebet skifter kun, naar brugeren trykker
            // en ny tast, og saa er det den skaerm, der siger til.
            TilslutGenvej();

            Moedevagten.Opdater();

            // Vagten skal starte med appen. Foerst naar den bliver LAEST, findes
            // den - og en vagt, ingen har spurgt efter, kigger aldrig paa uret.
            _ = Kalendervagten;

            // Bjaelken skal foelge transskriptionen paa ALLE skaerme.
            Jobs.Udskriftsvagt.Aendret += VisUdskrift;

            // Mapperne, brugeren har peget paa. Starter tomt og koster
            // ingenting, naar der ikke er nogen - se Mappevagt.Kig.
            Jobs.Mappevagt.Start();

            // Aftaler og opgaver hentes hvert kvarter - og med det samme her,
            // saa dagen er rigtig, naar man saetter sig. Koster ingenting,
            // naar ingen integration er forbundet.
            Jobs.Synkvagt.Start();

            // STILHEDSMODELLEN HENTES, HVIS DEN MANGLER.
            //
            // 885 KB, og den er ikke et valg - den er en del af motoren. Uden
            // den finder whisper underteksterkreditter i de tredive sekunder,
            // hvor ingen siger noget, og de staar i transskriptionen som alt
            // andet. Se WhisperInstall.VadModel for maalingen.
            //
            // Uden ventetid og uden besked. Gaar det galt, koeres der som foer.
            _ = NoteApp.Core.WhisperInstall.HentVadAsync();
        };
    }

    /// <summary>
    /// Slår de to ting til, der skal være slået til fra begyndelsen — én gang.
    ///
    /// APPEN STARTER MED WINDOWS, og den SPØRGER, når et program bruger
    /// mikrofonen. Begge skal kunne slås fra; ingen af dem skal skulle findes
    /// og slås til.
    ///
    /// Grunden er den samme for begge: de findes for at redde de møder, man
    /// glemmer at optage. Genvejstasten kan kun starte en optagelse, hvis
    /// appen kører, og mødevagten kan kun spørge, hvis den er slået til. En
    /// funktion, man selv skal finde, redder ingen af de møder.
    ///
    /// DET SKER ÉN GANG OG ALDRIG IGEN. Flaget står i indstillingerne, så et
    /// fravalg bliver stående. Et hak, der kommer tilbage af sig selv ved
    /// næste opstart, er værre end intet hak — så holder man op med at tro på
    /// indstillingerne overhovedet.
    ///
    /// Autostarten skrives i Windows' Run-nøgle. Fejler det — en låst maskine,
    /// en politik fra en it-afdeling — bliver flaget alligevel sat. Ellers
    /// ville appen prøve igen ved hver eneste start og fejle hver gang.
    /// </summary>
    private static void SaetStandardvalg()
    {
        var s = AppSettings.Current;
        if (s.StandardvalgSat) return;

        s.MoedevagtTil = true;
        s.StandardvalgSat = true;

        try { Autostart.Saet(true); }
        catch (Exception)
        {
            // En maskine, hvor det ikke kan lade sig goere. Appen virker
            // uaendret; den starter bare ikke af sig selv.
        }

        s.Save();
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

    private Kalendervagt? _kalendervagt;

    /// <summary>
    /// Vagten, der starter optagelsen på de aftaler, der er markeret til det.
    ///
    /// DER SPØRGES IKKE, når den går i gang. Mødet er ved at begynde, og der
    /// sidder måske ingen ved maskinen — en dialog ville stå og vente, mens
    /// mødet blev holdt.
    ///
    /// Vinduet hentes frem, så det er til at se, at der optages. Ellers ville
    /// den eneste besked være optagebåndet, og det kan ligge bag et andet
    /// program.
    /// </summary>
    public Kalendervagt Kalendervagten => _kalendervagt ??= new Kalendervagt(
        optagerAllerede: () => _moede.IsRecording,
        klargoer: a =>
        {
            // VINDUET HENTES FREM. Hele pointen er, at man kan SE, at boksen
            // staar klar; ligger den bag et andet program, staar den klar for
            // ingen.
            App.HentFrem(this);
            Startklaren.Stil(a);
        });

    private Startklar? _startklar;

    /// <summary>
    /// Boksen, der staar klar foer et booket moede og selv trykker paa knappen.
    ///
    /// Den ligger HER og ikke i MeetingView, fordi den skal virke, uanset
    /// hvilken skaerm man staar paa - praecis som Kalendervagten.
    /// </summary>
    private Startklar Startklaren
    {
        get
        {
            if (_startklar is not null) return _startklar;

            _startklar = new Startklar(() => _moede.IsRecording);

            _startklar.Aendret += () =>
            {
                if (_startklar!.Aftalen is not { } a) { _moede.StaaKlar(null); return; }

                var om = a.Start - DateTimeOffset.Now;
                var naar = om > TimeSpan.Zero
                    ? string.Format(NoteApp.Core.Sprog.T("klar.om"), (int)Math.Ceiling(om.TotalMinutes))
                    : NoteApp.Core.Sprog.T("klar.nu");

                // ============ BOKSEN SKAL KUNNE SES AT LYTTE ============
                //
                // Den sagde «KLAR» og roerte sig ikke. En boks, der staar
                // stille, kan ikke skelnes fra en, der er gaaet i staa - og
                // saa tror man ikke paa den. Samme fejl som medskrivningens
                // usynlighed, bare et andet sted.
                //
                // Der staar HVAD den hoerer, ikke et tal. «Hoerer moedet»
                // siger, at lyden naar frem; «58 %» siger ingenting. Og det
                // aendrer sig af sig selv fire gange i sekundet, saa man kan
                // se, at den lever.
                var hoerer = NoteApp.Core.Sprog.T(
                    (_startklar.UdslagMikrofon > 0.15, _startklar.UdslagOnline > 0.15) switch
                    {
                        (true, true)  => "klar.hoererbegge",
                        (true, false) => "klar.hoererdig",
                        (false, true) => "klar.hoerermoedet",
                        _             => "klar.stille"
                    });

                _moede.StaaKlar(string.Format(
                    NoteApp.Core.Sprog.T(_startklar.VenterPaaSvar ? "klar.venterpaasvar" : "klar.staarklar"),
                    a.Titel, naar) + $"   ·   {hoerer}");
            };

            _startklar.Gaaigang += a =>
            {
                _moede.StaaKlar(null);
                App.HentFrem(this);
                OptagAftale(a, spoerg: false);
            };

            return _startklar;
        }
    }

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
    /// Sætter genvejen i gang og fortæller mødeskærmen, hvad der gælder.
    /// Kaldes igen, når brugeren har trykket en ny tast.
    /// </summary>
    /// <remarks>
    /// HER STOD EN REGISTRERING, DER KUNNE MISLYKKES.
    ///
    /// Appen bad Windows om en genvej. Var den taget, tog appen den næste på
    /// en liste og skrev en bemærkning om det — og hvilken tast der gjaldt,
    /// afhang derfor af, hvad der tilfældigvis var ledigt i det sekund.
    ///
    /// Nu ser appen tastaturet selv. Der er ingen at komme i vejen for, intet
    /// at falde tilbage på og ingen liste. Grebet er brugerens og skifter kun,
    /// når han trykker en ny tast. Se <see cref="Tastehook"/>.
    /// </remarks>
    public void TilslutGenvej()
    {
        _genvej.Greb = Core.Genvejsgreb.Laes(AppSettings.Current.Genvejsgreb)
                       ?? Core.Genvejsgreb.Standard;

        var kører = _genvej.Kører || _genvej.Start();

        _moede.VisGenvej(
            kører ? _genvej.Greb.Navn() : null,
            kører ? null : "tastaturvagten kunne ikke sættes i gang");

        Spor.Skriv($"TilslutGenvej: greb={_genvej.Greb.Gem()} ({_genvej.Greb.Navn()}) "
                   + $"koerer={kører}");
    }

    /// <summary>
    /// Starter optagelsen af en aftale fra kalenderen.
    ///
    /// AFTALENS EGNE VALG FØLGER MED — mappe, mødetype og sprog er valgt, da
    /// aftalen blev lagt ind. De skal ikke vælges igen med mødet i gang; det
    /// er hele grunden til, at de kan sættes på en aftale.
    ///
    /// ER DE IKKE SAT, SPØRGES DER SOM SÆDVANLIG. Så er man præcis lige så
    /// langt som uden kalenderen, og aftalen har i det mindste sparet én ting:
    /// man skal ikke lede efter optageknappen.
    ///
    /// Aftalen får optagelsens id med, når mødet er slut. Det er DEN
    /// forbindelse, der lukker ringen: kalenderen siger, at mødet var der, og
    /// optagelsen siger, hvad der blev sagt.
    /// </summary>
    /// <param name="spoerg">
    /// Må der spørges, hvis aftalen mangler mappe eller sprog?
    ///
    /// FALSK, NÅR VAGTEN STARTER. Mødet er ved at begynde, og der sidder
    /// måske ingen ved maskinen. En dialog ville stå og vente, mens de første
    /// ti minutter blev sagt — og optagelse må aldrig kunne blokeres. Så
    /// hellere en optagelse i den forkerte mappe end ingen optagelse; mappen
    /// kan flyttes bagefter, replikkerne kan ikke.
    /// </param>
    public void OptagAftale(Aftale a, bool spoerg = true)
    {
        if (_moede.IsRecording)
        {
            // Vagten spoerger selv, om der optages, foer den kalder her. Sker
            // det alligevel, er det ikke noget, der skal afbryde nogen midt i
            // et moede.
            if (spoerg)
                Dialogs.AppDialog.Vis(this, "Der optages allerede",
                    "Stop den igangværende optagelse først.", Dialogs.Slags.Valg);

            return;
        }

        // Mangler noget af det, aftalen skulle have svaret paa, spoerges der.
        // Sproget alene er ikke nok til at springe dialogen over: uden en
        // mappe lander optagelsen samme sted som alt andet, og det er dét,
        // hele opstartsdialogen er sat i verden for at undgaa.
        // ============ DER SPOERGES KUN OM DET, DER MANGLER ============
        //
        // Her stod «a.Sprog.Length == 0 || a.Mappe.Length == 0». En TOM MAPPE
        // er ikke et manglende svar - det er et svar: optagelsen hoerer ikke
        // til i en folder. De fleste moeder goer ikke.
        //
        // Foelgen var, at dialogen kom hver gang paa en aftale, hvor sprog og
        // moedetype var sat, men folderen med vilje var tom - og saa skulle
        // man svare paa det samme igen, mens moedet gik i gang.
        //
        // SPROGET ER DET ENESTE, DER IKKE KAN GAETTES. Rammer det forkert,
        // bliver hele transskriptionen vroevl, og det opdages foerst i
        // referatet. Mappe og moedetype kan saettes bagefter paa et oejeblik.
        if (spoerg && a.Sprog.Length == 0)
        {
            var slags = a.ErWebinar
                ? OpstartWindow.Slags.Webinar
                : OpstartWindow.Slags.Moede;

            // AFTALENS EGNE VAERDIER GIVES MED. Er de sat, skjules felterne,
            // og der staar eet spoergsmaal tilbage - sproget. Uden det her
            // skulle man vaelge mappe og moedetype for ANDEN gang, mens
            // moedet gik i gang.
            var vindue = new OpstartWindow(slags, a.Mappe, a.Moedetype) { Owner = this };
            if (vindue.ShowDialog() != true) return;

            a.Mappe = vindue.Mappe ?? a.Mappe;
            a.Moedetype = vindue.Moedetype ?? a.Moedetype;
            a.Sprog = vindue.Sprog;
        }

        _venterAftale = a;

        // MAERKET HER, hvor optagelsen faktisk begynder.
        //
        // Foer stod det i Kalendervagten, som dengang startede optagelsen selv.
        // Nu STILLER vagten kun boksen klar, og en boks, der staar klar, er
        // ikke en optagelse. Blev aftalen maerket dengang, ville et aflyst
        // moede staa som optaget - og var det blevet flyttet en time frem,
        // ville boksen aldrig stille sig klar til det igen.
        //
        // Uden maerkning ville vagten til gengaeld stille klar igen, saa snart
        // optagelsen stoppede. Derfor skal den staa - bare her.
        if (a.Startet is null)
        {
            a.Startet = DateTimeOffset.Now;
            Kalender.Gem(a);
        }

        _moede.Start(new MeetingView.Opstart(
            a.Sprog, a.Link.Length > 0 ? a.Link : null,
            a.Mappe.Length > 0 ? a.Mappe : null,
            a.Moedetype.Length > 0 ? a.Moedetype : null,
            a.ErWebinar));
    }

    /// <summary>Aftalen, den igangværende optagelse hører til. Null uden for en aftale.</summary>
    private Aftale? _venterAftale;

    /// <summary>
    /// Genvejstasten er trykket. Vinduet hentes frem, og optagelsen går i gang
    /// med det samme — man skal ikke først finde den rigtige skærm.
    /// </summary>

    private readonly Dikteringsvagt _diktat = new();
    private readonly Vaageordsvagt _vaage = new();
    private DispatcherTimer? _vaageur;

    // ============================ VAAGEORDET ============================

    /// <summary>
    /// Slår vågeordet til eller fra, alt efter om der må lyttes lige nu.
    /// </summary>
    /// <remarks>
    /// DET ER HER, LØSNINGEN BLIVER BILLIG. Vinduet omkring en aftale er
    /// forskellen på en mikrofon, der er åben en time om dagen, og en, der er
    /// åben fireogtyve. Se <see cref="Core.Vaageord.Skal"/>, hvor beslutningen
    /// ligger — den kan prøves af uden mikrofon.
    ///
    /// Der spørges hvert halve minut. Et vindue, der åbner et halvt minut for
    /// sent, er ikke til at mærke; et ur, der tikker hvert sekund, er.
    /// </remarks>
    private void SaetVaageord()
    {
        var v = Core.AppSettings.Current;

        // ============ VAAGEORDET HAR SIN EGEN MODEL ============
        //
        // HER STOD Locate().ModelPath, OG DET VAR MOEDEMODELLEN. Den er
        // valgt til at vaere den bedste, der findes - paa den her maskine
        // knap 3 GB - og saa laa den og lyttede efter to ord hele tiden.
        // Se WhisperInstall.Vaageordsmodel: her vinder den mindste.
        //
        // Er der ingen model, er der intet at lytte med, og det skal siges
        // paa samme maade som en manglende motor. Ellers staar der «lytter»
        // paa skaermen, mens der ikke sker noget.
        var model = Core.WhisperInstall.Vaageordsmodel();

        var svar = Core.Vaageord.Skal(
            v.VaageordTil,
            Vaageordsvagt.MotorFindes && model is not null,
            _diktat.Igang || OptagerNu(),
            Laast());

        if (svar == Core.Lyttesvar.Lytter)
        {
            _vaage.Start(model!, Core.Sprog.Kode);
        }
        else if (_vaage.Lytter)
        {
            _vaage.Stop();
        }
    }

    /// <summary>
    /// Kører der en optagelse?
    /// </summary>
    /// <remarks>
    /// Dikteringsvagten ved det om SIN egen; mødeoptagelsen kender den ikke.
    /// Den spørges derfor det sted, der viser den: optagebåndet. Er der ikke
    /// noget bånd, kører der ingen optagelse.
    /// </remarks>
    private bool OptagerNu() =>
        OptagBjaelke.Content is Meeting.MeetingView m && m.OptagerNu;

    /// <summary>Er maskinen låst?</summary>
    private static bool Laast()
    {
        // Er der ingen forgrund, er skrivebordet ikke vores - saa er skaermen
        // laast, eller en anden bruger sidder der.
        return Indsaetter.Laes().Haandtag == IntPtr.Zero;
    }

    private void VaageordHoert(string efter) => Dispatcher.BeginInvoke(() =>
    {
        _diktat.Melder -= VisDiktat;
        _diktat.Melder += VisDiktat;

        // ============ VAR DET EN KOMMANDO? ============
        //
        // Blev der sagt noget efter vaageordet, kan det vaere en kommando.
        // Kan det ikke findes i listen, var det TALE - og saa er det en
        // diktering. Det er den rigtige vej at fejle: man faar sin tekst.
        var kommandoer = Core.AppSettings.Current.Kommandoer is { Count: > 0 } egne
            ? egne
            : Core.Kommandotolk.Standard;

        if (efter.Length > 0 && Core.Kommandotolk.Find(efter, kommandoer) is { } k)
        {
            Udfoer(k);
            return;
        }

        // ============ SAMME VEJ SOM ET HOLD PAA TASTEN ============
        //
        // Der emuleres ikke et tastetryk. Der kaldes den samme kode.
        _diktat.BegyndPaaVaageord();
    });

    /// <summary>
    /// Gør det, kommandoen siger.
    /// </summary>
    /// <remarks>
    /// KUN DET, DER STÅR PÅ LISTEN. Der køres ikke en tekst, brugeren ikke selv
    /// har skrevet ind, og der findes ingen kommandotype, der kan køre noget
    /// vilkårligt — se <see cref="Core.Kommandotype"/>.
    /// </remarks>
    private void Udfoer(Core.Kommando k)
    {
        try
        {
            switch (k.Type)
            {
                case Core.Kommandotype.Optag:
                    LynstartOptagelse();
                    break;

                case Core.Kommandotype.Webinar:
                    if (OptagBjaelke.Content is Meeting.MeetingView w) w.StartWebinar();
                    break;

                case Core.Kommandotype.AabnSkaerm:
                    GaaTilSkaerm(k.Maal);
                    break;

                case Core.Kommandotype.AabnProgram:
                    // UseShellExecute: saa virker baade et program, en mappe og
                    // en adresse - det er de tre ting, man peger paa.
                    if (k.Maal.Length > 0)
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(k.Maal) { UseShellExecute = true });
                    }
                    break;
            }

            VisDiktat(Core.Sprog.T("kommandoer.udfoert", k.Udtryk));
        }
        catch (Exception ex)
        {
            // En kommando, der ikke kan koere, skal SIGE det. Ellers taler man
            // til en app, der ikke svarer, og tror det er vaageordet.
            VisDiktat(Core.Sprog.T("kommandoer.gik_galt", k.Udtryk, ex.Message));
        }
    }

    /// <summary>Går til en skærm ud fra dens navn i kommandolisten.</summary>
    private void GaaTilSkaerm(string navn)
    {
        var knap = navn.Trim().ToLowerInvariant() switch
        {
            "cockpit" => NavCockpit,
            "diktering" or "ordbog" => NavDiktering,
            "optagelser" => NavTransskriber,
            "dokumenter" => NavDokumenter,
            "moedetyper" or "mødetyper" or "skabeloner" => NavSkabeloner,
            "modeller" or "ai-modeller" => NavMotor,
            "compliance" => NavCompliance,
            "historik" => NavHistorik,
            "indstillinger" => NavIndstillinger,
            _ => null,
        };

        if (knap is not null) knap.IsChecked = true;
    }

    /// <summary>
    /// Afgør, om et hold på tasten skal betyde noget.
    /// </summary>
    /// <remarks>
    /// TO TING SKAL VÆRE SANDE, og den anden er let at glemme: uden en nøgle
    /// til leverandøren kan der ikke dikteres. Så ville ventetiden på 350 ms
    /// være ren udgift — man ville betale for en funktion, der ikke kan
    /// svare.
    ///
    /// Der spørges her og ikke i beskedbehandlingen: at læse en fil inde i en
    /// hook er præcis det, der ikke må ske dér.
    /// </remarks>
    private void SaetDiktering()
    {
        var til = AppSettings.Current.DikteringTil;
        var noegle = Core.Llm.SkyNoegle.Hent() is not null;

        _genvej.HoldGiverDiktering = til && noegle;

        Spor.Skriv($"SaetDiktering: DikteringTil={til} noegle={noegle} "
                          + $"-> hold={_genvej.HoldGiverDiktering}");
    }

    /// <summary>
    /// Tasten er holdt nede. Dikteringen begynder at lytte.
    /// </summary>
    /// <remarks>
    /// Beskederne gaar til baandet, saa man kan SE, at der lyttes. En
    /// mikrofon, der er aaben uden at det staar nogen steder, er praecis det,
    /// resten af appen lover ikke at goere.
    /// </remarks>
    private void DiktatBegynd()
    {
        _diktat.Melder -= VisDiktat;
        _diktat.Melder += VisDiktat;
        _diktat.Faerdig -= DiktatFaerdig;
        _diktat.Faerdig += DiktatFaerdig;
        _diktat.Begynd();
    }

    private void DiktatSlut() => _ = _diktat.SlutAsync(afbrudt: false);

    private void DiktatAfbrudt() => _ = _diktat.SlutAsync(afbrudt: true);

    /// <summary>Den seneste diktering — så den kan gemmes, hvis man vil.</summary>
    private string? _sidsteDiktat;

    /// <summary>
    /// En diktering er landet. Nu tilbydes den som note.
    /// </summary>
    /// <remarks>
    /// KNAPPEN ER SPØRGSMÅLET. En dialog ville rive fokus væk fra det felt,
    /// teksten lige er landet i — i samme sekund man var færdig med at tale.
    /// Knappen står på bjælken og venter, til man kigger.
    ///
    /// Den forsvinder sammen med bjælken. Gemte man ikke, var svaret nej.
    /// </remarks>
    private void DiktatFaerdig(string tekst) => Dispatcher.BeginInvoke(() =>
    {
        _sidsteDiktat = tekst;
        GemNoteKnap.Visibility = Visibility.Visible;
        GemNoteKnap.IsEnabled = true;
    });

    private void GemNote_Klik(object sender, RoutedEventArgs e)
    {
        if (_sidsteDiktat is not { Length: > 0 } tekst) return;

        Core.Diktatnoter.Tilfoej(tekst);

        GemNoteKnap.IsEnabled = false;
        _sidsteDiktat = null;

        DiktatBesked.Text = Core.Sprog.T("noter.gemt");

        // Er Noter-fanen fremme, skal den vise den med det samme.
        Diktering.NoterView.Aendret?.Invoke();
    }

    /// <summary>
    /// Sætter en besked på skærmen NU — ikke når der bliver tid.
    /// </summary>
    /// <remarks>
    /// VisDiktat lægger beskeden i kø, og køen bliver først tømt, når
    /// arbejdet er færdigt. Det er præcis forkert her: beskeden skal stå der,
    /// MENS man venter, ellers er der ikke noget at vente på.
    ///
    /// Derfor sættes teksten direkte, og der tvinges en tegning igennem, før
    /// der arbejdes videre. Det koster et par millisekunder og fjerner
    /// grunden til at trykke en gang til.
    /// </remarks>
    private void Kvitter(string besked)
    {
        DiktatBesked.Text = besked;
        DiktatBjaelke.Visibility = Visibility.Visible;

        // Tomt kald med Render-forrang: alt med hoejere forrang - herunder
        // selve tegningen - er faerdigt, naar den vender tilbage.
        Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Viser, hvad dikteringen laver — og skjuler bjælken igen bagefter.
    /// </summary>
    /// <remarks>
    /// Den bliver staaende et par sekunder, naar teksten er klar. «Klar — 80
    /// tegn ligger i udklipsholderen» er den eneste kvittering, man faar, og
    /// forsvandt den med det samme, ville man ikke vide, om det lykkedes.
    /// </remarks>
    private void VisDiktat(string besked) => Dispatcher.BeginInvoke(() =>
    {
        DiktatBesked.Text = besked;
        DiktatBjaelke.Visibility = Visibility.Visible;

        // En NY diktering rydder tilbuddet fra den forrige. Ellers ville man
        // gemme det forkerte, fordi knappen stod der endnu.
        if (_diktat.Igang)
        {
            GemNoteKnap.Visibility = Visibility.Collapsed;
            _sidsteDiktat = null;
        }

        _diktatUr?.Stop();

        if (_diktat.Igang) return;

        // TILBUDDET SKAL KUNNE NAAS. Seks sekunder er nok til at laese en
        // kvittering, men ikke til at komme tilbage fra det program, man
        // dikterede ind i, og tage stilling til en note.
        _diktatUr = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(GemNoteKnap.Visibility == Visibility.Visible ? 45 : 6),
        };

        _diktatUr.Tick += (_, _) =>
        {
            _diktatUr?.Stop();
            _diktatUr = null;

            // Er der begyndt et nyt diktat i mellemtiden, skal bjaelken blive.
            if (_diktat.Igang) return;

            DiktatBjaelke.Visibility = Visibility.Collapsed;

            // Gemte man ikke, mens den stod der, var svaret nej.
            GemNoteKnap.Visibility = Visibility.Collapsed;
            _sidsteDiktat = null;
        };

        _diktatUr.Start();
    });

    private System.Windows.Threading.DispatcherTimer? _diktatUr;

    /// <summary>Sat, mens et tryk er ved at blive udført.</summary>
    private bool _lynstartIGang;

    private void LynstartOptagelse()
    {
        // ============ ÉT TRYK ER ÉT TRYK ============
        //
        // Der gaar tid, foer der er noget at se: vinduet skal frem, mikrofonen
        // aabnes, og opstartsruden bygges. I mellemtiden sker der ingenting
        // paa skaermen, og saa trykker man igen - og igen.
        //
        // Set 30-08-2026: flere optagelser oven i hinanden, som alle skulle
        // lukkes bagefter. Brugeren havde gjort det eneste rigtige; det var
        // appen, der ikke kvitterede.
        //
        // To ting loeser det, og de skal begge to vaere der: der tages ikke
        // imod flere tryk, mens det foerste arbejder - og der kommer svar med
        // det samme, saa der ikke ER nogen grund til at trykke igen.
        if (_lynstartIGang) return;
        _lynstartIGang = true;

        Kvitter(Core.Sprog.T("topbar.aabner_optagelse"));

        try
        {
            LynstartNu();
        }
        finally
        {
            // Slippes foerst, naar skaermen er tegnet faerdig. Slap vi den her
            // og nu, ville et hurtigt dobbelttryk stadig naa igennem.
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => _lynstartIGang = false));
        }
    }

    private void LynstartNu()
    {
        // ============ VINDUET FREM FOERST - MEN DET MAA IKKE KUNNE STOPPE OS ============
        //
        // Raekkefoelgen er rigtig: Lynstart kan have brug for at vise noget -
        // «ingen mikrofon fundet», «mikrofonen er skiftet» - og en dialog paa
        // et minimeret vindue er en dialog, ingen ser. Saa staar man med en
        // genvej, der «ikke gjorde noget», mens svaret laa nede i proceslinjen.
        //
        // DET, DER MANGLEDE, VAR VAERNET. Windows kan afvise Activate() af sig
        // selv: har et andet program netop faaet fokus, gaelder foreground-
        // spaerren. Det er praecis situationen her - genvejen bruges, mens man
        // staar i Teams. Uden try naaede optagelsen aldrig at begynde.
        //
        // OPTAGELSE MAA ALDRIG KUNNE BLOKERES. Gaar vinduet galt, optages der
        // alligevel, og baandet ligger oeverst paa skaermen.
        try
        {
            App.HentFrem(this);
        }
        catch (Exception)
        {
            // Vinduet kom ikke frem. Der optages stadig.
        }

        _moede.Lynstart();
    }

    /// <summary>Grebet, der gælder nu. Null hvis vagten ikke kører.</summary>
    public Core.Genvejsgreb? AktivtGreb => _genvej.Kører ? _genvej.Greb : null;

    /// <summary>Navnet på genvejen, som brugeren ser den. Null hvis ingen.</summary>
    public string? AktivGenvejNavn => AktivtGreb?.Navn();

    /// <summary>
    /// Lader brugeren trykke sin egen tast. Svaret kommer, når han har trykket.
    /// </summary>
    /// <remarks>
    /// HER STOD «PAUSE» OG «GENOPTAG». Den gamle genvej skulle slippes, mens
    /// brugeren valgte en ny — ellers snappede den tastetrykket, startede en
    /// optagelse, og skærmen så aldrig tasten.
    ///
    /// Det problem findes ikke mere. Vagten ser tastaturet selv, og mens den
    /// fanger, udløser den ingenting. Trykket bliver også ædt, så det ikke
    /// samtidig lander i et tekstfelt bagved.
    /// </remarks>
    public void FangGreb(Action<Core.Genvejsgreb> naarTrykket) =>
        _genvej.Fanger = naarTrykket;

    /// <summary>Fortryder en fangst, der er i gang.</summary>
    public void AfbrydFangst() => _genvej.Fanger = null;

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

    // ============================ MENUEN KLAPPES ============================

    /// <summary>Bredden, når menuen er ude.</summary>
    private const double MenuUde = 196;

    /// <summary>Bredden, når den er inde: ét ikon plus luft på begge sider.</summary>
    private const double MenuInde = 62;

    /// <summary>
    /// Er menuen klappet ind?
    /// </summary>
    /// <remarks>
    /// DEN ER EN DEPENDENCYPROPERTY, fordi menupunkterne selv skal kunne se
    /// den. Deres skabelon ligger i App.xaml og skjuler teksten med en
    /// DataTrigger, der binder herop — så behøver koden ikke lede efter ni
    /// tekstfelter inde i ni skabeloner og sætte dem enkeltvis.
    /// </remarks>
    public static readonly DependencyProperty MenuSammenklappetProperty =
        DependencyProperty.Register(nameof(MenuSammenklappet), typeof(bool), typeof(MainWindow),
                                    new PropertyMetadata(false));

    public bool MenuSammenklappet
    {
        get => (bool)GetValue(MenuSammenklappetProperty);
        set => SetValue(MenuSammenklappetProperty, value);
    }

    /// <summary>
    /// Sætter menuen i den stand, den skal have — og husker den.
    /// </summary>
    /// <remarks>
    /// HJÆLPETEKSTERNE SÆTTES HER OG IKKE I XAML. Sammenklappet er ikonet det
    /// eneste, der er tilbage, og et ikon uden navn er en gætteleg. Teksten er
    /// punktets eget navn — den, der allerede er oversat — så den kan ikke
    /// komme til at sige noget andet end menupunktet.
    /// </remarks>
    /// <summary>
    /// Menuens bredde undervejs: 1 er helt ude, 0 er helt inde.
    /// </summary>
    /// <remarks>
    /// SAMME GREB SOM I COCKPITTET. En GridLength kan ikke animeres direkte,
    /// så der animeres ét almindeligt tal, og bredden regnes ud af det.
    ///
    /// Farten står i <see cref="Glid"/> — ét sted, så menuen og sidespalterne
    /// ikke kan komme til at bevæge sig forskelligt.
    /// </remarks>
    public static readonly DependencyProperty MenuudfoldningProperty =
        DependencyProperty.Register(nameof(Menuudfoldning), typeof(double), typeof(MainWindow),
            new PropertyMetadata(1.0, Menufoldet));

    public double Menuudfoldning
    {
        get => (double)GetValue(MenuudfoldningProperty);
        set => SetValue(MenuudfoldningProperty, value);
    }

    private static void Menufoldet(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MainWindow v) v.SaetMenubredde((double)e.NewValue);
    }

    /// <summary>Sætter bredden ud fra, hvor langt foldningen er nået.</summary>
    private void SaetMenubredde(double f)
    {
        Menubredde.Width = new GridLength(MenuInde + (MenuUde - MenuInde) * f);

        // Polstringen følger med. Blev den stående på fjorten, mens bredden
        // falder, klemmes ikonerne ud til siden undervejs.
        var kant = 8 + (14 - 8) * f;
        Menuindhold.Margin = new Thickness(kant, 22, kant, 16);
    }

    private void SaetMenu(bool sammenklappet, bool gem = true)
    {
        MenuSammenklappet = sammenklappet;

        // ============ TEKSTEN GAAR FOERST, KOMMER SIDST ============
        //
        // Klappes der ind, forsvinder teksten med det samme: en tekst, der
        // klippes over, mens bredden falder, ser i stykker ud.
        //
        // Klappes der ud, kommer den foerst, naar der ER plads.
        Logotekst.Visibility = sammenklappet ? Visibility.Collapsed : Visibility.Hidden;
        Datafod.Visibility = sammenklappet ? Visibility.Collapsed : Visibility.Hidden;

        Logolinje.Margin = new Thickness(sammenklappet ? 0 : 4, 0, 0, 2);
        Logolinje.HorizontalAlignment = sammenklappet
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;

        var animation = Glid.Til(sammenklappet ? 0.0 : 1.0);

        if (!sammenklappet)
        {
            animation.Completed += (_, _) =>
            {
                Logotekst.Visibility = Visibility.Visible;
                Datafod.Visibility = Visibility.Visible;
            };
        }

        BeginAnimation(MenuudfoldningProperty, animation);

        VisKlap(sammenklappet);

        var klaptekst = Core.Sprog.T(sammenklappet ? "nav.klap_ud" : "nav.klap_ind");

        Klap.ToolTip = klaptekst;

        // EN KNAP UDEN NAVN FINDES IKKE FOR EN SKAERMLAESER. Indholdet er en
        // pil i en ikonskrift, og den laeses som et tegn uden betydning.
        // Navnet skal derfor saettes i haanden - og det skifter med standen,
        // fordi knappen goer to forskellige ting.
        System.Windows.Automation.AutomationProperties.SetName(Klap, klaptekst);

        foreach (var knap in Menupunkter())
            knap.ToolTip = sammenklappet ? knap.Content as string : null;

        if (gem)
        {
            Core.AppSettings.Current.MenuSammenklappet = sammenklappet;
            Core.AppSettings.Current.Save();
        }
    }

    /// <summary>
    /// Pilen på klappen. Den peger den vej, klappen GØR noget: ind, når menuen
    /// er ude.
    /// </summary>
    private void VisKlap(bool sammenklappet)
    {
        if (Klap.Template.FindName("pil", Klap) is System.Windows.Controls.TextBlock pil)
            pil.Text = sammenklappet ? "\uE76C" : "\uE76B";
    }

    private IEnumerable<System.Windows.Controls.RadioButton> Menupunkter() => new[]
    {
        NavCockpit, NavDiktering, NavTransskriber, NavDokumenter, NavSkabeloner,
        NavMotor, NavCompliance, NavHistorik, NavIndstillinger,
    };

    private void Klap_Klik(object sender, RoutedEventArgs e) => SaetMenu(!MenuSammenklappet);

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
        else if (NavDiktering.IsChecked == true)
        {
            // Bygges hver gang: ordbogen er en tekstfil, og teksttypernes
            // instruktioner staar i indstillingerne - begge kan vaere rettet
            // et andet sted siden sidst.
            Indhold.Content = new Diktering.DikteringView();
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

        GemVinduesstoerrelse();

        _genvej.Dispose();
        base.OnClosing(e);
    }

    /// <summary>
    /// Husker, hvor stort vinduet stod.
    ///
    /// DER GEMMES «RESTORE»-MAALENE, ikke de aktuelle. Er vinduet maksimeret,
    /// er ActualWidth hele skærmen — og gemte man det, ville vinduet fylde
    /// skærmen ud, også når man bagefter trykker gendan. RestoreBounds er
    /// målene, det havde, FØR det blev maksimeret, og det er dem, man vil
    /// tilbage til.
    ///
    /// POSITIONEN GEMMES IKKE. En skærm, der er koblet fra siden i går, ville
    /// betyde et vindue, ingen kan se — en langt værre fejl end at åbne midt
    /// på skærmen.
    /// </summary>
    private void GemVinduesstoerrelse()
    {
        try
        {
            var maksimeret = WindowState == WindowState.Maximized;

            var maal = maksimeret || WindowState == WindowState.Minimized
                ? RestoreBounds
                : new Rect(Left, Top, ActualWidth, ActualHeight);

            // Et minimeret vindue uden gemte maal giver et tomt rektangel.
            // Saa gemmes der ingenting frem for et vindue paa nul gange nul.
            if (maal.Width >= MinWidth && maal.Height >= MinHeight)
            {
                AppSettings.Current.VinduesBredde = maal.Width;
                AppSettings.Current.VinduesHoejde = maal.Height;
            }

            AppSettings.Current.VinduetMaksimeret = maksimeret;
            AppSettings.Current.Save();
        }
        catch (Exception)
        {
            // En stoerrelse er ikke noget at forhindre en nedlukning for.
        }
    }

    /// <summary>
    /// Sætter vinduet, som det stod sidst.
    ///
    /// MAALENE KLEMMES IND PAA EN SKAERM, DER FINDES. Har man kørt appen på en
    /// bred skærm og starter den på den bærbare, ville et vindue på 3400
    /// pixel række langt ud over kanten — med lukkeknappen uden for skærmen.
    /// </summary>
    private void HentVinduesstoerrelse()
    {
        try
        {
            var b = AppSettings.Current.VinduesBredde;
            var h = AppSettings.Current.VinduesHoejde;

            if (b >= MinWidth && h >= MinHeight)
            {
                Width = Math.Min(b, SystemParameters.WorkArea.Width);
                Height = Math.Min(h, SystemParameters.WorkArea.Height);
            }

            if (AppSettings.Current.VinduetMaksimeret)
                WindowState = WindowState.Maximized;
        }
        catch (Exception)
        {
            // Saa staar maalene fra XAML'en. Appen skal aabne.
        }
    }
}
