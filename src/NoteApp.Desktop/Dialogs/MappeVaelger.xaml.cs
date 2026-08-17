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

    private void Fyld(string? vaelg)
    {
        var punkter = new List<string> { Mapper.Ingen };
        punkter.AddRange(Mapper.Alle(_slags));

        Liste.ItemsSource = punkter;
        Liste.SelectedItem = string.IsNullOrWhiteSpace(vaelg)
            ? Mapper.Ingen
            : punkter.FirstOrDefault(p => p.Equals(vaelg, StringComparison.CurrentCultureIgnoreCase)) ?? Mapper.Ingen;
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
        if (Liste.SelectedItem is string) Flyt_Click(sender, new RoutedEventArgs());
    }

    private void Flyt_Click(object sender, RoutedEventArgs e)
    {
        if (Liste.SelectedItem is not string valgt) return;

        Valgt = valgt == Mapper.Ingen ? null : valgt;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
