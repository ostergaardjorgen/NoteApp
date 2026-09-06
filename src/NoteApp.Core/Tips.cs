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

        // ============ TELEFONEN SOM OPTAGER ============
        //
        // Fem vinkler paa den samme kaede, fordi den ikke opdages ved at
        // bruge appen: den skal saettes op én gang, og foerst derefter er
        // den usynlig. Hver vinkel er et sted, man kan komme ind fra -
        // moedet i bilen, telefonen man har i forvejen, skytjenesten man
        // allerede betaler for, og spoergsmaalet om, hvem der faar adgang.
        new("du kan optage møder med din iPhone eller din Android-telefon. "
            + "Gem optagelsen i din HeyPia-mappe i skyen, så står den klar "
            + "her, næste gang du åbner appen.",
            "65-skytjenester"),

        new("mødet i bilen også kan blive til et referat. Optag med "
            + "telefonens egen optager, del optagelsen til iCloud, OneDrive "
            + "eller Google Drev — resten sker af sig selv.",
            "65-skytjenester"),

        new("HeyPia aldrig beder om adgang til din Google-, Microsoft- eller "
            + "Apple-konto. Appen ser en mappe på din egen disk; det er "
            + "cloud servicens eget program, der fylder den.",
            "65-skytjenester"),

        new("du kan bestemme, hvor optagelserne fra telefonen skal ligge. "
            + "Under Indstillinger → Filer retter du stien, blader dig frem "
            + "til mappen og gemmer — forslaget er kun et forslag.",
            "65-skytjenester"),

        new("en optagelse beholder sin egen dato. En samtale fra i tirsdags "
            + "sorterer som i tirsdags, også selv om telefonen først når at "
            + "sende den videre om torsdagen.",
            "65-skytjenester"),
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

        // Se den danske liste for hvorfor der er fem af dem.
        new("you can record meetings with your iPhone or your Android phone. "
            + "Save the recording to your HeyPia folder in the cloud, and it "
            + "is waiting here the next time you open the app.",
            "65-skytjenester"),

        new("the meeting in the car can become minutes too. Record with the "
            + "phone's own recorder, share it to iCloud, OneDrive or Google "
            + "Drive — the rest happens by itself.",
            "65-skytjenester"),

        new("HeyPia never asks for access to your Google, Microsoft or Apple "
            + "account. The app sees a folder on your own disk; the cloud "
            + "service's own program is what fills it.",
            "65-skytjenester"),

        new("you decide where recordings from the phone are kept. Under "
            + "Settings → Files you can edit the path, browse to the folder "
            + "and save — the suggestion is only a suggestion.",
            "65-skytjenester"),

        new("a recording keeps its own date. A conversation from Tuesday "
            + "sorts as Tuesday, even if the phone only gets around to "
            + "sending it on Thursday.",
            "65-skytjenester"),
    };

    /// <summary>Tipsene på det sprog, der vises nu.</summary>
    public static IReadOnlyList<Tip> Alle() =>
        Sprog.Kode.Equals("en", StringComparison.OrdinalIgnoreCase) ? Engelsk : Dansk;
}
