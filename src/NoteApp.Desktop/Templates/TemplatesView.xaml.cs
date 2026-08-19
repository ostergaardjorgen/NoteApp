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
        FeltAgenda.Text = Agenda();

        Felter.Text = string.Join("   ",
            PromptTemplate.Fields.Keys.Select(f => "{{" + f + "}}"));

        // De indbyggede skabeloner lægges i datamappen, hvis de ikke er der.
        // Uden dette står skærmen tom første gang, og så ser det ud, som om
        // appen ikke kan noget — mens den i virkeligheden bare ikke har pakket
        // ud endnu.
        try { DraftStore.SeedTemplates(); } catch (Exception) { /* vises som tom liste */ }
        Indlæs();
    }

    // ------------------------------------------------------------- modeller

    // HER LAA VisModelStatus. Panelet, den skrev til, er fjernet: hvad
    // dokumenter laves af, staar under AI-modeller og under Compliance. Tre
    // steder er ikke tre gange saa tydeligt.

    // ------------------------------------------------------------ indlæsning

    private void Indlæs(string? vælgNavn = null)
    {
        _skabeloner.Clear();
        _skabeloner.AddRange(PromptTemplate.LoadAll());

        // Traeet bygges forfra. Skabeloner har hverken mapper eller arkiv, saa
        // der er eet bibliotek og ingen undermapper - formen er den samme som
        // paa de to andre skaerme, indholdet er bare fladere.
        _byggerTrae = true;

        var varUdfoldet = _rod?.ErUdfoldet ?? true;

        _rod = Biblioteker.Biblioteksnode.Bibliotek("Skabeloner", "", Transcribe.Gruppe.Moede);
        _rod.Antal = _skabeloner.Count;
        foreach (var t in _skabeloner)
            _rod.Boern.Add(Biblioteker.Biblioteksnode.Skabelonnode(t));

        Trae.ItemsSource = new[] { _rod };
        _rod.ErUdfoldet = varUdfoldet;

        _byggerTrae = false;

        if (_skabeloner.Count == 0)
        {
            Status.Text = $"Ingen skabeloner i {PromptTemplate.Directory}. Tryk «Ny skabelon» for at lave den første.";
            Detaljer.IsEnabled = false;
            SletKnap.IsEnabled = false;
            return;
        }

        Detaljer.IsEnabled = true;

        var valgt = _rod.Boern.FirstOrDefault(k => k.Skabelon?.Name == vælgNavn) ?? _rod.Boern[0];
        valgt.ErValgt = true;
        Vis(valgt.Skabelon);
    }

    private Biblioteker.Biblioteksnode? _rod;
    private bool _byggerTrae;

    /// <summary>
    /// Standarddagsordenen. Den ligger som en indlejret tekstfil frem for i
    /// koden, saa den kan rettes uden at roere en eneste linje C#.
    /// </summary>
    private static string Agenda()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var navn = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("agenda.txt", StringComparison.Ordinal));

        if (navn is null) return "Dagsordenen kunne ikke indlæses.";

        using var s = asm.GetManifestResourceStream(navn);
        if (s is null) return "Dagsordenen kunne ikke indlæses.";

        using var l = new System.IO.StreamReader(s, System.Text.Encoding.UTF8);
        return l.ReadToEnd();
    }

    private void KopierAgenda_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(FeltAgenda.Text);
            Status.Text = "Dagsordenen er kopieret. Sæt den ind i mødeindkaldelsen.";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke kopiere",
                $"Udklipsholderen kunne ikke skrives til:\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    private void Trae_Valgt(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_byggerTrae) return;
        if (e.NewValue is not Biblioteker.Biblioteksnode knude) return;

        Vis(knude.Skabelon);
    }

    /// <summary>
    /// Saetter redigeringsruden efter det, der er valgt. Kaldes ogsaa med
    /// null, naar markeringen staar paa biblioteket - saa er der ikke noget
    /// at redigere, og «Slet» skal vaere graa.
    /// </summary>
    private void Vis(PromptTemplate? valgtSkabelon)
    {
        SletKnap.IsEnabled = valgtSkabelon is not null;

        if (valgtSkabelon is not { } t)
        {
            Detaljer.IsEnabled = false;
            return;
        }

        Detaljer.IsEnabled = true;

        _indlæser = true;
        _valgt = t;

        FeltNavn.Text = t.Name;
        FeltBeskrivelse.Text = t.Description ?? "";
        FeltTemperatur.Text = t.Temperature.ToString("0.##", CultureInfo.CurrentCulture);
        FeltMaksTokens.Text = t.MaxTokens.ToString();
        FeltSystem.Text = t.SystemPrompt;
        FeltBruger.Text = t.UserPrompt;

        // HER BLEV SKABELONENS PreferredModel LAEST IND I EN COMBOBOX.
        // Feltet findes stadig i filformatet, saa gamle skabeloner kan laeses,
        // men det peger paa lokale gguf-filer, som ikke bruges laengere.

        _indlæser = false;
        GemKnap.IsEnabled = false;
        Status.Text = t.Path is null ? "Indbygget skabelon" : t.Path;
    }

    // -------------------------------------------------------------- ændring

    private void Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = true;
    }

    // ---------------------------------------------------------------- gem

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        if (string.IsNullOrWhiteSpace(FeltNavn.Text))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler navn", "Skabelonen skal have et navn.", Dialogs.Slags.Pas_paa);
            return;
        }

        // Tallene kontrolleres FOER der gemmes. En skabelon med en ulaeselig
        // temperatur ville blive gemt fint og foerst fejle den dag, den skulle
        // bruges — og saa staar man med et moede, der skal skrives i haanden.
        if (!TryTal(FeltTemperatur.Text, out var temp) || temp < 0 || temp > 2)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Temperaturen kan ikke bruges", "Temperatur skal være et tal mellem 0 og 2. Til referater er 0,2 et fornuftigt sted.", Dialogs.Slags.Pas_paa);
            return;
        }

        if (!int.TryParse(FeltMaksTokens.Text, out var maks) || maks < 128 || maks > 32768)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Længden kan ikke bruges", "Maks. længde skal være et helt tal mellem 128 og 32768. 2048 rækker til et fyldigt referat.", Dialogs.Slags.Pas_paa);
            return;
        }

        if (string.IsNullOrWhiteSpace(FeltSystem.Text))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler instruktion", "Instruktionen til modellen må ikke være tom — det er den, der afgør, hvad der kommer ud.", Dialogs.Slags.Pas_paa);
            return;
        }

        if (!FeltBruger.Text.Contains("{{transskription}}"))
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Mødet mangler i skabelonen",
                "Feltet {{transskription}} står ikke i teksten forneden. Uden det får modellen ikke " +
                "selve mødet at se, og udkastet bliver skrevet ud af ingenting.",
                godkend: "Gem alligevel", annuller: "Tilbage til teksten",
                slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

            if (!ja) return;
        }

        var gammelSti = _valgt.Path;

        _valgt.Name = FeltNavn.Text.Trim();
        _valgt.Description = string.IsNullOrWhiteSpace(FeltBeskrivelse.Text) ? null : FeltBeskrivelse.Text.Trim();
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke gemme", $"Skabelonen kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke oprette", $"Skabelonen kunne ikke oprettes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt?.Path is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke slettes", "Der er ingen fil at slette — skabelonen er indbygget.", Dialogs.Slags.Valg);
            return;
        }

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet skabelonen «{_valgt.Name}»?",
            $"{_valgt.Path}\n\nFilen slettes. Har du brugt den til udkast tidligere, ligger de " +
            "udkast stadig hvor de er.",
            godkend: "Slet skabelonen", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            File.Delete(_valgt.Path);
            Status.Text = "Skabelonen er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke slette", $"Skabelonen kunne ikke slettes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(PromptTemplate.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{PromptTemplate.Directory}\"")
        { UseShellExecute = true });
    }
}
