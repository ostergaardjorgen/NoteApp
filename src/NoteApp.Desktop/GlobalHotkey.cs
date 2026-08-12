using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>Én kandidat til lynstart-genvejen.</summary>
public sealed record HotkeyValg(string Id, string Navn, uint Modifiers, uint Key, string Hvorfor);

/// <summary>
/// Genvejstasten, der starter en optagelse — også når appen er skjult bag
/// andre vinduer.
///
/// HVORFOR DEN SKAL VÆRE GLOBAL
///
/// Et møde begynder, mens man har noget andet på skærmen: en kalender, et
/// Teams-vindue, et dokument. Skal man først finde appen frem, er de første
/// minutter væk — og det er tit dér, dagsordenen bliver aftalt.
///
/// HVORFOR DER ER FLERE KANDIDATER
///
/// Genvejstaster er optaget af vidt forskellige programmer fra maskine til
/// maskine. Ctrl+Alt+R var ledig i teorien og taget i praksis på den første
/// maskine, den blev prøvet på. Et fast valg, der ikke kan lade sig gøre, er
/// ingen genvej — derfor prøves listen igennem, indtil en er ledig, og
/// brugeren kan vælge en anden bagefter.
///
/// Fravalgt med vilje: Ctrl+R og Ctrl+Shift+R (genindlæsning i alle browsere),
/// Win+R (Kør), Win+Alt+R (Xbox Game Bar optager skærmen), Ctrl+Alt+Delete.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int Id = 0x4E0A;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    /// <summary>
    /// Kandidaterne i den rækkefølge, de prøves. Funktionstasterne står sidst
    /// som sikkerhedsnet: de er sjældent taget, men også sværere at huske.
    /// </summary>
    public static readonly IReadOnlyList<HotkeyValg> Muligheder = new[]
    {
        new HotkeyValg("ctrl-alt-r", "Ctrl+Alt+R", MOD_CONTROL | MOD_ALT, 0x52,
            "R for «record». Ledig i Windows selv, men tages af nogle lyd- og skærmoptagere."),
        new HotkeyValg("ctrl-alt-m", "Ctrl+Alt+M", MOD_CONTROL | MOD_ALT, 0x4D,
            "M for «møde». Bruges af enkelte noteprogrammer."),
        new HotkeyValg("ctrl-alt-o", "Ctrl+Alt+O", MOD_CONTROL | MOD_ALT, 0x4F,
            "O for «optag». Sjældent taget."),
        new HotkeyValg("ctrl-shift-alt-r", "Ctrl+Shift+Alt+R", MOD_CONTROL | MOD_SHIFT | MOD_ALT, 0x52,
            "Tre taster gør den næsten sikkert ledig — til gengæld skal begge hænder med."),
        new HotkeyValg("ctrl-alt-f9", "Ctrl+Alt+F9", MOD_CONTROL | MOD_ALT, 0x78,
            "Funktionstast. Næsten altid ledig, men sværere at huske."),
        new HotkeyValg("ctrl-alt-f12", "Ctrl+Alt+F12", MOD_CONTROL | MOD_ALT, 0x7B,
            "Sidste udvej. Fri på stort set enhver maskine.")
    };

    private HwndSource? _kilde;
    private IntPtr _håndtag;
    private bool _registreret;

    /// <summary>Rejses når genvejen bliver trykket.</summary>
    public event Action? Trykket;

    /// <summary>Den kombination, der faktisk blev registreret. Null hvis ingen lykkedes.</summary>
    public HotkeyValg? Aktiv { get; private set; }

    /// <summary>Sat, hvis den ønskede kombination var taget, og der blev valgt en anden.</summary>
    public string? Bemærkning { get; private set; }

    /// <summary>
    /// Kobler genvejen på et vindue og finder en kombination, der er ledig.
    ///
    /// Den ønskede prøves først. Er den taget, prøves resten af listen — og
    /// det siges bagefter, hvilken der blev brugt. Et program, der stille
    /// vælger noget andet, end brugeren har bedt om, er værre end et, der
    /// fejler.
    /// </summary>
    public bool Tilslut(Window vindue, string? ønsketId = null)
    {
        Frigiv();

        _håndtag = new WindowInteropHelper(vindue).Handle;
        if (_håndtag == IntPtr.Zero)
        {
            Bemærkning = "vinduet var ikke klar";
            return false;
        }

        _kilde = HwndSource.FromHwnd(_håndtag);
        if (_kilde is null)
        {
            Bemærkning = "kunne ikke få fat i vinduet";
            return false;
        }

        _kilde.AddHook(Hook);

        var ønsket = Muligheder.FirstOrDefault(m => m.Id == ønsketId);
        var rækkefølge = ønsket is null
            ? Muligheder
            : new[] { ønsket }.Concat(Muligheder.Where(m => m.Id != ønsket.Id)).ToList();

        foreach (var valg in rækkefølge)
        {
            // MOD_NOREPEAT: holder man tasten nede, skal der starte EEN
            // optagelse, ikke tyve.
            if (!RegisterHotKey(_håndtag, Id, valg.Modifiers | MOD_NOREPEAT, valg.Key)) continue;

            _registreret = true;
            Aktiv = valg;
            Bemærkning = ønsket is not null && valg.Id != ønsket.Id
                ? $"{ønsket.Navn} var taget af et andet program — bruger {valg.Navn} i stedet"
                : null;
            return true;
        }

        Bemærkning = "alle kombinationer på listen er taget af andre programmer";
        _kilde.RemoveHook(Hook);
        _kilde = null;
        return false;
    }

    /// <summary>Er kombinationen ledig lige nu? Bruges til at vise listen ærligt.</summary>
    public static bool ErLedig(HotkeyValg valg, Window vindue)
    {
        var h = new WindowInteropHelper(vindue).Handle;
        if (h == IntPtr.Zero) return true;

        // Proev at tage den, og giv den fra dig igen med det samme. Et andet
        // id end det rigtige, saa en aktiv registrering ikke bliver forstyrret.
        const int prøveId = 0x4E0B;
        if (!RegisterHotKey(h, prøveId, valg.Modifiers | MOD_NOREPEAT, valg.Key)) return false;

        UnregisterHotKey(h, prøveId);
        return true;
    }

    private IntPtr Hook(IntPtr hwnd, int besked, IntPtr wParam, IntPtr lParam, ref bool håndteret)
    {
        if (besked != WM_HOTKEY || wParam.ToInt32() != Id) return IntPtr.Zero;

        håndteret = true;
        Trykket?.Invoke();
        return IntPtr.Zero;
    }

    private void Frigiv()
    {
        if (_registreret && _håndtag != IntPtr.Zero) UnregisterHotKey(_håndtag, Id);
        _registreret = false;
        Aktiv = null;

        _kilde?.RemoveHook(Hook);
        _kilde = null;
    }

    public void Dispose() => Frigiv();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
