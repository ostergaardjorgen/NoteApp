using System.Windows;
using System.Windows.Input;
using NoteApp.Core;

namespace NoteApp.Desktop.Dialogs;

/// <summary>
/// Vælg hvilken mappe noget skal ligge i.
///
/// «Uden mappe» står ØVERST og er altid med. Det er vejen tilbage: uden den
/// ville en ting, der først var lagt i en mappe, aldrig kunne komme ud igen
/// uden at man slettede mappen — og det ville ramme alt andet i den.
///
/// Der kan oprettes en mappe herfra. Det er det sted, behovet opstår: man
/// opdager, at der mangler en mappe, i det øjeblik man skal flytte noget ind
/// i den, ikke før.
/// </summary>
public partial class MappeVaelger : Window
{
    /// <summary>Den valgte mappe. Null betyder «uden mappe».</summary>
    public string? Valgt { get; private set; }

    private readonly Mapper.Slags _slags;

    public MappeVaelger(Mapper.Slags slags, string? nuvaerende, string hvad)
    {
        InitializeComponent();

        _slags = slags;

        Overskrift.Text = $"Hvilken mappe skal {hvad} ligge i?";
        Forklaring.Text =
            "Mapper holder materiale adskilt — fx en kundes møder for sig. " +
            "Filerne bliver liggende, hvor de er; det er kun visningen i appen, der ændrer sig.";

        Fyld(nuvaerende);
    }

    /// <summary>
    /// En mappe i træet. <see cref="Sti"/> er den fulde sti — «Møder/Ibrar» —
    /// og <see cref="Navn"/> er kun det sidste led, som det står på skærmen.
    /// </summary>
    /// <remarks>
    /// «Uden mappe» er også en knude. Den har null som sti, og det er dét, der
    /// betyder «ud af mapperne igen».
    /// </remarks>
    public sealed class Mappeknude : System.ComponentModel.INotifyPropertyChanged
    {
        public required string Navn { get; init; }
        public string? Sti { get; init; }
        public List<Mappeknude> Boern { get; } = new();

        private bool _erValgt;
        public bool ErValgt
        {
            get => _erValgt;
            set { _erValgt = value; PropertyChanged?.Invoke(this, new(nameof(ErValgt))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public IEnumerable<Mappeknude> MedBoern() =>
            new[] { this }.Concat(Boern.SelectMany(b => b.MedBoern()));
    }

    private readonly List<Mappeknude> _roedder = new();
    private Mappeknude? _markeret;

    /// <summary>
    /// Bygger træet af de stier, der findes.
    /// </summary>
    /// <remarks>
    /// STIERNE ER FLADE I FILEN — «Møder», «Møder/Ibrar» — og hierarkiet
    /// udledes af skråstregerne. Et mellemled, ingen har oprettet for sig
    /// selv, får en knude alligevel, ellers ville «Møder/Ibrar» stå uden en
    /// «Møder» at ligge i.
    /// </remarks>
    private void Fyld(string? vaelg)
    {
        _roedder.Clear();

        var uden = new Mappeknude { Navn = Mapper.Ingen, Sti = null };
        _roedder.Add(uden);

        var kendte = new Dictionary<string, Mappeknude>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var sti in Mapper.Alle(_slags))
        {
            var led = sti.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var samlet = "";
            Mappeknude? foraelder = null;

            foreach (var navn in led)
            {
                samlet = samlet.Length == 0 ? navn : samlet + "/" + navn;

                if (!kendte.TryGetValue(samlet, out var knude))
                {
                    knude = new Mappeknude { Navn = navn, Sti = samlet };
                    kendte[samlet] = knude;

                    if (foraelder is null) _roedder.Add(knude);
                    else foraelder.Boern.Add(knude);
                }

                foraelder = knude;
            }
        }

        Trae.ItemsSource = null;
        Trae.ItemsSource = _roedder;

        var skal = string.IsNullOrWhiteSpace(vaelg)
            ? uden
            : kendte.GetValueOrDefault(vaelg!) ?? uden;

        Vaelg(skal);
        Udfold(skal);
    }

    private void Vaelg(Mappeknude knude)
    {
        foreach (var k in _roedder.SelectMany(r => r.MedBoern())) k.ErValgt = false;

        knude.ErValgt = true;
        _markeret = knude;
    }

    /// <summary>Folder vejen ned til knuden ud, så markeringen kan ses.</summary>
    private void Udfold(Mappeknude maal)
    {
        // Traeet staar udfoldet i forvejen (IsExpanded=True i stilen), saa der
        // er ikke noget at folde ud. Metoden findes for det tilfaelde, at
        // stilen aendres - og for at markeringen kan rulles frem.
        var punkt = Trae.ItemContainerGenerator.ContainerFromItem(maal) as System.Windows.Controls.TreeViewItem;
        punkt?.BringIntoView();
    }

    private void Trae_Valgt(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is Mappeknude k) Vaelg(k);
    }

    private void Ny_Click(object sender, RoutedEventArgs e)
    {
        var vindue = Transcribe.RenameWindow.TilNyMappe();
        vindue.Owner = this;

        if (vindue.ShowDialog() != true) return;

        var navn = vindue.NytNavn;

        if (!Mapper.Opret(_slags, navn))
        {
            AppDialog.Vis(this, "Den findes allerede",
                $"Der er allerede en mappe, der hedder «{navn}».", Slags.Valg);

            // Den findes - saa vaelg den frem for at lade brugeren lede.
            Fyld(navn);
            return;
        }

        Fyld(navn);
    }

    private void Liste_DobbeltKlik(object sender, MouseButtonEventArgs e)
    {
        if (_markeret is not null) Flyt_Click(sender, new RoutedEventArgs());
    }

    private void Flyt_Click(object sender, RoutedEventArgs e)
    {
        if (_markeret is null) return;

        Valgt = _markeret.Sti;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
