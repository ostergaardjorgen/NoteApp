using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// En tjeneste, appen kan hente kalenderaftaler fra.
///
/// HVORFOR DE STÅR I ÉN LISTE OG IKKE HVER FOR SIG
///
/// Der kommer flere. Google er den første, Microsoft 365 den næste, og
/// forskellen på dem er kun, hvor man henter en nøgle og hvilken adresse der
/// bliver spurgt. Alt det andet — at man skal godkende, at aftalerne
/// afløses ved hver hentning, at de forsvinder igen, når man slår fra — er
/// det samme.
///
/// Skrives den ene som et særtilfælde, bliver den anden det også, og så er
/// der to steder at rette, hver gang noget skal ændres for begge.
/// </summary>
public sealed record Integration(
    string Id,
    string Navn,
    string Leverandoer,
    string Hjemland,
    Kalenderkilde Kilde,
    string Hvad,
    string Hvordan,
    bool Klar);

/// <summary>
/// Integrationer til fremmede kalendere.
///
/// HVAD DER SENDES, OG HVORFOR DET ER I ORDEN HER
///
/// Appens hovedregel er, at data bliver på maskinen, og at det, der skal
/// behandles i skyen, behandles i EU. En kalenderintegration bryder ikke den
/// regel — den vender den om: aftalerne ligger allerede hos Google eller
/// Microsoft, og appen HENTER dem ned. Der sendes ingenting op.
///
/// Har man valgt Google Workspace eller Microsoft 365, har man selv taget
/// stilling til, at mødetitler og deltagere ligger hos en amerikansk
/// leverandør. Appen ændrer ikke på det; den læser det, der er der i forvejen.
///
/// DET, DER ALDRIG SENDES: lyden, transkriptionerne, noterne og dokumenterne.
/// En kalenderintegration giver adgang til at LÆSE en kalender og intet andet.
///
/// KALENDEREN VIRKER UDEN. Det er ikke en overgangsløsning — se
/// <see cref="Kalender"/>.
/// </summary>
public static class Integrationer
{
    /// <summary>
    /// Google Kalender. Den første, og den eneste, der er bygget.
    ///
    /// Microsoft 365 står med som «ikke klar» frem for slet ikke at stå der.
    /// Det er en oplysning, ikke en mangel: den, der bruger Microsoft, skal
    /// kunne se, at det er på vej — og ikke bruge tid på at lede efter en
    /// indstilling, der ikke findes.
    /// </summary>
    public static readonly IReadOnlyList<Integration> Alle = new[]
    {
        new Integration(
            Id: "google",
            Navn: "Google Kalender",
            Leverandoer: "Google",
            Hjemland: "USA",
            Kilde: Kalenderkilde.Google,
            Hvad: "Dine aftaler læses ind i kalenderen, så du kan trykke optag direkte på et møde. " +
                  "Der bliver ikke skrevet noget tilbage til Google.",
            Hvordan: "Kræver et klient-id fra Google Cloud Console. Det er gratis, og det er dit eget.",
            Klar: true),

        new Integration(
            Id: "microsoft",
            Navn: "Microsoft 365-kalender",
            Leverandoer: "Microsoft",
            Hjemland: "USA",
            Kilde: Kalenderkilde.Microsoft,
            Hvad: "Det samme som Google Kalender: aftalerne læses ind, og der skrives intet tilbage.",
            Hvordan: "Bygges, når Google-vejen står og virker. Formen bliver den samme.",
            Klar: false)
    };

    public static Integration? Find(string id) =>
        Alle.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Det, en integration skal bruge for at kunne forbinde — gemt i datamappen.
///
/// HVORFOR DER IKKE FØLGER EN NØGLE MED APPEN
///
/// Samme grund som ved sprogmodellen: en nøgle, der er indbygget, er den
/// samme for alle, der har programmet. Den kan aflæses ud af filen, den kan
/// misbruges i andres navn, og den dag den bliver spærret, holder appen op med
/// at virke for alle på én gang.
///
/// Klient-id'et er brugerens eget og hentes gratis hos leverandøren. Det er en
/// omvej — og den er den rigtige.
///
/// FILEN LIGGER I DATAMAPPEN, uden for kode-repoet pr. konstruktion. Der
/// findes ingen .gitignore-fejl, der kan lække den, fordi den ikke er inde i
/// arbejdstræet til at begynde med.
/// </summary>
public sealed record Integrationsopsaetning
{
    public string KlientId { get; set; } = "";

    /// <summary>
    /// Klienthemmeligheden. Tom for de flows, der ikke bruger en.
    ///
    /// Google kalder en desktop-app for en «offentlig klient», og
    /// hemmeligheden dér er ikke en hemmelighed i egentlig forstand — den kan
    /// læses ud af enhver installeret app. Den skal alligevel med, fordi
    /// Googles endepunkt kræver den.
    /// </summary>
    public string Hemmelighed { get; set; } = "";

    /// <summary>Den nøgle, der bruges til at hente aftaler. Sat efter godkendelse.</summary>
    public string Opdateringsnoegle { get; set; } = "";

    public DateTimeOffset? SidstHentet { get; set; }

    /// <summary>Hvor mange aftaler der kom ind sidste gang.</summary>
    public int SidsteAntal { get; set; }

    /// <summary>Det, der gik galt sidst. Tom, når det gik godt.</summary>
    public string SidsteFejl { get; set; } = "";

    public bool ErForbundet => Opdateringsnoegle.Length > 0;
    public bool HarOpsaetning => KlientId.Length > 0;
}

/// <summary>Læser og gemmer opsætningen for hver integration.</summary>
public static class Integrationsfiler
{
    private static string Fil(string id) =>
        Path.Combine(UserDataPaths.Root, $"integration-{id}.json");

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public static Integrationsopsaetning Hent(string id)
    {
        try
        {
            var sti = Fil(id);

            if (File.Exists(sti))
                return JsonSerializer.Deserialize<Integrationsopsaetning>(
                           File.ReadAllText(sti, Encoding.UTF8))
                       ?? new Integrationsopsaetning();
        }
        catch (Exception)
        {
            // En oedelagt fil betyder «ikke sat op». Saa kan man saette den op
            // igen - det er ubehageligt, men intet er tabt.
        }

        return new Integrationsopsaetning();
    }

    public static void Gem(string id, Integrationsopsaetning o)
    {
        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Fil(id), JsonSerializer.Serialize(o, Format), new UTF8Encoding(false));
    }

    /// <summary>
    /// Glemmer alt om en integration.
    ///
    /// AFTALERNE FJERNES MED. En integration, man har slået fra, må ikke
    /// efterlade en kalender fuld af aftaler, appen ikke længere kan holde
    /// opdateret — de ville blive stående og blive forkerte uden at nogen
    /// kunne se hvorfor.
    /// </summary>
    public static void Glem(string id, Kalenderkilde kilde)
    {
        try { if (File.Exists(Fil(id))) File.Delete(Fil(id)); } catch (IOException) { }

        Kalender.Fjern(kilde);
    }
}
