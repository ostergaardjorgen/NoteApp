using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            Fundtype.Udskrift => ("TRANSKRIPTION", Temaskift.Pensel("Accent")),
            Fundtype.Note => ("DIN NOTE", Temaskift.Pensel("Godkendt")),
            _ => ("DOKUMENT", Temaskift.Pensel("Dokument"))
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

        LytEfterSynk();

        Loaded += (_, _) =>
        {
            HentSpaltebredder();
            StartTips();
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

            // En gemt bredde kan vaere fra en stoerre skaerm end den, appen
            // aabnes paa nu.
            KlipSpalter();
        }
        catch (Exception)
        {
            // Kan indstillingerne ikke laeses, staar standardbredderne. En
            // spaltebredde er ikke noget at vaelte en skaerm for.
        }
    }

    /// <summary>
    /// Sidespalterne må ikke skubbe midten ud over kanten.
    /// </summary>
    /// <remarks>
    /// DE TO SIDESPALTER ER ABSOLUTTE BREDDER, og en absolut Grid-spalte
    /// skrumper ikke, når pladsen slipper op — den bliver stående, og resten
    /// af rækken lægger sig uden for vinduet. Målt i et vindue på appens
    /// mindstebredde 1040: 340 + 14 + 320 + 14 + 340 er 1028, og der var 796
    /// at gøre godt med. Hele højre spalte lå uden for kanten, og opgaverne
    /// var klippet midt i et ord.
    ///
    /// Det kan ikke løses med stjernebredder: bredderne animeres, trækkes og
    /// gemmes som tal, og en stjerne er ikke et tal. Her klippes de i stedet
    /// til det, der er plads til — og kun dét: bliver vinduet bredt igen, står
    /// den bredde, man selv har trukket, uændret i <c>_venstreFuld</c>.
    ///
    /// SKRIVER KUN, NÅR DET ÆNDRER NOGET. En bredde sat inde i SizeChanged
    /// udløser en ny måling, og uden den her betingelse ville de to gå i ring.
    /// </remarks>
    private void Spalter_Maalt(object sender, SizeChangedEventArgs e) => KlipSpalter();

    /// <summary>
    /// Klipper sidespalterne til det, der er plads til.
    /// </summary>
    /// <remarks>
    /// DEN SKAL KALDES BEGGE STEDER. Første forsøg hang den kun på
    /// SizeChanged, og det virkede ikke: bredderne bliver hentet fra
    /// indstillingerne EFTER den første måling, og at sætte en spaltebredde
    /// ændrer ikke gitterets egen bredde — der kommer altså ingen ny måling,
    /// og de gemte 340 blev stående. Målt: højre spalte lå stadig uden for
    /// kanten. Derfor kaldes den også, når bredderne er hentet.
    /// </remarks>
    private void KlipSpalter()
    {
        // Mens spalterne folder sig ind, er MinWidth sat til nul med vilje.
        // Saa er det animationen, der bestemmer, ikke den her.
        if (Venstrespalte.MinWidth < 1) return;

        // MAALT PAA RUDEN, IKKE PAA GITTERET. Gitteret med de tre spalter
        // rapporterer sin ØNSKEDE bredde, ikke den plads det fik: 1028 i et
        // vindue, hvor der var 780. Saa kunne det ikke selv se, at det var
        // for stort. Ruden udenom kender den rigtige bredde.
        var plads = ActualWidth
                    - Rammen.Margin.Left - Rammen.Margin.Right
                    - VenstreSplitter.Width - HoejreSplitter.Width
                    - Midterspalte.MinWidth;

        if (double.IsNaN(plads) || plads <= 0) return;

        var loft = Math.Max(220, plads / 2);

        Klip(Venstrespalte, loft);
        Klip(Hoejrespalte, loft);
    }

    private static void Klip(ColumnDefinition spalte, double loft)
    {
        if (!spalte.Width.IsAbsolute || spalte.Width.Value <= loft) return;
        spalte.Width = new GridLength(loft);
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


    // ============================ SPALTERNE GLIDER ============================

    /// <summary>
    /// Hvor meget sidespalterne er foldet ud: 1 er helt ude, 0 er helt inde.
    /// </summary>
    /// <remarks>
    /// EN GRIDLENGTH KAN IKKE ANIMERES DIREKTE. WPF har ingen animation for
    /// den type, og en egen ville skulle skrives fra bunden.
    ///
    /// I stedet animeres ét almindeligt tal, og det er dette tal, der ganges
    /// på de bredder, spalterne HAR. Så følger de hinanden, og en spalte, du
    /// selv har trukket bredere, folder sig ud til netop dén bredde igen.
    /// </remarks>
    public static readonly DependencyProperty UdfoldningProperty =
        DependencyProperty.Register(nameof(Udfoldning), typeof(double), typeof(SearchView),
            new PropertyMetadata(1.0, Udfoldet));

    public double Udfoldning
    {
        get => (double)GetValue(UdfoldningProperty);
        set => SetValue(UdfoldningProperty, value);
    }

    private static void Udfoldet(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SearchView v) v.Saet((double)e.NewValue);
    }

    /// <summary>Bredderne, spalterne folder sig ud til.</summary>
    private double _venstreFuld;
    private double _hoejreFuld;

    private void Saet(double f)
    {
        Venstrespalte.Width = new GridLength(Math.Max(0, _venstreFuld * f));
        Hoejrespalte.Width = new GridLength(Math.Max(0, _hoejreFuld * f));

        // Traekhaandtagene skal med. Blev de staaende, ville der vaere
        // otteogtyve pixels tom kant tilbage, naar spalterne var vaek.
        VenstreSplitter.Width = 14 * f;
        HoejreSplitter.Width = 14 * f;

        // Indholdet toner ud lidt foer pladsen er vaek. En rude, der er
        // presset sammen til nogle faa pixels med skrift i, ser i stykker ud.
        var toning = Math.Min(1, f * 1.6);
        VenstreRude.Opacity = toning;
        HoejreRude.Opacity = toning;

        // MinWidth skal vaek, mens der foldes. Ellers stopper spalten paa 220
        // og bliver staaende - og saa glider den halvvejs og hakker.
        Venstrespalte.MinWidth = f > 0.99 ? 220 : 0;
        Hoejrespalte.MinWidth = f > 0.99 ? 220 : 0;

        VenstreSplitter.IsEnabled = f > 0.99;
        HoejreSplitter.IsEnabled = f > 0.99;
    }

    /// <summary>
    /// Folder spalterne ind eller ud.
    /// </summary>
    /// <remarks>
    /// FIRE TIENDEDELE AF ET SEKUND, med en blød kurve i begge ender. En rude,
    /// der forsvinder med et snup, læses som en fejl; en, der glider, læses
    /// som en bevægelse, man selv satte i gang.
    ///
    /// Kortere, og det føles hastigt. Længere, og man sidder og venter på at
    /// kunne se sit resultat.
    /// </remarks>
    private void Fold(bool ud)
    {
        var maal = ud ? 1.0 : 0.0;

        // Allerede paa vej derhen? Saa skal der ikke startes forfra - det
        // giver et ryk midt i bevaegelsen, hver gang man taster et bogstav.
        if (Math.Abs(_foldesTil - maal) < 0.001) return;
        _foldesTil = maal;

        SikrBredder();

        if (ud)
        {
            // Bredderne kan vaere aendret, mens spalterne var inde. Hentes de
            // ikke igen, folder de sig ud til noget forkert.
            HentBredder();
        }

        // Farten staar i Glid. Menuen i venstre side bruger den samme, og de
        // to skal bevaege sig ens - ellers laeses de som to forskellige slags
        // ting, og man maerker det laenge foer man kan sige hvorfor.
        BeginAnimation(UdfoldningProperty, Glid.Til(maal));
    }

    private double _foldesTil = 1.0;

    /// <summary>
    /// Henter bredderne, første gang der er brug for dem.
    /// </summary>
    /// <remarks>
    /// De kan ikke hentes i konstruktøren: er de nul, folder spalterne sig ud
    /// til ingenting, første gang søgefeltet ryddes — og så er kalenderen væk,
    /// uden at nogen har bedt om det.
    /// </remarks>
    private void SikrBredder()
    {
        if (_venstreFuld <= 0 || _hoejreFuld <= 0) HentBredder();
    }

    private void HentBredder()
    {
        var v = AppSettings.Current.CockpitVenstre;
        var h = AppSettings.Current.CockpitHoejre;

        _venstreFuld = v > 0 ? v : 306;
        _hoejreFuld = h > 0 ? h : 340;
    }

    private void Felt_Aendret(object sender, TextChangedEventArgs e)
    {
        var tomt = Felt.Text.Length == 0;

        // Soeger man, er det soegningen, der skal fylde. Kalenderen og
        // opgaverne er dét, man kigger paa, naar man IKKE leder efter noget
        // bestemt.
        Fold(ud: tomt);

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

        /// <summary>Datoen ved siden af dagsnavnet — «24. august».</summary>
        public string Dagsdato { get; set; } = "";

        /// <summary>Er det her dagens første aftale?</summary>
        public Visibility Dagsvis =>
            Dagsnavn.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>I dag skal skille sig ud — det er den dag, man kan nå noget på.</summary>
        /// <remarks>
        /// TEKSTSVAG OG IKKE TEKSTMEGET. Ved siden af opgavespalten, hvor alt
        /// står i den almindelige tekstfarve, så kalenderens dagsoverskrifter
        /// ud til at være slået fra. Et trin op er nok — de er stadig
        /// overskrifter og skal ikke konkurrere med aftalernes titler.
        /// </remarks>
        public Brush Dagsfarve =>
            _a.Start.Date == _nu.Date ? Pensel("Advarsel") : Pensel("TekstSvag");

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
        /// Det, der står i boblen, når musen hviler på aftalen.
        /// </summary>
        /// <remarks>
        /// LINJEN UNDER TITLEN ER TAGET AF KORTET. Der stod «Google» på hver
        /// eneste aftale — den samme tekst hele vejen ned, og en oplysning,
        /// der er ens på alt, siger ingenting. Til gengæld står HELE titlen
        /// her, og den er ofte klippet på kortet.
        /// </remarks>
        public string Boble
        {
            get
            {
                var linjer = new List<string> { _a.Titel };

                var tid = _a.Start.LocalDateTime.ToString("dddd d. MMMM 'kl.' HH:mm");

                var under = new List<string> { tid };
                if (Under.Length > 0) under.Add(Under);

                linjer.Add(string.Join("  ·  ", under));
                linjer.Add("Klik for at åbne aftalen.");

                return string.Join(Environment.NewLine + Environment.NewLine, linjer.Where(l => l.Length > 0));
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
                if (_a.MoedeId.Length > 0) return Pensel("Godkendt");
                if (_a.ErIGang(_nu)) return Pensel("Optager");
                if (_a.ErOverstaaet(_nu)) return Pensel("Slukket");

                return _a.Start <= _nu.AddHours(1)
                    ? Pensel("Advarsel")
                    : Pensel("PanelKant");
            }
        }

        /// <summary>
        /// Klokkeslættet siger det samme som striben — men det skal kunne læses.
        ///
        /// Tallene stod før i «Kant», og Kant er en STRIBEFARVE. Den er dæmpet
        /// med vilje, og det er rigtigt om tre pixels kant. Om tal er det
        /// forkert: en aftale senere på ugen fik #3A4150 mod panelet, altså
        /// 1,52:1, og en overstået fik 1,23:1 — og blev derefter ganget med
        /// Dæmpning. Klokkeslættet var reelt ikke tegnet.
        ///
        /// Samme betydning, samme rækkefølge, læsbare lysstyrker: rødt er i
        /// gang, gult er inden for en time, grønt optager selv, gråt er
        /// senere. Ingen af dem går under 4,5:1.
        /// </summary>
        public Brush Klokkefarve
        {
            get
            {
                if (_a.MoedeId.Length > 0) return Pensel("Godkendt");
                if (_a.ErIGang(_nu)) return Pensel("FejlTekst");
                if (_a.ErOverstaaet(_nu)) return Pensel("Slukket");

                return _a.Start <= _nu.AddHours(1)
                    ? Pensel("Advarsel")
                    : Pensel("TekstSvag");
            }
        }

        /// <summary>
        /// Overståede møder står dæmpet.
        ///
        /// DE BLIVER STÅENDE DAGEN UD, fordi spørgsmålet klokken to ikke kun
        /// er «hvad mangler jeg», men også «hvad nåede jeg» — og et møde, der
        /// forsvinder, ser ud som et møde, der aldrig var der.
        ///
        /// Halv gennemsigtighed frem for en grå farve på hvert element:
        /// dæmper hele kortet på én gang, også kanten og knappen, og kan ikke
        /// komme ud af trit med resten, den dag farverne ændres.
        /// </summary>
        // 0,45 var for haardt. En overstaaet aftale skal traede i baggrunden,
        // ikke forsvinde - man spoerger ogsaa «hvad naaede jeg».
        public double Daempning => _a.ErOverstaaet(_nu) ? 0.72 : 1.0;

        private static Brush Pensel(string noegle) => Temaskift.Pensel(noegle);

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
        System.Globalization.CultureInfo.GetCultureInfo("da-DK");   // se Sprogkultur nedenfor

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
            var ugedag = dag.ToString("dddd", Sprog.Kultur);

            v.Dagsnavn = iDag ? Sprog.T("kalender.idag")
                       : iMorgen ? Sprog.T("kalender.imorgen")
                       : char.ToUpper(ugedag[0]) + ugedag[1..];

            // DATOFORMATET STAAR I SPROGFILEN. Dansk skriver «27. august»,
            // engelsk «27 August». Punktummet er ikke en detalje, man kan
            // regne sig frem til - det hoerer til sproget.
            // MAANEDEN SKRIVES HELT UD. Den var kortvarigt forkortet, af
            // frygt for at «8. september» ville skubbe dagsnavnet ud af
            // spalten. Det gjorde den ikke: da dagen og datoen kom paa samme
            // linje, blev der plads til overs, og «Tirsdag 8. september»
            // fylder under det halve af spalten.
            //
            // Opgavernes fristmaerkat er stadig forkortet. Dér er det en
            // maerkat mellem to andre ting paa den samme linje, og her er det
            // en overskrift, der har linjen for sig selv.
            v.Dagsdato = dag.ToString(Sprog.T("kalender.datoformat"), Sprog.Kultur);
        }

        Kalenderrude.ItemsSource = visninger;
        IngenAftaler.Visibility = kommende.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Overskriften sagde foer «I dag · 2». Nu staar «I DAG» som den
        // foerste dagsoverskrift inde i listen, og to steder, der siger det
        // samme, er ét sted for meget. Her staar antallet i stedet - det
        // svarer paa «hvor meget ligger der forude», som listen ikke selv
        // svarer paa, foer man har rullet den igennem.
        Kalenderoverskrift.Text = kommende.Count == 0
            ? Sprog.T("kalender.titel")
            : Sprog.T("kalender.titelmed", kommende.Count);

        VisSynktilstand();
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
        if (sender is not FrameworkElement { Tag: Aftalevisning v }) return;

        AabnAftale(v.Aftale);
    }

    /// <summary>
    /// Åbner én aftale. Ét sted, fordi der er to veje ind: kortet i
    /// Cockpittet og listen bag «Vis alle aftaler».
    /// </summary>
    private async void AabnAftale(Aftale aftalen)
    {
        var v = new Aftalevisning(aftalen, DateTimeOffset.Now);

        var vindue = new Meeting.AftaleWindow(v.Aftale) { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true) return;

        if (vindue.Slettet)
        {
            // HOS GOOGLE FOERST, LOKALT BAGEFTER. Gaar det galt hos Google,
            // staar aftalen her endnu, og man kan proeve igen. Omvendt ville
            // den vaere vaek paa skaermen og tilbage ved naeste hentning -
            // altsaa praecis den forvirring, sletningen skulle raade bod paa.
            if (!await SletHosGoogle(v.Aftale)) return;

            Kalender.Slet(v.Aftale.Id);
            VisKalender();
            return;
        }

        Kalender.Gem(vindue.Aftalen);
        VisKalender();

        await LaegOpHosGoogle(vindue);

        // OPTAGELSEN TIL SIDST. Aftalen er gemt, og er den lagt op hos
        // Google, er moedelinket kommet med tilbage - saa har optagelsen det
        // link, den skal aabne.
        if (vindue.SkalOptage) Optag(vindue.Aftalen);
    }

    /// <summary>
    /// Starter optagelsen af en aftale — eller går til den, hvis den findes.
    ///
    /// Ét sted, fordi det kaldes fra to: knappen inde i aftalevinduet og
    /// vagten, der starter af sig selv.
    /// </summary>
    private void Optag(Aftale a)
    {
        if (Window.GetWindow(this) is not MainWindow hoved) return;

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
    private void Opgave_Aabn(object sender, MouseButtonEventArgs e)
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

        AabnOpgave(v.Opgave);
    }

    /// <summary>
    /// Åbner én opgave. Ét sted, fordi der er to veje ind: kortet i
    /// Cockpittet og listen bag «Vis alle opgaver».
    /// </summary>
    private void AabnOpgave(Registeropgave r)
    {
        var vindue = new OpgaveWindow(r) { Owner = Window.GetWindow(this) };
        vindue.ShowDialog();

        // Ogsaa naar den er slettet. Listen skal tegnes om, uanset om
        // opgaven blev rettet eller fjernet - ellers staar den, man lige har
        // slettet, og ser ud som om den er der endnu.
        if (vindue.Gemt || vindue.Slettet) VisOpgaver();
    }

    /// <summary>
    /// En opgave, man selv skriver.
    /// </summary>
    /// <remarks>
    /// DEN SKAL KUNNE LAVES UDEN ET MØDE OG UDEN EN INTEGRATION — samme
    /// begrundelse som «Ny aftale» ved siden af kalenderen. Indtil nu kunne
    /// Cockpittet kun VISE opgaver: dem, appen selv havde fundet i en
    /// transkription, og dem, der lå i Google Tasks. Alt andet skulle skrives
    /// et andet sted, og så er listen her ikke længere den ene liste, den er
    /// bygget for at være.
    ///
    /// Den bruger det samme vindue som en opgave, der rettes. Felterne, den
    /// måde der gemmes på, og vejen op til Google er de samme — se
    /// <see cref="OpgaveWindow"/>.
    /// </remarks>
    private void NyOpgave_Klik(object sender, RoutedEventArgs e)
    {
        var vindue = OpgaveWindow.Ny();
        vindue.Owner = Window.GetWindow(this);

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

            // NOTEN KUN NAAR MOEDET SKAL OPTAGES. Aftalens eget hak afgoer
            // det - en indkaldelse, der varsler en optagelse, der aldrig
            // kommer, er en, folk holder op med at laese.
            var svar = await Googlekalender.OpretAsync(
                vindue.Aftalen, noegle, vindue.SkalHaveMeet,
                medNote: vindue.Aftalen.OptagAutomatisk);

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
                "Den er gemt i HeyPia. Det var oplægningen hos Google, der ikke "
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

        // ============ SAMME OVERSKRIFT SOM KALENDEREN ============
        //
        // Kalenderen ved siden af skriver «Kalender - 4». Her stod et tal med
        // svag skrift ude i hoejre side i stedet - «6 i alt fra 1
        // optagelser» - og det var baade ulaeseligt og forkert: opgaver
        // skrevet i haanden eller hentet fra Google kommer ikke fra en
        // optagelse. Se kommentaren i XAML'en.
        //
        // Haster noget, siger overskriften DET. Saa er antallet af aabne det
        // mindst vigtige tal paa skaermen, og de haastende staar i forvejen
        // med roed dato paa hvert kort.
        OpgaveOverskrift.Text = aabne.Count == 0
            ? Sprog.T("opgaver.ingen")
            : haster > 0
                ? Sprog.T("opgaver.haster", haster)
                : Sprog.T("opgaver.aabne") + " · " + aabne.Count;

        VisSynktilstand();
    }

    // ==================== HENT FRA GOOGLE, MANUELT ====================
    //
    // Der hentes IKKE af sig selv. En aftale, der oprettes direkte i Google,
    // er ikke i HeyPia, foer nogen henter den - og det laa foer kun under
    // Indstillinger, hvor ingen leder efter det.
    //
    // Ikonet staar kun, naar der ER noget at hente fra. En knap, der ikke kan
    // andet end at sige "ingen kalender er forbundet", er stoej.

    private bool _synker;

    /// <summary>
    /// Lytter efter den automatiske hentning.
    ///
    /// Uden det ville en aftale, der kom ind kl. 10.15, først dukke op, når
    /// man skiftede skærm frem og tilbage — og så ville hentningen være
    /// usynlig lige dér, hvor den skulle gøre en forskel.
    /// </summary>
    private void LytEfterSynk()
    {
        Jobs.Synkvagt.Hentet += Synkkom;
        Sprog.Aendret += Sprogskiftet;

        Unloaded += (_, _) =>
        {
            Jobs.Synkvagt.Hentet -= Synkkom;
            Sprog.Aendret -= Sprogskiftet;
        };
    }

    /// <summary>
    /// Sproget er skiftet. De tekster, der er BUNDET i XAML, skifter af sig
    /// selv; dem, der bygges her i koden — overskrifter med tal i — skal
    /// skrives om.
    /// </summary>
    private void Sprogskiftet()
    {
        VisKalender();
        VisOpgaver();
    }

    private void Synkkom()
    {
        // Der hentes udenom brugeren, saa listerne skal bygges forfra. Det er
        // billigt: begge laeser een fil hver.
        VisKalender();
        VisOpgaver();
    }

    private void VisSynktilstand()
    {
        var kalender = Synkronisering.Kalenderklar;
        var opgaver = Synkronisering.Opgaverklar;

        Kalendersynk.Visibility = kalender ? Visibility.Visible : Visibility.Collapsed;
        Kalendersynklinje.Visibility = kalender ? Visibility.Visible : Visibility.Collapsed;

        Opgavesynk.Visibility = opgaver ? Visibility.Visible : Visibility.Collapsed;
        Opgavesynklinje.Visibility = opgaver ? Visibility.Visible : Visibility.Collapsed;

        if (_synker) return;

        if (kalender) Kalendersynklinje.Text = Synkronisering.Siden(Synkronisering.SidstKalender());
        if (opgaver) Opgavesynklinje.Text = Synkronisering.Siden(Synkronisering.SidstOpgaver());
    }

    private async void Kalendersynk_Klik(object sender, RoutedEventArgs e)
    {
        if (_synker) return;

        _synker = true;
        Kalendersynk.IsEnabled = false;
        Kalendersynklinje.Text = Sprog.T("kalender.henter");

        try
        {
            var svar = await Synkronisering.Kalenderen();

            // Listen bygges forfra UANSET udfaldet. Kom der aftaler ind, skal
            // de ses; kom der ingen, skal linjen alligevel rettes.
            _synker = false;
            VisKalender();

            Kalendersynklinje.Text = svar.Lykkedes
                ? $"{svar.Besked} · {Synkronisering.Siden(Synkronisering.SidstKalender())}"
                : svar.Besked;
        }
        catch (Exception ex)
        {
            _synker = false;
            Kalendersynklinje.Text = ex.Message;
        }
        finally
        {
            _synker = false;
            Kalendersynk.IsEnabled = true;
        }
    }

    private async void Opgavesynk_Klik(object sender, RoutedEventArgs e)
    {
        if (_synker) return;

        _synker = true;
        Opgavesynk.IsEnabled = false;
        Opgavesynklinje.Text = Sprog.T("kalender.henter");

        try
        {
            var svar = await Synkronisering.Opgaverne();

            _synker = false;
            VisOpgaver();

            Opgavesynklinje.Text = svar.Lykkedes
                ? $"{svar.Besked} · {Synkronisering.Siden(Synkronisering.SidstOpgaver())}"
                : svar.Besked;
        }
        catch (Exception ex)
        {
            _synker = false;
            Opgavesynklinje.Text = ex.Message;
        }
        finally
        {
            _synker = false;
            Opgavesynk.IsEnabled = true;
        }
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
        // FANEN HAVDE SINE EGNE FARVER, valgt til det moerke tema. I det
        // lyse blev den valgte fane en moerk plet paa et hvidt kort, og den
        // fravalgte tekst laa paa 2,4:1. De hoerer til det samme sted som
        // resten: paletten.
        public Brush Flade => Temaskift.Pensel(Valgt ? "Valgt" : "Trykket");
        public Brush Kant => Temaskift.Pensel(Valgt ? "Accent" : "PanelKant");
        public Brush Skrift => Temaskift.Pensel(Valgt ? "Tekst" : "TekstMeget");
    }

    /// <summary>
    /// Sletter aftalen hos Google, hvis den kom derfra.
    /// </summary>
    /// <returns>Sandt, når der kan slettes lokalt bagefter.</returns>
    /// <remarks>
    /// DEN SIGER TIL, NÅR DET GÅR GALT — modsat afkrydsningen på en opgave,
    /// som med vilje er tavs. Forskellen er, hvad brugeren kan gøre ved det:
    /// en afkrydsning, der ikke nåede frem, retter sig selv ved næste
    /// hentning, men en sletning gør ikke. Aftalen ville komme igen, og så
    /// står man og sletter den samme aftale hver dag uden at forstå hvorfor.
    ///
    /// Er der ingen nøgle, slettes der bare lokalt. Så er integrationen ikke
    /// sat op, og der er ikke noget derude at slette.
    /// </remarks>
    private async Task<bool> SletHosGoogle(Aftale aftale)
    {
        if (aftale.Kilde != Kalenderkilde.Google || aftale.FremmedId.Length == 0) return true;

        try
        {
            var noegle = Integrationsfiler.Hent(Googlekalender.Id).Opdateringsnoegle;
            if (noegle.Length == 0) return true;

            await Googlekalender.SletAsync(aftale.FremmedId, noegle);
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Aftalen blev ikke slettet hos Google",
                "Den står her endnu, så du kan prøve igen. " + ex.Message,
                Dialogs.Slags.Pas_paa);

            return false;
        }
    }

    /// <summary>
    /// Åbner prioritetsmenuen ved et almindeligt venstreklik.
    /// </summary>
    /// <remarks>
    /// EN ContextMenu ÅBNER NORMALT PÅ HØJREKLIK. Den er brugt her, fordi WPF
    /// selv lukker den ved klik udenfor og ved Escape, og fordi den tegnes med
    /// appens egen menu-stil — men den skal åbne på venstreklik, for det er
    /// dét, man gør ved en knap.
    ///
    /// PlacementTarget skal sættes i hånden. Uden den åbner menuen ved musen
    /// og ikke ved ringen, og så lander den et andet sted, alt efter hvor i
    /// ringen man ramte.
    /// </remarks>
    private void Prioritet_Klik(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button knap || knap.ContextMenu is null) return;

        knap.ContextMenu.PlacementTarget = knap;
        knap.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        knap.ContextMenu.IsOpen = true;
    }

    /// <summary>
    /// Der blev valgt en prioritet i menuen.
    /// </summary>
    /// <remarks>
    /// OPGAVEN HENTES FRA KNAPPEN, ikke fra menupunktet. En ContextMenu ligger
    /// i sit eget vindue og er ikke barn af knappen i den visuelle træstruktur
    /// — DataContext arves derfor ikke. PlacementTarget er vejen tilbage til
    /// det kort, menuen hører til.
    /// </remarks>
    private void Prioritet_Valgt(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not MenuItem punkt) return;
        if (punkt.Parent is not ContextMenu menu) return;
        if (menu.PlacementTarget is not FrameworkElement { Tag: Opgavevisning v }) return;
        if (!int.TryParse(punkt.Tag as string, out var pri)) return;

        // Saetteren gemmer og tegner listen om, saa ringen faar sit nye tal.
        v.Prioritetsvalg = pri;
    }

    // ==================== VIDSTE DU AT … ====================

    private DispatcherTimer? _tipsur;
    private int _tipsnr;

    /// <summary>
    /// Starter tipsbåndet — eller lader være, hvis det er slået fra.
    /// </summary>
    /// <remarks>
    /// TOLV SEKUNDER MELLEM HVERT. Kortere, og det bliver en ting, der
    /// bevæger sig i øjenkrogen, mens man læser noget andet; længere, og man
    /// ser det samme tip hver gang, man åbner Cockpittet.
    ///
    /// DER STARTES ET TILFÆLDIGT STED I LISTEN. Ellers ville det første tip
    /// være det samme hver eneste dag, og de sidste i listen ville aldrig
    /// blive læst.
    /// </remarks>
    private void StartTips()
    {
        if (AppSettings.Current.TipsSlaaetFra || Tips.Alle().Count == 0)
        {
            Tipsbaand.Visibility = Visibility.Collapsed;
            return;
        }

        Tipsbaand.Visibility = Visibility.Visible;

        _tipsnr = Random.Shared.Next(Tips.Alle().Count);
        VisTip(toning: false);

        _tipsur?.Stop();
        _tipsur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _tipsur.Tick += (_, _) =>
        {
            _tipsnr = (_tipsnr + 1) % Tips.Alle().Count;
            VisTip(toning: true);
        };
        _tipsur.Start();
    }

    /// <summary>
    /// Skriver tippet — med en blød toning, når det er et skift.
    /// </summary>
    /// <remarks>
    /// TEKSTEN SKIFTES MIDT I TONINGEN og ikke før den. Gøres det før, ser man
    /// det nye tip springe frem i fuld styrke og derefter tone ud og ind igen
    /// — altså det modsatte af et blødt skift.
    ///
    /// Første visning toner ikke. Et bånd, der toner sig selv frem, når man
    /// åbner skærmen, er en bevægelse, ingen har bedt om.
    /// </remarks>
    private void VisTip(bool toning)
    {
        var tip = Tips.Alle()[_tipsnr];

        if (!toning)
        {
            Saettip(tip);
            return;
        }

        var ud = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        ud.Completed += (_, _) =>
        {
            Saettip(tip);

            Tipsindhold.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                });
        };

        Tipsindhold.BeginAnimation(OpacityProperty, ud);
    }

    private void Saettip(Tip tip)
    {
        Tipstekst.Text = " " + tip.Tekst;
        Tipslink.Tag = tip.Afsnit;
    }

    /// <summary>Åbner hjælpen på det afsnit, tippet kom fra.</summary>
    private void Tipslaesmere_Klik(object sender, RoutedEventArgs e)
    {
        var afsnit = (sender as FrameworkElement)?.Tag as string ?? "";

        try
        {
            // Aabn og ikke new: haandbogen er EET vindue. Er den aabne
            // allerede, springer den bare til afsnittet.
            Help.HjaelpWindow.Aabn(Window.GetWindow(this), afsnit);
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Hjælpen kunne ikke åbnes",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>
    /// Slår båndet fra — for altid, indtil man selv slår det til igen.
    /// </summary>
    /// <remarks>
    /// DER SPØRGES IKKE. Man trykker på et kryds for at få noget væk, ikke
    /// for at få et spørgsmål. Hvor det tændes igen, står i beskeden.
    /// </remarks>
    private void Tipsluk_Klik(object sender, RoutedEventArgs e)
    {
        _tipsur?.Stop();
        _tipsur = null;

        Tipsbaand.Visibility = Visibility.Collapsed;

        try
        {
            AppSettings.Current.TipsSlaaetFra = true;
            AppSettings.Current.Save();
        }
        catch (Exception)
        {
            // Kan det ikke gemmes, er det slaaet fra resten af sessionen.
            // Det er den mindst irriterende maade at fejle paa.
        }
    }

    // ==================== HELE LISTEN ====================

    /// <summary>Månedens navn med år — «september 2026». Til grupperingen.</summary>
    private static string Maaned(DateTimeOffset d) =>
        d.LocalDateTime.ToString("MMMM yyyy", new System.Globalization.CultureInfo("da-DK"));

    /// <summary>
    /// Åbner hele kalenderen.
    /// </summary>
    /// <remarks>
    /// DER LÆSES FORFRA. Ruden i Cockpittet har kun de nærmeste, og vinduet
    /// skal have dem alle — også dem, der ligger bagud.
    /// </remarks>
    private void AlleAftaler_Klik(object sender, RoutedEventArgs e)
    {
        List<Aftale> alle;
        try { alle = Kalender.Alle(); }
        catch (Exception) { alle = new List<Aftale>(); }

        var nu = DateTimeOffset.Now;

        var punkter = alle
            .OrderBy(a => a.Start)
            .Select(a => new Oversigtspunkt(
                Maaned(a.Start),
                a.Start.LocalDateTime.ToString("ddd d. HH:mm"),
                a.Titel,
                Aftaleunder(a),
                $"{a.Titel}\n\n{a.Start.LocalDateTime:dddd d. MMMM yyyy 'kl.' HH:mm}",
                Temaskift.Pensel(a.Slutter < nu ? "Slukket"
                               : a.MoedeId.Length > 0 ? "Godkendt" : "Accent"),
                a))
            .ToList();

        var vindue = new Oversigtsvindue(
            Sprog.T("oversigt.alle_aftaler"),
            Sprog.T("oversigt.alle_aftaler_under"),
            punkter,
            k => { if (k is Aftale a) AabnAftale(a); })
        {
            Owner = Window.GetWindow(this)
        };

        vindue.ShowDialog();
    }

    private static string Aftaleunder(Aftale a)
    {
        var dele = new List<string>();

        if (a.Sted.Length > 0) dele.Add(a.Sted);
        else if (a.Link.Length > 0) dele.Add("online");

        if (a.Kilde != Kalenderkilde.Lokal) dele.Add(a.Kilde.ToString());
        if (a.MoedeId.Length > 0) dele.Add("optaget");

        return string.Join("  ·  ", dele);
    }

    /// <summary>
    /// Åbner hele opgavelisten — også de færdige.
    /// </summary>
    /// <remarks>
    /// DE FÆRDIGE ER MED HER OG IKKE I COCKPITTET. Ruden dér er «hvad skal
    /// jeg gøre»; det her er «hvad har jeg haft». En afkrydset opgave fra i
    /// forgårs er ikke støj, når man leder efter den — den er svaret.
    /// </remarks>
    private void AlleOpgaver_Klik(object sender, RoutedEventArgs e)
    {
        List<Registeropgave> alle;
        try { alle = Opgaveregister.Alle(); }
        catch (Exception) { alle = new List<Registeropgave>(); }

        var idag = DateOnly.FromDateTime(DateTime.Today);

        var punkter = alle
            .OrderBy(r => r.Opgave.Deadline ?? DateTimeOffset.MaxValue)
            .Select(r => new Oversigtspunkt(
                r.Opgave.Deadline is { } d ? Maaned(d) : Sprog.T("oversigt.uden_frist"),
                r.Opgave.Deadline is { } f
                    ? f.LocalDateTime.ToString("ddd d.")
                    : "",
                r.Opgave.Visningsnavn,
                Opgaveunder(r),
                r.Opgave.HarMere ? $"{r.Opgave.Visningsnavn}\n\n{r.Opgave.Tekst.Trim()}"
                                 : r.Opgave.Visningsnavn,
                Temaskift.Pensel(r.Opgave.Faerdig ? "Slukket" : Hastefarve(r, idag)),
                r))
            .ToList();

        var vindue = new Oversigtsvindue(
            Sprog.T("oversigt.alle_opgaver"),
            Sprog.T("oversigt.alle_opgaver_under"),
            punkter,
            k => { if (k is Registeropgave r) AabnOpgave(r); })
        {
            Owner = Window.GetWindow(this)
        };

        vindue.ShowDialog();
    }

    private static string Hastefarve(Registeropgave r, DateOnly idag) => r.Hastighed(idag) switch
    {
        Hastighed.Overskredet => "Optager",
        Hastighed.I_dag => "Advarsel",
        Hastighed.Denne_uge => "Advarsel",
        Hastighed.Senere => "Godkendt",
        _ => "PanelKant"
    };

    private static string Opgaveunder(Registeropgave r)
    {
        var dele = new List<string>();

        if (r.Opgave.Faerdig) dele.Add("gjort");
        if (r.Opgave.Prioritet is >= 1 and <= 3) dele.Add($"prioritet {r.Opgave.Prioritet}");
        if (r.Opgave.Herkomst == Opgavekilde.Google) dele.Add("Google Tasks");
        if (r.Moedetitel.Length > 0) dele.Add(r.Moedetitel);

        return string.Join("  ·  ", dele);
    }

    // ==================== TRAEK EN OPGAVE PAA PLADS ====================

    /// <summary>Hvor musen blev trykket ned, og på hvad.</summary>
    /// <remarks>
    /// DER SKAL BAADE VAERE ET STARTPUNKT OG EN AFSTAND. Hele kortet er
    /// klikbart — et klik aabner opgaven — og uden en mindsteafstand ville
    /// enhver lille rysten paa haanden starte et traek i stedet for at aabne.
    /// Windows' egen graense er den rigtige at bruge; det er den, alt andet
    /// paa maskinen bruger.
    /// </remarks>
    private Point _traekstart;
    private Opgavevisning? _traekker;

    private void Opgave_Museknap(object sender, MouseButtonEventArgs e)
    {
        _traekstart = e.GetPosition(this);
        _traekker = (sender as FrameworkElement)?.Tag as Opgavevisning;
    }

    private void Opgave_Musbevaegelse(object sender, MouseEventArgs e)
    {
        if (_traekker is null || e.LeftButton != MouseButtonState.Pressed) return;

        var nu = e.GetPosition(this);

        if (Math.Abs(nu.X - _traekstart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(nu.Y - _traekstart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var det = _traekker;
        _traekker = null;

        // DoDragDrop BLOKERER, indtil der slippes, og den spiser museknappen
        // op undervejs — saa Opgave_Aabn kommer ikke bagefter.
        DragDrop.DoDragDrop((DependencyObject)sender,
            new DataObject(typeof(Opgavevisning), det), DragDropEffects.Move);

        // DoDragDrop vender først tilbage, når der er sluppet eller fortrudt.
        // Det er den ENESTE besked, der kommer i begge tilfælde — en Escape
        // eller et slip uden for listen giver hverken Drop eller DragLeave.
        SlukStreg();
    }

    /// <summary>
    /// Viser, hvor den lander — som en streg over eller under kortet.
    /// </summary>
    /// <remarks>
    /// FOERSTE UDGAVE GJORDE KORTETS EGEN KANT TYKKERE, og det var for lidt:
    /// kortene har allerede en kant, og tre pixels mere paa den ene side er
    /// ikke noget, man opdager, mens man holder musen nede.
    ///
    /// Nu en streg paa tvaers i accentfarven. Den siger «her lander den» og
    /// ikke «det her kort er markeret» — og det er dét, man staar og leder
    /// efter, mens man traekker.
    ///
    /// DEN NEDERSTE BRUGES KUN PAA DET SIDSTE KORT. Ellers ville to naboer
    /// vise den samme plads to gange: bunden af den ene og toppen af den
    /// naeste er det samme sted i listen.
    /// </remarks>
    private void Opgave_TraekkesHen(object sender, DragEventArgs e)
    {
        if (sender is not Border kort) return;

        e.Effects = e.Data.GetDataPresent(typeof(Opgavevisning))
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;

        if (e.Effects == DragDropEffects.None) return;

        var under = e.GetPosition(kort).Y >= kort.ActualHeight / 2;
        var sidste = ErSidste(kort);

        // Er man i den nederste halvdel af et kort, der IKKE er det sidste,
        // er landingspladsen den samme som toppen af det naeste. Stregen
        // vises derfor dér, saa den ikke hopper mellem to udgaver af det
        // samme sted.
        var linje = under
            ? sidste
                ? Streg(kort, "Indsaetefter")
                : Naboen(kort) is { } naeste ? Streg(naeste, "Indsaetfoer") : null
            : Streg(kort, "Indsaetfoer");

        Taend(linje);
    }

    /// <summary>
    /// Den streg, der lyser lige nu. Højst én ad gangen.
    /// </summary>
    /// <remarks>
    /// ============ FØR SLUKKEDE NABOEN DEN, KORTET LIGE HAVDE TÆNDT ============
    ///
    /// Hvert kort tændte sine egne streger i DragOver og slukkede dem i
    /// DragLeave. Men går man fra bunden af ét kort til toppen af det næste,
    /// er det DEN SAMME streg, de to kort er enige om at vise — og
    /// rækkefølgen af DragOver og DragLeave er ikke givet. Naboens DragLeave
    /// kunne derfor slukke det, kortet lige havde tændt. Den blinkede.
    ///
    /// Nu er der ét sted, der ved, hvad der lyser. Er svaret det samme som
    /// sidst, røres der ingenting — og så kan der ikke blinke noget.
    /// </remarks>
    private Border? _lysendeStreg;

    private void Taend(Border? streg)
    {
        if (ReferenceEquals(_lysendeStreg, streg)) return;

        if (_lysendeStreg is not null) _lysendeStreg.Visibility = Visibility.Collapsed;

        _lysendeStreg = streg;

        if (streg is not null) streg.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Slukker stregen, uanset hvilket kort den sidder på.
    /// </summary>
    /// <remarks>
    /// Kaldes to steder: når der SLIPPES, og når trækket er forbi — også hvis
    /// det blev fortrudt med Escape eller sluppet uden for listen.
    /// <c>DoDragDrop</c> vender først tilbage dér, og det er den eneste
    /// besked, der kommer i alle tilfælde.
    /// </remarks>
    private void SlukStreg() => Taend(null);

    /// <summary>
    /// Finder en af stregerne på ét kort.
    /// </summary>
    /// <remarks>
    /// Der gås gennem det TEGNEDE kort og ikke gennem FindName. En
    /// DataTemplate laver ét sæt elementer pr. række, og et x:Name derinde
    /// kan ikke slås op udefra — der er tyve elementer med det navn, ét pr.
    /// opgave. Vejen er den visuelle træstruktur, hvor hvert kort kun kan se
    /// sine egne.
    /// </remarks>
    private static Border? Streg(Border kort, string navn)
    {
        if (VisualTreeHelper.GetParent(kort) is not Grid ramme) return null;

        foreach (var barn in ramme.Children)
            if (barn is Border b && b.Name == navn)
                return b;

        return null;
    }

    /// <summary>Kortet lige under dette — eller <c>null</c>, hvis det er det sidste.</summary>
    private static Border? Naboen(Border kort)
    {
        if (VisualTreeHelper.GetParent(kort) is not Grid ramme) return null;
        if (VisualTreeHelper.GetParent(ramme) is not ContentPresenter plads) return null;
        if (VisualTreeHelper.GetParent(plads) is not Panel stak) return null;

        var nr = stak.Children.IndexOf(plads);
        if (nr < 0 || nr + 1 >= stak.Children.Count) return null;

        return Kortet(stak.Children[nr + 1]);
    }

    private static bool ErSidste(Border kort) => Naboen(kort) is null;

    /// <summary>Selve kortet inde i en række — den Border, der har et Tag.</summary>
    private static Border? Kortet(DependencyObject rod)
    {
        if (rod is ContentPresenter && VisualTreeHelper.GetChildrenCount(rod) == 1)
            rod = VisualTreeHelper.GetChild(rod, 0);

        if (rod is not Grid ramme) return null;

        foreach (var barn in ramme.Children)
            if (barn is Border b && b.Tag is Opgavevisning)
                return b;

        return null;
    }

    private void Opgave_Sluppet(object sender, DragEventArgs e)
    {
        SlukStreg();
        e.Handled = true;

        if (sender is not Border kort) return;
        if (e.Data.GetData(typeof(Opgavevisning)) is not Opgavevisning flyttet) return;
        if (kort.Tag is not Opgavevisning maal || ReferenceEquals(maal, flyttet)) return;

        if (Opgaverude.ItemsSource is not List<Opgavevisning> liste) return;

        var raekken = liste.Select(v => v.Id).ToList();

        var fra = raekken.IndexOf(flyttet.Id);
        if (fra < 0) return;

        raekken.RemoveAt(fra);

        var til = raekken.IndexOf(maal.Id);
        if (til < 0) return;

        // Over eller under det kort, der blev sluppet paa. Uden den skelnen
        // kan man ikke laegge noget nederst i listen.
        if (e.GetPosition(kort).Y >= kort.ActualHeight / 2) til++;

        raekken.Insert(til, flyttet.Id);

        try
        {
            Opgaveregister.SaetRaekkefoelge(raekken);
        }
        catch (Exception ex)
        {
            Historik.Skriv(HaendelseType.Andet, "Rækkefølgen kunne ikke gemmes",
                ex.Message, Udfald.SeEfter);
        }

        VisOpgaver();
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
            new(Sprog.T("cockpit.heletiden"), null),
            new("I dag", "idag"),
            new("Denne uge", "uge"),
            new("Denne måned", "maaned"),
            new("I år", "aar"),
            new("Fra og til …", "valgt")
        };
        Periodefilter.SelectedIndex = 0;

        var mapper = new List<Filterpunkt> { new(Sprog.T("cockpit.allemapper"), null) };
        var typer = new List<Filterpunkt> { new("Alle mødetyper", null) };
        var sprog = new List<Filterpunkt> { new(Sprog.T("cockpit.allesprog"), null) };

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

    /// <summary>Opgavens id. Til traek og slip, som flytter på id'er og ikke på kort.</summary>
    public Guid Id => _r.Opgave.Id;

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

    /// <summary>
    /// Sender en afkrydsning videre til Google. Sker i baggrunden.
    ///
    /// GÅR DET GALT, SIGES DER INGENTING. Fluebenet er sat lokalt og gemt;
    /// en fejlbesked om noget, brugeren allerede har set virke, er støj. Næste
    /// hentning retter forskellen — og der ER ingen forskel at rette, hvis
    /// nettet bare var væk et øjeblik.
    /// </summary>
    private static async void Sendfaerdig(Opgave o)
    {
        if (o.Herkomst != Opgavekilde.Google) return;

        try
        {
            var noegle = Integrationsfiler.Hent(Googleopgaver.Id).Opdateringsnoegle;
            if (noegle.Length == 0) return;

            await Googleopgaver.SaetFaerdigAsync(o, noegle);
        }
        catch (Exception)
        {
            // Se doc-kommentaren. I stilhed, med vilje.
        }
    }

    /// <summary>Opgaven bag visningen — til vinduet, der åbner den.</summary>
    public Registeropgave Opgave => _r;

    /// <summary>
    /// En opgave, der er krydset af i dag, står dæmpet.
    ///
    /// DEN BLIVER STÅENDE DAGEN UD. En opgave, der forsvinder i det sekund,
    /// man sætter fluebenet, giver ingen kvittering: man ved ikke, om man
    /// ramte den rigtige, og man kan ikke fortryde uden at lede efter den.
    /// I morgen er den væk.
    ///
    /// Fluebenet kan stadig trykkes — Opacity slår ikke klik fra. Det er
    /// netop dét, der gør fortrydelsen mulig.
    /// </summary>
    public double Daempning => _r.Opgave.Faerdig ? 0.72 : 1.0;

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

            // «Google Tasks» og ikke listens navn alene. «Mine opgaver» siger
            // ikke, hvor den kom fra - og det er dét, man vil vide, naar man
            // ser en opgave, man ikke husker at have skrevet i appen.
            if (_r.Opgave.Herkomst == Opgavekilde.Google)
                dele.Add("Google Tasks");

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

    /// <summary>
    /// Rød, gul eller grøn — efter hvor meget der er til fristen.
    ///
    /// DEN HER ER STRIBEN i venstre kant. Den fylder mange pixler og må gerne
    /// være mættet. Til skrift er den for mørk — se Fristfarve nedenfor.
    /// </summary>
    public Brush Farve => _r.Hastighed(_idag) switch
    {
        Hastighed.Overskredet => Pensel("Optager"),
        Hastighed.I_dag => Pensel("Advarsel"),
        Hastighed.Denne_uge => Pensel("Advarsel"),
        Hastighed.Senere => Pensel("Godkendt"),
        _ => Pensel("PanelKant")
    };

    /// <summary>
    /// Samme besked som striben — men skrevet, så det kan læses.
    ///
    /// Fristmærkaten stod før i stribens farve: elleve pixels tekst på den
    /// mørke pille #2C323D. Tre af de fire faldt igennem — «senere» ramte
    /// 4,13:1 og «ingen frist» 1,26:1. En dato, man ikke kan læse, er værre
    /// end ingen dato: mærkaten ser ud, som om den siger noget.
    ///
    /// Rækkefølgen er den samme, så striben og teksten aldrig kan komme til
    /// at sige hver sit. Ingen af dem går under 5:1.
    /// </summary>
    public Brush Fristfarve => _r.Hastighed(_idag) switch
    {
        Hastighed.Overskredet => Pensel("FejlTekst"),
        Hastighed.I_dag => Pensel("Advarsel"),
        Hastighed.Denne_uge => Pensel("Advarsel"),
        Hastighed.Senere => Pensel("Godkendt"),
        _ => Pensel("TekstMeget")
    };

    /// <summary>
    /// Farven fra paletten. Tog før en hex-streng.
    /// </summary>
    /// <remarks>
    /// DE HEX-VÆRDIER, DER STOD HER, VAR VALGT TIL DET MØRKE TEMA ALENE. Det
    /// stod endda i kommentarerne — «elleve pixels tekst på den mørke pille
    /// #2C323D» — og der blev regnet omhyggeligt på kontrasten mod netop den
    /// flade. Men en farve, der ikke kommer fra paletten, følger ikke med, når
    /// temaet skifter: den lysegrønne #7EDCA0 lå på 1,6:1 mod et hvidt kort,
    /// og fristen «16. september» kunne ikke læses i det lyse tema.
    ///
    /// Nu slås de op på navn. Så gælder <c>TemaTest</c> også dem, og de kan
    /// ikke blive tilbage i det ene tema.
    /// </remarks>
    private static Brush Pensel(string noegle) => Temaskift.Pensel(noegle);

    public bool Faerdig
    {
        get => _r.Opgave.Faerdig;
        set
        {
            if (_r.Opgave.Faerdig == value) return;

            // SaetFaerdig og ikke Faerdig = value: datoen skal foelge med,
            // saa opgaven kan blive staaende daempet dagen ud.
            _r.Opgave.SaetFaerdig(value);
            Opgaveregister.Gem(_r);

            // ER DEN FRA GOOGLE, SKAL DET OGSAA SIGES DÉR.
            //
            // Et flueben, der kun virker i HeyPia, er vaerre end ingen:
            // opgaven staar stadig paa telefonen, og saa holder man op med at
            // stole paa begge lister.
            //
            // Det sker i baggrunden. Fluebenet er sat lokalt, og det maa ikke
            // vente paa nettet - gaar det galt, staar afkrydsningen her, og
            // naeste hentning retter den.
            Sendfaerdig(_r.Opgave);
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

    /// <summary>
    /// Det, der står i boblen, når musen hviler på kortet.
    /// </summary>
    /// <remarks>
    /// KORTET VISER DET KORTE, BOBLEN DET HELE. Titlen er klippet til to
    /// linjer på kortet, herkomsten er taget helt af, og fristen står som en
    /// dato uden årstal. Alt det står her — sammen med det, kortet aldrig har
    /// vist: hele opgaveteksten.
    ///
    /// Det er dét, en boble er til: mere om det, man peger på, uden at det
    /// fylder, når man ikke peger.
    /// </remarks>
    public string Boble
    {
        get
        {
            var linjer = new List<string> { _r.Opgave.Visningsnavn };

            if (_r.Opgave.HarMere) linjer.Add(_r.Opgave.Tekst.Trim());

            var under = new List<string>();

            if (Frist.Length > 0) under.Add(Frist);

            if (_r.Opgave.Prioritet is >= 1 and <= 3)
                under.Add($"prioritet {_r.Opgave.Prioritet}");

            under.Add(Under);

            linjer.Add(string.Join("  ·  ", under.Where(x => x.Length > 0)));
            linjer.Add("Klik for at åbne opgaven. Træk for at flytte den i listen.");

            return string.Join("\n\n", linjer.Where(l => l.Length > 0));
        }
    }

    /// <summary>Tallet i ringen. Nul er «ikke sat» og vises som en tankestreg.</summary>
    public string Prioritetstekst =>
        _r.Opgave.Prioritet is >= 1 and <= 3 ? _r.Opgave.Prioritet.ToString() : "–";



    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void Meld(string navn) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(navn));
}

/// <summary>Ét søgeord og hvor meget der er af det. Til den tomme skærm.</summary>
    /// <param name="RetVis">
    /// Synlig for ord, der IKKE står nogen steder. Det er dem, der som regel
    /// er hørt forkert — se <see cref="Ret_Klik"/>.
    /// </param>
    public sealed record Ordvisning(string Ord, string Hvor, Visibility RetVis);

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

        // En rettelsesrude, der bliver staaende paa et ord fra den forrige
        // soegning, retter det forkerte ord.
        Retrude.Visibility = Visibility.Collapsed;
        _retter = null;

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
                      $"{(e.Kilder == 1 ? "kilde" : "kilder")}",

                // ============ «INGEN STEDER» ER ET FINGERPEG ============
                //
                // Et ord, der ikke staar ét eneste sted i alt, appen har
                // skrevet ud, er som regel ikke et sjaeldent ord - det er et
                // ord, der er hoert forkert. «Storistech» staar ingen steder;
                // «StorageTek» staar tre.
                //
                // Derfor tilbydes rettelsen praecis dér og ikke paa alle ord.
                // En knap ved hvert ord ville vaere stoej; en knap ved dét
                // ord, der staar i vejen, er et svar.
                e.Steder == 0 ? Visibility.Visible : Visibility.Collapsed))
            .ToList();
    }

    /// <summary>Det ord, der er ved at blive rettet.</summary>
    private string? _retter;

    /// <summary>
    /// Lærer ordbogen, hvad ordet skulle have været.
    /// </summary>
    /// <remarks>
    /// MAN OPDAGER FEJLEN HER, OG DET ER HER, DEN SKAL KUNNE RETTES.
    ///
    /// Ordbogen kunne kun læres op på sin egen skærm — man skulle huske
    /// stavemåden, gå derhen og skrive den ind. En rettelse, der kræver, at
    /// man forlader dét, man er i gang med, bliver ikke lavet.
    ///
    /// Nu står den, hvor fejlen ses: i søgefeltets opdeling, ved de ord, der
    /// ikke findes nogen steder. Brugerens ord 31-08-2026: «man klikker på
    /// ordet og skriver, hvad det skulle have været, og på den måde lærer man
    /// løsningen op, mens man arbejder i den».
    /// </remarks>
    private void Ret_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string forkert) return;

        _retter = forkert;

        Rettitel.Text = Core.Sprog.T("searchview.ret_titel", forkert);
        Retsvar.Text = "";
        Retfelt.Text = "";
        Retrude.Visibility = Visibility.Visible;

        Retfelt.Focus();
    }

    private void Retfelt_Tast(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;

        GemRettelsen();
        e.Handled = true;
    }

    private void RetGem_Klik(object sender, RoutedEventArgs e) => GemRettelsen();

    /// <summary>
    /// Skriver rettelsen i ordbogen.
    /// </summary>
    /// <remarks>
    /// TO TING SKER PÅ ÉN GANG. Det rigtige ord lægges i ordbogen, hvis det
    /// ikke står der, og det forkerte lægges som alias. Uden det første ville
    /// aliasset pege på et ord, der ikke findes; uden det andet ville den
    /// samme fejl komme igen i morgen.
    /// </remarks>
    private void GemRettelsen()
    {
        if (_retter is not { } forkert) return;

        var rigtigt = (Retfelt.Text ?? "").Trim();
        if (rigtigt.Length == 0) return;

        try
        {
            Ordbibliotek.Tilfoej(rigtigt);

            Retsvar.Text = Ordbibliotek.TilfoejAlias(rigtigt, forkert)
                ? Core.Sprog.T("searchview.ret_lagt", forkert, rigtigt)
                : Core.Sprog.T("searchview.ret_kan_ikke", forkert);
        }
        catch (Exception ex)
        {
            Retsvar.Text = Core.Sprog.T("diktering.gik_galt", ex.Message);
        }
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

    private static readonly Brush Gul = Temaskift.Pensel("Fremhaev");
    private static readonly Brush PaaGul = Temaskift.Pensel("PaaFremhaev");

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
