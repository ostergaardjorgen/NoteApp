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
                           IReadOnlyList<ScriptBlock> blocks, IReadOnlyList<ScriptParagraph> paragraphs)
    {
        Title = title;
        Instructions = instructions;
        Blocks = blocks;
        Paragraphs = paragraphs;
    }

    public string Title { get; }
    public IReadOnlyList<string> Instructions { get; }
    public IReadOnlyList<ScriptBlock> Blocks { get; }
    public IReadOnlyList<ScriptParagraph> Paragraphs { get; }

    public int TotalWords => Paragraphs.Sum(p => p.WordCount);

    /// <summary>Forventet varighed. 120 ord i minuttet er det tempo, tidsmærkerne i teksten er sat efter.</summary>
    public TimeSpan EstimatedDuration => TimeSpan.FromMinutes(TotalWords / 120.0);

    /// <summary>
    /// Hvor teksten hentes fra. Repo-kopien vinder, hvis den findes: så slår
    /// rettelser i teksten — navne skiftet ud med rigtige — igennem med det
    /// samme, uden at appen skal bygges om. Ellers bruges den indlejrede kopi,
    /// så appen også virker på en maskine uden repoet.
    /// </summary>
    public static string? DiskPath() => RepoFiles.Find("fase0", "oplaesning", "testtekst.md");

    public static ScriptDocument Load()
    {
        var sti = DiskPath();
        if (sti is not null) return Parse(File.ReadAllText(sti, Encoding.UTF8));

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("NoteApp.Desktop.testtekst.md")
            ?? throw new InvalidOperationException("Oplæsningsteksten er ikke indlejret i appen.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static ScriptDocument Parse(string markdown)
    {
        var titel = "Oplæsningstekst";
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

        return new ScriptDocument(titel, instruktioner, blokke, afsnit);
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
