using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Teksttyperne: hvad det talte skal blive til.
///
/// LAA UNDER SKABELONER INDTIL 28-08-2026. Den hører til dikteringen: man
/// vælger en type, prøver den af i prøverummet ved siden af, og retter
/// instruktionen, hvis resultatet ikke var det, man ville have. Tre skærme,
/// man skulle skifte imellem, gjorde det til tre opgaver i stedet for én.
/// </summary>
public partial class TeksttyperView : UserControl
{
    public TeksttyperView()
    {
        InitializeComponent();
        Loaded += (_, _) => VisTeksttyper();
    }

    /// <summary>Én teksttype, som listen viser den.</summary>
    private sealed record Teksttypevalg(
        Dikteringsformaal Vaerdi, string Navn, string Hvornaar, string Maerke, Visibility ErRettet);

    private Dikteringsformaal? _valgtType;
    private bool _typeIndlaest;

    /// <summary>
    /// Fylder listen. Kaldes hver gang, fordi en type kan være rettet et andet
    /// sted — instruktionerne ligger i indstillingsfilen.
    /// </summary>
    private void VisTeksttyper()
    {
        var egne = AppSettings.Current.Teksttyper;
        var valgt = _valgtType;

        Liste.ItemsSource = Teksttyper.Alle.Select(f => new Teksttypevalg(
            f,
            Sprog.T(Teksttyper.Noegletekst(f)),
            Sprog.T(Teksttyper.Noegletekst(f) + "_hvornaar"),
            Sprog.T("templatesview.teksttype_rettet"),
            Teksttyper.ErRettet(f, egne) ? Visibility.Visible : Visibility.Collapsed)).ToList();

        Liste.SelectedItem = ((IEnumerable<Teksttypevalg>)Liste.ItemsSource)
            .FirstOrDefault(t => t.Vaerdi == (valgt ?? Dikteringsformaal.Note));
    }

    private void Teksttype_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Liste.SelectedItem is not Teksttypevalg valg) return;

        _valgtType = valg.Vaerdi;

        // Flaget holder TextChanged ude, mens feltet fyldes. Uden det ville
        // «Gem» blive aktiv, saa snart man klikkede paa en anden type.
        _typeIndlaest = false;
        Instruktion.Text = Teksttyper.Prompt(valg.Vaerdi, AppSettings.Current.Teksttyper);
        _typeIndlaest = true;

        GemTeksttype.IsEnabled = false;
        TeksttypeStatus.Text = "";
    }

    private void Instruktion_Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_typeIndlaest) return;

        GemTeksttype.IsEnabled = true;
        TeksttypeStatus.Text = "";
    }

    private void GemTeksttype_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgtType is not { } formaal) return;

        // Saet fjerner selv, hvis teksten er tom eller den samme som
        // standarden - se Teksttyper.Saet.
        Teksttyper.Saet(AppSettings.Current.Teksttyper, formaal, Instruktion.Text);
        AppSettings.Current.Save();

        GemTeksttype.IsEnabled = false;
        TeksttypeStatus.Text = Sprog.T("templatesview.teksttype_gemt");
        VisTeksttyper();
    }

    private void NulstilTeksttype_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgtType is not { } formaal) return;

        AppSettings.Current.Teksttyper.Remove(Teksttyper.Noegle(formaal));
        AppSettings.Current.Save();

        _typeIndlaest = false;
        Instruktion.Text = Voxtral.Pudseprompt(formaal);
        _typeIndlaest = true;

        GemTeksttype.IsEnabled = false;
        TeksttypeStatus.Text = Sprog.T("templatesview.teksttype_nulstillet");
        VisTeksttyper();
    }
}
