using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Afkrydsningerne på mødetypen: hvilke afsnit dokumentet skal indeholde.
/// </summary>
/// <remarks>
/// PRØVEN FINDES, FORDI HAKKET IKKE MÅ SLETTE NOGET. Fravalget står i
/// frontmatter, og teksten er urørt — så et hak kan sættes tilbage. Går den
/// del i stykker, opdages det først som et afsnit, der er væk for altid.
/// </remarks>
public class AfsnitTest
{
    private const string Skabelon = """
        navn: Prøve
        temperatur: 0.2
        maks_tokens: 2048
        ---
        Du skriver referater.

        ## Resumé
        Kort om mødet.

        ## Gennemgang
        Emne for emne.

        ### Et enkelt emne
        Tre firkanter er IKKE et afsnit.

        ## Citater
        De vigtigste citater.
        ---
        {{transskription}}
        """;

    [Fact]
    public void Afsnit_findes_af_overskrifterne()
    {
        var t = PromptTemplate.Parse(Skabelon);

        Assert.Equal(new[] { "Resumé", "Gennemgang", "Citater" }, t.Afsnit());
    }

    [Fact]
    public void Uden_fravalg_er_prompten_uaendret()
    {
        var t = PromptTemplate.Parse(Skabelon);

        Assert.Contains("## Citater", t.RenderSystem());
        Assert.Contains("## Resumé", t.RenderSystem());
    }

    [Fact]
    public void Fravalgt_afsnit_kommer_ikke_med()
    {
        var t = PromptTemplate.Parse(Skabelon);
        t.UdeladteAfsnit.Add("Citater");

        var s = t.RenderSystem();

        Assert.DoesNotContain("## Citater", s);
        Assert.DoesNotContain("De vigtigste citater", s);

        // De andre bliver staaende — ogsaa underoverskriften inde i gennemgangen.
        Assert.Contains("## Resumé", s);
        Assert.Contains("## Gennemgang", s);
        Assert.Contains("### Et enkelt emne", s);
    }

    [Fact]
    public void Teksten_i_filen_er_uroert()
    {
        var t = PromptTemplate.Parse(Skabelon);
        t.UdeladteAfsnit.Add("Citater");

        // Selve skabelonen har stadig afsnittet - hakket kan saettes tilbage.
        Assert.Contains("## Citater", t.SystemPrompt);
        Assert.Contains("## Citater", t.ToMarkdown());
        Assert.Contains("udeladte_afsnit: Citater", t.ToMarkdown());
    }

    [Fact]
    public void Fravalget_overlever_en_tur_gennem_filen()
    {
        var t = PromptTemplate.Parse(Skabelon);
        t.UdeladteAfsnit.Add("Citater");
        t.UdeladteAfsnit.Add("Resumé");

        var igen = PromptTemplate.Parse(t.ToMarkdown());

        Assert.Equal(new[] { "Citater", "Resumé" }, igen.UdeladteAfsnit);
        Assert.DoesNotContain("## Citater", igen.RenderSystem());
        Assert.DoesNotContain("## Resumé", igen.RenderSystem());
        Assert.Contains("## Gennemgang", igen.RenderSystem());
    }

    [Fact]
    public void Et_fravalg_af_noget_der_ikke_findes_goer_ingenting()
    {
        var t = PromptTemplate.Parse(Skabelon);
        t.UdeladteAfsnit.Add("Findes ikke");

        Assert.Contains("## Resumé", t.RenderSystem());
        Assert.Contains("## Citater", t.RenderSystem());
    }
}

/// <summary>
/// Deltagerreglerne: fælles regler, der slås til og fra med ét hak.
/// </summary>
/// <remarks>
/// PRØVEN FINDES, FORDI STANDARDEN SKAL VÆRE USYNLIG. En skabelon, der brugte
/// reglerne før hakket fandtes, skal blive ved med at bruge dem — og en, der
/// ikke gjorde, må ikke pludselig begynde. Går den del i stykker, ændrer alle
/// eksisterende mødetyper opførsel på én gang.
/// </remarks>
public class DeltagerreglerTest
{
    private static string Skabelon(string frontmatter, string krop) =>
        "navn: Prøve\n" + frontmatter + "\n---\n"
        + "Du skriver referater.\n" + krop + "\n"
        + "## Resumé\nKort om mødet.\n---\n"
        + "{{transskription}}\n";

    [Fact]
    public void Med_feltet_i_teksten_er_reglerne_slaaet_til()
    {
        var t = PromptTemplate.Parse(Skabelon("", "{{deltagerregler}}"));

        Assert.True(t.TagDeltagerregler);
        Assert.Contains("SÅDAN AFGØR DU, HVEM DER ER DELTAGERE", t.RenderSystem());
        Assert.DoesNotContain("{{deltagerregler}}", t.RenderSystem());
    }

    [Fact]
    public void Uden_feltet_er_de_slaaet_fra()
    {
        var t = PromptTemplate.Parse(Skabelon("", ""));

        Assert.False(t.TagDeltagerregler);
        Assert.DoesNotContain("SÅDAN AFGØR DU, HVEM DER ER DELTAGERE", t.RenderSystem());
    }

    [Fact]
    public void Hakket_fra_fjerner_reglerne_men_ikke_teksten()
    {
        var t = PromptTemplate.Parse(Skabelon("", "{{deltagerregler}}"));
        t.TagDeltagerregler = false;

        Assert.DoesNotContain("SÅDAN AFGØR DU, HVEM DER ER DELTAGERE", t.RenderSystem());

        // Feltet staar der stadig - hakket kan saettes tilbage.
        Assert.Contains("{{deltagerregler}}", t.SystemPrompt);
        Assert.Contains("{{deltagerregler}}", t.ToMarkdown());
    }

    [Fact]
    public void Hakket_til_uden_felt_laegger_reglerne_til_sidst()
    {
        var t = PromptTemplate.Parse(Skabelon("", ""));
        t.TagDeltagerregler = true;

        var s = t.RenderSystem();

        Assert.Contains("SÅDAN AFGØR DU, HVEM DER ER DELTAGERE", s);

        // Sprogreglerne laegges ogsaa til sidst - deltagerreglerne skal komme foer.
        Assert.True(s.IndexOf("SÅDAN AFGØR DU", StringComparison.Ordinal) > s.IndexOf("## Resumé", StringComparison.Ordinal));
    }

    [Fact]
    public void Valget_overlever_en_tur_gennem_filen()
    {
        var t = PromptTemplate.Parse(Skabelon("", "{{deltagerregler}}"));
        t.TagDeltagerregler = false;

        var igen = PromptTemplate.Parse(t.ToMarkdown());

        // Frontmatter vinder over det, teksten selv siger.
        Assert.False(igen.TagDeltagerregler);
        Assert.DoesNotContain("SÅDAN AFGØR DU, HVEM DER ER DELTAGERE", igen.RenderSystem());
    }

    [Fact]
    public void Frontmatter_vinder_over_teksten()
    {
        var med = PromptTemplate.Parse(Skabelon("deltagerregler: ja", ""));
        var uden = PromptTemplate.Parse(Skabelon("deltagerregler: nej", "{{deltagerregler}}"));

        Assert.True(med.TagDeltagerregler);
        Assert.False(uden.TagDeltagerregler);
    }
}
