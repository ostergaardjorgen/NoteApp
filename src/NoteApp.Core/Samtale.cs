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

        return taeller.Count == 0
            ? replikker
            : replikker.Where(r => !taeller.Contains(r.Tekst)).ToList();
    }

    /// <summary>
    /// Skriver den flettede samtale.
    ///
    /// Sammenhængende replikker fra samme spor lægges sammen til ét afsnit.
    /// Whisper skærer ved tredive sekunder, ikke ved talerskift, og uden det
    /// her ville udskriften være en mur af enkeltlinjer med et mærkat på hver.
    /// </summary>
    public static string Skriv(List<Replik> replikker, string? sprogHerfra = null, string? sprogDerfra = null)
    {
        var sb = new StringBuilder();

        // FORKLARINGEN STÅR ØVERST, OG DEN ER TIL MODELLEN.
        //
        // Uden den er «HERFRA» og «DERFRA» to ord uden betydning, og så
        // gætter sprogmodellen på, hvad de dækker. Med den kan den holde de
        // to sider fra hinanden — og den ved samtidig, at mærkaterne IKKE er
        // navne, så den ikke finder på en deltager, der hedder Derfra.
        sb.AppendLine("SÅDAN ER UDSKRIFTEN LAVET");
        sb.AppendLine();
        sb.AppendLine("Mødet blev optaget på to lydspor, skrevet ud hver for sig og flettet");
        sb.AppendLine("efter tid. Mærkatet foran hver replik siger, hvilket spor den kom fra:");
        sb.AppendLine();
        sb.AppendLine($"  {Herfra} = mikrofonen på den pc, mødet blev optaget fra. Det er den, der");
        sb.AppendLine("           optog, og alle andre i det samme lokale.");
        sb.AppendLine($"  {Derfra} = de øvrige deltagere, som de lød i højttaleren.");
        sb.AppendLine();

        // SPROGET PR. SPOR SKAL STAA DER.
        //
        // De to sider taler ikke noedvendigvis samme sprog - et dansk-norsk
        // moede er to sprog i én samtale, og hvert spor er skrevet ud gennem
        // sin egen model. Staar det ikke, kan hverken laeseren eller
        // sprogmodellen se, hvorfor den ene side lyder anderledes.
        //
        // Det er ogsaa det eneste sted, en forkert sprogindstilling kan
        // OPDAGES: staar der "engelsk" ud for et dansk moede, er det den
        // indstilling, der skal rettes.
        if (sprogHerfra is not null || sprogDerfra is not null)
        {
            sb.AppendLine($"Skrevet ud på: {Herfra} = {sprogHerfra ?? "ukendt"}, " +
                          $"{Derfra} = {sprogDerfra ?? "ukendt"}.");
            sb.AppendLine("Er et af sprogene forkert, rettes det under Indstillinger → Lyd.");
            sb.AppendLine();
        }
        sb.AppendLine("MÆRKATERNE ER IKKE NAVNE. De siger, hvilken side af mødet der talte, ikke");
        sb.AppendLine("hvem. Navnene skal findes i det, der bliver sagt — typisk i navnerunden");
        sb.AppendLine($"først i mødet. Der kan være flere personer bag både {Herfra} og {Derfra}.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        string? sidsteSpor = null;
        var afsnit = new StringBuilder();
        long afsnitStart = 0;

        void Luk()
        {
            if (afsnit.Length == 0) return;
            sb.AppendLine($"[{Tid(afsnitStart)}] {sidsteSpor}: {afsnit.ToString().Trim()}");
            sb.AppendLine();
            afsnit.Clear();
        }

        foreach (var r in replikker)
        {
            if (r.Spor != sidsteSpor)
            {
                Luk();
                sidsteSpor = r.Spor;
                afsnitStart = r.FraMs;
            }

            afsnit.Append(r.Tekst).Append(' ');
        }

        Luk();
        return sb.ToString().TrimEnd() + "\n";
    }

    private static string Tid(long ms) =>
        TimeSpan.FromMilliseconds(ms).ToString(@"hh\:mm\:ss");

    /// <summary>
    /// Hele vejen: to json-filer ind, én udskrift ud.
    ///
    /// Findes loopback-sporet ikke — et fysisk møde — er der intet at flette,
    /// og der returneres null. Så bliver mikrofonens egen udskrift stående som
    /// den, den var, og intet ændrer sig for de møder.
    /// </summary>
    public static string? Flet(string mikrofonJson, string? loopbackJson,
                               string? sprogHerfra = null, string? sprogDerfra = null)
    {
        if (loopbackJson is null || !File.Exists(loopbackJson)) return null;

        var mik = Laes(mikrofonJson, Herfra);
        var loop = Laes(loopbackJson, Derfra);

        if (loop.Count == 0) return null;

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
        // En "samtale" med mærkater, hvor kun den ene side siger noget, ville
        // love en opdeling, der ikke findes.
        if (loopRen.Count == 0) return null;

        return Skriv(FjernEkko(mikRen, loopRen)
                     .Concat(loopRen)
                     .OrderBy(r => r.FraMs)
                     .ThenBy(r => r.Spor)
                     .ToList(),
                     sprogHerfra, sprogDerfra);
    }
}
