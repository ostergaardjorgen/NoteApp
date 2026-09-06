using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Kommer projektets materiale med, når man søger på tværs?
/// </summary>
/// <remarks>
/// DET ER HELE VÆRDIEN AF ETAPE 1. En sætning fra en PDF og en sætning fra et
/// webinar skal kunne stå i det samme svar — det er dét, en søgetjeneste
/// uden dine møder ikke kan.
/// </remarks>
public class Projektsoegningstest
{
    private static Projekt MedFil(string navn, string indhold, bool medISoegning = true)
    {
        var p = Projektlager.Opret("Uddannelsen");
        p.MedISoegning = medISoegning;
        Projektlager.Gem(p);

        File.WriteAllText(Path.Combine(p.Dokumentmappe, navn), indhold);
        return p;
    }

    [Fact]
    public void En_fil_i_fundamentet_kan_findes()
    {
        using var m = new Proevemappe();

        MedFil("pensum.md", "Kapitel 3 handler om adgangsstyring i praksis.");

        var fund = Soegning.Soeg("adgangsstyring");

        var f = Assert.Single(fund);

        Assert.Equal(Fundtype.Projektfil, f.Slags);
        Assert.Contains("pensum.md", f.Overskrift);
        Assert.Contains("Uddannelsen", f.Overskrift);
        Assert.NotEmpty(f.Traef);
    }

    [Fact]
    public void Uddraget_peger_paa_stedet_i_filen()
    {
        using var m = new Proevemappe();

        var p = MedFil("pensum.md", "Indledning. " + new string('x', 300)
                                    + " Adgangsstyring står her.");

        var f = Assert.Single(Soegning.Soeg("adgangsstyring"));
        var t = Assert.Single(f.Traef);

        Assert.True(t.Position > 300, $"positionen var {t.Position}");
        Assert.Contains("Adgangsstyring", t.Uddrag, StringComparison.OrdinalIgnoreCase);

        // Kilden er stien - en projektfil har ikke et id.
        Assert.Equal(Path.Combine(p.Dokumentmappe, "pensum.md"), f.Kilde);
    }

    [Fact]
    public void Et_projekt_der_er_slaaet_fra_kommer_ikke_med()
    {
        using var m = new Proevemappe();

        MedFil("pensum.md", "Adgangsstyring i praksis.", medISoegning: false);

        Assert.Empty(Soegning.Soeg("adgangsstyring"));
    }

    [Fact]
    public void Der_kan_soeges_i_eet_projekt_ad_gangen()
    {
        using var m = new Proevemappe();

        var et = MedFil("et.md", "Adgangsstyring hos den første.");
        MedFil("to.md", "Adgangsstyring hos den anden.");

        Assert.Equal(2, Soegning.Soeg("adgangsstyring").Count);

        var kun = Soegning.Soeg("adgangsstyring", new Soegefilter(Projekt: new[] { et.Id }));

        Assert.Contains("et.md", Assert.Single(kun).Overskrift);
    }

    [Fact]
    public void Et_moedetypefilter_spoerger_om_optagelser_og_ikke_om_filer()
    {
        using var m = new Proevemappe();

        MedFil("pensum.md", "Adgangsstyring i praksis.");

        // En projektfil har hverken moedetype eller sprog. Stod den paa
        // listen alligevel, ville den lade som om, den svarede paa
        // spoergsmaalet.
        Assert.Empty(Soegning.Soeg("adgangsstyring", new Soegefilter(Moedetype: new[] { "Mødereferat" })));
        Assert.Empty(Soegning.Soeg("adgangsstyring", new Soegefilter(Mappe: new[] { "Møder" })));

        Assert.NotEmpty(Soegning.Soeg("adgangsstyring", new Soegefilter()));
    }

    [Fact]
    public void En_fil_der_ikke_kan_laeses_staar_ikke_i_svaret()
    {
        using var m = new Proevemappe();

        var p = MedFil("pensum.md", "Adgangsstyring i praksis.");

        // En Google-stump indeholder et link og ikke et dokument. Den maa
        // hverken give et fund eller vaelte soegningen.
        File.WriteAllText(Path.Combine(p.Dokumentmappe, "link.gdoc"),
            "{\"url\":\"https://docs.google.com/document/d/adgangsstyring\"}");

        var f = Assert.Single(Soegning.Soeg("adgangsstyring"));

        Assert.Contains("pensum.md", f.Overskrift);
    }

    [Fact]
    public void Baade_en_optagelse_og_en_projektfil_kan_staa_i_samme_svar()
    {
        using var m = new Proevemappe();

        MedFil("pensum.md", "Adgangsstyring i praksis.");

        // En optagelse med det samme ord i udskriften.
        var mappe = MeetingStore.CreateSessionDirectory("Webinar om adgang", DateTimeOffset.Now);
        MeetingStore.Save(mappe, new MeetingMetadata
        {
            StartedAt = DateTimeOffset.Now,
            Title = "Webinar om adgang",
        });

        File.WriteAllText(Path.Combine(mappe, "transskription.txt"),
            "Vi taler om adgangsstyring hele vejen igennem.");

        var fund = Soegning.Soeg("adgangsstyring");

        Assert.Contains(fund, f => f.Slags == Fundtype.Projektfil);
        Assert.Contains(fund, f => f.Slags == Fundtype.Udskrift);
    }
}
