using System.Windows.Threading;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Melder den her computer i den fælles mappe, så den anden kan se, at den er
/// her.
/// </summary>
/// <remarks>
/// ============ HJERTESLAGET ER TIL BRUGEREN, IKKE TIL MASKINEN ============
///
/// Der er ikke noget, der går i stykker, hvis et slag springes over. Det er
/// dét, der gør, at skærmen kan skrive «sidst set i går kl. 21» frem for bare
/// at vente — og en bruger, der ikke kan se, at den anden computer er slukket,
/// tror, appen er i stykker.
///
/// ============ FEM MINUTTER ============
///
/// Kortere ville betyde en fil skrevet gennem en synkroniseringsklient hvert
/// øjeblik, og det er ikke gratis for hverken drev eller båndbredde. Længere
/// ville få «her lige nu» til at halte bagefter virkeligheden. Grænsen for,
/// hvornår en computer regnes for væk, er tyve minutter — se
/// <see cref="Maskinoplysning.ILive"/> — så der er plads til, at et slag
/// forsvinder undervejs.
///
/// ============ DEN MÅ ALDRIG STÅ I VEJEN ============
///
/// Samme regel som <see cref="Mappevagt"/>: den skriver én lille fil, tager
/// ingen lås og venter ikke på noget. Er drevet væk, prøver den igen om fem
/// minutter.
/// </remarks>
public static class Delingsvagt
{
    private static DispatcherTimer? _ur;

    /// <summary>Hvor tit den her computer melder sig.</summary>
    public static readonly TimeSpan Mellemrum = TimeSpan.FromMinutes(5);

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Mellemrum };
        _ur.Tick += (_, _) => Meld();
        _ur.Start();

        // Der meldes med det samme. Aabner man appen paa den baerbare for at
        // se, om den stationaere er der, skal man ikke vente fem minutter paa,
        // at man selv dukker op paa den anden skaerm.
        Meld();
    }

    private static void Meld()
    {
        try
        {
            if (Delt.Mappe is null) return;

            Delt.Meld();

            // ============ LIGGER DER NOGET OG VENTER? ============
            //
            // Den anden computer har trykket «Send opsaetningen». Den ligger
            // krypteret i den faelles mappe og kan kun aabnes her - se
            // Noegledeling. Den hentes af sig selv, fordi et menneske
            // allerede har taget beslutningen paa den anden skaerm; at skulle
            // sige ja to gange til det samme er ikke en sikkerhed, det er en
            // forhindring.
            // TALLET VED MENUPUNKTET FOELGER MED. Det er her, den anden
            // computer bliver opdaget - og den, der sidder og venter paa, at
            // den melder sig, skal ikke skulle aabne Indstillinger for at se,
            // at den er kommet.
            (System.Windows.Application.Current?.MainWindow as MainWindow)
                ?.OpdaterOpsaetningsmaerkat();

            foreach (var (slags, udfald) in Noegledeling.HentAlle())
            {
                var api = slags == Kuvertslags.Apinoegle;

                if (udfald == Noegledeling.Udfald.Hentet)
                {
                    Historik.Skriv(HaendelseType.Andet,
                        api
                            ? "API-nøglen er modtaget fra din anden computer"
                            : "Google-forbindelsen er modtaget fra din anden computer",
                        api
                            ? "Nøglen lå krypteret i den fælles mappe og er nu gemt på den her "
                              + "computer. Du kan lave referater og dokumenter med det samme."
                            : "Forbindelsen lå krypteret i den fælles mappe. Din kalender og dine "
                              + "opgaver kommer ind her ved næste hentning — du skal ikke logge "
                              + "ind hos Google en gang til.",
                        Udfald.Fuldført);
                }
                else if (udfald == Noegledeling.Udfald.Afvist)
                {
                    Historik.Skriv(HaendelseType.Andet,
                        "Noget fra din anden computer blev afvist",
                        "Der lå noget til den her computer i den fælles mappe, men det kunne ikke "
                        + "åbnes — enten var det for gammelt, eller også kom det fra en computer, "
                        + "der ikke er godkendt. Det er ryddet væk.",
                        Udfald.SeEfter);
                }
            }
        }
        catch (Exception)
        {
            // Et hjerteslag, der ikke naaede frem, er ikke en fejl, nogen skal
            // se. Naeste slag kommer om fem minutter.
        }
    }
}
