using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Sproget, brugeren taler - der skal altid vaere et svar.
/// </summary>
/// <remarks>
/// «MitSprog ?? "da"» fangede kun null. En TOM streng slap igennem, og saa
/// blev der hverken sendt et sprog med eller lagt en dansk ledetraad foran.
///
/// Maalt 31-08-2026: «Omada, IBM, NetIQ» - alle tre i ordlisten - kom
/// tilbage som «Onera, IPM, NenaQ».
/// </remarks>
public class TalesprogTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Intet_valg_betyder_dansk(string? gemt)
    {
        var v = AppSettings.Current;
        v.MitSprog = gemt;

        Assert.Equal("da", v.Talesprog);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("  DA  ", "DA")]   // trimmes, men skrives ikke om
    [InlineData("auto", "auto")]
    public void Et_valg_bliver_staaende(string gemt, string vented)
    {
        var v = AppSettings.Current;
        v.MitSprog = gemt;

        Assert.Equal(vented, v.Talesprog);
    }

    [Fact]
    public void Dansk_faar_en_ledetraad_fordi_det_ikke_kan_vaelges()
    {
        // Voxtral afviser «da» som sprogvalg med 400. Ledetraaden er det,
        // der goer det alligevel - og den skal findes for det, Talesprog
        // giver, ellers hjaelper normaliseringen ingenting.
        Assert.False(NoteApp.Core.Llm.Voxtral.Kendes("da"));
        Assert.NotNull(NoteApp.Core.Llm.Voxtral.Ledetraad("da"));
    }
}
