using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Én afsendelse ud af maskinen — hvad der gik, hvorhen, hvornår, og hvad det
/// kostede.
/// </summary>
public sealed record Kvittering
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public DateTimeOffset Tidspunkt { get; init; } = DateTimeOffset.Now;

    /// <summary>Den fulde adresse. Ikke «Mistral» — den adresse, der blev kaldt.</summary>
    public string Endepunkt { get; init; } = "";

    public string Model { get; init; } = "";
    public string Skabelon { get; init; } = "";

    /// <summary>Mødets id, når afsendelsen hørte til et møde. Tom ellers.</summary>
    public string Kilde { get; init; } = "";

    public string KildeTitel { get; init; } = "";

    /// <summary>Hvor mange tegn der blev sendt. Selve teksten gemmes IKKE.</summary>
    public int Tegn { get; init; }

    /// <summary>
    /// SHA-256 af de nøjagtige bytes, der blev sendt.
    ///
    /// Det er dét, der gør kvitteringen til et bevis frem for en påstand. Man
    /// kan tage den udskrift, man mener blev sendt, bygge anmodningen igen og
    /// se, om summen passer. Gør den ikke, blev der sendt noget andet.
    /// </summary>
    public string Sum { get; init; } = "";

    public int TokensInd { get; init; }
    public int TokensUd { get; init; }
    /// <summary>
    /// Prisen i EURO.
    ///
    /// Kontoen hos Mistral gøres op i euro — set på deres egen konsol, hvor
    /// både forbrug, den inkluderede mængde og forbrugsloftet står i EUR.
    /// Hed før PrisUsd, og det var forkert: en kvittering skal kunne holdes
    /// op mod en faktura, og står der dollar det ene sted og euro det andet,
    /// duer den ikke til netop det, den findes for.
    /// </summary>
    public decimal PrisEur { get; init; }
    public double Sekunder { get; init; }

    /// <summary>
    /// Gik det igennem? En fejlet afsendelse er STADIG en afsendelse — data
    /// forlod maskinen, uanset hvad der kom tilbage. Derfor skrives
    /// kvitteringen også, når kaldet fejler.
    /// </summary>
    public bool Lykkedes { get; init; } = true;

    public string Fejl { get; init; } = "";
}

/// <summary>
/// Kvitteringer for alt, hvad der har forladt maskinen.
///
/// HVORFOR DEN FINDES
///
/// Appen kan i forvejen sige, hvad den plejer at gøre: lyden bliver, teksten
/// går til Mistral i Europa. Det er en beskrivelse af koden.
///
/// En revision spørger om noget andet: hvad skete der den 14. august? Og dét
/// kan en beskrivelse ikke svare på. Kvitteringen kan: tidspunkt, adresse,
/// model, antal tegn, en kontrolsum af de nøjagtige bytes, og hvad der kom
/// tilbage.
///
/// Ingen andre laver det, fordi alle andre er sky-først og derfor ikke kan.
/// Her kan det lade sig gøre, fordi der findes præcis ét sted, data går ud.
///
/// HVAD DER IKKE GEMMES
///
/// Selve teksten. Den ligger allerede ved mødet, og en kopi mere ville være
/// endnu et sted, den kunne slippe ud fra. I stedet gemmes en kontrolsum, der
/// kan holdes op mod originalen.
///
/// Prisen for det valg er ærlig at nævne: er udskriften rettet bagefter,
/// passer summen ikke længere — og så kan man se, AT den er ændret, men ikke
/// hvad der stod.
/// </summary>
public static class Kvitteringer
{
    public static string Directory => Path.Combine(UserDataPaths.Root, "kvitteringer");

    /// <summary>
    /// Én fil pr. måned. Filen er JSONL — én kvittering pr. linje — så den kan
    /// læses af et regneark, af et script og af et menneske, uden at appen
    /// skal være installeret.
    /// </summary>
    private static string Fil(DateTimeOffset t) =>
        Path.Combine(Directory, $"{t:yyyy-MM}.jsonl");

    private static readonly object Laas = new();

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Kontrolsummen af det, der bliver sendt.</summary>
    public static string Kontrolsum(string krop) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(krop))).ToLowerInvariant();

    /// <summary>
    /// Skriver en kvittering.
    ///
    /// Må ALDRIG kaste. En fejl i bogføringen må ikke vælte det, der blev
    /// bogført — men den skal heller ikke skjules, så den skrives til
    /// historikken i stedet.
    /// </summary>
    public static void Skriv(Kvittering k)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            lock (Laas)
                File.AppendAllText(Fil(k.Tidspunkt),
                                   JsonSerializer.Serialize(k, Format) + "\n",
                                   new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Historik.Skriv(HaendelseType.Andet, "Kvitteringen kunne ikke skrives",
                $"Afsendelsen skete, men den blev ikke bogført: {ex.Message}",
                Udfald.SeEfter);
        }
    }

    /// <summary>
    /// Alle kvitteringer, nyeste først.
    ///
    /// Der læses fra filerne hver gang. Se <see cref="Soegning"/> for samme
    /// valg og samme begrundelse: en kopi, der kan blive uenig med
    /// virkeligheden, er værre end en langsom læsning — og her er det tilmed
    /// hele pointen, at det, der vises, ER det, der står i filerne.
    /// </summary>
    public static List<Kvittering> Laes(int maks = 500)
    {
        var ud = new List<Kvittering>();
        if (!System.IO.Directory.Exists(Directory)) return ud;

        foreach (var fil in System.IO.Directory.GetFiles(Directory, "*.jsonl")
                     .OrderByDescending(f => f))
        {
            foreach (var linje in Linjer(fil).Reverse())
            {
                if (ud.Count >= maks) return ud;

                try
                {
                    if (JsonSerializer.Deserialize<Kvittering>(linje) is { } k) ud.Add(k);
                }
                catch (JsonException)
                {
                    // En ulaeselig linje maa ikke skjule de oevrige.
                }
            }
        }

        return ud;
    }

    private static string[] Linjer(string fil)
    {
        try { return File.ReadAllLines(fil, Encoding.UTF8).Where(l => l.Length > 0).ToArray(); }
        catch (IOException) { return Array.Empty<string>(); }
    }
}
