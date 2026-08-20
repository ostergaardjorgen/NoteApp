using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>En påstand i en genereret tekst, der ikke kan findes i kilden.</summary>
public sealed record Ubelagt(
    /// <summary>Det, der står i teksten — «seks kunder», «11 år», «Nomada».</summary>
    string Tekst,
    /// <summary>Hvad det er: et tal eller et navn. Afgør, hvor alvorligt det er.</summary>
    string Slags,
    int Position);

/// <summary>
/// Efterprøver en genereret tekst mod den udskrift, den skulle bygge på.
///
/// HVORFOR DEN FINDES
///
/// Målt 20-08-2026 på et rigtigt møde skrev den lokale model Qwen3-4B «allerede
/// seks kunder i Danmark». Ordene «seks kunder» står ikke ét sted i udskriften.
/// Gemma-3-4B skrev «etableret i Norge i 11-11 år»; «11 år» står der heller
/// ikke. Begge dele læses som fakta.
///
/// Det er ikke et skønhedsproblem, og det bliver ikke løst af en bedre prompt —
/// begge modeller havde allerede fået at vide, at de aldrig måtte skrive et tal,
/// der ikke stod i udskriften. De gjorde det alligevel.
///
/// Men det KAN efterprøves. Kilden ligger på maskinen, og et opslag tager
/// millisekunder. Det, der ikke kan findes, bliver markeret — så forskellen er
/// ikke længere «modellen finder på ting», men «modellen finder på ting, og
/// appen fanger det».
///
/// DEN FJERNER IKKE NOGET AF SIG SELV
///
/// En påstand kan være rigtig, selvom ordene står anderledes: modellen kan
/// skrive «seks» om noget, der blev sagt som «et halvt dusin». At slette den
/// ville være at rette en fejl, der måske ikke er der. Den bliver markeret, og
/// et menneske afgør.
/// </summary>
public static class Efterproevning
{
    /// <summary>
    /// Talord skrevet med bogstaver, med deres ciffer. Både dansk og norsk:
    /// møder holdes på begge, og modellen svarer på dansk om et norsk møde.
    ///
    /// «En» og «et» står IKKE på listen. De er ubestemte artikler før de er
    /// tal — «en kunde» betyder ikke «1 kunde», og hvert eneste «en» i teksten
    /// ville ellers blive slået op.
    /// </summary>
    private static readonly Dictionary<string, string> Talord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["to"] = "2", ["tre"] = "3", ["fire"] = "4", ["fem"] = "5", ["seks"] = "6",
        ["syv"] = "7", ["otte"] = "8", ["åtte"] = "8", ["ni"] = "9", ["ti"] = "10",
        ["elleve"] = "11", ["tolv"] = "12", ["tretten"] = "13", ["fjorten"] = "14",
        ["femten"] = "15", ["seksten"] = "16", ["tyve"] = "20", ["tredive"] = "30",
        ["fyrre"] = "40", ["halvtreds"] = "50", ["tres"] = "60", ["hundrede"] = "100"
    };

    /// <summary>
    /// Ord med stort, der ikke er navne. Uden dem ville hver sætnings første
    /// ord og hvert punkt i en punktopstilling blive slået op som et navn.
    /// </summary>
    private static readonly HashSet<string> IkkeNavne = new(StringComparer.OrdinalIgnoreCase)
    {
        "Der", "Det", "De", "Den", "Deltagere", "Deltagerne", "Mødet", "Moedet",
        "Her", "Hvad", "Hvordan", "Hvem", "Han", "Hun", "Vi", "Du", "Jeg", "Man",
        "Selskabet", "Firmaet", "Virksomheden", "Kunden", "Kunderne", "Aftalen",
        "Introduktion", "Partnerskaber", "Fremtidsplaner", "Strategisk", "Kundefokus",
        "Danmark", "Norge", "Sverige", "Norden", "Europa", "Danske", "Norske",
        "Januar", "Februar", "Marts", "April", "Maj", "Juni", "Juli", "August",
        "September", "Oktober", "November", "December",
        "Mandag", "Tirsdag", "Onsdag", "Torsdag", "Fredag", "Lørdag", "Søndag",
        "Og", "Men", "Så", "Der", "Efter", "Ved", "Til", "For", "Med", "Uden",
        "Alle", "Andre", "Begge", "Både", "Ingen"
    };

    // Et tal - i cifre eller bogstaver - efterfulgt af det, det taeller.
    //
    // ENHEDEN MAA VAERE PAA TO BOGSTAVER.
    //
    // Foerst stod der mindst tre, og saa slap «11-11 år» igennem: «år» er to
    // bogstaver. Det er netop de korte, der baerer tallene - aar, kr, uge, dag,
    // pct. En graense, der lukker enhederne ude, efterproever ikke tal.
    private static readonly Regex Talpar = new(
        @"\b(\d{1,4}|" + @"to|tre|fire|fem|seks|syv|otte|åtte|ni|ti|elleve|tolv|tretten|fjorten|femten|seksten|tyve|tredive|fyrre|halvtreds|tres|hundrede" + @")[\s-]+([a-zæøåA-ZÆØÅ]{2,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Ord med stort midt i en saetning. Ogsaa sammensatte navne: «Beyond Trust».
    private static readonly Regex Navn = new(
        @"\b([A-ZÆØÅ][a-zæøå]{2,}(?:\s+[A-ZÆØÅ][a-zæøå]{2,})?)",
        RegexOptions.Compiled);

    /// <summary>
    /// Finder de påstande i <paramref name="tekst"/>, der ikke kan genfindes i
    /// <paramref name="kilde"/>.
    /// </summary>
    public static List<Ubelagt> Find(string tekst, string kilde)
    {
        var fundne = new List<Ubelagt>();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ---------- tal med det, de tæller
        foreach (Match m in Talpar.Matches(tekst))
        {
            var tal = m.Groups[1].Value;
            var ord = m.Groups[2].Value;
            var par = $"{tal} {ord}";

            if (!set.Add(par)) continue;

            // Baade «seks kunder» og «6 kunder» taeller som belaeg. Modellen
            // skriver tit tallet med bogstaver, hvor udskriften har cifret.
            if (HarBelaeg(kilde, tal, ord)) continue;

            fundne.Add(new Ubelagt(m.Value.Trim(), "tal", m.Index));
        }

        // ---------- navne
        foreach (Match m in Navn.Matches(tekst))
        {
            var navn = m.Value.Trim();

            // Foerste ord i teksten eller efter et punktum staar med stort af
            // grammatiske grunde. Kun ord med stort MIDT i noget er kandidater.
            if (m.Index == 0) continue;
            if (ErSaetningsstart(tekst, m.Index)) continue;

            var foerste = navn.Split(' ')[0];
            if (IkkeNavne.Contains(foerste) || IkkeNavne.Contains(navn)) continue;

            if (!set.Add(navn)) continue;
            if (kilde.Contains(navn, StringComparison.OrdinalIgnoreCase)) continue;

            // Et sammensat navn taeller som belagt, hvis foerste del findes -
            // «Beyond Trust» mod «Beyond Trust Software».
            if (navn.Contains(' ') && kilde.Contains(foerste, StringComparison.OrdinalIgnoreCase)) continue;

            fundne.Add(new Ubelagt(navn, "navn", m.Index));
        }

        return fundne.OrderBy(f => f.Position).ToList();
    }

    private static bool HarBelaeg(string kilde, string tal, string ord)
    {
        if (kilde.Contains($"{tal} {ord}", StringComparison.OrdinalIgnoreCase)) return true;

        // Talordet skrevet som ciffer, og omvendt.
        if (Talord.TryGetValue(tal, out var ciffer)
            && kilde.Contains($"{ciffer} {ord}", StringComparison.OrdinalIgnoreCase)) return true;

        foreach (var (bogstaver, c) in Talord)
            if (c == tal && kilde.Contains($"{bogstaver} {ord}", StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>
    /// Står ordet i begyndelsen af en sætning, et punkt eller en linje?
    ///
    /// Dansk skriver kun navne med stort, men det gør et punktum også — og en
    /// punktopstilling begynder hver linje med stort. Uden det her ville hvert
    /// punkt i opsummeringen blive slået op som et navn.
    /// </summary>
    private static bool ErSaetningsstart(string tekst, int i)
    {
        for (var j = i - 1; j >= 0; j--)
        {
            var c = tekst[j];

            if (c is ' ' or '\t' or '*' or '-' or '•' or '#' or '_') continue;
            if (c is '\n' or '\r') return true;

            return c is '.' or ':' or '!' or '?';
        }

        return true;
    }
}
