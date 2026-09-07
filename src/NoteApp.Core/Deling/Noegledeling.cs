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
/// Sender API-nøglen fra den ene af to parrede computere til den anden.
/// </summary>
/// <remarks>
/// ============ HVORFOR DEN FINDES ============
///
/// Nøglen til Mistral hører til maskinen, ikke til datasættet — der ligger
/// derfor ikke én i den fælles mappe, som begge kan læse. Men når man lige har
/// stået og sammenlignet seks cifre på to skærme for at sige «det er mine to
/// computere», er det en dårlig belønning at skulle finde nøglen frem og taste
/// den ind en gang til.
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
/// AFSENDER, MODTAGER OG UDLØB ER BUNDET TIL KRYPTERINGEN. De står som
/// «yderligere data» i GCM: rettes ét tegn i dem, kan kuverten ikke åbnes.
/// Uden det kunne en kuvert flyttes til en anden modtager eller få forlænget
/// sit udløb af den, der kan skrive i mappen.
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
/// maskine, der HAR nøglen. At kopiere en adgangsnøgle rundt uden at nogen har
/// bedt om det er ikke en bekvemmelighed — det er en overraskelse.
/// </remarks>
public static class Noegledeling
{
    /// <summary>Hvor længe en kuvert kan åbnes.</summary>
    public static readonly TimeSpan Levetid = TimeSpan.FromHours(1);

    private const string Formaal = "heypia-api-noegle-v1";

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Mappe(string rod) => Path.Combine(rod, "noegler");

    private static string Fil(string rod, string modtager) =>
        Path.Combine(Mappe(rod), modtager + ".json");

    // ==================================================================== send

    /// <summary>
    /// Lægger nøglen i en lukket kuvert til en parret computer.
    /// </summary>
    /// <returns>Sandt, hvis kuverten blev skrevet.</returns>
    /// <exception cref="InvalidOperationException">
    /// Hvis modtageren ikke er parret, eller der ikke er en nøgle at sende.
    /// </exception>
    public static bool Send(Maskinoplysning modtager)
    {
        if (Delt.Mappe is not { } rod)
            throw new InvalidOperationException("Der er ingen fælles mappe.");

        // ============ KUN TIL EN, DER ER GODKENDT ============
        //
        // Parringen ER tilladelsen. Uden den ved vi ikke, hvis noegle vi
        // krypterer til - og en API-noegle sendt til den forkerte er ikke
        // noget, man kan kalde tilbage.
        if (!Parring.MaaUdveksle(modtager))
            throw new InvalidOperationException(
                "Computeren er ikke godkendt. Sammenlign koden først.");

        var noegle = SkyNoegle.Hent();

        if (string.IsNullOrWhiteSpace(noegle))
            throw new InvalidOperationException("Der er ingen API-nøgle på den her computer.");

        var udloeber = DateTimeOffset.Now + Levetid;

        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);

        var laas = Laasenoegle(modtager.Noegle, salt);

        var klar = Encoding.UTF8.GetBytes(noegle);
        var lukket = new byte[klar.Length];
        var maerke = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var gcm = new AesGcm(laas, maerke.Length))
            gcm.Encrypt(nonce, klar, lukket, maerke, Bundet(Maskinid.Id, modtager.Id, udloeber));

        var kuvert = new Kuvert(
            Maskinid.Id, modtager.Id,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(maerke),
            Convert.ToBase64String(lukket),
            udloeber);

        Directory.CreateDirectory(Mappe(rod));

        var fil = Fil(rod, modtager.Id);
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

        /// <summary>Nøglen er hentet og gemt.</summary>
        Hentet,

        /// <summary>Der lå en, men den kunne ikke bruges. Den er ryddet.</summary>
        Afvist,
    }

    /// <summary>
    /// Ser efter en kuvert til den her computer, og gemmer nøglen, hvis den
    /// kan åbnes.
    /// </summary>
    /// <remarks>
    /// EN NØGLE, DER ALLEREDE ER SAT, OVERSKRIVES IKKE. Har man selv tastet
    /// en ind på den her maskine, er dét valget — og en kuvert, der lå fra i
    /// forgårs, må ikke stille og roligt bytte den ud.
    /// </remarks>
    public static Udfald Hent()
    {
        if (Delt.Mappe is not { } rod) return Udfald.Ingenting;

        var fil = Fil(rod, Maskinid.Id);

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

        if (!string.IsNullOrWhiteSpace(SkyNoegle.Hent()))
        {
            // Der ER en noegle i forvejen. Kuverten ryddes, saa den ikke
            // bliver liggende som en hemmelighed, ingen skal bruge.
            Ryd(fil);
            return Udfald.Ingenting;
        }

        var afsender = Delt.Alle().FirstOrDefault(m => m.Id == kuvert.Fra);

        if (afsender is null || !Parring.MaaUdveksle(afsender) || kuvert.Udloeber < DateTimeOffset.Now)
        {
            Ryd(fil);
            return Udfald.Afvist;
        }

        try
        {
            var salt = Convert.FromBase64String(kuvert.Salt);
            var nonce = Convert.FromBase64String(kuvert.Nonce);
            var maerke = Convert.FromBase64String(kuvert.Maerke);
            var lukket = Convert.FromBase64String(kuvert.Indhold);

            var laas = Laasenoegle(afsender.Noegle, salt);
            var klar = new byte[lukket.Length];

            using (var gcm = new AesGcm(laas, maerke.Length))
                gcm.Decrypt(nonce, lukket, maerke, klar, Bundet(kuvert.Fra, kuvert.Til, kuvert.Udloeber));

            var noegle = Encoding.UTF8.GetString(klar).Trim();

            if (noegle.Length == 0)
            {
                Ryd(fil);
                return Udfald.Afvist;
            }

            SkyNoegle.Gem(noegle);
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
    public static bool Venter()
    {
        if (Delt.Mappe is not { } rod) return false;

        try { return File.Exists(Fil(rod, Maskinid.Id)); }
        catch (Exception) { return false; }
    }

    /// <summary>Ligger der en, vi har sendt, som ikke er hentet endnu?</summary>
    public static bool Sendt(string modtagerId)
    {
        if (Delt.Mappe is not { } rod) return false;

        try { return File.Exists(Fil(rod, modtagerId)); }
        catch (Exception) { return false; }
    }

    /// <summary>Fortryder en kuvert, der ikke er hentet endnu.</summary>
    public static void Fortryd(string modtagerId)
    {
        if (Delt.Mappe is not { } rod) return;

        Ryd(Fil(rod, modtagerId));
    }

    // ================================================================= det indre

    /// <summary>
    /// Krypteringsnøglen til én kuvert.
    /// </summary>
    /// <remarks>
    /// Den fælles nøgle fra parringen er den samme hele tiden. HKDF med et
    /// nyt salt pr. kuvert giver hver besked sin egen nøgle, og formålet står
    /// med i udledningen, så den samme fælles nøgle kan bære andre slags
    /// beskeder senere uden at genbruge en nøgle på tværs.
    /// </remarks>
    private static byte[] Laasenoegle(string modpartensOffentlige, byte[] salt) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256,
                       Maskinid.Faellesnoegle(modpartensOffentlige),
                       32, salt, Encoding.UTF8.GetBytes(Formaal));

    /// <summary>
    /// Det, der er bundet til krypteringen uden at være krypteret.
    /// </summary>
    /// <remarks>
    /// Rettes afsender, modtager eller udløb, kan kuverten ikke åbnes. Uden
    /// det kunne den, der kan skrive i mappen, flytte en kuvert til en anden
    /// modtager eller give den et nyt udløb.
    /// </remarks>
    private static byte[] Bundet(string fra, string til, DateTimeOffset udloeber) =>
        Encoding.UTF8.GetBytes($"{Formaal}|{fra}|{til}|{udloeber:o}");

    private static void Ryd(string fil)
    {
        try { if (File.Exists(fil)) File.Delete(fil); }
        catch (IOException) { /* laast lige nu. Den ryddes naeste gang. */ }
    }
}
