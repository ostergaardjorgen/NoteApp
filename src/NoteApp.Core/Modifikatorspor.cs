namespace NoteApp.Core;

/// <summary>
/// Holder styr på Ctrl, Shift og Alt ud fra de tastetryk, hooken selv ser.
/// </summary>
/// <remarks>
/// ============ HVORFOR DER IKKE SPØRGES ============
///
/// Der findes to Windows-funktioner, der kan svare på, om Shift er nede, og
/// de blev prøvet begge to. Ingen af dem duer, og målingen 05-09-2026 siger
/// hvorfor — se <see cref="Se"/>. Derfor bogfører hooken det selv: den ser
/// hvert eneste tastetryk på maskinen i forvejen.
///
/// Klassen ligger i Core og ikke i hooken af én grund: så kan den prøves af.
/// Selve hooken kan ikke kaldes fra en prøve — den skal have et rigtigt
/// tastatur og en beskedkø. Bogføringen er ren regning og kan.
/// </remarks>
public sealed class Modifikatorspor
{
    /// <summary>
    /// Windows' mærke på et tastetryk, den har fundet på selv.
    /// </summary>
    /// <remarks>
    /// Bitten sidder i SCANCODEN, hvor der ellers kun står, hvor tasten sad.
    /// En rigtig scancode fylder én byte; alt over 0xFF er ikke et sted på et
    /// tastatur. Målt: 0x22A, altså 0x200 lagt oven på venstre Shift (0x2A).
    /// </remarks>
    public const uint Opfundet = 0x200;

    /// <summary>Er tastkoden en af holdetasterne?</summary>
    public static bool Er(uint vkCode) =>
        vkCode is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    public bool Ctrl { get; private set; }
    public bool Shift { get; private set; }
    public bool Alt { get; private set; }

    /// <summary>
    /// Sætter udgangsstillingen. Bruges ÉN gang, når hooken sættes ind.
    /// </summary>
    /// <remarks>
    /// Holder man allerede Ctrl nede i det sekund, appen starter, har hooken
    /// ikke set trykket. Herefter er det hookens egne hændelser, der gælder.
    /// </remarks>
    public void Udgangsstilling(bool ctrl, bool shift, bool alt)
    {
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;
    }

    /// <summary>
    /// Bogfører ét tastetryk. Returnerer sandt, hvis det var en holdetast.
    /// </summary>
    /// <remarks>
    /// ============ WINDOWS OPFINDER ET SHIFT-SLIP ============
    ///
    /// MÅLT 05-09-2026 med NumLock tændt, Ctrl+Shift holdt nede, og et tryk på
    /// TALTASTATURETS komma. Det er den genvej, der var valgt (greb «cs:53»).
    /// Hooken så det her, i den rækkefølge:
    ///
    ///     scan   vk    ned/op   udsendt
    ///     0x1D   0xA2  NED      Ctrl ned
    ///     0x2A   0xA0  NED      Shift ned
    ///     0x22A  0xA0  OP       ← FINDES IKKE. Ingen slap noget.
    ///     0x53   0x2E  NED      kommaet — og Shift står nu som oppe
    ///     0x53   0x2E  OP
    ///     0x2A   0xA0  NED      ← Windows sætter Shift tilbage bagefter
    ///     0x2A   0xA0  OP       fingeren slipper for alvor
    ///
    /// Windows kan ikke lade Shift og taltastaturet gælde samtidig: Shift
    /// vender NumLock om, mens den holdes. Derfor fabrikeres et slip lige før
    /// tasten og et tryk lige efter, så resten af verden ser en tast uden
    /// Shift. Med NumLock SLUKKET sker det ikke — så er der intet at vende om.
    ///
    /// DET ER DEN SAMME FEJL TRE GANGE. Ved kommaets tryk svarede ALLE tre
    /// veje det samme, nemlig at Shift var oppe:
    ///
    ///     GetKeyState       nej      (beskedkøen)
    ///     GetAsyncKeyState  nej      (den fysiske tilstand)
    ///     hookens egen bog  nej      (den havde jo lige set et slip)
    ///
    /// Det opfundne slip er ægte hele vejen ned. Der var aldrig et valg
    /// mellem tre metoder — der var ét spøgelse, som alle tre troede på. De to
    /// første rettelser flyttede rundt på symptomet uden at røre årsagen.
    ///
    /// Derfor: et slip med <see cref="Opfundet"/> i scancoden bogføres ikke.
    /// Alt andet gør.
    /// </remarks>
    public bool Se(uint vkCode, uint scanCode, bool ned)
    {
        if (!Er(vkCode)) return false;

        // Rør ikke ved tilstanden. Fingeren gjorde ingenting.
        if ((scanCode & Opfundet) != 0) return true;

        if (vkCode is 0x10 or 0xA0 or 0xA1) Shift = ned;
        if (vkCode is 0x11 or 0xA2 or 0xA3) Ctrl = ned;
        if (vkCode is 0x12 or 0xA4 or 0xA5) Alt = ned;

        return true;
    }
}
