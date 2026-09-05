using System.IO;

namespace NoteApp.Desktop;

/// <summary>
/// Skriver ned, hvad tastehooken ser — men KUN når nogen udtrykkeligt beder om det.
/// </summary>
/// <remarks>
/// ============ HOOKEN LOVER AT SKRIVE INTET NED ============
///
/// Det løfte står i <see cref="Tastehook"/> og på Compliance-siden, og det
/// gælder. Sporet her bryder det ikke: det er slået fra, med mindre
/// miljøvariablen <c>HEYPIA_TASTESPOR</c> er sat, og den sætter ingen ved et
/// uheld. Der er ingen knap, ingen indstilling og ingen automatik.
///
/// HVORFOR DET FINDES ALLIGEVEL. Genvejen blev rettet fire gange på to dage,
/// og hver gang blev rettelsen målt på KUNSTIGE tastetryk i en prøve, mens
/// meldingen fra maskinen blev ved med at være «der sker ingenting». En
/// prøve, der ikke kan svare på, hvor kæden knækker, er ikke en måling — den
/// er et gæt med tal på.
///
/// Filen ligger i maskinens temp-mappe og hedder heypia-tastespor.log.
/// </remarks>
public static class Tastespor
{
    /// <summary>Er sporet slået til? Læses én gang.</summary>
    public static readonly bool Til =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HEYPIA_TASTESPOR"));

    private static readonly string Fil =
        Path.Combine(Path.GetTempPath(), "heypia-tastespor.log");

    private static readonly object Laas = new();

    public static void Skriv(string linje)
    {
        if (!Til) return;

        try
        {
            lock (Laas)
                File.AppendAllText(Fil,
                    $"{DateTime.Now:HH:mm:ss.fff}  {linje}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Et spor, der vælter appen, er værre end intet spor.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
