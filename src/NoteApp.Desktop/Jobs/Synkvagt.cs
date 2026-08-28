using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Henter aftaler og opgaver ned hvert kvarter.
///
/// HVORFOR DEN FINDES
///
/// Der blev aldrig hentet af sig selv. En aftale, der blev oprettet direkte i
/// Google, var ikke i HeyPia, før nogen gik ind under Indstillinger og
/// trykkede Hent — og så viste kalenderen i Cockpittet bare, hvad der sidst
/// tilfældigvis blev hentet. Ikonet på panelerne (24-08) gjorde det muligt at
/// hente ét sted fra; den her gør, at man ikke skal huske det.
///
/// ET KVARTER
///
/// Kort nok til, at dagen er rigtig, når man sætter sig — og langt nok til at
/// være gratis. En hentning er to HTTP-kald og et par kilobytes.
///
/// DEN KØRER OGSÅ UNDER EN OPTAGELSE, og det er et bevidst valg.
/// Hovedreglen i appen er, at optagelse aldrig må forstyrres af noget andet.
/// Den her rører hverken mikrofonen, grafikkortet eller disken ud over to små
/// filer, og den venter ikke på nogen lås. Der er intet, den kan komme i vejen
/// for.
///
/// DEN GØR OPMÆRKSOM PÅ SIG SELV I ÉT TILFÆLDE: aldrig.
/// Fejler en hentning, står fejlen på integrationens linje og under ikonet i
/// Cockpittet — dér, hvor man kigger, når man undrer sig. En besked midt i et
/// møde, fordi Google ikke svarede, ville være værre end problemet.
/// </summary>
public static class Synkvagt
{
    private static DispatcherTimer? _ur;
    private static bool _henter;

    /// <summary>Hvor tit der hentes.</summary>
    public static readonly TimeSpan Mellemrum = TimeSpan.FromMinutes(15);

    /// <summary>Meldes, når der er hentet — også når det gik galt. Skærmene lytter med.</summary>
    public static event Action? Hentet;

    public static void Start()
    {
        if (_ur is not null) return;

        _ur = new DispatcherTimer(DispatcherPriority.Background) { Interval = Mellemrum };
        _ur.Tick += (_, _) => _ = Hent();
        _ur.Start();

        // DER HENTES VED OPSTART. Det er det vigtigste enkelte tidspunkt: saa
        // er dagen rigtig, naar man saetter sig ved maskinen.
        //
        // Er netvaerket ikke oppe endnu, fejler den i stilhed, og uret proever
        // igen om et kvarter. Derfor ingen genforsoegslogik her - den ville
        // vaere en anden maade at skrive "vent et kvarter" paa.
        _ = Hent();
    }

    /// <summary>
    /// Henter nu. Kaldes af uret, af opstarten — og kan kaldes af en skærm.
    ///
    /// To hentninger ad gangen er der ingen grund til: den anden ville hente
    /// præcis det samme og skrive oven i den første.
    /// </summary>
    public static async Task Hent()
    {
        if (_henter) return;

        var kalender = Synkronisering.Kalenderklar;
        var opgaver = Synkronisering.Opgaverklar;

        // Er intet forbundet, er der ingenting at goere - og saa koster vagten
        // heller ingenting for den, der ikke bruger integrationerne.
        if (!kalender && !opgaver) return;

        _henter = true;

        try
        {
            if (kalender) await Synkronisering.Kalenderen();
            if (opgaver) await Synkronisering.Opgaverne();

            Hentet?.Invoke();
        }
        catch (Exception)
        {
            // Synkronisering fanger selv sine fejl og skriver dem paa
            // integrationen. Naar noget alligevel slipper ud, er svaret det
            // samme: ti stille, og proev igen om et kvarter.
        }
        finally
        {
            _henter = false;
        }
    }
}
