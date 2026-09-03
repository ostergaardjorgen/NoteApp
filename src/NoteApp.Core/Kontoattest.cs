using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>
/// Hvad en kontrol hviler på: noget appen selv kan se, eller noget et
/// menneske har slået til et andet sted.
/// </summary>
public enum Grundlag
{
    /// <summary>Appen kan efterprøve det selv, hver gang der sendes.</summary>
    Verificeret,

    /// <summary>Kræver manuel aktivering hos leverandøren. Appen kan hverken slå det til eller læse det.</summary>
    KraeverManuel
}

/// <summary>
/// Ét af de tre kontrolpunkter, compliance-skærmen svarer på.
/// </summary>
/// <remarks>
/// LISTEN LIGGER I KODEN OG IKKE I XAML'EN, fordi det skal kunne prøves, at
/// appen ikke påstår at have verificeret noget, den ikke kan verificere.
/// Teksten på skærmen kan oversættes; grundlaget kan ikke.
/// </remarks>
public sealed record Kontrolpunkt(string Id, Grundlag Grundlag);

/// <summary>
/// Brugerens egen registrering af, at en kontoindstilling ER slået til.
/// </summary>
/// <remarks>
/// HVORFOR APPEN IKKE BARE TJEKKER DET SELV
///
/// Zero Data Retention og fravalg af modeltræning er kontoindstillinger hos
/// Mistral. De sættes under Workspace i deres konsol, og der findes ikke et
/// endepunkt, appen kan spørge. Appen kan altså hverken slå dem til eller
/// bekræfte, at de er slået til — og må derfor ikke skrive, at de er det.
///
/// Det, der kan lade sig gøre, er at gøre påstanden revisionsklar: hvem satte
/// den, hvornår, og hvor ligger dokumentationen. Det er ikke en verifikation,
/// og skærmen kalder det heller ikke det. Det er en attest med et navn på.
///
/// HVAD DER IKKE GEMMES
///
/// Intet fra kontoen. Ikke konto-id, ikke workspace-id, ikke nøglen, ikke
/// organisationens navn hos leverandøren. Tre felter, brugeren selv skriver,
/// og en kontrol der afviser noget, der ligner en nøgle — se
/// <see cref="Kontoattester.Fejl"/>.
/// </remarks>
public sealed record Attest
{
    /// <summary>Datoen som ISO — <c>2026-09-03</c>. Tom betyder «ikke registreret».</summary>
    public string Dato { get; init; } = "";

    /// <summary>Den, der står inde for det. Et navn eller en rolle.</summary>
    public string Ansvarlig { get; init; } = "";

    /// <summary>
    /// Hvor dokumentationen ligger LOKALT — et sagsnummer, en filsti, et
    /// bilagsnavn. Ikke en adresse hos leverandøren.
    /// </summary>
    public string Reference { get; init; } = "";

    /// <summary>Er der registreret noget overhovedet?</summary>
    public bool ErRegistreret =>
        Dato.Trim().Length > 0 && Ansvarlig.Trim().Length > 0;
}

/// <summary>Attesterne for de to kontoindstillinger, appen ikke kan sætte.</summary>
public sealed record Kontoattest
{
    /// <summary>Zero Data Retention — leverandøren gemmer ikke anmodningen.</summary>
    public Attest Zdr { get; init; } = new();

    /// <summary>Fravalg af, at anmodningerne bruges til at træne modeller.</summary>
    public Attest Traening { get; init; } = new();
}

/// <summary>
/// Læser og skriver <see cref="Kontoattest"/> i datamappen.
/// </summary>
/// <remarks>
/// Filen ligger hos data og ikke i repoet: den er brugerens egen registrering
/// af sin egen konto, og den hører ikke til i koden. Se
/// <see cref="UserDataPaths"/>.
/// </remarks>
public static class Kontoattester
{
    /// <summary>De tre linjer, compliance-skærmen svarer på — i rækkefølge.</summary>
    /// <remarks>
    /// EU-linjen er den ENESTE, der er verificeret, og den er det, fordi
    /// <see cref="Llm.SkyKatalog.KraevEuropa"/> kaster, hvis adressen er en
    /// anden. De to andre kan appen ikke se. Bliver den forskel visket ud,
    /// falder <c>KontoattestTest</c>.
    /// </remarks>
    public static readonly IReadOnlyList<Kontrolpunkt> Kontrolpunkter = new[]
    {
        new Kontrolpunkt("eu", Grundlag.Verificeret),
        new Kontrolpunkt("zdr", Grundlag.KraeverManuel),
        new Kontrolpunkt("traening", Grundlag.KraeverManuel)
    };

    public static string Fil => Path.Combine(UserDataPaths.Root, "kontoattest.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    /// <summary>
    /// Noget, der ligner en API-nøgle eller et konto-id: en lang ubrudt
    /// blanding af bogstaver og tal.
    /// </summary>
    /// <remarks>
    /// GRÆNSEN PÅ 20 ER MÅLT, IKKE GÆTTET. Mistrals nøgler er 32 tegn
    /// (efterprøvet 03-09-2026 mod en nøgle fra console.mistral.ai), og et
    /// workspace-id er en UUID på 32 hextegn plus bindestreger. Et sagsnummer
    /// som «SAG-2026-0142» og et bilagsnavn som «bilag-4.pdf» har begge
    /// skilletegn og bliver derfor ikke ramt.
    ///
    /// Den fanger ikke alt, og det skal den heller ikke — den er der, så en
    /// nøgle, der bliver klistret ind i et forkert felt, stopper med en
    /// besked frem for at blive gemt.
    /// </remarks>
    private static readonly Regex LignerNoegle =
        new(@"[A-Za-z0-9]{20,}", RegexOptions.Compiled);

    /// <summary>
    /// Hvorfor en værdi ikke må gemmes — eller <c>null</c>, hvis den må.
    /// </summary>
    /// <remarks>
    /// Kontrollen står ét sted og kaldes både af skærmen (så brugeren får
    /// beskeden, mens feltet er åbent) og af <see cref="Gem"/> (så et nyt
    /// kaldsted ikke kan komme udenom den). Det er samme opbygning som
    /// <c>SkyKatalog.KraevEuropa</c>, og af samme grund.
    /// </remarks>
    public static string? Fejl(string vaerdi)
    {
        if (vaerdi.Length > 200)
            return "Feltet er for langt. Skriv en henvisning, ikke et dokument.";

        return LignerNoegle.IsMatch(vaerdi)
            ? "Det ser ud som en nøgle eller et konto-id. Attesten gemmer intet fra kontoen "
              + "hos leverandøren — skriv en dato, et navn og en henvisning til dine egne bilag."
            : null;
    }

    /// <summary>Attesten, som den står. En manglende eller ødelagt fil betyder «intet registreret».</summary>
    public static Kontoattest Hent()
    {
        try
        {
            if (File.Exists(Fil))
                return JsonSerializer.Deserialize<Kontoattest>(File.ReadAllText(Fil, Encoding.UTF8))
                       ?? new Kontoattest();
        }
        catch (Exception)
        {
            // En oedelagt fil betyder «ikke registreret». Det er det
            // forsigtige svar: at vise en attest, der maaske ikke findes, er
            // den forkerte fejl paa netop den her skaerm.
        }

        return new Kontoattest();
    }

    /// <summary>
    /// Gemmer attesten. Kaster, hvis et felt ligner en konto-oplysning.
    /// </summary>
    /// <remarks>
    /// ATOMISK, som resten af det, der skrives i datamappen: går strømmen
    /// midt i en skrivning, skal der stå enten den gamle eller den nye attest
    /// — ikke en halv fil, der læses som «intet registreret».
    /// </remarks>
    public static void Gem(Kontoattest a)
    {
        foreach (var felt in new[]
                 {
                     a.Zdr.Dato, a.Zdr.Ansvarlig, a.Zdr.Reference,
                     a.Traening.Dato, a.Traening.Ansvarlig, a.Traening.Reference
                 })
        {
            if (Fejl(felt) is { } grund) throw new InvalidOperationException(grund);
        }

        Directory.CreateDirectory(UserDataPaths.Root);

        var midlertidig = Fil + ".ny";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(a, Format), new UTF8Encoding(false));
        File.Move(midlertidig, Fil, overwrite: true);
    }
}
