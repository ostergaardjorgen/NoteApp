using System.Diagnostics;
using System.IO;
using System.Text;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Lytter efter «Hej Pia» og «Hey Pia» — og trykker på genvejstasten.
///
/// VÅGEORDET ER EN UDLØSER, IKKE EN NY FUNKTION. Bliver ordet hørt, kaldes
/// præcis den samme kode som et hold på Ctrl+, — dikteringen begynder, teksten
/// lander, hvor markøren står. Alt nedenunder findes og er prøvet af.
///
/// HVORFOR whisper-command
///
/// Den ligger allerede på maskinen. whisper.cpp, som appen henter til
/// transskriptionen, indeholder den — sammen med stemmevagten
/// ggml-silero-v5.1.2.bin på 0,9 MB. MIT-licens, ingen ny afhængighed, ingen
/// træning i en sky, og dansk er understøttet.
///
/// HVORFOR IKKE DENS EGET «-p»
///
/// whisper-command har et flag til netop et vågeord: <c>-p</c>, «the required
/// activation prompt». Det tager ÉT udtryk. Der skal være to — «hej» og «hey»
/// lyder næsten ens, og udskriften vælger den ene efter, hvor hårdt h'et
/// siges. Lyttes der kun efter den ene, virker vågeordet hver anden gang.
///
/// Derfor skriver den ud, hvad den hører, og matchningen sker her — med
/// <see cref="Vaageord.Hoert"/>, som er prøvet af uden mikrofon.
/// </summary>
public sealed class Vaageordsvagt : IDisposable
{
    private Process? _proces;
    private readonly object _laas = new();

    /// <summary>Rejses, når et vågeord blev hørt. Giver det, der stod efter det.</summary>
    public event Action<string>? Hoert;

    /// <summary>Siger til, når der sker noget, der skal kunne ses.</summary>
    public event Action<string>? Melder;

    /// <summary>Lytter der lige nu?</summary>
    public bool Lytter => _proces is { HasExited: false };

    private DateTime _startet;

    /// <summary>
    /// Hvor meget af én kerne lytningen bruger, i procent. Null, når der ikke
    /// lyttes.
    /// </summary>
    /// <remarks>
    /// DER MAALES, DER PAASTAAS IKKE.
    ///
    /// «Det bruger naesten ingenting» er en paastand, ingen kan efterproeve -
    /// og den slags skal en app ikke komme med om sig selv, naar den har
    /// mikrofonen aaben. Tallet her er processortiden, barneprocessen faktisk
    /// har brugt, delt med den tid der er gaaet. Det staar paa skaermen, og
    /// det kan sammenlignes med Jobliste.
    /// </remarks>
    public double? Forbrug
    {
        get
        {
            try
            {
                if (_proces is not { HasExited: false } p) return null;

                var gaaet = (DateTime.UtcNow - _startet).TotalSeconds;
                if (gaaet < 2) return null;   // for kort til at sige noget

                p.Refresh();
                return p.TotalProcessorTime.TotalSeconds / gaaet * 100;
            }
            catch (Exception)
            {
                // Processen kan vaere doed mellem de to linjer. Saa er der
                // ikke noget at maale.
                return null;
            }
        }
    }

    /// <summary>
    /// Motoren, der lyttes med. Tom, hvis den ikke er hentet.
    /// </summary>
    public static string Motorsti =>
        Path.Combine(UserDataPaths.Root, "motor", "whisper", "bin", "Release", "whisper-command.exe");

    public static bool MotorFindes => File.Exists(Motorsti);

    /// <summary>
    /// Starter lytningen, hvis den ikke kører.
    /// </summary>
    /// <param name="model">Whisper-modellen. Den, brugeren allerede har.</param>
    /// <param name="sprog">«da» eller «en».</param>
    public void Start(string model, string sprog)
    {
        lock (_laas)
        {
            if (Lytter) return;

            if (!MotorFindes || !File.Exists(model))
            {
                Melder?.Invoke(Sprog.T("vaageord.ingen_motor"));
                return;
            }

            try
            {
                var start = new ProcessStartInfo(Motorsti)
                {
                    // -t 2: to traade. Vagten skal ikke tage maskinen fra det,
                    // brugeren laver - den skal bare vaere der.
                    Arguments = $"-m \"{model}\" -l {sprog} -t 2",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };

                _proces = Process.Start(start);
                if (_proces is null) return;

                // ============ DEN SKAL DØ SAMMEN MED APPEN ============
                //
                // Lukkes appen paent, lukkes den her med i Ryd(). Men appen
                // bliver ikke altid lukket paent - udgivelsen slaar en koerende
                // app ihjel, og det goer Jobliste ogsaa. Saa levede den videre
                // med sin model i hukommelsen og sit greb om grafikkortet.
                //
                // Maalt 29-08-2026: TRE af dem samtidig, den aeldste tretten
                // timer gammel, 2,4 GB og 94 % af grafikkortet - mens
                // vaageordet var slaaet FRA. Se Boernejob.
                Boernejob.Tilfoej(_proces);

                _startet = DateTime.UtcNow;

                _proces.OutputDataReceived += (_, e) => Laes(e.Data);
                _proces.BeginOutputReadLine();

                // Fejlstroemmen laeses OGSAA. Gjorde den ikke det, ville
                // roeret loebe fuldt, og programmet ville staa stille uden at
                // sige hvorfor - whisper skriver sin opstart paa stderr.
                _proces.ErrorDataReceived += (_, _) => { };
                _proces.BeginErrorReadLine();

                Melder?.Invoke(Sprog.T("vaageord.lytter"));
            }
            catch (Exception ex)
            {
                Ryd();
                Melder?.Invoke(Sprog.T("vaageord.gik_galt", ex.Message));
            }
        }
    }

    /// <summary>Stopper lytningen.</summary>
    public void Stop()
    {
        lock (_laas)
        {
            if (_proces is null) return;

            Ryd();
            Melder?.Invoke(Sprog.T("vaageord.stoppet"));
        }
    }

    /// <summary>
    /// Én linje fra motoren.
    /// </summary>
    /// <remarks>
    /// DER LYTTES EFTER ET ORD, IKKE EFTER ALT. Linjen kan være hvad som
    /// helst — motoren skriver også sin egen opstart og sine mellemregninger.
    /// Kun linjer, der begynder med et vågeord, betyder noget; resten falder
    /// på gulvet.
    ///
    /// Det er også dét, der gør, at et møde ikke starter en diktering: der
    /// tales i timevis, og ingen af sætningerne begynder med «Hej Pia».
    /// </remarks>
    private void Laes(string? linje)
    {
        if (string.IsNullOrWhiteSpace(linje)) return;

        var ord = AppSettings.Current.Vaageord is { Count: > 0 } egne
            ? egne
            : Vaageord.Standardord;

        if (Vaageord.Hoert(linje, ord) is not { } fundet) return;

        Hoert?.Invoke(Vaageord.Efter(linje, fundet));
    }

    private void Ryd()
    {
        try
        {
            if (_proces is { HasExited: false })
            {
                _proces.Kill(entireProcessTree: true);
                _proces.WaitForExit(2000);
            }
        }
        catch (Exception)
        {
            // Processen kan vaere doed i forvejen. Der er ikke mere at goere.
        }

        try { _proces?.Dispose(); } catch (Exception) { }
        _proces = null;
    }

    public void Dispose() => Ryd();
}
