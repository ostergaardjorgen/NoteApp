using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Henter opgaver fra Google Tasks — og krydser dem af igen.
///
/// HVORFOR DEN FINDES
///
/// Opgaver bliver til alle mulige steder: i et møde, i en mail, på vej hjem.
/// Er de spredt over to lister, bliver den ene holdt ved lige, og den anden
/// bliver ikke. Kan man skrive dem i Google på telefonen og se dem i
/// Cockpittet, er der ÉN liste.
///
/// DET ER EN UDVIDELSE AF, HVAD DER DELES, OG DET SKAL SIGES
///
/// Kalenderen læser noget, der allerede lå hos Google. Her går det begge veje:
/// krydser man en Google-opgave af i NoteApp, sendes det op. Det er stadig kun
/// opgaver — men det er en beslutning, brugeren skal træffe bevidst, og derfor
/// er det en integration for sig, man slår til hver for sig.
///
/// DET, DER ALDRIG SENDES: lyden, transkriptionerne, noterne og dokumenterne.
/// Opgaver, der er lavet ud af et møde, lægges IKKE op af sig selv — så ville
/// et referats indhold ende i skyen uden at nogen havde bedt om det.
///
/// SAMME KONTO, SAMME GODKENDELSE. Området lægges oven i det, kalenderen
/// bruger, så der kun er ét login. Slår man opgaver til, skal man godkende
/// igen — Google beder om det, når et område kommer til.
/// </summary>
public static class Googleopgaver
{
    public const string Id = "google-opgaver";

    /// <summary>
    /// Opgaver, og intet andet i kontoen.
    ///
    /// Der bedes om skriveadgang og ikke kun læsning, fordi et flueben, der
    /// kun virker den ene vej, er værre end ingen: man krydser af i NoteApp,
    /// og opgaven står stadig på telefonen.
    /// </summary>
    public const string Omraade = "https://www.googleapis.com/auth/tasks";

    private const string Lister = "https://tasks.googleapis.com/tasks/v1/users/@me/lists";
    private const string Opgaver = "https://tasks.googleapis.com/tasks/v1/lists";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Hvad der står i Google Tasks nu.
    ///
    /// KUN DE ÅBNE OG DE NETOP AFKRYDSEDE. Google gemmer afkrydsede opgaver i
    /// årevis; hentes de alle, drukner Cockpittet i ting, der er gjort. Der
    /// hentes derfor kun det, der ikke er færdigt, plus det, der blev krydset
    /// af inden for det seneste døgn — så en afkrydsning, man lige har lavet
    /// på telefonen, ikke ser ud som en opgave, der forsvandt.
    /// </summary>
    public static async Task<List<Opgave>> HentAsync(string opdateringsnoegle,
                                                     CancellationToken ct = default)
    {
        var noegle = await Noegle(opdateringsnoegle, ct);

        var ud = new List<Opgave>();

        foreach (var (listeId, listenavn) in await Listerne(noegle, ct))
        {
            var adresse = $"{Opgaver}/{Uri.EscapeDataString(listeId)}/tasks" +
                          "?showCompleted=true&showHidden=false&maxResults=100" +
                          $"&completedMin={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("o"))}";

            // To kald: de aabne, og de netop afkrydsede. Googles filter tager
            // ikke begge dele paa én gang - completedMin udelukker alt, der
            // ikke er faerdigt.
            ud.AddRange(await Hent(listeId, listenavn, $"{Opgaver}/{Uri.EscapeDataString(listeId)}/tasks?showCompleted=false&maxResults=100", noegle, ct));
            ud.AddRange(await Hent(listeId, listenavn, adresse, noegle, ct));
        }

        return ud;
    }

    private static async Task<List<Opgave>> Hent(string listeId, string listenavn,
                                                 string adresse, string noegle,
                                                 CancellationToken ct)
    {
        var ud = new List<Opgave>();

        using var anmodning = new HttpRequestMessage(HttpMethod.Get, adresse);
        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google svarede {(int)svar.StatusCode}. {Kort(tekst)}");

        using var doc = JsonDocument.Parse(tekst);
        if (!doc.RootElement.TryGetProperty("items", out var poster)) return ud;

        foreach (var p in poster.EnumerateArray())
        {
            var titel = Tekst(p, "title").Trim();

            // Google tillader en TOM opgave. Den har ingen mening her, og en
            // raekke uden tekst ligner en fejl i appen.
            if (titel.Length == 0) continue;

            var faerdig = Tekst(p, "status") == "completed";

            ud.Add(new Opgave
            {
                Navn = Opgave.Kort(titel),
                Tekst = Tekst(p, "notes") is { Length: > 0 } n ? titel + "\n\n" + n : titel,
                Faerdig = faerdig,
                Faerdiggjort = faerdig ? Tid(p, "completed") : null,
                Deadline = Tid(p, "due"),
                Oprettet = Tid(p, "updated") ?? DateTimeOffset.Now,
                Herkomst = Opgavekilde.Google,
                FremmedId = Tekst(p, "id"),
                FremmedListe = listeId,
                Moedetitel = listenavn
            });
        }

        return ud;
    }

    /// <summary>Brugerens opgavelister. Google har mindst én, «Mine opgaver».</summary>
    private static async Task<List<(string Id, string Navn)>> Listerne(string noegle,
                                                                       CancellationToken ct)
    {
        var ud = new List<(string, string)>();

        using var anmodning = new HttpRequestMessage(HttpMethod.Get, Lister + "?maxResults=50");
        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google svarede {(int)svar.StatusCode}. {Kort(tekst)}");

        using var doc = JsonDocument.Parse(tekst);
        if (!doc.RootElement.TryGetProperty("items", out var poster)) return ud;

        foreach (var p in poster.EnumerateArray())
        {
            var id = Tekst(p, "id");
            if (id.Length > 0) ud.Add((id, Tekst(p, "title")));
        }

        return ud;
    }

    /// <summary>
    /// Krydser en opgave af — eller tager afkrydsningen tilbage.
    ///
    /// DEN ANDEN VEJ ER HELE POINTEN. Et flueben, der kun virker i NoteApp,
    /// er værre end ingen: opgaven står stadig på telefonen, og så holder man
    /// op med at stole på nogen af listerne.
    /// </summary>
    public static async Task SaetFaerdigAsync(Opgave o, string opdateringsnoegle,
                                              CancellationToken ct = default)
    {
        if (o.Herkomst != Opgavekilde.Google) return;
        if (o.FremmedId.Length == 0 || o.FremmedListe.Length == 0) return;

        var noegle = await Noegle(opdateringsnoegle, ct);

        var krop = new Dictionary<string, object?>
        {
            ["id"] = o.FremmedId,
            ["status"] = o.Faerdig ? "completed" : "needsAction"
        };

        // «completed» SKAL ryddes, naar man tager afkrydsningen tilbage.
        // Google afviser ellers med 400: en opgave kan ikke baade vaere aaben
        // og have et faerdigtidspunkt.
        if (!o.Faerdig) krop["completed"] = null;

        var adresse = $"{Opgaver}/{Uri.EscapeDataString(o.FremmedListe)}" +
                      $"/tasks/{Uri.EscapeDataString(o.FremmedId)}";

        using var anmodning = new HttpRequestMessage(HttpMethod.Patch, adresse)
        {
            Content = new StringContent(JsonSerializer.Serialize(krop), Encoding.UTF8, "application/json")
        };

        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Google svarede {(int)svar.StatusCode}. " +
                Kort(await svar.Content.ReadAsStringAsync(ct)));
    }

    // ------------------------------------------------------------ hjaelpere

    private static async Task<string> Noegle(string opdateringsnoegle, CancellationToken ct) =>
        await Googlekalender.FriskNoegleTil(opdateringsnoegle, ct);

    private static string Tekst(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static DateTimeOffset? Tid(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v)
        && v.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParse(v.GetString(), out var t) ? t : null;

    private static string Kort(string s) => s.Length <= 200 ? s : s[..200] + " …";
}
