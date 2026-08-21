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
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? PreferredModel { get; set; }
    public double Temperature { get; set; } = 0.2;

    /// <summary>Loft over svarets længde. Et referat af et langt møde skal have plads.</summary>
    public int MaxTokens { get; set; } = 2048;

    public required string SystemPrompt { get; set; }
    public required string UserPrompt { get; set; }

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
        ["sprog"] = "Det sprog mødet blev holdt på, som Whisper fandt det",

        // LINKET ER DET, DER GØR ET WEBINARREFERAT NOGET VAERD.
        //
        // Et webinar ligger tit online bagefter. Staar linket i dokumentet,
        // er der vej tilbage til det, der blev VIST paa skaermen - de slides,
        // ingen udskrift kan gengive. Uden det er dokumentet en blindgyde.
        //
        // Feltet er tomt ved almindelige moeder. En skabelon, der bruger det,
        // skal derfor kunne taale, at der ikke staar noget.
        ["kilde"] = "Linket til webinaret, hvis der blev sat et ind. Tomt ved møder",

        // Faelles regler, ikke en oplysning om moedet. Den hoerer hjemme i
        // systemprompten - se Deltagerregler.
        [Deltagerregler.Felt] = "Reglerne for, hvem der kommer på deltagerlisten — fælles for alle skabeloner",

        [Sprogregler.Felt] = "Sproget, dokumentet skal skrives på. Det vælges, når dokumentet oprettes — skriv ikke selv et sprog i skabelonen"
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
    /// <summary>
    /// Systemprompten med de fælles felter sat ind.
    ///
    /// Kun <c>{{deltagerregler}}</c> giver mening her — mødets egne
    /// oplysninger hører til i brugerprompten. Skriver en skabelon feltet i
    /// systemprompten, kommer reglerne med; gør den ikke, sker der
    /// ingenting.
    ///
    /// Alle veje til en model skal bruge DENNE frem for SystemPrompt direkte.
    /// Ellers ville en skabelon virke ét sted og ikke et andet.
    /// </summary>
    public string RenderSystem() => RenderSystem(Sprogregler.Standard);

    /// <summary>
    /// Systemprompten med de fælles felter sat ind, og med dokumentets sprog.
    ///
    /// SPROGET SKAL MED, OGSÅ NÅR SKABELONEN IKKE BEDER OM DET. Skriver en
    /// skabelon ikke <c>{{sprogregler}}</c>, lægges reglen til sidst. Ellers
    /// ville valget virke i de skabeloner, der er skrevet efter ændringen, og
    /// blive ignoreret i dem, brugeren selv har lavet — uden at noget sagde
    /// det.
    ///
    /// Til sidst og ikke først: det er den sidste instruktion, en model læser
    /// før udskriften, og den vinder over en modstridende linje længere oppe.
    /// </summary>
    public string RenderSystem(string dokumentsprog)
    {
        var s = SystemPrompt.Replace("{{" + Deltagerregler.Felt + "}}", Deltagerregler.Tekst);

        var felt = "{{" + Sprogregler.Felt + "}}";
        var regel = Sprogregler.Tekst(dokumentsprog);

        return s.Contains(felt)
            ? s.Replace(felt, regel)
            : s.TrimEnd() + "\n\n" + regel;
    }

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

    /// <summary>
    /// Skabelonen som den ser ud i filen. Samme format som den blev læst i —
    /// en skabelon, der er redigeret i appen, skal stadig kunne åbnes i en
    /// almindelig editor bagefter.
    /// </summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("navn: ").AppendLine(Name);
        if (!string.IsNullOrWhiteSpace(Description)) sb.Append("beskrivelse: ").AppendLine(Description);
        if (!string.IsNullOrWhiteSpace(PreferredModel)) sb.Append("model: ").AppendLine(PreferredModel);
        sb.Append("temperatur: ").AppendLine(
            Temperature.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
        sb.Append("maks_tokens: ").AppendLine(MaxTokens.ToString());
        sb.AppendLine("---");
        sb.AppendLine(SystemPrompt.Trim());
        sb.AppendLine("---");
        sb.AppendLine(UserPrompt.Trim());
        return sb.ToString();
    }

    /// <summary>
    /// Gemmer skabelonen. Skrives til en midlertidig fil og flyttes på plads,
    /// så en afbrudt skrivning ikke efterlader en halv skabelon — den ville
    /// først fejle den dag, man havde brug for den.
    /// </summary>
    public string Save(string? path = null)
    {
        var mål = path ?? Path ?? System.IO.Path.Combine(Directory, Filnavn(Name));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(mål)!);

        var midlertidig = mål + ".ny";
        File.WriteAllText(midlertidig, ToMarkdown(), new UTF8Encoding(false));
        File.Move(midlertidig, mål, overwrite: true);
        return mål;
    }

    /// <summary>Et navn, der kan være et filnavn — æøå og mellemrum oversat.</summary>
    public static string Filnavn(string navn)
    {
        var sb = new StringBuilder();
        foreach (var c in navn.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
            else if (c == 'æ') sb.Append("ae");
            else if (c == 'ø') sb.Append("oe");
            else if (c == 'å') sb.Append("aa");
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }

        var rent = sb.ToString().Trim('-');
        return (rent.Length == 0 ? "skabelon" : rent) + ".md";
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
