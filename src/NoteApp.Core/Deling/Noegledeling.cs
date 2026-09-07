using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoteApp.Core.Llm;

namespace NoteApp.Core.Deling;

/// <summary>En lukket kuvert i den fælles mappe. Kun modtageren kan åbne den.</summary>
/// <param name="Fra">Afsenderens maskin-id.</param>
/// <param name="Til">Modtagerens maskin-id.</param>
/// <param name="Salt">Tilfældig pr. kuvert. Se <see cref="Noegledeling"/>.</param>
/// <param name="Nonce">Engangstal til AES-GCM.</param>
/// <param name="Maerke">GCM-mærket, der afslører enhver ændring.</param>
/// <param name="Indhold">Selve den krypterede tekst.</param>
/// <param name="Udloeber">Efter det tidspunkt åbnes den ikke.</param>
public sealed record Kuvert(
    string Fra,
    string Til,
    string Salt,
    string Nonce,
    string Maerke,
    string Indhold,
    DateTimeOffset Udloeber);

/// <summary>
/// Hvad en kuvert bærer.
/// </summary>
/// <remarks>
/// HVER SLAGS HAR SIT EGET FORMÅL I UDLEDNINGEN. To slags hemmeligheder må
/// ikke krypteres under den samme nøgle: kan en kuvert med den ene flyttes
/// hen, hvor den anden læses, er der åbnet en dør, ingen havde tænkt på.
/// </remarks>
public enum Kuvertslags
{
    /// <summary>API-nøglen til den europæiske sprogmodel.</summary>
    Apinoegle,

    /// <summary>Adgangen til Google Kalender.</summary>
    Googlekalender,

    /// <summary>Adgangen til Google Tasks.</summary>
    Googleopgaver,
}

/// <summary>
/// Sender en adgang fra den ene af to parrede computere til den anden.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN FINDES ============
///
/// Nøglen til Mistral og forbindelsen til Google hører til maskinen, ikke til
/// datasættet — de ligger derfor ikke i den fælles mappe, som begge kan læse.
/// Men når man lige har stået og sammenlignet seks cifre på to skærme for at
/// sige «det er mine to computere», er det en dårlig belønning at skulle finde
/// nøglen frem og logge ind hos Google en gang til.
///
/// ============ DEN LIGGER ALDRIG I KLARTEKST ============
///
/// Kuverten krypteres med AES-256-GCM under en nøgle, der er udledt af den
/// fælles nøgle fra parringen — den, ingen af maskinerne har sendt nogen
/// steder. Den, der kan læse den fælles mappe, ser et tilfældigt tal.
///
/// SALTET ER NYT HVER GANG. Den fælles nøgle er den samme, så længe de to
/// maskiner er parret; bruges den direkte som krypteringsnøgle, krypteres to
/// beskeder under den samme nøgle. Saltet gør, at hver kuvert får sin egen.
///
/// AFSENDER, MODTAGER, UDLØB OG FORMÅL ER BUNDET TIL KRYPTERINGEN. De står som
/// «yderligere data» i GCM: rettes ét tegn i dem, kan kuverten ikke åbnes.
/// Uden det kunne en kuvert flyttes til en anden modtager, få forlænget sit
/// udløb eller blive læst som en anden slags af den, der kan skrive i mappen.
///
/// ============ DEN LEVER EN TIME OG ÅBNES ÉN GANG ============
///
/// En hemmelighed, der bliver liggende i en mappe, er en hemmelighed mere,
/// nogen skal huske at rydde op efter. Kuverten slettes, når den er åbnet, og
/// den afvises efter en time — også selv om filen stadig ligger der.
///
/// ============ DEN SENDES MED HÅNDEN ============
///
/// Der sendes aldrig noget af sig selv. Et menneske trykker på knappen på den
/// maskine, der HAR adgangen. At kopiere en adgang rundt uden at nogen har
/// bedt om det er ikke en bekvemmelighed — det er en overraskelse.
/// </remarks>
public static class Noegledeling
{
    /// <summary>Hvor længe en kuvert kan åbnes.</summary>
    public static readonly TimeSpan Levetid = TimeSpan.FromHours(1);

    /// <summary>Alle de slags, der ses efter, når en computer melder sig.</summary>
    public static readonly IReadOnlyList<Kuvertslags> Slags = new[]
    {
        Kuvertslags.Apinoegle,
        Kuvertslags.Googlekalender,
        Kuvertslags.Googleopgaver,
    };

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Mappe(string rod) => Path.Combine(rod, "noegler");

    /// <summary>
    /// Kuvertens navn i mappen.
    /// </summary>
    /// <remarks>
    /// API-NØGLEN BEHOLDER SIT GAMLE NAVN. En computer, der endnu ikke er
    /// opdateret, ser kun efter <c>&lt;id&gt;.json</c> — får den navnet et
    /// endelsestillæg, holder nøgledelingen op med at virke mellem to udgaver,
    /// og det ville vise sig som ingenting: kuverten bliver bare liggende.
    /// </remarks>
    private static string Fil(string rod, string modtager, Kuvertslags slags) =>
        Path.Combine(Mappe(rod), slags == Kuvertslags.Apinoegle
            ? modtager + ".json"
            : $"{modtager}-{Kort(slags)}.json");

    private static string Kort(Kuvertslags slags) => slags switch
    {
        Kuvertslags.Googlekalender => Googlekalender.Id,
        Kuvertslags.Googleopgaver => Googleopgaver.Id,
        _ => "api",
    };

    private static string Formaal(Kuvertslags slags) => slags switch
    {
        Kuvertslags.Apinoegle => "heypia-api-noegle-v1",
        _ => $"heypia-{Kort(slags)}-v1",
    };

    // ================================================================ hvad der sendes

    /// <summary>Det, der skal i kuverten. Tom, når der ikke er noget at sende.</summary>
    private static string Indhold(Kuvertslags slags) => slags switch
    {
        Kuvertslags.Apinoegle => SkyNoegle.Hent() ?? "",
        _ => Integrationsfiler.Hent(Kort(slags)).Opdateringsnoegle,
    };

    /// <summary>Er der allerede sådan en adgang på den her computer?</summary>
    public static bool Findes(Kuvertslags slags) => !string.IsNullOrWhiteSpace(Indhold(slags));

    /// <summary>Gemmer det, en kuvert bar, på den her computer.</summary>
    private static void Gem(Kuvertslags slags, string vaerdi)
    {
        if (slags == Kuvertslags.Apinoegle)
        {
            SkyNoegle.Gem(vaerdi);
            return;
        }

        var id = Kort(slags);

        // RESTEN AF OPSAETNINGEN SKRIVES FORFRA. «Sidst hentet» og «sidste
        // fejl» hoerte til den anden computer og siger intet om den her; de
        // faar deres vaerdi ved foerste hentning.
        Integrationsfiler.Gem(id, new Integrationsopsaetning { Opdateringsnoegle = vaerdi });

        // ============ HAKKET FOELGER MED ============
        //
        // «Dette er laest og forstaaet» er sat paa den maskine, forbindelsen
        // kom fra - af det samme menneske, med den samme Google-konto. At
        // skulle laese og kvittere for den samme tekst een gang til, fordi
        // man skiftede computer, er ikke et samtykke mere; det er en
        // forhindring, man klikker igennem uden at laese.
        //
        // Der skal to ting til, foer en forbindelse naar hertil: begge
        // maskiner har godkendt hinanden med koden, og et menneske har
        // trykket «Send opsaetningen». Ingen af delene sker af sig selv.
        AppSettings.Current.IntegrationerLaest = true;

        // Og der skal staa et maerkat paa fanen, til den har vaeret aabnet.
        // Ellers er den eneste maade at opdage forbindelsen paa at gaa ind og
        // kigge - og saa kunne man lige saa godt saette den op i haanden.
        AppSettings.Current.IntegrationerNyt = true;
        AppSettings.Current.Save();
    }

    // ==================================================================== send

    /// <summary>
    /// Lægger en adgang i en lukket kuvert til en parret computer.
    /// </summary>
    /// <returns>Sandt, hvis kuverten blev skrevet.</returns>
    /// <exception cref="InvalidOperationException">
    /// Hvis modtageren ikke er parret, eller der ikke er noget at sende.
    /// </exception>
    public static bool Send(Maskinoplysning modtager, Kuvertslags slags = Kuvertslags.Apinoegle)
    {
        if (Delt.Mappe is not { } rod)
            throw new InvalidOperationException("Der er ingen fælles mappe.");

        // ============ KUN TIL EN, DER ER GODKENDT ============
        //
        // Parringen ER tilladelsen. Uden den ved vi ikke, hvis noegle vi
        // krypterer til - og en adgang sendt til den forkerte er ikke
        // noget, man kan kalde tilbage.
        if (!Parring.MaaUdveksle(modtager))
            throw new InvalidOperationException(
                "Computeren er ikke godkendt. Sammenlign koden først.");

        var noegle = Indhold(slags);

        if (string.IsNullOrWhiteSpace(noegle))
            throw new InvalidOperationException(
                slags == Kuvertslags.Apinoegle
                    ? "Der er ingen API-nøgle på den her computer."
                    : "Der er ingen Google-forbindelse på den her computer.");

        var udloeber = DateTimeOffset.Now + Levetid;

        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);

        var laas = Laasenoegle(modtager.Noegle, salt, slags);

        var klar = Encoding.UTF8.GetBytes(noegle);
        var lukket = new byte[klar.Length];
        var maerke = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var gcm = new AesGcm(laas, maerke.Length))
            gcm.Encrypt(nonce, klar, lukket, maerke, Bundet(Maskinid.Id, modtager.Id, udloeber, slags));

        var kuvert = new Kuvert(
            Maskinid.Id, modtager.Id,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(maerke),
            Convert.ToBase64String(lukket),
            udloeber);

        Directory.CreateDirectory(Mappe(rod));

        var fil = Fil(rod, modtager.Id, slags);
        var midlertidig = fil + ".ny";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(kuvert, Format), new UTF8Encoding(false));
        File.Move(midlertidig, fil, overwrite: true);

        return true;
    }

    // ==================================================================== hent

    /// <summary>Hvad der skete, da der blev set efter en kuvert.</summary>
    public enum Udfald
    {
        /// <summary>Der lå ingen.</summary>
        Ingenting,

        /// <summary>Adgangen er hentet og gemt.</summary>
        Hentet,

        /// <summary>Der lå en, men den kunne ikke bruges. Den er ryddet.</summary>
        Afvist,

        /// <summary>
        /// Der ligger en fra en computer, vi ikke har godkendt endnu.
        /// </summary>
        /// <remarks>
        /// DEN BLIVER LIGGENDE. Før blev den ryddet som «afvist», og det var
        /// forkert: afsenderen er en maskine, der står i mappen, og det
        /// eneste, der mangler, er godkendelsen på DEN HER skærm. Kuverten
        /// bliver brugbar i samme øjeblik, der er trykket ja — og den
        /// udløber af sig selv efter en time.
        /// </remarks>
        Ikkegodkendt,
    }

    /// <summary>Ser efter kuverter af alle slags.</summary>
    public static IReadOnlyList<(Kuvertslags Slags, Udfald Udfald)> HentAlle() =>
        Slags.Select(s => (s, Hent(s)))
             .Where(x => x.Item2 != Udfald.Ingenting)
             .ToList();

    /// <summary>
    /// Ser efter en kuvert til den her computer, og gemmer indholdet, hvis den
    /// kan åbnes.
    /// </summary>
    /// <remarks>
    /// EN ADGANG, DER ALLEREDE ER SAT, OVERSKRIVES IKKE. Har man selv sat en
    /// op på den her maskine, er dét valget — og en kuvert, der lå fra i
    /// forgårs, må ikke stille og roligt bytte den ud.
    /// </remarks>
    public static Udfald Hent(Kuvertslags slags = Kuvertslags.Apinoegle)
    {
        if (Delt.Mappe is not { } rod) return Udfald.Ingenting;

        var fil = Fil(rod, Maskinid.Id, slags);

        Kuvert? kuvert;

        try
        {
            if (!File.Exists(fil)) return Udfald.Ingenting;

            kuvert = JsonSerializer.Deserialize<Kuvert>(File.ReadAllText(fil, Encoding.UTF8));
        }
        catch (Exception)
        {
            // En halvskrevet fil fra en synkronisering, der er i gang. Den er
            // hel om lidt, og saa proever vi igen.
            return Udfald.Ingenting;
        }

        if (kuvert is null) return Udfald.Ingenting;

        if (Findes(slags))
        {
            // Der ER en adgang i forvejen. Kuverten ryddes, saa den ikke
            // bliver liggende som en hemmelighed, ingen skal bruge.
            Ryd(fil);
            return Udfald.Ingenting;
        }

        var afsender = Delt.Alle().FirstOrDefault(m => m.Id == kuvert.Fra);

        if (afsender is null || kuvert.Udloeber < DateTimeOffset.Now)
        {
            Ryd(fil);
            return Udfald.Afvist;
        }

        // ============ VI HAR IKKE GODKENDT DEN ENDNU ============
        //
        // Afsenderen staar i mappen, og det eneste, der mangler, er et ja paa
        // den her skaerm. Kuverten bliver liggende - se Udfald.Ikkegodkendt.
        if (!Parring.MaaUdveksle(afsender)) return Udfald.Ikkegodkendt;

        try
        {
            var salt = Convert.FromBase64String(kuvert.Salt);
            var nonce = Convert.FromBase64String(kuvert.Nonce);
            var maerke = Convert.FromBase64String(kuvert.Maerke);
            var lukket = Convert.FromBase64String(kuvert.Indhold);

            var laas = Laasenoegle(afsender.Noegle, salt, slags);
            var klar = new byte[lukket.Length];

            using (var gcm = new AesGcm(laas, maerke.Length))
                gcm.Decrypt(nonce, lukket, maerke, klar,
                            Bundet(kuvert.Fra, kuvert.Til, kuvert.Udloeber, slags));

            var noegle = Encoding.UTF8.GetString(klar).Trim();

            if (noegle.Length == 0)
            {
                Ryd(fil);
                return Udfald.Afvist;
            }

            Gem(slags, noegle);
            Ryd(fil);

            return Udfald.Hentet;
        }
        catch (CryptographicException)
        {
            // MAERKET PASSEDE IKKE. Enten er der rettet i kuverten, eller ogsaa
            // er den skrevet af en anden end den, vi tror. Begge dele er den
            // samme beslutning: den bruges ikke, og den ryddes.
            Ryd(fil);
            return Udfald.Afvist;
        }
        catch (FormatException)
        {
            Ryd(fil);
            return Udfald.Afvist;
        }
    }

    /// <summary>Ligger der en kuvert til den her computer og venter?</summary>
    public static bool Venter(Kuvertslags slags = Kuvertslags.Apinoegle)
    {
        if (Delt.Mappe is not { } rod) return false;

        try { return File.Exists(Fil(rod, Maskinid.Id, slags)); }
        catch (Exception) { return false; }
    }

    /// <summary>Ligger der en, vi har sendt, som ikke er hentet endnu?</summary>
    public static bool Sendt(string modtagerId, Kuvertslags slags = Kuvertslags.Apinoegle)
    {
        if (Delt.Mappe is not { } rod) return false;

        try { return File.Exists(Fil(rod, modtagerId, slags)); }
        catch (Exception) { return false; }
    }

    /// <summary>Fortryder en kuvert, der ikke er hentet endnu.</summary>
    public static void Fortryd(string modtagerId, Kuvertslags slags = Kuvertslags.Apinoegle)
    {
        if (Delt.Mappe is not { } rod) return;

        Ryd(Fil(rod, modtagerId, slags));
    }

    // ================================================================= det indre

    /// <summary>
    /// Krypteringsnøglen til én kuvert.
    /// </summary>
    /// <remarks>
    /// Den fælles nøgle fra parringen er den samme hele tiden. HKDF med et
    /// nyt salt pr. kuvert giver hver besked sin egen nøgle, og formålet står
    /// med i udledningen, så den samme fælles nøgle kan bære flere slags
    /// beskeder uden at genbruge en nøgle på tværs.
    /// </remarks>
    private static byte[] Laasenoegle(string modpartensOffentlige, byte[] salt, Kuvertslags slags) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256,
                       Maskinid.Faellesnoegle(modpartensOffentlige),
                       32, salt, Encoding.UTF8.GetBytes(Formaal(slags)));

    /// <summary>
    /// Det, der er bundet til krypteringen uden at være krypteret.
    /// </summary>
    /// <remarks>
    /// Rettes afsender, modtager, udløb eller formål, kan kuverten ikke åbnes.
    /// Uden det kunne den, der kan skrive i mappen, flytte en kuvert til en
    /// anden modtager eller give den et nyt udløb.
    /// </remarks>
    private static byte[] Bundet(string fra, string til, DateTimeOffset udloeber, Kuvertslags slags) =>
        Encoding.UTF8.GetBytes($"{Formaal(slags)}|{fra}|{til}|{udloeber:o}");

    private static void Ryd(string fil)
    {
        try { if (File.Exists(fil)) File.Delete(fil); }
        catch (IOException) { /* laast lige nu. Den ryddes naeste gang. */ }
    }
}
