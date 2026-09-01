using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Pudsningen skal vide, hvilket sprog der blev talt.
/// </summary>
/// <remarks>
/// Sproget er valgt ét sted i appen og sendes med, naar lyden skrives ud. Men
/// pudsningen fik det aldrig at vide - den ryddede op i den tekst, den fik,
/// uden at vaere bundet til noget.
///
/// Set 31-08-2026: en dansk diktering kom tilbage som «Hae, Pia. Eg kunne
/// godt senga» - islandsk-faeroesk i formen. Udskrivningen var rettet, men
/// pudsningen havde ingen grund til at skrive det om, for ingen havde sagt,
/// at det skulle vaere dansk.
/// </remarks>
public class SprogkravTest
{
    /// <summary>
    /// Kravet staar FOERST - foer alt andet i instruktionen.
    /// </summary>
    [Theory]
    [InlineData(Dikteringsformaal.Note)]
    [InlineData(Dikteringsformaal.Mail)]
    [InlineData(Dikteringsformaal.Prompt)]
    [InlineData(Dikteringsformaal.Opgave)]
    public void Hver_instruktion_begynder_med_sprogkravet(Dikteringsformaal formaal)
    {
        var p = Voxtral.Pudseprompt(formaal, navn: null, sprog: "da");

        Assert.StartsWith(Voxtral.Sprogkrav("da"), p);
    }

    [Fact]
    public void Dansk_er_standarden_naar_intet_er_valgt()
    {
        foreach (var sprog in new string?[] { null, "", "da", "  DA  " })
            Assert.Contains("SVAR ALTID PÅ DANSK", Voxtral.Sprogkrav(sprog));
    }

    /// <summary>
    /// Vaelger man engelsk, er det altid engelsk.
    /// </summary>
    /// <remarks>
    /// Brugerens aftale 31-08-2026: «diktering er altid dansk, med mindre man
    /// aendrer sproget til engelsk - saa er det altid engelsk».
    /// </remarks>
    [Fact]
    public void Engelsk_valgt_betyder_altid_engelsk()
    {
        var p = Voxtral.Sprogkrav("en");

        Assert.Contains("SVAR ALTID PÅ ENGELSK", p);
        Assert.DoesNotContain("DANSK", p);
    }

    /// <summary>
    /// De sprog, udskriften faktisk drev hen imod, naevnes ved navn.
    /// </summary>
    /// <remarks>
    /// «Skriv paa dansk» er en regel. «Ser den ud som islandsk, er den hoert
    /// forkert» er en anvisning paa netop dét, der gik galt - og det er
    /// forskellen paa en instruktion, der virker, og en, der lyder rigtig.
    /// </remarks>
    [Fact]
    public void De_sprog_der_blev_maalt_naevnes()
    {
        var p = Voxtral.Sprogkrav("da");

        foreach (var sprog in new[] { "islandsk", "færøsk", "tysk" })
            Assert.Contains(sprog, p);
    }

    [Fact]
    public void Kravet_er_en_ordre_og_ikke_en_oplysning()
    {
        Assert.Contains("Skriv aldrig på et andet sprog", Voxtral.Sprogkrav("da"));
    }
}
