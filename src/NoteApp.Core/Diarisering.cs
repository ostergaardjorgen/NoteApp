using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NoteApp.Core;

/// <summary>Et stykke tale fra én stemme, målt i sekunder fra optagelsens start.</summary>
public sealed record Talestykke(double FraSek, double TilSek, string Stemme)
{
    public double Sekunder => TilSek - FraSek;
}

/// <summary>
/// Hvem talte hvornår på ét lydspor.
///
/// Filen ligger ved siden af udskriften og hører til LYDEN, ikke til teksten.
/// Derfor står modellen for talergenkendelsen i den, og ikke whisper-modellen:
/// skrives optagelsen ud igen med en anden model, er det stadig de samme
/// stemmer i den samme lyd.
/// </summary>
public sealed class Talere
{
    /// <summary>HERFRA eller DERFRA — hvilket spor der blev set på.</summary>
    public string Spor { get; init; } = "";

    public List<Talestykke> Stykker { get; init; } = new();

    /// <summary>Hvor mange stemmer der blev fundet, efter støjen er kasseret.</summary>
    public int Stemmer { get; init; }

    public DateTimeOffset Lavet { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Indstillingerne, resultatet blev til med.
    ///
    /// De står i filen, fordi de er blevet ændret én gang og kommer til det
    /// igen. Uden dem kan man et halvt år senere ikke afgøre, om en mærkelig
    /// opdeling skyldes lyden eller en indstilling, der siden er rettet.
    /// </summary>
    public string Indstillinger { get; init; } = "";

    /// <summary>Stemmerne, med mest talende først.</summary>
    public IReadOnlyList<string> Navne() => Stykker
        .GroupBy(s => s.Stemme)
        .OrderByDescending(g => g.Sum(s => s.Sekunder))
        .Select(g => g.Key)
        .ToList();
}

/// <summary>
/// Talergenkendelse — at skille stemmerne fra hinanden inde på ét lydspor.
///
/// HVAD DEN LØSER
///
/// Et onlinemøde optages på to spor: din mikrofon og de andres lyd. Det siger
/// hvilken SIDE der talte, men ikke hvem. Sad der tre personer i den anden
/// ende, står de alle som «Gæster». På en optagelse med ét spor — et fysisk
/// møde eller en lydfil, der er lagt ind — siger sporet ingenting overhovedet.
///
/// MÅLT 20. AUGUST 2026 PÅ ET RIGTIGT MØDE
///
/// Gæstesporet fra et møde på 35 minutter med to talere, Alexander og Espen:
///
///   to stemmer fundet, 54,1 % og 37,6 % af taletiden
///   194 af 220 replikker (88 %) lagt på den rigtige af de to
///   18 replikker (8,2 %) uden stemme — korte indskud som «Ja. Ja.»
///   8 replikker (3,6 %) i en klynge, der kom af, at der blev talt i munden
///     på hinanden: begge spor har de samme ord dér, og lyden er en blanding
///
/// Det svære er ikke at høre et skift. Det er at holde en stemme fast gennem
/// et helt møde. Alexander taler 03:02, tier i ni minutter og taler igen
/// 13:47 — og får samme mærkat begge gange. Det blev efterprøvet to gange på
/// hver sit udsnit af lyden med samme svar.
///
/// TÆRSKLEN ER HELE FORSKELLEN
///
/// Værktøjets egen standard er 0,5. Med den blev det samme møde delt i 72
/// stemmer. Med den rigtige blev det til 2. Der er intet imellem, der er
/// «næsten rigtigt»: en forkert tærskel gør funktionen ubrugelig frem for
/// unøjagtig, og derfor står tallet i koden og ikke i en indstilling, nogen
/// kan komme til at dreje på.
///
/// HVORFOR DEN LILLE STEMMEMODEL OG IKKE DEN STORE
///
/// Tre blev målt mod den samme lyd med kendt facit.
///
/// 3D-Speaker ERes2Net ramte rigtigt, men er fravalgt af en anden grund:
/// modelvægtene har ingen oplyst licens, og de er trænet på et
/// forskningsdatasæt. Det kan ikke stå i Compliance som frit at bruge.
///
/// NVIDIA TitaNet-large — 97 MB — fandt aldrig en ren opdeling. Ved enhver
/// tærskel gav den enten en falsk tredje stemme eller slog de to rigtige
/// sammen til én på 95 %. Der findes ikke et brugbart tal for den.
///
/// NVIDIA TitaNet-small — 38 MB — ramte facit på et bredt leje, er dobbelt så
/// hurtig som de to andre, og rettede oven i købet den ene fejl, den store og
/// ERes2Net begge lavede: atten sekunders tale, der blev lagt i sin egen
/// klynge frem for hos den, der sagde det.
///
/// Den er CC-BY-4.0. Kommerciel brug og videredistribution er tilladt mod
/// kreditering af NVIDIA, og den kreditering står under Compliance.
/// </summary>
public static class Diarisering
{
    // ---------------------------------------------------------- indstillinger

    /// <summary>
    /// Afstanden, to stykker tale skal have for at være to forskellige
    /// stemmer. Større tal giver færre stemmer.
    ///
    /// Målt på tolv minutter af et møde med to talere, hvor facit er kendt:
    ///
    ///   0,50   3 stemmer   65 / 27 / 4 %   én for mange
    ///   0,70   2 stemmer   68 / 31 %       rigtigt
    ///   0,90   2 stemmer   68 / 31 %       rigtigt
    ///   1,10   2 stemmer   68 / 32 %       rigtigt
    ///
    /// Værdien er lagt midt i det stabile leje. Det er bredt nok til, at et
    /// møde med lidt anden lyd ikke falder ud over kanten — og det er selve
    /// grunden til, at netop denne stemmemodel blev valgt.
    /// </summary>
    private const string Taerskel = "0.90";

    /// <summary>
    /// Klynger, der fylder mindre end det her af taletiden, kasseres som støj.
    ///
    /// Der kommer altid en håndfuld af dem: stumper på et halvt sekund,
    /// host, og de steder hvor to taler samtidig. På det målte møde var det
    /// 34 klynger med 5,2 % af tiden tilsammen — mod to rigtige stemmer med
    /// 54 % og 38 %. Uden den her ville en udskrift med to talere se ud til
    /// at have seksogtredive.
    /// </summary>
    private const double Stoejgraense = 0.03;

    /// <summary>
    /// Antal tråde.
    ///
    /// Målt på fire minutters lyd: én tråd 56,9 s, to 39,2 s, fire 36,6 s,
    /// seks 37,0 s, otte 43,3 s. Otte er LANGSOMMERE end fire, fordi de sidste
    /// fire kun er hypertråde på de samme fire fysiske kerner. Resultatet var
    /// byte-identisk ved alle trådtal, så det koster ingen nøjagtighed.
    ///
    /// Der bindes til de fysiske kerner, dog højst fire: whisper skal kunne
    /// fodre grafikkortet imens, og et møde bliver ikke skrevet ud hurtigere
    /// af, at talergenkendelsen har taget hele maskinen.
    /// </summary>
    private static int Traade => Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));

    // ------------------------------------------------------------- filerne

    /// <summary>
    /// Hvor værktøjet og de to modeller ligger.
    ///
    /// DE FØLGER MED APPEN OG HENTES IKKE.
    ///
    /// Til sammen fylder de 48 MB — mod whisper-modellens 2.952 MB, som ikke
    /// kan pakkes med. Men størrelsen er ikke grunden.
    ///
    /// Tærsklen på 0,80 er MÅLT mod præcis de to modelfiler. Skiftes
    /// stemmemodellen ud, gælder målingen ikke længere, og den samme optagelse
    /// kan gå fra to talere til toogfirs, uden at nogen har rørt en
    /// indstilling. Model og indstilling er én ting og skal følges ad.
    ///
    /// Derfor ligger de ved siden af programmet og bliver skiftet ud sammen
    /// med det. Mappen i datamappen bruges kun, når de ikke er der — det er
    /// dér, en håndlagt kopi kan stå under udvikling.
    /// </summary>
    public static string Mappe { get; } = Vaelg();

    private static string Vaelg()
    {
        var vedSiden = Path.Combine(AppContext.BaseDirectory, "talere");
        return Directory.Exists(vedSiden)
            ? vedSiden
            : Path.Combine(WhisperInstall.Root, "talere");
    }

    public static string? Vaerktoej() => Findes(Path.Combine(Mappe, "bin", "sherpa-onnx-offline-speaker-diarization.exe"));
    public static string? Segmentering() => Findes(Path.Combine(Mappe, "segmentering.onnx"));
    public static string? Stemmemodel() => Findes(Path.Combine(Mappe, "stemmer.onnx"));

    private static string? Findes(string sti) => File.Exists(sti) ? sti : null;

    public static bool ErInstalleret =>
        Vaerktoej() is not null && Segmentering() is not null && Stemmemodel() is not null;

    /// <summary>
    /// Hvor resultatet for ét spor ligger.
    ///
    /// Navnet indeholder ikke whisper-modellen. Talergenkendelsen ser på
    /// lyden, ikke på teksten, og skal ikke laves om, fordi optagelsen bliver
    /// skrevet ud igen med en anden model.
    /// </summary>
    public static string Sti(string mappe, string spor) =>
        Path.Combine(mappe, $"talere_{(spor.Length == 0 ? "alle" : spor.ToLowerInvariant())}.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static Talere? Hent(string mappe, string spor)
    {
        var sti = Sti(mappe, spor);
        if (!File.Exists(sti)) return null;

        try { return JsonSerializer.Deserialize<Talere>(File.ReadAllText(sti, Encoding.UTF8)); }
        catch (Exception) { return null; }
    }

    public static void Gem(string mappe, Talere talere)
    {
        Directory.CreateDirectory(mappe);
        File.WriteAllText(Sti(mappe, talere.Spor),
            JsonSerializer.Serialize(talere, Format), new UTF8Encoding(false));
    }

    // -------------------------------------------------------------- kørslen

    private static readonly Regex Linje = new(
        @"^\s*([\d.]+)\s+--\s+([\d.]+)\s+(speaker_\d+)", RegexOptions.Compiled);

    /// <summary>
    /// Kører talergenkendelsen på én lydfil.
    ///
    /// DEN MÅ ALDRIG KUNNE SPÆRRE FOR NOGET.
    ///
    /// Fejler den — mangler værktøjet, er lyden for kort, dør processen — så
    /// returneres null, og resten fortsætter uændret. En udskrift uden navne
    /// er stadig en udskrift; en udskrift, man ikke fik, er ingenting.
    /// </summary>
    public static async Task<Talere?> KoerAsync(
        string wav, string spor,
        IProgress<string>? fremdrift = null,
        CancellationToken ct = default)
    {
        if (Vaerktoej() is not { } exe) return null;
        if (Segmentering() is not { } seg) return null;
        if (Stemmemodel() is not { } emb) return null;
        if (!File.Exists(wav)) return null;

        var traade = Traade;

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // TALLENE SKAL SKRIVES MED PUNKTUM.
        //
        // Vaerktoejet laeser dem med C-locale. Bliver de formateret efter
        // maskinens sprog, staar der "0,80" - og saa laeses der "0", hvorefter
        // hvert eneste stykke tale bliver sin egen stemme. Det er ikke en
        // teoretisk risiko: det skete under maalingen, og resultatet saa
        // plausibelt ud, indtil tallene blev talt efter.
        foreach (var a in new[]
        {
            $"--clustering.cluster-threshold={Taerskel}",
            $"--segmentation.pyannote-model={seg}",
            $"--segmentation.num-threads={traade.ToString(CultureInfo.InvariantCulture)}",
            $"--embedding.model={emb}",
            $"--embedding.num-threads={traade.ToString(CultureInfo.InvariantCulture)}",
            wav
        }) psi.ArgumentList.Add(a);

        var raa = new List<Talestykke>();

        try
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (_, e) => Laes(e.Data, raa);
            p.ErrorDataReceived += (_, e) => Laes(e.Data, raa);

            if (!p.Start()) return null;

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            fremdrift?.Report($"finder stemmerne på {traade} tråde …");

            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            if (p.ExitCode != 0) return null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return null; }

        if (raa.Count == 0) return null;

        var rene = FjernStoej(raa);
        if (rene.Count == 0) return null;

        return new Talere
        {
            Spor = spor,
            Stykker = rene,
            Stemmer = rene.Select(s => s.Stemme).Distinct().Count(),
            // Ogsaa her skrives tallene med punktum. Teksten laeses igen af et
            // menneske et halvt aar senere, og "0,03" og "0.03" ser ens ud,
            // lige indtil man skal sammenligne to koersler fra hver sin maskine.
            Indstillinger = string.Format(CultureInfo.InvariantCulture,
                "taerskel={0}, traade={1}, stoejgraense={2}", Taerskel, traade, Stoejgraense)
        };
    }

    private static void Laes(string? linje, List<Talestykke> ud)
    {
        if (linje is null) return;

        var m = Linje.Match(linje);
        if (!m.Success) return;

        var fra = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var til = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);

        lock (ud) ud.Add(new Talestykke(fra, til, m.Groups[3].Value));
    }

    /// <summary>
    /// Kasserer de klynger, der ikke er stemmer.
    ///
    /// Stemmerne omdøbes samtidig til «SPOR#0», «SPOR#1» … i rækkefølge efter
    /// taletid, så den, der taler mest, står først. Værktøjets egne numre
    /// siger ingenting og springer i tal, når noget er kasseret — og de ville
    /// blive stående i mødets metadata, hvor et menneske skal sætte navn på
    /// dem bagefter.
    /// </summary>
    private static List<Talestykke> FjernStoej(List<Talestykke> raa)
    {
        var tid = raa.GroupBy(s => s.Stemme)
                     .ToDictionary(g => g.Key, g => g.Sum(s => s.Sekunder));

        var samlet = tid.Values.Sum();
        if (samlet <= 0) return new List<Talestykke>();

        var beholdt = tid.Where(p => p.Value / samlet >= Stoejgraense)
                         .OrderByDescending(p => p.Value)
                         .Select(p => p.Key)
                         .ToList();

        var nyt = beholdt.Select((gammelt, i) => (gammelt, i))
                         .ToDictionary(x => x.gammelt, x => x.i);

        return raa.Where(s => nyt.ContainsKey(s.Stemme))
                  .Select(s => s with { Stemme = nyt[s.Stemme].ToString(CultureInfo.InvariantCulture) })
                  .OrderBy(s => s.FraSek)
                  .ToList();
    }

    // -------------------------------------------------------- på udskriften

    /// <summary>
    /// Sætter stemmer på de replikker, der kom fra det spor.
    ///
    /// Der vælges den stemme, der fylder MEST inde i replikken. Whisper skærer
    /// ikke ved talerskift, så en replik kan strække sig hen over et; så hører
    /// den til den, der sagde mest af den. Rammer ingen stemme replikken —
    /// det sker ved korte indskud på et par ord — bliver den stående uden.
    /// Et gæt ville se ud som viden.
    /// </summary>
    /// <returns>Hvor mange replikker der fik en stemme.</returns>
    public static int Anvend(Udskrift udskrift, Talere talere)
    {
        // EN STEMME ER IKKE EN OPDELING.
        //
        // Fandt den kun een stemme paa sporet, har den intet fortalt, som
        // sporet ikke sagde i forvejen. At saette "Gaest 1" paa hver eneste
        // replik ville se ud som et resultat og vaere en omdoebning.
        if (talere.Stemmer < 2) return 0;

        var sat = 0;

        foreach (var l in udskrift.Linjer)
        {
            if (l.Spor != talere.Spor) continue;

            var fra = l.FraMs / 1000.0;
            var til = l.TilMs / 1000.0;

            string? bedst = null;
            var mest = 0.0;

            foreach (var g in talere.Stykker.GroupBy(s => s.Stemme))
            {
                var overlap = g.Sum(s => Math.Max(0, Math.Min(til, s.TilSek) - Math.Max(fra, s.FraSek)));
                if (overlap > mest) { mest = overlap; bedst = g.Key; }
            }

            if (bedst is null) continue;

            l.Stemme = $"{talere.Spor}#{bedst}";
            sat++;
        }

        return sat;
    }
}
