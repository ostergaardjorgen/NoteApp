namespace NoteApp.Core;

/// <summary>
/// Ét sted der afgør hvor DINE data ligger — og dermed hvad der aldrig kan
/// havne i et git-repo.
///
/// Grænsen er fysisk, ikke en regel man skal huske: koden ligger i
/// C:\NoteApp (et git-checkout), dine data ligger i %LOCALAPPDATA%\NoteApp.
/// De to mapper overlapper ikke. Der findes ingen .gitignore-fejl der kan
/// lække en optagelse, en note eller en indlært rettelse, fordi filerne
/// ikke er inde i arbejdstræet til at begynde med.
///
/// Vil du flytte data et andet sted hen — fx til en krypteret disk — så sæt
/// miljøvariablen NOTEAPP_DATA. Alt andet følger med af sig selv.
/// </summary>
public static class UserDataPaths
{
    public const string OverrideVariable = "NOTEAPP_DATA";

    /// <summary>Roden af alt, der er dit. Backup tager præcis denne mappe.</summary>
    public static string Root
    {
        get
        {
            var tilsidesat = Environment.GetEnvironmentVariable(OverrideVariable);
            if (!string.IsNullOrWhiteSpace(tilsidesat)) return Path.GetFullPath(tilsidesat);

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NoteApp");
        }
    }

    /// <summary>Møder: lyd, noter, transskriptioner, metadata.</summary>
    public static string Meetings => Path.Combine(Root, "moeder");

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
    /// ved opstart. Fejler hellere højlydt end at lække stille.
    /// </summary>
    public static void AssertOutsideRepository(string repositoryRoot)
    {
        var data = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar);
        var repo = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar);

        if (data.StartsWith(repo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(data, repo, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Datamappen ({data}) ligger inde i kodemappen ({repo}). " +
                "Dine data ville dermed kunne havne i git. Sæt NOTEAPP_DATA til en sti uden for repoet.");
        }
    }
}
