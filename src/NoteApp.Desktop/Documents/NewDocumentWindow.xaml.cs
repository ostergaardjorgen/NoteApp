using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Documents;

/// <summary>
/// Valget før et dokument bliver lavet: hvilken skabelon, hvilken titel, og
/// en beskrivelse man selv skriver.
///
/// Skabelonen fylder mest på skærmen, fordi det er dét, der afgør, hvad der
/// kommer ud — et referat og en opgaveliste af det samme møde er to vidt
/// forskellige dokumenter. Beskrivelsen af hver skabelon står under den, så
/// valget ikke er et gæt.
/// </summary>
public partial class NewDocumentWindow : Window
{
    public PromptTemplate? Valgt { get; private set; }
    public string Titel => FeltTitel.Text.Trim();
    public string Beskrivelse => FeltBeskrivelse.Text.Trim();
    public string ModelSti { get; private set; } = "";

    private readonly IReadOnlyList<string> _modeller;
    private readonly string _optagelse;

    public NewDocumentWindow(string optagelsesTitel, IReadOnlyList<PromptTemplate> skabeloner,
                             IReadOnlyList<string> modeller)
    {
        InitializeComponent();

        _modeller = modeller;
        _optagelse = optagelsesTitel;
        Kilde.Text = $"Bygges på «{optagelsesTitel}». Dokumentet gemmes som .odt og kan åbnes i Word.";

        Skabeloner.ItemsSource = skabeloner;
        if (skabeloner.Count > 0) Skabeloner.SelectedIndex = 0;

        // Titlen saettes af Skabelon_Valgt, som fyrer paa SelectedIndex ovenfor.
        FeltTitel.Focus();
        FeltTitel.SelectAll();
    }

    private void Skabelon_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Skabeloner.SelectedItem is not PromptTemplate t) return;

        // Skabelonens foretrukne model, hvis den er hentet. Bliver der brugt en
        // anden, SKAL det staa — ellers tror man, man fik den, der stod i
        // skabelonen, og undrer sig over resultatet.
        var ønsket = t.PreferredModel;
        var model = _modeller.FirstOrDefault(m =>
            ønsket is not null && Path.GetFileName(m).Contains(ønsket, StringComparison.OrdinalIgnoreCase));

        ModelSti = model ?? (_modeller.Count > 0 ? _modeller[0] : "");

        ModelTekst.Text = ModelSti.Length == 0
            ? "Der er ingen sprogmodel hentet. Hent en under «AI-modeller»."
            : model is not null || ønsket is null
                ? $"Sprogmodel: {Path.GetFileNameWithoutExtension(ModelSti)}. Tager typisk to til fire minutter."
                : $"Sprogmodel: {Path.GetFileNameWithoutExtension(ModelSti)} — skabelonen foretrækker «{ønsket}», som ikke er hentet.";

        // Dokumentets navn er optagelsens navn plus skabelonens. Saadan kan man se
        // i listen, hvad det er, uden at aabne det.
        if (FeltTitel.Text.Length == 0 || Valgt is not null && FeltTitel.Text == $"{_optagelse} — {Valgt.Name}")
            FeltTitel.Text = $"{_optagelse} — {t.Name}";

        Valgt = t;
    }

    private void Opret_Click(object sender, RoutedEventArgs e)
    {
        if (Skabeloner.SelectedItem is not PromptTemplate t)
        {
            MessageBox.Show("Vælg en skabelon.", "Mangler valg", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (Titel.Length == 0)
        {
            MessageBox.Show("Dokumentet skal have en titel.", "Mangler titel",
                MessageBoxButton.OK, MessageBoxImage.Information);
            FeltTitel.Focus();
            return;
        }

        if (ModelSti.Length == 0)
        {
            MessageBox.Show("Der er ingen sprogmodel at lave dokumentet med. Hent en under «AI-modeller».",
                "Mangler sprogmodel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Valgt = t;
        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
