using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Materialet, en skabelon bygger på — og listen over, hvad der kom med.
/// </summary>
/// <remarks>
/// DE TO TING, DER AFGØR OM ET GENERERET DOKUMENT ER BRUGBART:
///
///   at det kan efterprøves — hver stump er mærket med sin fil, og
///   kildelisten står nederst;
///   at et hul siges højt — står der intet om et afsnit, skal det stå i
///   teksten, så modellen kan svare «det står ikke i dine kilder» i stedet
///   for at finde på noget, der lyder rigtigt.
/// </remarks>
public class Projektkontekstest
{
    private static Projekt Med(params (string Navn, string Indhold)[] filer)
    {
        var p = Projektlager.Opret("Uddannelsen");

        foreach (var (navn, indhold) in filer)
            File.WriteAllText(Path.Combine(p.Dokumentmappe, navn), indhold);

        return p;
    }

    [Fact]
    public void Begyndelsen_af_hver_fil_kommer_altid_med()
    {
        using var m = new Proevemappe();

        // Filen handler om noget, ingen af afsnittene spoerger om. Den skal
        // med alligevel - ellers er den usynlig for modellen, ogsaa naar den
        // er den vigtigste i mappen.
        var p = Med(("pensum.md", "Studieordningen beskriver forløbet over to år."));

        var svar = Projektkontekst.Byg(p, new[] { "Tidsplan" });

        Assert.Contains("pensum.md", svar.Tekst);
        Assert.Contains("Studieordningen beskriver", svar.Tekst);
        Assert.Contains(svar.Kilder, k => k.Navn == "pensum.md");
    }

    [Fact]
    public void Et_afsnit_uden_daekning_siges_hoejt()
    {
        using var m = new Proevemappe();

        var p = Med(("pensum.md", "Studieordningen beskriver forløbet."));

        var svar = Projektkontekst.Byg(p, new[] { "Budget" });

        Assert.Contains("Der står intet om «Budget»", svar.Tekst);
    }

    [Fact]
    public void Et_afsnit_med_daekning_faar_sine_passager()
    {
        using var m = new Proevemappe();

        var p = Med(("plan.md",
            "Indledning. " + new string('x', 2000)
            + " Tidsplanen strækker sig fra august til juni. " + new string('y', 500)));

        var svar = Projektkontekst.Byg(p, new[] { "Tidsplan" });

        Assert.Contains("OM AFSNITTET «Tidsplan»", svar.Tekst);
        Assert.Contains("Tidsplanen strækker sig fra august til juni", svar.Tekst);

        // Passagen skal vaere maerket med sin fil - ellers kan den ikke
        // efterproeves.
        Assert.Contains("Fra plan.md:", svar.Tekst);
    }

    [Fact]
    public void Et_uddrag_begynder_ikke_midt_i_et_ord()
    {
        using var m = new Proevemappe();

        var p = Med(("plan.md",
            string.Join(' ', Enumerable.Repeat("ord", 400)) + " Tidsplanen står her. "
            + string.Join(' ', Enumerable.Repeat("ord", 400))));

        var svar = Projektkontekst.Byg(p, new[] { "Tidsplan" });

        // Der klippes ved et mellemrum, og et klippet uddrag markeres.
        Assert.Contains("… ", svar.Tekst);
        Assert.DoesNotContain("… rd", svar.Tekst);
    }

    [Fact]
    public void Loftet_holdes_og_det_siges_naar_der_var_mere()
    {
        using var m = new Proevemappe();

        var p = Med(
            ("en.md", new string('a', 5000)),
            ("to.md", new string('b', 5000)),
            ("tre.md", new string('c', 5000)));

        var svar = Projektkontekst.Byg(p, Array.Empty<string>(), loft: 2000);

        Assert.True(svar.Afkortet, "der var mere materiale end der var plads til");
        Assert.True(svar.Tekst.Length < 5000, $"teksten fyldte {svar.Tekst.Length}");

        // OG DET SKAL STAA I KILDELISTEN. Et dokument bygget paa halvdelen
        // skal sige det.
        Assert.Contains("mere materiale", Projektkontekst.Kildeliste(svar));
    }

    [Fact]
    public void Kildelisten_naevner_hver_fil_med_sin_sti()
    {
        using var m = new Proevemappe();

        var p = Med(("pensum.md", "Studieordningen."), ("noter.md", "Noter fra timen."));

        var liste = Projektkontekst.Kildeliste(Projektkontekst.Byg(p, Array.Empty<string>()));

        Assert.Contains("pensum.md", liste);
        Assert.Contains("noter.md", liste);
        Assert.Contains(p.Dokumentmappe, liste);
    }

    [Fact]
    public void Et_tomt_fundament_siger_det_frem_for_at_lade_som_ingenting()
    {
        using var m = new Proevemappe();

        var svar = Projektkontekst.Byg(Projektlager.Opret("Tomt"), new[] { "Formål" });

        Assert.Empty(svar.Kilder);
        Assert.Contains("Der var intet materiale", Projektkontekst.Kildeliste(svar));
    }

    [Fact]
    public void En_fil_der_ikke_kan_laeses_kommer_ikke_med_som_kilde()
    {
        using var m = new Proevemappe();

        var p = Med(("pensum.md", "Studieordningen."),
                    ("link.gdoc", "{\"url\":\"https://docs.google.com/\"}"));

        var svar = Projektkontekst.Byg(p, Array.Empty<string>());

        Assert.DoesNotContain(svar.Kilder, k => k.Navn == "link.gdoc");
        Assert.Contains(svar.Kilder, k => k.Navn == "pensum.md");
    }
}
