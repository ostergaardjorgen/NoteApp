namespace NoteApp.Core.Llm;

/// <summary>
/// Holder øje med, at pudsningen ikke finder på noget.
/// </summary>
/// <remarks>
/// DEN FINDES PÅ GRUND AF EN MAIL, INGEN HAVDE SAGT.
///
/// Målt 31-08-2026: brugeren dikterede et par sætninger og bad om formen
/// «mail». Tilbage kom et helt brev med en prioriteret opgaveliste,
/// deadlines, mødetidspunkter og et «[Dit navn]». Intet af det var sagt, og
/// teksten så helt igennem rigtig ud — det er dét, der gør den farlig. Den
/// slags sendes videre med brugerens navn under.
///
/// INSTRUKTIONEN ER IKKE NOK I SIG SELV. Den kan strammes, og det er den —
/// men en sprogmodel, der er bedt om at lave en mail, laver en mail. Derfor
/// måles svaret bagefter: er der kommet meget mere tekst ud, end der gik ind,
/// er det ikke oprydning længere. Så er det digtning.
///
/// TALLET ER IKKE PYNT. En oprydning fjerner som regel ord — fyldord,
/// gentagelser, tøven. Kommer der en hilsen og en afsked på, vokser en kort
/// besked nogle få ord. Bliver den TRE GANGE så lang og mere end tredive ord
/// længere, er der kommet indhold til, som ingen har sagt.
/// </remarks>
public static class Pudsevagt
{
    /// <summary>Hvor mange gange længere svaret må være.</summary>
    public const double Faktor = 3.0;

    /// <summary>
    /// Hvor mange ord der må komme til uanset faktoren.
    /// </summary>
    /// <remarks>
    /// «Hej. Ja, det lyder godt. Med venlig hilsen» er tre gange længere end
    /// «det lyder godt», og det er en rigtig mail. Under tredive ekstra ord
    /// er der ikke plads til en opdigtet opgaveliste.
    /// </remarks>
    public const int Frie = 30;

    /// <summary>Ord i en tekst. Tegnsætning tælles ikke med som ord.</summary>
    public static int Ord(string? tekst) =>
        string.IsNullOrWhiteSpace(tekst)
            ? 0
            : tekst.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Er svaret vokset så meget, at der må være kommet indhold til?
    /// </summary>
    public static bool ErOppustet(string? raa, string? pudset)
    {
        var ind = Ord(raa);
        var ud = Ord(pudset);

        if (ind == 0) return false;          // intet at sammenligne med
        if (ud <= ind + Frie) return false;  // en hilsen og en afsked er ikke digtning

        return ud > ind * Faktor;
    }
}
