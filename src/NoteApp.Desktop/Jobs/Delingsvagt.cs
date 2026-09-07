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

            // ============ LIGGER DER EN NOEGLE OG VENTER? ============
            //
            // Den anden computer har trykket «Send API-noeglen hertil». Den
            // ligger krypteret i den faelles mappe og kan kun aabnes her - se
            // Noegledeling. Den hentes af sig selv, fordi et menneske
            // allerede har taget beslutningen paa den anden skaerm; at skulle
            // sige ja to gange til det samme er ikke en sikkerhed, det er en
            // forhindring.
            switch (Noegledeling.Hent())
            {
                case Noegledeling.Udfald.Hentet:
                    Historik.Skriv(HaendelseType.Andet,
                        "API-noeglen er modtaget fra en anden computer",
                        "Noeglen laa krypteret i den faelles mappe og er nu gemt paa den her "
                        + "computer. Kuverten er ryddet.",
                        Udfald.Fuldført);
                    break;

                case Noegledeling.Udfald.Afvist:
                    Historik.Skriv(HaendelseType.Andet,
                        "En noeglekuvert blev afvist",
                        "Der laa en kuvert i den faelles mappe, men den kunne ikke aabnes - den "
                        + "var udloebet, eller den kom fra en computer, der ikke er godkendt. "
                        + "Den er ryddet.",
                        Udfald.SeEfter);
                    break;
            }
        }
        catch (Exception)
        {
            // Et hjerteslag, der ikke naaede frem, er ikke en fejl, nogen skal
            // se. Naeste slag kommer om fem minutter.
        }
    }
}
