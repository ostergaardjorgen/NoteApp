using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Ser tastaturet selv i stedet for at bede Windows om en genvej.
///
/// HVORFOR DEN AFLØSTE RegisterHotKey
///
/// Windows' egen genvejsmekanisme har fire svagheder, og vi ramte dem alle
/// fire i løbet af to dage:
///
///   1. FØRST TIL MØLLE. Et andet program kan have tasten. Appen opdager det
///      først, når den prøver — og kan intet gøre.
///   2. LYKKES ER IKKE DET SAMME SOM VIRKER. Ctrl+Shift+ciffer registrerer
///      fint og gør ingenting, når der er to tastaturlayout. Målt 20-08 og
///      22-08-2026.
///   3. ÉN TASTKODE. Den kan kun lytte efter ét tal, og Windows oversætter
///      tasten til det tal, FØR appen ser den. Målt 30-08-2026: 669 tryk i
///      træk på taltastaturets komma kom ind som vk=0x2E (Delete), fordi
///      Shift midlertidigt vender NumLock om. Appen lyttede efter 0xBC.
///   4. INTET SLIP. WM_HOTKEY siger «trykket», aldrig «sluppet». Derfor blev
///      tasten spurgt 40 gange i sekundet, om den stadig var nede.
///
/// En lavniveau-hook har ingen af delene. Den ser hvert tryk OG hvert slip,
/// den ser tastens fysiske plads i stedet for Windows' oversættelse, og den
/// kan ikke tages af nogen: hooks stables, alle får lov at se tasten.
///
/// HVAD DEN SER, OG HVAD DEN IKKE GØR
///
/// Den ser hvert eneste tastetryk på maskinen. Det er den samme teknik, en
/// keylogger bruger, og derfor står der i Compliance, hvad den gør. Her står
/// det i koden:
///
///   - Der skrives INTET ned. Ingen fil, ingen liste, ingen log.
///   - Der huskes intet mellem to tryk ud over ÉN ting: om vores egen tast er
///     nede lige nu.
///   - Tegn slås aldrig op. Der ses på tastens plads og på Ctrl/Shift/Alt.
///   - Alt andet end vores eget greb sendes videre uændret og glemmes i samme
///     øjeblik.
///
/// DEN SKAL VÆRE HURTIG
///
/// Kaldet ligger i vejen for hvert tastetryk på maskinen. Er det langsomt,
/// bliver hele tastaturet trægt — og Windows fjerner hooken uden varsel, hvis
/// den bruger for lang tid. Derfor gør kaldet her ikke andet end at
/// sammenligne fire tal og lægge en besked i kø. Alt arbejde sker bagefter,
/// på appens egen tråd.
///
/// Og fordi Windows kan fjerne den i stilhed, sættes den ind igen med jævne
/// mellemrum. En genvej, der holder op med at virke uden at sige det, er
/// præcis dét, vi kom fra.
/// </summary>
public sealed class Tastehook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const uint LLKHF_EXTENDED = 0x01;
    private const uint LLKHF_INJECTED = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct TASTEDATA
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr Kald(int kode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, Kald kald, IntPtr modul, uint traad);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int kode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? navn);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vk);

    /// <summary>
    /// Tastens FYSISKE tilstand — uafhængig af, hvem der har fokus.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    // Delegaten SKAL holdes i live her. Gav vi den bare med til Windows, ville
    // den blive ryddet op af sig selv, og hooken ville doe efter et stykke tid
    // uden at nogen kunne se hvorfor.
    private readonly Kald _kald;
    private IntPtr _hook = IntPtr.Zero;
    private DispatcherTimer? _vagt;

    /// <summary>Det greb, der lyttes efter. Sættes af appen.</summary>
    public Genvejsgreb? Greb { get; set; }

    /// <summary>Skal et hold betyde noget andet end et tryk?</summary>
    public bool HoldGiverDiktering { get; set; }

    /// <summary>Rejses ved et kort tryk.</summary>
    public event Action? Trykket;

    /// <summary>Rejses, når tasten har været holdt længe nok til at være et diktat.</summary>
    public event Action? HoldBegyndt;

    /// <summary>Rejses, når tasten slippes igen efter et hold.</summary>
    public event Action? HoldSluttet;

    /// <summary>Rejses, når loftet blev nået, mens tasten stadig var nede.</summary>
    public event Action? HoldAfbrudt;

    /// <summary>
    /// Fanger det næste greb i stedet for at udløse det. Bruges, når brugeren
    /// vælger sin genvej ved at trykke den.
    /// </summary>
    public Action<Genvejsgreb>? Fanger { get; set; }

    /// <summary>Lytter hooken lige nu?</summary>
    public bool Kører => _hook != IntPtr.Zero;

    public Tastehook()
    {
        _kald = Behandl;
    }

    /// <summary>Sætter hooken ind og holder øje med, at den bliver siddende.</summary>
    public bool Start()
    {
        if (!Saet()) return false;

        // ============ WINDOWS FJERNER DEN UDEN AT SIGE DET ============
        //
        // Bruger kaldet for lang tid, ryger hooken ud - der kommer ingen
        // besked, og der er ingen maade at spoerge paa. Derfor saettes den ind
        // igen med jaevne mellemrum. Det koster ingenting, og alternativet er
        // en genvej, der holder op med at virke i stilhed.
        //
        // TYVE SEKUNDER OG IKKE TO MINUTTER. Vagten er den eneste vej tilbage,
        // naar hooken er faldet ud, og to minutter er lang tid at staa i et
        // andet program og holde en tast nede uden at der sker noget. Prisen
        // er et SetWindowsHookEx hvert tyvende sekund, og det er ingenting.
        _vagt?.Stop();
        _vagt = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(20),
        };
        _vagt.Tick += (_, _) => Efterse();
        _vagt.Start();

        return true;
    }

    /// <summary>Hvor mange gange hooken er faldet ud og sat ind igen.</summary>
    public int Genoprettet { get; private set; }

    /// <summary>
    /// Sætter hooken ind igen — og skriver det ned, hvis den var væk.
    /// </summary>
    /// <remarks>
    /// FØR STOD DER BARE «Fjern(); Saet();» HVER GANG. Så blev hooken
    /// udskiftet hvert eneste tik, uanset om der fejlede noget, og der var
    /// ingen måde at se, om den nogensinde HAVDE fejlet. Meldingen «jeg holdt
    /// tasten nede i Word, og der skete ingenting» kunne derfor hverken
    /// bekræftes eller afvises.
    ///
    /// Nu prøves den først af: <c>GetKeyState</c> på en tast, alle tastaturer
    /// har. Svarer hooken ikke, er den væk, og så sættes den ind igen — og
    /// linjen i historikken siger, at det skete.
    ///
    /// Der skrives kun, når noget ER galt. En linje hvert tyvende sekund om,
    /// at alt er i orden, er en logfil, ingen læser.
    /// </remarks>
    private void Efterse()
    {
        if (_hook != IntPtr.Zero && Lever()) return;

        var varDer = _hook != IntPtr.Zero;

        Fjern();
        var kom = Saet();

        Genoprettet++;

        try
        {
            Historik.Skriv(HaendelseType.Andet,
                kom ? "Tastaturvagten blev sat ind igen"
                    : "Tastaturvagten kunne ikke saettes ind igen",
                varDer
                    ? "Windows havde fjernet den. Genvejen virkede ikke i mellemtiden."
                    : "Den var ikke sat. Genvejen virkede ikke i mellemtiden.",
                kom ? Udfald.Fuldført : Udfald.SeEfter);
        }
        catch (Exception)
        {
            // Kan historikken ikke skrives, er hooken alligevel sat ind igen.
        }
    }

    /// <summary>
    /// Svarer hooken stadig?
    /// </summary>
    /// <remarks>
    /// Der er ingen Windows-funktion, der kan spørge om en hook stadig
    /// sidder. <c>GetKeyState</c> er det nærmeste: den går gennem den samme
    /// kø, og svarer den ikke, er der noget galt med tastaturvejen.
    ///
    /// Den kan ikke afsløre alt — en hook, der lige er faldet ud, ser fin ud
    /// et øjeblik. Derfor er tallet i <see cref="Genoprettet"/> også med: det
    /// siger, hvor mange gange det ER sket.
    /// </remarks>
    private static bool Lever()
    {
        try
        {
            _ = GetKeyState(0x10);   // Shift. Findes på alle tastaturer.
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool Saet()
    {
        if (_hook != IntPtr.Zero) return true;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _kald, GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    private void Fjern()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    /// <summary>
    /// Er tasten fysisk nede lige nu?
    /// </summary>
    /// <remarks>
    /// ============ HER LÅ FEJLEN, OG DEN VAR STOR ============
    ///
    /// Der stod <c>GetKeyState</c>. Den svarer IKKE på, om tasten er nede —
    /// den svarer på, hvad DEN KALDENDE TRÅDS beskedkø har set. Køen bliver
    /// kun opdateret, når tråden selv behandler tastetryk, og det gør vores
    /// tråd kun, når HeyPia er det forreste vindue.
    ///
    /// Følgen var, at genvejen KUN virkede, når man stod i appen. Stod man i
    /// Word, kom tasten fint ind i hooken — men Ctrl og Shift så ud til at
    /// være oppe, <c>Genvejsgreb.Passer</c> sagde nej, og der skete
    /// ingenting. «Hold Ctrl+Shift+, nede mens du taler» virkede alle steder,
    /// hvor man ikke havde brug for det.
    ///
    /// Meldt 05-09-2026: «jeg har lige stået i Word og holdt genvejstasterne
    /// nede, og der skete ingenting».
    ///
    /// <c>GetAsyncKeyState</c> læser den fysiske tilstand og går ikke gennem
    /// nogen kø. Den er det rigtige valg i en lavniveau-hook, netop fordi
    /// hooken kaldes for tastetryk, der hører til et andet program.
    /// </remarks>
    private static bool Nede(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // Er vores egen tast nede lige nu? DET ER DET ENESTE, DER HUSKES.
    private bool _nede;
    private DateTime _nedTid;
    private bool _holder;
    private DispatcherTimer? _loft;

    /// <summary>
    /// Selve kaldet. Må ikke lave andet end at sammenligne og lægge i kø.
    /// </summary>
    private IntPtr Behandl(int kode, IntPtr wParam, IntPtr lParam)
    {
        if (kode < 0) return CallNextHookEx(_hook, kode, wParam, lParam);

        var besked = (int)wParam;
        var ned = besked is WM_KEYDOWN or WM_SYSKEYDOWN;
        var op = besked is WM_KEYUP or WM_SYSKEYUP;

        if (!ned && !op) return CallNextHookEx(_hook, kode, wParam, lParam);

        var d = Marshal.PtrToStructure<TASTEDATA>(lParam);

        // Vores egne udsendte tryk maa ikke udloese os selv.
        if ((d.flags & LLKHF_INJECTED) != 0 && d.dwExtraInfo == Maerke)
            return CallNextHookEx(_hook, kode, wParam, lParam);

        var udvidet = (d.flags & LLKHF_EXTENDED) != 0;

        // Holdetasterne selv er ikke en genvej.
        var erModifikator = d.vkCode is 0x10 or 0x11 or 0x12
                            or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

        if (erModifikator) return CallNextHookEx(_hook, kode, wParam, lParam);

        var ctrl = Nede(0x11);
        var shift = Nede(0x10);
        var alt = Nede(0x12);

        // ============ FANGER BRUGEREN SIN EGEN TAST? ============
        if (Fanger is not null && ned)
        {
            var fanget = new Genvejsgreb(d.scanCode, udvidet, ctrl, shift, alt);
            if (fanget.Duer)
            {
                var f = Fanger;
                Fanger = null;
                Kø(() => f(fanget));
                return (IntPtr)1;   // tasten skal ikke ogsaa lande i et felt
            }

            return CallNextHookEx(_hook, kode, wParam, lParam);
        }

        var greb = Greb;
        if (greb is null) return CallNextHookEx(_hook, kode, wParam, lParam);

        // ============ SLIP ============
        //
        // Slippet ses paa TASTEN alene. Modifikatorerne kan vaere sluppet
        // foerst - man loefter sjaeldent alle fingre paa samme mikrosekund -
        // og saa ville et krav om Ctrl+Shift her betyde, at slippet forsvandt.
        if (op)
        {
            if (!_nede || d.scanCode != greb.Scancode) return CallNextHookEx(_hook, kode, wParam, lParam);

            _nede = false;
            Kø(Sluppet);
            return (IntPtr)1;
        }

        // ============ TRYK ============
        if (!greb.Passer(d.scanCode, udvidet, ctrl, shift, alt))
            return CallNextHookEx(_hook, kode, wParam, lParam);

        // Windows gentager tasten, mens den holdes. Kun det foerste taeller.
        if (_nede) return (IntPtr)1;

        _nede = true;
        Kø(Trykket_Ned);

        // Tasten aedes. Ellers ville Ctrl+Shift+Delete ogsaa naa browseren.
        return (IntPtr)1;
    }

    /// <summary>Vores eget mærke på udsendte tastetryk, så vi kan kende dem igen.</summary>
    private static readonly IntPtr Maerke = new(0x48657950);   // "HeyP"

    private static void Kø(Action handling) =>
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Input, handling);

    // ------------------------------------------------------ tryk eller hold

    private void Trykket_Ned()
    {
        _nedTid = DateTime.UtcNow;
        _holder = false;

        if (!HoldGiverDiktering) return;

        // ÉN timer, der fyrer ÉN gang: naar graensen er naaet, og tasten
        // stadig er nede, er det et hold. Der spoerges ikke om noget
        // undervejs - slippet kommer af sig selv.
        _loft?.Stop();
        _loft = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = Holdvurdering.Graense,
        };
        _loft.Tick += (_, _) =>
        {
            _loft?.Stop();
            if (!_nede) return;

            _holder = true;
            HoldBegyndt?.Invoke();
            StartLoft();
        };
        _loft.Start();
    }

    /// <summary>Efter loftet skal der siges til — ellers taler man ud i intet.</summary>
    private void StartLoft()
    {
        var loft = Holdvurdering.LoftFra(Core.AppSettings.Current.DikteringLoftMinutter);

        _loft = new DispatcherTimer(DispatcherPriority.Background) { Interval = loft };
        _loft.Tick += (_, _) =>
        {
            _loft?.Stop();
            if (!_holder) return;

            _holder = false;
            _nede = false;
            HoldAfbrudt?.Invoke();
        };
        _loft.Start();
    }

    private void Sluppet()
    {
        _loft?.Stop();
        _loft = null;

        if (_holder)
        {
            _holder = false;
            HoldSluttet?.Invoke();
            return;
        }

        // Kort tryk. Var dikteringen slaaet fra, er det ogsaa den her vej,
        // trykket gaar - praecis som foer holdet fandtes.
        if (!HoldGiverDiktering || DateTime.UtcNow - _nedTid < Holdvurdering.Graense)
            Trykket?.Invoke();
    }

    public void Dispose()
    {
        _vagt?.Stop();
        _vagt = null;
        _loft?.Stop();
        _loft = null;
        Fjern();
    }
}
