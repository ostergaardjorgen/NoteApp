using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Preferences;

/// <summary>
/// Én sikkerhedskopi, som listen kan vise. Fulgte med fra «Filer og backup»,
/// da den skærm blev til to faner her.
/// </summary>
public sealed record ArkivVisning(string Navn, string Detaljer);

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

        Loaded += (_, _) => { Indlaes(); VisAutostart(); OpdaterFiler(); };
        Unloaded += (_, _) => { _timer.Stop(); StopProber(); AfbrydTest(); };
    }

    // ---------------------------------------------------------- klar til møde

    private void VisAutostart()
    {
        StartMedWindows.IsChecked = Autostart.ErSlaaetTil();

        AutostartStatus.Text = Autostart.ErForaeldet()
            ? "Bemærk: den gemte opstart peger på en anden placering, end appen kører fra nu. " +
              "Slå den fra og til igen, så bliver stien rettet."
            : "Appen åbner ikke et vindue ved opstart — den ligger klar, indtil du trykker genvejstasten.";

        VisGenveje();
    }

    private bool _indlæserGenveje;

    /// <summary>
    /// Listen over genvejskombinationer, med hvad der faktisk er ledigt lige
    /// nu. Ledigheden PRØVES af frem for at blive gættet — det er forskelligt
    /// fra maskine til maskine, og et valg, der ikke kan lade sig gøre, skal
    /// ikke stå som om det kan.
    /// </summary>
    private void VisGenveje()
    {
        var vindue = Window.GetWindow(this);
        if (vindue is null) return;

        _indlæserGenveje = true;

        var valgt = AppSettings.Current.HotkeyId;
        var rækker = GlobalHotkey.Muligheder.Select(m =>
        {
            var ledig = m.Id == valgt || GlobalHotkey.ErLedig(m, vindue);
            return new
            {
                m.Id,
                m.Hvorfor,
                Visning = ledig ? m.Navn : $"{m.Navn}  —  optaget af et andet program",
                Ledig = ledig
            };
        }).ToList();

        Genveje.ItemsSource = rækker;
        Genveje.SelectedItem = rækker.FirstOrDefault(r => r.Id == valgt)
                            ?? rækker.FirstOrDefault(r => r.Ledig);

        _indlæserGenveje = false;

        var v = Genveje.SelectedItem as dynamic;
        GenvejForklaring.Text = v is null ? "" : (string)v.Hvorfor;
    }

    private void Genvej_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_indlæserGenveje || Genveje.SelectedItem is null) return;

        var valg = (dynamic)Genveje.SelectedItem!;
        GenvejForklaring.Text = (string)valg.Hvorfor;

        if (!(bool)valg.Ledig)
        {
            Status.Text = "Den kombination er taget af et andet program. Vælg en anden.";
            return;
        }

        AppSettings.Current.HotkeyId = (string)valg.Id;
        AppSettings.Current.Save();

        // Registreringen skal ske med det samme. Et valg, der foerst virker
        // efter en genstart, er et valg, man tror er i kraft.
        if (Window.GetWindow(this) is MainWindow hoved) hoved.TilslutGenvej();

        Status.Text = $"Lynstart er nu {(string)valg.Visning}.";
    }

    private void Autostart_Klik(object sender, RoutedEventArgs e)
    {
        var til = StartMedWindows.IsChecked == true;
        var fejl = Autostart.Saet(til);

        if (fejl is not null)
        {
            // Feltet skal vise virkeligheden, ikke oensket. Et haevet flueben,
            // der ikke gjorde noget, er en loegn, man opdager en morgen.
            StartMedWindows.IsChecked = Autostart.ErSlaaetTil();
            Status.Text = $"Kunne ikke ændre opstarten: {fejl}";
            return;
        }

        Status.Text = til
            ? "NoteApp starter nu med Windows og ligger klar til genvejstasten."
            : "NoteApp starter ikke længere med Windows.";

        VisAutostart();
    }

    // ------------------------------------------------------------ mødevagten

    private void Moedevagt_Klik(object sender, RoutedEventArgs e)
    {
        var til = MoedevagtTil.IsChecked == true;

        AppSettings.Current.MoedevagtTil = til;
        AppSettings.Current.Save();

        // Hakket skal virke med det samme. Et valg, der foerst traeder i kraft
        // efter en genstart, er et valg, man tror er i kraft.
        if (Window.GetWindow(this) is MainWindow hoved) hoved.Moedevagten.Opdater();

        Status.Text = til
            ? "Appen spørger nu, når et andet program bruger mikrofonen i mere end et halvt minut."
            : "Appen holder ikke længere øje med mikrofonen.";

        VisFravalgte();
    }

    /// <summary>
    /// Et program, der har fået «spørg aldrig», sættes tilbage på listen.
    ///
    /// Fravalget tages med ét klik i en besked, der kommer, mens man er på vej
    /// ind til et møde — og det er også dér, man rammer forkert. Uden den her
    /// vej tilbage ville appen tie om netop det program, man holder sine møder
    /// i, uden at man kunne finde ud af hvorfor.
    /// </summary>
    private void Fravalg_Fjern(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string noegle) return;

        var s = AppSettings.Current;
        s.MoedevagtAldrig.RemoveAll(n => n.Equals(noegle, StringComparison.OrdinalIgnoreCase));
        s.Save();

        VisFravalgte();
        Status.Text = "Der spørges igen næste gang, det program bruger mikrofonen.";
    }

    /// <summary>
    /// Listen over fravalgte programmer, med et navn man kan genkende.
    ///
    /// Nøglen er en sti eller et pakkenavn og kan være hundrede tegn lang.
    /// Den står med småt under navnet — man skal kunne se, HVILKEN af to
    /// installationer det er, uden at skulle læse en sti for at genkende
    /// «Teams».
    /// </summary>
    private void VisFravalgte()
    {
        var liste = AppSettings.Current.MoedevagtAldrig
            .Select(n => new
            {
                Noegle = n,
                Navn = Mikrofonvagt.Navnet(n),
                Sti = n.Replace('#', '\\')
            })
            .ToList();

        Fravalgte.ItemsSource = liste;

        FravalgtPanel.Visibility = liste.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------------------------------------------------------- integrationer

    /// <summary>Én integration, som fanen viser den.</summary>
    public sealed class Integrationsvisning : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly Integration _i;
        private readonly Integrationsopsaetning _o;

        public Integrationsvisning(Integration i, Integrationsopsaetning o)
        {
            _i = i;
            _o = o;
        }

        public string Id => _i.Id;
        public string Navn => _i.Navn;
        public string Hvad => _i.Hvad;

        /// <summary>
        /// Hvad man skal gøre. Er appen ikke sat op til leverandøren, står det
        /// HER — og ikke som en fejl, brugeren har lavet.
        /// </summary>
        public string Hvordan => _i.Klar && !Googleklient.ErSatOp
            ? "Google-integrationen er ikke slået til i den her udgave af appen. Kontakt den, der har installeret den."
            : _i.Hvordan;

        public string Under => $"{_i.Leverandoer} · {_i.Hjemland}";

        public bool ErForbundet => _o.ErForbundet;

        public Visibility Opsaetningsvis =>
            _i.Klar && Googleklient.ErSatOp ? Visibility.Visible : Visibility.Collapsed;

        public Visibility Forbundetvis => _o.ErForbundet ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Forbindvis => _o.ErForbundet ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>
        /// Må der aktiveres? Først når kvitteringen øverst er sat.
        ///
        /// Knappen er GRÅ og ikke skjult. En knap, der ikke er der, er der
        /// ingen, der leder efter — og så finder man aldrig ud af, hvad der
        /// mangler. En grå knap med et svar i sin tooltip er selv forklaringen.
        /// </summary>
        public bool MaaAktivere => AppSettings.Current.IntegrationerLaest;

        public string Aktivertip => MaaAktivere
            ? "Åbner en side hos leverandøren, hvor du logger ind og godkender"
            : "Sæt hakket «Dette er læst og forstået» øverst først";

        public string Tilstand => !_i.Klar ? "KOMMER SENERE"
            : !Googleklient.ErSatOp ? "IKKE SLÅET TIL"
            : _o.ErForbundet ? "FORBUNDET"
            : "IKKE FORBUNDET";

        public Brush Tilstandsfarve => !_i.Klar || !Googleklient.ErSatOp
            ? (Brush)new BrushConverter().ConvertFrom("#FF9BA6B8")!
            : _o.ErForbundet
                ? (Brush)new BrushConverter().ConvertFrom("#FF4CBE72")!
                : (Brush)new BrushConverter().ConvertFrom("#FFE8A33D")!;

        /// <summary>
        /// Linjen under knapperne: hvornår der sidst blev hentet, og hvad der
        /// kom ud af det.
        ///
        /// EN FEJL SKAL STÅ, TIL DEN ER VÆK. Uden den ville en integration,
        /// der er holdt op med at virke, se ud som en, der bare ikke har
        /// hentet noget endnu — og de to kræver hver sin handling.
        /// </summary>
        public string Sidst
        {
            get
            {
                if (_o.SidsteFejl.Length > 0) return "Sidste forsøg gik galt: " + _o.SidsteFejl;

                if (_o.SidstHentet is not { } t) return "";

                return $"Hentede {_o.SidsteAntal} " +
                       $"{(_o.SidsteAntal == 1 ? "aftale" : "aftaler")} " +
                       $"{t.LocalDateTime:d. MMMM 'kl.' HH:mm}.";
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    private void VisIntegrationer()
    {
        VisNote();

        IntegrationerLaest.IsChecked = AppSettings.Current.IntegrationerLaest;

        LaestNote.Visibility = AppSettings.Current.IntegrationerLaest
            ? Visibility.Collapsed : Visibility.Visible;

        Integrationsliste.ItemsSource = Integrationer.Alle
            .Select(i => new Integrationsvisning(i, Integrationsfiler.Hent(i.Id)))
            .ToList();
    }

    /// <summary>
    /// Kvitteringen for at have læst, hvad en integration betyder.
    ///
    /// Den låser Aktiver-knapperne op. Tages hakket af igen, låses de igen —
    /// en forbindelse, der allerede står, bliver ikke afbrudt af det. Det ville
    /// være at straffe nogen for at klikke forkert, og forbindelsen kan
    /// afbrydes med den knap, der er sat i verden til det.
    /// </summary>
    // ---------------------------------------------- noten i indkaldelsen

    /// <summary>
    /// Fylder feltet med den tekst, der faktisk bliver brugt.
    ///
    /// Har man ikke skrevet sin egen, står standarden i feltet — ikke en tom
    /// rude med «her kunne der stå noget». Man skal kunne LÆSE, hvad der
    /// bliver sendt ud i ens navn, uden først at skulle fremkalde det.
    /// </summary>
    private void VisNote()
    {
        _fylderNote = true;
        Optagenote.Text = Googlekalender.Optagenote;
        _fylderNote = false;

        Notestatus.Text = AppSettings.Current.Optagenote.Length > 0
            ? "Din egen tekst." : "Standardteksten.";
    }

    /// <summary>
    /// Sandt, mens feltet fyldes fra koden.
    ///
    /// Uden det ville VisNote's egen skrivning tælle som en ændring, og
    /// statuslinjen ville sige «ikke gemt» om noget, ingen havde rørt.
    /// </summary>
    private bool _fylderNote;

    private void Optagenote_Aendret(object sender, TextChangedEventArgs e)
    {
        if (_fylderNote) return;

        Notestatus.Text = "Ikke gemt endnu.";
    }

    private void GemNote_Klik(object sender, RoutedEventArgs e)
    {
        var tekst = Optagenote.Text.Trim();

        // ER DEN LIG STANDARDEN, GEMMES DER INGENTING. Så følger noten med,
        // den dag standarden bliver bedre — i stedet for at stå fast på den
        // ordlyd, der tilfældigvis var i feltet den dag, man trykkede gem.
        AppSettings.Current.Optagenote =
            tekst == Googlekalender.StandardOptagenote.Trim() ? "" : tekst;

        AppSettings.Current.Save();

        VisNote();
        Notestatus.Text = AppSettings.Current.Optagenote.Length > 0
            ? "Gemt. Din egen tekst bruges." : "Gemt. Standardteksten bruges.";
    }

    private void NulstilNote_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.Optagenote = "";
        AppSettings.Current.Save();

        VisNote();
        Notestatus.Text = "Nulstillet til standardteksten.";
    }

    private void IntegrationerLaest_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.IntegrationerLaest = IntegrationerLaest.IsChecked == true;
        AppSettings.Current.Save();

        VisIntegrationer();
    }

    /// <summary>
    /// Godkender hos leverandøren. Browseren åbnes, og appen venter på svaret.
    ///
    /// Der spørges FØRST. Handlingen sender brugeren ud af appen og ind på en
    /// side hos Google, og det skal man vide, inden browseren springer op.
    /// </summary>
    private async void Forbind_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        if (Integrationer.Find(id) is not { } i) return;

        var o = Integrationsfiler.Hent(id);

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Forbind til {i.Navn}?",
            Googlekalender.Vejledning,
            godkend: "Åbn browseren", annuller: "Ikke nu", slags: Dialogs.Slags.Valg);

        if (!ja) return;

        Status.Text = $"Venter på godkendelse i browseren …";

        try
        {
            var noegle = await Googlekalender.ForbindAsync();

            o.Opdateringsnoegle = noegle;
            o.SidsteFejl = "";
            Integrationsfiler.Gem(id, o);

            Status.Text = $"{i.Navn} er forbundet.";
            VisIntegrationer();

            await Hent(id);
        }
        catch (Exception ex)
        {
            o.SidsteFejl = ex.Message;
            Integrationsfiler.Gem(id, o);

            Status.Text = "Forbindelsen blev ikke oprettet.";
            VisIntegrationer();

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke forbinde",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    private async void Hent_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id }) await Hent(id);
    }

    /// <summary>
    /// Henter aftalerne ind. Alt fra kilden afløses — se Kalender.Afloes.
    /// </summary>
    private async Task Hent(string id)
    {
        if (Integrationer.Find(id) is not { } i) return;

        var o = Integrationsfiler.Hent(id);
        if (!o.ErForbundet) return;

        Status.Text = $"Henter aftaler fra {i.Navn} …";

        try
        {
            var aftaler = await Googlekalender.HentAsync(o.Opdateringsnoegle);

            var n = Kalender.Afloes(i.Kilde, aftaler);

            o.SidstHentet = DateTimeOffset.Now;
            o.SidsteAntal = n;
            o.SidsteFejl = "";
            Integrationsfiler.Gem(id, o);

            Status.Text = $"Hentede {n} {(n == 1 ? "aftale" : "aftaler")} fra {i.Navn}.";
        }
        catch (Exception ex)
        {
            o.SidsteFejl = ex.Message;
            Integrationsfiler.Gem(id, o);

            Status.Text = "Aftalerne kunne ikke hentes.";
        }

        VisIntegrationer();
    }

    /// <summary>
    /// Glemmer forbindelsen — og de aftaler, den havde hentet.
    ///
    /// AFTALERNE SKAL MED VÆK. Bliver de stående, kan appen ikke længere holde
    /// dem opdaterede, og en aflyst aftale ville stå der for evigt.
    /// </summary>
    private void Afbryd_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        if (Integrationer.Find(id) is not { } i) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Afbryd forbindelsen til {i.Navn}?",
            "De aftaler, appen har hentet derfra, forsvinder fra kalenderen. Dine egne " +
            "aftaler bliver stående.\n\n" +
            "Du kan forbinde igen senere — klient-id'et bliver ikke slettet.",
            godkend: "Afbryd forbindelsen", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        var o = Integrationsfiler.Hent(id);
        o.Opdateringsnoegle = "";
        o.SidstHentet = null;
        o.SidsteAntal = 0;
        o.SidsteFejl = "";
        Integrationsfiler.Gem(id, o);

        Kalender.Fjern(i.Kilde);

        Status.Text = $"Forbindelsen til {i.Navn} er afbrudt.";
        VisIntegrationer();
    }

    private void Indlaes()
    {
        MoedevagtTil.IsChecked = AppSettings.Current.MoedevagtTil;
        VisFravalgte();
        VisIntegrationer();

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

    }

    private static void Vis(TextBlock felt, bool synlig, string tekst)
    {
        felt.Visibility = synlig ? Visibility.Visible : Visibility.Collapsed;
        felt.Text = synlig ? tekst : "";
    }

    // ------------------------------------------------------------------ valg

    // HER LAA VALGET AF SPROG - dit og modpartens. Flyttet til
    // Transcribe\SprogvalgWindow, hvor der spoerges ved selve knappen.
    // En global indstilling var rigtig for ét moede og forkert for det
    // naeste, og fejlen viste sig ikke som en fejl: udskriften saa faerdig
    // ud, den var bare paa det forkerte sprog.

    private void Mikrofon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Mikrofoner.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.MicrophoneId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Mikrofon: {d.FriendlyName}";
    }

    private void Hoejttaler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Hoejttalere.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.SpeakerId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Højttaler: {d.FriendlyName}";
    }

    private void Genindlaes_Click(object sender, RoutedEventArgs e)
    {
        StopProber();
        Indlaes();
        Status.Text = "Enhedslisten er opdateret.";
    }

    // ---------------------------------------------------------------- målere

    /// <summary>
    /// Åbner mikrofon og højttaler for at MÅLE niveauet — kun når brugeren
    /// har bedt om det.
    ///
    /// Før kørte målingen, så snart man åbnede Indstillinger, og blev ved,
    /// så længe man var der. Appen lyttede altså uden at nogen havde trykket
    /// optag. Det er forkert i en app, hvis hele løfte er, at man ved præcis,
    /// hvornår der bliver optaget — og det er også skidt for optagelser: to
    /// programmer om den samme enhed er sjældent gratis.
    ///
    /// Måleren slukker af sig selv efter et stykke tid. En prøve, man skal
    /// huske at stoppe, bliver ikke stoppet.
    /// </summary>
    private void StartProber()
    {
        StopProber();

        if (Mikrofoner.SelectedItem is DeviceInfo m)
            _mikProbe = WasapiLevelProbe.Start(m.Id, loopback: false);

        if (Hoejttalere.SelectedItem is DeviceInfo h)
            _hoejtProbe = WasapiLevelProbe.Start(h.Id, loopback: true);

        _timer.Start();

        _proeveSlut = DateTime.Now.AddSeconds(20);
        ProeveKnap.Content = "■ Stop prøven";
        MikStatus.Text = "Sig noget — måleren skal røre sig";
    }

    private DateTime _proeveSlut = DateTime.MinValue;

    private void Proeve_Click(object sender, RoutedEventArgs e)
    {
        if (_mikProbe is null && _hoejtProbe is null) StartProber();
        else StopProber();
    }

    private void StopProber()
    {
        _mikProbe?.Dispose();
        _hoejtProbe?.Dispose();
        _mikProbe = null;
        _hoejtProbe = null;

        _timer.Stop();
        _proeveSlut = DateTime.MinValue;

        if (ProeveKnap is not null) ProeveKnap.Content = "▶ Prøv mikrofon og højttaler";
        if (MikNiveau is not null) MikNiveau.Width = 0;
        if (HoejtNiveau is not null) HoejtNiveau.Width = 0;
        if (MikStatus is not null) MikStatus.Text = "Måleren kører kun, mens du prøver";
        if (HoejtStatus is not null) HoejtStatus.Text = "";
    }

    private void OpdaterMaalere()
    {
        // Proeven slukker af sig selv. En maaler, der koerer videre, er en
        // mikrofon, der staar aaben uden grund.
        if (_proeveSlut != DateTime.MinValue && DateTime.Now > _proeveSlut) { StopProber(); return; }

        Tegn(_mikProbe, MikNiveau, MikStatus, "Sig noget — måleren skal røre sig");
        Tegn(_hoejtProbe, HoejtNiveau, HoejtStatus, "Afspil lyd — måleren skal røre sig");
    }

    // -------------------------------------------------------- mikrofontesten

    private ShortClipRecorder? _testKlip;
    private DispatcherTimer? _testUr;
    private DateTime _testStart;
    private string? _testMappe;

    /// <summary>
    /// Starter eller stopper testen.
    ///
    /// Den samme knap gør begge dele med vilje. En separat stopknap ville
    /// stå og være grå det meste af tiden, og man skal kunne slutte, når man
    /// er færdig med at læse — ikke vente på et ur.
    /// </summary>
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_testKlip is not null) { await AfslutTest(); return; }

        // Uden en motor er der intet at maale med. Det skal staa nu og ikke
        // efter en oplaesning paa tredive sekunder.
        var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
        if (!install.IsComplete)
        {
            TestStatus.Text = "Testen kræver, at Whisper er hentet. Det sker under «AI-modeller».";
            return;
        }

        // To programmer om den samme mikrofon er sjaeldent gratis, og
        // niveaumaaleren holder enheden aaben.
        StopProber();

        _testMappe = Path.Combine(Path.GetTempPath(), "noteapp-miktest-" + Guid.NewGuid().ToString("N")[..8]);
        _testKlip = new ShortClipRecorder(Path.Combine(_testMappe, "proeve.wav"));

        try
        {
            _testKlip.Start(AppSettings.Current.MicrophoneId);
        }
        catch (Exception ex)
        {
            _testKlip.Dispose();
            _testKlip = null;
            TestStatus.Text = $"Mikrofonen kunne ikke åbnes: {ex.Message}";
            return;
        }

        _testStart = DateTime.Now;

        TestTekst.Text = Mikrofontest.Proevetekst;
        TestTrin.Text = "Læs højt — i almindeligt tempo";
        TestRude.Visibility = Visibility.Visible;
        TestSvar.Visibility = Visibility.Collapsed;
        TestKnap.Content = "■ Jeg er færdig";
        TestStatus.Text = "Optager. Læs teksten højt, og tryk «Jeg er færdig», når du er igennem.";

        _testUr = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _testUr.Tick += TestUr_Tick;
        _testUr.Start();
    }

    private async void TestUr_Tick(object? sender, EventArgs e)
    {
        if (_testKlip is null) return;

        var gaaet = DateTime.Now - _testStart;
        TestUr.Text = $"{gaaet.TotalSeconds:0} sek";

        var bredde = TestNiveau.Parent is FrameworkElement f && f.ActualWidth > 0 ? f.ActualWidth : 300;
        TestNiveau.Width = bredde * PeakMeter.ToMeterScale(_testKlip.Niveau);

        // Optagelsen stopper af sig selv. Glemmer man knappen, skal
        // mikrofonen ikke staa aaben resten af dagen.
        if (gaaet > Mikrofontest.Maksimum) await AfslutTest();
    }

    /// <summary>
    /// Stopper optagelsen, skriver den ud og bedømmer den.
    /// </summary>
    private async Task AfslutTest()
    {
        var klip = _testKlip;
        var mappe = _testMappe;
        if (klip is null) return;

        _testKlip = null;
        _testUr?.Stop();
        _testUr = null;
        TestNiveau.Width = 0;

        double sekunder;
        try { sekunder = klip.Stop(); }
        finally { klip.Dispose(); }

        TestKnap.IsEnabled = false;
        TestKnap.Content = "▶ Start test";

        if (sekunder < Mikrofontest.Mindstelaengde.TotalSeconds)
        {
            Ryd(mappe);
            TestRude.Visibility = Visibility.Collapsed;
            TestKnap.IsEnabled = true;
            TestStatus.Text = sekunder <= 0
                ? "Der kom ingen lyd ud af optagelsen. Tjek, at den rigtige mikrofon er valgt ovenfor."
                : $"Optagelsen var kun {sekunder:0} sekunder. Læs hele teksten igennem, så der er nok at måle på.";
            return;
        }

        TestTrin.Text = "Skriver lyden ud …";
        TestUr.Text = "";
        TestStatus.Text = "Det tager typisk et halvt minut.";

        try
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
            var motor = new Transcriber(install.WhisperCli!);

            // Sproget er LAAST til dansk her, modsat moeder hvor det er
            // "auto". Vi ved, hvad der blev sagt; et fejlgaettet sprog ville
            // maale Whispers sproggenkendelse i stedet for mikrofonen.
            var r = await motor.RunAsync(
                new TranscriptionRequest(klip.Path, install.ModelPath!,
                                         Path.Combine(mappe!, "proeve"), "da"),
                null, CancellationToken.None);

            VisTestsvar(Mikrofontest.Bedoem(r.Text, sekunder));
        }
        catch (Exception ex)
        {
            TestRude.Visibility = Visibility.Collapsed;
            TestStatus.Text = $"Testen kunne ikke gennemføres: {ex.Message}";
        }
        finally
        {
            // Lyden er brugerens stemme. Den maa ikke blive liggende i en
            // temp-mappe, naar den har gjort sit.
            Ryd(mappe);
            TestKnap.IsEnabled = true;
        }
    }

    /// <summary>
    /// Kaster testen væk uden at måle noget. Bruges, når man forlader
    /// skærmen midt i en oplæsning — mikrofonen må ikke blive stående åben,
    /// bare fordi man klikkede videre.
    /// </summary>
    private void AfbrydTest()
    {
        if (_testKlip is null) return;

        var klip = _testKlip;
        var mappe = _testMappe;
        _testKlip = null;
        _testUr?.Stop();
        _testUr = null;

        try { klip.Stop(); } catch (Exception) { }
        klip.Dispose();
        Ryd(mappe);
    }

    private static void Ryd(string? mappe)
    {
        try { if (mappe is not null && Directory.Exists(mappe)) Directory.Delete(mappe, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void VisTestsvar(Mikrofontest.Resultat r)
    {
        TestRude.Visibility = Visibility.Collapsed;
        TestSvar.Visibility = Visibility.Visible;
        TestStatus.Text = "";

        TestTal.Text = $"{r.Procent:0} %";
        TestTal.Foreground = (Brush)FindResource(r.Karakter switch
        {
            Mikrofontest.Bedoemmelse.God => "Godkendt",
            Mikrofontest.Bedoemmelse.Brugbar => "Advarsel",
            _ => "FejlTekst"
        });

        TestDom.Text = $"{r.Ramt} af {r.Ialt} ord ramt · {r.Sekunder:0} sekunder";
        TestRaad.Text = r.Raad;

        // De faktiske fejl staar der, saa tallet kan efterproeves. Et tal, man
        // ikke kan se grundlaget for, er et tal, man enten tror blindt paa
        // eller afviser — begge dele er forkert.
        TestFejl.Text = r.Afvigelser.Count == 0
            ? "Ingen afvigelser."
            : "Hørt forkert: " + string.Join(", ", r.Afvigelser
                .Where(a => a.Forventet.Length > 0)
                .Take(12)
                .Select(a => a.Hørt.Length == 0
                    ? $"«{a.Forventet}» blev ikke hørt"
                    : $"«{a.Forventet}» → «{a.Hørt}»"));

        TestUdskrift.Text = r.Udskrift;
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

    // ================= FLYTTET FRA «FILER OG BACKUP» ==================
    //
    // Skaermen var sit eget menupunkt. Den er nu to faner her, og koden er
    // flyttet med uaendret. To ting maatte omdoebes, fordi navnene fandtes i
    // forvejen: Status blev til FilStatus, og Proeve_Click - som proevekoerte
    // et arkiv - blev til ProeveArkiv_Click, saa den ikke forveksles med den,
    // der proever mikrofonen.

    // FilesView's konstruktoer er vaek - SettingsView har sin egen, og den
    // kalder OpdaterFiler() ved Loaded sammen med resten.

    private string Destination =>
        AppSettings.Current.BackupDestination ?? BackupService.DefaultDestination;

    private void OpdaterFiler()
    {
        DataSti.Text = UserDataPaths.Root;

        var optagelser = Directory.Exists(UserDataPaths.Meetings)
            ? Directory.GetDirectories(UserDataPaths.Meetings).Length : 0;
        var dokumenter = NoteApp.Core.Documents.DocumentStore.LoadAll().Count;
        DataIndhold.Text = $"{optagelser} optagelser · {dokumenter} dokumenter";

        BackupSti.Text = Destination;

        var seneste = BackupService.Latest(Destination);
        BackupSenest.Text = seneste is null
            ? "Ingen sikkerhedskopi taget endnu."
            : $"Senest {seneste.When:dd/MM/yyyy HH:mm} · {seneste.MegaBytes:0.0} MB";

        var ude = BackupService.LeavesMachine(Destination);
        BackupAdvarsel.Visibility = ude ? Visibility.Visible : Visibility.Collapsed;
        BackupAdvarsel.Text = ude
            ? "Denne mappe ligger uden for maskinen. Arkivet er ikke krypteret, og alle med adgang til den kan åbne det."
            : "";

        TagLydMed.IsChecked = AppSettings.Current.BackupIncludeAudio;

        // Forskellen mellem med og uden lyd er typisk tre stoerrelsesordener.
        // Den skal staa der, ellers er afkrydsningsfeltet et gaet.
        var (medLyd, udenLyd, lydFiler) = BackupService.Estimate();
        LydStoerrelse.Text = lydFiler == 0
            ? "Der er ingen lydfiler endnu."
            : $"{lydFiler} lydfiler. Med lyd: {medLyd / 1024.0 / 1024.0:0} MB · uden lyd: {udenLyd / 1024.0 / 1024.0:0.0} MB";

        Arkiver.ItemsSource = BackupService.Existing(Destination)
            .Select(a => new ArkivVisning(Path.GetFileName(a.Path),
                                          $"{a.When:dd/MM HH:mm} · {a.MegaBytes:0.0} MB"))
            .ToList();
    }

    // ------------------------------------------------------------ datamappen

    private void SkiftData_Click(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe("Vælg hvor NoteApps filer skal ligge", UserDataPaths.Root);
        if (valgt is null) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            "Flyt dine filer hertil?",
            $"{valgt}\n\n" +
            $"Alt i {UserDataPaths.Root} kopieres derover, og det gamle sted ryddes bagefter. " +
            "Optagelser, noter og indlærte rettelser følger med.",
            godkend: "Flyt filerne", annuller: "Bliv hvor de er");

        if (!ja) return;

        try
        {
            UserDataPaths.SetRoot(valgt);
            FilStatus.Text = $"Dine filer ligger nu i {UserDataPaths.Root}";
            OpdaterFiler();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke flytte", ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    private void AabnData_Click(object sender, RoutedEventArgs e) => Aabn(UserDataPaths.Root);

    // ----------------------------------------------------------------- backup

    private void SkiftBackup_Click(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe("Vælg hvor sikkerhedskopien skal ligge", Destination);
        if (valgt is null) return;

        // De tre krav fra datagraensen: valget er aktivt, godkendelsen sker
        // her og nu, og risikoen staar listet FOER der spoerges.
        if (BackupService.LeavesMachine(valgt))
        {
            var lokale = string.Join(", ", BackupService.LocalDrives());
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Den mappe ligger uden for denne maskine",
                $"{valgt}\n\n" +
                "Hvad du er ved at beslutte:\n\n" +
                "• Arkivet indeholder ALT: mødeoptagelser som lyd, transskriptioner, " +
                "dine noter og dine indlærte rettelser.\n" +
                "• Zip-filen er IKKE krypteret — hverken undervejs eller når den ligger der.\n" +
                "• Alle med adgang til mappen kan åbne den, også administratorer og " +
                "backup af det system, den lander på.\n" +
                "• Mødedeltagerne har ikke sagt ja til dette.\n" +
                "• Det kan ikke fortrydes. En kopi, der først er ude, er ude.\n\n" +
                $"Lokale drev lige nu: {lokale}",
                godkend: "Brug den alligevel", annuller: "Vælg et lokalt drev",
                slags: Dialogs.Slags.Fejl, godkendErStandard: false);

            var svar = ja ? MessageBoxResult.OK : MessageBoxResult.Cancel;

            if (svar != MessageBoxResult.OK) return;

            BackupService.Log($"GODKENDT: backupmappe uden for maskinen -> {valgt}");
        }

        AppSettings.Current.BackupDestination = valgt;
        AppSettings.Current.Save();
        FilStatus.Text = $"Sikkerhedskopier lægges nu i {valgt}";
        OpdaterFiler();
    }

    private void Lyd_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.BackupIncludeAudio = TagLydMed.IsChecked == true;
        AppSettings.Current.Save();
        OpdaterFiler();
    }

    /// <summary>
    /// Tager sikkerhedskopien — på en baggrundstråd.
    ///
    /// HVORFOR DET IKKE MAA KOERE HER
    ///
    /// Foer laa BackupService.Run() direkte i denne handler, altsaa paa
    /// UI-traaden. Med et par hundrede megabyte gik det upaafaldende. Da
    /// datamappen voksede, holdt vinduet op med at tegne sig selv, og Windows
    /// skrev «Svarer ikke» i titelbjaelken, mens kopien koerte helt fint.
    ///
    /// Det er den vaerste slags fejl i netop denne funktion: en bruger, der
    /// tror, appen er gaaet ned, lukker den — og saa ER sikkerhedskopien
    /// afbrudt. Fejlen skabte det nedbrud, den lignede.
    ///
    /// Task.Run holder traaden fri, saa vinduet tegner og knappen kan vise,
    /// at der arbejdes.
    /// </summary>
    private async void Koer_Click(object sender, RoutedEventArgs e)
    {
        KoerKnap.IsEnabled = false;
        var oprindeligTekst = KoerKnap.Content;
        KoerKnap.Content = "Tager kopi …";

        var medLyd = AppSettings.Current.BackupIncludeAudio;
        var start = DateTime.Now;

        BackupBjaelke.Visibility = Visibility.Visible;
        BackupBjaelke.Value = 0;

        // FREMDRIFTEN KOMMER FRA KOPIEN SELV, IKKE FRA ET UR.
        //
        // Her taalte et ur sekunder, og det er ikke det samme: sekunder siger,
        // at der GAAR tid, men ikke hvor meget der er igen. En kopi med lyd er
        // hundredvis af megabyte, og en langsom koersel ligner en, der haenger.
        // Saa lukker man appen midt i den - og saa ER kopien afbrudt.
        var senest = DateTime.MinValue;

        var fremdrift = new Progress<BackupService.BackupFremdrift>(p =>
        {
            BackupBjaelke.Value = p.Procent;

            // Der tegnes hoejst fem gange i sekundet. Ved tusindvis af smaa
            // filer ville hver enkelt ellers udloese en opdatering, og saa
            // bruger skaermen mere tid end selve kopieringen.
            var nu = DateTime.Now;
            if (p.Gjort < p.IAlt && (nu - senest).TotalMilliseconds < 200) return;
            senest = nu;

            var gaaet = DateTime.Now - start;

            FilStatus.Text = p.Fil.Length == 0
                ? $"Lukker arkivet … {gaaet.TotalSeconds:0} sek."
                : $"Tager sikkerhedskopi … {p.Gjort} af {p.IAlt} filer " +
                  $"({p.Procent:0} %) · {gaaet.TotalSeconds:0} sek. — {p.Fil}";
        });

        // Uret loeber stadig, men kun saa laenge der ikke er meldt fremdrift
        // endnu: filerne skal foerst taelles op, og det tager tid i sig selv
        // paa en stor datamappe.
        var ur = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        ur.Tick += (_, _) =>
        {
            if (senest != DateTime.MinValue) return;

            var gaaet = DateTime.Now - start;
            FilStatus.Text = $"Finder filerne … {gaaet.TotalSeconds:0} sek. " +
                          "Du kan roligt lave noget andet imens.";
        };
        ur.Start();

        try
        {
            var r = await Task.Run(() =>
                BackupService.Run(Destination, includeAudio: medLyd, fremdrift: fremdrift));

            FilStatus.Text = $"Færdig: {r.Files} filer, {r.MegaBytes:0.0} MB " +
                          $"({(medLyd ? "med lyd" : "uden lyd")}) på {r.Elapsed.TotalSeconds:0.0} sek. " +
                          $"{r.Kept} arkiver gemt.";
            OpdaterFiler();
        }
        catch (Exception ex)
        {
            FilStatus.Text = "Sikkerhedskopien fejlede.";
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke tage backup", ex.Message, Dialogs.Slags.Pas_paa);
        }
        finally
        {
            ur.Stop();
            BackupBjaelke.Visibility = Visibility.Collapsed;
            KoerKnap.Content = oprindeligTekst;
            KoerKnap.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------ gendannelse

    private void Arkiv_Valgt(object sender, SelectionChangedEventArgs e)
    {
        var valgt = Arkiver.SelectedItem as ArkivVisning;
        ProeveArkivKnap.IsEnabled = valgt is not null;
        GendanKnap.IsEnabled = valgt is not null;

        if (valgt is null)
        {
            ValgtArkiv.Text = "Vælg et arkiv for at se, hvad det indeholder.";
            return;
        }

        try
        {
            var i = RestoreService.Inspect(Path.Combine(Destination, valgt.Navn));
            ValgtArkiv.Text = $"{i.Files} filer · {i.MegaBytes:0.0} MB · {i.Summary}";
        }
        catch (Exception ex)
        {
            ValgtArkiv.Text = $"Kan ikke læse arkivet: {ex.Message}";
            ProeveArkivKnap.IsEnabled = false;
            GendanKnap.IsEnabled = false;
        }
    }

    /// <summary>
    /// Prøvekørslen rører ikke dine data. Den findes, fordi en backup, man
    /// aldrig har prøvet at gendanne, er en formodning — og det opdager man
    /// ellers først den dag, det gælder.
    /// </summary>
    private void ProeveArkiv_Click(object sender, RoutedEventArgs e)
    {
        if (Arkiver.SelectedItem is not ArkivVisning valgt) return;
        var sti = Path.Combine(Destination, valgt.Navn);

        try
        {
            var i = RestoreService.Inspect(sti);
            var udpakket = RestoreService.TestRestore(sti);

            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Prøvekørsel gennemført",
                "Dine nuværende data er IKKE rørt.\n\n" +
                $"Arkivet: {valgt.Navn}\n" +
                $"Indhold: {i.Files} filer, {i.MegaBytes:0.0} MB — {i.Summary}\n" +
                (i.HasDictionary
                    ? "Rettelserne er med og er en gyldig databasefil.\n"
                    : "BEMÆRK: der er ingen rettelser i arkivet.\n") +
                (i.AudioFiles == 0
                    ? "Der er ingen lyd i arkivet — optagelserne kan ikke afspilles efter en gendannelse.\n"
                    : "") +
                $"\nUdpakket til:\n{udpakket}",
                godkend: "Åbn mappen", annuller: "Luk",
                slags: Dialogs.Slags.Godt);

            if (ja) Aabn(udpakket);
            FilStatus.Text = $"Prøvekørsel af {valgt.Navn} gennemført.";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Prøvekørslen fejlede", ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    private void Gendan_Click(object sender, RoutedEventArgs e)
    {
        if (Arkiver.SelectedItem is not ArkivVisning valgt) return;
        var sti = Path.Combine(Destination, valgt.Navn);

        ArchiveContents i;
        try { i = RestoreService.Inspect(sti); }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke læse arkivet", ex.Message, Dialogs.Slags.Pas_paa);
            return;
        }

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Gendan fra {valgt.Navn}?",
            $"Fra {i.Created:dd/MM/yyyy HH:mm} · {i.Summary}\n\n" +
            $"Filerne skrives ind i {UserDataPaths.Root} og overskriver dem, der hedder det samme.\n\n" +
            (i.AudioFiles == 0
                ? "Arkivet indeholder ingen lyd. Eksisterende lydfiler bliver liggende — de bliver ikke slettet.\n\n"
                : "") +
            "Der tages automatisk et sikkerhedsarkiv af dine nuværende data først, så du kan fortryde.",
            godkend: "Gendan nu", annuller: "Lad være",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            var r = RestoreService.Restore(sti);

            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Gendannet", $"{r.FilesWritten} filer gendannet.\n\n" +
                (r.SafetyArchive is null
                    ? "Der blev ikke taget et fortrydelsesarkiv — datamappen var tom, så der var intet at sikre.\n\n"
                    : $"Dine tidligere data ligger som:\n{r.SafetyArchive}\n\n") +
                "LUK OG START APPEN IGEN, så de gendannede data læses ind. Indtil da " +
                "viser appen stadig det, den havde i hukommelsen.", Dialogs.Slags.Valg);

            FilStatus.Text = $"Gendannet fra {valgt.Navn}. Genstart appen.";
            OpdaterFiler();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Gendannelsen fejlede", ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    // ------------------------------------------------------------------ hjælp

    private string? VaelgMappe(string titel, string start)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = titel,
            InitialDirectory = Directory.Exists(start) ? start : "C:\\"
        };
        return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FolderName : null;
    }

    private static void Aabn(string sti)
    {
        Directory.CreateDirectory(sti);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{sti}\"") { UseShellExecute = true });
    }
}
