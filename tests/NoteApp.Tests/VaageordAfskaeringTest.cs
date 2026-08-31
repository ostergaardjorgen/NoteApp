using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Vaageordet maa ikke ende i det, man dikterede.
/// </summary>
public class VaageordAfskaeringTest
{
    private static readonly string[] Ord = { "hej pia", "hey pia" };

    [Fact]
    public void Vaageordet_skaeres_af()
    {
        Assert.Equal("opret en note",
            Hensigtstolk.UdenVaageord("Hej Pia, opret en note", Ord));
    }

    /// <summary>
    /// «BYE, PIA» STOD I SOEGEFELTET 31-08-2026.
    /// </summary>
    /// <remarks>
    /// Det, der ender i teksten, er Voxtrals udskrift af halen - ikke det ord,
    /// motoren lyttede efter. Stavemaaden er derfor en anden hver gang.
    /// </remarks>
    [Theory]
    [InlineData("Bye, Pia. Vil du gerne se noget?", "Vil du gerne se noget?")]
    [InlineData("Hey Pia vil du gerne se noget?", "vil du gerne se noget?")]
    [InlineData("Hi Pia, opret en note", "opret en note")]
    [InlineData("Haj Pia - skriv en mail", "skriv en mail")]
    public void En_hilsen_til_navnet_skaeres_ogsaa_af(string sagt, string vented)
    {
        Assert.Equal(vented, Hensigtstolk.UdenVaageord(sagt, Ord));
    }

    /// <summary>
    /// «Pia» kan vaere et menneske, og saa skal saetningen blive staaende.
    /// </summary>
    [Theory]
    [InlineData("Jørgen og Pia skal mødes på torsdag")]
    [InlineData("Send den til Pia i morgen")]
    public void Et_menneske_ved_navn_Pia_roeres_ikke(string sagt)
    {
        Assert.Equal(sagt, Hensigtstolk.UdenVaageord(sagt, Ord));
    }

    [Fact]
    public void To_ord_alene_skaeres_ikke_ned_til_ingenting()
    {
        // «Hej Pia» og intet andet: der er ikke noget tilbage at diktere, og
        // saa skal teksten ikke blive til en tom streng, der ligner et svar.
        Assert.Equal("Hej Pia", Hensigtstolk.UdenVaageord("Hej Pia", new[] { "goddag pia" }));
    }

    /// <summary>
    /// Ogsaa naar der IKKE er en hensigt at genkende.
    /// </summary>
    /// <remarks>
    /// AFSKAERINGEN LAA FOR SENT. Den skete inde i ruteren - altsaa kun, naar
    /// der var en hensigt som «opret en note» at genkende. Sagde man «Hej Pia,
    /// kan vi optage det nu», svarede ruteren falsk, teksten gik den
    /// almindelige vej, og «Hej Pia» fulgte med baade i udklipsholderen og i
    /// noten. Set 31-08-2026.
    /// </remarks>
    [Theory]
    [InlineData("Hej Pia. Kan vi optage det nu, eller hvordan har du det med det?",
                "Kan vi optage det nu, eller hvordan har du det med det?")]
    [InlineData("Hej Pia, jeg skal huske at ringe til min mor",
                "jeg skal huske at ringe til min mor")]
    public void Vaageordet_skaeres_af_ogsaa_uden_en_hensigt(string sagt, string vented)
    {
        Assert.Equal(vented, Hensigtstolk.UdenVaageord(sagt, new[] { "hej pia" }));
    }

    /// <summary>
    /// Blev der ikke sagt andet end ordet, maa teksten ikke blive tom.
    /// </summary>
    /// <remarks>
    /// En tom note er vaerre end en med to ord i: saa ved man ikke, om der
    /// blev optaget noget som helst.
    /// </remarks>
    [Fact]
    public void Kun_vaageordet_giver_en_tom_rest()
    {
        Assert.Equal("", Hensigtstolk.UdenVaageord("Hej Pia.", new[] { "hej pia" }));
    }
}
