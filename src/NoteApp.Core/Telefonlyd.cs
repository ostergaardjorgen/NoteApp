using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NoteApp.Core;

/// <summary>
/// Går der lyd fra en telefon gennem Bluetooth lige nu?
/// </summary>
/// <remarks>
/// ============ TELEFONLINK MELDER SIG IKKE PÅ MIKROFONEN ============
///
/// Mødevagten spørger Windows' egen liste over, hvem der bruger mikrofonen.
/// Målt 10-09-2026 under et opkald gennem Telefonlink: Telefonlink stod der
/// ikke — hverken under opkaldet eller nogensinde før. Lyden føres af Windows
/// selv, ikke af appen, og så er der ingen app at skrive på listen. Vagten så
/// ingenting, og der kom ingen besked.
///
/// ============ DET, DER ER DER, ER TELEFONENS LYDENHED ============
///
/// En parret telefon giver Windows to lydenheder — en højttaler og en
/// mikrofon — der hører til telefonen og ikke til pc'en. Uden et opkald står
/// de som ikke tilsluttet; det er målt. Det er dem, samtalen går igennem.
///
/// ============ TELEFON OG IKKE HEADSET ============
///
/// Et Bluetooth-headset giver også sådan et par enheder, og et Teams-møde i
/// et headset er ikke et telefonopkald. Forskellen står i Bluetooth-
/// standarden: en TELEFON udbyder tjenesten «Hands-Free Audio Gateway»
/// (0x111F), et headset udbyder «Hands-Free» (0x111E). Tjenesten står i
/// enhedens id i Windows, to led over lydenheden:
///
///   lydenhed      SWD\MMDEVAPI\{0.0.1.00000000}.{79F1…}
///   forælder      BTHHFENUM\BthHFPAudio\8&amp;2f8a…
///   bedsteforælder BTHENUM\{0000111f-0000-1000-8000-00805f9b34fb}_VID…
///
/// Der sammenlignes med det tal og ikke med navnet «Hands-Free HF Audio»,
/// som Windows kan finde på at oversætte.
/// </remarks>
public static class Telefonlyd
{
    /// <summary>
    /// Bluetooth-tjenesten «Hands-Free Audio Gateway». Den udbydes af en
    /// telefon — aldrig af et headset.
    /// </summary>
    public const string Telefontjeneste = "{0000111f-0000-1000-8000-00805f9b34fb}";

    /// <summary>
    /// Går der et opkald gennem en parret telefon lige nu?
    /// </summary>
    /// <remarks>
    /// Fejler opslaget, er svaret nej. Vagten er en hjælp, ikke en
    /// forudsætning — og den må aldrig vælte appen.
    /// </remarks>
    public static bool IGang()
    {
        try
        {
            using var e = new MMDeviceEnumerator();

            foreach (var d in e.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
            {
                using (d)
                {
                    if (ErTelefon(d.ID)) return true;
                }
            }
        }
        catch (Exception)
        {
            // Intet svar er et nej. Se ovenfor.
        }

        return false;
    }

    /// <summary>Hører lydenheden til en telefon?</summary>
    /// <param name="endepunkt">Lydenhedens id, som Windows' lyd-API giver det.</param>
    public static bool ErTelefon(string endepunkt) =>
        Slaegt(@"SWD\MMDEVAPI\" + endepunkt).Any(ErTelefonsti);

    /// <summary>
    /// Står telefontjenesten i det her enheds-id?
    /// </summary>
    /// <remarks>
    /// Skilt ud, så den kan prøves af uden en telefon: id'erne er målt og står
    /// i testen.
    /// </remarks>
    public static bool ErTelefonsti(string instans) =>
        instans.Contains(Telefontjeneste, StringComparison.OrdinalIgnoreCase);

    /// <summary>Enheden selv og op til fire led over den.</summary>
    private static IEnumerable<string> Slaegt(string instans)
    {
        if (CM_Locate_DevNodeW(out var node, instans, 0) != 0) yield break;

        for (var led = 0; led < 5; led++)
        {
            var buf = new char[400];
            if (CM_Get_Device_IDW(node, buf, buf.Length, 0) != 0) yield break;

            yield return new string(buf).TrimEnd('\0');

            if (CM_Get_Parent(out node, node, 0) != 0) yield break;
        }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devinst, string id, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint parent, uint devinst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_IDW(uint devinst, char[] buffer, int len, uint flags);
}
