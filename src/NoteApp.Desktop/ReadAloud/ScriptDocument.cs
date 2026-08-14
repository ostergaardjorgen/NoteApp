using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace NoteApp.Desktop.ReadAloud;

/// <summary>Ét afsnit i oplæsningen — én "side" i teleprompteren.</summary>
public sealed record ScriptParagraph(int BlockIndex, string Text)
{
    public int WordCount { get; } = Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}

/// <summary>
/// En blok med et tidsmål. Tidsmålene er tjekpunkter, ikke krav — men uden
/// dem kan man ikke se, om man er ved at læse 12 minutter i stedet for 20,
/// og så er optagelsen ubrugelig til det, den skal måle.
/// </summary>
public sealed record ScriptBlock(int Index, string Title, TimeSpan TargetStart, TimeSpan TargetEnd);

/// <summary>
/// Oplæsningsteksten, parset til noget teleprompteren kan vise ét afsnit ad
/// gangen.
///
/// Overskrifter og instruktioner læses IKKE højt — de er til brugeren. Derfor
/// skilles de fra her, i stedet for at brugeren skal huske at springe dem over
/// midt i en optagelse.
/// </summary>
public sealed class ScriptDocument
{
    private static readonly Regex BlokMønster =
        new(@"^##\s+(?<titel>.+?)\s*\[(?<fra>\d+:\d{2})\s*-\s*(?<til>\d+:\d{2})\]\s*$",
            RegexOptions.Compiled);

    private ScriptDocument(string title, IReadOnlyList<string> instructions,
                           IReadOnlyList<ScriptBlock> blocks, IReadOnlyList<ScriptParagraph> paragraphs,
                           string language)
    {
        Title = title;
        Instructions = instructions;
        Blocks = blocks;
        Paragraphs = paragraphs;
        Language = language;
    }

    public string Title { get; }
    public IReadOnlyList<string> Instructions { get; }
    public IReadOnlyList<ScriptBlock> Blocks { get; }
    public IReadOnlyList<ScriptParagraph> Paragraphs { get; }

    /// <summary>
    /// Sproget teksten er skrevet på, læst af frontmatter. Bruges af den
    /// live-lytning, der flytter afsnittet af sig selv: lytter den efter
    /// dansk, mens der læses engelsk, holder den op med at følge med.
    /// </summary>
    public string Language { get; }

    public int TotalWords => Paragraphs.Sum(p => p.WordCount);

    /// <summary>Forventet varighed. 120 ord i minuttet er det tempo, tidsmærkerne i teksten er sat efter.</summary>
    public TimeSpan EstimatedDuration => TimeSpan.FromMinutes(TotalWords / 120.0);

    /// <summary>
    /// Hvor teksten hentes fra. Repo-kopien vinder, hvis den findes: så slår
    /// rettelser i teksten — navne skiftet ud med rigtige — igennem med det
    /// samme, uden at appen skal bygges om. Ellers bruges den indlejrede kopi,
    /// så appen også virker på en maskine uden repoet.
    /// </summary>
    public static string? DiskPath(string fileName = DefaultFile) =>
        RepoFiles.Find("fase0", "oplaesning", fileName);

    public const string DefaultFile = "testtekst.md";

    /// <summary>
    /// De tekster, der kan læses op, i den rækkefølge de giver mening at tage.
    ///
    /// Navnet er det, der står i menuen; filen er den, der bliver læst. Listen
    /// står her frem for i UI'et, så en ny tekst kun skal tilføjes ét sted —
    /// og så det fremgår, hvad hver tekst måler. En tekst uden et formål er
    /// bare tyve minutters oplæsning.
    /// </summary>
    public static readonly IReadOnlyList<(string File, string Key, string Name, string Why, string Next)> Available = new[]
    {
        (DefaultFile, "dansk", "Dansk",
            "Beslutningerne og opgaverne ligger sidst i teksten. Læser du kun de første minutter, måler du ingen af dem.",
            "Det er også den eneste, der er lang nok til at afgøre, om en senere opdatering af modellen faktisk hjalp."),

        ("testtekst-blandet.md", "blandet", "Blandet",
            "Whisper finder sproget én gang, ud fra de første tredive sekunder. Skifter mødet sprog undervejs, opdager den det ikke.",
            "Beslutningen ligger med vilje i den engelske del, så det kan ses, om et sprogskifte koster dig en beslutning."),

        ("testtekst-engelsk.md", "engelsk", "Engelsk",
            "En dansker, der taler engelsk, er det svære tilfælde: accenten trækker genkendelsen mod dansk.",
            "Den viser samtidig, om referatet kommer ud på dansk, selvom mødet ikke var det.")
    };

    /// <summary>
    /// Henter en tekst. Repo-kopien vinder, hvis den findes: så slår rettelser
    /// igennem med det samme, uden at appen skal bygges om. Ellers bruges den
    /// indlejrede kopi, så appen også virker på en maskine uden repoet.
    /// </summary>
    public static ScriptDocument Load(string fileName = DefaultFile) => Parse(RåTekst(fileName));

    /// <summary>
    /// Teksten, som den står i filen — uden fortolkning.
    ///
    /// Målingen skal have den rå udgave: den skal selv afgøre, hvad der læses
    /// højt, og hvad der er vejledning. Gik den gennem <see cref="Parse"/>
    /// først, ville de to have hver sin mening om det, og facit ville afhænge
    /// af, hvem der blev spurgt.
    /// </summary>
    public static string RåTekst(string fileName = DefaultFile)
    {
        var sti = DiskPath(fileName);
        if (sti is not null) return File.ReadAllText(sti, Encoding.UTF8);

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("NoteApp.Desktop." + fileName)
            ?? throw new InvalidOperationException(
                $"Oplæsningsteksten «{fileName}» findes hverken på disken eller i appen.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static ScriptDocument Parse(string markdown)
    {
        var titel = "Oplæsningstekst";
        var sprog = "da";
        var instruktioner = new List<string>();
        var blokke = new List<ScriptBlock>();
        var afsnit = new List<ScriptParagraph>();

        // -1 betyder "før første blok": alt der står der, er vejledning til
        // brugeren og skal aldrig vises i teleprompteren.
        var blokIndex = -1;

        foreach (var rå in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var linje = rå.Trim();

            if (linje.Length == 0 || linje == "---") continue;

            // Frontmatter: kun "sprog" bruges. Den staar foer titlen, saa den
            // fanges her frem for at ende som en vejledningslinje paa skaermen.
            if (blokIndex < 0 && linje.StartsWith("sprog:", StringComparison.OrdinalIgnoreCase))
            {
                sprog = linje[6..].Trim().ToLowerInvariant();
                continue;
            }

            if (linje.StartsWith("# "))
            {
                titel = Rens(linje[2..]);
                continue;
            }

            var m = BlokMønster.Match(linje);
            if (m.Success)
            {
                blokIndex = blokke.Count;
                blokke.Add(new ScriptBlock(
                    blokIndex,
                    Rens(m.Groups["titel"].Value),
                    ParseTid(m.Groups["fra"].Value),
                    ParseTid(m.Groups["til"].Value)));
                continue;
            }

            // Andre overskrifter læses ikke højt.
            if (linje.StartsWith("#")) continue;

            if (blokIndex < 0)
            {
                instruktioner.Add(Rens(linje.TrimStart('-', '*', ' ')));
                continue;
            }

            afsnit.Add(new ScriptParagraph(blokIndex, Rens(linje)));
        }

        return new ScriptDocument(titel, instruktioner, blokke, afsnit, sprog);
    }

    private static TimeSpan ParseTid(string mmss)
    {
        var dele = mmss.Split(':');
        return new TimeSpan(0, int.Parse(dele[0]), int.Parse(dele[1]));
    }

    /// <summary>Fjerner markdown-udmærkning, så teleprompteren viser ren tekst.</summary>
    private static string Rens(string s) =>
        s.Replace("**", string.Empty).Replace("`", string.Empty).Trim();
}
