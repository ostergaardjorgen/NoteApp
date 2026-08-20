using System.Text;
using NoteApp.Core.Documents;

namespace NoteApp.Core;

/// <summary>Hvilken slags ting et fund peger på.</summary>
public enum Fundtype
{
    Udskrift,
    Note,
    Dokument
}

/// <summary>
/// Ét sted, ordet står — med teksten omkring og positionen i filen.
/// </summary>
/// <param name="Position">
/// Tegnnummeret i den tekst, der blev søgt i. Det er DEN, der gør, at man kan
/// springe hen til stedet frem for blot at åbne filen og lede selv.
/// </param>
public sealed record Traef(int Position, string Uddrag);

/// <summary>
/// Én kilde, der indeholder det søgte — og hvert sted, det står.
/// </summary>
/// <param name="Kilde">
/// Mødets eller dokumentets id. Det er DEN, der åbner tingen — ikke stien.
/// En sti kan flyttes og en titel omdøbes; id'et kan ikke.
/// </param>
public sealed record Fund(
    Fundtype Slags,
    string Kilde,
    string Overskrift,
    DateTimeOffset Tid,
    IReadOnlyList<Traef> Traef);

/// <summary>
/// Søgning på tværs af alle møder og dokumenter.
///
/// HVORFOR DEN FINDES
///
/// «Hvornår talte vi sidst om Kernesys, og hvad blev der sagt?» Uden en
/// søgning er svaret at åbne møder ét ad gangen og læse. Efter tredive møder
/// er det ikke længere noget, nogen gør — og så er arkivet reelt tabt.
///
/// HVORFOR HVERT STED STÅR FOR SIG
///
/// Første udgave gav ét fund pr. fil og et link til at åbne den. Det var ikke
/// et svar: står ordet tolv steder i en udskrift på en time, er spørgsmålet
/// ikke OM det står der, men i hvilken sammenhæng — og hvilket af de tolv
/// steder man skal læse.
///
/// Nu står hvert sted for sig med teksten omkring og et tegnnummer, så man
/// kan springe direkte derhen.
///
/// HVORFOR DER IKKE ER ET INDEKS
///
/// Der scannes direkte i filerne, hver gang. En times møde giver omkring
/// 50 KB tekst; hundrede møder er 5 MB, og det læses på et øjeblik. Et indeks
/// ville spare millisekunder og koste en kopi, der kan blive uenig med
/// virkeligheden — og en søgning, der ikke finder noget, man VED er der, er
/// værre end ingen søgning.
///
/// ALT SKER LOKALT. Søgningen læser filer på maskinen og sender intet.
/// </summary>
public static class Soegning
{
    /// <summary>Så mange tegn vises omkring et træf.</summary>
    private const int Omkring = 80;

    /// <summary>
    /// Så mange steder tages med pr. kilde.
    ///
    /// Søger man på «og», er der tusind. Listen skal kunne læses, ikke være
    /// udtømmende — og står ordet flere gange end det her, er det ikke det
    /// enkelte sted, man leder efter.
    /// </summary>
    private const int MaksPrKilde = 40;

    /// <summary>
    /// Finder alle steder, ordene optræder.
    ///
    /// Flere ord betyder, at de ALLE skal stå i den samme tekst — ikke
    /// nødvendigvis ved siden af hinanden. Stederne findes på det sjældneste
    /// af ordene: det almindelige ord stod der bare for at snævre ind.
    /// </summary>
    public static List<Fund> Soeg(string spoergsmaal, CancellationToken ct = default)
    {
        var ord = spoergsmaal
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.Trim())
            .Where(o => o.Length > 0)
            .ToArray();

        var fund = new List<Fund>();
        if (ord.Length == 0) return fund;

        foreach (var mappe in Moedemapper())
        {
            ct.ThrowIfCancellationRequested();

            var meta = MeetingStore.Load(mappe);
            var id = meta?.Id.ToString() ?? "";
            var titel = meta?.Title ?? Path.GetFileName(mappe);
            var tid = meta?.StartedAt ?? new DateTimeOffset(Directory.GetLastWriteTime(mappe));

            // Kun ÉN udskrift pr. møde. Et onlinemøde har tre txt-filer — de
            // to spor og fletningen — og uden det her ville hvert eneste ord
            // give tre fund af det samme.
            var udskrift = Udskriften(mappe);
            if (udskrift is not null)
                Tilfoej(fund, Fundtype.Udskrift, id, titel, tid, Laes(udskrift), ord);

            var noter = Path.Combine(mappe, "notes.jsonl");
            if (File.Exists(noter))
                Tilfoej(fund, Fundtype.Note, id, titel, tid, Laes(noter), ord);
        }

        foreach (var d in DocumentStore.LoadAll())
        {
            ct.ThrowIfCancellationRequested();

            // Der søges i selve dokumentet. Positionerne skal passe med den
            // tekst, skærmen viser — ellers springer man det forkerte sted
            // hen. Beskrivelsen er brugerens egne ord og tages med, men uden
            // egne positioner: den står ikke i dokumentteksten.
            var tekst = d.Markdown;

            if (!ord.All(o => tekst.Contains(o, StringComparison.OrdinalIgnoreCase))
                && ord.All(o => (tekst + "\n" + d.Description).Contains(o, StringComparison.OrdinalIgnoreCase)))
                tekst += "\n" + d.Description;

            Tilfoej(fund, Fundtype.Dokument, d.Id, d.Title, d.Created, tekst, ord);
        }

        // Flest steder først. Ét træf er en omtale i forbifarten; tolv er dét,
        // mødet handlede om — og det er som regel den, man leder efter.
        return fund
            .OrderByDescending(f => f.Traef.Count)
            .ThenByDescending(f => f.Tid)
            .ToList();
    }

    /// <summary>
    /// Den udskrift, der gælder for et møde.
    ///
    /// Findes fletningen af de to spor, er det den — den indeholder begge
    /// sider af mødet. Ellers den nyeste txt-fil. Sporfilerne hver for sig
    /// søges IKKE: alt i dem står også i fletningen, og tre fund af den samme
    /// sætning er ikke tre svar.
    /// </summary>
    private static string? Udskriften(string mappe)
    {
        if (!Directory.Exists(mappe)) return null;

        var filer = Directory.GetFiles(mappe, "*.txt")
            .Where(f => !f.EndsWith(".raa.txt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // «udskrift_» er den gengivelse, der gaelder - med talernavne og
        // med de rettelser, der er lavet. «samtale_» er det gamle navn og
        // bliver liggende paa moeder, der er skrevet ud foer.
        var gaeldende = filer.FirstOrDefault(f =>
                            Path.GetFileName(f).StartsWith("udskrift_", StringComparison.OrdinalIgnoreCase))
                        ?? filer.FirstOrDefault(f =>
                            Path.GetFileName(f).StartsWith("samtale_", StringComparison.OrdinalIgnoreCase));

        return gaeldende ?? filer.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
    }

    private static IEnumerable<string> Moedemapper()
    {
        if (!Directory.Exists(UserDataPaths.Meetings)) yield break;

        foreach (var m in Directory.EnumerateDirectories(UserDataPaths.Meetings))
            yield return m;
    }

    private static string Laes(string sti)
    {
        try { return File.ReadAllText(sti, Encoding.UTF8); }
        catch (IOException) { return ""; }
    }

    private static void Tilfoej(List<Fund> fund, Fundtype slags, string kilde, string overskrift,
                                DateTimeOffset tid, string tekst, string[] ord)
    {
        if (tekst.Length == 0 || kilde.Length == 0) return;

        // ALLE ord skal stå der. Ét manglende ord betyder, at teksten ikke er
        // den, der blev spurgt om.
        if (!ord.All(o => tekst.Contains(o, StringComparison.OrdinalIgnoreCase))) return;

        // Stederne findes på det SJÆLDNESTE ord. Søger man «Alexander
        // Finsburg», står «Alexander» otte steder og «Finsburg» ét — og det
        // ene er dét, man ledte efter.
        var bedst = ord.OrderBy(o => Antal(tekst, o)).First();

        var traef = new List<Traef>();
        var i = 0;

        while (traef.Count < MaksPrKilde
               && (i = tekst.IndexOf(bedst, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            traef.Add(new Traef(i, Uddrag(tekst, i, bedst.Length)));
            i += bedst.Length;
        }

        if (traef.Count > 0)
            fund.Add(new Fund(slags, kilde, overskrift, tid, traef));
    }

    private static int Antal(string tekst, string ord)
    {
        var n = 0;
        var i = 0;

        while ((i = tekst.IndexOf(ord, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            n++;
            i += ord.Length;
        }

        return n;
    }

    /// <summary>
    /// Teksten omkring et træf, på én linje.
    ///
    /// Uden uddraget ville et fund kun sige, at ordet står et sted i et møde
    /// på en time. Det er ikke et svar — det er en henvisning til at læse
    /// videre. Uddraget er dét, der gør søgningen brugbar uden at åbne noget.
    /// </summary>
    private static string Uddrag(string tekst, int position, int laengde)
    {
        var fra = Math.Max(0, position - Omkring);
        var til = Math.Min(tekst.Length, position + laengde + Omkring);

        var s = tekst[fra..til].Replace('\r', ' ').Replace('\n', ' ').Trim();

        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);

        return (fra > 0 ? "… " : "") + s + (til < tekst.Length ? " …" : "");
    }
}
