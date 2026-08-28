namespace NoteApp.Core.Llm;

/// <summary>
/// Teksttyperne: hvad det talte skal blive til.
///
/// EN SKABELON, MAN KAN RETTE I — ligesom mødetyperne.
///
/// Instruktionen bag hver type er den, der sendes til sprogmodellen sammen med
/// den rå udskrift. Den er skrevet, så den passer de fleste, og den er ikke
/// hellig: skriver man altid mails, der begynder med «Hej» og slutter med sit
/// fornavn, er det ikke en indstilling — det er en skabelon, der skal rettes
/// én gang.
///
/// STANDARDEN STÅR I KODEN, DET RETTEDE I INDSTILLINGERNE. Er der ikke rettet
/// noget, bruges <see cref="Voxtral.Pudseprompt"/>, og en ny udgave af appen
/// kan forbedre den. Har man rettet, vinder ens egen — også når appen bliver
/// opdateret. Det modsatte ville betyde, at en opdatering stille kunne skrive
/// ens egne mails om.
/// </summary>
public static class Teksttyper
{
    /// <summary>Alle typerne i den rækkefølge, de vises.</summary>
    public static readonly IReadOnlyList<Dikteringsformaal> Alle = new[]
    {
        Dikteringsformaal.Note,
        Dikteringsformaal.Mail,
        Dikteringsformaal.Prompt,
        Dikteringsformaal.Opgave,
    };

    /// <summary>Nøglen, en egen instruktion gemmes under.</summary>
    public static string Noegle(Dikteringsformaal formaal) => formaal.ToString();

    /// <summary>
    /// Instruktionen, der faktisk bliver sendt: ens egen, hvis der er en.
    /// </summary>
    public static string Prompt(Dikteringsformaal formaal, IDictionary<string, string>? egne)
    {
        if (egne is not null
            && egne.TryGetValue(Noegle(formaal), out var egen)
            && !string.IsNullOrWhiteSpace(egen))
        {
            return egen.Trim();
        }

        return Voxtral.Pudseprompt(formaal);
    }

    /// <summary>Er der rettet i den her type?</summary>
    public static bool ErRettet(Dikteringsformaal formaal, IDictionary<string, string>? egne) =>
        egne is not null
        && egne.TryGetValue(Noegle(formaal), out var egen)
        && !string.IsNullOrWhiteSpace(egen)
        && egen.Trim() != Voxtral.Pudseprompt(formaal);

    /// <summary>
    /// Gemmer en egen instruktion — eller fjerner den, hvis den er tom eller
    /// den samme som standarden.
    /// </summary>
    /// <remarks>
    /// EN KOPI AF STANDARDEN GEMMES IKKE. Gjorde den det, ville typen være
    /// «rettet» for altid, og en forbedring i en ny udgave ville aldrig nå
    /// frem — uden at nogen havde valgt det.
    /// </remarks>
    public static void Saet(IDictionary<string, string> egne, Dikteringsformaal formaal, string? tekst)
    {
        var t = (tekst ?? "").Trim();
        var noegle = Noegle(formaal);

        if (t.Length == 0 || t == Voxtral.Pudseprompt(formaal)) egne.Remove(noegle);
        else egne[noegle] = t;
    }

    /// <summary>Det, brugeren skal kunne læse om typen. Kort, og på skærmen.</summary>
    public static string Noegletekst(Dikteringsformaal formaal) => formaal switch
    {
        Dikteringsformaal.Note => "teksttyper.note",
        Dikteringsformaal.Mail => "teksttyper.mail",
        Dikteringsformaal.Prompt => "teksttyper.prompt",
        Dikteringsformaal.Opgave => "teksttyper.opgave",
        _ => throw new ArgumentOutOfRangeException(nameof(formaal)),
    };
}
