using System.IO;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Holder øje med de mapper, brugeren har peget på — roadmap 2.2.
///
/// HVORFOR DEN SPØRGER PÅ ET UR OG IKKE LYTTER EFTER BESKEDER
///
/// Windows kan sige til, når noget ændrer sig i en mappe
/// (<c>FileSystemWatcher</c>), og det ville være billigere. Men det er ikke
/// pålideligt her: en synkroniseringsklient skriver en fil i etaper, omdøber
/// den undervejs og ændrer dens attributter bagefter, og beskederne kan gå
/// tabt, når der kommer mange på én gang. En mappe i skyen er præcis det
/// tilfælde, hvor beskederne svigter.
///
/// Et kig hvert minut koster en filoptælling og ingen læsning. Det er billigt
/// nok til at være enkelt, og det kan ikke gå glip af noget.
///
/// DEN STARTER INGEN INDLÆSNING PÅ EGEN HÅND, medmindre mappen er sat til
/// det. Ellers samler den fundene op, og skærmen tilbyder dem.
///
/// DEN MÅ ALDRIG STÅ I VEJEN FOR EN OPTAGELSE. Den læser ikke i filer, den
/// tager ingen lås, og den venter ikke på noget. Fejler et kig, prøver den
/// igen om et minut.
/// </summary>
public static class Mappevagt
{
    private static DispatcherTimer? _ur;
    private static bool _kigger;

    /// <summary>
    /// Hvor tit der kigges.
    ///
    /// Et minut er valgt, fordi det er kortere end den tid, det tager at gå
    /// fra telefonen til computeren — filen er der, når man kommer. Kortere
    /// ville ikke gøre noget bedre; længere ville få funktionen til at føles
    /// som noget, der ikke virker.
    /// </summary>
    public static readonly TimeSpan Mellemrum = TimeSpan.FromMinutes(1);

    /// <summary>Filer, der venter på, at nogen tager stilling.</summary>
    public static IReadOnlyList<Lydfund> Ventende { get; private set; } = Array.Empty<Lydfund>();

    /// <summary>Meldes, når listen ændrer sig. Skærmene lytter med.</summary>
    public static event Action? Aendret;

    /// <summary>Meldes, når en fil er lagt ind af sig selv, så listerne kan opdateres.</summary>
    public static event Action<string>? Lagtind;

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Mellemrum };
        _ur.Tick += (_, _) => _ = Kig();
        _ur.Start();

        // Der kigges med det samme. Den fil, der kom i gaar aftes, skal ikke
        // vente et minut mere, fordi appen lige er startet.
        _ = Kig();
    }

    /// <summary>
    /// Kigger nu. Kaldes af uret og af skærmene, når brugeren beder om det.
    /// </summary>
    public static async Task Kig()
    {
        if (_kigger) return;
        if (Overvaagning.Mapper().Count == 0)
        {
            if (Ventende.Count > 0) { Ventende = Array.Empty<Lydfund>(); Aendret?.Invoke(); }
            return;
        }

        _kigger = true;

        try
        {
            var mapper = Overvaagning.Mapper();

            // Selve optaellingen ud paa en anden traad. En skymappe med mange
            // filer kan tage et oejeblik, og skaermen maa ikke staa stille
            // imens - slet ikke hvis der optages.
            var fund = await Task.Run(() => Overvaagning.Kig(mapper));

            var automatiske = mapper
                .Where(m => m.Automatisk)
                .Select(m => m.Sti)
                .ToList();

            var tilbage = new List<Lydfund>();

            foreach (var f in fund)
            {
                var auto = automatiske.Any(a => f.Sti.StartsWith(a, StringComparison.OrdinalIgnoreCase));

                if (!auto) { tilbage.Add(f); continue; }

                try
                {
                    var svar = await Task.Run(() =>
                        Indlaesning.Indlaes(f.Sti, mappe: f.Folder));

                    Overvaagning.Husk(f.Sti);

                    Historik.Skriv(HaendelseType.Optagelse,
                                   Path.GetFileNameWithoutExtension(f.Sti),
                                   $"Lagt ind af sig selv fra {f.Mappenavn}",
                                   sti: svar.Mappe);

                    Lagtind?.Invoke(svar.Mappe);
                }
                catch (Exception)
                {
                    // Fejler den automatiske indlaesning, bliver filen staaende
                    // som et tilbud. Saa kan brugeren proeve selv og faa
                    // fejlen at se - i stedet for at filen forsvinder tavst.
                    tilbage.Add(f);
                }
            }

            var aendret = tilbage.Count != Ventende.Count
                || !tilbage.Select(f => f.Sti).SequenceEqual(Ventende.Select(f => f.Sti));

            Ventende = tilbage;
            if (aendret) Aendret?.Invoke();
        }
        catch (Exception)
        {
            // Et kig, der gaar galt, maa ikke vaelte appen. Der kigges igen om
            // et minut, og saa er den mappe, der ikke kunne laeses, maaske
            // tilbage.
        }
        finally
        {
            _kigger = false;
        }
    }

    /// <summary>
    /// Filen skal ikke tilbydes igen. Kaldes både efter en indlæsning og
    /// efter et nej — bogen svarer på «er der taget stilling», ikke på
    /// «blev den brugt».
    /// </summary>
    public static void Afgjort(string sti)
    {
        Overvaagning.Husk(sti);

        Ventende = Ventende.Where(f => !string.Equals(f.Sti, sti, StringComparison.OrdinalIgnoreCase)).ToList();
        Aendret?.Invoke();
    }
}
