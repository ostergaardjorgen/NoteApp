using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>Hvad den her PC laver i et opsæt med flere maskiner.</summary>
public enum Maskinrolle
{
    /// <summary>
    /// Skriver ud — også for de andre. Den maskine, der har kræfterne.
    /// </summary>
    Arbejdsstation,

    /// <summary>
    /// Optager og læser, men sender det tunge arbejde videre.
    /// </summary>
    /// <remarks>
    /// DET ER EN STANDARD, IKKE EN LÅS. En bærbar kan stadig skrive ud med en
    /// mindre model, når man sidder i et tog og skal bruge det nu. Rollen
    /// afgør, hvad der sker af sig selv — ikke hvad man har lov til.
    /// </remarks>
    Let,
}

/// <summary>
/// Den her maskines identitet i et opsæt med flere PC'er.
/// </summary>
/// <remarks>
/// ============ DEN LIGGER HOS MASKINEN, IKKE I DATASÆTTET ============
///
/// Id, navn, rolle og stien til den fælles mappe hører til computeren og ikke
/// til de data, man tilfældigvis kigger på. Lå de i <c>indstillinger.json</c>,
/// ville demoen arve dem — og så ville to «maskiner» melde sig i den fælles
/// mappe med det samme id. Se <see cref="UserDataPaths.Maskinrod"/>.
///
/// ============ NØGLEPARRET ER MASKINENS BEVIS ============
///
/// Hver maskine laver ét nøglepar, første gang den skal dele noget. Den
/// offentlige halvdel lægges i den fælles mappe, så den anden maskine kan
/// finde den; den private bliver liggende her, beskyttet med
/// <see cref="Hemmelighed"/>, så den ikke kan læses af en kopi af mappen.
///
/// Det er dét, der gør, at en tredje maskine ikke bare kan melde sig ind:
/// efter parringen kender de to hinandens nøgle, og en ny nøgle på et kendt
/// navn er ikke det samme som den, der blev parret — se <see cref="Parring"/>.
/// </remarks>
public static class Maskinid
{
    private static string Mappe => Path.Combine(UserDataPaths.Maskinrod, "deling");

    private static string Fil => Path.Combine(Mappe, "maskin.json");

    /// <summary>Den private nøgle. Beskyttet med Windows-brugerens egen nøgle.</summary>
    private static string Noeglefil => Path.Combine(Mappe, "noegle.txt");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record Gemt(string Id, string Navn, Maskinrolle Rolle, string? Deltmappe);

    /// <summary>
    /// Der læses forfra hver gang.
    /// </summary>
    /// <remarks>
    /// Filen er på under hundrede bytes, og den kan ændre sig i det ene
    /// øjeblik, brugeren retter noget på skærmen. En huskeværdi ville skulle
    /// ryddes de rigtige steder, og det er den slags, der bliver forkert en
    /// gang om året uden at nogen opdager det.
    /// </remarks>
    private static Gemt Laes()
    {
        try
        {
            if (File.Exists(Fil))
            {
                var g = JsonSerializer.Deserialize<Gemt>(File.ReadAllText(Fil, Encoding.UTF8));
                if (g is not null && g.Id.Length > 0) return g;
            }
        }
        catch (Exception)
        {
            // En ulæselig fil skrives forfra nedenfor. Id'et skifter, og så
            // skal maskinen parres igen — det er synligt og til at forstå,
            // hvor en app, der ikke kan starte, ikke er.
        }

        var ny = new Gemt(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                          Environment.MachineName, Maskinrolle.Arbejdsstation, null);

        Skriv(ny);
        return ny;
    }

    private static void Skriv(Gemt g)
    {
        Directory.CreateDirectory(Mappe);

        var midlertidig = Fil + ".ny";
        File.WriteAllText(midlertidig, JsonSerializer.Serialize(g, Format), new UTF8Encoding(false));
        File.Move(midlertidig, Fil, overwrite: true);
    }

    /// <summary>Maskinens id. Laves første gang og skifter aldrig.</summary>
    public static string Id => Laes().Id;

    /// <summary>Det navn, den anden maskine ser. «Stationær», «Bærbar».</summary>
    public static string Navn
    {
        get => Laes().Navn;
        set
        {
            var navn = (value ?? "").Trim();
            if (navn.Length == 0) navn = Environment.MachineName;

            Skriv(Laes() with { Navn = navn });
        }
    }

    public static Maskinrolle Rolle
    {
        get => Laes().Rolle;
        set => Skriv(Laes() with { Rolle = value });
    }

    /// <summary>
    /// Stien til den fælles mappe. Null, når der ikke deles noget.
    /// </summary>
    /// <remarks>
    /// DEN ER FORSKELLIG PÅ DE TO MASKINER, og det er hele grunden til, at den
    /// står her og ikke i den fælles mappe. Et drevbogstav på den ene er en
    /// UNC-sti på den anden.
    /// </remarks>
    public static string? Deltmappe
    {
        get => Laes().Deltmappe;
        set => Skriv(Laes() with
        {
            Deltmappe = string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value),
        });
    }

    // ==================================================================== nøglen

    /// <summary>
    /// Maskinens nøglepar. Laves første gang, det bruges.
    /// </summary>
    /// <remarks>
    /// P-256 og ikke noget større: den skal kunne skrives i en fil, en
    /// almindelig bruger kommer til at kigge på, og den skal virke på enhver
    /// Windows uden ekstra pakker. Styrken er rigelig til det, den skal —
    /// se <see cref="Faellesnoegle"/>.
    /// </remarks>
    public static ECDiffieHellman Noegle()
    {
        var gemt = Hemmelighed.Laes(Noeglefil);

        if (!string.IsNullOrWhiteSpace(gemt))
        {
            try
            {
                var d = ECDiffieHellman.Create();
                d.ImportPkcs8PrivateKey(Convert.FromBase64String(gemt), out _);
                return d;
            }
            catch (Exception)
            {
                // Kan den ikke læses, laves der en ny. Så skal maskinen parres
                // igen, og det siger skærmen — det er bedre end en app, der
                // ikke kan dele noget og ikke kan sige hvorfor.
            }
        }

        var ny = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        Directory.CreateDirectory(Mappe);
        Hemmelighed.Skriv(Noeglefil, Convert.ToBase64String(ny.ExportPkcs8PrivateKey()));

        return ny;
    }

    /// <summary>Den offentlige halvdel, som den lægges i den fælles mappe.</summary>
    public static string Offentlignoegle()
    {
        using var d = Noegle();
        return Convert.ToBase64String(d.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// Den fælles nøgle mellem den her maskine og en anden.
    /// </summary>
    /// <remarks>
    /// ============ DEN UDVEKSLES ALDRIG ============
    ///
    /// Begge maskiner regner den ud af sin egen private nøgle og modpartens
    /// offentlige. Den står ikke i den fælles mappe, og den sendes ingen
    /// steder — den, der kan læse mappen, kan altså ikke regne den ud.
    ///
    /// Det er den, etape 2 signerer og krypterer med: uden den kan ingen
    /// lægge et «møde» i mappen, som den anden maskine går i gang med, og
    /// ingen kan læse lyden undervejs.
    /// </remarks>
    public static byte[] Faellesnoegle(string modpartensOffentlige)
    {
        using var min = Noegle();

        using var deres = ECDiffieHellman.Create();
        deres.ImportSubjectPublicKeyInfo(Convert.FromBase64String(modpartensOffentlige), out _);

        return min.DeriveKeyFromHash(deres.PublicKey, HashAlgorithmName.SHA256);
    }

    /// <summary>Glemmer maskinens identitet helt. Bruges af prøver og af «start forfra».</summary>
    public static void Nulstil()
    {
        try
        {
            if (File.Exists(Fil)) File.Delete(Fil);
            if (File.Exists(Noeglefil)) File.Delete(Noeglefil);
        }
        catch (IOException)
        {
            // En låst fil betyder, at der køres igen om lidt. Der er ikke
            // noget at gøre ved det her.
        }
    }
}
