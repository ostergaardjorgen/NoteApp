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
/// <param name="Vaegt">
/// Hvor godt det bedste sted i kilden svarer på spørgsmålet — hvor mange af
/// søgeordene der står tæt på hinanden dér.
///
/// Den findes, fordi ANTALLET af steder ikke er det samme som relevans. En
/// udskrift, hvor «indigo» står tolv gange og «access» én gang i den anden
/// ende, har tolv steder og intet svar. En, hvor de to står i den samme
/// sætning, har ét sted og er dét, man ledte efter.
/// </param>
public sealed record Fund(
    Fundtype Slags,
    string Kilde,
    string Overskrift,
    DateTimeOffset Tid,
    IReadOnlyList<Traef> Traef,
    int Vaegt = 0)
{
    /// <summary>
    /// Hvor mange af søgeordene der står tæt på hinanden det bedste sted.
    ///
    /// DEN SKAL KUNNE VISES. Søger man på to ord og får steder, hvor kun det
    /// ene står, ser det ud som en fejl i søgningen — og det er det ikke:
    /// begge ord ER i optagelsen, de bliver bare aldrig sagt i den samme
    /// sammenhæng. Målt på et rigtigt webinar 21-08-2026 med «access indigo»:
    /// begge ord står der mange gange, og de står aldrig inden for hundrede
    /// tegn af hinanden.
    ///
    /// Uden det her tal kan skærmen ikke sige forskel på «her er svaret» og
    /// «de to ting hører ikke sammen i den her optagelse».
    /// </summary>
    public int OrdSammen => Vaegt / 10;
}

/// <summary>
/// Hvad søgningen overhovedet skal lede i.
///
/// HVORFOR DET IKKE ER NOK AT SØGE PÅ ORD
///
/// Efter to hundrede optagelser står det samme fagord i halvdelen af dem.
/// «Access review» i kundens møder er ét spørgsmål; det samme ord i en stak
/// engelske webinarer er et andet. Uden en afgrænsning er svaret en liste,
/// man ikke kan overskue — og så er arkivet reelt lige så tabt som uden en
/// søgning.
///
/// TRE FELTER, OG DE ER ALLE TRE VALGT, FØR OPTAGELSEN BEGYNDTE. Mappen og
/// mødetypen vælges i opstartsdialogen; sproget gør de også. Det er dét, der
/// gør filtrene brugbare fra dag ét — de er ikke noget, man skal gå tilbage
/// og udfylde bagefter.
///
/// Null eller tom betyder «alle».
/// </summary>
/// <param name="Fra">Tidligste tidspunkt, der tælles med. Null = ingen grænse.</param>
/// <param name="Til">
/// Seneste tidspunkt. Null = ingen grænse.
///
/// Den er TIL OG MED den valgte dag. Vælger man 21. august i begge ender,
/// forventer man dagens optagelser — ikke en tom liste, fordi mødet lå klokken
/// ti og grænsen gik ved midnat. Datovælgeren giver en dato, ikke et
/// klokkeslæt, og så skal dagen tælle hele vejen.
/// </param>
public sealed record Soegefilter(string? Sprog = null, string? Moedetype = null,
                                 string? Mappe = null,
                                 DateTimeOffset? Fra = null, DateTimeOffset? Til = null)
{
    private static bool Ens(string? a, string? b) =>
        string.IsNullOrWhiteSpace(a) || (b is not null && a.Equals(b, StringComparison.CurrentCultureIgnoreCase));

    private bool IPerioden(DateTimeOffset t) =>
        (Fra is null || t >= Fra) && (Til is null || t <= Til);

    /// <summary>
    /// De faste perioder.
    ///
    /// HVORFOR DE ER DER, NÅR MAN OGSÅ KAN VÆLGE FRA OG TIL
    ///
    /// «Denne måned» er ét klik. Den samme afgrænsning med to datovælgere er
    /// fire — og man skal vide, hvilken dato måneden begyndte. De faste
    /// perioder er ikke en genvej til det svære; de er det, man spørger om ni
    /// gange ud af ti.
    ///
    /// Ugen begynder mandag. Det gør den i Danmark, og et filter, der siger
    /// «denne uge» og tæller fra søndag, giver et forkert svar én dag om ugen —
    /// den dag, hvor nogen faktisk kigger efter.
    /// </summary>
    public static (DateTimeOffset? Fra, DateTimeOffset? Til) Periode(string navn)
    {
        var nu = DateTime.Now;
        var idag = nu.Date;

        return navn switch
        {
            "idag" => (Lokal(idag), null),
            "uge" => (Lokal(idag.AddDays(-(((int)nu.DayOfWeek + 6) % 7))), null),
            "maaned" => (Lokal(new DateTime(nu.Year, nu.Month, 1)), null),
            "aar" => (Lokal(new DateTime(nu.Year, 1, 1)), null),
            _ => (null, null)
        };
    }

    /// <summary>
    /// En dato med DEN DAGS egen tidsforskel — ikke dagens i dag.
    ///
    /// MÅLT 21-08-2026: «i år» begyndte 31-12-2025 kl. 23:00. Grunden var, at
    /// 1. januar blev bygget med sommertidens forskel på to timer, mens
    /// januar er på én. Fejlen er en time og ét døgn for meget i hver ende af
    /// en sommertidsgrænse — lille nok til aldrig at blive opdaget, og stor
    /// nok til at et møde 31. december dukker op under «i år».
    ///
    /// Der spørges derfor tidszonen om, hvad forskellen var PÅ DEN DATO.
    /// </summary>
    public static DateTimeOffset Lokal(DateTime dag) =>
        new(dag, TimeZoneInfo.Local.GetUtcOffset(dag));

    /// <summary>Til og med hele den valgte dag.</summary>
    public static DateTimeOffset SlutAfDagen(DateTime d) =>
        Lokal(d.Date).AddDays(1).AddSeconds(-1);

    /// <summary>
    /// Sproget på en optagelse.
    ///
    /// Whispers eget svar vinder, fordi det er dét, teksten FAKTISK blev
    /// skrevet ud på. Er den ikke skrevet ud endnu, gælder det, brugeren
    /// valgte ved start — og på et webinar er det højttalersporet, fordi der
    /// ikke er noget mikrofonspor.
    /// </summary>
    public static string? SprogPaa(MeetingMetadata? m) =>
        m?.Language ?? m?.ValgtSprogLoop ?? m?.ValgtSprogMik;

    public bool Passer(MeetingMetadata? m)
    {
        if (m is null) return Erbart();

        return Ens(Sprog, SprogPaa(m))
               && Ens(Moedetype, m.Moedetype)
               && Ens(Mappe, m.Mappe)
               && IPerioden(m.StartedAt);
    }

    /// <summary>
    /// Et dokument bedømmes på sine EGNE oplysninger, hvor det har nogen, og
    /// ellers på den optagelse, det er lavet af.
    ///
    /// Mødetypen og mappen står på dokumentet selv og kan være ændret siden;
    /// sproget står kun på optagelsen.
    /// </summary>
    public bool PasserDokument(Documents.DocumentInfo d, MeetingMetadata? kilde)
    {
        if (Tomt) return true;

        var type = string.IsNullOrWhiteSpace(d.Template) ? kilde?.Moedetype : d.Template;
        var mappe = string.IsNullOrWhiteSpace(d.Mappe) ? kilde?.Mappe : d.Mappe;

        // DATOEN ER MØDETS, IKKE DOKUMENTETS.
        //
        // Man spørger om «møderne i denne måned». Et referat, der blev skrevet
        // i dag af et møde fra maj, hører til i maj — ellers dukker maj-mødet
        // op under august gennem bagdøren.
        var tid = kilde?.StartedAt ?? d.Created;

        return Ens(Sprog, SprogPaa(kilde)) && Ens(Moedetype, type) && Ens(Mappe, mappe)
               && IPerioden(tid);
    }

    public bool Tomt => string.IsNullOrWhiteSpace(Sprog)
                        && string.IsNullOrWhiteSpace(Moedetype)
                        && string.IsNullOrWhiteSpace(Mappe)
                        && Fra is null && Til is null;

    /// <summary>
    /// En optagelse uden oplysninger — en fra før felterne fandtes, eller en
    /// fra konsolprogrammet.
    ///
    /// Den kommer kun med, når der ikke er filtreret. Ellers ville et filter
    /// på «Webinarer» give en liste med alt det, appen ikke ved noget om — og
    /// et filter, der ikke filtrerer, er værre end intet filter.
    /// </summary>
    private bool Erbart() => Tomt;
}

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
    private const int Omkring = 100;

    /// <summary>
    /// Så mange steder tages med pr. kilde.
    ///
    /// Søger man på «og», er der tusind. Listen skal kunne læses, ikke være
    /// udtømmende — og står ordet flere gange end det her, er det ikke det
    /// enkelte sted, man leder efter.
    /// </summary>
    private const int MaksPrKilde = 40;

    /// <summary>
    /// Hvor langt to ord må stå fra hinanden for at høre sammen.
    ///
    /// HUNDREDE TEGN TIL HVER SIDE — omtrent en sætning eller to. Står
    /// «access» og «Indigo» inden for det, handler stedet om begge dele. Står
    /// de tyve minutter fra hinanden i den samme udskrift, er det to
    /// forskellige emner, der tilfældigvis blev nævnt i det samme møde.
    ///
    /// Det er dét, der skiller et svar fra et sammentræf.
    ///
    /// DEN ER DEN SAMME SOM UDDRAGETS BREDDE, OG DET SKAL DEN BLIVE VED MED.
    /// Var nærheden større, ville et ord kunne tælle som «tæt på» og alligevel
    /// være klippet væk af uddraget — og så stod der en række, der lovede to
    /// ord og viste ét. Målt med 100 mod 80: to af de fem øverste rækker på
    /// «identity governance» viste kun det ene ord.
    /// </summary>
    private const int Naerhed = Omkring;

    /// <summary>
    /// Deler spørgsmålet i søgeord — og holder sammen på det, der står i
    /// anførselstegn.
    ///
    /// «"access review" indigo» er to søgeord: sætningen «access review», som
    /// skal stå ordret, og ordet «indigo». Uden anførselstegnene ville
    /// «access» og «review» blive to krav, der kunne opfyldes hver for sig i
    /// hver sin ende af et møde.
    /// </summary>
    public static List<string> Del(string spoergsmaal)
    {
        var ud = new List<string>();
        var sb = new StringBuilder();
        var iCitat = false;

        foreach (var c in spoergsmaal)
        {
            if (c == '"')
            {
                iCitat = !iCitat;
                if (!iCitat && sb.Length > 0) { ud.Add(sb.ToString().Trim()); sb.Clear(); }
                continue;
            }

            if (c == ' ' && !iCitat)
            {
                if (sb.Length > 0) { ud.Add(sb.ToString().Trim()); sb.Clear(); }
                continue;
            }

            sb.Append(c);
        }

        if (sb.Length > 0) ud.Add(sb.ToString().Trim());

        return ud.Where(o => o.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// Finder alle steder, ordene optræder.
    ///
    /// ALLE ORD SKAL STÅ I DEN SAMME TEKST. Det er uændret — ét manglende ord
    /// betyder, at teksten ikke er den, der blev spurgt om.
    ///
    /// MEN STEDERNE FINDES NU PÅ ALLE ORDENE, IKKE KUN PÅ ÉT.
    ///
    /// Før blev stederne fundet på det SJÆLDNESTE ord, og de øvrige ord stod
    /// kun for at snævre ind. Det så rigtigt ud i teorien og var forkert på
    /// skærmen: søger man «access indigo», fik man otte steder, hvor der stod
    /// «Indigo» — og ikke ét af dem havde «access» i sig, selv om ordet stod i
    /// udskriften. Fundet 21-08-2026.
    ///
    /// Nu findes hvert sted, hvert af ordene står, og stederne rangordnes
    /// efter, HVOR MANGE af ordene der står tæt på. Et sted med begge ord slår
    /// et sted med ét — og det er dét, man leder efter, når man skriver to ord.
    /// </summary>
    public static List<Fund> Soeg(string spoergsmaal, CancellationToken ct = default) =>
        Soeg(spoergsmaal, new Soegefilter(), ct);

    /// <param name="filter">
    /// Hvad der overhovedet skal ledes i. Et tomt filter betyder alt.
    /// </param>
    public static List<Fund> Soeg(string spoergsmaal, Soegefilter filter,
                                  CancellationToken ct = default)
    {
        var ord = Del(spoergsmaal).ToArray();

        var fund = new List<Fund>();
        if (ord.Length == 0) return fund;

        // Moedernes oplysninger slaas op ÉN gang og genbruges til dokumenterne.
        // Et dokument kender ikke sit eget sprog — det staar paa den optagelse,
        // det er lavet af, og uden opslaget kunne et sprogfilter ikke gaelde
        // dokumenter overhovedet.
        var moeder = new Dictionary<string, MeetingMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var mappe in Moedemapper())
        {
            ct.ThrowIfCancellationRequested();

            var meta = MeetingStore.Load(mappe);
            var id = meta?.Id.ToString() ?? "";
            var titel = meta?.Title ?? Path.GetFileName(mappe);
            var tid = meta?.StartedAt ?? new DateTimeOffset(Directory.GetLastWriteTime(mappe));

            if (meta is not null && id.Length > 0) moeder[id] = meta;

            if (!filter.Passer(meta)) continue;

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

            // Et dokument arver optagelsens oplysninger, hvor det giver
            // mening. Dets egen mødetype og mappe vinder — de kan være
            // ændret, siden dokumentet blev lavet.
            moeder.TryGetValue(d.SourceMeetingId ?? "", out var kilde);

            if (!filter.PasserDokument(d, kilde)) continue;

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

        // DET BEDSTE SVAR FØRST, IKKE DET FLESTE.
        //
        // Her stod «flest steder først», og det var forkert, saa snart man
        // soeger paa to ting: en udskrift, hvor det ene ord staar tolv gange
        // og det andet én gang i den anden ende, laa oeverst — over den, hvor
        // de to stod i den samme saetning.
        //
        // Vaegten er, hvor mange af ordene der staar taet paa hinanden det
        // bedste sted. Er den ens, afgoer antallet, og derefter datoen.
        // ============ KILDEN SLÅR GENFORTÆLLINGEN ============
        //
        // Staar ordet lige godt i en udskrift og i et dokument, der er lavet
        // AF den udskrift, skal udskriften staa oeverst. Dokumentet er et
        // referat af det, der blev sagt; udskriften ER det, der blev sagt.
        //
        // Maalt 21-08-2026 paa de tyve proever: «crowdstrike» laa paa
        // tredjepladsen, fordi to referater af det samme moede laa foran
        // moedet selv. Med den her linje: foerstepladsen. Ogsaa «webinar»
        // rykkede fra fjerde til anden.
        //
        // Raekkefoelgen ligger i Fundtype: udskrift, note, dokument. Noten er
        // brugerens egne ord under moedet og staar derfor foran et dokument,
        // en model har skrevet bagefter.
        return fund
            .OrderByDescending(f => f.Vaegt)
            .ThenByDescending(f => f.Traef.Count)
            .ThenBy(f => f.Slags)
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

        // Hvert ord for sig: hvor staar det henne? Listerne er sorterede af
        // sig selv, fordi der soeges forfra.
        var steder = ord.Select(o => Alle(tekst, o)).ToArray();

        // ALLE ord skal staa der. Ét manglende ord betyder, at teksten ikke er
        // den, der blev spurgt om.
        if (steder.Any(s => s.Count == 0)) return;

        // ============ HVERT STED VEJES EFTER, HVOR MANGE ORD DER ER TÆT PÅ ============
        //
        // Et sted, hvor baade «access» og «indigo» staar inden for en sætning
        // eller to, er et svar. Et sted, hvor kun det ene staar, er en
        // omtale. Forskellen skal kunne ses paa raekkefoelgen, ellers er det
        // tilfaeldigt, hvad man faar oejnene op for foerst.
        var vejet = new List<(int Position, int Laengde, int Vaegt, int Ord)>();

        for (var w = 0; w < ord.Length; w++)
        foreach (var p in steder[w])
        {
            var naer = 0;

            for (var a = 0; a < ord.Length; a++)
                if (Findes(steder[a], p - Naerhed, p + ord[w].Length + Naerhed))
                    naer++;

            // Et helt ord vejer tungere end en stump inde i et andet ord.
            // «access» i «accessories» er ikke det, nogen leder efter — men
            // det skal stadig kunne findes, saa det er en vaegt og ikke et
            // filter. Sammensatte ord paa dansk lever af det samme.
            var helt = ErHeltOrd(tekst, p, ord[w].Length) ? 1 : 0;

            vejet.Add((p, ord[w].Length, naer * 10 + helt, w));
        }

        // ============ TUNGEST FØRST — OG ORDENE PÅ SKIFT ============
        //
        // Ved lige vaegt skiftes der mellem ordene. Uden det ville listen ved
        // en soegning paa to ting, der aldrig staar sammen, vise det ene ord
        // hele vejen ned: «access» staar 27 steder og «indigo» 8, saa de
        // foerste mange raekker blev «access». Maalt paa et rigtigt webinar
        // 21-08-2026, hvor de to ord aldrig kommer naermere end 238 tegn paa
        // hinanden.
        //
        // Man har spurgt om begge dele. Saa skal man kunne se begge dele.
        // ============ ET STED SKAL HAVE ALLE ORDENE ============
        //
        // HVERT ORD, MAN SKRIVER, SKAL SNÆVRE IND. Det er hele grunden til at
        // skrive et ord mere.
        //
        // To udgaver før denne var forkerte, og begge blev set på skærmen
        // 21-08-2026:
        //
        //   1. Stederne blev fundet på ÉT af ordene. «access indigo» gav otte
        //      steder med Indigo og ikke ét med access.
        //   2. Så blev de bedste steder vist — men «bedst» kunne stadig være
        //      to ud af tre. «omada espen ibm» viste række efter række med
        //      Omada og Espen, hvor IBM ikke var med.
        //
        // Nu skal ALLE ordene stå inden for det samme vindue. Er der ingen
        // steder, er der intet fund — og så siger skærmen HVORFOR frem for at
        // vise noget, der ligner et svar.
        //
        // Et tomt svar med en forklaring er bedre end en fyldt liste, man skal
        // læse for at opdage, at det sidste ord blev ignoreret.
        var idet = vejet.Where(v => v.Vaegt / 10 == ord.Length).ToList();
        if (idet.Count == 0) return;

        var traef = new List<Traef>();
        var taget = new List<int>();

        foreach (var v in idet
                     .GroupBy(v => v.Vaegt)
                     .OrderByDescending(g => g.Key)
                     .SelectMany(g => PaaSkift(g, ord.Length)))
        {
            if (traef.Count >= MaksPrKilde) break;

            // TO STEDER, DER OVERLAPPER, ER ÉT STED. Uden det her ville
            // «access indigo» i den samme saetning give to raekker med
            // naesten samme uddrag — og saa fylder det samme svar to gange.
            if (taget.Any(t => Math.Abs(t - v.Position) < Omkring)) continue;

            taget.Add(v.Position);
            traef.Add(new Traef(v.Position, Uddrag(tekst, v.Position, v.Laengde)));
        }

        if (traef.Count > 0)
            fund.Add(new Fund(slags, kilde, overskrift, tid, traef,
                              vejet.Count == 0 ? 0 : vejet.Max(v => v.Vaegt)));
    }

    /// <summary>
    /// Hvor mange steder hvert ord står — hvert ord for sig.
    ///
    /// BRUGES, NÅR DER IKKE ER NOGET FUND. Uden den kan skærmen kun sige
    /// «ingen fund», og så er man lige vidt: står ordet der slet ikke, eller
    /// står det bare aldrig sammen med de andre? Det er to helt forskellige
    /// svar, og kun det ene betyder, at man skrev forkert.
    ///
    /// Der tælles på tværs af alle kilder, som søgningen selv læser dem.
    /// </summary>
    public static List<(string Ord, int Steder, int Kilder)> Enkeltvis(
        string spoergsmaal, Soegefilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new Soegefilter();

        return Del(spoergsmaal)
            .Select(o =>
            {
                var f = Soeg(o, filter, ct);
                return (o, f.Sum(x => x.Traef.Count), f.Count);
            })
            .ToList();
    }

    /// <summary>
    /// Tager stederne på skift mellem søgeordene: først et sted for ord 1, så
    /// et for ord 2, og forfra.
    ///
    /// Er ét af ordene brugt op, fortsætter de øvrige. Rækkefølgen inden for
    /// hvert ord er den, det blev sagt i.
    /// </summary>
    private static IEnumerable<(int Position, int Laengde, int Vaegt, int Ord)> PaaSkift(
        IEnumerable<(int Position, int Laengde, int Vaegt, int Ord)> steder, int antalOrd)
    {
        var koeer = Enumerable.Range(0, antalOrd)
            .Select(w => steder.Where(s => s.Ord == w).OrderBy(s => s.Position).ToList())
            .ToList();

        for (var i = 0; koeer.Any(k => i < k.Count); i++)
        foreach (var k in koeer)
            if (i < k.Count) yield return k[i];
    }

    /// <summary>Alle steder, ordet står — forfra, så listen er sorteret.</summary>
    private static List<int> Alle(string tekst, string ord)
    {
        var ud = new List<int>();
        var i = 0;

        while ((i = tekst.IndexOf(ord, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            ud.Add(i);
            i += ord.Length;

            // Et loft. Soeger man paa «og» i hundrede moeder, er der ikke
            // noget svar at finde alligevel, og listen maa ikke koste et
            // sekund pr. kilde.
            if (ud.Count >= 2000) break;
        }

        return ud;
    }

    /// <summary>
    /// Er der en forekomst i intervallet? Listen er sorteret, så der ledes
    /// binært — ellers ville hvert sted skulle sammenlignes med hvert andet,
    /// og en times udskrift har tusinder af dem.
    /// </summary>
    private static bool Findes(List<int> sorteret, int fra, int til)
    {
        var lo = 0;
        var hi = sorteret.Count - 1;

        while (lo <= hi)
        {
            var m = (lo + hi) / 2;

            if (sorteret[m] < fra) lo = m + 1;
            else if (sorteret[m] > til) hi = m - 1;
            else return true;
        }

        return false;
    }

    /// <summary>
    /// Står ordet alene — altså ikke inde i et længere ord?
    ///
    /// Der ses på tegnet før og efter. Er begge noget andet end bogstaver og
    /// tal, er det et helt ord.
    /// </summary>
    private static bool ErHeltOrd(string tekst, int position, int laengde)
    {
        var foer = position == 0 || !char.IsLetterOrDigit(tekst[position - 1]);
        var efter = position + laengde >= tekst.Length
                    || !char.IsLetterOrDigit(tekst[position + laengde]);

        return foer && efter;
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
