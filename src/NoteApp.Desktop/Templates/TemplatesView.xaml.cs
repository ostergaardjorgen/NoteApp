using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Templates;

/// <summary>
/// Et modelvalg, som det ser ud i rullelisten.
///
/// Både de hentede og de ikke-hentede står på listen. En model, der kan
/// vælges, men ikke findes, er ikke en fejl — den skal bare hentes først, og
/// det skal fremgå af valget frem for at komme som en overraskelse den dag,
/// skabelonen bliver brugt.
/// </summary>
public sealed record ModelValg(string? Id, string Visning, string Forklaring, bool Hentet);

/// <summary>
/// Opsætning af skabeloner: hvad der skal laves ud af en optagelse, og hvilken
/// sprogmodel der skal lave det.
///
/// Skabelonerne er filer i datamappen. Skærmen her redigerer filerne — den
/// indfører ikke et parallelt format, der skal holdes i sync. Det, man laver
/// her, kan åbnes i en almindelig editor, kopieres til en kollega og indgå i
/// din backup som alt andet.
/// </summary>
public partial class TemplatesView : UserControl
{
    private readonly List<PromptTemplate> _skabeloner = new();
    private PromptTemplate? _valgt;
    private bool _indlæser;

    public TemplatesView()
    {
        InitializeComponent();

        Felter.Text = string.Join("   ",
            PromptTemplate.Fields.Keys.Select(f => "{{" + f + "}}"));

        // De indbyggede skabeloner lægges i datamappen, hvis de ikke er der.
        // Uden dette står skærmen tom første gang, og så ser det ud, som om
        // appen ikke kan noget — mens den i virkeligheden bare ikke har pakket
        // ud endnu.
        try { DraftStore.SeedTemplates(); } catch (Exception) { /* vises som tom liste */ }

        VisModelStatus();
        FyldModeller();
        Indlæs();
    }

    // ------------------------------------------------------------- modeller

    /// <summary>
    /// De hentede modeller, slået op i kataloget. Filnavnet alene siger ikke
    /// nok — licens og hvad modellen egner sig til står i kataloget.
    /// </summary>
    private static IReadOnlyList<ModelValg> Modeller()
    {
        var hentede = LlmRunner.InstalledModels()
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        var liste = new List<ModelValg>
        {
            new(null, "Ingen — skabelonen bruges ikke til udkast",
                "Skabelonen kan stadig redigeres og gemmes. Den kan bare ikke bruges til at lave et udkast, før der er valgt en model.",
                true)
        };

        foreach (var m in LlmCatalog.Default)
        {
            var hentet = hentede.Contains(m.FileName);
            liste.Add(new ModelValg(
                m.Id,
                hentet ? $"{m.Id}  ·  {m.SizeText}" : $"{m.Id}  ·  {m.SizeText}  (ikke hentet)",
                hentet
                    ? $"{m.Summary}\n\nFordel: {m.Pros}\nUlempe: {m.Cons}\nLicens: {m.License} — fri at sælge med."
                    : $"Ikke hentet endnu. Hent den under «Motor og model», så fylder den {m.SizeText} på disken.\n\n{m.Summary}",
                hentet));
        }

        // Modeller paa disken, der ikke staar i kataloget. De skal med — ellers
        // ville en model, brugeren selv har lagt derind, se ud som om den ikke
        // fandtes.
        foreach (var fil in hentede)
        {
            if (LlmCatalog.Known.Any(m => m.FileName.Equals(fil, StringComparison.OrdinalIgnoreCase))) continue;
            liste.Add(new ModelValg(Path.GetFileNameWithoutExtension(fil),
                $"{Path.GetFileNameWithoutExtension(fil)}  ·  egen",
                "Lagt i modelmappen uden om kataloget. Appen kan ikke sige noget om licens eller egnethed — det er dit eget valg.",
                true));
        }

        return liste;
    }

    private void FyldModeller()
    {
        FeltModel.ItemsSource = Modeller();
    }

    private void VisModelStatus()
    {
        var hentede = LlmRunner.InstalledModels().Count;
        var cli = LlmRunner.FindCli();

        ModelStatus.Text = (hentede, cli) switch
        {
            (0, _) => "Der er ingen sprogmodeller hentet. Skabelonerne kan sættes op nu og bruges den dag, du henter en — under «Motor og model».",
            (_, null) => $"{hentede} sprogmodel(ler) hentet, men llama.cpp mangler. Motoren hentes under «Motor og model».",
            var (n, _) => $"{n} sprogmodel(ler) klar. Et udkast laves fra en optagelse under «Optagelser»."
        };
    }

    // ------------------------------------------------------------ indlæsning

    private void Indlæs(string? vælgNavn = null)
    {
        _skabeloner.Clear();
        _skabeloner.AddRange(PromptTemplate.LoadAll());

        Liste.ItemsSource = null;
        Liste.ItemsSource = _skabeloner;

        if (_skabeloner.Count == 0)
        {
            Status.Text = $"Ingen skabeloner i {PromptTemplate.Directory}. Tryk «Ny skabelon» for at lave den første.";
            Detaljer.IsEnabled = false;
            return;
        }

        Detaljer.IsEnabled = true;
        Liste.SelectedItem = _skabeloner.FirstOrDefault(t => t.Name == vælgNavn) ?? _skabeloner[0];
    }

    private void Liste_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (Liste.SelectedItem is not PromptTemplate t) return;

        _indlæser = true;
        _valgt = t;

        FeltNavn.Text = t.Name;
        FeltBeskrivelse.Text = t.Description ?? "";
        FeltTemperatur.Text = t.Temperature.ToString("0.##", CultureInfo.CurrentCulture);
        FeltMaksTokens.Text = t.MaxTokens.ToString();
        FeltSystem.Text = t.SystemPrompt;
        FeltBruger.Text = t.UserPrompt;

        var valg = (IReadOnlyList<ModelValg>)FeltModel.ItemsSource;
        FeltModel.SelectedItem =
            valg.FirstOrDefault(m => string.Equals(m.Id, t.PreferredModel, StringComparison.OrdinalIgnoreCase))
            ?? valg[0];

        _indlæser = false;
        GemKnap.IsEnabled = false;
        Status.Text = t.Path is null ? "Indbygget skabelon" : t.Path;
    }

    // -------------------------------------------------------------- ændring

    private void Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = true;
    }

    private void Model_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (FeltModel.SelectedItem is ModelValg m)
        {
            ModelForklaring.Text = m.Forklaring;
            ModelForklaring.Foreground = (System.Windows.Media.Brush)FindResource(
                m.Hentet ? "TekstMeget" : "Advarsel");
        }

        if (!_indlæser) GemKnap.IsEnabled = true;
    }

    // ---------------------------------------------------------------- gem

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        if (string.IsNullOrWhiteSpace(FeltNavn.Text))
        {
            MessageBox.Show("Skabelonen skal have et navn.", "Mangler navn",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Tallene kontrolleres FOER der gemmes. En skabelon med en ulaeselig
        // temperatur ville blive gemt fint og foerst fejle den dag, den skulle
        // bruges — og saa staar man med et moede, der skal skrives i haanden.
        if (!TryTal(FeltTemperatur.Text, out var temp) || temp < 0 || temp > 2)
        {
            MessageBox.Show("Temperatur skal være et tal mellem 0 og 2. Til referater er 0,2 et fornuftigt sted.",
                "Temperaturen kan ikke bruges", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(FeltMaksTokens.Text, out var maks) || maks < 128 || maks > 32768)
        {
            MessageBox.Show("Maks. længde skal være et helt tal mellem 128 og 32768. 2048 rækker til et fyldigt referat.",
                "Længden kan ikke bruges", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(FeltSystem.Text))
        {
            MessageBox.Show("Instruktionen til modellen må ikke være tom — det er den, der afgør, hvad der kommer ud.",
                "Mangler instruktion", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!FeltBruger.Text.Contains("{{transskription}}"))
        {
            var svar = MessageBox.Show(
                "Feltet {{transskription}} står ikke i teksten forneden.\n\n" +
                "Uden det får modellen ikke selve mødet at se, og udkastet bliver skrevet ud af ingenting.\n\n" +
                "Gem alligevel?",
                "Mødet mangler", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (svar != MessageBoxResult.Yes) return;
        }

        var gammelSti = _valgt.Path;

        _valgt.Name = FeltNavn.Text.Trim();
        _valgt.Description = string.IsNullOrWhiteSpace(FeltBeskrivelse.Text) ? null : FeltBeskrivelse.Text.Trim();
        _valgt.PreferredModel = (FeltModel.SelectedItem as ModelValg)?.Id;
        _valgt.Temperature = temp;
        _valgt.MaxTokens = maks;
        _valgt.SystemPrompt = FeltSystem.Text.Trim();
        _valgt.UserPrompt = FeltBruger.Text.Trim();

        try
        {
            var sti = _valgt.Save();

            // Er navnet aendret, skal den gamle fil vaek — ellers ligger
            // skabelonen to steder, og den naeste redigering rammer den ene af
            // dem uden at man kan se hvilken.
            if (gammelSti is not null && !gammelSti.Equals(sti, StringComparison.OrdinalIgnoreCase)
                                      && File.Exists(gammelSti))
                File.Delete(gammelSti);

            Status.Text = $"Gemt: {sti}";
            GemKnap.IsEnabled = false;
            Indlæs(_valgt.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Skabelonen kunne ikke gemmes.\n\n{ex.Message}", "Kunne ikke gemme",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Accepterer både komma og punktum — dansk tastatur skriver komma.</summary>
    private static bool TryTal(string s, out double værdi) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out værdi) ||
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out værdi);

    // ------------------------------------------------------------- ny/slet

    private void Ny_Click(object sender, RoutedEventArgs e)
    {
        var ny = new PromptTemplate
        {
            Name = "Ny skabelon",
            Description = "Beskriv hvad den laver",
            Temperature = 0.2,
            MaxTokens = 2048,
            SystemPrompt =
                "Du skriver på dansk ud fra en udskrift af et møde.\n\n" +
                "Skriv kun det, der faktisk står i udskriften. Find ikke på deltagere, datoer, tal\n" +
                "eller beslutninger. Skriv aldrig et tal, der ikke står i udskriften.\n\n" +
                "Er noget uklart, så skriv det som et åbent spørgsmål frem for at gætte.",
            UserPrompt =
                "Her er udskriften af mødet.\n\n" +
                "Titel: {{titel}}\nDato: {{dato}}\nMødet blev holdt på: {{sprog}}\n\n" +
                "Fagord og navne, der kan optræde: {{ordbog}}\n\n" +
                "Udskrift:\n{{transskription}}"
        };

        try
        {
            var sti = Path.Combine(PromptTemplate.Directory, PromptTemplate.Filnavn(ny.Name));
            if (File.Exists(sti))
            {
                ny.Name = "Ny skabelon " + DateTime.Now.ToString("HH-mm");
            }

            ny.Save();
            Indlæs(ny.Name);
            Status.Text = "Ny skabelon oprettet. Giv den et navn, og skriv hvad den skal lave.";
            FeltNavn.Focus();
            FeltNavn.SelectAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Skabelonen kunne ikke oprettes.\n\n{ex.Message}", "Kunne ikke oprette",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt?.Path is null)
        {
            MessageBox.Show("Der er ingen fil at slette — skabelonen er indbygget.", "Kan ikke slettes",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var svar = MessageBox.Show(
            $"Slet skabelonen «{_valgt.Name}»?\n\n{_valgt.Path}\n\n" +
            "Filen slettes. Har du brugt den til udkast tidligere, ligger de udkast stadig hvor de er.",
            "Slet skabelon", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (svar != MessageBoxResult.Yes) return;

        try
        {
            File.Delete(_valgt.Path);
            Status.Text = "Skabelonen er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Skabelonen kunne ikke slettes.\n\n{ex.Message}", "Kunne ikke slette",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(PromptTemplate.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{PromptTemplate.Directory}\"")
        { UseShellExecute = true });
    }
}
