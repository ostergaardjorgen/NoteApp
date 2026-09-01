using System.Linq;
using NoteApp.Core;
using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Dikteringen skrives ud paa maskinen, naar skyen ikke kan vaelge sproget.
/// </summary>
/// <remarks>
/// Voxtral afviser «da» som sprogvalg - efterproevet mod det rigtige
/// endepunkt 30-08 og igen 31-08-2026, ogsaa mod den nyeste model. Sproget
/// kunne derfor kun PAAVIRKES gennem en ledetraad, aldrig vaelges, og den
/// samme danske diktering kom tilbage som fransk, tysk og hollandsk paa tre
/// forskellige dage.
/// </remarks>
public class LokaludskriftTest
{
    /// <summary>
    /// Det er dansk, hele den lokale vej findes for.
    /// </summary>
    [Fact]
    public void Dansk_kan_ikke_vaelges_i_skyen()
    {
        Assert.False(Voxtral.Kendes("da"));
    }

    /// <summary>
    /// Og de sprog, der KAN vaelges, skal blive i skyen.
    /// </summary>
    /// <remarks>
    /// Den lokale vej er svaret paa et sprogproblem, ikke en generel
    /// udskiftning af motoren. Kan sproget vaelges, er skyen bedre til at
    /// rydde op og til fagord.
    /// </remarks>
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("nl")]
    public void De_sprog_skyen_kender_bliver_i_skyen(string sprog)
    {
        Assert.True(Voxtral.Kendes(sprog));
    }

    [Fact]
    public void Tomme_maerker_kommer_ikke_med_i_teksten()
    {
        // Whisper skriver «[BLANK_AUDIO]» og «(musik)» paa tavse klip. Det er
        // ikke noget, nogen har sagt, og det maa ikke ende i en note.
        var raa = "[BLANK_AUDIO]\n Det her blev sagt.\n(musik)\n\n*klik*\n";

        Assert.Equal("Det her blev sagt.", Lokaludskrift.Rens(raa));
    }

    [Fact]
    public void Flere_linjer_bliver_til_én_tekst()
    {
        Assert.Equal("Foerste linje. Anden linje.",
            Lokaludskrift.Rens(" Foerste linje.\n Anden linje.\n"));
    }

    [Fact]
    public void Ingenting_ind_er_ingenting_ud()
    {
        Assert.Equal("", Lokaludskrift.Rens(null));
        Assert.Equal("", Lokaludskrift.Rens("   \n\n  "));
        Assert.Equal("", Lokaludskrift.Rens("[BLANK_AUDIO]"));
    }

    // ============ ORDBOGEN ============

    /// <summary>
    /// Ordbogen skal med, ogsaa naar teksten skrives ud paa maskinen.
    /// </summary>
    /// <remarks>
    /// DEN BLEV TABT, DA DIKTERINGEN FLYTTEDE HJEM. Skyen fik ordlisten som
    /// ledetraad; den lokale vej fik kun lyden og sproget.
    ///
    /// Maalt 31-08-2026: «Omada IBM StorageTek NetIQ» kom tilbage som «IBM,
    /// StargeTech, NetIQ» - det foerste ord helt vaek og StorageTek forkert,
    /// selv om begge stod i ordbogen.
    /// </remarks>
    [Fact]
    public void Fagordene_bliver_til_en_ledetraad()
    {
        var t = Lokaludskrift.Ledetraad(new[] { "Omada", "StorageTek", "NetIQ" });

        Assert.Equal("Omada, StorageTek, NetIQ.", t);
    }

    /// <summary>
    /// Anfoerselstegn skal vaek.
    /// </summary>
    /// <remarks>
    /// Ledetraaden saettes ind i en kommandolinje i anfoerselstegn. Staar der
    /// et i selve teksten, braekker resten af linjen af, og motoren faar noget
    /// helt andet at vide, end der stod.
    /// </remarks>
    [Fact]
    public void Anfoerselstegn_i_et_fagord_braekker_ikke_kommandolinjen()
    {
        var t = Lokaludskrift.Ledetraad(new[] { "Om\"ada", "NetIQ" });

        Assert.DoesNotContain("\"", t);
        Assert.Contains("Omada", t);
    }

    [Fact]
    public void En_ordbog_der_vokser_loeber_ikke_over()
    {
        var mange = Enumerable.Range(1, 500).Select(n => "ord" + n);

        var t = Lokaludskrift.Ledetraad(mange);

        Assert.Equal(80, t.TrimEnd('.').Split(", ").Length);
    }

    [Fact]
    public void Ingen_ordbog_giver_ingen_ledetraad()
    {
        Assert.Equal("", Lokaludskrift.Ledetraad(null));
        Assert.Equal("", Lokaludskrift.Ledetraad(new string[0]));
        Assert.Equal("", Lokaludskrift.Ledetraad(new[] { "  ", "" }));
    }
}
