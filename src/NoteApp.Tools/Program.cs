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
        "hent"      => await Hent(args.Skip(1).ToArray()),
        "hentmotor" => await HentMotor(args.Skip(1).ToArray()),
        "sprogmodel" => await Sprogmodel(args.Skip(1).ToArray()),
        "udkast"    => await Udkast(args.Skip(1).ToArray()),
        "gendan"    => Gendan(args.Skip(1).ToArray()),
        "transskriber" => await Transskriber(args.Skip(1).ToArray()),
        "laer"      => Laer(args.Skip(1).ToArray()),
        "dokument"  => Dokument(args.Skip(1).ToArray()),
        "score"     => Score(args.Skip(1).ToArray()),
        "mikrofontest" => Mikrofontest(args.Skip(1).ToArray()),
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

        Dine data ligger i C:\AppNoter — eller den mappe, du har valgt i
        appen under Filer og backup. Miljøvariablen NOTEAPP_DATA vinder over
        begge dele.

        De ligger med vilje uden for kode-repoet, så de ikke kan komme med
        i en git-push.

        Sikkerhedskopi tages i appen under Filer og backup — der kan du også
        gendanne. scripts\backup-mine-data.ps1 gør det samme fra en planlagt
        opgave.
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
    // Samme valg som appen. Indstillingerne ligger i Core netop for det:
    // laeser CLI'en ikke det samme, viser den en anden model end den appen
    // bruger, og saa bruger man en time paa at forstaa hvorfor.
    var valgt = AppSettings.Current.PreferredModel;
    var s = WhisperInstall.Locate(valgt);

    Console.WriteLine($"Valgt i appen: {valgt ?? "(intet valg — bedste model på disken bruges)"}");
    Console.WriteLine();
    Console.WriteLine($"Motor    : {s.WhisperCli ?? "IKKE FUNDET"}");
    var (etiket, vaerdi) = s.AgeLine;
    Console.WriteLine($"{etiket,-9}: {vaerdi}");
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

/// <summary>
/// Sprogmodeller til skabelonlaget. Uden argument vises kataloget.
/// </summary>
static async Task<int> Sprogmodel(string[] a)
{
    if (a.Length == 0)
    {
        var vram = 6144L * 1024 * 1024;   // maales rigtigt i appen; her til visningen

        Console.WriteLine("Frit til salg (Apache 2.0 / MIT):");
        foreach (var m in NoteApp.Core.Llm.LlmCatalog.Default)
            Console.WriteLine($"  {m.Id,-20} {m.SizeText,8}  {(m.FitsInVram(vram) ? "passer i VRAM" : "kører på CPU")}  {m.License}");

        Console.WriteLine();
        Console.WriteLine("Betingelser følger med til dine kunder — vælges kun bevidst:");
        foreach (var m in NoteApp.Core.Llm.LlmCatalog.WithObligations)
            Console.WriteLine($"  {m.Id,-20} {m.SizeText,8}  {m.License}");

        var fravalgt = NoteApp.Core.Llm.LlmCatalog.Rejected.ToList();
        if (fravalgt.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Undersøgt og fravalgt — kan ikke hentes:");
            foreach (var m in fravalgt)
                Console.WriteLine($"  {m.Id,-20} {m.SizeText,8}  se doc/findings.md");
        }

        Console.WriteLine();
        Console.WriteLine("Hent med: noteapp sprogmodel <id>");
        return 0;
    }

    var valgt = NoteApp.Core.Llm.LlmCatalog.ById(a[0]);
    if (valgt is null) { Console.Error.WriteLine($"Kender ikke modellen: {a[0]}"); return 2; }

    // En model, der er proevet af og fravalgt, maa ikke kunne hentes ved et
    // uheld. Begrundelsen staar, saa fravalget kan omgoeres bevidst — og saa
    // de timer, det kostede at afvise den, ikke bliver brugt igen.
    if (valgt.Rejected is not null)
    {
        Console.Error.WriteLine($"{valgt.Id} er undersøgt og fravalgt.");
        Console.Error.WriteLine();
        Console.Error.WriteLine(valgt.Rejected);
        Console.Error.WriteLine();
        Console.Error.WriteLine("Se doc/findings.md. Skal den ind igen, skal fravalget fjernes bevidst i LlmCatalog.cs.");
        return 3;
    }

    if (valgt.LicenseClass != NoteApp.Core.Llm.LicenseClass.FriTilSalg)
    {
        Console.WriteLine($"BEMÆRK — {valgt.License}");
        Console.WriteLine(valgt.LicenseObligation);
        Console.WriteLine();
    }

    Directory.CreateDirectory(NoteApp.Core.Llm.LlmRunner.ModelDirectory);
    var maal = Path.Combine(NoteApp.Core.Llm.LlmRunner.ModelDirectory, valgt.FileName);

    Console.WriteLine($"Henter {valgt.Id} ({valgt.SizeText}) fra {valgt.Repo}");

    var sidst = -1;
    var fremdrift = new Progress<DownloadProgress>(p =>
    {
        var pct = (int)p.Percent;
        if (pct == sidst) return;
        sidst = pct;
        var tilbage = p.Remaining is null ? "" : $"  {p.Remaining.Value:mm\\:ss} tilbage";
        Console.Write($"\r  {pct,3}%  {p.BytesDone / 1024.0 / 1024.0,6:0} / {p.BytesTotal / 1024.0 / 1024.0:0} MB" +
                      $"  {p.BytesPerSecond / 1024.0 / 1024.0:0.0} MB/s{tilbage}   ");
    });

    await new Downloader().DownloadAsync(valgt.Url, maal, null, fremdrift);

    Console.WriteLine();
    Console.WriteLine($"Hentet: {maal}  ({new FileInfo(maal).Length / 1024.0 / 1024.0 / 1024.0:0.0} GB)");
    return 0;
}

/// <summary>
/// Kører en skabelon på en tekst og gemmer udkastet med proveniens.
///   noteapp udkast &lt;mappe-eller-tekstfil&gt; [skabelon] [model.gguf]
/// </summary>
static async Task<int> Udkast(string[] a)
{
    NoteApp.Core.Llm.DraftStore.SeedTemplates();

    var cli = NoteApp.Core.Llm.LlmRunner.FindCli();
    if (cli is null) { Console.Error.WriteLine("llama-cli.exe er ikke hentet endnu."); return 1; }

    var modeller = NoteApp.Core.Llm.LlmRunner.InstalledModels();
    if (modeller.Count == 0) { Console.Error.WriteLine("Ingen sprogmodel hentet. Kør 'noteapp sprogmodel'."); return 1; }

    var skabeloner = NoteApp.Core.Llm.PromptTemplate.LoadAll();
    if (skabeloner.Count == 0) { Console.Error.WriteLine($"Ingen skabeloner i {NoteApp.Core.Llm.PromptTemplate.Directory}"); return 1; }

    if (a.Length == 0)
    {
        Console.WriteLine("Skabeloner:");
        foreach (var s in skabeloner) Console.WriteLine($"  {s.Name}");
        Console.WriteLine();
        Console.WriteLine("Modeller:");
        foreach (var m in modeller) Console.WriteLine($"  {Path.GetFileName(m)}");
        Console.WriteLine();
        Console.WriteLine("Brug: noteapp udkast <mappe-eller-tekstfil> [skabelon] [model.gguf]");
        return 0;
    }

    // Kilden: enten en moedemappe eller en ren tekstfil.
    string tekst, moedeMappe, titel;
    string sprog = "ikke registreret";
    if (Directory.Exists(a[0]))
    {
        moedeMappe = a[0];
        var txt = Directory.GetFiles(moedeMappe, "*.txt").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        if (txt is null) { Console.Error.WriteLine("Fandt ingen transskription (.txt) i mappen."); return 1; }
        tekst = File.ReadAllText(txt);
        var meta = MeetingStore.Load(moedeMappe);
        titel = meta?.Title ?? Path.GetFileName(moedeMappe);

        // Sproget staar kun her, hvis moedet er transskriberet med
        // detektering. Er det ikke registreret, siges det — frem for at
        // antage dansk og faa skabelonen til at tro paa noget, ingen har maalt.
        if (!string.IsNullOrEmpty(meta?.Language))
            sprog = Transcriber.LanguageName(meta.Language);
    }
    else if (File.Exists(a[0]))
    {
        tekst = File.ReadAllText(a[0]);
        moedeMappe = Path.GetDirectoryName(Path.GetFullPath(a[0]))!;
        titel = Path.GetFileNameWithoutExtension(a[0]);
    }
    else { Console.Error.WriteLine($"Findes ikke: {a[0]}"); return 1; }

    var skabelon = a.Length > 1
        ? skabeloner.FirstOrDefault(s => s.Name.Contains(a[1], StringComparison.OrdinalIgnoreCase)) ?? skabeloner[0]
        : skabeloner[0];

    var model = a.Length > 2
        ? modeller.FirstOrDefault(m => Path.GetFileName(m).Contains(a[2], StringComparison.OrdinalIgnoreCase)) ?? modeller[0]
        : modeller[0];

    using var store = new LearningStore();

    var felter = new Dictionary<string, string?>
    {
        ["transskription"] = tekst,
        ["titel"] = titel,
        ["dato"] = DateTime.Now.ToString("d. MMMM yyyy"),
        ["varighed"] = "",
        ["noter"] = "",
        ["sprog"] = sprog,
        // Ordbogen sendes som en ren liste. BuildWhisperPrompt formulerer den
        // som "Vi taler om ..." fordi Whisper konditionerer paa sprogtone —
        // en sprogmodel skal bare have ordene.
        ["ordbog"] = store.BuildWhisperPrompt(tokenBudget: 2000)
            .Replace("Vi taler om ", "").TrimEnd('.')
    };

    Console.WriteLine($"Skabelon : {skabelon.Name}");
    Console.WriteLine($"Model    : {Path.GetFileName(model)}");
    Console.WriteLine($"Tekst    : {tekst.Length} tegn (~{tekst.Length / 3} tokens)");
    Console.WriteLine();

    var runner = new NoteApp.Core.Llm.LlmRunner(cli);
    var sidstBesked = "";
    var fremdrift = new Progress<NoteApp.Core.Llm.LlmProgress>(p =>
    {
        if (p.Message != sidstBesked) { sidstBesked = p.Message; Console.WriteLine($"  {p.Message}"); }
    });

    var r = await runner.RunAsync(model, skabelon, skabelon.Render(felter), fremdrift);

    var sti = NoteApp.Core.Llm.DraftStore.Save(moedeMappe, skabelon, r);

    Console.WriteLine();
    Console.WriteLine($"Tid      : {r.Elapsed.TotalSeconds:0.0} sek");
    Console.WriteLine($"Tokens   : {r.PromptTokens} ind, {r.ResponseTokens} ud  ({r.TokensPerSecond:0.0}/sek)");
    Console.WriteLine($"Gemt     : {sti}");
    return 0;
}

/// <summary>
/// Henter og udpakker Whisper-motoren. Uden argument vises hvad der findes,
/// og hvad der anbefales til denne maskine.
/// </summary>
static async Task<int> HentMotor(string[] a)
{
    Console.WriteLine("Spørger GitHub om nyeste udgivelse ...");
    var udgivelse = await EngineInstaller.FetchLatestAsync();

    Console.WriteLine($"Version : {udgivelse.Version}");
    Console.WriteLine($"NVIDIA  : {(EngineInstaller.HasNvidiaGpu() ? "ja — CUDA kan bruges" : "nej — CPU-udgave anbefales")}");
    Console.WriteLine();

    var anbefalet = EngineInstaller.Recommend(udgivelse);

    Console.WriteLine("Udgaver:");
    foreach (var b in udgivelse.Builds)
    {
        var maerke = b == anbefalet ? " <- anbefales" : "";
        Console.WriteLine($"  {b.FileName,-38} {b.SizeText,8}{maerke}");
    }
    Console.WriteLine();

    if (a.Length == 0)
    {
        Console.WriteLine("Hent med: noteapp hentmotor <filnavn>   (eller 'anbefalet')");
        return 0;
    }

    var valgt = a[0].Equals("anbefalet", StringComparison.OrdinalIgnoreCase)
        ? anbefalet
        : udgivelse.Builds.FirstOrDefault(b => b.FileName.Equals(a[0], StringComparison.OrdinalIgnoreCase));

    if (valgt is null) { Console.Error.WriteLine($"Kender ikke udgaven: {a[0]}"); return 2; }

    var sidst = -1;
    var fremdrift = new Progress<DownloadProgress>(p =>
    {
        var pct = (int)p.Percent;
        if (pct == sidst) return;
        sidst = pct;
        Console.Write($"\r  {pct,3}%  {p.BytesDone / 1024.0 / 1024.0,6:0} / {p.BytesTotal / 1024.0 / 1024.0:0} MB   ");
    });

    await EngineInstaller.InstallAsync(valgt, udgivelse.Version, fremdrift,
        new Progress<string>(s => Console.WriteLine($"\n{s}")));

    Console.WriteLine();
    var s2 = WhisperInstall.Locate();
    Console.WriteLine($"Motor nu : {s2.WhisperCli ?? "IKKE FUNDET"}");
    var (etiket2, vaerdi2) = s2.AgeLine;
    Console.WriteLine($"{etiket2,-9}: {vaerdi2}");
    return s2.WhisperCli is null ? 1 : 0;
}

/// <summary>
/// Gendanner fra en sikkerhedskopi. Uden --foralvor er det en proevekoersel,
/// der ikke roerer dine data — det er den, man skal have koert mindst een
/// gang, foer man faar brug for den rigtige.
/// </summary>
static int Gendan(string[] a)
{
    if (a.Length == 0)
    {
        Console.Error.WriteLine("Brug: noteapp gendan <arkiv.zip> [--foralvor]");
        return 2;
    }

    var arkiv = a[0];
    if (!File.Exists(arkiv)) { Console.Error.WriteLine($"Findes ikke: {arkiv}"); return 1; }

    var i = RestoreService.Inspect(arkiv);
    Console.WriteLine($"Arkiv     : {Path.GetFileName(arkiv)}");
    Console.WriteLine($"Fra       : {i.Created:dd/MM/yyyy HH:mm}");
    Console.WriteLine($"Indhold   : {i.Files} filer, {i.MegaBytes:0.0} MB");
    Console.WriteLine($"            {i.Summary}");
    Console.WriteLine($"Ordbog med: {(i.HasDictionary ? "ja" : "NEJ")}");
    Console.WriteLine();

    if (!a.Contains("--foralvor"))
    {
        var udpakket = RestoreService.TestRestore(arkiv);
        Console.WriteLine("PRØVEKØRSEL — dine data er ikke rørt.");
        Console.WriteLine($"Udpakket til: {udpakket}");
        Console.WriteLine("Ordbogen i arkivet er en gyldig SQLite-fil.");
        Console.WriteLine();
        Console.WriteLine("Kør med --foralvor for at gendanne rigtigt.");
        return 0;
    }

    var r = RestoreService.Restore(arkiv);
    Console.WriteLine($"Gendannet : {r.FilesWritten} filer til {UserDataPaths.Root}");
    Console.WriteLine($"Fortrydes : {r.SafetyArchive ?? "ingen kopi taget — datamappen var tom"}");
    return 0;
}

/// <summary>
/// Henter en model. Samme kode som appens knap — der er kun ét sted i
/// projektet, der rører netværket, og det er Downloader.
/// </summary>
static async Task<int> Hent(string[] a)
{
    if (a.Length == 0)
    {
        Console.Error.WriteLine("Brug: noteapp hent <model>");
        Console.Error.WriteLine($"Modeller: {string.Join(", ", WhisperInstall.Models.Select(m => m.Id))}");
        return 2;
    }

    var model = WhisperInstall.Model(a[0]);
    if (model is null) { Console.Error.WriteLine($"Ukendt model: {a[0]}"); return 2; }

    var mål = WhisperInstall.ModelDestination(model);

    Console.WriteLine($"Model     : {model.Id} ({model.SizeText})");
    Console.WriteLine($"Sprog     : {(model.SupportsDanish ? "flersproget, kan dansk" : "KUN ENGELSK")}");
    Console.WriteLine($"Hentes fra: {model.Url}");
    Console.WriteLine($"Gemmes som: {mål}");
    Console.WriteLine();

    var sidst = -1;
    var fremdrift = new Progress<DownloadProgress>(p =>
    {
        var pct = (int)p.Percent;
        if (pct == sidst) return;
        sidst = pct;
        var tilbage = p.Remaining is null ? "" : $"  {p.Remaining.Value:mm\\:ss} tilbage";
        Console.Write($"\r  {pct,3}%  {p.BytesDone / 1024.0 / 1024.0,7:0} / {p.BytesTotal / 1024.0 / 1024.0:0} MB" +
                      $"  {p.BytesPerSecond / 1024.0 / 1024.0:0.0} MB/s{tilbage}    ");
    });

    var ur = System.Diagnostics.Stopwatch.StartNew();
    await new Downloader().DownloadAsync(model.Url, mål, model.Bytes, fremdrift);
    ur.Stop();

    var fil = new FileInfo(mål);
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Hentet    : {fil.Length / 1024.0 / 1024.0:0.0} MB på {ur.Elapsed.TotalSeconds:0} sek");
    Console.WriteLine($"Forventet : {model.Bytes / 1024.0 / 1024.0:0.0} MB");
    Console.WriteLine($"Stemmer   : {(fil.Length == model.Bytes ? "JA" : "NEJ - filen har ikke den forventede størrelse")}");
    return fil.Length == model.Bytes ? 0 : 1;
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

    // --sprog da laaser sproget; uden det finder Whisper det selv. Detektering
    // er standard, fordi et engelsk moede transskriberet som dansk giver
    // volapyk frem for en fejl — og volapyk ligner et resultat.
    var i = Array.IndexOf(a, "--sprog");
    var ønsketSprog = i >= 0 && i + 1 < a.Length ? a[i + 1] : "auto";

    var r = await motor.RunAsync(
        new TranscriptionRequest(wav, s.ModelPath!, udBase, ønsketSprog, prompt, a.Contains("--cpu")),
        fremdrift);

    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Tid brugt : {r.ElapsedSeconds:0.0} sek på {r.AudioSeconds:0.0} sek lyd");
    Console.WriteLine($"RTF       : {r.RealTimeFactor:0.00}  ({(r.RealTimeFactor <= 1 ? "hurtigere end realtid" : "LANGSOMMERE end realtid — planlæg som natjob")})");
    Console.WriteLine($"Motor-id  : {r.EngineId}");

    var sikkerhed = r.LanguageProbability is double p2
        ? $" (detekteret, {p2 * 100:0}% sikker)"
        : " (valgt på forhånd)";
    Console.WriteLine($"Sprog     : {Transcriber.LanguageName(r.DetectedLanguage)}{sikkerhed}");

    if (r.LanguageProbability is double lav && lav < 0.7)
        Console.WriteLine("            BEMÆRK: usikker detektering. Dansk, norsk og svensk " +
                          "ligner hinanden. Lås sproget med --sprog da, hvis teksten ser forkert ud.");

    // Sproget gemmes paa moedet, saa skabelonerne kan forholde sig til det
    // senere. Uden det er detekteringen kun en linje i en log, der bliver slettet.
    if (Directory.Exists(input))
    {
        var meta = MeetingStore.Load(input);
        if (meta is not null)
        {
            meta.Language = r.DetectedLanguage;
            meta.LanguageProbability = r.LanguageProbability;
            MeetingStore.Save(input, meta);
        }
    }

    // Efterretning: de fejl, du allerede har rettet een gang, rettes nu af sig
    // selv. Det er DEN vej, appen laerer — ordlisten i Whispers initial_prompt
    // er maalt til ingen forskel at goere (se doc/findings.md 8).
    var retter = TranscriptCorrector.FromStore(store);
    var ændringer = retter.ApplyToFile(r.TextPath);
    if (ændringer.Count > 0)
    {
        var ialt = ændringer.Sum(æ => æ.Count);
        Console.WriteLine($"Rettet    : {ialt} steder ud fra {ændringer.Count} lærte regler");
        foreach (var æ in ændringer.OrderByDescending(x => x.Count).Take(6))
            Console.WriteLine($"            {æ.Heard} → {æ.Corrected}  ({æ.Count}x)");
        Console.WriteLine($"            rå udgave: {Path.ChangeExtension(r.TextPath, ".raa.txt")}");
    }

    Console.WriteLine($"Tekst     : {r.TextPath}");
    return 0;
}

/// <summary>
/// Lærer appen en rettelse: «det her blev hørt, det skal hedde det her».
///
/// Det er den vej, appen bliver bedre. Ordlisten i Whispers initial_prompt er
/// målt til ingen forskel at gøre (doc/findings.md 8), men en rettelse, der er
/// skrevet ned én gang, virker hver gang derefter — og den kan efterprøves ved
/// at køre den samme lyd igennem igen.
/// </summary>
static int Laer(string[] a)
{
    using var store = new LearningStore();

    if (a.Length == 0)
    {
        var regler = store.AutoApplyRules();
        Console.WriteLine($"Lærte rettelser: {regler.Count}");
        foreach (var r in regler.Take(40))
            Console.WriteLine($"  {r.Normalized,-32} → {r.Canonical}");

        Console.WriteLine();
        Console.WriteLine("Lær en ny:   noteapp laer \"det der blev hørt\" \"det rigtige\"");
        Console.WriteLine("Prøv på fil: noteapp laer --proev <tekstfil>");
        return 0;
    }

    // --proev retter en kopi og viser hvad der ville ske. En regel, man ikke
    // kan proeve af, er et gaet.
    if (a[0] is "--proev" or "--prøv")
    {
        if (a.Length < 2 || !File.Exists(a[1])) { Console.Error.WriteLine("Angiv en tekstfil."); return 1; }

        var (tekst, æ) = TranscriptCorrector.FromStore(store)
            .Apply(File.ReadAllText(a[1], System.Text.Encoding.UTF8));

        Console.WriteLine($"{æ.Sum(x => x.Count)} rettelser fra {æ.Count} regler:");
        foreach (var x in æ.OrderByDescending(x => x.Count))
            Console.WriteLine($"  {x.Heard,-32} → {x.Corrected,-24} {x.Count}x");

        if (æ.Count == 0) Console.WriteLine("  (ingen — enten er teksten ren, eller reglerne mangler)");
        return 0;
    }

    if (a.Length < 2) { Console.Error.WriteLine("Brug: noteapp laer \"hørt\" \"rigtigt\""); return 1; }

    store.LearnCorrection(a[0], a[1], a.Length > 2 ? a[2] : "fagterm");
    Console.WriteLine($"Lært: «{a[0]}» rettes til «{a[1]}» fremover.");
    return 0;
}

/// <summary>
/// Laver et OpenDocument-dokument ud af et udkast, der allerede findes.
///
/// Formatet er .odt — en åben ISO-standard, som Word, LibreOffice og Google
/// Docs alle kan læse. Et referat skal kunne sendes til en kollega uden at
/// spørge, hvad de har installeret.
/// </summary>
static int Dokument(string[] a)
{
    if (a.Length == 0)
    {
        var alle = NoteApp.Core.Documents.DocumentStore.LoadAll();
        Console.WriteLine($"Dokumenter i {NoteApp.Core.Documents.DocumentStore.Directory}: {alle.Count}");
        foreach (var d in alle.Take(30))
            Console.WriteLine($"  {d.Created:dd-MM HH:mm}  {d.Title,-40} {d.Template}");

        Console.WriteLine();
        Console.WriteLine("Lav et: noteapp dokument <udkast.md> [titel]");
        return 0;
    }

    if (!File.Exists(a[0])) { Console.Error.WriteLine($"Findes ikke: {a[0]}"); return 1; }

    var råt = File.ReadAllText(a[0], System.Text.Encoding.UTF8);

    // Forsiden fra udkastet er metadata, ikke indhold. Den skal ikke staa
    // midt i referatet som en raekke tekniske linjer.
    var dele = System.Text.RegularExpressions.Regex.Split(råt.Replace("\r\n", "\n").TrimStart(), @"(?m)^---\s*$");
    var krop = dele.Length > 2 ? string.Join("\n", dele.Skip(2)).Trim() : råt.Trim();

    string Hent(string felt)
    {
        var m = System.Text.RegularExpressions.Regex.Match(råt, $@"(?m)^{felt}:\s*(.+)$");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    var titel = a.Length > 1 ? a[1] : Path.GetFileNameWithoutExtension(a[0]);
    var skabelon = Hent("skabelon");
    var model = Hent("model");

    var info = new NoteApp.Core.Documents.DocumentInfo
    {
        Title = titel,
        Template = skabelon.Length > 0 ? skabelon : "(ukendt)",
        Model = model.Length > 0 ? model : "(ukendt)",
        SourceRecording = Path.GetDirectoryName(Path.GetFullPath(a[0])) ?? "",
        SourceTitle = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(a[0])))) ?? "",
        Markdown = krop,
        FileName = NoteApp.Core.Documents.DocumentStore.FileNameFor(titel, skabelon)
    };

    var sti = NoteApp.Core.Documents.DocumentStore.Save(info);
    Console.WriteLine($"Gemt: {sti}");
    Console.WriteLine($"      {new FileInfo(sti).Length:N0} byte · åbnes i Word, LibreOffice og Google Docs");
    return 0;
}

/// <summary>
/// Hvor tæt er udskriften på manuskriptet?
///
/// Det er den eneste rigtige måling appen har: ved en oplæsning findes facit.
/// Tallet er dét, der skal flytte sig, når ordbogen bliver bedre.
/// </summary>
static int Score(string[] a)
{
    if (a.Length < 2)
    {
        Console.WriteLine("Brug: noteapp score <manuskript.md> <udskrift.txt> [--alle]");
        return 0;
    }

    if (!File.Exists(a[0]) || !File.Exists(a[1]))
    {
        Console.Error.WriteLine("Én af filerne findes ikke.");
        return 1;
    }

    var manus = NoteApp.Core.ReadAloudScore.ManuskriptTekst(
        File.ReadAllText(a[0], System.Text.Encoding.UTF8));

    var (ialt, ramt, afvigelser) = NoteApp.Core.ReadAloudScore.Sammenlign(
        manus, File.ReadAllText(a[1], System.Text.Encoding.UTF8));

    if (ialt == 0) { Console.Error.WriteLine("Manuskriptet gav ingen ord."); return 1; }

    Console.WriteLine($"Ord i manuskriptet : {ialt}");
    Console.WriteLine($"Ramt               : {ramt}  ({100.0 * ramt / ialt:0.0} %)");
    Console.WriteLine($"Afvigelser         : {afvigelser.Count}");

    if (a.Contains("--alle"))
    {
        Console.WriteLine();
        foreach (var f in afvigelser)
            Console.WriteLine($"  {f.Forventet,-28} → {(f.Hørt.Length == 0 ? "(manglede)" : f.Hørt)}");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("De 20 første:");
        foreach (var f in afvigelser.Take(20))
            Console.WriteLine($"  {f.Forventet,-28} → {(f.Hørt.Length == 0 ? "(manglede)" : f.Hørt)}");
    }

    return 0;
}

/// <summary>
/// Optager nogle få sekunder og siger, hvad der kom ud.
///
/// To spørgsmål besvares: kom der lyd, og kom der lyd nok TID. Det sidste er
/// ikke selvfølgeligt — en fejl i optagerens pumpe gjorde syv sekunders tale
/// til halvandet sekunds fil, og det så ud som om Whisper hørte forkert.
/// </summary>
static int Mikrofontest(string[] a)
{
    var sekunder = a.Length > 0 && double.TryParse(a[0], out var s) ? s : 5.0;

    var mik = AudioDevices.ResolveMicrophone(AppSettings.Current.MicrophoneId, out var faldtTilbage);
    if (mik is null) { Console.Error.WriteLine("Ingen mikrofon fundet."); return 1; }

    Console.WriteLine($"Mikrofon : {mik.FriendlyName}{(faldtTilbage ? "  (IKKE den valgte — faldt tilbage)" : "")}");
    Console.WriteLine($"Optager  : {sekunder:0.0} sekunder — sig noget nu …");

    var fil = Path.Combine(Path.GetTempPath(), $"noteapp-miktest-{Guid.NewGuid():N}.wav");
    var optager = new ShortClipRecorder(fil);

    optager.Start(mik.Id);
    var ur = System.Diagnostics.Stopwatch.StartNew();

    var top = 0f;
    while (ur.Elapsed.TotalSeconds < sekunder)
    {
        top = Math.Max(top, optager.Niveau);
        Thread.Sleep(100);
    }

    var længde = optager.Stop();
    optager.Dispose();

    var bytes = new FileInfo(fil).Length;

    Console.WriteLine();
    Console.WriteLine($"Filen    : {bytes:N0} byte = {længde:0.0} sekunder");
    Console.WriteLine($"Højeste niveau: {top * 100:0.0} % af fuld skala");
    Console.WriteLine();

    // Det er FORHOLDET, der afgoer, om pumpen virker. Er filen kortere end
    // den tid, der blev optaget, er der tabt lyd — og saa er det ikke
    // Whisper, der hoerer forkert.
    var andel = længde / sekunder;
    if (andel < 0.9)
        Console.WriteLine($"FEJL: kun {andel * 100:0} % af tiden blev skrevet til filen. Der tabes lyd.");
    else
        Console.WriteLine($"Længden passer ({andel * 100:0} % af den optagne tid).");

    if (top < 0.01)
        Console.WriteLine("ADVARSEL: der kom stort set ingen lyd. Er det den rigtige mikrofon?");

    Console.WriteLine($"Klippet : {fil}");
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
