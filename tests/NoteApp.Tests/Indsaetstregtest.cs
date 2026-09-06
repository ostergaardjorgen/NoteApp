using System.Xml.Linq;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Indsætstregen ved træk og slip må ikke fylde noget.
/// </summary>
/// <remarks>
/// ============ LISTEN HOPPEDE, OG DET VAR STREGENS SKYLD ============
///
/// Stregen lå i sin egen række med <c>Height="Auto"</c>: fire pixels streg
/// plus fire pixels luft. Kortet blev derfor otte pixels højere i det
/// øjeblik, stregen kom frem — målt 05-09-2026 — og alt nedenunder rykkede
/// ned.
///
/// Det er dét, der fik listen til at hoppe, mens man trak: kortet under musen
/// flyttede sig, markøren stod pludselig på den anden halvdel eller på
/// naboen, DragOver fyrede igen, stregen flyttede, og layoutet rykkede
/// tilbage. En løkke, der kun stopper, når man holder musen helt stille.
///
/// Prøven her er en tekstprøve og ikke en måling — den rigtige måling kræver
/// en skærm. Den holder de to ting fast, som gjorde det galt: at stregen ikke
/// står i sin egen række, og at musen går forbi den.
/// </remarks>
public class Indsaetstregtest
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

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

    private static IEnumerable<XElement> Streger()
    {
        var fil = Path.Combine(Rod(), "src", "NoteApp.Desktop", "Search", "SearchView.xaml");
        Assert.True(File.Exists(fil), fil);

        var doc = XDocument.Load(fil);

        return doc.Descendants(X + "Border")
            .Where(b => (string?)b.Attribute(Xaml + "Name") is "Indsaetfoer" or "Indsaetefter");
    }

    [Fact]
    public void Begge_streger_findes()
    {
        var navne = Streger().Select(b => (string)b.Attribute(Xaml + "Name")!).ToList();

        Assert.Contains("Indsaetfoer", navne);
        Assert.Contains("Indsaetefter", navne);
    }

    [Fact]
    public void Stregen_staar_ikke_i_sin_egen_raekke()
    {
        foreach (var streg in Streger())
        {
            var navn = (string)streg.Attribute(Xaml + "Name")!;

            // Grid.Row betyder, at den har sin egen plads i opstillingen - og
            // en plads, der er tom, naar stregen er slukket, og fylder, naar
            // den er taendt. Det er praecis dét, der fik listen til at hoppe.
            Assert.True(streg.Attribute("Grid.Row") is null,
                $"{navn} staar i sin egen Grid-raekke igen. Saa vokser kortet, "
                + "naar stregen kommer frem, og listen hopper under traekket.");

            var margin = (string?)streg.Attribute("Margin");

            Assert.True(margin is null,
                $"{navn} har en Margin ({margin}). En margin taeller med i "
                + "maalingen: enten vokser kortet, eller ogsaa aeder en negativ "
                + "margin stregens hoejde, saa den forsvinder helt. Flyt den "
                + "med en RenderTransform i stedet.");
        }
    }

    [Fact]
    public void Musen_gaar_forbi_stregen()
    {
        foreach (var streg in Streger())
        {
            var navn = (string)streg.Attribute(Xaml + "Name")!;

            // Kan stregen selv rammes, bliver den maalet som traekkets maal -
            // og saa peger den paa sig selv i stedet for paa kortet.
            Assert.Equal("False", (string?)streg.Attribute("IsHitTestVisible"));

            Assert.False(string.IsNullOrEmpty((string?)streg.Attribute("Height")),
                $"{navn} skal have en fast hoejde, ellers kan den ikke ses.");
        }
    }
}
