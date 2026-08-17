using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Llm;

/// <summary>
/// En sprogmodel, der kører hos en leverandør frem for på maskinen.
///
/// HVORFOR DEN LIGGER I SIN EGEN KLASSE
///
/// <see cref="LlmRunner"/> lover, at der ikke findes et netværkskald i den.
/// Det løfte er hele grundlaget for «optagelse og udskrift forlader aldrig
/// maskinen», og det skal kunne efterprøves ved at læse én fil.
///
/// Derfor bliver netværket her, i en klasse man skal vælge aktivt. Grænsen er
/// fysisk frem for en regel, man skal huske — nøjagtig som datamappen ligger
/// uden for git-checkouten.
/// </summary>
public sealed record SkyModel(
    string Id,
    string ApiId,
    string Navn,
    string Leverandoer,
    string Hjemland,
    decimal PrisIndPrMTok,
    decimal PrisUdPrMTok,
    string Beskrivelse)
{
    /// <summary>
    /// Hvad én kørsel kostede, i dollar. Regnet på de tokens, leverandøren
    /// selv oplyser — ikke på et skøn over teksten.
    ///
    /// EU-tillægget på 10 % er ganget på. Uden det ville prisen se rigtig ud
    /// og være for lav, og et budget lagt på tallet ville skride.
    /// </summary>
    public decimal Pris(int tokensInd, int tokensUd) =>
        (tokensInd / 1_000_000m * PrisIndPrMTok + tokensUd / 1_000_000m * PrisUdPrMTok)
        * SkyKatalog.EuTillaeg;
}

/// <summary>
/// De sky-modeller, appen kan bruge.
///
/// Listen er BEVIDST KORT. Den indeholder europæiske leverandører, fordi det
/// er det, produktet lover: optagelse og udskrift bliver på maskinen,
/// bearbejdningen sker i Europa. En amerikansk model i denne liste ville
/// gøre løftet usandt, uanset hvor pænt den var markeret.
///
/// PRISERNE ER SLÅET OP 16-08-2026 og skal kontrolleres, før de vises et sted,
/// nogen handler på. De står her for at kunne regne en kørsel om til kroner i
/// målingerne, ikke som en prisliste til brugeren.
/// </summary>
public static class SkyKatalog
{
    /// <summary>
    /// EU-ENDEPUNKTET. IKKE api.mistral.ai.
    ///
    /// Det her er hele forskellen på, om budskabet holder. Mistral har tre
    /// endepunkter: api.eu.mistral.ai, api.us.mistral.ai og api.mistral.ai.
    /// Det sidste er det GLOBALE, og om det skriver Mistral ordret, at de
    /// «ikke forpligter sig på en bestemt geografi for anmodninger sendt til
    /// dette endepunkt».
    ///
    /// Her stod api.mistral.ai indtil 17-08-2026, fordi det er det, der står i
    /// alle kodeeksempler. Det virkede fint, målingerne var gode, og
    /// bearbejdningen kunne være foregået hvor som helst. En påstand om
    /// europæisk bearbejdning ville have været usand — uden at noget så
    /// forkert ud.
    ///
    /// EU-endepunktet koster 10 % mere. Det er prisen for at kunne sige det,
    /// vi siger, og den er 2 øre pr. referat.
    /// </summary>
    public const string Endpoint = "https://api.eu.mistral.ai/v1/chat/completions";
    public const string ModelListeEndpoint = "https://api.eu.mistral.ai/v1/models";

    /// <summary>
    /// Tillægget for at binde bearbejdningen til Europa. Ganges på alle
    /// tokenpriser — se <see cref="SkyModel.Pris"/>.
    /// </summary>
    public const decimal EuTillaeg = 1.1m;

    /// <summary>Den ENESTE vært, appen må sende mødetekst til.</summary>
    public const string TilladtVaert = "api.eu.mistral.ai";

    /// <summary>
    /// Kaster, hvis en adresse ikke er det europæiske endepunkt.
    ///
    /// HVORFOR DET IKKE ER NOK, AT KONSTANTEN OVENFOR ER RIGTIG
    ///
    /// Den var forkert indtil 17-08-2026. Der stod api.mistral.ai — det, der
    /// står i alle kodeeksempler — og alt virkede: kaldene gik igennem,
    /// målingerne var gode, og bearbejdningen kunne være foregået hvor som
    /// helst. Ingen test slog fejl, fordi der ikke var noget at slå fejl på.
    ///
    /// En konstant, der er rigtig i dag, er ikke det samme som en, der bliver
    /// ved med at være det. Den her kontrol gør et skift til det globale eller
    /// det amerikanske endepunkt til noget, der stopper med en fejl frem for
    /// noget, der bare sker.
    ///
    /// Skal appen en dag kunne bruge en anden leverandør, er det ikke DEN her
    /// linje, der skal rettes — det er et bevidst valg om en ny leverandør,
    /// og så skal både den, priserne og teksten til brugeren følge med.
    /// </summary>
    public static void KraevEuropa(string url)
    {
        var vaert = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : "";

        if (!vaert.Equals(TilladtVaert, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Afvist: appen sender kun til det europæiske endepunkt.\n\n" +
                $"Forsøgt: {(vaert.Length > 0 ? vaert : url)}\n" +
                $"Tilladt: {TilladtVaert}\n\n" +
                "Mistrals globale endepunkt (api.mistral.ai) forpligter sig ikke på nogen " +
                "geografi, og det amerikanske ligger i USA. Ingen af dem må bruges — " +
                "løftet om europæisk bearbejdning står og falder med det.");
    }

    public static readonly IReadOnlyList<SkyModel> Kendte = new[]
    {
        new SkyModel(
            "mistral-medium", "mistral-medium-latest", "Mistral Medium 3.5",
            "Mistral AI", "Frankrig", 1.50m, 7.50m,
            "Mellemklassen. Den, der skal måle sig med et rigtigt referat."),

        new SkyModel(
            "mistral-large", "mistral-large-latest", "Mistral Large 3",
            "Mistral AI", "Frankrig", 0.50m, 1.50m,
            "Billigere END Medium og alligevel større. Prisen skal efterprøves, ikke antages.")
    };

    public static SkyModel? Find(string id) =>
        Kendte.FirstOrDefault(m =>
            m.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
            m.ApiId.Equals(id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Hvor nøglen kommer fra.
///
/// DEN LIGGER ALDRIG I KODEN OG ALDRIG I REPOET. To steder læses der fra, i
/// denne rækkefølge:
///
///   1. Miljøvariablen MISTRAL_API_KEY — til kørsler fra kommandolinjen.
///   2. Filen sky-noegle.txt i datamappen, som ligger uden for git-checkouten.
///
/// Brugeren taster den selv. Der bliver aldrig lagt en nøgle ind i en
/// udgivelse: så ville alle, der installerer appen, dele den samme konto, og
/// den første, der misbruger den, lukker den for alle andre.
/// </summary>
public static class SkyNoegle
{
    public const string Miljoevariabel = "MISTRAL_API_KEY";

    public static string Fil => Path.Combine(UserDataPaths.Root, "sky-noegle.txt");

    public static string? Hent()
    {
        var fraMiljoe = Environment.GetEnvironmentVariable(Miljoevariabel);
        if (!string.IsNullOrWhiteSpace(fraMiljoe)) return fraMiljoe.Trim();

        try
        {
            if (File.Exists(Fil))
            {
                var fraFil = File.ReadAllText(Fil, Encoding.UTF8).Trim();
                if (fraFil.Length > 0) return fraFil;
            }
        }
        catch (IOException)
        {
            // En ulaeselig noeglefil maa ikke vaelte noget. Kalderen faar null
            // og siger det samme, som hvis der slet ingen noegle var.
        }

        return null;
    }

    /// <summary>
    /// Gemmer nøglen i datamappen.
    ///
    /// Filen ligger uden for git-checkouten pr. konstruktion — se
    /// <see cref="UserDataPaths"/>. Der findes ingen .gitignore-fejl, der kan
    /// lække den, fordi den ikke er inde i arbejdstræet til at begynde med.
    /// </summary>
    public static void Gem(string noegle)
    {
        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Fil, noegle.Trim(), new UTF8Encoding(false));
    }

    public static void Slet()
    {
        try { if (File.Exists(Fil)) File.Delete(Fil); } catch (IOException) { }
    }

    /// <summary>Beskeden, når der ikke er nogen nøgle. Skal kunne handles på.</summary>
    public static string Vejledning =>
        $"Der er ingen API-nøgle til Mistral.\n\n" +
        $"Sæt den som miljøvariabel:\n" +
        $"    setx {Miljoevariabel} din-noegle-her\n\n" +
        $"eller læg den i filen:\n" +
        $"    {Fil}\n\n" +
        $"Nøglen hentes på console.mistral.ai. Den skal være din egen — der " +
        $"følger ingen med appen.";
}

/// <summary>
/// Resultatet af en sky-kørsel. Samme form som en lokal kørsel, plus det, der
/// kun findes i skyen: hvad den kostede.
/// </summary>
public sealed record SkyResultat(
    string Tekst,
    TimeSpan Forloebet,
    SkyModel Model,
    int TokensInd,
    int TokensUd)
{
    public decimal PrisUsd => Model.Pris(TokensInd, TokensUd);

    public double TokensPrSekund => Forloebet.TotalSeconds <= 0 ? 0 : TokensUd / Forloebet.TotalSeconds;

    /// <summary>Den samme form som en lokal kørsel, så målingerne kan stilles op ved siden af hinanden.</summary>
    public LlmResult SomLlmResult() => new(Tekst, Forloebet, Model.Navn, TokensInd, TokensUd);
}

/// <summary>
/// Kører en prompt hos Mistral.
///
/// HVORFOR DER IKKE DELES OP I STYKKER
///
/// <see cref="Referatbygger"/> deler mødet i blokke. Det er ikke en god idé i
/// sig selv — det er en nødløsning på, at en 8B-model plus et 61-minutters
/// møde ikke er plads til på et 6 GB-kort. Opdelingen kostede os deltagerne:
/// hver blok blev læst i blinde, og den ene deltagers livsforløb blev tillagt
/// den anden.
///
/// En sky-model har hundredtusinder af tokens kontekst. Hele mødet kan komme
/// ind på én gang, og så forsvinder både opdelingen og den fejl, den
/// medførte. Det er billigere OG bedre — der er ingen afvejning at træffe.
/// </summary>
public sealed class SkyRunner
{
    private readonly string _noegle;
    private readonly HttpClient _klient;

    public SkyRunner(string noegle, HttpClient? klient = null)
    {
        _noegle = noegle;

        // Et moedereferat kan tage over et minut at skrive. Standarden paa 100
        // sekunder klipper den over midt i saetningen, og fejlen ligner en
        // netvaerksfejl frem for det, den er.
        _klient = klient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Spørger leverandøren, hvilke modeller nøglen faktisk kan bruge.
    ///
    /// Model-id'er som «mistral-medium-latest» står ikke i den offentlige
    /// dokumentation, og et gættet id fejler med en 400'er, der ikke siger
    /// hvorfor. Derfor kan listen hentes, så et forkert id kan opdages som
    /// dét frem for som «der skete en fejl».
    /// </summary>
    public async Task<IReadOnlyList<string>> ModellerAsync(CancellationToken ct = default)
    {
        using var svar = await SendAsync(HttpMethod.Get, SkyKatalog.ModelListeEndpoint, null, ct);
        var krop = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode) throw Fejl(svar.StatusCode, krop);

        using var doc = JsonDocument.Parse(krop);

        return doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
                  .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "")
                  .Where(s => s.Length > 0)
                  .OrderBy(s => s, StringComparer.Ordinal)
                  .ToList()
            : new List<string>();
    }

    public async Task<SkyResultat> KoerAsync(
        SkyModel model,
        PromptTemplate skabelon,
        string brugerPrompt,
        IProgress<LlmProgress>? fremdrift = null,
        CancellationToken ct = default)
    {
        fremdrift?.Report(new LlmProgress($"Sender til {model.Navn} ({model.Hjemland}) …"));

        var krop = JsonSerializer.Serialize(new
        {
            model = model.ApiId,
            messages = new object[]
            {
                new { role = "system", content = skabelon.SystemPrompt },
                new { role = "user", content = brugerPrompt }
            },
            temperature = skabelon.Temperature,
            max_tokens = skabelon.MaxTokens
        });

        var ur = Stopwatch.StartNew();
        using var svar = await SendAsync(HttpMethod.Post, SkyKatalog.Endpoint, krop, ct);
        var svarKrop = await svar.Content.ReadAsStringAsync(ct);
        ur.Stop();

        if (!svar.IsSuccessStatusCode) throw Fejl(svar.StatusCode, svarKrop);

        using var doc = JsonDocument.Parse(svarKrop);
        var rod = doc.RootElement;

        var tekst = rod.TryGetProperty("choices", out var valg) && valg.GetArrayLength() > 0
            && valg[0].TryGetProperty("message", out var besked)
            && besked.TryGetProperty("content", out var indhold)
                ? indhold.GetString() ?? ""
                : "";

        if (tekst.Trim().Length == 0)
            throw new InvalidOperationException(
                $"{model.Navn} svarede uden indhold. Hele svaret:\n{Forkort(svarKrop, 800)}");

        // Tokentallene kommer fra leverandoeren og er dem, der faktureres. De
        // maa ikke skoennes: hele pointen med maalingen er at kunne sige, hvad
        // en koersel KOSTEDE, ikke hvad den cirka kostede.
        var (ind, ud) = LaesTokens(rod);

        fremdrift?.Report(new LlmProgress("Færdig", Percent: 100));

        return new SkyResultat(tekst.Trim(), ur.Elapsed, model, ind, ud);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod metode, string url, string? json, CancellationToken ct)
    {
        // Kontrollen ligger HER, paa det ene sted alt gaar igennem, frem for
        // ved kaldstederne. Et nyt kaldsted kan glemme en kontrol; det kan
        // ikke undgaa den her.
        SkyKatalog.KraevEuropa(url);

        using var anmodning = new HttpRequestMessage(metode, url);
        anmodning.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _noegle);
        anmodning.Headers.UserAgent.ParseAdd("NoteApp");

        if (json is not null)
            anmodning.Content = new StringContent(json, new UTF8Encoding(false), "application/json");

        return await _klient.SendAsync(anmodning, ct);
    }

    private static (int Ind, int Ud) LaesTokens(JsonElement rod)
    {
        if (!rod.TryGetProperty("usage", out var brug)) return (0, 0);

        int Tal(string navn) =>
            brug.TryGetProperty(navn, out var v) && v.TryGetInt32(out var n) ? n : 0;

        return (Tal("prompt_tokens"), Tal("completion_tokens"));
    }

    /// <summary>
    /// En fejl, man kan handle på.
    ///
    /// «401» siger ingenting til den, der har skrevet nøglen forkert. Derfor
    /// oversættes de statuskoder, der faktisk rammer folk, til det, der er galt
    /// — og resten får leverandørens eget svar med, så det kan slås op.
    /// </summary>
    private static InvalidOperationException Fejl(System.Net.HttpStatusCode kode, string krop) =>
        new((int)kode switch
        {
            401 => "Mistral afviste nøglen. Kontrollér den på console.mistral.ai.",
            402 => "Der er ikke dækning på Mistral-kontoen.",
            422 => $"Mistral afviste anmodningen — som regel et ukendt model-id.\n" +
                   $"Kør «noteapp sky modeller» for at se, hvilke id'er nøglen kan bruge.\n\n{Forkort(krop, 500)}",
            429 => "Der er sendt for mange anmodninger til Mistral. Vent lidt og prøv igen.",
            _ => $"Mistral svarede {(int)kode}.\n\n{Forkort(krop, 800)}"
        });

    private static string Forkort(string tekst, int maks) =>
        tekst.Length <= maks ? tekst : tekst[..maks] + " …";
}
