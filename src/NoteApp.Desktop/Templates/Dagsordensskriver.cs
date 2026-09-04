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
    /// Bygger dagsordenen ud fra de afsnit, dokumentet skal have.
    /// </summary>
    /// <remarks>
    /// DAGSORDENEN AENDREDE SIG IKKE, uanset hvad man klikkede fra. Den var en
    /// fast tekst, der kun blev skrevet om, hvis man bad Mistral om det — så
    /// kunne man tage «Beslutninger» fra i dokumentet og stadig sende en
    /// dagsorden, der brugte fem minutter på at samle beslutninger op.
    ///
    /// Nu bygges den her, af de afsnit der er hakket til. Kæden hænger sammen:
    /// dagsordenen får mødet til at sige det højt, transkriptionen fanger det,
    /// og dokumentet har et afsnit at skrive det i.
    ///
    /// TRE PUNKTER ER FASTE. Navnerunden, fordi talegenkendelsen ellers ingen
    /// navne har at genkende — det er det enkelte punkt, der afgør mest.
    /// Formål og rammer, fordi et møde uden dem ikke kan skrives ud til noget
    /// brugbart. Og afrundingen, hvor det, der blev aftalt, siges højt én gang
    /// til.
    ///
    /// «Deltagere» og «Resumé» får ikke deres eget punkt: navnerunden dækker
    /// det første, og det andet er noget, der skrives BAGEFTER — ikke noget,
    /// mødet skal bruge tid på.
    /// </remarks>
    public static string Byg(IReadOnlyList<string> afsnit)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("DAGSORDEN — [mødets navn]");
        sb.AppendLine("Dato og tid: [dato], kl. [fra]–[til]");
        sb.AppendLine("Sted / link: [fysisk lokale eller mødelink]");
        sb.AppendLine();
        sb.AppendLine("Mødet optages og skrives ud til tekst. Sig til, hvis du ikke ønsker det.");
        sb.AppendLine();
        sb.AppendLine();

        var nr = 1;

        sb.AppendLine($"{nr++}. NAVNERUNDE (2 min)");
        sb.AppendLine("   Alle siger navn, rolle og organisation — også dem, der kender hinanden.");
        sb.AppendLine("   Én sætning hver: \"Jeg hedder [navn], jeg er [rolle] hos [firma].\"");
        sb.AppendLine();
        sb.AppendLine("   Det tager to minutter og er det, der afgør, om referatet kan skrive,");
        sb.AppendLine("   hvem der sagde hvad.");
        sb.AppendLine();
        sb.AppendLine();

        sb.AppendLine($"{nr++}. FORMÅL OG RAMMER (3 min)");
        sb.AppendLine("   Mødeleder siger højt:");
        sb.AppendLine("   - hvad vi skal opnå i dag");
        sb.AppendLine("   - hvad der allerede er besluttet, og som vi IKKE tager op igen");
        sb.AppendLine("   - hvad der er åbent");
        sb.AppendLine();
        sb.AppendLine();

        foreach (var navn in afsnit.Where(Egetpunkt))
        {
            sb.AppendLine($"{nr++}. {navn.ToUpperInvariant()}");

            foreach (var linje in Punkttekst(navn)) sb.AppendLine("   " + linje);

            sb.AppendLine();
            sb.AppendLine();
        }

        sb.AppendLine($"{nr}. AFRUNDING (3 min)");
        sb.AppendLine("   Mødeleder gentager højt, hvad der blev aftalt — også når det føles");
        sb.AppendLine("   overflødigt. Det, ingen siger højt, findes ikke i udskriften.");
        sb.AppendLine();
        sb.AppendLine("   Aftal, hvornår I mødes igen, og hvem der samler op.");

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>Skal afsnittet have sit eget punkt på dagsordenen?</summary>
    private static bool Egetpunkt(string navn) =>
        !navn.Equals("Deltagere", StringComparison.CurrentCultureIgnoreCase)
        && !navn.StartsWith("Resum", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Hvad mødet skal gøre for at afsnittet kan skrives.
    /// </summary>
    /// <remarks>
    /// De fire kendte navne beholder den ordlyd, standarddagsordenen havde —
    /// den er skrevet og prøvet af, og den skal ikke gå tabt, fordi teksten nu
    /// bygges. Alle andre afsnit, også dem brugeren selv finder på, får den
    /// generelle linje: sig det højt, ellers står afsnittet tomt.
    /// </remarks>
    private static string[] Punkttekst(string navn) => navn.ToLowerInvariant() switch
    {
        "gennemgang" => new[]
        {
            "Ét punkt ad gangen. Sig navnet på det, I taler om, før I går i gang.",
            "Nævn tal, systemnavne og versioner højt — også dem, alle kender."
        },

        "beslutninger" => new[]
        {
            "Mødeleder gentager hver beslutning højt, én ad gangen:",
            "\"Vi har besluttet, at [beslutning], fordi [begrundelse].\""
        },

        "opgaver" => new[]
        {
            "Hver opgave siges højt med ejer og tid:",
            "\"[Navn] gør [opgave] inden [tidspunkt].\""
        },

        "åbne spørgsmål" => new[]
        {
            "Gå rundt om bordet: hvad kunne vi ikke svare på i dag?",
            "For hvert punkt: hvad mangler vi at vide, hvem undersøger det, og hvornår."
        },

        _ => new[]
        {
            "Tal om det, og sig det højt — dokumentet får et afsnit om det, og",
            "det kan kun indeholde det, nogen sagde."
        }
    };

    // HER LAA SkrivAsync OG Standard() - Mistral-kaldet, der skrev en
    // dagsorden om til een moedetype, og den faste standardtekst i agenda.txt.
    //
    // Begge hoerte til den GEMTE dagsorden. Den findes ikke mere: dagsordenen
    // bygges af afsnittene, hver gang fanen tegnes - saa der er ikke to slags
    // at skifte imellem, ingen gemt tilstand man ikke kan se, og ingen knap til
    // at komme ud af den.
}
