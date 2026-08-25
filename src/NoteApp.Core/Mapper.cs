using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Brugerens egne mapper til at holde materiale adskilt.
///
/// HVORFOR DE IKKE ER RIGTIGE MAPPER PÅ DISKEN
///
/// Det var det oplagte, og det er forkert her. Optagelsens sti står gemt i
/// hvert dokuments <c>SourceRecording</c>, sikkerhedskopien går filsti for
/// filsti, og kommandolinjen tager en mappe som argument. Flytter man en
/// optagelse fysisk, rives alle de forbindelser over på én gang — og de
/// opdages ikke med det samme, men den dag nogen leder efter et gammelt
/// referats ophav.
///
/// Mappen er derfor et FELT i metadata, nøjagtig som arkiveringen er en dato i
/// stedet for en «Arkiv»-mappe. At flytte noget er en tekstændring i en
/// json-fil; intet på disken rører sig, og ingen sti bliver forkert.
///
/// Prisen er, at mapperne kun findes inde i appen. Åbner man datamappen i
/// Stifinder, ligger alt stadig side om side. Det er en reel ulempe — og den
/// er mindre end den anden.
///
/// MAPPER I MAPPER — NAVNET BÆRER STIEN
///
/// En mappe hedder «Møder/Steen». Der er ikke en trædatastruktur nogen steder;
/// der er stadig en flad liste af navne, og en optagelses <c>Mappe</c> er
/// stadig ét felt med ét navn i.
///
/// Det er valgt, fordi alternativet — mapper med id'er og et forælder-felt —
/// ville betyde, at hver eneste optagelse, hvert dokument og hver
/// sikkerhedskopi skulle læse to filer for at vide, hvor noget lå. Med stien i
/// navnet er «hvor ligger det» stadig ét opslag, og en gammel mapper.json
/// uden skråstreger er stadig gyldig: en mappe uden skråstreg ligger i roden.
///
/// Til gengæld skal to ting holdes i hævd, og de er begge prøvet af: en
/// skråstreg i et NAVN er forbudt (ellers kan man ikke skelne «A/B» fra en
/// mappe, der hedder «A/B»), og at flytte eller omdøbe en mappe skal flytte
/// alle dens undermapper OG rette det indhold, der peger på dem.
/// </summary>
public static class Mapper
{
    /// <summary>Filen med de mapper, brugeren har oprettet.</summary>
    private static string Fil => Path.Combine(UserDataPaths.Root, "mapper.json");

    /// <summary>Vises i stedet for et mappenavn, når noget ikke ligger i en mappe.</summary>
    public const string Ingen = "Uden mappe";

    private sealed record Gemt(List<string> Optagelser, List<string> Dokumenter);

    /// <summary>Hvad mapperne bruges til. De to lister deles ikke.</summary>
    public enum Slags
    {
        /// <summary>Mapper til optagelser.</summary>
        Optagelser,

        /// <summary>Mapper til dokumenter.</summary>
        Dokumenter
    }

    private static Gemt Indlaes()
    {
        try
        {
            if (File.Exists(Fil))
                return JsonSerializer.Deserialize<Gemt>(File.ReadAllText(Fil, Encoding.UTF8))
                       ?? new Gemt(new(), new());
        }
        catch (Exception)
        {
            // En oedelagt fil maa ikke forhindre appen i at starte. Saa er der
            // ingen mapper, og brugeren kan oprette dem igen — det er
            // ubehageligt, men det er ikke tab af data: selve optagelserne og
            // dokumenterne ligger uroert.
        }

        return new Gemt(new(), new());
    }

    private static void Gem(Gemt g)
    {
        Directory.CreateDirectory(UserDataPaths.Root);

        // Skriv til side og flyt paa plads, saa en afbrudt skrivning ikke
        // efterlader en halv fil, der ikke kan laeses.
        var midlertidig = Fil + ".ny";
        File.WriteAllText(midlertidig,
            JsonSerializer.Serialize(g, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        File.Move(midlertidig, Fil, overwrite: true);
    }

    private static List<string> Liste(Gemt g, Slags s) =>
        s == Slags.Optagelser ? g.Optagelser : g.Dokumenter;

    public static IReadOnlyList<string> Alle(Slags slags) =>
        Liste(Indlaes(), slags).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

    /// <summary>
    /// Opretter en mappe. Returnerer false, hvis navnet er tomt eller allerede
    /// findes — to mapper med samme navn kan man ikke skelne, og så er den ene
    /// af dem et sted, ting bliver væk.
    /// </summary>
    public static bool Opret(Slags slags, string navn)
    {
        navn = navn.Trim();
        if (navn.Length == 0 || navn.Equals(Ingen, StringComparison.CurrentCultureIgnoreCase)) return false;

        var g = Indlaes();
        var liste = Liste(g, slags);

        if (liste.Any(n => n.Equals(navn, StringComparison.CurrentCultureIgnoreCase))) return false;

        liste.Add(navn);
        Gem(g);
        return true;
    }

    /// <summary>
    /// Fjerner mappen fra listen. Indholdet slettes ALDRIG — det, der lå i
    /// den, står bare uden mappe bagefter.
    ///
    /// Det er med vilje: en mappe er en måde at se på tingene, ikke en
    /// beholder. At slette en mappe og tage tyve optagelser med ville være
    /// den slags tab, ingen forventer af en oprydning.
    /// </summary>
    public static void Slet(Slags slags, string navn)
    {
        var g = Indlaes();
        Liste(g, slags).RemoveAll(n => n.Equals(navn, StringComparison.CurrentCultureIgnoreCase));
        Gem(g);
    }

    /// <summary>
    /// Sikrer, at en mappe, der er i brug, også står på listen.
    ///
    /// Bliver mapper.json væk, mens optagelserne stadig peger på mapper, ville
    /// de ellers forsvinde ud af listen og alt ligge «uden mappe» — uden at
    /// noget var gået tabt, og uden at nogen kunne se hvorfor.
    /// </summary>
    public static void SikrFindes(Slags slags, IEnumerable<string?> ibrug)
    {
        var g = Indlaes();
        var liste = Liste(g, slags);
        var tilfoejet = false;

        foreach (var navn in ibrug)
        {
            if (string.IsNullOrWhiteSpace(navn)) continue;
            if (liste.Any(n => n.Equals(navn, StringComparison.CurrentCultureIgnoreCase))) continue;

            liste.Add(navn.Trim());
            tilfoejet = true;
        }

        if (tilfoejet) Gem(g);
    }

    // ==================== MAPPER I MAPPER ====================

    /// <summary>Skiller led i en mappesti. Må ikke stå i et mappenavn.</summary>
    public const char Adskiller = '/';

    /// <summary>Er navnet brugbart som ÉT led i en sti?</summary>
    /// <remarks>
    /// En skråstreg i navnet ville gøre «A/B» tvetydig: er det mappen B inde i
    /// A, eller en mappe, der hedder «A/B»? To betydninger af det samme er
    /// præcis dét, man ikke kan rette op på bagefter.
    /// </remarks>
    public static bool ErGyldigtLed(string led) =>
        led.Trim().Length > 0
        && !led.Contains(Adskiller)
        && !led.Trim().Equals(Ingen, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Mappens sidste led — det, der vises i træet.</summary>
    public static string Bladnavn(string sti)
    {
        var i = sti.LastIndexOf(Adskiller);
        return i < 0 ? sti : sti[(i + 1)..];
    }

    /// <summary>Mappen, den ligger i. Tom streng betyder roden.</summary>
    public static string Foraelder(string sti)
    {
        var i = sti.LastIndexOf(Adskiller);
        return i < 0 ? "" : sti[..i];
    }

    /// <summary>Er stien den samme som eller under en anden?</summary>
    public static bool LiggerUnder(string sti, string over) =>
        over.Length == 0
        || sti.Equals(over, StringComparison.CurrentCultureIgnoreCase)
        || sti.StartsWith(over + Adskiller, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Sammensætning af to led, hvor tom forælder betyder roden.</summary>
    public static string Sammensaet(string foraelder, string led) =>
        foraelder.Length == 0 ? led : foraelder + Adskiller + led;

    /// <summary>Hvad en flytning eller omdøbning gjorde ved navnene.</summary>
    public readonly record struct Navneskifte(string Fra, string Til);

    /// <summary>
    /// Flytter en mappe ind under en anden — eller ud i roden.
    /// </summary>
    /// <param name="nyForaelder">Tom streng flytter mappen ud i roden.</param>
    /// <returns>
    /// Alle de navneskift, flytningen medførte — mappen selv og hver eneste
    /// undermappe. Tom liste betyder, at der ikke blev flyttet noget.
    /// </returns>
    /// <remarks>
    /// LISTEN SKAL BRUGES. Den, der kalder, har ansvaret for at rette det
    /// indhold, der peger på de gamle navne. Gøres det ikke, står indholdet
    /// tilbage i en mappe, der ikke findes længere — og det ser ud, som om det
    /// er væk.
    ///
    /// EN MAPPE KAN IKKE FLYTTES IND I SIG SELV eller ned i en af sine egne
    /// undermapper. Det ville lave en sti, der peger på sig selv, og træet
    /// ville aldrig kunne bygges færdigt.
    /// </remarks>
    public static IReadOnlyList<Navneskifte> Flyt(Slags slags, string mappe, string nyForaelder)
    {
        mappe = mappe.Trim();
        nyForaelder = nyForaelder.Trim();

        if (mappe.Length == 0) return Array.Empty<Navneskifte>();

        // IND I SIG SELV ELLER NED I SIT EGET: nej.
        if (nyForaelder.Length > 0 && LiggerUnder(nyForaelder, mappe))
            return Array.Empty<Navneskifte>();

        var nyt = Sammensaet(nyForaelder, Bladnavn(mappe));
        if (nyt.Equals(mappe, StringComparison.CurrentCultureIgnoreCase))
            return Array.Empty<Navneskifte>();

        return Doeb(slags, mappe, nyt);
    }

    /// <summary>
    /// Giver en mappe et nyt sidste led. Undermapper følger med.
    /// </summary>
    public static IReadOnlyList<Navneskifte> Omdoeb(Slags slags, string mappe, string nytLed)
    {
        nytLed = nytLed.Trim();
        if (!ErGyldigtLed(nytLed)) return Array.Empty<Navneskifte>();

        var nyt = Sammensaet(Foraelder(mappe), nytLed);
        return nyt.Equals(mappe, StringComparison.CurrentCultureIgnoreCase)
            ? Array.Empty<Navneskifte>()
            : Doeb(slags, mappe, nyt);
    }

    /// <summary>Selve omskrivningen. Mappen og alt under den får ny sti.</summary>
    private static IReadOnlyList<Navneskifte> Doeb(Slags slags, string fra, string til)
    {
        var g = Indlaes();
        var liste = Liste(g, slags);

        // FINDES DER ALLEREDE EN MAPPE MED DET NYE NAVN, sker der ingenting.
        // To mapper med samme sti kan man ikke skelne, og saa er den ene et
        // sted, ting bliver vaek.
        if (liste.Any(n => n.Equals(til, StringComparison.CurrentCultureIgnoreCase)))
            return Array.Empty<Navneskifte>();

        var skift = new List<Navneskifte>();

        for (var i = 0; i < liste.Count; i++)
        {
            if (!LiggerUnder(liste[i], fra)) continue;

            var nyt = til + liste[i][fra.Length..];
            skift.Add(new Navneskifte(liste[i], nyt));
            liste[i] = nyt;
        }

        if (skift.Count == 0) return Array.Empty<Navneskifte>();

        SikrForaeldre(liste, til);
        Gem(g);
        return skift;
    }

    /// <summary>
    /// Sletter en mappe OG alle dens undermapper fra listen.
    ///
    /// Indholdet slettes aldrig. Det, der lå i dem, står bare uden mappe
    /// bagefter — en mappe er en måde at se på tingene, ikke en beholder.
    /// </summary>
    /// <returns>
    /// De mapper, der blev fjernet. Den, der kalder, skal rydde
    /// <c>Mappe</c>-feltet på det, der pegede på dem.
    /// </returns>
    public static IReadOnlyList<string> SletMedIndhold(Slags slags, string mappe)
    {
        var g = Indlaes();
        var liste = Liste(g, slags);

        var ramte = liste.Where(n => LiggerUnder(n, mappe)).ToList();
        if (ramte.Count == 0) return ramte;

        liste.RemoveAll(n => LiggerUnder(n, mappe));
        Gem(g);
        return ramte;
    }

    /// <summary>Opretter en mappe under en anden. Forældrene oprettes, hvis de mangler.</summary>
    public static bool OpretUnder(Slags slags, string foraelder, string led)
    {
        if (!ErGyldigtLed(led)) return false;

        var sti = Sammensaet(foraelder.Trim(), led.Trim());

        var g = Indlaes();
        var liste = Liste(g, slags);

        if (liste.Any(n => n.Equals(sti, StringComparison.CurrentCultureIgnoreCase))) return false;

        liste.Add(sti);
        SikrForaeldre(liste, sti);
        Gem(g);
        return true;
    }

    /// <summary>
    /// Lægger de mellemliggende led ind, hvis de mangler.
    ///
    /// «Møder/Steen/2026» kræver, at både «Møder» og «Møder/Steen» står på
    /// listen — ellers kan træet ikke bygges, og en mappe med indhold ville
    /// være usynlig.
    /// </summary>
    private static void SikrForaeldre(List<string> liste, string sti)
    {
        var led = sti.Split(Adskiller);
        var samlet = "";

        for (var i = 0; i < led.Length - 1; i++)
        {
            samlet = Sammensaet(samlet, led[i]);
            var her = samlet;
            if (!liste.Any(n => n.Equals(her, StringComparison.CurrentCultureIgnoreCase)))
                liste.Add(her);
        }
    }
}
