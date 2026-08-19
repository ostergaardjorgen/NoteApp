using System;
using System.Linq;
using System.Threading.Tasks;
using NoteApp.Core.Llm;

namespace NoteApp.Desktop.Templates;

/// <summary>
/// Dagsordenen til et møde, der skal optages.
///
/// HVORFOR DEN SKRIVES AF EN MODEL
///
/// En dagsorden er ikke pynt her. Den er det eneste sted, hvor man kan
/// påvirke, hvad der faktisk bliver SAGT under mødet — og udskriften kan kun
/// indeholde det, nogen sagde højt. Beder dagsordenen ikke om frister, står
/// der ingen frister i referatet, uanset hvor god skabelonen er.
///
/// Derfor hører dagsordenen til skabelonen: et referat skal bruge beslutninger
/// og frister, en dokumentation skal bruge forudsætninger og det, ingen kunne
/// svare på, og en brainstorm skal bruge de idéer, der blev forkastet, og
/// hvorfor.
///
/// HVORFOR STANDARDEN GIVES MED
///
/// Modellen bliver bedt om at TILPASSE standarden, ikke om at skrive en fra
/// bunden. De fem ting, standarden sikrer — navne, formål, beslutninger sagt
/// højt, det ingen ved, og hvem der følger op — gælder ethvert møde, og de
/// skal ikke kunne forsvinde, fordi en model syntes, den kunne gøre det bedre.
///
/// Navnerunden skal blive ordret. Uden den har talegenkendelsen ingen navne at
/// genkende, og så kan intet dokument sige, hvem der sagde hvad.
/// </summary>
internal static class Dagsordensskriver
{
    /// <summary>
    /// Standarddagsordenen. Den ligger som en indlejret tekstfil frem for i
    /// koden, så den kan rettes uden at røre en eneste linje C#.
    /// </summary>
    public static string Standard()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var navn = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("agenda.txt", StringComparison.Ordinal));

        if (navn is null) return "Dagsordenen kunne ikke indlæses.";

        using var s = asm.GetManifestResourceStream(navn);
        if (s is null) return "Dagsordenen kunne ikke indlæses.";

        using var l = new System.IO.StreamReader(s, System.Text.Encoding.UTF8);
        return l.ReadToEnd();
    }

    /// <summary>
    /// Beder Mistral tilpasse standarddagsordenen til én bestemt skabelon.
    ///
    /// Kaldes to steder, og det er med vilje det samme kald: fra guiden, når
    /// en ny skabelon laves, og fra knappen på Agenda-fanen, når instruktionen
    /// er ændret bagefter. Stod prompten to steder, ville de to veje langsomt
    /// give hver sit resultat.
    /// </summary>
    public static async Task<string> SkrivAsync(string noegle, string skabelonnavn, string systemprompt)
    {
        var opskrift = new PromptTemplate
        {
            Name = "Dagsordensskriver",
            Temperature = 0.3,
            MaxTokens = 2000,
            SystemPrompt =
                "Du tilpasser dagsordener til møder, der bliver optaget og skrevet ud " +
                "til tekst. Du skriver ALTID på dansk.\n\n" +
                "Du får en standarddagsorden og den instruktion, et dokument bliver " +
                "lavet efter bagefter. Din opgave er at rette dagsordenen til, så mødet " +
                "af sig selv kommer omkring det, dokumentet har brug for.\n\n" +
                "Behold punkt 1 (navnerunden) ordret. Uden den har talegenkendelsen " +
                "ingen navne at genkende.\n\n" +
                "Behold formen: nummererede punkter med en tidsangivelse i parentes og " +
                "konkrete sætninger, deltagerne skal sige højt. Tilføj, fjern og omskriv " +
                "de øvrige punkter, så de passer til dokumentet.\n\n" +
                "Svar med dagsordenen og intet andet — ingen indledning, ingen " +
                "forklaring, ingen kodeblok omkring.",
            UserPrompt = ""
        };

        var svar = await new SkyRunner(noegle).KoerAsync(
            SkyKatalog.Standard, opskrift,
            $"Dokumentet, der skal laves bagefter, hedder «{skabelonnavn}».\n\n" +
            $"Instruktionen til det:\n{systemprompt}\n\n" +
            $"Standarddagsordenen:\n{Standard()}");

        return Afpil(svar.Tekst);
    }

    /// <summary>
    /// Fjerner en kodeblok, hvis modellen har lagt en om svaret alligevel.
    /// </summary>
    private static string Afpil(string raa)
    {
        var tekst = raa.Trim();
        if (!tekst.StartsWith("```", StringComparison.Ordinal)) return tekst;

        var foerste = tekst.IndexOf('\n');
        if (foerste > 0) tekst = tekst[(foerste + 1)..];
        if (tekst.EndsWith("```", StringComparison.Ordinal)) tekst = tekst[..^3];
        return tekst.Trim();
    }
}
