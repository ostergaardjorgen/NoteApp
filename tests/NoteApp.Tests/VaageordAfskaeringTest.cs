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
}
