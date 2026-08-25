using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Prøver af mapper i mapper.
///
/// Den dyre fejl her er ikke, at en flytning ikke virker — det ser man med det
/// samme. Den dyre er, at mappen flytter sig, men indholdet ikke følger med:
/// så står tyve optagelser og peger på et navn, der ikke findes længere, og
/// det ser ud, som om de er væk.
///
/// Derfor returnerer Flyt og Omdøb en LISTE over, hvad der skiftede navn.
/// Prøverne herunder holder øje med, at listen er fuldstændig.
/// </summary>
public sealed class MapperTest
{
    private const Mapper.Slags Slags = Mapper.Slags.Optagelser;

    private static void Ryd()
    {
        foreach (var m in Mapper.Alle(Slags).ToList())
            Mapper.SletMedIndhold(Slags, m);
    }

    // ------------------------------------------------------------- stinavne

    [Fact]
    public void Bladnavn_og_foraelder_deler_stien()
    {
        Assert.Equal("Steen", Mapper.Bladnavn("Møder/Steen"));
        Assert.Equal("Møder", Mapper.Foraelder("Møder/Steen"));

        Assert.Equal("Møder", Mapper.Bladnavn("Møder"));
        Assert.Equal("", Mapper.Foraelder("Møder"));

        Assert.Equal("2026", Mapper.Bladnavn("Møder/Steen/2026"));
        Assert.Equal("Møder/Steen", Mapper.Foraelder("Møder/Steen/2026"));
    }

    [Fact]
    public void En_skraastreg_i_et_navn_er_forbudt()
    {
        // Ellers kan man ikke skelne «A/B» fra en mappe, der hedder «A/B».
        Assert.False(Mapper.ErGyldigtLed("Møder/Steen"));
        Assert.False(Mapper.ErGyldigtLed(""));
        Assert.False(Mapper.ErGyldigtLed("   "));
        Assert.False(Mapper.ErGyldigtLed(Mapper.Ingen));
        Assert.True(Mapper.ErGyldigtLed("Steen"));
    }

    [Fact]
    public void LiggerUnder_rammer_ikke_et_navn_der_bare_begynder_ens()
    {
        Assert.True(Mapper.LiggerUnder("Møder/Steen", "Møder"));
        Assert.True(Mapper.LiggerUnder("Møder", "Møder"));

        // «Møderække» ligger IKKE under «Møder». Uden skråstregen ville en
        // flytning af «Møder» tage den med.
        Assert.False(Mapper.LiggerUnder("Møderække", "Møder"));
    }

    // -------------------------------------------------------------- oprettelse

    [Fact]
    public void Opret_under_laver_foraeldrene_med()
    {
        using var p = new Proevemappe();
        Ryd();

        Assert.True(Mapper.OpretUnder(Slags, "Møder/Steen", "2026"));

        var alle = Mapper.Alle(Slags);
        Assert.Contains("Møder", alle);
        Assert.Contains("Møder/Steen", alle);
        Assert.Contains("Møder/Steen/2026", alle);
    }

    [Fact]
    public void Samme_sti_to_gange_afvises()
    {
        using var p = new Proevemappe();
        Ryd();

        Assert.True(Mapper.OpretUnder(Slags, "Møder", "Steen"));
        Assert.False(Mapper.OpretUnder(Slags, "Møder", "Steen"));
    }

    [Fact]
    public void Samme_navn_i_to_forskellige_mapper_er_i_orden()
    {
        using var p = new Proevemappe();
        Ryd();

        Assert.True(Mapper.OpretUnder(Slags, "Møder", "Steen"));
        Assert.True(Mapper.OpretUnder(Slags, "Webinarer", "Steen"));
    }

    // ---------------------------------------------------------------- flytning

    [Fact]
    public void Flyt_en_mappe_ind_under_en_anden()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.Opret(Slags, "Møder");
        Mapper.Opret(Slags, "Steen");

        var skift = Mapper.Flyt(Slags, "Steen", "Møder");

        Assert.Single(skift);
        Assert.Equal("Steen", skift[0].Fra);
        Assert.Equal("Møder/Steen", skift[0].Til);

        Assert.Contains("Møder/Steen", Mapper.Alle(Slags));
        Assert.DoesNotContain("Steen", Mapper.Alle(Slags));
    }

    [Fact]
    public void Undermapper_foelger_med_og_staar_i_listen()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.Opret(Slags, "Møder");
        Mapper.OpretUnder(Slags, "Steen", "2025");
        Mapper.OpretUnder(Slags, "Steen", "2026");

        var skift = Mapper.Flyt(Slags, "Steen", "Møder");

        // Alle tre skal med - ellers staar indholdet i 2025 og 2026 tilbage
        // og peger paa navne, der ikke findes.
        Assert.Equal(3, skift.Count);
        Assert.Contains(skift, s => s.Fra == "Steen" && s.Til == "Møder/Steen");
        Assert.Contains(skift, s => s.Fra == "Steen/2025" && s.Til == "Møder/Steen/2025");
        Assert.Contains(skift, s => s.Fra == "Steen/2026" && s.Til == "Møder/Steen/2026");
    }

    [Fact]
    public void En_mappe_kan_ikke_flyttes_ind_i_sig_selv()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.OpretUnder(Slags, "Møder", "Steen");

        Assert.Empty(Mapper.Flyt(Slags, "Møder", "Møder"));
        Assert.Empty(Mapper.Flyt(Slags, "Møder", "Møder/Steen"));

        // Listen skal vaere uroert.
        Assert.Contains("Møder", Mapper.Alle(Slags));
        Assert.Contains("Møder/Steen", Mapper.Alle(Slags));
    }

    [Fact]
    public void Flyt_ud_i_roden()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.OpretUnder(Slags, "Møder", "Steen");

        var skift = Mapper.Flyt(Slags, "Møder/Steen", "");

        Assert.Single(skift);
        Assert.Equal("Steen", skift[0].Til);
        Assert.Contains("Steen", Mapper.Alle(Slags));
    }

    [Fact]
    public void Flytning_til_et_navn_der_findes_afvises()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.Opret(Slags, "Steen");
        Mapper.OpretUnder(Slags, "Møder", "Steen");

        // «Møder» har allerede en «Steen». To ens stier kan man ikke skelne.
        Assert.Empty(Mapper.Flyt(Slags, "Steen", "Møder"));
        Assert.Contains("Steen", Mapper.Alle(Slags));
    }

    // -------------------------------------------------------------- omdøbning

    [Fact]
    public void Omdoeb_tager_undermapperne_med()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.OpretUnder(Slags, "Møder/Steen", "2026");

        var skift = Mapper.Omdoeb(Slags, "Møder", "Kundemøder");

        Assert.Equal(3, skift.Count);
        Assert.Contains("Kundemøder/Steen/2026", Mapper.Alle(Slags));
        Assert.DoesNotContain("Møder", Mapper.Alle(Slags));
    }

    [Fact]
    public void Omdoeb_med_skraastreg_afvises()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.Opret(Slags, "Møder");
        Assert.Empty(Mapper.Omdoeb(Slags, "Møder", "Kunder/Steen"));
        Assert.Contains("Møder", Mapper.Alle(Slags));
    }

    // ---------------------------------------------------------------- sletning

    [Fact]
    public void Slet_tager_undermapperne_med_og_melder_dem()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.OpretUnder(Slags, "Møder/Steen", "2026");
        Mapper.Opret(Slags, "Webinarer");

        var fjernet = Mapper.SletMedIndhold(Slags, "Møder");

        Assert.Equal(3, fjernet.Count);
        Assert.DoesNotContain("Møder/Steen/2026", Mapper.Alle(Slags));

        // Naboen roeres ikke.
        Assert.Contains("Webinarer", Mapper.Alle(Slags));
    }

    [Fact]
    public void De_to_slags_deler_ikke_mapper()
    {
        using var p = new Proevemappe();
        Ryd();

        Mapper.Opret(Mapper.Slags.Optagelser, "Møder");

        Assert.DoesNotContain("Møder", Mapper.Alle(Mapper.Slags.Dokumenter));
    }
}
