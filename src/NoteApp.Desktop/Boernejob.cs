using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NoteApp.Desktop;

/// <summary>
/// Sørger for, at appens egne hjælpeprogrammer dør sammen med appen.
///
/// HVAD DER SKETE UDEN DEN
///
/// Vågeordet lytter med whisper-command.exe — en selvstændig proces, appen
/// starter. Lukkes appen pænt, lukkes den med. Men appen bliver IKKE altid
/// lukket pænt: udgivelsen slår en kørende app ihjel, og det gør Jobliste og
/// et strømsvigt også.
///
/// Så levede barnet videre. Det holdt sin model i hukommelsen og sit greb om
/// grafikkortet, og næste start lavede et nyt. Målt 29-08-2026: TRE
/// whisper-command samtidig, den ældste tretten timer gammel, tilsammen 2,4 GB
/// og 94 % af grafikkortet — mens vågeordet var slået FRA. Maskinen var
/// mærkbart langsom, og intet i appen viste hvorfor.
///
/// HVORDAN DEN VIRKER
///
/// Windows har en mekanisme til præcis det: et jobobjekt med
/// KILL_ON_JOB_CLOSE. Alle processer i jobbet slås ihjel, når det sidste
/// håndtag til jobbet lukkes — og et håndtag lukkes ALTID, når processen, der
/// holder det, forsvinder. Også når den bliver dræbt.
///
/// Det er derfor et jobobjekt og ikke en oprydning i koden: oprydning kræver,
/// at koden når at køre.
/// </summary>
public static class Boernejob
{
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr sikkerhed, string? navn);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr job, int slags, IntPtr oplysninger, uint laengde);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proces);

    private static IntPtr _job = IntPtr.Zero;
    private static readonly object Laas = new();

    /// <summary>
    /// Lægger en proces i appens job. Så dør den med appen — også hvis appen
    /// bliver dræbt.
    /// </summary>
    /// <remarks>
    /// Lykkes det ikke, køres der videre. En manglende oprydning er bedre end
    /// en funktion, der ikke starter — og opstår problemet igen, bliver det
    /// fanget af oprydningen ved næste start.
    /// </remarks>
    public static bool Tilfoej(Process proces)
    {
        try
        {
            lock (Laas)
            {
                if (_job == IntPtr.Zero)
                {
                    _job = CreateJobObject(IntPtr.Zero, null);
                    if (_job == IntPtr.Zero) return false;

                    var oplys = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                    oplys.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                    var stoerrelse = Marshal.SizeOf(oplys);
                    var peger = Marshal.AllocHGlobal(stoerrelse);
                    try
                    {
                        Marshal.StructureToPtr(oplys, peger, fDeleteOld: false);
                        if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation,
                                                     peger, (uint)stoerrelse))
                        {
                            return false;
                        }
                    }
                    finally { Marshal.FreeHGlobal(peger); }
                }

                return AssignProcessToJobObject(_job, proces.Handle);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Slår hjælpeprogrammer ihjel, der er blevet tilbage fra en tidligere
    /// kørsel. Returnerer, hvor mange der blev lukket.
    /// </summary>
    /// <remarks>
    /// Jobobjektet ovenfor forhindrer nye forældreløse. Den her rydder op
    /// efter dem, der allerede findes — fra en udgave uden jobbet, eller fra
    /// et nedbrud så hårdt, at Windows ikke nåede med.
    ///
    /// Der ryddes KUN op i det, der ligger i appens egen motormappe. Et
    /// program med samme navn et andet sted er ikke vores at lukke.
    ///
    /// Vores egne børn røres ikke: de hører til den app, der kører nu.
    /// </remarks>
    public static int RydForaeldreloese(string motormappe, params string[] navne)
    {
        var mappe = motormappe.TrimEnd('\\', '/') + '\\';
        var lukket = 0;

        foreach (var navn in navne)
        {
            Process[] fundne;
            try { fundne = Process.GetProcessesByName(navn); }
            catch (Exception) { continue; }

            foreach (var p in fundne)
            {
                try
                {
                    var sti = p.MainModule?.FileName;
                    if (sti is null) continue;
                    if (!sti.StartsWith(mappe, StringComparison.OrdinalIgnoreCase)) continue;

                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(3000);
                    lukket++;
                }
                catch (Exception)
                {
                    // Kan den ikke laeses eller lukkes, springes den over.
                    // En oprydning maa ikke kunne forhindre en opstart.
                }
                finally { p.Dispose(); }
            }
        }

        return lukket;
    }
}
