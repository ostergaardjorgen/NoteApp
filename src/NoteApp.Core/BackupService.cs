using System.IO.Compression;
using System.Text;

namespace NoteApp.Core;

public sealed record BackupResult(string ArchivePath, int Files, long Bytes, TimeSpan Elapsed, int Kept)
{
    public double MegaBytes => Bytes / 1024.0 / 1024.0;
}

public sealed record BackupInfo(string Path, DateTimeOffset When, long Bytes)
{
    public double MegaBytes => Bytes / 1024.0 / 1024.0;
}

/// <summary>
/// Sikkerhedskopi af datamappen — inde i appen, ikke i et script ved siden af.
///
/// De tre ting, scriptet gjorde rigtigt, gør denne klasse også, fordi de er
/// forskellen på en backup og en formodning om en backup:
///   1. Arkivet åbnes efter oprettelsen. Et arkiv, der ikke kan læses, er
///      ingen backup, og det skal opdages nu — ikke den dag, man får brug for det.
///   2. Kilden skrives i loggen. En log, der ikke siger HVAD den sikrede, kan
///      ikke afsløre, at den sikrede den forkerte mappe.
///   3. Den nægter at køre, hvis datamappen hverken har ordbog eller
///      optagelser. Ellers får man en stribe grønne kvitteringer på en tom mappe.
/// </summary>
public static class BackupService
{
    /// <summary>
    /// Hvor sikkerhedskopierne lander — og hvor de gamle stadig findes.
    /// </summary>
    /// <remarks>
    /// APPEN HED NoteApp INDTIL 28-08-2026, og kopierne lå i «NoteApp-backup».
    /// Skiftede standarden bare, ville de gamle blive liggende et sted, ingen
    /// leder — og den, der en dag skulle gendanne, ville få at vide, at der
    /// ingen kopier var.
    ///
    /// Findes den gamle mappe, og den nye ikke, bruges den gamle fortsat. Der
    /// FLYTTES IKKE: en sikkerhedskopi er det sidste, man vil flytte
    /// automatisk, og den, der har peget en backup et bestemt sted hen, har
    /// gjort det med vilje.
    /// </remarks>
    public static string DefaultDestination
    {
        get
        {
            var hjem = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var ny = Path.Combine(hjem, "HeyPia-backup");
            var gammel = Path.Combine(hjem, "NoteApp-backup");

            return !Directory.Exists(ny) && Directory.Exists(gammel) ? gammel : ny;
        }
    }

    /// <summary>
    /// Sand hvis stien ligger uden for den fysiske maskine. Et drevbogstav
    /// siger intet: mappede shares ser ud som almindelige drev, så drevtypen
    /// afgør det — ikke stiens udseende.
    /// </summary>
    public static bool LeavesMachine(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;

        try
        {
            var rod = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrWhiteSpace(rod)) return false;
            return new DriveInfo(rod).DriveType == DriveType.Network;
        }
        catch
        {
            // Kan drevtypen ikke afgoeres, behandles stien som lokal. Et
            // utilgaengeligt drev fejler alligevel hoejlydt, naar der skrives.
            return false;
        }
    }

    /// <summary>Lokale drev, der kan foreslås som alternativ til en netværkssti.</summary>
    public static IEnumerable<string> LocalDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(d => d.Name.TrimEnd('\\'));

    /// <summary>Filtyper, der regnes som lyd. De fylder alt; resten fylder ingenting.</summary>
    private static readonly string[] AudioExtensions = { ".wav", ".mp3", ".m4a", ".flac", ".ogg" };

    public static bool IsAudio(string path) =>
        AudioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Hvad en backup ville fylde, med og uden lyd. Bruges til at vise
    /// forskellen FØR valget træffes — den er typisk tre størrelsesordener,
    /// og det er ikke til at gætte.
    /// </summary>
    public static (long WithAudio, long WithoutAudio, int AudioFiles) Estimate()
    {
        long alt = 0, udenLyd = 0;
        var lydFiler = 0;

        if (!Directory.Exists(UserDataPaths.Root)) return (0, 0, 0);

        foreach (var f in Directory.EnumerateFiles(UserDataPaths.Root, "*", SearchOption.AllDirectories))
        {
            long størrelse;
            try { størrelse = new FileInfo(f).Length; } catch (IOException) { continue; }

            if (ErMotorfil(f)) continue;

            alt += størrelse;
            if (IsAudio(f)) lydFiler++;
            else udenLyd += størrelse;
        }

        return (alt, udenLyd, lydFiler);
    }

    /// <summary>
    /// Tager sikkerhedskopien.
    ///
    /// <paramref name="includeAudio"/> er som standard FALSK. Lyden er det
    /// eneste, der fylder noget — en time optagelse er over 100 MB, mens
    /// ordbog, noter, transskriptioner og indstillinger tilsammen er få MB.
    /// Og lyden er også den del, der er lettest at undvære: transskriptionen
    /// og rettelserne er det, der er arbejde i. Tages lyden med hver uge,
    /// bliver backuppen så stor, at man holder op med at tage den.
    /// </summary>
    /// <summary>
    /// Hvor langt sikkerhedskopien er nået. <paramref name="Fil"/> er navnet på
    /// den, der pakkes lige nu — så en kørsel, der tager tre minutter, kan ses
    /// bevæge sig og ikke bare vare længe.
    /// </summary>
    public readonly record struct BackupFremdrift(int Gjort, int IAlt, string Fil)
    {
        public double Procent => IAlt <= 0 ? 0 : Gjort * 100.0 / IAlt;
    }

    public static BackupResult Run(string destination, int keep = 8, bool includeAudio = false,
                                   IProgress<BackupFremdrift>? fremdrift = null)
    {
        var kilde = UserDataPaths.Root;

        if (!Directory.Exists(kilde))
            throw new DirectoryNotFoundException($"Datamappen findes ikke: {kilde}");

        if (!Directory.Exists(UserDataPaths.Meetings) && !Directory.Exists(Documents.DocumentStore.Directory))
            throw new InvalidOperationException(
                $"Datamappen {kilde} indeholder hverken optagelser eller dokumenter. Det ligner den forkerte mappe.");

        Directory.CreateDirectory(destination);

        var filer = Directory.GetFiles(kilde, "*", SearchOption.AllDirectories)
            .Where(f => !ErMotorfil(f))
            .Where(f => includeAudio || !IsAudio(f))
            .ToArray();

        if (filer.Length == 0)
            throw new InvalidOperationException(
                includeAudio
                    ? $"Der er ingen filer at sikre i {kilde}"
                    : $"Der er kun lydfiler i {kilde}, og lyd er slået fra. Slå lyd til, eller kør appen først.");

        var arkiv = Path.Combine(destination, $"heypia-data_{DateTime.Now:yyyy-MM-dd_HHmm}.zip");

        // ARKIVET BYGGES UNDER ET ANDET NAVN OG DOEBES OM TIL SIDST.
        //
        // Existing() finder arkiver paa moenstret heypia-data_*.zip. Byggede
        // vi direkte paa det navn, ville en afbrudt koersel - appen lukket,
        // strommen vaek, disken fuld - efterlade en halv zip, der staar i
        // listen som en rigtig sikkerhedskopi. Man opdager det den dag, man
        // skal bruge den. Maalt 18-08-2026: en afbrudt koersel efterlod
        // heypia-data_2026-08-18_1547.zip paa 870 MB, som saa gyldig ud.
        //
        // .part matcher ikke moenstret, saa en halv fil er usynlig for baade
        // listen og rotationen.
        var undervejs = arkiv + ".part";
        var ur = System.Diagnostics.Stopwatch.StartNew();

        if (File.Exists(undervejs)) File.Delete(undervejs);

        // Arkivet bygges fil for fil frem for med CreateFromDirectory, netop
        // for at kunne udelade lyden. Stierne gemmes relativt til datamappen,
        // saa gendannelse lander samme sted, uanset hvor mappen ligger.
        using (var zip = ZipFile.Open(undervejs, ZipArchiveMode.Create))
        {
            var gjort = 0;

            // Der meldes FOER filen pakkes, ikke efter.
            //
            // Den fil, der tager tid, er den store - en optagelse paa hundrede
            // megabyte. Meldte vi bagefter, ville navnet paa den staa stille i
            // et halvt minut, mens taelleren allerede var gaaet videre, og saa
            // peger skaermen paa noget, der er faerdigt.
            foreach (var f in filer)
            {
                fremdrift?.Report(new BackupFremdrift(gjort, filer.Length, Path.GetFileName(f)));

                var relativ = Path.GetRelativePath(kilde, f).Replace('\\', '/');
                try { zip.CreateEntryFromFile(f, relativ, CompressionLevel.Optimal); }
                catch (IOException) { /* en fil i brug — resten skal stadig med. */ }

                gjort++;
            }

            fremdrift?.Report(new BackupFremdrift(filer.Length, filer.Length, ""));
        }
        ur.Stop();

        // Krav 1: aabn arkivet med det samme. Det sker MENS det hedder .part,
        // saa en fil, der ikke kan aabnes, aldrig naar at faa det rigtige navn.
        int iArkiv;
        try
        {
            using var zip = ZipFile.OpenRead(undervejs);
            iArkiv = zip.Entries.Count;
        }
        catch (Exception ex)
        {
            Ryd(undervejs);
            throw new InvalidOperationException($"Arkivet kunne ikke åbnes efter oprettelse: {ex.Message}");
        }

        if (iArkiv < filer.Length)
        {
            Ryd(undervejs);
            throw new InvalidOperationException(
                $"Arkivet indeholder {iArkiv} poster, men der skulle have været {filer.Length}.");
        }

        // Foerst her bliver den til en sikkerhedskopi.
        if (File.Exists(arkiv)) File.Delete(arkiv);
        File.Move(undervejs, arkiv);

        Rotate(destination, keep);

        var bytes = new FileInfo(arkiv).Length;

        // Loggen skal sige, HVAD der blev sikret — ogsaa om lyden var med.
        // Ellers kan man ikke bagefter afgoere, om et arkiv er nok til at
        // gendanne et helt moede eller kun teksten.
        Log($"{filer.Length} filer  {bytes / 1024.0 / 1024.0:0.0} MB  " +
            $"{(includeAudio ? "MED lyd" : "uden lyd")}  fra {kilde}  -> {Path.GetFileName(arkiv)}");

        return new BackupResult(arkiv, filer.Length, bytes, ur.Elapsed, Existing(destination).Count());
    }

    /// <summary>
    /// Motoren og modellerne — whisper.cpp, llama.cpp og modelfilerne.
    ///
    /// DE SKAL IKKE MED I EN SIKKERHEDSKOPI.
    ///
    /// De ligger i datamappen, fordi de er brugerens installation og ikke
    /// kodens — men de er ikke brugerens DATA. De kan hentes igen med et
    /// klik, de er identiske på enhver maskine, og de fylder alt.
    ///
    /// Målt 18-08-2026 på en almindelig installation: motormappen var 6,9 GB
    /// af datamappens 7,1 GB. Sikkerhedskopien var altså 97 % filer, man kan
    /// hente igen — og den tog så lang tid, at Windows skrev «Svarer ikke»
    /// på vinduet, mens den kørte. En sikkerhedskopi, der ligner et nedbrud,
    /// bliver ikke taget.
    ///
    /// 4,7 GB af det var oven i købet sprogmodeller til den lokale vej, som
    /// blev fjernet fra appen 18-08-2026. Filer til en funktion, der ikke
    /// findes, blev sikret hver uge.
    /// </summary>
    private static bool ErMotorfil(string sti)
    {
        var relativ = Path.GetRelativePath(UserDataPaths.Root, sti)
            .Replace('\\', '/');

        return relativ.StartsWith("motor/", StringComparison.OrdinalIgnoreCase);
    }

    private static void Ryd(string sti)
    {
        try { if (File.Exists(sti)) File.Delete(sti); } catch (IOException) { }
    }

    private static void Rotate(string destination, int keep)
    {
        foreach (var gammel in Existing(destination).Skip(keep))
        {
            try { File.Delete(gammel.Path); } catch (IOException) { }
        }
    }

    public static IEnumerable<BackupInfo> Existing(string destination)
    {
        if (!Directory.Exists(destination)) yield break;

        var filer = Directory.GetFiles(destination, "heypia-data_*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime);

        foreach (var f in filer)
            yield return new BackupInfo(f.FullName, f.LastWriteTime, f.Length);
    }

    public static BackupInfo? Latest(string destination) => Existing(destination).FirstOrDefault();

    /// <summary>Krav 2: hvad blev sikret, og hvorfra. Må aldrig vælte en backup.</summary>
    public static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(UserDataPaths.Logs);
            File.AppendAllText(Path.Combine(UserDataPaths.Logs, "backup.log"),
                $"{DateTime.Now:s}  {text}{Environment.NewLine}", new UTF8Encoding(false));
        }
        catch (IOException) { }
    }
}
