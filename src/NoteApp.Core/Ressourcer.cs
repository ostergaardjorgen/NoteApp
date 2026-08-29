using System.Diagnostics;

namespace NoteApp.Core;

/// <summary>Hvad ét program bruger lige nu.</summary>
/// <param name="Navn">Programmets navn, som det skal stå på skærmen.</param>
/// <param name="Pid">Processens nummer, så det kan slås op i Jobliste.</param>
/// <param name="Procent">Andel af én processorkerne, målt over det sidste stykke tid.</param>
/// <param name="Megabyte">Hukommelse i brug.</param>
/// <param name="Alder">Hvor længe processen har kørt.</param>
/// <param name="ErOs">Er det HeyPia selv, eller et af appens hjælpeprogrammer?</param>
public sealed record Forbrugspost(
    string Navn, int Pid, double Procent, double Megabyte, TimeSpan Alder, bool ErOs);

/// <summary>
/// Måler, hvad appen og dens hjælpeprogrammer bruger.
///
/// HVORFOR DEN FINDES
///
/// Appen starter andre programmer: whisper-cli skriver møderne ud,
/// whisper-command lytter efter vågeordet. De kan hver især tage flere
/// gigabyte og et helt grafikkort, og de kører uden for appens eget vindue.
///
/// Målt 29-08-2026 stod der tre whisper-command tilbage fra tidligere
/// kørsler. Tilsammen 2,4 GB og 94 % af grafikkortet — mens vågeordet var
/// slået fra. Maskinen var mærkbart langsom, og der var INTET sted i appen,
/// hvor man kunne se det. Man kunne kun se det i Jobliste, og der hedder de
/// ikke HeyPia.
///
/// HVORDAN DER MÅLES
///
/// Processortid er et tal, der kun vokser. Ét aflæsning siger derfor
/// ingenting om, hvad der sker NU — kun hvad der er brugt siden start. Derfor
/// måles der to gange med en pause imellem, og forskellen deles med den tid,
/// der gik. Det er den samme regnemåde som Joblistes, og tallene kan
/// sammenlignes.
///
/// 100 % betyder én kerne fuldt i brug. På en maskine med otte kerner kan der
/// altså stå 800 %, og det er ikke en fejl.
/// </summary>
public static class Ressourcer
{
    /// <summary>Hjælpeprogrammerne, appen selv starter.</summary>
    public static readonly string[] Hjaelpere = { "whisper-cli", "whisper-command", "main" };

    /// <summary>
    /// Regner processorforbruget ud af to aflæsninger.
    /// </summary>
    /// <remarks>
    /// Skilt ud, så den kan prøves af uden en maskine at måle på. Er der ikke
    /// gået tid, er der ikke noget at regne på, og der svares null frem for at
    /// dividere med nul og skrive et tal, ingen kan bruge.
    /// </remarks>
    public static double? Procent(TimeSpan foer, TimeSpan efter, TimeSpan gaaet)
    {
        if (gaaet <= TimeSpan.Zero) return null;

        var brugt = (efter - foer).TotalSeconds;
        if (brugt < 0) return null;   // processen er skiftet ud under os

        return brugt / gaaet.TotalSeconds * 100.0;
    }

    /// <summary>Læser processortid og hukommelse for én proces. Null, hvis den er væk.</summary>
    private static (TimeSpan Tid, double MB, TimeSpan Alder)? Laes(Process p)
    {
        try
        {
            p.Refresh();
            if (p.HasExited) return null;
            return (p.TotalProcessorTime, p.WorkingSet64 / 1048576.0, DateTime.Now - p.StartTime);
        }
        catch (Exception)
        {
            // Adgang naegtet eller processen naaede at doe. Begge dele betyder
            // det samme her: der er ikke noget at vise.
            return null;
        }
    }

    /// <summary>
    /// Finder appen og dens hjælpeprogrammer, måler dem over
    /// <paramref name="vindue"/> og giver én linje pr. program.
    /// </summary>
    /// <remarks>
    /// Der ledes KUN efter hjælpeprogrammer i appens egen motormappe. Et
    /// program med samme navn et andet sted er ikke vores at gøre op — og at
    /// vise det ville være at tage skylden for en anden.
    /// </remarks>
    public static async Task<IReadOnlyList<Forbrugspost>> MaalAsync(
        TimeSpan vindue, CancellationToken ct = default)
    {
        var mappe = WhisperInstall.Root.TrimEnd('\\', '/') + '\\';
        var processer = new List<(Process P, string Navn, bool Os)>();

        try
        {
            var mig = Process.GetCurrentProcess();
            processer.Add((mig, "HeyPia", true));
        }
        catch (Exception) { }

        foreach (var navn in Hjaelpere)
        {
            Process[] fundne;
            try { fundne = Process.GetProcessesByName(navn); }
            catch (Exception) { continue; }

            foreach (var p in fundne)
            {
                var vores = false;
                try { vores = p.MainModule?.FileName?.StartsWith(mappe, StringComparison.OrdinalIgnoreCase) == true; }
                catch (Exception) { }

                if (vores) processer.Add((p, p.ProcessName, false));
                else p.Dispose();
            }
        }

        var foer = new Dictionary<int, TimeSpan>();
        foreach (var (p, _, _) in processer)
        {
            var l = Laes(p);
            if (l is not null) foer[p.Id] = l.Value.Tid;
        }

        var ur = Stopwatch.StartNew();
        await Task.Delay(vindue, ct).ConfigureAwait(false);
        ur.Stop();

        var ud = new List<Forbrugspost>();
        foreach (var (p, navn, os) in processer)
        {
            var l = Laes(p);
            if (l is null) { p.Dispose(); continue; }

            double procent = 0;
            if (foer.TryGetValue(p.Id, out var start))
                procent = Procent(start, l.Value.Tid, ur.Elapsed) ?? 0;

            ud.Add(new Forbrugspost(navn, p.Id, procent, l.Value.MB, l.Value.Alder, os));
            p.Dispose();
        }

        // Appen selv foerst, saa det tungeste. Den, der aabner skaermen, vil
        // vide hvad der fylder - ikke laese en liste i tilfaeldig raekkefoelge.
        return ud
            .OrderByDescending(f => f.ErOs)
            .ThenByDescending(f => f.Megabyte)
            .ToList();
    }
}
