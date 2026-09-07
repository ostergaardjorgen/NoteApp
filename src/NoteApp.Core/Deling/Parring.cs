using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>Hvordan en maskine i den fælles mappe står i forhold til os.</summary>
public enum Parringstilstand
{
    /// <summary>Set i mappen, men aldrig godkendt. Der udveksles intet.</summary>
    Ukendt,

    /// <summary>Parret, og nøglen er den samme som dengang.</summary>
    Parret,

    /// <summary>
    /// Parret én gang — men nøglen i mappen er en anden nu.
    /// </summary>
    /// <remarks>
    /// DET ER DEN VIGTIGE. Enten har maskinen fået sin app installeret forfra,
    /// eller også står der en anden maskine og udgiver sig for den. Appen kan
    /// ikke se forskel, og derfor gør den ingen af delene: der udveksles
    /// intet, før et menneske har set koden igen.
    /// </remarks>
    Nyngle,
}

/// <summary>En maskine, vi har godkendt.</summary>
public sealed record Betroet(string Id, string Navn, string Noegle, DateTimeOffset Parret);

/// <summary>
/// Parring: at to maskiner ved, at det er hinanden.
/// </summary>
/// <remarks>
/// ============ HVAD DET BESKYTTER MOD, OG HVAD DET IKKE GØR ============
///
/// DET GØR: at kun de maskiner, du selv har godkendt, kan lægge arbejde ind,
/// som den anden går i gang med — og at en, der senere skifter nøgle, ikke
/// bliver godkendt i stilhed.
///
/// DET GØR IKKE mappen privat. Kan andre læse mappen, kan de læse det, der
/// ligger i den. Det er delingens egne rettigheder — på NAS'en eller i
/// skytjenesten — der afgør det, og appen skriver det på skærmen frem for at
/// lade brugeren tro noget andet.
///
/// ============ SÅDAN VIRKER DET ============
///
/// Hver maskine lægger sin OFFENTLIGE nøgle i den fælles mappe. Begge regner
/// den fælles nøgle ud af sin egen private og modpartens offentlige; den
/// udveksles aldrig og står ingen steder.
///
/// Problemet er, at den, der kan skrive i mappen, kan bytte en offentlig
/// nøgle ud med sin egen og stå i midten. Derfor vises der en KODE PÅ SEKS
/// CIFRE, der er regnet ud af begge nøgler. Står der det samme tal på de to
/// skærme, er det de rigtige nøgler — en, der har byttet den ene ud, kan ikke
/// få tallene til at passe.
///
/// Koden er ikke en hemmelighed. Den er en sammenligning, og den skal ske med
/// øjnene, på de to skærme, af det menneske der ejer begge maskiner. Det er
/// det samme greb, en sikkerhedskode i en beskedapp bruger.
/// </remarks>
public static class Parring
{
    private static string Fil =>
        Path.Combine(UserDataPaths.Maskinrod, "deling", "betroede.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ===================================================================== koden

    /// <summary>
    /// De seks cifre, der skal stå ens på begge skærme.
    /// </summary>
    /// <remarks>
    /// REKKEFØLGEN MÅ IKKE TÆLLE. De to maskiner regner den ud hver for sig og
    /// kender ikke hinandens «tur», så nøglerne sorteres, før de hashes. Uden
    /// det ville koden være forskellig på de to skærme, og funktionen ville
    /// være ubrugelig på den mest forvirrende måde: den ville se ud som om,
    /// nogen sad i midten, hver eneste gang.
    /// </remarks>
    public static string Kode(string minOffentlige, string modpartensOffentlige)
    {
        var a = Convert.FromBase64String(minOffentlige);
        var b = Convert.FromBase64String(modpartensOffentlige);

        var (foerst, sidst) = Sammenlign(a, b) <= 0 ? (a, b) : (b, a);

        var bunke = new byte[foerst.Length + sidst.Length];
        foerst.CopyTo(bunke, 0);
        sidst.CopyTo(bunke, foerst.Length);

        var sum = SHA256.HashData(bunke);
        var tal = BitConverter.ToUInt32(sum, 0) % 1_000_000;

        return tal.ToString("000000");
    }

    /// <summary>Koden, som den skal læses højt: «412 908».</summary>
    public static string Kodevisning(string minOffentlige, string modpartensOffentlige)
    {
        var k = Kode(minOffentlige, modpartensOffentlige);
        return k[..3] + " " + k[3..];
    }

    private static int Sammenlign(byte[] a, byte[] b)
    {
        var n = Math.Min(a.Length, b.Length);

        for (var i = 0; i < n; i++)
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);

        return a.Length.CompareTo(b.Length);
    }

    // ================================================================== de betroede

    public static IReadOnlyList<Betroet> Alle()
    {
        try
        {
            if (!File.Exists(Fil)) return Array.Empty<Betroet>();

            return JsonSerializer.Deserialize<List<Betroet>>(File.ReadAllText(Fil, Encoding.UTF8))
                   ?? new List<Betroet>();
        }
        catch (Exception)
        {
            // En ulæselig liste betyder, at intet er betroet. Det er den
            // sikre vej rundt: så skal der parres igen, og der udveksles
            // ingenting i mellemtiden.
            return Array.Empty<Betroet>();
        }
    }

    private static void Gem(List<Betroet> liste)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Fil)!);

        var midlertidig = Fil + ".ny";
        File.WriteAllText(midlertidig, JsonSerializer.Serialize(liste, Format), new UTF8Encoding(false));
        File.Move(midlertidig, Fil, overwrite: true);
    }

    /// <summary>
    /// Godkender en maskine. Kaldes, når brugeren har set, at koden passer.
    /// </summary>
    public static void Betro(Maskinoplysning m)
    {
        if (m.Id.Length == 0 || m.Noegle.Length == 0) return;

        var liste = Alle().Where(b => b.Id != m.Id).ToList();
        liste.Add(new Betroet(m.Id, m.Navn, m.Noegle, DateTimeOffset.Now));

        Gem(liste);
    }

    /// <summary>Fortryder en parring.</summary>
    public static void Glem(string id) => Gem(Alle().Where(b => b.Id != id).ToList());

    /// <summary>Hvor står den her maskine i forhold til os?</summary>
    public static Parringstilstand Tilstand(Maskinoplysning m)
    {
        var kendt = Alle().FirstOrDefault(b => b.Id == m.Id);

        if (kendt is null) return Parringstilstand.Ukendt;

        // Tidskonstant er ikke nødvendigt her: nøglen er offentlig, og der er
        // ingen hemmelighed at lække gennem svartiden. Det er en
        // sammenligning af to ting, der begge står i klartekst.
        return kendt.Noegle == m.Noegle ? Parringstilstand.Parret : Parringstilstand.Nyngle;
    }

    /// <summary>Må vi udveksle noget med den her maskine?</summary>
    public static bool MaaUdveksle(Maskinoplysning m) => Tilstand(m) == Parringstilstand.Parret;
}
