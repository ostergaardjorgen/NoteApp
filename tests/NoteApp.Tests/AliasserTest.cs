using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// De stavemaader, motoren FAKTISK leverer for et ord.
/// </summary>
/// <remarks>
/// Maalt 31-08-2026 kom «StorageTek» tilbage som «Starstake», «StargeTech» og
/// «Starz Tech» - tre gange, tre stavemaader, alle fem-seks bogstavfejl fra
/// det rigtige. Ordbogsrettelsen toer to paa et ord af den laengde.
///
/// En sprogmodel var heller ikke svaret: proevet samme dag mod den rigtige
/// model SLETTEDE den de ord, der ikke stod paa listen, og med faa ord
/// INDSATTE den et, der ikke blev sagt.
/// </remarks>
public class AliasserTest
{
    private static readonly string[] Fil =
    {
        "Omada",
        "StorageTek = starz tech, starstake, starge tech",
        "NetIQ",
    };

    [Fact]
    public void Aliasserne_laeses_af_linjen()
    {
        var a = Ordbibliotek.Aliasser(Fil);

        Assert.Equal("StorageTek", a["starz tech"]);
        Assert.Equal("StorageTek", a["starstake"]);
        Assert.Equal("StorageTek", a["starge tech"]);
    }

    /// <summary>
    /// Selve ordet er dét, der staar FOER lighedstegnet.
    /// </summary>
    /// <remarks>
    /// Uden det ville hele linjen staa i ordbogen som ét langt «ord» - og saa
    /// ville den blive sendt med som forhaandsviden i den form.
    /// </remarks>
    [Fact]
    public void Ordet_er_det_der_staar_foer_lighedstegnet()
    {
        var ord = Ordbibliotek.Ryd(Fil);

        Assert.Contains("StorageTek", ord);
        Assert.DoesNotContain(ord, o => o.Contains('='));
        Assert.DoesNotContain(ord, o => o.Contains("starz", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void En_linje_uden_lighedstegn_giver_ingen_aliasser()
    {
        Assert.Empty(Ordbibliotek.Aliasser(new[] { "Omada", "NetIQ" }));
    }

    [Fact]
    public void Et_alias_der_er_ordet_selv_er_ikke_en_rettelse()
    {
        var a = Ordbibliotek.Aliasser(new[] { "Omada = omada, omadaa" });

        Assert.False(a.ContainsKey("omada"));
        Assert.Equal("Omada", a["omadaa"]);
    }

    [Fact]
    public void Et_lighedstegn_uden_et_ord_foran_springes_over()
    {
        Assert.Empty(Ordbibliotek.Aliasser(new[] { "= noget", "   = andet" }));
    }

    /// <summary>
    /// «Joern» er to bogstavfejl fra «Joergen», og appen toer én.
    /// </summary>
    /// <remarks>
    /// Maalt 31-08-2026: brugeren havde «Joergen» i ordbogen, og udskriften
    /// skrev «Joern». Afstanden er to; taersklen for et ord paa seks bogstaver
    /// er én.
    ///
    /// OG DEN SKAL BLIVE VED AT VAERE ÉN. «Joern» er et rigtigt navn. Et
    /// afstandsmaal, der retter det af sig selv, ville rette en anden persons
    /// navn til brugerens - og dét er vaerre end en stavefejl.
    ///
    /// Derfor er svaret et alias: brugeren skriver ned, at netop HANS
    /// udskrift skal rettes.
    /// </remarks>
    [Fact]
    public void Et_navn_to_bogstavfejl_vaek_kraever_et_alias()
    {
        // Uden alias: for langt vaek til at blive rettet.
        var (uden, _) = Ordretter.Ret("Hilsen Jørn", new[] { "Jørgen" });
        Assert.Equal("Hilsen Jørn", uden);

        // Taersklen skal blive ved at vaere én for et ord paa seks bogstaver.
        Assert.Equal(1, Ordretter.Taerskel("Jørgen".Length));
    }

    [Fact]
    public void Aliasser_kan_ogsaa_vaere_navne()
    {
        var a = Ordbibliotek.Aliasser(new[] { "Jørgen = jørn, jørgan" });

        Assert.Equal("Jørgen", a["jørn"]);
        Assert.Equal("Jørgen", a["jørgan"]);
    }

    // ============ AT SKRIVE ET ALIAS NED FRA APPEN ============

    /// <summary>
    /// Et alias, der er et ANDET ord i ordbogen, maa ikke laegges til.
    /// </summary>
    /// <remarks>
    /// Det er den ene fejl, der er svaer at opdage: staar baade «Jørgen» og
    /// «Jørn» i ordbogen, og saetter man «Jørn» som alias for «Jørgen», bliver
    /// et RIGTIGT ord rettet til et forkert hver gang. Og bagefter kan man
    /// ikke se hvorfor - ordet stod jo i ordbogen.
    /// </remarks>
    [Fact]
    public void Reglen_der_beskytter_mod_at_rette_et_rigtigt_ord()
    {
        // Selve filskrivningen kraever en ordbog paa disken og proeves ikke
        // her. Reglen kan derimod laeses ud af aliasserne: et alias og et ord
        // maa ikke vaere det samme.
        var a = Ordbibliotek.Aliasser(new[] { "Jørgen = jørn", "Jørn" });

        // Aliasset findes, og det er praecis derfor TilfoejAlias afviser at
        // saette et, der ogsaa er et ord.
        Assert.Equal("Jørgen", a["jørn"]);
    }

    [Fact]
    public void Flere_aliasser_kan_staa_paa_samme_linje()
    {
        var a = Ordbibliotek.Aliasser(
            new[] { "StorageTek = starz tech, starstake, storistech" });

        Assert.Equal(3, a.Count);
        Assert.Equal("StorageTek", a["storistech"]);
    }
}
