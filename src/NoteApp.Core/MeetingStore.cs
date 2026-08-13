using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Læsning og skrivning af et mødes egne filer: meeting.json og notes.jsonl.
/// Alt ligger i din datamappe, aldrig i kode-repoet.
/// </summary>
public static class MeetingStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string CreateSessionDirectory(string? title, DateTimeOffset startedAt)
    {
        var mappe = Path.Combine(UserDataPaths.Meetings, Slug(title, startedAt));
        Directory.CreateDirectory(mappe);
        return mappe;
    }

    public static void Save(string sessionDir, MeetingMetadata meta)
    {
        var sti = Path.Combine(sessionDir, "meeting.json");
        File.WriteAllText(sti, JsonSerializer.Serialize(meta, Options), Encoding.UTF8);
    }

    public static MeetingMetadata? Load(string sessionDir)
    {
        var sti = Path.Combine(sessionDir, "meeting.json");
        if (!File.Exists(sti)) return null;

        try
        {
            return JsonSerializer.Deserialize<MeetingMetadata>(File.ReadAllText(sti, Encoding.UTF8));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static void MarkRecovered(string sessionDir, double seconds)
    {
        var meta = Load(sessionDir);
        if (meta is null) return;

        meta.RecoveredAfterCrash = true;
        meta.DurationSeconds = Math.Round(seconds, 1);
        meta.EndedAt ??= meta.StartedAt.AddSeconds(seconds);
        Save(sessionDir, meta);
    }

    /// <summary>
    /// Finder et møde på dets id.
    ///
    /// Id'et er den eneste holdbare forbindelse mellem et dokument og den
    /// optagelse, det er lavet af. Sti og titel kan begge ændre sig — det er
    /// sket, og begge gange rev det forbindelsen over. Derfor slås der op på
    /// id og ikke på navn.
    ///
    /// Der ledes i mapperne frem for i et register: et register kan komme ud
    /// af trit med det, der ligger på disken, og så peger det på noget, der
    /// ikke er der.
    /// </summary>
    public static (string Mappe, MeetingMetadata Meta)? FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Directory.Exists(UserDataPaths.Meetings)) return null;

        foreach (var mappe in Directory.EnumerateDirectories(UserDataPaths.Meetings))
        {
            var meta = Load(mappe);
            if (meta is not null && meta.Id.ToString() == id) return (mappe, meta);
        }

        return null;
    }

    public static string Slug(string? title, DateTimeOffset when)
    {
        var stempel = when.ToString("yyyy-MM-dd_HH-mm");
        if (string.IsNullOrWhiteSpace(title)) return stempel;

        var renset = new string(title.Select(c =>
            char.IsLetterOrDigit(c) || c is 'æ' or 'ø' or 'å' or 'Æ' or 'Ø' or 'Å' ? c : '-').ToArray());
        renset = string.Join('-', renset.Split('-', StringSplitOptions.RemoveEmptyEntries));

        if (renset.Length > 60) renset = renset[..60].TrimEnd('-');
        return string.IsNullOrEmpty(renset) ? stempel : $"{stempel}_{renset}";
    }
}

/// <summary>
/// Live-noter under mødet (feature 1) og bogmærker fra genvejstasten
/// (feature 2).
///
/// Skrives som append-only JSONL og flushes ved hver linje. En note er
/// værdiløs, hvis den forsvinder i det crash, den skulle have overlevet —
/// derfor ingen buffering, og derfor ét selvstændigt kald pr. note.
/// </summary>
public sealed class MeetingNotebook : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly List<MeetingNote> _notes = new();
    private readonly object _gate = new();
    private readonly Func<double> _elapsedSeconds;

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MeetingNotebook(string sessionDir, Func<double> elapsedSeconds)
    {
        _elapsedSeconds = elapsedSeconds;
        Path = System.IO.Path.Combine(sessionDir, "notes.jsonl");
        _writer = new StreamWriter(Path, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public string Path { get; }

    public IReadOnlyList<MeetingNote> Notes
    {
        get { lock (_gate) return _notes.ToList(); }
    }

    public MeetingNote Add(string text)
    {
        var note = new MeetingNote
        {
            AtSeconds = Math.Round(_elapsedSeconds(), 1),
            WallClock = DateTimeOffset.Now,
            Text = text.Trim(),
            IsMarker = false
        };
        Append(note);
        return note;
    }

    /// <summary>
    /// Bogmærke fra genvejstasten: bruges når nogen siger noget vigtigt, og
    /// du ikke kan nå at skrive. Ved eksport bliver markeringerne til
    /// overskrifter, du kan navigere efter.
    /// </summary>
    public MeetingNote AddMarker()
    {
        var note = new MeetingNote
        {
            AtSeconds = Math.Round(_elapsedSeconds(), 1),
            WallClock = DateTimeOffset.Now,
            IsMarker = true
        };
        Append(note);
        return note;
    }

    private void Append(MeetingNote note)
    {
        lock (_gate)
        {
            _notes.Add(note);
            _writer.WriteLine(JsonSerializer.Serialize(note, Compact));
        }
    }

    public static IReadOnlyList<MeetingNote> Read(string sessionDir)
    {
        var sti = System.IO.Path.Combine(sessionDir, "notes.jsonl");
        if (!File.Exists(sti)) return Array.Empty<MeetingNote>();

        var liste = new List<MeetingNote>();
        foreach (var linje in File.ReadLines(sti, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(linje)) continue;
            try
            {
                var n = JsonSerializer.Deserialize<MeetingNote>(linje);
                if (n is not null) liste.Add(n);
            }
            catch (JsonException)
            {
                // En halvskrevet sidste linje efter et crash. Resten står fast.
            }
        }
        return liste;
    }

    public void Dispose() => _writer.Dispose();
}
