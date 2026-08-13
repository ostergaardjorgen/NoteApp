using System.Windows;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// Et navn. Bruges tre steder: når en optagelse skal navngives efter mødet,
/// når en optagelse skal omdøbes, og når et dokument skal omdøbes.
///
/// Én dialog frem for tre. Tre dialoger, der spørger om det samme, ville
/// før eller siden holde op med at ligne hinanden — og så bliver det en
/// tilfældighed, hvad man kan i hvilken.
/// </summary>
public partial class RenameWindow : Window
{
    public string NytNavn => Felt.Text.Trim();

    public RenameWindow(string forslag, string overskrift, string forklaring,
                        string hjælp = "", string knap = "Gem navnet", string titel = "Navngiv")
    {
        InitializeComponent();

        Title = titel;
        Overskrift.Text = overskrift;
        Forklaring.Text = forklaring;
        Hjaelp.Text = hjælp;
        Hjaelp.Visibility = hjælp.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        GemKnap.Content = knap;

        Felt.Text = forslag;

        // Hele forslaget markeres, saa man kan skrive henover uden foerst at
        // slette. Det er det hyppigste: forslaget er et udgangspunkt, ikke et
        // svar.
        Loaded += (_, _) => { Felt.Focus(); Felt.SelectAll(); };
    }

    /// <summary>Til omdøbning af en optagelse.</summary>
    public static RenameWindow TilOptagelse(string nuværende) => new(
        nuværende,
        "Hvad skal optagelsen hedde?",
        "Kun navnet ændres. Mappen på disken bliver liggende, så dokumenter, der allerede er lavet, stadig kan finde tilbage.");

    /// <summary>Til omdøbning af et dokument.</summary>
    public static RenameWindow TilDokument(string nuværende) => new(
        nuværende,
        "Hvad skal dokumentet hedde?",
        "Titlen står øverst i selve dokumentet, ikke kun i listen — så den følger med, når du sender filen videre.",
        "Filen på disken beholder sit nuværende navn. Den er allerede sendt eller åbnet af nogen, og et filnavn, der skifter under hånden, kan ikke findes igen.");

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (NytNavn.Length == 0)
        {
            MessageBox.Show("Navnet må ikke være tomt.", "Mangler navn",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Felt.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
