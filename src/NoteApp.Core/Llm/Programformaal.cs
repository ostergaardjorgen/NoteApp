namespace NoteApp.Core.Llm;

/// <summary>
/// Vælger formen ud fra det program, der er fremme.
///
/// DET ER DÉT, DER GØR DIKTERING BEDRE END EN DIKTAFON. Det samme talte skal
/// blive til forskellig tekst: en mail har en indledning, en prompt har ingen
/// høflighed, og en opgave er én linje i bydeform. Appen ved, hvor du var i
/// gang, og skal bruge det.
///
/// LISTEN ER BEVIDST KORT OG BEVIDST FORSIGTIG. Rammer den forkert, får du en
/// mail, hvor du ville have en note — og det er værre end ingen gætteri, fordi
/// du skal opdage det og skrive om. Derfor svares der <c>null</c>, når
/// programmet ikke er genkendt, og så bruges dét, du selv har valgt.
/// </summary>
public static class Programformaal
{
    /// <summary>
    /// Programmer, hvor man skriver til et menneske.
    /// </summary>
    /// <remarks>
    /// Der matches på processens navn, ikke på vinduets titel. Titlen er
    /// dokumentets navn og skifter hele tiden; processen er den samme.
    /// </remarks>
    private static readonly string[] Post =
    {
        "outlook",       // klassisk Outlook
        "olk",           // det nye Outlook for Windows
        "thunderbird",
        "mailspring",
        "em client",
        "emclient",
        "postbox",
    };

    /// <summary>Programmer, hvor man skriver til en AI.</summary>
    private static readonly string[] Assistent =
    {
        "claude",
        "chatgpt",
        "copilot",
        "cursor",
        "perplexity",
    };

    /// <summary>Programmer, hvor en linje er en opgave.</summary>
    private static readonly string[] Opgaver =
    {
        "todoist",
        "ticktick",
        "microsoft.todos",
        "todo",
    };

    /// <summary>
    /// Websider tæller med, når de står i browserens titel.
    /// </summary>
    /// <remarks>
    /// Det meste af det, der ligner et program i dag, er en fane. Er browseren
    /// fremme, er processens navn «chrome» eller «msedge» og siger ingenting —
    /// titlen er det eneste, der gør.
    ///
    /// Kun titlen. Der kigges ikke på adressen, og der læses ikke i siden.
    /// </remarks>
    private static readonly (string Ord, Dikteringsformaal Formaal)[] Faner =
    {
        ("gmail", Dikteringsformaal.Mail),
        ("outlook", Dikteringsformaal.Mail),
        ("google tasks", Dikteringsformaal.Opgave),
        ("todoist", Dikteringsformaal.Opgave),
        ("trello", Dikteringsformaal.Opgave),
        ("jira", Dikteringsformaal.Opgave),
    };

    private static readonly string[] Browsere =
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "arc",
    };

    /// <summary>
    /// Formen, programmet lægger op til — eller <c>null</c>, hvis det ikke
    /// er til at sige.
    /// </summary>
    /// <param name="proces">Processens navn, uden «.exe».</param>
    /// <param name="titel">Vinduets titel. Må gerne være tom.</param>
    public static Dikteringsformaal? Gaet(string? proces, string? titel)
    {
        var p = (proces ?? "").Trim().ToLowerInvariant();
        var t = (titel ?? "").Trim().ToLowerInvariant();

        if (p.EndsWith(".exe", StringComparison.Ordinal)) p = p[..^4];

        if (p.Length == 0) return null;

        // Browseren foerst: er den fremme, siger processens navn ingenting, og
        // titlen er det eneste, der goer.
        if (Browsere.Any(b => p.Contains(b, StringComparison.Ordinal)))
        {
            foreach (var (ord, formaal) in Faner)
                if (t.Contains(ord, StringComparison.Ordinal))
                    return formaal;

            return null;
        }

        if (Post.Any(x => p.Contains(x, StringComparison.Ordinal))) return Dikteringsformaal.Mail;
        if (Opgaver.Any(x => p.Contains(x, StringComparison.Ordinal))) return Dikteringsformaal.Opgave;

        return null;
    }

    /// <summary>
    /// Formen, der skal bruges: programmets, hvis den kan gættes, ellers den
    /// valgte.
    /// </summary>
    public static Dikteringsformaal Vaelg(string? proces, string? titel,
                                          Dikteringsformaal standard, bool efterProgram)
        => efterProgram ? Gaet(proces, titel) ?? standard : standard;
}
