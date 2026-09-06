using System.IO.Compression;
using System.Text;
using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Teksten ud af de dokumenter, et projekt bygger på.
/// </summary>
/// <remarks>
/// Filerne bygges her i prøven i stedet for at ligge som prøvedata. Grunden
/// er, at en .docx ER en ZIP med XML indeni — bygger prøven den selv, står
/// det sort på hvidt, hvad læseren forventer at finde, og et format, der
/// ændrer sig, bliver opdaget som en fejl i stedet for som en tom tekst.
///
/// De rigtige filer fra Word, Excel og PowerPoint er prøvet af ved siden af,
/// 06-09-2026, og teksten kom ud af dem alle.
/// </remarks>
public class Dokumenttekstest
{
    private static string Zip(string mappe, string navn, params (string Sti, string Xml)[] dele)
    {
        var fil = Path.Combine(mappe, navn);

        using var stroem = File.Create(fil);
        using var zip = new ZipArchive(stroem, ZipArchiveMode.Create);

        foreach (var (sti, xml) in dele)
        {
            var post = zip.CreateEntry(sti);
            using var skriver = new StreamWriter(post.Open(), new UTF8Encoding(false));
            skriver.Write(xml);
        }

        return fil;
    }

    [Fact]
    public void En_docx_er_en_zip_med_xml()
    {
        using var p = new Proevemappe();

        var fil = Zip(UserDataPaths.Root, "noter.docx",
            ("word/document.xml",
             """
             <?xml version="1.0" encoding="UTF-8"?>
             <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
               <w:body><w:p><w:r><w:t>Netværk</w:t></w:r><w:r><w:t>og sikkerhed</w:t></w:r></w:p></w:body>
             </w:document>
             """));

        var u = Dokumenttekst.Laes(fil);

        Assert.Null(u.Advarsel);
        Assert.Contains("Netværk", u.Tekst);
        Assert.Contains("og sikkerhed", u.Tekst);
    }

    [Fact]
    public void Navnerummet_afgoer_ikke_om_teksten_findes()
    {
        using var p = new Proevemappe();

        // Navnerummet skifter mellem udgaver af Office. Der ses paa det
        // LOKALE navn, saa en fil fra 2010 kan laeses.
        var fil = Zip(UserDataPaths.Root, "gammel.docx",
            ("word/document.xml",
             """
             <?xml version="1.0" encoding="UTF-8"?>
             <dok xmlns:x="http://noget-helt-andet"><x:t>Eksamen i januar</x:t></dok>
             """));

        Assert.Contains("Eksamen i januar", Dokumenttekst.Laes(fil).Tekst);
    }

    [Fact]
    public void En_odt_laeses_af_sit_indhold()
    {
        using var p = new Proevemappe();

        var fil = Zip(UserDataPaths.Root, "oversigt.odt",
            ("content.xml",
             """
             <?xml version="1.0" encoding="UTF-8"?>
             <office:document-content xmlns:office="urn:oasis" xmlns:text="urn:oasis:text">
               <office:body><office:text>
                 <text:h>Pensum</text:h>
                 <text:p>Kapitel 1 om netværk.</text:p>
               </office:text></office:body>
             </office:document-content>
             """));

        var u = Dokumenttekst.Laes(fil);

        Assert.Contains("Pensum", u.Tekst);
        Assert.Contains("Kapitel 1 om netværk.", u.Tekst);
    }

    [Fact]
    public void En_fil_der_ikke_er_en_zip_giver_en_besked_og_ikke_volapyk()
    {
        using var p = new Proevemappe();

        var fil = Path.Combine(UserDataPaths.Root, "loegn.docx");
        File.WriteAllText(fil, "det her er ikke en docx");

        var u = Dokumenttekst.Laes(fil);

        Assert.Equal("", u.Tekst);
        Assert.NotNull(u.Advarsel);
    }

    [Fact]
    public void Ren_tekst_laeses_som_den_staar()
    {
        using var p = new Proevemappe();

        var fil = Path.Combine(UserDataPaths.Root, "pensum.md");
        File.WriteAllText(fil, "# Pensum\n\nKapitel 1.");

        Assert.Equal("# Pensum\n\nKapitel 1.", Dokumenttekst.Laes(fil).Tekst);
    }

    [Fact]
    public void De_tomme_linjer_ryddes_ud_af_et_regneark()
    {
        using var p = new Proevemappe();

        // Et regneark giver een celle pr. element. Uden oprydning ville
        // halvdelen af teksten vaere mellemrum - og soegningen leder efter
        // ord, der staar TAET paa hinanden.
        var fil = Zip(UserDataPaths.Root, "ark.xlsx",
            ("xl/sharedStrings.xml",
             """
             <?xml version="1.0" encoding="UTF-8"?>
             <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
               <si><t>Emne</t></si><si><t>Underviser</t></si>
             </sst>
             """));

        var tekst = Dokumenttekst.Laes(fil).Tekst;

        Assert.DoesNotContain("  ", tekst);
        Assert.DoesNotContain("\n\n", tekst);
        Assert.Contains("Emne", tekst);
    }
}

/// <summary>
/// Cachen — teksten trækkes ud én gang.
/// </summary>
public class Tekstcachetest
{
    private static string Fil(string navn, string indhold)
    {
        var sti = Path.Combine(UserDataPaths.Root, navn);
        File.WriteAllText(sti, indhold);
        return sti;
    }

    [Fact]
    public void Teksten_kommer_fra_cachen_anden_gang()
    {
        using var p = new Proevemappe();

        var sti = Fil("pensum.md", "Kapitel 1 om netværk.");

        Assert.Equal("Kapitel 1 om netværk.", Tekstcache.Hent(sti).Tekst);

        // BEVISET: cachefilen rettes. Bliver teksten laest forfra, kommer den
        // oprindelige tilbage - og saa er cachen ikke i brug.
        var cachet = Directory.EnumerateFiles(Tekstcache.Mappe, "*.txt").Single();
        File.WriteAllText(cachet, "fra cachen");

        Assert.Equal("fra cachen", Tekstcache.Hent(sti).Tekst);
    }

    [Fact]
    public void En_rettet_fil_laeses_forfra()
    {
        using var p = new Proevemappe();

        var sti = Fil("pensum.md", "Første udgave");
        Tekstcache.Hent(sti);

        // Noeglen er sti, stoerrelse og dato. Rettes filen, er den ikke den
        // samme tekst mere.
        File.WriteAllText(sti, "Anden udgave, som er længere");
        File.SetLastWriteTimeUtc(sti, DateTime.UtcNow.AddSeconds(5));

        Assert.Equal("Anden udgave, som er længere", Tekstcache.Hent(sti).Tekst);
    }

    [Fact]
    public void En_fil_der_er_vaek_giver_en_besked()
    {
        using var p = new Proevemappe();

        var u = Tekstcache.Hent(Path.Combine(UserDataPaths.Root, "findes-ikke.pdf"));

        Assert.Equal("", u.Tekst);
        Assert.NotNull(u.Advarsel);
    }

    [Fact]
    public void Advarslen_overlever_ogsaa_i_cachen()
    {
        using var p = new Proevemappe();

        var sti = Path.Combine(UserDataPaths.Root, "loegn.docx");
        File.WriteAllText(sti, "ikke en docx");

        Assert.NotNull(Tekstcache.Hent(sti).Advarsel);

        // Anden gang kommer svaret fra cachen. En advarsel, der forsvandt
        // dér, ville betyde, at filen saa fin ud efter en genstart.
        Assert.NotNull(Tekstcache.Hent(sti).Advarsel);
    }
}
