using NAudio.Wave;

namespace NoteApp.Core;

/// <summary>
/// Samler 30-sekunders segmenterne til én WAV.
///
/// Alle segmenter er i samme udgangsformat (16 kHz mono PCM16), også når de
/// blev optaget på forskellige enheder — konverteringen sker undervejs, ikke
/// bagefter. Derfor er sammensætningen ren kopiering af lyddata.
/// </summary>
public static class SegmentAssembler
{
    public static readonly WaveFormat Format =
        new(AudioFormat.SampleRate, AudioFormat.BitsPerSample, AudioFormat.Channels);

    /// <summary>Antal segmenter fundet, og hvor mange sekunder de udgør tilsammen.</summary>
    public sealed record Inventory(int SegmentCount, double Seconds, long Bytes, int Unreadable);

    public static Inventory Inspect(string segmentDir)
    {
        if (!Directory.Exists(segmentDir)) return new Inventory(0, 0, 0, 0);

        long bytes = 0;
        var tællesMed = 0;
        var ulæselige = 0;

        foreach (var fil in Segments(segmentDir))
        {
            try
            {
                using var r = new WaveFileReader(fil);
                bytes += r.Length;
                tællesMed++;
            }
            catch (Exception)
            {
                // Det sidste segment kan være halvskrevet, hvis strømmen gik.
                ulæselige++;
            }
        }

        return new Inventory(tællesMed, bytes / (double)AudioFormat.BytesPerSecond, bytes, ulæselige);
    }

    /// <summary>
    /// Skriver alle læsbare segmenter sammen til <paramref name="targetPath"/>.
    /// Et ulæseligt segment springes over frem for at vælte hele samlingen —
    /// at miste 30 sekunder er bedre end at miste mødet.
    /// </summary>
    public static Inventory Assemble(string segmentDir, string targetPath)
    {
        var segmenter = Segments(segmentDir).ToList();
        if (segmenter.Count == 0)
            throw new InvalidOperationException($"Ingen segmenter i {segmentDir}");

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        long bytes = 0;
        var skrevet = 0;
        var ulæselige = 0;
        var buffer = new byte[AudioFormat.BytesPerSecond];

        using (var writer = new WaveFileWriter(targetPath, Format))
        {
            foreach (var fil in segmenter)
            {
                try
                {
                    using var reader = new WaveFileReader(fil);
                    int læst;
                    while ((læst = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        writer.Write(buffer, 0, læst);
                        bytes += læst;
                    }
                    skrevet++;
                }
                catch (Exception)
                {
                    ulæselige++;
                }
            }
        }

        return new Inventory(skrevet, bytes / (double)AudioFormat.BytesPerSecond, bytes, ulæselige);
    }

    private static IEnumerable<string> Segments(string segmentDir) =>
        Directory.Exists(segmentDir)
            ? Directory.EnumerateFiles(segmentDir, "seg_*.wav").OrderBy(f => f, StringComparer.Ordinal)
            : Enumerable.Empty<string>();
}

public sealed record RecoverableSession(
    string SessionDir,
    string Title,
    DateTimeOffset When,
    IReadOnlyDictionary<string, SegmentAssembler.Inventory> Tracks)
{
    public double LongestTrackSeconds => Tracks.Count == 0 ? 0 : Tracks.Values.Max(t => t.Seconds);
}

/// <summary>
/// Finder møder, hvor der ligger segmenter tilbage — altså optagelser der
/// aldrig blev lukket ordentligt, fordi appen crashede eller maskinen døde.
///
/// Det er hele pointen med segmenteringen: uden den ville et crash efter 70
/// minutter koste 70 minutter. Med den koster det højst 30 sekunder, og
/// resten kan samles op her.
/// </summary>
public static class SessionRecovery
{
    public static IReadOnlyList<RecoverableSession> Scan(string? meetingsRoot = null)
    {
        var rod = meetingsRoot ?? UserDataPaths.Meetings;
        if (!Directory.Exists(rod)) return Array.Empty<RecoverableSession>();

        var fundne = new List<RecoverableSession>();

        foreach (var sessionDir in Directory.EnumerateDirectories(rod))
        {
            var segmentRod = Path.Combine(sessionDir, "segmenter");
            if (!Directory.Exists(segmentRod)) continue;

            var spor = new Dictionary<string, SegmentAssembler.Inventory>();
            foreach (var sporDir in Directory.EnumerateDirectories(segmentRod))
            {
                var opgørelse = SegmentAssembler.Inspect(sporDir);
                if (opgørelse.SegmentCount > 0)
                    spor[Path.GetFileName(sporDir)] = opgørelse;
            }

            if (spor.Count == 0) continue;

            fundne.Add(new RecoverableSession(
                sessionDir,
                Path.GetFileName(sessionDir),
                Directory.GetCreationTime(sessionDir),
                spor));
        }

        return fundne.OrderByDescending(f => f.When).ToList();
    }

    /// <summary>
    /// Samler et efterladt møde. Markerer det i meeting.json, så det senere
    /// er tydeligt, at optagelsen ikke blev afsluttet normalt.
    /// </summary>
    public static IReadOnlyDictionary<string, SegmentAssembler.Inventory> Recover(RecoverableSession session)
    {
        var resultat = new Dictionary<string, SegmentAssembler.Inventory>();
        var segmentRod = Path.Combine(session.SessionDir, "segmenter");

        foreach (var spor in session.Tracks.Keys)
        {
            var kilde = Path.Combine(segmentRod, spor);
            var mål = Path.Combine(session.SessionDir, $"{spor}.wav");
            resultat[spor] = SegmentAssembler.Assemble(kilde, mål);
        }

        try { Directory.Delete(segmentRod, recursive: true); }
        catch (IOException) { /* lad dem ligge — de gør ingen skade */ }

        MeetingStore.MarkRecovered(session.SessionDir, resultat.Values.Max(v => v.Seconds));
        return resultat;
    }
}
