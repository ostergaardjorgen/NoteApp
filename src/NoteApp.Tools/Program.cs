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
          prompt    Viser den ordliste der sendes til Whisper (valgfrit: <kunde>)
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
