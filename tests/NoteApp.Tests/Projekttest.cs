using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Projektet — og den ene regel, hele modellen står på: det PEGER, det
/// indeholder ikke.
/// </summary>
public class Projekttest
{
    [Fact]
    public void Et_nyt_projekt_faar_sine_mapper()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Datateknikeruddannelsen");

        Assert.True(Directory.Exists(pr.Mappe));
        Assert.True(Directory.Exists(pr.Dokumentmappe));
        Assert.StartsWith(Projektlager.Rod, pr.Mappe, StringComparison.Ordinal);
    }

    [Fact]
    public void Det_kan_laeses_igen()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Tilbud til Indigo", "Alt om udbuddet");
        pr.Mapper.Add(@"C:\iCloudDrive\Kursus");
        pr.Tilfoej(Medlemsslags.Optagelse, "abc123");
        Projektlager.Gem(pr);

        var igen = Projektlager.Hent(pr.Id);

        Assert.NotNull(igen);
        Assert.Equal("Tilbud til Indigo", igen!.Navn);
        Assert.Equal("Alt om udbuddet", igen.Beskrivelse);
        Assert.Equal(@"C:\iCloudDrive\Kursus", Assert.Single(igen.Mapper));
        Assert.True(igen.Har(Medlemsslags.Optagelse, "abc123"));
    }

    [Fact]
    public void Standarden_er_med_i_soegningen_men_ikke_som_kilde()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Nyt");

        // Soegning er rent lokal. Kildedeling sender tekst ud af maskinen, og
        // det skal vaere et valg, man har truffet.
        Assert.True(pr.MedISoegning);
        Assert.False(pr.MaaSendesSomKilde);

        Assert.False(Projektlager.Hent(pr.Id)!.MaaSendesSomKilde);
    }

    [Fact]
    public void Det_samme_medlem_to_gange_er_ikke_to()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Nyt");

        Assert.True(pr.Tilfoej(Medlemsslags.Optagelse, "abc"));
        Assert.False(pr.Tilfoej(Medlemsslags.Optagelse, "abc"));

        Assert.Single(pr.Medlemmer);

        // Samme id, anden slags, er noget andet.
        Assert.True(pr.Tilfoej(Medlemsslags.Note, "abc"));
        Assert.Equal(2, pr.Medlemmer.Count);
    }

    [Fact]
    public void Arkivering_roerer_ikke_det_projektet_peger_paa()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Faerdigt");
        pr.Tilfoej(Medlemsslags.Optagelse, "abc");
        Projektlager.Gem(pr);

        Projektlager.Arkiver(pr);

        var igen = Projektlager.Hent(pr.Id)!;

        Assert.Equal(Projektstatus.Arkiveret, igen.Status);
        Assert.NotNull(igen.Arkiveret);

        // MEDLEMSKABET STAAR VED. At laegge et projekt vaek maa ikke skjule de
        // moeder, det peger paa - de hoerer ogsaa til andre sammenhaenge.
        Assert.True(igen.Har(Medlemsslags.Optagelse, "abc"));

        Projektlager.Genaktiver(igen);
        Assert.Equal(Projektstatus.Aktiv, Projektlager.Hent(pr.Id)!.Status);
        Assert.Null(Projektlager.Hent(pr.Id)!.Arkiveret);
    }

    [Fact]
    public void Aktive_projekter_staar_foerst()
    {
        using var p = new Proevemappe();

        var gammelt = Projektlager.Opret("Gammelt");
        Projektlager.Arkiver(gammelt);

        Projektlager.Opret("I gang");

        var alle = Projektlager.Alle();

        Assert.Equal(2, alle.Count);
        Assert.Equal("I gang", alle[0].Navn);
        Assert.Equal(Projektstatus.Arkiveret, alle[1].Status);
    }

    [Fact]
    public void Sletning_tager_kun_projektets_egne_ting()
    {
        using var p = new Proevemappe();

        // En tilknyttet mappe UDEN FOR datamappen. Den skal ligge urørt
        // bagefter - projektet ejer den ikke.
        var udenfor = Path.Combine(Path.GetTempPath(), "heypia-proeve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(udenfor);
        File.WriteAllText(Path.Combine(udenfor, "pensum.md"), "# Pensum");

        try
        {
            var pr = Projektlager.Opret("Slettes");
            pr.Mapper.Add(udenfor);
            Projektlager.Gem(pr);

            File.WriteAllText(Path.Combine(pr.Dokumentmappe, "egen.md"), "# Egen");

            Projektlager.Slet(pr);

            Assert.False(Directory.Exists(pr.Mappe));
            Assert.True(File.Exists(Path.Combine(udenfor, "pensum.md")));
            Assert.Empty(Projektlager.Alle());
        }
        finally
        {
            try { Directory.Delete(udenfor, recursive: true); } catch (IOException) { }
        }
    }
}

/// <summary>
/// Fundamentets filer — og det, der ikke kan læses.
/// </summary>
public class Projektkildetest
{
    private static Projekt MedFiler(params string[] filnavne)
    {
        var pr = Projektlager.Opret("Uddannelse");

        foreach (var n in filnavne)
            File.WriteAllText(Path.Combine(pr.Dokumentmappe, n), "indhold");

        return pr;
    }

    [Fact]
    public void De_laesbare_kommer_med()
    {
        using var p = new Proevemappe();

        var pr = MedFiler("pensum.pdf", "noter.docx", "regneark.xlsx",
                          "oversigt.odt", "liste.md");

        var filer = Projektkilder.Filer(pr);

        Assert.Equal(5, filer.Count);
        Assert.All(filer, f => Assert.True(f.Laesbar));
        Assert.All(filer, f => Assert.True(f.Egen));
    }

    [Theory]
    [InlineData("gammel.doc", "docx")]
    [InlineData("gammel.xls", "xlsx")]
    [InlineData("noget.gdoc", "Download")]
    [InlineData("ark.gsheet", "Download")]
    public void Det_der_ikke_kan_laeses_faar_en_grund(string filnavn, string forventet)
    {
        using var p = new Proevemappe();

        var f = Assert.Single(Projektkilder.Filer(MedFiler(filnavn)));

        Assert.False(f.Laesbar);
        Assert.NotNull(f.Afvist);
        Assert.Contains(forventet, f.Afvist!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Alt_andet_springes_over_uden_en_bemaerkning()
    {
        using var p = new Proevemappe();

        // En mappe med billeder og programfiler skal ikke give en liste af
        // afvisninger, ingen har bedt om.
        var pr = MedFiler("billede.png", "lyd.mp3", "program.exe", "pensum.pdf");

        var f = Assert.Single(Projektkilder.Filer(pr));
        Assert.Equal("pensum.pdf", f.Filnavn);
    }

    [Fact]
    public void En_tilknyttet_mappe_er_ikke_projektets_egen()
    {
        using var p = new Proevemappe();

        var udenfor = Path.Combine(Path.GetTempPath(), "heypia-proeve-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(udenfor);
        File.WriteAllText(Path.Combine(udenfor, "kursus.pdf"), "x");

        try
        {
            var pr = MedFiler("egen.md");
            pr.Mapper.Add(udenfor);

            var filer = Projektkilder.Filer(pr);

            Assert.Equal(2, filer.Count);
            Assert.True(filer.Single(f => f.Filnavn == "egen.md").Egen);
            Assert.False(filer.Single(f => f.Filnavn == "kursus.pdf").Egen);
        }
        finally
        {
            try { Directory.Delete(udenfor, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void En_mappe_der_ikke_findes_vaelter_ingenting()
    {
        using var p = new Proevemappe();

        var pr = Projektlager.Opret("Uddannelse");
        pr.Mapper.Add(@"Z:\findes\ikke");

        Assert.Empty(Projektkilder.Filer(pr));
    }
}
