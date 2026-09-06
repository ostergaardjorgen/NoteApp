namespace NoteApp.Core;

/// <summary>
/// Demotilstand: appen peger på et andet datasæt, så man kan se, hvordan den
/// ser ud med indhold i.
/// </summary>
/// <remarks>
/// ============ DET ER DATAMAPPEN, DER SKIFTER — INTET ANDET ============
///
/// Der er ikke en «demo-udgave» af skærmene, ingen prøveindhold blandet ind i
/// listerne, ingen betingelser rundt om i koden. Appen ser præcis det samme
/// som altid; den kigger bare et andet sted hen.
///
/// Det er hele pointen med <see cref="UserDataPaths"/>: ét sted afgør, hvor
/// dine data ligger. En demotilstand, der i stedet lagde eksempler ind blandt
/// brugerens egne optagelser, ville være noget, man skulle rydde op efter —
/// og en oprydning, der fejler, koster rigtige data.
///
/// ============ DINE EGNE DATA RØRES ALDRIG ============
///
/// Demoen ligger i sin egen mappe under %LOCALAPPDATA%, uden for både
/// datamappen og koden. Den kan slettes med hånden uden at miste noget.
///
/// Valget står i en fil INDE i demomappen. Det er med vilje: sletter man
/// mappen, er demotilstanden også slukket, og appen finder tilbage til
/// brugerens egne data af sig selv. Lå flaget uden for, kunne appen stå og
/// pege på en mappe, der ikke fandtes.
///
/// ============ MILJØVARIABLEN VINDER OVER DET HELE ============
///
/// <see cref="UserDataPaths.OverrideVariable"/> slår demoen fra. Prøver og
/// målinger sætter den, og de skal ramme deres egen sandkasse — ikke demoen,
/// fordi den tilfældigvis var tændt på maskinen.
/// </remarks>
public static class Demotilstand
{
    /// <summary>Til prøver: peger demomappen et andet sted hen.</summary>
    public const string RodVariabel = "HEYPIA_DEMO";

    /// <summary>Hvor demoens data ligger.</summary>
    public static string Rod
    {
        get
        {
            var sat = Environment.GetEnvironmentVariable(RodVariabel);

            return string.IsNullOrWhiteSpace(sat)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HeyPia", "demo")
                : Path.GetFullPath(sat);
        }
    }

    /// <summary>Filen, der siger, at demoen er tændt. Ligger i demomappen.</summary>
    private static string Flagfil => Path.Combine(Rod, "demo-er-taendt.txt");

    /// <summary>Er appen i demotilstand lige nu?</summary>
    public static bool Taendt
    {
        get
        {
            try { return File.Exists(Flagfil); }
            catch (IOException) { return false; }
        }
    }

    /// <summary>
    /// Tænder demoen. Selve dataene bygges først af den nystartede app —
    /// se <see cref="Demodata"/>.
    /// </summary>
    /// <remarks>
    /// DATAENE BYGGES IKKE HER. Generatoren skriver gennem appens egne lagre,
    /// og de skriver dér, hvor <see cref="UserDataPaths.Root"/> peger hen. Kaldte
    /// vi den, mens appen stadig peger på brugerens egen mappe, ville
    /// demooptagelserne lande midt i de rigtige.
    ///
    /// Derfor: sæt flaget, genstart, byg. Rækkefølgen er hele forskellen på en
    /// demo og et rod i nogens data.
    ///
    /// ============ OG DEN KØRENDE APP FLYTTER SIG IKKE ============
    ///
    /// Her stod <c>UserDataPaths.Glem()</c>, så roden blev slået op forfra med
    /// det samme. Det er forkert: appen er på vej ud, og alt, den skriver på
    /// vejen — vinduets størrelse, sidste skærm, en linje i historikken —
    /// ville lande i den ANDEN mappe. Skiftet fra demoen tilbage ville dermed
    /// skrive demoens sidste sekunder ind i brugerens egne data.
    ///
    /// Flaget er en fil. Den nye proces læser den ved opstart, og dér hører
    /// skiftet hjemme.
    /// </remarks>
    public static void Taend()
    {
        Directory.CreateDirectory(Rod);
        File.WriteAllText(Flagfil, DateTimeOffset.Now.ToString("o"));
    }

    /// <summary>Slukker demoen. Dataene bliver liggende til næste gang.</summary>
    public static void Sluk()
    {
        try { if (File.Exists(Flagfil)) File.Delete(Flagfil); }
        catch (IOException) { /* er den låst, står den der til næste forsøg */ }
    }

    /// <summary>Sletter demoens data helt. Bruges ikke af appen — men den skal kunne.</summary>
    public static void Ryd()
    {
        try { if (Directory.Exists(Rod)) Directory.Delete(Rod, recursive: true); }
        catch (IOException) { /* en åben fil. Mappen kan slettes med hånden. */ }
    }
}
