namespace NoteApp.Core;

/// <summary>
/// En opgave, som listerne viser den.
///
/// HERKOMSTEN LIGGER PAA OPGAVEN SELV. Typen havde før en Mappe — stien til
/// optagelsen, hvor opgavefilen lå. Den findes ikke mere: alle opgaver ligger
/// samlet, og hvor de kom fra, er felter på opgaven. Se <see cref="Opgavelager"/>.
///
/// Typen bliver stående, fordi skærmene binder til den — og fordi den er det
/// naturlige sted at regne hastigheden ud.
/// </summary>
public sealed record Registeropgave(Opgave Opgave)
{
    public string MoedeId => Opgave.MoedeId;
    public string Moedetitel => Opgave.Moedetitel.Length > 0 ? Opgave.Moedetitel : "Skrevet i hånden";

    /// <summary>Hvor presserende er den — set fra i dag?</summary>
    public Hastighed Hastighed(DateOnly idag)
    {
        if (Opgave.Faerdig) return NoteApp.Core.Hastighed.Faerdig;
        if (Opgave.Deadline is not { } d) return NoteApp.Core.Hastighed.Uden_frist;

        var dage = DateOnly.FromDateTime(d.LocalDateTime).DayNumber - idag.DayNumber;

        return dage switch
        {
            < 0 => NoteApp.Core.Hastighed.Overskredet,
            0 => NoteApp.Core.Hastighed.I_dag,
            <= 7 => NoteApp.Core.Hastighed.Denne_uge,
            _ => NoteApp.Core.Hastighed.Senere
        };
    }
}

/// <summary>
/// Hvor presserende en opgave er.
///
/// DET ER FRISTEN, DER GIVER FARVEN — IKKE PRIORITETEN.
///
/// De to er ikke det samme, og det er en fejl at blande dem: en opgave med
/// prioritet 1 om tre uger haster ikke i dag, og en prioritet 3, der skulle
/// have været lavet i mandags, gør. Farven svarer på «hvad skal jeg gøre nu»;
/// prioriteten svarer på «hvad er vigtigst, når jeg har tid til ét af dem».
/// </summary>
public enum Hastighed
{
    Overskredet,
    I_dag,
    Denne_uge,
    Senere,
    Uden_frist,
    Faerdig
}

/// <summary>
/// Opgaverne, som skærmene skal bruge dem: sorteret, filtreret og med
/// herkomsten på.
///
/// SELVE OPBEVARINGEN LIGGER I <see cref="Opgavelager"/>, og dér står også,
/// hvorfor opgaver flyttede ud af optagelsernes mapper 24-08-2026.
///
/// Der læses fra disken ved hvert opslag. Det er den samme afvejning som i
/// søgningen — læs kilden, hold ikke en kopi, der kan blive uenig med den.
/// </summary>
public static class Opgaveregister
{
    /// <summary>
    /// Alle opgaver, nyeste først.
    ///
    /// Der læses direkte fra disken hver gang. En liste, der kan komme ud af
    /// trit med filen, ville vise en opgave, man netop har krydset af.
    /// </summary>
    public static List<Registeropgave> Alle() =>
        Opgavelager.Alle()
            .OrderByDescending(o => o.Oprettet)
            .Select(o => new Registeropgave(o))
            .ToList();

    /// <summary>
    /// De opgaver, der ikke er færdige — sorteret efter, hvad man skal se på.
    ///
    /// RÆKKEFØLGEN ER: overskredet, i dag, denne uge, senere, uden frist.
    /// Inden for hver gruppe efter prioritet, og derefter efter frist.
    ///
    /// Opgaver UDEN frist står nederst og ikke øverst. De er ikke uvigtige —
    /// men de er heller ikke noget, der skal ske i dag, og en liste, der
    /// begynder med tyve fristløse punkter, bliver ikke læst til ende.
    /// </summary>
    /// <remarks>
    /// DAGENS AFKRYDSEDE ER MED — dæmpet og nederst.
    ///
    /// En opgave, der forsvinder i det sekund, man sætter fluebenet, giver
    /// ingen kvittering: man ved ikke, om man ramte den rigtige, og man kan
    /// ikke fortryde uden at lede efter den. Den bliver stående dagen ud og
    /// er væk i morgen.
    ///
    /// De havner nederst af sig selv — Hastighed.Faerdig er sidste værdi i
    /// enum'en, og der sorteres på den.
    /// </remarks>
    public static List<Registeropgave> Aabne(DateOnly idag)
    {
        var aabne = Alle()
            .Where(r => !r.Opgave.Faerdig || r.Opgave.FaerdigIDag(idag))
            .ToList();

        // ============ DIN EGEN RAEKKEFOELGE VINDER OVER ALT ============
        //
        // Ogsaa over fristen. Det er hele pointen: en liste, der bliver ved
        // med at sortere sig selv om, er ikke din - og «hvad vil JEG tage
        // foerst» er ikke et spoergsmaal, en dato kan svare paa.
        //
        // De afkrydsede er stadig nederst. En opgave, man lige har sat flueben
        // ved, skal ikke blive staaende midt i listen resten af dagen.
        if (AppSettings.Current.OpgaverManueltSorteret)
            return aabne
                .OrderBy(r => r.Opgave.Faerdig ? 1 : 0)
                .ThenBy(r => r.Opgave.Raekkefoelge)
                .ToList();

        return aabne
            .OrderBy(r => (int)r.Hastighed(idag))
            .ThenBy(r => r.Opgave.Prioritet == 0 ? 4 : r.Opgave.Prioritet)
            .ThenBy(r => r.Opgave.Deadline ?? DateTimeOffset.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Skriver den rækkefølge ned, listen står i nu.
    /// </summary>
    /// <remarks>
    /// HELE LISTEN STEMPLES, ikke kun den, der blev flyttet. Første gang der
    /// trækkes, står der nul på dem alle sammen, og et enkelt nyt tal ville
    /// ikke sige noget om, hvor de andre skal stå.
    ///
    /// Der gemmes ÉN gang. <c>Opgavelager.Gem(o)</c> læser hele filen ind for
    /// hver opgave, og det ville være tyve læsninger og tyve skrivninger for
    /// ét træk.
    ///
    /// Opgaver, der ikke er med i rækken — de færdige fra i går, dem der er
    /// filtreret fra — beholder deres eget tal. De er ikke flyttet.
    /// </remarks>
    public static void SaetRaekkefoelge(IReadOnlyList<Guid> raekken)
    {
        if (raekken.Count == 0) return;

        var alle = Opgavelager.Alle();

        for (var nr = 0; nr < raekken.Count; nr++)
        {
            var o = alle.FirstOrDefault(x => x.Id == raekken[nr]);
            if (o is not null) o.Raekkefoelge = nr + 1;
        }

        Opgavelager.Gem(alle);

        AppSettings.Current.OpgaverManueltSorteret = true;
        AppSettings.Current.Save();
    }

    /// <summary>
    /// Tallet, en ny opgave skal have, så den lander øverst.
    /// </summary>
    /// <remarks>
    /// EN NY OPGAVE MAA IKKE FORSVINDE NEDERST. Har man selv sat rækkefølgen,
    /// har alt andet et tal, og en ny med nul ville lægge sig først af sig
    /// selv — men kun indtil nogen trækker igen og stempler hele listen om.
    /// Derfor sættes den eksplicit under den mindste, der findes.
    ///
    /// Er rækkefølgen ikke sat i hånden, svares nul: så bestemmer fristen,
    /// som den altid har gjort.
    /// </remarks>
    public static double NyestePlads()
    {
        if (!AppSettings.Current.OpgaverManueltSorteret) return 0;

        try
        {
            var mindste = Opgavelager.Alle()
                .Select(o => o.Raekkefoelge)
                .DefaultIfEmpty(1)
                .Min();

            return mindste - 1;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>
    /// Gemmer en ændret opgave.
    ///
    /// Lageret læser filen ind igen først. Den kan være ændret, siden listen
    /// blev bygget — man kan have haft opgaven åben i den anden ende af appen
    /// — og et blindt overskriv ville smide den anden ændring væk.
    /// </summary>
    public static void Gem(Registeropgave r) => Opgavelager.Gem(r.Opgave);

    public static void Slet(Registeropgave r) => Opgavelager.Slet(r.Opgave.Id);
}
