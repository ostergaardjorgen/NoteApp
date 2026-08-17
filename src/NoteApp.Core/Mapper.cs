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
}
