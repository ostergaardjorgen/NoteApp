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
/// Microsoft, og appen HENTER dem ned.
///
/// Den anden vej sker kun på brugerens udtrykkelige valg: en aftale, der laves
/// i appen, kommer kun i Google, hvis der sættes hak ved det. Der er intet, der
/// sendes op af sig selv.
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
                  "En aftale, du laver her, kommer kun i Google, hvis du sætter hak ved det.",
            Hvordan: "Tryk Forbind, log ind hos Google og godkend. Der er ikke mere at gøre — " +
                     "og du kan altid afbryde forbindelsen igen.",
            Klar: true),

        new Integration(
            Id: "google-opgaver",
            Navn: "Google Tasks",
            Leverandoer: "Google",
            Hjemland: "USA",
            Kilde: Kalenderkilde.Google,
            Hvad: "Opgaver, du skriver i Google — på telefonen eller i browseren — dukker op i Cockpittet. " +
                  "Krydser du en af dem af her, bliver den også krydset af hos Google.",
            Hvordan: "Egen godkendelse, adskilt fra kalenderen. Der bedes om ét område: dine opgaver. " +
                     "Opgaver, appen selv har fundet i et møde, sendes ALDRIG op.",
            Klar: true),

        new Integration(
            Id: "microsoft",
            Navn: "Microsoft 365-kalender",
            Leverandoer: "Microsoft",
            Hjemland: "USA",
            Kilde: Kalenderkilde.Microsoft,
            Hvad: "Det samme som Google Kalender: aftalerne læses ind, og du vælger selv, " +
                  "om en aftale herfra også skal oprettes dér.",
            Hvordan: "Bygges, når Google-vejen står og virker. Formen bliver den samme.",
            Klar: false)
    };

    public static Integration? Find(string id) =>
        Alle.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Det, en forbindelse efterlader: nøglen, der giver adgang, og hvordan det
/// gik sidst.
///
/// HER LÅ OGSÅ ET KLIENT-ID, BRUGEREN SELV SKULLE HENTE. Det er væk. Appen har
/// sit eget — se <see cref="Googleklient"/> — og kunden trykker Forbind,
/// logger ind og er færdig. Den, der skal optage et møde om fem minutter,
/// opretter ikke et cloud-projekt først.
///
/// FILEN LIGGER I DATAMAPPEN, uden for kode-repoet pr. konstruktion. Der
/// findes ingen .gitignore-fejl, der kan lække den, fordi den ikke er inde i
/// arbejdstræet til at begynde med.
/// </summary>
public sealed record Integrationsopsaetning
{
    /// <summary>Den nøgle, der bruges til at hente aftaler. Sat efter godkendelse.</summary>
    public string Opdateringsnoegle { get; set; } = "";

    public DateTimeOffset? SidstHentet { get; set; }

    /// <summary>Hvor mange aftaler der kom ind sidste gang.</summary>
    public int SidsteAntal { get; set; }

    /// <summary>Det, der gik galt sidst. Tom, når det gik godt.</summary>
    public string SidsteFejl { get; set; } = "";

    public bool ErForbundet => Opdateringsnoegle.Length > 0;
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
            {
                var o = JsonSerializer.Deserialize<Integrationsopsaetning>(
                            File.ReadAllText(sti, Encoding.UTF8))
                        ?? new Integrationsopsaetning();

                // ============ OPDATERINGSNOEGLEN LIGGER BESKYTTET ============
                //
                // Den er hele adgangen til brugerens kalender og opgaver, og
                // den udloeber ikke af sig selv. Laa den i klartekst, kunne
                // enhver proces - eller enhver, der fik fat i en
                // sikkerhedskopi af mappen - bruge den til at laese og skrive
                // i Google-kontoen, uden at nogen blev spurgt.
                //
                // Nu bindes den til Windows-brugerens egen noegle. Resten af
                // filen - hvornaar der sidst blev hentet, hvor mange, den
                // sidste fejl - staar som foer: det er ikke hemmeligt, og det
                // skal kunne laeses, ogsaa naar noeglen ikke kan.
                o.Opdateringsnoegle = Hemmelighed.Aabnfelt(o.Opdateringsnoegle);

                return o;
            }
        }
        catch (Exception)
        {
            // En oedelagt fil betyder «ikke sat op». Saa kan man saette den op
            // igen - det er ubehageligt, men intet er tabt.
        }

        return new Integrationsopsaetning();
    }

    /// <summary>
    /// Gemmer opsætningen. Opdateringsnøglen beskyttes; resten står som før.
    /// </summary>
    /// <remarks>
    /// ATOMISK. Går strømmen midt i en skrivning, skal der stå enten den
    /// gamle eller den nye opsætning — ikke en halv fil. En ødelagt fil
    /// betyder «ikke sat op», og så skal brugeren forbinde Google igen.
    /// </remarks>
    public static void Gem(string id, Integrationsopsaetning o)
    {
        Directory.CreateDirectory(UserDataPaths.Root);

        // Der gemmes en KOPI med noeglen laast. Objektet, kalderen sidder med,
        // skal blive ved at have den brugbare noegle - ellers ville en
        // gemning midt i et forloeb tage adgangen fra resten af det.
        var udgave = new Integrationsopsaetning
        {
            Opdateringsnoegle = Hemmelighed.Lukfelt(o.Opdateringsnoegle),
            SidstHentet = o.SidstHentet,
            SidsteAntal = o.SidsteAntal,
            SidsteFejl = o.SidsteFejl,
        };

        var sti = Fil(id);
        var kladde = sti + ".kladde";

        File.WriteAllText(kladde, JsonSerializer.Serialize(udgave, Format), new UTF8Encoding(false));

        if (File.Exists(sti)) File.Replace(kladde, sti, null);
        else File.Move(kladde, sti);
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
        // NOEGLEN OVERSKRIVES, ikke bare slettes. En fil, der slettes, ligger
        // stadig paa disken, til pladsen bruges igen - og en adgangsnoegle til
        // en Google-konto, der kan graves op, er ikke vaek.
        Hemmelighed.Slet(Fil(id));

        // OPGAVEINTEGRATIONEN RØRER IKKE KALENDEREN, OG OMVENDT.
        //
        // De to har hver sin nøgle og hvert sit område. Afbryder man den ene,
        // skal den andens data blive stående — ellers forsvinder hele
        // kalenderen, fordi man slog opgaver fra.
        if (id == Googleopgaver.Id) Opgavelager.Fjern(Opgavekilde.Google);
        else Kalender.Fjern(kilde);
    }
}
