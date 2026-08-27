namespace NoteApp.Desktop.Jobs;

/// <summary>
/// Hvem venter på hvem, når et møde slutter.
///
/// PROBLEMET DEN LØSER
///
/// Når optagelsen stopper, sker to ting på én gang: medskrivningen skriver
/// halen færdig, og den automatiske transskription går i gang. Den anden
/// kigger efter, om der allerede ligger en udskrift, den kan genbruge — og
/// hvis halen ikke er skrevet ned endnu, finder den ingenting og skriver hele
/// mødet ud forfra.
///
/// Målt 27-08-2026 på et webinar på 36 minutter: optagelsen stoppede 15:58:47,
/// halen blev færdig 15:59, og imellem de to nåede den automatiske udskrivning
/// at gå i gang. Otte bidders arbejde blev lavet om — fire minutters
/// grafikkort til ingen verdens nytte.
///
/// LØSNINGEN ER AT SIGE DET HØJT. Medskrivningen lægger sin opgave her, når
/// den begynder på halen, og fjerner den, når filerne ligger. Den, der vil
/// skrive ud, spørger først — og venter, hvis der er noget at vente på.
///
/// DER VENTES ALDRIG I BLINDE. Er der ingen opgave for mappen, svares der med
/// det samme. Går halen i stå, er der en grænse: se <see cref="Vent"/>.
/// </summary>
public static class Medskrivning
{
    /// <summary>
    /// Hvor længe der højst ventes.
    ///
    /// To minutter. Halen er højst fem minutters lyd, og den tog 20–41
    /// sekunder pr. bid i den måling, der findes. To minutter er rigeligt og
    /// samtidig kort nok til, at en hængt medskrivning ikke låser mødet fast.
    ///
    /// Løber tiden ud, skrives der bare ud på den almindelige måde. Det koster
    /// tid, ikke rigtighed.
    /// </summary>
    private static readonly TimeSpan Taalmodighed = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<string, Task> Igang =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly object Laas = new();

    /// <summary>Meld, at halen skrives for den her optagelse.</summary>
    public static void Meld(string mappe, Task arbejde)
    {
        lock (Laas) Igang[mappe] = arbejde;
    }

    /// <summary>Meld, at den er færdig — uanset hvordan det gik.</summary>
    public static void Faerdig(string mappe)
    {
        lock (Laas) Igang.Remove(mappe);
    }

    /// <summary>
    /// Venter på, at halen er skrevet ned — hvis der er en hale.
    /// </summary>
    /// <remarks>
    /// Kaldes af den, der vil skrive optagelsen ud. Er der ingen medskrivning
    /// i gang, kommer den tilbage med det samme, og alt er som før.
    /// </remarks>
    public static async Task Vent(string mappe, CancellationToken ct = default)
    {
        Task? arbejde;
        lock (Laas) Igang.TryGetValue(mappe, out arbejde);

        if (arbejde is null) return;

        try
        {
            await arbejde.WaitAsync(Taalmodighed, ct);
        }
        catch (Exception)
        {
            // Tiden loeb ud, eller halen gik galt. Saa skrives der ud paa den
            // almindelige maade - det koster tid, ikke rigtighed.
        }
        finally
        {
            Faerdig(mappe);
        }
    }
}
