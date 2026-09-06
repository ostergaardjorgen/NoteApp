using PdfSharp.Fonts;

namespace NoteApp.Core.Documents;

/// <summary>
/// Finder skrifttyperne til PDF'erne i Windows' egen skriftmappe.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN SKAL SKRIVES SELV ============
///
/// PDFsharps kernepakke har ingen skrifttyper med sig. Den kaster, første
/// gang den skal tegne et bogstav — «No appropriate font found for family
/// name». Det er ikke en fejl, der kan ses, når koden skrives; den kommer
/// først, når nogen bygger et dokument. Målt 06-09-2026.
///
/// Der findes en platformsudgave af pakken, som spørger Windows selv. Den
/// trækker WPF ind i NoteApp.Core, og Core skal kunne køre uden en skærm —
/// det er dét, der gør, at prøverne kan bygge en PDF. Fyrre linjer her er
/// billigere end den afhængighed.
///
/// ============ SKRIFTEN SKAL VÆRE DER PÅ ENHVER WINDOWS ============
///
/// Der ledes efter flere, og den første, der findes, vinder. Segoe UI har
/// fulgt med Windows siden Vista; Calibri følger med Office og med Windows;
/// Arial har været der altid. Findes ingen af dem, kastes der med en besked,
/// et menneske kan handle på — ikke med en fejl om et navnerum.
/// </remarks>
public sealed class Skriftfinder : IFontResolver
{
    /// <summary>Skriftmappen. Windows' egen.</summary>
    private static readonly string Mappe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    /// <summary>
    /// Familierne, der ledes efter — i rækkefølge.
    /// </summary>
    /// <remarks>
    /// Filnavnene er Windows' egne: almindelig, fed, kursiv, fed kursiv.
    /// </remarks>
    private static readonly (string Familie, string[] Filer)[] Kendte =
    {
        ("segoe ui", new[] { "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf" }),
        ("calibri", new[] { "calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf" }),
        ("arial", new[] { "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf" }),
        ("verdana", new[] { "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf" }),
    };

    private static readonly Dictionary<string, byte[]> Laest = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Laas = new();

    /// <summary>Sætter finderen ind. Kan kaldes så tit man vil.</summary>
    public static void Sikr()
    {
        lock (Laas)
        {
            GlobalFontSettings.FontResolver ??= new Skriftfinder();
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var oensket = (familyName ?? "").Trim().ToLowerInvariant();

        // DEN OENSKEDE FOERST, DEREFTER RAEKKEFOELGEN. Beder skabelonen om
        // Calibri, og den findes, er det den, der bruges; ellers den naeste,
        // der er der. En PDF med en anden skrift er stadig en PDF; en, der
        // kaster, er ingenting.
        var raekke = Kendte
            .OrderByDescending(k => k.Familie == oensket)
            .ToList();

        foreach (var (familie, filer) in raekke)
        {
            var nr = (isBold, isItalic) switch
            {
                (true, true) => 3,
                (true, false) => 1,
                (false, true) => 2,
                _ => 0,
            };

            var sti = Path.Combine(Mappe, filer[nr]);

            // Findes den fede ikke, tages den almindelige. En manglende
            // variant maa ikke koste hele dokumentet.
            if (!File.Exists(sti)) sti = Path.Combine(Mappe, filer[0]);
            if (!File.Exists(sti)) continue;

            var navn = familie + "#" + nr;

            lock (Laas)
            {
                if (!Laest.ContainsKey(navn))
                {
                    try { Laest[navn] = File.ReadAllBytes(sti); }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }
                }
            }

            return new FontResolverInfo(navn);
        }

        throw new InvalidOperationException(
            "Der blev ikke fundet nogen skrifttype til PDF'en. Der blev ledt efter "
            + string.Join(", ", Kendte.Select(k => k.Familie))
            + $" i {Mappe}.");
    }

    public byte[]? GetFont(string faceName)
    {
        lock (Laas) return Laest.TryGetValue(faceName, out var data) ? data : null;
    }
}
