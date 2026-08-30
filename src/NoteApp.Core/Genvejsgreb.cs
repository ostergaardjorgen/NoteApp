namespace NoteApp.Core;

/// <summary>
/// Et greb på tastaturet: en fysisk tast plus de taster, der holdes nede.
/// </summary>
/// <param name="Scancode">Tastens FYSISKE plads på tastaturet.</param>
/// <param name="Udvidet">
/// Sad tasten i den «udvidede» gruppe? Det er dét, der skiller taltastaturets
/// komma fra Delete-tasten over piletasterne — de har den SAMME scancode.
/// </param>
/// <param name="Ctrl">Skal Ctrl holdes nede?</param>
/// <param name="Shift">Skal Shift holdes nede?</param>
/// <param name="Alt">Skal Alt holdes nede?</param>
/// <remarks>
/// HVORFOR SCANCODE OG IKKE TASTKODE — DET ER HELE POINTEN.
///
/// Windows oversætter en tast til et tal (en «virtual key»), FØR programmet
/// ser den. Oversættelsen afhænger af tastaturlayout, af NumLock, og af om
/// Shift holdes nede.
///
/// Målt 30-08-2026 på denne maskine, 669 tryk i træk på den samme tast:
///
///     vk=0x2E   scancode=0x53
///
/// 0x53 er taltastaturets komma. 0x2E er VK_DELETE. Brugeren trykkede på et
/// komma, og Windows leverede en Delete — fordi Shift midlertidigt vender
/// NumLock om. Appen lyttede efter kommaet ved siden af M (0xBC) og hørte
/// derfor ingenting. Det havde stået på i dagevis.
///
/// Scancoden er den samme, uanset hvad Windows oversætter tasten til. Den
/// siger HVOR fingeren var, ikke hvad Windows mente om det. Det er det eneste
/// stabile at bygge en genvej på.
/// </remarks>
public sealed record Genvejsgreb(
    uint Scancode, bool Udvidet, bool Ctrl, bool Shift, bool Alt)
{
    /// <summary>Kommaet ved siden af M.</summary>
    public const uint Komma = 0x33;

    /// <summary>Kommaet på taltastaturet — den store nederst til højre.</summary>
    public const uint Taltastaturkomma = 0x53;

    /// <summary>Punktummet ved siden af kommaet.</summary>
    public const uint Punktum = 0x34;

    /// <summary>Mellemrumstasten.</summary>
    public const uint Mellemrum = 0x39;

    /// <summary>
    /// Er der overhovedet en holdetast med?
    /// </summary>
    /// <remarks>
    /// En genvej uden Ctrl, Shift eller Alt ville udløse sig selv, hver gang
    /// nogen skrev det tegn. Den slags gemmes ikke.
    /// </remarks>
    public bool Duer => Scancode != 0 && (Ctrl || Shift || Alt);

    /// <summary>
    /// Passer et tastetryk på grebet?
    /// </summary>
    /// <remarks>
    /// Der ses IKKE på, hvilken tastkode Windows nåede frem til. Kun på hvor
    /// tasten sad, og hvad der blev holdt nede.
    /// </remarks>
    public bool Passer(uint scancode, bool udvidet, bool ctrl, bool shift, bool alt) =>
        scancode == Scancode && udvidet == Udvidet
        && ctrl == Ctrl && shift == Shift && alt == Alt;

    /// <summary>
    /// Navnet, brugeren ser — det, der STÅR PÅ TASTEN.
    /// </summary>
    /// <remarks>
    /// «Ctrl+Shift+,» og ikke «Ctrl+Shift+Delete».
    ///
    /// Windows oversætter taltastaturets komma til en Delete, når Shift
    /// holdes nede. Det er sandt indeni, og det er præcis dét, der gjorde
    /// genvejen umulig at fejlfinde — men det er ikke, hvad brugeren ser. På
    /// tasten står der et komma, fingeren rammer et komma, og så hedder
    /// genvejen et komma.
    ///
    /// Hvilken af de to kommataster det er, står i <see cref="Hvor"/> — som
    /// en oplysning ved siden af, ikke inde i navnet.
    /// </remarks>
    public string Navn()
    {
        var dele = new List<string>();
        if (Ctrl) dele.Add("Ctrl");
        if (Shift) dele.Add("Shift");
        if (Alt) dele.Add("Alt");
        dele.Add(Tastenavn(Scancode, Udvidet));
        return string.Join("+", dele);
    }

    /// <summary>
    /// Hvor tasten sidder. Tom, når der ikke er noget at forveksle den med.
    /// </summary>
    /// <remarks>
    /// De to kommaer SKAL kunne skelnes et sted — det var forvekslingen, der
    /// kostede tre dage. Men den hører til som en oplysning ved siden af
    /// navnet, ikke inde i det.
    /// </remarks>
    public string Hvor() => (Scancode, Udvidet) switch
    {
        (Komma, false) => "kommaet ved siden af M",
        (Taltastaturkomma, false) => "kommaet på taltastaturet",
        (Punktum, false) => "punktummet ved siden af kommaet",
        _ => "",
    };

    /// <summary>
    /// Tastens navn ud fra dens plads — som det står på tasten.
    /// </summary>
    public static string Tastenavn(uint scancode, bool udvidet) => (scancode, udvidet) switch
    {
        (Komma, false) => ",",
        (Taltastaturkomma, false) => ",",
        (Taltastaturkomma, true) => "Delete",
        (Punktum, false) => ".",
        (Mellemrum, false) => "Mellemrum",

        (0x02, false) => "1", (0x03, false) => "2", (0x04, false) => "3",
        (0x05, false) => "4", (0x06, false) => "5", (0x07, false) => "6",
        (0x08, false) => "7", (0x09, false) => "8", (0x0A, false) => "9",
        (0x0B, false) => "0",

        (0x3B, false) => "F1", (0x3C, false) => "F2", (0x3D, false) => "F3",
        (0x3E, false) => "F4", (0x3F, false) => "F5", (0x40, false) => "F6",
        (0x41, false) => "F7", (0x42, false) => "F8", (0x43, false) => "F9",
        (0x44, false) => "F10", (0x57, false) => "F11", (0x58, false) => "F12",

        (0x1C, false) => "Enter", (0x1C, true) => "Enter (taltastatur)",
        (0x0E, false) => "Backspace", (0x0F, false) => "Tabulator",
        (0x35, false) => "-", (0x35, true) => "/",
        (0x0C, false) => "+",
        (0x4E, false) => "+", (0x4A, false) => "-",
        (0x37, false) => "*",

        (0x52, false) => "0", (0x52, true) => "Insert",

        _ => udvidet ? $"tast 0x{scancode:X2}+" : $"tast 0x{scancode:X2}",
    };

    /// <summary>Skriver grebet, så det kan gemmes i indstillingerne.</summary>
    public string Gem() =>
        $"{(Ctrl ? "c" : "")}{(Shift ? "s" : "")}{(Alt ? "a" : "")}{(Udvidet ? "u" : "")}:{Scancode:X}";

    /// <summary>
    /// Læser et gemt greb. Null, hvis teksten ikke giver mening.
    /// </summary>
    /// <remarks>
    /// Et ulæseligt greb bliver til null og ikke til et gæt. Et gæt ville
    /// betyde, at genvejen stille blev en anden — og det er præcis dét, den
    /// her ombygning findes for at gøre en ende på.
    /// </remarks>
    public static Genvejsgreb? Laes(string? gemt)
    {
        if (string.IsNullOrWhiteSpace(gemt)) return null;

        var dele = gemt.Split(':');
        if (dele.Length != 2) return null;

        if (!uint.TryParse(dele[1], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var sc))
            return null;

        var m = dele[0];
        var greb = new Genvejsgreb(sc, m.Contains('u'),
                                   m.Contains('c'), m.Contains('s'), m.Contains('a'));

        return greb.Duer ? greb : null;
    }

    /// <summary>
    /// Startværdien: Ctrl+Shift og kommaet ved siden af M.
    /// </summary>
    /// <remarks>
    /// Det er KUN en startværdi. Trykker man en anden, gælder den — og appen
    /// vælger aldrig selv en ny, uanset hvad andre programmer laver. Se
    /// Tastehook.
    /// </remarks>
    public static Genvejsgreb Standard =>
        new(Komma, Udvidet: false, Ctrl: true, Shift: true, Alt: false);
}
