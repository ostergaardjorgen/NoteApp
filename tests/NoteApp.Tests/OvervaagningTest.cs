using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// De overvågede mapper — reglerne fra doc/overvaagede-mapper.md.
///
/// Prøverne står i samme rækkefølge som reglerne i dokumentet, så en ændret
/// regel og en fejlende prøve peger på hinanden.
/// </summary>
public class OvervaagningTest
{
    /// <summary>En mappe uden for datamappen, som kan overvåges.</summary>
    private static string Kigmappe(Proevemappe p, string navn = "kig")
    {
        var m = Path.Combine(p.Sti, navn);
        Directory.CreateDirectory(m);
        return m;
    }

    private static string Lydfil(string mappe, string navn, int minutterGammel = 60, int bytes = 4096)
    {
        var sti = Path.Combine(mappe, navn);
        File.WriteAllBytes(sti, new byte[bytes]);
        File.SetLastWriteTime(sti, DateTime.Now.AddMinutes(-minutterGammel));
        return sti;
    }

    private static List<Overvaagetmappe> Een(string sti, bool automatisk = false, bool undermapper = true) =>
        new() { new Overvaagetmappe { Sti = sti, Aktiv = true, Automatisk = automatisk, Undermapper = undermapper } };

    // ===================== DER FINDES KUN LYDFILER =====================

    [Fact]
    public void Kun_kendte_lydformater_er_fund()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Lydfil(m, "moede.m4a");
        Lydfil(m, "diktat.mp3");
        Lydfil(m, "referat.docx");
        Lydfil(m, "billede.png");
        Lydfil(m, "noter.txt");

        var fund = Overvaagning.Kig(Een(m));

        Assert.Equal(2, fund.Count);
        Assert.Contains(fund, f => f.Filnavn == "moede.m4a");
        Assert.Contains(fund, f => f.Filnavn == "diktat.mp3");
    }

    [Fact]
    public void Tomme_filer_er_ikke_fund()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Lydfil(m, "tom.m4a", bytes: 0);

        Assert.Empty(Overvaagning.Kig(Een(m)));
    }

    // ===================== EN FIL SKAL LIGGE STILLE =====================

    [Fact]
    public void En_fil_der_lige_er_skrevet_venter()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        // Nul minutter gammel: den kan stadig vaere ved at blive kopieret ind.
        // Laeses den nu, bliver den lagt ind halv.
        Lydfil(m, "under-kopiering.m4a", minutterGammel: 0);

        Assert.Empty(Overvaagning.Kig(Een(m)));
    }

    [Fact]
    public void En_fil_der_har_ligget_stille_er_et_fund()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Lydfil(m, "faerdig.m4a", minutterGammel: 1);

        Assert.Single(Overvaagning.Kig(Een(m)));
    }

    // ===================== SKJULTE FILER OG SYSTEMFILER =====================

    [Fact]
    public void Skjulte_filer_springes_over()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        var sti = Lydfil(m, "synkroniseringsrester.m4a");
        File.SetAttributes(sti, File.GetAttributes(sti) | FileAttributes.Hidden);

        Assert.Empty(Overvaagning.Kig(Een(m)));
    }

    // ===================== UNDERMAPPER =====================

    [Fact]
    public void Undermapper_kan_slaas_fra()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        var under = Path.Combine(m, "dybere");
        Directory.CreateDirectory(under);

        Lydfil(m, "oeverst.m4a");
        Lydfil(under, "nede.m4a");

        Assert.Equal(2, Overvaagning.Kig(Een(m, undermapper: true)).Count);
        Assert.Single(Overvaagning.Kig(Een(m, undermapper: false)));
    }

    // ===================== HVER FIL TILBYDES ÉN GANG =====================

    [Fact]
    public void En_husket_fil_tilbydes_ikke_igen()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        var sti = Lydfil(m, "een-gang.m4a");

        Assert.Single(Overvaagning.Kig(Een(m)));

        Overvaagning.Husk(sti);

        Assert.Empty(Overvaagning.Kig(Een(m)));
        Assert.Equal(1, Overvaagning.Husket());
    }

    [Fact]
    public void En_aendret_fil_regnes_som_ny()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        var sti = Lydfil(m, "skrevet-om.m4a", bytes: 1000);
        Overvaagning.Husk(sti);

        Assert.Empty(Overvaagning.Kig(Een(m)));

        // Samme navn, anden stoerrelse: det ER ikke den samme optagelse.
        File.WriteAllBytes(sti, new byte[2000]);
        File.SetLastWriteTime(sti, DateTime.Now.AddMinutes(-60));

        Assert.Single(Overvaagning.Kig(Een(m)));
    }

    [Fact]
    public void GlemAlt_tilbyder_dem_igen()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Overvaagning.Husk(Lydfil(m, "a.m4a"));
        Overvaagning.Husk(Lydfil(m, "b.m4a"));

        Assert.Empty(Overvaagning.Kig(Een(m)));

        Overvaagning.GlemAlt();

        Assert.Equal(2, Overvaagning.Kig(Een(m)).Count);
        Assert.Equal(0, Overvaagning.Husket());
    }

    // ===================== INAKTIVE MAPPER =====================

    [Fact]
    public void En_inaktiv_mappe_kigges_der_ikke_i()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Lydfil(m, "ligegyldig.m4a");

        var mapper = Een(m);
        mapper[0].Aktiv = false;

        Assert.Empty(Overvaagning.Kig(mapper));
    }

    [Fact]
    public void En_mappe_der_er_forsvundet_vaelter_ingenting()
    {
        using var p = new Proevemappe();

        var mapper = Een(Path.Combine(p.Sti, "findes-ikke"));

        Assert.Empty(Overvaagning.Kig(mapper));
    }

    // ===================== INDSTILLINGERNE GEMMES =====================

    [Fact]
    public void Mapperne_overlever_en_gemning()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Overvaagning.Gem(new[]
        {
            new Overvaagetmappe { Sti = m, Automatisk = true, Folder = "Kunde A", Herkomst = "iCloudDrive" }
        });

        var laest = Overvaagning.Mapper();

        Assert.Single(laest);
        Assert.Equal(m, laest[0].Sti);
        Assert.True(laest[0].Automatisk);
        Assert.Equal("Kunde A", laest[0].Folder);
        Assert.Equal("iCloudDrive", laest[0].Herkomst);
    }

    [Fact]
    public void Ingen_gemte_mapper_giver_en_tom_liste_og_ingen_fejl()
    {
        using var p = new Proevemappe();

        Assert.Empty(Overvaagning.Mapper());
        Assert.Empty(Overvaagning.Kig());
    }

    // ===================== NYESTE FØRST =====================

    [Fact]
    public void Fundene_kommer_nyeste_foerst()
    {
        using var p = new Proevemappe();
        var m = Kigmappe(p);

        Lydfil(m, "gammel.m4a", minutterGammel: 6000);
        Lydfil(m, "ny.m4a", minutterGammel: 30);
        Lydfil(m, "mellem.m4a", minutterGammel: 600);

        var fund = Overvaagning.Kig(Een(m));

        Assert.Equal("ny.m4a", fund[0].Filnavn);
        Assert.Equal("mellem.m4a", fund[1].Filnavn);
        Assert.Equal("gammel.m4a", fund[2].Filnavn);
    }
}
