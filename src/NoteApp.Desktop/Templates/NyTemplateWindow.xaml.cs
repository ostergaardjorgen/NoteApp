using System.Windows;
using System.Windows.Controls;
using NoteApp.Core;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Templates;

/// <summary>
/// Guiden, der laver en ny skabelon.
///
/// HVORFOR EN MODEL SKRIVER DEN
///
/// En tom skabelon kræver, at man ved, hvad en systemprompt er, hvilke afsnit
/// der giver mening, og hvordan man formulerer en regel, en model faktisk
/// følger. Det er en anden slags arbejde end at vide, hvad man skal bruge
/// dokumentet til — og det sidste er dét, brugeren kan svare på.
///
/// Guiden spørger om fem ting og lader Mistral skrive resten. Vejen «start
/// fra en tom» bliver stående: den virker uden en API-nøgle og uden at sende
/// noget nogen steder hen.
///
/// RESULTATET ER ET UDKAST
///
/// Skabelonen åbnes til redigering bagefter. En skabelon, man ikke selv har
/// læst igennem, er en, man ikke opdager fejl i — og fejlen dukker først op
/// i et dokument, man skulle bruge til noget.
/// </summary>
public partial class NyTemplateWindow : Window
{
    /// <summary>Den færdige skabelon. Null hvis der blev fortrudt.</summary>
    public PromptTemplate? Resultat { get; private set; }

    /// <summary>Hvilket bibliotek skabelonen skal ligge i.</summary>
    private readonly Skabelonslags _slags;

    private bool Projekt => _slags == Skabelonslags.Projektoutput;

    /// <summary>
    /// De afsnit, en projektskabelon kan indeholde — og hvilke der er sat
    /// til at begynde med.
    /// </summary>
    /// <remarks>
    /// ============ HVORFOR EN LISTE OG IKKE ET TOMT FELT ============
    ///
    /// «Hvad skal med?» er svært at svare på fra en tom side. Man husker
    /// formålet og glemmer forudsætningerne — og det opdages først i det
    /// dokument, man skulle sende. En liste at sætte hak i er det samme
    /// spørgsmål stillet sådan, at man kan svare ved at læse.
    ///
    /// DE OTTE FØRSTE ER SAT. De er dem, de fire indbyggede skabeloner er
    /// enige om; resten er dem, der giver mening for nogle dokumenter og ikke
    /// for andre. Et hak kan både sættes og tages fra bagefter — på fanen
    /// «Hvad skal dokumentet indeholde?», hvor de samme afsnit står.
    ///
    /// RÆKKEFØLGEN ER DOKUMENTETS. Listen læses oppefra og ned, og det er
    /// den rækkefølge, afsnittene bliver bedt om i.
    /// </remarks>
    private static readonly (string Navn, bool Sat)[] Projektafsnit =
    {
        ("Kort fortalt", true),
        ("Baggrund", true),
        ("Formål og mål", true),
        ("Omfang og leverancer", true),
        ("Afgrænsning", false),
        ("Tidsplan og milepæle", true),
        ("Roller og ansvar", false),
        ("Økonomi", true),
        ("Pris", false),
        ("Forudsætninger", false),
        ("Risici", true),
        ("Beslutninger", false),
        ("Modstrid i materialet", false),
        ("Åbne spørgsmål", true),
        ("Næste skridt", false),
        ("Det, der mangler", false),
    };

    private static readonly (string Navn, int Tokens)[] Længder =
    {
        ("Kort — et notat på en halv side", 1024),
        ("Almindelig — et referat på et par sider", 4096),
        ("Udførlig — alle detaljer med, ingen øvre grænse i praksis", 32000)
    };

    public NyTemplateWindow() : this(Skabelonslags.Moedetype) { }

    public NyTemplateWindow(Skabelonslags slags)
    {
        InitializeComponent();

        _slags = slags;
        if (Projekt) Projektudgave();

        FeltLaengde.ItemsSource = Længder.Select(l => l.Navn).ToList();
        FeltLaengde.SelectedIndex = 1;

        // Uden noegle er der ingen model at spoerge. Knappen bliver graa frem
        // for at fejle bagefter, og der peges paa opsaetningen.
        Klar(SkyNoegle.Hent() is not null);

        FeltNavn.Focus();
    }

    /// <summary>
    /// Gør guiden til en projektskabelon i stedet for en mødetype.
    /// </summary>
    /// <remarks>
    /// SPØRGSMÅLENE ER DE SAMME, FORDI DE ER DE RIGTIGE. Hvad skal det hedde,
    /// hvad skal der komme ud af det, hvem læser det, hvor langt må det være
    /// — det er det samme, uanset om dokumentet bygges på én udskrift eller
    /// på tredive filer.
    ///
    /// DET, DER ER FORSKELLIGT, ER HVAD DER BYGGES AF. En mødetype får en
    /// udskrift og en deltagerliste; en projektskabelon får projektets
    /// materiale og skal henvise til, hvilken fil den har det fra. Det er
    /// derfor hjælpeteksterne, instruktionen til modellen og brugerprompten
    /// skiftes ud — og ikke bare overskriften.
    /// </remarks>
    private void Projektudgave()
    {
        Title = Sprog.T("nytemplatewindow.ny_projektskabelon");
        Overskrift.Text = Title;
        Indledning.Text = Sprog.T("nytemplatewindow.projekt_indledning");
        Navnehjaelp.Text = Sprog.T("nytemplatewindow.projekt_navnehjaelp");
        Formaalshjaelp.Text = Sprog.T("nytemplatewindow.projekt_formaalshjaelp");
        SkalMedOverskrift.Text = Sprog.T("nytemplatewindow.projekt_andre_afsnit");
        SkalMedHjaelp.Text = Sprog.T("nytemplatewindow.projekt_andre_afsnit_hjaelp");
        Selvfoelgeligt.Text = Sprog.T("nytemplatewindow.projekt_selvfoelgeligt");
        LavKnap.Content = Sprog.T("nytemplatewindow.projekt_lav_skabelonen");

        Emnerude.Visibility = Visibility.Visible;

        foreach (var (navn, sat) in Projektafsnit)
        {
            Emner.Children.Add(new CheckBox
            {
                Content = new TextBlock { Text = navn },
                Tag = navn,
                IsChecked = sat,
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 18, 6),
            });
        }
    }

    /// <summary>De afsnit, der er hak ved — i listens rækkefølge.</summary>
    private List<string> Afkrydsede() => Emner.Children.OfType<CheckBox>()
        .Where(k => k.IsChecked == true)
        .Select(k => (string)k.Tag)
        .ToList();

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>
    /// Åbner opsætningen af Mistral og prøver igen bagefter.
    ///
    /// HER LÅ «start fra en tom». Den er fjernet: en tom skabelon er kun
    /// brugbar for en, der i forvejen ved, hvad en systemprompt er — og en
    /// dårlig skabelon opdages først i et dokument, man skulle bruge til
    /// noget. Uden en nøgle er der derfor ingen vej frem her, og så skal der
    /// peges på den, der findes.
    /// </summary>
    private void SaetOp_Click(object sender, RoutedEventArgs e)
    {
        new Documents.SkySetupWindow { Owner = this }.ShowDialog();
        Klar(SkyNoegle.Hent() is not null);
    }

    private void Klar(bool harNoegle)
    {
        LavKnap.IsEnabled = harNoegle;
        SaetOpKnap.Visibility = harNoegle ? Visibility.Collapsed : Visibility.Visible;

        Status.Text = harNoegle ? "" : Sprog.T(Projekt
            ? "nytemplatewindow.projekt_skrives_af_mistral"
            : "nytemplatewindow.moedetypen_skrives_af_mistral");
    }

    /// <summary>
    /// Dagsordenen til den nye skabelon. Null, hvis den ikke kunne skrives —
    /// så gælder standarden. Gemmes af den, der kalder, når navnet ligger fast.
    /// </summary>
    public string? Dagsorden { get; private set; }

    /// <summary>
    /// Sat, hvis skabelonen blev lavet, men dagsordenen ikke kunne skrives.
    /// Null når alt gik godt.
    /// </summary>
    public string? Advarsel { get; private set; }

    private async void Lav_Click(object sender, RoutedEventArgs e)
    {
        var hvad = Projekt ? "Skabelonen" : "Mødetypen";

        var navn = FeltNavn.Text.Trim();
        if (navn.Length == 0)
        {
            Vis($"{hvad} skal have et navn.");
            FeltNavn.Focus();
            return;
        }

        var formaal = FeltFormaal.Text.Trim();
        if (formaal.Length == 0)
        {
            Vis("Skriv, hvad der skal komme ud af det. Det er dét, modellen bygger "
                + (Projekt ? "skabelonen" : "mødetypen") + " på.");
            FeltFormaal.Focus();
            return;
        }

        // ET DOKUMENT UDEN AFSNIT ER IKKE ET DOKUMENT. Afsnittene er ogsaa
        // dem, der henter materialet frem - uden dem faar modellen kun
        // begyndelsen af hver fil. Se Projektkontekst.
        var afsnit = Projekt ? Afkrydsede() : new List<string>();

        if (Projekt && afsnit.Count == 0 && FeltSkalMed.Text.Trim().Length == 0)
        {
            Vis("Sæt hak ved mindst ét afsnit — eller skriv dine egne nedenfor. "
                + "Afsnittene er både det, der skrives, og det, der bliver hentet frem "
                + "af projektets materiale.");
            return;
        }

        var noegle = SkyNoegle.Hent();
        if (noegle is null) { Vis("Der er ingen API-nøgle. Sæt Mistral op først."); return; }

        Fejl.Visibility = Visibility.Collapsed;
        LavKnap.IsEnabled = false;
        Status.Text = $"Mistral skriver {(Projekt ? "skabelonen" : "mødetypen")} "
            + "… det tager typisk under et minut.";

        var tokens = Længder[Math.Max(0, FeltLaengde.SelectedIndex)].Tokens;

        try
        {
            // SkyRunner tager en skabelon, ikke en loes systemprompt. Her
            // bygges en midlertidig een - den gemmes aldrig, den er kun
            // indpakningen om det ene kald.
            var opskrift = new PromptTemplate
            {
                Name = "Skabelonskriver",
                Temperature = 0.3,
                MaxTokens = 4000,
                SystemPrompt = Projekt ? ProjektOpskrift() : Opskrift(),
                UserPrompt = ""
            };

            var svar = await new SkyRunner(noegle).KoerAsync(
                SkyKatalog.Standard,
                opskrift,
                Opgave(navn, formaal, FeltLaeser.Text.Trim(), FeltSkalMed.Text.Trim(),
                       Længder[Math.Max(0, FeltLaengde.SelectedIndex)].Navn, afsnit));

            Resultat = Byg(navn, formaal, tokens, svar.Tekst);

            // DAGSORDENEN LAVES MED DET SAMME.
            //
            // Skabelonen bestemmer, hvad dokumentet skal indeholde. Dagsordenen
            // bestemmer, hvad der bliver SAGT paa moedet - og udskriften kan
            // kun indeholde det, nogen sagde hoejt. En ny skabelon uden en
            // dagsorden, der passer til den, beder derfor om felter, ingen kom
            // omkring, og de staar tomme i det foerste dokument.
            //
            // Den bygges paa den instruktion, modellen lige har skrevet - ikke
            // paa svarene her - fordi det er instruktionen, dokumentet faktisk
            // laves efter.
            //
            // FEJLER DEN, STAAR SKABELONEN STADIG. Standarddagsordenen gaelder
            // saa, og knappen paa Agenda-fanen kan proeve igen. At kaste
            // skabelonen vaek, fordi dagsordenen ikke kunne skrives, ville
            // vaere at smide det dyre kald vaek for det billige.
            //
            // DEN GEMMES IKKE HER. Findes navnet i forvejen, laegger den, der
            // kaldte, et nummer paa - og saa ville en Gem() her have skrevet
            // hen over dagsordenen paa den skabelon, der allerede hed det.
            // Teksten gives tilbage, og den gemmes, naar navnet ligger fast.
            // HER BAD GUIDEN MISTRAL OM EN DAGSORDEN og gemte den paa
            // moedetypen. Dagsordenen bygges nu af afsnittene, hver gang fanen
            // tegnes - saa der er ikke en at skrive og gemme, og et kald og et
            // fejlspor mindre paa vejen til en ny moedetype.

            DialogResult = true;
        }
        catch (Exception ex)
        {
            Vis($"{hvad} kunne ikke laves: {ex.Message}\n\n" +
                "Prøv igen — det er som regel en midlertidig fejl på forbindelsen.");
            LavKnap.IsEnabled = true;
            Status.Text = "";
        }
    }

    /// <summary>
    /// Instruktionen til den model, der skriver skabelonen.
    ///
    /// Den beder om PROMPTEN og ikke om et referat — det er en anden opgave
    /// end den, appen ellers stiller, og det skal siges tydeligt, ellers
    /// begynder modellen at skrive et eksempel på et dokument i stedet.
    /// </summary>
    private static string Opskrift() =>
        "Du skriver systemprompter til en anden sprogmodel, som skal lave dokumenter ud fra " +
        "transkriptioner af møder. Du skriver ALTID på dansk.\n\n" +
        "Du skal IKKE skrive et eksempel på et dokument. Du skal skrive den INSTRUKTION, " +
        "som en model skal følge for at lave sådan et dokument hver gang.\n\n" +
        "Svar med instruktionen og intet andet — ingen indledning, ingen forklaring af hvad du " +
        "har gjort, ingen markdown-kodeblok omkring.\n\n" +
        "Instruktionen skal:\n" +
        "· være skrevet i bydeform til modellen\n" +
        "· sige, at hele svaret skal være på dansk\n" +
        "· forbyde at finde på tal, navne, datoer og beslutninger, der ikke står i transkriptionen\n" +
        "· beskrive dokumentets afsnit med markdown-overskrifter (##) og sige, hvad hvert " +
        "afsnit skal indeholde\n" +
        "· sige, at et afsnit udelades helt, hvis der ikke er noget at skrive i det\n\n" +
        "Skriv IKKE regler om, hvem der kommer på deltagerlisten — de tilføjes automatisk " +
        "bagefter. Skriv heller ikke et «## Deltagere»-afsnit.\n\n" +
        "Hold dig til 40-70 linjer.";

    /// <summary>
    /// Instruktionen til den model, der skriver en PROJEKTskabelon.
    ///
    /// Forskellen fra <see cref="Opskrift"/> er, hvad dokumentet bygges af:
    /// ikke én udskrift, men et helt projekts materiale — hvor hver stump er
    /// mærket med den fil, den kom fra. Derfor skal instruktionen bede om
    /// kildehenvisninger, og derfor må et afsnit ikke bare forsvinde: en pris,
    /// der ikke står noget sted, skal siges højt, ikke udelades i stilhed.
    /// </summary>
    private static string ProjektOpskrift() =>
        "Du skriver systemprompter til en anden sprogmodel, som skal lave dokumenter ud fra " +
        "MATERIALET I ET PROJEKT — dokumenter, noter og mødereferater, brugeren har samlet. " +
        "Du skriver ALTID på dansk.\n\n" +
        "Du skal IKKE skrive et eksempel på et dokument. Du skal skrive den INSTRUKTION, " +
        "som en model skal følge for at lave sådan et dokument hver gang.\n\n" +
        "Svar med instruktionen og intet andet — ingen indledning, ingen forklaring af hvad du " +
        "har gjort, ingen markdown-kodeblok omkring.\n\n" +
        "Instruktionen skal:\n" +
        "· være skrevet i bydeform til modellen\n" +
        "· forbyde at finde på tal, navne, datoer, priser og beslutninger, der ikke står i " +
        "projektets materiale\n" +
        "· sige, at materialet er mærket med den fil, hver stump kom fra, og at en oplysning, " +
        "der kun står ét sted, skal have filens navn i parentes efter sig\n" +
        "· sige, at et afsnit ALDRIG udelades: er der ikke dækning for det i materialet, skal " +
        "overskriften blive stående med linjen «Ikke oplyst i materialet.» under sig\n" +
        "· sige, at modstrid i materialet — to beløb, to frister — skal skrives frem med hver " +
        "sin kilde, og at modellen ikke selv må vælge det ene\n" +
        "· beskrive dokumentets afsnit med markdown-overskrifter (##) og sige, hvad hvert " +
        "afsnit skal indeholde\n\n" +
        "Skriv IKKE en regel om sproget — den lægges på automatisk. Skriv heller ikke, at " +
        "modellen skal lave en kildeliste til sidst; den lægges også på automatisk.\n\n" +
        "Hold dig til 40-70 linjer.";

    private static string Opgave(string navn, string formaal, string laeser, string skalMed,
                                 string laengde, IReadOnlyList<string> afsnit) =>
        $"Lav instruktionen til en skabelon, der hedder «{navn}».\n\n" +
        $"Hvad der skal komme ud af det:\n{formaal}\n\n" +
        (laeser.Length > 0 ? $"Hvem der skal læse det:\n{laeser}\n\n" : "") +

        // AFSNITTENE ER VALGT MED HAANDEN, OG DE SKAL FOELGES. Uden den her
        // linje skriver modellen sine egne - og saa svarer dokumentet paa
        // noget andet end det, brugeren lige har krydset af.
        (afsnit.Count > 0
            ? "Dokumentet skal have PRÆCIS disse afsnit, i denne rækkefølge, med disse "
              + "overskrifter:\n" + string.Join("\n", afsnit.Select(a => "## " + a)) + "\n\n"
            : "") +

        (skalMed.Length > 0
            ? (afsnit.Count > 0 ? "Derudover skal det altid med:\n" : "Det skal altid med:\n")
              + skalMed + "\n\n"
            : "") +
        $"Længde: {laengde}";

    /// <summary>
    /// Sætter svaret sammen til en skabelon.
    ///
    /// Deltagerreglerne sættes PÅ til sidst som felt, uanset hvad modellen
    /// skrev. De er fælles og skal ikke kunne glemmes af en model, der havde
    /// travlt.
    /// </summary>
    private PromptTemplate Byg(string navn, string formaal, int tokens, string svar)
    {
        var system = svar.Trim();

        // Modeller pakker gerne svaret i en kodeblok, selv naar man beder om
        // det modsatte. Den skal vaek, ellers staar der ``` i prompten.
        if (system.StartsWith("```", StringComparison.Ordinal))
        {
            var foerste = system.IndexOf('\n');
            if (foerste > 0) system = system[(foerste + 1)..];
            if (system.EndsWith("```", StringComparison.Ordinal)) system = system[..^3];
            system = system.Trim();
        }

        // ============ TO SLAGS BRUGERPROMPT ============
        //
        // En moedetype faar EEN udskrift med en titel, en dato og et sprog.
        // Et projektoutput faar projektets materiale, samlet af Projektkontekst
        // og maerket med hvilken fil hver stump kom fra.
        //
        // De to kan ikke bruges i hinandens sted: «{{transskription}}» staar
        // tom, naar der ikke er en optagelse, og «{{kilder}}» staar tom, naar
        // der ikke er et projekt. Et felt, der altid er tomt, ser ud som en
        // oplysning, der mangler.
        //
        // DELTAGERREGLERNE ER FRA I ET PROJEKT. De handler om, hvem der sagde
        // hvad paa et moede; et projekt har ikke deltagere, det har filer.
        return new PromptTemplate
        {
            Name = navn,
            Description = Kort(formaal),
            Slags = _slags,
            Temperature = 0.2,
            MaxTokens = tokens,
            TagDeltagerregler = !Projekt,
            SystemPrompt = Projekt
                ? system
                : system + "\n\n{{" + Deltagerregler.Felt + "}}\n",
            UserPrompt = Projekt
                ? "Her er projektets materiale.\n\n" +
                  "Projekt:\n{{projekt}}\n\n" +
                  "Materialet:\n{{kilder}}"
                : "Her er transkriptionen af mødet.\n\n" +
                  "Titel: {{titel}}\nDato: {{dato}}\nVarighed: {{varighed}}\n" +
                  "Mødet blev holdt på: {{sprog}}\n\n" +
                  "Mine egne noter undervejs:\n{{noter}}\n\n" +
                  "Transkription:\n{{transskription}}"
        };
    }

    /// <summary>Første sætning af formålet, som beskrivelse. Hele afsnittet ville fylde listen.</summary>
    private static string Kort(string formaal)
    {
        var linje = formaal.Replace('\n', ' ').Replace('\r', ' ').Trim();
        var punktum = linje.IndexOf('.');
        if (punktum > 20) linje = linje[..punktum];
        return linje.Length > 140 ? linje[..137] + "…" : linje;
    }

    private void Vis(string besked)
    {
        Fejl.Text = besked;
        Fejl.Visibility = Visibility.Visible;
    }
}
