using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Core.Llm;

/// <summary>
/// En skabelon: hvad der skal laves ud af en transskription — et referat, en
/// opgaveliste, et tilbudsudkast.
///
/// Skabeloner er filer i datamappen, ikke kode. De skal kunne rettes, kopieres
/// og sikkerhedskopieres som alt andet af brugerens, og en ny skabelon må ikke
/// kræve en ny udgave af appen.
///
/// Formatet er markdown med frontmatter — læsbart i enhver editor:
///
///   ---
///   navn: Mødereferat
///   model: qwen3-8b
///   temperatur: 0.2
///   ---
///   [systemprompt]
///   ---
///   [brugerprompt med {{transskription}} og andre felter]
/// </summary>
public sealed class PromptTemplate
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? PreferredModel { get; init; }
    public double Temperature { get; init; } = 0.2;

    /// <summary>Loft over svarets længde. Et referat af et langt møde skal have plads.</summary>
    public int MaxTokens { get; init; } = 2048;

    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }

    /// <summary>Filen den kom fra. Null hvis den er indbygget.</summary>
    public string? Path { get; init; }

    public static string Directory => System.IO.Path.Combine(UserDataPaths.Root, "skabeloner");

    /// <summary>
    /// Felter, en skabelon kan bruge. Står her frem for spredt ud i teksten,
    /// så en skabelonskriver kan se hvad der er at vælge imellem.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Fields = new Dictionary<string, string>
    {
        ["transskription"] = "Hele den udskrevne tekst fra mødet",
        ["titel"] = "Mødets titel",
        ["dato"] = "Dato og klokkeslæt",
        ["varighed"] = "Mødets længde",
        ["noter"] = "Dine egne noter og bogmærker fra mødet",
        ["ordbog"] = "Dine fagord og navne — hjælper modellen med at stave rigtigt",
        ["sprog"] = "Det sprog mødet blev holdt på, som Whisper fandt det"
    };

    public static PromptTemplate Parse(string text, string? path = null)
    {
        // Tre dele adskilt af --- paa egen linje: frontmatter, systemprompt,
        // brugerprompt. Er der kun to, er den foerste systemprompt.
        var dele = Regex.Split(text.Replace("\r\n", "\n").TrimStart(), @"(?m)^---\s*$")
            .Where(d => d.Trim().Length > 0)
            .ToArray();

        if (dele.Length < 2)
            throw new FormatException("Skabelonen skal have frontmatter og mindst én prompt, adskilt af linjer med ---");

        var felter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var linje in dele[0].Split('\n'))
        {
            var i = linje.IndexOf(':');
            if (i <= 0) continue;
            felter[linje[..i].Trim()] = linje[(i + 1)..].Trim();
        }

        double.TryParse(Hent(felter, "temperatur"), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var temp);
        int.TryParse(Hent(felter, "maks_tokens"), out var maks);

        return new PromptTemplate
        {
            Name = Hent(felter, "navn") ?? System.IO.Path.GetFileNameWithoutExtension(path) ?? "Uden navn",
            Description = Hent(felter, "beskrivelse"),
            PreferredModel = Hent(felter, "model"),
            Temperature = temp > 0 ? temp : 0.2,
            MaxTokens = maks > 0 ? maks : 2048,
            SystemPrompt = dele[1].Trim(),
            UserPrompt = dele.Length > 2 ? dele[2].Trim() : "{{transskription}}",
            Path = path
        };
    }

    private static string? Hent(Dictionary<string, string> d, string n) =>
        d.TryGetValue(n, out var v) && v.Length > 0 ? v : null;

    /// <summary>
    /// Sætter felterne ind. Et felt, der ikke er udfyldt, erstattes med tom
    /// tekst frem for at stå tilbage som {{noget}} — modellen ville ellers
    /// forsøge at udfylde det selv.
    /// </summary>
    public string Render(IReadOnlyDictionary<string, string?> values)
    {
        var sb = new StringBuilder(UserPrompt);
        foreach (var felt in Fields.Keys)
        {
            values.TryGetValue(felt, out var v);
            sb.Replace("{{" + felt + "}}", v ?? "");
        }
        return sb.ToString();
    }

    public static IReadOnlyList<PromptTemplate> LoadAll()
    {
        var liste = new List<PromptTemplate>();
        if (!System.IO.Directory.Exists(Directory)) return liste;

        foreach (var fil in System.IO.Directory.EnumerateFiles(Directory, "*.md"))
        {
            try { liste.Add(Parse(File.ReadAllText(fil, Encoding.UTF8), fil)); }
            catch (FormatException)
            {
                // En ulaeselig skabelon maa ikke skjule de oevrige. Den springes
                // over her og vises som fejl i UI'et, hvor der er plads til det.
            }
        }

        return liste.OrderBy(t => t.Name).ToList();
    }
}
