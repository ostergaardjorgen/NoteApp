using System.Diagnostics;

namespace NoteApp.Core;

/// <summary>
/// Hvor meget der er ledigt på grafikkortet — og hvad det betyder for en model,
/// der skal lægges derop.
///
/// HVORFOR DET SKAL SPØRGES OM, FØR EN KØRSEL STARTER
///
/// llama.cpp kaldes med «læg alle lag på grafikkortet». Men den beslutning
/// træffes, NÅR MODELLEN INDLÆSES, ud fra hvor meget der er ledigt i netop det
/// øjeblik. Er der for lidt, lægger den stille nogle lag på processoren i
/// stedet — og så er det afgjort for hele kørslen. Lukker man videoen et minut
/// senere, bliver den ikke hurtigere igen.
///
/// Forskellen er ikke lille. Et RTX 2060 med en 8B-model i Q4 giver 20-30
/// tokens i sekundet, når alt ligger på kortet. Målt 14. august 2026 med video
/// kørende i baggrunden: 0,7 tokens i sekundet — 28 minutter for ét referat.
/// Fyrre gange langsommere, uden en eneste fejlmeddelelse.
///
/// Det er ikke til at gennemskue for den, det sker for. Man leder efter fejlen
/// i appen, og den ligger i en YouTube-fane.
///
/// HVORFOR NVIDIA-SMI OG IKKE ET BIBLIOTEK
///
/// Et bibliotek ville skulle følge med driveren og fylde i pakken. nvidia-smi
/// følger med enhver NVIDIA-driver og skriver et tal ud. Findes den ikke — et
/// kort fra en anden producent, eller ingen driver — svarer denne klasse
/// «ved ikke», og så SKAL der ikke advares. En advarsel på et gæt er værre end
/// ingen advarsel.
/// </summary>
public static class Grafikhukommelse
{
    /// <summary>
    /// Ledig og samlet hukommelse på kortet, i byte. Null når det ikke kan
    /// læses — så er der ikke noget at sige, og der siges ikke noget.
    /// </summary>
    public static (long Fri, long Ialt)? Laes()
    {
        foreach (var sti in Kandidater())
        {
            try
            {
                var psi = new ProcessStartInfo(sti)
                {
                    Arguments = "--query-gpu=memory.used,memory.total --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p is null) continue;

                var svar = p.StandardOutput.ReadToEnd();

                // Et kort, der ikke svarer inden for et par sekunder, er ikke
                // noget at vente paa — svaret bruges til en advarsel, ikke til
                // en beregning.
                if (!p.WaitForExit(4000)) { try { p.Kill(); } catch (Exception) { } continue; }

                // Foerste linje er foerste kort. Er der flere, er det stadig
                // det foerste, llama.cpp bruger som standard.
                var linje = svar.Split('\n').FirstOrDefault(l => l.Trim().Length > 0);
                if (linje is null) continue;

                var dele = linje.Split(',', StringSplitOptions.TrimEntries);
                if (dele.Length < 2) continue;

                if (!long.TryParse(dele[0], out var brugtMb)) continue;
                if (!long.TryParse(dele[1], out var ialtMb)) continue;

                const long mb = 1024L * 1024L;
                return ((ialtMb - brugtMb) * mb, ialtMb * mb);
            }
            catch (Exception)
            {
                // Kan den ikke koeres, proev naeste sti.
            }
        }

        return null;
    }

    private static IEnumerable<string> Kandidater()
    {
        yield return "nvidia-smi";   // paa PATH

        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Path.Combine(system, "nvidia-smi.exe");

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Path.Combine(pf, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
    }

    /// <summary>
    /// Er der plads til modellen på kortet?
    ///
    /// <paramref name="modelBytes"/> er filens størrelse. Oven i den skal der
    /// være plads til KV-cache og beregningsbuffere — de vokser med, hvor lang
    /// teksten er, og et halvt gigabyte er et nøgternt tillæg for et almindeligt
    /// møde.
    ///
    /// Svaret er <c>null</c>, når det ikke kan afgøres. Kaldere skal behandle
    /// det som «gå videre» og ikke som «noget er galt».
    /// </summary>
    public static (bool Plads, long Fri, long Kraevet)? HarPlads(long modelBytes, long tillaeg = 600L * 1024 * 1024)
    {
        var kort = Laes();
        if (kort is null) return null;

        var kraevet = modelBytes + tillaeg;
        return (kort.Value.Fri >= kraevet, kort.Value.Fri, kraevet);
    }

    /// <summary>Læsbar størrelse — samme form som resten af appen bruger.</summary>
    public static string Gigabyte(long bytes) => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";
}
