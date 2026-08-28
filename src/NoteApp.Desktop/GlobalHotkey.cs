using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Registrerer genvejstasten hos Windows og siger til, når den bliver trykket.
///
/// KANDIDATERNE STÅR I <see cref="Genvejstaster"/>. Her er kun det, der kræver
/// Windows: registreringen, de kombinationer systemet selv har taget, og
/// beskeden, når nogen trykker.
///
/// HVORFOR DEN SKAL VÆRE GLOBAL
///
/// Et møde begynder, mens man har noget andet på skærmen: en kalender, et
/// Teams-vindue, et dokument. Skal man først finde appen frem, er de første
/// minutter væk — og det er tit dér, dagsordenen bliver aftalt.
///
/// DET SIGES HØJT, NÅR DER BLIVER VALGT NOGET ANDET
///
/// Er den ønskede tast optaget, tages den næste ledige. En app, der stille
/// vælger noget andet, end brugeren har bedt om, er værre end en, der fejler:
/// man trykker på sin egen tast, der sker ingenting, og der er intet at se.
/// Det er præcis dét, der fik genvejen til at virke «en gang imellem». Nu
/// skrives det i historikken, og der prøves at få den ønskede tast tilbage.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int Id = 0x4E0A;

    private const uint MOD_CONTROL = Genvejstaster.MOD_CONTROL;
    private const uint MOD_SHIFT = Genvejstaster.MOD_SHIFT;
    private const uint MOD_NOREPEAT = 0x4000;

    /// <summary>Kandidaterne i den rækkefølge, de prøves.</summary>
    public static IReadOnlyList<HotkeyValg> Muligheder => Genvejstaster.Muligheder;

    private HwndSource? _kilde;
    private IntPtr _håndtag;
    private bool _registreret;
    private Window? _vindue;
    private string? _ønsketId;
    private DispatcherTimer? _genforsøg;

    /// <summary>Rejses når genvejen bliver trykket.</summary>
    public event Action? Trykket;

    /// <summary>Den kombination, der faktisk blev registreret. Null hvis ingen lykkedes.</summary>
    public HotkeyValg? Aktiv { get; private set; }

    /// <summary>Sat, hvis den ønskede kombination var taget, og der blev valgt en anden.</summary>
    public string? Bemærkning { get; private set; }

    /// <summary>Rejses, når registreringen skifter — så skærmen kan følge med.</summary>
    public event Action? Ændret;

    /// <summary>
    /// Kobler genvejen på et vindue og finder en kombination, der er ledig.
    ///
    /// Den ønskede prøves først. Er den taget, prøves resten af listen — og
    /// det siges bagefter, hvilken der blev brugt.
    /// </summary>
    public bool Tilslut(Window vindue, string? ønsketId = null)
    {
        _vindue = vindue;
        _ønsketId = ønsketId;

        var ok = Registrer();

        // ============ DEN ØNSKEDE TAST SKAL PRØVES IGEN ============
        //
        // Er den taget af et andet program, er det som regel MIDLERTIDIGT: en
        // anden kopi af appen, der ikke var lukket helt, eller et program, der
        // slipper tasten, når det lukkes. Uden det her sad man på en anden
        // tast, til appen blev genstartet — og det var netop dét, der fik
        // genvejen til at virke «en gang imellem»: hver opstart kunne give sin
        // egen tast, uden at nogen havde valgt noget.
        //
        // Der prøves hvert minut. Et opslag, der tager mikrosekunder, og som
        // stopper af sig selv, så snart den rigtige tast er i hus.
        StartGenforsøg();

        return ok;
    }

    private bool Registrer()
    {
        Frigiv();

        if (_vindue is null) { Bemærkning = "der er ikke noget vindue"; return false; }

        _håndtag = new WindowInteropHelper(_vindue).Handle;
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

        var ønsket = Muligheder.FirstOrDefault(m => m.Id == _ønsketId);
        var rækkefølge = ønsket is null
            ? Muligheder
            : new[] { ønsket }.Concat(Muligheder.Where(m => m.Id != ønsket.Id)).ToList();

        // De kombinationer, Windows selv bruger. RegisterHotKey siger ja til
        // dem, saa de skal sorteres fra HER.
        var systemtaget = TagetAfWindows();
        string? spærret = null;

        foreach (var valg in rækkefølge)
        {
            if (systemtaget.Contains((valg.Modifiers, valg.Key)))
            {
                spærret ??= valg.Navn;
                continue;
            }

            // MOD_NOREPEAT: holder man tasten nede, skal der starte EEN
            // optagelse, ikke tyve.
            if (!RegisterHotKey(_håndtag, Id, valg.Modifiers | MOD_NOREPEAT, valg.Key)) continue;

            _registreret = true;
            Aktiv = valg;

            Bemærkning = valg.Navn == spærret ? null
                : spærret is not null && (ønsket is null || ønsket.Navn == spærret)
                    ? $"{spærret} bruger Windows selv til at skifte tastatursprog — bruger {valg.Navn} i stedet"
                    : ønsket is not null && valg.Id != ønsket.Id
                        ? $"{ønsket.Navn} var taget af et andet program — bruger {valg.Navn} i stedet"
                        : null;

            Skriv(valg, ønsket);
            return true;
        }

        Bemærkning = "alle kombinationer på listen er taget af andre programmer";
        Skriv(null, ønsket);

        _kilde.RemoveHook(Hook);
        _kilde = null;
        return false;
    }

    /// <summary>
    /// Skriver i historikken, hvad der blev registreret.
    /// </summary>
    /// <remarks>
    /// DET SKAL KUNNE SLÅS OP BAGEFTER. Blev der valgt en anden tast end den,
    /// brugeren tror, de har, opdager man det først den dag, et møde ikke blev
    /// optaget — og så er der intet at se på. En linje i historikken koster
    /// ingenting og gør forskellen på et gæt og et svar.
    ///
    /// Der skrives kun, når det ÆNDRER sig. Ellers ville hver opstart lægge en
    /// linje, og historikken ville drukne i det almindelige.
    /// </remarks>
    private static string? _sidstSkrevet;

    private void Skriv(HotkeyValg? valg, HotkeyValg? ønsket)
    {
        var linje = valg is null ? "ingen" : valg.Id;
        if (linje == _sidstSkrevet) return;
        _sidstSkrevet = linje;

        try
        {
            if (valg is null)
                Historik.Skriv(HaendelseType.Andet, "Genvejstasten virker ikke",
                    Bemærkning ?? "ingen kombination kunne registreres", Udfald.SeEfter);
            else if (ønsket is not null && valg.Id != ønsket.Id)
                Historik.Skriv(HaendelseType.Andet, "Genvejstasten blev en anden",
                    $"Du har valgt {ønsket.Navn}. Den var optaget, så {valg.Navn} bruges nu. "
                    + "Der prøves at få din egen tilbage hvert minut.", Udfald.SeEfter);
            else
                Historik.Skriv(HaendelseType.Andet, "Genvejstasten er klar",
                    $"{valg.Navn} starter en optagelse.", Udfald.Fuldført);
        }
        catch (Exception)
        {
            // Kan historikken ikke skrives, er der ikke mere at goere.
        }
    }

    private void StartGenforsøg()
    {
        _genforsøg?.Stop();

        // Har vi den, brugeren bad om — eller er der slet ikke oensket noget
        // bestemt og vi fik den foerste paa listen — er der intet at proeve.
        if (Aktiv is not null && (_ønsketId is null
                ? Aktiv.Id == Muligheder[0].Id
                : Aktiv.Id == _ønsketId))
            return;

        _genforsøg ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _genforsøg.Tick -= Genforsøg;
        _genforsøg.Tick += Genforsøg;
        _genforsøg.Start();
    }

    private void Genforsøg(object? afsender, EventArgs e)
    {
        var før = Aktiv?.Id;

        if (!Registrer()) return;

        if (Aktiv?.Id == før) return;

        _genforsøg?.Stop();
        Ændret?.Invoke();
        StartGenforsøg();
    }

    /// <summary>Er kombinationen ledig lige nu? Bruges til at vise listen ærligt.</summary>
    public static bool ErLedig(HotkeyValg valg, Window vindue)
    {
        // Windows' egen tekstbehandling spoerges FOERST. Se TagetAfWindows.
        if (TagetAfWindows().Contains((valg.Modifiers, valg.Key))) return false;

        var h = new WindowInteropHelper(vindue).Handle;
        if (h == IntPtr.Zero) return true;

        // Proev at tage den, og giv den fra dig igen med det samme. Et andet
        // id end det rigtige, saa en aktiv registrering ikke bliver forstyrret.
        const int prøveId = 0x4E0B;
        if (!RegisterHotKey(h, prøveId, valg.Modifiers | MOD_NOREPEAT, valg.Key)) return false;

        UnregisterHotKey(h, prøveId);
        return true;
    }

    /// <summary>
    /// De kombinationer, Windows' egen tekstbehandling allerede har taget.
    ///
    /// HVORFOR DEN HER KONTROL SKAL LAVES SÆRSKILT
    ///
    /// RegisterHotKey siger JA til dem. De to ting lever i hver sit system:
    /// genvejstaster hører til vindueshåndteringen, mens skift af inputmetode
    /// håndteres af tekstbehandlingen (TSF), som får tastetrykket først. Appen
    /// får altså lov at registrere en genvej, der aldrig kommer til at virke,
    /// og der er intet at se — hverken en fejl eller en advarsel.
    ///
    /// Det skete i praksis 20-08-2026 med Ctrl+Shift+0.
    ///
    /// MEN TABELLEN SIGER MERE, END DER GÆLDER
    ///
    /// Posternes numre betyder noget, og det blev overset første gang. Fra
    /// Windows' egen imm.h:
    ///
    ///     0x0010–0x0012   kinesisk IME
    ///     0x0030–0x0032   japansk IME
    ///     0x0050–0x0052   koreansk IME
    ///     0x0070–0x0072   thai IME
    ///     0x0100–0x011F   skift til et bestemt inputsprog
    ///     0x0200–0x021F   program-egne
    ///
    /// De fire første grupper gælder KUN, hvis den pågældende inputmetode er
    /// installeret. Posterne ligger der på enhver Windows-maskine, også uden
    /// et eneste asiatisk sprog.
    ///
    /// Målt på denne maskine 28-08-2026: post 00000010 og 00000070 stod begge
    /// på Ctrl+Space — kinesisk og thai. Ingen af delene er installeret; kun
    /// dansk (00000406) og amerikansk (00000409), som begge er almindelige
    /// tastaturlayout. Ctrl+Space blev afprøvet i praksis: registreret,
    /// tastetryk sendt, WM_HOTKEY kom frem. Den VIRKER.
    ///
    /// Uden den her skelnen ville appen afvise en tast, der er fuldt brugbar,
    /// og stille vælge en anden — netop den slags, der får en genvej til at
    /// virke «en gang imellem».
    /// </summary>
    private static HashSet<(uint Mod, uint Key)> TagetAfWindows()
    {
        var taget = new HashSet<(uint, uint)>();
        var harInputmetode = HarInputmetode();

        try
        {
            using var rod = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Control Panel\Input Method\Hot Keys");

            if (rod is null) return taget;

            foreach (var navn in rod.GetSubKeyNames())
            {
                if (!int.TryParse(navn, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var nummer))
                    continue;

                // Er det en IME-post, og er der ingen IME installeret, gaelder
                // den ikke. Se forklaringen ovenfor.
                var erIme = nummer < 0x0100;
                if (erIme && !harInputmetode) continue;

                using var post = rod.OpenSubKey(navn);
                if (post is null) continue;

                if (post.GetValue("Key Modifiers") is not byte[] mod || mod.Length == 0) continue;
                if (post.GetValue("Virtual Key") is not byte[] tast || tast.Length == 0) continue;

                // 0 betyder "ingen tast sat" - posten findes, men er slaaet fra.
                if (tast[0] == 0) continue;

                taget.Add((mod[0], tast[0]));
            }
        }
        catch (Exception)
        {
            // Kan registreringsdatabasen ikke laeses, falder vi tilbage til
            // den gamle opfoersel.
        }

        // ============ HELE TALRAEKKEN, NAAR DER ER FLERE LAYOUT ============
        //
        // Posterne ovenfor daekker ikke det hele. Maalt 22-08-2026: Ctrl+Shift+0
        // stod i tabellen, men Ctrl+Shift+1 gjorde IKKE - og den virkede
        // alligevel ikke. Windows reserverer HELE Ctrl+Shift+ciffer-raekken til
        // at skifte tastaturlayout, saa snart der er mere end ét installeret.
        if (FlereTastatursprog())
            for (uint n = 0; n <= 9; n++)
                taget.Add((MOD_CONTROL | MOD_SHIFT, 0x30 + n));

        return taget;
    }

    /// <summary>
    /// Er der en rigtig inputmetode installeret — ikke bare et tastaturlayout?
    /// </summary>
    /// <remarks>
    /// Et almindeligt layout står som otte hextegn, hvor de fire første er
    /// nuller: 00000406 er dansk, 00000409 amerikansk. En inputmetode har et
    /// tal forskelligt fra nul i den øverste halvdel — E0010804 og lignende.
    ///
    /// Kan det ikke afgøres, svares JA. Så gælder IME-posterne, og appen vælger
    /// en anden tast. Et valg for lidt koster ingenting; en genvej, der ikke
    /// virker, koster en optagelse.
    /// </remarks>
    private static bool HarInputmetode()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload");
            if (k is null) return true;

            foreach (var navn in k.GetValueNames())
            {
                if (k.GetValue(navn) is not string v) continue;
                if (v.Length != 8) continue;

                if (uint.TryParse(v, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var id)
                    && (id >> 16) != 0)
                    return true;
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    /// <summary>
    /// Er der mere end ét tastaturlayout installeret?
    ///
    /// Listen staar under Keyboard Layout\Preload med ét nummereret felt pr.
    /// layout. To felter betyder, at Windows har brug for en genvej til at
    /// skifte mellem dem.
    /// </summary>
    private static bool FlereTastatursprog()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload");
            return k is not null && k.GetValueNames().Length > 1;
        }
        catch (Exception)
        {
            // Kan det ikke afgoeres, antages det VAERSTE: at der er flere.
            return true;
        }
    }

    /// <summary>
    /// Tastetrykket er kommet. Der svares MED DET SAMME, og arbejdet lægges i kø.
    /// </summary>
    /// <remarks>
    /// DER MÅ IKKE ARBEJDES HERINDE. Hooken kører midt i vinduets
    /// beskedbehandling, og optagelsen åbner en dialog. En dialog pumper sin
    /// egen beskedkø, og gør den det inde fra en hook, sidder man med to
    /// pumper oven i hinanden — det kan låse, og det opfører sig forskelligt
    /// fra gang til gang.
    ///
    /// BeginInvoke lægger arbejdet bagest i køen. Hooken svarer med det samme,
    /// beskedbehandlingen kommer videre, og optagelsen starter et øjeblik
    /// efter — på et tidspunkt, hvor der er ryddet op.
    /// </remarks>
    private IntPtr Hook(IntPtr hwnd, int besked, IntPtr wParam, IntPtr lParam, ref bool håndteret)
    {
        if (besked != WM_HOTKEY || wParam.ToInt32() != Id) return IntPtr.Zero;

        håndteret = true;

        var kald = Trykket;
        if (kald is not null)
            _vindue?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, kald);

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

    public void Dispose()
    {
        _genforsøg?.Stop();
        _genforsøg = null;
        Frigiv();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
