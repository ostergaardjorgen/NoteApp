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
            Fundtype.Udskrift => ("UDSKRIFT", new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0))),
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
            FyldFiltre();

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

    private void Felt_Aendret(object sender, TextChangedEventArgs e)
    {
        Pladsholder.Visibility = Felt.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        _pause.Stop();
        _pause.Start();
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

    private Soegefilter Filteret() => new(
        Sprog: (Sprogfilter.SelectedItem as Filterpunkt)?.Vaerdi,
        Moedetype: (Typefilter.SelectedItem as Filterpunkt)?.Vaerdi,
        Mappe: (Mappefilter.SelectedItem as Filterpunkt)?.Vaerdi);

    private void Filter_Aendret(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        RydFilter.Visibility = Filteret().Tomt ? Visibility.Collapsed : Visibility.Visible;

        // Der soeges med det samme. Et filter, der foerst virker, naar man
        // roerer soegefeltet, foeles som om det ikke virkede.
        _pause.Stop();
        Soeg();
    }

    private void RydFilter_Klik(object sender, RoutedEventArgs e)
    {
        Mappefilter.SelectedIndex = 0;
        Typefilter.SelectedIndex = 0;
        Sprogfilter.SelectedIndex = 0;
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
            Rude.Visibility = Visibility.Collapsed;
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

            Liste.ItemsSource = fund.Select(f => new Fundvisning(f, spoergsmaal)).ToList();

            // Uddragene tegnes af Uddrag_Ind, naar de kommer paa skaermen.

            TomPanel.Visibility = Visibility.Collapsed;
            Rude.Visibility = fund.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            IntetPanel.Visibility = fund.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

            IntetOverskrift.Text = $"Ingen fund på «{spoergsmaal}»";

            var steder = fund.Sum(f => f.Traef.Count);

            // TIDEN STAAR DER, OG DET ER MED VILJE.
            //
            // Der er ikke noget indeks - der laeses i filerne hver gang. Saa
            // laenge tallet er tocifret i millisekunder, er det det rigtige
            // valg. Begynder det at vokse, kan det ses her, foer det bliver
            // til en irritation.
            // ============ NÅR ORDENE ALDRIG STÅR SAMMEN ============
            //
            // Soeger man paa to ting, og de begge findes i optagelsen uden
            // nogensinde at blive sagt i den samme sammenhaeng, ser listen ud
            // som en fejl: hvert sted har kun det ene ord.
            //
            // Det ER det rigtige svar — der er bare ikke noget sted, hvor de
            // to ting moedes. Maalt paa et rigtigt webinar 21-08-2026 med
            // «access indigo»: begge ord staar der mange gange, og aldrig
            // inden for hundrede tegn af hinanden.
            //
            // Uden linjen her leder man efter en fejl, der ikke er der.
            var soegeord = Soegning.Del(spoergsmaal).Count;
            var bedst = fund.Count == 0 ? 0 : fund.Max(f => f.OrdSammen);

            // Der staar HVOR MANGE af ordene der er med, ikke bare at der er
            // noget galt. «2 af 3 ord» er en oplysning, man kan handle paa:
            // saa ved man, at det tredje ord skal soeges for sig.
            var spredt = soegeord > 1 && fund.Count > 0 && bedst < soegeord
                ? $"  ·  bedste sted har {bedst} af {soegeord} ord — de står aldrig alle sammen"
                : "";

            Status.Text = fund.Count == 0
                ? $"Ingen fund  ·  {ur.ElapsedMilliseconds} ms"
                : $"{steder} {(steder == 1 ? "sted" : "steder")} i {fund.Count} " +
                  $"{(fund.Count == 1 ? "kilde" : "kilder")}{spredt}  ·  {ur.ElapsedMilliseconds} ms";
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
