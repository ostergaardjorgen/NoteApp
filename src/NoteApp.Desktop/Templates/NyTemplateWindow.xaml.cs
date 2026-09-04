using System.Windows;
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

    private static readonly (string Navn, int Tokens)[] Længder =
    {
        ("Kort — et notat på en halv side", 1024),
        ("Almindelig — et referat på et par sider", 4096),
        ("Udførlig — alle detaljer med, ingen øvre grænse i praksis", 32000)
    };

    public NyTemplateWindow()
    {
        InitializeComponent();

        FeltLaengde.ItemsSource = Længder.Select(l => l.Navn).ToList();
        FeltLaengde.SelectedIndex = 1;

        // Uden noegle er der ingen model at spoerge. Knappen bliver graa frem
        // for at fejle bagefter, og der peges paa opsaetningen.
        Klar(SkyNoegle.Hent() is not null);

        FeltNavn.Focus();
    }

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

        Status.Text = harNoegle
            ? ""
            : "Mødetypen skrives af Mistral, og den er ikke sat op endnu.";
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
        var navn = FeltNavn.Text.Trim();
        if (navn.Length == 0)
        {
            Vis("Mødetypen skal have et navn.");
            FeltNavn.Focus();
            return;
        }

        var formaal = FeltFormaal.Text.Trim();
        if (formaal.Length == 0)
        {
            Vis("Skriv, hvad der skal komme ud af det. Det er dét, modellen bygger mødetypen på.");
            FeltFormaal.Focus();
            return;
        }

        var noegle = SkyNoegle.Hent();
        if (noegle is null) { Vis("Der er ingen API-nøgle. Sæt Mistral op først."); return; }

        Fejl.Visibility = Visibility.Collapsed;
        LavKnap.IsEnabled = false;
        Status.Text = "Mistral skriver mødetypen … det tager typisk under et minut.";

        var tokens = Længder[Math.Max(0, FeltLaengde.SelectedIndex)].Tokens;

        try
        {
            // SkyRunner tager en skabelon, ikke en loes systemprompt. Her
            // bygges en midlertidig een - den gemmes aldrig, den er kun
            // indpakningen om det ene kald.
            var opskrift = new PromptTemplate
            {
                Name = "Mødetypeskriver",
                Temperature = 0.3,
                MaxTokens = 4000,
                SystemPrompt = Opskrift(),
                UserPrompt = ""
            };

            var svar = await new SkyRunner(noegle).KoerAsync(
                SkyKatalog.Standard,
                opskrift,
                Opgave(navn, formaal, FeltLaeser.Text.Trim(), FeltSkalMed.Text.Trim(),
                       Længder[Math.Max(0, FeltLaengde.SelectedIndex)].Navn));

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
            Vis($"Mødetypen kunne ikke laves: {ex.Message}\n\n" +
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

    private static string Opgave(string navn, string formaal, string laeser, string skalMed, string laengde) =>
        $"Lav instruktionen til en skabelon, der hedder «{navn}».\n\n" +
        $"Hvad der skal komme ud af det:\n{formaal}\n\n" +
        (laeser.Length > 0 ? $"Hvem der skal læse det:\n{laeser}\n\n" : "") +
        (skalMed.Length > 0 ? $"Det skal altid med:\n{skalMed}\n\n" : "") +
        $"Længde: {laengde}";

    /// <summary>
    /// Sætter svaret sammen til en skabelon.
    ///
    /// Deltagerreglerne sættes PÅ til sidst som felt, uanset hvad modellen
    /// skrev. De er fælles og skal ikke kunne glemmes af en model, der havde
    /// travlt.
    /// </summary>
    private static PromptTemplate Byg(string navn, string formaal, int tokens, string svar)
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

        return new PromptTemplate
        {
            Name = navn,
            Description = Kort(formaal),
            Temperature = 0.2,
            MaxTokens = tokens,
            SystemPrompt = system + "\n\n{{" + Deltagerregler.Felt + "}}\n",
            UserPrompt =
                "Her er transkriptionen af mødet.\n\n" +
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
