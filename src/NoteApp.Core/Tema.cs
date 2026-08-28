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
/// Det er ikke et hypotetisk problem. Den første palet var pæn og for svag:
/// brødteksten lå omkring 4:1, under kravet på 4,5:1. Det blev fundet i hånden
/// 10-08-2026. Anden gang blev det fundet af en prøve.
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
        "Baggrund", "Panel", "PanelKant", "FeltKant",
        "Svaev", "Trykket", "Valgt",
        "Tekst", "TekstSvag", "TekstMeget", "Slukket",
        "Accent", "Optager", "Godkendt", "Advarsel", "FejlTekst",
        "PaaAccent", "PaaOptager",
        "AccentFlade", "GodkendtFlade", "AdvarselFlade", "FejlFlade",
        "Dokument", "Fremhaev", "PaaFremhaev"
    };

    /// <summary>
    /// Det lyse tema.
    /// </summary>
    /// <remarks>
    /// BAGGRUNDEN ER IKKE HVID. Den er en anelse varm og et nøk mørkere end
    /// panelerne, så et kort kan ses at ligge OVEN PÅ noget. Er begge dele
    /// rent hvide, forsvinder inddelingen, og siden bliver én stor flade.
    ///
    /// Ren hvid baggrund er også hårdere at se på i lange stræk — det er den
    /// samme indvending, der gjorde appen mørk til at begynde med, og den
    /// forsvinder ikke, fordi temaet skifter.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Lys =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FFF7F7F5",
            ["Panel"]      = "#FFFFFFFF",
            ["PanelKant"]  = "#FFCCCCC5",
            ["FeltKant"]   = "#FF909081",

            // Musen over, knappen nede, raekken valgt. De laa hardkodet i
            // App.xaml som moerkeblaa toner - femten steder, der ikke fulgte
            // med et temaskift, og som ville have staaet som moerke pletter i
            // en lys app.
            ["Svaev"]      = "#FFF1F1EF",
            ["Trykket"]    = "#FFE8E8E5",
            ["Valgt"]      = "#FFD9E6F8",

            ["Tekst"]      = "#FF1B1B19",
            ["TekstSvag"]  = "#FF5A5A54",
            ["TekstMeget"] = "#FF6E6E67",

            // Tekst paa noget, man ikke kan bruge. WCAG undtager
            // deaktiverede kontroller, men en knap, man ikke kan LAESE,
            // kan man heller ikke finde ud af, hvorfor man ikke kan trykke
            // paa - saa den holdes paa 3:1.
            ["Slukket"]    = "#FF909087",

            ["Accent"]     = "#FF2A6DD4",
            ["Optager"]    = "#FFC9313A",
            ["Godkendt"]   = "#FF1B7340",
            ["Advarsel"]   = "#FF8A5A00",
            ["FejlTekst"]  = "#FFC9313A",

            // Tekst OVEN PAA en farvet knap. I det lyse tema er accenten
            // moerk nok til hvid skrift; i det moerke er den lys, og saa er
            // det omvendt. Det er den samme regel, WinUI foelger.
            ["PaaAccent"]  = "#FFFFFFFF",
            ["PaaOptager"] = "#FFFFFFFF",

            // Flader, der er tonet i betydningens farve: den groenne kasse om
            // noget, der er i orden, den roede om noget, der ikke er.
            ["AccentFlade"]   = "#FFEAF1FC",
            ["GodkendtFlade"] = "#FFE7F4EC",
            ["AdvarselFlade"] = "#FFFAF0DC",
            ["FejlFlade"]     = "#FFFBEAEB",

            // Et dokument er ikke en udskrift og ikke en note. Den lilla er
            // det eneste sted i appen, farven BETYDER noget andet end
            // "godt/pas paa/galt" - og den skal derfor kunne skelnes fra
            // accenten, som er blaa.
            ["Dokument"]    = "#FF7B3FAF",

            // Fundet i en soegning. Gul med moerk skrift, som en overstregning
            // paa papir.
            ["Fremhaev"]    = "#FFFFE99A",
            ["PaaFremhaev"] = "#FF1B1B19"
        };

    /// <summary>
    /// Det mørke tema — det, appen havde fra begyndelsen.
    /// </summary>
    /// <remarks>
    /// TO FARVER ER RETTET 28-08-2026. Optager og FejlTekst lå på 4,43:1 mod
    /// panelfladen — lige under kravet, og ingen havde regnet efter. Det blev
    /// fundet, første gang prøven blev skrevet, og det er hele pointen med at
    /// have den. #FFF05055 blev til #FFF05358: forskellen kan ikke ses, og
    /// den flytter forholdet til 4,51.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Moerk =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FF14161A",
            ["Panel"]      = "#FF20242C",
            ["PanelKant"]  = "#FF39404E",
            ["FeltKant"]   = "#FF626E86",

            ["Svaev"]      = "#FF2C323D",
            ["Trykket"]    = "#FF181B22",
            ["Valgt"]      = "#FF33405A",

            ["Tekst"]      = "#FFF4F6FA",
            ["TekstSvag"]  = "#FFC2CAD8",
            ["TekstMeget"] = "#FFAEB6C4",

            // Var #FF4A515E og laa paa 1,9:1 mod panelet - en graa knap kunne
            // ikke laeses. Haevet til 3:1 samtidig med, at den blev en noegle.
            ["Slukket"]    = "#FF6E7787",

            ["Accent"]     = "#FF5B99FF",
            ["Optager"]    = "#FFF05358",
            ["Godkendt"]   = "#FF4CBE72",
            ["Advarsel"]   = "#FFF0B23C",
            ["FejlTekst"]  = "#FFF05358",

            ["PaaAccent"]  = "#FF0B1220",
            ["PaaOptager"] = "#FF1A0A0B",

            ["AccentFlade"]   = "#FF1D2530",
            ["GodkendtFlade"] = "#FF16241C",
            ["AdvarselFlade"] = "#FF2A2318",
            ["FejlFlade"]     = "#FF2A1A1A",

            ["Dokument"]    = "#FFC98CF0",
            ["Fremhaev"]    = "#FFF5D13B",
            ["PaaFremhaev"] = "#FF14181F"
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
