using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Afsnittet om skytjenester skal kunne FINDES.
/// </summary>
/// <remarks>
/// Det er hele pointen med at skrive det. Den, der leder efter «onedrive»
/// eller «android», leder ikke efter et afsnit, der hedder «Lydfiler
/// udefra» — han skriver mærkenavnet på det, han bruger, og skal have svaret
/// første gang.
///
/// Søgningen vægter titel og undertitel tungest og tæller derefter
/// forekomster i teksten. Prøven her holder fast i, at afsnittet vinder på de
/// ord, brugeren faktisk skriver, i stedet for at stole på, at det gør det.
/// </remarks>
public class SkytjenesteTest
{
    private const string Afsnittet = "65-skytjenester";

    /// <summary>
    /// Mærkenavne. De peger kun ét sted, og så skal afsnittet ligge øverst.
    /// </summary>
    public static TheoryData<string> Maerkenavne => new()
    {
        "onedrive",
        "icloud",
        "apple",
        "android",
        "iphone",
        "dropbox",
    };

    /// <summary>
    /// Ord, der med rimelighed kan betyde to ting.
    /// </summary>
    /// <remarks>
    /// «Google» kan være Google Drev og kan være Google Kalender — og appen
    /// kan begge dele. «Telefon» og «skytjeneste» nævnes også, hvor filer
    /// udefra beskrives. Her er kravet, at afsnittet er blandt de første tre,
    /// og ikke at det slår de andre.
    ///
    /// EN PRØVE, DER KRÆVEDE FØRSTEPLADSEN, VILLE VÆRE EN OPFORDRING TIL AT
    /// FYLDE MÆRKENAVNE I TEKSTEN, indtil tallet passede. Det ville gøre
    /// håndbogen dårligere for at få en prøve til at lyse grønt.
    /// </remarks>
    public static TheoryData<string> Naerliggende => new()
    {
        "google",
        "telefon",
        "skytjeneste",
    };

    [Theory]
    [MemberData(nameof(Maerkenavne))]
    public void Maerkenavnet_giver_afsnittet_som_foerste_traef(string ord)
    {
        using var p = new Proevemappe();

        var traef = Hjaelp.Soeg(ord);

        Assert.True(traef.Count > 0, $"«{ord}» gav ingen træf overhovedet");

        Assert.True(traef[0].Afsnit.Id == Afsnittet,
            $"«{ord}» ramte «{traef[0].Afsnit.Id}» først og ikke «{Afsnittet}». "
            + "Rækkefølgen: " + string.Join(", ", traef.Take(4).Select(t => t.Afsnit.Id)));
    }

    [Theory]
    [MemberData(nameof(Naerliggende))]
    public void Det_naerliggende_ord_faar_afsnittet_frem(string ord)
    {
        using var p = new Proevemappe();

        var traef = Hjaelp.Soeg(ord).Take(3).Select(t => t.Afsnit.Id).ToList();

        Assert.True(traef.Contains(Afsnittet),
            $"«{ord}» fik ikke «{Afsnittet}» frem blandt de tre første. "
            + "Rækkefølgen: " + string.Join(", ", traef));
    }

    [Fact]
    public void Afsnittet_findes_paa_begge_sprog()
    {
        using var p = new Proevemappe();

        foreach (var sprog in new[] { "da", "en" })
        {
            var a = Hjaelp.Alle(sprog).FirstOrDefault(x => x.Id == Afsnittet);

            Assert.True(a is not null, $"{Afsnittet} mangler på {sprog}");
            Assert.False(string.IsNullOrWhiteSpace(a!.Undertitel),
                $"{Afsnittet} mangler undertitel på {sprog} — den vejer tungt i søgningen");
        }
    }

    [Fact]
    public void Hele_kaeden_staar_beskrevet()
    {
        using var p = new Proevemappe();

        var tekst = Hjaelp.Alle("da").Single(a => a.Id == Afsnittet).Tekst.ToLowerInvariant();

        // De fire led. Mangler ét af dem, er vejledningen en opskrift, der
        // ikke kan foelges til ende.
        foreach (var led in new[] { "iphone", "android", "icloud", "onedrive",
                                    "google drev", "dropbox", "indstillinger" })
            Assert.True(tekst.Contains(led), $"afsnittet naevner ikke «{led}»");
    }

    /// <summary>
    /// Peger hvert tip på et afsnit, der findes?
    /// </summary>
    /// <remarks>
    /// «Læs mere» er hele grunden til, at et tip ikke bare er en påstand.
    /// Peger det på et afsnit, der ikke findes, sker der ingenting, når man
    /// trykker — og det er værre end intet link.
    /// </remarks>
    [Fact]
    public void Hvert_tip_peger_paa_et_afsnit_der_findes()
    {
        using var p = new Proevemappe();

        var id = Hjaelp.Alle("da").Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sprog in new[] { "da", "en" })
        {
            Sprog.Skift(sprog);

            foreach (var tip in Tips.Alle())
                Assert.True(id.Contains(tip.Afsnit),
                    $"[{sprog}] tippet peger på «{tip.Afsnit}», som ikke findes: {tip.Tekst}");
        }

        Sprog.Skift("da");
    }

    [Fact]
    public void Telefonen_er_med_i_tipsene_paa_begge_sprog()
    {
        foreach (var sprog in new[] { "da", "en" })
        {
            Sprog.Skift(sprog);

            var vores = Tips.Alle().Count(t => t.Afsnit == Afsnittet);

            Assert.True(vores >= 3,
                $"[{sprog}] der er kun {vores} tip om skytjenester. "
                + "Ét tip er en oplysning; flere vinkler er det, der får den læst.");
        }

        Sprog.Skift("da");
    }
}
