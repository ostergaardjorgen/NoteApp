using NoteApp.Core;

// Kommandolinjeværktøj til de ting, der ikke hører hjemme i en optageknap:
// initialisering af ordbogen, genopretning efter crash, og et hurtigt kig på
// hvad appen faktisk sender med til Whisper.

Console.OutputEncoding = System.Text.Encoding.UTF8;

var kommando = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

try
{
    return kommando switch
    {
        "status"    => Status(),
        "init"      => Init(),
        "prompt"    => VisPrompt(args.Skip(1).FirstOrDefault()),
        "tilfoej"   => Tilfoej(args.Skip(1).ToArray()),
        "eksport"   => Eksport(args.Skip(1).FirstOrDefault()),
        "motor"     => Motor(),
        "transskriber" => await Transskriber(args.Skip(1).ToArray()),
        "recover"   => Genopret(),
        "hjaelp" or "--help" or "-h" => Hjælp(),
        _ => Ukendt(kommando)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FEJL: {ex.Message}");
    return 1;
}

static int Hjælp()
{
    Console.WriteLine("""
        noteapp <kommando>

          status    Viser hvor dine data ligger og hvad de indeholder
          init      Opretter ordbogen og indlæser ordliste.txt
          tilfoej   Lægger et ord i ordbogen:
                      noteapp tilfoej "Malene" person [vaegt] [kunde]
                    Kategorier: person, organisation, produkt, fagterm, forkortelse
          prompt    Viser den ordliste der sendes til Whisper (valgfrit: <kunde>)
          eksport   Skriver ordlisten til ordliste.txt, som Fase 0-scriptet læser
          motor     Viser hvilken Whisper-motor og model der er i brug
          transskriber  Transskriberer en optagelse:
                      noteapp transskriber <mappe-eller-wav> [--cpu]
          recover   Samler møder der aldrig blev lukket ordentligt

        Dine data ligger i:
          %LOCALAPPDATA%\NoteApp   (eller NOTEAPP_DATA, hvis sat)

        De ligger med vilje uden for kode-repoet, så de ikke kan komme med
        i en git-push. Backup tages med scripts\backup-mine-data.ps1.
        """);
    return 0;
}

static int Ukendt(string k)
{
    Console.Error.WriteLine($"Ukendt kommando: {k}");
    Hjælp();
    return 2;
}

static int Status()
{
    UserDataPaths.EnsureCreated();

    Console.WriteLine($"Datamappe   : {UserDataPaths.Root}");
    Console.WriteLine($"Møder       : {UserDataPaths.Meetings}");
    Console.WriteLine($"Ordbog      : {UserDataPaths.LearningDatabase}");
    Console.WriteLine();

    var møder = Directory.Exists(UserDataPaths.Meetings)
        ? Directory.GetDirectories(UserDataPaths.Meetings).Length
        : 0;
    Console.WriteLine($"Møder gemt  : {møder}");

    if (File.Exists(UserDataPaths.LearningDatabase))
    {
        using var store = new LearningStore();
        Console.WriteLine($"Termer      : {store.TermCount()}");

        var pr = store.CorrectionsByEngine();
        if (pr.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Rettelser pr. motor (grundlag for regressionsmåling ved opdatering):");
            foreach (var (motor, rettelser, møderMedRettelser) in pr)
                Console.WriteLine($"  {motor,-34} {rettelser} rettelser i {møderMedRettelser} møder");
        }
    }
    else
    {
        Console.WriteLine("Ordbog      : ikke oprettet endnu — kør 'noteapp init'");
    }

    var efterladte = SessionRecovery.Scan();
    if (efterladte.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{efterladte.Count} møde(r) blev aldrig lukket ordentligt. Kør 'noteapp recover'.");
    }

    var enheder = new[]
    {
        ("Mikrofon ", AudioDevices.DefaultMicrophone()?.FriendlyName),
        ("Systemlyd", AudioDevices.DefaultRenderDevice()?.FriendlyName)
    };
    Console.WriteLine();
    foreach (var (navn, enhed) in enheder)
        Console.WriteLine($"{navn}   : {enhed ?? "(ingen fundet)"}");

    return 0;
}

static int Init()
{
    UserDataPaths.EnsureCreated();
    UserDataPaths.AssertOutsideRepository(AppContext.BaseDirectory);

    using var store = new LearningStore();
    Console.WriteLine($"Ordbog oprettet: {store.Path}");

    if (File.Exists(UserDataPaths.Vocabulary))
    {
        var antal = store.ImportVocabularyFile(UserDataPaths.Vocabulary);
        Console.WriteLine($"Indlæste {antal} termer fra {UserDataPaths.Vocabulary}");
    }
    else
    {
        Console.WriteLine($"Ingen ordliste fundet på {UserDataPaths.Vocabulary} — ordbogen er tom.");
    }

    Console.WriteLine($"Termer i alt: {store.TermCount()}");
    return 0;
}

/// <summary>
/// Lægger ét ord i ordbogen. Findes, fordi UI'et er rigtigt til at gå en
/// ordbog igennem, men klodset når man skal hælde en håndfuld navne ind på
/// én gang — fx alle deltagerne fra ét møde.
/// </summary>
static int Tilfoej(string[] a)
{
    if (a.Length < 2)
    {
        Console.Error.WriteLine("Brug: noteapp tilfoej \"<ord>\" <kategori> [vaegt] [kunde]");
        Console.Error.WriteLine($"Kategorier: {string.Join(", ", TermCategories.All)}");
        return 2;
    }

    var ord = a[0];
    var kategori = a[1].ToLowerInvariant();

    if (!TermCategories.All.Contains(kategori))
    {
        Console.Error.WriteLine($"Ukendt kategori: {kategori}");
        Console.Error.WriteLine($"Vælg mellem: {string.Join(", ", TermCategories.All)}");
        return 2;
    }

    var vægt = a.Length > 2 && double.TryParse(a[2],
        System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 1.0;
    var kunde = a.Length > 3 ? a[3] : null;

    using var store = new LearningStore();
    store.AddTerm(ord, kategori, vægt, kunde);

    Console.WriteLine($"Tilføjet: {ord} ({TermCategories.Label(kategori)}, vægt {vægt:0.0}" +
                      (kunde is null ? "" : $", kunde {kunde}") + ")");
    Console.WriteLine($"Termer i alt: {store.TermCount()}");
    return 0;
}

/// <summary>
/// Skriver ordbogens prompt til ordliste.txt. Ordbogen er kilden; filen er et
/// øjebliksbillede af den, og det er filen, Fase 0-scriptet læser. Uden dette
/// trin måler gaten på en ordliste, der kan være uger gammel.
/// </summary>
static int Eksport(string? kunde)
{
    using var store = new LearningStore();
    var sti = store.ExportVocabularyFile(scope: kunde);

    Console.WriteLine($"Skrevet : {sti}");
    Console.WriteLine($"Termer  : {store.TermCount()} i ordbogen");
    Console.WriteLine($"Tokens  : {LearningStore.EstimateTokens(File.ReadAllText(sti))} af budgettet på 200");
    return 0;
}

static int Motor()
{
    var s = WhisperInstall.Locate();

    Console.WriteLine($"Motor    : {s.WhisperCli ?? "IKKE FUNDET"}");
    Console.WriteLine($"Version  : {s.EngineVersion ?? "kunne ikke aflæses"}");
    Console.WriteLine($"Beregning: {s.Engine}");
    Console.WriteLine($"Model    : {s.ModelFileName ?? "IKKE FUNDET"}");
    Console.WriteLine($"Sti      : {s.ModelPath ?? "-"}");
    Console.WriteLine();

    Console.WriteLine("Modeller på disken:");
    var installeret = WhisperInstall.Installed().ToList();
    if (installeret.Count == 0) Console.WriteLine("  (ingen)");
    foreach (var m in installeret)
        Console.WriteLine($"  {m.Id,-16} {m.SizeText,8}  {(m.SupportsDanish ? "dansk+" : "KUN engelsk")}");

    Console.WriteLine();
    Console.WriteLine("Kan hentes:");
    foreach (var m in WhisperInstall.Models)
        Console.WriteLine($"  {m.Id,-16} {m.SizeText,8}  {m.Summary}");

    return s.IsComplete ? 0 : 1;
}

static async Task<int> Transskriber(string[] a)
{
    if (a.Length == 0)
    {
        Console.Error.WriteLine("Brug: noteapp transskriber <mappe-eller-wav> [--cpu]");
        return 2;
    }

    var s = WhisperInstall.Locate();
    if (!s.IsComplete)
    {
        Console.Error.WriteLine("Motor eller model mangler. Kør 'noteapp motor' for at se hvad.");
        return 1;
    }

    // Peges der på en mappe, tages mikrofonsporet — det er det spor, der
    // altid findes, ogsaa ved fysiske moeder uden loopback.
    var input = a[0];
    var wav = Directory.Exists(input) ? Path.Combine(input, "mikrofon.wav") : input;
    if (!File.Exists(wav)) { Console.Error.WriteLine($"Findes ikke: {wav}"); return 1; }

    using var store = new LearningStore();
    var prompt = store.BuildWhisperPrompt();

    var udBase = Path.Combine(Path.GetDirectoryName(wav)!,
        Path.GetFileNameWithoutExtension(wav) + "_" +
        Path.GetFileNameWithoutExtension(s.ModelPath!).Replace("ggml-", ""));

    Console.WriteLine($"Lyd     : {wav} ({Transcriber.WavSeconds(wav):0.0} sek)");
    Console.WriteLine($"Model   : {s.ModelFileName}  ({s.Engine})");
    Console.WriteLine($"Ordliste: {LearningStore.EstimateTokens(prompt)} tokens");
    Console.WriteLine();

    var motor = new Transcriber(s.WhisperCli!);
    var sidst = -1;
    var fremdrift = new Progress<TranscriptionProgress>(p =>
    {
        var pct = (int)p.Percent;
        if (pct == sidst) return;
        sidst = pct;
        Console.Write($"\r  {p.Message,-40}");
    });

    var r = await motor.RunAsync(
        new TranscriptionRequest(wav, s.ModelPath!, udBase, "da", prompt, a.Contains("--cpu")),
        fremdrift);

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Tid brugt : {r.ElapsedSeconds:0.0} sek på {r.AudioSeconds:0.0} sek lyd");
    Console.WriteLine($"RTF       : {r.RealTimeFactor:0.00}  ({(r.RealTimeFactor <= 1 ? "hurtigere end realtid" : "LANGSOMMERE end realtid — planlæg som natjob")})");
    Console.WriteLine($"Motor-id  : {r.EngineId}");
    Console.WriteLine($"Tekst     : {r.TextPath}");
    return 0;
}

static int VisPrompt(string? kunde)
{
    using var store = new LearningStore();
    var prompt = store.BuildWhisperPrompt(kunde);

    Console.WriteLine($"Kunde/scope : {kunde ?? "(alle globale termer)"}");
    Console.WriteLine($"Tokens brugt: {LearningStore.EstimateTokens(prompt)} af budgettet på 200");
    Console.WriteLine($"Termer i alt: {store.TermCount()}");
    Console.WriteLine();
    Console.WriteLine(string.IsNullOrEmpty(prompt) ? "(tom — kør 'noteapp init')" : prompt);
    Console.WriteLine();
    Console.WriteLine("Kommer alle termer ikke med, er det med vilje: Whisper har kun plads til");
    Console.WriteLine("ca. 224 tokens, og overfyldes prompten, skriver den den ind i transskriptionen.");
    return 0;
}

static int Genopret()
{
    var efterladte = SessionRecovery.Scan();
    if (efterladte.Count == 0)
    {
        Console.WriteLine("Ingen efterladte optagelser. Alt er lukket ordentligt.");
        return 0;
    }

    Console.WriteLine($"{efterladte.Count} møde(r) har segmenter der aldrig blev samlet:");
    Console.WriteLine();

    foreach (var s in efterladte)
    {
        Console.WriteLine($"  {s.Title}");
        foreach (var (spor, opgørelse) in s.Tracks)
        {
            var advarsel = opgørelse.Unreadable > 0 ? $"  ({opgørelse.Unreadable} ulæselige segmenter springes over)" : "";
            Console.WriteLine($"    {spor,-10} {opgørelse.SegmentCount} segmenter = {TimeSpan.FromSeconds(opgørelse.Seconds):hh\\:mm\\:ss}{advarsel}");
        }
    }

    Console.WriteLine();
    Console.Write("Saml dem nu? [j/N] ");
    if (!string.Equals(Console.ReadLine()?.Trim(), "j", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Afbrudt. Segmenterne ligger urørt.");
        return 0;
    }

    foreach (var s in efterladte)
    {
        var resultat = SessionRecovery.Recover(s);
        foreach (var (spor, opgørelse) in resultat)
            Console.WriteLine($"  {s.Title}\\{spor}.wav — {TimeSpan.FromSeconds(opgørelse.Seconds):hh\\:mm\\:ss} reddet");
    }

    return 0;
}
