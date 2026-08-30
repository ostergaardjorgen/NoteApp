using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvilke sprog Voxtral tager imod.
///
/// DANSK ER IKKE ET AF DEM, og det kostede en diktering at opdage.
///
/// Da appen begyndte at sende sproget med, blev hele kaldet afvist. Ordret
/// fra endepunktet, målt 30-08-2026:
///
///   Got unsupported language `da`, should be one of: ['ar', 'en', 'de',
///   'es', 'fr', 'hi', 'it', 'nl', 'pt', 'zh', 'ru', 'ko', 'ja']
///
/// Modellen KAN skrive dansk ud — det har den gjort hele tiden. Den vil bare
/// ikke have det som instruks. Sender man det alligevel, kommer der ingen
/// tekst overhovedet, og det er værre end en udskrift på det forkerte sprog.
/// </summary>
public class VoxtralsprogTest
{
    [Fact]
    public void Dansk_kan_ikke_vaelges()
    {
        // DET ER HELE POINTEN. Staar den her og fejler en dag, har
        // leverandoeren aabnet for dansk - og saa skal Ledetraad-omvejen vaek.
        Assert.False(Voxtral.Kendes("da"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("nl")]
    public void De_understoettede_kan(string kode)
    {
        Assert.True(Voxtral.Kendes(kode));
    }

    [Fact]
    public void Store_bogstaver_og_mellemrum_er_lige_meget()
    {
        Assert.True(Voxtral.Kendes(" EN "));
    }

    [Fact]
    public void Tomt_og_auto_kan_ikke_vaelges()
    {
        Assert.False(Voxtral.Kendes(null));
        Assert.False(Voxtral.Kendes(""));
        Assert.False(Voxtral.Kendes("auto"));
    }

    [Fact]
    public void De_nordiske_faar_en_ledetraad()
    {
        // Naar sproget ikke kan vaelges, er en saetning paa sproget det
        // bedste, der kan goeres. Den skal VAERE paa sproget - en engelsk
        // saetning ville traekke den forkerte vej.
        Assert.Contains("dansk", Voxtral.Ledetraad("da")!);
        Assert.Contains("norsk", Voxtral.Ledetraad("no")!);
        Assert.Contains("svenska", Voxtral.Ledetraad("sv")!);
    }

    [Fact]
    public void Et_sprog_der_kan_vaelges_har_ingen_ledetraad()
    {
        // Der skal ikke baade instrueres og antydes. Kan sproget vaelges, er
        // det valgt, og saa er en ledetraad stoej i prompten.
        Assert.Null(Voxtral.Ledetraad("en"));
        Assert.Null(Voxtral.Ledetraad("auto"));
        Assert.Null(Voxtral.Ledetraad(null));
    }
}
