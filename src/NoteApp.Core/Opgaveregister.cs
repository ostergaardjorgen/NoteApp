namespace NoteApp.Core;

/// <summary>En opgave med det møde, den kom fra.</summary>
/// <param name="Mappe">Optagelsens mappe på disken — dér ligger opgavefilen.</param>
/// <param name="MoedeId">Mødets id. Det er DEN, der åbner mødet, ikke stien.</param>
public sealed record Registeropgave(Opgave Opgave, string Mappe, string MoedeId, string Moedetitel)
{
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
/// Alle opgaver på tværs af alle optagelser.
///
/// HVORFOR DE IKKE LIGGER I ÉN FIL
///
/// Hver optagelse har sin egen `opgaver.json` ved siden af lyden. Det er
/// bevidst: sletter man et møde, forsvinder dets opgaver med det, en
/// sikkerhedskopi af mappen indeholder alt om mødet, og der er ingen central
/// fil, der kan blive uenig med virkeligheden.
///
/// Prisen er, at et samlet overblik skal læse alle mapper igennem. Målt: en
/// mappe koster et filopslag, og der er tale om hundredvis, ikke millioner.
/// Det er den samme afvejning som i søgningen — læs kilden, hold ikke en kopi.
/// </summary>
public static class Opgaveregister
{
    /// <summary>
    /// Alle opgaver, nyeste møde først.
    ///
    /// Der læses direkte fra disken hver gang. En liste, der kan komme ud af
    /// trit med filerne, ville vise en opgave, man netop har krydset af.
    /// </summary>
    public static List<Registeropgave> Alle()
    {
        var ud = new List<Registeropgave>();

        if (!Directory.Exists(UserDataPaths.Meetings)) return ud;

        foreach (var mappe in Directory.EnumerateDirectories(UserDataPaths.Meetings))
        {
            if (!File.Exists(Opgaveliste.Sti(mappe))) continue;

            MeetingMetadata? meta = null;
            try { meta = MeetingStore.Load(mappe); } catch (Exception) { }

            var liste = Opgaveliste.Hent(mappe);

            foreach (var o in liste.Opgaver)
                ud.Add(new Registeropgave(o, mappe,
                    meta?.Id.ToString() ?? "",
                    meta?.Title ?? Path.GetFileName(mappe)));
        }

        return ud;
    }

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
    public static List<Registeropgave> Aabne(DateOnly idag) =>
        Alle()
            .Where(r => !r.Opgave.Faerdig || r.Opgave.FaerdigIDag(idag))
            .OrderBy(r => (int)r.Hastighed(idag))
            .ThenBy(r => r.Opgave.Prioritet == 0 ? 4 : r.Opgave.Prioritet)
            .ThenBy(r => r.Opgave.Deadline ?? DateTimeOffset.MaxValue)
            .ToList();

    /// <summary>
    /// Gemmer en ændret opgave tilbage i den fil, den kom fra.
    ///
    /// Der læses ind igen først. Filen kan være ændret, siden listen blev
    /// bygget — man kan have haft mødet åbent i den anden ende af appen — og
    /// et blindt overskriv ville smide den anden ændring væk.
    /// </summary>
    public static void Gem(Registeropgave r)
    {
        var liste = Opgaveliste.Hent(r.Mappe);
        var nr = liste.Opgaver.FindIndex(o => o.Id == r.Opgave.Id);

        if (nr < 0) liste.Opgaver.Add(r.Opgave);
        else liste.Opgaver[nr] = r.Opgave;

        liste.Gem(r.Mappe);
    }
}
