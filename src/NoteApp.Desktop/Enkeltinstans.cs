using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Sørger for, at der kun kører én NoteApp ad gangen mod den samme datamappe.
///
/// HVORFOR DEN FINDES
///
/// To kopier af appen kunne startes samtidig — udviklingsudgaven og den
/// installerede — og de læser og skriver den SAMME datamappe. To processer,
/// der gemmer den samme fil, er en fejl, der ikke fortryder sig selv.
///
/// Det viste sig først på genvejstasten: den ene instans fik Ctrl+Shift+0,
/// den anden måtte tage Ctrl+Shift+1. Tasten på skærmen passede, men det var
/// ikke den, brugeren havde valgt — og hvilken af de to der optog, når man
/// trykkede, afhang af hvilken der var startet først.
///
/// LÅSEN FØLGER DATAMAPPEN, IKKE PROGRAMFILEN
///
/// Det er ikke to exe-filer, der er problemet — det er to skrivere på de
/// samme data. Låsens navn udledes derfor af <see cref="UserDataPaths.Root"/>.
/// To kopier, der peger på hver sin datamappe, må gerne køre samtidig; de
/// generer ikke hinanden. To kopier på samme mappe må ikke.
///
/// DEN MÅ ALDRIG FORHINDRE EN OPSTART
///
/// Et møde begynder om et øjeblik, og appen skal op. Går noget galt i selve
/// låsemekanismen — rettigheder, et navn systemet ikke vil have, hvad som
/// helst — starter appen som om spærren ikke fandtes. En spærre, der kan
/// koste en optagelse, er værre end det, den beskytter mod.
/// </summary>
internal static class Enkeltinstans
{
    /// <summary>Holdes i live så længe appen kører. Må ikke opsamles.</summary>
    private static Mutex? _laas;
    private static EventWaitHandle? _flag;

    /// <summary>
    /// Giver enhver proces lov til at tage forgrunden. Kaldes af den NYE
    /// instans, mens den stadig selv har retten — brugeren har jo lige
    /// dobbeltklikket. Uden det bliver den kørende app blot markeret i
    /// proceslinjen i stedet for at komme frem.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const int ASFW_ANY = -1;

    /// <summary>
    /// Sandt hvis dette er den første instans, og appen skal fortsætte.
    ///
    /// Falsk betyder, at en anden allerede kører mod samme datamappe. Så er
    /// den blevet bedt om at komme frem, og denne proces skal lukke uden at
    /// vise noget — brugeren skal se ét vindue, ikke en fejlbesked om, at
    /// programmet allerede kører.
    /// </summary>
    /// <param name="komFrem">
    /// Kaldes i den FØRSTE instans, når en ny bliver forsøgt startet. Kaldet
    /// sker på en baggrundstråd; modtageren skal selv skifte til UI-tråden.
    /// </param>
    public static bool ErFoerste(Action komFrem)
    {
        try
        {
            var navn = Navn();

            _laas = new Mutex(initiallyOwned: true, name: @"Local\NoteApp-" + navn, out var vandt);
            _flag = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\NoteApp-frem-" + navn);

            if (!vandt)
            {
                // En anden kører. Giv den lov til at tage forgrunden, bed den
                // komme frem, og forsvind stille.
                AllowSetForegroundWindow(ASFW_ANY);
                _flag.Set();
                return false;
            }

            // Vi er den første. Lyt efter, at nogen forsøger at starte nummer to.
            var lytter = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        _flag.WaitOne();
                        komFrem();
                    }
                    catch (Exception)
                    {
                        // En fejl her må ikke tage appen med sig. Værst tænkelige
                        // udfald er, at vinduet ikke kommer frem af sig selv.
                        return;
                    }
                }
            })
            { IsBackground = true, Name = "NoteApp enkeltinstans" };

            lytter.Start();
            return true;
        }
        catch (Exception)
        {
            // Se klassekommentaren: spærren må aldrig koste en opstart.
            return true;
        }
    }

    /// <summary>
    /// Et navn, der er entydigt for datamappen og gyldigt som objektnavn.
    ///
    /// Stien kan ikke bruges direkte — den indeholder backslashes, som er
    /// navnerumsskilletegn for kerneobjekter. Den hashes i stedet, og der
    /// sammenlignes på små bogstaver, så «C:\AppNoter» og «c:\appnoter» er
    /// den samme mappe. Det er de også for Windows.
    /// </summary>
    private static string Navn()
    {
        var sti = Path.TrimEndingDirectorySeparator(UserDataPaths.Root)
                      .ToLowerInvariant();

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sti)))[..16];
    }
}
