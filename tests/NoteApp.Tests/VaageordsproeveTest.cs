using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Brugerens egen maade at sige vaageordet paa.
/// </summary>
/// <remarks>
/// whisper-command i guided mode sammenligner det, den hoerer, med de
/// BOGSTAVER, der staar paa listen. Staar der «hej pia», og modellen hoerer
/// stemmen som «hej bia», rammer den skaevt hver gang.
///
/// Maalt 31-08-2026: en af brugerens dikteringer landede som «Hej Bia, lav
/// nogle lydboelger», og hans «Hej Pia» blev bedoemt til 0,109 og 0,141 -
/// under graensen, saa der skete ingenting.
/// </remarks>
public class VaageordsproeveTest
{
    [Theory]
    [InlineData("Hej Pia.", "hej pia")]
    [InlineData(" Hej Bia! ", "hej bia")]
    // Et enkelt ord duer ikke som vaageord - se Vaageord.Rens. Det er
    // ogsaa dét, der holder whispers hallucinationer paa tavse klip ude:
    // «[BLANK_AUDIO]» og «[MUSIK]» er ét ord og bliver til ingenting.
    [InlineData("[BLANK_AUDIO]", "")]
    [InlineData("[MUSIK]", "")]
    [InlineData("Hej, Pia?", "hej pia")]
    public void Det_hoerte_renses_til_listeform(string hoert, string vented)
    {
        Assert.Equal(vented, Vaageordsproeve.Rens(hoert));
    }

    [Fact]
    public void Det_der_blev_hoert_flere_gange_kommer_foerst()
    {
        var forsoeg = new[]
        {
            new Vaageordsforsoeg("Hej Bia", 1.1),
            new Vaageordsforsoeg("Hej Pia", 1.0),
            new Vaageordsforsoeg("Hej Bia", 1.2),
        };

        var valgt = Vaageordsproeve.Vaelg(forsoeg);

        Assert.Equal("hej bia", valgt[0]);
        Assert.Contains("hej pia", valgt);
    }

    /// <summary>
    /// Listen maa ikke vokse ukontrolleret.
    /// </summary>
    /// <remarks>
    /// Hvert udtryk mere deler sandsynligheden med de andre, saa hvert enkelt
    /// bliver svagere. Det var praecis dét, der gjorde «hej pia» og «hey pia»
    /// til et problem.
    /// </remarks>
    [Fact]
    public void Der_kommer_hoejst_tre_stavemaader_paa()
    {
        var forsoeg = new[]
        {
            new Vaageordsforsoeg("hej pia", 1), new Vaageordsforsoeg("hej bia", 1),
            new Vaageordsforsoeg("hey pja", 1), new Vaageordsforsoeg("hej pja", 1),
            new Vaageordsforsoeg("haj bia", 1),
        };

        Assert.Equal(3, Vaageordsproeve.Vaelg(forsoeg).Count);
    }

    [Fact]
    public void Intet_brugbart_giver_ingenting()
    {
        var forsoeg = new[] { new Vaageordsforsoeg("", 1), new Vaageordsforsoeg("   ", 1) };

        Assert.Empty(Vaageordsproeve.Vaelg(forsoeg));
    }

    /// <summary>
    /// Et vaageord paa ét bogstav ville udloese paa alt.
    /// </summary>
    [Theory]
    [InlineData("hej pia", true)]
    [InlineData("hej bia", true)]
    [InlineData("pia", false)]      // ét ord er for lidt - se Vaageord.Rens
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData("og saa gik vi ud i koekkenet og lavede kaffe til alle sammen", false)]
    public void Kun_noget_der_ligner_et_vaageord_duer(string hoert, bool vented)
    {
        Assert.Equal(vented, Vaageordsproeve.Duer(hoert));
    }

    /// <summary>
    /// Radioen skal have et sted at gaa hen.
    /// </summary>
    /// <remarks>
    /// Guided mode KAN ikke svare «ingenting» - den vaelger altid noget fra
    /// listen. Er der kun danske hverdagsvendinger paa den, bliver musik og
    /// engelsk tale tvunget over paa dem.
    /// </remarks>
    [Fact]
    public void Listen_rummer_ogsaa_musik_og_engelsk()
    {
        var liste = Vaageordsliste.Byg(new string[0], "da");

        Assert.Contains("musik", liste);
        Assert.Contains(liste, o => o.Contains("the", StringComparison.OrdinalIgnoreCase));
    }
}
