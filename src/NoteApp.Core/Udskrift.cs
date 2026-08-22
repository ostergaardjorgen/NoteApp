using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Én replik, som den står i udskriften — og som den kan rettes.
///
/// Modsat <see cref="Replik"/>, der er det, motoren leverede, er den her
/// foranderlig. Det er hele pointen: udskriften er ikke længere noget, man kun
/// kan læse.
/// </summary>
public sealed class Udskriftslinje
{
    public long FraMs { get; set; }
    public long TilMs { get; set; }

    /// <summary>HERFRA eller DERFRA. Tom, når mødet blev optaget på ét spor.</summary>
    public string Spor { get; set; } = "";

    public string Tekst { get; set; } = "";

    /// <summary>
    /// Er linjen rettet i hånden?
    ///
    /// Står her og ikke kun som en fornemmelse, fordi det afgør, hvad der må
    /// ske ved en ny transskription — og fordi det er værd at kunne se, hvad
    /// der er maskinens ord og hvad der er ens egne.
    /// </summary>
    public bool Rettet { get; set; }

    /// <summary>Maskinens oprindelige ord. Gemmes, første gang linjen rettes.</summary>
    public string? Oprindelig { get; set; }

    /// <summary>
    /// Hvilken STEMME på sporet der sagde det — «DERFRA#0», «DERFRA#1» og så
    /// videre. Null, når talergenkendelsen ikke er kørt, eller når ingen
    /// stemme kunne knyttes til replikken.
    ///
    /// Sporet siger hvilken SIDE af mødet der talte. Stemmen siger hvem af
    /// dem. De to er ikke det samme, og de kan ikke slås sammen: sporet er
    /// noget, appen ved med sikkerhed, fordi lyden kom ad to veje. Stemmen er
    /// noget, den har regnet sig frem til, og den kan tage fejl.
    /// </summary>
    public string? Stemme { get; set; }

    public string Tid => TimeSpan.FromMilliseconds(FraMs).ToString(@"hh\:mm\:ss");
}

/// <summary>
/// Udskriften af et møde — struktureret, så den kan rettes, navngives og vises
/// med tidsstempler.
///
/// HVORFOR DEN ER STRUKTURERET OG IKKE EN TEKSTFIL
///
/// Indtil nu var udskriften én tekstblok. Det rakte, så længe den kun skulle
/// læses og sendes videre. Skal den kunne rettes replik for replik, skal
/// appen vide, hvor den ene slutter og den næste begynder — og hvornår hver
/// enkelt blev sagt.
///
/// TALERNAVNE BAGES IKKE IND
///
/// Sporet står som HERFRA eller DERFRA. Navnene ligger i mødets egne
/// oplysninger og sættes på, når teksten vises. Det er det samme princip, der
/// bærer resten af appen: gem id'et, slå navnet op. En omdøbning af en
/// deltager skal ikke kræve, at udskriften skrives om.
///
/// RETTELSER OVERLEVER EN NY TRANSSKRIPTION
///
/// Den rettede udgave ligger i sin egen fil. En ny kørsel skriver den
/// maskingenererede — aldrig den rettede. Bliver de to uenige, er det et
/// spørgsmål til brugeren, ikke noget appen afgør selv.
///
/// Den regel findes, fordi vi har brudt den før: mekanismen, der anvendte
/// lærte rettelser på udskrifter, ville have omskrevet enhver dansk udskrift
/// med ordet «eller» i. Den nåede kun at lade være, fordi der tilfældigvis
/// ikke lå filer at røre.
/// </summary>
public sealed class Udskrift
{
    public List<Udskriftslinje> Linjer { get; init; } = new();

    /// <summary>Er der rettet i den?</summary>
    public bool ErRettet => Linjer.Any(l => l.Rettet);

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ------------------------------------------------------------- filerne

    /// <summary>Maskinens udgave. Skrives af transskriptionen, læses aldrig som facit.</summary>
    public static string MaskinSti(string mappe, string model) =>
        Path.Combine(mappe, $"udskrift_{model}.json");

    /// <summary>
    /// Den rettede udgave. Findes kun, når nogen har rettet noget.
    ///
    /// Navnet indeholder ikke modellen: rettelserne hører til MØDET, ikke til
    /// den model, der tilfældigvis skrev det ud. Skifter man model, skal
    /// rettelserne ikke forsvinde.
    /// </summary>
    public static string RettetSti(string mappe) =>
        Path.Combine(mappe, "transkription.rettet.json");

    // -------------------------------------------------------------- læsning

    /// <summary>
    /// Udskriften, som den gælder: den rettede, hvis den findes, ellers
    /// maskinens.
    /// </summary>
    public static Udskrift? Hent(string mappe, string model)
    {
        return Laes(RettetSti(mappe)) ?? Laes(MaskinSti(mappe, model));
    }

    /// <summary>
    /// Udskriften — og hvis der ikke findes en struktureret, bygges den af
    /// det, motoren efterlod.
    ///
    /// HVORFOR DEN BYGGER FREM FOR AT GIVE OP
    ///
    /// Møder, der blev skrevet ud før redigeringen fandtes, har ingen
    /// struktureret fil. Uden det her ville de være låst som læsestof, indtil
    /// nogen brugte tyve minutter på at køre dem om — for en funktion, der
    /// ikke handler om at skrive dem ud igen.
    ///
    /// Råmaterialet ligger der stadig: motorens json-filer med tidsstempler.
    /// Der er intet at køre, kun noget at læse.
    /// </summary>
    public static Udskrift? HentEllerByg(string mappe, string model)
    {
        if (Hent(mappe, model) is { } fundet) return fundet;

        var mik = Path.Combine(mappe, $"mikrofon_{model}.json");
        if (!File.Exists(mik)) return null;

        var loop = Path.Combine(mappe, $"loopback_{model}.json");

        var bygget = Af(Samtale.Flet(mik, File.Exists(loop) ? loop : null));
        if (bygget.Linjer.Count == 0) return null;

        try { bygget.GemMaskin(mappe, model); } catch (IOException) { }

        return bygget;
    }

    /// <summary>
    /// Er der rettet noget i udskriften?
    ///
    /// DET ER IKKE NOK, AT FILEN FINDES.
    ///
    /// Editoren gemmer, når man har rørt teksten — også når man kun har
    /// rullet igennem og lukket igen. Så ligger der en rettet fil uden en
    /// eneste rettelse i.
    ///
    /// Blev der kun set på filen, ville appen advare «Du har rettet i
    /// udskriften», hver gang man ville skrive ud igen, af noget der aldrig
    /// blev rettet. En advarsel, der er forkert hver anden gang, bliver til en
    /// advarsel, man klikker væk uden at læse — og så virker den heller ikke
    /// den gang, den har ret.
    ///
    /// Der læses derfor efter, om en linje faktisk er mærket som rettet.
    /// </summary>
    public static bool HarRettelser(string mappe) =>
        Laes(RettetSti(mappe)) is { ErRettet: true };

    /// <summary>
    /// Den rettede udgave, hvis filen findes — også når den ikke indeholder
    /// nogen rettelser endnu.
    ///
    /// <see cref="HarRettelser"/> svarer på, om nogen HAR rettet noget.
    /// Det her svarer på, om der ligger en fil, som editoren vil læse. De to
    /// er ikke det samme, og forskellen betyder noget, når stemmerne skal
    /// skrives ind: sættes de kun på maskinens udgave, mens editoren læser
    /// den rettede, står navnene i en fil, ingen kigger i.
    /// </summary>
    public static Udskrift? HentRettet(string mappe) => Laes(RettetSti(mappe));

    private static Udskrift? Laes(string sti)
    {
        if (!File.Exists(sti)) return null;

        try
        {
            var linjer = JsonSerializer.Deserialize<List<Udskriftslinje>>(
                File.ReadAllText(sti, Encoding.UTF8));

            return linjer is null ? null : new Udskrift { Linjer = linjer };
        }
        catch (Exception)
        {
            // En ulaeselig fil maa ikke vaelte skaermen. Kalderen faar null og
            // falder tilbage paa den anden udgave.
            return null;
        }
    }

    // -------------------------------------------------------------- skrivning

    public void GemMaskin(string mappe, string model) => Gem(MaskinSti(mappe, model));

    public void GemRettet(string mappe) => Gem(RettetSti(mappe));

    private void Gem(string sti)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sti)!);

        // Skrives til en midlertidig fil og omdoebes. Gaar strømmen midt i en
        // skrivning, er den gamle udgave stadig hel - en halv udskrift er
        // vaerre end en gammel.
        var midlertidig = sti + ".part";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(Linjer, Format),
                          new UTF8Encoding(false));

        File.Move(midlertidig, sti, overwrite: true);
    }

    // -------------------------------------------------------------- visning

    /// <summary>
    /// Udskriften som almindelig tekst — det, dokumenter og søgning bruger.
    ///
    /// <paramref name="navne"/> er talernes navne pr. spor. Er der ingen, står
    /// mærkaterne som de er, og forklaringen øverst siger hvorfor.
    /// </summary>
    public string SomTekst(IReadOnlyDictionary<string, string>? navne = null)
    {
        var sb = new StringBuilder();

        var toSpor = Linjer.Any(l => l.Spor == Samtale.Herfra)
                     && Linjer.Any(l => l.Spor == Samtale.Derfra);

        if (toSpor) sb.Append(Samtale.Forklaring(navne));

        foreach (var l in Linjer)
        {
            var hvem = Navn(l, navne);

            sb.Append('[').Append(l.Tid).Append(']');
            if (hvem.Length > 0) sb.Append(' ').Append(hvem).Append(':');

            sb.Append(' ').AppendLine(l.Tekst.Trim());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// Navnet på et spor.
    ///
    /// Er der ikke sat et navn, bruges «Mig» og «Gæster» — ikke HERFRA og
    /// DERFRA. De to sidste er NØGLER i filerne og skal blive, hvor de er;
    /// men de siger ingenting til hverken et menneske eller en sprogmodel.
    /// «Gæster» siger samtidig det rigtige om, at der kan være flere.
    /// </summary>
    public static string Navn(string spor, IReadOnlyDictionary<string, string>? navne)
    {
        if (spor.Length == 0) return "";

        if (navne is not null && navne.TryGetValue(spor, out var n) && n.Length > 0) return n;

        return spor == Samtale.Derfra ? "Gæster" : "Mig";
    }

    /// <summary>
    /// Navnet på den, der sagde en bestemt replik.
    ///
    /// Er talergenkendelsen kørt, står der en STEMME på linjen, og så er det
    /// den, der tæller: «Gæst 2» siger mere end «Gæster». Er den ikke kørt,
    /// eller kunne replikken ikke knyttes til en stemme, falder den tilbage
    /// til sporet — det, appen ved med sikkerhed.
    ///
    /// Nøglen til et navn er stemmen selv («DERFRA#1»), så navne, man har sat
    /// i hånden, overlever en ny kørsel af udskriften.
    /// </summary>
    public static string Navn(Udskriftslinje linje, IReadOnlyDictionary<string, string>? navne)
    {
        if (linje.Stemme is not { Length: > 0 } stemme) return Navn(linje.Spor, navne);

        if (navne is not null && navne.TryGetValue(stemme, out var n) && n.Length > 0) return n;

        var nummer = stemme.LastIndexOf('#') is var i and >= 0
                     && int.TryParse(stemme[(i + 1)..], out var x) ? x + 1 : 1;

        return linje.Spor == Samtale.Derfra ? $"Gæst {nummer}" : $"Taler {nummer}";
    }

    /// <summary>
    /// Bygger udskriften af det, motoren leverede.
    ///
    /// SAMMENHÆNGENDE REPLIKKER FRA SAMME SPOR LÆGGES SAMMEN.
    ///
    /// Whisper skærer ved tredive sekunder, ikke ved talerskift. Uden det her
    /// ville udskriften være en mur af enkeltlinjer med et mærkat på hver — og
    /// som noget, der skal RETTES, ville den være ubrugelig: man ville sidde
    /// og rette en sætning, der er skåret over midt i to felter.
    ///
    /// Et afsnit svarer til «det, den ene side sagde, før den anden svarede».
    /// Det er den enhed, man tænker i, når man læser et referat igennem.
    ///
    /// MEN DER SKAL VÆRE EN ENDE PÅ ET AFSNIT.
    ///
    /// Reglen ovenfor holder, så længe der ER to sider, der skiftes til at
    /// tale. Er der kun ét spor — et webinar, et fysisk møde, en lydfil fra en
    /// telefon — skifter mærkatet aldrig, og så blev HELE optagelsen til ét
    /// afsnit. Målt på et webinar 21-08-2026: 305 segmenter blev til én replik
    /// på 22 minutter med ét tidsstempel, 00:00:00.
    ///
    /// Det koster mere end læsbarhed. Tidsstemplerne er vejen tilbage til
    /// lyden, søgningen viser et uddrag pr. afsnit, og talergenkendelsen
    /// sætter én stemme pr. afsnit. Med ét afsnit findes ingen af delene.
    ///
    /// TO GRÆNSER, OG DEN ENE ER MÅLT:
    ///
    ///   Pausen. Målt på de 305 segmenter er hullet mellem to segmenter 0 ms i
    ///   halvdelen af tilfældene og under 540 ms i ni ud af ti. Kun 18 huller
    ///   var over 800 ms. Et hul over den grænse er altså ikke et snit, Whisper
    ///   har lavet — det er en pause, der rent faktisk var der.
    ///
    ///   Længden. Taler nogen længe uden at trække vejret, skal der alligevel
    ///   begynde et nyt afsnit. Men SNITTET SKAL FALDE VED ET PUNKTUM. Første
    ///   forsøg skar stift ved fyrre sekunder, og så begyndte hvert andet
    ///   afsnit midt i en sætning — «is room for Q&amp;A at the end». Det læser
    ///   værre end den mur, det skulle afhjælpe, og det er umuligt at rette i.
    ///
    ///   Derfor: efter 25 sekunder brydes ved den FØRSTE sætning, der slutter.
    ///   Slutter ingen — én lang sætning uden punktum — brydes der alligevel
    ///   efter 60 sekunder. Den grænse er der kun for ikke at kunne løbe løbsk.
    ///
    /// Grænserne gælder BEGGE slags optagelser. En tre minutters monolog på et
    /// tosporsmøde er lige så ulæselig som på et webinar, og to regler for det
    /// samme ville før eller siden holde op med at ligne hinanden.
    /// </summary>
    private const long PauseMs = 800;

    private const long LangPauseMs = 2_000;

    private const long BrydEfterMs = 25_000;

    private const long SenestMs = 60_000;

    /// <summary>
    /// Slutter teksten en sætning? Kun dér må et afsnit brydes på længden.
    ///
    /// Whisper sætter tegn, og de er pålidelige nok til det her: et punktum,
    /// et spørgsmålstegn eller et udråbstegn i slutningen af et segment er et
    /// sætningsskel. Rammer den forkert, bliver et afsnit et par sekunder
    /// længere eller kortere — det koster ingenting.
    /// </summary>
    private static bool SlutterSaetning(string tekst)
    {
        var t = tekst.TrimEnd();
        return t.Length > 0 && (t[^1] == '.' || t[^1] == '!' || t[^1] == '?');
    }

    public static Udskrift Af(IEnumerable<Replik> replikker)
    {
        var ud = new List<Udskriftslinje>();

        foreach (var r in replikker)
        {
            var sidste = ud.Count > 0 ? ud[^1] : null;

            var laengde = sidste is null ? 0 : r.TilMs - sidste.FraMs;

            // Ogsaa en PAUSE skal falde ved et punktum. Maalt paa webinaret:
            // 13 af 44 afsnit begyndte med lille bogstav, fordi taleren trak
            // vejret midt i en saetning. En pause paa 800 ms er et aandedrag,
            // ikke et afsnitsskift. Kun en pause paa over to sekunder bryder
            // uanset hvad — saa er der sket noget andet end vejrtraekning.
            var pause = sidste is null ? 0 : r.FraMs - sidste.TilMs;
            var slutter = sidste is not null && SlutterSaetning(sidste.Tekst);

            var brydes = sidste is not null
                         && (pause >= LangPauseMs
                             || (pause >= PauseMs && slutter)
                             || (laengde >= BrydEfterMs && slutter)
                             || laengde >= SenestMs);

            if (sidste is not null && sidste.Spor == r.Spor && !brydes)
            {
                sidste.Tekst = (sidste.Tekst + " " + r.Tekst.Trim()).Trim();
                sidste.TilMs = r.TilMs;
                continue;
            }

            ud.Add(new Udskriftslinje
            {
                FraMs = r.FraMs,
                TilMs = r.TilMs,
                Spor = r.Spor,
                Tekst = r.Tekst.Trim()
            });
        }

        return new Udskrift { Linjer = ud };
    }
}
