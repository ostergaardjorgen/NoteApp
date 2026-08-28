using System.Text;

namespace NoteApp.Core;

/// <summary>
/// Ét sted der afgør hvor DINE data ligger — og dermed hvad der aldrig kan
/// havne i et git-repo.
///
/// Grænsen er fysisk, ikke en regel man skal huske: koden ligger i et
/// git-checkout, dine data ligger i en helt anden mappe. De to overlapper
/// ikke. Der findes ingen .gitignore-fejl der kan lække en optagelse, en note
/// eller en indlært rettelse, fordi filerne ikke er inde i arbejdstræet til
/// at begynde med.
///
/// Placeringen bestemmes i denne rækkefølge:
///   1. Miljøvariablen HEYPIA_DATA — vinder altid, til test og særtilfælde.
///      NOTEAPP_DATA virker fortsat; appen skiftede navn 28-08-2026.
///   2. Pegefilen i %APPDATA%\HeyPia\datasti.txt — sat af appen, når
///      brugeren har valgt en anden mappe. Den gamle i \NoteApp\ læses også.
///   3. Standarden C:\AppNoter.
///
/// Pegefilen ligger med vilje UDEN FOR datamappen. Lå valget inde i den mappe,
/// det selv udpeger, kunne appen ikke finde det igen efter en flytning.
/// </summary>
public static class UserDataPaths
{
    /// <summary>
    /// Miljøvariablen, der tilsidesætter alt. Bruges af prøver og målinger.
    /// </summary>
    /// <remarks>
    /// BEGGE NAVNE VIRKER. Appen skiftede navn 28-08-2026, og HEYPIA_DATA er
    /// det rigtige nu — men NOTEAPP_DATA står i prøvescripts og i vaner, og en
    /// variabel, der stille holdt op med at virke, ville få en prøvekørsel til
    /// at ramme de RIGTIGE data i stedet for sandkassen. Det er ikke en fejl,
    /// man opdager før bagefter.
    /// </remarks>
    public const string OverrideVariable = "HEYPIA_DATA";

    /// <summary>Det gamle navn. Virker fortsat — se <see cref="OverrideVariable"/>.</summary>
    public const string GammelOverrideVariable = "NOTEAPP_DATA";

    /// <summary>Standardplaceringen for en frisk installation.</summary>
    public static readonly string DefaultRoot = Path.Combine("C:", "AppNoter");

    /// <summary>
    /// Hvor det tidligere lå. Bruges kun til at tilbyde en flytning.
    /// </summary>
    /// <remarks>
    /// DEN SKAL BLIVE VED AT PEGE PÅ FORTIDEN. Et navneskift-søgeerstat døbte
    /// den om til «HeyPia» 28-08-2026, og det er noget nær det modsatte af,
    /// hvad den er til: den findes for at kunne finde data, der ligger, hvor
    /// de lå FØR.
    /// </remarks>
    public static string LegacyRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoteApp");

    /// <summary>
    /// Hvor valget af datamappe står. Der læses fra begge navne, og skrives
    /// til det nye.
    /// </summary>
    /// <remarks>
    /// FILEN FINDES PÅ MASKINER, DER ER I BRUG. Den blev skrevet, da appen hed
    /// NoteApp, og den fortæller, hvor brugerens optagelser ligger. Ledte
    /// appen kun det nye sted, ville den falde tilbage på standarden — og for
    /// den, der HAR flyttet sin datamappe, ville appen se tom ud.
    /// </remarks>
    private static string PointerFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeyPia", "datasti.txt");

    private static string GammelPointerFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoteApp", "datasti.txt");

    private static string? _cached;

    /// <summary>Roden af alt, der er dit. Backup tager præcis denne mappe.</summary>
    public static string Root
    {
        get
        {
            var tilsidesat = Environment.GetEnvironmentVariable(OverrideVariable);
            if (string.IsNullOrWhiteSpace(tilsidesat))
                tilsidesat = Environment.GetEnvironmentVariable(GammelOverrideVariable);
            if (!string.IsNullOrWhiteSpace(tilsidesat)) return Path.GetFullPath(tilsidesat);

            if (_cached is not null) return _cached;

            try
            {
                // Det nye sted foerst, det gamle bagefter. Se PointerFile.
                foreach (var fil in new[] { PointerFile, GammelPointerFile })
                {
                    if (!File.Exists(fil)) continue;

                    var valgt = File.ReadAllText(fil, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(valgt)) return _cached = Path.GetFullPath(valgt);
                }
            }
            catch (IOException)
            {
                // En ulæselig pegefil må ikke forhindre appen i at starte.
                // Standarden er altid brugbar.
            }

            return _cached = DefaultRoot;
        }
    }

    /// <summary>
    /// Flytter datamappen til et nyt sted. Filerne kopieres FØRST og slettes
    /// bagefter: en afbrudt flytning skal efterlade data ét sted, ikke ingen.
    /// </summary>
    public static void SetRoot(string newRoot, bool moveExistingFiles = true)
    {
        newRoot = Path.GetFullPath(newRoot);
        var gammel = Root;

        if (string.Equals(newRoot, gammel, StringComparison.OrdinalIgnoreCase)) return;

        Directory.CreateDirectory(newRoot);
        AssertOutsideRepository(AppContext.BaseDirectory, newRoot);

        if (moveExistingFiles && Directory.Exists(gammel))
        {
            CopyTree(gammel, newRoot);

            // Først når alt er kopieret, ryddes det gamle. Rækkefølgen er
            // hele forskellen på en flytning og et tab.
            try { Directory.Delete(gammel, recursive: true); }
            catch (IOException) { /* en åben fil — data er i behold i det nye. */ }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PointerFile)!);
        File.WriteAllText(PointerFile, newRoot, new UTF8Encoding(false));
        _cached = newRoot;

        EnsureCreated();
    }

    private static void CopyTree(string fra, string til)
    {
        foreach (var mappe in Directory.GetDirectories(fra, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(mappe.Replace(fra, til));

        foreach (var fil in Directory.GetFiles(fra, "*", SearchOption.AllDirectories))
            File.Copy(fil, fil.Replace(fra, til), overwrite: true);
    }

    /// <summary>Optagelser: lyd, noter, transskriptioner, metadata.</summary>
    public static string Meetings => Path.Combine(Root, "Optagelser");

    /// <summary>Den indlærte ordbog og rettelseshistorikken. Unik for din installation.</summary>
    public static string LearningDatabase => Path.Combine(Root, "learning.db");

    /// <summary>Ordlisten der sendes med til Whisper. Vokser med det, du lærer appen.</summary>
    public static string Vocabulary => Path.Combine(Root, "ordliste.txt");

    public static string Logs => Path.Combine(Root, "log");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Meetings);
        Directory.CreateDirectory(Logs);
    }

    /// <summary>
    /// Værn mod at en fremtidig ændring peger datamappen ind i repoet. Kaldes
    /// ved opstart og ved hver flytning. Fejler hellere højlydt end at lække
    /// stille.
    /// </summary>
    public static void AssertOutsideRepository(string repositoryRoot, string? candidate = null)
    {
        var data = Path.GetFullPath(candidate ?? Root).TrimEnd(Path.DirectorySeparatorChar);
        var repo = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar);

        if (data.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(data, repo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Datamappen ({data}) ligger inde i kodemappen ({repo}). " +
                "Dine data ville dermed kunne havne i git. Vælg en mappe uden for repoet.");
        }
    }
}
