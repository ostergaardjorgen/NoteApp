using System.IO;
using System.Text;
using System.Text.Json;
using NoteApp.Core;

namespace NoteApp.Desktop;

/// <summary>
/// Appens egne indstillinger. Ligger i datamappen sammen med resten af det,
/// der er brugerens — ikke i registreringsdatabasen og ikke ved siden af exe'en,
/// så backup af datamappen også tager indstillingerne med.
///
/// Der gemmes ingenting her, der kan identificere nogen. Valgt model og om
/// opsætningen er kørt, intet andet.
/// </summary>
public sealed class Settings
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string? PreferredModel { get; set; }

    public bool SetupCompleted { get; set; }

    public string? Industry { get; set; }

    public string? BackupDestination { get; set; }

    /// <summary>
    /// Lyd med i sikkerhedskopien. Falsk som standard: lyden er tusind gange
    /// større end alt det andet tilsammen, og den er også det, der er lettest
    /// at undvære — transskriptionen og de indlærte rettelser er arbejdet.
    /// </summary>
    public bool BackupIncludeAudio { get; set; }

    private static string Path => System.IO.Path.Combine(UserDataPaths.Root, "indstillinger.json");

    private static Settings? _current;

    public static Settings Current => _current ??= Load();

    private static Settings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path, Encoding.UTF8)) ?? new Settings();
        }
        catch (JsonException)
        {
            // En ødelagt indstillingsfil må ikke forhindre appen i at starte.
            // Standardværdier er altid brugbare.
        }
        return new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, Options), Encoding.UTF8);
    }
}
