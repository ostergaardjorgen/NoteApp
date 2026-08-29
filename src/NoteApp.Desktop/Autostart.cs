using Microsoft.Win32;
using NoteApp.Core;

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
    // ============ NAVNET I RUN-NOEGLEN ============
    //
    // Det skiftede fra «NoteApp» til «HeyPia» 28-08-2026. Den gamle post
    // peger paa NoteApp.exe, som ikke findes mere, og den skal ryddes -
    // ellers proever Windows at starte en fil, der er vaek, ved hvert
    // login. Se Ryd gamle navne nedenfor.
    //
    // DE TO NAVNE HER STOD ET DOEGN SOM DET SAMME ORD. Navneskiftet blev
    // koert hen over hele kildekoden, og det ramte ogsaa det gamle navn:
    // «Gamle» kom til at indeholde «HeyPia». Saa ledte oprydningen efter det
    // navn, appen bruger NU, fandt intet, og gjorde ingenting - mens den
    // gamle post blev liggende og pegede paa en exe, der ikke findes mere.
    // Autostarten var altsaa slaaet til og virkede ikke.
    //
    // Havde der ligget en post under det nye navn, ville den samme fejl have
    // slettet den - ved hver opstart. Derfor staar der ogsaa et vaern nede i
    // RydGamleNavne. Rettet 29-08-2026.
    private const string Navn = "HeyPia";

    /// <summary>Navne, appen har haft foer. De ryddes ved opstart.</summary>
    private static readonly string[] Gamle = { "NoteApp" };

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
    /// Flytter en autostart fra et gammelt navn over på det nye.
    /// </summary>
    /// <remarks>
    /// APPEN SKIFTEDE NAVN 28-08-2026, og exe-filen med. Den gamle post i
    /// Run-nøglen peger på NoteApp.exe, som ikke findes længere — Windows
    /// ville prøve at starte den ved hvert login og fejle i stilhed.
    ///
    /// VÆRRE: den, der havde autostart slået til, ville miste den uden at
    /// blive spurgt, og opdage det den dag genvejstasten ikke virkede, fordi
    /// appen ikke kørte. Derfor flyttes valget med over — det er brugerens
    /// valg, ikke navnets.
    ///
    /// Kaldes én gang ved opstart. Er der intet gammelt, sker der ingenting.
    /// </remarks>
    public static void RydGamleNavne()
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Nøgle, writable: true);
            if (k is null) return;

            foreach (var gammelt in Gamle)
            {
                // ET GAMMELT NAVN, DER ER LIG DET NYE, ER IKKE ET GAMMELT NAVN.
                // Vaernet staar her, fordi listen EEN gang kom til at indeholde
                // det navn, appen bruger nu - og saa ville oprydningen slette
                // den post, den var sat i verden for at redde. Sker det igen,
                // sker der nu ingenting.
                if (string.Equals(gammelt, Navn, StringComparison.OrdinalIgnoreCase)) continue;

                if (k.GetValue(gammelt) is not string s || s.Length == 0) continue;

                // Var den slaaet til under det gamle navn, skal den vaere det
                // under det nye - med stien til den exe, der koerer NU.
                if (k.GetValue(Navn) is not string ny || ny.Length == 0)
                    k.SetValue(Navn, Kommando, RegistryValueKind.String);

                k.DeleteValue(gammelt, throwOnMissingValue: false);

                try
                {
                    Historik.Skriv(HaendelseType.Andet, "Autostart flyttet til det nye navn",
                        $"«{gammelt}» pegede på en fil, der ikke findes mere. "
                        + "Appen starter nu med Windows under navnet HeyPia.",
                        Udfald.Fuldført);
                }
                catch (Exception)
                {
                    // Kan historikken ikke skrives, er flytningen sket alligevel.
                }
            }
        }
        catch (Exception)
        {
            // En autostart, der ikke kunne flyttes, maa ikke forhindre appen i
            // at aabne. Skaermen viser tilstanden, som den faktisk er.
        }
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
