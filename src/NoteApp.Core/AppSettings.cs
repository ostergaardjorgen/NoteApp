using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Appens indstillinger. Ligger i datamappen sammen med resten af det, der er
/// brugerens — ikke i registreringsdatabasen og ikke ved siden af exe'en, så
/// en backup af datamappen også tager indstillingerne med.
///
/// Klassen ligger i Core og ikke i UI-projektet, fordi kommandolinjeværktøjet
/// skal læse præcis de samme valg. Lå den i WPF-projektet, ville 'noteapp
/// motor' vise en anden model end den, appen faktisk bruger — og det er den
/// slags uoverensstemmelse, man bruger en time på at forstå.
///
/// Der gemmes intet her, der kan identificere nogen: valgt model, valgte
/// lydenheders Windows-ID, backupmappe. Ikke andet.
/// </summary>
public sealed class AppSettings
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

    /// <summary>
    /// Valgte lydenheder, gemt som Windows-enheds-ID. Navnet duer ikke som
    /// nøgle: to headset af samme model hedder præcis det samme. Er null,
    /// bruges Windows' standard.
    /// </summary>
    public string? MicrophoneId { get; set; }

    public string? SpeakerId { get; set; }

    /// <summary>
    /// Skift afsnit automatisk under oplæsning. Kræver en lille model ved
    /// siden af den store — se LiveListener. Slået fra som standard: den
    /// koster GPU-tid, og en fejltolkning midt i en oplæsning er irriterende.
    /// </summary>
    public bool AutoAdvance { get; set; }

    // HER LAA LiveModel = "small".
    //
    // Feltet blev aldrig laest. Der er ingen live-lytning i appen, og der har
    // ikke vaeret det. Det stod og lignede en forklaring paa, hvorfor
    // ggml-small.bin laa i modelmappen - og den forklaring var forkert.
    // Fjernet 18-08-2026.

    /// <summary>
    /// Den genvejstast, der lynstarter en optagelse — gemt som id, ikke som
    /// tastekombination, så navnet kan skrives om uden at valget går tabt.
    ///
    /// Null betyder «tag den første ledige». Genvejstaster er optaget af vidt
    /// forskellige programmer fra maskine til maskine, og et fast valg, der
    /// ikke kan lade sig gøre, er ingen genvej.
    /// </summary>
    public string? HotkeyId { get; set; }

    /// <summary>
    /// Hvornår klokken sidst blev åbnet. Alt nyere end det er ulæst.
    ///
    /// Sættes kun, når man ÅBNER klokken — ikke ved opstart. En besked, man
    /// aldrig nåede at se, skal ikke forsvinde, fordi man genstartede appen.
    /// </summary>
    public DateTimeOffset NotifikationerSetTil { get; set; } = DateTimeOffset.MinValue;

    private static string Path => System.IO.Path.Combine(UserDataPaths.Root, "indstillinger.json");

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path, Encoding.UTF8)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // En ødelagt indstillingsfil må ikke forhindre appen i at starte.
            // Standardværdier er altid brugbare.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, Options), Encoding.UTF8);
    }

    /// <summary>Tvinger næste læsning til at gå på disken igen — fx efter en gendannelse.</summary>
    public static void Reload() => _current = null;
}
