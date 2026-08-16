using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

public enum HeavyJobKind
{
    /// <summary>Whisper skriver en optagelse ud.</summary>
    Transskription,

    /// <summary>En sprogmodel laver et udkast ud fra en skabelon.</summary>
    Skabelon
}

public sealed record HeavyJobInfo(HeavyJobKind Kind, string What, DateTimeOffset Started, TimeSpan? Estimate)
{
    public TimeSpan Elapsed => DateTimeOffset.Now - Started;

    public TimeSpan? Remaining => Estimate is null
        ? null
        : (Estimate.Value - Elapsed) is { Ticks: > 0 } r ? r : TimeSpan.Zero;

    public string Beskrivelse => Kind == HeavyJobKind.Transskription
        ? $"En transskription kører ({What})"
        : $"Et udkast er i gang ({What})";
}

/// <summary>
/// Kun én tung opgave ad gangen.
///
/// Grunden er målt, ikke antaget: Whispers large-v3 fylder 3.094 MB på
/// grafikkortet, og en 8B sprogmodel omkring 4.700 MB. På et 6 GB-kort kan de
/// ikke være der samtidig — den ene ville falde tilbage på CPU og blive ti
/// gange langsommere, eller løbe tør for hukommelse midt i en kørsel.
///
/// OPTAGELSE ER IKKE OMFATTET. Den bruger 31 KB/sek disk, én tråd og ingen
/// GPU. Man kan roligt optage et møde, mens der laves et referat af et andet,
/// og appen skal ikke advare om noget, der ikke er et problem.
///
/// Låsen er navngivet på tværs af processer, fordi kommandolinjeværktøjet og
/// appen kan køre samtidig — og de deler det samme grafikkort.
/// </summary>
/// <summary>
/// Hvor meget arbejdshukommelse maskinen har fri, og hvad det betyder for en
/// model, der vil fylde et bestemt antal byte.
///
/// Låsen ovenfor beskytter grafikkortet. Den beskytter ikke mod DEN her:
/// en model, der ikke kan være på kortet, lander i almindelig RAM, og løber
/// den maskinen tør, begynder Windows at swappe. Så holder alt op med at
/// svare — også vinduer, der intet har med sagen at gøre.
///
/// Målt 12. august 2026: en 12,4 GB model stod på 18,9 GB RAM og efterlod
/// 3,6 GB af 32,5. Appen holdt op med at reagere og måtte lukkes hårdt. Der
/// gik ingen data tabt, men maskinen var ubrugelig imens.
/// </summary>
public static class Arbejdshukommelse
{
    /// <summary>Fri fysisk hukommelse i byte. 0 hvis det ikke kan aflæses.</summary>
    public static long Fri()
    {
        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref status) ? (long)status.ullAvailPhys : 0;
        }
        catch (Exception) { return 0; }
    }

    /// <summary>
    /// Er der plads til en model på <paramref name="modelBytes"/>, når den
    /// ikke kan ligge på grafikkortet?
    ///
    /// Tillægget på 40 % er ikke et sikkerhedsskøn taget ud af luften: de
    /// målte kørsler brugte 14,1 GB RAM til en 14,3 GB-model, der lå delvist
    /// på kortet, og 18,9 GB til en 12,4 GB-model, der ikke gjorde. Konteksten,
    /// tokenbufferne og selve programmet kommer oven i vægtene.
    ///
    /// Der skal desuden blive 2 GB tilbage til Windows og til appen selv.
    /// Uden den margen swapper maskinen, og så er det ikke kun modellen, der
    /// bliver langsom.
    /// </summary>
    public static (bool Plads, long KrævetBytes, long FriBytes) HarPlads(long modelBytes)
    {
        // HELE MODELLEN TÆLLER MED — OGSÅ DEN DEL, DER LIGGER PÅ KORTET.
        //
        // Der blev 16. august 2026 lavet en «forbedring» her: de lag, der
        // lægges på grafikkortet, blev trukket fra kravet, fordi de jo ikke
        // ligger i RAM. Det lyder rigtigt og er forkert.
        //
        // llama.cpp hukommelses-mapper modelfilen. Windows holder den i sin
        // filcache, mens den læses, og de sider tæller med i den fysiske
        // hukommelse — uanset hvor mange lag der bagefter er kopieret over på
        // kortet. Målt umiddelbart efter ændringen: en 13,3 GB-model blev
        // sluppet igennem med 16,2 GB fri, og fem sekunder senere var der
        // 0,4 GB tilbage af 31,7. Maskinen var på vej i knæ og måtte stoppes
        // hårdt.
        //
        // Tillægget på 40 % er ikke taget ud af luften: de målte kørsler brugte
        // 14,1 GB RAM til en 14,3 GB-model, der lå delvist på kortet, og
        // 18,9 GB til en 12,4 GB-model, der ikke gjorde. Konteksten,
        // tokenbufferne og selve programmet kommer oven i vægtene.
        //
        // Der skal desuden blive 2 GB tilbage til Windows og til appen selv.
        // Uden den margen swapper maskinen, og så er det ikke kun modellen,
        // der bliver langsom.
        var krævet = (long)(modelBytes * 1.4) + 2L * 1024 * 1024 * 1024;
        var fri = Fri();

        // Kan hukommelsen ikke aflaeses, blokeres der ikke. En advarsel, der
        // bygger paa et tal, vi ikke har, er vaerre end ingen advarsel.
        return (fri == 0 || fri >= krævet, krævet, fri);
    }

    public static string Gigabyte(long bytes) => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

public sealed class HeavyJobLock : IDisposable
{
    private const string MutexNavn = @"Local\NoteApp.TungOpgave";
    private static readonly string StatusFil = Path.Combine(UserDataPaths.Root, "log", "tung-opgave.json");

    private readonly Mutex _mutex;
    private bool _holder;

    private HeavyJobLock(Mutex mutex, bool holder)
    {
        _mutex = mutex;
        _holder = holder;
    }

    /// <summary>
    /// Forsøger at tage låsen. Lykkes det ikke, fortæller <paramref name="optaget"/>
    /// hvad der kører, og hvor lang tid der cirka er igen — så kalderen kan
    /// sige noget brugbart frem for bare "prøv igen".
    /// </summary>
    public static bool TryAcquire(HeavyJobKind kind, string what, TimeSpan? estimate,
                                  out HeavyJobLock? laas, out HeavyJobInfo? optaget)
    {
        var m = new Mutex(false, MutexNavn);

        bool fik;
        try { fik = m.WaitOne(TimeSpan.Zero); }
        catch (AbandonedMutexException)
        {
            // Den forrige proces doede uden at slippe laasen. Saa er den fri.
            fik = true;
        }

        if (!fik)
        {
            laas = null;
            optaget = LaesStatus();
            m.Dispose();
            return false;
        }

        SkrivStatus(new HeavyJobInfo(kind, what, DateTimeOffset.Now, estimate));
        laas = new HeavyJobLock(m, true);
        optaget = null;
        return true;
    }

    /// <summary>Hvad kører der lige nu? Null hvis der ikke kører noget tungt.</summary>
    public static HeavyJobInfo? Current()
    {
        var m = new Mutex(false, MutexNavn);
        try
        {
            var fri = m.WaitOne(TimeSpan.Zero);
            if (fri) { m.ReleaseMutex(); return null; }
            return LaesStatus();
        }
        catch (AbandonedMutexException) { return null; }
        finally { m.Dispose(); }
    }

    private static void SkrivStatus(HeavyJobInfo info)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatusFil)!);
            File.WriteAllText(StatusFil, JsonSerializer.Serialize(info), new UTF8Encoding(false));
        }
        catch (IOException) { }
    }

    private static HeavyJobInfo? LaesStatus()
    {
        try
        {
            if (!File.Exists(StatusFil)) return null;
            return JsonSerializer.Deserialize<HeavyJobInfo>(File.ReadAllText(StatusFil, Encoding.UTF8));
        }
        catch (Exception) { return null; }
    }

    public void Dispose()
    {
        if (!_holder) return;
        _holder = false;

        try { File.Delete(StatusFil); } catch (IOException) { }
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex.Dispose();
    }
}

/// <summary>
/// Målt hastighed pr. model, så et estimat er noget, der er set før — ikke et
/// gæt. Første gang en model bruges, findes der ikke et tal, og så skal appen
/// sige det frem for at finde på et.
/// </summary>
public static class JobSpeeds
{
    private static string Fil => Path.Combine(UserDataPaths.Root, "hastigheder.json");

    private static Dictionary<string, double> Indlaes()
    {
        try
        {
            if (File.Exists(Fil))
                return JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(Fil, Encoding.UTF8))
                       ?? new Dictionary<string, double>();
        }
        catch (Exception) { }
        return new Dictionary<string, double>();
    }

    /// <summary>Gemmer tokens pr. sekund for en model. Et glidende gennemsnit, så én langsom kørsel ikke vælter estimatet.</summary>
    public static void Record(string model, double tokensPerSecond)
    {
        if (tokensPerSecond <= 0) return;

        var d = Indlaes();
        d[model] = d.TryGetValue(model, out var gammel) ? (gammel * 2 + tokensPerSecond) / 3 : tokensPerSecond;

        try
        {
            Directory.CreateDirectory(UserDataPaths.Root);
            File.WriteAllText(Fil, JsonSerializer.Serialize(d), new UTF8Encoding(false));
        }
        catch (IOException) { }
    }

    /// <summary>Skøn over hvor lang tid en kørsel tager. Null hvis modellen aldrig er brugt før.</summary>
    public static TimeSpan? Estimate(string model, int expectedTokens)
    {
        var d = Indlaes();
        if (!d.TryGetValue(model, out var tps) || tps <= 0) return null;

        // Modelindlaesning taeller med: 4,7 GB fra disk tager tid, og det er
        // en fast omkostning pr. koersel, naar modellen ikke bliver liggende.
        return TimeSpan.FromSeconds(expectedTokens / tps + 15);
    }
}
