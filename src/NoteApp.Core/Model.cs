using System.Text.Json.Serialization;

namespace NoteApp.Core;

/// <summary>
/// Mødetypen styrer hvilke spor der optages, hvordan de flettes i fase 2, og
/// hvilken kontekstblok eksporten får med. Der er bevidst ingen standardværdi:
/// vælges der forkert på et onlinemøde, opdages det først når mødet er slut og
/// loopback-sporet er tomt.
/// </summary>
public enum MeetingType
{
    Physical,
    Online
}

public enum TrackKind
{
    Microphone,
    Loopback
}

/// <summary>
/// Metadata for ét møde. Skrives til meeting.json ved start og opdateres ved
/// stop, så en afbrudt optagelse stadig efterlader noget læsbart.
/// </summary>
public sealed class MeetingMetadata
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MeetingType Type { get; set; }

    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public string? Title { get; set; }

    public string MicDeviceName { get; init; } = "";

    /// <summary>Null ved fysiske møder — det er også flaget fase 2 læser for at springe fletningen over.</summary>
    public string? LoopbackDeviceName { get; set; }

    public int SampleRate { get; init; } = AudioFormat.SampleRate;
    public int Channels { get; init; } = 1;

    /// <summary>Sat af genopretningen når mødet blev samlet fra efterladte segmenter.</summary>
    public bool RecoveredAfterCrash { get; set; }

    /// <summary>
    /// Sproget mødet blev holdt på, som Whisper fandt det. Null indtil mødet
    /// er transskriberet.
    ///
    /// Gemmes, fordi skabelonerne skal kunne forholde sig til sproget bagefter
    /// — et referat af et engelsk møde skal skrives på dansk, og det kan kun
    /// lade sig gøre, hvis det står et sted, at mødet var engelsk.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Hvor sikker sprogdetekteringen var (0-1). Null når sproget var valgt
    /// på forhånd. Dansk, norsk og svensk ligner hinanden nok til, at tallet
    /// er værd at kunne slå op, når et referat ser forkert ud.
    /// </summary>
    public double? LanguageProbability { get; set; }

    public Dictionary<string, string> Tracks { get; init; } = new();
}

/// <summary>
/// En note skrevet under mødet, eller et bogmærke sat med genvejstasten.
/// Tidsstemplet er relativt til optagelsens start, så noten kan flettes ind i
/// transskriptionen det rigtige sted.
/// </summary>
public sealed class MeetingNote
{
    public double AtSeconds { get; init; }
    public DateTimeOffset WallClock { get; init; }

    /// <summary>Tom ved et rent bogmærke fra genvejstasten.</summary>
    public string Text { get; init; } = "";

    public bool IsMarker { get; init; }

    public string Timecode => TimeSpan.FromSeconds(AtSeconds).ToString(@"hh\:mm\:ss");
}

public static class AudioFormat
{
    /// <summary>whisper.cpp kræver 16 kHz mono. Alt konverteres undervejs, ikke bagefter.</summary>
    public const int SampleRate = 16_000;

    public const int Channels = 1;
    public const int BitsPerSample = 16;
    public const int BytesPerSecond = SampleRate * Channels * (BitsPerSample / 8);

    /// <summary>
    /// Længden på hvert autosave-segment. Crasher appen eller løber batteriet
    /// tør, mister du højst dette — ikke hele mødet. Det er ikke en feature,
    /// det er forudsætningen for at turde bruge værktøjet til noget.
    /// </summary>
    public static readonly TimeSpan ChunkDuration = TimeSpan.FromSeconds(30);
}
