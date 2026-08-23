using System.Text;
using System.Text.Json;

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

        try
        {
            if (File.Exists(Sti))
            {
                var f = JsonSerializer.Deserialize<Fil>(File.ReadAllText(Sti, Encoding.UTF8));

                if (f is not null && !string.IsNullOrWhiteSpace(f.KlientId))
                    return _hentet = (f.KlientId.Trim(), (f.Hemmelighed ?? "").Trim());
            }
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
