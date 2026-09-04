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
