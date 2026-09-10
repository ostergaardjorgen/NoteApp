using System.Globalization;

namespace NoteApp.Core;

/// <summary>
/// De navne, der kan afgrænses på — de samme, som står i Cockpittets filtre.
/// </summary>
/// <remarks>
/// DE KOMMER FRA FILTRENE OG IKKE ANDRE STEDER FRA. Genkender sætningen en
/// mappe, skal det være en, der også kan vælges i listen — ellers afgrænser
/// fortolkningen til noget, brugeren ikke selv kan se eller slå fra.
/// </remarks>
/// <param name="Projekter">Id og navn. Filtret afgrænser på id'et.</param>
public sealed record Kendtenavne(IReadOnlyList<string> Mapper,
                                 IReadOnlyList<(string Id, string Navn)> Projekter)
{
    public static readonly Kendtenavne Ingen =
        new(Array.Empty<string>(), Array.Empty<(string, string)>());
}

/// <summary>
/// Hvad der blev forstået af en søgning skrevet som en almindelig sætning.
/// </summary>
/// <param name="Soegeord">Det, der skal søges på — uden fyldord, tid og afgrænsninger.</param>
/// <param name="Fra">Periodens begyndelse, hvis sætningen nævnte en.</param>
/// <param name="Til">Periodens slutning, hvis den har en.</param>
/// <param name="Periode">Perioden, som den skal stå på skærmen. Null uden periode.</param>
/// <param name="Aendret">Blev der ændret noget i forhold til det, der blev skrevet?</param>
/// <param name="Projekt">Projektets id — det, filtret afgrænser på.</param>
/// <param name="Sprog">Sprogkoden, fx «en».</param>
public sealed record Fortolkning(string Soegeord, DateTimeOffset? Fra, DateTimeOffset? Til,
                                 string? Periode, bool Aendret,
                                 string? Mappe = null, Soegetype? Type = null,
                                 string? Projekt = null, string? Projektnavn = null,
                                 string? Sprog = null)
{
    /// <summary>
    /// Det, der blev forstået, på én linje: «Søger efter «Omada» · august 2026».
    /// </summary>
    public string Beskriv()
    {
        var dele = new List<string> { $"Søger efter «{Soegeord}»" };

        if (Type is { } t)
            dele.Add(t switch
            {
                Soegetype.Opkald => "kun opkald",
                Soegetype.Webinar => "kun webinarer",
                Soegetype.Note => "kun noter",
                _ => "kun møder",
            });

        if (Periode is not null) dele.Add(Periode);
        if (Mappe is not null) dele.Add($"mappen {Mappe}");
        if (Projektnavn is not null) dele.Add($"projektet {Projektnavn}");
        if (Sprog is not null) dele.Add($"på {Fritekst.Sprognavn(Sprog)}");

        return string.Join("  ·  ", dele);
    }
}

/// <summary>
/// Gør en sætning til en søgning: «Talte med en om Omada i sidste måned, kan
/// du finde mødet» bliver til «Omada» i august.
/// </summary>
/// <remarks>
/// ============ HVORFOR DET ER NØDVENDIGT ============
///
/// Søgningen kræver, at ALLE ord står i det samme møde. Det er rigtigt, når
/// man skriver søgeord — hvert ord skal snævre ind. Men skriver eller siger man
/// en hel sætning, bliver «talte», «med», «en» og «mødet» også krav, og så er
/// svaret nul fund, selvom mødet med Omada ligger der.
///
/// ============ HVORFOR DET IKKE ER EN SPROGMODEL ============
///
/// Spørgsmålet har faste dele, og ingen af dem kræver en model. «I sidste
/// måned» er en fast vending med ét rigtigt svar, og det svar skal være det
/// samme hver gang — en model, der gætter på en periode, kan gætte forkert
/// uden at sige det. Mapper og projekter har navne, der kan slås op. Og de
/// ord, der er tilbage, er netop dem, der skal søges på. Det sker på
/// millisekunder, uden modelfiler, og alt bliver på maskinen.
///
/// ============ SAMME AFGRÆNSNINGER SOM FILTRENE ============
///
/// Alt, hvad filtrene under søgefeltet kan, kan sætningen også: periode,
/// type, mappe, projekt og sprog.
///
/// MAPPER OG PROJEKTER KRÆVER ET STIKORD — «mappen Kunder», «projektet
/// Vagtsom». Deres navne er brugerens egne, og en mappe hedder tit det samme
/// som kunden. Blev «Omada» til en afgrænsning på mappen Omada, forsvandt
/// alle de møder, hvor Omada blev nævnt, men som ligger et andet sted. Et ord
/// må kun holde op med at blive søgt på, når det er helt klart, at det ikke
/// var meningen.
///
/// TYPER OG SPROG GENKENDES UDEN. «Opkaldet med Espen», «webinarer om AI» og
/// «engelske møder» siger det selv, og ordene står sjældent i udskriften.
///
/// «MØDET» ER IKKE EN TYPE. Det er det ord, man bruger om alt: «kan du finde
/// mødet» siges også om et opkald. Blev det til en afgrænsning, forsvandt
/// opkaldet fra en søgning, der ledte efter det. Møder vælges i filtret.
///
/// ============ DET, DER STÅR I ANFØRSELSTEGN, RØRES IKKE ============
///
/// «"access review"» betyder: præcis det her. Der fjernes ingenting.
///
/// ============ HELLERE FOR LIDT END FOR MEGET ============
///
/// Er der intet emne tilbage — «webinarer i sidste måned» — søges der på det,
/// der blev skrevet, med perioden, men uden de øvrige afgrænsninger. Et ord,
/// man selv har skrevet, må ikke forsvinde og efterlade en tom søgning.
/// </remarks>
public static class Fritekst
{
    /// <summary>
    /// Ord, der bærer sætningen, men ikke søgningen.
    /// </summary>
    /// <remarks>
    /// Ordene om SAMTALEN er med — «talte», «mødet», «sagde». Man søger i
    /// møder og samtaler; at det var et møde, står ikke i udskriften.
    /// </remarks>
    private static readonly HashSet<string> Fyldord = new(StringComparer.OrdinalIgnoreCase)
    {
        // dansk: smaaord
        "jeg", "du", "vi", "han", "hun", "de", "man", "mig", "dig", "os", "jer", "dem", "ham", "hende",
        "en", "et", "den", "det", "der", "som", "at", "og", "eller", "men",
        "med", "om", "i", "på", "til", "af", "for", "fra", "hos", "ved", "efter", "under",
        "er", "var", "har", "havde", "blev", "bliver", "kan", "kunne", "vil", "ville", "skal", "skulle",
        "noget", "nogen", "nogle", "hvor", "hvad", "hvornår", "hvem", "hvilket", "hvilken",
        "lige", "gerne", "venligst", "tak", "mon", "vist", "egentlig", "sidst", "sidste", "gang",
        "mine", "min", "mit", "dine", "din", "dit",
        // dansk: om at finde
        "find", "finde", "finder", "vis", "vise", "søg", "søge", "søger", "hent", "leder", "lede",
        // dansk: om samtalen
        "møde", "mødet", "møder", "moede", "moedet", "samtale", "samtalen",
        "snak", "snakken", "snakkede", "snakket", "talte", "talt", "tale", "sagde", "sagt",
        "nævnte", "nævnt", "drøftede", "drøftet", "diskuterede", "diskuteret", "ringede", "ringet",
        // engelsk
        "we", "he", "she", "they", "me", "us", "them", "him", "her", "you", "my",
        "a", "an", "the", "with", "about", "in", "on", "at", "to", "of", "from", "and", "or",
        "was", "were", "had", "have", "did", "can", "could", "would", "will", "please",
        "what", "when", "where", "who", "which", "last", "time",
        "find", "show", "search", "meeting", "talked", "spoke", "discussed", "mentioned", "said",
    };

    /// <summary>Ord, der siger, hvad slags optagelse der ledes efter.</summary>
    private static readonly Dictionary<string, Soegetype> Typeord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["opkald"] = Soegetype.Opkald, ["opkaldet"] = Soegetype.Opkald, ["opkaldene"] = Soegetype.Opkald,
        ["telefonopkald"] = Soegetype.Opkald, ["telefonopkaldet"] = Soegetype.Opkald,
        ["telefonsamtale"] = Soegetype.Opkald, ["telefonsamtalen"] = Soegetype.Opkald,
        ["telefonen"] = Soegetype.Opkald, ["call"] = Soegetype.Opkald, ["calls"] = Soegetype.Opkald,
        ["phone"] = Soegetype.Opkald,

        ["webinar"] = Soegetype.Webinar, ["webinaret"] = Soegetype.Webinar,
        ["webinarer"] = Soegetype.Webinar, ["webinarerne"] = Soegetype.Webinar,
        ["webinars"] = Soegetype.Webinar,

        ["note"] = Soegetype.Note, ["noten"] = Soegetype.Note, ["noter"] = Soegetype.Note,
        ["noterne"] = Soegetype.Note, ["notes"] = Soegetype.Note,
    };

    private static readonly HashSet<string> Mappeord = new(StringComparer.OrdinalIgnoreCase)
        { "mappe", "mappen", "folder", "folderen" };

    private static readonly HashSet<string> Projektord = new(StringComparer.OrdinalIgnoreCase)
        { "projekt", "projektet", "project" };

    /// <summary>Sprogord og deres koder — de samme koder, som står på optagelserne.</summary>
    private static readonly Dictionary<string, string> Sprogord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dansk"] = "da", ["danske"] = "da", ["danish"] = "da",
        ["engelsk"] = "en", ["engelske"] = "en", ["english"] = "en",
        ["norsk"] = "no", ["norske"] = "no", ["norwegian"] = "no",
        ["svensk"] = "sv", ["svenske"] = "sv", ["swedish"] = "sv",
        ["tysk"] = "de", ["tyske"] = "de", ["german"] = "de",
        ["fransk"] = "fr", ["franske"] = "fr", ["french"] = "fr",
        ["spansk"] = "es", ["spanske"] = "es", ["spanish"] = "es",
    };

    /// <summary>Sproget på dansk, som det står i linjen over fundene.</summary>
    public static string Sprognavn(string kode) => kode switch
    {
        "da" => "dansk", "en" => "engelsk", "no" => "norsk", "sv" => "svensk",
        "de" => "tysk", "fr" => "fransk", "es" => "spansk", _ => kode
    };

    private static readonly string[] Maaneder =
    {
        "januar", "februar", "marts", "april", "maj", "juni",
        "juli", "august", "september", "oktober", "november", "december"
    };

    private static readonly string[] MonthsEn =
    {
        "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december"
    };

    private static readonly string[] Ugedage =
    {
        "mandags", "tirsdags", "onsdags", "torsdags", "fredags", "lørdags", "søndags"
    };

    private static readonly Dictionary<string, int> Tal = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = 1, ["et"] = 1, ["to"] = 2, ["tre"] = 3, ["fire"] = 4, ["fem"] = 5,
        ["seks"] = 6, ["syv"] = 7, ["otte"] = 8, ["ni"] = 9, ["ti"] = 10,
        ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
    };

    private static readonly CultureInfo Dansk = new("da-DK");

    public static Fortolkning Fortolk(string tekst) => Fortolk(tekst, DateTime.Now, Kendtenavne.Ingen);

    /// <param name="nu">Tidspunktet, perioden regnes fra. Sat i prøverne.</param>
    /// <param name="kendte">Mapper og projekter, der kan afgrænses på.</param>
    public static Fortolkning Fortolk(string tekst, DateTime nu, Kendtenavne? kendte = null)
    {
        kendte ??= Kendtenavne.Ingen;

        var raa = tekst.Trim();

        if (raa.Length == 0 || raa.Contains('"'))
            return new Fortolkning(raa, null, null, null, false);

        // Ordene, som de blev skrevet - «Omada» med stort - og en ren udgave
        // at sammenligne paa.
        var ord = raa.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                     .Select(o => o.Trim(',', '.', '?', '!', ':', ';', '«', '»'))
                     .Where(o => o.Length > 0)
                     .ToList();

        var smaa = ord.Select(o => o.ToLowerInvariant()).ToList();

        var (fra, til, periode, tid) = Periode(smaa, nu.Date);

        // ============ AFGRAENSNINGERNE ============
        //
        // Hvert ord kan kun bruges én gang. Perioden tager sine foerst; saa
        // kan «august» ikke ogsaa blive et projekt, der hedder August.
        var brugt = new HashSet<int>(tid);

        var mappe = EfterStikord(smaa, brugt, Mappeord, kendte.Mapper);

        var projektnavn = EfterStikord(smaa, brugt, Projektord, kendte.Projekter.Select(p => p.Navn).ToList());
        var projekt = projektnavn is null ? null
            : kendte.Projekter.First(p => p.Navn.Equals(projektnavn, StringComparison.CurrentCultureIgnoreCase)).Id;

        Soegetype? type = null;
        string? sprog = null;

        for (var i = 0; i < smaa.Count; i++)
        {
            if (brugt.Contains(i)) continue;

            if (type is null && Typeord.TryGetValue(smaa[i], out var t))
            {
                type = t;
                brugt.Add(i);
            }
            else if (sprog is null && Sprogord.TryGetValue(smaa[i], out var kode))
            {
                sprog = kode;
                brugt.Add(i);
            }
        }

        var tilbage = ord.Where((_, i) => !brugt.Contains(i) && !Fyldord.Contains(smaa[i])).ToList();

        // ============ INTET EMNE TILBAGE ============
        //
        // Der soeges paa det, der blev skrevet - med perioden, men uden de
        // oevrige afgraensninger. Ellers ville «webinarer i sidste maaned»
        // blive en soegning paa ingenting.
        if (tilbage.Count == 0)
        {
            tilbage = ord.Where((_, i) => !tid.Contains(i)).ToList();
            if (tilbage.Count == 0) tilbage = ord;

            mappe = projekt = projektnavn = sprog = null;
            type = null;
        }

        var soegeord = string.Join(' ', tilbage);

        var aendret = periode is not null || mappe is not null || type is not null
                      || projekt is not null || sprog is not null
                      || !soegeord.Equals(raa, StringComparison.Ordinal);

        return new Fortolkning(soegeord, fra, til, periode, aendret,
                               mappe, type, projekt, projektnavn, sprog);
    }

    /// <summary>
    /// «mappen Kunder», «projektet Vagtsom IAM»: et stikord efterfulgt af et
    /// kendt navn. Det længste navn vinder, så «Vagtsom IAM» slår «Vagtsom».
    /// </summary>
    private static string? EfterStikord(List<string> smaa, HashSet<int> brugt,
                                        HashSet<string> stikord, IReadOnlyList<string> navne)
    {
        for (var i = 0; i < smaa.Count - 1; i++)
        {
            if (brugt.Contains(i) || !stikord.Contains(smaa[i])) continue;

            if (Laengst(smaa, brugt, i + 1, navne) is not { } fund) continue;

            brugt.Add(i);
            for (var k = 0; k < fund.Laengde; k++) brugt.Add(i + 1 + k);

            return fund.Navn;
        }

        return null;
    }

    /// <summary>Det længste kendte navn, der står ordret fra position <paramref name="fra"/>.</summary>
    private static (string Navn, int Laengde)? Laengst(List<string> smaa, HashSet<int> brugt,
                                                       int fra, IReadOnlyList<string> navne)
    {
        (string Navn, int Laengde)? bedst = null;

        foreach (var navn in navne)
        {
            var dele = navn.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (dele.Length == 0 || fra + dele.Length > smaa.Count) continue;

            var passer = true;
            for (var k = 0; k < dele.Length && passer; k++)
                passer = !brugt.Contains(fra + k) && smaa[fra + k] == dele[k];

            if (passer && (bedst is null || dele.Length > bedst.Value.Laengde))
                bedst = (navn, dele.Length);
        }

        return bedst;
    }

    /// <summary>
    /// Finder en tidsangivelse i sætningen og siger, hvilke ord den brugte.
    /// </summary>
    private static (DateTimeOffset? Fra, DateTimeOffset? Til, string? Navn, HashSet<int> Brugt) Periode(
        List<string> o, DateTime idag)
    {
        var mandag = idag.AddDays(-(((int)idag.DayOfWeek + 6) % 7));

        for (var i = 0; i < o.Count; i++)
        {
            string At(int k) => i + k < o.Count ? o[i + k] : "";

            // «i» foran er valgfrit - «sidste måned» og «i sidste måned» er det samme.
            var foran = o[i] is "i" or "in" ? 1 : 0;
            var a = At(foran);
            var b = At(foran + 1);

            HashSet<int> Ord(int antal) => Enumerable.Range(i, foran + antal).ToHashSet();

            // ---- dage
            if (a is "dag" && foran == 1 || a is "idag" or "today")
                return Dag(idag, "i dag", Ord(1));

            if (a is "går" && foran == 1 || a is "igår" or "yesterday")
                return Dag(idag.AddDays(-1), "i går", Ord(1));

            if (a is "forgårs" && foran == 1)
                return Dag(idag.AddDays(-2), "i forgårs", Ord(1));

            // ---- i mandags ... i soendags
            if (foran == 1 && Array.IndexOf(Ugedage, a) is var u and >= 0)
            {
                var dag = mandag.AddDays(u);
                if (dag >= idag) dag = dag.AddDays(-7);

                return Dag(dag, dag.ToString("dddd dd-MM", Dansk), Ord(1));
            }

            // ---- uge, maaned, aar
            if (a is "denne" or "this" && b is "uge" or "week")
                return (Soegefilter.Lokal(mandag), null, "denne uge", Ord(2));

            if (a is "sidste" or "forrige" or "last" && b is "uge" or "week")
                return Uge(mandag.AddDays(-7), Ord(2));

            if (a is "denne" or "this" && b is "måned" or "month")
                return (Soegefilter.Lokal(new DateTime(idag.Year, idag.Month, 1)), null, "denne måned", Ord(2));

            if (a is "sidste" or "forrige" or "last" && b is "måned" or "month")
                return Maaned(new DateTime(idag.Year, idag.Month, 1).AddMonths(-1), Ord(2));

            if (a is "år" && foran == 1 || a is "dette" or "this" && b is "år" or "year")
                return (Soegefilter.Lokal(new DateTime(idag.Year, 1, 1)), null, "i år",
                        a is "år" ? Ord(1) : Ord(2));

            if (a is "sidste" or "forrige" or "last" && b is "år" or "year" || a is "fjor" && foran == 1)
            {
                var y = idag.Year - 1;
                return (Soegefilter.Lokal(new DateTime(y, 1, 1)),
                        Soegefilter.SlutAfDagen(new DateTime(y, 12, 31)),
                        y.ToString(CultureInfo.InvariantCulture), a is "fjor" ? Ord(1) : Ord(2));
            }

            // ---- for N dage/uger/maaneder siden
            if (o[i] is "for" && (Tal.TryGetValue(At(1), out var n) || int.TryParse(At(1), out n))
                && At(3) is "siden")
            {
                var fire = Enumerable.Range(i, 4).ToHashSet();
                var enhed = At(2);

                if (enhed is "dag" or "dage")
                    return Dag(idag.AddDays(-n), idag.AddDays(-n).ToString("dddd dd-MM", Dansk), fire);

                if (enhed is "uge" or "uger")
                    return Uge(mandag.AddDays(-7 * n), fire);

                if (enhed is "måned" or "måneder")
                    return Maaned(new DateTime(idag.Year, idag.Month, 1).AddMonths(-n), fire);
            }

            // ---- i august / i august 2025
            if (foran == 1)
            {
                var m = Array.IndexOf(Maaneder, a);
                if (m < 0) m = Array.IndexOf(MonthsEn, a);

                if (m >= 0)
                {
                    var aarstal = int.TryParse(b, out var y) && y is > 1990 and < 2200;

                    // Uden aarstal er det den seneste af den maaned: «i
                    // november» sagt i september er sidste aars november.
                    var aar = aarstal ? y : m + 1 > idag.Month ? idag.Year - 1 : idag.Year;

                    return Maaned(new DateTime(aar, m + 1, 1), aarstal ? Ord(2) : Ord(1));
                }
            }
        }

        return (null, null, null, new HashSet<int>());
    }

    private static (DateTimeOffset?, DateTimeOffset?, string?, HashSet<int>) Dag(DateTime d, string navn, HashSet<int> brugt) =>
        (Soegefilter.Lokal(d), Soegefilter.SlutAfDagen(d), navn, brugt);

    private static (DateTimeOffset?, DateTimeOffset?, string?, HashSet<int>) Uge(DateTime mandag, HashSet<int> brugt) =>
        (Soegefilter.Lokal(mandag), Soegefilter.SlutAfDagen(mandag.AddDays(6)),
         $"uge {ISOWeek.GetWeekOfYear(mandag)}", brugt);

    private static (DateTimeOffset?, DateTimeOffset?, string?, HashSet<int>) Maaned(DateTime foerste, HashSet<int> brugt) =>
        (Soegefilter.Lokal(foerste), Soegefilter.SlutAfDagen(foerste.AddMonths(1).AddDays(-1)),
         $"{Maaneder[foerste.Month - 1]} {foerste.Year}", brugt);
}
