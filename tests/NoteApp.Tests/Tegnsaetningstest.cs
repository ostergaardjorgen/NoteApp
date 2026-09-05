using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Er der tekst på skærmen, hvor tegnene er gået i stykker?
/// </summary>
/// <remarks>
/// ============ DET SKETE, OG DET KAN IKKE SES I EN DIFF ============
///
/// 05-09-2026 blev fem tekster skrevet ind gennem et mellemled, der kodede
/// dem som UTF-8 og læste dem som latin-1. «Gennemse …» blev til «Gennemse
/// â€¦», «æ» blev til «Ã¦», og en tankestreg blev til tre tegn.
///
/// Ingen af delene fejler. Filen er gyldig JSON, appen starter, prøverne
/// passerer — og på skærmen står der volapyk. Det blev opdaget, fordi
/// brugeren kiggede på sin egen skærm og skrev «tegnsætning er itu».
///
/// Der er fire slags skade, og de har hver sit fingeraftryk:
///
///   ERSTATNINGSTEGN   U+FFFD. Noget blev læst med den forkerte kodning, og
///                     tegnet er tabt for altid.
///   STYRETEGN         U+0080-U+009F. De findes ikke som tekst; står de i en
///                     streng, er strengen læst som latin-1.
///   DOBBELTKODNING    «Ã¦», «Ã¸», «â€”». UTF-8-bytes læst én ad gangen.
///   HTML-ENTITET      «&#x2022;» er en XAML-entitet. Inde i en JSON-tekst er
///                     den ikke andet end de otte tegn, der står der — og så
///                     står de otte tegn på skærmen. Det gjorde de.
/// </remarks>
public class Tegnsaetningstest
{
    /// <summary>Repoets rod, fundet ved at gaa opad fra det byggede.</summary>
    private static string Rod()
    {
        var mappe = new DirectoryInfo(AppContext.BaseDirectory);

        while (mappe is not null)
        {
            if (Directory.Exists(Path.Combine(mappe.FullName, "src", "NoteApp.Desktop")))
                return mappe.FullName;

            mappe = mappe.Parent;
        }

        throw new DirectoryNotFoundException(
            "Fandt ikke repoets rod fra " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// UTF-8-bytes læst én ad gangen: et indledende Ã, Â eller â efterfulgt af
    /// den byte, der hørte til det.
    /// </summary>
    private static readonly Regex Dobbeltkodet =
        new("[ÂÃâ][-¿–-…]", RegexOptions.Compiled);

    public static TheoryData<string> Sprogfiler => new() { "da", "en" };

    [Theory]
    [MemberData(nameof(Sprogfiler))]
    public void Ingen_tekst_paa_skaermen_har_oedelagte_tegn(string sprog)
    {
        var fil = Path.Combine(Rod(), "src", "NoteApp.Core", "sprog", sprog + ".json");
        Assert.True(File.Exists(fil), fil);

        using var doc = JsonDocument.Parse(File.ReadAllText(fil));

        var daarlige = new List<string>();
        Gaa(doc.RootElement, "", daarlige);

        Assert.True(daarlige.Count == 0,
            $"{sprog}.json har {daarlige.Count} tekst(er) med ødelagte tegn:"
            + Environment.NewLine + string.Join(Environment.NewLine, daarlige));
    }

    private static void Gaa(JsonElement e, string sti, List<string> daarlige)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                    Gaa(p.Value, sti.Length == 0 ? p.Name : sti + "." + p.Name, daarlige);
                return;

            case JsonValueKind.Array:
                var i = 0;
                foreach (var b in e.EnumerateArray())
                    Gaa(b, $"{sti}[{i++}]", daarlige);
                return;

            case JsonValueKind.String:
                var grund = Skade(e.GetString());
                if (grund is not null) daarlige.Add($"  {sti}: {grund}");
                return;
        }
    }

    private static string? Skade(string? tekst)
    {
        if (string.IsNullOrEmpty(tekst)) return null;

        if (tekst.Contains('�'))
            return "erstatningstegn (U+FFFD) — noget blev læst med forkert kodning";

        foreach (var c in tekst)
            if (c >= '' && c <= '')
                return $"styretegn U+{(int)c:X4} — strengen er læst som latin-1";

        if (Dobbeltkodet.IsMatch(tekst))
            return "dobbeltkodet — UTF-8 læst én byte ad gangen («Ã¦» i stedet for «æ»)";

        if (tekst.Contains("&#x", StringComparison.OrdinalIgnoreCase))
            return "HTML-entitet — den vises som sine egne tegn, ikke som tegnet";

        return null;
    }
}
