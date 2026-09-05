namespace NoteApp.Core;

/// <summary>Hvad brugeren har valgt under Indstillinger.</summary>
public enum Temavalg
{
    /// <summary>Følg Windows' egen indstilling. Standard.</summary>
    FoelgWindows = 0,
    Lyst = 1,
    Moerkt = 2
}

/// <summary>
/// Appens to paletter — og beviset for, at de kan læses.
///
/// HVORFOR FARVERNE LIGGER I C# OG IKKE I XAML
///
/// Fordi de skal kunne EFTERPRØVES. En farve i en XAML-fil er en streng, som
/// ingen kan regne på; her er de data, og <see cref="Kontrast"/> er den samme
/// formel, WCAG bruger. Prøverne i TemaTest afviser en palet, hvor brødteksten
/// ikke kan læses — så kan en pæn farve ikke snige sig ind.
///
/// Det er ikke et hypotetisk problem, og det er sket tre gange. Den første
/// palet var pæn og for svag — brødteksten lå omkring 4:1. Da PIA's farver kom
/// ind 28-08-2026, viste regnestykket, at mærkets grønne accent ligger på
/// 2,3:1 mod hvidt: en CTA-knap i den farve med hvid skrift ville være
/// ulæselig. Og prøven fangede en palet, hvor fire nøgler var blevet den samme
/// farve, fordi der blev søgt et minimum i stedet for et mål.
///
/// SAMME NØGLER I BEGGE PALETTER
///
/// De to paletter har præcis de samme nøgler, og det er hele grunden til, at
/// et temaskift er billigt: skærmbillederne slår farven op på navn — 969
/// steder — og ved ikke, hvilken palet der er i brug. Der skiftes ét sted.
/// <see cref="Nøglerne"/> holder listen, og en prøve tjekker, at ingen palet
/// mangler en af dem.
///
/// HVORDAN SKIFTET SKER, UDEN AT ALT SKAL TEGNES OM
///
/// Se Temaskift i Desktop. Kort: hver farve står som en `Color` i App.xaml,
/// og penslen henter den med en DynamicResource. Temaskiftet bytter FARVEN,
/// og penslen følger med af sig selv — de 969 opslag på `{StaticResource ...}`
/// mærker intet.
///
/// FØRSTE FORSØG VAR AT ÆNDRE PENSLERNES Color DIREKTE, og det virkede ikke:
/// WPF fryser selv resurser fra XAML, og en frossen pensel kan ikke ændres.
/// Alle femogtyve var frosne. Omvejen over en `Color` er ikke pynt — den er
/// den eneste måde, en pensel fra XAML kan blive ved med at kunne skifte.
/// </summary>
public static class Tema
{
    /// <summary>
    /// De navne, en palet SKAL have. Rækkefølgen er den, de står i i appen:
    /// flader, så kanter, så tekst, så de fire betydninger.
    /// </summary>
    public static readonly IReadOnlyList<string> Nøglerne = new[]
    {
        "Baggrund", "Panel", "PanelKant",
        "InputKant", "InputFokusKant",
        "Svaev", "Trykket", "Valgt",
        "Tekst", "TekstSvag", "TekstMeget", "Slukket",
        "Accent", "Optager", "Godkendt", "Advarsel", "FejlTekst",
        "PaaAccent", "PaaOptager",
        "AccentFlade", "GodkendtFlade", "AdvarselFlade", "FejlFlade",
        "OptagerFlade",
        "NavigationFlade", "NavigationValgt", "NavigationKant",
        "PaaNavigation", "PaaNavigationSvag",
        "Dokument", "Fremhaev", "PaaFremhaev"
    };

    /// <summary>
    /// Det lyse tema — «rolig, nordisk arbejdsflade».
    /// </summary>
    /// <remarks>
    /// SÅDAN LÅ DEN FØR, OG SÅDAN LIGGER DEN NU. Baggrunden var isblå
    /// #F0F4F8, panelkanten lyseblå #A1CBF3 og feltkanten en mættet blå
    /// #2C8BE4. Alt var altså blåt: fladen, kanterne, felterne og knapperne.
    /// Når ALT er brandfarve, siger brandfarven ingenting — og en blå kant om
    /// hvert eneste felt støjer, uanset hvor pæn den er.
    ///
    /// NU HAR BLÅ ÉN ROLLE: navigation og handling. Arbejdsfladen er en
    /// næsten neutral #F6F8FB, panelerne er hvide, og kanterne er gråblå.
    /// Farven bruges dér, hvor der skal klikkes.
    ///
    /// SIDEBJÆLKEN ER MØRKEBLÅ, OGSÅ I DET LYSE TEMA. Det er dét ene greb,
    /// der giver appen et ansigt: uden det var den hvide paneler på en lysegrå
    /// flade — pænt, og til forveksling som enhver anden Windows-app. Derfor
    /// har navigationen sine EGNE nøgler; teksten derinde er lys, mens resten
    /// af appen er mørk på lyst, og de to må ikke blandes sammen.
    ///
    /// GRØN BETYDER KUN «DET GIK GODT». Den friske #02C39A ligger på 2,3:1
    /// mod hvidt og kan hverken bære tekst eller en streg; den bruges derfor
    /// som flade, og Godkendt er mørknet til #08765D.
    ///
    /// Hver nøgle sigter mod sit eget tal, så der er et hierarki — første
    /// forsøg søgte bare «mindst 4,5», og så blev PanelKant, TekstSvag,
    /// TekstMeget og Slukket alle den SAMME farve. Prøven sagde ja, og der
    /// var intet trin tilbage mellem dem.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Lys =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FFF6F8FB",
            ["Panel"]      = "#FFFFFFFF",
            // ============ KANTEN ER BLAA IGEN, OG DET ER MED VILJE ============
            //
            // Den laa paa #A1CBF3 og blev daempet til #D2DCE8 - fra 1,70 til
            // 1,39 mod panelet - dengang alt det blaa skulle ned. Det var en
            // tredjedel for meget: rammerne om kalenderens og opgavernes kort
            // forsvandt, og posterne blev til tekst, der laa loest paa en
            // flade.
            //
            // Den var kortvarigt paa 1,90 og var da for meget: kortene
            // raabte, og opgaverne - som har flere kanter pr. kort end
            // kalenderen - raabte hoejest. Nu 1,55 begge steder, og
            // FLADEN goer en del af arbejdet i stedet: kortet er en anden
            // farve end panelet, saa kanten kun skal afgraense det. Det modsiger ikke «mindre blaat
            // overalt»: en haarfin streg paa een pixel er ikke en flade. Det,
            // der stoejede, var de blaa FLADER og de blaa feltkanter om
            // hvert eneste felt, og de er der ikke mere.
            //
            // Modstykket i det moerke tema er maerkets #10497F, som stod der
            // fra begyndelsen. De to har samme styrke: 1,90 og 1,86.
            ["PanelKant"]  = "#FFBCD2E9",

            // ============ KANTEN OM ET SKRIVEFELT ============
            //
            // TO NOEGLER, IKKE EEN. Foer laa der «FeltKant» alene, og den var
            // en maettet blaa, som ALLE felter havde HELE tiden. Et felt, man
            // ikke har roert, raabte lige saa hoejt som det, markoeren staar
            // i - og saa er der ingen forskel paa at vaere et sted og at vaere
            // et andet sted.
            //
            // Hvilekanten er graablaa og lige praecis synlig: WCAG 1.4.11
            // kraever 3:1 for noget, man skal kunne finde. Fokuskanten er den
            // klare blaa, og den staar KUN paa det felt, der skrives i.
            ["InputKant"]      = "#FF7E8FA3",
            ["InputFokusKant"] = "#FF2A6FAD",

            // Musen over, knappen nede, raekken valgt. De laa hardkodet i
            // App.xaml som moerkeblaa toner - femten steder, der ikke fulgte
            // med et temaskift, og som ville have staaet som moerke pletter i
            // en lys app.
            ["Svaev"]      = "#FFEDF2F8",
            ["Trykket"]    = "#FFDFE7F1",
            ["Valgt"]      = "#FFDCE9F6",

            ["Tekst"]      = "#FF101828",
            ["TekstSvag"]  = "#FF3E5061",
            ["TekstMeget"] = "#FF53687D",

            // Tekst paa noget, man ikke kan bruge. WCAG undtager
            // deaktiverede kontroller, men en knap, man ikke kan LAESE,
            // kan man heller ikke finde ud af, hvorfor man ikke kan trykke
            // paa - saa den holdes paa 3:1.
            ["Slukket"]    = "#FF728699",

            // ACCENT ER HANDLING OG FOKUS. Ikke succes, ikke fremdrift, ikke
            // «forbundet» - dem har Godkendt. Se noten paa den moerke palet:
            // dér var de to naesten samme groenne, og saa betoed farven fire
            // ting paa een gang.
            ["Accent"]     = "#FF0D3B66",
            ["Optager"]    = "#FFB83B34",
            ["Godkendt"]   = "#FF08765D",
            ["Advarsel"]   = "#FF876208",
            ["FejlTekst"]  = "#FFB83B34",

            // Tekst OVEN PAA en farvet knap. I det lyse tema er accenten
            // moerk nok til hvid skrift; i det moerke er den lys, og saa er
            // det omvendt. Det er den samme regel, WinUI foelger.
            ["PaaAccent"]  = "#FFFFFFFF",
            ["PaaOptager"] = "#FFFFFFFF",

            // Flader, der er tonet i betydningens farve: den groenne kasse om
            // noget, der er i orden, den roede om noget, der ikke er.
            ["AccentFlade"]   = "#FFDEE9F5",
            ["GodkendtFlade"] = "#FFD6F0E7",
            ["AdvarselFlade"] = "#FFF7ECD1",
            ["FejlFlade"]     = "#FFF8E6E5",

            // Fladen bag «der optages nu». Se Optager.
            ["OptagerFlade"]  = "#FFFBE4E5",

            // ============ SIDEBJAELKEN ============
            //
            // Den er MOERKEBLAA i begge temaer. I det lyse er den appens
            // ansigt; i det moerke er den et noek moerkere end baggrunden, saa
            // arbejdsfladen loefter sig fra den.
            //
            // Teksten derinde kan IKKE hente sin farve i «Tekst». Den er
            // moerk paa lyst, og paa en natblaa bjaelke ville den vaere
            // usynlig. Derfor to egne tekstnoegler.
            //
            // FARVERNE ER DYBERE END MAERKETS EGEN #0D3B66, og det er med
            // vilje. Maalestokken er det MOERKE tema: dér staar et hvilende
            // menupunkt paa 9,7:1 og et valgt paa 10,3:1, og det er dét, der
            // goer bjaelken nem at laese. Paa maerkets #0D3B66 kunne den
            // lyse palet ikke naa hoejere end 7,7 og 6,6 - det valgte punkt
            // skal jo staa paa en LYSERE flade, og saa er der ikke plads.
            //
            // En dybere raekke giver plads til begge dele: 10,5 hvilende og
            // 9,2 valgt. Maerkets egen blaa er stadig Accent - knapper,
            // links og markering - saa den er ikke vaek, den er flyttet
            // derhen, hvor der klikkes.
            ["NavigationFlade"]   = "#FF092845",
            ["NavigationValgt"]   = "#FF15476C",
            ["NavigationKant"]    = "#FF11375C",
            ["PaaNavigation"]     = "#FFF4F8FC",
            ["PaaNavigationSvag"] = "#FFC7DAEC",

            // Et dokument er ikke en udskrift og ikke en note. Den lilla er
            // det eneste sted i appen, farven BETYDER noget andet end
            // "godt/pas paa/galt" - og den skal derfor kunne skelnes fra
            // accenten, som er blaa.
            ["Dokument"]    = "#FF7A45C0",

            // Fundet i en soegning. Gul med moerk skrift, som en overstregning
            // paa papir.
            ["Fremhaev"]    = "#FFF1DFA1",
            ["PaaFremhaev"] = "#FF101828"
        };

    /// <summary>
    /// Det mørke tema.
    /// </summary>
    /// <remarks>
    /// HER LÅ FEJLEN, DER VAR SVÆREST AT SE. Accent var #02C39A og Godkendt
    /// #02C59B — to farver, der er umulige at skelne. Grøn betød derfor på én
    /// gang «tryk her», «du står her», «den arbejder» og «det gik godt», og
    /// en flade, hvor alt betyder noget, betyder ingenting. Accenten er nu
    /// den lyse blå #73C2FB, og grøn er kun succes.
    ///
    /// BAGGRUNDEN ER SÆNKET fra #101828 til #0B1220, og panelet fra #112538
    /// til #111C2D. Ikke for at gøre den mørkere for mørkets skyld: de tre
    /// flader — baggrund, panel og det hævede panel — lå tæt og var alle
    /// stærkt blåmættede, og lange indstillingsskærme blev derfor ét fladt
    /// stykke. Nu er trinene større og kuløren roligere, så DYBDEN gør
    /// arbejdet i stedet for kanterne.
    ///
    /// DEN DYBE MØRKEBLÅ KAN IKKE BRUGES SOM ACCENT HER. #0D3B66 ligger på
    /// 1,4:1 mod baggrunden — de to har næsten samme lysstyrke. Den er
    /// derfor sidebjælkens kulør og intet andet.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Moerk =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FF0B1220",
            ["Panel"]      = "#FF111C2D",
            // Maerkets #10497F stod her og laa paa 1,86 mod panelet. Det
            // var for skarpt - se noten i den lyse palet. Nu 1,56, og
            // kortets flade loefter sig fra panelet i stedet, saa kanten
            // kun skal afgraense.
            ["PanelKant"]  = "#FF1B3E62",

            ["InputKant"]      = "#FF556D86",
            ["InputFokusKant"] = "#FF73C2FB",

            ["Svaev"]      = "#FF1E2C40",
            ["Trykket"]    = "#FF1A2739",
            ["Valgt"]      = "#FF1B3A5C",

            ["Tekst"]      = "#FFEFF5FA",
            ["TekstSvag"]  = "#FFB3C7DB",
            ["TekstMeget"] = "#FF8CABC9",

            // Var #FF4A515E og laa paa 1,9:1 mod panelet - en graa knap kunne
            // ikke laeses. Haevet til 3:1 samtidig med, at den blev en noegle.
            ["Slukket"]    = "#FF64788A",

            ["Accent"]     = "#FF73C2FB",
            ["Optager"]    = "#FFFF7770",
            ["Godkendt"]   = "#FF30C993",
            ["Advarsel"]   = "#FFEFAE31",
            ["FejlTekst"]  = "#FFFF7770",

            ["PaaAccent"]  = "#FF0B1220",
            ["PaaOptager"] = "#FF0B1220",

            ["AccentFlade"]   = "#FF0F2E4A",
            ["GodkendtFlade"] = "#FF0B3C31",
            ["AdvarselFlade"] = "#FF523B0F",
            ["FejlFlade"]     = "#FF5A1512",
            ["OptagerFlade"]  = "#FF4A1416",

            ["NavigationFlade"]   = "#FF0D1524",
            ["NavigationValgt"]   = "#FF1B3C5C",
            ["NavigationKant"]    = "#FF1B2A3E",
            ["PaaNavigation"]     = "#FFEFF5FA",
            ["PaaNavigationSvag"] = "#FFA9C0D6",

            ["Dokument"]    = "#FFCB90F0",
            ["Fremhaev"]    = "#FFF4CB23",
            ["PaaFremhaev"] = "#FF0B1220"
        };

    public static IReadOnlyDictionary<string, string> Palet(bool lyst) => lyst ? Lys : Moerk;

    // ------------------------------------------------------------ WCAG

    /// <summary>Relativ luminans, som WCAG 2.1 definerer den.</summary>
    private static double Lys_(string farve)
    {
        var (r, g, b) = Kanaler(farve);

        static double K(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        return 0.2126 * K(r) + 0.7152 * K(g) + 0.0722 * K(b);
    }

    /// <summary>
    /// Kontrastforholdet mellem to farver. 1 = ens, 21 = sort mod hvid.
    /// </summary>
    /// <remarks>
    /// Brødtekst skal over 4,5:1, stor tekst over 3:1, og grafiske skel — en
    /// feltkant, man skal kunne finde — over 3:1. Det er WCAG 2.1 AA.
    /// </remarks>
    public static double Kontrast(string a, string b)
    {
        double la = Lys_(a), lb = Lys_(b);
        var (høj, lav) = la > lb ? (la, lb) : (lb, la);
        return (høj + 0.05) / (lav + 0.05);
    }

    /// <summary>
    /// Farven som R, G og B mellem 0 og 1. Tager både #AARRGGBB og #RRGGBB.
    /// </summary>
    /// <remarks>
    /// GENNEMSIGTIGHED IGNORERES. Alfa-kanalen læses ikke, og det er med
    /// vilje: en halvgennemsigtig farve har ingen kontrast, før man ved, hvad
    /// der ligger bagved. Paletten har derfor kun uigennemsigtige farver, og
    /// en prøve holder øje med det.
    /// </remarks>
    public static (double R, double G, double B) Kanaler(string farve)
    {
        var h = farve.TrimStart('#');

        if (h.Length == 8) h = h[2..];
        if (h.Length != 6) throw new ArgumentException($"Ikke en farve: {farve}", nameof(farve));

        return (Convert.ToInt32(h[..2], 16) / 255.0,
                Convert.ToInt32(h.Substring(2, 2), 16) / 255.0,
                Convert.ToInt32(h.Substring(4, 2), 16) / 255.0);
    }

    /// <summary>Er farven uigennemsigtig? Paletten må ikke indeholde andet.</summary>
    public static bool ErUigennemsigtig(string farve)
    {
        var h = farve.TrimStart('#');
        return h.Length == 6 || (h.Length == 8 && h[..2].Equals("FF", StringComparison.OrdinalIgnoreCase));
    }
}
