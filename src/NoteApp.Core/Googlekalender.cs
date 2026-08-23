using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Henter aftaler fra Google Kalender.
///
/// HVAD DEN GØR, OG HVAD DEN ALDRIG GØR
///
/// Den LÆSER kalenderen. Der er bedt om ét område — `calendar.readonly` — og
/// det er hele adgangen. Appen kan ikke oprette, ændre eller slette en aftale
/// hos Google, og den sender ingenting op. Områdenavnet står i koden og kan
/// efterprøves; det er ikke et løfte, det er en grænse, Google håndhæver.
///
/// GODKENDELSEN FOREGÅR I BRUGERENS EGEN BROWSER
///
/// Appen ser aldrig en adgangskode. Der åbnes en side hos Google, man logger
/// ind dér, og Google sender en engangskode tilbage til en port på maskinen.
/// Det er den vej, Google selv foreskriver for et program, der kører på en
/// pc — «loopback», hvor svaret aldrig forlader maskinen.
///
/// BRUGEREN SKAL IKKE OPRETTE NOGET
///
/// Appen har sit eget klient-id hos Google — se <see cref="Googleklient"/>.
/// Kunden trykker «Forbind», logger ind og godkender. Det er hele forløbet.
///
/// Første udgave bad brugeren om selv at oprette et projekt i Google Cloud
/// Console. Det virker teknisk og er forkert som produkt: den, der skal optage
/// et møde om fem minutter, opretter ikke et cloud-projekt først.
///
/// SIKKERHEDEN LIGGER IKKE I, AT ID'ET ER HEMMELIGT
///
/// Det kan læses ud af ethvert installeret program, og Google ved det. Den
/// ligger i, at godkendelsen sker i brugerens egen browser, at svaret kun
/// sendes til maskinens loopback-adresse, og at der bruges PKCE — så en
/// opsnappet kode ikke kan byttes til en nøgle af nogen anden.
/// </summary>
public static class Googlekalender
{
    public const string Id = "google";

    /// <summary>
    /// Kun læseadgang til kalenderen. Intet andet område bedes der om.
    ///
    /// Den, der godkender, får det at se på Googles egen side — det er ikke
    /// appen, der fortæller, hvad den beder om. Derfor kan det efterprøves.
    /// </summary>
    private const string Omraade = "https://www.googleapis.com/auth/calendar.readonly";

    private const string Godkend = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Noegler = "https://oauth2.googleapis.com/token";
    private const string Aftaler = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Hvad der sker, når man trykker Forbind. Til dialogen FØR browseren
    /// åbner — man skal vide, hvor man bliver sendt hen, inden det sker.
    /// </summary>
    public static string Vejledning =>
        "Der åbner en side hos Google i din browser.\n\n" +
        "Log ind med den konto, din kalender ligger på, og godkend. Så er den " +
        "forbundet — der er ikke mere at gøre.\n\n" +
        "Appen beder om LÆSEADGANG til kalenderen og intet andet. Den kan " +
        "hverken oprette, ændre eller slette noget hos Google, og du kan se " +
        "det på Googles egen side, inden du godkender.";

    // ------------------------------------------------------------ godkendelse

    /// <summary>
    /// Åbner Googles godkendelsesside og venter på svaret.
    ///
    /// DER LYTTES PÅ EN TILFÆLDIG PORT PÅ LOOPBACK, og der lukkes igen, så
    /// snart svaret er kommet. En fast port ville kunne være optaget af noget
    /// andet, og en, der bliver stående åben, er en dør, ingen har brug for.
    ///
    /// Der er en frist. Uden den ville appen kunne stå og lytte for evigt,
    /// fordi nogen lukkede browservinduet i stedet for at trykke annullér.
    /// </summary>
    public static async Task<string> ForbindAsync(CancellationToken ct = default)
    {
        if (Googleklient.Hent() is not var (klientId, hemmelighed) || klientId.Length == 0)
            throw new InvalidOperationException(Googleklient.Mangler);

        // PKCE. Koden fra Google er kun brugbar sammen med den hemmelighed,
        // der blev fundet paa HER - og den forlader aldrig maskinen undtagen
        // som et hash. Uden den kunne et andet program paa maskinen, der
        // naaede at snuppe koden, bytte den til en noegle.
        var verifikator = Tilfaeldig();
        var udfordring = Hash(verifikator);

        var port = LedigPort();
        var svarAdresse = $"http://127.0.0.1:{port}/";

        using var lytter = new HttpListener();
        lytter.Prefixes.Add(svarAdresse);
        lytter.Start();

        // access_type=offline giver en opdateringsnoegle, saa man ikke skal
        // godkende igen hver time. prompt=consent tvinger Google til at
        // udlevere den ogsaa anden gang, man forbinder.
        var adresse =
            $"{Godkend}?client_id={Uri.EscapeDataString(klientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(svarAdresse)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(Omraade)}" +
            $"&access_type=offline&prompt=consent" +
            $"&code_challenge={udfordring}&code_challenge_method=S256";

        Process.Start(new ProcessStartInfo(adresse) { UseShellExecute = true });

        using var frist = CancellationTokenSource.CreateLinkedTokenSource(ct);
        frist.CancelAfter(TimeSpan.FromMinutes(3));

        HttpListenerContext kontekst;

        try
        {
            var venter = lytter.GetContextAsync();
            var færdig = await Task.WhenAny(venter, Task.Delay(Timeout.Infinite, frist.Token));

            if (færdig != venter)
                throw new TimeoutException(
                    "Der kom ikke noget svar fra Google inden for tre minutter. " +
                    "Prøv igen — og lad browservinduet stå åbent, til det er færdigt.");

            kontekst = await venter;
        }
        finally
        {
            // Doeren lukkes, uanset hvordan det gik.
            try { lytter.Stop(); } catch (Exception) { }
        }

        var kode = kontekst.Request.QueryString["code"];
        var fejl = kontekst.Request.QueryString["error"];

        await SvarIBrowseren(kontekst, fejl is null);

        if (fejl is not null)
            throw new InvalidOperationException($"Google svarede: {fejl}");

        if (string.IsNullOrWhiteSpace(kode))
            throw new InvalidOperationException("Google sendte ikke nogen kode tilbage.");

        return await ByttKodeTilNoegle(kode, klientId, hemmelighed, svarAdresse, verifikator, ct);
    }

    /// <summary>
    /// Den side, brugeren ser i browseren bagefter.
    ///
    /// Den skal sige, at man kan lukke vinduet. Uden det bliver man stående og
    /// venter på, at der sker noget mere.
    /// </summary>
    private static async Task SvarIBrowseren(HttpListenerContext k, bool gik)
    {
        var side =
            "<!doctype html><html lang=\"da\"><meta charset=\"utf-8\">" +
            "<title>NoteApp</title>" +
            "<body style=\"font-family:system-ui;background:#0f1216;color:#f4f6fa;" +
            "display:flex;align-items:center;justify-content:center;height:100vh;margin:0\">" +
            "<div style=\"text-align:center;max-width:30rem;padding:2rem\">" +
            (gik
                ? "<h1 style=\"font-size:1.4rem\">Kalenderen er forbundet</h1>" +
                  "<p style=\"color:#9ba6b8\">Du kan lukke det her vindue og gå tilbage til NoteApp.</p>"
                : "<h1 style=\"font-size:1.4rem\">Det blev ikke til noget</h1>" +
                  "<p style=\"color:#9ba6b8\">Godkendelsen blev afbrudt. Luk vinduet og prøv igen i NoteApp.</p>") +
            "</div></body></html>";

        var data = Encoding.UTF8.GetBytes(side);

        k.Response.ContentType = "text/html; charset=utf-8";
        k.Response.ContentLength64 = data.Length;

        await k.Response.OutputStream.WriteAsync(data);
        k.Response.Close();
    }

    private static async Task<string> ByttKodeTilNoegle(string kode, string klientId,
                                                        string hemmelighed, string svarAdresse,
                                                        string verifikator, CancellationToken ct)
    {
        var krop = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = kode,
            ["client_id"] = klientId,
            ["client_secret"] = hemmelighed,
            ["redirect_uri"] = svarAdresse,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = verifikator
        });

        using var svar = await Http.PostAsync(Noegler, krop, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google afviste godkendelsen ({(int)svar.StatusCode}). {Kort(tekst)}");

        using var doc = JsonDocument.Parse(tekst);

        if (!doc.RootElement.TryGetProperty("refresh_token", out var n))
            throw new InvalidOperationException(
                "Google sendte ikke en varig nøgle tilbage. Fjern appens adgang under " +
                "myaccount.google.com/permissions, og prøv igen.");

        return n.GetString() ?? "";
    }

    // --------------------------------------------------------------- hentning

    /// <summary>
    /// Henter aftalerne fra i dag og fjorten dage frem.
    ///
    /// FJORTEN DAGE, IKKE ET ÅR. Kalenderen i appen svarer på «hvad skal jeg
    /// optage», og det spørgsmål rækker ikke længere end et par uger. En
    /// hentning af hele året ville tage flere kald, fylde listen med noget,
    /// ingen kigger på, og gøre hver synkronisering langsommere uden at svare
    /// på mere.
    ///
    /// AFLYSTE AFTALER TAGES IKKE MED. Google beholder dem med status
    /// «cancelled», og de ville ellers stå i listen som noget, der skal
    /// optages.
    /// </summary>
    public static async Task<List<Aftale>> HentAsync(string opdateringsnoegle, int dage = 14,
                                                     CancellationToken ct = default)
    {
        if (Googleklient.Hent() is not var (klientId, hemmelighed) || klientId.Length == 0)
            throw new InvalidOperationException(Googleklient.Mangler);

        var noegle = await FriskNoegle(klientId, hemmelighed, opdateringsnoegle, ct);

        var fra = DateTimeOffset.Now.Date;
        var til = fra.AddDays(dage);

        var adresse =
            $"{Aftaler}?timeMin={Uri.EscapeDataString(new DateTimeOffset(fra).ToString("o"))}" +
            $"&timeMax={Uri.EscapeDataString(new DateTimeOffset(til).ToString("o"))}" +
            $"&singleEvents=true&orderBy=startTime&maxResults=250";

        using var anmodning = new HttpRequestMessage(HttpMethod.Get, adresse);
        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google svarede {(int)svar.StatusCode}. {Kort(tekst)}");

        return Laes(tekst);
    }

    private static List<Aftale> Laes(string json)
    {
        var ud = new List<Aftale>();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var poster)) return ud;

        foreach (var p in poster.EnumerateArray())
        {
            if (p.TryGetProperty("status", out var s)
                && s.GetString() == "cancelled") continue;

            var start = Tidspunkt(p, "start");
            if (start is null) continue;

            ud.Add(new Aftale
            {
                Titel = Tekst(p, "summary") is { Length: > 0 } t ? t : "Uden titel",
                Start = start.Value,
                Slut = Tidspunkt(p, "end"),
                Sted = Tekst(p, "location"),
                Link = Moedelink(p),
                Kilde = Kalenderkilde.Google,
                FremmedId = Tekst(p, "id")
            });
        }

        return ud;
    }

    private static string Tekst(JsonElement e, string navn) =>
        e.TryGetProperty(navn, out var v) ? v.GetString() ?? "" : "";

    /// <summary>
    /// Tidspunktet. En heldagsaftale har «date» i stedet for «dateTime» — den
    /// regnes fra midnat, så den står øverst på dagen frem for at forsvinde.
    /// </summary>
    private static DateTimeOffset? Tidspunkt(JsonElement e, string navn)
    {
        if (!e.TryGetProperty(navn, out var d)) return null;

        if (d.TryGetProperty("dateTime", out var dt)
            && DateTimeOffset.TryParse(dt.GetString(), out var t)) return t;

        if (d.TryGetProperty("date", out var dag)
            && DateTime.TryParse(dag.GetString(), out var kun))
            return new DateTimeOffset(kun.Date, TimeZoneInfo.Local.GetUtcOffset(kun.Date));

        return null;
    }

    /// <summary>
    /// Mødelinket. Google har det tre steder, og de tre er ikke lige gode.
    ///
    /// «hangoutLink» er Meet-linket og det rigtige, når det findes.
    /// Ellers står linket tit i beskrivelsen — men dér står også alt muligt
    /// andet, så der ledes efter noget, der ligner en adresse, og ikke bare
    /// tages det første ord.
    /// </summary>
    private static string Moedelink(JsonElement p)
    {
        var meet = Tekst(p, "hangoutLink");
        if (meet.Length > 0) return meet;

        var beskrivelse = Tekst(p, "description");

        var m = System.Text.RegularExpressions.Regex.Match(
            beskrivelse, @"https?://[^\s""<>]+");

        return m.Success ? m.Value : "";
    }

    /// <summary>
    /// Bytter den varige nøgle til en, der gælder en time.
    ///
    /// Den friske nøgle gemmes IKKE. Den holder en time, og at gemme den ville
    /// betyde, at der lå en brugbar adgang i en fil hele tiden. Den varige
    /// nøgle skal ligge et sted — den friske behøver ikke.
    /// </summary>
    private static async Task<string> FriskNoegle(string klientId, string hemmelighed,
                                                  string opdateringsnoegle, CancellationToken ct)
    {
        var krop = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = klientId,
            ["client_secret"] = hemmelighed,
            ["refresh_token"] = opdateringsnoegle,
            ["grant_type"] = "refresh_token"
        });

        using var svar = await Http.PostAsync(Noegler, krop, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Adgangen til Google virker ikke længere ({(int)svar.StatusCode}). " +
                $"Forbind igen under Indstillinger. {Kort(tekst)}");

        using var doc = JsonDocument.Parse(tekst);

        return doc.RootElement.TryGetProperty("access_token", out var a)
            ? a.GetString() ?? ""
            : throw new InvalidOperationException("Google sendte ingen adgangsnøgle.");
    }

    /// <summary>
    /// En tilfældig streng til PKCE. Googles krav er 43-128 tegn fra et
    /// begrænset alfabet; base64url af 32 tilfældige byte giver 43.
    /// </summary>
    private static string Tilfaeldig()
    {
        var b = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(b);
        return UdenPolstring(b);
    }

    private static string Hash(string s)
    {
        var h = System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(s));
        return UdenPolstring(h);
    }

    /// <summary>
    /// base64url: som base64, men uden polstring og med de to tegn, der ikke
    /// kan stå i en adresse, byttet ud.
    /// </summary>
    private static string UdenPolstring(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>En ledig port. Nul beder styresystemet om at finde en.</summary>
    private static int LedigPort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>
    /// Fejlteksten fra Google, kortet af.
    ///
    /// Hele svaret er en json på flere hundrede tegn, og den hører ikke i en
    /// dialog. Men den skal med i afkortet form: uden den står der «det gik
    /// galt», og så kan man ikke gøre noget ved det.
    /// </summary>
    private static string Kort(string s)
    {
        var t = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return t.Length <= 200 ? t : t[..200] + " …";
    }
}
