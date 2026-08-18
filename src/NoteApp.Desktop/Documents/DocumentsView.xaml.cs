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

    /// <summary>Den valgte knude i træet. Null indtil træet er bygget.</summary>
    private Biblioteker.Biblioteksnode? _valgtKnude;

    private Biblioteker.Biblioteksnode _rod = null!;
    private bool _byggerTrae;

    /// <summary>
    /// Bygger træet og vælger et dokument.
    ///
    /// Samme opbygning som «Optagelser» — se TranscribeView for hvorfor
    /// træet bygges forfra hver gang, og hvad der skal sættes tilbage
    /// bagefter.
    ///
    /// Der er ét bibliotek, ikke to. Dokumenter har ikke et arkiv: et
    /// dokument er færdigt i det øjeblik, det er lavet.
    /// </summary>
    private void Indlæs(string? vælgId = null)
    {
        _alle = DocumentStore.LoadAll().ToList();

        // En mappe, der er i brug, skal staa i traeet - ogsaa hvis
        // mapper.json er gaaet tabt. Ellers ville dokumenterne falde ud af
        // deres mappe, uden at nogen havde flyttet dem.
        NoteApp.Core.Mapper.SikrFindes(NoteApp.Core.Mapper.Slags.Dokumenter, _alle.Select(d => d.Mappe));

        _byggerTrae = true;

        var udfoldet = AlleKnuder().Where(k => k.ErUdfoldet).Select(Noegle).ToHashSet();
        var varValgt = vælgId is not null ? "d:" + vælgId
                     : _valgtKnude is not null ? Noegle(_valgtKnude)
                     : null;

        _rod = Biblioteker.Biblioteksnode.Bibliotek("Dokumenter", "\uE8F1", Transcribe.Gruppe.Moede);
        _rod.Antal = _alle.Count;

        foreach (var m in NoteApp.Core.Mapper.Alle(NoteApp.Core.Mapper.Slags.Dokumenter))
        {
            var mappe = Biblioteker.Biblioteksnode.Mappenode(m, Transcribe.Gruppe.Moede);
            foreach (var d in _alle.Where(d => m.Equals(d.Mappe, StringComparison.CurrentCultureIgnoreCase)))
                mappe.Boern.Add(Biblioteker.Biblioteksnode.Dokumentnode(d));

            mappe.Antal = mappe.Boern.Count;
            _rod.Boern.Add(mappe);
        }

        foreach (var d in _alle.Where(d => string.IsNullOrWhiteSpace(d.Mappe)))
            _rod.Boern.Add(Biblioteker.Biblioteksnode.Dokumentnode(d));

        Trae.ItemsSource = new[] { _rod };

        foreach (var k in AlleKnuder().Where(k => k.ErBeholder))
            k.ErUdfoldet = udfoldet.Contains(Noegle(k));

        var igen = varValgt is null ? null : AlleKnuder().FirstOrDefault(k => Noegle(k) == varValgt);

        _valgtKnude = igen;
        if (igen is not null)
        {
            igen.ErValgt = true;
            foreach (var f in Forfaedre(igen)) f.ErUdfoldet = true;
        }

        _byggerTrae = false;

        TomPanel.Visibility = _alle.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_alle.Count == 0)
        {
            Detaljer.Visibility = Visibility.Collapsed;
            Status.Text = $"Dokumenter gemmes i {DocumentStore.Directory}";
            return;
        }

        Vis(_valgtKnude?.Dokument);
    }

    /// <summary>
    /// En knudes identitet paa tvaers af to opbygninger af traeet. Objekterne
    /// er nye hver gang, saa referencer duer ikke.
    /// </summary>
    private static string Noegle(Biblioteker.Biblioteksnode k) =>
        k.Dokument is { } d ? "d:" + d.Id : "b:" + (k.Mappe ?? "");

    private IEnumerable<Biblioteker.Biblioteksnode> AlleKnuder() =>
        Trae.ItemsSource is null
            ? Enumerable.Empty<Biblioteker.Biblioteksnode>()
            : Trae.Items.OfType<Biblioteker.Biblioteksnode>().SelectMany(r => r.MedBoern());

    private IEnumerable<Biblioteker.Biblioteksnode> Forfaedre(Biblioteker.Biblioteksnode k)
    {
        foreach (var rod in Trae.Items.OfType<Biblioteker.Biblioteksnode>())
        {
            if (rod.Boern.Contains(k)) { yield return rod; yield break; }

            foreach (var mappe in rod.Boern.Where(b => b.ErBeholder))
            {
                if (!mappe.Boern.Contains(k)) continue;
                yield return rod;
                yield return mappe;
                yield break;
            }
        }
    }

    private void Trae_Valgt(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_byggerTrae) return;
        if (e.NewValue is not Biblioteker.Biblioteksnode knude) return;

        _valgtKnude = knude;
        Vis(knude.Dokument);
    }

    private void NyMappe_Click(object sender, RoutedEventArgs e)
    {
        var vindue = Transcribe.RenameWindow.TilNyMappe();
        vindue.Owner = Window.GetWindow(this);

        if (vindue.ShowDialog() != true) return;

        var navn = vindue.NytNavn;

        if (!NoteApp.Core.Mapper.Opret(NoteApp.Core.Mapper.Slags.Dokumenter, navn))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Den findes allerede",
                $"Der er allerede en mappe, der hedder «{navn.Trim()}».", Dialogs.Slags.Valg);
            return;
        }

        _valgtKnude = null;
        Indlæs();

        // Den nye mappe foldes ud og markeres. Den er tom, og det skal man
        // kunne se - ellers ligner det, at der ikke skete noget.
        var ny = AlleKnuder().FirstOrDefault(k => k.ErBeholder && k.Mappe == navn.Trim());
        if (ny is null) return;

        _rod.ErUdfoldet = true;
        _valgtKnude = ny;
        ny.ErValgt = true;
        Vis(null);
    }

    // --------------------------------------------------------- traek og slip

    private System.Windows.Point _traekStart;

    private void Trae_MusNed(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _traekStart = e.GetPosition(null);

    private void Trae_MusBevaeget(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

        var flyttet = e.GetPosition(null) - _traekStart;
        if (Math.Abs(flyttet.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(flyttet.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (e.OriginalSource is not DependencyObject kilde) return;
        if (FindOpad<TreeViewItem>(kilde) is not { } punkt) return;
        if (punkt.DataContext is not Biblioteker.Biblioteksnode { Dokument: { } d }) return;

        DragDrop.DoDragDrop(punkt, new DataObject(typeof(DocumentInfo), d), DragDropEffects.Move);
    }

    private static T? FindOpad<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null and not T) d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        return d as T;
    }

    private void Trae_TraekOver(object sender, DragEventArgs e)
    {
        var maal = MaalUnderMusen(e);
        foreach (var k in AlleKnuder()) k.ErDropmaal = ReferenceEquals(k, maal);

        e.Effects = maal is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void Trae_TraekForlod(object sender, DragEventArgs e) => RydDropmaal();

    private void RydDropmaal()
    {
        foreach (var k in AlleKnuder()) k.ErDropmaal = false;
    }

    private Biblioteker.Biblioteksnode? MaalUnderMusen(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(DocumentInfo))) return null;

        var ramt = Trae.InputHitTest(e.GetPosition(Trae)) as DependencyObject;
        var knude = FindOpad<TreeViewItem>(ramt)?.DataContext as Biblioteker.Biblioteksnode;

        return knude is { ErBeholder: true } ? knude : null;
    }

    /// <summary>
    /// Slipper et dokument i en mappe — eller i roden, som tager det ud af
    /// den mappe, det lå i. Samme regler som i «Optagelser».
    /// </summary>
    private void Trae_Slip(object sender, DragEventArgs e)
    {
        RydDropmaal();

        var maal = MaalUnderMusen(e);
        if (maal is null) return;
        if (e.Data.GetData(typeof(DocumentInfo)) is not DocumentInfo d) return;

        d.Mappe = maal.Mappe;
        DocumentStore.Save(d);

        maal.ErUdfoldet = true;
        Indlæs(d.Id);

        Status.Text = maal.Mappe is null
            ? $"«{d.Title}» ligger nu uden mappe."
            : $"«{d.Title}» er flyttet til «{maal.Navn}».";
    }

    /// <summary>
    /// Saetter skaermen efter det, der er valgt i traeet. Kaldes ogsaa med
    /// null, naar markeringen staar paa en mappe - saa skal knapperne blive
    /// graa, og detaljeruden skal vaek.
    /// </summary>
    private void Vis(DocumentInfo? valgtDokument)
    {
        if (valgtDokument is not { } d)
        {
            _valgt = null;
            SletKnap.IsEnabled = AabnKnap.IsEnabled = GemKnap.IsEnabled = OmdoebKnap.IsEnabled = FlytKnap.IsEnabled = false;
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

        MappeNavn.Text = string.IsNullOrWhiteSpace(d.Mappe) ? "(ingen)" : d.Mappe;

        var fil = DocumentStore.Path_(d);
        var findes = File.Exists(fil);
        Filnavn.Text = findes ? d.FileName : $"{d.FileName} — filen mangler";

        Indhold.Text = d.Markdown.Length > 0 ? d.Markdown : "(intet gemt indhold)";

        _indlæser = false;

        SletKnap.IsEnabled = true;
        OmdoebKnap.IsEnabled = true;
        FlytKnap.IsEnabled = true;
        AabnKnap.IsEnabled = findes;
        GemKnap.IsEnabled = false;
        Status.Text = findes ? fil : "Dokumentfilen findes ikke længere — kun oplysningerne om den.";
    }

    private void Beskrivelse_Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = _valgt is not null;
    }

    /// <summary>
    /// Omdøber dokumentet. Titlen skrives ind i selve Word-filen, så den
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke omdøbe", $"Navnet kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>
    /// Flytter dokumentet til en mappe. Kun feltet ændrer sig — filen bliver
    /// liggende, hvor den er, så gemte stier og sikkerhedskopier stadig virker.
    /// </summary>
    private void Flyt_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var vindue = new Dialogs.MappeVaelger(
            NoteApp.Core.Mapper.Slags.Dokumenter, _valgt.Mappe, "dokumentet")
        { Owner = Window.GetWindow(this) };

        if (vindue.ShowDialog() != true) return;

        try
        {
            _valgt.Mappe = vindue.Valgt;
            DocumentStore.Save(_valgt);

            var id = _valgt.Id;
            Indlæs(id);

            Status.Text = vindue.Valgt is null
                ? "Dokumentet ligger nu uden mappe."
                : $"Flyttet til «{vindue.Valgt}».";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke flytte",
                ex.Message, Dialogs.Slags.Pas_paa);
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
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke gemme", $"Beskrivelsen kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    private void Aabn_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var fil = DocumentStore.Path_(_valgt);
        if (!File.Exists(fil))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke åbne", "Filen findes ikke længere.", Dialogs.Slags.Valg);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(fil) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke åbne", $"Dokumentet kunne ikke åbnes.\n\n{ex.Message}\n\n" +
                "Er der ikke noget program til .docx-filer, kan de åbnes i Word, LibreOffice eller Google Docs.", Dialogs.Slags.Valg);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet «{_valgt.Title}»?",
            "Selve optagelsen og udskriften bliver liggende — det er kun dokumentet, der slettes. " +
            "Du kan lave et nyt af den samme optagelse.",
            godkend: "Slet dokumentet", annuller: "Behold det",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            DocumentStore.Delete(_valgt);
            Status.Text = "Dokumentet er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke slette", $"Dokumentet kunne ikke slettes.\n\n{ex.Message}", Dialogs.Slags.Pas_paa);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(DocumentStore.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DocumentStore.Directory}\"")
        { UseShellExecute = true });
    }
}
