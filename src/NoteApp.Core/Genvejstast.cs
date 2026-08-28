namespace NoteApp.Core;

/// <summary>
/// Én genvejskombination — modifikatortaster og en tast.
///
/// HVORFOR DEN FINDES VED SIDEN AF <see cref="Genvejstaster"/>
///
/// Listen dér er FORSLAG. Den her er det, brugeren FAKTISK har trykket.
///
/// Forskellen kostede en hel formiddag 28-08-2026. Appen lyttede efter
/// kommaet ved siden af M (0xBC, scancode 0x33). Brugeren trykkede på
/// kommaet på taltastaturet (scancode 0x53) — det er dét, der står et komma
/// på et dansk tastatur. Målt med en lavniveau-hook, mens han trykkede:
///
///     vk=0x2E  scancode=0x53  Ctrl+   (fysisk tast)   — gentaget mange gange
///
/// Der kom aldrig et eneste 0xBC. Alt i appen virkede; den lyttede bare på
/// en anden tast, end der blev trykket på, og der var intet at se nogen
/// steder. En liste, man VÆLGER fra, kan ikke fange den slags — man kan ikke
/// se på «Ctrl+Shift+, (komma)», hvilken af de to kommataster der menes.
///
/// DERFOR TRYKKER MAN DEN I STEDET. Kombinationen fanges fra et rigtigt
/// tastetryk, og så er der pr. definition ingen forskel på det, brugeren
/// trykker, og det, appen lytter efter.
/// </summary>
/// <param name="Mod">Alt=1, Ctrl=2, Shift=4 — samme værdier som RegisterHotKey.</param>
/// <param name="Vk">Den virtuelle tast.</param>
public readonly record struct Genvejstast(uint Mod, uint Vk)
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;

    /// <summary>Er der noget her? En tom kombination betyder «ikke valgt».</summary>
    public bool ErSat => Vk != 0;

    /// <summary>
    /// Kan den bruges som global genvej?
    /// </summary>
    /// <remarks>
    /// DER SKAL VÆRE MINDST ÉN MODIFIKATOR. En genvej på én tast ville gå i
    /// gang, hver gang man skrev det bogstav — i et hvilket som helst program.
    ///
    /// Og modifikatorerne kan ikke selv være tasten: «Ctrl+Ctrl» er ikke en
    /// kombination. Windows afviser det, men fejlen ville vise sig som «der
    /// skete ingenting», og det er den værste slags.
    /// </remarks>
    public bool Duer =>
        ErSat && Mod != 0 && Vk is not (0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C);

    /// <summary>
    /// Navnet, som det skal stå på skærmen.
    /// </summary>
    /// <param name="tegn">
    /// Tegnet, tasten giver på det aktuelle tastaturlayout — hvis det kendes.
    /// Bruges kun til taster, tabellen ikke har et navn for; de er
    /// layoutafhængige, og Core kan ikke spørge Windows.
    /// </param>
    public string Navn(string? tegn = null)
    {
        var dele = new List<string>();
        if ((Mod & MOD_CONTROL) != 0) dele.Add("Ctrl");
        if ((Mod & MOD_SHIFT) != 0) dele.Add("Shift");
        if ((Mod & MOD_ALT) != 0) dele.Add("Alt");

        dele.Add(Tastnavn(Vk, tegn));
        return string.Join("+", dele);
    }

    /// <summary>
    /// Navnet på én tast.
    /// </summary>
    /// <remarks>
    /// TALTASTATURET SKRIVES UD. «Ctrl+,» siger ikke, HVILKET komma — og det
    /// er præcis den tvetydighed, der kostede en formiddag. Derfor står der
    /// «(taltastatur)» på dem, der sidder derovre.
    /// </remarks>
    public static string Tastnavn(uint vk, string? tegn = null) => vk switch
    {
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x1B => "Esc",
        0x20 => "Mellemrum",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Venstre pil",
        0x26 => "Pil op",
        0x27 => "Højre pil",
        0x28 => "Pil ned",
        0x2C => "PrtScn",
        0x2D => "Insert",
        0x2E => "Delete",

        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),

        >= 0x60 and <= 0x69 => $"{vk - 0x60} (taltastatur)",
        0x6A => "* (taltastatur)",
        0x6B => "+ (taltastatur)",
        0x6D => "- (taltastatur)",
        0x6E => ", (taltastatur)",
        0x6F => "/ (taltastatur)",

        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",

        // De layoutafhaengige. Tegnet kommer fra Windows, naar det kendes -
        // ellers staar tastens nummer, saa der i det mindste staar noget, man
        // kan kende igen.
        _ => tegn is { Length: > 0 } ? Beskriv(tegn) : $"tast 0x{vk:X2}"
    };

    /// <summary>
    /// Et tegn med sit navn i parentes, hvis det er et, man kan tage fejl af.
    /// </summary>
    private static string Beskriv(string tegn) => tegn switch
    {
        "," => ", (komma)",
        "." => ". (punktum)",
        " " => "Mellemrum",
        _ => tegn
    };

    /// <summary>
    /// Tvillingen på taltastaturet — den tast, den SAMME finger sender, når
    /// NumLock står omvendt. Nul, hvis tasten ikke sidder på taltastaturet.
    /// </summary>
    /// <remarks>
    /// DET ER DEN, DER GØR GENVEJEN UPÅLIDELIG. Taltastaturets komma sender
    /// 0x6E med NumLock slået til og 0x2E (Delete) med den slået fra. Registrerer
    /// man kun den ene, holder genvejen op med at virke i det øjeblik, nogen
    /// rører NumLock — og der er intet at se.
    ///
    /// Målt 28-08-2026: brugeren trykkede og fik «Ctrl+Delete» fanget. Det var
    /// taltastaturets komma med NumLock fra.
    ///
    /// SHIFT VENDER NUMLOCK OM, mens den holdes nede. Derfor er
    /// Ctrl+Shift+taltastaturtast altid død: kombinationen registreres på den
    /// ene tast, og fingeren sender den anden.
    /// </remarks>
    public uint Tvilling => Tvillingen(Vk);

    public static uint Tvillingen(uint vk) => vk switch
    {
        0x60 => 0x2D,   // 0 <-> Insert
        0x61 => 0x23,   // 1 <-> End
        0x62 => 0x28,   // 2 <-> Pil ned
        0x63 => 0x22,   // 3 <-> PageDown
        0x64 => 0x25,   // 4 <-> Venstre pil
        0x65 => 0x0C,   // 5 <-> Clear
        0x66 => 0x27,   // 6 <-> Hoejre pil
        0x67 => 0x24,   // 7 <-> Home
        0x68 => 0x26,   // 8 <-> Pil op
        0x69 => 0x21,   // 9 <-> PageUp
        0x6E => 0x2E,   // komma <-> Delete

        0x2D => 0x60,
        0x23 => 0x61,
        0x28 => 0x62,
        0x22 => 0x63,
        0x25 => 0x64,
        0x0C => 0x65,
        0x27 => 0x66,
        0x24 => 0x67,
        0x26 => 0x68,
        0x21 => 0x69,
        0x2E => 0x6E,

        _ => 0
    };

    /// <summary>
    /// Shift kan ikke være med på en tast fra taltastaturet.
    /// </summary>
    /// <remarks>
    /// Shift vender NumLock om, mens den holdes nede, så tasten bliver til sin
    /// tvilling. Efterprøvet: Ctrl+Shift+numpad-0 og Ctrl+Shift+numpad-komma er
    /// begge døde, mens Ctrl+numpad-0 og Ctrl+numpad-komma virker.
    /// </remarks>
    public bool ShiftDuerIkke => (Mod & MOD_SHIFT) != 0 && Tvilling != 0;

    // ------------------------------------------------------------ gemning

    /// <summary>Som tekst, til indstillingsfilen. Tom streng = ikke sat.</summary>
    public override string ToString() => ErSat ? $"{Mod}:{Vk}" : "";

    /// <summary>Læser den tilbage. Ugyldigt indhold giver en tom kombination.</summary>
    public static Genvejstast Laes(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return default;

        var dele = s.Split(':');
        if (dele.Length != 2) return default;

        return uint.TryParse(dele[0], out var m) && uint.TryParse(dele[1], out var v)
            ? new Genvejstast(m, v)
            : default;
    }
}
