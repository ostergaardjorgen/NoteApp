using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Llm;

/// <summary>
/// Hvad den dikterede tekst skal bruges til.
///
/// FORMEN FØLGER OPGAVEN. Det samme talte indhold skal se forskelligt ud alt
/// efter, hvor det lander: en mail har en indledning, en prompt har ingen
/// høflighed, og en opgave er én linje i bydeform.
///
/// Det er dét, der skiller diktering fra transskription. Rå tale er ikke
/// brugbar tekst — den har fyldord, halve sætninger og ingen tegnsætning — og
/// en diktafon, der skriver det hele ned ordret, har ikke løst opgaven.
/// </summary>
public enum Dikteringsformaal
{
    /// <summary>Tæt på det talte. Kun ryddet op.</summary>
    Note,

    /// <summary>Hel tekst med indledning og afslutning.</summary>
    Mail,

    /// <summary>Instruktion til en AI. Ingen høflighed, ingen indpakning.</summary>
    Prompt,

    /// <summary>Kort, i bydeform, én linje.</summary>
    Opgave,
}

/// <summary>Det, der kom ud af en diktering.</summary>
/// <param name="Raa">Udskriften, som Voxtral gav den — med fyldord og det hele.</param>
/// <param name="Sprog">Sproget, modellen selv fandt. Kan være tomt.</param>
/// <param name="Sekunder">Klippets længde, som leverandøren gjorde den op.</param>
public sealed record Dikteringsresultat(string Raa, string Sprog, double Sekunder);

/// <summary>
/// Lyd til tekst hos Mistral.
///
/// GRÆNSEN FLYTTER SIG HER, OG DET SKAL SIGES HØJT
///
/// Indtil nu har appen kunnet sige, at lyden aldrig forlader maskinen. Møder
/// optages og skrives ud lokalt, og kun teksten er sendt videre.
///
/// Diktering sender LYD. Det er hele pointen med Voxtral, og det kan ikke
/// laves om ved at formulere sig anderledes. Derfor skelnes der fra nu af
/// mellem to slags lyd:
///
///   MØDER   optages og skrives ud på maskinen. Uændret.
///   DIKTAT  dit eget klip, dine egne sekunder, sendt til europæisk AI.
///
/// Forskellen er ikke en spidsfindighed. Et møde med fem mennesker, der ikke
/// er blevet spurgt, er noget andet end en sætning, du selv taler ind i en
/// mail. Men teksterne til brugeren skal sige det, FØR den her klasse tages i
/// brug — ikke bagefter. Se doc/diktering.md.
/// </summary>
public static class Voxtral
{
    /// <summary>
    /// Diktering, én optagelse ad gangen.
    ///
    /// Efterprøvet 28-08-2026 mod EU-endepunktet: HTTP 200, dansk genkendt
    /// uden at sproget blev oplyst.
    /// </summary>
    public const string Model = "voxtral-mini-latest";

    /// <summary>
    /// Streaming, mens der tales. IKKE brugt endnu.
    /// </summary>
    /// <remarks>
    /// Den kræver WebSocket. Et almindeligt kald bliver afvist med «only
    /// supports realtime transcription» — prøvet 28-08-2026. Streaming er en
    /// anden opgave end den første udgave, og den skal ikke blandes ind i
    /// den.
    /// </remarks>
    public const string StreamingModel = "voxtral-mini-realtime-latest";

    /// <summary>
    /// Modellen, der pudser den rå udskrift af.
    /// </summary>
    /// <remarks>
    /// Den lille med vilje. Et diktat er sekunder, og ventetiden mærkes
    /// direkte i hånden — en stor model ville skrive lidt pænere og føles
    /// meget langsommere. Opgaven er at rydde op, ikke at tænke.
    /// </remarks>
    public const string Pudsemodel = "mistral-small-latest";

    public static string Endepunkt => $"https://{SkyKatalog.TilladtVaert}/v1/audio/transcriptions";

    /// <summary>
    /// Instruktionen til tekstpudsningen. Ren funktion, så den kan prøves af
    /// uden at der sendes noget nogen steder.
    /// </summary>
    /// <remarks>
    /// «Tilføj intet» står i dem alle med vilje. En sprogmodel, der får lov,
    /// finder på en indledning, du ikke har sagt — og i en mail, du sender
    /// videre, er det din underskrift, der står under det.
    /// </remarks>
    public static string Pudseprompt(Dikteringsformaal formaal) => formaal switch
    {
        Dikteringsformaal.Note =>
            "Du er en dikteringsassistent. Ryd op i den følgende rå udskrift: "
            + "fjern fyldord og gentagelser, ret tegnsætning og åbenlyse fejl. "
            + "Behold ordvalget og tonen. Tilføj intet, og forklar intet.",

        Dikteringsformaal.Mail =>
            "Du er en dikteringsassistent. Skriv den følgende rå udskrift om til "
            + "en mail: en kort indledning, indholdet i hele sætninger, og en "
            + "afslutning. Behold afsenderens tone. Tilføj intet indhold, der "
            + "ikke blev sagt, og skriv ingen emnelinje.",

        Dikteringsformaal.Prompt =>
            "Du er en dikteringsassistent. Skriv den følgende rå udskrift om til "
            + "en instruktion til en AI-assistent. Ingen høflighed, ingen "
            + "indledning, ingen forklaring — kun opgaven, klart formuleret. "
            + "Tilføj intet, der ikke blev sagt.",

        Dikteringsformaal.Opgave =>
            "Du er en dikteringsassistent. Skriv den følgende rå udskrift om til "
            + "ÉN opgavelinje i bydeform, højst 80 tegn. Ingen forklaring, ingen "
            + "punktum i slutningen. Nævnes en dato eller et tidspunkt, skal det "
            + "stå med i linjen. Tilføj intet, der ikke blev sagt — en opgave, "
            + "der er fundet på, lander på listen og skal gøres.",

        _ => throw new ArgumentOutOfRangeException(nameof(formaal)),
    };
}

/// <summary>
/// Sender et lydklip til udskrift og får rå tekst tilbage.
/// </summary>
public sealed class Dikteringsklient
{
    private readonly string _noegle;
    private readonly HttpClient _klient;

    public Dikteringsklient(string noegle, HttpClient? klient = null)
    {
        _noegle = noegle;

        // Et diktat er sekunder, ikke minutter. Er der ikke svar paa to
        // minutter, er der noget galt — og saa er det bedre at sige det end at
        // lade brugeren staa og vente paa noget, der ikke kommer.
        _klient = klient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    /// <param name="lydfil">Klippet. Skal findes på disken.</param>
    /// <param name="fagord">
    /// Ord, modellen skal kende — navne, fagtermer, forkortelser.
    ///
    /// Appen har allerede sådan en liste: den ordliste, der bliver bedre af de
    /// rettelser, du selv laver. Den skal sendes med her frem for at blive
    /// bygget forfra, ellers lærer dikteringen ikke det, transskriptionen
    /// allerede har lært.
    /// </param>
    public async Task<Dikteringsresultat> SkrivUdAsync(
        string lydfil,
        IEnumerable<string>? fagord = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(lydfil))
            throw new FileNotFoundException("Der er ingen lydfil at skrive ud.", lydfil);

        // Samme kontrol som resten af sky-kaldene, paa vejen ud. En ny vej ud
        // af huset maa ikke kunne komme udenom den — se SkyKatalog.KraevEuropa.
        SkyKatalog.KraevEuropa(Voxtral.Endepunkt);

        using var indhold = new MultipartFormDataContent();
        var lyd = new ByteArrayContent(await File.ReadAllBytesAsync(lydfil, ct));
        lyd.Headers.ContentType = new MediaTypeHeaderValue(Medietype(lydfil));

        indhold.Add(lyd, "file", Path.GetFileName(lydfil));
        indhold.Add(new StringContent(Voxtral.Model), "model");

        var liste = fagord?.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
        if (liste is { Count: > 0 })
            indhold.Add(new StringContent(string.Join(", ", liste), new UTF8Encoding(false)), "prompt");

        using var anmodning = new HttpRequestMessage(HttpMethod.Post, Voxtral.Endepunkt)
        {
            Content = indhold,
        };
        anmodning.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _noegle);
        anmodning.Headers.UserAgent.ParseAdd("HeyPia");

        using var svar = await _klient.SendAsync(anmodning, ct);
        var krop = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Udskriften blev afvist ({(int)svar.StatusCode}).\n\n{Kort(krop)}");

        return Laes(krop);
    }

    /// <summary>
    /// Pudser den rå udskrift af, så den passer til det, den skal bruges til.
    ///
    /// DET ER HER DIKTERING SKILLER SIG FRA EN DIKTAFON. Rå tale har fyldord,
    /// halve sætninger og ingen tegnsætning. Skrives den bare ned ordret, er
    /// opgaven ikke løst — den er flyttet.
    /// </summary>
    /// <param name="raa">Udskriften fra <see cref="SkrivUdAsync"/>.</param>
    /// <param name="formaal">Hvor teksten skal hen. Bestemmer formen.</param>
    public async Task<string> PudsAsync(
        string raa,
        Dikteringsformaal formaal,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raa)) return "";

        SkyKatalog.KraevEuropa(SkyKatalog.Endpoint);

        var krop = JsonSerializer.Serialize(new
        {
            model = Voxtral.Pudsemodel,
            messages = new object[]
            {
                new { role = "system", content = Voxtral.Pudseprompt(formaal) },
                new { role = "user", content = raa },
            },

            // LAV TEMPERATUR MED VILJE. Der skal ryddes op i det, der blev
            // sagt — ikke skrives noget nyt. En model, der faar spillerum,
            // finder paa en indledning, du ikke har sagt.
            temperature = 0.2,
        });

        using var anmodning = new HttpRequestMessage(HttpMethod.Post, SkyKatalog.Endpoint)
        {
            Content = new StringContent(krop, new UTF8Encoding(false), "application/json"),
        };
        anmodning.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _noegle);
        anmodning.Headers.UserAgent.ParseAdd("HeyPia");

        using var svar = await _klient.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Tekstpudsningen blev afvist ({(int)svar.StatusCode}).\n\n{Kort(tekst)}");

        return LaesSvar(tekst);
    }

    /// <summary>Teksten ud af et chat-svar. Tom, hvis der ingen kom.</summary>
    public static string LaesSvar(string json)
    {
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.TryGetProperty("choices", out var valg)
               && valg.ValueKind == JsonValueKind.Array
               && valg.GetArrayLength() > 0
               && valg[0].TryGetProperty("message", out var besked)
               && besked.TryGetProperty("content", out var indhold)
            ? (indhold.GetString() ?? "").Trim()
            : "";
    }

    /// <summary>
    /// Hele vejen: lyd ind, brugbar tekst ud.
    /// </summary>
    /// <remarks>
    /// DEN RÅ TEKST FØLGER MED TILBAGE. Pudsningen kan gøre teksten forkert —
    /// en model, der rydder op, kan rydde noget væk, du mente. Uden den rå
    /// tekst ved siden af er der ingen vej tilbage til det, du faktisk sagde.
    /// </remarks>
    public async Task<(string Pudset, Dikteringsresultat Raa)> DikterAsync(
        string lydfil,
        Dikteringsformaal formaal = Dikteringsformaal.Note,
        IEnumerable<string>? fagord = null,
        CancellationToken ct = default)
    {
        var raa = await SkrivUdAsync(lydfil, fagord, ct);
        if (raa.Raa.Length == 0) return ("", raa);

        return (await PudsAsync(raa.Raa, formaal, ct), raa);
    }

    /// <summary>
    /// Piller svaret fra hinanden. Formen er efterprøvet mod det rigtige
    /// endepunkt 28-08-2026: <c>text</c>, <c>language</c>, <c>usage</c>.
    /// </summary>
    public static Dikteringsresultat Laes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var rod = doc.RootElement;

        var tekst = rod.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

        // «language» kan komme tilbage som null. Modellen finder sproget selv,
        // men den siger det ikke altid — og et tomt sprog maa ikke vaelte noget.
        var sprog = rod.TryGetProperty("language", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? ""
            : "";

        // FELTET HEDDER prompt_audio_seconds. Foerste udgave laeste
        // «audio_seconds» — et navn jeg havde gaettet — og proeven paa den
        // gaettede form bestod. Fejlen viste sig foerst, da kaeden blev koert
        // paa et rigtigt klip og skrev «0 sek lyd» om tolv sekunders tale.
        //
        // «audio_seconds» staar tilbage som andet valg. Skifter leverandoeren
        // navn igen, er det bedre at ramme det gamle end at vise nul.
        double sekunder = 0;
        if (rod.TryGetProperty("usage", out var u))
        {
            foreach (var navn in new[] { "prompt_audio_seconds", "audio_seconds" })
            {
                if (u.TryGetProperty(navn, out var a)
                    && a.ValueKind == JsonValueKind.Number
                    && a.TryGetDouble(out var tal))
                {
                    sekunder = tal;
                    break;
                }
            }
        }

        return new Dikteringsresultat(tekst.Trim(), sprog, sekunder);
    }

    /// <summary>
    /// Medietypen ud fra endelsen.
    /// </summary>
    /// <remarks>
    /// Sendes der application/octet-stream, afviser endepunktet filen uden at
    /// sige hvorfor. Appen optager selv i wav, men et klip kan komme udefra.
    /// </remarks>
    public static string Medietype(string sti) => Path.GetExtension(sti).ToLowerInvariant() switch
    {
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" or ".mp4" => "audio/mp4",
        ".ogg" or ".opus" => "audio/ogg",
        ".flac" => "audio/flac",
        ".webm" => "audio/webm",
        _ => "audio/wav",
    };

    private static string Kort(string s) => s.Length <= 400 ? s : s[..400] + "…";
}
