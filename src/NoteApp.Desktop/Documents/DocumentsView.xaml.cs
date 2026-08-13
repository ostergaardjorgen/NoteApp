using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core.Documents;

namespace NoteApp.Desktop.Documents;

/// <summary>
/// De færdige dokumenter.
///
/// De ligger for sig selv frem for nede i den enkelte optagelses mappe: det
/// er dokumenterne, man leder efter bagefter, ikke lydfilerne, og et referat
/// skal kunne findes uden at vide hvilket møde det kom fra.
///
/// Hvert dokument bærer, hvad det er lavet af — optagelse, skabelon, model.
/// Uden det er det en tekst, ingen tør bruge til noget, for man kan ikke
/// finde tilbage til lyden og tjekke efter.
/// </summary>
public partial class DocumentsView : UserControl
{
    private List<DocumentInfo> _alle = new();
    private DocumentInfo? _valgt;
    private bool _indlæser;

    public DocumentsView(string? aabnId = null)
    {
        InitializeComponent();
        Indlæs(aabnId);
    }

    private void Indlæs(string? vælgId = null)
    {
        _alle = DocumentStore.LoadAll().ToList();

        Liste.ItemsSource = null;
        Liste.ItemsSource = _alle;

        Antal.Text = _alle.Count switch
        {
            0 => "Ingen dokumenter",
            1 => "1 dokument",
            _ => $"{_alle.Count} dokumenter"
        };

        TomPanel.Visibility = _alle.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Detaljer.Visibility = Visibility.Collapsed;

        if (_alle.Count == 0)
        {
            Status.Text = $"Dokumenter gemmes i {DocumentStore.Directory}";
            return;
        }

        Liste.SelectedItem = _alle.FirstOrDefault(d => d.Id == vælgId) ?? _alle[0];
    }

    private void Valgt_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Liste.SelectedItem is not DocumentInfo d)
        {
            _valgt = null;
            SletKnap.IsEnabled = AabnKnap.IsEnabled = GemKnap.IsEnabled = OmdoebKnap.IsEnabled = false;
            return;
        }

        _indlæser = true;
        _valgt = d;

        TomPanel.Visibility = Visibility.Collapsed;
        Detaljer.Visibility = Visibility.Visible;

        Titel.Text = d.Title;
        Lavet.Text = d.Created.ToString("dddd d. MMMM yyyy 'kl.' HH:mm");
        Skabelon.Text = d.Template;
        Model.Text = d.Model;
        FeltBeskrivelse.Text = d.Description;

        // Findes kilden stadig? En optagelse kan vaere slettet, og saa skal der
        // staa det frem for en sti, der ikke foerer nogen steder hen.
        var kildeFindes = d.SourceRecording.Length > 0 && Directory.Exists(d.SourceRecording);
        FraOptagelse.Text = d.SourceTitle.Length == 0
            ? "(ukendt)"
            : kildeFindes ? d.SourceTitle : $"{d.SourceTitle} — optagelsen er slettet";

        var fil = DocumentStore.Path_(d);
        var findes = File.Exists(fil);
        Filnavn.Text = findes ? d.FileName : $"{d.FileName} — filen mangler";

        Indhold.Text = d.Markdown.Length > 0 ? d.Markdown : "(intet gemt indhold)";

        _indlæser = false;

        SletKnap.IsEnabled = true;
        OmdoebKnap.IsEnabled = true;
        AabnKnap.IsEnabled = findes;
        GemKnap.IsEnabled = false;
        Status.Text = findes ? fil : "Dokumentfilen findes ikke længere — kun oplysningerne om den.";
    }

    private void Beskrivelse_Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = _valgt is not null;
    }

    /// <summary>
    /// Omdøber dokumentet. Titlen skrives ind i selve .odt-filen, så den
    /// følger med, når filen sendes videre.
    ///
    /// FILNAVNET røres ikke. Filen kan allerede være sendt eller åbnet af
    /// nogen, og et filnavn, der skifter under hånden, kan ikke findes igen.
    /// Sammenhængen holdes af id'et, ikke af navnet.
    /// </summary>
    private void Omdoeb_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var vindue = Transcribe.RenameWindow.TilDokument(_valgt.Title);
        vindue.Owner = Window.GetWindow(this);

        if (vindue.ShowDialog() != true) return;

        try
        {
            _valgt.Title = vindue.NytNavn;
            DocumentStore.Save(_valgt);

            var id = _valgt.Id;
            Indlæs(id);
            Status.Text = $"Omdøbt til «{vindue.NytNavn}».";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Navnet kunne ikke gemmes.\n\n{ex.Message}", "Kunne ikke omdøbe",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        try
        {
            // Dokumentet skrives om, saa forsiden foelger med. Ellers ville den
            // fil, man sender videre, sige noget andet end appen.
            DocumentStore.UpdateDescription(_valgt, FeltBeskrivelse.Text.Trim());
            GemKnap.IsEnabled = false;
            Status.Text = "Beskrivelsen er gemt og skrevet ind i dokumentet.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Beskrivelsen kunne ikke gemmes.\n\n{ex.Message}", "Kunne ikke gemme",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var fil = DocumentStore.Path_(_valgt);
        if (!File.Exists(fil))
        {
            MessageBox.Show("Filen findes ikke længere.", "Kan ikke åbne",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(fil) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Dokumentet kunne ikke åbnes.\n\n{ex.Message}\n\n" +
                "Er der ikke noget program til .odt-filer, kan Word åbne dem — vælg «Åbn med».",
                "Kunne ikke åbne", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var svar = MessageBox.Show(
            $"Slet «{_valgt.Title}»?\n\nSelve optagelsen og udskriften bliver liggende — det er kun " +
            "dokumentet, der slettes. Du kan lave et nyt af den samme optagelse.",
            "Slet dokument", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (svar != MessageBoxResult.Yes) return;

        try
        {
            DocumentStore.Delete(_valgt);
            Status.Text = "Dokumentet er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dokumentet kunne ikke slettes.\n\n{ex.Message}", "Kunne ikke slette",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DocumentStore.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DocumentStore.Directory}\"")
        { UseShellExecute = true });
    }
}
