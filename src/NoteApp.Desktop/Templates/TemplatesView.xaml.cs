using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        // HER BLEV FELTKNAPPERNE FYLDT. De er blevet til hak, og de bygges
        // pr. skabelon - se Byg_Felter.

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

        // PROJEKTETS TO. De stod som «kilder» og «projekt» — noegler, ikke
        // ord. Se resten af listen her: skaermen taler dansk, filen taler
        // kroellede parenteser.
        "kilder" => "Projektets materiale",
        "projekt" => "Projektets navn",
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

    /// <summary>
    /// Bygger træet forfra — begge biblioteker.
    /// </summary>
    /// <remarks>
    /// ============ TO BIBLIOTEKER, IKKE TO SKÆRME ============
    ///
    /// En mødetype skriver et dokument ud af én optagelse. Et projektoutput
    /// skriver et dokument ud af et helt projekt. Formen er den samme —
    /// afsnit, der bestemmer indholdet — og derfor deler de skærm, editor og
    /// filformat. To skærme ville betyde to editorer at vedligeholde og to
    /// steder at lede efter den samme slags ting.
    ///
    /// Hvert bibliotek har sin egen mappe på disken; se
    /// <see cref="PromptTemplate.Skabelonmappe"/>. Det er DEN, «Ny», «Slet» og
    /// «Åbn mappen» retter sig efter, alt efter hvad der er valgt.
    /// </remarks>
    private void Indlæs(string? vælgNavn = null)
    {
        _skabeloner.Clear();
        _skabeloner.AddRange(PromptTemplate.LoadAll(Skabelonslags.Moedetype));

        _projektoutput.Clear();
        _projektoutput.AddRange(PromptTemplate.LoadAll(Skabelonslags.Projektoutput));

        _byggerTrae = true;

        var varUdfoldet = _rod?.ErUdfoldet ?? true;
        var varProjektUdfoldet = _projektrod?.ErUdfoldet ?? true;

        _rod = Biblioteker.Biblioteksnode.Bibliotek("Mødetyper", "", Transcribe.Gruppe.Moede);
        _rod.Antal = _skabeloner.Count;
        foreach (var t in _skabeloner)
            _rod.Boern.Add(Biblioteker.Biblioteksnode.Skabelonnode(t));

        _projektrod = Biblioteker.Biblioteksnode.Bibliotek("Projekt output", "", Transcribe.Gruppe.Moede);
        _projektrod.Antal = _projektoutput.Count;
        foreach (var t in _projektoutput)
            _projektrod.Boern.Add(Biblioteker.Biblioteksnode.Skabelonnode(t));

        Trae.ItemsSource = new[] { _rod, _projektrod };
        _rod.ErUdfoldet = varUdfoldet;
        _projektrod.ErUdfoldet = varProjektUdfoldet;

        _byggerTrae = false;

        var knuder = _rod.Boern.Concat(_projektrod.Boern).ToList();

        if (knuder.Count == 0)
        {
            Status.Text = $"Ingen skabeloner i {PromptTemplate.Directory}. "
                          + "Tryk «Ny» for at lave den første.";
            Detaljer.IsEnabled = false;
            SletKnap.IsEnabled = false;
            return;
        }

        Detaljer.IsEnabled = true;

        var valgt = knuder.FirstOrDefault(k => k.Skabelon?.Name == vælgNavn) ?? knuder[0];

        valgt.ErValgt = true;
        Vis(valgt.Skabelon);
    }

    private Biblioteker.Biblioteksnode? _rod;
    private Biblioteker.Biblioteksnode? _projektrod;

    /// <summary>Skabelonerne i det andet bibliotek.</summary>
    private readonly List<PromptTemplate> _projektoutput = new();

    /// <summary>
    /// Hvilket bibliotek er der peget på lige nu?
    /// </summary>
    /// <remarks>
    /// Bruges af «Ny», «Slet» og «Åbn mappen». Står markeringen på en
    /// skabelon, er svaret dens eget bibliotek; står den på selve
    /// biblioteksrækken, er det den, der peges på.
    ///
    /// UDEN DEN VILLE «NY» ALTID LAVE EN MØDETYPE. Man ville stå i «Projekt
    /// output», trykke Ny, og se den dukke op i den anden liste — uden en
    /// fejl og uden en forklaring.
    /// </remarks>
    private Skabelonslags _valgtSlags = Skabelonslags.Moedetype;

    private bool _byggerTrae;

    // Standarddagsordenen og selve Mistral-kaldet laa her. De er flyttet til
    // Dagsordensskriver, fordi guiden til nye skabeloner skal bruge praecis
    // det samme kald - stod prompten to steder, ville de to veje langsomt
    // give hver sit resultat.
    // HER LAA Agenda() - den faste standardtekst. Dagsordenen bygges nu af
    // afsnittene; se Dagsordensskriver.Byg.

    // HER LAA Felt_Klik OG Systemfelt_Klik - knapperne, der satte
    // «{{titel}}» og «{{deltagerregler}}» ind, hvor markoeren stod.
    //
    // Begge er blevet til hak. Knapperne gav to slags fejl, der var svaere at
    // se: man kunne saette det samme felt ind to gange, og man kunne saette det
    // ind midt i en saetning. Se Byg_Felter og HakDeltagere.

    /// <summary>
    /// Viser den dagsorden, der hører til den valgte skabelon — eller
    /// standarden, hvis den ikke har sin egen.
    /// </summary>
    private void VisAgenda()
    {
        var navn = _valgt?.Name;

        // ============ DEN BYGGES HVER GANG ============
        //
        // Foer kunne en moedetype have sin EGEN gemte dagsorden, skrevet af
        // Mistral. Den vandt over alt andet, og saa aendrede dagsordenen sig
        // ikke, uanset hvad man klikkede til og fra - man skulle foerst vide,
        // at der laa en gemt, og saa trykke en knap for at komme af med den.
        //
        // Det er unoedvendig kompleksitet: to slags dagsordener, en gemt
        // tilstand man ikke kan se, og en knap til at komme ud af den. Nu er
        // der EEN, og den bygges af afsnittene, hver gang fanen tegnes.
        var aktive = _valgt is null
            ? Array.Empty<string>()
            : PromptTemplate.AfsnitI(Tekst(FeltSystem))
                .Where(a => !_udeladte.Contains(a, StringComparer.CurrentCultureIgnoreCase))
                .ToArray();

        FeltAgenda.Text = Dagsordensskriver.Byg(aktive);

        AgendaOverskrift.Text = navn is null
            ? "Dagsorden til mødet"
            : $"Dagsorden til «{navn}»";

        if (_valgt is null)
        {
            Afsnitsliste.Visibility = Visibility.Collapsed;
            return;
        }

        Afsnitsliste.Visibility = Visibility.Visible;
        Afsnitsliste.Text = aktive.Length == 0
            ? "Mødetypen har ingen afsnit endnu — se «Sådan skal det skrives»."
            : "Bygget af afsnittene: " + string.Join(", ", aktive) + ". "
              + "Tager du et hak fra, forsvinder punktet herunder med det samme.";

        if (Pensel("TekstMeget") is { } farve) Afsnitsliste.Foreground = farve;
    }

    // HER LAA «Tilpas til denne mødetype», «Tilbage til standarden» og
    // spoergsmaalet «Skal dagsordenen foelge med?».
    //
    // Alle tre hoerte til den GEMTE dagsorden, og den findes ikke mere. En
    // dagsorden, der bygges hver gang, kan ikke komme i utakt med afsnittene -
    // saa er der heller ikke noget at minde om, at tilpasse eller at nulstille.

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

        // BIBLIOTEKET FØLGER MED MARKERINGEN. Se _valgtSlags.
        _valgtSlags = ReferenceEquals(knude, _projektrod)
                      || (knude.Skabelon is { } valgtSkabelon
                          && valgtSkabelon.Slags == Skabelonslags.Projektoutput)
            ? Skabelonslags.Projektoutput
            : Skabelonslags.Moedetype;

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

        TilpasTilSlagsen();

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
        SaetTekst(FeltSystem, t.SystemPrompt);

        _udeladte = new List<string>(t.UdeladteAfsnit);
        HakDeltagere.IsChecked = t.TagDeltagerregler;
        _udeladteFelter = new List<string>(t.UdeladteFelter);
        Byg_Afsnit();

        // TEKSTEN FOERST, SAA HAKKENE. Byg_Felter laeser FeltBruger for at se,
        // hvilke felter der staar i teksten - og den laeste den FORRIGE
        // skabelons prompt, saa hvert eneste hak stod tomt paa en skabelon,
        // der brugte alle sine felter. Set 06-09-2026 paa projektoutput,
        // hvor det ramte hver gang, fordi ingen moedetype har {{kilder}}.
        SaetTekst(FeltBruger, t.UserPrompt);
        Byg_Felter();

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

    }

    /// <summary>
    /// Skjuler det, der ikke giver mening for den slags skabelon, man står i.
    /// </summary>
    /// <remarks>
    /// ============ TRE TING HØRER KUN TIL ET MØDE ============
    ///
    /// MAPPEN. Den siger, hvilken mappe optagelser af den her type lander i.
    /// Et projektoutput laver ikke optagelser — det læser dokumenter, der
    /// ligger i projektet i forvejen.
    ///
    /// DELTAGERREGLERNE. De handler om, hvem der kommer på deltagerlisten, og
    /// hvordan man skriver om den, der sagde noget. Et projekt har ikke
    /// deltagere; det har filer.
    ///
    /// DAGSORDENEN. Den er det, der skal SIGES på mødet, for at udskriften
    /// kommer til at indeholde det, skabelonen beder om. Et projekt har ikke
    /// et møde at holde — materialet ligger der allerede.
    ///
    /// De bliver SKJULT og ikke grå. En grå fane er et spørgsmål om, hvad
    /// man har gjort forkert; en fane, der ikke er der, er et spørgsmål, der
    /// ikke bliver stillet.
    /// </remarks>
    private void TilpasTilSlagsen()
    {
        var projekt = _valgtSlags == Skabelonslags.Projektoutput;
        var vis = projekt ? Visibility.Collapsed : Visibility.Visible;

        Mappeoverskrift.Visibility = vis;
        Mapperaekke.Visibility = vis;
        Mappehjaelp.Visibility = vis;
        HakDeltagere.Visibility = vis;

        // EN SKJULT FANE, MAN STAAR PAA, ER ET TOMT PANEL. Markeringen skal
        // flyttes, foer den forsvinder.
        if (projekt && AgendaFane.IsSelected) MaterialeFane.IsSelected = true;
        AgendaFane.Visibility = vis;

        Materialelinje1.Text = projekt
            ? "Den anden fane er reglerne for, hvordan der skrives. Her vælger du, HVAD "
              + "modellen får at arbejde med fra projektet."
            : Sprog.T("templatesview.instruktion_er_reglerne_hvad_modellen_skal");

        Materialelinje2.Text = projekt
            ? "«Materialet» er projektets dokumenter, samlet og mærket med, hvilken fil "
              + "hver stump kom fra. Er hakket taget fra, sendes linjen ikke — og så har "
              + "modellen ingenting at skrive ud af."
            : Sprog.T("templatesview.staar_et_felt_ikke_her_naar_de_oplysninger");
    }

    // HER LAA _systemVedIndlaesning - instruktionen, som den saa ud, da
    // skabelonen blev aabnet. Den fandtes for at opdage, om dagsordenen var
    // kommet i utakt. Dagsordenen bygges nu af afsnittene hver gang, saa den
    // kan ikke komme i utakt.

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

    /// <summary>Overskrifterne, brugeren har taget hakket fra.</summary>
    private List<string> _udeladte = new();

    /// <summary>
    /// Én afkrydsning pr. afsnit i skabelonens tekst.
    /// </summary>
    /// <remarks>
    /// AFSNITTENE LÆSES AF TEKSTEN SELV — «## Beslutninger» og de andre
    /// overskrifter. Der er altså ikke en liste i appen, der skal holdes i
    /// trit med skabelonerne: laver man en ny mødetype med sine egne afsnit,
    /// står de her af sig selv.
    ///
    /// DET SIDSTE HAK KAN IKKE TAGES FRA. Et dokument uden et eneste afsnit er
    /// ikke et dokument, og en skærm, der lader en gøre det, er en fælde.
    /// </remarks>
    private void Byg_Afsnit()
    {
        _byggerAfsnit = true;
        Afsnit.Children.Clear();

        // AFSNITTENE LAESES AF DET, DER STAAR I FELTET - ikke af den gemte
        // skabelon. Skriver man et nyt «## Citater», skal hakket staa med det
        // samme og ikke foerst efter et gem.
        var fundne = PromptTemplate.AfsnitI(Tekst(FeltSystem));

        // Et fravalg af et afsnit, teksten ikke har mere, skal ikke blive
        // haengende i filen og forvirre naeste laeser.
        _udeladte.RemoveAll(u => !fundne.Contains(u, StringComparer.CurrentCultureIgnoreCase));

        foreach (var navn in fundne)
        {
            var hak = new CheckBox
            {
                Content = new TextBlock { Text = navn },
                Tag = navn,
                Margin = new Thickness(0, 0, 18, 6),
                FontSize = 12.5,
                IsChecked = !_udeladte.Contains(navn, StringComparer.CurrentCultureIgnoreCase)
            };

            hak.Checked += Afsnit_Aendret;
            hak.Unchecked += Afsnit_Aendret;
            Afsnit.Children.Add(hak);
        }

        Afsnitsrude.Visibility = fundne.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _byggerAfsnit = false;

        Vis_Fravalg();
    }

    /// <summary>
    /// Viser hvad hakkene betyder — på hakket selv og i en linje under dem.
    /// </summary>
    /// <remarks>
    /// TEKSTEN BLIVER STÅENDE MED VILJE, og derfor skete der ingenting synligt,
    /// når man tog et hak fra: skærmen så præcis ud som før, og hakket lignede
    /// noget, der ikke virkede. Nu er den fravalgte overskrift streget over, og
    /// linjen under siger, hvad der sker, når dokumentet laves.
    /// </remarks>
    /// <summary>
    /// Deltagerreglerne slås til eller fra.
    /// </summary>
    /// <remarks>
    /// SOM AFSNITTENE: teksten røres ikke. Står <c>{{deltagerregler}}</c> i
    /// prompten, bliver det stående — det er hakket, der afgør, om reglerne
    /// sendes. Se <c>PromptTemplate.RenderSystem</c>.
    /// </remarks>
    private void Deltagere_Aendret(object sender, RoutedEventArgs e)
    {
        if (_indlæser) return;

        GemKnap.IsEnabled = true;
        Vis_Fravalg();
    }

    private System.Windows.Media.Brush? Pensel(string noegle) =>
        TryFindResource(noegle) as System.Windows.Media.Brush
        ?? Application.Current?.TryFindResource(noegle) as System.Windows.Media.Brush;

    private void Vis_Fravalg()
    {
        foreach (var k in Afsnit.Children.OfType<CheckBox>())
        {
            if (k.Content is not TextBlock t) continue;

            var fra = k.IsChecked != true;

            t.TextDecorations = fra ? TextDecorations.Strikethrough : null;

            // Pensel og ikke FindResource: opslaget kaster, naar ruden endnu
            // ikke er i et vindue - se den samme faelde i Dokumentrude.
            if (Pensel(fra ? "Slukket" : "Tekst") is { } pensel) t.Foreground = pensel;
        }

        // Deltagerreglerne er ikke et afsnit i teksten, men de er det samme
        // slags valg - saa de skal se ud og opfoere sig ens.
        if (HakDeltagere.Content is TextBlock d)
        {
            var fraD = HakDeltagere.IsChecked != true;

            d.TextDecorations = fraD ? TextDecorations.Strikethrough : null;
            if (Pensel(fraD ? "Slukket" : "Tekst") is { } p) d.Foreground = p;
        }

        var ude = Afsnit.Children.OfType<CheckBox>()
            .Where(k => k.IsChecked != true)
            .Select(k => (string)k.Tag!)
            .ToList();

        if (HakDeltagere.IsChecked != true) ude.Insert(0, "Deltagerregler");

        if (ude.Count == 0)
        {
            Streg_Afsnit();
            Afsnitsbesked.Visibility = Visibility.Collapsed;
            return;
        }

        // ============ OG DE STREGES OVER INDE I TEKSTEN ============
        //
        // Hakket sagde det, og linjen her siger det - men i boksen saa alt ens
        // ud, saa man skulle selv holde styr paa, hvilke linjer der ikke blev
        // sendt. Nu kan man se det dér, hvor teksten staar.
        Streg_Afsnit();

        Afsnitsbesked.Visibility = Visibility.Visible;
        Afsnitsbesked.Text = ude.Count == 1
            ? $"«{ude[0]}» springes over, når dokumentet laves. Teksten nedenfor bliver stående, "
              + "så du kan sætte hakket tilbage."
            : $"Disse springes over, når dokumentet laves: {string.Join(", ", ude)}. "
              + "Teksten nedenfor bliver stående, så du kan sætte hakkene tilbage.";
    }

    /// <summary>
    /// Markerer afsnittet i teksten, så man kan se hvad hakket handler om.
    /// </summary>
    /// <remarks>
    /// Afsnittet er overskriften og alt frem til den næste «## »-linje — samme
    /// afgrænsning som <c>PromptTemplate</c> bruger, når dokumentet laves. Er
    /// de to uenige, er det dét, der skal rettes; markeringen er skærmens svar
    /// på «hvad er det så, der ryger?».
    /// </remarks>
    private void Marker_Afsnit(string navn)
    {
        var linjer = Tekst(FeltSystem).Split('\n');

        var start = -1;
        var slut = linjer.Length - 1;

        for (var i = 0; i < linjer.Length; i++)
        {
            var erOverskrift = linjer[i].StartsWith("##") && !linjer[i].StartsWith("###");
            var dette = erOverskrift && linjer[i].TrimStart('#').Trim()
                .Equals(navn, StringComparison.CurrentCultureIgnoreCase);

            if (dette) start = i;
            else if (start >= 0 && erOverskrift) { slut = i - 1; break; }
        }

        if (start < 0) return;

        Marker(FeltSystem, start, slut);
    }

    private bool _byggerAfsnit;
    private bool _byggerFelter;

    /// <summary>Felterne, brugeren har taget hakket fra.</summary>
    private List<string> _udeladteFelter = new();

    /// <summary>
    /// Én afkrydsning pr. felt, der kan komme med fra mødet.
    /// </summary>
    /// <remarks>
    /// ALLE FELTER STÅR PÅ LISTEN, også dem skabelonen ikke bruger. Ellers
    /// kunne man ikke få dem med: det var netop dét, de gamle knapper var til,
    /// og en liste, hvor kun det valgte står, kan man ikke vælge fra.
    ///
    /// Hakket er sat, når feltet står i teksten OG ikke er fravalgt.
    /// </remarks>
    private void Byg_Felter()
    {
        _byggerFelter = true;
        Feltvalg.Children.Clear();

        var tekst = Tekst(FeltBruger);

        // DELTAGERREGLER OG SPROGREGLER STAAR IKKE HER.
        //
        // Begge er REGLER til instruktionen, ikke oplysninger fra moedet - og
        // fanen her handler om, hvad modellen faar at arbejde med. Sproget
        // vaelges desuden, naar dokumentet oprettes, og laegges til
        // instruktionen af sig selv; se PromptTemplate.RenderSystem.
        // FELTERNE FØLGER SLAGSEN. En mødetype har en transskription; et
        // projektoutput har et fundament. Se PromptTemplate.Felter.
        foreach (var f in PromptTemplate.Felter(_valgtSlags)
                     .Where(k => k != Deltagerregler.Felt && k != Sprogregler.Felt))
        {
            var staar = tekst.Contains("{{" + f + "}}", StringComparison.CurrentCultureIgnoreCase);
            var fravalgt = _udeladteFelter.Contains(f, StringComparer.CurrentCultureIgnoreCase);

            var hak = new CheckBox
            {
                Content = new TextBlock { Text = Feltnavn(f) },
                Tag = f,
                Margin = new Thickness(0, 0, 18, 6),
                FontSize = 12.5,
                IsChecked = staar && !fravalgt,
                ToolTip = PromptTemplate.Fields[f] + "\n\nStår i teksten som {{" + f + "}}"
            };

            hak.Checked += Felt_Aendret;
            hak.Unchecked += Felt_Aendret;
            Feltvalg.Children.Add(hak);
        }

        _byggerFelter = false;
        Vis_Feltfravalg();
    }

    /// <summary>
    /// Slår et felt til eller fra.
    /// </summary>
    /// <remarks>
    /// FRA rører ikke teksten — feltet står, og linjen springes over, når
    /// dokumentet laves. TIL på et felt, teksten ikke har, skriver en ny linje
    /// til sidst; ellers ville hakket ikke kunne sættes på noget, skabelonen
    /// aldrig har brugt.
    /// </remarks>
    private void Felt_Aendret(object sender, RoutedEventArgs e)
    {
        if (_byggerFelter || _indlæser) return;
        if (sender is not CheckBox { Tag: string felt } hak) return;

        var mærke = "{{" + felt + "}}";
        var staar = Tekst(FeltBruger).Contains(mærke, StringComparison.CurrentCultureIgnoreCase);

        if (hak.IsChecked == true)
        {
            _udeladteFelter.RemoveAll(u => u.Equals(felt, StringComparison.CurrentCultureIgnoreCase));

            if (!staar)
            {
                // Transskriptionen staar alene; de oevrige faar deres egen
                // overskrift, saa modellen ved hvad tallet eller navnet er.
                var linje = felt == "transskription" ? mærke : $"{Feltnavn(felt)}: {mærke}";

                SaetTekst(FeltBruger, Tekst(FeltBruger).TrimEnd() + "\n\n" + linje);

                var sidste = FeltBruger.Document.Blocks.OfType<Paragraph>().Count() - 1;
                Marker(FeltBruger, sidste, sidste);
            }
        }
        else if (!_udeladteFelter.Contains(felt, StringComparer.CurrentCultureIgnoreCase))
        {
            _udeladteFelter.Add(felt);
        }

        GemKnap.IsEnabled = true;
        Vis_Feltfravalg();
        TjekFelter();
    }

    /// <summary>Streger de fravalgte felter over og siger hvad det betyder.</summary>
    private void Vis_Feltfravalg()
    {
        foreach (var k in Feltvalg.Children.OfType<CheckBox>())
        {
            if (k.Content is not TextBlock t) continue;

            var fra = k.IsChecked != true;

            t.TextDecorations = fra ? TextDecorations.Strikethrough : null;
            if (Pensel(fra ? "Slukket" : "Tekst") is { } p) t.Foreground = p;
        }

        var ude = Feltvalg.Children.OfType<CheckBox>()
            .Where(k => k.IsChecked != true)
            .Select(k => Feltnavn((string)k.Tag!))
            .ToList();

        // De fravalgte LINJER streges over i teksten, saa man kan se det dér,
        // hvor teksten staar - se Streg_Linjer.
        var maerker = Feltvalg.Children.OfType<CheckBox>()
            .Where(k => k.IsChecked != true)
            .Select(k => "{{" + (string)k.Tag! + "}}")
            .ToList();

        Streg_Linjer(FeltBruger, (_, linje) =>
            maerker.Any(m => linje.Contains(m, StringComparison.CurrentCultureIgnoreCase)));

        if (ude.Count == 0)
        {
            Feltbesked2.Visibility = Visibility.Collapsed;
            return;
        }

        Feltbesked2.Visibility = Visibility.Visible;
        Feltbesked2.Text = $"Kommer ikke med: {string.Join(", ", ude)}.";
    }


    private void Afsnit_Aendret(object sender, RoutedEventArgs e)
    {
        if (_byggerAfsnit || _indlæser) return;
        if (sender is not CheckBox { Tag: string navn } hak) return;

        if (hak.IsChecked == true)
        {
            _udeladte.RemoveAll(u => u.Equals(navn, StringComparison.CurrentCultureIgnoreCase));
        }
        else
        {
            // DET SIDSTE HAK BLIVER SIDDENDE. Se Byg_Afsnit.
            var tilbage = Afsnit.Children.OfType<CheckBox>().Count(k => k.IsChecked == true);

            if (tilbage == 0)
            {
                Dialogs.AppDialog.Vis(Window.GetWindow(this), "Der skal være mindst ét afsnit",
                    "Et dokument uden et eneste afsnit ville være tomt. Sæt et andet hak først, "
                    + "hvis du vil tage det her fra.", Dialogs.Slags.Valg);

                _byggerAfsnit = true;
                hak.IsChecked = true;
                _byggerAfsnit = false;
                return;
            }

            if (!_udeladte.Contains(navn, StringComparer.CurrentCultureIgnoreCase))
                _udeladte.Add(navn);
        }

        GemKnap.IsEnabled = true;

        Vis_Fravalg();
        Marker_Afsnit(navn);

        // Dagsordensfanen siger, hvilke afsnit moedet skal komme omkring.
        // Tages et hak fra, er den listen forkert, indtil den bliver tegnet om.
        VisAgenda();
    }

    // ==================== TEKSTRUDERNE ====================
    //
    // De to store felter er RichTextBox og ikke TextBox, fordi de fravalgte
    // linjer skal kunne streges over inde i teksten. Alt herunder er det, der
    // skal til for at bruge en RichTextBox som et almindeligt tekstfelt: eet
    // afsnit pr. linje, og teksten laest og skrevet som ren tekst.

    /// <summary>Indholdet som ren tekst — ét afsnit er én linje.</summary>
    private static string Tekst(RichTextBox r) =>
        string.Join("\n", r.Document.Blocks.OfType<Paragraph>()
            .Select(a => new TextRange(a.ContentStart, a.ContentEnd).Text.TrimEnd('\r')));

    /// <summary>Fylder ruden med ren tekst.</summary>
    private void SaetTekst(RichTextBox r, string tekst)
    {
        // SKRIFTEN SAETTES PAA DOKUMENTET. Et FlowDocument arver IKKE
        // stoerrelse, skrift og vaegt fra den rude, det ligger i - det har sine
        // egne standarder, og de er stoerre og federe end resten af appen.
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = r.FontFamily,
            FontSize = r.FontSize,
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };

        foreach (var linje in tekst.Replace("\r\n", "\n").Split('\n'))
            doc.Blocks.Add(new Paragraph(new Run(linje)) { Margin = new Thickness(0) });

        r.Document = doc;
    }

    /// <summary>Sat mens ruden bliver tegnet om — ellers svarer TextChanged på os selv.</summary>
    private bool _tegner;

    /// <summary>
    /// Streger de linjer over, der ikke bliver sendt.
    /// </summary>
    /// <remarks>
    /// EN AENDRING AF FORMATERINGEN UDLØSER TextChanged i en RichTextBox. Uden
    /// <see cref="_tegner"/> ville hver overstregning kalde os selv igen.
    /// </remarks>
    private void Streg_Linjer(RichTextBox r, Func<int, string, bool> udeladt)
    {
        if (_tegner) return;
        _tegner = true;

        var nr = 0;

        foreach (var a in r.Document.Blocks.OfType<Paragraph>())
        {
            var linje = new TextRange(a.ContentStart, a.ContentEnd).Text.TrimEnd('\r');
            var fra = udeladt(nr++, linje);

            a.TextDecorations = fra ? TextDecorations.Strikethrough : null;
            if (Pensel(fra ? "Slukket" : "Tekst") is { } pensel) a.Foreground = pensel;
        }

        _tegner = false;
    }

    /// <summary>
    /// Streger de linjer over i instruktionen, der hører til et fravalgt afsnit.
    /// </summary>
    /// <remarks>
    /// Et afsnit er overskriften og alt frem til den næste «## »-linje — samme
    /// afgrænsning som <c>PromptTemplate</c> bruger, når dokumentet laves.
    /// </remarks>
    private void Streg_Afsnit()
    {
        var ude = new HashSet<string>(
            Afsnit.Children.OfType<CheckBox>()
                .Where(k => k.IsChecked != true)
                .Select(k => (string)k.Tag!),
            StringComparer.CurrentCultureIgnoreCase);

        var linjer = Tekst(FeltSystem).Split('\n');
        var fravalgt = new HashSet<int>();
        var i_afsnit = false;

        for (var i = 0; i < linjer.Length; i++)
        {
            var erOverskrift = linjer[i].StartsWith("##") && !linjer[i].StartsWith("###");

            if (erOverskrift) i_afsnit = ude.Contains(linjer[i].TrimStart('#').Trim());
            if (i_afsnit) fravalgt.Add(i);
        }

        Streg_Linjer(FeltSystem, (nr, _) => fravalgt.Contains(nr));
    }

    /// <summary>Markerer og ruller frem til de afsnit, der hører til ét udsnit.</summary>
    private static void Marker(RichTextBox r, int fraLinje, int tilLinje)
    {
        var afsnit = r.Document.Blocks.OfType<Paragraph>().ToList();
        if (fraLinje < 0 || fraLinje >= afsnit.Count) return;

        tilLinje = Math.Min(tilLinje, afsnit.Count - 1);

        r.Selection.Select(afsnit[fraLinje].ContentStart, afsnit[tilLinje].ContentEnd);
        r.Focus();
        afsnit[fraLinje].BringIntoView();
    }

    // -------------------------------------------------------------- ændring

    private void Aendret(object sender, TextChangedEventArgs e)
    {
        if (!_indlæser) GemKnap.IsEnabled = true;
        TjekFelter();

        // Skriver man i instruktionen, kan der vaere kommet et afsnit til eller
        // vaere forsvundet et. Raekken bygges kun om, naar den faktisk er blevet
        // forkert - ellers ville hvert tastetryk bygge den forfra.
        if (!_indlæser && ReferenceEquals(sender, FeltSystem)) Foelg_Afsnit();

        // Det samme paa materialefanen: skriver eller sletter man et felt i
        // haanden, skal hakket foelge med.
        if (!_indlæser && ReferenceEquals(sender, FeltBruger)) Foelg_Felter();
    }

    private void Foelg_Felter()
    {
        var tekst = Tekst(FeltBruger);

        foreach (var k in Feltvalg.Children.OfType<CheckBox>())
        {
            var felt = (string)k.Tag!;
            var staar = tekst.Contains("{{" + felt + "}}", StringComparison.CurrentCultureIgnoreCase);
            var skal = staar && !_udeladteFelter.Contains(felt, StringComparer.CurrentCultureIgnoreCase);

            if ((k.IsChecked == true) == skal) continue;

            _byggerFelter = true;
            k.IsChecked = skal;
            _byggerFelter = false;
        }

        Vis_Feltfravalg();
    }

    private void Foelg_Afsnit()
    {
        var nu = PromptTemplate.AfsnitI(Tekst(FeltSystem));
        var vist = Afsnit.Children.OfType<CheckBox>().Select(k => (string)k.Tag).ToList();

        if (nu.SequenceEqual(vist, StringComparer.CurrentCultureIgnoreCase)) return;

        Byg_Afsnit();
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
        var tekst = Tekst(FeltBruger);

        var brugte = System.Text.RegularExpressions.Regex
            .Matches(tekst, @"\{\{\s*([a-zA-Z_æøåÆØÅ0-9]+)\s*\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ============ HVAD DER IKKE MAA MANGLE, AFHAENGER AF SLAGSEN ============
        //
        // En moedetype uden {{transskription}} skriver et referat af
        // ingenting. Et projektoutput uden {{kilder}} goer praecis det samme -
        // og det er en ANDEN fejl, som den samme kontrol skal kunne se.
        //
        // Fejlen var her indtil 06-09-2026: skaermen forlangte
        // {{transskription}} af hver eneste projektskabelon og naegtede at
        // gemme, selv om feltet ville staa tomt, hvis man satte det ind.
        var kraevet = _valgtSlags == Skabelonslags.Projektoutput ? "kilder" : "transskription";
        var manglerUdskrift = !brugte.Contains(kraevet, StringComparer.OrdinalIgnoreCase);
        var ukendte = brugte.Where(f => !PromptTemplate.Fields.ContainsKey(f)).ToList();
        var reglerForkert = brugte.Contains(Deltagerregler.Felt, StringComparer.OrdinalIgnoreCase);

        var linjer = new List<string>();

        if (manglerUdskrift)
            linjer.Add(_valgtSlags == Skabelonslags.Projektoutput
                ? "{{kilder}} mangler. Uden det felt får modellen ikke projektets materiale, "
                  + "og dokumentet bliver skrevet på ingenting. Der kan ikke gemmes, før "
                  + "feltet er sat ind."
                : "{{transskription}} mangler. Uden det felt får modellen ikke selve "
                  + "transkriptionen, og dokumentet bliver skrevet på ingenting. Der kan ikke "
                  + "gemmes, før feltet er sat ind.");

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
            // HER STOD «Sendes med: transskription, titel, dato ...».
            //
            // Den samme liste staar nu som hak ovenfor, paa dansk og til at
            // klikke paa. To lister over det samme er een for meget - og den
            // her var den af dem, man ikke kunne gore noget ved.
            Feltkontrol.Visibility = Visibility.Collapsed;
            GemKnap.IsEnabled = GemKnap.IsEnabled && !manglerUdskrift;
            return;
        }

        Feltbesked.Text = string.Join("\n\n", linjer);
        // FLADEN KOM FRA PALETTEN. Her stod #33E5484D - en roed tone med
        // 20 % daekning. Den laa oven paa, hvad der nu var under den, og saa
        // «rigtig nok ud» i begge temaer, men den var ikke en af appens egne
        // farver, og ingen proeve kendte den. Paletten har allerede de fire
        // tonede kasser.
        Saet(Feltkontrol, Feltbesked,
             manglerUdskrift ? "FejlFlade" : "AdvarselFlade",
             manglerUdskrift ? "FejlTekst" : "Advarsel",
             "Tekst");

        Feltkontrol.Visibility = Visibility.Visible;
        GemKnap.IsEnabled = GemKnap.IsEnabled && !manglerUdskrift;
    }

    private void Saet(Border ramme, TextBlock tekst, string baggrund, string kant, string skrift)
    {
        // TryFindResource og ikke FindResource: det sidste KASTER, naar ruden
        // endnu ikke er i et vindue, eller hvis en noegle mangler - og en
        // farve, der ikke kan slaas op, maa ikke vaelte en kontrol af felterne.
        if (Pensel(baggrund) is { } b) ramme.Background = b;
        if (Pensel(kant) is { } k) ramme.BorderBrush = k;
        if (Pensel(skrift) is { } f) tekst.Foreground = f;
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

        if (string.IsNullOrWhiteSpace(Tekst(FeltSystem)))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Mangler instruktion", "Instruktionen til modellen må ikke være tom — det er den, der afgør, hvad der kommer ud.", Dialogs.Slags.Pas_paa);
            return;
        }

        var projektskabelon = _valgtSlags == Skabelonslags.Projektoutput;
        var feltet = projektskabelon ? "{{kilder}}" : "{{transskription}}";

        if (!Tekst(FeltBruger).Contains(feltet))
        {
            var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
                projektskabelon ? "Materialet mangler i skabelonen" : "Mødet mangler i mødetypen",
                $"Feltet {feltet} står ikke i teksten forneden. Uden det får modellen ikke "
                + (projektskabelon ? "projektets materiale" : "selve mødet")
                + " at se, og udkastet bliver skrevet ud af ingenting.",
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
        _valgt.SystemPrompt = Tekst(FeltSystem).Trim();
        _valgt.UdeladteAfsnit = new List<string>(_udeladte);
        _valgt.TagDeltagerregler = HakDeltagere.IsChecked == true;
        _valgt.UdeladteFelter = new List<string>(_udeladteFelter);
        _valgt.UserPrompt = Tekst(FeltBruger).Trim();

        // SIDSTE SPAERRE. Knappen er graa, naar udskriften mangler, men et
        // gem kan ogsaa komme herind ad andre veje. En skabelon uden
        // {{transskription}} maa ikke naa filen - den ville producere et
        // dokument skrevet paa ingenting, og fejlen viser sig foerst dér.
        if (!_valgt.UserPrompt.Contains(feltet, StringComparison.OrdinalIgnoreCase))
        {
            MaterialeFane.IsSelected = true;
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                projektskabelon ? "Materialet mangler" : "Transkriptionen mangler",
                (projektskabelon ? "Skabelonen" : "Mødetypen")
                + $" kan ikke gemmes uden feltet {feltet} under «Hvad der kommer med». Uden "
                + "det får modellen ikke "
                + (projektskabelon ? "projektets materiale" : "selve transkriptionen af mødet")
                + ", og dokumentet bliver skrevet på ingenting.",
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
            // Der spoerges ikke om noget mere: dagsordenen bygges af
            // afsnittene, hver gang fanen tegnes, saa den KAN ikke komme i
            // utakt med instruktionen.
            Status.Text = $"Gemt: {sti}";
            GemKnap.IsEnabled = false;
            Indlæs(_valgt.Name);
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
        // GUIDEN SPOERGER OM DET BIBLIOTEK, MAN STAAR I. Uden slagsen her
        // stillede den altid spoergsmaal om en moedetype - «Ny mødetype»,
        // «Lav mødetypen» - ogsaa naar man stod i «Projekt output», og den
        // skabelon, der kom ud, bad om en transskription, der ikke findes i
        // et projekt.
        var vindue = new NyTemplateWindow(_valgtSlags) { Owner = Window.GetWindow(this) };
        if (vindue.ShowDialog() != true || vindue.Resultat is not { } ny) return;

        try
        {
            // Et navn, der allerede findes, ville overskrive en skabelon, man
            // stadig bruger. Der laegges et nummer paa i stedet.
            ny.Slags = _valgtSlags;

            var mappe = PromptTemplate.Skabelonmappe(_valgtSlags);

            var grund = ny.Name;
            var n = 2;
            while (File.Exists(Path.Combine(mappe, PromptTemplate.Filnavn(ny.Name))))
                ny.Name = $"{grund} {n++}";

            ny.Save();

            Indlæs(ny.Name);
            Status.Text = $"«{ny.Name}» er lavet. Læs den igennem, og ret det, der skal rettes.";

            if (vindue.Advarsel is { } advarsel)
            {
                Status.Text = $"«{ny.Name}» er lavet. Læs den igennem, og ret det, der skal rettes.";
                Dialogs.AppDialog.Vis(Window.GetWindow(this), "Dagsordenen mangler",
                    advarsel, Dialogs.Slags.Pas_paa);
            }
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), $"Kunne ikke gemme {Ordet}",
                ex.Message, Dialogs.Slags.Pas_paa);
        }
    }

    /// <summary>«mødetypen» eller «skabelonen» — efter det bibliotek, man står i.</summary>
    private string Ordet => _valgtSlags == Skabelonslags.Projektoutput ? "skabelonen" : "mødetypen";

    /// <summary>Det samme ord med stort begyndelsesbogstav.</summary>
    private string Ordet1 => char.ToUpper(Ordet[0]) + Ordet[1..];

    private void Slet_Click(object sender, RoutedEventArgs e)
    {
        if (_valgt?.Path is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kan ikke slettes", $"Der er ingen fil at slette — {Ordet} er indbygget.", Dialogs.Slags.Valg);
            return;
        }

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            $"Slet {Ordet} «{_valgt.Name}»?",
            $"{_valgt.Path}\n\nFilen slettes. Har du brugt den til udkast tidligere, ligger de " +
            "udkast stadig hvor de er.",
            godkend: $"Slet {Ordet}", annuller: "Behold den",
            slags: Dialogs.Slags.Pas_paa, godkendErStandard: false);

        if (!ja) return;

        try
        {
            File.Delete(_valgt.Path);
            Status.Text = $"{Ordet1} er slettet.";
            Indlæs();
        }
        catch (Exception ex)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this), "Kunne ikke slette", $"{Ordet1} kunne ikke slettes.\n\n{ex.Message}", Dialogs.Slags.Fejl);
        }
    }

    private void Mappe_Click(object sender, RoutedEventArgs e)
    {
        // DEN MAPPE, DER HØRER TIL DET VALGTE BIBLIOTEK. Åbnede den altid
        // mødetypernes mappe, ville man stå i «Projekt output», trykke, og
        // finde noget andet.
        var mappe = PromptTemplate.Skabelonmappe(_valgtSlags);

        Directory.CreateDirectory(mappe);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{mappe}\"")
        { UseShellExecute = true });
    }
}
