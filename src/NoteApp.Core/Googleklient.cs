using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>
/// Appens eget klient-id hos Google.
///
/// HVORFOR DET IKKE ER BRUGERENS OPGAVE
///
/// Første udgave bad brugeren om at oprette et projekt i Google Cloud Console
/// og indsætte et klient-id. Det virker teknisk og er forkert som produkt: den,
/// der skal optage et møde om fem minutter, opretter ikke et cloud-projekt
/// først. Kunden trykker «Forbind», logger ind, og så er den forbundet.
///
/// Id'et hører derfor til APPEN og ikke til brugeren. Det er den samme
/// beslutning, ethvert program med en Google-integration træffer.
///
/// HVORFOR DET SO ALLIGEVEL IKKE ER EN NØGLE, DER LÆKKER
///
/// Google kalder det en «installed app» — en offentlig klient. Både id og
/// hemmelighed kan læses ud af ethvert installeret program, og Google ved det:
/// deres egen dokumentation siger, at hemmeligheden i den her klienttype ikke
/// behandles som fortrolig. Sikkerheden ligger ikke i, at den er hemmelig; den
/// ligger i, at godkendelsen sker i brugerens egen browser, at svaret kun
/// sendes til maskinens egen loopback-adresse, og at der bruges PKCE, så en
/// opsnappet kode ikke kan bruges af nogen anden.
///
/// DEN LIGGER I EN FIL VED SIDEN AF PROGRAMMET OG IKKE I KODEN
///
/// Ikke fordi den er hemmelig, men fordi den er en OPSÆTNING: den skal kunne
/// skiftes uden en ny udgave af programmet, den dag projektet hos Google
/// hedder noget andet. Filen er holdt uden for versionsstyringen — en
/// legitimation, der ligger i et repo, ender i historikken for evigt, også
/// efter den er fjernet.
///
/// MANGLER FILEN, ER DET IKKE BRUGERENS FEJL. Så er appen ikke sat op til
/// Google, og det skal skærmen sige på den måde — ikke bede kunden om at
/// oprette et cloud-projekt.
/// </summary>
public static class Googleklient
{
    /// <summary>Til udvikling og prøvekørsler, så filen ikke skal ligge på plads.</summary>
    public const string MiljoeId = "NOTEAPP_GOOGLE_CLIENT_ID";
    public const string MiljoeHemmelighed = "NOTEAPP_GOOGLE_CLIENT_SECRET";

    private sealed record Fil(string KlientId, string Hemmelighed);

    /// <summary>
    /// Googles EGEN fil, som den hentes fra konsollen.
    ///
    /// Den ser sådan ud, og der er ingen grund til at bede nogen om at skrive
    /// den om i hånden:
    ///
    ///     { "installed": { "client_id": "...", "client_secret": "..." } }
    ///
    /// Et hjemmelavet format, der SKAL bruges, er et ekstra sted at lave en
    /// tastefejl — og fejlen viser sig først som «invalid_client» hos Google,
    /// hvor ingen leder efter den.
    /// </summary>
    private sealed record Googlefil(Googleafsnit? Installed, Googleafsnit? Web);

    private sealed record Googleafsnit(
        [property: JsonPropertyName("client_id")] string? ClientId,
        [property: JsonPropertyName("client_secret")] string? ClientSecret);

    /// <summary>
    /// Filen ligger ved siden af programmet.
    ///
    /// AppContext.BaseDirectory er exe-mappen — også for en enkeltfils-udgivelse,
    /// hvor resten af programmet pakkes ud et midlertidigt sted. Det er
    /// efterprøvet; se WhisperInstall, hvor den samme fælde blev fundet.
    /// </summary>
    public static string Sti => Path.Combine(AppContext.BaseDirectory, "google-klient.json");

    private static (string Id, string Hemmelighed)? _hentet;

    public static (string Id, string Hemmelighed)? Hent()
    {
        if (_hentet is not null) return _hentet;

        var id = Environment.GetEnvironmentVariable(MiljoeId);
        var hem = Environment.GetEnvironmentVariable(MiljoeHemmelighed);

        if (!string.IsNullOrWhiteSpace(id))
            return _hentet = (id.Trim(), (hem ?? "").Trim());

        foreach (var sti in Steder())
        {
            var fundet = Laes(sti);
            if (fundet is not null) return _hentet = fundet.Value;
        }

        return null;
    }

    /// <summary>
    /// Hvor der ledes, i den rækkefølge.
    ///
    /// Filen ved siden af programmet er den rigtige — den kommer med
    /// udgivelsen. Men Googles egen fil hedder noget i retning af
    /// `client_secret_386…apps.googleusercontent.com.json`, og den, der lige
    /// har hentet den, har den liggende i mappen med det navn. At kræve en
    /// omdøbning først er en fejlkilde uden gevinst.
    /// </summary>
    private static IEnumerable<string> Steder()
    {
        yield return Sti;

        var mappe = Path.GetDirectoryName(Sti);
        if (mappe is null) yield break;

        string[] fundne;

        try { fundne = Directory.GetFiles(mappe, "client_secret*.json"); }
        catch (Exception) { yield break; }

        // Fast raekkefoelge. Ligger der to, skal det vaere den samme hver gang
        // - ellers virker appen forskelligt fra start til start.
        Array.Sort(fundne, StringComparer.OrdinalIgnoreCase);

        foreach (var f in fundne) yield return f;
    }

    private static (string, string)? Laes(string sti)
    {
        try
        {
            if (!File.Exists(sti)) return null;

            var tekst = File.ReadAllText(sti, Encoding.UTF8);

            // Googles eget format foerst. Det er det, folk faktisk har paa
            // disken, og det kan kendes paa «installed».
            var g = JsonSerializer.Deserialize<Googlefil>(tekst);
            var afsnit = g?.Installed ?? g?.Web;

            if (!string.IsNullOrWhiteSpace(afsnit?.ClientId))
                return (afsnit.ClientId.Trim(), (afsnit.ClientSecret ?? "").Trim());

            var f = JsonSerializer.Deserialize<Fil>(tekst);

            if (f is not null && !string.IsNullOrWhiteSpace(f.KlientId))
                return (f.KlientId.Trim(), (f.Hemmelighed ?? "").Trim());
        }
        catch (Exception)
        {
            // En oedelagt fil betyder «ikke sat op». Skaermen siger det
            // samme, som hvis den slet ikke var der.
        }

        return null;
    }

    public static bool ErSatOp => Hent() is not null;

    /// <summary>
    /// Beskeden, når appen ikke er sat op til Google.
    ///
    /// DEN ER SKREVET TIL DEN, DER UDGIVER APPEN — ikke til kunden. Kunden kan
    /// ikke gøre noget ved det, og en vejledning i at oprette et cloud-projekt
    /// er ikke et svar på «hvorfor kan jeg ikke forbinde».
    /// </summary>
    public static string Mangler =>
        "Google-integrationen er ikke slået til i den her udgave af appen.\n\n" +
        "Der mangler filen:\n" +
        $"    {Sti}\n\n" +
        "Den indeholder appens eget klient-id hos Google og hører til " +
        "udgivelsen — ikke til dig som bruger. Kontakt den, der har " +
        "installeret appen.";
}
