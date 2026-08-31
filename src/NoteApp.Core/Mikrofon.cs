namespace NoteApp.Core;

/// <summary>
/// Den valgte mikrofon — ét sted, for hele appen.
/// </summary>
/// <remarks>
/// DEN FINDES, FORDI DER VAR TI STEDER, DER SVAREDE PÅ DET SAMME SPØRGSMÅL.
///
/// «Hvilken mikrofon har brugeren valgt?» blev besvaret hver for sig i
/// opsætningen, i indstillingerne, i mødeskærmen, i startklar-kontrollen, i
/// diktatvagten, i føroptageren, i hovedvinduet og i værktøjet. Otte kopier
/// af den samme opgave — og en niende i vågeordsvagten, som oven i købet
/// arbejder med en HELT ANDEN identitet: motorens eget SDL-nummer, som den
/// finder ved at læse motorens opstart og sammenligne navne.
///
/// Det er dét, der gør, at mikrofonen «bliver væk». Det er ikke ét sted, der
/// glemmer den. Det er ni steder, der hver især har en mening, og som ikke
/// bliver enige, når noget skifter: Windows' standard laves om, en enhed
/// tages ud, en indstilling gemmes ét sted og læses et andet.
///
/// TRE SPØRGSMÅL, ÉT SVAR HVER:
///
///   Valgt()      hvilken enhed skal bruges — og faldt vi tilbage på en anden
///   Nummer()     hvad hedder den hos lyttemotoren, som tæller sine egne
///   Vaelg()      sæt en ny, og glem det, der hørte til den gamle
///
/// HVORFOR NUMMERET RYDDES SAMMEN MED VALGET
///
/// Motorens nummer hører til ÉN mikrofon. Vælger man en anden og beholder
/// nummeret, lytter vågeordet et andet sted, end appen siger. Det er værre
/// end ikke at lytte: skærmen påstår noget, der ikke passer.
/// </remarks>
public static class Mikrofon
{
    /// <summary>
    /// Den mikrofon, der skal bruges lige nu.
    /// </summary>
    /// <param name="erstatning">
    /// Sat, hvis den valgte ikke fandtes, og der blev taget en anden. Det
    /// SKAL kunne ses: en optagelse på den forkerte mikrofon ligner en
    /// optagelse, indtil man hører den.
    /// </param>
    public static DeviceInfo? Valgt(out bool erstatning) =>
        AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out erstatning);

    /// <summary>Den valgte mikrofon, når det ikke betyder noget, hvordan den blev fundet.</summary>
    public static DeviceInfo? Valgt() => Valgt(out _);

    /// <summary>Navnet, som brugeren kender det. Tom, hvis der ingen er.</summary>
    public static string Navn() => Valgt()?.FriendlyName ?? "";

    /// <summary>Er der overhovedet en mikrofon at optage med?</summary>
    public static bool Findes() => Valgt() is not null;

    /// <summary>
    /// Mikrofonens nummer hos lyttemotoren — eller −1, hvis det ikke er fundet.
    /// </summary>
    /// <remarks>
    /// MOTOREN TÆLLER SINE EGNE. Windows kender enhederne på et id, motoren
    /// kender dem på et nummer, den selv har givet dem, og de to har intet
    /// med hinanden at gøre. Broen er NAVNET, og derfor gemmes navnet sammen
    /// med nummeret: passer det ikke længere, er enhederne rykket rundt, og
    /// nummeret peger nu på en anden mikrofon.
    /// </remarks>
    public static int Nummer()
    {
        var v = AppSettings.Current;

        if (v.VaageordMikrofonNummer is not { } nr) return -1;
        if (string.IsNullOrWhiteSpace(v.VaageordMikrofonNavn)) return -1;

        var valgt = Valgt();
        if (valgt is null) return -1;

        return v.VaageordMikrofonNavn.Equals(valgt.FriendlyName, StringComparison.OrdinalIgnoreCase)
            ? nr
            : -1;
    }

    /// <summary>Husker, hvad motoren kalder den mikrofon, der er valgt nu.</summary>
    public static void HuskNummer(int nr, string motornavn)
    {
        var v = AppSettings.Current;
        v.VaageordMikrofonNummer = nr;
        v.VaageordMikrofonNavn = motornavn;
        v.Save();
    }

    /// <summary>
    /// Vælger en mikrofon.
    /// </summary>
    /// <remarks>
    /// HER RYDDES MOTORENS NUMMER MED. Det hørte til den gamle enhed, og et
    /// tal, der peger forkert, er værre end intet tal: så findes det bare
    /// forfra næste gang motoren starter.
    /// </remarks>
    public static void Vaelg(string? enhedsId)
    {
        var v = AppSettings.Current;

        if (string.Equals(v.MicrophoneId, enhedsId, StringComparison.OrdinalIgnoreCase)) return;

        v.MicrophoneId = enhedsId;
        v.VaageordMikrofonNummer = null;
        v.VaageordMikrofonNavn = null;
        v.Save();

        Skiftet?.Invoke();
    }

    /// <summary>Siger til, når mikrofonen er skiftet — så lytningen kan følge med.</summary>
    public static Action? Skiftet { get; set; }
}
