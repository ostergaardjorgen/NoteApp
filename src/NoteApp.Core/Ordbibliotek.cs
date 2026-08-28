using System.Text;

namespace NoteApp.Core;

/// <summary>
/// Ordbogen: navne, fagtermer og forkortelser, som udskriften skal kende.
///
/// DEN ER DET ENESTE, DER SENDES MED LYDEN. Voxtral tager en liste af ord som
/// forhåndsviden, og den liste forlader maskinen ved hver diktering. Derfor er
/// den ikke en skjult hjælpefil, men noget, man skal kunne se, rette i og
/// tage med sig.
///
/// DEN VAR SKJULT, OG DET GIK GALT. Indtil 28-08-2026 lå den som én lang
/// sætning i prosa — «Vi taler om Anders, Malene, …» — og blev læst med
/// ReadAllLines. Det gav ÉN linje på 473 tegn i stedet for en ordliste, så
/// forhåndsviden var i praksis ét langt vrøvlefelt. Værre: der stod et
/// firmanavn i den, som ikke måtte forlade maskinen, og det gjorde det ved
/// hver eneste diktering. Ingen havde set filen i månedsvis.
///
/// Derfor: ÉT ORD PR. LINJE, og den skal kunne vises i brugerfladen.
/// </summary>
public static class Ordbibliotek
{
    /// <summary>Hvor den ligger. Samme fil som før — kun formen er lagt om.</summary>
    public static string Sti => UserDataPaths.Vocabulary;

    /// <summary>
    /// Højeste antal ord, der sendes med.
    /// </summary>
    /// <remarks>
    /// Forhåndsviden er ikke gratis: den tæller med i det, leverandøren gør op,
    /// og en liste, der er for lang, trækker udskriften i retning af ordene
    /// frem for mod det, der faktisk blev sagt. Bliver biblioteket større,
    /// sendes de første — se <see cref="TilAfsendelse"/>.
    /// </remarks>
    public const int MaksSendte = 200;

    /// <summary>Længste ord. Længere end det er en sætning, ikke et opslag.</summary>
    public const int MaksLaengde = 60;

    /// <summary>
    /// Renser ét ord. Tom streng betyder «hører ikke hjemme her».
    /// </summary>
    /// <remarks>
    /// Kommaer og semikolon ryger ud, fordi de er dét, den gamle prosaform
    /// brugte til at skille ord — indsætter man «Anders, Malene», er det to
    /// ord, man mente, ikke ét.
    /// </remarks>
    public static string Rens(string? ord)
    {
        if (ord is null) return "";

        var s = ord.Replace('\t', ' ').Trim().Trim(',', ';', '.', '"', '\'').Trim();

        // Flere mellemrum bliver til eet. «Entra  ID» og «Entra ID» er samme ord.
        while (s.Contains("  ", StringComparison.Ordinal)) s = s.Replace("  ", " ");

        return s.Length is > 0 and <= MaksLaengde ? s : "";
    }

    /// <summary>Læser biblioteket. Findes filen ikke, er det tomt — ikke en fejl.</summary>
    public static List<string> Laes()
    {
        try
        {
            if (!File.Exists(Sti)) return new List<string>();

            return Ryd(File.ReadAllLines(Sti, Encoding.UTF8));
        }
        catch (IOException)
        {
            // En ulaeselig ordbog maa ikke forhindre en diktering. Uden den
            // bliver udskriften en anelse ringere; det er alt.
            return new List<string>();
        }
    }

    /// <summary>
    /// Renser en samling: tomme ud, dubletter ud, rækkefølgen bevaret.
    /// </summary>
    /// <remarks>
    /// Dubletter sammenlignes UDEN hensyn til store bogstaver. «Entra ID» og
    /// «entra id» er det samme opslag, og to af dem ville optage en plads i
    /// det, der bliver sendt, uden at give noget.
    /// </remarks>
    public static List<string> Ryd(IEnumerable<string?> ord)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ud = new List<string>();

        foreach (var raa in ord)
        {
            var o = Rens(raa);

            // Kommentarlinjer beholdes ikke. Filen er en liste, ikke kode.
            if (o.Length == 0 || o.StartsWith('#')) continue;
            if (set.Add(o)) ud.Add(o);
        }

        return ud;
    }

    /// <summary>Skriver biblioteket. Ét ord pr. linje, UTF-8 uden BOM.</summary>
    public static void Gem(IEnumerable<string?> ord)
    {
        var rene = Ryd(ord);

        Directory.CreateDirectory(Path.GetDirectoryName(Sti)!);
        File.WriteAllText(Sti, string.Join('\n', rene) + (rene.Count > 0 ? "\n" : ""),
                          new UTF8Encoding(false));
    }

    /// <summary>
    /// Tilføjer ét ord. Falsk betyder, at det allerede stod der — eller ikke
    /// var et ord.
    /// </summary>
    public static bool Tilfoej(string? ord)
    {
        var o = Rens(ord);
        if (o.Length == 0) return false;

        var liste = Laes();
        if (liste.Any(x => x.Equals(o, StringComparison.OrdinalIgnoreCase))) return false;

        liste.Add(o);
        Gem(liste);
        return true;
    }

    /// <summary>Fjerner ét ord. Falsk betyder, at det ikke stod der.</summary>
    public static bool Fjern(string? ord)
    {
        var o = Rens(ord);
        if (o.Length == 0) return false;

        var liste = Laes();
        var faerre = liste.Where(x => !x.Equals(o, StringComparison.OrdinalIgnoreCase)).ToList();

        if (faerre.Count == liste.Count) return false;

        Gem(faerre);
        return true;
    }

    /// <summary>Det, der faktisk sendes med lyden.</summary>
    public static IReadOnlyList<string> TilAfsendelse(IEnumerable<string>? biblioteket = null) =>
        (biblioteket ?? Laes()).Take(MaksSendte).ToList();

    /// <summary>
    /// Lægger en fil ind i biblioteket.
    /// </summary>
    /// <param name="fil">Filen. Ét ord pr. linje; kommaer tæller også som skel.</param>
    /// <param name="flet">
    /// Sandt: læg til det, der er. Falsk: erstat alt.
    ///
    /// FLETNING ER STANDARDEN, fordi en indlæsning, der sletter, er umulig at
    /// fortryde — ordene er lært over måneder, én rettelse ad gangen.
    /// </param>
    /// <returns>Hvor mange der kom til.</returns>
    public static int Indlaes(string fil, bool flet = true)
    {
        var raa = File.ReadAllLines(fil, Encoding.UTF8)
                      .SelectMany(l => l.Split(',', ';'))
                      .ToList();

        var foer = flet ? Laes() : new List<string>();
        var antalFoer = foer.Count;

        foer.AddRange(raa);
        var efter = Ryd(foer);

        Gem(efter);
        return efter.Count - antalFoer;
    }

    /// <summary>
    /// Skriver biblioteket til en fil, man kan tage med sig.
    /// </summary>
    /// <remarks>
    /// Ren tekst med vilje. En ordbog, der kun kan læses af det program, der
    /// lavede den, er ikke en ordbog, man ejer — den kan åbnes i en hvilken
    /// som helst editor, rettes, og lægges ind igen.
    /// </remarks>
    public static int Udlaes(string fil)
    {
        var liste = Laes();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(fil))!);
        File.WriteAllText(fil, string.Join(Environment.NewLine, liste) + Environment.NewLine,
                          new UTF8Encoding(false));

        return liste.Count;
    }
}
