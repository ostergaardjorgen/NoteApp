using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af ordbogen og af retteren.
///
/// DEN HER FINDES PÅ GRUND AF EN FIL, INGEN HAVDE SET.
///
/// Ordlisten lå i datamappen som ÉN lang sætning i prosa og blev læst med
/// ReadAllLines. Det gav én linje på 473 tegn i stedet for en ordliste — så
/// forhåndsviden til udskriften var i praksis ét vrøvlefelt, og det så ud til
/// at virke.
///
/// Værre: der stod et firmanavn i den, som ikke måtte forlade maskinen, og
/// listen sendes til leverandøren ved hver eneste diktering. Leverancetjekket
/// så det aldrig, fordi det kontrollerer repoet, og filen ligger uden for.
///
/// Derfor prøves formen her, og derfor skal ordbogen kunne ses i appen.
/// </summary>
public class OrdbibliotekTest
{
    // ============================ Rensningen ============================

    [Theory]
    [InlineData("  Entra ID  ", "Entra ID")]
    [InlineData("Entra  ID", "Entra ID")]
    [InlineData("Nordby,", "Nordby")]
    [InlineData("\"Kernesys\"", "Kernesys")]
    [InlineData("SCIM.", "SCIM")]
    public void Rens_fjerner_det_der_ikke_hoerer_til(string ind, string ventet)
    {
        Assert.Equal(ventet, Ordbibliotek.Rens(ind));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Tomme_ord_hoerer_ikke_hjemme(string? ind)
    {
        Assert.Equal("", Ordbibliotek.Rens(ind));
    }

    [Fact]
    public void En_hel_saetning_er_ikke_et_ord()
    {
        // Det var praecis det, der stod i filen: 473 tegn paa een linje.
        var prosa = new string('a', Ordbibliotek.MaksLaengde + 1);

        Assert.Equal("", Ordbibliotek.Rens(prosa));
    }

    [Fact]
    public void Dubletter_ryger_ud_uanset_store_bogstaver()
    {
        var ud = Ordbibliotek.Ryd(new[] { "Entra ID", "entra id", "ENTRA ID", "SCIM" });

        Assert.Equal(2, ud.Count);
        Assert.Equal("Entra ID", ud[0]);   // den foerste stavemaade vinder
        Assert.Equal("SCIM", ud[1]);
    }

    [Fact]
    public void Raekkefoelgen_bevares()
    {
        var ud = Ordbibliotek.Ryd(new[] { "Zitadel", "Anders", "Nordby" });

        Assert.Equal(new[] { "Zitadel", "Anders", "Nordby" }, ud);
    }

    [Fact]
    public void Der_sendes_hoejst_det_aftalte_antal()
    {
        // Forhaandsviden er ikke gratis, og en for lang liste traekker
        // udskriften mod ordene frem for mod det, der blev sagt.
        var mange = Enumerable.Range(1, Ordbibliotek.MaksSendte + 50).Select(i => "ord" + i);

        Assert.Equal(Ordbibliotek.MaksSendte, Ordbibliotek.TilAfsendelse(mange).Count);
    }

    // ============================ Retteren ============================

    private static readonly string[] Ordbog =
    {
        "Kernesys", "Nordby", "Zitadel", "provisionering", "attestering",
        "Entra ID", "SCIM", "IAM",
    };

    [Theory]
    [InlineData("Vi bruger Kernesus til det", "Vi bruger Kernesys til det")]
    [InlineData("Det var Norby, der ringede", "Det var Nordby, der ringede")]
    [InlineData("provitionering af brugere", "provisionering af brugere")]
    public void Et_ord_der_blev_hoert_naesten_rigtigt_rettes(string ind, string ventet)
    {
        var (ud, rettelser) = Ordretter.Ret(ind, Ordbog);

        Assert.Equal(ventet, ud);
        Assert.Single(rettelser);
    }

    [Fact]
    public void Store_bogstaver_rettes_paa_et_ord_der_ellers_var_rigtigt()
    {
        var (ud, _) = Ordretter.Ret("vi bruger zitadel", Ordbog);

        Assert.Equal("vi bruger Zitadel", ud);
    }

    [Fact]
    public void Korte_ord_roeres_ikke()
    {
        // «IAM» og «jam» er een fejl fra hinanden. Den forskel maa ikke
        // afgoeres af en taerskel.
        var (ud, rettelser) = Ordretter.Ret("der var jam til maden", Ordbog);

        Assert.Equal("der var jam til maden", ud);
        Assert.Empty(rettelser);
    }

    [Fact]
    public void To_lige_gode_kandidater_giver_ingen_rettelse()
    {
        // «Hanse» er lige langt fra begge. Vaelger vi den foerste, retter vi
        // til det forkerte ord halvdelen af gangene.
        var ordbog = new[] { "Hansen", "Hanser" };
        var (ud, rettelser) = Ordretter.Ret("det var Hanse", ordbog);

        Assert.Equal("det var Hanse", ud);
        Assert.Empty(rettelser);
    }

    [Fact]
    public void Et_helt_andet_ord_rettes_ikke()
    {
        var (ud, rettelser) = Ordretter.Ret("vi mødtes i kantinen på mandag", Ordbog);

        Assert.Equal("vi mødtes i kantinen på mandag", ud);
        Assert.Empty(rettelser);
    }

    [Fact]
    public void Tegnsaetning_og_mellemrum_bevares()
    {
        var (ud, _) = Ordretter.Ret("Kernesus, Norby — og så provitionering!", Ordbog);

        Assert.Equal("Kernesys, Nordby — og så provisionering!", ud);
    }

    [Fact]
    public void Flerordstermer_roeres_ikke()
    {
        // «Entra ID» kraever, at man ved, hvor ordet begynder og slutter. Det
        // er en anden opgave, og retteren maa ikke lade som om den kan den.
        var (ud, _) = Ordretter.Ret("vi bruger Entrada ID her", Ordbog);

        Assert.Equal("vi bruger Entrada ID her", ud);
    }

    [Fact]
    public void Tom_ordbog_aendrer_ingenting()
    {
        var (ud, rettelser) = Ordretter.Ret("Kernesus og Norby", Array.Empty<string>());

        Assert.Equal("Kernesus og Norby", ud);
        Assert.Empty(rettelser);
    }

    [Fact]
    public void Tom_tekst_vaelter_ingenting()
    {
        var (ud, rettelser) = Ordretter.Ret("", Ordbog);

        Assert.Equal("", ud);
        Assert.Empty(rettelser);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(14, 2)]
    public void Taersklen_foelger_ordets_laengde(int laengde, int ventet)
    {
        Assert.Equal(ventet, Ordretter.Taerskel(laengde));
    }

    [Fact]
    public void Afstanden_giver_op_over_loftet()
    {
        // Loftet er ikke pynt: retteren koerer over hvert ord i udskriften
        // gange hvert ord i ordbogen.
        Assert.Equal(0, Ordretter.Afstand("Kernesys", "kernesys", 2));
        Assert.Equal(1, Ordretter.Afstand("Kernesus", "Kernesys", 2));
        Assert.True(Ordretter.Afstand("kantine", "Kernesys", 2) > 2);
    }
}
