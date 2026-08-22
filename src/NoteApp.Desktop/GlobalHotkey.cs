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
    public static readonly IReadOnlyList<HotkeyValg> Muligheder = Byg();

    private static IReadOnlyList<HotkeyValg> Byg()
    {
        var liste = new List<HotkeyValg>();

        // ============ KOMMAET STÅR FØRST OG ER DERMED STANDARDEN ============
        //
        // HotkeyId = null betyder «tag den første ledige», så rækkefølgen her
        // ER valget.
        //
        // Grebet er delt mellem hænderne: venstre holder Ctrl+Shift nede,
        // højre finder tasten. Så skal tasten ligge i højre side, og der er
        // kommaet det nærmeste, der ikke er taget af noget andet.
        //
        // Efterprøvet 20-08-2026 på denne maskine: fri både hos RegisterHotKey
        // og i Windows' egen inputtabel.
        //
        // «(komma)» står med i navnet med vilje. «Ctrl+Shift+,» slutter på et
        // komma, og et komma sidst i en sætning ligner tegnsætning — man kan
        // ikke se, om tasten er en del af genvejen eller bare et skilletegn.
        liste.Add(new HotkeyValg("ctrl-shift-komma", "Ctrl+Shift+, (komma)",
            MOD_CONTROL | MOD_SHIFT, 0xBC,
            "Venstre hånd holder Ctrl+Shift, højre rammer kommaet. Fri på stort set enhver maskine."));

        // TALLENE SOM ALTERNATIV. De er sjældent taget af andre programmer:
        // programmer bruger typisk bogstaver, og Windows selv bruger Win+tal
        // til proceslinjen — ikke Ctrl+Shift.
        //
        // HER STOD Ctrl+Shift+0 SOM STANDARD, OG DEN VIRKEDE IKKE.
        //
        // Windows havde selv taget den til at skifte tastatursprog, fordi der
        // var to layout installeret. RegisterHotKey sagde ja alligevel — de to
        // ting lever i hver sit system — så appen viste genvejen som aktiv,
        // mens intet skete, når man trykkede. Se TagetAfWindows.
        //
        // Nullet er ikke fjernet: er der kun ét tastatursprog på maskinen, er
        // det stadig en fin genvej. Men det står ikke længere først.
        //
        // Ctrl+0 UDEN Shift er efterprøvet ledig 19-08-2026 og er alligevel
        // ikke med. En global genvej vinder over det program, man står i, og
        // Ctrl+0 nulstiller zoom i enhver browser og fjerner afsnitsafstand i
        // Word. Windows siger ikke fra — konflikten viser sig som «zoom virker
        // ikke længere», og den ville ingen kæde sammen med den her app.
        liste.Add(new HotkeyValg("ctrl-shift-0", "Ctrl+Shift+0", MOD_CONTROL | MOD_SHIFT, 0x30,
            "Nullet ligger yderst på talrækken. Bruges af Windows til at skifte tastatursprog, hvis du har flere sprog installeret."));

        for (var n = 1; n <= 9; n++)
            liste.Add(new HotkeyValg($"ctrl-shift-{n}", $"Ctrl+Shift+{n}",
                MOD_CONTROL | MOD_SHIFT, (uint)(0x30 + n),
                "Tal rammes med venstre hånd alene og er sjældent taget af andre programmer."));

        // Punktummet lige ved siden af kommaet. Efterproevet fri samme dag.
        // Ctrl+Alt+komma er derimod TAGET (post 00000072) og Ctrl+punktum
        // ogsaa (post 00000012) - derfor staar de to ikke paa listen.
        liste.Add(new HotkeyValg("ctrl-shift-punktum", "Ctrl+Shift+. (punktum)",
            MOD_CONTROL | MOD_SHIFT, 0xBE,
            "Nabotasten til kommaet. Lige så fri — vælg den, der falder bedst i hånden."));

        // Bogstaverne som reserve. De er nemmere at huske, men oftere taget —
        // Ctrl+Alt+R og Ctrl+Alt+M var begge optaget på den første maskine,
        // appen blev prøvet på.
        liste.AddRange(new[]
        {
            new HotkeyValg("ctrl-alt-r", "Ctrl+Alt+R", MOD_CONTROL | MOD_ALT, 0x52,
                "R for «record». Tages af nogle lyd- og skærmoptagere."),
            new HotkeyValg("ctrl-alt-m", "Ctrl+Alt+M", MOD_CONTROL | MOD_ALT, 0x4D,
                "M for «møde». Bruges af enkelte noteprogrammer."),
            new HotkeyValg("ctrl-alt-o", "Ctrl+Alt+O", MOD_CONTROL | MOD_ALT, 0x4F,
                "O for «optag»."),
            new HotkeyValg("ctrl-shift-alt-r", "Ctrl+Shift+Alt+R", MOD_CONTROL | MOD_SHIFT | MOD_ALT, 0x52,
                "Tre taster gør den næsten sikkert ledig — til gengæld skal begge hænder med."),
            new HotkeyValg("ctrl-alt-f9", "Ctrl+Alt+F9", MOD_CONTROL | MOD_ALT, 0x78,
                "Funktionstast. Næsten altid ledig, men sværere at huske."),
            new HotkeyValg("ctrl-alt-f12", "Ctrl+Alt+F12", MOD_CONTROL | MOD_ALT, 0x7B,
                "Sidste udvej. Fri på stort set enhver maskine."),

            // PILETASTEN STAAR SIDST OG MED EN ADVARSEL.
            //
            // Den er teknisk fri - efterproevet 20-08-2026, baade hos
            // RegisterHotKey og i Windows' inputtabel - men den er i brug
            // overalt: Ctrl+Shift+pil markerer naeste ord i hvert eneste
            // tekstfelt i Windows. En global genvej vinder over det program,
            // man staar i, saa ordmarkering ville holde op med at virke, og
            // ingen ville kaede det sammen med den her app.
            //
            // Den er med, fordi valget er brugerens - ikke fordi den er god.
            new HotkeyValg("ctrl-shift-hoejre", "Ctrl+Shift+→", MOD_CONTROL | MOD_SHIFT, 0x27,
                "FRARÅDES: den markerer næste ord i alle tekstfelter i Windows. Vælger du den, holder ordmarkering op med at virke i andre programmer.")
        });

        return liste;
    }

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

        // De kombinationer, Windows selv bruger til at skifte inputmetode.
        // RegisterHotKey siger ja til dem, saa de skal sorteres fra HER -
        // ellers vaelger appen en genvej, der aldrig kommer til at virke.
        var systemtaget = TagetAfWindows();
        string? spaerret = null;

        foreach (var valg in rækkefølge)
        {
            if (systemtaget.Contains((valg.Modifiers, valg.Key)))
            {
                spaerret ??= valg.Navn;
                continue;
            }

            // MOD_NOREPEAT: holder man tasten nede, skal der starte EEN
            // optagelse, ikke tyve.
            if (!RegisterHotKey(_håndtag, Id, valg.Modifiers | MOD_NOREPEAT, valg.Key)) continue;

            _registreret = true;
            Aktiv = valg;

            Bemærkning = valg.Navn == spaerret ? null
                : spaerret is not null && (ønsket is null || ønsket.Navn == spaerret)
                    ? $"{spaerret} bruger Windows selv til at skifte tastatursprog — bruger {valg.Navn} i stedet"
                    : ønsket is not null && valg.Id != ønsket.Id
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
        // Windows' egen tekstbehandling spoerges FOERST. Se TagetAfWindows:
        // RegisterHotKey siger ja til de kombinationer, den har taget.
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
    /// Det skete i praksis 20-08-2026. Ctrl+Shift+0 stod i appen som den
    /// aktive genvej og gjorde ingenting. Årsagen lå i posten «00000104»
    /// under Control Panel\Input Method\Hot Keys, hvor Windows havde lagt
    /// Ctrl+Shift+0 til at skifte inputmetode — fordi der var to
    /// tastaturlayout installeret, dansk og amerikansk.
    ///
    /// Værdierne er fire byte hver, hvor kun den første betyder noget. Alt,
    /// Ctrl og Shift har præcis de samme bitværdier som i RegisterHotKey
    /// (1, 2 og 4), så de kan sammenlignes direkte.
    /// </summary>
    private static HashSet<(uint Mod, uint Key)> TagetAfWindows()
    {
        var taget = new HashSet<(uint, uint)>();

        try
        {
            using var rod = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Control Panel\Input Method\Hot Keys");

            if (rod is null) return taget;

            foreach (var navn in rod.GetSubKeyNames())
            {
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
            // den gamle opfoersel. En genvej, der maaske ikke virker, er
            // bedre end en app, der ikke starter.
        }

        // ============ HELE TALRAEKKEN, NAAR DER ER FLERE LAYOUT ============
        //
        // Posterne ovenfor daekker ikke det hele. Maalt 22-08-2026 paa denne
        // maskine: Ctrl+Shift+0 stod i tabellen, men Ctrl+Shift+1 gjorde IKKE
        // - og den virkede alligevel ikke. Appen havde registreret den
        // (efterproevet: en anden proces kunne ikke tage den bagefter), og
        // intet skete, naar der blev trykket.
        //
        // Forklaringen er, at Windows reserverer HELE Ctrl+Shift+ciffer-
        // raekken til at skifte til et bestemt tastaturlayout, saa snart der
        // er mere end ét installeret. Kun de layout, der har faaet et
        // udtrykkeligt nummer, staar i tabellen; resten af raekken er
        // reserveret uden at staa nogen steder.
        //
        // Derfor spoerges der efter, hvor mange layout der er - og er der
        // flere end ét, ryger 0 til 9 ud under ét. Prisen er ti valg paa en
        // liste med tyve; prisen ved at lade vaere er en genvej, der ser
        // aktiv ud og ikke goer noget.
        if (FlereTastatursprog())
            for (uint n = 0; n <= 9; n++)
                taget.Add((MOD_CONTROL | MOD_SHIFT, 0x30 + n));

        return taget;
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
            // Saa vaelges en genvej uden for talraekken, og den virker uanset
            // hvad. Et valg for lidt koster ingenting; et, der ikke virker,
            // koster en optagelse.
            return true;
        }
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
