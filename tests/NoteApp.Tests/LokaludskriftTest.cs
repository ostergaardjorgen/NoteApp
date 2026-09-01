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
    /// <summary>
    /// Ordlisten hoerer IKKE til i whispers ledetraad.
    /// </summary>
    /// <remarks>
    /// OBSERVERET TO GANGE 31-08-2026, begge med «-l da» sat:
    ///
    ///   «Hae, Pia. Fett, er du klar? Eg kunne godt senga.»
    ///   «Hae, Pia. Se er ut til, at den er ved at vaere gott nu ...»
    ///
    /// Islandsk-faeroesk i formen, selv om sproget var VALGT.
    ///
    /// Whispers «--prompt» er ikke en instruktion; den saettes ind som
    /// TIDLIGERE TEKST, og modellen skriver videre i den stil, den finder.
    /// Fyrre fagord er ikke dansk prosa.
    ///
    /// At pakke listen ind i en dansk saetning raakkede ikke - det andet
    /// udfald kom EFTER den rettelse. Listen gav ét ord mere rigtigt og
    /// kostede hele saetningens sprog. Et forkert egennavn kan rettes med et
    /// alias; en saetning paa islandsk kan ingenting redde.
    /// </remarks>
    [Fact]
    public void Ledetraaden_er_kun_en_dansk_saetning()
    {
        var t = Lokaludskrift.Ledetraad(new[] { "Omada", "SCIM", "SSO", "ISO 27001" }, "da");

        Assert.Contains("diktering på dansk", t);
        Assert.DoesNotContain("Omada", t);
        Assert.DoesNotContain("SCIM", t);
    }

    [Theory]
    [InlineData("en", "dictation in English")]
    [InlineData("no", "diktat på norsk")]
    [InlineData(null, "diktering på dansk")]
    public void Ledetraaden_foelger_sproget(string? sprog, string vented)
    {
        Assert.Contains(vented, Lokaludskrift.Ledetraad(null, sprog));
    }

    /// <summary>
    /// Og den maa ikke vaere tom - saa er der intet, der traekker mod dansk.
    /// </summary>
    [Fact]
    public void Der_er_altid_en_ledetraad()
    {
        Assert.NotEmpty(Lokaludskrift.Ledetraad(null, "da"));
        Assert.NotEmpty(Lokaludskrift.Ledetraad(new string[0], "da"));
    }
}
