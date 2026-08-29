using System.Diagnostics;
using Microsoft.Win32;

namespace NoteApp.Core;

/// <summary>Et program, der bruger mikrofonen lige nu.</summary>
/// <param name="Noegle">
/// Den værdi, der huskes, når man siger «spørg aldrig for det her program».
/// Stabil på tværs af genstarter — stien eller pakkenavnet, ikke et pid.
/// </param>
/// <param name="Navn">Programmets navn, som det skal stå på skærmen.</param>
/// <param name="Startet">Hvornår det åbnede mikrofonen.</param>
public sealed record Mikrofonbruger(string Noegle, string Navn, DateTime Startet);

/// <summary>
/// Hvem bruger mikrofonen lige nu?
///
/// HVORFOR DET ER MIKROFONEN OG IKKE HØJTTALEREN, DER MÅLES
///
/// Spørgsmålet, der skal besvares, er «er der startet et møde?». Et møde og et
/// webinar lyder ens i højttaleren — begge er lyd, appen ikke selv laver — og
/// de kan ikke skilles ad dér.
///
/// Men et møde bruger DIN MIKROFON. Et webinar, en YouTube-video og en
/// streamingtjeneste rører den aldrig.
///
/// MÅLT 21-08-2026 på denne maskine, mens et webinar blev afspillet OG optaget:
///
///   højttaler   Reolink, HeyPia        (der var lyd)
///   mikrofon    ingen app bruger den    (og det er svaret)
///
/// Og med et program, der holdt mikrofonen åben, blev det set med navn, sti og
/// starttidspunkt inden for få sekunder. Begge halvdele er efterprøvet: at der
/// ikke måles noget falsk, og at der måles noget rigtigt.
///
/// HVOR OPLYSNINGEN KOMMER FRA
///
/// Windows fører selv listen — det er den, «Hvilke apps har brugt mikrofonen»
/// under Indstillinger viser. Et program, der bruger mikrofonen lige nu, har
/// sluttidspunktet stående som 0.
///
/// Det er en READ-ONLY forespørgsel til brugerens eget register. Der lyttes
/// ikke med, der optages ikke, og der forlader intet maskinen. Appen får
/// nøjagtig samme oplysning frem, som brugeren selv kan slå op i Indstillinger.
///
/// HVORFOR IKKE WASAPI-SESSIONERNE I STEDET
///
/// De virker også — målt samtidig, samme svar. Men de kan kun ses på ÉN
/// optageenhed ad gangen, og bruger mødeprogrammet et andet headset end det,
/// appen har valgt, ses det ikke. Registret er uafhængigt af enhed, og det har
/// starttidspunktet med, som der alligevel skal bruges.
/// </summary>
public static class Mikrofonvagt
{
    private const string Rod =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

    /// <summary>
    /// Programmerne, der bruger mikrofonen lige nu. HeyPia selv er ikke med.
    ///
    /// Fejler opslaget — en nøgle, der ikke findes på en ældre Windows —
    /// er svaret en tom liste. Vagten skal kunne slå fejl uden at koste noget;
    /// den er en hjælp, ikke en forudsætning.
    /// </summary>
    public static IReadOnlyList<Mikrofonbruger> IBrug()
    {
        var ud = new List<Mikrofonbruger>();

        try
        {
            Laes(Rod, pakket: true, ud);
            Laes(Rod + @"\NonPackaged", pakket: false, ud);
        }
        catch (Exception)
        {
            // Uden svar spoerger vagten ikke. Det er den rigtige fejlretning:
            // et spoergsmaal for lidt er til at leve med, et forkert er ikke.
        }

        return ud;
    }

    private static void Laes(string sti, bool pakket, List<Mikrofonbruger> ud)
    {
        using var k = Registry.CurrentUser.OpenSubKey(sti);
        if (k is null) return;

        foreach (var navn in k.GetSubKeyNames())
        {
            if (navn.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase)) continue;

            using var app = k.OpenSubKey(navn);
            if (app is null) continue;

            // Stop == 0 betyder «i brug lige nu». Mangler vaerdien, ved vi det
            // ikke, og saa er svaret nej.
            if (app.GetValue("LastUsedTimeStop") is not long stop || stop != 0) continue;

            var start = app.GetValue("LastUsedTimeStart") is long s && s > 0
                ? DateTime.FromFileTime(s)
                : DateTime.Now;

            if (ErOsSelv(navn)) continue;
            if (!pakket && !Koerer(navn)) continue;

            ud.Add(new Mikrofonbruger(navn, Visningsnavn(navn, pakket), start));
        }
    }

    /// <summary>
    /// HeyPia skal aldrig spørge om sig selv.
    ///
    /// Optager man et almindeligt møde, ER mikrofonen i brug — af appen. Uden
    /// det her ville vagten spørge, om den skulle optage det møde, den lige er
    /// gået i gang med at optage.
    ///
    /// Nøglerne skriver stien med # i stedet for \.
    /// </summary>
    private static bool ErOsSelv(string noegle) =>
        ErEgetProgram(noegle, Environment.ProcessPath, WhisperInstall.Root);

    /// <summary>
    /// Er det her program appen selv — eller en af dens egne motorer?
    /// </summary>
    /// <remarks>
    /// HER STOD KUN «ER DET MIN EGEN EXE». Det var nok, så længe appen selv
    /// var det eneste, der rørte mikrofonen.
    ///
    /// Så kom vågeordet, og det lytter med whisper-command.exe — en anden
    /// proces, som appen selv starter. Mikrofonvagten så et fremmed program
    /// åbne mikrofonen og spurgte, om mødet skulle optages. Appen spurgte
    /// altså om sig selv, og svaret stod i selve boblen: «whisper-command
    /// bruger din mikrofon». Set 29-08-2026.
    ///
    /// Der sammenlignes med MOTORMAPPEN og ikke med et filnavn. En liste over
    /// navne skal vedligeholdes, og den bliver forkert den dag, motoren
    /// skifter navn — mappen er appens egen uanset hvad der lægges i den.
    ///
    /// Skilt ud, fordi den kan prøves af uden et register og en mikrofon.
    /// </remarks>
    public static bool ErEgetProgram(string noegle, string? migSelv, string motormappe)
    {
        var sti = noegle.Replace('#', '\\');

        if (migSelv is not null && sti.Equals(migSelv, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(motormappe)) return false;

        var mappe = motormappe.TrimEnd('\\', '/') + '\\';
        return sti.StartsWith(mappe, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kører programmet stadig?
    ///
    /// EN NØGLE KAN BLIVE HÆNGENDE. Går et program ned uden at slippe
    /// mikrofonen, bliver sluttidspunktet stående som 0, og så ville vagten
    /// spørge om et møde i et program, der ikke findes mere — hver gang, uden
    /// at der var noget at gøre ved det.
    ///
    /// Kontrollen gælder kun almindelige programmer. En pakket app kan ikke
    /// slås op på sin sti, og dér er nøglen til gengæld Windows' eget ansvar.
    /// </summary>
    private static bool Koerer(string noegle)
    {
        try
        {
            var navn = Path.GetFileNameWithoutExtension(noegle.Replace('#', '\\'));
            return navn.Length > 0 && Process.GetProcessesByName(navn).Length > 0;
        }
        catch (Exception)
        {
            // Kan det ikke afgoeres, tages nøglen for gode varer. Et
            // spoergsmaal for meget er bedre end en vagt, der tier, fordi et
            // opslag fejlede.
            return true;
        }
    }

    /// <summary>
    /// Navnet på et program, man kun har nøglen til.
    ///
    /// Bruges dér, hvor de fravalgte programmer skal vises igen — dér findes
    /// kun det, der blev gemt. Om nøglen er en sti eller et pakkenavn kan ses
    /// på den selv: en sti har mapper i sig.
    /// </summary>
    public static string Navnet(string noegle) =>
        Visningsnavn(noegle, pakket: !noegle.Contains('#') && !noegle.Contains('\\'));

    /// <summary>
    /// Navnet, brugeren ser. «Teams», ikke en sti på hundrede tegn.
    ///
    /// Programmets egen beskrivelse foretrækkes — den står i filen og er den,
    /// leverandøren selv har skrevet. Findes den ikke, bruges filnavnet.
    /// </summary>
    private static string Visningsnavn(string noegle, bool pakket)
    {
        if (pakket)
        {
            // Pakkenavne ser saadan ud: «Microsoft.Teams_8wekyb3d8bbwe».
            var uden = noegle.Split('_')[0];
            var sidste = uden.Split('.').LastOrDefault();
            return string.IsNullOrWhiteSpace(sidste) ? uden : sidste;
        }

        var sti = noegle.Replace('#', '\\');

        try
        {
            var b = FileVersionInfo.GetVersionInfo(sti).FileDescription;
            if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
        }
        catch (Exception)
        {
            // Filen kan vaere vaek eller utilgaengelig. Filnavnet duer.
        }

        return Path.GetFileNameWithoutExtension(sti);
    }
}
