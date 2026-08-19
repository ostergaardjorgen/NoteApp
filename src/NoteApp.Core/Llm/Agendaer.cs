using System.IO;

namespace NoteApp.Core.Llm;

/// <summary>
/// Dagsordenen, der hører til en skabelon.
///
/// HVORFOR DEN IKKE ER ÉN FÆLLES
///
/// Første udgave viste den samme dagsorden, uanset hvilken skabelon man stod
/// på. Det var forkert tænkt: et referat, en dokumentation og en brainstorm
/// har brug for vidt forskellige ting sagt højt undervejs.
///
/// Et referat skal bruge beslutninger og frister. En dokumentation skal
/// bruge forudsætninger og det, ingen kunne svare på. En brainstorm skal
/// bruge de idéer, der blev forkastet, og hvorfor — og det er præcis dét,
/// ingen siger højt, medmindre dagsordenen beder om det.
///
/// Dagsordenen ligger derfor ved siden af skabelonen som <c>navn.agenda.md</c>.
/// Den er en almindelig tekstfil: den kan rettes i hånden, og den kan slettes,
/// hvorefter standarden gælder igen.
///
/// HVORFOR STANDARDEN BLIVER
///
/// En skabelon uden sin egen dagsorden falder tilbage på den fælles. Den er
/// bedre end ingenting, og den er den, alle møder starter med — det, der
/// afgør, om udskriften overhovedet kan bruges, er de samme fem ting hver
/// gang: navne, formål, beslutninger sagt højt, det ingen ved, og hvem der
/// følger op.
/// </summary>
public static class Agendaer
{
    private static string Sti(string skabelonnavn) =>
        Path.Combine(PromptTemplate.Directory,
                     Path.GetFileNameWithoutExtension(PromptTemplate.Filnavn(skabelonnavn)) + ".agenda.md");

    /// <summary>Har skabelonen sin egen dagsorden?</summary>
    public static bool HarEgen(string skabelonnavn) => File.Exists(Sti(skabelonnavn));

    /// <summary>
    /// Skabelonens dagsorden, eller standarden hvis den ikke har sin egen.
    /// </summary>
    public static string Hent(string skabelonnavn, string standard)
    {
        var sti = Sti(skabelonnavn);
        if (!File.Exists(sti)) return standard;

        try { return File.ReadAllText(sti, System.Text.Encoding.UTF8); }
        catch (IOException) { return standard; }
    }

    public static void Gem(string skabelonnavn, string tekst)
    {
        Directory.CreateDirectory(PromptTemplate.Directory);
        File.WriteAllText(Sti(skabelonnavn), tekst, new System.Text.UTF8Encoding(false));
    }

    /// <summary>Fjerner skabelonens egen dagsorden, så standarden gælder igen.</summary>
    public static void Slet(string skabelonnavn)
    {
        try { File.Delete(Sti(skabelonnavn)); } catch (IOException) { }
    }
}
