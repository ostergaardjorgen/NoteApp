using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
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

    public DocumentsView(string? aabnId = null) : this(aabnId, 0) { }

    /// <param name="position">
    /// Tegnnummeret i dokumentets tekst, der skal springes til. Nul betyder
    /// «vis bare dokumentet». Kommer fra søgningen, hvor man klikkede på ét
    /// bestemt sted frem for på dokumentet som helhed.
    /// </param>
    public DocumentsView(string? aabnId, int position)
    {
        InitializeComponent();
        Indlæs(aabnId);

        // Springet sker foerst, naar teksten er tegnet. Et opslag paa
        // position i en TextBox, der endnu ikke har maalt sit indhold, giver
        // ingenting - og saa lander man oeverst uden at vide hvorfor.
        if (position > 0) Loaded += (_, _) => SpringTil(position);

        // Koerslen er startet fra en anden skaerm og lever i BackgroundJobs.
        // Her lyttes der bare med, saa bjaelken viser det samme, uanset hvor
        // man staar - og saa den forsvinder igen, naar dokumentet er der.
        Jobs.BackgroundJobs.Ændret += Job_Aendret;
        Jobs.BackgroundJobs.DokumentFærdigt += Dokument_Faerdigt;

        Unloaded += (_, _) =>
        {
            Jobs.BackgroundJobs.Ændret -= Job_Aendret;
            Jobs.BackgroundJobs.DokumentFærdigt -= Dokument_Faerdigt;
        };

        // Er der allerede en koersel i gang, naar skaermen aabnes, skal
        // bjaelken staa med det samme - ikke foerst ved naeste melding.
        if (Jobs.BackgroundJobs.Kører)
            Job_Aendret(new Jobs.JobStatus("Dokument",
                Jobs.BackgroundJobs.HvadKører ?? "Laver dokument …", true));
    }

    private void Job_Aendret(Jobs.JobStatus j)
    {
        Dispatcher.Invoke(() =>
        {
            Fremdriftsrude.Visibility = j.Kører ? Visibility.Visible : Visibility.Collapsed;
            if (!j.Kører) return;

            JobTekst.Text = j.Besked;
            JobDetaljer.Text = j.Detaljer;

            // Negativ procent betyder "vi ved ikke hvor langt" - saa skal
            // bjaelken vise arbejde frem for et tal, den ikke har.
            Fremdrift.IsIndeterminate = j.Procent < 0;
            if (j.Procent >= 0) Fremdrift.Value = j.Procent;
            JobTal.Text = j.Procent >= 0 ? $"{j.Procent:0} %" : "";
        });
    }

    private void Dokument_Faerdigt(string id) => Dispatcher.Invoke(() => Indlæs(id));

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

        // Panelet til hoejre laeses samtidig med traeet. To lister over de
        // samme filer maa ikke kunne komme ud af trit.
        VisSeneste();

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
            Forklaring.Visibility = Visibility.Collapsed;
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
    // ------------------------------------------------- seneste dokumenter

    /// <summary>Ét dokument i panelet til højre.</summary>
    public sealed record Senestevisning(string Titel, string Under, string Id);

    /// <summary>
    /// De nyeste dokumenter.
    ///
    /// Et dokument bliver læst kort efter, det er skrevet — og så er det ikke
    /// i træet, man leder, men efter «den dér, jeg lige lavede».
    ///
    /// Kun de seks nyeste. Listen er en genvej, ikke et arkiv; hele arkivet
    /// står i træet til venstre.
    /// </summary>
    private void VisSeneste()
    {
        List<Senestevisning> liste;

        try
        {
            liste = _alle
                .OrderByDescending(d => d.Created)
                .Take(6)
                .Select(d =>
                {
                    var dele = new List<string> { d.Created.LocalDateTime.ToString("dd-MM HH:mm") };

                    if (d.SourceTitle.Length > 0) dele.Add(d.SourceTitle);
                    else if (d.Mappe is { Length: > 0 } m) dele.Add(m);

                    return new Senestevisning(
                        d.Title.Length > 0 ? d.Title : "Uden navn",
                        string.Join("  ·  ", dele),
                        d.Id);
                })
                .ToList();
        }
        catch (Exception)
        {
            liste = new List<Senestevisning>();
        }

        Senesterude.ItemsSource = liste;
        IngenSeneste.Visibility = liste.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Vælger dokumentet.
    ///
    /// Der bygges ikke en ny skærm — vi ER på den. Se den tilsvarende i
    /// TranscribeView; de to paneler skal opføre sig ens.
    /// </summary>
    private void Seneste_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Senestevisning v) return;

        var d = _alle.FirstOrDefault(x => x.Id == v.Id);

        if (d is null)
        {
            // Slettet, mens skaermen stod aaben. Listen laeses forfra.
            Indlæs();
            return;
        }

        Vis(d);
    }

    private void Vis(DocumentInfo? valgtDokument)
    {
        if (valgtDokument is not { } d)
        {
            _valgt = null;
            SletKnap.IsEnabled = AabnKnap.IsEnabled = GemKnap.IsEnabled = OmdoebKnap.IsEnabled = FlytKnap.IsEnabled = false;

            // RUDEN STOD TOM HER.
            // TomPanel gaelder kun, naar der slet ikke findes dokumenter.
            // Var der ét, og stod markeringen ikke paa det, sagde skaermen
            // ingenting - og det er netop foerste gang, man kommer herover.
            Detaljer.Visibility = Visibility.Collapsed;
            Forklaring.Visibility = _alle.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        _indlæser = true;
        _valgt = d;

        TomPanel.Visibility = Visibility.Collapsed;
        Forklaring.Visibility = Visibility.Collapsed;
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

    /// <summary>
    /// Ruller hen til et bestemt sted i dokumentteksten og markerer det.
    ///
    /// Teksten ligger i en TextBox INDE i en ScrollViewer, så den har ingen
    /// egen rulning. Derfor slås stedets placering op i tekstfeltet og
    /// regnes om til ruderens koordinater — ScrollToLine ville ikke gøre
    /// noget her.
    /// </summary>
    private void SpringTil(int position)
    {
        if (position < 0 || position >= Indhold.Text.Length) return;

        Indhold.Focus();
        Indhold.Select(position, Math.Min(60, Indhold.Text.Length - position));

        try
        {
            var kasse = Indhold.GetRectFromCharacterIndex(position);
            var punkt = Indhold.TransformToAncestor(Detaljer).Transform(new Point(0, kasse.Top));

            // Et stykke over stedet, saa man kan se, hvad der staar FOER det.
            // Lander linjen oeverst i ruden, mangler sammenhaengen.
            Detaljer.ScrollToVerticalOffset(Math.Max(0, Detaljer.VerticalOffset + punkt.Y - 120));
        }
        catch (InvalidOperationException)
        {
            // Er teksten ikke maalt endnu, bliver man staaende oeverst.
            // Markeringen er der stadig, saa stedet kan findes med rulning.
        }
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
            var fraMappe = _valgt.Mappe;
            _valgt.Mappe = vindue.Valgt;
            DocumentStore.Save(_valgt);

            Historik.Skriv(HaendelseType.Flyttet, $"Dokument flyttet: {_valgt.Title}",
                $"Fra «{fraMappe ?? "roden"}» til «{vindue.Valgt ?? "roden"}».",
                Udfald.Fuldført, kilde: _valgt.Id);

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
            "Selve optagelsen og transkriptionen bliver liggende — det er kun dokumentet, der slettes. " +
            "Du kan lave et nyt af den samme optagelse.",
            godkend: "Slet dokumentet", annuller: "Behold det",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            // SLETNINGEN LOGGES, FOER FILEN ER VAEK.
            // Der er ingen papirkurv. Linjen i historikken er derfor det
            // eneste spor af, at dokumentet fandtes - og id'et goer, at en
            // aeldre linje om det samme dokument stadig kan kendes.
            var slettetNavn = _valgt.Title;
            var slettetId = _valgt.Id;

            DocumentStore.Delete(_valgt);

            Historik.Skriv(HaendelseType.Slettet, $"Dokument slettet: {slettetNavn}",
                "Der er ingen papirkurv — filen er væk.",
                Udfald.Fuldført, kilde: slettetId);

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
