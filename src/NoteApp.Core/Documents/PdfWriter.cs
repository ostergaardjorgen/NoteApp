using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace NoteApp.Core.Documents;

/// <summary>
/// Skriver et dokument som PDF — samme markdown som <see cref="DocxWriter"/>.
/// </summary>
/// <remarks>
/// ============ HVORFOR BEGGE DELE ============
///
/// Word er til det, der skal rettes videre i. PDF er til det, der skal
/// sendes: et tilbud, en projektbeskrivelse, en studieplan, man vil have i
/// hånden. De to formater svarer på hvert sit spørgsmål, og derfor er det et
/// valg og ikke en indstilling, man sætter én gang.
///
/// DEN SAMME MARKDOWN FORSTÅS BEGGE STEDER. «# », «## », «### », «- » og
/// **fed**. Kunne det ene format mere end det andet, ville det samme
/// dokument se forskelligt ud alt efter, hvad man valgte — og så ville
/// valget handle om andet end formatet.
///
/// SKRIFTEN TAGES FRA WINDOWS. PDFsharp har ingen skrifttyper med sig;
/// uden den linje kaster den, første gang den skal tegne et bogstav.
/// </remarks>
public static class PdfWriter
{
    /// <summary>
    /// Skriften tages fra Windows' egen mappe.
    /// </summary>
    /// <remarks>
    /// PDFsharps kernepakke har ingen skrifttyper med sig og kaster, foerste
    /// gang den skal tegne et bogstav. Se Skriftfinder for hvorfor den er
    /// skrevet selv frem for at traekke en platformspakke ind i Core.
    /// </remarks>
    private static void SikrSkrifter() => Skriftfinder.Sikr();

    public static void Write(string path, string title, string markdown,
                             IReadOnlyList<(string Navn, string Værdi)>? forside = null)
    {
        SikrSkrifter();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var doc = new Document { Info = { Title = title } };

        Typografier(doc);

        var sektion = doc.AddSection();
        sektion.PageSetup.PageFormat = PageFormat.A4;
        sektion.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        sektion.PageSetup.BottomMargin = Unit.FromCentimeter(2.2);
        sektion.PageSetup.LeftMargin = Unit.FromCentimeter(2.5);
        sektion.PageSetup.RightMargin = Unit.FromCentimeter(2.5);

        // SIDETAL I BUNDEN. Et dokument paa tolv sider uden sidetal kan ikke
        // henvises til - og det er netop dét, den slags dokumenter er til.
        var bund = sektion.Footers.Primary.AddParagraph();
        bund.Format.Alignment = ParagraphAlignment.Center;
        bund.Format.Font.Size = 8;
        bund.Format.Font.Color = Colors.Gray;
        bund.AddPageField();

        var overskrift = sektion.AddParagraph(title);
        overskrift.Style = "Titel";

        if (forside is { Count: > 0 })
        {
            foreach (var (navn, vaerdi) in forside)
            {
                if (string.IsNullOrWhiteSpace(vaerdi)) continue;

                var p = sektion.AddParagraph();
                p.Style = "Forside";
                p.AddFormattedText(navn + ": ", TextFormat.Bold);
                p.AddText(vaerdi);
            }

            sektion.AddParagraph().Format.SpaceAfter = Unit.FromPoint(10);
        }

        Krop(sektion, markdown);

        var midlertidig = path + ".ny";

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(midlertidig);

        File.Move(midlertidig, path, overwrite: true);
    }

    private static void Typografier(Document doc)
    {
        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = "Calibri";
        normal.Font.Size = 11;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(7);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = 1.15;

        var titel = doc.Styles.AddStyle("Titel", "Normal");
        titel.Font.Size = 22;
        titel.Font.Bold = true;
        titel.ParagraphFormat.SpaceAfter = Unit.FromPoint(16);

        var forside = doc.Styles.AddStyle("Forside", "Normal");
        forside.Font.Size = 10;
        forside.Font.Color = Colors.DimGray;
        forside.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);

        for (var n = 1; n <= 3; n++)
        {
            var h = doc.Styles[$"Heading{n}"]!;
            h.Font.Name = "Calibri";
            h.Font.Bold = true;
            h.Font.Size = n switch { 1 => 17, 2 => 14, _ => 12 };
            h.Font.Color = Colors.Black;
            h.ParagraphFormat.SpaceBefore = Unit.FromPoint(n == 1 ? 16 : 12);
            h.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
            h.ParagraphFormat.KeepWithNext = true;
        }

        var punkt = doc.Styles.AddStyle("Punkt", "Normal");
        punkt.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.6);
        punkt.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
    }

    /// <summary>
    /// Selve teksten. Samme regler som i <see cref="DocxWriter"/>.
    /// </summary>
    private static void Krop(Section sektion, string markdown)
    {
        foreach (var raa in (markdown ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var linje = raa.Trim();

            if (linje.Length == 0) continue;

            // En vandret streg er en adskillelse i markdown og ikke tekst.
            if (linje.Length >= 3 && linje.All(c => c is '-' or '*' or '_'))
            {
                var streg = sektion.AddParagraph();
                streg.Format.Borders.Bottom.Width = 0.5;
                streg.Format.Borders.Bottom.Color = Colors.LightGray;
                streg.Format.SpaceBefore = Unit.FromPoint(8);
                streg.Format.SpaceAfter = Unit.FromPoint(8);
                continue;
            }

            if (linje.StartsWith("### ", StringComparison.Ordinal))
            {
                Fed(sektion.AddParagraph(), linje[4..]).Style = "Heading3";
                continue;
            }

            if (linje.StartsWith("## ", StringComparison.Ordinal))
            {
                Fed(sektion.AddParagraph(), linje[3..]).Style = "Heading2";
                continue;
            }

            if (linje.StartsWith("# ", StringComparison.Ordinal))
            {
                Fed(sektion.AddParagraph(), linje[2..]).Style = "Heading1";
                continue;
            }

            if (linje.StartsWith("- ", StringComparison.Ordinal)
                || linje.StartsWith("* ", StringComparison.Ordinal))
            {
                var p = sektion.AddParagraph();
                p.Style = "Punkt";
                p.AddText("•  ");
                Fed(p, linje[2..]);
                continue;
            }

            // «> » er et citat i markdown. Det bruges til bemaerkningen om, at
            // materialet blev afkortet - og den maa ikke staa som en linje,
            // der begynder med et staerkt-tegn.
            if (linje.StartsWith("> ", StringComparison.Ordinal))
            {
                var p = sektion.AddParagraph();
                p.Format.LeftIndent = Unit.FromCentimeter(0.5);
                p.Format.Font.Color = Colors.DimGray;
                p.Format.Font.Italic = true;
                Fed(p, linje[2..]);
                continue;
            }

            Fed(sektion.AddParagraph(), linje);
        }
    }

    /// <summary>**fed** oversættes; resten er ren tekst.</summary>
    /// <remarks>
    /// Samme regel som i DocxWriter: er der et ULIGE antal stjernepar, er det
    /// sidste stykke almindelig tekst. En stjerne, der står alene, er et
    /// tegn og ikke en formatering.
    /// </remarks>
    private static Paragraph Fed(Paragraph p, string tekst)
    {
        var dele = tekst.Split("**");

        for (var i = 0; i < dele.Length; i++)
        {
            if (dele[i].Length == 0) continue;

            var fed = i % 2 == 1 && (i < dele.Length - 1 || dele.Length % 2 == 1);

            if (fed) p.AddFormattedText(dele[i], TextFormat.Bold);
            else p.AddText(dele[i]);
        }

        return p;
    }
}
