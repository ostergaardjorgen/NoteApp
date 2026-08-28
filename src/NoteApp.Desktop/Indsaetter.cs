using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace NoteApp.Desktop;

/// <summary>Det vindue, der var fremme, da dikteringen begyndte.</summary>
/// <param name="Haandtag">Vinduet selv.</param>
/// <param name="Proces">Processens navn, uden «.exe». Tom, hvis den ikke kunne læses.</param>
/// <param name="Titel">Vinduets titel, som den var ved starten.</param>
public readonly record struct Forgrundsvindue(IntPtr Haandtag, string Proces, string Titel)
{
    public bool Findes => Haandtag != IntPtr.Zero;
}

/// <summary>
/// Lægger den dikterede tekst, hvor markøren står — i det program, du var i
/// gang med.
///
/// HVORFOR DER INDSÆTTES VED AT SÆTTE IND, OG IKKE VED AT SKRIVE
///
/// Teksten kunne også sendes tegn for tegn som tastetryk. Det ville lade
/// udklipsholderen være i fred, og det var den første tanke. Men et tastetryk
/// pr. tegn er hundredvis af beskeder til et fremmed program, og de programmer,
/// der taber et af dem — terminaler, ældre felter, alt med sin egen
/// tastaturhåndtering — taber det midt i et ord. En sætning med et bogstav for
/// lidt er værre end ingen indsættelse, fordi man ikke opdager den.
///
/// Ctrl+V er ét tastetryk. Programmet henter selv teksten, og enten virker
/// det, eller også sker der ingenting.
///
/// TEKSTEN BLIVER LIGGENDE I UDKLIPSHOLDEREN BAGEFTER
///
/// Den kunne sættes tilbage til det, der lå der før. Det gør den ikke, og det
/// er et valg: det, man lige har dikteret, er dét, man vil sætte ind igen, hvis
/// det første forsøg landede et forkert sted. Blev det gamle sat tilbage,
/// ville teksten være væk i samme øjeblik, man opdagede fejlen.
/// </summary>
public static class Indsaetter
{
    /// <summary>Læser, hvad der er fremme lige nu. Kaldes, når holdet begynder.</summary>
    public static Forgrundsvindue Laes()
    {
        try
        {
            var h = GetForegroundWindow();
            if (h == IntPtr.Zero) return default;

            _ = GetWindowThreadProcessId(h, out var pid);

            var navn = "";
            try { navn = Process.GetProcessById((int)pid).ProcessName; }
            catch (Exception) { /* processen kan vaere lukket igen. */ }

            var sb = new StringBuilder(512);
            _ = GetWindowText(h, sb, sb.Capacity);

            return new Forgrundsvindue(h, navn, sb.ToString());
        }
        catch (Exception)
        {
            // Kan forgrunden ikke laeses, indsaettes der ikke. Teksten ligger
            // stadig i udklipsholderen.
            return default;
        }
    }

    /// <summary>
    /// Sætter teksten ind i <paramref name="maal"/>, hvis det stadig er fremme.
    /// </summary>
    /// <returns>Sandt, hvis der blev sat ind. Falsk betyder «ligger i udklipsholderen».</returns>
    /// <remarks>
    /// DER TAGES IKKE FOKUS. Appen kalder ikke SetForegroundWindow og flytter
    /// ikke noget frem — den skriver kun, hvis du selv stadig står dér.
    ///
    /// Er du skiftet væk i mellemtiden — udskriften tager et sekund eller to —
    /// ville teksten ellers lande i et helt andet program, midt i noget andet.
    /// </remarks>
    public static bool Indsaet(string tekst, Forgrundsvindue maal)
    {
        if (tekst.Length == 0 || !maal.Findes) return false;

        Læg(tekst);

        if (GetForegroundWindow() != maal.Haandtag) return false;

        // ============ VENT TIL GENVEJSTASTEN ER SLUPPET HELT ============
        //
        // Holdet var Ctrl+komma. Slippes kommaet foerst, mens Ctrl bliver
        // holdt et oejeblik mere, ville vores Ctrl+V blive til Ctrl+Ctrl+V -
        // eller vaerre, ramme en helt anden genvej i programmet.
        //
        // Udskriften tager over et sekund, saa i praksis er alt for laengst
        // sluppet. Det her er for det tilfaelde, hvor den ikke er.
        for (var i = 0; i < 40 && Nede(); i++) Thread.Sleep(25);
        if (Nede()) return false;

        try
        {
            Tast(VK_CONTROL, ned: true);
            Tast(VK_V, ned: true);
            Tast(VK_V, ned: false);
            Tast(VK_CONTROL, ned: false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Er en af de taster, der indgår i genvejen, stadig nede?</summary>
    private static bool Nede() =>
        (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
        || (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0
        || (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

    /// <summary>
    /// Lægger teksten i udklipsholderen.
    /// </summary>
    /// <remarks>
    /// UDKLIPSHOLDEREN KAN VÆRE OPTAGET. Et andet program kan holde den et
    /// øjeblik, og så fejler det første forsøg. Der prøves et par gange —
    /// ellers ville et diktat, der lykkedes hele vejen, gå tabt på det sidste
    /// skridt.
    /// </remarks>
    public static void Læg(string tekst)
    {
        for (var forsøg = 0; forsøg < 5; forsøg++)
        {
            try
            {
                Clipboard.SetText(tekst);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(60);
            }
        }
    }

    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_MENU = 0x12;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void Tast(byte vk, bool ned) =>
        keybd_event(vk, (byte)MapVirtualKey(vk, 0), ned ? 0 : KEYEVENTF_KEYUP, UIntPtr.Zero);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder tekst, int maks);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flag, UIntPtr ekstra);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint kode, uint slags);
}
