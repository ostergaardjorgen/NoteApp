namespace NoteApp.Core;

/// <summary>
/// Microsofts C++-komponent — den, whisper er bygget med.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN STÅR HER SOM SIT EGET ============
///
/// Whisper er skrevet i C++ og bygget med Microsofts værktøjer. Programmet
/// bærer ikke selv de biblioteker med; de kommer med «Microsoft Visual C++
/// Redistributable», som følger med Visual Studio, med spil, med Office og med
/// hundrede andre programmer. På de fleste maskiner er den der allerede, og
/// derfor opdager man den ikke.
///
/// På en ny bærbar er den der ikke. Målt 07-09-2026 på en frisk installation:
/// appen startede, mikrofonen virkede, optagelsen kørte — og i det øjeblik
/// teksten skulle skrives ud, kom Windows' egen fejlkasse med «VCOMP140.DLL
/// blev ikke fundet», mens appen stod og skrev «Skriver teksten ud …» i det
/// uendelige.
///
/// DET ER IKKE EN FEJL, BRUGEREN KAN GÆTTE. Der står et filnavn og intet
/// andet. Derfor ser appen efter den SELV, siger hvad der mangler, og hvor
/// den hentes.
///
/// ============ HVOR DEN LEDES EFTER ============
///
/// To steder: ved siden af motoren (en kopi, der er lagt der med vilje), og i
/// Windows' egen systemmappe, hvor Microsofts pakke lægger den. Er den
/// ingen af stederne, kan whisper ikke starte — heller ikke selv om filen
/// findes et helt tredje sted, for det er de to steder, Windows leder.
/// </remarks>
public static class Cppkomponent
{
    /// <summary>De biblioteker, whisper og ggml er bygget op om.</summary>
    /// <remarks>
    /// vcomp140 er OpenMP — den, ggml regner parallelt med, og den, der
    /// manglede. De to andre er selve C++-runtime; de mangler sjældnere, men
    /// de kommer fra den samme pakke, så der er ingen grund til kun at se
    /// efter den ene.
    /// </remarks>
    public static readonly IReadOnlyList<string> Kraevede = new[]
    {
        "vcomp140.dll",
        "msvcp140.dll",
        "vcruntime140.dll",
        "vcruntime140_1.dll",
    };

    /// <summary>Microsofts egen adresse. Den peger altid på den nyeste.</summary>
    public const string Hentesti = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

    /// <summary>Navnet, som det står i Windows' liste over programmer.</summary>
    public const string Navn = "Microsoft Visual C++ Redistributable (x64)";

    /// <summary>
    /// Hvad der mangler. Tom liste betyder, at alt er på plads.
    /// </summary>
    /// <param name="vedSidenAf">
    /// Mappen, motoren ligger i. Windows leder dér først, så en kopi ved siden
    /// af programmet tæller.
    /// </param>
    /// <param name="systemmappe">
    /// Windows' systemmappe. Kun til prøverne — de kan ikke lægge filer i den
    /// rigtige, og en prøve, der måler på maskinen, den tilfældigvis kører på,
    /// siger ikke noget om koden.
    /// </param>
    public static IReadOnlyList<string> Mangler(string? vedSidenAf = null, string? systemmappe = null)
    {
        var ud = new List<string>();

        foreach (var fil in Kraevede)
        {
            if (Findes(fil, vedSidenAf, systemmappe)) continue;

            ud.Add(fil);
        }

        return ud;
    }

    /// <summary>Er alt på plads?</summary>
    public static bool Klar(string? vedSidenAf = null, string? systemmappe = null) =>
        Mangler(vedSidenAf, systemmappe).Count == 0;

    private static bool Findes(string fil, string? vedSidenAf, string? systemmappe)
    {
        try
        {
            if (vedSidenAf is { Length: > 0 } m && File.Exists(Path.Combine(m, fil))) return true;

            return File.Exists(Path.Combine(systemmappe ?? Environment.SystemDirectory, fil));
        }
        catch (Exception)
        {
            // Kan mappen ikke laeses, er svaret «ved ikke». Det behandles som
            // «findes», saa en maskine, hvor vi ikke kan se efter, ikke faar
            // en fejl om noget, der maaske er helt i orden.
            return true;
        }
    }

    /// <summary>
    /// Det, brugeren skal læse, når den mangler.
    /// </summary>
    /// <remarks>
    /// DER STÅR HVAD DER SKER, IKKE HVAD DER MANGLER. «vcomp140.dll blev ikke
    /// fundet» er sandt og ubrugeligt. Filnavnene står til sidst, for den dag
    /// nogen skal fejlsøge det.
    /// </remarks>
    public static string Besked(string? vedSidenAf = null, string? systemmappe = null)
    {
        var mangler = Mangler(vedSidenAf, systemmappe);

        if (mangler.Count == 0) return "";

        return "HeyPia skriver dine optagelser ud med Whisper, og Whisper mangler en komponent fra "
             + "Microsoft på den her computer. Den følger med de fleste programmer, men en ny "
             + "Windows har den ikke altid.\n\n"
             + $"Hent «{Navn}» hos Microsoft og installér den. Det tager et minut, og der skal "
             + "ikke gøres andet bagefter — hverken optagelser eller indstillinger går tabt.\n\n"
             + Hentesti + "\n\n"
             + "Det, der mangler: " + string.Join(", ", mangler);
    }
}
