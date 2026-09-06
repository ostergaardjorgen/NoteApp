using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>Er projektet i gang, eller er det lagt væk?</summary>
public enum Projektstatus
{
    Aktiv,
    Arkiveret,
}

/// <summary>Hvad et medlemskab peger på.</summary>
public enum Medlemsslags
{
    Optagelse,
    Note,
    Dokument,
}

/// <summary>
/// Én ting, projektet peger på — ikke en kopi af den.
/// </summary>
/// <param name="Id">
/// Tingens eget id. Det er DEN, der åbner den — ikke en sti. En sti kan
/// flyttes og en titel omdøbes; id'et kan ikke.
/// </param>
public sealed record Projektmedlem(Medlemsslags Slags, string Id);

/// <summary>
/// Et projekt: et informationsfundament med en begyndelse og en ende.
/// </summary>
/// <remarks>
/// ============ ET PROJEKT PEGER, DET INDEHOLDER IKKE ============
///
/// Et webinar hører hjemme under Optagelser. Hører det også til et projekt,
/// er der to måder at sige det på, og kun den ene holder:
///
///   INDEHOLDER  optagelsen kopieres ind i projektet. Så findes den to
///               steder, og de to eksemplarer driver fra hinanden.
///   PEGER PÅ    optagelsen bliver, hvor den er, og projektet husker, at den
///               hører med. Ét hjem, mange medlemskaber.
///
/// Det er derfor <see cref="Medlemmer"/> kun er id'er. Og det er derfor,
/// arkivering er ufarlig: at lægge et projekt væk må aldrig skjule de møder,
/// det peger på — de hører også til andre sammenhænge.
///
/// DET NYE MATERIALE HAR PROJEKTET SOM HJEM. De dokumenter, du selv lægger
/// ind, ligger i projektets egen mappe under datamappen — så er de med i
/// sikkerhedskopien. Materiale, der allerede ligger i en cloud-mappe, bliver
/// liggende dér og bliver tilknyttet i stedet; se <see cref="Mapper"/>. Hvert
/// dokument har præcis ét hjem, og de to slags tilknytning blandes ikke.
///
/// INGEN DYBDE. Et projekt kan ikke ligge i et andet. Dybde er dér,
/// mappesystemer går galt.
/// </remarks>
public sealed class Projekt
{
    /// <summary>Nøglen. Skifter aldrig, heller ikke når navnet gør.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Navn { get; set; } = "";

    /// <summary>Hvad projektet handler om. Står under navnet på listen.</summary>
    public string Beskrivelse { get; set; } = "";

    public Projektstatus Status { get; set; } = Projektstatus.Aktiv;

    public DateTimeOffset Oprettet { get; set; } = DateTimeOffset.Now;

    /// <summary>Hvornår det blev lagt væk. Null så længe det er i gang.</summary>
    public DateTimeOffset? Arkiveret { get; set; }

    /// <summary>
    /// Mapper uden for datamappen, projektet læser med.
    /// </summary>
    /// <remarks>
    /// Typisk en cloud-mappe med studiemateriale eller kundens filer.
    /// Projektet EJER dem ikke: der læses, der skrives ikke, og de ligger
    /// stadig, hvor de lå, hvis projektet slettes.
    /// </remarks>
    public List<string> Mapper { get; set; } = new();

    /// <summary>Optagelser, noter og dokumenter, projektet peger på.</summary>
    public List<Projektmedlem> Medlemmer { get; set; } = new();

    /// <summary>
    /// Må projektets materiale indgå, når man søger på tværs?
    /// </summary>
    /// <remarks>
    /// Rent lokalt. Der sendes ingenting nogen steder, og derfor er den til
    /// som standard.
    /// </remarks>
    public bool MedISoegning { get; set; } = true;

    /// <summary>
    /// Må teksten sendes med, når der bygges et dokument?
    /// </summary>
    /// <remarks>
    /// ============ HER FORLADER MATERIALET MASKINEN ============
    ///
    /// Appen lover, at lyden bliver her, og at kun teksten sendes videre — og
    /// kun når man beder om et dokument. Et projekt fyldt med kundens filer
    /// eller dit studiemateriale er en ny slags tekst at sende, og det skal
    /// være et valg, man har truffet.
    ///
    /// Derfor to kontakter og ikke én, og derfor er DEN HER fra som standard.
    /// </remarks>
    public bool MaaSendesSomKilde { get; set; }

    /// <summary>Projektets egen mappe i datamappen.</summary>
    [JsonIgnore]
    public string Mappe => Projektlager.Mappe(Id);

    /// <summary>Dér, hvor projektets egne dokumenter ligger.</summary>
    [JsonIgnore]
    public string Dokumentmappe => Path.Combine(Mappe, "dokumenter");

    /// <summary>Peger projektet på den her ting?</summary>
    public bool Har(Medlemsslags slags, string id) =>
        Medlemmer.Any(m => m.Slags == slags &&
                           string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Tilføjer et medlemskab. Det samme to gange er ikke to.</summary>
    public bool Tilfoej(Medlemsslags slags, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || Har(slags, id)) return false;

        Medlemmer.Add(new Projektmedlem(slags, id));
        return true;
    }

    public bool Fjern(Medlemsslags slags, string id) =>
        Medlemmer.RemoveAll(m => m.Slags == slags &&
                                 string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
}

/// <summary>
/// Projekterne på disken.
/// </summary>
/// <remarks>
/// ÉN MAPPE PR. PROJEKT, med beskrivelsen i en fil og dokumenterne ved siden
/// af. Alternativet — én stor fil med alle projekter og dokumenterne et
/// tredje sted — betyder, at et projekt kan miste sine filer uden at nogen
/// opdager det. Her ligger de sammen, og en sikkerhedskopi af datamappen
/// tager hele projektet med.
/// </remarks>
public static class Projektlager
{
    /// <summary>Roden. Under den ligger én mappe pr. projekt.</summary>
    public static string Rod => Path.Combine(UserDataPaths.Root, "projekter");

    public static string Mappe(string id) => Path.Combine(Rod, id);

    private static string Fil(string id) => Path.Combine(Mappe(id), "projekt.json");

    private static readonly JsonSerializerOptions Opsaetning = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Alle projekter — aktive først, nyeste øverst.</summary>
    public static List<Projekt> Alle()
    {
        var ud = new List<Projekt>();

        try
        {
            if (!Directory.Exists(Rod)) return ud;

            foreach (var mappe in Directory.EnumerateDirectories(Rod))
            {
                var p = Laes(Path.Combine(mappe, "projekt.json"));
                if (p is not null) ud.Add(p);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ud.OrderBy(p => p.Status)
                 .ThenByDescending(p => p.Oprettet)
                 .ToList();
    }

    public static Projekt? Hent(string id) => Laes(Fil(id));

    private static Projekt? Laes(string sti)
    {
        try
        {
            if (!File.Exists(sti)) return null;

            var p = JsonSerializer.Deserialize<Projekt>(
                File.ReadAllText(sti, Encoding.UTF8), Opsaetning);

            // ET PROJEKT UDEN ID KAN IKKE GEMMES IGEN. Mappen ER id'et, saa
            // den er svaret, hvis filen har mistet sit.
            if (p is not null && string.IsNullOrWhiteSpace(p.Id))
                p.Id = Path.GetFileName(Path.GetDirectoryName(sti)!);

            return p;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gemmer projektet — gennem en midlertidig fil, så en afbrudt skrivning
    /// ikke efterlader et halvt projekt.
    /// </summary>
    public static void Gem(Projekt p)
    {
        Directory.CreateDirectory(Mappe(p.Id));
        Directory.CreateDirectory(p.Dokumentmappe);

        var maal = Fil(p.Id);
        var midlertidig = maal + ".ny";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(p, Opsaetning), new UTF8Encoding(false));
        File.Move(midlertidig, maal, overwrite: true);
    }

    /// <summary>Opretter et projekt og dets mapper.</summary>
    public static Projekt Opret(string navn, string beskrivelse = "")
    {
        var p = new Projekt
        {
            Navn = string.IsNullOrWhiteSpace(navn) ? "Nyt projekt" : navn.Trim(),
            Beskrivelse = beskrivelse.Trim(),
        };

        Gem(p);
        return p;
    }

    /// <summary>Lægger projektet væk. Intet slettes, og intet skjules andre steder.</summary>
    public static void Arkiver(Projekt p)
    {
        p.Status = Projektstatus.Arkiveret;
        p.Arkiveret = DateTimeOffset.Now;
        Gem(p);
    }

    /// <summary>Tager det frem igen.</summary>
    public static void Genaktiver(Projekt p)
    {
        p.Status = Projektstatus.Aktiv;
        p.Arkiveret = null;
        Gem(p);
    }

    /// <summary>
    /// Sletter projektet OG dets egne dokumenter.
    /// </summary>
    /// <remarks>
    /// DET, PROJEKTET KUN PEGEDE PÅ, RØRES IKKE. Optagelser, noter og
    /// dokumenter bliver, hvor de er, og de tilknyttede mapper uden for
    /// datamappen bliver liggende urørt. Kun det, projektet selv var hjem
    /// for, forsvinder — og det er derfor, arkivering findes: så man kan
    /// lægge noget væk uden at slette det.
    /// </remarks>
    public static void Slet(Projekt p)
    {
        try
        {
            if (Directory.Exists(Mappe(p.Id))) Directory.Delete(Mappe(p.Id), recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
