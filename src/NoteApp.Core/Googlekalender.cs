using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

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

    /// <summary>
    /// Det, der skrives i mødeindkaldelsen, når appen laver et Meet-link.
    ///
    /// APPEN OPFORDRER ALTID TIL AT FORTÆLLE, AT DER OPTAGES. Det er ikke kun
    /// jura. Det er produktets stærkeste argument: fordi lyden bliver på
    /// maskinen, kan den, der optager, se de andre i øjnene og sige, hvor
    /// optagelsen ender.
    ///
    /// Den står i indkaldelsen og ikke kun på skærmen hos den, der optager.
    /// Deltagerne får den at vide FØR mødet, hvor de kan nå at sige fra — ikke
    /// i det øjeblik optagelsen begynder.
    ///
    /// «PLANLÆGGES OPTAGET» og ikke «bliver optaget». Et Meet-link er ikke et
    /// løfte om, at der bliver trykket optag, og en indkaldelse, der siger
    /// noget, der ikke skete, er værre end ingen note.
    ///
    /// RETTEN TIL AT SIGE NEJ STÅR MED, OG DEN STÅR KONKRET. «Sig til, hvis du
    /// helst er fri» er en høflighed; en modtager ved ikke, om det betyder
    /// noget. Der skal stå, hvem man siger det til, hvornår det kan siges, og
    /// hvad der så sker — ellers er det en oplysning og ikke et valg.
    ///
    /// DEN SIGER «MØDETRANSSKRIPTION» OG IKKE «OPTAGELSE».
    ///
    /// Det er dét, det er: formålet er den skrevne tekst, og lydoptagelsen er
    /// midlet. Zoom skriver det på den måde, og de har ret — «mødet optages»
    /// får folk til at tænke på en fil, nogen kan finde frem om to år.
    ///
    /// Men optagelsen NÆVNES i samme åndedrag. Der optages faktisk lyd, og
    /// det er dét, man siger ja eller nej til; en formulering, der skjulte
    /// det bag et pænere ord, ville være en, der ikke holdt, hvis nogen
    /// spurgte bagefter.
    ///
    /// OMFANGET STÅR ØVERST, OG DET ER MED VILJE. «Mødet optages» læses i dag
    /// som «mødet filmes» — det er blevet almindeligt, og folk siger nej til
    /// det. Her optages der KUN lyd, og det er en langt mindre ting at sige ja
    /// til. Står det ikke i første linje, bliver der sagt nej til noget, appen
    /// slet ikke gør.
    ///
    /// Formen er med vilje juridisk: faste overskrifter, adskilt fra resten af
    /// indkaldelsen, uden salgstone. Den skal kunne læses af en, der ikke
    /// kender appen, og stå sig, hvis nogen spørger bagefter.
    ///
    /// LYDEN OG TEKSTEN ER IKKE DET SAMME, OG DET SKAL STÅ.
    ///
    /// Her stod først «lyd og udskrevet tekst deles ikke med eksterne
    /// tjenester». Lyden forlader aldrig maskinen, og dét er sandt — men
    /// vælger arrangøren at få lavet et referat, sendes TEKSTEN til Mistral.
    /// Sætningen ville altså være usand netop i det tilfælde, hvor nogen
    /// bagefter kiggede efter. En ansvarsfraskrivelse, der ikke holder, er
    /// værre end ingen.
    ///
    /// Den siger derfor to ting hver for sig: lyden bliver, teksten kan
    /// behandles hos en databehandler i EU.
    /// </summary>
    public const string StandardOptagenote =
        "Der anvendes mødetransskription til det her møde. Det kræver, at " +
        "LYDEN optages. Der optages ikke video.\n" +
        "\n" +
        "— — — — — — — — — —\n" +
        "OPLYSNING OM MØDETRANSSKRIPTION\n" +
        "\n" +
        "Omfang: Der optages alene lyd, og formålet er den skrevne tekst. " +
        "Der optages hverken video, kamerabillede eller skærm, og der gemmes " +
        "ingen billeder af mødet.\n" +
        "\n" +
        "Formål: Optagelsen anvendes til at udarbejde referat og noter fra " +
        "mødet.\n" +
        "\n" +
        "Behandling: Lydoptagelsen behandles lokalt på arrangørens computer " +
        "og overføres ikke til eksterne tjenester. Vælger arrangøren at få " +
        "udarbejdet et referat med en sprogmodel, behandles den udskrevne " +
        "tekst — ikke lyden — hos en databehandler i EU.\n" +
        "\n" +
        "Indsigelse: Du kan gøre indsigelse mod at blive optaget. Meddel det " +
        "til arrangøren forud for mødet eller ved mødets begyndelse; " +
        "optagelsen undlades da.\n" +
        "\n" +
        "Sletning: Du kan til enhver tid anmode arrangøren om at få " +
        "lydoptagelsen og den udskrevne tekst slettet.\n" +
        "\n" +
        "Ansvarlig: Mødets arrangør er dataansvarlig for optagelsen.";

    /// <summary>
    /// Den engelske udgave.
    ///
    /// EN INDKALDELSE PÅ DANSK TIL EN, DER IKKE LÆSER DANSK, ER IKKE EN
    /// OPLYSNING. Retten til at sige fra er kun værd at have, hvis den kan
    /// læses — og et møde, hvor sproget er sat til engelsk, har med sikkerhed
    /// deltagere, der ikke læser dansk.
    ///
    /// Det er en oversættelse af den danske, ikke en anden tekst. To udgaver,
    /// der siger noget forskelligt, er værre end én, der er på det forkerte
    /// sprog: så afhænger det af, hvem der læser hvilken.
    /// </summary>
    public const string StandardOptagenoteEn =
        "Meeting transcription is used for this meeting. This requires the " +
        "AUDIO to be recorded. No video is recorded.\n" +
        "\n" +
        "— — — — — — — — — —\n" +
        "NOTICE OF MEETING TRANSCRIPTION\n" +
        "\n" +
        "Scope: Audio only, and the purpose is the written transcript. No " +
        "video, camera image or screen is recorded, and no images of the " +
        "meeting are stored.\n" +
        "\n" +
        "Purpose: The recording is used to produce minutes and notes from the " +
        "meeting.\n" +
        "\n" +
        "Processing: The audio recording is processed locally on the " +
        "organiser's computer and is not transferred to external services. " +
        "If the organiser chooses to have minutes drafted using a language " +
        "model, the transcribed text — not the audio — is processed by a data " +
        "processor within the EU.\n" +
        "\n" +
        "Objection: You may object to being recorded. Please tell the " +
        "organiser before the meeting or at its start, and no recording will " +
        "be made.\n" +
        "\n" +
        "Deletion: You may at any time ask the organiser to delete the audio " +
        "recording and the transcribed text.\n" +
        "\n" +
        "Controller: The meeting organiser is the data controller for the " +
        "recording.";

    /// <summary>
    /// Den note, der faktisk skrives — brugerens egen, hvis der er sat en.
    ///
    /// DEN KAN RETTES, MEN IKKE FJERNES. Ordlyden hører til den, der holder
    /// mødet: et firma har sin egen formulering, en underviser en anden, og
    /// en tekst, man ikke må røre, bliver til en, man arbejder udenom.
    ///
    /// Står feltet tomt, bruges standarden. Appen opfordrer ALTID til at
    /// fortælle deltagerne, at der optages, og et tomt felt er ikke et valg
    /// om at lade være — det er et felt, ingen har udfyldt.
    /// </summary>
    public static string Optagenote => Note("");

    /// <summary>
    /// Noten på det sprog, mødet holdes på.
    ///
    /// SPROGET KOMMER FRA AFTALEN. Er der valgt engelsk til optagelsen, er
    /// det fordi mødet holdes på engelsk — og så skal indkaldelsen være det
    /// også. Er der intet valgt, er dansk det rigtige gæt: appen er dansk.
    /// </summary>
    /// <param name="sprogkode">Aftalens sprog. Tom betyder dansk.</param>
    public static string Note(string sprogkode)
    {
        var engelsk = sprogkode.Length > 0
                   && !sprogkode.StartsWith("da", StringComparison.OrdinalIgnoreCase);

        try
        {
            var egen = engelsk
                ? AppSettings.Current.OptagenoteEn
                : AppSettings.Current.Optagenote;

            if (!string.IsNullOrWhiteSpace(egen)) return egen.Trim();
        }
        catch (Exception)
        {
            // Kan indstillingerne ikke laeses, staar standarden.
        }

        return engelsk ? StandardOptagenoteEn : StandardOptagenote;
    }

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
    /// <param name="medMeet">Skal Google oprette et mødelokale?</param>
    /// <param name="medNote">
    /// Skal oplysningen om optagelse med i indkaldelsen?
    ///
    /// KUN NÅR MØDET FAKTISK SKAL OPTAGES. Et Meet-link er ikke i sig selv en
    /// beslutning om at optage — man laver også online-møder, man ikke skal
    /// have referat af. En indkaldelse, der varsler en optagelse, der aldrig
    /// kommer, er en, folk holder op med at læse.
    /// </param>
    public static async Task<Googlesvar> OpretAsync(Aftale aftale, string opdateringsnoegle,
                                                    bool medMeet = true,
                                                    bool medNote = false,
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

        // Deltagerne skal vide det, FOER moedet - ikke i det oejeblik
        // optagelsen begynder. Men kun naar der FAKTISK skal optages.
        if (medNote) krop["description"] = Note(aftale.Sprog);

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

    /// <summary>
    /// Lægger et Google Meet-link på en aftale, der allerede findes hos Google.
    ///
    /// Svarer med linket.
    ///
    /// DEN ÆNDRER INTET ANDET. Der sendes en PATCH med conferenceData og intet
    /// andet felt — titel, tidspunkt, sted og deltagere er, som de var. En
    /// hentet aftale hører hjemme hos Google, og appen skal ikke rette i den,
    /// fordi nogen bad om et mødelink.
    ///
    /// conferenceDataVersion=1 skal med. Uden den bliver anmodningen tavst
    /// ignoreret: Google svarer 200, aftalen er uændret, og der er ingen fejl
    /// at gå efter.
    /// </summary>
    /// <param name="medNote">
    /// Skal oplysningen om optagelse med? Kun når mødet faktisk skal optages —
    /// et mødelink er ikke i sig selv en beslutning om at optage.
    /// </param>
    /// <param name="sprogkode">Mødets sprog — afgør, om noten er dansk eller engelsk.</param>
    public static async Task<string> TilfoejMeetAsync(string fremmedId, string opdateringsnoegle,
                                                      bool medNote = false,
                                                      string sprogkode = "",
                                                      CancellationToken ct = default)
    {
        if (Googleklient.Hent() is not var (klientId, hemmelighed) || klientId.Length == 0)
            throw new InvalidOperationException(Googleklient.Mangler);

        if (string.IsNullOrWhiteSpace(fremmedId))
            throw new InvalidOperationException(
                "Aftalen findes ikke hos Google, så der kan ikke lægges et mødelink på den.");

        var noegle = await FriskNoegle(klientId, hemmelighed, opdateringsnoegle, ct);

        // DEN EKSISTERENDE BESKRIVELSE HENTES FOERST.
        //
        // En PATCH med «description» ERSTATTER feltet. Skrev vi bare noten
        // ind, ville alt, arrangoeren havde skrevet i indkaldelsen - dagsorden,
        // links, aftaler - vaere vaek. Det ville vaere et rigtigt tab, og det
        // ville ske i stilhed.
        var krop = new Dictionary<string, object?>
        {
            ["conferenceData"] = new Dictionary<string, object?>
            {
                ["createRequest"] = new Dictionary<string, object?>
                {
                    ["requestId"] = Guid.NewGuid().ToString("N"),
                    ["conferenceSolutionKey"] = new Dictionary<string, string>
                    {
                        ["type"] = "hangoutsMeet"
                    }
                }
            }
        };

        // NOTEN KUN NAAR DER FAKTISK SKAL OPTAGES.
        //
        // Den eksisterende beskrivelse hentes foerst: en PATCH med
        // «description» ERSTATTER feltet, og skrev vi bare noten ind,
        // ville dagsorden, links og aftaler vaere vaek. Det ville vaere et
        // rigtigt tab, og det ville ske i stilhed.
        if (medNote)
        {
            var beskrivelse = await HentBeskrivelse(fremmedId, noegle, ct);

            var note = Note(sprogkode);

            krop["description"] =
                beskrivelse.Contains("NoteApp", StringComparison.OrdinalIgnoreCase) ? beskrivelse
                : beskrivelse.Length > 0 ? beskrivelse.TrimEnd() + "\n\n" + note
                : note;
        }

        var adresse = $"{Aftaler}/{Uri.EscapeDataString(fremmedId)}?conferenceDataVersion=1";

        using var anmodning = new HttpRequestMessage(HttpMethod.Patch, adresse)
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

        var link = Moedelink(doc.RootElement);

        if (link.Length == 0)
            throw new InvalidOperationException(
                "Google oprettede ikke et mødelokale. Det sker, hvis kontoen ikke " +
                "har Google Meet, eller hvis aftalen ligger i en kalender, du ikke " +
                "kan redigere.");

        return link;
    }

    /// <summary>
    /// Beskrivelsen på en aftale hos Google.
    ///
    /// Tom, hvis den ikke kan læses. Så skrives noten alene — det er bedre end
    /// at lade være, og det værste, der kan ske, er en beskrivelse, der skal
    /// skrives igen. Alternativet var at afvise mødelinket, fordi ét felt ikke
    /// kunne hentes.
    /// </summary>
    private static async Task<string> HentBeskrivelse(string fremmedId, string noegle,
                                                      CancellationToken ct)
    {
        try
        {
            using var anmodning = new HttpRequestMessage(HttpMethod.Get,
                $"{Aftaler}/{Uri.EscapeDataString(fremmedId)}");

            anmodning.Headers.Authorization = new("Bearer", noegle);

            using var svar = await Http.SendAsync(anmodning, ct);
            if (!svar.IsSuccessStatusCode) return "";

            using var doc = JsonDocument.Parse(await svar.Content.ReadAsStringAsync(ct));

            return Tekst(doc.RootElement, "description");
        }
        catch (Exception)
        {
            return "";
        }
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
                FremmedId = Tekst(p, "id"),
                Arrangoer = Arrangoeren(p),
                ErEgetMoede = ErEgen(p)
            });
        }

        return ud;
    }

    /// <summary>
    /// Hvem der har indkaldt. Navnet, hvis Google har et — ellers adressen.
    ///
    /// «organizer» og ikke «creator»: den, der ejer mødet, er den, man skal
    /// svare. En sekretær, der har oprettet det, er ikke den, mødet er med.
    /// </summary>
    private static string Arrangoeren(JsonElement p)
    {
        if (!p.TryGetProperty("organizer", out var o)) return "";

        var navn = Tekst(o, "displayName");
        if (navn.Length > 0) return navn;

        var post = Tekst(o, "email");

        // Kun den del foer snabel-a, naar der ikke er et navn. En hel adresse
        // fylder en linje i en smal spalte og siger ikke mere.
        var snabel = post.IndexOf('@');
        return snabel > 0 ? post[..snabel] : post;
    }

    /// <summary>
    /// Er det brugerens eget møde? Google sætter «self» på den, der er logget
    /// ind.
    /// </summary>
    private static bool ErEgen(JsonElement p) =>
        p.TryGetProperty("organizer", out var o)
        && o.TryGetProperty("self", out var s)
        && s.ValueKind == JsonValueKind.True;

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
