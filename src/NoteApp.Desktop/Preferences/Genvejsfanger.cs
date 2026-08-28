using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop.Preferences;

/// <summary>
/// Fanger en genvejskombination ved at lade brugeren TRYKKE den — og
/// beviser bagefter, at den virker.
///
/// HVORFOR DEN FINDES
///
/// En liste, man vælger fra, kan ikke skelne mellem to taster, der hedder det
/// samme. Det kostede en formiddag 28-08-2026: appen lyttede efter kommaet
/// ved siden af M, brugeren trykkede på kommaet på TALTASTATURET — dét, der
/// står et komma på, på et dansk tastatur. Målt med en lavniveau-hook, mens
/// han trykkede:
///
///     vk=0x2E  scancode=0x53  Ctrl+   (fysisk tast)   — gentaget mange gange
///
/// Der kom aldrig et eneste 0xBC. Det samme gælder tallene: talrækkens 1 og
/// taltastaturets 1 er to forskellige taster, og en liste viser dem begge
/// som «1».
///
/// DE TRE TRIN
///
///   1. FANG. Brugeren trykker kombinationen. Nu er der pr. definition ingen
///      forskel på det, han trykker, og det, appen lytter efter.
///   2. REGISTRÉR. Er den taget af et andet program, eller reserveret af
///      Windows, siges det med det samme — ikke ved næste møde.
///   3. BEKRÆFT. Brugeren trykker den ÉN GANG TIL, og der ventes på, at
///      genvejen faktisk fyrer. Først dér gemmes den.
///
/// Trin 3 er det vigtige. Trin 1 og 2 kan begge lykkes, mens genvejen alligevel
/// er død: RegisterHotKey siger ja til kombinationer, Windows' egen
/// tekstbehandling har taget, og så sker der ingenting, når man trykker. Det
/// er sket, og det var ikke til at se. Et bekræftende tryk beviser hele kæden
/// — fra fingeren til appen.
/// </summary>
public sealed class Genvejsfanger : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int PrøveId = 0x4E0C;

    private static readonly TimeSpan Taalmodighed = TimeSpan.FromSeconds(15);

    private readonly Window _vindue;
    private readonly IntPtr _håndtag;
    private HwndSource? _kilde;
    private readonly DispatcherTimer _ur = new() { Interval = TimeSpan.FromSeconds(1) };

    private DateTimeOffset _venterSiden;
    private bool _registreret;

    /// <summary>Hvor langt vi er.</summary>
    public enum Trin { Venter, Fanget, Bekræftet, Fejlet }

    public Trin Hvor { get; private set; } = Trin.Venter;

    /// <summary>Kombinationen, der blev fanget.</summary>
    public Genvejstast Tast { get; private set; }

    /// <summary>Hvad der skal stå på skærmen lige nu.</summary>
    public string Besked { get; private set; } = "";

    /// <summary>Der er sket noget — tegn om.</summary>
    public event Action? Aendret;

    /// <summary>Kombinationen er bekræftet og kan gemmes.</summary>
    public event Action<Genvejstast>? Faerdig;

    public Genvejsfanger(Window vindue)
    {
        _vindue = vindue;
        _håndtag = new WindowInteropHelper(vindue).Handle;

        _ur.Tick += (_, _) =>
        {
            if (Hvor != Trin.Fanget) return;

            var tilbage = Taalmodighed - (DateTimeOffset.Now - _venterSiden);
            if (tilbage > TimeSpan.Zero)
            {
                Sig($"Tryk {Navn()} én gang til for at bekræfte. {(int)tilbage.TotalSeconds} sekunder tilbage.");
                return;
            }

            // DEN KOM ALDRIG FREM. Registreringen lykkedes, og tastetrykket
            // skete - men genvejen fyrede ikke. Det er praecis den tilstand,
            // der foer var usynlig.
            Slip();
            Hvor = Trin.Fejlet;
            Sig($"{Navn()} kom ikke frem. Windows eller et andet program opsnapper den. Vælg en anden.");
        };
    }

    private string Navn() => Tast.Navn(GlobalHotkey.Tastetegn(Tast.Vk));

    private void Sig(string s)
    {
        Besked = s;
        Aendret?.Invoke();
    }

    /// <summary>Begynd at lytte. Næste tastetryk er kombinationen.</summary>
    public void Begynd()
    {
        Slip();
        Hvor = Trin.Venter;
        Tast = default;
        Sig("Tryk den kombination, du vil bruge — den skal have mindst én af Ctrl, Shift eller Alt.");
    }

    /// <summary>
    /// Kaldes fra vinduets PreviewKeyDown, mens der lyttes.
    /// </summary>
    /// <returns>Sandt, hvis tastetrykket blev brugt og ikke skal videre.</returns>
    public bool Tastetryk(KeyEventArgs e)
    {
        if (Hvor != Trin.Venter) return false;

        // Systemtaster kommer som Key.System; den rigtige tast staar i
        // SystemKey. Uden det her ville Alt-kombinationer blive til «Alt+Alt».
        var tast = e.Key == Key.System ? e.SystemKey : e.Key;

        var vk = (uint)KeyInterop.VirtualKeyFromKey(tast);

        uint mod = 0;
        var m = Keyboard.Modifiers;
        if ((m & ModifierKeys.Control) != 0) mod |= Genvejstast.MOD_CONTROL;
        if ((m & ModifierKeys.Shift) != 0) mod |= Genvejstast.MOD_SHIFT;
        if ((m & ModifierKeys.Alt) != 0) mod |= Genvejstast.MOD_ALT;

        var kandidat = new Genvejstast(mod, vk);

        // MODIFIKATORERNE ALENE ER IKKE EN KOMBINATION. De trykkes foerst,
        // og uden det her ville «Ctrl» blive fanget, foer man naaede tasten.
        if (!kandidat.Duer)
        {
            if (mod != 0 || vk is 0x10 or 0x11 or 0x12) return true;   // hold vejret
            Sig("Der skal være mindst én af Ctrl, Shift eller Alt med — ellers går genvejen i gang, hver gang du skriver.");
            return true;
        }

        e.Handled = true;
        Tast = kandidat;

        // Escape afbryder.
        if (vk == 0x1B) { Afbryd(); return true; }

        Proev();
        return true;
    }

    private void Proev()
    {
        // ============ SHIFT KAN IKKE VAERE MED PAA TALTASTATURET ============
        //
        // Shift vender NumLock om, mens den holdes nede, saa tasten bliver til
        // sin tvilling: taltastaturets komma bliver til Delete, nullet bliver
        // til Insert. Kombinationen ville blive registreret paa den ene tast,
        // mens fingeren sendte den anden.
        //
        // Efterproevet 28-08-2026: Ctrl+Shift+numpad-0 og
        // Ctrl+Shift+numpad-komma er begge doede, mens de samme uden Shift
        // virker. Det siges her frem for at lade brugeren opdage det ved et
        // moede, der ikke blev optaget.
        if (Tast.ShiftDuerIkke)
        {
            Hvor = Trin.Fejlet;
            Sig($"Shift kan ikke være med på {Genvejstast.Tastnavn(Tast.Vk)}. "
                + "Shift slår NumLock fra, mens den holdes nede, så tasten bliver til noget andet. "
                + "Prøv den samme tast uden Shift.");
            return;
        }

        if (GlobalHotkey.ErSpaerretAfWindows(Tast.Mod, Tast.Vk))
        {
            Hvor = Trin.Fejlet;
            Sig($"{Navn()} bruger Windows selv. Den ville se ud til at virke og gøre ingenting. Vælg en anden.");
            return;
        }

        _kilde ??= HwndSource.FromHwnd(_håndtag);
        if (_kilde is null)
        {
            Hvor = Trin.Fejlet;
            Sig("Kunne ikke få fat i vinduet. Prøv igen.");
            return;
        }

        _kilde.AddHook(Hook);

        if (!GlobalHotkey.Tag(_håndtag, PrøveId, Tast.Mod, Tast.Vk))
        {
            Slip();
            Hvor = Trin.Fejlet;
            Sig($"{Navn()} er taget af et andet program. Vælg en anden.");
            return;
        }

        _registreret = true;
        Hvor = Trin.Fanget;
        _venterSiden = DateTimeOffset.Now;
        _ur.Start();

        Sig($"Tryk {Navn()} én gang til for at bekræfte.");
    }

    private IntPtr Hook(IntPtr hwnd, int besked, IntPtr w, IntPtr l, ref bool håndteret)
    {
        if (besked != WM_HOTKEY || w.ToInt32() != PrøveId) return IntPtr.Zero;
        if (Hvor != Trin.Fanget) return IntPtr.Zero;

        håndteret = true;

        // DEN KOM FREM. Hele kaeden er dermed bevist: fingeren, tastaturet,
        // Windows og appen.
        Slip();
        Hvor = Trin.Bekræftet;

        // TVILLINGEN NAEVNES. Vaelger man en tast paa taltastaturet, tages
        // BEGGE - ellers holder genvejen op med at virke, saa snart nogen
        // roerer NumLock. Prisen er, at tvillingens egen genvej ryger med, og
        // det skal staa, foer det opdages i Word.
        var tvilling = Tast.Tvilling;

        Sig(tvilling == 0
            ? $"{Navn()} virker. Den starter en optagelse fra nu af."
            : $"{Navn()} virker. Den starter en optagelse fra nu af — uanset om NumLock "
              + $"er slået til eller fra. Prisen er, at "
              + $"{new Genvejstast(Tast.Mod, tvilling).Navn(GlobalHotkey.Tastetegn(tvilling))} "
              + "også bliver taget, for det er den samme tast under fingeren.");

        _vindue.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
            () => Faerdig?.Invoke(Tast));

        return IntPtr.Zero;
    }

    public void Afbryd()
    {
        Slip();
        Hvor = Trin.Venter;
        Tast = default;
        Sig("");
    }

    private void Slip()
    {
        _ur.Stop();

        if (_registreret) GlobalHotkey.Slip(_håndtag, PrøveId);
        _registreret = false;

        _kilde?.RemoveHook(Hook);
        _kilde = null;
    }

    public void Dispose() => Slip();
}
