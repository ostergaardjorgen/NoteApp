using System.Text;
using NoteApp.Core.Llm;

namespace NoteApp.Core;

/// <summary>
/// Hvad dokumentet skal blive til.
/// </summary>
/// <remarks>
/// ET VALG OG IKKE EN INDSTILLING. Word er til det, der skal rettes videre i;
/// PDF er til det, der skal sendes. De to svarer på hvert sit spørgsmål, og
/// hvilket af dem der er det rigtige, afhænger af dokumentet — ikke af
/// brugeren.
///
/// MARKDOWN ER IKKE ET VALG HER. Den er råvaren: modellen skriver markdown,
/// og begge formater bygges af den. Filen bliver liggende i projektets mappe
/// ved siden af, så den kan bygges om til det andet format uden en ny
/// kørsel — og så den kan søges i.
/// </remarks>
public enum Dokumentformat
{
    /// <summary>.docx — til det, der skal rettes videre i.</summary>
    Word,

    /// <summary>.pdf — til det, der skal sendes.</summary>
    Pdf,
}

/// <summary>Resultatet af en kørsel — filen, tallene og det, der blev brugt.</summary>
public sealed record Projektresultat(
    string Sti,
    string Markdownsti,
    Projektsvar Kontekst,
    int TokensInd,
    int TokensUd,
    decimal PrisEur,
    TimeSpan Forloebet);

/// <summary>
/// Bygger et dokument ud af et helt projekt.
/// </summary>
/// <remarks>
/// ============ HVOR DET LANDER, OG HVORFOR DÉR ============
///
/// Dokumentet skrives ned i PROJEKTETS EGEN MAPPE. Det har tre følger, og de
/// er alle med vilje:
///
///   Det er med i sikkerhedskopien, som alt andet i datamappen.
///   Det bliver en del af fundamentet — næste dokument kan bygge på det.
///   Det kan åbnes uden appen. En Markdown-fil er en tekstfil.
///
/// ============ KILDERNE STÅR I FILEN ============
///
/// Nederst i hvert dokument står, hvilke filer det blev bygget på. Et
/// dokument, en model har skrevet, er et forslag, indtil nogen har set efter
/// — og det kan man kun, hvis der står hvor. Det er derfor
/// <see cref="Projektkontekst.Kildeliste"/> ikke er til at slå fra.
///
/// ============ SAMTYKKET SPØRGES HER ============
///
/// <see cref="Projekt.MaaSendesSomKilde"/> er fra som standard. Er den ikke
/// sat, køres der ikke — og der siges hvorfor. Det er dét sted, projektets
/// materiale ellers ville forlade maskinen.
/// </remarks>
public static class Projektdokument
{
    /// <summary>Kastes, når projektet ikke må sendes som kilde.</summary>
    public sealed class IkkeTilladt(string besked) : Exception(besked);

    public static async Task<Projektresultat> BygAsync(
        Projekt projekt,
        PromptTemplate skabelon,
        SkyModel model,
        string noegle,
        Dokumentformat format = Dokumentformat.Word,
        IProgress<LlmProgress>? fremdrift = null,
        CancellationToken ct = default,
        string dokumentsprog = Sprogregler.Standard)
    {
        if (!projekt.MaaSendesSomKilde)
        {
            throw new IkkeTilladt(
                "Projektet må ikke bruges som kilde til dokumenter. "
                + "Sæt hakket under Indstillinger på projektet — det er dér, "
                + "materialet forlader din maskine.");
        }

        fremdrift?.Report(new LlmProgress("Samler projektets materiale …"));

        // SKABELONENS AFSNIT ER FREMSØGNINGSSPØRGSMÅLENE. Hedder et afsnit
        // «Tidsplan», hentes de steder, der handler om tidsplan. Det er dét,
        // der gør et langt projekt brugbart uden at sende det hele.
        var afsnit = PromptTemplate.AfsnitI(skabelon.SystemPrompt).ToList();
        var kontekst = Projektkontekst.Byg(projekt, afsnit);

        var felter = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["kilder"] = kontekst.Tekst,
            ["projekt"] = string.IsNullOrWhiteSpace(projekt.Beskrivelse)
                ? projekt.Navn
                : $"{projekt.Navn}\n\n{projekt.Beskrivelse}",
        };

        var r = await new SkyRunner(noegle).KoerAsync(
            model, skabelon, skabelon.Render(felter), fremdrift, ct,
            kilde: projekt.Id,
            kildeTitel: projekt.Navn,
            dokumentsprog: dokumentsprog);

        var (sti, markdownsti) = Skriv(projekt, skabelon, r.Tekst, kontekst, format);

        Historik.Skriv(HaendelseType.Dokument,
            $"Projektdokument oprettet: {Path.GetFileName(sti)}",
            $"Projekt «{projekt.Navn}» · skabelon «{skabelon.Name}» · {model.Navn} · "
            + $"{r.TokensInd} tokens sendt, {r.TokensUd} modtaget · €{r.PrisEur:0.0000} · "
            + $"{kontekst.Kilder.Count} kilde(r)"
            + (kontekst.Afkortet ? " · materialet blev afkortet" : ""),
            Udfald.Fuldført, model.Navn, sti, r.Forloebet.TotalSeconds,
            kilde: projekt.Id);

        return new Projektresultat(sti, markdownsti, kontekst, r.TokensInd, r.TokensUd,
            r.PrisEur, r.Forloebet);
    }

    /// <summary>
    /// Skriver dokumentet ned i projektets egen mappe.
    /// </summary>
    /// <remarks>
    /// FILNAVNET BÆRER DATOEN. Bygger man en studieplan igen om en måned, er
    /// det et nyt dokument og ikke en rettelse af det gamle — og de to skal
    /// kunne stå ved siden af hinanden. En fil, der overskrives i stilhed, er
    /// en fil, man ikke kan sammenligne med noget.
    /// </remarks>
    private static (string Sti, string Markdown) Skriv(
        Projekt projekt, PromptTemplate skabelon, string tekst,
        Projektsvar kontekst, Dokumentformat format)
    {
        Directory.CreateDirectory(projekt.Dokumentmappe);

        var sb = new StringBuilder();

        sb.AppendLine(tekst.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(Projektkontekst.Kildeliste(kontekst));

        var markdown = sb.ToString();

        // ============ STAMMEN ER FAELLES ============
        //
        // Markdownfilen og det valgte format faar det SAMME navn med hver sin
        // endelse. Saa staar de ved siden af hinanden i mappen, og man kan se,
        // at de hoerer sammen - uden at skulle laese i dem.
        var stamme = Uden(PromptTemplate.Filnavn($"{skabelon.Name} {DateTime.Now:yyyy-MM-dd}"));
        var endelse = format == Dokumentformat.Pdf ? ".pdf" : ".docx";

        // TO PAA DEN SAMME DAG ER TO. Det andet faar et nummer frem for at
        // skrive hen over det foerste.
        var n = 2;
        while (File.Exists(Path.Combine(projekt.Dokumentmappe, stamme + endelse))
               || File.Exists(Path.Combine(projekt.Dokumentmappe, stamme + ".md")))
        {
            stamme = Uden(PromptTemplate.Filnavn(
                $"{skabelon.Name} {DateTime.Now:yyyy-MM-dd} {n++}"));
        }

        var md = Path.Combine(projekt.Dokumentmappe, stamme + ".md");
        var sti = Path.Combine(projekt.Dokumentmappe, stamme + endelse);

        // MARKDOWNEN BLIVER LIGGENDE. Den er raavaren: den kan bygges om til
        // det andet format uden en ny koersel, og den kan soeges i.
        var midlertidig = md + ".ny";
        File.WriteAllText(midlertidig, markdown, new UTF8Encoding(false));
        File.Move(midlertidig, md, overwrite: true);

        var titel = $"{skabelon.Name} — {projekt.Navn}";

        var forside = new List<(string, string)>
        {
            ("Projekt", projekt.Navn),
            ("Skabelon", skabelon.Name),
            ("Dato", DateTime.Now.ToString("d. MMMM yyyy", Sprog.Kultur)),
        };

        if (format == Dokumentformat.Pdf)
            Documents.PdfWriter.Write(sti, titel, markdown, forside);
        else
            Documents.DocxWriter.Write(sti, titel, markdown, forside);

        return (sti, md);
    }

    /// <summary>Filnavnet uden endelse. Filnavn() laegger «.md» paa.</summary>
    private static string Uden(string filnavn) =>
        filnavn.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? filnavn[..^3] : filnavn;
}
