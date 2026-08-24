using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>Ét sted, ordet står — som listen viser det.</summary>
public sealed class Traefvisning
{
    public Traefvisning(Fund f, Traef t, int nummer, string soegeord)
    {
        Fund = f;
        Traef = t;
        Nummer = $"{nummer}.";
        Uddrag = t.Uddrag;
        Soegeord = soegeord;
    }

    public Fund Fund { get; }
    public Traef Traef { get; }
    public string Nummer { get; }
    public string Uddrag { get; }

    /// <summary>Det, der blev søgt på — så det kan markeres i uddraget.</summary>
    public string Soegeord { get; }

    /// <summary>
    /// Uddraget skåret op, så det søgte kan markeres med gult.
    ///
    /// SAMME MARKERING SOM I UDSKRIFTEN.
    ///
    /// Her stod uddraget som ren tekst, mens søgningen inde i en udskrift
    /// markerede med gult. To steder, der gør det samme, skal se ens ud —
    /// ellers skal man lære skærmene hver for sig, og man leder efter det gule
    /// på en skærm, hvor det ikke findes.
    ///
    /// Der skæres på HVERT ord i søgningen. Søger man «Omada pipeline», er det
    /// begge ord, man leder efter i teksten.
    /// </summary>
    public IEnumerable<(string Tekst, bool Gul)> Dele()
    {
        var ord = Soegeord.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(o => o.Length > 1)
                          .OrderByDescending(o => o.Length)
                          .ToList();

        if (ord.Count == 0) { yield return (Uddrag, false); yield break; }

        var i = 0;

        while (i < Uddrag.Length)
        {
            var bedst = -1;
            var laengde = 0;

            foreach (var o in ord)
            {
                var j = Uddrag.IndexOf(o, i, StringComparison.OrdinalIgnoreCase);
                if (j < 0) continue;
                if (bedst < 0 || j < bedst) { bedst = j; laengde = o.Length; }
            }

            if (bedst < 0) { yield return (Uddrag[i..], false); yield break; }

            if (bedst > i) yield return (Uddrag[i..bedst], false);

            yield return (Uddrag.Substring(bedst, laengde), true);
            i = bedst + laengde;
        }
    }
}

/// <summary>Én kilde med alle sine steder.</summary>
public sealed class Fundvisning
{
    public Fundvisning(Fund f, string soegeord)
    {
        Fund = f;
        Overskrift = f.Overskrift;

        (Maerkat, Maerkatfarve) = f.Slags switch
        {
            Fundtype.Udskrift => ("TRANSKRIPTION", new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0))),
            Fundtype.Note => ("DIN NOTE", new SolidColorBrush(Color.FromRgb(0x4C, 0xBE, 0x72))),
            _ => ("DOKUMENT", new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0)))
        };

        Hoejre = f.Traef.Count == 1
            ? $"{f.Tid:dd-MM-yyyy}  ·  1 sted"
            : $"{f.Tid:dd-MM-yyyy}  ·  {f.Traef.Count} steder";

        Steder = f.Traef.Select((t, n) => new Traefvisning(f, t, n + 1, soegeord)).ToList();
    }

    public Fund Fund { get; }
    public string Overskrift { get; }
    public string Maerkat { get; }
    public Brush Maerkatfarve { get; }
    public string Hoejre { get; }
    public IReadOnlyList<Traefvisning> Steder { get; }
}

/// <summary>
/// Søgning på tværs af alle møder og dokumenter.
///
/// HVORFOR HVERT STED STÅR FOR SIG
///
/// Første udgave viste ét fund pr. fil og åbnede filen ved klik. Det hjalp
/// ikke: står ordet tolv steder i en udskrift på en time, er spørgsmålet ikke
/// OM det står der, men i hvilken sammenhæng — og hvilket af de tolv steder
/// man skal læse.
///
/// Nu står hvert sted som sin egen linje med teksten omkring, og et klik
/// springer hen til netop dét sted i teksten.
///
/// HVORFOR DER SØGES, MENS MAN SKRIVER
///
/// En søgeknap gør et opslag til noget, man overvejer. Uden knap er det noget,
/// man bare gør — og det er hele forskellen på, om arkivet bliver brugt.
/// </summary>
public partial class SearchView : UserControl
{
    private readonly DispatcherTimer _pause;
    private CancellationTokenSource? _afbryd;

    public SearchView() : this(null) { }

    /// <param name="start">
    /// Et ord, der skal søges på med det samme. Sat, når man kommer tilbage
    /// fra et sted, man klikkede sig hen til — så listen står, som den gjorde,
    /// og man kan gå videre til det næste sted.
    /// </param>
    public SearchView(string? start)
    {
        InitializeComponent();

        // DER VENTES ET OEJEBLIK, FOER DER SOEGES.
        //
        // Uden pausen startes en ny gennemloebning af alle filer for hvert
        // eneste tastetryk. Ved "Cloudworks" er det ti soegninger, hvor de ni
        // er smidt vaek, foer de blev faerdige.
        //
        // 220 ms er kortere end pausen mellem to ord og laengere end mellem
        // to bogstaver.
        _pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _pause.Tick += (_, _) => { _pause.Stop(); Soeg(); };

        Loaded += (_, _) =>
        {
            HentSpaltebredder();
            FyldFiltre();
            VisOpgaver();
            VisKalender();

            if (start is { Length: > 0 })
            {
                // Teksten saettes FOER fokus. Ellers markerer TextBox'en det
                // hele, og det naeste tastetryk sletter soegningen.
                Felt.Text = start;
                Felt.CaretIndex = Felt.Text.Length;
            }

            Felt.Focus();
        };
    }

    // ------------------------------------------------------- spaltebredderne

    /// <summary>
    /// Sætter spalterne, som de stod sidst.
    ///
    /// Nul betyder «aldrig rørt», og så bliver standardbredden fra XAML'en
    /// stående. Det er ikke det samme som en spalte, nogen har trukket helt
    /// sammen — den har sin mindstebredde og er derfor aldrig nul.
    ///
    /// Der gemmes IKKE en bredde for midterspalten. Den er «*» og tager, hvad
    /// der bliver til overs; gemte man den, ville skærmen se forkert ud, den
    /// dag appen åbnes på en anden opløsning.
    /// </summary>
    private void HentSpaltebredder()
    {
        try
        {
            var v = AppSettings.Current.CockpitVenstre;
            var h = AppSettings.Current.CockpitHoejre;

            if (v > 0) Venstrespalte.Width = new GridLength(v);
            if (h > 0) Hoejrespalte.Width = new GridLength(h);
        }
        catch (Exception)
        {
            // Kan indstillingerne ikke laeses, staar standardbredderne. En
            // spaltebredde er ikke noget at vaelte en skaerm for.
        }
    }

    /// <summary>
    /// Gemmer bredden, når trækket slippes — ikke undervejs.
    ///
    /// Undervejs ville filen blive skrevet mange gange i sekundet for et tal,
    /// der først betyder noget, når musen slippes.
    /// </summary>
    private void Spalte_Trukket(object sender,
                                System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        try
        {
            AppSettings.Current.CockpitVenstre = Venstrespalte.ActualWidth;
            AppSettings.Current.CockpitHoejre = Hoejrespalte.ActualWidth;
            AppSettings.Current.Save();
        }
        catch (Exception)
        {
            // Kan den ikke gemmes, staar bredden alligevel resten af
            // sessionen. Det er den mindst irriterende maade at fejle paa.
        }
    }

    private void Felt_Aendret(object sender, TextChangedEventArgs e)
    {
        var tomt = Felt.Text.Length == 0;

        Pladsholder.Visibility = tomt ? Visibility.Visible : Visibility.Collapsed;
        Ryd.Visibility = tomt ? Visibility.Collapsed : Visibility.Visible;

        _pause.Stop();
        _pause.Start();
    }

    /// <summary>
    /// Escape rydder søgningen — samme tast som i udskriftens søgefelt.
    ///
    /// To søgefelter i den samme app skal opføre sig ens. Ellers skal man
    /// lære dem hver for sig, og så trykker man Escape det ene sted og
    /// opdager, at feltet stadig står med tekst.
    /// </summary>
    private void Felt_Tast(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || Felt.Text.Length == 0) return;

        Ryd_Klik(sender, e);
        e.Handled = true;
    }

    private void Ryd_Klik(object sender, RoutedEventArgs e)
    {
        Felt.Clear();

        // Fokus tilbage i feltet. Man rydder for at skrive noget andet, ikke
        // for at holde op med at soege.
        Felt.Focus();
    }

    // -------------------------------------------------------------- kalenderen

    /// <summary>Én aftale, som Cockpittet viser den.</summary>
    public sealed class Aftalevisning
    {
        private readonly Aftale _a;
        private readonly DateTimeOffset _nu;

        public Aftalevisning(Aftale a, DateTimeOffset nu)
        {
            _a = a;
            _nu = nu;
        }

        public Aftale Aftale => _a;

        public string Titel => _a.Titel;

        /// <summary>
        /// Dagsoverskriften — «I DAG», «I MORGEN» eller «TORSDAG».
        ///
        /// DEN STÅR OVER DAGENS FØRSTE AFTALE OG IKKE PÅ HVER ENKELT.
        ///
        /// Datoen stod før inde i hver aftale, klemt sammen med klokkeslæt,
        /// sted og kilde: «25-08 · 14:00 · Microsoft Teams-møde · Google».
        /// Fire oplysninger på én linje i en smal spalte betyder, at ingen af
        /// dem kan læses — og datoen er dén, man leder efter først.
        ///
        /// Med en overskrift pr. dag står datoen ét sted, stort nok til at
        /// blive set, og aftalerne under den behøver kun deres klokkeslæt.
        /// </summary>
        public string Dagsnavn { get; set; } = "";

        /// <summary>Datoen under dagsnavnet — «24. august».</summary>
        public string Dagsdato { get; set; } = "";

        /// <summary>Er det her dagens første aftale?</summary>
        public Visibility Dagsvis =>
            Dagsnavn.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>I dag skal skille sig ud — det er den dag, man kan nå noget på.</summary>
        public Brush Dagsfarve =>
            _a.Start.Date == _nu.Date ? Pensel("#FFE8A33D") : Pensel("#FF9BA6B8");

        /// <summary>
        /// Klokkeslættet — eller «nu», når den er i gang.
        ///
        /// Kun tidspunktet. Dagen står i overskriften ovenover, og at gentage
        /// den på hver aftale ville tage den plads, titlen har brug for.
        /// </summary>
        public string Klokken =>
            _a.ErIGang(_nu) ? "nu" : _a.Start.LocalDateTime.ToString("HH:mm");

        /// <summary>Hvor den kommer fra, og hvad der ellers er værd at vide.</summary>
        public string Under
        {
            get
            {
                var dele = new List<string>();

                if (_a.Sted.Length > 0) dele.Add(_a.Sted);
                else if (_a.Link.Length > 0) dele.Add("online");

                if (_a.Kilde != Kalenderkilde.Lokal) dele.Add(_a.Kilde.ToString());

                if (_a.MoedeId.Length > 0) dele.Add("optaget");

                // DET SKAL KUNNE SES PAA LISTEN. En aftale, der starter en
                // optagelse af sig selv, maa ikke goere det som en
                // overraskelse - og markeringen er sat i et vindue, man
                // lukkede for en uge siden.
                else if (_a.OptagAutomatisk) dele.Add("optager selv");

                return string.Join("  ·  ", dele);
            }
        }

        /// <summary>
        /// Farven siger, hvor tæt den er på.
        ///
        /// Rød er i gang lige nu — det er dét, der skal handles på. Gul er
        /// inden for en time. Resten er grå: en aftale i overmorgen skal ikke
        /// råbe op.
        /// </summary>
        public Brush Kant
        {
            get
            {
                if (_a.MoedeId.Length > 0) return Pensel("#FF3DA55A");
                if (_a.ErIGang(_nu)) return Pensel("#FFE5484D");

                return _a.Start <= _nu.AddHours(1)
                    ? Pensel("#FFE8A33D")
                    : Pensel("#FF3A4150");
            }
        }

        private static Brush Pensel(string hex) => (Brush)new BrushConverter().ConvertFrom(hex)!;

        public string Knap => _a.MoedeId.Length > 0 ? "Vis" : "Optag";

        public string Knaptip => _a.MoedeId.Length > 0
            ? "Gå til optagelsen af det her møde"
            : "Start optagelsen med aftalens mødetype, mappe og sprog";
    }

    /// <summary>
    /// Kalenderen i Cockpittet: det, der skal ske.
    ///
    /// Aftalerne står i ÉN liste, uanset om de er oprettet her eller hentet
    /// fra Google. Hvem der har lagt dem ind, er ikke det, man leder efter,
    /// når man skal optage om fem minutter — det står med småt under titlen.
    /// </summary>
    /// <summary>
    /// Dansk, uanset hvad Windows står på.
    ///
    /// Ugedage og måneder skal hedde det samme for alle, der bruger appen.
    /// Kører Windows på engelsk, ville kalenderen ellers sige «Thursday» midt
    /// i en dansk skærm.
    /// </summary>
    private static readonly System.Globalization.CultureInfo Dansk =
        System.Globalization.CultureInfo.GetCultureInfo("da-DK");

    private void VisKalender()
    {
        var nu = DateTimeOffset.Now;

        List<Aftale> kommende;
        try { kommende = Kalender.Kommende(nu); }
        catch (Exception) { kommende = new List<Aftale>(); }

        // DAGSOVERSKRIFTEN SAETTES PAA DAGENS FOERSTE AFTALE.
        //
        // Alternativet var to slags rækker i den samme liste - en overskrift
        // og en aftale - og det kræver en skabelonvælger og en type, der ikke
        // er en aftale. Her bærer aftalen selv sin overskrift, og skabelonen
        // skjuler den bare på de øvrige.
        //
        // Listen kommer sorteret fra Kalender.Kommende, saa «foerste paa
        // dagen» er den, hvis dato er en anden end den forriges.
        var visninger = kommende.Select(a => new Aftalevisning(a, nu)).ToList();

        var forrige = DateTime.MinValue;

        foreach (var v in visninger)
        {
            var dag = v.Aftale.Start.LocalDateTime.Date;
            if (dag == forrige) continue;

            forrige = dag;

            var iDag = dag == nu.LocalDateTime.Date;
            var iMorgen = dag == nu.LocalDateTime.Date.AddDays(1);

            // Ugedagen med stort forbogstav. Dansk skriver dem med lille, men
            // som overskrift laeses «Torsdag» hurtigere end «torsdag» - og
            // ToUpper paa hele ordet ville raabe.
            var ugedag = dag.ToString("dddd", Dansk);

            v.Dagsnavn = iDag ? "I DAG"
                       : iMorgen ? "I MORGEN"
                       : char.ToUpper(ugedag[0]) + ugedag[1..];

            v.Dagsdato = dag.ToString("d. MMMM", Dansk);
        }

        Kalenderrude.ItemsSource = visninger;
        IngenAftaler.Visibility = kommende.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Overskriften sagde foer «I dag · 2». Nu staar «I DAG» som den
        // foerste dagsoverskrift inde i listen, og to steder, der siger det
        // samme, er ét sted for meget. Her staar antallet i stedet - det
        // svarer paa «hvor meget ligger der forude», som listen ikke selv
        // svarer paa, foer man har rullet den igennem.
        Kalenderoverskrift.Text = kommende.Count == 0
            ? "Kalender"
            : $"Kalender · {kommende.Count}";
    }

    /// <summary>
    /// Starter optagelsen af en aftale — eller går til den, hvis den er lavet.
    ///
    /// AFTALENS EGNE VALG FØLGER MED. Mødetype, mappe og sprog er valgt, da
    /// aftalen blev lagt ind, og de skal ikke vælges igen med mødet i gang.
    /// Er de ikke sat, kommer den almindelige opstartsdialog — så er man
    /// præcis lige så langt som uden kalenderen.
    /// </summary>
    private void Aftale_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Aftalevisning v) return;
        if (Window.GetWindow(this) is not MainWindow hoved) return;

        var a = v.Aftale;

        if (a.MoedeId.Length > 0)
        {
            if (!hoved.GaaTilOptagelse(a.MoedeId))
                Dialogs.AppDialog.Vis(hoved, "Optagelsen findes ikke længere",
                    "Den er slettet eller flyttet uden for appen. Aftalen bliver stående.",
                    Dialogs.Slags.Valg);

            return;
        }

        hoved.OptagAftale(a);
        VisKalender();
    }

    /// <summary>
    /// Åbner en aftale, så den kan rettes eller slettes.
    ///
    /// En hentet aftale kan få mødetype, mappe og sprog på — det er appens
    /// egne felter, og de findes ikke hos leverandøren. Titel og tidspunkt er
    /// låst; de bliver overskrevet ved næste hentning.
    /// </summary>
    private async void RetAftale_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Aftalevisning v) return;

        var vindue = new Meeting.AftaleWindow(v.Aftale) { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true) return;

        if (vindue.Slettet)
        {
            Kalender.Slet(v.Aftale.Id);
            VisKalender();
            return;
        }

        Kalender.Gem(vindue.Aftalen);
        VisKalender();

        await LaegOpHosGoogle(vindue);
    }

    /// <summary>
    /// En aftale, man selv lægger ind.
    ///
    /// DEN SKAL KUNNE LAVES UDEN EN INTEGRATION. Målgruppen er studerende og
    /// mindre selvstændige, og en del af dem har hverken Google Workspace
    /// eller Microsoft 365. En kalender, der kræver en konto hos Google for at
    /// virke, er ubrugelig for dem.
    /// </summary>
    private async void NyAftale_Klik(object sender, RoutedEventArgs e)
    {
        var vindue = new Meeting.AftaleWindow(null) { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true) return;

        Kalender.Gem(vindue.Aftalen);
        VisKalender();

        await LaegOpHosGoogle(vindue);
    }

    /// <summary>
    /// Åbner aftalen på Googles egen side, så der kan inviteres gæster.
    ///
    /// DET ER DÉR, KONTAKTERNE ER. Invitationer sendes af Google, svarene
    /// lander hos Google, og navnene ligger i Googles adressebog. En
    /// deltagerliste bygget her ville være en dårligere kopi af en skærm,
    /// brugeren kender — og hver adresse skulle tastes forfra.
    ///
    /// Kan browseren ikke åbnes, siges det. Aftalen ER oprettet, og det må
    /// ikke se ud, som om noget gik tabt.
    /// </summary>
    /// <summary>
    /// Åbner opgaven i sit eget vindue.
    ///
    /// LISTEN ER IKKE ET ARBEJDSBORD. Den viser opgaven på to linjer, så tyve
    /// af dem kan skimmes; skal en af dem rettes, skal der være plads til at
    /// læse den og skrive i den.
    ///
    /// Der læses forfra bagefter — også når der ikke blev gemt. Det koster et
    /// filopslag pr. mappe, og alternativet er en liste, der kan komme ud af
    /// trit med filerne.
    /// </summary>
    private void Opgave_Aabn(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Opgavevisning v }) return;

        // FLUEBENET, PRIORITETEN OG HERKOMSTLINKET LIGGER INDEN I KORTET.
        //
        // De markerer selv deres klik som behandlede, saa de burde aldrig naa
        // herud. «Burde» er ikke godt nok: en rulleliste, hvis punkter tegnes
        // i et popup-lag, foelger ikke altid den regel, og resultatet ville
        // vaere et vindue, der springer op, hver gang man saetter en prioritet.
        //
        // Derfor gaas der op gennem traeet fra dét, der faktisk blev ramt.
        if (e.OriginalSource is DependencyObject ramt)
        {
            for (var p = ramt; p is not null && p != sender; p = System.Windows.Media.VisualTreeHelper.GetParent(p))
            {
                if (p is System.Windows.Controls.Primitives.ButtonBase or ComboBox or ComboBoxItem)
                    return;
            }
        }

        var vindue = new OpgaveWindow(v.Opgave) { Owner = Window.GetWindow(this) };
        vindue.ShowDialog();

        if (vindue.Gemt) VisOpgaver();
    }

    private void AabnHosGoogle(string webadresse)
    {
        if (webadresse.Length == 0) return;

        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(webadresse) { UseShellExecute = true });
        }
        catch (Exception)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                "Aftalen er oprettet hos Google",
                "Browseren kunne ikke åbnes herfra. Åbn Google Kalender og find "
                + "aftalen dér for at invitere gæster.");
        }
    }

    /// <summary>
    /// Lægger aftalen op hos Google, hvis brugeren satte hak.
    ///
    /// DEN KØRER EFTER, AT AFTALEN ER GEMT LOKALT. Går oplægningen galt, står
    /// aftalen der stadig — den er ikke gået tabt, fordi nettet var nede. Den
    /// omvendte rækkefølge ville gøre appens egen kalender afhængig af en
    /// tjeneste, den er bygget til at kunne undvære.
    ///
    /// FREMMED-ID'ET GEMMES PÅ DEN LOKALE AFTALE. Uden det ville den samme
    /// aftale komme retur ved næste hentning og stå to gange. Se spærren i
    /// Kalender.Afloes.
    /// </summary>
    private async Task LaegOpHosGoogle(Meeting.AftaleWindow vindue)
    {
        if (!vindue.SkalOpHosGoogle) return;

        try
        {
            var noegle = Integrationsfiler.Hent("google").Opdateringsnoegle;
            if (noegle.Length == 0) return;

            var svar = await Googlekalender.OpretAsync(
                vindue.Aftalen, noegle, vindue.SkalHaveMeet);

            vindue.Aftalen.FremmedId = svar.Id;

            // MEET-LINKET SKAL TILBAGE I APPEN. Uden det er mødet klikbart hos
            // Google og dødt her — og så er kalenderen i Cockpittet noget, man
            // alligevel skal forlade for at komme med til mødet.
            //
            // Det overskriver ikke et link, brugeren selv har skrevet. Har man
            // sat et Teams-link ind, er det dét, mødet foregår på.
            if (svar.Moedelink.Length > 0 && vindue.Aftalen.Link.Length == 0)
                vindue.Aftalen.Link = svar.Moedelink;

            Kalender.Gem(vindue.Aftalen);
            VisKalender();

            if (vindue.SkalInvitere) AabnHosGoogle(svar.Webadresse);
        }
        catch (Exception ex)
        {
            // AFTALEN ER GEMT. Det her er ikke en fejl, der skal se ud som om
            // arbejdet gik tabt - kun som at den ene halvdel manglede.
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                "Aftalen kom ikke i Google Kalender",
                "Den er gemt i NoteApp. Det var oplægningen hos Google, der ikke "
                + "lykkedes." + Environment.NewLine + Environment.NewLine + ex.Message);
        }
    }

    // ------------------------------------------------------------- opgaverne

    /// <summary>
    /// De åbne opgaver på tværs af alle optagelser.
    ///
    /// De står i Cockpittet, fordi det er den skærm, man ser først, og fordi
    /// spørgsmålet «hvad skal jeg gøre nu» ikke kan besvares af ét møde ad
    /// gangen. En opgave fra et møde i maj og en fra i går står side om side —
    /// det er dét, «på tværs» betyder.
    ///
    /// Der læses fra disken hver gang, skærmen åbnes eller en opgave ændres.
    /// En liste, der kan komme ud af trit med filerne, ville vise en opgave,
    /// man netop har krydset af.
    /// </summary>
    private void VisOpgaver()
    {
        var idag = DateOnly.FromDateTime(DateTime.Today);

        List<Registeropgave> aabne;
        try { aabne = Opgaveregister.Aabne(idag); }
        catch (Exception) { aabne = new List<Registeropgave>(); }

        Opgaverude.ItemsSource = aabne
            .Select(r => new Opgavevisning(r, idag, VisOpgaver))
            .ToList();

        IngenOpgaver.Visibility = aabne.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var haster = aabne.Count(r => r.Hastighed(idag)
            is Hastighed.Overskredet or Hastighed.I_dag);

        OpgaveOverskrift.Text = aabne.Count == 0
            ? "Ingen åbne opgaver"
            : haster > 0 ? $"{haster} skal gøres nu" : "Åbne opgaver";

        OpgaveTal.Text = aabne.Count == 0
            ? ""
            : $"{aabne.Count} i alt fra {aabne.Select(r => r.MoedeId).Distinct().Count()} optagelser";
    }

    /// <summary>
    /// Går til den optagelse, opgaven kom fra.
    ///
    /// Der slås op på id og ikke på sti — mødet kan være flyttet til en anden
    /// mappe, siden opgaven blev oprettet. Findes det ikke længere, siges det
    /// frem for at skifte skærm og vise en tom liste.
    /// </summary>
    private void Opgave_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Opgavevisning v) return;
        if (Window.GetWindow(this) is not MainWindow hoved) return;

        // TOM MoedeId ER IKKE EN FEJL. Opgaven er skrevet i haanden og har
        // aldrig haft en optagelse bag sig. Knappen vises slet ikke i det
        // tilfaelde - se Linkvis - men skulle den alligevel blive trykket, er
        // «findes ikke laengere» det forkerte svar: der var aldrig noget.
        if (v.MoedeId.Length == 0) return;

        if (!hoved.GaaTilOptagelse(v.MoedeId))
            Dialogs.AppDialog.Vis(hoved, "Optagelsen findes ikke længere",
                "Den er slettet eller flyttet uden for appen, siden opgaven blev oprettet. " +
                "Opgaven bliver stående — den er stadig din.",
                Dialogs.Slags.Valg);
    }

    // ------------------------------------------------- faner og sortering

    /// <summary>Én fane over resultatet: en slags kilde og hvor mange der er.</summary>
    public sealed record Fanevisning(string Navn, Fundtype? Slags, bool Valgt)
    {
        public Brush Flade => Valgt
            ? (Brush)new BrushConverter().ConvertFrom("#FF33405A")!
            : (Brush)new BrushConverter().ConvertFrom("#FF181B22")!;

        public Brush Kant => Valgt
            ? (Brush)new BrushConverter().ConvertFrom("#FF5B9DF0")!
            : (Brush)new BrushConverter().ConvertFrom("#FF3A4150")!;

        public Brush Skrift => Valgt
            ? (Brush)new BrushConverter().ConvertFrom("#FFF4F6FA")!
            : (Brush)new BrushConverter().ConvertFrom("#FF9BA6B8")!;
    }

    /// <summary>Én måde at sortere resultatet på.</summary>
    public sealed record Sorteringsvalg(string Navn, string Id);

    private IReadOnlyList<Fund> _fund = Array.Empty<Fund>();
    private string _ord = "";
    private Fundtype? _valgtType;

    /// <summary>
    /// Fanerne over resultatet — én pr. slags kilde, der faktisk er noget af.
    ///
    /// «Alt» står først og er valgt fra begyndelsen. Fanerne kommer kun frem,
    /// når der er mere end én slags at vælge imellem; en fanerække med ét
    /// punkt er ikke et valg, den er pynt.
    ///
    /// Rækkefølgen er den samme som relevansens: transkription, note,
    /// dokument. Kilden først, genfortællingen sidst.
    /// </summary>
    private void ByggFaner()
    {
        var slags = new (string Navn, Fundtype Type)[]
        {
            ("Transkriptioner", Fundtype.Udskrift),
            ("Noter", Fundtype.Note),
            ("Dokumenter", Fundtype.Dokument)
        };

        var findes = slags.Where(s => _fund.Any(f => f.Slags == s.Type)).ToList();

        // Er den valgte fane toemt af en ny soegning, faldes der tilbage til
        // «Alt». Ellers ville skaermen staa tom, mens der ER resultater.
        if (_valgtType is { } v && findes.All(s => s.Type != v)) _valgtType = null;

        var faner = new List<Fanevisning> { new($"Alt ({_fund.Count})", null, _valgtType is null) };

        foreach (var s in findes)
        {
            var antal = _fund.Count(f => f.Slags == s.Type);
            faner.Add(new Fanevisning($"{s.Navn} ({antal})", s.Type, _valgtType == s.Type));
        }

        Typefaner.ItemsSource = faner;
        Typefaner.Visibility = findes.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (Sortering.ItemsSource is null)
        {
            // NYESTE FOERST ER STANDARDEN.
            //
            // Relevans er maalt bedst til at finde det rigtige moede - 90 pct
            // paa foerstepladsen, se doc/findings.md - men det er ikke altid
            // det, man leder efter. «Hvad talte vi om for nylig» er et
            // almindeligt spoergsmaal, og dér er datoen svaret.
            //
            // Begge staar der, saa valget er brugerens.
            Sortering.ItemsSource = new List<Sorteringsvalg>
            {
                new("Nyeste først", "nyeste"),
                new("Ældste først", "aeldste"),
                new("Bedste træf først", "traef"),
                new("Mødetype", "moedetype")
            };
            Sortering.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Tegner listen ud fra den valgte fane og den valgte sortering.
    ///
    /// Der soeges IKKE forfra. Fundene ligger i _fund, og fane og sortering er
    /// kun to maader at vise dem paa — en ny gennemloebning af alle filer for
    /// et klik paa en fane ville vaere det samme arbejde to gange.
    /// </summary>
    private void TegnResultat()
    {
        var vist = _valgtType is null
            ? _fund
            : _fund.Where(f => f.Slags == _valgtType).ToList();

        var id = (Sortering.SelectedItem as Sorteringsvalg)?.Id ?? "nyeste";

        vist = id switch
        {
            "aeldste" => vist.OrderBy(f => f.Tid).ToList(),
            "traef" => vist.OrderByDescending(f => f.Vaegt)
                           .ThenByDescending(f => f.Traef.Count)
                           .ThenBy(f => f.Slags)
                           .ToList(),
            // Moedetypen staar ikke paa fundet - den slaas op paa moedet. Er
            // den ikke sat, staar optagelsen sidst: en tom gruppe skal ikke
            // ligge oeverst og fylde, foer man naar dem, der ER sorteret.
            "moedetype" => vist.OrderBy(f => Moedetypen(f).Length == 0 ? 1 : 0)
                               .ThenBy(f => Moedetypen(f), StringComparer.CurrentCultureIgnoreCase)
                               .ThenByDescending(f => f.Tid)
                               .ToList(),
            _ => vist.OrderByDescending(f => f.Tid).ToList()
        };

        Liste.ItemsSource = vist.Select(f => new Fundvisning(f, _ord)).ToList();
    }

    /// <summary>
    /// Mødetypen på et fund. Slås op på mødet — den står ikke i selve fundet.
    ///
    /// Svarene huskes: den samme optagelse optræder tit flere gange i ét
    /// resultat, og et opslag pr. række ville laese den samme fil ti gange.
    /// </summary>
    private readonly Dictionary<string, string> _typer = new();

    private string Moedetypen(Fund f)
    {
        if (_typer.TryGetValue(f.Kilde, out var t)) return t;

        var svar = "";

        try
        {
            if (MeetingStore.FindById(f.Kilde) is { } fundet)
                svar = fundet.Meta.Moedetype ?? "";
        }
        catch (Exception)
        {
            // Et dokument har ikke et moede-id. Saa er svaret tomt, og det
            // lander nederst - det er det rigtige.
        }

        _typer[f.Kilde] = svar;
        return svar;
    }

    private void Fane_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Fanevisning v) return;

        _valgtType = v.Slags;
        ByggFaner();
        TegnResultat();
    }

    private void Sortering_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _fund.Count == 0) return;

        TegnResultat();
    }

    // ------------------------------------------------------------- filtrene

    /// <summary>Ét punkt i en filterliste: det, der vises, og det, der gælder.</summary>
    public sealed record Filterpunkt(string Navn, string? Vaerdi);

    /// <summary>
    /// Fylder de tre filterlister med det, der FAKTISK ligger i arkivet.
    ///
    /// DER STÅR KUN DET, MAN KAN VÆLGE. En liste over alle tænkelige sprog
    /// ville have tredive punkter, hvor de otteogtyve ikke giver noget. Har
    /// man kun danske møder, er der ét sprog at vælge — og så siger listen
    /// samtidig noget sandt om arkivet.
    ///
    /// Listerne bygges én gang, når skærmen åbnes. Kommer der en ny mappe til,
    /// mens man står her, er den med næste gang; det er billigere end at læse
    /// alle møder igennem ved hvert klik.
    /// </summary>
    private void FyldFiltre()
    {
        // Perioderne er faste og staar altid — de afhaenger ikke af, hvad der
        // ligger i arkivet.
        Periodefilter.ItemsSource = new List<Filterpunkt>
        {
            new("Hele tiden", null),
            new("I dag", "idag"),
            new("Denne uge", "uge"),
            new("Denne måned", "maaned"),
            new("I år", "aar"),
            new("Fra og til …", "valgt")
        };
        Periodefilter.SelectedIndex = 0;

        var mapper = new List<Filterpunkt> { new("Alle mapper", null) };
        var typer = new List<Filterpunkt> { new("Alle mødetyper", null) };
        var sprog = new List<Filterpunkt> { new("Alle sprog", null) };

        var seteMapper = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var seteTyper = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var seteSprog = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in MeetingStore.Alle())
        {
            if (!string.IsNullOrWhiteSpace(m.Mappe) && seteMapper.Add(m.Mappe))
                mapper.Add(new Filterpunkt(m.Mappe, m.Mappe));

            if (!string.IsNullOrWhiteSpace(m.Moedetype) && seteTyper.Add(m.Moedetype))
                typer.Add(new Filterpunkt(m.Moedetype, m.Moedetype));

            if (Soegefilter.SprogPaa(m) is { Length: > 0 } s && seteSprog.Add(s))
                sprog.Add(new Filterpunkt(Transcriber.LanguageName(s), s));
        }

        Saet(Mappefilter, mapper);
        Saet(Typefilter, typer);
        Saet(Sprogfilter, sprog);

        static void Saet(System.Windows.Controls.ComboBox b, List<Filterpunkt> punkter)
        {
            // Er der kun «alle», er der intet at vaelge imellem — og en liste
            // med ét punkt er en knap, der ikke goer noget.
            b.ItemsSource = punkter;
            b.SelectedIndex = 0;
            b.IsEnabled = punkter.Count > 1;
        }
    }

    /// <summary>
    /// Perioden, som den er valgt lige nu.
    ///
    /// «Fra og til» læser datovælgerne; alt andet er en fast periode. Er kun
    /// den ene dato sat, gælder den ene grænse — «fra 1. august og frem» er et
    /// rimeligt spørgsmål, og det skal ikke kræve, at man også finder på en
    /// slutdato.
    /// </summary>
    private (DateTimeOffset? Fra, DateTimeOffset? Til) Perioden()
    {
        var valg = (Periodefilter.SelectedItem as Filterpunkt)?.Vaerdi;

        if (valg != "valgt") return Soegefilter.Periode(valg ?? "");

        return (
            FraDato.SelectedDate is { } f ? Soegefilter.Lokal(f.Date) : null,
            TilDato.SelectedDate is { } t ? Soegefilter.SlutAfDagen(t) : null);
    }

    private Soegefilter Filteret()
    {
        var (fra, til) = Perioden();

        return new Soegefilter(
            Sprog: (Sprogfilter.SelectedItem as Filterpunkt)?.Vaerdi,
            Moedetype: (Typefilter.SelectedItem as Filterpunkt)?.Vaerdi,
            Mappe: (Mappefilter.SelectedItem as Filterpunkt)?.Vaerdi,
            Fra: fra, Til: til);
    }

    private void Filter_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        RydFilter.Visibility = Filteret().Tomt ? Visibility.Collapsed : Visibility.Visible;

        // Der soeges med det samme. Et filter, der foerst virker, naar man
        // roerer soegefeltet, foeles som om det ikke virkede.
        _pause.Stop();
        Soeg();
    }

    private void Periode_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        var valgt = (Periodefilter.SelectedItem as Filterpunkt)?.Vaerdi == "valgt";
        Datoraekke.Visibility = valgt ? Visibility.Visible : Visibility.Collapsed;

        // Er der ikke valgt datoer endnu, er der ingen afgraensning at soege
        // paa — og saa skal listen ikke tømmes, mens man leder efter
        // datovaelgeren.
        if (valgt && FraDato.SelectedDate is null && TilDato.SelectedDate is null)
        {
            RydFilter.Visibility = Filteret().Tomt ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        Filter_Aendret(sender, e);
    }

    /// <summary>
    /// En datovælger er ændret.
    ///
    /// DER SØGES IKKE PÅ EN OMVENDT PERIODE. Er «fra» efter «til», er der
    /// ingen dage imellem, og svaret ville være en tom liste, der ligner et
    /// resultat. Det siges i stedet.
    /// </summary>
    private void Dato_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        if (FraDato.SelectedDate is { } f && TilDato.SelectedDate is { } t && f > t)
        {
            Datofejl.Text = "«Fra» ligger efter «til».";
            return;
        }

        Datofejl.Text = "";
        Filter_Aendret(sender, e);
    }

    private void RydFilter_Klik(object sender, RoutedEventArgs e)
    {
        Mappefilter.SelectedIndex = 0;
        Typefilter.SelectedIndex = 0;
        Sprogfilter.SelectedIndex = 0;

        FraDato.SelectedDate = null;
        TilDato.SelectedDate = null;
        Datofejl.Text = "";

        Periodefilter.SelectedIndex = 0;
    }

    private async void Soeg()
    {
        var spoergsmaal = Felt.Text.Trim();

        // Den foregaaende soegning afbrydes. Skriver man videre, mens der
        // ledes i et stort arkiv, skal svaret paa det GAMLE ord ikke naa at
        // lande i listen bagefter.
        _afbryd?.Cancel();
        _afbryd?.Dispose();
        _afbryd = null;

        if (spoergsmaal.Length == 0)
        {
            TomPanel.Visibility = Visibility.Visible;
            Resultatrude.Visibility = Visibility.Collapsed;
            IntetPanel.Visibility = Visibility.Collapsed;
            Status.Text = "";
            return;
        }

        // Ét bogstav rammer alt og siger ingenting. Der ledes fra to.
        if (spoergsmaal.Length < 2)
        {
            Status.Text = "Skriv mindst to bogstaver.";
            return;
        }

        _afbryd = new CancellationTokenSource();
        var ct = _afbryd.Token;

        Status.Text = "Leder …";

        try
        {
            var filter = Filteret();

            var ur = System.Diagnostics.Stopwatch.StartNew();
            var fund = await Task.Run(() => Soegning.Soeg(spoergsmaal, filter, ct), ct);
            ur.Stop();

            if (ct.IsCancellationRequested) return;

            _fund = fund;
            _ord = spoergsmaal;

            ByggFaner();
            TegnResultat();

            // Uddragene tegnes af Uddrag_Ind, naar de kommer paa skaermen.

            TomPanel.Visibility = Visibility.Collapsed;
            Resultatrude.Visibility = fund.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            IntetPanel.Visibility = fund.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            if (fund.Count == 0) ForklarIntet(spoergsmaal, filter, ct);

            var steder = fund.Sum(f => f.Traef.Count);

            // TIDEN STAAR DER, OG DET ER MED VILJE.
            //
            // Der er ikke noget indeks - der laeses i filerne hver gang. Saa
            // laenge tallet er tocifret i millisekunder, er det det rigtige
            // valg. Begynder det at vokse, kan det ses her, foer det bliver
            // til en irritation.
            // ALLE ORDENE STAAR I HVERT ENESTE STED, DER VISES. Derfor er der
            // ikke laengere et forbehold at skrive her: er der et fund, er det
            // et svar paa hele spoergsmaalet — ikke paa halvdelen af det.
            Status.Text = fund.Count == 0
                ? $"Ingen fund  ·  {ur.ElapsedMilliseconds} ms"
                : $"{steder} {(steder == 1 ? "sted" : "steder")} i {fund.Count} " +
                  $"{(fund.Count == 1 ? "kilde" : "kilder")}  ·  {ur.ElapsedMilliseconds} ms";
        }
        catch (OperationCanceledException)
        {
            // Der blev skrevet videre. Den naeste soegning svarer.
        }
        catch (Exception ex)
        {
            Status.Text = "Søgningen gik galt.";
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke søge", ex.Message,
                Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>Én åben opgave, som Cockpittet viser den.</summary>
public sealed class Opgavevisning : System.ComponentModel.INotifyPropertyChanged
{
    private readonly Registeropgave _r;
    private readonly DateOnly _idag;
    private readonly Action _aendret;

    public Opgavevisning(Registeropgave r, DateOnly idag, Action aendret)
    {
        _r = r;
        _idag = idag;
        _aendret = aendret;
    }

    public string Tekst => _r.Opgave.Tekst;

    /// <summary>Det korte navn — det, listen viser. Højst to linjer.</summary>
    public string Navn => _r.Opgave.Visningsnavn;

    /// <summary>
    /// Hele opgaven. Vises først, når man folder den ud.
    ///
    /// EN LISTE, DER VISER ALT, KAN IKKE SKIMMES. Fem opgaver på fire linjer
    /// hver fylder en skærm, og så er overblikket væk — og overblikket er hele
    /// grunden til, at de står i Cockpittet.
    /// </summary>
    public string Beskrivelse => _r.Opgave.Tekst;

    /// <summary>Opgaven bag visningen — til vinduet, der åbner den.</summary>
    public Registeropgave Opgave => _r;

    /// <summary>
    /// Er herkomsten et LINK — eller bare en oplysning?
    ///
    /// EN OPGAVE UDEN OPTAGELSE HAR INGEN VEJ TILBAGE. Den er skrevet i
    /// hånden, og der findes ingen replik at høre igen. Et link, der åbner
    /// «optagelsen findes ikke længere», er værre end ingen link: det får
    /// noget, der er helt i orden, til at se ud som et tab.
    /// </summary>
    public Visibility Linkvis =>
        _r.MoedeId.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility Kildevis =>
        _r.MoedeId.Length > 0 ? Visibility.Collapsed : Visibility.Visible;


    /// <summary>Mødets id — vejen tilbage til det, der blev sagt.</summary>
    public string MoedeId => _r.MoedeId;

    /// <summary>Hvor den kom fra — og hvem der skal gøre det.</summary>
    public string Under
    {
        get
        {
            var dele = new List<string>();

            if (_r.Opgave.Ejer.Length > 0) dele.Add(_r.Opgave.Ejer);
            dele.Add(_r.Moedetitel);
            if (_r.Opgave.Kilde.Length > 0) dele.Add(_r.Opgave.Kilde);

            return string.Join("  ·  ", dele);
        }
    }

    /// <summary>
    /// Fristen, skrevet som man ville sige den.
    ///
    /// «i morgen» slår «23-08-2026» — man skal ikke regne for at forstå, hvor
    /// meget det haster. Er datoen gættet ud af en tvetydig vending, står der
    /// et spørgsmålstegn: en frist, appen har valgt, må ikke se ud som en,
    /// nogen har sagt.
    /// </summary>
    public string Frist
    {
        get
        {
            if (_r.Opgave.Deadline is not { } d) return "";

            var dato = DateOnly.FromDateTime(d.LocalDateTime);
            var tekst = dato < _idag
                ? $"{Datoforstaaelse.Skriv(dato, _idag)} — overskredet"
                : Datoforstaaelse.Skriv(dato, _idag);

            return _r.Opgave.DeadlineUsikker ? tekst + " ?" : tekst;
        }
    }

    public Visibility Fristvis => _r.Opgave.Deadline is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Rød, gul eller grøn — efter hvor meget der er til fristen.</summary>
    public Brush Farve => _r.Hastighed(_idag) switch
    {
        Hastighed.Overskredet => Pensel("#FFE5484D"),
        Hastighed.I_dag => Pensel("#FFE8A33D"),
        Hastighed.Denne_uge => Pensel("#FFE8A33D"),
        Hastighed.Senere => Pensel("#FF3DA55A"),
        _ => Pensel("#FF3A4150")
    };

    private static Brush Pensel(string hex) =>
        (Brush)new BrushConverter().ConvertFrom(hex)!;

    public bool Faerdig
    {
        get => _r.Opgave.Faerdig;
        set
        {
            if (_r.Opgave.Faerdig == value) return;

            _r.Opgave.Faerdig = value;
            Opgaveregister.Gem(_r);
            _aendret();
        }
    }

    /// <summary>0 = ingen, 1-3 = prioritet. Passer til rullelistens pladser.</summary>
    public int Prioritetsvalg
    {
        get => _r.Opgave.Prioritet;
        set
        {
            if (_r.Opgave.Prioritet == value) return;

            _r.Opgave.Prioritet = value;
            Opgaveregister.Gem(_r);
            _aendret();
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void Meld(string navn) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(navn));
}

/// <summary>Ét søgeord og hvor meget der er af det. Til den tomme skærm.</summary>
    public sealed record Ordvisning(string Ord, string Hvor);

    /// <summary>
    /// Siger, HVORFOR der ikke er noget fund.
    ///
    /// «Ingen fund» alene er ikke et svar. To vidt forskellige ting ser ens
    /// ud: at ordet slet ikke står nogen steder, og at det står mange steder
    /// uden nogensinde at være i nærheden af de øvrige. Kun det første betyder,
    /// at man skrev forkert.
    ///
    /// Hvert ord søges derfor for sig, og resultatet står med en knap, der
    /// søger på netop det. Det er en søgning pr. ord og koster få
    /// millisekunder — man ville selv gøre det manuelt bagefter.
    /// </summary>
    private void ForklarIntet(string spoergsmaal, Soegefilter filter, CancellationToken ct)
    {
        var ord = Soegning.Del(spoergsmaal);

        IntetOverskrift.Text = $"Ingen fund på «{spoergsmaal}»";
        Enkeltvis.ItemsSource = null;

        if (ord.Count < 2)
        {
            IntetTekst.Text = filter.Tomt
                ? "Ordet står ikke i nogen transkription, note eller dokument."
                : "Ordet står ikke i det, du har afgrænset til. Prøv at rydde afgrænsningen.";
            return;
        }

        List<(string Ord, int Steder, int Kilder)> enkelt;
        try { enkelt = Soegning.Enkeltvis(spoergsmaal, filter, ct); }
        catch (OperationCanceledException) { return; }

        var findes = enkelt.Where(e => e.Steder > 0).ToList();
        var mangler = enkelt.Where(e => e.Steder == 0).Select(e => e.Ord).ToList();

        IntetTekst.Text = mangler.Count > 0
            ? $"«{string.Join("», «", mangler)}» står ingen steder. De øvrige ord findes."
            : "Alle ordene findes — men de står aldrig tæt nok på hinanden til at høre sammen. " +
              "Søg på færre ord ad gangen.";

        Enkeltvis.ItemsSource = enkelt
            .Select(e => new Ordvisning(e.Ord,
                e.Steder == 0
                    ? "ingen steder"
                    : $"{e.Steder} {(e.Steder == 1 ? "sted" : "steder")} i {e.Kilder} " +
                      $"{(e.Kilder == 1 ? "kilde" : "kilder")}"))
            .ToList();
    }

    private void KunDet_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string ord) return;

        Felt.Text = ord;
        Felt.CaretIndex = Felt.Text.Length;
        Felt.Focus();
    }

    /// <summary>
    /// Springer hen til det sted, der blev klikket på.
    ///
    /// Der slås op på id og ikke på sti: et møde kan være flyttet til en
    /// mappe, og et dokument kan være omdøbt, siden udskriften blev lavet.
    /// Positionen er tegnnummeret i den tekst, skærmen viser — derfor kan der
    /// rulles direkte derhen frem for blot at åbne filen.
    /// </summary>
    /// <summary>
    /// Tegner uddraget med det søgte markeret i gult.
    ///
    /// Samme fremgangsmåde som i udskriften: en TextBlock kan give enkelte ord
    /// en baggrund, hvis dens indhold bygges som stykker frem for som én
    /// tekst — og det kan ikke bindes i XAML, så det gøres her.
    /// </summary>
    private void Uddrag_Ind(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock tb || tb.DataContext is not Traefvisning v) return;

        tb.Inlines.Clear();

        foreach (var (tekst, gul) in v.Dele())
            tb.Inlines.Add(gul
                ? new System.Windows.Documents.Run(tekst) { Background = Gul, Foreground = PaaGul }
                : new System.Windows.Documents.Run(tekst));
    }

    private static readonly Brush Gul = new SolidColorBrush(Color.FromRgb(0xF5, 0xD1, 0x3B));
    private static readonly Brush PaaGul = new SolidColorBrush(Color.FromRgb(0x14, 0x18, 0x1F));

    private void Sted_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Traefvisning v) return;
        if (Application.Current.MainWindow is not MainWindow hoved) return;

        // Ordet huskes EFTER navigationen. Nav_Changed rydder linjen, og
        // den fyrer undervejs - saettes den foer, er den vaek igen med det
        // samme.
        var ord = Felt.Text.Trim();
        var steder = v.Fund.Traef.Count;

        if (v.Fund.Slags == Fundtype.Dokument)
        {
            hoved.GaaTilDokumenter(v.Fund.Kilde, v.Traef.Position);
            hoved.HuskSoegning(ord, steder);
            return;
        }

        if (hoved.GaaTilOptagelse(v.Fund.Kilde, v.Traef.Position))
            hoved.HuskSoegning(ord, steder);
        else
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes ikke længere",
                "Optagelsen er slettet eller flyttet uden for appen, siden den blev skrevet ud.",
                Dialogs.Slags.Valg);
    }
}
