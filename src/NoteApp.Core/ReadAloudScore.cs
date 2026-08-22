using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>
/// Ét sted, hvor udskriften ikke svarer til manuskriptet.
///
/// Der er TO positioner, og de er ikke det samme. <see cref="Position"/> er
/// pladsen i manuskriptet — den bruges til at sige hvad der skulle have stået.
/// <see cref="TranskriptionPosition"/> er pladsen i udskriften, og den findes, fordi
/// udskriften er den eneste vej tilbage til LYDEN: Whisper leverer et tidsrum
/// pr. sætning, og uden at vide hvilket ord i udskriften afvigelsen sad ved,
/// kan man ikke finde det tidsrum og spille sætningen igen.
/// </summary>
public sealed record Afvigelse(int Position, string Forventet, string Hørt, int UdskriftPosition = 0);

/// <summary>
/// Den færdige opstilling af manuskript mod udskrift.
///
/// <see cref="TilManuskript"/> har ét tal pr. ord i udskriften: hvilket ord i
/// manuskriptet det blev stillet op imod. Den bruges til at oversætte et
/// stykke af udskriften — fx én sætning fra Whisper — til det stykke af
/// manuskriptet, der skulle have stået der.
/// </summary>
public sealed record Opstilling(
    int Ialt,
    int Ramt,
    IReadOnlyList<Afvigelse> Afvigelser,
    IReadOnlyList<int> TilManuskript);

/// <summary>
/// Hvor tæt er udskriften på det, der faktisk blev læst op?
///
/// HVORFOR DET KAN MÅLES HER OG INGEN ANDRE STEDER
///
/// Ved en oplæsning findes facit: manuskriptet. Det gør det muligt at sige et
/// tal, der betyder noget — «94 % af ordene er ramt» — frem for et skøn.
/// Et rigtigt møde har intet at holde teksten op imod, og dér kan tallet ikke
/// gives. Derfor er oplæsningerne ikke en øvelse: de er den eneste måling,
/// appen har.
///
/// HVAD DER NORMALISERES, OG HVORFOR
///
/// Manuskriptet skriver tal som ORD («fireogtyve»), Whisper skriver dem som
/// CIFRE («24»). Det er ikke en fejl, det er to skrivemåder for det samme, og
/// en måling, der tæller dem som fejl, ville aldrig kunne nå i mål — uanset
/// hvor meget man rettede.
///
/// Det var præcis dét, der skete første gang: 45 «talfejl», hvoraf ingen var
/// fejl.
/// </summary>
public static class ReadAloudScore
{
    /// <summary>
    /// Danske talord til cifre. Kun dem, der optræder i prøveteksterne —
    /// listen skal kunne overskues, ikke være komplet.
    /// </summary>
    private static readonly Dictionary<string, string> Talord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nul"] = "0", ["en"] = "1", ["et"] = "1", ["to"] = "2", ["tre"] = "3",
        ["fire"] = "4", ["fem"] = "5", ["seks"] = "6", ["syv"] = "7", ["otte"] = "8",
        ["ni"] = "9", ["ti"] = "10", ["elleve"] = "11", ["tolv"] = "12",
        ["tretten"] = "13", ["fjorten"] = "14", ["femten"] = "15", ["seksten"] = "16",
        ["sytten"] = "17", ["atten"] = "18", ["nitten"] = "19", ["tyve"] = "20",
        ["enogtyve"] = "21", ["toogtyve"] = "22", ["treogtyve"] = "23",
        ["fireogtyve"] = "24", ["femogtyve"] = "25", ["tredive"] = "30",
        ["fyrre"] = "40", ["toogfyrre"] = "42", ["halvtreds"] = "50",
        ["tres"] = "60", ["fireogtres"] = "64", ["halvfjerds"] = "70",
        ["firs"] = "80", ["otteogfirs"] = "88", ["halvfems"] = "90",
        ["hundrede"] = "100", ["tusind"] = "1000",
        ["ellevte"] = "11", ["tolvte"] = "12", ["fjortende"] = "14"
    };

    /// <summary>
    /// Ordene i en tekst, gjort sammenlignelige: små bogstaver, ingen
    /// tegnsætning, talord som cifre.
    ///
    /// TEGN, DER ER ORD. Whisper skriver «6%» og «kr.»; manuskriptet skriver
    /// «6 procent» og «kroner». Uden en oversættelse ville tegnsætningen blive
    /// smidt væk, og ordet i manuskriptet ville stå som manglende — 22 fejl,
    /// hvoraf ingen var fejl. Det er den samme faldgrube som talordene, og den
    /// koster på præcis samme måde: et tal, man aldrig kan rette sig ud af.
    /// </summary>
    public static List<string> Ord(string tekst)
    {
        tekst = tekst
            .Replace("%", " procent ")
            .Replace("&", " og ");

        var rå = Regex.Split(tekst.Replace("**", ""), @"[^\p{L}\p{N}]+")
            .Where(o => o.Length > 0)
            .Select(o => o.ToLowerInvariant())
            .ToList();

        var ud = new List<string>();

        for (var i = 0; i < rå.Count; i++)
        {
            var (værdi, brugt) = LæsTal(rå, i);

            if (brugt > 0)
            {
                ud.Add(værdi.ToString());
                i += brugt - 1;
                continue;
            }

            ud.Add(rå[i]);
        }

        return ud;
    }

    /// <summary>
    /// Læser et sammensat dansk talord fra <paramref name="pos"/> og frem.
    /// Returnerer værdien og hvor mange ord der blev brugt; 0 hvis der ikke
    /// stod et tal.
    ///
    /// HVORFOR DET SKAL VÆRE SAMMENSAT
    ///
    /// «fire hundrede og tolv» er ÉT tal: 412. Oversættes ordene hver for sig,
    /// bliver det «4 100 og 12» — og så passer det ikke til Whispers «412»,
    /// selv om begge siger det samme. Første udgave gjorde netop det, og
    /// målingen faldt til 37 %, hvor den skulle have været over 90.
    ///
    /// «en» og «et» er med vilje UNDTAGET, medmindre de står inde i et tal.
    /// De er artikler i næsten hver anden sætning, og «1 fejlrate» er ikke en
    /// forbedring.
    /// </summary>
    private static (int Værdi, int Brugt) LæsTal(List<string> ord, int pos)
    {
        int værdi = 0, brugt = 0, i = pos;
        var harNoget = false;

        while (i < ord.Count)
        {
            var o = ord[i];

            if (o is "og")
            {
                // «og» taeller kun med, hvis der baade staar tal foer og efter.
                if (!harNoget || i + 1 >= ord.Count || !ErTalord(ord[i + 1], midtITal: true)) break;
                i++; brugt++;
                continue;
            }

            if (o is "hundrede" or "hundred")
            {
                værdi = Math.Max(værdi, 1) * 100;
                harNoget = true;
                i++; brugt++;
                continue;
            }

            if (o is "tusind" or "tusinde")
            {
                værdi = Math.Max(værdi, 1) * 1000;
                harNoget = true;
                i++; brugt++;
                continue;
            }

            if (Talord.TryGetValue(o, out var t) && int.TryParse(t, out var n))
            {
                // «en»/«et» alene er en artikel, ikke et tal. De taeller kun,
                // naar de staar inde i et tal: «tre hundrede og en».
                if ((o is "en" or "et") && !harNoget) break;

                værdi += n;
                harNoget = true;
                i++; brugt++;
                continue;
            }

            break;
        }

        return harNoget ? (værdi, brugt) : (0, 0);
    }

    private static bool ErTalord(string o, bool midtITal) =>
        o is "hundrede" or "hundred" or "tusind" or "tusinde" ||
        (Talord.ContainsKey(o) && (midtITal || o is not ("en" or "et")));

    /// <summary>Manuskriptets tekst — kun det, der faktisk læses højt.</summary>
    public static string ManuskriptTekst(string markdown)
    {
        var sb = new StringBuilder();
        var iBlok = false;

        foreach (var rå in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var linje = rå.Trim();

            // Alt foer foerste blok er vejledning, og overskrifter laeses ikke
            // op. Tages de med, maaler man mod noget, ingen har sagt.
            if (linje.StartsWith("## ")) { iBlok = true; continue; }
            if (linje.StartsWith("#") || linje == "---") continue;
            if (!iBlok || linje.Length == 0) continue;
            if (linje.StartsWith("sprog:")) continue;

            sb.AppendLine(linje);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sammenligner udskrift med manuskript.
    ///
    /// Der bruges en almindelig længste-fælles-følge. Den er langsommere end
    /// et hurtigt hash-opslag, men den finder par, der er RYKKET — og et
    /// manglende ord må ikke få resten af teksten til at se forkert ud.
    /// </summary>
    public static (int Ialt, int Ramt, IReadOnlyList<Afvigelse> Afvigelser) Sammenlign(
        string manuskript, string udskrift, int maksAfvigelser = 400)
    {
        var r = StilOp(Ord(manuskript), Ord(udskrift), maksAfvigelser);
        return (r.Ialt, r.Ramt, r.Afvigelser);
    }

    /// <summary>
    /// Samme opstilling, men på ord der allerede er delt op — og med vejen
    /// tilbage til udskriften bevaret.
    ///
    /// Overloaden findes, fordi træningsvisningen deler udskriften op PR.
    /// SÆTNING, før den måler. Kaldte den <see cref="Ord"/> på den samlede
    /// tekst i stedet, kunne taldelingen løbe hen over en sætningsgrænse
    /// («fire hundrede» delt over to sætninger), og så passede ordnumrene ikke
    /// længere med de tidsrum, sætningerne skal spilles fra.
    /// </summary>
    public static Opstilling StilOp(
        IReadOnlyList<string> a, IReadOnlyList<string> b, int maksAfvigelser = 400)
    {
        if (a.Count == 0) return new Opstilling(0, 0, Array.Empty<Afvigelse>(), Array.Empty<int>());

        // RIGTIG OPSTILLING, IKKE GRAADIG SOEGNING.
        //
        // Foerste udgave gik frem ord for ord og ledte efter naeste sted, de to
        // tekster moedtes. Ved den foerste afvigelse — «inden» mod «i en» —
        // fandt den ordet «i» fem ord laengere fremme og sprang alt derimellem
        // over. Derfra var alt forskubbet, og maalingen sagde 37 %, hvor den
        // skulle have sagt over 90.
        //
        // Her opstilles teksterne rigtigt: mindste antal aendringer, der
        // forbinder dem. Det koster en tabel paa n gange m, men 2400 gange 2400
        // er faa millioner celler — under et sekund og et par megabyte.
        var n = a.Count;
        var m = b.Count;

        var forrige = new int[m + 1];
        var nuvaerende = new int[m + 1];

        // Retningen gemmes, saa vejen kan gaas baglaens bagefter. En byte pr.
        // celle: 1 = ordene passer, 2 = ord mangler i udskriften, 3 = ord kom
        // til, 4 = eet ord byttet ud med et andet.
        var retning = new byte[(n + 1) * (m + 1)];

        for (var j = 0; j <= m; j++) { forrige[j] = j; retning[j] = 3; }
        retning[0] = 0;

        for (var i = 1; i <= n; i++)
        {
            nuvaerende[0] = i;
            retning[i * (m + 1)] = 2;

            for (var j = 1; j <= m; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    nuvaerende[j] = forrige[j - 1];
                    retning[i * (m + 1) + j] = 1;
                    continue;
                }

                var byt = forrige[j - 1] + 1;
                var mangler = forrige[j] + 1;
                var kom = nuvaerende[j - 1] + 1;

                var bedst = Math.Min(byt, Math.Min(mangler, kom));
                nuvaerende[j] = bedst;

                retning[i * (m + 1) + j] = bedst == byt ? (byte)4
                                        : bedst == mangler ? (byte)2
                                        : (byte)3;
            }

            (forrige, nuvaerende) = (nuvaerende, forrige);
        }

        // Baglaens gennem vejen. Afvigelser samles op undervejs og vendes til
        // sidst, saa de staar i laeseraekkefoelge.
        var fundne = new List<Afvigelse>();
        var ramt = 0;
        var x = n;
        var y = m;

        // Ét tal pr. ord i UDSKRIFTEN: hvor i manuskriptet det hører hjemme.
        // Fyldes undervejs baglæns, så der ikke skal gås gennem tabellen to
        // gange.
        var tilManuskript = new int[m];

        while (x > 0 || y > 0)
        {
            var r = retning[x * (m + 1) + y];

            switch (r)
            {
                case 1:
                    ramt++;
                    tilManuskript[y - 1] = x - 1;
                    x--; y--;
                    break;

                case 4:
                    fundne.Add(new Afvigelse(x - 1, a[x - 1], b[y - 1], y - 1));
                    tilManuskript[y - 1] = x - 1;
                    x--; y--;
                    break;

                case 2:
                    // Ordet blev sagt i manuskriptet, men står ikke i
                    // udskriften. Der er intet udskriftsord at pege på, så
                    // afvigelsen hænges på det næste — dér vil man lede.
                    fundne.Add(new Afvigelse(x - 1, a[x - 1], "", Math.Min(y, Math.Max(0, m - 1))));
                    x--;
                    break;

                default:
                    fundne.Add(new Afvigelse(Math.Max(0, x - 1), "", b[y - 1], y - 1));
                    tilManuskript[y - 1] = Math.Max(0, x - 1);
                    y--;
                    break;
            }
        }

        fundne.Reverse();

        // Naboafvigelser slaas sammen: «start dato» mod «startdasser» er EEN
        // fejl, ikke to. Ellers taeller listen forkert, og den bliver ulaeselig.
        var afvigelser = new List<Afvigelse>();

        foreach (var f in fundne)
        {
            var sidste = afvigelser.Count > 0 ? afvigelser[^1] : null;

            if (sidste is not null && f.Position - sidste.Position <= 1)
            {
                afvigelser[^1] = sidste with
                {
                    Forventet = (sidste.Forventet + " " + f.Forventet).Trim(),
                    Hørt = (sidste.Hørt + " " + f.Hørt).Trim()
                };
                continue;
            }

            if (afvigelser.Count < maksAfvigelser) afvigelser.Add(f);
        }

        return new Opstilling(a.Count, ramt, afvigelser, tilManuskript);
    }
}
