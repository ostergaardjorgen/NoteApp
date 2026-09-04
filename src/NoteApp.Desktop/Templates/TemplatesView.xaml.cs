using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        // Felterne er knapper, ikke en liste at laese. Forklaringen fra
        // PromptTemplate.Fields bliver til hjaelpeteksten paa hver enkelt.
        // DELTAGERREGLER ER IKKE MED HER.
        // Feltet erstattes kun i systemprompten. Blev det sat ind paa
        // materialefanen, blev det byttet ud med ingenting - reglerne
        // forsvandt uden en lyd. Knappen til det staar paa Instruktion.
        Felter.ItemsSource = PromptTemplate.Fields
            .Where(f => f.Key != Deltagerregler.Felt)
            .Select(f => new
            {
                Navn = Feltnavn(f.Key),
                Felt = "{{" + f.Key + "}}",
                Forklaring = f.Value + "\n\nSættes ind som {{" + f.Key + "}}"
            })
            .ToList();

        // De indbyggede skabeloner lægges i datamappen, hvis de ikke er der.
        // Uden dette står skærmen tom første gang, og så ser det ud, som om
        // appen ikke kan noget — mens den i virkeligheden bare ikke har pakket
        // ud endnu.
        try { DraftStore.SeedTemplates(); } catch (Exception) { /* vises som tom liste */ }
        Indlæs();
    }

    /// <summary>
    /// Feltets navn på almindeligt dansk.
    /// </summary>
    /// <remarks>
    /// KNAPPERNE HED «{{transskription}}». Det er appens eget sprog, ikke
    /// brugerens — og skærmen skal kunne bruges af en, der bare vil optage sit
    /// møde og have et referat ud af det. Pladsholderen bliver stadig sat ind i
    /// teksten; den står nu i hjælpeteksten frem for på knappen.
    ///
    /// Er feltet ikke i tabellen, står nøglen som før. Så bliver et nyt felt
    /// synligt frem for at forsvinde.
    /// </remarks>
    private static string Feltnavn(string noegle) => noegle switch
    {
        "transskription" => "Mødets tekst",
        "titel" => "Mødets titel",
        "dato" => "Dato og tid",
        "varighed" => "Mødets længde",
        "noter" => "Dine noter",
        "sprog" => "Sprog",
        "kilde" => "Link til webinaret",
        "ordbog" => "Ordliste",
        "sprogregler" => "Dokumentets sprog",
        _ => noegle
    };

    /// <summary>
    /// Tokens for den valgte længde.
    /// </summary>
    /// <remarks>
    /// Sat af <see cref="Indlaes"/> til det, der STÅR i filen — ikke til det
    /// punkt, der blev markeret. En gammel skabelon med 2.048 skal ikke blive
    /// til 2.500, fordi nogen kiggede på skærmen. Først når man selv vælger et
    /// punkt, skifter tallet.
    /// </remarks>
    private int _valgtLaengde;

    private void Laengde_Valgt(object sender, SelectionChangedEventArgs e)
    {
        if (_indlæser) return;
        if (FeltLaengde.SelectedItem is not Laengdepunkt l) return;

        _valgtLaengde = l.Tokens;

        GemKnap.IsEnabled = true;
    }

    /// <summary>Et valg i «Hvor langt må dokumentet blive?».</summary>
    public sealed record Laengdepunkt(string Navn, int Tokens);

    /// <summary>
    /// Længderne, oversat fra tokens til sider.
    /// </summary>
    /// <remarks>
    /// HER STOD ET FELT MED «32000» I. Hjælpeteksten forklarede, at der er
    /// cirka tre tegn pr. token — altså skulle man selv gange sig frem til,
    /// hvor langt et referat blev. Det kan man ikke svare på uden at vide,
    /// hvad en sprogmodel er.
    ///
    /// Regnestykket: cirka 3 tegn pr. token, og en almindelig A4-side med
    /// tekst er cirka 2.500 tegn — altså rundt regnet 800 tokens pr. side.
    /// Tallene herunder er rundet op til pæne tal, fordi de er et LOFT og ikke
    /// et mål: er mødet kort, bliver dokumentet kort uanset hvad.
    ///
    /// Det er de samme tal, der gemmes i filen som før. Kun spørgsmålet er
    /// skiftet.
    /// </remarks>
    private static readonly Laengdepunkt[] Laengder =
    {
        new("Kort — cirka 1 side", 800),
        new("Almindelig — cirka 2-3 sider", 2500),
        new("Lang — cirka 5 sider", 4500),
        new("Meget lang — cirka 10 sider", 9000),
        new("Så langt som nødvendigt", 32000)
    };

    /// <summary>
    /// Det valg, der passer bedst til det tal, der står i filen.
    /// </summary>
    /// <remarks>
    /// EN GAMMEL SKABELON HAR ET VILKÅRLIGT TAL — 2048, 32000, hvad som helst.
    /// Den skal ikke rettes bag om ryggen på nogen, så der vælges det
    /// nærmeste punkt, og tallet i filen bliver stående, indtil man selv
    /// vælger noget andet. Se Gem_Click.
    /// </remarks>
    private static Laengdepunkt Naermeste(int tokens) =>
        Laengder.OrderBy(l => Math.Abs(l.Tokens - tokens)).First();

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

        _rod = Biblioteker.Biblioteksnode.Bibliotek("Mødetyper", "", Transcribe.Gruppe.Moede);
        _rod.Antal = _skabeloner.Count;
        foreach (var t in _skabeloner)
            _rod.Boern.Add(Biblioteker.Biblioteksnode.Skabelonnode(t));

        Trae.ItemsSource = new[] { _rod };
        _rod.ErUdfoldet = varUdfoldet;

        _byggerTrae = false;

        if (_skabeloner.Count == 0)
        {
            Status.Text = $"Ingen mødetyper i {PromptTemplate.Directory}. Tryk «Ny» for at lave den første.";
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

    // Standarddagsordenen og selve Mistral-kaldet laa her. De er flyttet til
    // Dagsordensskriver, fordi guiden til nye skabeloner skal bruge praecis
    // det samme kald - stod prompten to steder, ville de to veje langsomt
    // give hver sit resultat.
    private static string Agenda() => Dagsordensskriver.Standard();

    /// <summary>
    /// Saetter et felt ind, hvor markoeren staar i opgaveteksten.
    ///
    /// Feltlisten var foer en fane for sig med en raekke navne, man kunne
    /// laese. Den svarede ikke paa det, man staar med - hvor skal de hen? -
    /// og man skulle skrive de kroellede parenteser af i haanden. Nu staar
    /// listen under den tekst, felterne hoerer til, og et klik goer arbejdet.
    /// </summary>
    private void Felt_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string felt) return;

        var pos = FeltBruger.CaretIndex;
        FeltBruger.Text = FeltBruger.Text.Insert(pos, felt);

        // Markoeren skal staa EFTER det indsatte, saa man kan skrive videre.
        FeltBruger.CaretIndex = pos + felt.Length;
        FeltBruger.Focus();
    }

    /// <summary>
    /// Sætter de fælles deltagerregler ind i instruktionen, hvor markøren
    /// står. Egen metode, fordi den skriver i et andet felt end Felt_Klik.
    /// </summary>
    private void Systemfelt_Klik(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string felt) return;

        var pos = FeltSystem.CaretIndex;
        FeltSystem.Text = FeltSystem.Text.Insert(pos, felt);
        FeltSystem.CaretIndex = pos + felt.Length;
        FeltSystem.Focus();
    }

    /// <summary>
    /// Viser den dagsorden, der hører til den valgte skabelon — eller
    /// standarden, hvis den ikke har sin egen.
    /// </summary>
    private void VisAgenda()
    {
        var navn = _valgt?.Name;
        var egen = navn is not null && Agendaer.HarEgen(navn);

        FeltAgenda.Text = navn is null ? Agenda() : Agendaer.Hent(navn, Agenda());

        AgendaOverskrift.Text = egen
            ? $"Dagsorden til «{navn}»"
            : "Hvorfor en fast dagsorden";

        AgendaNulstilKnap.Visibility = egen ? Visibility.Visible : Visibility.Collapsed;
        AgendaTilpasKnap.IsEnabled = navn is not null && SkyNoegle.Hent() is not null;
    }

    /// <summary>
    /// Beder Mistral skrive en dagsorden, der passer til netop denne
    /// skabelon.
    ///
    /// Standarden gives med som udgangspunkt frem for at bede om en fra
    /// bunden. De fem ting, den sikrer — navne, formål, beslutninger sagt
    /// højt, det ingen ved, og hvem der følger op — gælder alle møder, og de
    /// skal ikke kunne forsvinde, fordi en model syntes, den kunne gøre det
    /// bedre.
    /// </summary>
    private async void TilpasAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is not { } t) return;

        var noegle = SkyNoegle.Hent();
        if (noegle is null) return;

        AgendaTilpasKnap.IsEnabled = false;
        Status.Text = "Mistral tilpasser dagsordenen …";

        try
        {
            Agendaer.Gem(t.Name, await Dagsordensskriver.SkrivAsync(noegle, t.Name, t.SystemPrompt));
            VisAgenda();

            // Instruktionen og dagsordenen passer sammen nu. Saa skal der
            // ikke mindes om noget, foer den bliver aendret igen.
            _systemVedIndlaesning = t.SystemPrompt;
            Status.Text = $"Dagsordenen er tilpasset «{t.Name}». Læs den igennem, før du bruger den.";
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke tilpasse dagsordenen",
                ex.Message, Dialogs.Slags.Pas_paa);
            Status.Text = "";
        }
        finally
        {
            AgendaTilpasKnap.IsEnabled = SkyNoegle.Hent() is not null;
        }
    }

    /// <summary>
    /// Spørger, om dagsordenen skal følge med den ændrede instruktion.
    ///
    /// Det er et spørgsmål og ikke en automatik: en dagsorden, man selv har
    /// rettet i hånden, må ikke blive skrevet over, fordi man rettede et komma
    /// i instruktionen.
    /// </summary>
    private void MindOmDagsorden()
    {
        var navn = _valgt?.Name;
        if (navn is null) return;

        var egen = Agendaer.HarEgen(navn);

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            "Skal dagsordenen følge med?",
            $"Du har ændret instruktionen til «{navn}».\n\n" +
            "Dagsordenen bestemmer, hvad der bliver sagt på mødet. Beder den ikke om " +
            "det, den nye instruktion har brug for, står felterne tomme i dokumentet — " +
            "transkriptionen kan kun indeholde det, nogen sagde højt.\n\n" +
            (egen
                ? "Den nuværende dagsorden bliver skrevet over."
                : "Mødetypen bruger standarddagsordenen. Den bliver liggende — der laves en egen."),
            "Tilpas dagsordenen nu", "Ikke nu", Dialogs.Slags.Valg);

        if (!ja) return;

        AgendaFane.IsSelected = true;
        TilpasAgenda_Click(this, new RoutedEventArgs());
    }

    private void NulstilAgenda_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is not { } t) return;

        Agendaer.Slet(t.Name);
        VisAgenda();
        Status.Text = $"«{t.Name}» bruger standarddagsordenen igen.";
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

        // MAPPERNE HENTES HVER GANG. De aendrer sig, mens appen koerer - en
        // liste, der blev fyldt ved opstart, ville mangle den mappe, man lige
        // har lavet.
        FyldMapper(t.Mappe);
        FeltBeskrivelse.Text = t.Description ?? "";
        // TEMPERATUREN VISES IKKE LAENGERE. Den staar stadig i filen og bruges
        // uaendret - se kommentaren i TemplatesView.xaml.
        FeltLaengde.ItemsSource = Laengder;
        FeltLaengde.SelectedItem = Naermeste(t.MaxTokens);
        _valgtLaengde = t.MaxTokens;
        FeltSystem.Text = t.SystemPrompt;
        FeltBruger.Text = t.UserPrompt;

        // HER BLEV SKABELONENS PreferredModel LAEST IND I EN COMBOBOX.
        // Feltet findes stadig i filformatet, saa gamle skabeloner kan laeses,
        // men det peger paa lokale gguf-filer, som ikke bruges laengere.

        _indlæser = false;
        GemKnap.IsEnabled = false;
        Status.Text = t.Path is null ? "Indbygget mødetype" : t.Path;

        // EFTER _valgt er sat, ikke foer. Kaldet laa i Indlaes, hvor _valgt
        // endnu var null - saa "Tilpas til denne mødetype" var graa paa hver
        // eneste skabelon, fordi den spurgte om et navn, der ikke fandtes
        // endnu.
        VisAgenda();

        // Aendrer man instruktionen, passer dagsordenen maaske ikke laengere.
        _systemVedIndlaesning = t.SystemPrompt;
    }

    /// <summary>
    /// Instruktionen, som den saa ud da skabelonen blev aabnet.
    ///
    /// Bruges til at opdage, om DEN er aendret ved et gem — og kun den.
    /// Retter man et navn eller en temperatur, har dagsordenen intet med det
    /// at goere, og saa skal der ikke mindes om noget.
    /// </summary>
    private string? _systemVedIndlaesning;

    /// <summary>Mappen, mødetypen peger på. Null betyder «uden mappe».</summary>
    private string? _mappe;

    /// <summary>
    /// Viser den valgte mappe.
    /// </summary>
    /// <remarks>
    /// EN MAPPE, DER ER SLETTET, SKAL STADIG KUNNE SES. Peger mødetypen på
    /// noget, der ikke findes længere, står stien alligevel og med
    /// «(findes ikke)» efter sig. Stod der bare ingenting, ville det ligne, at
    /// der aldrig var valgt noget — og næste gang nogen gemte, ville det være
    /// sandt.
    /// </remarks>
    private void FyldMapper(string? valgt)
    {
        _mappe = valgt;

        if (string.IsNullOrWhiteSpace(valgt))
        {
            MappeTekst.Text = NoteApp.Core.Mapper.Ingen;
            return;
        }

        var findes = NoteApp.Core.Mapper.Alle(NoteApp.Core.Mapper.Slags.Optagelser)
            .Any(m => m.Equals(valgt, StringComparison.CurrentCultureIgnoreCase));

        MappeTekst.Text = findes ? valgt : $"{valgt}  (findes ikke)";
    }

    private void VaelgMappe_Klik(object sender, RoutedEventArgs e)
    {
        var vindue = new Dialogs.MappeVaelger(
            NoteApp.Core.Mapper.Slags.Optagelser, _mappe, "optagelser med den her mødetype")
        { Owner = Window.GetWindow(this) };

        if (vindue.ShowDialog() != true) return;

        FyldMapper(vindue.Valgt);
        GemKnap.IsEnabled = true;
    }

    // -------------------------------------------------------------- ændring

    private void Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = true;
        TjekFelter();
    }

    /// <summary>
    /// Kontrollerer felterne i materialeteksten, mens der skrives.
    ///
    /// TRE FEJL, DER ELLERS FOERST VISER SIG I ET FAERDIGT DOKUMENT:
    ///
    /// 1. {{transskription}} mangler. Så beder skabelonen en model om at
    ///    skrive et referat af ingenting. Den fejl er altid en fejl, og der
    ///    kan ikke gemmes, før den er rettet.
    ///
    /// 2. Et felt findes ikke. {{deltagere}} ser rigtigt ud, men står ikke på
    ///    listen — og så sendes de krøllede parenteser ordret videre til
    ///    modellen, som gætter på, hvad de betyder.
    ///
    /// 3. {{deltagerregler}} står her i stedet for i instruktionen. Det
    ///    erstattes kun i systemprompten, så her bliver det byttet ud med
    ///    ingenting, og reglerne forsvinder uden en lyd.
    /// </summary>
    private void TjekFelter()
    {
        var tekst = FeltBruger.Text;

        var brugte = System.Text.RegularExpressions.Regex
            .Matches(tekst, @"\{\{\s*([a-zA-Z_æøåÆØÅ0-9]+)\s*\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var manglerUdskrift = !brugte.Contains("transskription", StringComparer.OrdinalIgnoreCase);
        var ukendte = brugte.Where(f => !PromptTemplate.Fields.ContainsKey(f)).ToList();
        var reglerForkert = brugte.Contains(Deltagerregler.Felt, StringComparer.OrdinalIgnoreCase);

        var linjer = new List<string>();

        if (manglerUdskrift)
            linjer.Add("{{transskription}} mangler. Uden det felt får modellen ikke selve " +
                       "transkriptionen, og dokumentet bliver skrevet på ingenting. Der kan ikke " +
                       "gemmes, før feltet er sat ind.");

        if (reglerForkert)
            linjer.Add("{{deltagerregler}} hører til under «Instruktion». Her bliver det byttet " +
                       "ud med ingenting, og reglerne når aldrig frem til modellen.");

        if (ukendte.Count > 0)
            linjer.Add((ukendte.Count == 1 ? "Feltet " : "Felterne ") +
                       string.Join(", ", ukendte.Select(f => "{{" + f + "}}")) +
                       (ukendte.Count == 1 ? " findes ikke" : " findes ikke") +
                       " og bliver sendt ordret videre til modellen. Brug knapperne herunder.");

        if (linjer.Count == 0)
        {
            // Naar alt er i orden, staar der hvilke oplysninger der faktisk
            // sendes. Det er svaret paa "goer den her fane en forskel?" -
            // den goer, og det er DET her, den goer.
            var navne = PromptTemplate.Fields.Keys
                .Where(k => k != Deltagerregler.Felt && brugte.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var ubrugte = PromptTemplate.Fields.Keys
                .Where(k => k != Deltagerregler.Felt && !brugte.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();

            Feltbesked.Text = $"Sendes med: {string.Join(", ", navne)}." +
                              (ubrugte.Count == 0
                                  ? ""
                                  : $" Sendes ikke: {string.Join(", ", ubrugte)}.");

            Saet(Feltkontrol, Feltbesked, "#FF181B22", "PanelKant", "TekstMeget");
        }
        else
        {
            Feltbesked.Text = string.Join("\n\n", linjer);
            Saet(Feltkontrol, Feltbesked,
                 manglerUdskrift ? "#33E5484D" : "#33E8A33D",
                 manglerUdskrift ? "FejlTekst" : "Advarsel",
                 "Tekst");
        }

        Feltkontrol.Visibility = Visibility.Visible;
        GemKnap.IsEnabled = GemKnap.IsEnabled && !manglerUdskrift;
    }

    private void Saet(Border ramme, TextBlock tekst, string baggrund, string kant, string skrift)
    {
        ramme.Background = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(baggrund));
        ramme.BorderBrush = (Brush)FindResource(kant);
        tekst.Foreground = (Brush)FindResource(skrift);
    }

    // ---------------------------------------------------------------- gem

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt is null) return;

        if (string.IsNullOrWhiteSpace(FeltNavn.Text))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler navn", "Mødetypen skal have et navn.", Dialogs.Slags.Pas_paa);
            return;
        }

        // HER LAA TO KONTROLLER AF INDTASTEDE TAL - temperatur og maks. tokens.
        // Begge felter er vaek: temperaturen spoerges der ikke om laengere, og
        // laengden er en liste, man vaelger i. Et valg i en liste kan ikke vaere
        // ulaeseligt, saa der er ikke noget at kontrollere.

        if (string.IsNullOrWhiteSpace(FeltSystem.Text))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler instruktion", "Instruktionen til modellen må ikke være tom — det er den, der afgør, hvad der kommer ud.", Dialogs.Slags.Pas_paa);
            return;
        }

        if (!FeltBruger.Text.Contains("{{transskription}}"))
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                "Mødet mangler i mødetypen",
                "Feltet {{transskription}} står ikke i teksten forneden. Uden det får modellen ikke " +
                "selve mødet at se, og udkastet bliver skrevet ud af ingenting.",
                godkend: "Gem alligevel", annuller: "Tilbage til teksten",
                slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

            if (!ja) return;
        }

        var gammelSti = _valgt.Path;

        _valgt.Name = FeltNavn.Text.Trim();
        _valgt.Description = string.IsNullOrWhiteSpace(FeltBeskrivelse.Text) ? null : FeltBeskrivelse.Text.Trim();
        _valgt.Mappe = _mappe;
        // Temperaturen roeres ikke - den staar, som den stod i filen.
        _valgt.MaxTokens = _valgtLaengde;
        _valgt.SystemPrompt = FeltSystem.Text.Trim();
        _valgt.UserPrompt = FeltBruger.Text.Trim();

        // SIDSTE SPAERRE. Knappen er graa, naar udskriften mangler, men et
        // gem kan ogsaa komme herind ad andre veje. En skabelon uden
        // {{transskription}} maa ikke naa filen - den ville producere et
        // dokument skrevet paa ingenting, og fejlen viser sig foerst dér.
        if (!_valgt.UserPrompt.Contains("{{transskription}}", StringComparison.OrdinalIgnoreCase))
        {
            MaterialeFane.IsSelected = true;
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Transkriptionen mangler",
                "Mødetypen kan ikke gemmes uden feltet {{transskription}} under " +
                "«Hvad modellen får». Uden det får modellen ikke selve transkriptionen af " +
                "mødet, og dokumentet bliver skrevet på ingenting.",
                Dialogs.Slags.Pas_paa);
            return;
        }

        try
        {
            var sti = _valgt.Save();

            // Er navnet aendret, skal den gamle fil vaek — ellers ligger
            // skabelonen to steder, og den naeste redigering rammer den ene af
            // dem uden at man kan se hvilken.
            if (gammelSti is not null && !gammelSti.Equals(sti, StringComparison.OrdinalIgnoreCase)
                                      && File.Exists(gammelSti))
                File.Delete(gammelSti);

            // AENDRET INSTRUKTION KAN GOERE DAGSORDENEN FORKERT.
            // Instruktionen bestemmer, hvad dokumentet skal indeholde -
            // dagsordenen bestemmer, hvad der bliver SAGT paa moedet. Aendrer
            // man den ene og glemmer den anden, beder skabelonen om noget,
            // ingen kom til at sige hoejt, og feltet staar tomt i dokumentet.
            //
            // Der spoerges kun, naar netop instruktionen er aendret. Retter
            // man et navn eller en temperatur, har dagsordenen intet med det
            // at goere.
            var instruktionAendret = _systemVedIndlaesning is not null
                                     && _systemVedIndlaesning != _valgt.SystemPrompt;

            Status.Text = $"Gemt: {sti}";
            GemKnap.IsEnabled = false;
            Indlæs(_valgt.Name);

            if (instruktionAendret && SkyNoegle.Hent() is not null)
                MindOmDagsorden();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke gemme", $"Mødetypen kunne ikke gemmes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
        }
    }

    /// <summary>Accepterer både komma og punktum — dansk tastatur skriver komma.</summary>
    private static bool TryTal(string s, out double værdi) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out værdi) ||
        double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out værdi);

    // ------------------------------------------------------------- ny/slet

    /// <summary>
    /// Ny skabelon — gennem guiden.
    ///
    /// Her blev der foer lavet en tom skabelon med det samme. Den vej findes
    /// stadig, som en knap i guiden; men den tomme er kun brugbar for en, der
    /// ved, hvad en systemprompt er, og hvilke afsnit der giver mening.
    /// </summary>
    private void Ny_Click(object sender, RoutedEventArgs e)
    {
        var vindue = new NyTemplateWindow { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true || vindue.Resultat is not { } ny) return;

        try
        {
            // Et navn, der allerede findes, ville overskrive en skabelon, man
            // stadig bruger. Der laegges et nummer paa i stedet.
            var grund = ny.Name;
            var n = 2;
            while (File.Exists(Path.Combine(PromptTemplate.Directory, PromptTemplate.Filnavn(ny.Name))))
                ny.Name = $"{grund} {n++}";

            ny.Save();

            // FOERST HER ligger navnet fast. Gemte guiden selv dagsordenen,
            // ville et navnesammenfald have skrevet hen over dagsordenen paa
            // den skabelon, der allerede hed det.
            if (vindue.Dagsorden is { Length: > 0 } dagsorden)
                Agendaer.Gem(ny.Name, dagsorden);

            Indlæs(ny.Name);
            Status.Text = $"«{ny.Name}» er lavet med sin egen dagsorden. Læs begge dele igennem, og ret det, der skal rettes.";

            if (vindue.Advarsel is { } advarsel)
            {
                Status.Text = $"«{ny.Name}» er lavet. Læs den igennem, og ret det, der skal rettes.";
                Dialogs.AppDialog.Vis(Window.GetWindow(this), "Dagsordenen mangler",
                    advarsel, Dialogs.Slags.Pas_paa);
            }
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke gemme mødetypen",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt?.Path is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke slettes", "Der er ingen fil at slette — mødetypen er indbygget.", Dialogs.Slags.Valg);
            return;
        }

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet mødetypen «{_valgt.Name}»?",
            $"{_valgt.Path}\n\nFilen slettes. Har du brugt den til udkast tidligere, ligger de " +
            "udkast stadig hvor de er.",
            godkend: "Slet mødetypen", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            File.Delete(_valgt.Path);
            Status.Text = "Mødetypen er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke slette", $"Mødetypen kunne ikke slettes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(PromptTemplate.Directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{PromptTemplate.Directory}\"")
        { UseShellExecute = true });
    }
}
