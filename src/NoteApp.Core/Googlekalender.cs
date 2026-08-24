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
/// Den LÆSER kalenderen, og den kan oprette en aftale — men KUN den, brugeren
/// selv sætter hak ved. Der er bedt om ét område, `calendar.events`, og det er
/// hele adgangen: aftaler. Appen kan ikke røre indstillinger, ikke oprette
/// eller slette kalendere, og den kan ikke se noget som helst andet i kontoen.
/// Områdenavnet står i koden og kan efterprøves; det er ikke et løfte, det er
/// en grænse, Google håndhæver.
///
/// HVORFOR IKKE BARE calendar.readonly
///
/// Det var det, der blev bedt om indtil 24-08-2026, og det var ærligere at
/// skrive om — «der sendes ingenting op» er en stærk sætning. Men en
/// kalender, man kun kan læse, er en kalender, der lever to steder: aftalen,
/// man laver i appen, findes ikke på telefonen. Det er ikke en integration,
/// det er en visning.
///
/// Området blev udvidet, MENS DER VAR NUL BRUGERE. Tilføjes et område bagefter,
/// skal hver eneste, der har forbundet, igennem godkendelsen igen.
///
/// DET, DER ALDRIG SENDES: lyden, transkriptionerne, noterne og dokumenterne.
/// Der findes ingen kodesti, der lægger dem i en aftale, og området giver ikke
/// adgang til andet end aftaler alligevel.
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
/// <summary>
/// Svaret, når en aftale er lagt op hos Google.
///
/// DER RETURNERES TRE TING OG IKKE KUN ET ID, fordi alle tre skal bruges med
/// det samme og ikke kan hentes igen uden endnu et kald:
///
///   Id           genkender aftalen ved næste hentning, så den ikke står dobbelt
///   Moedelink    Meet-linket — det skal tilbage i appen, ellers er mødet
///                klikbart hos Google og dødt her
///   Webadresse   aftalen på Googles egen side. Den bruges, når der skal
///                inviteres gæster: det er dér, man har sine kontakter
/// </summary>
public sealed record Googlesvar(string Id, string Moedelink, string Webadresse);

public static class Googlekalender
{
    public const string Id = "google";

    /// <summary>
    /// Aftaler, og intet andet i kontoen. Området rækker til at læse aftaler
    /// og til at oprette dem — det sidste sker kun, når brugeren sætter hak.
    ///
    /// Den, der godkender, får det at se på Googles egen side — det er ikke
    /// appen, der fortæller, hvad den beder om. Derfor kan det efterprøves.
    /// </summary>
    private const string Omraade = "https://www.googleapis.com/auth/calendar.events";

    private const string Godkend = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Noegler = "https://oauth2.googleapis.com/token";
    private const string Aftaler = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>
    /// Hvad der sker, når man trykker Forbind. Til dialogen FØR browseren
    /// åbner — man skal vide, hvad man giver adgang til, inden det sker.
    ///
    /// DEN ER SKREVET SOM ET SAMTYKKE OG IKKE SOM EN VEJLEDNING. Adgangen
    /// rækker længere end før: appen kan nu også oprette en aftale. Det sker
    /// stadig kun på et hak, brugeren selv sætter — men en udvidet adgang, der
    /// præsenteres som «tryk her, så er du forbundet», er ikke oplyst samtykke,
    /// uanset hvor pænt det står.
    ///
    /// Teksten siger tre ting i den rækkefølge, folk har brug for dem: hvad
    /// appen MÅ, hvad den GØR af sig selv, og hvad den ALDRIG sender.
    /// </summary>
    public static string Vejledning =>
        "Der åbner en side hos Google i din browser. Log ind med den konto, " +
        "din kalender ligger på, og godkend.\n\n" +
        "DETTE FÅR APPEN LOV TIL\n" +
        "Ét område: dine aftaler. Appen kan læse dem, og den kan oprette en " +
        "aftale. Den kan ikke se din mail, dine filer, dine kontakter eller " +
        "noget andet i kontoen — området rækker ikke dertil, og det er Google, " +
        "der håndhæver grænsen.\n\n" +
        "DETTE GØR DEN AF SIG SELV\n" +
        "Henter dine aftaler ned, så du kan trykke optag direkte på et møde. " +
        "Der bliver hverken oprettet eller ændret noget hos Google, medmindre " +
        "du sætter hak ved «Opret også i Google Kalender» på en aftale.\n\n" +
        "DETTE SENDES ALDRIG\n" +
        "Lyd, transkriptioner, noter og dokumenter. Der findes ingen vej i " +
        "appen, der lægger dem i en kalender.\n\n" +
        "Aftaler hos Google er personoplysninger — titler og deltagere — og de " +
        "ligger hos en amerikansk leverandør. Læs «Andre integrationer» under " +
        "Compliance, inden du forbinder. Du kan afbryde forbindelsen igen når " +
        "som helst, og så forsvinder de hentede aftaler.";

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
        catch (Exception)
        {
            // Gik det galt, er der ingen browser at svare - saa lukkes doeren
            // her.
            try { lytter.Stop(); } catch (Exception) { }
            throw;
        }

        string? kode;
        string? fejl;

        try
        {
            kode = kontekst.Request.QueryString["code"];
            fejl = kontekst.Request.QueryString["error"];

            await SvarIBrowseren(kontekst, fejl is null);
        }
        finally
        {
            // FOERST HER. Doeren maa ikke lukkes, foer browseren har faaet sit
            // svar.
            //
            // Stod Stop() i et finally lige efter GetContextAsync, blev
            // svarstroemmen revet ned, inden der var skrevet paa den. Brugeren
            // saa en tom side, og appen sagde «Cannot access a disposed
            // object: ThreadPoolBoundHandle» - en besked, der ikke paa nogen
            // maade peger paa, at det var vores egen oprydning, der kom for
            // tidligt. Fundet 24-08-2026, foerste gang forloebet blev koert.
            try { lytter.Stop(); } catch (Exception) { }
        }

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

    /// <summary>
    /// Lægger en aftale op i Google. Svarer med aftalens id dér.
    ///
    /// DEN KALDES KUN, NÅR BRUGEREN HAR SAT HAK. Der er ingen sti i appen, der
    /// sender noget op af sig selv — hverken ved en hentning, ved en optagelse
    /// eller når en aftale rettes uden hakket. Det er hele forskellen på en
    /// integration, man kan overskue, og en, man ikke tør slå til.
    ///
    /// DER SENDES FIRE FELTER: titel, start, slut og sted eller link. Ikke
    /// mødetype, ikke mappe, ikke sprog — de er appens egne og vedkommer ikke
    /// Google. Og aldrig lyd, transkriptioner, noter eller dokumenter; der er
    /// ikke noget felt at lægge dem i, og området giver ikke adgang til det.
    ///
    /// TIDSZONEN SENDES MED. Google gætter ellers på kalenderens egen, og en
    /// aftale, der lander en time forkert, er værre end en, der fejler.
    /// </summary>
    public static async Task<Googlesvar> OpretAsync(Aftale aftale, string opdateringsnoegle,
                                                    bool medMeet = true,
                                                    CancellationToken ct = default)
    {
        if (Googleklient.Hent() is not var (klientId, hemmelighed) || klientId.Length == 0)
            throw new InvalidOperationException(Googleklient.Mangler);

        var noegle = await FriskNoegle(klientId, hemmelighed, opdateringsnoegle, ct);

        var zone = TimeZoneInfo.Local.Id;

        var krop = new Dictionary<string, object?>
        {
            ["summary"] = aftale.Titel,
            ["start"] = new Dictionary<string, string>
            {
                ["dateTime"] = aftale.Start.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ["timeZone"] = zone
            },
            ["end"] = new Dictionary<string, string>
            {
                ["dateTime"] = aftale.Slutter.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ["timeZone"] = zone
            }
        };

        // Stedet og linket er det samme felt hos Google, naar der ikke er tale
        // om et Meet-moede. Et link i «location» er klikbart i deres app.
        var hvor = aftale.Link.Length > 0 ? aftale.Link : aftale.Sted;
        if (hvor.Length > 0) krop["location"] = hvor;

        // ---- Google Meet
        //
        // Et Meet-link laves ikke ved at skrive en adresse i et felt. Google
        // SKAL bede om det, og det sker med en createRequest med et id, der er
        // vores eget. Id'et goer kaldet gentageligt: sendes det samme to gange,
        // laver Google ikke to moederum.
        //
        // conferenceDataVersion=1 skal med paa adressen. Uden den bliver hele
        // conferenceData tavst ignoreret - aftalen bliver oprettet, bare uden
        // link, og der kommer ingen fejl at gaa efter.
        var adresse = Aftaler;

        if (medMeet)
        {
            krop["conferenceData"] = new Dictionary<string, object?>
            {
                ["createRequest"] = new Dictionary<string, object?>
                {
                    ["requestId"] = Guid.NewGuid().ToString("N"),
                    ["conferenceSolutionKey"] = new Dictionary<string, string>
                    {
                        ["type"] = "hangoutsMeet"
                    }
                }
            };

            adresse += "?conferenceDataVersion=1";
        }

        using var anmodning = new HttpRequestMessage(HttpMethod.Post, adresse)
        {
            Content = new StringContent(JsonSerializer.Serialize(krop),
                                        Encoding.UTF8, "application/json")
        };

        anmodning.Headers.Authorization = new("Bearer", noegle);

        using var svar = await Http.SendAsync(anmodning, ct);
        var tekst = await svar.Content.ReadAsStringAsync(ct);

        if (!svar.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google svarede {(int)svar.StatusCode}. {Kort(tekst)}");

        using var doc = JsonDocument.Parse(tekst);

        // Uden id'et kan aftalen ikke genkendes ved naeste hentning, og saa
        // ville den staa dobbelt. Et tomt svar er derfor en fejl, ikke en
        // detalje - selv om aftalen ER oprettet hos Google.
        if (!doc.RootElement.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } v)
            throw new InvalidOperationException(
                "Aftalen blev oprettet hos Google, men der kom intet id tilbage. " +
                "Den kan komme til at staa dobbelt ved naeste hentning.");

        return new Googlesvar(v, Moedelink(doc.RootElement), Tekst(doc.RootElement, "htmlLink"));
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

        // conferenceData er den anden vej til det samme.
        //
        // NAAR ET MEET-RUM LIGE ER BESTILT, er hangoutLink ikke altid udfyldt i
        // svaret - rummet kan staa som «pending» et oejeblik. Linket ligger til
        // gengaeld i entryPoints med det samme. Uden det her ville et nyoprettet
        // moede af og til komme uden link, og det ville se tilfaeldigt ud, for
        // det ville virke de fleste gange.
        if (p.TryGetProperty("conferenceData", out var konf)
            && konf.TryGetProperty("entryPoints", out var indgange)
            && indgange.ValueKind == JsonValueKind.Array)
        {
            foreach (var indgang in indgange.EnumerateArray())
            {
                if (Tekst(indgang, "entryPointType") != "video") continue;

                var uri = Tekst(indgang, "uri");
                if (uri.Length > 0) return uri;
            }
        }

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
        {
            // invalid_grant betyder, at noeglen er udloebet eller trukket
            // tilbage - ikke at der er noget galt med nettet. Forskellen
            // afgoer, om man skal forbinde igen eller bare vente, og en
            // besked, der ikke skelner, sender folk det forkerte sted hen.
            //
            // DET SKER HVER UGE, SAA LAENGE APPEN STAAR SOM «TESTING» HOS
            // GOOGLE: opdateringsnoegler til en app i test udloeber efter syv
            // dage. Det er ikke en fejl i appen, og det skal beskeden sige.
            var udloebet = tekst.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase);

            throw new InvalidOperationException(udloebet
                ? "Forbindelsen til Google er udløbet. Tryk Forbind igen under " +
                  "Indstillinger → Integrationer. Dine aftaler i appen er urørte."
                : $"Der kunne ikke hentes fra Google ({(int)svar.StatusCode}). " +
                  $"{Kort(tekst)}");
        }

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
