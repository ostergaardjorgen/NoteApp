using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af, hvad en sagt sætning skal bruges til.
///
/// «Hej Pia, opret en opgave: ring til Anders på fredag» er ÉN handling. Man
/// siger, hvad man vil, ligesom man ville sige det til et menneske — i stedet
/// for at få en rude med fire knapper, som man skal røre. Vågeordet findes
/// netop for ikke at skulle røre noget.
///
/// FEJLER DEN, FEJLER DEN DEN RIGTIGE VEJ: genkendes ingen indledning, er det
/// en almindelig diktering, og teksten lander, hvor markøren står. Man mister
/// ikke det, man sagde.
/// </summary>
public class HensigtTest
{
    private static readonly string[] Vaageord = { "hej pia", "hey pia" };

    // ==================== vågeordet skal skæres af ====================

    [Fact]
    public void Vaageordet_skaeres_af_forrest()
    {
        // SET 30-08-2026: «Hej Pia» landede i soegefeltet som tekst.
        // Vaageordet hoeres af én motor, og optagelsen begynder foerst, naar
        // den har meldt det - halen af ordet er stadig i luften.
        Assert.Equal("opret en note", Hensigtstolk.UdenVaageord("Hej Pia, opret en note", Vaageord));
        Assert.Equal("opret en note", Hensigtstolk.UdenVaageord("hey pia opret en note", Vaageord));
    }

    [Fact]
    public void En_saetning_uden_vaageord_roeres_ikke()
    {
        Assert.Equal("ring til Anders", Hensigtstolk.UdenVaageord("ring til Anders", Vaageord));
    }

    [Fact]
    public void Vaageord_midt_i_saetningen_skaeres_ikke()
    {
        // «Jeg sagde hej pia til hende» er ikke et kald.
        var t = "Jeg sagde hej pia til hende";
        Assert.Equal(t, Hensigtstolk.UdenVaageord(t, Vaageord));
    }

    // ==================== indledningerne ====================

    [Theory]
    [InlineData("opret en opgave: ring til Anders", Hensigt.Opgave, "Ring til Anders")]
    [InlineData("ny opgave ring til Anders", Hensigt.Opgave, "Ring til Anders")]
    [InlineData("husk at ringe til Anders", Hensigt.Opgave, "Ringe til Anders")]
    [InlineData("mind mig om at købe mælk", Hensigt.Opgave, "Købe mælk")]
    [InlineData("opret en note: mødet gik godt", Hensigt.Note, "Mødet gik godt")]
    [InlineData("ny note mødet gik godt", Hensigt.Note, "Mødet gik godt")]
    [InlineData("søg efter Nordby", Hensigt.Soegning, "Nordby")]
    [InlineData("søg på Nordby", Hensigt.Soegning, "Nordby")]
    [InlineData("find Nordby", Hensigt.Soegning, "Nordby")]
    [InlineData("opret en aftale med Anders på fredag", Hensigt.Aftale, "Med Anders på fredag")]
    [InlineData("book et møde med Anders", Hensigt.Aftale, "Med Anders")]
    public void Indledningen_afgoer_hvad_det_skal_bruges_til(
        string sagt, Hensigt ventet, string tekst)
    {
        var fund = Hensigtstolk.Tolk(sagt);

        Assert.Equal(ventet, fund.Hvad);
        Assert.Equal(tekst, fund.Tekst);
    }

    [Fact]
    public void Den_laengste_indledning_vinder()
    {
        // «opret en opgave» skal slaa «opgave». Ellers ville teksten blive
        // «en opgave: ring til Anders».
        var fund = Hensigtstolk.Tolk("opret en opgave: ring til Anders");

        Assert.Equal(Hensigt.Opgave, fund.Hvad);
        Assert.Equal("Ring til Anders", fund.Tekst);
    }

    [Fact]
    public void Et_ord_der_BEGYNDER_som_en_indledning_taeller_ikke()
    {
        // «opgaver» er ikke «opgave». Uden den kontrol ville «Opgaverne er
        // fordelt» blive til en opgave med teksten «rne er fordelt».
        var fund = Hensigtstolk.Tolk("opgaverne er fordelt");

        Assert.Equal(Hensigt.Diktat, fund.Hvad);
        Assert.Equal("opgaverne er fordelt", fund.Tekst);
    }

    [Fact]
    public void Uden_indledning_er_det_en_almindelig_diktering()
    {
        // DEN RIGTIGE VEJ AT FEJLE. Man faar sin tekst; den lander bare, hvor
        // markoeren staar.
        var fund = Hensigtstolk.Tolk("jeg tror vi skal vente til på mandag");

        Assert.Equal(Hensigt.Diktat, fund.Hvad);
        Assert.Equal("jeg tror vi skal vente til på mandag", fund.Tekst);
    }

    [Fact]
    public void Tom_tale_giver_ingenting()
    {
        Assert.Equal(Hensigt.Diktat, Hensigtstolk.Tolk(null).Hvad);
        Assert.Equal("", Hensigtstolk.Tolk("   ").Tekst);
    }

    [Fact]
    public void Store_og_smaa_bogstaver_er_lige_meget()
    {
        Assert.Equal(Hensigt.Note, Hensigtstolk.Tolk("Opret En Note: noget").Hvad);
    }

    // ==================== hele vejen ====================

    [Fact]
    public void Hele_vejen_fra_det_sagte()
    {
        var sagt = "Hej Pia, opret en opgave: ring til Anders på fredag";

        var fund = Hensigtstolk.Tolk(Hensigtstolk.UdenVaageord(sagt, Vaageord));

        Assert.Equal(Hensigt.Opgave, fund.Hvad);
        Assert.Equal("Ring til Anders på fredag", fund.Tekst);
    }

    [Fact]
    public void Kun_vaageordet_giver_en_tom_diktering()
    {
        // Praecis det, der skete: brugeren sagde bare «Hej Pia», og ordet
        // selv endte i soegefeltet.
        var fund = Hensigtstolk.Tolk(Hensigtstolk.UdenVaageord("Hej Pia", Vaageord));

        Assert.Equal(Hensigt.Diktat, fund.Hvad);
        Assert.Equal("", fund.Tekst);
    }
}
