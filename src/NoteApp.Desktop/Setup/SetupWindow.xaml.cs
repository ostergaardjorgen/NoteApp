using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Setup;

public sealed class BrancheVisning
{
    public BrancheVisning(IndustryTemplate t)
    {
        Id = t.Id;
        Name = t.Name;
        Description = t.Description;
        AntalTekst = t.Count == 0 ? "" : $"{t.Count} ord";
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string AntalTekst { get; }
}

/// <summary>
/// Opsætning ved første start.
///
/// Tre trin, ikke flere. Formålet er, at appen kender brugerens fagområde og
/// navne, før det første møde — ikke at samle oplysninger. En tom ordbog gør
/// de første møder dårligere end nødvendigt, og navne er dem, Whisper oftest
/// staver forkert.
///
/// Modellen hentes IKKE her. Det sker på skærmen Motor og model, hvor
/// størrelse, fordele og ulemper står ved hvert valg — at trække en 2,9 GB
/// hentning ind i et velkomstforløb ville gøre valget til noget, man klikker
/// sig forbi.
/// </summary>
public partial class SetupWindow : Window
{
    private int _trin;
    private string _branche = "ingen";

    private static readonly (string Titel, string Under)[] Trin =
    {
        ("Velkommen til NoteApp", "Møde-noter der bliver på din egen maskine"),
        ("Hvad handler dine møder om?", "Så starter ordbogen med de rigtige fagord"),
        ("Hvem holder du møder med?", "Navne er dem, Whisper oftest staver forkert")
    };

    public SetupWindow()
    {
        InitializeComponent();

        DataSti.Text = UserDataPaths.Root;
        Brancher.ItemsSource = IndustryTemplates.All.Select(t => new BrancheVisning(t)).ToList();

        VisTrin(0);
    }

    private void VisTrin(int nr)
    {
        _trin = nr;

        Trin1.Visibility = nr == 0 ? Visibility.Visible : Visibility.Collapsed;
        Trin2.Visibility = nr == 1 ? Visibility.Visible : Visibility.Collapsed;
        Trin3.Visibility = nr == 2 ? Visibility.Visible : Visibility.Collapsed;

        TrinTitel.Text = Trin[nr].Titel;
        TrinUnder.Text = Trin[nr].Under;
        TrinTaeller.Text = $"Trin {nr + 1} af {Trin.Length}";

        TilbageKnap.Visibility = nr == 0 ? Visibility.Collapsed : Visibility.Visible;
        NaesteKnap.Content = nr switch
        {
            0 => "Kom i gang",
            1 => "Næste",
            _ => "Færdig"
        };
    }

    /// <summary>
    /// Lader brugeren vælge, hvor filerne skal ligge. Sker det her — før det
    /// første møde — er der intet at flytte. Vælges der om senere, flyttes det,
    /// der allerede er.
    /// </summary>
    private void SkiftMappe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Vælg hvor NoteApps filer skal ligge",
            InitialDirectory = Directory.Exists(UserDataPaths.Root)
                ? UserDataPaths.Root
                : Path.GetPathRoot(UserDataPaths.DefaultRoot)!
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            UserDataPaths.SetRoot(dialog.FolderName);
            DataSti.Text = UserDataPaths.Root;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Kunne ikke skifte mappe",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Branche_Valgt(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string id }) _branche = id;
    }

    /// <summary>
    /// Pladsholderen forsvinder, så snart der står noget. WPF's TextBox har
    /// ingen indbygget pladsholder, og eksemplet i feltet er her ikke pynt:
    /// det er dét, der fortæller, at der skal skiftes linje mellem hvert navn.
    /// </summary>
    private void Felt_Changed(object sender, TextChangedEventArgs e)
    {
        if (NavnePladsholder is null || FirmaerPladsholder is null) return;

        NavnePladsholder.Visibility = Navne.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        FirmaerPladsholder.Visibility = Firmaer.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Tilbage_Click(object sender, RoutedEventArgs e) => VisTrin(Math.Max(0, _trin - 1));

    private void Naeste_Click(object sender, RoutedEventArgs e)
    {
        if (_trin < Trin.Length - 1)
        {
            VisTrin(_trin + 1);
            return;
        }

        Afslut();
    }

    private void Afslut()
    {
        using var store = new LearningStore();

        var skabelon = IndustryTemplates.ById(_branche);
        var fraSkabelon = skabelon is null ? 0 : IndustryTemplates.Apply(store, skabelon);
        var navne = IndustryTemplates.AddNames(store, Navne.Text);
        var firmaer = IndustryTemplates.AddNames(store, Firmaer.Text, TermCategories.Organisation);

        // Ordlisten skrives med det samme. Ellers ville ordbogen vaere fyldt,
        // men filen Whisper faktisk laeser vaere tom indtil naeste gang nogen
        // huskede at trykke eksportér.
        store.ExportVocabularyFile();

        Settings.Current.SetupCompleted = true;
        Settings.Current.Industry = _branche;
        Settings.Current.Save();

        MessageBox.Show(
            $"Ordbogen er sat op med {fraSkabelon + navne + firmaer} ord: " +
            $"{fraSkabelon} fra skabelonen, {navne} navne og {firmaer} firmaer.\n\n" +
            "Næste skridt er at hente en sprogmodel under Motor og model — " +
            "uden den kan der ikke transskriberes.",
            "Klar", MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }
}
