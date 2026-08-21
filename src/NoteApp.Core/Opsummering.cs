using System.Text;
using System.Text.Json;
using NoteApp.Core.Llm;

namespace NoteApp.Core;

/// <summary>
/// En kort opsummering af et møde — gemt ved siden af optagelsen.
/// </summary>
public sealed record Opsummeringsdata
{
    public string Tekst { get; init; } = "";
    public DateTimeOffset Lavet { get; init; } = DateTimeOffset.Now;
    public string Model { get; init; } = "";

    /// <summary>
    /// Kontrolsum af den udskrift, opsummeringen blev lavet af.
    ///
    /// Er udskriften rettet siden, passer summen ikke — og så er
    /// opsummeringen lavet på noget andet, end der står på skærmen. Det skal
    /// kunne ses, ikke gættes.
    /// </summary>
    public string UdskriftSum { get; init; } = "";
}

/// <summary>
/// Opsummeringen af et møde.
///
/// HVORFOR DEN IKKE ER ET DOKUMENT
///
/// Appen kan i forvejen lave dokumenter ud fra skabeloner. Det er den store
/// vej: vælg skabelon, giv det et navn, få en Word-fil. Opsummeringen er den
/// lille: hvad handlede mødet om, i ti linjer, uden at der skal navngives
/// eller gemmes noget.
///
/// Den ligger derfor ved optagelsen og ikke i dokumentmappen. Man laver den
/// for at kunne huske mødet, ikke for at sende den videre.
///
/// DEN LAVES IKKE AF SIG SELV
///
/// At lave en opsummering sender udskriften til Mistral. Det må aldrig ske,
/// fordi man klikkede på en fane — kun fordi man bad om det. Knappen siger
/// hvad der sker, inden den gør det.
/// </summary>
public static class Opsummering
{
    public static string Sti(string mappe) => Path.Combine(mappe, "opsummering.json");

    public static Opsummeringsdata? Hent(string mappe)
    {
        var sti = Sti(mappe);
        if (!File.Exists(sti)) return null;

        try
        {
            return JsonSerializer.Deserialize<Opsummeringsdata>(
                File.ReadAllText(sti, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Gem(string mappe, Opsummeringsdata data)
    {
        Directory.CreateDirectory(mappe);

        File.WriteAllText(Sti(mappe),
            JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }),
            new UTF8Encoding(false));
    }

    public static void Slet(string mappe)
    {
        try { File.Delete(Sti(mappe)); } catch (IOException) { }
    }

    /// <summary>
    /// Opskriften, opsummeringen laves efter.
    ///
    /// Den er indbygget og ikke en skabelon, man kan rette. Skabelonerne er
    /// til dokumenter, hvor man selv bestemmer formen; det her er ét fast
    /// svar på ét fast spørgsmål — hvad handlede mødet om?
    ///
    /// Kravene er de samme som alle andre steder: skriv på dansk, find ikke
    /// på noget, og lad være med at fylde op, når der ikke er mere at sige.
    /// </summary>
    public static PromptTemplate Opskrift() => new()
    {
        Name = "Opsummering",
        Temperature = 0.2,
        MaxTokens = 1200,
        SystemPrompt =
            "Du laver en KORT opsummering af et møde ud fra en udskrift. Du skriver " +
            "ALTID på dansk, også når mødet blev holdt på et andet sprog.\n\n" +
            "FORMEN\n\n" +
            "Første afsnit: to til fire linjer om, hvad mødet handlede om, og hvad " +
            "der kom ud af det. Ingen overskrift.\n\n" +
            "Derefter mellem tre og syv punkter, hver på én til to linjer, med det " +
            "vigtigste: beslutninger, aftaler, tal og datoer der blev nævnt, og det " +
            "der stod åbent, da mødet sluttede. Skriv dem som punkter med bindestreg.\n\n" +
            "Til sidst én linje, der begynder med «Åbent:», hvis der er noget, ingen " +
            "kunne svare på. Er der ikke det, udelades linjen helt.\n\n" +
            "REGLERNE\n\n" +
            "Skriv ALDRIG et tal, et navn eller en dato, der ikke står i udskriften.\n\n" +
            "Fyld ikke op. Var mødet kort eller uden indhold, må opsummeringen være " +
            "på tre linjer. En lang opsummering af et tyndt møde er værre end en kort.\n\n" +
            "Er udskriften mærket med «Mig» og «Gæster», er det SIDER af mødet, ikke " +
            "navne. Brug de rigtige navne, hvis de bliver sagt undervejs — ellers " +
            "skriv «du» om den, der optog, og nævn de øvrige uden at finde på navne.\n\n" +
            "Svar med opsummeringen og intet andet. Ingen indledning, ingen " +
            "overskrift, ingen kodeblok omkring.",
        UserPrompt = ""
    };

    /// <summary>
    /// Opskriften, når opsummeringen laves PÅ MASKINEN.
    ///
    /// DEN ER KORTERE MED VILJE — OG DET ER IKKE EN NEDPRIORITERING.
    ///
    /// Målt 20-08-2026: den lokale model blev bedt om op til 1.200 tokens oven
    /// på en udskrift, der fylder 23.345. På et 6 GB-kort er der ikke plads til
    /// begge dele, og svaret stoppede midt i et ord — «- Cloudworks har planer
    /// på at st». En opsummering, der ender sådan, kan man hverken bruge eller
    /// stole på.
    ///
    /// Med fem linjer i stedet for ti er der plads, og så bliver den færdig.
    ///
    /// DEN MÅ IKKE SKRIVE TAL
    ///
    /// Den samme model skrev «allerede seks kunder i Danmark». De ord falder
    /// ikke ét sted i udskriften. Efterprøvningen fanger den slags bagefter,
    /// men det bedste er, at den ikke bliver skrevet: et tal i en opsummering
    /// på fem linjer bærer sjældent sin egen vægt, og et forkert et koster
    /// mere, end et rigtigt gavner.
    /// </summary>
    /// <summary>
    /// Opskriften til et WEBINAR, lavet på maskinen.
    ///
    /// ET WEBINAR SKAL IKKE SPØRGES OM DET SAMME SOM ET MØDE.
    ///
    /// Mødeopskriften beder om «det vigtigste, der blev sagt eller aftalt», og
    /// på et webinar er der ikke aftalt noget. Resultatet var en opsummering,
    /// der begyndte med «Mødet handlede om …» og ledte efter beslutninger, der
    /// ikke fandtes — set på skærmen 21-08-2026.
    ///
    /// Her er spørgsmålet et andet: hvad ville de lære mig, og hvad kan jeg
    /// tage med? Det er også dét, man vil vide om et webinar, man ikke selv
    /// nåede at se.
    ///
    /// Grænserne er de samme som ved møder og af samme grund: fem linjer, ingen
    /// tal. Se <see cref="LokalOpskrift"/> for målingen bag.
    /// </summary>
    public static PromptTemplate LokalWebinaropskrift() => new()
    {
        Name = "Opsummering af webinar (på maskinen)",
        Temperature = 0.2,
        MaxTokens = 320,
        SystemPrompt =
            "Du laver en MEGET KORT opsummering af et WEBINAR ud fra en udskrift. Du " +
            "skriver ALTID på dansk, også når webinaret blev holdt på et andet sprog.\n\n" +
            "ET WEBINAR ER IKKE ET MØDE\n\n" +
            "Der bliver ikke besluttet noget, og der bliver ikke aftalt noget. Nogen " +
            "underviser. Skriv aldrig «mødet» — skriv «webinaret».\n\n" +
            "FORMEN\n\n" +
            "To til tre linjer om, hvad webinaret handlede om, og hvem det er relevant " +
            "for. Derefter højst fire punkter med bindestreg, hver på én linje: det " +
            "vigtigste, man kan tage med sig.\n\n" +
            "Et punkt skal kunne stå alene og sige noget. «Om adgangsstyring» siger " +
            "ingenting; «Identitet er blevet den vigtigste kontrol, fordi netværkets " +
            "yderkant er brudt sammen» siger noget.\n\n" +
            "Fagudtryk beholdes, som de blev sagt — også engelske. Opfind ikke danske " +
            "ord for dem.\n\n" +
            "Hold dig under 120 ord i alt. Bliv færdig — en opsummering, der stopper " +
            "midt i en sætning, kan ikke bruges.\n\n" +
            "REGLERNE\n\n" +
            "Skriv ALDRIG et navn eller en dato, der ikke står i udskriften.\n\n" +
            "SKRIV IKKE TAL. Ingen antal, ingen beløb, ingen procenter, ingen årstal.\n\n" +
            "Er webinaret holdt af en leverandør, så skriv hvem der påstår noget om " +
            "eget produkt — «Elimity oplyser, at …». Vurder aldrig selv, om det passer.\n\n" +
            "Er du i tvivl om noget, så lad være med at skrive det.\n\n" +
            "Svar med opsummeringen og intet andet.",
        UserPrompt = ""
    };

    /// <summary>
    /// Den rigtige opskrift til den slags optagelse, det er.
    ///
    /// ÉT STED AT VÆLGE. Kaldes opskrifterne direkte rundt omkring, får et
    /// webinar før eller siden mødeopskriften igen — og det viser sig som en
    /// opsummering, der leder efter beslutninger, der ikke findes.
    /// </summary>
    public static PromptTemplate LokalOpskrift(MeetingType type) =>
        type == MeetingType.Webinar ? LokalWebinaropskrift() : LokalOpskrift();

    public static PromptTemplate LokalOpskrift() => new()
    {
        Name = "Opsummering (på maskinen)",
        Temperature = 0.2,
        MaxTokens = 320,
        SystemPrompt =
            "Du laver en MEGET KORT opsummering af et møde ud fra en udskrift. Du " +
            "skriver ALTID på dansk, også når mødet blev holdt på et andet sprog.\n\n" +
            "FORMEN\n\n" +
            "To til tre linjer om, hvad mødet handlede om. Derefter højst fire punkter " +
            "med bindestreg, hver på én linje: det vigtigste, der blev sagt eller aftalt.\n\n" +
            "Hold dig under 120 ord i alt. Bliv færdig — en opsummering, der stopper " +
            "midt i en sætning, kan ikke bruges.\n\n" +
            "REGLERNE\n\n" +
            "Skriv ALDRIG et navn eller en dato, der ikke står i udskriften.\n\n" +
            "SKRIV IKKE TAL. Ingen antal, ingen beløb, ingen procenter, ingen årstal. " +
            "Skriv «flere kunder» frem for et antal. Er et tal helt afgørende, så skriv " +
            "den sætning af, det stod i.\n\n" +
            "Er du i tvivl om noget, så lad være med at skrive det. Det, der står, skal " +
            "kunne genfindes i udskriften.\n\n" +
            "Svar med opsummeringen og intet andet.",
        UserPrompt = ""
    };
}
