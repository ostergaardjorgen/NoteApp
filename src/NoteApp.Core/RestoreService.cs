using System.IO.Compression;
using System.Text;

namespace NoteApp.Core;

public sealed record ArchiveContents(
    string Path,
    int Files,
    long Bytes,
    bool HasDictionary,
    bool HasSettings,
    int Recordings,
    int AudioFiles,
    DateTimeOffset Created)
{
    public double MegaBytes => Bytes / 1024.0 / 1024.0;

    /// <summary>
    /// Et arkiv uden ordbog er ikke ubrugeligt, men det er heller ikke en hel
    /// gendannelse. Brugeren skal vide det, før der overskrives.
    /// </summary>
    public bool LooksComplete => HasDictionary;

    public string Summary
    {
        get
        {
            var dele = new List<string>();
            if (HasDictionary) dele.Add("ordbog");
            if (HasSettings) dele.Add("indstillinger");
            if (Recordings > 0) dele.Add($"{Recordings} optagelser");
            dele.Add(AudioFiles > 0 ? $"{AudioFiles} lydfiler" : "ingen lyd");
            return string.Join(" · ", dele);
        }
    }
}

/// <summary>
/// <paramref name="SafetyArchive"/> er null, når der ikke var noget at sikre
/// — datamappen var tom. Det skal kunne skelnes fra "der blev taget en kopi":
/// et løfte om en fortrydelsesmulighed, der ikke findes, opdages først den dag
/// man vil bruge den.
/// </summary>
public sealed record RestoreResult(int FilesWritten, string? SafetyArchive);

/// <summary>
/// Gendannelse fra en sikkerhedskopi.
///
/// En backup, man aldrig har gendannet, er en formodning — ikke en backup.
/// Derfor kan arkivet undersøges FØR der røres ved noget: hvad indeholder det,
/// er ordbogen med, er lyden med. Og derfor tages der automatisk et
/// sikkerhedsarkiv af det nuværende, inden noget overskrives. Den dag man
/// gendanner, er sjældent en dag, hvor man har overskud til at opdage, at man
/// gendannede det forkerte arkiv.
/// </summary>
public static class RestoreService
{
    public static ArchiveContents Inspect(string archivePath)
    {
        using var zip = ZipFile.OpenRead(archivePath);

        var filer = 0;
        long bytes = 0;
        var ordbog = false;
        var indstillinger = false;
        var lyd = 0;
        var optagelser = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in zip.Entries)
        {
            if (e.FullName.EndsWith('/')) continue;

            filer++;
            bytes += e.Length;

            var navn = e.FullName.Replace('\\', '/');

            if (navn.Equals("learning.db", StringComparison.OrdinalIgnoreCase)) ordbog = true;
            if (navn.Equals("indstillinger.json", StringComparison.OrdinalIgnoreCase)) indstillinger = true;
            if (BackupService.IsAudio(navn)) lyd++;

            var dele = navn.Split('/');
            if (dele.Length >= 2 && dele[0].Equals("Optagelser", StringComparison.OrdinalIgnoreCase))
                optagelser.Add(dele[1]);
        }

        return new ArchiveContents(archivePath, filer, bytes, ordbog, indstillinger,
                                   optagelser.Count, lyd, new FileInfo(archivePath).LastWriteTime);
    }

    /// <summary>
    /// Prøvekørsel: pakker ud i en midlertidig mappe og kontrollerer, at
    /// ordbogen er en gyldig SQLite-fil. Rører ikke dine nuværende data.
    ///
    /// Prøv den mindst én gang, før du får brug for den. Det er hele forskellen
    /// på at have en backup og at tro, man har en.
    /// </summary>
    public static string TestRestore(string archivePath)
    {
        var midlertidig = Path.Combine(Path.GetTempPath(), "NoteApp-proeve-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(midlertidig);

        ZipFile.ExtractToDirectory(archivePath, midlertidig, overwriteFiles: true);

        var db = Path.Combine(midlertidig, "learning.db");
        if (File.Exists(db))
        {
            // SQLite-filer starter med "SQLite format 3\0". Er de hovedet ikke
            // der, er filen ikke en database — og et arkiv, der ikke kan
            // gendanne ordbogen, skal fejle her og ikke den dag, det bruges.
            using var fs = File.OpenRead(db);
            var hoved = new byte[16];
            var læst = fs.Read(hoved, 0, 16);
            var tekst = Encoding.ASCII.GetString(hoved, 0, Math.Max(0, læst - 1));

            if (!tekst.StartsWith("SQLite format 3", StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Ordbogen i arkivet er ikke en gyldig SQLite-fil. Arkivet kan være beskadiget.");
        }

        return midlertidig;
    }

    /// <summary>
    /// Gendanner for alvor. Der tages ALTID et sikkerhedsarkiv af det
    /// nuværende først — også når brugeren er sikker. Fortrydelse skal være
    /// mulig, og det er den kun, hvis kopien blev taget før, ikke efter.
    /// </summary>
    public static RestoreResult Restore(string archivePath, bool replaceEverything = false)
    {
        var indhold = Inspect(archivePath);
        if (indhold.Files == 0) throw new InvalidOperationException("Arkivet er tomt.");

        // Sikkerhedsarkivet lander ved siden af det, der gendannes fra, saa
        // det ikke ryger med i en rotation af de almindelige backups.
        var sikkerhedsmappe = Path.Combine(Path.GetDirectoryName(archivePath)!, "foer-gendannelse");
        Directory.CreateDirectory(sikkerhedsmappe);

        var sti = Path.Combine(sikkerhedsmappe,
            $"foer-gendannelse_{DateTime.Now:yyyy-MM-dd_HHmm}.zip");

        // Kun hvis der FAKTISK er noget at sikre. Ellers rapporteres null, og
        // brugeren faar ikke at vide, at der findes en fortrydelsesmulighed,
        // som ikke er der.
        string? sikkerhed = null;
        if (Directory.Exists(UserDataPaths.Root) &&
            Directory.EnumerateFileSystemEntries(UserDataPaths.Root).Any())
        {
            if (File.Exists(sti)) File.Delete(sti);
            ZipFile.CreateFromDirectory(UserDataPaths.Root, sti, CompressionLevel.Optimal, false);
            sikkerhed = sti;
        }

        // Ryddes der, sker det FOERST efter sikkerhedsarkivet er skrevet.
        if (replaceEverything && Directory.Exists(UserDataPaths.Root))
        {
            foreach (var f in Directory.GetFiles(UserDataPaths.Root, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(f); } catch (IOException) { }
            }
        }

        UserDataPaths.EnsureCreated();

        var skrevet = 0;
        using (var zip = ZipFile.OpenRead(archivePath))
        {
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith('/')) continue;

                var mål = Path.Combine(UserDataPaths.Root, e.FullName.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(mål)!);

                try
                {
                    e.ExtractToFile(mål, overwrite: true);
                    skrevet++;
                }
                catch (IOException)
                {
                    // Filen er i brug — typisk learning.db, hvis appen har den
                    // aaben. Resten gendannes, og brugeren faar besked om at
                    // genstarte. Halvt gendannet slaar slet ikke gendannet.
                }
            }
        }

        BackupService.Log($"GENDANNET fra {Path.GetFileName(archivePath)}: {skrevet} filer. " +
                          $"Sikkerhedsarkiv: {(sikkerhed is null ? "ingen — datamappen var tom" : Path.GetFileName(sikkerhed))}");

        return new RestoreResult(skrevet, sikkerhed);
    }
}
