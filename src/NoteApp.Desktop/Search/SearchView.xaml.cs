using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>Ét sted, ordet står — som listen viser det.</summary>
public sealed class Traefvisning
{
    public Traefvisning(Fund f, Traef t, int nummer)
    {
        Fund = f;
        Traef = t;
        Nummer = $"{nummer}.";
        Uddrag = t.Uddrag;
    }

    public Fund Fund { get; }
    public Traef Traef { get; }
    public string Nummer { get; }
    public string Uddrag { get; }
}

/// <summary>Én kilde med alle sine steder.</summary>
public sealed class Fundvisning
{
    public Fundvisning(Fund f)
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

        Steder = f.Traef.Select((t, n) => new Traefvisning(f, t, n + 1)).ToList();
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
            var ur = System.Diagnostics.Stopwatch.StartNew();
            var fund = await Task.Run(() => Soegning.Soeg(spoergsmaal, ct), ct);
            ur.Stop();

            if (ct.IsCancellationRequested) return;

            Liste.ItemsSource = fund.Select(f => new Fundvisning(f)).ToList();

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

    /// <summary>
    /// Springer hen til det sted, der blev klikket på.
    ///
    /// Der slås op på id og ikke på sti: et møde kan være flyttet til en
    /// mappe, og et dokument kan være omdøbt, siden udskriften blev lavet.
    /// Positionen er tegnnummeret i den tekst, skærmen viser — derfor kan der
    /// rulles direkte derhen frem for blot at åbne filen.
    /// </summary>
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
