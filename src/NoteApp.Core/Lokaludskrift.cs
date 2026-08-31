using System.Diagnostics;
using System.Text;

namespace NoteApp.Core;

/// <summary>
/// Skriver en diktering ud på maskinen i stedet for i skyen.
/// </summary>
/// <remarks>
/// DEN FINDES, FORDI SKYEN IKKE KAN FÅ AT VIDE, HVILKET SPROG DER TALES.
///
/// Voxtral afviser «da» som sprogvalg. Det er ikke en formulering, der kan
/// laves om — det er en liste, endepunktet svarer med, og dansk står ikke på
/// den. Efterprøvet igen 31-08-2026 mod det rigtige endepunkt, også mod den
/// nyeste model, voxtral-mini-2602:
///
///   Got unsupported language `da`, should be one of: ['ar', 'en', 'de',
///   'es', 'fr', 'hi', 'it', 'nl', 'pt', 'zh', 'ru', 'ko', 'ja']
///
/// Sproget kunne derfor kun PÅVIRKES gennem en ledetråd i prompten, aldrig
/// vælges. Det holdt ikke: den samme danske diktering kom tilbage som fransk
/// 30-08, tysk 31-08 og hollandsk samme eftermiddag. Ledetråden blev
/// strammet undervejs, og det hjalp — men en påvirkning er ikke et valg, og
/// hver gang den fejler, er HVERT ORD forkert.
///
/// whisper.cpp tager imod «-l da». Så er der ikke noget at gætte på.
///
/// HVAD DET KOSTER. Målt 31-08-2026 på en RTX 2060, kort klip, med
/// modelindlæsningen regnet med:
///
///   ggml-small             2,3 sek
///   ggml-large-v3-turbo    2,5 sek
///   ggml-medium            5,5 sek
///
/// Turbo er altså den bedste model til nærmest samme tid som den mindste, og
/// hele turen er på niveau med et kald ud af huset. Der er ingen afvejning
/// mellem hurtigt og rigtigt her.
///
/// OG LYDEN BLIVER PÅ MASKINEN. Diktering var det ENESTE sted, hvor appens
/// lyd forlod huset. Med den her vej gør den det ikke længere.
/// </remarks>
public static class Lokaludskrift
{
    /// <summary>
    /// Modellen, dikteringen bruger — den bedste, der er hentet.
    /// </summary>
    /// <remarks>
    /// IKKE DEN MINDSTE, som vågeordet bruger. Vågeordet skal genkende to
    /// ord og ligge på grafikkortet hele dagen; en diktering køres én gang og
    /// skal være rigtig. Målingen ovenfor viser, at det er gratis.
    /// </remarks>
    public static string? Model()
    {
        var mappe = Path.Combine(UserDataPaths.Root, "motor", "modeller");
        if (!Directory.Exists(mappe)) return null;

        foreach (var navn in new[]
                 {
                     "ggml-large-v3-turbo.bin",
                     "ggml-large-v3.bin",
                     "ggml-medium.bin",
                     "ggml-small.bin",
                 })
        {
            var sti = Path.Combine(mappe, navn);
            if (File.Exists(sti)) return sti;
        }

        return null;
    }

    /// <summary>Kan der skrives ud på maskinen lige nu?</summary>
    public static bool Kan() =>
        WhisperInstall.Locate().WhisperCli is not null && Model() is not null;

    /// <summary>
    /// Skriver lydfilen ud. Tom tekst betyder, at der ikke blev hørt noget.
    /// </summary>
    /// <param name="sprog">
    /// Sproget, der TALES. Sendes som «-l». Det er hele grunden til, at den
    /// her vej findes, så den må ikke kunne blive tom.
    /// </param>
    public static async Task<string> SkrivUdAsync(
        string lydfil, string sprog, CancellationToken ct = default)
    {
        if (!File.Exists(lydfil))
            throw new FileNotFoundException("Der er ingen lydfil at skrive ud.", lydfil);

        var motor = WhisperInstall.Locate().WhisperCli
                    ?? throw new InvalidOperationException("whisper-cli blev ikke fundet.");

        var model = Model()
                    ?? throw new InvalidOperationException("Der er ingen model at skrive ud med.");

        var rent = string.IsNullOrWhiteSpace(sprog) ? "da" : sprog.Trim().ToLowerInvariant();

        // -nt: ingen tidsstempler. Det er en diktering, ikke et moede - og et
        // tidsstempel foran hver linje skal alligevel skaeres vaek bagefter.
        var start = new ProcessStartInfo(motor)
        {
            Arguments = $"-m \"{model}\" -l {rent} -t 4 -nt -f \"{lydfil}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var p = Process.Start(start)
                      ?? throw new InvalidOperationException("Motoren kunne ikke startes.");

        // FEJLSTROEMMEN SKAL LAESES. Goer den ikke det, loeber roeret fuldt, og
        // programmet staar stille uden at sige hvorfor - whisper skriver hele
        // sin opstart og alle sine tal paa stderr. Det var praecis den fejl,
        // der gjorde, at vaageordet aldrig meldte sig klar.
        var ud = p.StandardOutput.ReadToEndAsync(ct);
        var fejl = p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);

        var tekst = await ud;
        await fejl;

        return Rens(tekst);
    }

    /// <summary>
    /// Piller udskriften ud af whispers udskrift.
    /// </summary>
    /// <remarks>
    /// Selv med -nt kan der komme tomme linjer og enkelte mærker som
    /// «[BLANK_AUDIO]» eller «(musik)», når klippet er tavst. De er ikke
    /// noget, nogen har sagt, og de må ikke ende i en note.
    /// </remarks>
    public static string Rens(string? raa)
    {
        if (string.IsNullOrWhiteSpace(raa)) return "";

        var linjer = raa
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Where(l => !(l.StartsWith('[') && l.EndsWith(']')))
            .Where(l => !(l.StartsWith('(') && l.EndsWith(')')))
            .Where(l => !(l.StartsWith('*') && l.EndsWith('*')));

        return string.Join(" ", linjer).Trim();
    }
}
