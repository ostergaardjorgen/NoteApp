using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// En søgning skrevet som en sætning: hvad bliver søgt på, og i hvilken periode?
/// </summary>
/// <remarks>
/// Alle prøver regner fra torsdag 10-09-2026, så en periode har ét rigtigt svar.
/// </remarks>
public class Fritekststest
{
    private static readonly DateTime Nu = new(2026, 9, 10, 14, 30, 0);

    private static Fortolkning F(string s) => Fritekst.Fortolk(s, Nu);

    private static void Periode(Fortolkning f, DateTime fra, DateTime til)
    {
        Assert.Equal(fra, f.Fra!.Value.DateTime);
        Assert.Equal(til.Date.AddDays(1).AddSeconds(-1), f.Til!.Value.DateTime);
    }

    [Fact]
    public void Eksemplet_bliver_til_Omada_i_august()
    {
        var f = F("Talte med en om Omada i sidste måned, kan du finde mødet");

        Assert.Equal("Omada", f.Soegeord);
        Assert.Equal("august 2026", f.Periode);
        Periode(f, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
        Assert.True(f.Aendret);
    }

    [Fact]
    public void Almindelige_soegeord_roeres_ikke()
    {
        var f = F("omada pipeline");

        Assert.Equal("omada pipeline", f.Soegeord);
        Assert.Null(f.Periode);
        Assert.False(f.Aendret);
    }

    [Fact]
    public void Anfoerselstegn_betyder_ordret()
    {
        var f = F("\"access review\" i sidste måned");

        Assert.Equal("\"access review\" i sidste måned", f.Soegeord);
        Assert.Null(f.Fra);
    }

    [Theory]
    [InlineData("møde")]
    [InlineData("opkald")]
    public void Kun_fyldord_soeger_paa_det_skrevne(string s) =>
        Assert.Equal(s, F(s).Soegeord);

    [Fact]
    public void Kun_en_periode_og_fyldord_giver_ikke_en_tom_soegning()
    {
        // «møderne i går» - der er ikke noget emne. Saa soeges der paa det,
        // der staar, i stedet for paa ingenting.
        var f = F("mødet i går");

        Assert.Equal("mødet", f.Soegeord);
        Assert.Equal("i går", f.Periode);
    }

    [Fact]
    public void I_gaar_er_hele_dagen()
    {
        var f = F("hvad sagde Espen om prisen i går");

        Assert.Equal("Espen prisen", f.Soegeord);
        Periode(f, new DateTime(2026, 9, 9), new DateTime(2026, 9, 9));
    }

    [Fact]
    public void Sidste_uge_er_mandag_til_soendag()
    {
        var f = F("IBM sidste uge");

        Assert.Equal("IBM", f.Soegeord);
        Periode(f, new DateTime(2026, 8, 31), new DateTime(2026, 9, 6));
    }

    [Fact]
    public void Denne_maaned_har_ingen_slutning()
    {
        var f = F("kernesys denne måned");

        Assert.Equal(new DateTime(2026, 9, 1), f.Fra!.Value.DateTime);
        Assert.Null(f.Til);
    }

    [Theory]
    [InlineData("i maj", 2026, 5)]
    [InlineData("i november", 2025, 11)]      // sagt i september: sidste aars november
    [InlineData("i september", 2026, 9)]
    [InlineData("i marts 2025", 2025, 3)]
    public void En_maaned_er_den_seneste_af_den(string s, int aar, int maaned)
    {
        var f = F("Omada " + s);

        Assert.Equal("Omada", f.Soegeord);
        var foerste = new DateTime(aar, maaned, 1);
        Periode(f, foerste, foerste.AddMonths(1).AddDays(-1));
    }

    [Fact]
    public void For_to_uger_siden_er_hele_den_uge()
    {
        var f = F("budget for to uger siden");

        Assert.Equal("budget", f.Soegeord);
        Periode(f, new DateTime(2026, 8, 24), new DateTime(2026, 8, 30));
    }

    [Fact]
    public void I_mandags_er_den_seneste_mandag()
    {
        var f = F("tilbud i mandags");

        Assert.Equal("tilbud", f.Soegeord);
        Periode(f, new DateTime(2026, 9, 7), new DateTime(2026, 9, 7));
    }

    [Fact]
    public void I_torsdags_sagt_om_torsdagen_er_en_uge_siden()
    {
        // I dag ER torsdag. «I torsdags» er ikke i dag.
        Periode(F("tilbud i torsdags"), new DateTime(2026, 9, 3), new DateTime(2026, 9, 3));
    }

    [Fact]
    public void Sidste_aar_er_hele_aaret()
    {
        var f = F("Omada sidste år");

        Assert.Equal("2025", f.Periode);
        Periode(f, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
    }

    [Fact]
    public void Engelsk_forstaas_ogsaa()
    {
        var f = F("find the meeting about Omada last month");

        Assert.Equal("Omada", f.Soegeord);
        Periode(f, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));
    }

    [Fact]
    public void Store_bogstaver_bevares()
    {
        Assert.Equal("Omada IBM", F("talte med Omada om IBM").Soegeord);
    }

    // ============ DE SAMME AFGRAENSNINGER SOM FILTRENE ============

    private static readonly Kendtenavne Arkivet = new(
        Mapper: new[] { "Kunder", "Omada", "Webinarer" },
        Projekter: new[] { ("p1", "Vagtsom IAM"), ("p2", "Vagtsom") });

    private static Fortolkning K(string s) => Fritekst.Fortolk(s, Nu, Arkivet);

    [Fact]
    public void Webinarer_genkendes_som_type()
    {
        var f = K("webinarer om AI i sidste måned");

        Assert.Equal("AI", f.Soegeord);
        Assert.Equal(Soegetype.Webinar, f.Type);
        Assert.Equal("august 2026", f.Periode);
    }

    [Theory]
    [InlineData("opkaldet med Espen i går")]
    [InlineData("hvad sagde Espen i telefonen i går")]
    public void Et_opkald_genkendes_som_type(string s)
    {
        var f = K(s);

        Assert.Equal("Espen", f.Soegeord);
        Assert.Equal(Soegetype.Opkald, f.Type);
        Assert.Equal("i går", f.Periode);
    }

    [Fact]
    public void Mine_noter_er_noter()
    {
        var f = K("mine noter om budget");

        Assert.Equal("budget", f.Soegeord);
        Assert.Equal(Soegetype.Note, f.Type);
    }

    [Fact]
    public void Moedet_er_ikke_en_type()
    {
        // «Kan du finde mødet» siges ogsaa om et opkald. Blev det til en
        // afgraensning paa moeder, forsvandt opkaldet fra soegningen.
        var f = K("Talte med en om Omada i sidste måned, kan du finde mødet");

        Assert.Equal("Omada", f.Soegeord);
        Assert.Null(f.Type);
    }

    [Fact]
    public void En_mappe_kraever_stikordet()
    {
        var f = K("Omada i mappen Kunder");

        Assert.Equal("Omada", f.Soegeord);
        Assert.Equal("Kunder", f.Mappe);
    }

    [Fact]
    public void Et_ord_der_ogsaa_er_en_mappe_bliver_ved_med_at_blive_soegt_paa()
    {
        // Mappen hedder tit det samme som kunden. «Omada» uden stikord er et
        // soegeord - ellers forsvinder de moeder, hvor Omada blot blev naevnt.
        var f = K("talte med en om Omada");

        Assert.Equal("Omada", f.Soegeord);
        Assert.Null(f.Mappe);
    }

    [Fact]
    public void Det_laengste_projektnavn_vinder()
    {
        var f = K("adgangsstyring i projektet Vagtsom IAM");

        Assert.Equal("adgangsstyring", f.Soegeord);
        Assert.Equal("p1", f.Projekt);
        Assert.Equal("Vagtsom IAM", f.Projektnavn);
    }

    [Fact]
    public void Sproget_genkendes()
    {
        var f = K("engelske webinarer om Omada");

        Assert.Equal("Omada", f.Soegeord);
        Assert.Equal("en", f.Sprog);
        Assert.Equal(Soegetype.Webinar, f.Type);
    }

    [Fact]
    public void Intet_emne_tilbage_soeger_paa_ordene_med_perioden_men_uden_afgraensning()
    {
        var f = K("webinarer i sidste måned");

        Assert.Equal("webinarer", f.Soegeord);
        Assert.Null(f.Type);
        Assert.Equal("august 2026", f.Periode);
    }

    [Fact]
    public void Linjen_siger_alt_det_der_blev_forstaaet()
    {
        var f = K("engelske webinarer om Omada i mappen Kunder i sidste måned");

        Assert.Equal("Søger efter «Omada»  ·  kun webinarer  ·  august 2026  ·  mappen Kunder  ·  på engelsk",
                     f.Beskriv());
    }
}
