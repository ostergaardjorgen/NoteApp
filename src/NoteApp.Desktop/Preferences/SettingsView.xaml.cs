using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Llm;

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

        Loaded += (_, _) => { Indlaes(); VisTema(); LytPaaTema(); VisGenvejNu(); VisAutostart(); OpdaterFiler(); VisKrav(); VisPlads(); VisOvervaagede(); IndlaesDiktering(); };
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
    }

    // ------------------------------------------------ tryk din egen genvej

    private bool _fanger;

    /// <summary>
    /// Viser den genvej, der gælder nu — og hvilken tast det er.
    /// </summary>
    /// <remarks>
    /// NAVNET ER DET, DER STÅR PÅ TASTEN: «Ctrl+Shift+,». Hvilket af de to
    /// kommaer det er, står som en linje nedenunder — det er dér, forskellen
    /// hører til, ikke inde i navnet.
    /// </remarks>
    private void VisGenvejNu()
    {
        if (GenvejNu is null) return;

        var greb = (Window.GetWindow(this) as MainWindow)?.AktivtGreb;

        GenvejNu.Text = greb?.Navn() ?? "Ingen genvej";

        if (greb is null)
        {
            GenvejStatus.Text = "Tastaturvagten kører ikke. Genstart appen.";
            return;
        }

        var hvor = greb.Hvor();
        var egen = AppSettings.Current.Genvejsgreb is not null;

        GenvejStatus.Text =
            (hvor.Length > 0 ? $"Det er {hvor}. " : "")
            + (egen
                ? "Du har trykket den selv."
                : "Standardvalget. Tryk «Vælg genvej», og brug den tast, du faktisk rammer.");
    }

    /// <summary>
    /// Lader brugeren trykke sin egen tast.
    /// </summary>
    /// <remarks>
    /// HER LAA TRE TRIN: fang, registrér, og bekræft med et ekstra tryk. Det
    /// midterste kunne mislykkes — Windows kunne have taget kombinationen — og
    /// det sidste fandtes, fordi de to første kunne lykkes, mens genvejen
    /// alligevel var død.
    ///
    /// Ingen af delene findes mere. Appen ser tastaturet selv, så der er ingen
    /// registrering at mislykkes med, og intet at bekræfte: den tast, der blev
    /// trykket, ER den, der lyttes efter. Ét trin.
    /// </remarks>
    private void GenvejVaelg_Klik(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow hoved) return;

        if (_fanger)
        {
            hoved.AfbrydFangst();
            StopFanger();
            return;
        }

        _fanger = true;
        GenvejFanger.Visibility = Visibility.Visible;
        GenvejFanger.Foreground = (System.Windows.Media.Brush)FindResource("Tekst");
        GenvejFanger.Text = "Tryk den kombination, du vil bruge — hold Ctrl, Shift eller Alt nede.";
        GenvejVaelg.Content = "Fortryd";

        hoved.FangGreb(GemGreb);
    }

    /// <summary>Tasten er trykket. Så er det den, der gælder.</summary>
    private void GemGreb(Genvejsgreb greb)
    {
        AppSettings.Current.Genvejsgreb = greb.Gem();
        AppSettings.Current.Save();

        StopFanger();

        if (Window.GetWindow(this) is MainWindow hoved) hoved.TilslutGenvej();

        VisGenvejNu();

        var hvor = greb.Hvor();
        Status.Text = $"Genvejen er nu {greb.Navn()}"
                      + (hvor.Length > 0 ? $" — {hvor}." : ".");
    }

    private void StopFanger()
    {
        _fanger = false;
        GenvejVaelg.Content = NoteApp.Core.Sprog.T("settingsview.vaelg_genvej");
        GenvejFanger.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- temaet

    /// <summary>
    /// Sandt, mens knapperne sættes op efter det gemte valg.
    /// </summary>
    /// <remarks>
    /// RadioButton.Checked fyrer også, når koden sætter hakket. Uden det her
    /// ville indlæsningen skrive indstillingen tilbage og skifte tema, hver
    /// gang skærmen blev åbnet — usynligt, indtil man en dag ser temaet
    /// blinke, fordi man kiggede på Indstillinger.
    /// </remarks>
    private bool _saetterTema;

    private void Tema_Valgt(object sender, RoutedEventArgs e)
    {
        if (_saetterTema) return;

        var valg = TemaLyst.IsChecked == true ? Temavalg.Lyst
                 : TemaMoerkt.IsChecked == true ? Temavalg.Moerkt
                 : Temavalg.FoelgWindows;

        AppSettings.Current.Tema = valg;
        AppSettings.Current.Save();

        // Slaar igennem med det samme. Et udseende, man skal genstarte for at
        // se, er et udseende, man ikke tror paa.
        Temaskift.Anvend();
    }

    /// <summary>
    /// Holder de tre knapper i trit med det tema, der faktisk er i brug.
    /// </summary>
    /// <remarks>
    /// TEMAET KAN OGSAA SKIFTES ANDRE STEDER FRA: knappen i topbjaelken, og
    /// Windows selv ved solnedgang, hvis man foelger systemet. Uden det her
    /// ville skaermen sige «Lyst», mens appen var moerk — og det ville ligne,
    /// at valget ikke virkede.
    /// </remarks>
    private void LytPaaTema()
    {
        // Der meldes fra foerst, saa der ikke ligger to abonnementer, hvis
        // skaermen aabnes igen. Unloaded fyrer ikke paalideligt til at rydde
        // op alene - se MeetingView.
        Temaskift.Skiftet -= VisTema;
        Temaskift.Skiftet += VisTema;
        Unloaded -= Slip;
        Unloaded += Slip;
    }

    private void Slip(object? afsender, RoutedEventArgs e) => Temaskift.Skiftet -= VisTema;

    private void VisTema()
    {
        // Knapperne findes foerst, naar skaermen er bygget. Skiftet kan komme
        // foer det, hvis Windows skifter i samme oejeblik.
        if (TemaLyst is null) return;

        _saetterTema = true;

        var valg = AppSettings.Current.Tema;
        TemaLyst.IsChecked = valg == Temavalg.Lyst;
        TemaMoerkt.IsChecked = valg == Temavalg.Moerkt;
        TemaFoelg.IsChecked = valg == Temavalg.FoelgWindows;

        _saetterTema = false;
    }

    // ============================ DIKTERING ============================

    /// <summary>Et formaal, som det staar i listen.</summary>
    private sealed record Formaalsvalg(Dikteringsformaal Vaerdi, string Navn);

    private bool _dikteringIndlaest;

    /// <summary>
    /// Fylder dikteringsfanen ud fra det, der faktisk er gemt.
    /// </summary>
    /// <remarks>
    /// <c>_dikteringIndlaest</c> holder hændelserne ude, mens felterne sættes.
    /// Uden den ville hvert felt gemme sig selv under indlæsningen — og et
    /// valg, brugeren aldrig har truffet, ville blive skrevet ned som om han
    /// havde.
    /// </remarks>
    private void IndlaesDiktering()
    {
        _dikteringIndlaest = false;

        try
        {
            var v = AppSettings.Current;

            DikteringTil.IsChecked = v.DikteringTil;
            DikteringPuds.IsChecked = v.DikteringPuds;
            DikteringFagord.IsChecked = v.DikteringFagord;
            DikteringIndsaet.IsChecked = v.DikteringIndsaet;
            DikteringEfterProgram.IsChecked = v.DikteringEfterProgram;

            // Loftet: hvert minut fra det mindste til det stoerste. En fri
            // talindtastning ville give nul og bogstaver, og saa skal der
            // baade valideres og forklares.
            DikteringLoft.ItemsSource = Enumerable
                .Range(Holdvurdering.MindsteLoftMinutter,
                       Holdvurdering.StoersteLoftMinutter - Holdvurdering.MindsteLoftMinutter + 1)
                .ToList();

            DikteringLoft.SelectedItem =
                (int)Holdvurdering.LoftFra(v.DikteringLoftMinutter).TotalMinutes;

            DikteringFormaal.ItemsSource = new[]
            {
                new Formaalsvalg(Dikteringsformaal.Note, Sprog.T("settingsview.diktering_formaal_note")),
                new Formaalsvalg(Dikteringsformaal.Mail, Sprog.T("settingsview.diktering_formaal_mail")),
                new Formaalsvalg(Dikteringsformaal.Prompt, Sprog.T("settingsview.diktering_formaal_prompt")),
                new Formaalsvalg(Dikteringsformaal.Opgave, Sprog.T("settingsview.diktering_formaal_opgave")),
            };

            var valgt = Enum.TryParse<Dikteringsformaal>(v.DikteringFormaal, ignoreCase: true, out var f)
                ? f
                : Dikteringsformaal.Note;

            DikteringFormaal.SelectedItem = ((IEnumerable<Formaalsvalg>)DikteringFormaal.ItemsSource)
                .FirstOrDefault(x => x.Vaerdi == valgt);

            // Uden noegle kan der ikke dikteres. Det skal staa, FOER man slaar
            // noget til - ikke bagefter, naar man taler til et program, der
            // ikke kan svare.
            DikteringNoegle.Visibility = SkyNoegle.Hent() is null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _dikteringIndlaest = true;
        }
    }

    private void GemDiktering()
    {
        if (!_dikteringIndlaest) return;

        var v = AppSettings.Current;

        v.DikteringTil = DikteringTil.IsChecked == true;
        v.DikteringPuds = DikteringPuds.IsChecked == true;
        v.DikteringFagord = DikteringFagord.IsChecked == true;
        v.DikteringIndsaet = DikteringIndsaet.IsChecked == true;
        v.DikteringEfterProgram = DikteringEfterProgram.IsChecked == true;

        if (DikteringLoft.SelectedItem is int minutter) v.DikteringLoftMinutter = minutter;
        if (DikteringFormaal.SelectedItem is Formaalsvalg f) v.DikteringFormaal = f.Vaerdi.ToString();

        v.Save();

        // Genvejen skal vide det MED DET SAMME. Ellers skal appen genstartes,
        // foer et hold begynder at betyde noget - og saa tror man, det er
        // gaaet galt.
        Dikteringsskift?.Invoke(v.DikteringTil);
    }

    /// <summary>Siger til, når dikteringen bliver slået til eller fra.</summary>
    public static Action<bool>? Dikteringsskift { get; set; }

    private void DikteringTil_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringPuds_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringFagord_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringIndsaet_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringEfterProgram_Klik(object sender, RoutedEventArgs e) => GemDiktering();

    private void DikteringLoft_Valgt(object sender, SelectionChangedEventArgs e) => GemDiktering();

    private void DikteringFormaal_Valgt(object sender, SelectionChangedEventArgs e) => GemDiktering();

    /// <summary>
    /// Kører velkomstforløbet igen.
    /// </summary>
    /// <remarks>
    /// SetupCompleted røres IKKE. Forløbet vises her og nu; det skal ikke
    /// komme igen ved næste opstart, fordi nogen kiggede på det én gang.
    ///
    /// Bagefter læses skærmen ind på ny. Forløbet kan have ændret mikrofonen
    /// og hakkene, og to skærme, der viser hver sin sandhed om det samme, er
    /// værre end én, der er lidt for langsom.
    /// </remarks>
    private void Forloeb_Klik(object sender, RoutedEventArgs e)
    {
        var vindue = new Setup.SetupWindow(igen: true) { Owner = Window.GetWindow(this) };
        vindue.ShowDialog();

        // HER STOD «AppSettings.Reload()», OG DEN VAR DEN DIREKTE AARSAG TIL,
        // AT VALGTE ENHEDER FORSVANDT.
        //
        // Forloebet skriver i AppSettings.Current og gemmer selv. Der er intet
        // at laese ind igen. Reload byttede objektet ud, og alle de steder,
        // der holder fast i en reference - moedevagten fra appen starter til
        // den lukkes - sad tilbage med det gamle. Naeste gemning derfra skrev
        // brugerens nye valg vaek.
        //
        // Reload er nu ufarlig i sig selv (den fylder det samme objekt), men
        // kaldet hoerer stadig ikke til her. Fjernet 30-08-2026.
        Indlaes();
        VisAutostart();
        IndlaesDiktering();

        // Genvejen og vaageordet skal foelge med det samme. Ellers skal appen
        // genstartes, foer et hak, der lige er sat, betyder noget.
        Dikteringsskift?.Invoke(AppSettings.Current.DikteringTil);
        Diktering.KommandoerView.Aendret?.Invoke();

        Status.Text = "Opsætningen er gennemgået.";
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
            ? "HeyPia starter nu med Windows og ligger klar til genvejstasten."
            : "HeyPia starter ikke længere med Windows.";

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

                // DER STAAR OGSAA, AT DET SKER AF SIG SELV.
                //
                // Ellers ser linjen ud, som om hentningen KUN sker, naar man
                // trykker - og saa staar man og trykker for en sikkerheds
                // skyld, hver gang man er i tvivl.
                return $"Hentede {_o.SidsteAntal} " +
                       $"{(_o.SidsteAntal == 1 ? "aftale" : "aftaler")} " +
                       $"{t.LocalDateTime:d. MMMM 'kl.' HH:mm}. " +
                       "Der hentes af sig selv hvert kvarter, mens appen er åben.";
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
    // ------------------------------------------------- plads og lydfiler

    /// <summary>Ét valg i rullelisten over, hvor længe lyden bliver liggende.</summary>
    private sealed record Fristvalg(string Navn, int Dage);

    private bool _fylderFrist;

    /// <summary>
    /// Pladsen, som den ser ud lige nu — og hvad lyden fylder af den.
    ///
    /// TALLENE LÆSES FRA DISKEN HVER GANG. Et regnskab, der holdes ved lige,
    /// kommer ud af trit med virkeligheden, og det her tal er præcis dét, man
    /// åbner skærmen for at få at vide.
    /// </summary>
    private void VisPlads()
    {
        Pladsopgoerelse o;
        try { o = Lydoprydning.Opgoer(); }
        catch (Exception) { return; }

        PladsLyd.Text = Maal(o.LydBytes);
        PladsLydUnder.Text = o.LydFiler == 0
            ? "Ingen lydfiler endnu."
            : $"{o.LydFiler} filer · {o.LydAndelAfDisk:0.0} % af disken";

        PladsBrugt.Text = $"{o.BrugtGb - o.LydGb:0.0} GB";
        PladsBrugtUnder.Text = $"Windows, dine programmer og HeyPias tekst " +
                               $"({Maal(o.RestBytes)})";

        PladsFri.Text = $"{o.FritGb:0.0} GB";
        PladsFriUnder.Text = $"{o.FriAndel:0} % af {o.IAltGb:0} GB på {o.DrevNavn}";

        // Soejlen. Nul-bredde paa en stjerne er lovligt og betyder «ingenting».
        SoejleLyd.Width = new GridLength(Math.Max(0, o.LydBytes), GridUnitType.Star);
        SoejleAndet.Width = new GridLength(Math.Max(0, o.DrevIAlt - o.DrevFrit - o.LydBytes), GridUnitType.Star);
        SoejleFri.Width = new GridLength(Math.Max(1, o.DrevFrit), GridUnitType.Star);

        VisFrist();
        VisSpoergegraense();
    }

    private static string Maal(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / 1024.0 / 1024.0:0} MB";
        return $"{bytes / 1024.0:0} KB";
    }

    /// <summary>
    /// Fristvalget og hvad det ville rydde lige nu.
    ///
    /// DER STÅR, HVAD DET VILLE KOSTE — ikke bare hvad valget hedder. «Efter
    /// et år» siger ingenting; «rydder 3 optagelser og frigiver 1,2 GB» er et
    /// tal, man kan tage stilling til.
    /// </summary>
    private void VisFrist()
    {
        var valg = new[]
        {
            new Fristvalg("Ryd aldrig — behold lyden", 0),
            new Fristvalg("Efter 30 dage", 30),
            new Fristvalg("Efter 90 dage", 90),
            new Fristvalg("Efter 180 dage", 180),
            new Fristvalg("Efter 1 år (standard)", 365),
            new Fristvalg("Efter 2 år", 730)
        };

        var dage = AppSettings.Current.SletLydEfterDage;

        _fylderFrist = true;
        Lydfrist.ItemsSource = valg;
        Lydfrist.SelectedItem = valg.FirstOrDefault(v => v.Dage == dage) ?? valg[4];
        _fylderFrist = false;

        List<Lydkandidat> klar;
        try { klar = Lydoprydning.Kandidater(dage); }
        catch (Exception) { klar = new List<Lydkandidat>(); }

        var bytes = klar.Sum(k => k.Bytes);

        RydNuKnap.IsEnabled = klar.Count > 0;

        Lydfriststatus.Text = dage <= 0
            ? "Lyden bliver liggende, indtil du selv sletter den. Du kan altid rydde en enkelt optagelse fra Optagelser-skærmen."
            : klar.Count == 0
                ? $"Ingen optagelser er ældre end {dage} dage endnu. Der ryddes automatisk, når appen åbnes."
                : $"{klar.Count} optagelse(r) er klar til at blive ryddet — det ville frigive {Maal(bytes)}. Det sker af sig selv, næste gang appen åbnes.";
    }

    /// <summary>Hvornår et møde er langt nok til, at der spørges.</summary>
    private void VisSpoergegraense()
    {
        var valg = new[]
        {
            new Fristvalg("Spørg aldrig", 0),
            new Fristvalg("1 time", 60),
            new Fristvalg("2 timer (standard)", 120),
            new Fristvalg("3 timer", 180),
            new Fristvalg("4 timer", 240)
        };

        var m = AppSettings.Current.SpoergOmLydOverMinutter;

        _fylderFrist = true;
        Spoergegraense.ItemsSource = valg;
        Spoergegraense.SelectedItem = valg.FirstOrDefault(v => v.Dage == m) ?? valg[2];
        _fylderFrist = false;
    }

    private void Spoergegraense_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_fylderFrist || Spoergegraense.SelectedItem is not Fristvalg v) return;

        AppSettings.Current.SpoergOmLydOverMinutter = v.Dage;
        AppSettings.Current.Save();

        Status.Text = v.Dage <= 0
            ? "Der spørges ikke om lyden efter en udskrift."
            : $"Der spørges efter møder over {v.Dage} minutter.";
    }

    private void Lydfrist_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_fylderFrist || Lydfrist.SelectedItem is not Fristvalg v) return;

        AppSettings.Current.SletLydEfterDage = v.Dage;
        AppSettings.Current.Save();

        VisFrist();

        Status.Text = v.Dage <= 0
            ? "Lydfiler bliver liggende."
            : $"Lydfiler ryddes {v.Dage} dage efter, optagelsen er skrevet ud.";
    }

    /// <summary>
    /// Rydder nu, i stedet for at vente til næste opstart.
    ///
    /// DER SPØRGES, OG DER STÅR HVOR MEGET. Sletningen kan ikke fortrydes, og
    /// et tal er forskellen på et valg og et klik.
    /// </summary>
    private void RydLyd_Klik(object sender, RoutedEventArgs e)
    {
        var dage = AppSettings.Current.SletLydEfterDage;

        List<Lydkandidat> klar;
        try { klar = Lydoprydning.Kandidater(dage); }
        catch (Exception) { return; }

        if (klar.Count == 0) return;

        var bytes = klar.Sum(k => k.Bytes);

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Ryd lyden på {klar.Count} optagelse(r)?",
            $"Det frigiver {Maal(bytes)}.\n\n" +
            "Teksten, dine noter, opgaverne og dokumenterne bliver liggende. " +
            "Det, du ikke kan bagefter, er at skrive optagelsen ud igen, køre " +
            "talergenkendelsen om eller høre efter, om maskinen hørte rigtigt.\n\n" +
            "Det kan ikke fortrydes.",
            godkend: "Ryd lyden", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        var (filer, frigivet) = Lydoprydning.Ryd(dage);

        Status.Text = $"{filer} lydfil(er) ryddet — {Maal(frigivet)} frigivet.";

        VisPlads();
        OpdaterFiler();
    }

    // ------------------------------------------------- krav til maskinen

    /// <summary>Ét krav, som listen viser det — med farven på den her maskines svar.</summary>
    public sealed record Kravvisning(string Hvad, string Har, string Hvorfor, Brush Kant);

    /// <summary>
    /// Kravene, målt mod maskinen appen kører på.
    ///
    /// GULT OG IKKE RØDT, når noget mangler. Appen kan optage og skrive ud på
    /// næsten hvad som helst; forskellen er, hvor længe man venter. Et rødt
    /// kryds ville sige, at noget ikke virker.
    /// </summary>
    // ------------------------------------------------------ hvad den bruger

    /// <summary>Én linje i forbrugslisten.</summary>
    private sealed record Forbrugsvisning(
        string Navn, string Hvad, string Cpu, string Hukommelse, string Kant);

    private bool _maaler;

    /// <summary>
    /// Måler appen og dens hjælpeprogrammer og viser det.
    /// </summary>
    /// <remarks>
    /// DER MÅLES, NÅR DER TRYKKES — ikke hele tiden.
    ///
    /// En måling koster selv noget, og en skærm, der opdaterer sig hvert
    /// sekund, ville stå og bruge det, den er sat til at vise. To sekunders
    /// vindue er nok til et tal, der ligner Joblistes.
    /// </remarks>
    private async void Forbrug_Klik(object sender, RoutedEventArgs e)
    {
        if (_maaler) return;
        _maaler = true;

        ForbrugKnap.IsEnabled = false;
        ForbrugSum.Text = Sprog.T("settingsview.forbrug_maaler");

        try
        {
            var poster = await Ressourcer.MaalAsync(TimeSpan.FromSeconds(2));

            Forbrugsliste.ItemsSource = poster.Select(p => new Forbrugsvisning(
                p.ErOs ? "HeyPia" : p.Navn,
                p.ErOs
                    ? $"pid {p.Pid}"
                    : $"pid {p.Pid} · hjælpeprogram · kørt i {Varighed(p.Alder)}",
                $"{p.Procent:0} %",
                $"{p.Megabyte:N0} MB",
                Farve(p))).ToList();

            var cpu = poster.Sum(p => p.Procent);
            var mb = poster.Sum(p => p.Megabyte);
            var kerner = Environment.ProcessorCount;

            ForbrugSum.Text =
                $"I alt {cpu:0} % af én kerne ({kerner} kerner i maskinen) og {mb:N0} MB "
                + $"fordelt på {poster.Count} program(mer).";
        }
        catch (Exception ex)
        {
            ForbrugSum.Text = $"Kunne ikke måle: {ex.Message}";
        }
        finally
        {
            ForbrugKnap.IsEnabled = true;
            _maaler = false;
        }
    }

    /// <summary>
    /// Farven på linjen. Rød, når ét program tager mere end en hel kerne —
    /// det er dér, man mærker det.
    /// </summary>
    private static string Farve(Forbrugspost p) =>
        p.Procent >= 100 || p.Megabyte >= 1500 ? "#E5484D"
        : p.Procent >= 25 || p.Megabyte >= 500 ? "#F5A524"
        : "#4CBE72";

    private static string Varighed(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{t.TotalHours:0.#} timer"
        : t.TotalMinutes >= 1 ? $"{t.TotalMinutes:0} min"
        : $"{t.TotalSeconds:0} sek";

    private void VisKrav()
    {
        List<Krav> krav;
        try { krav = Maskinkrav.Alle(); }
        catch (Exception) { krav = new List<Krav>(); }

        Kravliste.ItemsSource = krav.Select(k => new Kravvisning(
            k.Hvad,
            k.Har,
            k.Hvorfor,
            (Brush)new BrushConverter().ConvertFrom(k.Slags switch
            {
                Kravsvar.Opfyldt => "#FF4CBE72",
                Kravsvar.Mangler => "#FFE8A33D",
                _ => "#FF3A4150"
            })!)).ToList();
    }

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
        Optagenote.Text = Googlekalender.Note("da");
        OptagenoteEn.Text = Googlekalender.Note("en");
        _fylderNote = false;

        var egne = (AppSettings.Current.Optagenote.Length > 0 ? 1 : 0)
                 + (AppSettings.Current.OptagenoteEn.Length > 0 ? 1 : 0);

        Notestatus.Text = egne switch
        {
            2 => "Begge tekster er dine egne.",
            1 => "Den ene tekst er din egen.",
            _ => "Standardteksterne."
        };
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
        // ER DEN LIG STANDARDEN, GEMMES DER INGENTING. Så følger noten med,
        // den dag standarden bliver bedre — i stedet for at stå fast på den
        // ordlyd, der tilfældigvis var i feltet den dag, man trykkede gem.
        var dansk = Optagenote.Text.Trim();
        var engelsk = OptagenoteEn.Text.Trim();

        AppSettings.Current.Optagenote =
            dansk == Googlekalender.StandardOptagenote.Trim() ? "" : dansk;

        AppSettings.Current.OptagenoteEn =
            engelsk == Googlekalender.StandardOptagenoteEn.Trim() ? "" : engelsk;

        AppSettings.Current.Save();

        VisNote();
        Notestatus.Text = "Gemt.";
    }

    private void NulstilNote_Klik(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.Optagenote = "";
        AppSettings.Current.OptagenoteEn = "";
        AppSettings.Current.Save();

        VisNote();
        Notestatus.Text = "Nulstillet til standardteksterne.";
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
    /// <summary>
    /// Hvad man siger ja til, når opgaverne forbindes.
    ///
    /// EN EGEN TEKST, FORDI DET ER EN ANDEN AFTALE END KALENDEREN. Kalenderen
    /// læser noget, der allerede lå hos Google. Her går det begge veje, og det
    /// skal stå i første afsnit — ikke i en fodnote.
    /// </summary>
    private const string Opgavevejledning =
        "Der åbner en side hos Google i din browser. Log ind og godkend.\n\n" +
        "DETTE FÅR APPEN LOV TIL\n" +
        "Ét område: dine opgaver i Google Tasks. Appen kan læse dem og krydse " +
        "dem af. Den kan ikke se din kalender, din mail, dine filer eller " +
        "noget andet i kontoen.\n\n" +
        "DET GÅR BEGGE VEJE\n" +
        "Opgaver, du skriver i Google, dukker op i Cockpittet. Krydser du en " +
        "af dem af her, bliver den også krydset af hos Google. Det er hele " +
        "meningen: ét sted at holde øje med, ikke to.\n\n" +
        "DETTE SENDES ALDRIG\n" +
        "Opgaver, appen selv har fundet i et møde, lægges IKKE op. De kommer " +
        "fra en transkription, og et referats indhold skal ikke ende i skyen, " +
        "fordi man slog opgaver til. Lyd, transkriptioner, noter og dokumenter " +
        "sendes heller aldrig.\n\n" +
        "Det er en egen godkendelse, adskilt fra kalenderen. Du kan afbryde " +
        "den igen når som helst, og så forsvinder de hentede opgaver.";

    private async void Forbind_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string id) return;
        if (Integrationer.Find(id) is not { } i) return;

        var o = Integrationsfiler.Hent(id);

        var erOpgaver = id == Googleopgaver.Id;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Forbind til {i.Navn}?",
            erOpgaver ? Opgavevejledning : Googlekalender.Vejledning,
            godkend: "Åbn browseren", annuller: "Ikke nu", slags: Dialogs.Slags.Valg);

        if (!ja) return;

        Status.Text = $"Venter på godkendelse i browseren …";

        try
        {
            // EGET OMRAADE, EGEN NOEGLE. Den, der kun vil dele sin kalender,
            // skal ikke se «og dine opgaver» paa Googles skaerm.
            var noegle = await Googlekalender.ForbindAsync(
                erOpgaver ? Googleopgaver.Omraade : "");

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
    /// Henter aftaler eller opgaver ind.
    ///
    /// SELVE HENTNINGEN LIGGER I Core.Synkronisering, fordi Cockpittet gør
    /// præcis det samme, når man trykker på synkroniseringsikonet dér. To
    /// steder, der hentede hver for sig, ville før eller siden bogføre
    /// «sidst hentet» forskelligt — og så kan man ikke stole på nogen af dem.
    /// </summary>
    private async Task Hent(string id)
    {
        if (Integrationer.Find(id) is not { } i) return;
        if (!Integrationsfiler.Hent(id).ErForbundet) return;

        var opgaver = id == Googleopgaver.Id;

        Status.Text = opgaver
            ? $"Henter opgaver fra {i.Navn} …"
            : $"Henter aftaler fra {i.Navn} …";

        var svar = opgaver
            ? await Synkronisering.Opgaverne()
            : await Synkronisering.Kalenderen();

        Status.Text = svar.Lykkedes
            ? $"{svar.Besked} fra {i.Navn}."
            : svar.Besked;

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
        // ============ SKÆRMEN MÅ IKKE VÆLGE FOR BRUGEREN ============
        //
        // HER FORSVANDT DET VALGTE HEADSET. At saette SelectedItem udloeser
        // SelectionChanged, praecis som var det brugeren, der klikkede - og
        // handleren gemmer valget. Var headsettet ikke tilsluttet i det
        // sekund, blev Windows' standard gemt OVEN I brugerens eget valg.
        //
        // Saa skulle man vaelge sit headset igen. Og bare det at aabne
        // fanen, mens det var vaek, gjorde det igen. Det stod paa i dagevis
        // og blev opdaget 30-08-2026, da en diktering fejlede paa en
        // mikrofon, brugeren aldrig havde valgt.
        //
        // Temaet og fristen havde laenge hver sin vagt. Lyden havde ingen.
        _fylderLyd = true;
        try
        {
            Mikrofoner.ItemsSource = mikrofoner;
            Hoejttalere.ItemsSource = hoejttalere;

            var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var mikFallback);
            var hoejt = AudioDevices.ResolveSpeaker(AppSettings.Current.SpeakerId, out var hoejtFallback);

            Mikrofoner.SelectedItem = mikrofoner.FirstOrDefault(d => d.Id == mik?.Id);
            Hoejttalere.SelectedItem = hoejttalere.FirstOrDefault(d => d.Id == hoejt?.Id);

            // Er den valgte enhed vaek — headsettet er taget ud — skal det staa
            // her, ikke opdages naar optagelsen er slut.
            //
            // VALGET BLIVER STAAENDE. Enheden er vaek lige nu; den er ikke
            // fravalgt. Naar headsettet kommer i igen, skal det bare virke.
            Vis(MikAdvarsel, mikFallback,
                "Den mikrofon, du har valgt, er ikke tilsluttet lige nu. Appen bruger Windows' "
                + "standard imens — dit valg bliver stående og gælder igen, så snart den er tilbage.");
            Vis(HoejtAdvarsel, hoejtFallback,
                "Den højttaler, du har valgt, er ikke tilsluttet lige nu. Appen bruger Windows' "
                + "standard imens — dit valg bliver stående og gælder igen, så snart den er tilbage.");

            if (mikrofoner.Count == 0) MikStatus.Text = "ingen mikrofon fundet";
            if (hoejttalere.Count == 0) HoejtStatus.Text = "ingen afspilningsenhed fundet";
        }
        finally { _fylderLyd = false; }
    }

    /// <summary>Sat, mens lydlisterne fyldes — så et valg ikke gemmes af sig selv.</summary>
    private bool _fylderLyd;

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
        if (_fylderLyd) return;
        if (Mikrofoner.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.MicrophoneId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Mikrofon: {d.FriendlyName}";

        Vis(MikAdvarsel, false, "");
    }

    private void Hoejttaler_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_fylderLyd) return;
        if (Hoejttalere.SelectedItem is not DeviceInfo d) return;

        AppSettings.Current.SpeakerId = d.Id;
        AppSettings.Current.Save();
        Status.Text = $"Højttaler: {d.FriendlyName}";

        Vis(HoejtAdvarsel, false, "");
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

        _testMappe = Path.Combine(Path.GetTempPath(), "heypia-miktest-" + Guid.NewGuid().ToString("N")[..8]);
        _testKlip = new ShortClipRecorder(Path.Combine(_testMappe, "proeve.wav"));

        try
        {
            // Den enhed, der FAKTISK findes - ikke et id, der maaske er vaek.
            var enhed = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out _);
            if (enhed is null)
            {
                _testKlip.Dispose();
                _testKlip = null;
                TestStatus.Text = NoteApp.Core.Sprog.T("diktering.ingen_mikrofon");
                return;
            }

            _testKlip.Start(enhed.Id);
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

        try
        {
            var install = WhisperInstall.Locate(AppSettings.Current.PreferredModel);
            var motor = new Transcriber(install.WhisperCli!);

            // ============ HER STOD «DET TAGER TYPISK ET HALVT MINUT» ============
            //
            // Det passede ikke, og det var et gæt uden noget bag sig. Testen
            // skriver ud med den SAMME model som møderne — på denne maskine
            // 2,9 GB — og et halvt minuts tale er stadig et halvt minut, den
            // skal igennem. Det tager længere end oplæsningen selv, og
            // maskinen kan mærkes imens.
            //
            // Modellen er med vilje den samme. Målte vi med en mindre, målte
            // vi modellen og ikke mikrofonen, og tallet ville ikke sige noget
            // om, hvad der sker under et rigtigt møde.
            //
            // Så siges det i stedet, hvad der foregår — og fremdriften vises.
            // En linje, der står stille, ligner en app, der er gået i stå.
            var navn = install.ModelFileName ?? "sprogmodellen";
            TestStatus.Text =
                $"Skriver ud med {navn} — den samme model som dine møder. "
                + "Det tager typisk længere end oplæsningen selv, og maskinen "
                + "kan mærkes imens.";

            var fremdrift = new Progress<TranscriptionProgress>(p =>
            {
                if (p.Percent > 0) TestUr.Text = $"{p.Percent:0} %";
            });

            // Sproget er LAAST til dansk her, modsat moeder hvor det er
            // "auto". Vi ved, hvad der blev sagt; et fejlgaettet sprog ville
            // maale Whispers sproggenkendelse i stedet for mikrofonen.
            var r = await motor.RunAsync(
                new TranscriptionRequest(klip.Path, install.ModelPath!,
                                         Path.Combine(mappe!, "proeve"), "da"),
                fremdrift, CancellationToken.None);

            TestUr.Text = "";
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

    // ==================== OVERVAAGEDE MAPPER ====================
    //
    // Roadmap 2.2. Hele mekanikken ligger i NoteApp.Core.Overvaagning; her er
    // kun skaermen. Metoden er skrevet ned i doc/overvaagede-mapper.md.

    /// <summary>
    /// Én række i listen. Skriver sig selv tilbage, når hakket ændres, så
    /// «læg ind automatisk» virker uden en gem-knap — der er ingen andre
    /// steder på skærmen, hvor man skal gemme noget.
    /// </summary>
    public sealed class Mappevisning
    {
        public required string Sti { get; init; }
        public required string Linje { get; init; }

        private bool _automatisk;

        public bool Automatisk
        {
            get => _automatisk;
            set
            {
                if (_automatisk == value) return;
                _automatisk = value;

                var alle = Overvaagning.Mapper();
                var min = alle.FirstOrDefault(m =>
                    string.Equals(m.Sti, Sti, StringComparison.OrdinalIgnoreCase));

                if (min is null) return;

                min.Automatisk = value;
                Overvaagning.Gem(alle);
            }
        }

        public static Mappevisning Af(Overvaagetmappe m)
        {
            var dele = new List<string>();

            if (m.Herkomst.Length > 0) dele.Add(m.Herkomst);
            dele.Add(m.Undermapper ? "med undermapper" : "kun mappen selv");

            if (!Directory.Exists(m.Sti)) dele.Add("MAPPEN FINDES IKKE LÆNGERE");

            return new Mappevisning
            {
                Sti = m.Sti,
                Linje = string.Join("  ·  ", dele),
                _automatisk = m.Automatisk
            };
        }
    }

    /// <summary>En skytjeneste på maskinen, og om HeyPia-mappen findes i den.</summary>
    private sealed record Skyvisning(string Navn, string Sti, string Knap, bool Kan);

    private void VisOvervaagede()
    {
        var mapper = Overvaagning.Mapper();

        Overvaagede.ItemsSource = mapper.Select(Mappevisning.Af).ToList();
        IngenOvervaagede.Visibility = mapper.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // SKYTJENESTERNE. De staar altid, ogsaa naar mappen allerede
        // overvaages - saa kan man se, at appen HAR fundet dem, i stedet for
        // at sidde og lede efter en knap, der ikke er der.
        var vaagne = mapper.Select(m => m.Sti).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Skytjenester.ItemsSource = Overvaagning.Synkroniseringsroedder()
            .Select(r =>
            {
                var mappe = Path.Combine(r.Rod, Overvaagning.Standardmappe);

                if (vaagne.Contains(mappe))
                    return new Skyvisning(r.Navn, mappe, "Overvåges", false);

                return Directory.Exists(mappe)
                    ? new Skyvisning(r.Navn, mappe, "Overvåg den", true)
                    : new Skyvisning(r.Navn, r.Rod, "Opret HeyPia-mappen", true);
            })
            .ToList();

        var set = Overvaagning.Husket();

        Bogen.Text = set == 0
            ? "Der er ikke taget stilling til nogen filer endnu."
            : set == 1
                ? "Der er taget stilling til én fil. Den bliver ikke tilbudt igen."
                : $"Der er taget stilling til {set} filer. De bliver ikke tilbudt igen.";
    }

    private void TilfoejMappe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Vælg mappen, der skal holdes øje med",
            Multiselect = false
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        Tilfoej(dialog.FolderName, "Tilføjet af dig");
    }

    private void OpretSkymappe_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sti }) return;

        // Knappen staar med to betydninger, og Tag afgoer hvilken: findes
        // HeyPia-mappen, er stien mappen selv; findes den ikke, er stien
        // skytjenestens rod, og saa skal mappen oprettes foerst.
        var navn = Path.GetFileName(sti.TrimEnd('\\'));

        var mappe = string.Equals(navn, Overvaagning.Standardmappe, StringComparison.OrdinalIgnoreCase)
            ? sti
            : Path.Combine(sti, Overvaagning.Standardmappe);

        try
        {
            Directory.CreateDirectory(mappe);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mappen kunne ikke oprettes",
                ex.Message, Dialogs.Slags.Pas_paa);
            return;
        }

        Tilfoej(mappe, "Skytjeneste");
    }

    private void Tilfoej(string sti, string herkomst)
    {
        var alle = Overvaagning.Mapper();

        if (alle.Any(m => string.Equals(m.Sti, sti, StringComparison.OrdinalIgnoreCase)))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den holdes der allerede øje med",
                $"«{sti}» står allerede på listen.", Dialogs.Slags.Valg);
            return;
        }

        // DATAMAPPEN MAA IKKE OVERVAAGES.
        //
        // Appens egne optagelser ligger der som mikrofon.wav. Blev mappen
        // overvaaget, ville hver eneste optagelse blive tilbudt som en "ny
        // fil udefra" og kunne laegges ind som en kopi af sig selv.
        if (Ligger(sti, UserDataPaths.Root))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den mappe kan ikke overvåges",
                "Det er appens egen datamappe. Alle optagelser ligger der i forvejen, "
                + "og de ville blive tilbudt som nye filer.", Dialogs.Slags.Pas_paa);
            return;
        }

        alle.Add(new Overvaagetmappe { Sti = sti, Herkomst = herkomst, Aktiv = true });
        Overvaagning.Gem(alle);

        VisOvervaagede();
    }

    /// <summary>Ligger stien i eller er den lig med mappen?</summary>
    private static bool Ligger(string sti, string mappe)
    {
        try
        {
            var a = Path.GetFullPath(sti).TrimEnd('\\') + "\\";
            var b = Path.GetFullPath(mappe).TrimEnd('\\') + "\\";

            return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
                || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    private void FjernMappe_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sti }) return;

        var alle = Overvaagning.Mapper();
        alle.RemoveAll(m => string.Equals(m.Sti, sti, StringComparison.OrdinalIgnoreCase));

        Overvaagning.Gem(alle);
        VisOvervaagede();
    }

    private void GlemSete_Click(object sender, RoutedEventArgs e)
    {
        var svar = Dialogs.AppDialog.Spoerg(Window.GetWindow(this), "Tilbyd alle filer igen?",
            "Hver lydfil i de overvågede mapper bliver tilbudt igen — også dem, du "
            + "har sagt nej til. Optagelser, der allerede er lagt ind, bliver ikke rørt, "
            + "men filerne bag dem kan lægges ind endnu en gang.",
            "Glem hvad der er set", slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!svar) return;

        Overvaagning.GlemAlt();
        VisOvervaagede();
    }

    private void SkiftData_Click(object sender, RoutedEventArgs e)
    {
        var valgt = VaelgMappe("Vælg hvor HeyPias filer skal ligge", UserDataPaths.Root);
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

