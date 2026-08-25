namespace NoteApp.Core;

/// <summary>Sproget til en udskrift — og hvor svaret kom fra.</summary>
public sealed record Sprogsvar(string? Mit, string? Deres, string Kilde)
{
    /// <summary>Kan udskriften starte uden at spørge?</summary>
    public bool Kendt => !string.IsNullOrWhiteSpace(Mit);
}

/// <summary>
/// Hvad appen ved om en optagelse, uden at spørge.
///
/// HVORFOR DEN FINDES
///
/// «Transskription er default og går i gang, så snart det er muligt.» Det
/// kræver, at sproget er kendt — for et gæt er dyrere end en ventetid. En
/// udskrift på det forkerte sprog ligner en færdig tekst, og fejlen opdages
/// først i referatet.
///
/// Derfor spørges der **én gang**, og svaret huskes. Ikke pr. møde: pr.
/// bruger. Man holder ikke sine møder på et nyt sprog hver gang.
///
/// RÆKKEFØLGEN ER IKKE TILFÆLDIG
///
/// 1. Det, der allerede står på optagelsen — den er skrevet ud før, og så er
///    svaret afgjort.
/// 2. Aftalen, mødet kom fra. Den kan bære et sprog, sat på forhånd.
/// 3. Indstillingen «det sprog, jeg taler».
/// 4. Ingenting. Så skal der spørges, og svaret gemmes i indstillingen.
/// </summary>
public static class Udskriftsvalg
{
    /// <summary>
    /// Sproget, der skal bruges — eller <c>Kendt == false</c>, hvis der skal
    /// spørges.
    /// </summary>
    public static Sprogsvar Sprog(string moedemappe, bool kunHoejttaler = false)
    {
        MeetingMetadata? meta = null;
        try { meta = MeetingStore.Load(moedemappe); } catch (Exception) { }

        // 1. Optagelsen selv. Er den skrevet ud foer, er svaret givet - og et
        //    nyt svar ville give en udskrift, der ikke kan sammenlignes med
        //    den gamle.
        var paaOptagelsen = kunHoejttaler ? meta?.ValgtSprogLoop : meta?.ValgtSprogMik;

        if (!string.IsNullOrWhiteSpace(paaOptagelsen))
            return new Sprogsvar(paaOptagelsen, meta?.ValgtSprogLoop, "optagelsen");

        // 2. Aftalen. Den kan baere et sprog, sat da moedet blev lagt i
        //    kalenderen - saa er der taget stilling, foer mikrofonen gik i
        //    gang.
        var fraAftalen = Fraaftalen(meta);
        if (!string.IsNullOrWhiteSpace(fraAftalen))
            return new Sprogsvar(fraAftalen, fraAftalen, "aftalen");

        // 3. Indstillingen.
        var s = AppSettings.Current;

        if (!string.IsNullOrWhiteSpace(s.MitSprog))
            return new Sprogsvar(s.MitSprog, s.DeresSprog ?? s.MitSprog, "indstillingerne");

        // 4. Der skal spoerges.
        return new Sprogsvar(null, null, "");
    }

    /// <summary>Sproget på den aftale, mødet kom fra. Null, hvis der ingen er.</summary>
    private static string? Fraaftalen(MeetingMetadata? meta)
    {
        if (meta is null) return null;

        var id = meta.Id.ToString();

        try
        {
            return Kalender.Alle()
                .FirstOrDefault(a => string.Equals(a.MoedeId, id, StringComparison.OrdinalIgnoreCase))
                ?.Sprog;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Husker svaret, så der ikke spørges igen.
    ///
    /// Kun når der IKKE står noget i forvejen. Har brugeren selv sat et sprog
    /// under Indstillinger, skal ét møde på engelsk ikke skrive det om.
    /// </summary>
    public static void Husk(string? mit, string? deres)
    {
        if (string.IsNullOrWhiteSpace(mit)) return;

        var s = AppSettings.Current;
        if (!string.IsNullOrWhiteSpace(s.MitSprog)) return;

        s.MitSprog = mit;
        if (!string.IsNullOrWhiteSpace(deres)) s.DeresSprog = deres;

        s.Save();
    }
}
