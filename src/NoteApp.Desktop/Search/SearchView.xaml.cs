using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Search;

/// <summary>Ét fund, som listen kan vise det.</summary>
public sealed class Fundvisning
{
    public Fundvisning(Fund f)
    {
        Fund = f;
        Overskrift = f.Overskrift;
        Uddrag = f.Uddrag;

        (Maerkat, Maerkatfarve) = f.Slags switch
        {
            Fundtype.Udskrift => ("UDSKRIFT", new SolidColorBrush(Color.FromRgb(0x5B, 0x9D, 0xF0))),
            Fundtype.Note => ("DIN NOTE", new SolidColorBrush(Color.FromRgb(0x4C, 0xBE, 0x72))),
            _ => ("DOKUMENT", new SolidColorBrush(Color.FromRgb(0xC9, 0x8C, 0xF0)))
        };

        // Datoen og antallet står yderst til højre. Antallet er ikke pynt:
        // ét træf er en omtale i forbifarten, tolv er dét, mødet handlede om.
        Hoejre = f.AntalIAlt > 1
            ? $"{f.Tid:dd-MM-yyyy}  ·  {f.AntalIAlt} steder"
            : $"{f.Tid:dd-MM-yyyy}";
    }

    public Fund Fund { get; }
    public string Overskrift { get; }
    public string Uddrag { get; }
    public string Maerkat { get; }
    public Brush Maerkatfarve { get; }
    public string Hoejre { get; }
}

/// <summary>
/// Søgning på tværs af alle møder og dokumenter.
///
/// HVORFOR DEN HAR SIN EGEN SKÆRM
///
/// Et søgefelt på Optagelser ville kun kunne søge i optagelser. Det, man
/// leder efter, er sjældent bundet til én slags ting: «hvad blev der sagt om
/// Kernesys» kan lige så godt stå i et referat som i en udskrift eller i en
/// note, man selv skrev midt i mødet.
///
/// HVORFOR DER SØGES, MENS MAN SKRIVER
///
/// En søgeknap gør et opslag til noget, man overvejer. Uden knap er det noget,
/// man bare gør — og det er hele forskellen på, om arkivet bliver brugt.
///
/// Selve søgningen ligger i <see cref="Soegning"/> og kører på en baggrunds-
/// tråd. Skærmen her venter aldrig på disken.
/// </summary>
public partial class SearchView : UserControl
{
    private readonly DispatcherTimer _pause;
    private CancellationTokenSource? _afbryd;

    public SearchView()
    {
        InitializeComponent();

        // DER VENTES ET OEJEBLIK, FOER DER SOEGES.
        //
        // Uden pausen startes en ny gennemloebning af alle filer for hvert
        // eneste tastetryk. Ved "Cloudworks" er det ti soegninger, hvor de ni
        // er smidt vaek, foer de blev faerdige.
        //
        // 220 ms er valgt, fordi det er kortere end pausen mellem to ord og
        // laengere end mellem to bogstaver.
        _pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _pause.Tick += (_, _) => { _pause.Stop(); Soeg(); };

        Loaded += (_, _) => Felt.Focus();
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

            // TIDEN STAAR DER, OG DET ER MED VILJE.
            //
            // Der er ikke noget indeks - der laeses i filerne hver gang. Saa
            // laenge tallet er tocifret i millisekunder, er det det rigtige
            // valg. Begynder det at vokse, kan det ses her, foer det bliver
            // til en irritation.
            Status.Text = fund.Count == 0
                ? $"Ingen fund  ·  {ur.ElapsedMilliseconds} ms"
                : $"{fund.Count} fund  ·  {ur.ElapsedMilliseconds} ms";
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
    /// Går til det, fundet peger på.
    ///
    /// Der slås op på id og ikke på sti: et møde kan være flyttet til en
    /// mappe, og et dokument kan være omdøbt, siden udskriften blev lavet.
    /// </summary>
    private void Fund_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Fundvisning v) return;
        if (Application.Current.MainWindow is not MainWindow hoved) return;

        if (v.Fund.Slags == Fundtype.Dokument)
        {
            hoved.GaaTilDokumenter(v.Fund.Kilde);
            return;
        }

        if (!hoved.GaaTilOptagelse(v.Fund.Kilde))
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes ikke længere",
                "Optagelsen er slettet eller flyttet uden for appen, siden den blev skrevet ud.",
                Dialogs.Slags.Valg);
    }
}
