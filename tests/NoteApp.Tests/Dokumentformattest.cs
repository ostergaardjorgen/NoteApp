using NoteApp.Core;
using NoteApp.Core.Documents;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Word og PDF — det er et valg, og begge skal indeholde teksten.
/// </summary>
/// <remarks>
/// PRØVEN LÆSER FILERNE TILBAGE. En skrivning, der ikke fejler, er ikke det
/// samme som en fil, der kan læses: en .docx uden den rigtige relationsfil
/// åbner ikke i Word, og en PDF uden en skrifttype tegner ingenting. Begge
/// dele ville se ud som en succes i koden.
///
/// Derfor pakkes .docx'en op igen, og PDF'en læses med den samme læser, som
/// projektets fundament bruger. Kommer teksten ud i begge ender, holder
/// kæden.
/// </remarks>
public class Dokumentformattest
{
    private const string Markdown = """
        # Studieplan

        Forløbet strækker sig over to år med **fire moduler**.

        ## Modul 1

        - Netværk og infrastruktur
        - Adgangsstyring i praksis

        ---

        > Der var mere materiale, end der var plads til.
        """;

    private static readonly (string, string)[] Forside =
    {
        ("Projekt", "Datateknikeruddannelsen"),
        ("Dato", "6. september 2026"),
    };

    [Fact]
    public void En_docx_kan_pakkes_op_og_indeholder_teksten()
    {
        using var p = new Proevemappe();

        var sti = Path.Combine(UserDataPaths.Root, "plan.docx");
        DocxWriter.Write(sti, "Studieplan", Markdown, Forside);

        Assert.True(File.Exists(sti));

        // Samme laeser som projektets fundament bruger.
        var u = Dokumenttekst.Laes(sti);

        Assert.Null(u.Advarsel);
        Assert.Contains("Studieplan", u.Tekst);
        Assert.Contains("fire moduler", u.Tekst);
        Assert.Contains("Adgangsstyring i praksis", u.Tekst);
        Assert.Contains("Datateknikeruddannelsen", u.Tekst);
    }

    [Fact]
    public void En_pdf_kan_laeses_tilbage_og_indeholder_teksten()
    {
        using var p = new Proevemappe();

        var sti = Path.Combine(UserDataPaths.Root, "plan.pdf");
        PdfWriter.Write(sti, "Studieplan", Markdown, Forside);

        Assert.True(File.Exists(sti));

        var u = Dokumenttekst.Laes(sti);

        // EN PDF UDEN TEKSTLAG VILLE GIVE ADVARSLEN OM EN SCANNET FIL. Faar
        // vi den her, har vi skrevet en PDF, ingen kan soege i.
        Assert.Null(u.Advarsel);

        Assert.Contains("Studieplan", u.Tekst);
        Assert.Contains("fire moduler", u.Tekst);
        Assert.Contains("Adgangsstyring i praksis", u.Tekst);
        Assert.Contains("Datateknikeruddannelsen", u.Tekst);
    }

    [Fact]
    public void De_to_formater_indeholder_det_samme()
    {
        using var p = new Proevemappe();

        var docx = Path.Combine(UserDataPaths.Root, "a.docx");
        var pdf = Path.Combine(UserDataPaths.Root, "a.pdf");

        DocxWriter.Write(docx, "Studieplan", Markdown, Forside);
        PdfWriter.Write(pdf, "Studieplan", Markdown, Forside);

        var a = Dokumenttekst.Laes(docx).Tekst;
        var b = Dokumenttekst.Laes(pdf).Tekst;

        // ORD FOR ORD ER FOR STRENGT - en PDF ombryder linjer og har sidetal.
        // MEN INDHOLDET SKAL VAERE DET SAMME: kunne det ene format mere end
        // det andet, ville valget handle om andet end formatet.
        foreach (var ord in new[]
                 {
                     "Studieplan", "Forløbet", "fire moduler", "Modul 1",
                     "Netværk og infrastruktur", "Adgangsstyring i praksis",
                     "mere materiale",
                 })
        {
            Assert.True(a.Contains(ord, StringComparison.OrdinalIgnoreCase),
                $"«{ord}» mangler i .docx");
            Assert.True(b.Contains(ord, StringComparison.OrdinalIgnoreCase),
                $"«{ord}» mangler i .pdf");
        }
    }

    [Fact]
    public void Stjernerne_bliver_ikke_staaende_i_teksten()
    {
        using var p = new Proevemappe();

        var pdf = Path.Combine(UserDataPaths.Root, "b.pdf");
        PdfWriter.Write(pdf, "Prøve", "Det er **fed** tekst.", null);

        var tekst = Dokumenttekst.Laes(pdf).Tekst;

        Assert.Contains("fed", tekst);
        Assert.DoesNotContain("**", tekst);
    }

    [Fact]
    public void En_overskrift_bliver_ikke_til_en_havelaage()
    {
        using var p = new Proevemappe();

        var pdf = Path.Combine(UserDataPaths.Root, "c.pdf");
        PdfWriter.Write(pdf, "Prøve", "# Overskriften\n\nBrødtekst.", null);

        var tekst = Dokumenttekst.Laes(pdf).Tekst;

        Assert.Contains("Overskriften", tekst);
        Assert.DoesNotContain("# Overskriften", tekst);
    }
}
