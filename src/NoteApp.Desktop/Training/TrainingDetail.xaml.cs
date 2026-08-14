using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Training;

/// <summary>
/// Overblikket over ÉN oplæsning: tallet, bjælken, og vejen ind til
/// sætningerne.
///
/// HVAD DER LIGGER HER, OG HVAD DER LIGGER I VINDUET
///
/// Her: det, man skal kunne se på ét blik — hvor godt gik det, og hvad skal
/// jeg gøre. Der: arbejdet, sætning for sætning.
///
/// Skillelinjen er ikke kosmetisk. Listen kan være hundrede sætninger lang, og
/// lå den her, fyldte den hele skærmen og skubbede de tre tekstbokse væk — så
/// kunne man ikke se, hvor man var, mens man arbejdede.
/// </summary>
public partial class TrainingDetail : UserControl
{
    private string _mappe = "";
    private string _titel = "";
    private Traeningsmaaling? _maaling;

    /// <summary>Fyres, når der er lært en rettelse eller læst en sætning om.</summary>
    public event Action? Aendret;

    public TrainingDetail() => InitializeComponent();

    /// <summary>Ingen lyd at stoppe her — den ligger i vinduet. Findes for værtens skyld.</summary>
    public void Luk() { }

    public void Vis(string mappe, string titel, Traeningsmaaling maaling)
    {
        _mappe = mappe;
        _titel = titel;
        _maaling = maaling;

        StatOverskrift.Text = titel;
        StatUnder.Text = $"{maaling.Saetninger.Count} sætninger · {maaling.Saetninger.Count(s => s.Fejler)} med fejl";

        TalOrd.Text = $"{maaling.Ord}";
        TalForkerte.Text = $"{maaling.Forkerte}";
        TalProcent.Text = $"{maaling.Procent:0.0} %";

        var farve = (Brush)FindResource(Traeningsmaaling.Farve(maaling.Procent));
        TalProcent.Foreground = farve;
        Bjaelke.Background = farve;

        LoftTekst.Text = $"{Traeningsmaaling.Loft:0} % — så godt kan det blive";

        Bjaelke.Tag = Math.Min(1.0, maaling.Procent / Traeningsmaaling.Loft);
        SaetBjaelke();
        SizeChanged -= Bredde_Aendret;
        SizeChanged += Bredde_Aendret;

        // RAADET SKAL VAERE SANDT. Maalt paa den danske oplaesning er kun godt
        // en tiendedel af afvigelserne navne og fagord — resten er dagligsprog,
        // hvor en regel ville rette rigtige ord forkert andre steder.
        StatRaad.Text = maaling.Procent switch
        {
            >= 94 => "Så godt bliver det. Det, der er tilbage, er ord, hvor to mennesker også ville skrive forskelligt.",
            >= 90 => "Godt ramt. Ret navnene og fagordene — de flytter ikke procenten meget, men de er dem, man lægger mærke til i et referat.",
            >= 80 => "Brugbart, men der er en del at rette. Hør et par af sætningerne: sidder fejlene på navne, hjælper en rettelse. Lyder din stemme utydelig, hjælper det mere at læse teksten op igen.",
            _ => "Noget er galt. Det er for mange fejl til at være almindelig unøjagtighed — tjek at det var den rigtige mikrofon, og at du sad tæt nok på."
        };

        VisGenlaest();
    }

    private void VisGenlaest()
    {
        var g = Genlaesninger.Laes(_mappe);

        if (g.Count == 0)
        {
            StatGenlaest.Text = "";
            return;
        }

        var rene = g.Values.Count(x => x.Rent);

        StatGenlaest.Text =
            $"{g.Count} sætning{(g.Count == 1 ? "" : "er")} læst op igen · " +
            $"{rene} ramt helt anden gang. Genindtalinger tæller ikke med i procenten.";
    }

    private void Bredde_Aendret(object? sender, SizeChangedEventArgs e) => SaetBjaelke();

    private void SaetBjaelke()
    {
        if (Bjaelke.Tag is not double andel) return;
        if (Bjaelke.Parent is not FrameworkElement spor) return;
        if (spor.ActualWidth > 0) Bjaelke.Width = spor.ActualWidth * andel;
    }

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        if (_maaling is null) return;

        var vindue = new TrainingWindow(_mappe, _titel, _maaling)
        {
            Owner = Window.GetWindow(this)
        };

        vindue.ShowDialog();

        // Blev der rettet noget, skal overblikket vide det. Genindtalinger
        // aendrer ikke procenten — men linjen om dem skal passe.
        if (!vindue.NogetAendret) return;

        VisGenlaest();
        Aendret?.Invoke();
    }
}
