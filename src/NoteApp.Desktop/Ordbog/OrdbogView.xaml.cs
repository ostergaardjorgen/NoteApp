using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoteApp.Core;

namespace NoteApp.Desktop.Ordbog;

/// <summary>
/// Ordbogen: navne, fagtermer og forkortelser, udskriften skal kende.
///
/// EGEN SKÆRM, IKKE EN INDSTILLING. Man tilføjer et ord, mens man har det for
/// øjnene af sig — ikke næste gang man alligevel er inde at rette noget andet.
/// </summary>
public partial class OrdbogView : UserControl
{
    public OrdbogView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            Vis();

            // Markoeren i feltet med det samme. Man kommer herind for at
            // tilfoeje et ord, ikke for at kigge paa en liste.
            Nyt.Focus();
        };
    }

    /// <summary>
    /// Viser ordbogen, som den står på disken.
    /// </summary>
    /// <remarks>
    /// DER LÆSES FRA FILEN HVER GANG, ikke fra en kopi i hukommelsen. Ordbogen
    /// er en tekstfil, og det er meningen, at den skal kunne åbnes i en
    /// editor. En liste på skærmen, der viser noget andet end det, der bliver
    /// sendt, er værre end ingen liste.
    /// </remarks>
    private void Vis()
    {
        var ord = Ordbibliotek.Laes();

        Liste.ItemsSource = ord;

        Antal.Text = ord.Count == 0
            ? Sprog.T("ordbogview.tom")
            : ord.Count > Ordbibliotek.MaksSendte
                ? Sprog.T("ordbogview.antal", ord.Count, Ordbibliotek.MaksSendte)
                : Sprog.T("ordbogview.antal_alle", ord.Count);
    }

    private void Tilfoej_Klik(object sender, RoutedEventArgs e) => TilfoejOrd();

    /// <summary>Enter gør det samme som knappen. Man tilføjer flere ad gangen.</summary>
    private void Nyt_Tast(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        TilfoejOrd();
        e.Handled = true;
    }

    private void TilfoejOrd()
    {
        var ord = Ordbibliotek.Rens(Nyt.Text);

        if (ord.Length == 0)
        {
            Nyt.Clear();
            return;
        }

        if (!Ordbibliotek.Tilfoej(ord))
        {
            // Det stod der allerede. Feltet ryddes IKKE - saa kan man se, hvad
            // man skrev, og rette i det.
            Antal.Text = Sprog.T("ordbogview.findes", ord);
            return;
        }

        Nyt.Clear();
        Vis();

        // Det nye ord er nederst. Uden det her ser man ikke, at der skete noget.
        Liste.ScrollIntoView(ord);
        Liste.SelectedItem = ord;
        Nyt.Focus();
    }

    private void Fjern_Klik(object sender, RoutedEventArgs e)
    {
        if (Liste.SelectedItem is not string ord) return;

        // Ingen bekraeftelse. Et ord er tilfoejet igen paa to sekunder, og en
        // dialog for hver fjernelse goer oprydning til noget, man lader vaere
        // med.
        Ordbibliotek.Fjern(ord);
        Vis();
    }

    private void GemFil_Klik(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "ordbog.txt",
            DefaultExt = ".txt",
            Filter = Sprog.T("ordbogview.filter"),
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var antal = Ordbibliotek.Udlaes(dialog.FileName);
            Antal.Text = Sprog.T("ordbogview.gemt", antal, dialog.FileName);
        }
        catch (IOException ex)
        {
            Antal.Text = ex.Message;
        }
    }

    private void HentFil_Klik(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            DefaultExt = ".txt",
            Filter = Sprog.T("ordbogview.filter"),
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // DER FLETTES, DER ERSTATTES IKKE. Ordene er laert over maaneder,
            // een rettelse ad gangen, og en indlaesning, der sletter dem, kan
            // ikke fortrydes.
            var nye = Ordbibliotek.Indlaes(dialog.FileName, flet: true);

            Vis();
            Antal.Text = Sprog.T("ordbogview.hentet", nye);
        }
        catch (IOException ex)
        {
            Antal.Text = ex.Message;
        }
    }
}
