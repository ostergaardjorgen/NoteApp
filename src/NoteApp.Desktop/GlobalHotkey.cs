using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NoteApp.Desktop;

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
/// VALGET AF TASTER
///
/// Ctrl+Alt+R. Kombinationen er ledig i Windows selv og i de programmer, et
/// møde typisk foregår i (Teams, Zoom, Outlook, browsere). Ctrl+Shift+R er
/// genindlæsning i alle browsere, Win+R åbner Kør, og Ctrl+R er genindlæsning
/// eller «svar» de fleste steder — de tre er derfor valgt fra.
///
/// Er kombinationen alligevel optaget af noget andet, fejler registreringen.
/// Det skal SIGES: en genvej, der stille ikke virker, opdages først den dag,
/// man trykker på den under et møde og bagefter opdager, at der ikke blev
/// optaget noget.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int Id = 0x4E0A;   // vilkaarligt, men fast: id'et skal matche ved afmelding

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_R = 0x52;

    public const string Beskrivelse = "Ctrl+Alt+R";

    private HwndSource? _kilde;
    private bool _registreret;

    /// <summary>Rejses når genvejen bliver trykket.</summary>
    public event Action? Trykket;

    /// <summary>Sat hvis registreringen mislykkedes — teksten kan vises til brugeren.</summary>
    public string? Fejl { get; private set; }

    public bool Aktiv => _registreret;

    /// <summary>
    /// Kobler genvejen på et vindue. Vinduet skal være vist, før dets håndtag
    /// findes — derfor kaldes den fra Loaded, ikke fra konstruktøren.
    /// </summary>
    public bool Tilslut(Window vindue)
    {
        if (_registreret) return true;

        var håndtag = new WindowInteropHelper(vindue).Handle;
        if (håndtag == IntPtr.Zero)
        {
            Fejl = "vinduet er ikke klar endnu";
            return false;
        }

        _kilde = HwndSource.FromHwnd(håndtag);
        if (_kilde is null)
        {
            Fejl = "kunne ikke få fat i vinduet";
            return false;
        }

        _kilde.AddHook(Hook);

        // MOD_NOREPEAT: holder man tasten nede, skal der starte EEN optagelse,
        // ikke tyve.
        _registreret = RegisterHotKey(håndtag, Id, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_R);

        if (!_registreret)
        {
            var kode = Marshal.GetLastWin32Error();
            Fejl = kode == 1409
                ? $"{Beskrivelse} er allerede taget af et andet program"
                : $"{Beskrivelse} kunne ikke registreres (fejl {kode})";
            _kilde.RemoveHook(Hook);
            _kilde = null;
        }

        return _registreret;
    }

    private IntPtr Hook(IntPtr hwnd, int besked, IntPtr wParam, IntPtr lParam, ref bool håndteret)
    {
        if (besked != WM_HOTKEY || wParam.ToInt32() != Id) return IntPtr.Zero;

        håndteret = true;
        Trykket?.Invoke();
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_kilde is null) return;

        if (_registreret)
        {
            var håndtag = _kilde.Handle;
            if (håndtag != IntPtr.Zero) UnregisterHotKey(håndtag, Id);
            _registreret = false;
        }

        _kilde.RemoveHook(Hook);
        _kilde = null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
