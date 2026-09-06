using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;

namespace NoteApp.Desktop.Projekter;

/// <summary>
/// Projekter — et informationsfundament med en begyndelse og en ende.
/// </summary>
/// <remarks>
/// Modellen står i <see cref="Projekt"/>. Det korte: projektet PEGER på
/// møder, webinarer og noter i stedet for at indeholde dem, og kun de
/// dokumenter, man selv lægger ind, har projektet som hjem.
///
/// SKÆRMEN HAR SAMME FORM SOM OPTAGELSER OG SKABELONER: biblioteker til
/// venstre, det valgte til højre. Tre skærme, der ligner hinanden, er én
/// skærm at lære.
/// </remarks>
public partial class ProjekterView : UserControl
{
    private readonly List<Projekt> _projekter = new();
    private Projekt? _valgt;

    private Biblioteker.Biblioteksnode? _aktive;
    private Biblioteker.Biblioteksnode? _arkiv;
    private bool _byggerTrae;
    private bool _fylderFelter;

    public ProjekterView()
    {
        InitializeComponent();

        // Bygges i konstruktoeren og ikke paa Loaded - samme sted som
        // Skabeloner goer det. Loaded fyrer foerst, naar vinduet tegnes, og
        // saa kan skaermen ikke proeves af uden en skaerm.
        Indlaes();
    }

    // ------------------------------------------------------------ indlæsning

    private void Indlaes(string? vaelgId = null)
    {
        _projekter.Clear();
        _projekter.AddRange(Projektlager.Alle());

        _byggerTrae = true;

        var varAktivUdfoldet = _aktive?.ErUdfoldet ?? true;
        var varArkivUdfoldet = _arkiv?.ErUdfoldet ?? false;

        _aktive = Bibliotek("Aktive projekter");
        _arkiv = Bibliotek("Arkiv");

        foreach (var p in _projekter)
        {
            var rod = p.Status == Projektstatus.Aktiv ? _aktive : _arkiv;
            rod.Boern.Add(Projektnode(p));
        }

        _aktive.Antal = _aktive.Boern.Count;
        _arkiv.Antal = _arkiv.Boern.Count;

        Trae.ItemsSource = new[] { _aktive, _arkiv };
        _aktive.ErUdfoldet = varAktivUdfoldet;
        _arkiv.ErUdfoldet = varArkivUdfoldet;

        _byggerTrae = false;

        var knuder = _aktive.Boern.Concat(_arkiv.Boern).ToList();

        if (knuder.Count == 0)
        {
            Status.Text = Sprog.T("projekterview.ingen_projekter");
            Vis(null);
            return;
        }

        Status.Text = "";

        var valgt = knuder.FirstOrDefault(k => Id(k) == vaelgId) ?? knuder[0];
        valgt.ErValgt = true;

        Vis(_projekter.FirstOrDefault(p => p.Id == Id(valgt)));
    }

    private static Biblioteker.Biblioteksnode Bibliotek(string navn) =>
        Biblioteker.Biblioteksnode.Bibliotek(navn, "", Transcribe.Gruppe.Moede);

    /// <summary>
    /// Én række i træet.
    /// </summary>
    /// <remarks>
    /// Projektets ID ligger i <c>Mappe</c> og ikke i navnet. To projekter må
    /// gerne hedde det samme — det er id'et, der åbner det rigtige.
    /// </remarks>
    private static Biblioteker.Biblioteksnode Projektnode(Projekt p)
    {
        return Biblioteker.Biblioteksnode.Projektnode(p.Navn, p.Id);
    }

    private static string? Id(Biblioteker.Biblioteksnode k) => k.Mappe;

    // ------------------------------------------------------------ visningen

    private void Trae_Valgt(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_byggerTrae) return;
        if (e.NewValue is not Biblioteker.Biblioteksnode knude) return;

        Vis(_projekter.FirstOrDefault(p => p.Id == Id(knude)));
    }

    private void Vis(Projekt? p)
    {
        _valgt = p;

        Faner.Visibility = p is null ? Visibility.Collapsed : Visibility.Visible;
        Intet.Visibility = p is null ? Visibility.Visible : Visibility.Collapsed;

        if (p is null) return;

        // FELTERNE FYLDES UDEN AT DET TAELLER SOM EN RETTELSE. Uden det ville
        // «Gem» lyse op, hver gang man klikkede paa et andet projekt.
        _fylderFelter = true;

        Navn.Text = p.Navn;
        Beskrivelse.Text = p.Beskrivelse;
        ISoegning.IsChecked = p.MedISoegning;
        SomKilde.IsChecked = p.MaaSendesSomKilde;

        _fylderFelter = false;

        GemKnap.IsEnabled = false;

        ArkivKnap.Content = p.Status == Projektstatus.Aktiv
            ? Sprog.T("projekterview.arkiver")
            : Sprog.T("projekterview.genaktiver");

        EgenSti.Text = p.Dokumentmappe;

        Mapper.ItemsSource = p.Mapper.ToList();
        IngenMapper.Visibility = p.Mapper.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        VisFiler(p);
    }

    /// <summary>Én fil på skærmen.</summary>
    private sealed record Filvisning(string Navn, string Linje, string Grund, Visibility Advarsel);

    private void VisFiler(Projekt p)
    {
        var filer = Projektkilder.Filer(p);

        Filer.ItemsSource = filer
            .OrderByDescending(f => f.Egen)
            .ThenBy(f => f.Filnavn, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => new Filvisning(
                f.Filnavn,
                Linje(f),
                f.Afvist ?? "",
                f.Laesbar ? Visibility.Collapsed : Visibility.Visible))
            .ToList();

        IngenFiler.Visibility = filer.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string Linje(Projektfil f)
    {
        var dele = new List<string>
        {
            f.Egen ? Sprog.T("projekterview.egen_mappe") : Path.GetDirectoryName(f.Sti) ?? "",
            Stoerrelse(f.Bytes),
            f.Aendret.ToString("d. MMM yyyy", Sprog.Kultur),
        };

        if (!f.Laesbar) dele.Add(Sprog.T("projekterview.kan_ikke_laeses"));

        return string.Join("  ·  ", dele.Where(d => d.Length > 0));
    }

    private static string Stoerrelse(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };

    // ------------------------------------------------------------ handlinger

    private void Nyt_Klik(object sender, RoutedEventArgs e)
    {
        // INGEN DIALOG. Et projekt bliver til ved at blive lavet, og navnet
        // skrives dér, hvor det skal staa bagefter - ikke i en rude, man
        // foerst skal igennem. Se hvad der sker nedenunder.
        var p = Projektlager.Opret("Nyt projekt");
        Indlaes(p.Id);

        // NAVNET SKAL SKRIVES MED DET SAMME. Et projekt, der hedder «Nyt
        // projekt», er ikke til at kende fra det naeste.
        Faner.SelectedIndex = 1;
        Navn.Focus();
        Navn.SelectAll();
    }

    private void Rettet(object sender, TextChangedEventArgs e)
    {
        if (_fylderFelter || _valgt is null) return;
        GemKnap.IsEnabled = true;
    }

    private void Gem_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var navn = Navn.Text.Trim();
        if (navn.Length == 0) navn = "Nyt projekt";

        _valgt.Navn = navn;
        _valgt.Beskrivelse = Beskrivelse.Text.Trim();

        Projektlager.Gem(_valgt);

        GemKnap.IsEnabled = false;
        Indlaes(_valgt.Id);
    }

    /// <summary>
    /// De to kontakter gemmes med det samme.
    /// </summary>
    /// <remarks>
    /// Et hak, der skal gemmes bagefter, er et hak, man tror er sat. Det
    /// gælder især dét, der afgør, om materialet forlader maskinen.
    /// </remarks>
    private void Kontakt_Klik(object sender, RoutedEventArgs e)
    {
        if (_fylderFelter || _valgt is null) return;

        _valgt.MedISoegning = ISoegning.IsChecked == true;
        _valgt.MaaSendesSomKilde = SomKilde.IsChecked == true;

        Projektlager.Gem(_valgt);
    }

    private void AabnEgen_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        Directory.CreateDirectory(_valgt.Dokumentmappe);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_valgt.Dokumentmappe}\"")
        { UseShellExecute = true });
    }

    private void TilknytMappe_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Sprog.T("projekterview.tilknyt_mappe"),
            Multiselect = false,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;

        var sti = dialog.FolderName;

        // PROJEKTETS EGEN MAPPE ER IKKE EN TILKNYTNING. Blev den tilknyttet,
        // ville hver fil staa to gange i fundamentet.
        if (Ligger(sti, _valgt.Mappe))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                "Den mappe hører allerede til",
                "Det er projektets egen mappe. Den er med i forvejen.",
                Dialogs.Slags.Valg);
            return;
        }

        if (_valgt.Mapper.Any(m => string.Equals(m, sti, StringComparison.OrdinalIgnoreCase)))
            return;

        _valgt.Mapper.Add(sti);
        Projektlager.Gem(_valgt);
        Vis(_valgt);
    }

    private static bool Ligger(string sti, string under)
    {
        try
        {
            var a = Path.GetFullPath(sti).TrimEnd('\\');
            var b = Path.GetFullPath(under).TrimEnd('\\');

            return a.Equals(b, StringComparison.OrdinalIgnoreCase)
                || a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void FjernMappe_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null || sender is not Button { Tag: string sti }) return;

        _valgt.Mapper.RemoveAll(m => string.Equals(m, sti, StringComparison.OrdinalIgnoreCase));
        Projektlager.Gem(_valgt);
        Vis(_valgt);
    }

    private void Arkiv_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        if (_valgt.Status == Projektstatus.Aktiv) Projektlager.Arkiver(_valgt);
        else Projektlager.Genaktiver(_valgt);

        Indlaes(_valgt.Id);
    }

    private void Slet_Klik(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet projektet «{_valgt.Navn}»?",
            $"{_valgt.Mappe}\n\nProjektets egne dokumenter slettes med. "
            + "Møder, noter og tilknyttede mapper røres ikke — de ligger andre steder.\n\n"
            + "Skal det bare væk fra listen, så arkivér det i stedet.",
            godkend: "Slet projektet", annuller: "Behold det",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        Projektlager.Slet(_valgt);
        _valgt = null;
        Indlaes();
    }
}
