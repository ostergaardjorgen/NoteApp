using System.Reflection;
using System.Text;

namespace NoteApp.Core;

/// <summary>Ét hjælpeafsnit: filens navn er id'et, første overskrift er titlen.</summary>
public sealed record Hjaelpeafsnit(string Id, string Titel, string Undertitel, string Tekst)
{
    /// <summary>Rækkefølgen kommer fra talprefikset i filnavnet: <c>10-kom-i-gang.md</c>.</summary>
    public int Orden
    {
        get
        {
            var streg = Id.IndexOf('-');
            return streg > 0 && int.TryParse(Id[..streg], out var n) ? n : 999;
        }
    }
}

/// <summary>Et søgetræf i hjælpen — med det stykke tekst, ordet stod i.</summary>
public sealed record Hjaelpetraef(Hjaelpeafsnit Afsnit, int Point, string Uddrag);

/// <summary>
/// Hjælpen, der følger med appen.
///
/// HVORFOR DEN LIGGER I APPEN OG IKKE PÅ EN HJEMMESIDE
///
/// Den virker uden net, den kan ikke blive forældet i forhold til den udgave,
/// man har installeret, og den sender ingenting — heller ikke hvad man søgte
/// efter. En hjælp, der ligger på nettet, er et sted mere, brugerens
/// spørgsmål kan havne.
///
/// SAMME OPBYGNING SOM SPROGFILERNE
///
/// Én mappe pr. sprog, én fil pr. afsnit, almindelig Markdown. Filerne følger
/// med appen og lægges i datamappen ved første brug, så de kan rettes uden at
/// bygge noget — og et nyt sprog er en mappe mere.
///
/// FALDER TILBAGE PÅ DANSK. Mangler et afsnit på det valgte sprog, vises det
/// danske. Halv hjælp slår ingen hjælp.
/// </summary>
public static class Hjaelp
{
    /// <summary>Roden. Under den ligger én mappe pr. sprog.</summary>
    public static string Mappe => Path.Combine(UserDataPaths.Root, "hjaelp");

    private static readonly object _laas = new();
    private static readonly Dictionary<string, List<Hjaelpeafsnit>> _laest = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Afsnittene på det sprog, der vises nu — med dansk som udfyldning.
    /// </summary>
    public static List<Hjaelpeafsnit> Alle() => Alle(Sprog.Kode);

    public static List<Hjaelpeafsnit> Alle(string sprogkode)
    {
        var valgt = PaaSproget(sprogkode);

        if (string.Equals(sprogkode, Sprog.Kilde, StringComparison.OrdinalIgnoreCase))
            return valgt.OrderBy(a => a.Orden).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();

        // DANSK FYLDER HULLERNE UD. Et afsnit, der ikke er oversat endnu, er
        // stadig bedre end ingenting - og det er den samme regel som for
        // teksterne i brugerfladen.
        var dansk = PaaSproget(Sprog.Kilde);
        var haves = valgt.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return valgt.Concat(dansk.Where(a => !haves.Contains(a.Id)))
                    .OrderBy(a => a.Orden)
                    .ThenBy(a => a.Id, StringComparer.Ordinal)
                    .ToList();
    }

    /// <summary>Ét afsnit. Null, hvis id'et ikke findes.</summary>
    public static Hjaelpeafsnit? Find(string id) =>
        Alle().FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Søger i hjælpen.
    ///
    /// ALLE ORD SKAL VÆRE DER. En søgning på «slet lyd» skal ikke svare med
    /// hvert afsnit, hvor ordet «slet» tilfældigvis står. Til gengæld behøver
    /// de ikke stå ved siden af hinanden — hjælpen er korte tekster, og et
    /// afsnit, der handler om begge dele, er det rigtige svar.
    ///
    /// TITLEN VEJER TUNGEST. Står ordet i overskriften, handler afsnittet om
    /// det; står det i brødteksten, bliver det måske bare nævnt.
    /// </summary>
    public static List<Hjaelpetraef> Soeg(string hvad)
    {
        var ord = (hvad ?? "")
            .Split(new[] { ' ', '\t', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(o => o.Length >= 2)
            .Select(o => o.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (ord.Count == 0) return new();

        var ud = new List<Hjaelpetraef>();

        foreach (var a in Alle())
        {
            var titel = a.Titel.ToLowerInvariant();
            var under = a.Undertitel.ToLowerInvariant();
            var tekst = a.Tekst.ToLowerInvariant();

            var point = 0;
            var alle = true;

            foreach (var o in ord)
            {
                var iTitel = Taeller(titel, o);
                var iUnder = Taeller(under, o);
                var iTekst = Taeller(tekst, o);

                if (iTitel + iUnder + iTekst == 0) { alle = false; break; }

                point += iTitel * 20 + iUnder * 8 + iTekst;
            }

            if (!alle) continue;

            ud.Add(new Hjaelpetraef(a, point, Uddrag(a.Tekst, ord[0])));
        }

        return ud.OrderByDescending(t => t.Point).ThenBy(t => t.Afsnit.Orden).ToList();
    }

    private static int Taeller(string hvor, string hvad)
    {
        var n = 0;
        var i = 0;

        while ((i = hvor.IndexOf(hvad, i, StringComparison.Ordinal)) >= 0) { n++; i += hvad.Length; }

        return n;
    }

    /// <summary>
    /// Linjen, ordet stod i — så man kan se, om træffet er det, man leder
    /// efter, uden at åbne afsnittet.
    /// </summary>
    private static string Uddrag(string tekst, string ord)
    {
        var linjer = tekst.Split('\n');

        foreach (var raa in linjer)
        {
            var linje = raa.Trim();

            if (linje.Length == 0 || linje.StartsWith('#')) continue;
            if (linje.IndexOf(ord, StringComparison.OrdinalIgnoreCase) < 0) continue;

            linje = Rens(linje);
            return linje.Length <= 160 ? linje : linje[..160].TrimEnd() + "…";
        }

        // Ordet stod kun i overskriften. Saa er den foerste rigtige linje det
        // bedste bud paa, hvad afsnittet handler om.
        foreach (var raa in linjer)
        {
            var linje = raa.Trim();
            if (linje.Length == 0 || linje.StartsWith('#')) continue;

            linje = Rens(linje);
            return linje.Length <= 160 ? linje : linje[..160].TrimEnd() + "…";
        }

        return "";
    }

    /// <summary>Markdown-tegnene væk, så uddraget kan læses som en sætning.</summary>
    private static string Rens(string linje)
    {
        var sb = new StringBuilder(linje.Length);

        foreach (var c in linje)
        {
            if (c is '*' or '`' or '>' or '_') continue;
            sb.Append(c);
        }

        var t = sb.ToString().Trim();

        if (t.StartsWith("- ", StringComparison.Ordinal)) t = t[2..];

        return t.Trim();
    }

    /// <summary>Tvinger næste opslag til at læse filerne forfra.</summary>
    public static void Genindlaes()
    {
        lock (_laas) _laest.Clear();
    }

    // ===================== INDMADEN =====================

    private static List<Hjaelpeafsnit> PaaSproget(string sprogkode)
    {
        var kode = (sprogkode ?? Sprog.Kilde).ToLowerInvariant();

        lock (_laas)
        {
            if (_laest.TryGetValue(kode, out var kendt)) return kendt;

            Udpak();

            var ud = new List<Hjaelpeafsnit>();
            var mappe = Path.Combine(Mappe, kode);

            try
            {
                if (Directory.Exists(mappe))
                {
                    foreach (var fil in Directory.EnumerateFiles(mappe, "*.md"))
                    {
                        var afsnit = Laes(fil);
                        if (afsnit is not null) ud.Add(afsnit);
                    }
                }
            }
            catch (IOException) { }

            _laest[kode] = ud;
            return ud;
        }
    }

    /// <summary>
    /// Læser ét afsnit.
    ///
    /// Formen er bevidst enkel: første <c>#</c>-linje er titlen, næste linje
    /// er en kort undertitel, resten er teksten. Ingen frontmatter — en
    /// hjælpefil skal kunne åbnes i Notesblok og se ud som noget, man kan
    /// læse.
    /// </summary>
    private static Hjaelpeafsnit? Laes(string sti)
    {
        try
        {
            var linjer = File.ReadAllLines(sti, Encoding.UTF8);
            if (linjer.Length == 0) return null;

            var id = Path.GetFileNameWithoutExtension(sti);
            var titel = "";
            var under = "";
            var krop = new List<string>();

            var i = 0;

            for (; i < linjer.Length; i++)
            {
                var l = linjer[i].Trim();
                if (l.Length == 0) continue;

                if (l.StartsWith("# ", StringComparison.Ordinal)) { titel = l[2..].Trim(); i++; }
                break;
            }

            for (; i < linjer.Length; i++)
            {
                var l = linjer[i].Trim();
                if (l.Length == 0) continue;

                if (l.StartsWith('*') && l.EndsWith('*') && l.Length > 2)
                {
                    under = l.Trim('*').Trim();
                    i++;
                }

                break;
            }

            for (; i < linjer.Length; i++) krop.Add(linjer[i]);

            if (titel.Length == 0) titel = id;

            return new Hjaelpeafsnit(id, titel, under, string.Join('\n', krop).Trim());
        }
        catch (IOException) { return null; }
    }

    /// <summary>
    /// Lægger hjælpen i datamappen — og opdaterer de filer, ingen har rettet.
    ///
    /// HVORFOR DET IKKE ER NOK AT SKRIVE FILEN ÉN GANG
    ///
    /// Første udgave sprang over, hvis filen fandtes. Så ville en rettet
    /// hjælpetekst i en ny udgave af appen aldrig nå frem til nogen, der
    /// havde appen i forvejen — hjælpen ville blive stående på den udgave,
    /// den blev installeret med, og efterhånden beskrive noget andet end det,
    /// skærmen gør.
    ///
    /// EN HJÆLPETEKST ER ÉT DOKUMENT, IKKE MANGE SMÅ TING
    ///
    /// Sprogfilerne kan flettes nøgle for nøgle. Det kan et afsnit ikke: to
    /// udgaver af den samme tekst kan ikke lægges sammen til noget, der giver
    /// mening.
    ///
    /// Derfor huskes det, appen SELV skrev — som en kontrolsum i
    /// <c>.udgivet.txt</c>. Er filen på disken uændret siden da, har ingen
    /// rørt den, og den kan trygt skiftes ud. Er den ændret, har nogen rettet
    /// i den, og så bliver den stående.
    /// </summary>
    private static void Udpak()
    {
        try
        {
            var samling = Assembly.GetExecutingAssembly();
            var bogen = Bog();
            var aendret = false;

            foreach (var navn in samling.GetManifestResourceNames())
            {
                var maerke = ".hjaelp.";
                var p = navn.IndexOf(maerke, StringComparison.OrdinalIgnoreCase);

                if (p < 0 || !navn.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;

                // «NoteApp.Core.hjaelp.da.10-kom-i-gang.md» -> «da», «10-kom-i-gang.md»
                var rest = navn[(p + maerke.Length)..];
                var punkt = rest.IndexOf('.');
                if (punkt <= 0) continue;

                var sprogmappe = rest[..punkt];
                var filnavn = rest[(punkt + 1)..];

                var maalmappe = Path.Combine(Mappe, sprogmappe);
                Directory.CreateDirectory(maalmappe);

                var maal = Path.Combine(maalmappe, filnavn);

                using var stroem = samling.GetManifestResourceStream(navn);
                if (stroem is null) continue;

                using var laeser = new StreamReader(stroem, Encoding.UTF8);
                var indhold = laeser.ReadToEnd();

                var noegle = sprogmappe + "/" + filnavn;
                var udgivetSum = Sum(indhold);

                if (File.Exists(maal))
                {
                    var paaDisken = Sum(File.ReadAllText(maal, Encoding.UTF8));

                    // Er den allerede den nyeste, er der ingenting at goere.
                    if (paaDisken == udgivetSum)
                    {
                        if (!bogen.TryGetValue(noegle, out var kendt) || kendt != udgivetSum)
                        {
                            bogen[noegle] = udgivetSum;
                            aendret = true;
                        }
                        continue;
                    }

                    // Staar der en anden sum i bogen end den, filen har nu, har
                    // nogen rettet i teksten. Den bliver staaende.
                    if (bogen.TryGetValue(noegle, out var skrevet) && skrevet != paaDisken) continue;

                    // Ingen bog: appen er opdateret fra en udgave, der ikke
                    // foerte den. Saa vides det ikke, om filen er rettet - og
                    // saa roeres den ikke. Bedre at have en foraeldet hjaelp
                    // end at slette en rettelse.
                    if (!bogen.ContainsKey(noegle))
                    {
                        bogen[noegle] = paaDisken;
                        aendret = true;
                        continue;
                    }
                }

                File.WriteAllText(maal, indhold, new UTF8Encoding(false));

                bogen[noegle] = udgivetSum;
                aendret = true;
            }

            if (aendret) GemBog(bogen);
        }
        catch (Exception)
        {
            // Kan hjaelpen ikke skrives ud, staar vinduet tomt. Det maa ikke
            // vaelte appen - hjaelpen er en hjaelp, ikke en forudsaetning.
        }
    }

    private static string Bogsti => Path.Combine(Mappe, ".udgivet.txt");

    /// <summary>Hvad appen selv skrev, sidst den lagde hjælpen ud. Én linje pr. fil.</summary>
    private static Dictionary<string, string> Bog()
    {
        var ud = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(Bogsti)) return ud;

            foreach (var linje in File.ReadAllLines(Bogsti, Encoding.UTF8))
            {
                var skil = linje.IndexOf('\t');
                if (skil <= 0) continue;

                ud[linje[..skil]] = linje[(skil + 1)..].Trim();
            }
        }
        catch (IOException) { }

        return ud;
    }

    private static void GemBog(Dictionary<string, string> bogen)
    {
        try
        {
            Directory.CreateDirectory(Mappe);

            var linjer = bogen.OrderBy(p => p.Key, StringComparer.Ordinal)
                              .Select(p => p.Key + "\t" + p.Value);

            File.WriteAllLines(Bogsti, linjer, new UTF8Encoding(false));

            // Filen er appens egen bogholderi og ikke noget, brugeren skal
            // forholde sig til. Den skjules, saa mappen ser ud som det, den er:
            // hjaelpetekster.
            try { File.SetAttributes(Bogsti, FileAttributes.Hidden); }
            catch (Exception) { }
        }
        catch (IOException) { }
    }

    private static string Sum(string tekst)
    {
        // Linjeskift taeller ikke med. En fil, der er gemt i Notesblok, faar
        // CRLF, og saa ville den se rettet ud, selv om teksten er den samme.
        var renset = tekst.Replace("\r\n", "\n").TrimEnd();

        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(renset));
        return Convert.ToHexString(bytes);
    }
}
