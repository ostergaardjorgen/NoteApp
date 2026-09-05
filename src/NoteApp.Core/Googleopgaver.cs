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
/// krydser man en Google-opgave af i HeyPia, sendes det op. Det er stadig kun
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
    /// kun virker den ene vej, er værre end ingen: man krydser af i HeyPia,
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

    /// <summary>
    /// Brugerens opgavelister — id og navn.
    /// </summary>
    /// <remarks>
    /// DEN FINDES, FOR AT MAN KAN VÆLGE, HVOR EN NY OPGAVE HAVNER.
    ///
    /// Google har mindst én liste og tit flere: «Mine opgaver» ved siden af én
    /// til studiet og én til huset. Faldt valget altid på den første, ville en
    /// opgave, man laver i HeyPia, lande et andet sted end den, man skriver på
    /// telefonen — og så er der to lister igen, hvilket er præcis det,
    /// integrationen findes for at undgå.
    ///
    /// DEN KASTER, HVIS GOOGLE IKKE SVARER. Den, der spørger, skal selv
    /// bestemme, hvad det betyder: for en dialog, der er ved at blive åbnet,
    /// er svaret at lade være med at vise valget — ikke at nægte at oprette
    /// opgaven. Se opgavevinduet.
    /// </remarks>
    public static async Task<List<(string Id, string Navn)>> ListerAsync(
        string opdateringsnoegle, CancellationToken ct = default) =>
        await Listerne(await Noegle(opdateringsnoegle, ct), ct);

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
    /// DEN ANDEN VEJ ER HELE POINTEN. Et flueben, der kun virker i HeyPia,
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

    /// <summary>
    /// Lægger en lokal opgave op i Google. Returnerer id og liste, den fik.
    /// </summary>
    /// <remarks>
    /// DEN SKAL VÆLGES, IKKE SKE AF SIG SELV. Alt, hvad appen finder i et
    /// møde, skal ikke ende i nogens telefon — de fleste opgaver fra et møde
    /// er noter til en selv. Derfor et hak på den enkelte opgave.
    ///
    /// LISTEN ER DEN FØRSTE, Google melder. Den er brugerens standardliste
    /// («Mine opgaver» hos de fleste), og det er dér, en opgave hører hjemme,
    /// når ingen har sagt andet. At spørge om listen ville være et spørgsmål
    /// mere i en dialog, der i forvejen har fem felter.
    ///
    /// FRISTEN SENDES SOM EN DATO UDEN KLOKKESLÆT. Google Tasks gemmer kun
    /// datoen og kaster tiden væk — sender man et klokkeslæt, ser det ud, som
    /// om det blev gemt, og det gjorde det ikke.
    /// </remarks>
    public static async Task<(string Id, string Liste)> OpretAsync(
        Opgave o, string opdateringsnoegle, CancellationToken ct = default)
    {
        var noegle = await Noegle(opdateringsnoegle, ct);

        var lister = await Listerne(noegle, ct);
        if (lister.Count == 0)
            throw new InvalidOperationException("Der er ingen opgavelister i kontoen.");

        var liste = o.FremmedListe.Length > 0
            ? o.FremmedListe
            : lister[0].Id;

        var krop = new Dictionary<string, object?>
        {
            ["title"] = o.Visningsnavn,
            ["status"] = o.Faerdig ? "completed" : "needsAction"
        };

        if (o.Tekst.Length > 0 && o.Tekst != o.Navn) krop["notes"] = o.Tekst;

        // Kun datoen. Google kaster klokkeslaettet vaek.
        if (o.Deadline is { } d)
            krop["due"] = d.UtcDateTime.Date.ToString("yyyy-MM-dd'T'00:00:00'Z'");

        var adresse = $"{Opgaver}/{Uri.EscapeDataString(liste)}/tasks";

        using var anmodning = new HttpRequestMessage(HttpMethod.Post, adresse)
        {
            Content = new StringContent(JsonSerializer.Serialize(krop), Encoding.UTF8, "application/json")
        };

        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Google svarede {(int)svar.StatusCode}. " + Kort(tekst));

        using var doku = JsonDocument.Parse(tekst);

        return (Tekst(doku.RootElement, "id"), liste);
    }

    /// <summary>
    /// Skriver en ændret opgave tilbage til Google.
    /// </summary>
    /// <remarks>
    /// TITEL, NOTE OG FRIST — ikke afkrydsningen. Den har sin egen vej i
    /// <see cref="SaetFaerdigAsync"/>, fordi den skal kunne sendes alene og
    /// med det samme: et flueben, der venter på, at man lukker en dialog, er
    /// et flueben, man ikke stoler på.
    ///
    /// EN FRIST, DER FJERNES, SKAL SENDES SOM NULL. Udelades feltet, lader
    /// Google den gamle stå — og så ser det ud, som om sletningen ikke virkede.
    /// </remarks>
    public static async Task OpdaterAsync(Opgave o, string opdateringsnoegle,
                                          CancellationToken ct = default)
    {
        if (o.FremmedId.Length == 0 || o.FremmedListe.Length == 0) return;

        var noegle = await Noegle(opdateringsnoegle, ct);

        var krop = new Dictionary<string, object?>
        {
            ["id"] = o.FremmedId,
            ["title"] = o.Visningsnavn,
            ["notes"] = o.Tekst.Length > 0 && o.Tekst != o.Navn ? o.Tekst : null,
            ["due"] = o.Deadline is { } d
                ? d.UtcDateTime.Date.ToString("yyyy-MM-dd'T'00:00:00'Z'")
                : null
        };

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

    /// <summary>
    /// Sletter en opgave hos Google.
    /// </summary>
    /// <remarks>
    /// Samme historie som aftalerne: afkrydsning og rettelser blev sendt
    /// videre, men en opgave kunne slet ikke slettes i appen — kun krydses af.
    /// Se <see cref="Googlekalender.SletAsync"/>.
    ///
    /// 404 og 410 tæller som succes. Den er væk, og det var dét, der blev
    /// bedt om.
    /// </remarks>
    public static async Task SletAsync(Opgave o, string opdateringsnoegle,
                                       CancellationToken ct = default)
    {
        if (o.FremmedId.Length == 0 || o.FremmedListe.Length == 0) return;

        var noegle = await Noegle(opdateringsnoegle, ct);

        var adresse = $"{Opgaver}/{Uri.EscapeDataString(o.FremmedListe)}" +
                      $"/tasks/{Uri.EscapeDataString(o.FremmedId)}";

        using var anmodning = new HttpRequestMessage(HttpMethod.Delete, adresse);
        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);

        if (svar.IsSuccessStatusCode) return;
        if (svar.StatusCode is System.Net.HttpStatusCode.NotFound
                            or System.Net.HttpStatusCode.Gone) return;

        throw new InvalidOperationException(
            $"Google svarede {(int)svar.StatusCode} paa sletningen.");
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
