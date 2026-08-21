using System.Windows;
using NoteApp.Desktop.Transcribe;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Spørgsmålene, der skal stilles FØR et webinar optages.
///
/// HVORFOR DE STILLES FØR OG IKKE BAGEFTER
///
/// Sproget: et webinar begynder på slaget, og det er for sent at svare på et
/// spørgsmål, når oplægsholderen er i gang. Målt 19-08-2026 gættede appen
/// begge spor til engelsk på et dansk-norsk møde, og hele udskriften blev
/// vrøvl — så gættet er ikke et alternativ.
///
/// Linket: det ligger i den mail, man tilmeldte sig med, og den er væk om en
/// måned. Bagefter kan man ikke skaffe det.
/// </summary>
public partial class WebinarWindow : Window
{
    public string Sprog { get; private set; } = "en";
    public string? Kilde { get; private set; }

    public WebinarWindow()
    {
        InitializeComponent();

        Sprogvalg.ItemsSource = SprogvalgWindow.Sprog
            .Select(s => new { s.Kode, s.Navn })
            .ToList();

        // ENGELSK ER FORVALGT, IKKE DANSK.
        //
        // Modsat alt andet i appen. Et webinar, man tilmelder sig, er oftere
        // på engelsk end på dansk — det er derfor, oversættelsen overhovedet
        // er en funktion. Standarden skal være det almindelige tilfælde.
        var engelsk = SprogvalgWindow.Sprog.ToList().FindIndex(s => s.Kode == "en");
        Sprogvalg.SelectedIndex = engelsk >= 0 ? engelsk : 0;

        Loaded += (_, _) => StartKnap.Focus();
    }

    private void Start_Klik(object sender, RoutedEventArgs e)
    {
        if (Sprogvalg.SelectedItem is { } valgt)
            Sprog = (string)valgt.GetType().GetProperty("Kode")!.GetValue(valgt)!;

        var link = Link.Text.Trim();
        Kilde = link.Length > 0 ? link : null;

        DialogResult = true;
        Close();
    }

    private void Fortryd_Klik(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
