using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Et stykke tale fra ét spor, med det tidspunkt det blev sagt.</summary>
public sealed record Replik(long FraMs, long TilMs, string Spor, string Tekst);

/// <summary>
/// Fletter de to lydspor til én samtale, hvor det fremgår, hvor hver replik
/// kom fra.
///
/// HVORFOR DEN FINDES
///
/// Et onlinemøde optages på to spor: mikrofonen og det, der kom ud af
/// højttaleren. Indtil nu blev kun mikrofonen skrevet ud. Loopback-sporet lå
/// og fyldte over hundrede megabyte pr. møde uden nogensinde at blive læst —
/// og alt, hvad de andre sagde, manglede derfor i referatet, med mindre det
/// tilfældigvis kunne høres akustisk i lokalet.
///
/// Det er også den mest sandsynlige forklaring på en opfundet deltager: hører
/// mikrofonen en fjern stemme svagt gennem en højttaler, gætter Whisper på
/// stavelserne, og et navn, ingen har sagt, kan komme ud af det.
///
/// HVAD FLETNINGEN KAN OG IKKE KAN
///
/// Den kan skille de to SIDER fra hinanden: hvad der blev sagt i lokalet, og
/// hvad der kom fra den anden ende. Det er nok til, at et referat ikke
/// tillægger dig en beslutning, du ikke traf.
///
/// Den kan IKKE skille de enkelte personer i den anden ende fra hinanden. Det
/// kræver rigtig talergenkendelse. Navnene kommer fra navnerunden og fra det,
/// folk kalder hinanden undervejs — det er også derfor, dagsordenen beder om
/// den runde.
/// </summary>
public static class Samtale
{
    public const string Herfra = "HERFRA";
    public const string Derfra = "DERFRA";

    /// <summary>
    /// Læser segmenterne ud af en json fra whisper.cpp.
    ///
    /// Formatet er <c>{"transcription":[{"offsets":{"from":0,"to":29980},
    /// "text":" ..."}]}</c>. Offsets er millisekunder og er det eneste, der
    /// bruges — tidsstemplerne som tekst er de samme tal skrevet ud.
    /// </summary>
    public static List<Replik> Laes(string jsonSti, string spor)
    {
        var ud = new List<Replik>();
        if (!File.Exists(jsonSti)) return ud;

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonSti, Encoding.UTF8));
        if (!doc.RootElement.TryGetProperty("transcription", out var liste)) return ud;

        foreach (var s in liste.EnumerateArray())
        {
            var tekst = s.TryGetProperty("text", out var t) ? (t.GetString() ?? "").Trim() : "";
            if (tekst.Length == 0) continue;

            long fra = 0, til = 0;
            if (s.TryGetProperty("offsets", out var o))
            {
                if (o.TryGetProperty("from", out var f)) fra = f.GetInt64();
                if (o.TryGetProperty("to", out var e)) til = e.GetInt64();
            }

            ud.Add(new Replik(fra, til, spor, tekst));
        }

        return ud;
    }

    /// <summary>
    /// Fjerner de replikker fra mikrofonsporet, der bare er den anden ende
    /// hørt gennem en højttaler.
    ///
    /// HVORFOR DET ER NØDVENDIGT
    ///
    /// Optages der med en højttalertelefon frem for et headset, kommer
    /// modpartens stemme MED i mikrofonsporet — dårligere, men tydeligt nok
    /// til at Whisper skriver den ud. Uden det her ville hver eneste sætning
    /// fra den anden ende stå to gange i udskriften: én gang rent fra
    /// loopback, én gang forvansket fra mikrofonen.
    ///
    /// To sætninger regnes for den samme, hvis de overlapper i tid OG deler
    /// hovedparten af deres ord. Der sammenlignes på ord og ikke på tegn,
    /// fordi det netop er de enkelte lyde, der bliver forkert i det svage
    /// spor — ordene rammes oftere end stavelserne.
    /// </summary>
    public static List<Replik> FjernEkko(List<Replik> mikrofon, List<Replik> loopback)
    {
        if (loopback.Count == 0) return mikrofon;

        var beholdt = new List<Replik>();

        foreach (var m in mikrofon)
        {
            var ekko = loopback.Any(l =>
                Overlapper(m, l) && Ligner(m.Tekst, l.Tekst));

            if (!ekko) beholdt.Add(m);
        }

        return beholdt;
    }

    /// <summary>
    /// Overlapper de to replikker i tid? Der gives et sekunds slæk: de to spor
    /// er ikke sample-synkrone, og en højttaler er et øjeblik bagud.
    /// </summary>
    private static bool Overlapper(Replik a, Replik b) =>
        a.FraMs < b.TilMs + 1000 && b.FraMs < a.TilMs + 1000;

    /// <summary>
    /// Deler de to sætninger hovedparten af deres ord?
    ///
    /// Der måles på, hvor stor en del af den KORTESTE der går igen i den
    /// anden. Det svage spor taber typisk ord i begge ender, så en ren
    /// sammenligning af mængderne ville lade for mange slippe igennem.
    /// </summary>
    private static bool Ligner(string a, string b)
    {
        var oa = Ord(a);
        var ob = Ord(b);
        if (oa.Count == 0 || ob.Count == 0) return false;

        // Meget korte udbrud — «ja», «mmh» — kan ikke afgøres på ordene. De
        // faar lov at blive: en dublet af et «ja» koster ingenting, mens et
        // tabt «ja» fra den forkerte side kan vende meningen af et referat.
        if (Math.Min(oa.Count, ob.Count) < 4) return false;

        var faelles = oa.Count(o => ob.Contains(o));
        return faelles / (double)Math.Min(oa.Count, ob.Count) >= 0.6;
    }

    private static HashSet<string> Ord(string s) =>
        new(s.ToLowerInvariant()
             .Split(new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '\n', '\r', '"' },
                    StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

    /// <summary>
    /// Fjerner Whispers hallucinationer fra stilhed.
    ///
    /// HVAD DET ER
    ///
    /// Whisper er trænet på undertekster, og i lange pauser skriver den de
    /// sætninger, der stod i slutteksterne på det materiale: «Danske tekster
    /// af …», «Undertekster af …», «Thank you.» Målt på ét rigtigt møde stod
    /// der 26 sådanne linjer ud af 471 — over fem procent af udskriften var
    /// tekst, ingen havde sagt.
    ///
    /// De koster to gange: de fylder i den prompt, der sendes af sted, og de
    /// giver sprogmodellen navne og sætninger, den kan bygge en opdigtet
    /// deltager af.
    ///
    /// HVORFOR REGLEN ER SÅ FORSIGTIG
    ///
    /// Der bruges ingen liste over kendte sætninger. En liste ville være på
    /// dansk og engelsk og ikke på tysk, og den ville skulle passes.
    ///
    /// I stedet: en sætning på over femten tegn, der optræder ORDRET tre
    /// gange eller mere, er ikke noget, nogen har sagt. Rigtig tale gentager
    /// korte ting — «ja», «præcis», «det er rigtigt» — men ikke en hel
    /// sætning ord for ord tre gange. Grænsen er sat, så den hellere lader
    /// noget slippe igennem end fjerner noget, der blev sagt.
    /// </summary>
    public static List<Replik> FjernStilhed(List<Replik> replikker)
    {
        var taeller = replikker
            .GroupBy(r => r.Tekst, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() >= 3 && g.Key.Length > 15)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return replikker.Where(r => !taeller.Contains(r.Tekst) && !ErStilhed(r)).ToList();
    }

    /// <summary>
    /// Er replikken en håndfuld ord spredt ud over et helt vindue?
    ///
    /// DET ER DET SIKRESTE KENDETEGN, OG DET ER MÅLT.
    ///
    /// Gentagelsesreglen ovenfor fanger kun det, der står tre gange ordret.
    /// Den holdt, så længe begge spor var på dansk og hallucinerede den samme
    /// sætning. Da gæsternes spor blev skrevet ud på norsk, kom der nye —
    /// «Undertekster av Ai-Media», «Teksting av Nicolai Winther» — og de stod
    /// for få gange hver til at blive fanget.
    ///
    /// Talehastigheden skiller dem rent. Målt på et rigtigt møde på en time:
    ///
    ///   hallucinationer     0,10 – 0,30 ord/sekund   (altid hele vinduet)
    ///   den langsomste
    ///   ægte lange replik   1,49 ord/sekund
    ///
    /// Der er et spring på en faktor fem mellem dem. Grænsen er sat på 0,5 —
    /// tre gange under den langsomste rigtige tale, der blev målt.
    ///
    /// Kun lange segmenter vurderes. Et kort «ja» fylder få ord på få
    /// sekunder og ville ellers ryge med.
    /// </summary>
    private static bool ErStilhed(Replik r)
    {
        var sekunder = (r.TilMs - r.FraMs) / 1000.0;
        if (sekunder < 20) return false;

        var ord = r.Tekst.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return ord / sekunder < 0.5;
    }

    /// <summary>
    /// Skriver den flettede samtale.
    ///
    /// Sammenhængende replikker fra samme spor lægges sammen til ét afsnit.
    /// Whisper skærer ved tredive sekunder, ikke ved talerskift, og uden det
    /// her ville udskriften være en mur af enkeltlinjer med et mærkat på hver.
    /// </summary>
    /// <summary>
    /// Forklaringen øverst i en to-spors udskrift.
    ///
    /// Den er til sprogmodellen lige så meget som til læseren: uden den er
    /// mærkaterne to ord uden betydning, og så gætter modellen på, hvad de
    /// dækker. Med den kan den holde de to sider fra hinanden — og den ved
    /// samtidig, at mærkaterne IKKE er navne, så den ikke finder på en
    /// deltager, der hedder Derfra.
    ///
    /// Er der sat navne på talerne, skifter forklaringen: så er der ikke
    /// længere mærkater at forklare, men til gengæld er der noget vigtigere
    /// at sige — at navnene er sat af et menneske og ikke genkendt af en
    /// maskine.
    /// </summary>
    public static string Forklaring(IReadOnlyDictionary<string, string>? navne = null)
    {
        var sb = new StringBuilder();

        var harNavne = navne is not null
                       && (navne.ContainsKey(Herfra) || navne.ContainsKey(Derfra));

        sb.AppendLine("SÅDAN ER TRANSKRIPTIONEN LAVET");
        sb.AppendLine();
        sb.AppendLine("Mødet blev optaget på to lydspor, skrevet ud hver for sig og flettet");
        sb.AppendLine("efter tid.");
        sb.AppendLine();

        if (harNavne)
        {
            sb.AppendLine($"  {Udskrift.Navn(Herfra, navne)} = mikrofonen på den pc, mødet blev optaget fra.");
            sb.AppendLine($"  {Udskrift.Navn(Derfra, navne)} = de øvrige deltagere, som de lød i højttaleren.");
            sb.AppendLine();
            sb.AppendLine("NAVNENE ER SAT I HÅNDEN, ikke genkendt af maskinen. De siger, hvilken");
            sb.AppendLine("SIDE af mødet der talte — der kan være flere personer bag hver af dem.");
        }
        else
        {
            // «Mig» og «Gaester» frem for HERFRA og DERFRA. Noeglerne i
            // filerne er uaendrede; det er kun det, modellen og laeseren ser.
            // To ord, der siger noget, slaar to ord, der skal forklares.
            sb.AppendLine("  Mig    = mikrofonen på den pc, mødet blev optaget fra. Det er den, der");
            sb.AppendLine("           optog, og alle andre i det samme lokale.");
            sb.AppendLine("  Gæster = de øvrige deltagere, som de lød i højttaleren.");
            sb.AppendLine();
            sb.AppendLine("MÆRKATERNE ER IKKE NAVNE. De siger, hvilken side af mødet der talte, ikke");
            sb.AppendLine("hvem. Navnene skal findes i det, der bliver sagt — typisk i navnerunden");
            sb.AppendLine("først i mødet. Der kan være flere personer bag både «Mig» og «Gæster».");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Tid(long ms) =>
        TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss");

    /// <summary>
    /// Hele vejen: json-filerne ind, replikkerne ud — rensede og flettede.
    ///
    /// Er der kun ét spor — et fysisk møde eller en optagelse fra en telefon —
    /// er der intet at flette. Så renses mikrofonsporet alene, og replikkerne
    /// får intet spormærkat: der er ingen sider at holde fra hinanden.
    /// </summary>
    public static List<Replik> Flet(string mikrofonJson, string? loopbackJson)
    {
        var etSpor = loopbackJson is null || !File.Exists(loopbackJson);

        var mik = Laes(mikrofonJson, etSpor ? "" : Herfra);
        var loop = etSpor ? new List<Replik>() : Laes(loopbackJson!, Derfra);

        if (loop.Count == 0) return FjernStilhed(mik);

        // DER TAELLES PAA BEGGE SPOR SAMLET, IKKE ET SPOR AD GANGEN.
        //
        // Maalt paa et rigtigt moede: «Danske tekster af Nicolai Winther»
        // stod 2 gange i mikrofonsporet og 13 gange i loopback. Taelles
        // sporene hver for sig, slipper de to igennem - og et opdigtet navn
        // er praecis det, der bliver til en opdigtet deltager.
        //
        // Samlet staar den 15 gange og ryger ud. Til sammenligning stod «Jeg
        // synes, det er en god idé.» 2 gange i alt, og den bliver staaende.
        // Det var netop den saetning, en lavere graense ville have kostet.
        //
        // Raekkefoelgen betyder ogsaa noget: hallucinationerne skal vaek FOER
        // ekkosammenligningen, ellers taeller de med som faelles ord og kan
        // faa en aegte replik til at ligne et ekko.
        var alle = FjernStilhed(mik.Concat(loop).ToList());

        var mikRen = alle.Where(r => r.Spor == Herfra).ToList();
        var loopRen = alle.Where(r => r.Spor == Derfra).ToList();

        // Var alt i det andet spor hallucinationer, er der intet at flette.
        // En "samtale" med maerkater, hvor kun den ene side siger noget, ville
        // love en opdeling, der ikke findes - saa staar mikrofonen alene og
        // uden maerkat.
        if (loopRen.Count == 0)
            return mikRen.Select(r => r with { Spor = "" }).ToList();

        return FjernEkko(mikRen, loopRen)
               .Concat(loopRen)
               .OrderBy(r => r.FraMs)
               .ThenBy(r => r.Spor)
               .ToList();
    }
}
