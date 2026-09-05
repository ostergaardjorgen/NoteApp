using System.Globalization;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>En dato fundet i talt sprog — og det stykke tekst, den kom af.</summary>
/// <param name="Dato">Dagen. Klokkeslæt er altid midnat: en frist er en dag.</param>
/// <param name="Ordene">Det, der stod i teksten. Vises, så gættet kan efterprøves.</param>
/// <param name="Sikker">
/// Er datoen entydig?
///
/// «på fredag» er sikker — der er kun én næste fredag. «i næste uge» er ikke:
/// den peger på syv dage, og der er valgt mandag. Forskellen skal kunne ses,
/// for en frist, appen har gættet, må aldrig se ud som en, nogen har sagt.
/// </param>
public sealed record Fundendato(DateOnly Dato, string Ordene, bool Sikker);

/// <summary>
/// Datoer i dansk talesprog.
///
/// HVORFOR DEN ER SKREVET I HÅNDEN
///
/// Microsoft.Recognizers.Text kan det her på fjorten sprog. Dansk er ikke et
/// af dem — efterprøvet 21-08-2026. Der findes ikke et bibliotek at hente,
/// og et engelsk et, der får dansk ind, svarer forkert frem for at svare
/// «ved ikke».
///
/// HVAD DEN KAN, OG HVAD DEN IKKE KAN
///
/// Den forstår det, folk faktisk siger i et møde: «på fredag», «i morgen»,
/// «om to uger», «den 15.», «1. september», «i næste uge», «inden månedens
/// udgang». Den forstår IKKE «når Anders er tilbage fra ferie» eller «efter
/// sommerferien» — det er ikke datoer, det er begivenheder.
///
/// DEN GÆTTER IKKE, NÅR DEN ER I TVIVL. Et svar er enten en dato eller ingen
/// ting. En frist, der er sat forkert, opdages den dag, den er overskredet —
/// og dér er den værre end ingen frist, fordi den blev troet.
///
/// UDSKRIFTEN ER LAVET AF EN MASKINE. Der kan stå «på fredag» som «på freddag»
/// eller «om to uger» som «om 2 uger». Reglerne er derfor skrevet tolerant på
/// tal og stavning, hvor det kan gøres uden at gætte.
/// </summary>
public static class Datoforstaaelse
{
    private static readonly string[] Ugedage =
        { "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag", "søndag" };

    private static readonly string[] Maaneder =
    {
        "januar", "februar", "marts", "april", "maj", "juni",
        "juli", "august", "september", "oktober", "november", "december"
    };

    /// <summary>
    /// Tal skrevet med bogstaver. Whisper skriver dem oftere end cifre —
    /// «om to uger», ikke «om 2 uger».
    /// </summary>
    private static readonly Dictionary<string, int> Talord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = 1, ["et"] = 1, ["én"] = 1, ["to"] = 2, ["tre"] = 3, ["fire"] = 4,
        ["fem"] = 5, ["seks"] = 6, ["syv"] = 7, ["otte"] = 8, ["ni"] = 9, ["ti"] = 10,
        ["elleve"] = 11, ["tolv"] = 12, ["fjorten"] = 14, ["tyve"] = 20, ["tredive"] = 30
    };

    /// <summary>
    /// Finder den første dato i teksten. Null når der ikke er nogen.
    /// </summary>
    /// <param name="idag">
    /// Dagen, der regnes fra. Gives med frem for at blive slået op, så
    /// målingen kan stille det samme spørgsmål hver dag og få det samme svar.
    /// </param>
    public static Fundendato? Find(string tekst, DateOnly idag)
    {
        if (string.IsNullOrWhiteSpace(tekst)) return null;

        var t = tekst.ToLowerInvariant();

        return IMorgen(t, idag)
               ?? Overmorgen(t, idag)
               ?? OmEnPeriode(t, idag)
               ?? Ugedagen(t, idag)
               ?? Ugen(t, idag)
               ?? Maanedsslut(t, idag)
               ?? DatoMedMaaned(t, idag)
               ?? DenXte(t, idag);
    }

    // ------------------------------------------------------------ i morgen

    private static Fundendato? IMorgen(string t, DateOnly idag) =>
        Regex.IsMatch(t, @"\bi\s*morgen\b")
            ? new Fundendato(idag.AddDays(1), "i morgen", true)
            : null;

    private static Fundendato? Overmorgen(string t, DateOnly idag) =>
        Regex.IsMatch(t, @"\bi?\s*overmorgen\b")
            ? new Fundendato(idag.AddDays(2), "i overmorgen", true)
            : null;

    // --------------------------------------------------------- om N dage

    /// <summary>
    /// «om to uger», «om 3 dage», «om en måned».
    ///
    /// EN UGE ER SYV DAGE, IKKE «NÆSTE UGE». Siger nogen «om en uge» på en
    /// onsdag, mener de onsdag i næste uge — ikke mandag. Det er den slags,
    /// der giver en frist to dage for tidligt uden at nogen opdager hvorfor.
    /// </summary>
    private static Fundendato? OmEnPeriode(string t, DateOnly idag)
    {
        // «måned(er)» og ikke «måneder?». Det sidste kræver «månede» og
        // rammer derfor ikke «om en måned» — overset af målingen 21-08-2026.
        var m = Regex.Match(t, @"\bom\s+(\d+|[a-zæøå]+)\s+(dage?|uger?|måned(?:er)?|maaned(?:er)?)\b");
        if (!m.Success) return null;

        var antal = Tal(m.Groups[1].Value);
        if (antal is null) return null;

        var enhed = m.Groups[2].Value;

        var dato = enhed.StartsWith("dag", StringComparison.Ordinal)
            ? idag.AddDays(antal.Value)
            : enhed.StartsWith("uge", StringComparison.Ordinal)
                ? idag.AddDays(antal.Value * 7)
                : idag.AddMonths(antal.Value);

        return new Fundendato(dato, m.Value.Trim(), true);
    }

    private static int? Tal(string s)
    {
        if (int.TryParse(s, out var n)) return n is > 0 and <= 365 ? n : null;
        return Talord.TryGetValue(s, out var v) ? v : null;
    }

    // ---------------------------------------------------------- på fredag

    /// <summary>
    /// «på fredag», «næste tirsdag», «på mandag».
    ///
    /// DEN NÆSTE AF DEN UGEDAG, OG ALDRIG I DAG. Siger nogen «på fredag» en
    /// fredag, mener de om syv dage — ikke om nul.
    ///
    /// «næste» lægger en uge til, men KUN når der ellers ville være mindre end
    /// en uge til. «næste fredag» sagt en mandag er den kommende fredag for de
    /// fleste; sagt en torsdag er det ugen efter. Det er tvetydigt i sproget
    /// selv, og derfor er svaret mærket som usikkert.
    /// </summary>
    private static Fundendato? Ugedagen(string t, DateOnly idag)
    {
        var m = Regex.Match(t, @"\b(på|næste|kommende|nu på)\s+(" + string.Join('|', Ugedage) + @")\b");
        if (!m.Success) return null;

        var nr = Array.IndexOf(Ugedage, m.Groups[2].Value);
        if (nr < 0) return null;

        // Mandag = 0 i vores liste, men DayOfWeek har soendag = 0.
        var idagNr = ((int)idag.DayOfWeek + 6) % 7;
        var frem = (nr - idagNr + 7) % 7;
        if (frem == 0) frem = 7;

        var naeste = m.Groups[1].Value is "næste";
        var dato = idag.AddDays(frem);

        if (naeste && frem < 7) dato = dato.AddDays(7);

        return new Fundendato(dato, m.Value.Trim(), !naeste);
    }

    // ------------------------------------------------------------ i næste uge

    /// <summary>
    /// «i næste uge», «i denne uge».
    ///
    /// DER PEGES PÅ SYV DAGE, IKKE PÅ ÉN. Der vælges mandag, og svaret er
    /// mærket USIKKERT — det er et gæt inden for den uge, der blev nævnt, og
    /// det skal fremgå på skærmen.
    /// </summary>
    private static Fundendato? Ugen(string t, DateOnly idag)
    {
        var m = Regex.Match(t, @"\bi\s+(næste|denne|kommende)\s+uge\b");
        if (!m.Success) return null;

        var mandag = idag.AddDays(-(((int)idag.DayOfWeek + 6) % 7));
        var dato = m.Groups[1].Value is "denne" ? mandag : mandag.AddDays(7);

        return new Fundendato(dato, m.Value.Trim(), false);
    }

    // ------------------------------------------------- inden månedens udgang

    private static Fundendato? Maanedsslut(string t, DateOnly idag)
    {
        if (!Regex.IsMatch(t, @"\b(inden|før|ved)\s+(månedens|maanedens)\s+(udgang|slutning|udløb)\b")
            && !Regex.IsMatch(t, @"\bsidst\s+på\s+måneden\b"))
            return null;

        var sidste = DateTime.DaysInMonth(idag.Year, idag.Month);
        return new Fundendato(new DateOnly(idag.Year, idag.Month, sidste), "inden månedens udgang", true);
    }

    // ---------------------------------------------------- den 1. september

    /// <summary>
    /// «den 1. september», «15. marts», «1/9».
    ///
    /// ÅRET GÆTTES FREM. Er datoen passeret i år, er det næste år, der menes —
    /// en frist ligger aldrig bagud. Det er den eneste antagelse, klassen
    /// gør, og den er rigtig, fordi det her er FRISTER og ikke historik.
    /// </summary>
    private static Fundendato? DatoMedMaaned(string t, DateOnly idag)
    {
        var m = Regex.Match(t, @"\b(?:den\s+)?(\d{1,2})\.?\s*(" + string.Join('|', Maaneder) + @")\b");

        if (!m.Success)
        {
            m = Regex.Match(t, @"\b(\d{1,2})\s*/\s*(\d{1,2})\b");
            if (!m.Success) return null;

            var d = int.Parse(m.Groups[1].Value);
            var maaned = int.Parse(m.Groups[2].Value);

            return Byg(d, maaned, idag, m.Value.Trim());
        }

        var dag = int.Parse(m.Groups[1].Value);
        var nr = Array.IndexOf(Maaneder, m.Groups[2].Value) + 1;

        return Byg(dag, nr, idag, m.Value.Trim());
    }

    private static Fundendato? Byg(int dag, int maaned, DateOnly idag, string ordene)
    {
        if (maaned is < 1 or > 12) return null;
        if (dag < 1 || dag > DateTime.DaysInMonth(idag.Year, maaned)) return null;

        var dato = new DateOnly(idag.Year, maaned, dag);
        if (dato < idag) dato = dato.AddYears(1);

        return new Fundendato(dato, ordene, true);
    }

    // --------------------------------------------------------- den femtende

    /// <summary>
    /// «den 15.» uden måned — altså den 15. i den måned, der kommer først.
    ///
    /// Kun med «den» foran og punktum efter. Uden det ville ethvert tal i en
    /// udskrift blive til en dato, og et møde er fuldt af tal.
    /// </summary>
    private static Fundendato? DenXte(string t, DateOnly idag)
    {
        var m = Regex.Match(t, @"\bden\s+(\d{1,2})\.(?!\d)");
        if (!m.Success) return null;

        var dag = int.Parse(m.Groups[1].Value);
        if (dag is < 1 or > 31) return null;

        var maaned = idag.Month;
        var aar = idag.Year;

        if (dag <= idag.Day || dag > DateTime.DaysInMonth(aar, maaned))
        {
            maaned++;
            if (maaned > 12) { maaned = 1; aar++; }
        }

        if (dag > DateTime.DaysInMonth(aar, maaned)) return null;

        return new Fundendato(new DateOnly(aar, maaned, dag), m.Value.Trim(), true);
    }

    /// <summary>Datoen skrevet, som den skal stå på skærmen.</summary>
    /// <summary>
    /// Månedens tre første bogstaver — «sep», «okt», «maj».
    /// </summary>
    /// <remarks>
    /// MAJ, JUNI OG JULI ER I FORVEJEN KORTE. At skrive «juni» som «jun»
    /// og «maj» som «maj» ser tilfældigt ud, når de står under hinanden.
    /// Måneder på fire bogstaver eller færre står derfor helt.
    /// </remarks>
    public static string Forkort(DateOnly d)
    {
        var navn = d.ToString("MMMM", new CultureInfo("da-DK"));

        return navn.Length <= 4 ? navn : navn[..3];
    }

    public static string Skriv(DateOnly d, DateOnly idag)
    {
        var dage = d.DayNumber - idag.DayNumber;

        return dage switch
        {
            0 => "i dag",
            1 => "i morgen",
            2 => "i overmorgen",
            > 2 and < 7 => d.ToString("dddd", new CultureInfo("da-DK")),

            // MAANEDEN FORKORTES. "16. september" er nitten tegn paa en
            // maerkat i en spalte, der er godt tre hundrede pixels bred -
            // og de fjorten af dem er et maanedsnavn, man kender fra de
            // tre foerste bogstaver.
            //
            // "MMM" ville give "sep." med et punktum fra .NET's egen
            // liste, og punktummet er stoej paa en maerkat. Der klippes
            // derfor selv.
            _ => $"{d.Day}. {Forkort(d)}"
        };
    }
}
