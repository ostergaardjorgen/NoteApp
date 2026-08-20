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
}
