using System.Text;

namespace NoteApp.Core;

/// <summary>Ét ord, der blev rettet.</summary>
public readonly record struct Ordrettelse(string Hoert, string Rigtigt);

/// <summary>
/// Retter ord, der blev hørt næsten rigtigt, til det, der står i ordbogen.
///
/// HVORFOR DET IKKE ER NOK AT SENDE ORDBOGEN MED
///
/// Forhåndsviden hjælper udskriften, men den afgør den ikke. «Kernesys» kan
/// stadig komme tilbage som «Kernesus», og et navn, der er én bogstavfejl fra
/// det rigtige, er lige så forkert som et, der er langt fra — det skal rettes
/// i hånden hver gang.
///
/// Her rettes det bagefter, mod ordbogen, som du selv har fyldt.
///
/// FORSIGTIGHEDEN ER HELE DESIGNET
///
/// En retter, der gætter, ødelægger tekst, man troede var i orden — og den
/// slags opdages ikke, fordi resultatet ser rigtigt ud. Derfor:
///
///   * kun ord på fem tegn og derover. «IAM» og «jam» er én fejl fra
///     hinanden, og den forskel må ikke afgøres af en tærskel.
///   * kun ÉN kandidat. Er to ord i ordbogen lige tæt på, ved vi ikke hvilket,
///     og så bliver der ikke rettet.
///   * aldrig et ord, der allerede står i ordbogen. Det var rigtigt.
///   * flerordstermer rettes ikke — kun deres store bogstaver. «Entra ID»
///     kræver, at man ved, hvor ordet begynder og slutter, og det er en anden
///     opgave.
/// </summary>
public static class Ordretter
{
    /// <summary>Korteste ord, der overhovedet rettes.</summary>
    public const int MindsteLaengde = 5;

    /// <summary>
    /// Hvor mange bogstaver der må være galt, før det ikke længere er samme ord.
    /// </summary>
    /// <remarks>
    /// Ét for korte ord, to for lange. «Nordby» og «Norby» er ét fra hinanden;
    /// «provisionering» og «provitionering» er to — og et ord på fjorten tegn
    /// er stadig genkendeligt med to fejl. Et ord på fem er ikke.
    /// </remarks>
    public static int Taerskel(int laengde) => laengde >= 8 ? 2 : 1;

    /// <summary>
    /// Retter teksten mod ordbogen.
    /// </summary>
    /// <returns>Den rettede tekst og listen over, hvad der blev rettet.</returns>
    public static (string Tekst, IReadOnlyList<Ordrettelse> Rettelser) Ret(
        string tekst, IEnumerable<string> ordbog)
    {
        var rettelser = new List<Ordrettelse>();

        if (string.IsNullOrEmpty(tekst)) return (tekst, rettelser);

        // Kun etordstermer kan rettes. Flerordstermer staar tilbage - se
        // klassens beskrivelse.
        var kandidater = ordbog
            .Select(Ordbibliotek.Rens)
            .Where(o => o.Length >= MindsteLaengde && !o.Contains(' ', StringComparison.Ordinal))
            .ToList();

        if (kandidater.Count == 0) return (tekst, rettelser);

        var kendte = new HashSet<string>(
            ordbog.Select(Ordbibliotek.Rens).Where(o => o.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var ud = new StringBuilder(tekst.Length);
        var i = 0;

        while (i < tekst.Length)
        {
            if (!ErOrdtegn(tekst[i]))
            {
                ud.Append(tekst[i++]);
                continue;
            }

            var start = i;
            while (i < tekst.Length && ErOrdtegn(tekst[i])) i++;

            var ord = tekst[start..i];
            var rettet = Bedste(ord, kandidater, kendte);

            if (rettet is null)
            {
                ud.Append(ord);
            }
            else
            {
                ud.Append(rettet);
                rettelser.Add(new Ordrettelse(ord, rettet));
            }
        }

        return (ud.ToString(), rettelser);
    }

    /// <summary>
    /// Det ord i ordbogen, der utvetydigt er det rigtige — eller <c>null</c>.
    /// </summary>
    private static string? Bedste(string ord, List<string> kandidater, HashSet<string> kendte)
    {
        if (ord.Length < MindsteLaengde) return null;

        // Staar det allerede i ordbogen, var det rigtigt. Kun store bogstaver
        // rettes: siger man «entra», skal der staa «Entra».
        if (kendte.Contains(ord))
        {
            var praecis = kandidater.FirstOrDefault(
                k => k.Equals(ord, StringComparison.OrdinalIgnoreCase));

            return praecis is not null && praecis != ord ? praecis : null;
        }

        // Der sammenlignes paa bogstaverne. «O'Mara» og «Omada» er ét
        // bogstavs forskel; med apostroffen med ville de vaere to.
        var rent = Bogstaver(ord);
        if (rent.Length < MindsteLaengde) return null;

        string? fundet = null;
        var bedste = int.MaxValue;
        var flere = false;

        foreach (var k in kandidater)
        {
            // Et ord, der er meget laengere eller kortere, er ikke det samme.
            // Kontrollen sparer ogsaa den dyre udregning.
            if (Math.Abs(k.Length - rent.Length) > Taerskel(k.Length)) continue;

            var d = Afstand(rent, k, Taerskel(k.Length));
            if (d > Taerskel(k.Length)) continue;

            if (d < bedste) { bedste = d; fundet = k; flere = false; }
            else if (d == bedste && !k.Equals(fundet, StringComparison.OrdinalIgnoreCase)) flere = true;
        }

        // TO LIGE GODE KANDIDATER ER INGEN KANDIDAT. Vaelger vi den foerste,
        // retter vi halvdelen af gangene til det forkerte ord.
        return flere ? null : fundet;
    }

    /// <summary>
    /// Levenshtein-afstand, der giver op, så snart den er over <paramref name="loft"/>.
    /// </summary>
    /// <remarks>
    /// Loftet er ikke pynt. Retteren kører over hvert ord i en udskrift gange
    /// hvert ord i ordbogen, og et diktat skal være klar med det samme.
    /// </remarks>
    public static int Afstand(string a, string b, int loft)
    {
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return 0;

        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var forrige = new int[b.Length + 1];
        var nu = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) forrige[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            nu[0] = i;
            var mindste = nu[0];

            for (var j = 1; j <= b.Length; j++)
            {
                var pris = a[i - 1] == b[j - 1] ? 0 : 1;

                nu[j] = Math.Min(Math.Min(nu[j - 1] + 1, forrige[j] + 1), forrige[j - 1] + pris);
                mindste = Math.Min(mindste, nu[j]);
            }

            // Hele raekken er over loftet: den kan ikke komme ned igen.
            if (mindste > loft) return loft + 1;

            (forrige, nu) = (nu, forrige);
        }

        return forrige[b.Length];
    }

    /// <summary>
    /// Hører tegnet med til et ord?
    /// </summary>
    /// <remarks>
    /// APOSTROFFEN SKAL MED, OG DET KOSTEDE EN RETTELSE, DER ALDRIG SKETE.
    ///
    /// Målt 30-08-2026: brugeren sagde «Omada» og fik «O'Mara». Han lagde
    /// «Omada» i ordbogen, og det hjalp ikke. Grunden var her: apostroffen
    /// talte ikke med i et ord, så «O'Mara» blev delt i «O» og «Mara» — to
    /// stumper på ét og fire tegn, begge under mindstelængden. Retteren så
    /// dem aldrig.
    ///
    /// Med apostroffen indenfor bliver ordet «O'Mara», og sammenligningen
    /// sker på bogstaverne alene: «omara» mod «omada» er ét bogstavs
    /// forskel, og så er det den samme.
    /// </remarks>
    private static bool ErOrdtegn(char c) =>
        char.IsLetterOrDigit(c) || c == '-' || c == '\'' || c == '’';

    /// <summary>
    /// Ordet uden apostroffer — det, der sammenlignes på.
    /// </summary>
    /// <remarks>
    /// Bindestregen bliver STÅENDE. «JIT-adgang» er ét ord med en bindestreg
    /// i, ikke to ord — og fjernede vi den, ville ordbogens egne termer holde
    /// op med at ligne sig selv.
    /// </remarks>
    private static string Bogstaver(string ord) =>
        ord.Replace("'", "", StringComparison.Ordinal)
           .Replace("’", "", StringComparison.Ordinal);
}
