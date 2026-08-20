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
        Path.Combine(mappe, "udskrift.rettet.json");

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
            var hvem = Navn(l.Spor, navne);

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
    /// </summary>
    public static Udskrift Af(IEnumerable<Replik> replikker)
    {
        var ud = new List<Udskriftslinje>();

        foreach (var r in replikker)
        {
            var sidste = ud.Count > 0 ? ud[^1] : null;

            if (sidste is not null && sidste.Spor == r.Spor)
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
