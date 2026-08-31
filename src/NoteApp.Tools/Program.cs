using NoteApp.Core;
using NoteApp.Core.Llm;

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
        "motor"     => Motor(),
        "hent"      => await Hent(args.Skip(1).ToArray()),
        "hentmotor" => await HentMotor(args.Skip(1).ToArray()),
        "sprogmodel" => await Sprogmodel(args.Skip(1).ToArray()),
        "udkast"    => await Udkast(args.Skip(1).ToArray()),
        "gendan"    => Gendan(args.Skip(1).ToArray()),
        "transskriber" => await Transskriber(args.Skip(1).ToArray()),
        "dokument"  => Dokument(args.Skip(1).ToArray()),
        "sky"       => await Sky(args.Skip(1).ToArray()),
        "forventning" => Forventning(args.Skip(1).ToArray()),
        "mikrofontest" => Mikrofontest(args.Skip(1).ToArray()),
        "maalsoegning" => Maalsoegning(),
        "maaldato"  => Maaldato(),
        "google"    => await Google(args.Skip(1).ToArray()),
        "opgaver"   => Opgaver(),
        "lydproeve" => Lydproeve(args.Skip(1).ToArray()),
        "diktat"    => await Diktat(args.Skip(1).ToArray()),
        "plads"     => Plads(args.Skip(1).ToArray()),
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
        heypia <kommando>

          status    Viser hvor dine data ligger og hvad de indeholder
          init      Opretter databasen til dine rettelser
          motor     Viser hvilken Whisper-motor og model der er i brug
          transskriber  Transskriberer en optagelse:
                      heypia transskriber <mappe-eller-wav> [--cpu]
          recover   Samler møder der aldrig blev lukket ordentligt
          maalsoegning  Kører de tyve søgeprøver med kendt facit
          maaldato  Måler datoforståelsen mod kendte svar
          google    Efterprøver Google Kalender-forbindelsen hele vejen:
                      heypia google [dage]
          opgaver   Viser alle opgaver og hvor de kom fra
          diktat    Koerer et lydklip gennem hele dikteringen:
                      heypia diktat <wav> [note|mail|prompt|opgave]
          lydproeve Komprimerer en optagelse og pakker den ud igen:
                      heypia lydproeve <wav> [kbit ...]
          plads     Viser hvad lyden fylder, og hvad der kan ryddes:
                      heypia plads [--ryd] [--dage N]
          udkast    Laver et referat med en lokal model — intet forlader maskinen
          sky       Laver et referat hos en europæisk leverandør:
                      SENDER UDSKRIFTEN UD AF MASKINEN. Se «heypia sky».

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
    Console.WriteLine();

    var møder = Directory.Exists(UserDataPaths.Meetings)
        ? Directory.GetDirectories(UserDataPaths.Meetings).Length
        : 0;
    Console.WriteLine($"Møder gemt  : {møder}");


    var efterladte = SessionRecovery.Scan();
    if (efterladte.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{efterladte.Count} møde(r) blev aldrig lukket ordentligt. Kør 'heypia recover'.");
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

    // Her blev ordbogsdatabasen oprettet. Baade ordlisten til Whisper og
    // ordbogen til sprogmodellen er maalt til nul effekt og fjernet
    // 18-08-2026 - se doc/maaling-sky.md. learning.db bliver liggende paa
    // disken, hvis den er der; den bliver bare ikke laest laengere.

    Console.WriteLine($"Datamappe klar: {UserDataPaths.Root}");
    return 0;
}

// HER LÅ «tilfoej», «eksport» OG «prompt».
//
// De tre kommandoer hørte til ordlisten: læg et ord ind, skriv listen ud til
// ordliste.txt, se den prompt Whisper fik. Prompten er målt til NUL forskel og
// er fjernet fra appen — så kommandoerne styrede noget, der ikke længere
// findes.
//
// «rettelser» viser i stedet det, der virker.

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
        Console.WriteLine("Hent med: heypia sprogmodel <id>");
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
///   heypia udkast &lt;mappe-eller-tekstfil&gt; [skabelon] [model.gguf]
/// </summary>
static async Task<int> Udkast(string[] a)
{
    NoteApp.Core.Llm.DraftStore.SeedTemplates();

    var cli = NoteApp.Core.Llm.LlmRunner.FindCli();
    if (cli is null) { Console.Error.WriteLine("llama-cli.exe er ikke hentet endnu."); return 1; }

    var modeller = NoteApp.Core.Llm.LlmRunner.InstalledModels();
    if (modeller.Count == 0) { Console.Error.WriteLine("Ingen sprogmodel hentet. Kør 'heypia sprogmodel'."); return 1; }

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
        Console.WriteLine("Brug: heypia udkast <mappe-eller-tekstfil> [skabelon] [model.gguf]");
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

    var felter = new Dictionary<string, string?>
    {
        ["transskription"] = tekst,
        ["titel"] = titel,
        ["dato"] = DateTime.Now.ToString("d. MMMM yyyy"),
        ["varighed"] = "",
        ["noter"] = "",
        ["sprog"] = sprog,
        // De rigtige stavemaader fra dine rettelser. Den gaar til
        // SPROGMODELLEN, ikke til Whisper.
        ["ordbog"] = ""
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

    // Samme vej som appen: Referatbygger deler lange moeder op, saa de kan
    // ligge paa grafikkortet. Koerer CLI'en noget andet end appen, maaler man
    // paa noget, brugeren ikke faar.
    var bygger = new NoteApp.Core.Llm.Referatbygger(runner);
    var r = await bygger.ByggAsync(model, skabelon, felter, fremdrift);

    var sti = NoteApp.Core.Llm.DraftStore.Save(moedeMappe, skabelon, r);

    Console.WriteLine();
    Console.WriteLine($"Tid      : {r.Elapsed.TotalSeconds:0.0} sek");
    Console.WriteLine($"Tokens   : {r.PromptTokens} ind, {r.ResponseTokens} ud  ({r.TokensPerSecond:0.0}/sek)");
    Console.WriteLine($"Gemt     : {sti}");
    return 0;
}

/// <summary>
/// Kører en skabelon hos en europæisk sky-model.
///
///   heypia sky                                    — hvad der er sat op
///   heypia sky modeller                           — de id'er, nøglen kan bruge
///   heypia sky referat &lt;mappe-eller-fil&gt; &lt;model&gt; [skabelon]
///
/// HVORFOR DEN LIGGER I EN EGEN KOMMANDO
///
/// «heypia udkast» lover, at intet forlader maskinen. Den kommando skal blive
/// ved med at betyde det. At sende udskriften afsted er et andet valg, og det
/// skal man skrive et andet ord for — ikke sætte et flag på det, man plejer
/// at køre.
/// </summary>
static async Task<int> Sky(string[] a)
{
    var underkommando = a.Length > 0 ? a[0].ToLowerInvariant() : "status";

    var noegle = NoteApp.Core.Llm.SkyNoegle.Hent();

    if (underkommando is "status")
    {
        Console.WriteLine("SKY — bearbejdning hos en europæisk leverandør");
        Console.WriteLine();
        Console.WriteLine($"  Nøgle    : {(noegle is null ? "mangler" : "fundet (" + noegle.Length + " tegn)")}");

        // Endepunktet skal staa her. Det er den ene oplysning, hele
        // EU-paastanden hviler paa, og forskellen mellem api.eu.mistral.ai og
        // api.mistral.ai kan ikke ses paa noget andet.
        Console.WriteLine($"  Endepunkt: {NoteApp.Core.Llm.SkyKatalog.Endpoint}");
        Console.WriteLine($"             EU-bundet. Priserne nedenfor er med " +
                          $"{(NoteApp.Core.Llm.SkyKatalog.EuTillaeg - 1) * 100:0}% EU-tillæg.");

        // SPAERRINGEN EFTERPROEVES, den vises ikke bare som en paastand.
        // De to adresser her er dem, der ville vaere lette at komme til at
        // bruge: den globale staar i alle kodeeksempler, og den amerikanske
        // ligner den europaeiske paa eet bogstav.
        foreach (var forsoeg in new[] { "https://api.mistral.ai/v1/models",
                                        "https://api.us.mistral.ai/v1/models" })
        {
            try
            {
                NoteApp.Core.Llm.SkyKatalog.KraevEuropa(forsoeg);
                Console.WriteLine($"  ADVARSEL : {new Uri(forsoeg).Host} blev IKKE afvist. Spærringen virker ikke.");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"  Spærret  : {new Uri(forsoeg).Host}");
            }
        }
        Console.WriteLine();
        Console.WriteLine("Modeller:");
        foreach (var m in NoteApp.Core.Llm.SkyKatalog.Kendte)
            Console.WriteLine($"  {m.Id,-16} {m.Navn,-20} ${m.PrisIndPrMTok}/${m.PrisUdPrMTok} pr. MTok" +
                              (m.Standard ? "   <- standard" : ""));
        Console.WriteLine();

        if (noegle is null) Console.WriteLine(NoteApp.Core.Llm.SkyNoegle.Vejledning);
        else Console.WriteLine("Brug: heypia sky referat <mappe-eller-tekstfil> <model> [skabelon]");

        return 0;
    }

    if (noegle is null) { Console.Error.WriteLine(NoteApp.Core.Llm.SkyNoegle.Vejledning); return 1; }

    var sky = new NoteApp.Core.Llm.SkyRunner(noegle);

    // Model-id'erne staar ikke i den offentlige dokumentation. Frem for at
    // gaette og faa en 422'er, der ikke siger hvorfor, kan listen hentes.
    if (underkommando is "modeller")
    {
        Console.WriteLine("Spørger Mistral, hvilke modeller nøglen kan bruge ...");
        Console.WriteLine();
        foreach (var id in await sky.ModellerAsync()) Console.WriteLine($"  {id}");
        return 0;
    }

    if (underkommando is not "referat" || a.Length < 2)
    {
        Console.Error.WriteLine("Brug: heypia sky referat <mappe-eller-tekstfil> [model] [skabelon]");
        return 1;
    }

    // Uden et modelnavn bruges standarden. Maalingerne skal kunne koere paa en
    // bestemt model, men den daglige brug skal ikke skulle vide, hvilken.
    var model = a.Length > 2
        ? NoteApp.Core.Llm.SkyKatalog.Find(a[2])
        : NoteApp.Core.Llm.SkyKatalog.Standard;
    if (model is null)
    {
        Console.Error.WriteLine($"Ukendt model: {a[2]}");
        Console.Error.WriteLine("Kendte: " + string.Join(", ", NoteApp.Core.Llm.SkyKatalog.Kendte.Select(m => m.Id)));
        return 1;
    }

    NoteApp.Core.Llm.DraftStore.SeedTemplates();

    // Kilden: enten en moedemappe eller en ren tekstfil. Samme regler som
    // «heypia udkast», saa de to kan sammenlignes paa det samme materiale.
    string tekst, moedeMappe, titel;
    if (Directory.Exists(a[1]))
    {
        moedeMappe = a[1];
        var txt = Directory.GetFiles(moedeMappe, "*.txt")
            .Where(f => !Path.GetFileName(f).Contains("gaet", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        if (txt is null) { Console.Error.WriteLine("Fandt ingen transskription (.txt) i mappen."); return 1; }
        tekst = File.ReadAllText(txt);
        titel = MeetingStore.Load(moedeMappe)?.Title ?? Path.GetFileName(moedeMappe);
    }
    else if (File.Exists(a[1]))
    {
        tekst = File.ReadAllText(a[1]);
        moedeMappe = Path.GetDirectoryName(Path.GetFullPath(a[1]))!;
        titel = Path.GetFileNameWithoutExtension(a[1]);
    }
    else { Console.Error.WriteLine($"Findes ikke: {a[1]}"); return 1; }

    var skabeloner = NoteApp.Core.Llm.PromptTemplate.LoadAll();
    if (skabeloner.Count == 0) { Console.Error.WriteLine($"Ingen skabeloner i {NoteApp.Core.Llm.PromptTemplate.Directory}"); return 1; }

    var skabelon = a.Length > 3 && !a[3].StartsWith("--")
        ? skabeloner.FirstOrDefault(s => s.Name.Contains(a[3], StringComparison.OrdinalIgnoreCase)) ?? skabeloner[0]
        : skabeloner[0];


    var felter = new Dictionary<string, string?>
    {
        ["transskription"] = tekst,
        ["titel"] = titel,
        ["dato"] = DateTime.Now.ToString("d. MMMM yyyy"),
        ["varighed"] = "",
        ["noter"] = "",
        ["sprog"] = "dansk",
        ["ordbog"] = ""
    };

    // DET HER SKAL STAA, HVER GANG. Kommandoen sender moedeudskriften til en
    // server i Frankrig, og det er ikke noget, man skal kunne komme til at
    // glemme, fordi man har koert den foer.
    Console.WriteLine("SENDES UD AF MASKINEN");
    Console.WriteLine($"  Modtager : {model.Leverandoer}, {model.Hjemland} ({model.Navn})");
    Console.WriteLine($"  Indhold  : hele transkriptionen, {tekst.Length:N0} tegn");
    Console.WriteLine($"  Skabelon : {skabelon.Name}");
    Console.WriteLine();

    var sidst = "";
    var fremdrift = new Progress<NoteApp.Core.Llm.LlmProgress>(p =>
    {
        if (p.Message != sidst) { sidst = p.Message; Console.WriteLine($"  {p.Message}"); }
    });

    // HELE MOEDET I EEN KOERSEL. Referatbygger deler op, fordi et 6 GB-kort
    // ikke kan rumme mere — det er en noedloesning, ikke en fordel, og den
    // kostede os deltagernavnene. En sky-model har plads til hele moedet, og
    // saa er der ingen grund til at klippe det i stykker.
    var r = await sky.KoerAsync(model, skabelon, skabelon.Render(felter), fremdrift);

    var sti = NoteApp.Core.Llm.DraftStore.Save(
        moedeMappe, skabelon, r.SomLlmResult(), motor: $"{model.Leverandoer} ({model.Hjemland})");

    Console.WriteLine();
    Console.WriteLine($"Tid      : {r.Forloebet.TotalSeconds:0.0} sek");
    Console.WriteLine($"Tokens   : {r.TokensInd:N0} ind, {r.TokensUd:N0} ud  ({r.TokensPrSekund:0.0}/sek)");
    Console.WriteLine($"Pris     : €{r.PrisEur:0.0000}  (ca. {r.PrisEur * 7.46m:0.00} kr.)");
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
    var udgivelse = await EngineInstaller.FetchAsync();

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
        Console.WriteLine("Hent med: heypia hentmotor <filnavn>   (eller 'anbefalet')");
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
        Console.Error.WriteLine("Brug: heypia gendan <arkiv.zip> [--foralvor]");
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
        Console.Error.WriteLine("Brug: heypia hent <model>");
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
        Console.Error.WriteLine("Brug: heypia transskriber <mappe-eller-wav> [--cpu]");
        return 2;
    }

    var s = WhisperInstall.Locate();
    if (!s.IsComplete)
    {
        Console.Error.WriteLine("Motor eller model mangler. Kør 'heypia motor' for at se hvad.");
        return 1;
    }

    // Peges der på en mappe, tages mikrofonsporet — det er det spor, der
    // altid findes, ogsaa ved fysiske moeder uden loopback.
    // Peges der paa en mappe, tages mikrofonsporet — og findes det ikke, er
    // det et webinar, hvor hoejttalersporet er det eneste, der blev optaget.
    var input = a[0];
    var wav = input;

    if (Directory.Exists(input))
    {
        wav = Path.Combine(input, "mikrofon.wav");
        if (!File.Exists(wav)) wav = Path.Combine(input, "loopback.wav");
    }

    if (!File.Exists(wav)) { Console.Error.WriteLine($"Findes ikke: {wav}"); return 1; }

    var udBase = Path.Combine(Path.GetDirectoryName(wav)!,
        Path.GetFileNameWithoutExtension(wav) + "_" +
        Path.GetFileNameWithoutExtension(s.ModelPath!).Replace("ggml-", ""));

    Console.WriteLine($"Lyd     : {wav} ({Transcriber.WavSeconds(wav):0.0} sek)");
    Console.WriteLine($"Model   : {s.ModelFileName}  ({s.Engine})");
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

    // --sprog da laaser sproget. Uden det bruges DET SPROG, DER BLEV VALGT,
    // DA DER BLEV OPTAGET - og kun hvis der ikke er valgt noget, finder
    // Whisper det selv.
    //
    // DET VAR EN RIGTIG FEJL. Vaerktoejet gaettede altid selv, ogsaa naar
    // brugeren havde valgt engelsk i opstartsdialogen. Paa et engelsk moede
    // med lange stille stykker i mikrofonsporet gaettede den NORSK med 66 %
    // sikkerhed og skrev «Teksting av Nicolai Winther» ud af stilheden.
    //
    // Den samme optagelse gav altsaa to forskellige resultater alt efter,
    // hvilken doer man kom ind ad. Et valg, brugeren har truffet, skal gaelde
    // begge veje. Fundet 24-08-2026.
    var i = Array.IndexOf(a, "--sprog");

    var ønsketSprog = i >= 0 && i + 1 < a.Length
        ? a[i + 1]
        : Valgtsprog(Directory.Exists(input) ? input : Path.GetDirectoryName(input)) ?? "auto";

    if (i < 0 && ønsketSprog != "auto")
        Console.WriteLine($"Sprog     : {ønsketSprog} — valgt da der blev optaget");

    var r = await motor.RunAsync(
        new TranscriptionRequest(wav, s.ModelPath!, udBase, ønsketSprog, "", a.Contains("--cpu")),
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
    var moedemappe = Directory.Exists(input) ? input : Path.GetDirectoryName(wav)!;
    var meta = MeetingStore.Load(moedemappe);

    if (meta is not null)
    {
        meta.Language = r.DetectedLanguage;
        meta.LanguageProbability = r.LanguageProbability;

        // Det valgte sprog huskes paa det spor, det gjaldt. Uden det ville
        // appen skrive hele optagelsen ud igen, naeste gang der blev trykket
        // paa knappen — den kan kun genbruge en koersel, den kan genkende.
        if (ønsketSprog != "auto")
        {
            if (Path.GetFileNameWithoutExtension(wav) == "loopback") meta.ValgtSprogLoop = ønsketSprog;
            else meta.ValgtSprogMik = ønsketSprog;
        }

        MeetingStore.Save(moedemappe, meta);
    }

    Console.WriteLine($"Tekst     : {r.TextPath}");

    // ============ ÉT SPOR SAMLES HELT FÆRDIGT ============
    //
    // Whisper efterlader en json og en txt pr. spor. Det, appen viser, er en
    // ANDEN fil: replikkerne flettet, med stemmerne skilt ad og navnene sat
    // paa. Uden det trin er en koersel her kun det halve arbejde, og resten
    // skal goeres i appen alligevel.
    //
    // KUN NAAR DER ER ÉT SPOR. Har moedet baade mikrofon og hoejttaler, er
    // fletningen af de to det egentlige arbejde, og en udskrift bygget paa det
    // ene spor ville se faerdig ud og mangle halvdelen af samtalen. Det er
    // vaerre end ingen udskrift.
    var beggeSpor = File.Exists(Path.Combine(moedemappe, "mikrofon.wav"))
                    && File.Exists(Path.Combine(moedemappe, "loopback.wav"));

    if (beggeSpor)
    {
        Console.WriteLine();
        Console.WriteLine("Mødet har to spor. Kør det færdigt i appen — dér skrives begge ud");
        Console.WriteLine("og flettes til én transkription.");
        return 0;
    }

    var modelNavn = Path.GetFileNameWithoutExtension(s.ModelPath!).Replace("ggml-", "");
    var replikker = Samtale.Flet(r.JsonPath, null);
    var udskrift = Udskrift.Af(replikker);

    if (Diarisering.ErInstalleret)
    {
        Console.WriteLine();
        Console.Write("Skiller stemmerne ad …");

        try
        {
            var talere = await Diarisering.KoerAsync(wav, "", null, CancellationToken.None);
            if (talere is not null)
            {
                Diarisering.Gem(moedemappe, talere);
                var sat = Diarisering.Anvend(udskrift, talere);
                Console.WriteLine($" {talere.Stemmer} stemmer, sat på {sat} replikker");
            }
            else Console.WriteLine(" ingen stemmer fundet");
        }
        catch (Exception ex)
        {
            // Uden navne, som foer. En udskrift uden talere er ikke daarlig,
            // den er bare ikke bedre.
            Console.WriteLine($" sprunget over ({ex.Message})");
        }
    }

    udskrift.GemMaskin(moedemappe, modelNavn);

    var udskriftSti = Path.Combine(moedemappe, $"udskrift_{modelNavn}.txt");
    File.WriteAllText(udskriftSti, udskrift.SomTekst(meta?.Talere),
                      new System.Text.UTF8Encoding(false));

    Console.WriteLine($"Transkription  : {udskriftSti}");

    // KLOKKEN SKAL RINGE — OGSAA NAAR KOERSLEN KOM HERFRA.
    //
    // Historikken er appens eneste kilde til notifikationer. Skrives linjen
    // ikke, staar en faerdig udskrift der uden at nogen faar det at vide, og
    // saa er forskellen paa at koere det her og at koere det i appen ikke
    // hastighed, men om man opdager, at det er faerdigt.
    Historik.Skriv(
        HaendelseType.Transskription,
        "Transskription færdig",
        $"{TimeSpan.FromSeconds(r.AudioSeconds):hh\\:mm\\:ss} lyd · " +
        $"sprog {Transcriber.LanguageName(r.DetectedLanguage)}" +
        (r.LanguageProbability is double p4 ? $" ({p4 * 100:0}% sikker)" : " (valgt)") +
        $" · RTF {r.RealTimeFactor:0.00}",
        Udfald.Fuldført,
        r.EngineId, udskriftSti, r.ElapsedSeconds,
        kilde: meta?.Id.ToString() ?? "");

    return 0;
}

/// <summary>
/// Datoforståelsen målt mod kendte svar.
///
/// Der regnes fra ONSDAG DEN 26. AUGUST 2026 — en fast dag, så prøven giver
/// det samme svar hver gang den køres. En måling, der afhænger af, hvornår
/// den blev kørt, kan ikke sammenlignes med sig selv.
///
/// Dagen er valgt midt i ugen med vilje: «på fredag» og «i næste uge» er
/// først forskellige, når man ikke står på en mandag.
/// </summary>
static int Maaldato()
{
    var idag = new DateOnly(2026, 8, 26);   // onsdag

    var proever = new (string Tekst, string Ventet)[]
    {
        // --- entydige ---
        ("Jeg sender det i morgen", "27-08-2026"),
        ("Vi ses i overmorgen", "28-08-2026"),
        ("Det er klart på fredag", "28-08-2026"),
        ("Kan du nå det på mandag?", "31-08-2026"),
        ("Jeg vender tilbage på onsdag", "02-09-2026"),
        ("Om to uger har vi svaret", "09-09-2026"),
        ("Om 3 dage er den klar", "29-08-2026"),
        ("Vi tager den om en måned", "26-09-2026"),
        ("Fristen er den 1. september", "01-09-2026"),
        ("Det skal være klar 15. september", "15-09-2026"),
        ("Deadline er den 15.", "15-09-2026"),
        ("Inden månedens udgang", "31-08-2026"),
        ("Vi lukker det sidst på måneden", "31-08-2026"),
        ("Det er den 1. marts", "01-03-2027"),      // passeret i aar -> naeste aar

        // --- usikre, men brugbare ---
        ("Vi kigger på det i næste uge", "31-08-2026"),
        ("Det klarer vi i denne uge", "24-08-2026"),

        // --- der er INGEN dato her ---
        ("Vi tager den når Anders er tilbage", ""),
        ("Det koster 15 kroner", ""),
        ("Vi var 12 til mødet", ""),
        ("Jeg ringer efter sommerferien", "")
    };

    int rigtige = 0, forkerte = 0, manglende = 0, falske = 0;

    Console.WriteLine($"Der regnes fra {idag:dddd d. MMMM yyyy}");
    Console.WriteLine();
    Console.WriteLine($"{"sagt",-44} {"forstået",12} {"ventet",12}  ");
    Console.WriteLine(new string('-', 78));

    foreach (var p in proever)
    {
        var f = Datoforstaaelse.Find(p.Tekst, idag);
        var fik = f is null ? "" : f.Dato.ToString("dd-MM-yyyy");
        var ok = fik == p.Ventet;

        if (ok) rigtige++;
        else if (p.Ventet.Length == 0) falske++;      // fandt en dato, der ikke var der
        else if (fik.Length == 0) manglende++;        // overså en dato
        else forkerte++;                              // forstod den forkert

        var maerke = ok ? "ok" : p.Ventet.Length == 0 ? "FALSK" : fik.Length == 0 ? "OVERSET" : "FORKERT";

        Console.WriteLine($"{p.Tekst,-44} {(fik.Length == 0 ? "-" : fik),12} " +
                          $"{(p.Ventet.Length == 0 ? "-" : p.Ventet),12}  {maerke}" +
                          (f is { Sikker: false } ? "  (usikker)" : ""));
    }

    var n = proever.Length;

    Console.WriteLine(new string('-', 78));
    Console.WriteLine();
    Console.WriteLine($"Rigtige            : {rigtige} af {n}  ({rigtige * 100.0 / n:0} %)");
    Console.WriteLine($"Forstået forkert   : {forkerte}");
    Console.WriteLine($"Overset            : {manglende}");
    Console.WriteLine($"Fandt en dato, der ikke var der : {falske}");
    Console.WriteLine();
    Console.WriteLine(falske > 0
        ? "EN FALSK DATO ER DEN DYRE FEJL. Den bliver til en frist, ingen har sat."
        : "Ingen falske datoer. En overset frist opdages; en opfundet gør ikke.");

    return forkerte == 0 && falske == 0 ? 0 : 1;
}


/// <summary>
/// De tyve søgeprøver med kendt facit.
///
/// HVORFOR DE LIGGER I KODEN OG IKKE I ET REGNEARK
///
/// Tallet skal kunne slås op igen, hver gang søgningen røres. En måling, der
/// kræver, at nogen husker, hvad der blev spurgt om, bliver lavet én gang.
///
/// FACIT EFTERPRØVES FØR MÅLINGEN. Hver prøve siger, hvilken optagelse svaret
/// står i, og der kontrolleres først, at ordene rent faktisk står dér. Er
/// facit forkert, er målingen værdiløs — så hellere opdage det med det samme.
///
/// Prøverne bygger på DE OPTAGELSER, DER LÅ 21-08-2026. Ligger de ikke
/// længere, siger kontrollen det, og målingen skal skrives om frem for at
/// blive rettet, til den passer.
/// </summary>
static int Maalsoegning()
{
    const string cloud = "Møde med Cloudworks";
    const string elimity = "Webinar 21. august kl. 12:10";
    const string indigo = "Webinar 21. august kl. 15:31";

    var proever = new (string Spoergsmaal, string Facit)[]
    {
        ("omada", cloud),
        ("sailpoint", cloud),
        ("crowdstrike", cloud),
        ("gartner", cloud),
        ("hubspot", cloud),
        ("beyond trust", cloud),
        ("one identity", cloud),
        ("espen omada", cloud),
        ("linkedin", cloud),
        ("webinar", cloud),

        ("orphaned accounts", elimity),
        ("segregation of duties", elimity),
        ("iso 27001", elimity),
        ("maarten", elimity),
        ("access review", elimity),

        ("identity chaos", indigo),
        ("power bi", indigo),
        ("rbac", indigo),
        ("illimiti", indigo),
        ("data-driven", indigo)
    };

    // ---- 1. er facit rigtigt?
    var tekster = new Dictionary<string, string>();

    foreach (var m in MeetingStore.Alle())
    {
        var mappe = MeetingStore.FindById(m.Id.ToString())?.Mappe;
        if (mappe is null) continue;

        var txt = Directory.GetFiles(mappe, "udskrift_*.txt").FirstOrDefault();
        if (txt is not null) tekster[m.Title ?? ""] = File.ReadAllText(txt, System.Text.Encoding.UTF8);
    }

    var facitfejl = 0;

    foreach (var p in proever)
    {
        if (!tekster.TryGetValue(p.Facit, out var t))
        {
            Console.WriteLine($"MANGLER: optagelsen «{p.Facit}» ligger ikke her");
            facitfejl++;
            continue;
        }

        var mangler = Soegning.Del(p.Spoergsmaal)
            .Where(o => !t.Contains(o, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mangler.Count > 0)
        {
            Console.WriteLine($"FACIT FORKERT: «{p.Spoergsmaal}» — {string.Join(", ", mangler)} " +
                              $"står ikke i {p.Facit}");
            facitfejl++;
        }
    }

    if (facitfejl > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{facitfejl} fejl i facit. Målingen kan ikke bruges, før de er rettet.");
        return 1;
    }

    Console.WriteLine($"Facit efterprøvet: alle {proever.Length} ord står i den optagelse, prøven peger på.");
    Console.WriteLine();

    // ---- 2. målingen
    int top1 = 0, top3 = 0, væk = 0;
    long ms = 0;

    Console.WriteLine($"{"spørgsmål",-26} {"plads",6} {"kilder",7} {"steder",7} {"ms",5}");
    Console.WriteLine(new string('-', 56));

    foreach (var p in proever)
    {
        var ur = System.Diagnostics.Stopwatch.StartNew();
        var fund = Soegning.Soeg(p.Spoergsmaal);
        ur.Stop();

        ms += ur.ElapsedMilliseconds;

        var plads = fund.FindIndex(f => f.Overskrift == p.Facit) + 1;

        if (plads == 1) top1++;
        if (plads is >= 1 and <= 3) top3++;
        if (plads == 0) væk++;

        Console.WriteLine($"{p.Spoergsmaal,-26} {(plads == 0 ? "IKKE" : plads.ToString()),6} " +
                          $"{fund.Count,7} {fund.Sum(f => f.Traef.Count),7} {ur.ElapsedMilliseconds,5}");
    }

    var n = proever.Length;

    Console.WriteLine(new string('-', 56));
    Console.WriteLine();
    Console.WriteLine($"Facit på førstepladsen : {top1} af {n}  ({top1 * 100.0 / n:0} %)");
    Console.WriteLine($"Facit i top tre        : {top3} af {n}  ({top3 * 100.0 / n:0} %)");
    Console.WriteLine($"Slet ikke fundet       : {væk}");
    Console.WriteLine($"Tid i gennemsnit       : {ms / (double)n:0.0} ms");

    return væk == 0 ? 0 : 1;
}


/// <summary>
/// Laver et Word-dokument ud af et udkast, der allerede findes.
///
/// Formatet er .docx. Modtageren af et mødereferat åbner det i Word, og et
/// dokument, der er konverteret på vej ind, taber det, konverteringen ikke kan
/// oversætte. LibreOffice og Google Docs læser det også.
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
        Console.WriteLine("Lav et: heypia dokument <udkast.md> [titel]");
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
    Console.WriteLine($"      {new FileInfo(sti).Length:N0} byte · Word-dokument");
    return 0;
}

/// <summary>
/// Hvor mange blokke et møde deles i, og hvor lang tid det cirka tager.
///
/// Findes for at kunne efterprøve tallet, appen viser, FØR man sætter en kørsel
/// i gang. Et skøn, der kun kan ses ét sted, kan man ikke opdage er forkert.
/// </summary>
static int Forventning(string[] a)
{
    if (a.Length == 0)
    {
        Console.WriteLine("Brug: heypia forventning <mappe-eller-tekstfil>");
        return 0;
    }

    var fil = Directory.Exists(a[0])
        ? Directory.GetFiles(a[0], "*.txt")
            .Where(f => !f.EndsWith(".raa.txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        : a[0];

    if (fil is null || !File.Exists(fil)) { Console.Error.WriteLine("Fandt ingen tekst."); return 1; }

    var tekst = File.ReadAllText(fil, System.Text.Encoding.UTF8);
    var (blokke, skoen) = NoteApp.Core.Llm.Referatbygger.Forventning(tekst);

    Console.WriteLine($"Tekst   : {tekst.Length} tegn (~{tekst.Length / 3} tokens)");
    Console.WriteLine($"Blokke  : {blokke}");
    Console.WriteLine($"Skoen   : {skoen.TotalMinutes:0} minutter");
    Console.WriteLine(blokke == 1
        ? "Koeres i een omgang — moedet er kort nok til at ligge i modellen paa een gang."
        : "Deles op, saa hver blok kan ligge helt paa grafikkortet.");

    // Grafikkortet lige nu. Er der optaget plads, koerer modellen delvist paa
    // processoren, og skoennet ovenfor holder ikke.
    Console.WriteLine();

    var kort = Grafikhukommelse.Laes();
    if (kort is null)
    {
        Console.WriteLine("Grafikkort: kan ikke aflaeses (ingen nvidia-smi).");
        return 0;
    }

    Console.WriteLine($"Grafikkort: {Grafikhukommelse.Gigabyte(kort.Value.Fri)} ledigt " +
                      $"af {Grafikhukommelse.Gigabyte(kort.Value.Ialt)}");

    var model = NoteApp.Core.Llm.LlmRunner.InstalledModels().FirstOrDefault();
    if (model is null) return 0;

    var plads = Grafikhukommelse.HarPlads(new FileInfo(model).Length);
    if (plads is null) return 0;

    Console.WriteLine($"Modellen kraever: {Grafikhukommelse.Gigabyte(plads.Value.Kraevet)}");
    Console.WriteLine(plads.Value.Plads
        ? "Der er plads — modellen kommer helt paa kortet."
        : "IKKE plads. En del af modellen lander paa processoren, og saa tager det " +
          "cirka fire gange saa lang tid. Luk video og andet, der bruger kortet.");

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

    var mik = Mikrofon.Valgt(out var faldtTilbage);
    if (mik is null) { Console.Error.WriteLine("Ingen mikrofon fundet."); return 1; }

    Console.WriteLine($"Mikrofon : {mik.FriendlyName}{(faldtTilbage ? "  (IKKE den valgte — faldt tilbage)" : "")}");
    Console.WriteLine($"Optager  : {sekunder:0.0} sekunder — sig noget nu …");

    var fil = Path.Combine(Path.GetTempPath(), $"heypia-miktest-{Guid.NewGuid():N}.wav");
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


// --------------------------------------------------------------- google
//
// EFTERPROEVER FORBINDELSEN HELE VEJEN uden at starte appen.
//
// Den findes, fordi integrationen ellers kun kan proeves ved at aabne
// programmet og trykke Forbind - og det kan man ikke, mens der optages.
// Her koeres praecis de samme to kald, appen bruger: ForbindAsync og
// HentAsync. Virker det her, virker knappen.
//
// Den GEMMER ingenting. Opdateringsnoeglen skrives ikke til noget, og
// indstillingerne roeres ikke. En proeve, der aendrer tilstand, er ikke en
// proeve - saa ved man bagefter ikke, om appen virker, eller om proeven
// efterlod noget.
static async Task<int> Google(string[] a)
{
    var dage = 14;
    if (a.Length > 0 && int.TryParse(a[0], out var d) && d > 0) dage = d;

    Console.WriteLine();
    Console.WriteLine("Google Kalender - efterproevning");
    Console.WriteLine();

    // 1. Er appen sat op?
    var klient = Googleklient.Hent();

    if (klient is null)
    {
        Console.WriteLine("IKKE SAT OP");
        Console.WriteLine();
        Console.WriteLine(Googleklient.Mangler);
        Console.WriteLine();
        Console.WriteLine($"Filen soeges her:  {Googleklient.Sti}");
        Console.WriteLine("Se doc/google-integration.md for de fire trin.");
        return 1;
    }

    var (id, hemmelighed) = klient.Value;

    // Kun halen af id'et. Hele id'et i en terminal ender i en skaermbillede
    // eller en logfil, og der er ingen grund til at det skal.
    var hale = id.Length > 24 ? "..." + id[^24..] : id;

    Console.WriteLine($"Klient-id   {hale}");
    Console.WriteLine($"Hemmelighed {(hemmelighed.Length > 0 ? "sat" : "MANGLER")}");
    Console.WriteLine();

    if (!id.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine("BEMAERK: id'et slutter normalt paa .apps.googleusercontent.com");

    // 2. Godkendelsen. Browseren aabner.
    Console.WriteLine("Browseren aabner nu. Log ind, og godkend adgang til kalenderen.");
    Console.WriteLine("Der bedes om ét omraade: calendar.events - aftaler, og intet");
    Console.WriteLine("andet i kontoen. Den her proeve LAESER kun.");
    Console.WriteLine();

    string noegle;

    try
    {
        noegle = await Googlekalender.ForbindAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("FORBINDELSEN LYKKEDES IKKE");
        Console.WriteLine($"  {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("De hyppigste aarsager:");
        Console.WriteLine("  access_denied      din konto staar ikke som testbruger paa");
        Console.WriteLine("                     samtykkeskaermen - tilfoej den dér");
        Console.WriteLine("  invalid_client     id eller hemmelighed passer ikke, eller");
        Console.WriteLine("                     klienten er ikke oprettet som «Desktop app»");
        Console.WriteLine("  Calendar API ...   API'et er ikke slaaet til paa projektet");
        return 1;
    }

    Console.WriteLine("GODKENDT. Opdateringsnoeglen er modtaget.");
    Console.WriteLine();

    // 3. Kan der rent faktisk hentes noget? En godkendelse, der ikke kan
    //    hente en aftale, er ikke en virkende integration.
    List<Aftale> aftaler;

    try
    {
        aftaler = await Googlekalender.HentAsync(noegle, dage);
    }
    catch (Exception ex)
    {
        Console.WriteLine("HENTNINGEN LYKKEDES IKKE");
        Console.WriteLine($"  {ex.Message}");
        return 1;
    }

    Console.WriteLine($"{aftaler.Count} aftale(r) i de naeste {dage} dage.");
    Console.WriteLine();

    foreach (var aft in aftaler.Take(20))
    {
        var naar = aft.Start.ToString("ddd dd-MM HH:mm");
        var hvor = aft.Link.Length > 0 ? "  [moedelink]"
                 : aft.Sted.Length > 0 ? $"  [{aft.Sted}]"
                 : "";

        Console.WriteLine($"  {naar}  {aft.Titel}{hvor}");
    }

    if (aftaler.Count > 20) Console.WriteLine($"  ... og {aftaler.Count - 20} mere");

    Console.WriteLine();

    if (aftaler.Count == 0)
    {
        Console.WriteLine("Ingen aftaler fundet. Det er ikke en fejl i sig selv -");
        Console.WriteLine("men laeg en aftale i kalenderen og koer igen, hvis du vil");
        Console.WriteLine("se, at der ogsaa kommer noget IND.");
    }
    else
    {
        Console.WriteLine("Integrationen virker hele vejen.");
    }

    Console.WriteLine();
    Console.WriteLine("Der er ikke gemt noget. Tryk Forbind i appen for at");
    Console.WriteLine("etablere forbindelsen dér.");

    return 0;
}


// -------------------------------------------------------------- opgaver
//
// Viser, hvad der ligger i opgavelageret, og hvor hver opgave kom fra.
//
// Den findes, fordi opgaver flyttede ud af optagelsernes mapper 24-08-2026.
// En flytning af brugerens data skal kunne EFTERPROEVES foer den slippes
// loes - ikke bare bygges og haabes paa.
static int Opgaver()
{
    var alle = Opgavelager.Alle();

    Console.WriteLine();
    Console.WriteLine($"Opgavelager: {Opgavelager.Fil}");
    Console.WriteLine($"{alle.Count} opgave(r)");
    Console.WriteLine();

    foreach (var o in alle.OrderByDescending(x => x.Oprettet))
    {
        var maerker = new List<string>();

        if (o.Faerdig) maerker.Add("faerdig");
        if (o.Prioritet > 0) maerker.Add("pri " + o.Prioritet);
        if (o.Deadline is { } d) maerker.Add(d.LocalDateTime.ToString("dd-MM-yyyy"));

        // HERKOMSTEN AFGOER DET, IKKE MOEDE-ID'ET.
        //
        // Her stod «skrevet i haanden» paa alt uden et moede - ogsaa paa de
        // opgaver, der lige var hentet fra Google. Det er den slags fejl, der
        // faar en til at tro, at integrationen ikke virkede.
        maerker.Add(o.Herkomst == Opgavekilde.Google
            ? $"Google Tasks: {o.Moedetitel}"
            : o.MoedeId.Length > 0
                ? "fra: " + o.Moedetitel
                : "skrevet i haanden");

        Console.WriteLine($"  {o.Visningsnavn}");
        Console.WriteLine($"      {string.Join("  ·  ", maerker)}");
    }

    Console.WriteLine();

    // Er der noget tilbage i de gamle mapper? Saa gik flytningen ikke helt
    // igennem, og det skal siges - ikke opdages ved at en opgave mangler.
    var tilbage = 0;

    if (Directory.Exists(UserDataPaths.Meetings))
        foreach (var m in Directory.EnumerateDirectories(UserDataPaths.Meetings))
            if (File.Exists(Opgaveliste.GammelSti(m))) tilbage++;

    if (tilbage > 0)
        Console.WriteLine($"BEMAERK: {tilbage} mappe(r) har stadig en gammel opgaver.json.");
    else
        Console.WriteLine("Ingen gamle opgavefiler tilbage i optagelsesmapperne.");

    return 0;
}


// ------------------------------------------------------------- lydproeve
//
// KAN VI KOMPRIMERE LYDEN?
//
// En times moede fylder 110 MB pr. spor som ukomprimeret PCM. Zoom oplyser
// 200 MB i timen for VIDEO. Sammenligningen er ukomprimeret mod komprimeret,
// og spoergsmaalet er, om vi kan lukke det hul uden at oedelaegge udskriften.
//
// Den her koder til AAC med Windows' EGEN Media Foundation - ingen ny binaer,
// ingen ffmpeg paa 40-80 MB. Bagefter pakkes filen ud igen til 16 kHz mono
// PCM, som whisper skal have den, saa resultatet kan transskriberes og maales
// mod facitlisten.
//
// DER MAALES, DER KONKLUDERES IKKE HER. Kommandoen skriver stoerrelser. Om
// udskriften bliver lige saa god, afgoeres af maal-noejagtighed.ps1 bagefter.
static int Lydproeve(string[] a)
{
    if (a.Length == 0)
    {
        Console.WriteLine("Brug: heypia lydproeve <wav> [kbit ...]");
        Console.WriteLine("      standard: 24 32 48 64");
        return 0;
    }

    var kilde = a[0];

    if (!File.Exists(kilde))
    {
        Console.Error.WriteLine($"Findes ikke: {kilde}");
        return 1;
    }

    var bitrater = a.Skip(1).Select(x => int.TryParse(x, out var n) ? n : 0)
                            .Where(n => n > 0).ToArray();

    if (bitrater.Length == 0) bitrater = new[] { 24, 32, 48, 64 };

    var udmappe = Path.Combine(Path.GetDirectoryName(kilde)!, "lydproeve");
    Directory.CreateDirectory(udmappe);

    var raa = new FileInfo(kilde).Length;

    using (var laes = new NAudio.Wave.WaveFileReader(kilde))
    {
        Console.WriteLine();
        Console.WriteLine($"Kilde   : {Path.GetFileName(kilde)}");
        Console.WriteLine($"Format  : {laes.WaveFormat.SampleRate} Hz, " +
                          $"{laes.WaveFormat.Channels} kanal(er), {laes.WaveFormat.BitsPerSample} bit");
        Console.WriteLine($"Laengde : {laes.TotalTime:hh\\:mm\\:ss}");
        Console.WriteLine($"Fylder  : {raa / 1024.0 / 1024.0:0.0} MB " +
                          $"({raa / 1024.0 / 1024.0 / laes.TotalTime.TotalHours:0.0} MB/time)");
        Console.WriteLine();
    }

    NAudio.MediaFoundation.MediaFoundationApi.Startup();

    Console.WriteLine("  kbit/s    komprimeret       MB/time     mod PCM   udpakket til");
    Console.WriteLine("  " + new string('-', 68));

    foreach (var kbit in bitrater)
    {
        var m4a = Path.Combine(udmappe, $"proeve-{kbit}.m4a");
        var udpakket = Path.Combine(udmappe, $"proeve-{kbit}.wav");

        try
        {
            using (var laes = new NAudio.Wave.WaveFileReader(kilde))
                NAudio.Wave.MediaFoundationEncoder.EncodeToAac(laes, m4a, kbit * 1000);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {kbit,6}    KAN IKKE: {Kort(ex.Message)}");
            continue;
        }

        // UD IGEN TIL 16 kHz MONO. Whisper skal have praecis det format, og
        // en proeve, der ikke kan pakkes ud, er ikke en proeve.
        try
        {
            using var ind = new NAudio.Wave.MediaFoundationReader(m4a);

            var maal = new NAudio.Wave.WaveFormat(16000, 16, 1);

            using var omsaet = new NAudio.Wave.MediaFoundationResampler(ind, maal) { ResamplerQuality = 60 };
            NAudio.Wave.WaveFileWriter.CreateWaveFile(udpakket, omsaet);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {kbit,6}    kodet, men kunne ikke pakkes ud: {Kort(ex.Message)}");
            continue;
        }

        var lille = new FileInfo(m4a).Length;

        double timer;
        using (var laes = new NAudio.Wave.WaveFileReader(kilde)) timer = laes.TotalTime.TotalHours;

        Console.WriteLine($"  {kbit,6}    {lille / 1024.0 / 1024.0,8:0.00} MB    " +
                          $"{lille / 1024.0 / 1024.0 / timer,8:0.0}    " +
                          $"{(double)raa / lille,6:0.0}x    {Path.GetFileName(udpakket)}");
    }

    Console.WriteLine();
    Console.WriteLine($"Filerne ligger i {udmappe}");
    Console.WriteLine();
    Console.WriteLine("NAESTE SKRIDT: transskriber hver .wav og maal mod facitlisten.");
    Console.WriteLine("En mindre fil er ingenting vaerd, hvis udskriften bliver daarligere.");

    return 0;
}

static string Kort(string s) => s.Length <= 90 ? s : s[..90] + " ...";


/// <summary>
/// Det sprog, brugeren valgte i opstartsdialogen.
///
/// Loopback-sporet foerst: paa et webinar er det DET, der bliver talt, og
/// mikrofonen er tavs. Er der ikke valgt noget, gives der null, og saa
/// finder Whisper det selv.
/// </summary>
static string? Valgtsprog(string? mappe)
{
    if (mappe is null || !Directory.Exists(mappe)) return null;

    try
    {
        var meta = MeetingStore.Load(mappe);

        var valgt = meta?.ValgtSprogLoop ?? meta?.ValgtSprogMik;

        return string.IsNullOrWhiteSpace(valgt) ? null : valgt;
    }
    catch (Exception)
    {
        // Kan filen ikke laeses, gaettes sproget som foer. Det er bedre end
        // at afvise at skrive ud.
        return null;
    }
}


// ----------------------------------------------------------------- plads
//
// Efterproever pladsopgoerelsen og oprydningen UDEN at slette noget.
//
// Den findes, fordi et tal paa en skaerm ikke er efterproevet, bare fordi det
// staar der. Her kan det holdes op mod, hvad der faktisk ligger paa disken.
static int Plads(string[] a)
{
    var ryd = a.Contains("--ryd");
    var dage = 365;

    var i = Array.IndexOf(a, "--dage");
    if (i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out var d)) dage = d;

    var o = Lydoprydning.Opgoer();

    Console.WriteLine();
    Console.WriteLine($"Drev      : {o.DrevNavn}");
    Console.WriteLine($"I alt     : {o.IAltGb:0.0} GB");
    Console.WriteLine($"Brugt     : {o.BrugtGb:0.0} GB  ({100 - o.FriAndel:0} %)");
    Console.WriteLine($"Ledigt    : {o.FritGb:0.0} GB  ({o.FriAndel:0} %)");
    Console.WriteLine();
    Console.WriteLine($"Lydfiler  : {o.LydGb:0.00} GB i {o.LydFiler} filer");
    Console.WriteLine($"            {o.LydAndelAfDisk:0.00} % af disken · {o.LydAndelAfBrugt:0.00} % af det brugte");
    Console.WriteLine($"Resten    : {o.RestBytes / 1024.0 / 1024.0:0.0} MB tekst, dokumenter og modeller");
    Console.WriteLine();

    foreach (var n in new[] { 30, 90, 180, 365, 730 })
    {
        var k = Lydoprydning.Kandidater(n);
        var b = k.Sum(x => x.Bytes) / 1024.0 / 1024.0;

        Console.WriteLine($"  efter {n,4} dage:  {k.Count,3} optagelse(r)  ·  {b,8:0.0} MB");
    }

    Console.WriteLine();

    if (!ryd)
    {
        Console.WriteLine("Der er ikke slettet noget. Det her er kun en opgoerelse.");
        Console.WriteLine("Tilfoej --ryd [--dage N] for at rydde. Standard er 365 dage.");
        return 0;
    }

    var (filer, bytes) = Lydoprydning.Ryd(dage);

    Console.WriteLine($"RYDDET: {filer} lydfil(er), {bytes / 1024.0 / 1024.0:0.0} MB frigivet " +
                      $"paa optagelser aeldre end {dage} dage.");

    return 0;
}


static async Task<int> Diktat(string[] a)
{
    if (a.Length == 0)
    {
        Console.Error.WriteLine("Brug: heypia diktat <wav> [note|mail|prompt|opgave]");
        return 2;
    }

    var lyd = a[0];
    if (!File.Exists(lyd))
    {
        Console.Error.WriteLine($"Der er ingen lydfil paa {lyd}");
        return 1;
    }

    if (!Enum.TryParse<Dikteringsformaal>(a.Length > 1 ? a[1] : "note", ignoreCase: true, out var formaal))
    {
        Console.Error.WriteLine($"Ukendt formaal: {a[1]}. Vaelg note, mail, prompt eller opgave.");
        return 2;
    }

    var noegle = SkyNoegle.Hent();
    if (noegle is null)
    {
        Console.Error.WriteLine(SkyNoegle.Vejledning);
        return 1;
    }

    var klient = new Dikteringsklient(noegle);
    var ur = System.Diagnostics.Stopwatch.StartNew();

    var raa = await klient.SkrivUdAsync(lyd, Fagord());
    var efterUdskrift = ur.ElapsedMilliseconds;

    Console.WriteLine($"UDSKRIFT  {efterUdskrift} ms   sprog={(raa.Sprog.Length > 0 ? raa.Sprog : "?")}   " +
                      $"{raa.Sekunder:0.#} sek lyd   {raa.Raa.Length} tegn");
    Console.WriteLine();
    Console.WriteLine(raa.Raa);
    Console.WriteLine();

    if (raa.Raa.Length == 0)
    {
        Console.Error.WriteLine("Der kom ingen tekst ud. Er der lyd paa klippet?");
        return 1;
    }

    var pudset = await klient.PudsAsync(raa.Raa, formaal);

    Console.WriteLine($"PUDSET    {ur.ElapsedMilliseconds - efterUdskrift} ms   " +
                      $"formaal={formaal}   {pudset.Length} tegn");
    Console.WriteLine();
    Console.WriteLine(pudset);
    Console.WriteLine();
    Console.WriteLine($"I ALT     {ur.ElapsedMilliseconds} ms");

    return 0;
}

/// <summary>
/// Ordlisten, appen har laert. Sendes med, saa dikteringen kender de navne og
/// fagtermer, transskriptionen allerede er blevet rettet i.
/// </summary>
static string[] Fagord()
{
    try
    {
        return File.Exists(UserDataPaths.Vocabulary)
            ? File.ReadAllLines(UserDataPaths.Vocabulary)
                  .Select(l => l.Trim())
                  .Where(l => l.Length > 0 && !l.StartsWith('#'))
                  .Take(200)
                  .ToArray()
            : Array.Empty<string>();
    }
    catch (IOException)
    {
        // En ulaeselig ordliste maa ikke forhindre et diktat. Uden den bliver
        // udskriften en anelse ringere; det er alt.
        return Array.Empty<string>();
    }
}
