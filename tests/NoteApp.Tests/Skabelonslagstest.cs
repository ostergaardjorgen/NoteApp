using NoteApp.Core.Llm;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// To slags skabeloner, to mapper — og de må ikke løbe ind i hinanden.
/// </summary>
/// <remarks>
/// En mødetype skriver et dokument ud af én optagelse. Et projektoutput
/// skriver et dokument ud af et helt projekt. Havnede et projektoutput blandt
/// mødetyperne, ville det stå som valgmulighed, når man laver et dokument ud
/// af en optagelse — og dér er det meningsløst, for der er intet projekt at
/// hente noget fra.
///
/// Prøven holder også fast i, at MØDETYPERNE BLIVER LIGGENDE, hvor de altid
/// har ligget. De skabeloner, brugeren selv har skrevet, må ikke flyttes af en
/// opdatering.
/// </remarks>
public class Skabelonslagstest
{
    private static PromptTemplate Ny(string navn, Skabelonslags slags) => new()
    {
        Name = navn,
        SystemPrompt = "## Afsnit\n\nSkriv noget.",
        UserPrompt = "{{transskription}}",
        Slags = slags,
    };

    [Fact]
    public void Moedetyperne_ligger_hvor_de_altid_har_ligget()
    {
        using var p = new Proevemappe();

        Assert.Equal(PromptTemplate.Directory,
            PromptTemplate.Skabelonmappe(Skabelonslags.Moedetype));
    }

    [Fact]
    public void Projektoutput_ligger_i_sin_egen_undermappe()
    {
        using var p = new Proevemappe();

        var mappe = PromptTemplate.Skabelonmappe(Skabelonslags.Projektoutput);

        Assert.StartsWith(PromptTemplate.Directory, mappe, StringComparison.Ordinal);
        Assert.NotEqual(PromptTemplate.Directory, mappe);
        Assert.Equal("projekt", Path.GetFileName(mappe));
    }

    [Fact]
    public void En_skabelon_gemmes_i_sin_egen_slags_mappe()
    {
        using var p = new Proevemappe();

        var moede = Ny("Referat", Skabelonslags.Moedetype).Save();
        var projekt = Ny("Tilbud", Skabelonslags.Projektoutput).Save();

        Assert.Equal(PromptTemplate.Skabelonmappe(Skabelonslags.Moedetype),
            Path.GetDirectoryName(moede));

        Assert.Equal(PromptTemplate.Skabelonmappe(Skabelonslags.Projektoutput),
            Path.GetDirectoryName(projekt));
    }

    [Fact]
    public void De_to_lister_holdes_adskilt()
    {
        using var p = new Proevemappe();

        Ny("Referat", Skabelonslags.Moedetype).Save();
        Ny("Tilbud", Skabelonslags.Projektoutput).Save();
        Ny("Projektbeskrivelse", Skabelonslags.Projektoutput).Save();

        var moedetyper = PromptTemplate.LoadAll(Skabelonslags.Moedetype);
        var projekt = PromptTemplate.LoadAll(Skabelonslags.Projektoutput);

        Assert.Contains(moedetyper, t => t.Name == "Referat");
        Assert.DoesNotContain(moedetyper, t => t.Name == "Tilbud");
        Assert.DoesNotContain(moedetyper, t => t.Name == "Projektbeskrivelse");

        Assert.Equal(2, projekt.Count);
        Assert.DoesNotContain(projekt, t => t.Name == "Referat");
    }

    [Fact]
    public void Den_gamle_LoadAll_svarer_stadig_kun_med_moedetyper()
    {
        using var p = new Proevemappe();

        Ny("Referat", Skabelonslags.Moedetype).Save();
        Ny("Tilbud", Skabelonslags.Projektoutput).Save();

        // Der er kaldere nok af den, og de mener alle sammen mødetyper.
        var alle = PromptTemplate.LoadAll();

        Assert.Contains(alle, t => t.Name == "Referat");
        Assert.DoesNotContain(alle, t => t.Name == "Tilbud");
    }

    [Fact]
    public void En_indlaest_skabelon_ved_hvilken_slags_den_er()
    {
        using var p = new Proevemappe();

        Ny("Tilbud", Skabelonslags.Projektoutput).Save();

        var t = PromptTemplate.LoadAll(Skabelonslags.Projektoutput).Single();

        Assert.Equal(Skabelonslags.Projektoutput, t.Slags);
    }

    [Fact]
    public void To_skabeloner_kan_hedde_det_samme_i_hver_sin_liste()
    {
        using var p = new Proevemappe();

        // «Opsummering» giver mening begge steder. Filnavnet er det samme, og
        // uden to mapper ville den ene overskrive den anden i stilhed.
        var a = Ny("Opsummering", Skabelonslags.Moedetype).Save();
        var b = Ny("Opsummering", Skabelonslags.Projektoutput).Save();

        Assert.NotEqual(a, b);
        Assert.True(File.Exists(a));
        Assert.True(File.Exists(b));

        Assert.Single(PromptTemplate.LoadAll(Skabelonslags.Moedetype));
        Assert.Single(PromptTemplate.LoadAll(Skabelonslags.Projektoutput));
    }
}
