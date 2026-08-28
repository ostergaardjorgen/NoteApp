namespace NoteApp.Core;

/// <summary>Én kandidat til lynstart-genvejen.</summary>
public sealed record HotkeyValg(string Id, string Navn, uint Modifiers, uint Key, string Hvorfor);

/// <summary>
/// Kandidaterne til genvejstasten, der starter en optagelse.
///
/// HVORFOR LISTEN LIGGER HER OG IKKE I DESKTOP
///
/// Fordi den er DATA og skal kunne efterprøves. Rækkefølgen er ikke en detalje
/// — den første ledige bliver standarden for enhver ny bruger. En dublet eller
/// en ombytning ville ændre, hvilken tast alle får, uden at nogen havde ændret
/// en indstilling. Se GenvejTest.
///
/// Selve registreringen hører til Windows og ligger i GlobalHotkey.
///
/// HVORFOR DER ER FLERE
///
/// Genvejstaster er optaget af vidt forskellige programmer fra maskine til
/// maskine. Ctrl+Alt+R var ledig i teorien og taget i praksis på den første
/// maskine, den blev prøvet på. Et fast valg, der ikke kan lade sig gøre, er
/// ingen genvej.
///
/// Fravalgt med vilje: Ctrl+R og Ctrl+Shift+R (genindlæsning i alle browsere),
/// Win+R (Kør), Win+Alt+R (Xbox Game Bar optager skærmen), Ctrl+Alt+Delete.
/// </summary>
public static class Genvejstaster
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;

    public static readonly IReadOnlyList<HotkeyValg> Muligheder = Byg();

    private static IReadOnlyList<HotkeyValg> Byg()
    {
        var liste = new List<HotkeyValg>();

        // ============ CTRL+SPACE STÅR FØRST OG ER DERMED STANDARDEN ============
        //
        // HotkeyId = null betyder «tag den første ledige», så rækkefølgen her
        // ER valget.
        //
        // Efterprøvet 28-08-2026, og ikke bare med RegisterHotKey:
        // kombinationen blev registreret, tastetrykket blev sendt, og
        // WM_HOTKEY kom frem. Det er den eneste måde at vide det — se
        // GlobalHotkey.TagetAfWindows for, hvorfor et ja fra RegisterHotKey
        // ikke er nok.
        //
        // Den er let: én hånd, to taster ved siden af hinanden, og
        // mellemrumstasten kan findes uden at kigge.
        //
        // DEN KOSTER NOGET, OG DET SKAL STÅ. En global genvej vinder over det
        // program, man står i. Ctrl+Space rydder formatering i Word, markerer
        // hele kolonnen i Excel og åbner forslagslisten i stort set alle
        // kodeeditorer. Vælger man den, holder de ting op med at virke — og
        // ingen kæder det sammen med den her app. Derfor står prisen i den
        // tekst, brugeren læser, og de andre valg bliver stående.
        liste.Add(new HotkeyValg("ctrl-space", "Ctrl+Space", MOD_CONTROL, 0x20,
            "Én hånd, to nabotaster, og mellemrum kan findes uden at kigge. "
            + "Bemærk: den rydder formatering i Word, markerer kolonnen i Excel "
            + "og åbner forslagslisten i kodeeditorer — de ting holder op med at "
            + "virke, så længe den er valgt."));

        // Naboen. Samme greb, én tast mere. Efterproevet virksom samme dag.
        //
        // HER STOD «UDEN KONFLIKTEN», OG DET VAR FOR MEGET SAGT. Enhver global
        // genvej opsnapper tasten fra det program, man staar i - det er maalt,
        // ikke antaget. Den her har bare en billigere konflikt: fast mellemrum
        // i Word og marker omraadet i Excel, mod ryd formatering og marker
        // kolonnen.
        liste.Add(new HotkeyValg("ctrl-shift-space", "Ctrl+Shift+Space",
            MOD_CONTROL | MOD_SHIFT, 0x20,
            "Samme greb som Ctrl+Space. Den koster et fast mellemrum i Word og "
            + "«markér området» i Excel — mindre brugte end dem, Ctrl+Space tager."));

        // ============ KOMMAET - den tidligere standard ============
        //
        // Grebet er delt mellem haenderne: venstre holder Ctrl+Shift nede,
        // hoejre finder tasten. Efterproevet virksom 28-08-2026.
        //
        // «(komma)» staar med i navnet med vilje. «Ctrl+Shift+,» slutter paa et
        // komma, og et komma sidst i en saetning ligner tegnsaetning - man kan
        // ikke se, om tasten er en del af genvejen eller bare et skilletegn.
        liste.Add(new HotkeyValg("ctrl-shift-komma", "Ctrl+Shift+, (komma)",
            MOD_CONTROL | MOD_SHIFT, 0xBC,
            "Venstre hånd holder Ctrl+Shift, højre rammer kommaet. Fri på stort set enhver maskine."));

        // Punktummet lige ved siden af kommaet. Efterproevet virksom samme dag.
        // Ctrl+Alt+komma er derimod TAGET (post 00000072) og Ctrl+punktum
        // ogsaa (post 00000012) - derfor staar de to ikke paa listen.
        liste.Add(new HotkeyValg("ctrl-shift-punktum", "Ctrl+Shift+. (punktum)",
            MOD_CONTROL | MOD_SHIFT, 0xBE,
            "Nabotasten til kommaet. Lige så fri — vælg den, der falder bedst i hånden."));

        // TALLENE SOM ALTERNATIV. Programmer bruger typisk bogstaver, og
        // Windows selv bruger Win+tal til proceslinjen - ikke Ctrl+Shift.
        //
        // HER STOD Ctrl+Shift+0 SOM STANDARD, OG DEN VIRKEDE IKKE. Windows
        // havde selv taget den til at skifte tastatursprog, fordi der var to
        // layout installeret. RegisterHotKey sagde ja alligevel.
        liste.Add(new HotkeyValg("ctrl-shift-0", "Ctrl+Shift+0", MOD_CONTROL | MOD_SHIFT, 0x30,
            "Nullet ligger yderst på talrækken. Bruges af Windows til at skifte tastatursprog, hvis du har flere sprog installeret."));

        for (var n = 1; n <= 9; n++)
            liste.Add(new HotkeyValg($"ctrl-shift-{n}", $"Ctrl+Shift+{n}",
                MOD_CONTROL | MOD_SHIFT, (uint)(0x30 + n),
                "Tal rammes med venstre hånd alene og er sjældent taget af andre programmer."));

        // Bogstaverne som reserve. Nemmere at huske, men oftere taget -
        // Ctrl+Alt+R og Ctrl+Alt+M var begge optaget paa den foerste maskine.
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
            // Den er teknisk fri - efterproevet 20-08-2026 - men den er i brug
            // overalt: Ctrl+Shift+pil markerer naeste ord i hvert eneste
            // tekstfelt i Windows. En global genvej vinder over det program,
            // man staar i, saa ordmarkering ville holde op med at virke.
            //
            // Den er med, fordi valget er brugerens - ikke fordi den er god.
            new HotkeyValg("ctrl-shift-hoejre", "Ctrl+Shift+→", MOD_CONTROL | MOD_SHIFT, 0x27,
                "FRARÅDES: den markerer næste ord i alle tekstfelter i Windows. Vælger du den, holder ordmarkering op med at virke i andre programmer.")
        });

        return liste;
    }
}
