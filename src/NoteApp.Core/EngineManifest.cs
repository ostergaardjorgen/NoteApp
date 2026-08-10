using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>
/// Hvad appen selv har installeret, og hvornår.
///
/// whisper.cpp stempler ikke sin exe: der er hverken en versionsressource i
/// binæren eller en versionslinje i dens output. Versionen kan derfor kun
/// kendes, hvis den skrives ned på det tidspunkt, den hentes. Gør vi ikke
/// det, kan appen aldrig svare på, om der findes en nyere — og så er
/// opdateringsknappen et gæt.
/// </summary>
public sealed record EngineManifest(string Version, string Url, DateTimeOffset InstalledAt, string? Sha256)
{
    public const string FileName = "motor.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string PathFor(string engineDirectory) => Path.Combine(engineDirectory, FileName);

    public void Save(string engineDirectory)
    {
        Directory.CreateDirectory(engineDirectory);
        File.WriteAllText(PathFor(engineDirectory), JsonSerializer.Serialize(this, Options), Encoding.UTF8);
    }

    /// <summary>
    /// Læser manifestet for den mappe, binæren ligger i. Findes det ikke, er
    /// motoren lagt der i hånden, og så VED vi ikke hvilken version det er.
    /// Null er det ærlige svar; et gæt ville gøre opdateringstjekket forkert.
    /// </summary>
    public static EngineManifest? Load(string whisperCliPath)
    {
        var mappe = Path.GetDirectoryName(whisperCliPath);
        while (mappe is not null)
        {
            var sti = Path.Combine(mappe, FileName);
            if (File.Exists(sti))
            {
                try { return JsonSerializer.Deserialize<EngineManifest>(File.ReadAllText(sti, Encoding.UTF8)); }
                catch (JsonException) { return null; }
            }
            mappe = Path.GetDirectoryName(mappe);
        }
        return null;
    }
}

public sealed record UpdateCheck(string? LatestVersion, string? InstalledVersion, string? DownloadUrl)
{
    /// <summary>
    /// Sand kun når begge versioner kendes OG de er forskellige. Er den
    /// installerede ukendt — motoren er lagt der i hånden — påstås der ikke
    /// noget: brugeren får tallet at se og træffer selv beslutningen.
    /// </summary>
    public bool UpdateAvailable =>
        LatestVersion is not null && InstalledVersion is not null &&
        !LatestVersion.TrimStart('v').Equals(InstalledVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase);

    public bool InstalledVersionUnknown => InstalledVersion is null;
}

/// <summary>
/// Slår op, om der er kommet en nyere whisper.cpp — kun når brugeren beder om det.
/// </summary>
public static class EngineUpdates
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest";

    /// <summary>Udgivelsens zip med Windows-binærer. {0} er versionen uden 'v'.</summary>
    public const string WindowsBuildUrlFormat =
        "https://github.com/ggerganov/whisper.cpp/releases/download/v{0}/whisper-bin-x64.zip";

    public static async Task<UpdateCheck> CheckAsync(string? installedVersion, CancellationToken ct = default)
    {
        var json = await new Downloader().FetchTextAsync(LatestReleaseUrl, ct);

        // Kun tag_name skal bruges. Der parses med et enkelt udtryk frem for
        // en model af hele GitHub-svaret: jo mindre af det svar vi binder os
        // til, jo mindre kan gaa i stykker, naar de aendrer noget andet.
        var m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"(?<tag>[^\"]+)\"");
        var seneste = m.Success ? m.Groups["tag"].Value : null;

        var url = seneste is null
            ? null
            : string.Format(WindowsBuildUrlFormat, seneste.TrimStart('v'));

        return new UpdateCheck(seneste, installedVersion, url);
    }
}
