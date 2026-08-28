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

    /// <summary>Id til tvillingen paa taltastaturet. Se registreringen nedenfor.</summary>
    private const int TvillingId = 0x4E0D;

    private bool _tvilling;

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

        // De kombinationer, Windows selv bruger. RegisterHotKey siger ja til
        // dem, saa de skal sorteres fra HER.
        var systemtaget = TagetAfWindows();

        // ============ DEN, BRUGEREN SELV HAR TRYKKET, KOMMER FOERST ============
        //
        // Er der en egen kombination, er den bekraeftet ved at blive trykket
        // OG ved at genvejen faktisk kom frem bagefter. Den skal derfor
        // proeves foer alt andet.
        var egen = Genvejstast.Laes(AppSettings.Current.Genvejskombi);

        if (egen.Duer && !systemtaget.Contains((egen.Mod, egen.Vk))
            && RegisterHotKey(_håndtag, Id, egen.Mod | MOD_NOREPEAT, egen.Vk))
        {
            _registreret = true;

            // ============ TVILLINGEN PAA TALTASTATURET SKAL MED ============
            //
            // Den samme fysiske tast sender to forskellige virtuelle taster
            // alt efter NumLock: taltastaturets komma er 0x6E med NumLock til
            // og 0x2E (Delete) med den fra. Registrerer vi kun den ene, holder
            // genvejen op med at virke i det oejeblik, nogen roerer NumLock -
            // og der er intet at se.
            //
            // Maalt 28-08-2026: brugeren trykkede og fik «Ctrl+Delete» fanget.
            // Det var taltastaturets komma med NumLock slaaet fra.
            var tvilling = egen.Tvilling;
            _tvilling = tvilling != 0
                        && !systemtaget.Contains((egen.Mod, tvilling))
                        && RegisterHotKey(_håndtag, TvillingId, egen.Mod | MOD_NOREPEAT, tvilling);

            Aktiv = new HotkeyValg("egen", egen.Navn(Tastetegn(egen.Vk)), egen.Mod, egen.Vk,
                "Den kombination, du selv har trykket.");
            Bemærkning = null;
            Skriv(Aktiv, Aktiv);
            return true;
        }

        var ønsket = Muligheder.FirstOrDefault(m => m.Id == _ønsketId);

        // ============ ET GEMT VALG, DER ALDRIG KAN VIRKE, RYDDES ============
        //
        // Er den gemte tast reserveret af Windows selv, er den ikke
        // «midlertidigt optaget» - den kommer aldrig til at virke paa den her
        // maskine. Uden det her proevede appen den ved hver opstart, faldt
        // tilbage til noget andet, og brugeren sad fast paa en tast, han
        // aldrig havde valgt.
        //
        // Set 28-08-2026: HotkeyId stod paa «ctrl-shift-1», og hele
        // Ctrl+Shift+ciffer-raekken er reserveret, naar der er mere end ét
        // tastaturlayout. Vaerdien stammer fra en gammel opfoersel, hvor appen
        // gemte sit eget noedvalg som om det var brugerens.
        //
        // Der ryddes til null = standarden. Det er ikke at overrule et valg;
        // det er at fjerne noget, brugeren aldrig traf.
        if (ønsket is not null && systemtaget.Contains((ønsket.Modifiers, ønsket.Key)))
        {
            try
            {
                Historik.Skriv(HaendelseType.Andet, "Et gemt genvejsvalg blev ryddet",
                    $"{ønsket.Navn} er reserveret af Windows paa den her maskine og kunne "
                    + "aldrig virke. Standarden bruges nu.", Udfald.SeEfter);

                AppSettings.Current.HotkeyId = null;
                AppSettings.Current.Save();
            }
            catch (Exception)
            {
                // Kan det ikke gemmes, koeres der videre paa standarden i den
                // her omgang. Bedre end at blive staaende paa noget doedt.
            }

            _ønsketId = null;
            ønsket = null;
        }

        var rækkefølge = ønsket is null
            ? Muligheder
            : new[] { ønsket }.Concat(Muligheder.Where(m => m.Id != ønsket.Id)).ToList();
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
        if (besked != WM_HOTKEY) return IntPtr.Zero;

        var hvem = wParam.ToInt32();
        if (hvem != Id && hvem != TvillingId) return IntPtr.Zero;

        håndteret = true;

        var kald = Trykket;
        if (kald is not null)
            _vindue?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, kald);

        return IntPtr.Zero;
    }

    /// <summary>
    /// Slip genvejen midlertidigt — mens brugeren vælger en ny.
    /// </summary>
    /// <remarks>
    /// EN GLOBAL GENVEJ VINDER OVER ALT ANDET. Også over den skærm, hvor man
    /// skifter den. Blev den siddende, ville tastetrykket gå til den gamle
    /// genvej — der ville starte en optagelse — og skærmen ville aldrig se
    /// tasten.
    ///
    /// Det er værst netop for den tast, man helst vil skifte TIL: overlapper
    /// den med den nuværende, kan man aldrig nå at vælge den. Set 28-08-2026,
    /// hvor taltastaturets komma og Ctrl+Delete er den samme fysiske tast.
    ///
    /// <see cref="Genoptag"/> sætter den tilbage. Genvejstasten er ude af
    /// drift imens, og det er den rigtige pris: man står i Indstillinger og
    /// vælger genvej, ikke til et møde.
    /// </remarks>
    public void Pause()
    {
        if (_registreret && _håndtag != IntPtr.Zero) UnregisterHotKey(_håndtag, Id);
        if (_tvilling && _håndtag != IntPtr.Zero) UnregisterHotKey(_håndtag, TvillingId);

        _pauset = _registreret;
        _pausetTvilling = _tvilling;
        _registreret = false;
        _tvilling = false;
    }

    /// <summary>Sæt den tilbage, som den var.</summary>
    public void Genoptag()
    {
        if (!_pauset || Aktiv is null || _håndtag == IntPtr.Zero) return;

        _registreret = RegisterHotKey(_håndtag, Id, Aktiv.Modifiers | MOD_NOREPEAT, Aktiv.Key);

        if (_pausetTvilling)
        {
            var t = Genvejstast.Tvillingen(Aktiv.Key);
            _tvilling = t != 0 && RegisterHotKey(_håndtag, TvillingId, Aktiv.Modifiers | MOD_NOREPEAT, t);
        }

        _pauset = false;
        _pausetTvilling = false;
    }

    private bool _pauset;
    private bool _pausetTvilling;

    private void Frigiv()
    {
        if (_registreret && _håndtag != IntPtr.Zero) UnregisterHotKey(_håndtag, Id);
        if (_tvilling && _håndtag != IntPtr.Zero) UnregisterHotKey(_håndtag, TvillingId);
        _registreret = false;
        _tvilling = false;
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

    /// <summary>
    /// Er kombinationen taget af Windows selv? Til <see cref="Preferences.Genvejsfanger"/>.
    /// </summary>
    /// <remarks>
    /// Den skal spoerges FOER registreringen. RegisterHotKey siger ja til de
    /// kombinationer, Windows' egen tekstbehandling har taget - og saa sker
    /// der ingenting, naar man trykker. Se TagetAfWindows.
    /// </remarks>
    public static bool ErSpaerretAfWindows(uint mod, uint vk) =>
        TagetAfWindows().Contains((mod, vk));

    /// <summary>Tag en kombination med et bestemt id. Til proevekoerslen.</summary>
    public static bool Tag(IntPtr vindue, int id, uint mod, uint vk) =>
        RegisterHotKey(vindue, id, mod | MOD_NOREPEAT, vk);

    /// <summary>Giv den fra dig igen.</summary>
    public static void Slip(IntPtr vindue, int id) => UnregisterHotKey(vindue, id);

    /// <summary>
    /// Tegnet, en tast giver paa det aktuelle layout — til at vise navnet med.
    /// </summary>
    /// <remarks>
    /// De layoutafhaengige taster har ingen fast betydning: 0xBC er «,» paa
    /// dansk og amerikansk, men den slags kan ikke antages for alle. Windows
    /// ved det, og Core goer ikke — derfor slaas det op her og gives videre.
    ///
    /// MAPVK_VK_TO_CHAR = 2. Svarer den nul, har tasten intet tegn (F-taster,
    /// piletaster), og saa bruger Core sit eget navn.
    /// </remarks>
    public static string? Tastetegn(uint vk)
    {
        try
        {
            var t = MapVirtualKey(vk, 2) & 0x7FFF;
            return t == 0 ? null : ((char)t).ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint kode, uint slags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
