namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Hvilken optagelse der bliver skrevet ud lige nu — og hvor langt den er.
///
/// HVORFOR DEN FINDES
///
/// Fremdriften stod KUN på den skærm, transskriptionen blev startet fra.
/// Skiftede man til Cockpittet eller Dokumenter, var der intet spor af, at
/// noget kørte — og en transskription tager fire et halvt minut for hvert
/// kvarters lyd. Man går væk fra skærmen, kommer tilbage, og ved ikke, om den
/// er i gang, færdig eller aldrig blev startet.
///
/// Det er præcis dét, der skete 24-08-2026: en optagelse på 23 minutter lå
/// uden udskrift, og spørgsmålet var ikke «hvornår er den færdig», men «kører
/// den overhovedet».
///
/// DEN STYRER INGENTING. Den ved kun, hvad der er i gang, så skærmene kan
/// vise det. Selve kørslen og afbrydelsen bliver, hvor de er — i den
/// skærm, der ejer den. En vagt, der også kunne stoppe kørsler, ville være et
/// andet sted at lede efter en fejl.
///
/// DEN OVERLEVER, AT SKÆRMEN BYGGES OM. Optagelsesskærmen laves forfra ved
/// hvert skift; tilstanden må derfor ikke ligge i den. Derfor statisk.
/// </summary>
public static class Udskriftsvagt
{
    /// <summary>Optagelsens mappe. Null, når der ikke skrives noget ud.</summary>
    public static string? Mappe { get; private set; }

    /// <summary>Optagelsens navn, som skærmene skal vise det.</summary>
    public static string Navn { get; private set; } = "";

    /// <summary>0-100. Negativ betyder «i gang, men uden et tal endnu».</summary>
    public static double Procent { get; private set; } = -1;

    /// <summary>Hvad der sker lige nu — modellen læses ind, spor 1 af 2, og så videre.</summary>
    public static string Besked { get; private set; } = "";

    public static bool Koerer => Mappe is not null;

    /// <summary>Meldes, hver gang noget ændrer sig. Skærmene lytter med.</summary>
    public static event Action? Aendret;

    /// <summary>Bliver DEN HER optagelse skrevet ud lige nu?</summary>
    public static bool ErIGang(string? mappe) =>
        Mappe is not null && mappe is not null &&
        string.Equals(Mappe.TrimEnd('\\'), mappe.TrimEnd('\\'),
                      StringComparison.OrdinalIgnoreCase);

    public static void Start(string mappe, string navn, string besked)
    {
        Mappe = mappe;
        Navn = navn;
        Besked = besked;
        Procent = -1;

        Meld();
    }

    public static void Fremdrift(double procent, string besked)
    {
        // Er der ikke noget i gang, meldes der ikke noget. En melding efter et
        // stop ville taende bjaelken igen paa en koersel, der er slut.
        if (Mappe is null) return;

        Procent = procent;
        Besked = besked;

        Meld();
    }

    public static void Slut()
    {
        if (Mappe is null) return;

        Mappe = null;
        Navn = "";
        Besked = "";
        Procent = -1;

        Meld();
    }

    private static void Meld()
    {
        // Hele udsendelsen er pakket ind. En skaerm, der er ved at blive
        // lukket, maa ikke kunne rive en transskription ned.
        try { Aendret?.Invoke(); } catch (Exception) { }
    }
}
