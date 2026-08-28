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
        "Baggrund", "Panel", "PanelKant", "FeltKant",
        "Svaev", "Trykket", "Valgt",
        "Tekst", "TekstSvag", "TekstMeget", "Slukket",
        "Accent", "Optager", "Godkendt", "Advarsel", "FejlTekst",
        "PaaAccent", "PaaOptager",
        "AccentFlade", "GodkendtFlade", "AdvarselFlade", "FejlFlade",
        "Dokument", "Fremhaev", "PaaFremhaev"
    };

    /// <summary>
    /// Det lyse tema — PIA's egne farver.
    /// </summary>
    /// <remarks>
    /// MÆRKETS EGNE: Baggrund er isblå #F0F4F8, Panel er ren hvid, Tekst er
    /// natblå #101828 (17,8:1 på hvidt), og Accent er den dybe mørkeblå
    /// #0D3B66 (11,5:1).
    ///
    /// DEN FRISKE GRØNNE ER IKKE ACCENT HER. #02C39A ligger på 2,3:1 mod
    /// hvidt — den kan hverken bære tekst eller en tynd streg. En knap i den
    /// farve med hvid skrift ville være ulæselig. Den bruges derfor som
    /// FLADE, hvor skriften er mørk, og som udgangspunkt for Godkendt, der er
    /// mørknet til 5:1.
    ///
    /// Resten er AFLEDT: samme kulør som mærkets mørkeblå, anden lysstyrke.
    /// Hver nøgle sigter mod sit eget tal, så der er et hierarki — første
    /// forsøg søgte bare «mindst 4,5», og så blev PanelKant, TekstSvag,
    /// TekstMeget og Slukket alle den SAMME farve. Prøven sagde ja, og der
    /// var intet trin tilbage mellem dem.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Lys =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FFF0F4F8",
            ["Panel"]      = "#FFFFFFFF",
            ["PanelKant"]  = "#FFA1CBF3",
            ["FeltKant"]   = "#FF2C8BE4",

            // Musen over, knappen nede, raekken valgt. De laa hardkodet i
            // App.xaml som moerkeblaa toner - femten steder, der ikke fulgte
            // med et temaskift, og som ville have staaet som moerke pletter i
            // en lys app.
            ["Svaev"]      = "#FFEFF5FB",
            ["Trykket"]    = "#FFE2EEF8",
            ["Valgt"]      = "#FFC9DEF2",

            ["Tekst"]      = "#FF101828",
            ["TekstSvag"]  = "#FF3E5061",
            ["TekstMeget"] = "#FF53687D",

            // Tekst paa noget, man ikke kan bruge. WCAG undtager
            // deaktiverede kontroller, men en knap, man ikke kan LAESE,
            // kan man heller ikke finde ud af, hvorfor man ikke kan trykke
            // paa - saa den holdes paa 3:1.
            ["Slukket"]    = "#FF728699",

            ["Accent"]     = "#FF0D3B66",
            ["Optager"]    = "#FFC72C22",
            ["Godkendt"]   = "#FF01775E",
            ["Advarsel"]   = "#FF876208",
            ["FejlTekst"]  = "#FFC72C22",

            // Tekst OVEN PAA en farvet knap. I det lyse tema er accenten
            // moerk nok til hvid skrift; i det moerke er den lys, og saa er
            // det omvendt. Det er den samme regel, WinUI foelger.
            ["PaaAccent"]  = "#FFFFFFFF",
            ["PaaOptager"] = "#FFFFFFFF",

            // Flader, der er tonet i betydningens farve: den groenne kasse om
            // noget, der er i orden, den roede om noget, der ikke er.
            ["AccentFlade"]   = "#FFD5E6F5",
            ["GodkendtFlade"] = "#FFD0F9F0",
            ["AdvarselFlade"] = "#FFF7ECD1",
            ["FejlFlade"]     = "#FFF8E6E5",

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
    /// Det mørke tema — PIA's egne farver.
    /// </summary>
    /// <remarks>
    /// MÆRKETS EGNE: Baggrund er natblå #101828 — mærkets egen mørke farve, så
    /// det mørke tema står på brandets grund og ikke på en tilfældig grå.
    /// Tekst er isblå #F0F4F8 (16,1:1), og Accent er den friske grønne
    /// #02C39A (7,8:1). Her kan den godt bære sin rolle.
    ///
    /// DEN DYBE MØRKEBLÅ KAN IKKE BRUGES HER. #0D3B66 ligger på 1,55:1 mod
    /// natblå — de to har næsten samme lysstyrke. Den bruges derfor kun som
    /// KULØR til de afledte farver, aldrig som en farve, noget skal ses på.
    ///
    /// Panelet er natblå løftet et nøk. Første forsøg løftede det til
    /// #205688, og det er ikke et panel, det er en knap — alt, der blev målt
    /// mod den flade, blev vasket ud, og Advarsel endte på ren hvid.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> Moerk =
        new Dictionary<string, string>
        {
            ["Baggrund"]   = "#FF101828",
            ["Panel"]      = "#FF112538",
            ["PanelKant"]  = "#FF10497F",
            ["FeltKant"]   = "#FF008072",

            ["Svaev"]      = "#FF112D48",
            ["Trykket"]    = "#FF0C2134",
            ["Valgt"]      = "#FF095149",

            ["Tekst"]      = "#FFF0F4F8",
            ["TekstSvag"]  = "#FFB3C7DB",
            ["TekstMeget"] = "#FF8CABC9",

            // Var #FF4A515E og laa paa 1,9:1 mod panelet - en graa knap kunne
            // ikke laeses. Haevet til 3:1 samtidig med, at den blev en noegle.
            ["Slukket"]    = "#FF64788A",

            ["Accent"]     = "#FF02C39A",
            ["Optager"]    = "#FFFF676B",
            ["Godkendt"]   = "#FF02C59B",
            ["Advarsel"]   = "#FFEFAE31",
            ["FejlTekst"]  = "#FFFF676B",

            ["PaaAccent"]  = "#FF101828",
            ["PaaOptager"] = "#FF101828",

            ["AccentFlade"]   = "#FF08443D",
            ["GodkendtFlade"] = "#FF09473A",
            ["AdvarselFlade"] = "#FF523B0F",
            ["FejlFlade"]     = "#FF5F0B0D",

            ["Dokument"]    = "#FFCB90F0",
            ["Fremhaev"]    = "#FFF4CB23",
            ["PaaFremhaev"] = "#FF101828"
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
