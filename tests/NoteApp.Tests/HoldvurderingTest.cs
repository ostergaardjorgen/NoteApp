using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af tryk mod hold.
///
/// SAMME TAST SKAL KUNNE TO TING. Et kort tryk starter en mødeoptagelse — det
/// har den altid gjort. Holdes den nede, er det en diktering.
///
/// Det farlige er ikke selve grænsen, men tilstanden omkring den: et hold, der
/// aldrig slutter, et tryk, der bliver væk, eller et slip, der giver besked to
/// gange. Alle tre ville vise sig som «genvejen virker en gang imellem» — og
/// det er præcis dén fejl, appen har været igennem én gang før.
/// </summary>
public class HoldvurderingTest
{
    private static readonly TimeSpan Lige = Holdvurdering.Graense;
    private static readonly TimeSpan Kort = Holdvurdering.Graense - TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan Lang = Holdvurdering.Graense + TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan Loft = Holdvurdering.LoftFra(Holdvurdering.StandardLoftMinutter);

    [Fact]
    public void Sluppet_med_det_samme_er_et_tryk()
    {
        Assert.Equal(Holdsvar.Tryk, Holdvurdering.Naeste(TimeSpan.Zero, nede: false, holderAllerede: false, Loft));
    }

    [Fact]
    public void Sluppet_lige_inden_graensen_er_stadig_et_tryk()
    {
        Assert.Equal(Holdsvar.Tryk, Holdvurdering.Naeste(Kort, nede: false, holderAllerede: false, Loft));
    }

    [Fact]
    public void Nede_men_ikke_laenge_nok_giver_ingenting()
    {
        // Her maa der IKKE ske noget. Sker der noget, starter en optagelse ved
        // et tryk, der endnu ikke er afgjort.
        Assert.Equal(Holdsvar.Vent, Holdvurdering.Naeste(Kort, nede: true, holderAllerede: false, Loft));
    }

    [Fact]
    public void Graensen_taeller_med()
    {
        // Praecis paa graensen er det et hold. En graense, der kun gaelder
        // OVER, giver et hul paa et millisekund, hvor intet sker.
        Assert.Equal(Holdsvar.Begynd, Holdvurdering.Naeste(Lige, nede: true, holderAllerede: false, Loft));
    }

    [Fact]
    public void Over_graensen_begynder_holdet()
    {
        Assert.Equal(Holdsvar.Begynd, Holdvurdering.Naeste(Lang, nede: true, holderAllerede: false, Loft));
    }

    [Fact]
    public void Holdet_begynder_kun_een_gang()
    {
        // Uret spoerger hvert 25. ms. Uden den her ville dikteringen begynde
        // fyrre gange i sekundet, saa laenge tasten var nede.
        Assert.Equal(Holdsvar.Vent, Holdvurdering.Naeste(Lang, nede: true, holderAllerede: true, Loft));
        Assert.Equal(Holdsvar.Vent,
            Holdvurdering.Naeste(TimeSpan.FromSeconds(30), nede: true, holderAllerede: true, Loft));
    }

    [Fact]
    public void Sluppet_efter_et_hold_afslutter_det()
    {
        Assert.Equal(Holdsvar.Slut, Holdvurdering.Naeste(Lang, nede: false, holderAllerede: true, Loft));
    }

    [Fact]
    public void Et_hold_der_er_afsluttet_giver_ikke_et_tryk_ogsaa()
    {
        // Slippet maa give EEN besked. Gav det baade Slut og Tryk, ville en
        // diktering slutte med at starte en moedeoptagelse.
        var svar = Holdvurdering.Naeste(Lang, nede: false, holderAllerede: true, Loft);

        Assert.NotEqual(Holdsvar.Tryk, svar);
        Assert.Equal(Holdsvar.Slut, svar);
    }

    [Fact]
    public void En_fastsiddende_tast_slipper_af_sig_selv()
    {
        // Uden loftet ville appen optage og sende, til nogen opdagede det.
        var forbi = Loft + TimeSpan.FromSeconds(1);

        Assert.Equal(Holdsvar.Loftet, Holdvurdering.Naeste(forbi, nede: true, holderAllerede: true, Loft));
    }

    [Fact]
    public void Afbrudt_er_ikke_det_samme_som_sluppet()
    {
        // HELE POINTEN MED DE TO SVAR. Slipper man selv, ved man, at
        // dikteringen er forbi. Bliver man afbrudt, staar man og taler videre
        // til et program, der er holdt op med at lytte - og opdager det
        // foerst, naar teksten mangler. Der SKAL siges til.
        var forbi = Loft + TimeSpan.FromSeconds(1);

        var afbrudt = Holdvurdering.Naeste(forbi, nede: true, holderAllerede: true, Loft);
        var sluppet = Holdvurdering.Naeste(forbi, nede: false, holderAllerede: true, Loft);

        Assert.NotEqual(afbrudt, sluppet);
        Assert.Equal(Holdsvar.Loftet, afbrudt);
        Assert.Equal(Holdsvar.Slut, sluppet);
    }

    [Fact]
    public void Loftet_naaet_uden_at_holdet_naaede_at_begynde_er_et_tryk()
    {
        // Kan ikke ske i praksis — graensen er 350 ms og loftet mindst et
        // minut — men et svar, der falder mellem to stole, maa aldrig vaere
        // «ingenting».
        var forbi = Loft + TimeSpan.FromSeconds(1);

        Assert.Equal(Holdsvar.Tryk, Holdvurdering.Naeste(forbi, nede: true, holderAllerede: false, Loft));
    }

    [Fact]
    public void Graensen_er_kortere_end_det_korteste_loft()
    {
        // Byttede de to plads, ville hvert tryk blive til en diktering.
        Assert.True(Holdvurdering.Graense
                    < Holdvurdering.LoftFra(Holdvurdering.MindsteLoftMinutter));
        Assert.True(Holdvurdering.Graense > TimeSpan.Zero);
    }

    [Theory]
    [InlineData(0, Holdvurdering.StandardLoftMinutter)]
    [InlineData(-5, Holdvurdering.StandardLoftMinutter)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(999, Holdvurdering.StoersteLoftMinutter)]
    public void Et_umuligt_loft_bliver_til_et_muligt(int valgt, int ventet)
    {
        // En indstillingsfil kan indeholde hvad som helst. Et nul ville
        // afbryde hver diktering med det samme; et negativt tal foer den
        // begyndte.
        Assert.Equal(TimeSpan.FromMinutes(ventet), Holdvurdering.LoftFra(valgt));
    }
}
