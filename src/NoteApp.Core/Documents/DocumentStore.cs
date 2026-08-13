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

    /// <summary>Mappen med optagelsen, dokumentet er lavet af. Tom hvis kilden er væk.</summary>
    public string SourceRecording { get; init; } = "";
    public string SourceTitle { get; init; } = "";

    public string Template { get; init; } = "";
    public string Model { get; init; } = "";
    public double Seconds { get; init; }

    /// <summary>Filnavnet på .odt-filen, uden sti — mappen kan flyttes.</summary>
    public string FileName { get; init; } = "";

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
/// Hvert dokument er to filer: en .odt, der kan åbnes i Word, og en .json med
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
    /// Gemmer et dokument som .odt plus metadata. Returnerer stien til .odt'en.
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

        var odt = Path.Combine(Directory, info.FileName);
        OdtWriter.Write(odt, info.Title, info.Markdown, forside);

        File.WriteAllText(MetaSti(info), JsonSerializer.Serialize(info, Options), Encoding.UTF8);
        return odt;
    }

    /// <summary>
    /// Opdaterer beskrivelsen og skriver .odt'en om, så forsiden følger med.
    /// Ellers ville dokumentet, man sender videre, sige noget andet end appen.
    /// </summary>
    public static string UpdateDescription(DocumentInfo info, string beskrivelse)
    {
        info.Description = beskrivelse;
        return Save(info);
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
                if (d is not null) liste.Add(d);
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
    /// </summary>
    public static string FileNameFor(string titel, string skabelon)
    {
        var rent = new StringBuilder();
        foreach (var c in $"{titel} - {skabelon}")
        {
            if (char.IsLetterOrDigit(c) || c is 'æ' or 'ø' or 'å' or 'Æ' or 'Ø' or 'Å') rent.Append(c);
            else if (rent.Length > 0 && rent[^1] != '-') rent.Append('-');
        }

        var navn = rent.ToString().Trim('-');
        if (navn.Length > 70) navn = navn[..70].TrimEnd('-');
        if (navn.Length == 0) navn = "dokument";

        return $"{navn}_{DateTime.Now:yyyy-MM-dd_HHmm}.odt";
    }
}
