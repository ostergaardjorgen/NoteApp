// NoteApp — Fase 0 to-spors optager.
//
// Optager mikrofon og WASAPI-loopback som to separate WAV-filer med faelles
// starttidsstempel. Begge spor skrives direkte som 16 kHz mono PCM16, som er
// det format whisper.cpp kraever — konverteringen sker undervejs, saa en
// 90-minutters optagelse fylder ca. 100 MB pr. spor i stedet for 2 GB.
//
// Brug:
//   Fase0Recorder --type online   --title "Kundemoede Nordby"
//   Fase0Recorder --type fysisk   --title "Sparring med Morten"

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace NoteApp.Fase0;

internal enum MeetingType { Physical, Online }

internal static class Program
{
    private const int TargetSampleRate = 16_000;   // whisper.cpp kraever 16 kHz mono

    /// <summary>
    /// Hvor optagelserne skal ligge. Fase 0-scriptet leder i repoets
    /// fase0\optagelser, så den sti vinder når den findes. Kildefilens
    /// placering på compile-tidspunktet peger på repoet uanset hvor
    /// build-output havner — men en publiceret kopi kan være flyttet til en
    /// maskine hvor stien ikke findes, og så falder vi tilbage på appens
    /// datamappe frem for at crashe eller skrive et tilfældigt sted hen.
    ///
    /// Rækkefølgen for den fallback er den samme som i appen: NOTEAPP_DATA,
    /// så pegefilen i %APPDATA%\NoteApp\datasti.txt, så standarden
    /// C:\AppNoter. Dette projekt henviser ikke til NoteApp.Core, så logikken
    /// står her — den skal holdes i takt med UserDataPaths.
    /// </summary>
    private static string DefaultOutputRoot([CallerFilePath] string? thisFile = null)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        var iRepo = Path.Combine(repoRoot, "fase0", "optagelser");
        if (Directory.Exists(repoRoot)) return iRepo;

        return Path.Combine(DataRoot(), "Optagelser");
    }

    private static string DataRoot()
    {
        var tilsidesat = Environment.GetEnvironmentVariable("NOTEAPP_DATA");
        if (!string.IsNullOrWhiteSpace(tilsidesat)) return Path.GetFullPath(tilsidesat);

        try
        {
            var peger = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NoteApp", "datasti.txt");

            if (File.Exists(peger))
            {
                var valgt = File.ReadAllText(peger).Trim();
                if (!string.IsNullOrWhiteSpace(valgt)) return Path.GetFullPath(valgt);
            }
        }
        catch (IOException)
        {
            // En ulæselig pegefil må ikke forhindre en optagelse i at starte.
        }

        return @"C:\AppNoter";
    }

    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        MeetingType type;
        string? title;
        string? outDir;
        try
        {
            (type, title, outDir) = ParseArgs(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Brug: Fase0Recorder --type online|fysisk [--title \"Mødetitel\"] [--out <mappe>]");
            return 2;
        }

        var outputRoot = outDir ?? DefaultOutputRoot();

        using var devices = new MMDeviceEnumerator();

        MMDevice micDevice;
        try
        {
            micDevice = devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ingen mikrofon fundet: {ex.Message}");
            return 3;
        }

        MMDevice? renderDevice = null;
        if (type == MeetingType.Online)
        {
            try
            {
                renderDevice = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ingen afspilningsenhed fundet — loopback er ikke muligt: {ex.Message}");
                return 3;
            }
        }

        // Startes appen fra en genvej, er der ingen --title. Spørg frem for at
        // ende med tyve mapper der kun hedder et tidsstempel — jf. speccen
        // afsnit 2: titelfeltet føles overflødigt indtil det ikke er det.
        if (title is null && !Console.IsInputRedirected)
        {
            Console.Write("Mødetitel (ENTER for kun dato/tid): ");
            var indtastet = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(indtastet)) title = indtastet.Trim();
            Console.WriteLine();
        }

        Console.WriteLine($"Mødetype    : {(type == MeetingType.Online ? "Onlinemøde (mikrofon + loopback)" : "Fysisk møde (kun mikrofon)")}");
        Console.WriteLine($"Mikrofon    : {micDevice.FriendlyName}");
        Console.WriteLine($"Systemlyd   : {renderDevice?.FriendlyName ?? "(ikke i brug)"}");
        Console.WriteLine();

        // Sanity-check fra speccen: et onlinemoede med tavst loopback-spor er
        // den fejl der koster et helt moede. Maal foer optagelsen begynder.
        if (renderDevice is not null && !LoopbackHasSignal(renderDevice, TimeSpan.FromSeconds(3)))
        {
            Console.WriteLine("ADVARSEL: intet signal på loopback-sporet i 3 sekunder.");
            Console.WriteLine("  Enten kører mødet ikke endnu, eller lyden går til en anden enhed");
            Console.WriteLine($"  end standard-afspilningsenheden ({renderDevice.FriendlyName}).");
            Console.WriteLine("  Afspil noget lyd og prøv igen, eller tryk ENTER for at optage alligevel.");
            Console.Write("  [ENTER = fortsæt, N = afbryd] ");
            var answer = Console.ReadLine();
            if (answer is not null && answer.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Afbrudt.");
                return 1;
            }
            Console.WriteLine();
        }

        var startedAt = DateTimeOffset.Now;
        var slug = MakeSlug(title, startedAt);
        var sessionDir = Path.GetFullPath(Path.Combine(outputRoot, slug));
        Directory.CreateDirectory(sessionDir);

        var micPath = Path.Combine(sessionDir, "mikrofon.wav");
        var loopbackPath = Path.Combine(sessionDir, "loopback.wav");

        using var micTrack = TrackRecorder.ForMicrophone(micDevice, micPath);
        using var loopbackTrack = renderDevice is null
            ? null
            : TrackRecorder.ForLoopback(renderDevice, loopbackPath);

        var clock = Stopwatch.StartNew();
        micTrack.Start();
        loopbackTrack?.Start();

        Console.WriteLine($"Optager → {sessionDir}");
        Console.WriteLine("Tryk ENTER for at stoppe.");
        Console.WriteLine();

        using var stopSignal = new ManualResetEventSlim(false);
        var meterThread = new Thread(() => RunMeters(clock, micTrack, loopbackTrack, stopSignal))
        {
            IsBackground = true
        };
        meterThread.Start();

        Console.ReadLine();
        stopSignal.Set();
        meterThread.Join(TimeSpan.FromSeconds(2));

        micTrack.Stop();
        loopbackTrack?.Stop();
        clock.Stop();

        var meta = new
        {
            id = Guid.NewGuid(),
            type = type.ToString(),
            startedAt,
            durationSeconds = Math.Round(clock.Elapsed.TotalSeconds, 1),
            title,
            micDeviceName = micDevice.FriendlyName,
            loopbackDeviceName = renderDevice?.FriendlyName,   // null ved fysisk møde
            sampleRate = TargetSampleRate,
            channels = 1,
            tracks = new
            {
                mic = Path.GetFileName(micPath),
                loopback = renderDevice is null ? null : Path.GetFileName(loopbackPath)
            }
        };
        File.WriteAllText(
            Path.Combine(sessionDir, "meeting.json"),
            JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"Færdig. Varighed {clock.Elapsed:hh\\:mm\\:ss}");
        Report(micPath, "mikrofon");
        if (renderDevice is not null) Report(loopbackPath, "loopback");
        Console.WriteLine($"Metadata: {Path.Combine(sessionDir, "meeting.json")}");
        return 0;
    }

    private static (MeetingType, string?, string?) ParseArgs(string[] args)
    {
        MeetingType? type = null;
        string? title = null;
        string? outDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--type" when i + 1 < args.Length:
                    type = args[++i].ToLowerInvariant() switch
                    {
                        "online" => MeetingType.Online,
                        "fysisk" or "physical" => MeetingType.Physical,
                        var other => throw new ArgumentException($"Ukendt mødetype: {other}")
                    };
                    break;
                case "--title" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "--out" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                default:
                    throw new ArgumentException($"Ukendt argument: {args[i]}");
            }
        }

        // Ingen default — modetypen skal vaelges aktivt, jf. speccen afsnit 2.
        if (type is null) throw new ArgumentException("--type mangler. Vælg online eller fysisk.");
        return (type.Value, title, outDir);
    }

    private static bool LoopbackHasSignal(MMDevice renderDevice, TimeSpan window)
    {
        Console.Write($"Måler loopback-niveau i {window.TotalSeconds:0} sekunder ... ");
        var peak = 0f;
        using var probe = new WasapiLoopbackCapture(renderDevice);
        probe.DataAvailable += (_, e) =>
        {
            var p = PeakOf(e.Buffer, e.BytesRecorded, probe.WaveFormat);
            if (p > peak) peak = p;
        };
        probe.StartRecording();
        Thread.Sleep(window);
        probe.StopRecording();

        // -60 dBFS: lavt nok til at fange en aaben mikrofon i et stille rum,
        // hoejt nok til at digital stilhed falder igennem.
        var ok = peak > 0.001f;
        Console.WriteLine(ok ? $"OK (top {ToDb(peak):0.0} dBFS)" : "STILHED");
        return ok;
    }

    private static void RunMeters(Stopwatch clock, TrackRecorder mic, TrackRecorder? loopback, ManualResetEventSlim stop)
    {
        // Uden konsolvindue (output omdirigeret til fil eller pipe) findes der
        // ingen bufferbredde, og \r giver ingen mening. Skriv en linje i
        // stedet, sjaeldnere, saa loggen ikke drukner.
        var live = !Console.IsOutputRedirected;
        var interval = TimeSpan.FromMilliseconds(live ? 200 : 5_000);

        while (!stop.IsSet)
        {
            var line = $"  {clock.Elapsed:hh\\:mm\\:ss}   MIK {Bar(mic.ReadPeak())}";
            if (loopback is not null) line += $"   SYS {Bar(loopback.ReadPeak())}";

            if (live)
            {
                var width = 40;
                try { width = Math.Max(Console.WindowWidth - 1, 40); } catch (IOException) { /* ingen buffer */ }
                Console.Write("\r" + line.PadRight(width));
            }
            else
            {
                Console.WriteLine(line);
            }

            stop.Wait(interval);
        }
    }

    private static string Bar(float peak)
    {
        const int width = 20;
        var db = ToDb(peak);
        var filled = (int)Math.Round(Math.Clamp((db + 60f) / 60f, 0f, 1f) * width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }

    private static float ToDb(float peak) => peak <= 0f ? -100f : 20f * MathF.Log10(peak);

    internal static float PeakOf(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        var peak = 0f;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var i = 0; i + 4 <= bytesRecorded; i += 4)
            {
                var v = Math.Abs(BitConverter.ToSingle(buffer, i));
                if (v > peak) peak = v;
            }
        }
        else if (format.BitsPerSample == 16)
        {
            for (var i = 0; i + 2 <= bytesRecorded; i += 2)
            {
                var v = Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f);
                if (v > peak) peak = v;
            }
        }
        return peak;
    }

    private static string MakeSlug(string? title, DateTimeOffset when)
    {
        var stamp = when.ToString("yyyy-MM-dd_HH-mm");
        if (string.IsNullOrWhiteSpace(title)) return stamp;

        var cleaned = new string(title.Select(c =>
            char.IsLetterOrDigit(c) || c is 'æ' or 'ø' or 'å' or 'Æ' or 'Ø' or 'Å' ? c : '-').ToArray());
        cleaned = string.Join('-', cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return $"{stamp}_{cleaned}";
    }

    private static void Report(string path, string label)
    {
        var info = new FileInfo(path);
        Console.WriteLine($"  {label,-10} {info.Length / 1024d / 1024d:0.0} MB   {path}");
    }
}

/// <summary>
/// Ét lydspor: opsamler fra WASAPI og skriver 16 kHz mono PCM16 til disk.
/// Konverteringen sker i en pumpe-tråd, så opsamlings-callbacken aldrig
/// blokerer på disk-I/O — det er dét, der ellers giver dropouts på lange møder.
/// </summary>
internal sealed class TrackRecorder : IDisposable
{
    private readonly WasapiCapture _capture;
    private readonly BufferedWaveProvider _incoming;
    private readonly IWaveProvider _converted;
    private readonly WaveFileWriter _writer;
    private readonly Thread _pump;
    private readonly ManualResetEventSlim _stopping = new(false);
    private readonly byte[] _pumpBuffer = new byte[16_000 * 2];   // 1 sekund ved 16 kHz mono PCM16

    private float _peak;

    private TrackRecorder(WasapiCapture capture, string path)
    {
        _capture = capture;

        _incoming = new BufferedWaveProvider(capture.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(30),
            DiscardOnBufferOverflow = true,

            // AFGØRENDE: default er true, hvilket får Read() til at fylde op med
            // stilhed i stedet for at returnere 0 når bufferen er tom. Pumpen
            // ville så aldrig gå i dvale, men skrive tomme samples så hurtigt
            // CPU'en kunne — 9 sekunders optagelse blev til en 192 MB WAV-fil.
            ReadFully = false
        };

        ISampleProvider samples = _incoming.ToSampleProvider();
        samples = ToMono(samples);
        samples = new WdlResamplingSampleProvider(samples, 16_000);
        _converted = samples.ToWaveProvider16();

        _writer = new WaveFileWriter(path, _converted.WaveFormat);

        _capture.DataAvailable += OnDataAvailable;
        _pump = new Thread(Pump) { IsBackground = true, Name = $"pump:{Path.GetFileName(path)}" };
    }

    public static TrackRecorder ForMicrophone(MMDevice device, string path)
        => new(new WasapiCapture(device), path);

    public static TrackRecorder ForLoopback(MMDevice device, string path)
        => new(new WasapiLoopbackCapture(device), path);

    public void Start()
    {
        _pump.Start();
        _capture.StartRecording();
    }

    public void Stop()
    {
        _capture.StopRecording();
        _stopping.Set();
        _pump.Join(TimeSpan.FromSeconds(10));
        DrainOnce();          // tøm det der stod i bufferen da vi stoppede
        _writer.Flush();
    }

    /// <summary>Højeste niveau siden sidste kald — nulstiller måleren.</summary>
    public float ReadPeak()
    {
        var p = _peak;
        _peak = 0f;
        return p;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var p = Program.PeakOf(e.Buffer, e.BytesRecorded, _capture.WaveFormat);
        if (p > _peak) _peak = p;
        _incoming.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void Pump()
    {
        while (!_stopping.IsSet)
        {
            if (DrainOnce() == 0)
                _stopping.Wait(TimeSpan.FromMilliseconds(50));
        }
    }

    private int DrainOnce()
    {
        var total = 0;
        int read;
        while ((read = _converted.Read(_pumpBuffer, 0, _pumpBuffer.Length)) > 0)
        {
            _writer.Write(_pumpBuffer, 0, read);
            total += read;
            if (total >= _pumpBuffer.Length * 4) break;   // giv maaleren luft
        }
        if (total > 0) _writer.Flush();                   // autosave: intet ligger og venter i RAM
        return total;
    }

    private static ISampleProvider ToMono(ISampleProvider source) => source.WaveFormat.Channels switch
    {
        1 => source,
        2 => new StereoToMonoSampleProvider(source) { LeftVolume = 0.5f, RightVolume = 0.5f },
        // Flerkanals-enheder (fx et headset der melder sig som 4 kanaler):
        // tag foerste kanal frem for at gaette paa en nedmixning.
        _ => new MultiplexingSampleProvider(new[] { source }, 1)
    };

    public void Dispose()
    {
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _writer.Dispose();
        _stopping.Dispose();
    }
}
