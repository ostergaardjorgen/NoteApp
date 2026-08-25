using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Documents;

/// <summary>
/// Et dokument og alt, det skal kunne svare på om sig selv.
///
/// Proveniensen er ikke pynt. Et referat, der ligger i en mappe om et halvt
/// år, skal kunne svare på: hvilket møde, hvilken skabelon, hvilken model.
/// Uden det er det en tekst, ingen tør bruge til noget — for man kan ikke
/// finde tilbage til lyden og tjekke efter.
/// </summary>
public sealed class DocumentInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = "";

    /// <summary>Brugerens egen beskrivelse. Det eneste felt, appen ikke rører.</summary>
    public string Description { get; set; } = "";

    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;

    /// <summary>
    /// Mødets id — den ENESTE holdbare forbindelse tilbage til optagelsen.
    ///
    /// Stien kan flyttes, og titlen kan omdøbes; begge dele skete, og begge
    /// dele rev forbindelsen over. Et id ændrer sig aldrig.
    /// </summary>
    public string SourceMeetingId { get; init; } = "";

    /// <summary>
    /// Sti og titel, som de var, da dokumentet blev lavet. De er til at VISE,
    /// ikke til at slå op med — de kan være forældede, og de opdateres af
    /// <see cref="DocumentStore.Opfrisk"/>, når mødet stadig findes.
    /// </summary>
    public string SourceRecording { get; set; } = "";

    public string SourceTitle { get; set; } = "";

    public string Template { get; init; } = "";
    public string Model { get; init; } = "";
    public double Seconds { get; init; }

    /// <summary>Filnavnet, uden sti — mappen kan flyttes.</summary>
    public string FileName { get; init; } = "";

    /// <summary>
    /// Brugerens egen mappe. Null eller tom betyder «uden mappe».
    ///
    /// Det er et felt og ikke en rigtig mappe på disken — se
    /// <see cref="NoteApp.Core.Mapper"/> for hvorfor.
    /// </summary>
    public string? Mappe { get; set; }

    public string Markdown { get; set; } = "";
}

/// <summary>
/// Dokumenterne: de færdige tekster, appen har lavet ud af møder.
///
/// De ligger for sig selv i datamappen frem for nede i den enkelte optagelses
/// mappe. Grunden er praktisk: det er dokumenterne, man leder efter bagefter,
/// ikke lydfilerne, og et referat skal kunne findes uden at vide hvilket møde
/// det kom fra.
///
/// Hvert dokument er to filer: en .docx, der åbnes i Word, og en .json med
/// hvor det kommer fra. Bliver .json væk, er dokumentet stadig et dokument.
/// </summary>
public static class DocumentStore
{
    public static string Directory => Path.Combine(UserDataPaths.Root, "Dokumenter");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Gemmer et dokument som .docx plus metadata. Returnerer stien til .docx'en.
    /// </summary>
    public static string Save(DocumentInfo info)
    {
        System.IO.Directory.CreateDirectory(Directory);

        var forside = new List<(string, string)>
        {
            ("Lavet", info.Created.ToString("d. MMMM yyyy 'kl.' HH:mm")),
            ("Fra optagelse", string.IsNullOrWhiteSpace(info.SourceTitle) ? "(ukendt)" : info.SourceTitle),
            ("Skabelon", info.Template),
            ("Sprogmodel", info.Model)
        };

        if (!string.IsNullOrWhiteSpace(info.Description))
            forside.Add(("Beskrivelse", info.Description));

        var fil = Path.Combine(Directory, info.FileName);
        DocxWriter.Write(fil, info.Title, info.Markdown, forside);

        File.WriteAllText(MetaSti(info), JsonSerializer.Serialize(info, Options), Encoding.UTF8);
        return fil;
    }

    /// <summary>
    /// Gemmer KUN metadata — .json'en. Word-filen røres ikke.
    /// </summary>
    /// <remarks>
    /// TIL DET, DER IKKE STÅR I DOKUMENTET. Hvilken mappe et dokument ligger
    /// i, er appens egen ordning; det står ikke på forsiden og skal ikke stå
    /// der. At skrive en 40-siders .docx om for at flytte den mellem to
    /// mapper er ikke bare spild — det FEJLER, hvis dokumentet er åbent i
    /// Word, og fejlen kommer som «Access to the path is denied».
    ///
    /// Set 25-08-2026, da en mappeflytning ramte alle dokumenter i mappen på
    /// én gang. Med ét dokument ad gangen var det sjældent nok til at ligne
    /// et uheld.
    ///
    /// Skal forsiden ændre sig — titel, beskrivelse, skabelon — er det
    /// <see cref="Save"/>, der skal bruges. Den skriver begge dele.
    /// </remarks>
    public static void GemMetadata(DocumentInfo info)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(MetaSti(info), JsonSerializer.Serialize(info, Options), Encoding.UTF8);
    }

    /// <summary>
    /// Opdaterer beskrivelsen og skriver dokumentet om, så forsiden følger med.
    /// Ellers ville dokumentet, man sender videre, sige noget andet end appen.
    /// </summary>
    public static string UpdateDescription(DocumentInfo info, string beskrivelse)
    {
        info.Description = beskrivelse;
        return Save(info);
    }

    /// <summary>
    /// Henter kildens NUVÆRENDE navn og sti ud fra mødets id.
    ///
    /// Uden dette ville et dokument blive ved med at vise det navn, mødet
    /// havde, da dokumentet blev lavet — også efter en omdøbning. To navne på
    /// den samme optagelse er værre end ét forkert: man kan ikke se, om det er
    /// det samme møde.
    ///
    /// Findes mødet ikke længere, bliver de gamle værdier stående. De er det
    /// eneste spor tilbage af, hvad dokumentet blev lavet af.
    /// </summary>
    public static bool Opfrisk(DocumentInfo info)
    {
        if (info.SourceMeetingId.Length == 0) return false;

        var fundet = MeetingStore.FindById(info.SourceMeetingId);
        if (fundet is null) return false;

        var (mappe, meta) = fundet.Value;
        var ændret = info.SourceRecording != mappe ||
                     info.SourceTitle != (meta.Title ?? Path.GetFileName(mappe));

        info.SourceRecording = mappe;
        info.SourceTitle = meta.Title ?? Path.GetFileName(mappe);
        return ændret;
    }

    public static IReadOnlyList<DocumentInfo> LoadAll()
    {
        var liste = new List<DocumentInfo>();
        if (!System.IO.Directory.Exists(Directory)) return liste;

        foreach (var fil in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            try
            {
                var d = JsonSerializer.Deserialize<DocumentInfo>(File.ReadAllText(fil, Encoding.UTF8));
                if (d is null) continue;

                // Navnet paa kilden hentes friskt. Er moedet doebt om, foelger
                // dokumentet med af sig selv.
                Opfrisk(d);
                liste.Add(d);
            }
            catch (JsonException)
            {
                // En ulaeselig metadatafil maa ikke skjule de oevrige.
            }
        }

        return liste.OrderByDescending(d => d.Created).ToList();
    }

    /// <summary>Sletter både dokumentet og dets metadata.</summary>
    public static void Delete(DocumentInfo info)
    {
        var odt = Path.Combine(Directory, info.FileName);
        if (File.Exists(odt)) File.Delete(odt);

        var meta = MetaSti(info);
        if (File.Exists(meta)) File.Delete(meta);
    }

    public static string Path_(DocumentInfo info) => Path.Combine(Directory, info.FileName);

    private static string MetaSti(DocumentInfo info) =>
        Path.Combine(Directory, System.IO.Path.ChangeExtension(info.FileName, ".json"));

    /// <summary>
    /// Et filnavn, der kan læses af et menneske og skrives af Windows.
    /// Tidsstemplet sikrer, at to referater af samme møde ikke overskriver
    /// hinanden — man kan have god grund til at lave det om.
    ///
    /// SKABELONNAVNET HÆNGES IKKE PÅ. Det gjorde det før, og resultatet var
    /// «Nyt-møde-referat-fra-Cloudworks-mødet-Mødereferat_2026-08-17_1157»:
    /// brugeren havde allerede skrevet, hvad dokumentet var, og så kom
    /// «Mødereferat» ovenpå en gang til. Titlen ER navnet — det er den, man
    /// skrev i feltet, og den skal ikke laves om bagefter.
    ///
    /// <paramref name="skabelon"/> står stadig i signaturen, fordi den bruges
    /// som nødnavn, når titlen er tom.
    /// </summary>
    public static string FileNameFor(string titel, string skabelon)
    {
        var rent = new StringBuilder();
        foreach (var c in titel.Trim().Length > 0 ? titel : skabelon)
        {
            if (char.IsLetterOrDigit(c) || c is 'æ' or 'ø' or 'å' or 'Æ' or 'Ø' or 'Å') rent.Append(c);
            else if (rent.Length > 0 && rent[^1] != '-') rent.Append('-');
        }

        var navn = rent.ToString().Trim('-');
        if (navn.Length > 70) navn = navn[..70].TrimEnd('-');
        if (navn.Length == 0) navn = "dokument";

        return $"{navn}_{DateTime.Now:yyyy-MM-dd_HHmm}.docx";
    }
}
