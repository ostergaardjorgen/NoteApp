namespace NoteApp.Core.Deling;

/// <summary>Hvor tit arkivet skal ses efter.</summary>
public enum Arkivtakt
{
    /// <summary>Hvert kvarter. Standarden.</summary>
    Kvarter,

    /// <summary>Hver time.</summary>
    Time,

    /// <summary>Hver fjerde time.</summary>
    Firetimer,

    /// <summary>To faste tidspunkter om dagen.</summary>
    Togange,

    /// <summary>Ét fast tidspunkt om dagen.</summary>
    Engang,

    /// <summary>Kun når der trykkes på knappen.</summary>
    Manuelt,
}

/// <summary>
/// Hvornår arkivet skal køre.
/// </summary>
/// <remarks>
/// ============ HVORFOR DET IKKE BARE ER ET TAL I MINUTTER ============
///
/// Et minuttal kan ikke sige «to gange om dagen, mellem 8 og 17». Det kan
/// kun sige «hver 270. minut», og så flytter tidspunkterne sig for hver dag,
/// alt efter hvornår maskinen sidst blev tændt. Den, der har valgt kl. 8 og
/// kl. 17, mener kl. 8 og kl. 17.
///
/// Derfor to slags: nogle takter tæller MELLEMRUM, andre rammer KLOKKESLÆT.
///
/// ============ DØGNET ER IKKE ARBEJDSTID ============
///
/// En maskine, der står tændt om natten, har ingen grund til at gennemgå
/// 352 filer over netværket kl. 03. Tidsrummet gør, at de hyppige takter kun
/// kører, når nogen faktisk laver noget.
///
/// De to klokkeslæt bruges begge steder, og det er med vilje: ved
/// <see cref="Arkivtakt.Kvarter"/> er de tidsrummets ender, ved
/// <see cref="Arkivtakt.Togange"/> ER de de to tidspunkter. Ét sæt tal at
/// forholde sig til frem for fire.
/// </remarks>
public static class Arkivplan
{
    /// <summary>Mellemrummet for de takter, der tæller tid. Null for dem, der rammer klokkeslæt.</summary>
    public static TimeSpan? Mellemrum(Arkivtakt takt) => takt switch
    {
        Arkivtakt.Kvarter => TimeSpan.FromMinutes(15),
        Arkivtakt.Time => TimeSpan.FromHours(1),
        Arkivtakt.Firetimer => TimeSpan.FromHours(4),
        _ => null,
    };

    /// <summary>Rammer takten faste klokkeslæt frem for et mellemrum?</summary>
    public static bool Faste(Arkivtakt takt) =>
        takt is Arkivtakt.Togange or Arkivtakt.Engang;

    /// <summary>
    /// Er det tid at køre?
    /// </summary>
    /// <param name="takt">Den valgte takt.</param>
    /// <param name="kunITidsrum">Skal de hyppige takter holde sig inden for tidsrummet?</param>
    /// <param name="fraKl">Første klokkeslæt — tidsrummets start, eller det første faste tidspunkt.</param>
    /// <param name="tilKl">Andet klokkeslæt — tidsrummets slut, eller det andet faste tidspunkt.</param>
    /// <param name="sidst">Hvornår der sidst blev kørt. Null når der aldrig er kørt.</param>
    /// <param name="nu">Klokken nu.</param>
    public static bool Er_det_tid(Arkivtakt takt, bool kunITidsrum, int fraKl, int tilKl,
                                  DateTimeOffset? sidst, DateTimeOffset nu)
    {
        if (takt == Arkivtakt.Manuelt) return false;

        if (Faste(takt)) return Fast_tid(takt, fraKl, tilKl, sidst, nu);

        var mellemrum = Mellemrum(takt) ?? TimeSpan.FromMinutes(15);

        if (kunITidsrum && !Indenfor(fraKl, tilKl, nu)) return false;

        // ============ ALDRIG KOERT ER ALTID TID ============
        //
        // Ellers ville en frisk installation vente et kvarter paa noget, den
        // kunne have gjort med det samme - og den foerste koersel er den, der
        // betyder mest.
        if (sidst is null) return true;

        return nu - sidst.Value >= mellemrum;
    }

    /// <summary>
    /// Er klokken inden for tidsrummet?
    /// </summary>
    /// <remarks>
    /// TIDSRUMMET MAA GERNE GAA OVER MIDNAT. «Fra 22 til 6» er et rigtigt svar
    /// for den, der arbejder om aftenen, og et tidsrum, der kun kunne gaa den
    /// ene vej, ville stille og roligt aldrig koere for ham.
    ///
    /// Er de to ens, er der intet tidsrum at tale om, og saa gaelder hele
    /// doegnet. Alternativet - at der aldrig koeres - er ikke det, nogen mener
    /// med at saette begge tal til 8.
    /// </remarks>
    public static bool Indenfor(int fraKl, int tilKl, DateTimeOffset nu)
    {
        var fra = Time(fraKl);
        var til = Time(tilKl);

        if (fra == til) return true;

        var t = nu.Hour;

        return fra < til ? t >= fra && t < til : t >= fra || t < til;
    }

    /// <summary>
    /// De faste tidspunkter: er dagens tur kommet, og er den ikke taget?
    /// </summary>
    /// <remarks>
    /// DER SPOERGES PAA DAGEN OG IKKE PAA MELLEMRUMMET. «Sidst for 24 timer
    /// siden» ville lade tidspunktet glide en smule for hver dag, indtil kl. 8
    /// var blevet kl. 11. Her er spoergsmaalet, om der er koert siden DAGENS
    /// klokkeslaet - og det svar flytter sig ikke.
    ///
    /// EN SLUKKET MASKINE TAGER TUREN, NAAR DEN TAENDES. Var maskinen slukket
    /// kl. 8, koeres der kl. 9.30 i stedet. Alternativet - at springe dagen
    /// over - er den slags, man opdager en uge senere.
    /// </remarks>
    private static bool Fast_tid(Arkivtakt takt, int fraKl, int tilKl,
                                 DateTimeOffset? sidst, DateTimeOffset nu)
    {
        var tider = takt == Arkivtakt.Engang
            ? new[] { Time(fraKl) }
            : new[] { Time(fraKl), Time(tilKl) }.Distinct().OrderBy(t => t).ToArray();

        // Det seneste tidspunkt, der er passeret i dag.
        var forfalden = tider.Where(t => nu.Hour >= t)
                             .Select(t => new DateTimeOffset(nu.Year, nu.Month, nu.Day, t, 0, 0, nu.Offset))
                             .OrderByDescending(t => t)
                             .FirstOrDefault();

        if (forfalden == default) return false;

        return sidst is null || sidst.Value < forfalden;
    }

    private static int Time(int kl) => Math.Clamp(kl, 0, 23);
}
