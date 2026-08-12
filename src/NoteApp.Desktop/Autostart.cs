using Microsoft.Win32;

namespace NoteApp.Desktop;

/// <summary>
/// Start med Windows — så genvejstasten virker fra morgenstunden.
///
/// HVORFOR DET OVERHOVEDET GIVER MENING
///
/// Genvejstasten kan kun starte en optagelse, hvis appen kører. Skal man først
/// åbne den, er den ikke en genvej, men et ekstra klik oven i det, man
/// alligevel skulle gøre. Derfor hænger de to ting sammen.
///
/// HVOR DET SKRIVES
///
/// I brugerens egen Run-nøgle (HKEY_CURRENT_USER), ikke maskinens. Det kræver
/// ikke administrator, det rører ikke andre brugere af pc'en, og det kan
/// fjernes igen af den, der satte det.
///
/// Dette er den ENESTE ting i appen, der rører registreringsdatabasen, og den
/// gør det kun på et bevidst valg. Der lægges intet ind ved installation.
/// </summary>
public static class Autostart
{
    private const string Nøgle = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Navn = "NoteApp";

    /// <summary>Stien til den kørende exe, i anførselstegn så mellemrum overlever.</summary>
    private static string Kommando
    {
        get
        {
            var exe = Environment.ProcessPath ?? "";
            return $"\"{exe}\" --minimeret";
        }
    }

    public static bool ErSlaaetTil()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Nøgle);
            return k?.GetValue(Navn) is string s && s.Length > 0;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Slår autostart til eller fra. Returnerer en fejltekst, hvis det ikke
    /// lykkedes — et afkrydsningsfelt, der stille ikke gjorde noget, ville
    /// love noget, det ikke holder.
    /// </summary>
    public static string? Saet(bool til)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Nøgle, writable: true);
            if (k is null) return "kunne ikke åbne opstartsindstillingerne";

            if (til) k.SetValue(Navn, Kommando, RegistryValueKind.String);
            else k.DeleteValue(Navn, throwOnMissingValue: false);

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Peger den gemte sti på den exe, der faktisk kører? Er appen flyttet
    /// eller geninstalleret et andet sted, peger den gamle værdi på ingenting,
    /// og så starter appen ikke — uden at nogen får det at vide.
    /// </summary>
    public static bool ErForaeldet()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Nøgle);
            return k?.GetValue(Navn) is string s && s.Length > 0 &&
                   !s.Equals(Kommando, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }
}
