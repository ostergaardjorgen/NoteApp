namespace NoteApp.Core;

/// <summary>Ét tip — «Vidste du at …».</summary>
/// <param name="Tekst">Selve sætningen. Uden «Vidste du at», som står i båndet.</param>
/// <param name="Afsnit">Id på det hjælpeafsnit, «Læs mere» skal åbne.</param>
public sealed record Tip(string Tekst, string Afsnit);

/// <summary>
/// Tipsene i Cockpittet.
///
/// HVORFOR DE FINDES
///
/// Appen kan en del, man ikke opdager ved at bruge den: at et webinar ikke
/// optager din mikrofon, at en diktering kan blive til en opgave, at
/// opgavelisten kan trækkes i rækkefølge. Det står i hjælpen, og hjælpen
/// læser man den dag, noget driller — altså ikke den dag, man kunne have
/// haft glæde af det.
///
/// Båndet står, hvor der alligevel er tom plads: under søgefeltet, når man
/// ikke søger. Det koster ingen plads fra noget andet.
///
/// HVER SÆTNING PEGER PÅ ET AFSNIT. Et tip, man ikke kan læse videre om, er
/// en påstand. «Læs mere» åbner hjælpen dér, hvor det står ordentligt.
///
/// DE SKRIVES HER OG IKKE I SPROGFILERNE. En sprogfil er en ordbog af
/// stumper; det her er sætninger, der hører sammen med et afsnit-id, og de to
/// skal kunne rettes samtidig. Oversættelsen ligger i den engelske liste
/// nedenfor.
/// </summary>
public static class Tips
{
    private static readonly Tip[] Dansk =
    {
        new("du kan diktere midt i et webinar. Et webinar optager højttaleren "
            + "og ikke din mikrofon, så det, du siger, forstyrrer ingenting.",
            "20-optagelse"),

        new("en diktering kan blive til en aftale eller en opgave med ét klik. "
            + "Knapperne står i boblen, så snart teksten er klar — du behøver "
            + "ikke forlade det program, du sidder i.",
            "10-kom-i-gang"),

        new("du selv kan bestemme rækkefølgen på dine opgaver. Træk en opgave "
            + "op eller ned, og fra da af er det din rækkefølge, der gælder — "
            + "uanset frist og prioritet.",
            "50-kalender-og-opgaver"),

        new("du kan rette et ord dér, hvor det står forkert. Klik på ordet i "
            + "udskriften, skriv det rigtige, og appen husker det til næste gang.",
            "30-transskription"),

        new("hvert møde har sin egen historik. Fanen «Historik» på optagelsen "
            + "viser, hvad den hed før, hvornår den blev skrevet ud, og hvilke "
            + "dokumenter der kom ud af den.",
            "20-optagelse"),

        new("lyden aldrig forlader din maskine. Optagelsen skrives ud her, og "
            + "kun teksten sendes videre — og kun når du beder om et dokument.",
            "80-data"),

        new("du kan slippe en lydfil hvor som helst i træet. Den lander i den "
            + "mappe, du slipper den på, og skrives ud som alt andet.",
            "60-filer-udefra"),

        new("mødetypen bestemmer, hvad der står i dokumentet. Under «Mødetyper» "
            + "sætter du hak ved de afsnit, du vil have med — og dagsordenen "
            + "bygger sig selv efter dem.",
            "40-dokumenter"),

        new("søgningen læser alt på én gang: transskriptioner, dine noter og "
            + "de dokumenter, appen har lavet. Flere ord betyder, at de alle "
            + "skal stå tæt på hinanden.",
            "90-sprog-og-soegning"),

        new("kalenderen virker uden nogen konto nogen steder. Google er noget, "
            + "du kan lægge oveni — ikke noget, appen står og falder med.",
            "50-kalender-og-opgaver"),
    };

    private static readonly Tip[] Engelsk =
    {
        new("you can dictate in the middle of a webinar. A webinar records the "
            + "speaker and not your microphone, so what you say disturbs nothing.",
            "20-optagelse"),

        new("a dictation can become an appointment or a task in one click. The "
            + "buttons appear in the bubble as soon as the text is ready — you "
            + "never have to leave the app you are in.",
            "10-kom-i-gang"),

        new("you can decide the order of your tasks yourself. Drag a task up or "
            + "down, and from then on your order is the one that counts — "
            + "whatever the deadline and priority say.",
            "50-kalender-og-opgaver"),

        new("you can fix a word right where it went wrong. Click the word in the "
            + "transcript, type the right one, and the app remembers it next time.",
            "30-transskription"),

        new("every meeting has its own history. The «History» tab on a recording "
            + "shows what it used to be called, when it was transcribed, and "
            + "which documents came out of it.",
            "20-optagelse"),

        new("the audio never leaves your machine. The recording is transcribed "
            + "here, and only the text goes further — and only when you ask for "
            + "a document.",
            "80-data"),

        new("you can drop an audio file anywhere in the tree. It lands in the "
            + "folder you drop it on and is transcribed like everything else.",
            "60-filer-udefra"),

        new("the meeting type decides what goes into the document. Under «Meeting "
            + "types» you tick the sections you want — and the agenda builds "
            + "itself from them.",
            "40-dokumenter"),

        new("search reads everything at once: transcripts, your notes and the "
            + "documents the app has made. More words means they all have to "
            + "stand close together.",
            "90-sprog-og-soegning"),

        new("the calendar works without an account anywhere. Google is something "
            + "you can add on top — not something the app stands or falls with.",
            "50-kalender-og-opgaver"),
    };

    /// <summary>Tipsene på det sprog, der vises nu.</summary>
    public static IReadOnlyList<Tip> Alle() =>
        Sprog.Kode.Equals("en", StringComparison.OrdinalIgnoreCase) ? Engelsk : Dansk;
}
