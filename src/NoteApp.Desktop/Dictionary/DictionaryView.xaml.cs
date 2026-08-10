using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Core;

namespace NoteApp.Desktop.Dictionary;

/// <summary>En række, som gitteret kan vise — med kategorien skrevet på dansk.</summary>
public sealed class TermVisning
{
    public TermVisning(TermRow r)
    {
        Row = r;
        KategoriTekst = TermCategories.Label(r.Category);
        AktivTekst = r.Active ? "ja" : "nej";
    }

    public TermRow Row { get; }
    public long Id => Row.Id;
    public string Canonical => Row.Canonical;
    public double Weight => Row.Weight;
    public string? Scope => Row.Scope;
    public int AliasCount => Row.AliasCount;
    public string KategoriTekst { get; }
    public string AktivTekst { get; }
}

/// <summary>
/// Vedligehold af den lokale ordbog.
///
/// Kategorierne er de fem, skemaet allerede kender, og der er ikke opfundet
/// flere: en kategori tjener kun et formål her, nemlig at afgøre hvem der
/// kommer med i Whispers prompt, når ordbogen er større end de ~224 tokens.
/// Personer og organisationer først, fordi navne er dem, modellen oftest
/// staver forkert. En kategori uden den konsekvens ville kun være mere
/// arbejde ved indtastning.
/// </summary>
public partial class DictionaryView : UserControl
{
    private readonly LearningStore _store;
    private long? _redigerer;

    private const string AlleKategorier = "Alle kategorier";

    public DictionaryView()
    {
        InitializeComponent();

        _store = new LearningStore();

        FilterKategori.Items.Add(AlleKategorier);
        foreach (var k in TermCategories.All)
        {
            FilterKategori.Items.Add(TermCategories.Label(k));
            FeltKategori.Items.Add(TermCategories.Label(k));
        }
        FilterKategori.SelectedIndex = 0;
        FeltKategori.SelectedIndex = TermCategories.All.ToList().IndexOf(TermCategories.Fagterm);

        Indlaes();
    }

    // ---------------------------------------------------------------- listen

    private void Indlaes()
    {
        var kategori = FilterKategori.SelectedIndex <= 0
            ? null
            : TermCategories.All[FilterKategori.SelectedIndex - 1];

        var rækker = _store.ListTerms(Soeg.Text, kategori);
        Gitter.ItemsSource = rækker.Select(r => new TermVisning(r)).ToList();

        SoegPladsholder.Visibility = string.IsNullOrEmpty(Soeg.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        var pr = _store.CountByCategory();
        var dele = TermCategories.All
            .Where(k => pr.ContainsKey(k))
            .Select(k => $"{pr[k]} {TermCategories.Label(k).ToLowerInvariant()}");
        Antal.Text = $"{_store.TermCount()} aktive: {string.Join(", ", dele)}";

        OpdaterPrompt();
    }

    private void Soeg_Changed(object sender, TextChangedEventArgs e) => Indlaes();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Indlaes();

    private void Gitter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SletKnap.IsEnabled = Gitter.SelectedItem is TermVisning;
        if (Gitter.SelectedItem is not TermVisning v) return;

        _redigerer = v.Row.Id;
        FormTitel.Text = $"Redigerer «{v.Canonical}»";
        FeltOrd.Text = v.Row.Canonical;
        FeltKategori.SelectedIndex = TermCategories.All.ToList().IndexOf(v.Row.Category);
        FeltVaegt.Text = v.Row.Weight.ToString("0.0", CultureInfo.CurrentCulture);
        FeltUdtale.Text = v.Row.Hint ?? "";
        FeltKunde.Text = v.Row.Scope ?? "";
        FeltAktiv.IsChecked = v.Row.Active;
    }

    // ------------------------------------------------------------ redigering

    private void Kategori_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FeltKategori.SelectedIndex < 0) return;
        KategoriForklaring.Text = TermCategories.Explanation(TermCategories.All[FeltKategori.SelectedIndex]);
    }

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        var ord = FeltOrd.Text.Trim();
        if (ord.Length == 0)
        {
            MessageBox.Show("Skriv et ord først.", "Mangler ord", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!double.TryParse(FeltVaegt.Text.Replace('.', ','), NumberStyles.Any,
                             CultureInfo.CurrentCulture, out var vægt))
            vægt = 1.0;

        var kategori = TermCategories.All[Math.Max(0, FeltKategori.SelectedIndex)];
        var udtale = Tom(FeltUdtale.Text);
        var kunde = Tom(FeltKunde.Text);
        var aktiv = FeltAktiv.IsChecked == true;

        if (_redigerer is long id)
            _store.UpdateTerm(id, ord, kategori, vægt, kunde, udtale, aktiv);
        else
            _store.AddTerm(ord, kategori, vægt, kunde, udtale);

        Ryd_Click(sender, e);
        Indlaes();
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (Gitter.SelectedItem is not TermVisning v) return;

        var svar = MessageBox.Show(
            $"Slet «{v.Canonical}» fra ordbogen?\n\n" +
            "Rettelser, du allerede har lavet i tidligere møder, bevares — de er " +
            "kendsgerninger om de møder og slettes ikke med ordet.",
            "Slet ord", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (svar != MessageBoxResult.Yes) return;

        _store.DeleteTerm(v.Row.Id);
        Ryd_Click(sender, e);
        Indlaes();
    }

    private void Ryd_Click(object sender, RoutedEventArgs e)
    {
        _redigerer = null;
        FormTitel.Text = "Nyt ord";
        FeltOrd.Text = "";
        FeltVaegt.Text = "1,0";
        FeltUdtale.Text = "";
        FeltKunde.Text = "";
        FeltAktiv.IsChecked = true;
        Gitter.SelectedItem = null;
        FeltOrd.Focus();
    }

    private static string? Tom(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ---------------------------------------------------------------- prompt

    private void OpdaterPrompt()
    {
        var prompt = _store.BuildWhisperPrompt();
        PromptTekst.Text = prompt.Length == 0
            ? "(tom — ordbogen har ingen aktive ord endnu)"
            : prompt;

        var tokens = LearningStore.EstimateTokens(prompt);
        var medtaget = prompt.Length == 0 ? 0 : prompt.Count(c => c == ',') + 1;
        var ialt = _store.TermCount();

        PromptTal.Text = $"~{tokens} af 224 tokens · {medtaget} af {ialt} ord med";

        // Kommer ikke alle med, er det ikke en fejl — det er budgettet, der
        // virker. Men brugeren skal vide det, for så er det vægten, der afgør
        // hvem der ryger ud.
        PromptTal.Foreground = medtaget < ialt
            ? (Brush)FindResource("Advarsel")
            : (Brush)FindResource("TekstSvag");
    }

    private void Eksport_Click(object sender, RoutedEventArgs e)
    {
        var sti = _store.ExportVocabularyFile();
        MessageBox.Show(
            $"Ordlisten er gemt som:\n{sti}\n\nDet er den fil, Fase 0-scriptet læser.",
            "Gemt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        // Den gamle statiske ordliste kan ligge to steder: i datamappen, eller
        // i repoet fra før ordbogen fandtes. Tag den, der findes.
        var fil = File.Exists(UserDataPaths.Vocabulary)
            ? UserDataPaths.Vocabulary
            : RepoFiles.Find("ordliste.txt");
        if (fil is null)
        {
            MessageBox.Show("Fandt ingen ordliste.txt at importere fra.", "Ingen fil",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var antal = _store.ImportVocabularyFile(fil);
        Indlaes();

        MessageBox.Show(
            $"{antal} ord læst ind fra:\n{fil}\n\n" +
            "De er sat som fagtermer og forkortelser. Gå dem igennem og flyt navnene " +
            "til kategorien Person — de bliver valgt først til prompten.",
            "Importeret", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
