using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace NoteApp.Core;

/// <summary>En mappe, appen holder øje med.</summary>
public sealed class Overvaagetmappe
{
    public string Sti { get; set; } = "";

    /// <summary>Slået fra betyder «husk mappen, men kig ikke i den».</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>
    /// Læg filen ind uden at spørge.
    ///
    /// Falsk som standard, og det er ikke forsigtighed for forsigtighedens
    /// skyld: en mappe, man deler med andre, kan få en lydfil, der ikke er et
    /// møde. Bliver den lagt ind af sig selv, står den i listen som en
    /// optagelse, ingen har bedt om.
    /// </summary>
    public bool Automatisk { get; set; }

    /// <summary>HeyPia-folderen, filerne skal lande i. Null betyder «uden folder».</summary>
    public string? Folder { get; set; }

    public bool Undermapper { get; set; } = true;

    /// <summary>Hvor mappen kom fra: «iCloud Drive», «OneDrive», «Tilføjet af dig».</summary>
    public string Herkomst { get; set; } = "";
}

/// <summary>En lydfil, der er dukket op i en overvåget mappe. Hedder ikke «Fund» — det navn er søgningens.</summary>
public sealed record Lydfund(
    string Sti,
    string Filnavn,
    long Bytes,
    DateTime Aendret,
    bool KunISkyen,
    string Mappenavn,
    string? Folder);

/// <summary>
/// Mapper, appen holder øje med — og lydfilerne, der dukker op i dem.
///
/// HVORFOR DEN IKKE BRUGER ET API HOS SKYTJENESTERNE
///
/// iCloud, Google Drev, OneDrive, Dropbox og Nextcloud har alle en klient til
/// Windows, der synkroniserer til en helt almindelig mappe på disken. Derfor
/// kræver det her ingen OAuth, ingen tokens og ingen ny leverandør på
/// Compliance-siden. Appen ser en mappe. Ikke andet.
///
/// DEN VIGTIGSTE DETALJE: PLADSHOLDERE
///
/// En fil i iCloud eller OneDrive ligger ikke nødvendigvis på disken. Der står
/// en pladsholder med det rigtige navn og den rigtige størrelse, og indholdet
/// hentes først, når nogen LÆSER filen. Det har to følger, og de er begge
/// afgørende for, hvordan der scannes:
///
///   1. Der læses aldrig i en fil under scanningen — kun navn, størrelse og
///      dato. Ellers ville appen hente hele skymappen ned, bare fordi den
///      kiggede efter.
///   2. En pladsholder er stadig et fund. Den fylder rigtigt, den hedder
///      noget, og den kan lægges ind — det koster bare en hentning, og det
///      bliver sagt.
///
/// Det blev fundet ud af 24-08-2026, hvor en talememo i iCloud var usynlig
/// for enhver søgning, indtil den blev læst.
/// </summary>
public static class Overvaagning
{
    /// <summary>
    /// Mappenavnet, der ledes efter i hver skytjeneste.
    ///
    /// HVORFOR IKKE HELE SKYMAPPEN. At overvåge hele iCloud Drive ville
    /// betyde, at hver eneste lydfil nogen steder i skyen — en podcast, en
    /// ringetone, en optagelse fra et helt andet program — stod som et fund,
    /// der skulle tages stilling til. En navngiven mappe er en aftale med
    /// brugeren om, hvor tingene skal ligge.
    /// </summary>
    public const string Standardmappe = "HeyPia";

    /// <summary>
    /// Navne, mappen kan have haft før. Der ledes efter dem alle.
    /// </summary>
    /// <remarks>
    /// APPEN SKIFTEDE NAVN 28-08-2026. Den, der allerede havde oprettet en
    /// «NoteApp»-mappe i sin OneDrive og lagt optagelser i den, ville med et
    /// rent navneskift opleve, at appen holdt op med at finde dem — uden en
    /// fejl og uden en besked. Filerne ville ligge der, og appen ville sige,
    /// at der ingen var.
    ///
    /// Mappen tilhører brugeren. Det er ikke appens ret at gøre den usynlig,
    /// fordi den selv har skiftet navn.
    /// </remarks>
    public static readonly string[] Mappenavne = { Standardmappe, "NoteApp" };

    private static string Sti => Path.Combine(UserDataPaths.Root, "overvaagning.json");

    // ===================== HVAD DER ER SET FOER =====================
    //
    // Uden den her ville de samme filer blive tilbudt igen hver eneste gang,
    // appen kiggede - ogsaa dem, brugeren netop har sagt nej til.
    //
    // NOEGLEN ER STI, STOERRELSE OG DATO TILSAMMEN. Kun stien ville betyde, at
    // en ny optagelse med samme navn aldrig blev opdaget. Alle tre betyder, at
    // en aendret fil regnes som ny - og det er det rigtige: er filen skrevet
    // om, er den ikke den samme optagelse.
    private static string Boglaeser => Path.Combine(UserDataPaths.Root, "overvaagning-set.json");

    /// <summary>
    /// Mapperne, der holdes øje med. Tom liste betyder «ingen overvågning».
    /// </summary>
    public static List<Overvaagetmappe> Mapper()
    {
        try
        {
            if (File.Exists(Sti))
                return JsonSerializer.Deserialize<List<Overvaagetmappe>>(
                    File.ReadAllText(Sti, Encoding.UTF8)) ?? new();
        }
        catch (JsonException) { }
        catch (IOException) { }

        return new();
    }

    public static void Gem(IEnumerable<Overvaagetmappe> mapper)
    {
        Directory.CreateDirectory(UserDataPaths.Root);

        File.WriteAllText(Sti,
            JsonSerializer.Serialize(mapper.ToList(), new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    /// <summary>
    /// Skytjenesterne på maskinen — og HeyPia-mappen i hver af dem.
    ///
    /// HVORDAN DE FINDES. Windows fører selv en liste over
    /// synkroniseringsrødder i registreringsdatabasen, og det er den, der
    /// spørges. Alternativet — at gætte på stier under brugermappen — blev
    /// prøvet 24-08-2026 og fejlede: iCloud Drive lå i `C:\iCloudDrive`, altså
    /// slet ikke under brugeren. Et gæt havde ikke fundet den.
    /// </summary>
    public static List<Overvaagetmappe> Foreslaa()
    {
        var forslag = new List<Overvaagetmappe>();

        foreach (var (navn, rod) in Synkroniseringsroedder())
        {
            // BEGGE NAVNE. Se Mappenavne: en mappe, brugeren har oprettet
            // foer navneskiftet, skal blive ved at vaere synlig.
            foreach (var mappenavn in Mappenavne)
            {
                var mappe = Path.Combine(rod, mappenavn);
                if (!Directory.Exists(mappe)) continue;

                forslag.Add(new Overvaagetmappe
                {
                    Sti = mappe,
                    Herkomst = navn,
                    Aktiv = true
                });
            }
        }

        return forslag;
    }

    /// <summary>
    /// Skytjenesterne og deres mapper, uanset om der ligger en HeyPia-mappe.
    /// Til skærmen, der skal kunne tilbyde at oprette den.
    /// </summary>
    public static List<(string Navn, string Rod)> Synkroniseringsroedder()
    {
        var fundet = new List<(string, string)>();

        try
        {
            using var noegle = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager");

            if (noegle is null) return fundet;

            foreach (var navn in noegle.GetSubKeyNames())
            {
                using var roedder = noegle.OpenSubKey(navn + @"\UserSyncRoots");
                if (roedder is null) continue;

                foreach (var v in roedder.GetValueNames())
                {
                    if (roedder.GetValue(v) is not string sti) continue;

                    sti = sti.TrimEnd('\\');
                    if (sti.Length == 0 || !Directory.Exists(sti)) continue;

                    // Navnet staar som «iCloudDrive!S-1-5-21-...!Personal».
                    // Kun det foerste led er laeseligt for et menneske.
                    var pænt = navn.Split('!')[0];

                    if (fundet.Any(f => string.Equals(f.Item2, sti, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    fundet.Add((pænt, sti));
                }
            }
        }
        catch (Exception)
        {
            // Kan registreringsdatabasen ikke laeses, er svaret «ingen
            // skytjenester fundet». Brugeren kan stadig tilfoeje mapper selv,
            // og det er hele funktionen - forslagene er en bekvemmelighed.
        }

        return fundet;
    }

    /// <summary>
    /// En fil skal ligge stille, før den regnes som færdig.
    ///
    /// En fil, der bliver kopieret ind eller synkroniseret ned, findes på
    /// disken længe før den er hel. Læses den for tidligt, bliver den lagt ind
    /// halv — og en halv optagelse ser ud som en optagelse, hvor mødet
    /// sluttede midt i en sætning.
    /// </summary>
    public static readonly TimeSpan Rovetid = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Kigger i mapperne og finder de lydfiler, der ikke er set før.
    ///
    /// LÆSER ALDRIG I EN FIL. Kun navn, størrelse og dato — se klassens
    /// forklaring om pladsholdere.
    /// </summary>
    public static List<Lydfund> Kig(IEnumerable<Overvaagetmappe>? mapper = null)
    {
        var fund = new List<Lydfund>();
        var set = Setfoer();
        var nu = DateTime.Now;

        foreach (var m in (mapper ?? Mapper()).Where(m => m.Aktiv))
        {
            if (string.IsNullOrWhiteSpace(m.Sti) || !Directory.Exists(m.Sti)) continue;

            IEnumerable<string> filer;

            try
            {
                filer = Directory.EnumerateFiles(m.Sti, "*",
                    m.Undermapper ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                continue;   // en mappe, der ikke kan laeses, springes over
            }

            var navn = Mappenavn(m.Sti);

            foreach (var fil in filer)
            {
                if (!Indlaesning.Kendes(fil)) continue;

                FileInfo oplysning;
                try { oplysning = new FileInfo(fil); }
                catch (Exception) { continue; }

                if (oplysning.Length == 0) continue;

                // Skjulte filer og systemfiler er ikke noget, nogen har lagt
                // der med vilje. Synkroniseringsklienterne bruger dem selv.
                if (oplysning.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                if (oplysning.Attributes.HasFlag(FileAttributes.System)) continue;

                if (nu - oplysning.LastWriteTime < Rovetid) continue;

                var noegle = Noegle(fil, oplysning);
                if (set.Contains(noegle)) continue;

                fund.Add(new Lydfund(
                    fil,
                    Path.GetFileName(fil),
                    oplysning.Length,
                    oplysning.LastWriteTime,
                    Indlaesning.KunISkyen(fil),
                    navn,
                    m.Folder));
            }
        }

        // Nyeste foerst. Den, man lige har lagt i mappen, er den, man venter
        // paa - ikke den, der har ligget der i tre uger.
        return fund.OrderByDescending(f => f.Aendret).ToList();
    }

    /// <summary>
    /// Skriver filen ind i bogen, så den ikke bliver tilbudt igen.
    ///
    /// Kaldes både når en fil er lagt ind, og når den er afvist. Bogen svarer
    /// på «er der taget stilling til den her», ikke på «blev den brugt».
    /// </summary>
    public static void Husk(string fil)
    {
        try
        {
            var oplysning = new FileInfo(fil);
            if (!oplysning.Exists) return;

            var set = Setfoer();
            set.Add(Noegle(fil, oplysning));

            Directory.CreateDirectory(UserDataPaths.Root);
            File.WriteAllText(Boglaeser,
                JsonSerializer.Serialize(set.ToList(), new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);
        }
        catch (Exception)
        {
            // En bog, der ikke kan skrives, betyder at filen bliver tilbudt
            // igen. Det er ubelejligt og ikke farligt - og det maa aldrig
            // vaelte den indlaesning, der lige er lykkedes.
        }
    }

    /// <summary>Glemmer alt. Så bliver hver fil i mapperne tilbudt igen.</summary>
    public static void GlemAlt()
    {
        try { if (File.Exists(Boglaeser)) File.Delete(Boglaeser); }
        catch (IOException) { }
    }

    /// <summary>Hvor mange filer der er taget stilling til.</summary>
    public static int Husket() => Setfoer().Count;

    private static HashSet<string> Setfoer()
    {
        try
        {
            if (File.Exists(Boglaeser))
                return JsonSerializer.Deserialize<List<string>>(
                           File.ReadAllText(Boglaeser, Encoding.UTF8))?
                       .ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { }
        catch (IOException) { }

        return new(StringComparer.OrdinalIgnoreCase);
    }

    private static string Noegle(string fil, FileInfo oplysning) =>
        $"{fil.ToLowerInvariant()}|{oplysning.Length}|{oplysning.LastWriteTimeUtc.Ticks}";

    /// <summary>Mappens navn, som det skal stå på skærmen.</summary>
    private static string Mappenavn(string sti)
    {
        try
        {
            var navn = new DirectoryInfo(sti).Name;
            return navn.Length > 0 ? navn : sti;
        }
        catch (Exception) { return sti; }
    }
}
