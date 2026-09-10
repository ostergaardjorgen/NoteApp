using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NoteApp.Core;

/// <summary>
/// Går der et telefonopkald gennem computeren lige nu?
/// </summary>
/// <remarks>
/// ============ TELEFONLINK MELDER SIG IKKE PÅ MIKROFONEN ============
///
/// Mødevagten spørger Windows' egen liste over, hvem der bruger mikrofonen.
/// Målt 10-09-2026 under et opkald gennem Telefonlink: Telefonlink stod der
/// ikke — hverken under opkaldet eller nogensinde før. Vagten så ingenting,
/// og der kom ingen besked.
///
/// ============ DET ER WINDOWS SELV, DER FØRER SAMTALEN ============
///
/// Målt under et rigtigt opkald samme dag, hvert sekund:
///
///   før opkaldet     ingen sessioner fra Windows' lydtjeneste
///   13:51:41         lydtjenesten (Audiosrv) åbner Jabra-mikrofonen OG
///                    Jabra-højttaleren — og holder dem, så længe der tales
///
/// Telefonens egne Bluetooth-lydenheder blev IKKE tilsluttet, som første
/// udgave regnede med. Lyden føres af lydtjenesten mellem telefonen og
/// standardkommunikationsenheden, og så er det lydtjenesten, der står som
/// den, der optager fra mikrofonen.
///
/// Et almindeligt program optager aldrig som lydtjenesten. Et Teams-møde
/// står som Teams, en diktering som sit eget program.
///
/// ============ OG KUN NÅR DER ER EN TELEFON ============
///
/// Som ekstra sikring kræves det, at en telefon er parret. En telefon kendes
/// på Bluetooth-tjenesten «Hands-Free Audio Gateway» (0x111F), som kun en
/// telefon udbyder — et headset udbyder «Hands-Free» (0x111E). Tjenesten
/// står i enhedens id i Windows, to led over lydenheden:
///
///   lydenhed       SWD\MMDEVAPI\{0.0.1.00000000}.{79F1…}
///   forælder       BTHHFENUM\BthHFPAudio\8&amp;2f8a…
///   bedsteforælder BTHENUM\{0000111f-0000-1000-8000-00805f9b34fb}_VID…
///
/// Der sammenlignes med tal og ikke med navne, som Windows kan oversætte.
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
            var lydtjeneste = Lydtjenestens_proces();
            if (lydtjeneste == 0) return false;

            using var e = new MMDeviceEnumerator();

            if (!Telefon_parret(e)) return false;

            foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (d)
                {
                    if (Optager_fra(d, lydtjeneste)) return true;
                }
            }
        }
        catch (Exception)
        {
            // Intet svar er et nej. Se ovenfor.
        }

        return false;
    }

    /// <summary>Holder lydtjenesten en aktiv session på den her mikrofon?</summary>
    private static bool Optager_fra(MMDevice d, uint lydtjeneste)
    {
        var sessioner = d.AudioSessionManager.Sessions;

        for (var i = 0; i < sessioner.Count; i++)
        {
            var s = sessioner[i];

            if (s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateActive
                && s.GetProcessID == lydtjeneste)
                return true;
        }

        return false;
    }

    /// <summary>Er der en telefon parret med computeren?</summary>
    /// <remarks>
    /// Alle lydenheder tælles med, også dem, der ikke er tilsluttet: målt
    /// står telefonens lydenheder som ikke tilsluttet, også midt i et opkald.
    /// </remarks>
    private static bool Telefon_parret(MMDeviceEnumerator e)
    {
        foreach (var d in e.EnumerateAudioEndPoints(DataFlow.All, DeviceState.All))
        {
            using (d)
            {
                try
                {
                    if (ErTelefon(d.ID)) return true;
                }
                catch (Exception)
                {
                    // En enhed, der er forsvundet undervejs. Videre.
                }
            }
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

    /// <summary>
    /// Processen, Windows' lydtjeneste kører i. 0, hvis den ikke kan findes.
    /// </summary>
    /// <remarks>
    /// Spørges hver gang og huskes ikke: tjenesten kan genstartes og får så
    /// en ny proces. Opslaget er et enkelt kald til tjenestestyringen.
    /// </remarks>
    private static uint Lydtjenestens_proces()
    {
        var styring = OpenSCManagerW(null, null, ScConnect);
        if (styring == IntPtr.Zero) return 0;

        try
        {
            var tjeneste = OpenServiceW(styring, "Audiosrv", ServiceQueryStatus);
            if (tjeneste == IntPtr.Zero) return 0;

            try
            {
                var status = new ServiceStatusProcess();

                return QueryServiceStatusEx(tjeneste, 0, ref status,
                                            Marshal.SizeOf<ServiceStatusProcess>(), out _)
                    ? status.ProcessId
                    : 0;
            }
            finally
            {
                CloseServiceHandle(tjeneste);
            }
        }
        finally
        {
            CloseServiceHandle(styring);
        }
    }

    private const uint ScConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode,
                    ServiceSpecificExitCode, CheckPoint, WaitHint, ProcessId, ServiceFlags;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenServiceW(IntPtr scm, string name, uint access);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int level,
        ref ServiceStatusProcess status, int size, out int needed);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devinst, string id, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint parent, uint devinst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_IDW(uint devinst, char[] buffer, int len, uint flags);
}
