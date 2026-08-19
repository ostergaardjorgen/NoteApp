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
/// Ét fund: hvor det står, hvad der står, og hvordan man kommer derhen.
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
    string Uddrag,
    int AntalIAlt);

/// <summary>
/// Søgning på tværs af alle møder og dokumenter.
///
/// HVORFOR DEN FINDES
///
/// «Hvornår talte vi sidst om Kernesys, og hvad blev der sagt?» Uden en
/// søgning er svaret at åbne møder ét ad gangen og læse. Efter tredive møder
/// er det ikke længere noget, nogen gør — og så er arkivet reelt tabt.
///
/// Værdien vokser med arkivet, og det gør den til den funktion, der bliver
/// mere værd, jo længere appen har været i brug.
///
/// HVORFOR DER IKKE ER ET INDEKS
///
/// Der scannes direkte i filerne, hver gang. Det er et bevidst valg.
///
/// En times møde giver omkring 50 KB tekst. Hundrede møder er 5 MB — det
/// læses på et øjeblik fra en SSD. Et indeks ville spare millisekunder og
/// koste noget langt dyrere: en kopi, der kan blive uenig med virkeligheden.
/// Retter man en udskrift i hånden, sletter man et møde uden om appen, eller
/// går en skrivning galt, så er indekset forkert — og en søgning, der ikke
/// finder noget, man VED er der, er værre end ingen søgning.
///
/// Bliver det for langsomt en dag, er det tidsnok at bygge indekset da. Så
/// ved vi også, hvor grænsen faktisk går, i stedet for at gætte den nu.
///
/// ALT SKER LOKALT. Søgningen læser filer på maskinen og sender intet.
/// </summary>
public static class Soegning
{
    /// <summary>Så mange tegn vises omkring et match.</summary>
    private const int Omkring = 90;

    /// <summary>
    /// Finder alle steder, ordene optræder.
    ///
    /// Flere ord betyder, at de ALLE skal stå i den samme tekst — ikke
    /// nødvendigvis ved siden af hinanden. Det er den opførsel, folk kender
    /// fra en søgeboks, og den er til at forudsige. Uddraget vises omkring
    /// det sjældneste af ordene — se <see cref="Tilfoej"/>.
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

            // Beskrivelsen er brugerens egne ord om dokumentet, og de er tit
            // dem, man husker. Den søges med.
            Tilfoej(fund, Fundtype.Dokument, d.Id, d.Title, d.Created,
                    d.Markdown + "\n" + d.Description, ord);
        }

        return fund.OrderByDescending(f => f.Tid).ToList();
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

        var flettet = filer.FirstOrDefault(f =>
            Path.GetFileName(f).StartsWith("samtale_", StringComparison.OrdinalIgnoreCase));

        return flettet ?? filer.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
    }

    private static IEnumerable<string> Moedemapper()
    {
        foreach (var rod in new[] { UserDataPaths.Meetings })
        {
            if (!Directory.Exists(rod)) continue;

            foreach (var m in Directory.EnumerateDirectories(rod))
                yield return m;
        }
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

        // UDDRAGET VISES OMKRING DET SJAELDNESTE ORD.
        //
        // Foerst blev det vist omkring det foerste ord, og det gav den
        // forkerte linje: soeger man «Alexander Finsburg», staar «Alexander»
        // otte steder og «Finsburg» ét. Uddraget landede paa en tilfaeldig
        // omtale af Alexander frem for paa den ene saetning, hvor manden
        // praesenterer sig selv.
        //
        // Det sjaeldneste ord er naesten altid det, man soegte PAA - det
        // almindelige ord stod der bare for at snaevre ind.
        var bedst = ord.OrderBy(o => Antal(tekst, o)).First();

        var hvor = tekst.IndexOf(bedst, StringComparison.OrdinalIgnoreCase);
        if (hvor < 0) return;

        fund.Add(new Fund(slags, kilde, overskrift, tid,
                          Uddrag(tekst, hvor, bedst.Length), Antal(tekst, bedst)));
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
    /// Teksten omkring et match, på én linje.
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
