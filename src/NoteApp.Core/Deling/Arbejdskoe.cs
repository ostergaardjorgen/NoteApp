using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>Et stykke arbejde, der er lagt i den fælles mappe.</summary>
/// <param name="Id">Opgavens eget navn — også mappens navn.</param>
/// <param name="Fra">Maskinen, der bad om det.</param>
/// <param name="Til">Maskinen, der skal lave det. Tom betyder «den, der kan».</param>
/// <param name="Moede">Mødets id hjemme hos afsenderen. Det er dét, svaret skal hjem til.</param>
/// <param name="Spor">«mikrofon» eller «loopback» — et møde kan have to.</param>
/// <param name="Sprog">Sproget, der tales. «auto» lader modellen selv finde det.</param>
/// <param name="Model">Den model, afsenderen ville have brugt. Et ønske, ikke et krav.</param>
/// <param name="Ledetraad">Ordbogens ord, som de skulle sendes med til whisper.</param>
/// <param name="Lydsum">SHA-256 af lydfilen. Uden den kunne indholdet byttes ud.</param>
/// <param name="Sekunder">Lydens længde. Så modtageren kan sige, hvor længe det tager.</param>
/// <param name="Oprettet">Hvornår opgaven blev lagt.</param>
/// <param name="Maerke">Seglet. Se <see cref="Arbejdskoe"/>.</param>
public sealed record Arbejdsopgave(
    string Id,
    string Fra,
    string Til,
    string Moede,
    string Spor,
    string Sprog,
    string Model,
    string Ledetraad,
    string Lydsum,
    double Sekunder,
    DateTimeOffset Oprettet,
    string Maerke = "");

/// <summary>Den maskine, der har taget en opgave, og hvornår den sidst rørte den.</summary>
public sealed record Arbejdskrav(string Maskine, DateTimeOffset Taget, DateTimeOffset Hjerteslag);

/// <summary>Svaret på en opgave.</summary>
/// <param name="Sprog">Det sprog, modellen landede på.</param>
/// <param name="Motor">Motorens eget id — hvilken model der faktisk kørte.</param>
/// <param name="Fejl">Tom, når det gik godt. Ellers står der hvorfor.</param>
public sealed record Arbejdssvar(
    string Id,
    string Fra,
    string Til,
    string Sprog,
    string Motor,
    double Sekunder,
    string Fejl,
    DateTimeOffset Faerdig,
    string Maerke = "");

/// <summary>
/// Køen: den bærbare optager, den kraftige skriver ud.
/// </summary>
/// <remarks>
/// ============ HVAD DER RENT FAKTISK REJSER ============
///
/// Ét spor ad gangen: en lydfil ud, en udskrift hjem. Ikke mødet, ikke noterne,
/// ikke dokumenterne — de bliver, hvor de er. Mødet samles hjemme hos den, der
/// optog det, af den udskrift, den får tilbage.
///
/// Grunden er den samme som altid: den fælles mappe er en POSTKASSE. Det, der
/// ligger i den, er på vej et sted hen, og det ryddes, når det er kommet frem.
/// En datamappe, to maskiner synkroniserer, går i stykker — se
/// <see cref="Delt"/>.
///
/// ============ SEGLET ER DET, DER GØR DEN SIKKER ============
///
/// Den, der kan skrive i mappen, kan lægge en opgave og skrive «fra den
/// bærbare» på den. Uden et segl ville den kraftige maskine gå i gang med en
/// fremmeds lyd og skrive svaret et sted, hvor det kan læses.
///
/// Derfor mærkes både opgaven og svaret med en HMAC under en nøgle, der er
/// udledt af parringen — den, ingen af maskinerne har sendt nogen steder. Den
/// dækker ALLE felterne OG lydfilens sum: rettes ét tegn, eller byttes lyden
/// ud, passer seglet ikke, og opgaven røres ikke.
///
/// LYDEN ER IKKE KRYPTERET. Den ligger på din egen NAS, og delingsskærmen
/// siger lige ud, at den, der kan læse mappen, kan læse det, der ligger i den.
/// Seglet beskytter mod at få lagt arbejde IND — ikke mod at nogen kigger med.
/// Det er mappens egne rettigheder, der afgør det sidste.
///
/// ============ KRAVET ER EN FIL, DER KUN KAN LAVES ÉN GANG ============
///
/// To maskiner kan se den samme opgave. Den, der først kan oprette
/// <c>krav.json</c> med CreateNew, har den — filsystemet afgør det, ikke en
/// aftale mellem to programmer. Går maskinen ned midt i arbejdet, står kravet
/// stille, og opgaven bliver ledig igen efter
/// <see cref="Kravet_udloeber"/>.
/// </remarks>
public static class Arbejdskoe
{
    /// <summary>Mappen i delingen.</summary>
    public const string Mappenavn = "arbejde";

    /// <summary>Lydfilens navn i opgavemappen.</summary>
    public const string Lydnavn = "lyd.wav";

    /// <summary>Et krav, der ikke er rørt så længe, regnes som faldet.</summary>
    public static readonly TimeSpan Kravet_udloeber = TimeSpan.FromMinutes(30);

    /// <summary>En opgave, ingen har hentet svar på, ryddes efter så lang tid.</summary>
    public static readonly TimeSpan Levetid = TimeSpan.FromDays(2);

    private const string Formaal = "heypia-arbejde-v1";

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ==================================================================== stierne

    private static string Rod(string delt) => Path.Combine(delt, Mappenavn);

    /// <summary>Mappen for én opgave.</summary>
    public static string Mappe(string delt, string id) => Path.Combine(Rod(delt), id);

    public static string Lydsti(string delt, string id) => Path.Combine(Mappe(delt, id), Lydnavn);

    private static string Opgavefil(string delt, string id) => Path.Combine(Mappe(delt, id), "opgave.json");
    private static string Kravfil(string delt, string id) => Path.Combine(Mappe(delt, id), "krav.json");
    private static string Svarfil(string delt, string id) => Path.Combine(Mappe(delt, id), "svar.json");

    /// <summary>Udskriften, som den kommer hjem: whispers to filer.</summary>
    public static string Udskriftsti(string delt, string id, string endelse) =>
        Path.Combine(Mappe(delt, id), "udskrift" + endelse);

    // ===================================================================== læg op

    /// <summary>
    /// Lægger et spor i køen til en godkendt computer.
    /// </summary>
    /// <returns>Opgaven, som den blev skrevet.</returns>
    /// <exception cref="InvalidOperationException">
    /// Hvis der ikke deles, modtageren ikke er godkendt, eller lyden mangler.
    /// </exception>
    public static Arbejdsopgave Laeg(
        Maskinoplysning modtager, string moede, string spor, string lydfil,
        string sprog, string model, string ledetraad, double sekunder)
    {
        if (Delt.Mappe is not { } delt)
            throw new InvalidOperationException("Der er ingen fælles mappe.");

        if (!Parring.MaaUdveksle(modtager))
            throw new InvalidOperationException(
                "Computeren er ikke godkendt. Sammenlign koden først.");

        if (!File.Exists(lydfil))
            throw new FileNotFoundException("Der er ingen lydfil at sende.", lydfil);

        var id = Guid.NewGuid().ToString("N");
        var mappe = Mappe(delt, id);

        Directory.CreateDirectory(mappe);

        // LYDEN SKRIVES FÆRDIG, FØR OPGAVEN LÆGGES. Den anden maskine ser
        // efter opgave.json; ligger den, mens lyden stadig er på vej gennem
        // et netværksdrev, går den i gang med en halv fil.
        var midlertidig = Lydsti(delt, id) + ".ny";

        File.Copy(lydfil, midlertidig, overwrite: true);
        File.Move(midlertidig, Lydsti(delt, id), overwrite: true);

        var sum = Filsum(Lydsti(delt, id));

        var opgave = new Arbejdsopgave(
            id, Maskinid.Id, modtager.Id, moede, spor, sprog, model, ledetraad,
            sum, Math.Round(sekunder, 1), DateTimeOffset.Now);

        opgave = opgave with { Maerke = Segl(opgave, modtager.Noegle) };

        Skriv(Opgavefil(delt, id), opgave);

        return opgave;
    }

    // ================================================================== find arbejde

    /// <summary>
    /// Opgaver, der venter på DEN HER maskine, og som er til at stole på.
    /// </summary>
    /// <remarks>
    /// Der ses kun på opgaver fra en computer, vi selv har godkendt, og hvor
    /// seglet passer. Alt andet ligger urørt: at rydde op i noget, vi ikke
    /// forstår, er at slette en andens arbejde.
    /// </remarks>
    public static IReadOnlyList<Arbejdsopgave> Venter()
    {
        if (Delt.Mappe is not { } delt) return Array.Empty<Arbejdsopgave>();

        var ud = new List<Arbejdsopgave>();

        foreach (var opgave in Alle(delt))
        {
            if (opgave.Til.Length > 0 && opgave.Til != Maskinid.Id) continue;

            if (opgave.Fra == Maskinid.Id) continue;

            if (Afsender(opgave) is null) continue;

            if (Svar(opgave.Id) is not null) continue;

            if (Taget(delt, opgave.Id) is { } krav
                && DateTimeOffset.Now - krav.Hjerteslag < Kravet_udloeber) continue;

            ud.Add(opgave);
        }

        return ud.OrderBy(o => o.Oprettet).ToList();
    }

    /// <summary>Vores egne opgaver — dem, vi venter svar på.</summary>
    public static IReadOnlyList<Arbejdsopgave> Mine()
    {
        if (Delt.Mappe is not { } delt) return Array.Empty<Arbejdsopgave>();

        return Alle(delt).Where(o => o.Fra == Maskinid.Id)
                         .OrderBy(o => o.Oprettet)
                         .ToList();
    }

    private static IEnumerable<Arbejdsopgave> Alle(string delt)
    {
        var rod = Rod(delt);

        if (!Directory.Exists(rod)) yield break;

        foreach (var mappe in Directory.EnumerateDirectories(rod))
        {
            var fil = Path.Combine(mappe, "opgave.json");

            Arbejdsopgave? o = null;

            try
            {
                if (File.Exists(fil))
                    o = JsonSerializer.Deserialize<Arbejdsopgave>(File.ReadAllText(fil, Encoding.UTF8));
            }
            catch (Exception)
            {
                // En halvskrevet fil fra et drev, der stadig synkroniserer.
                // Den er hel om lidt.
            }

            if (o is not null) yield return o;
        }
    }

    /// <summary>Afsenderen, hvis den er godkendt OG seglet passer. Ellers null.</summary>
    public static Maskinoplysning? Afsender(Arbejdsopgave opgave)
    {
        var maskine = Delt.Alle().FirstOrDefault(m => m.Id == opgave.Fra);

        if (maskine is null || !Parring.MaaUdveksle(maskine)) return null;

        return Passer(opgave, maskine.Noegle) ? maskine : null;
    }

    // ===================================================================== tag den

    /// <summary>
    /// Tager en opgave. Sandt, hvis den blev vores.
    /// </summary>
    /// <remarks>
    /// CreateNew og ikke «findes filen?»: to maskiner, der kigger samtidig,
    /// ser begge en ledig opgave. Det er filsystemet, der afgør, hvem der får
    /// den — ikke rækkefølgen på to opslag.
    /// </remarks>
    public static bool Tag(Arbejdsopgave opgave)
    {
        if (Delt.Mappe is not { } delt) return false;

        var fil = Kravfil(delt, opgave.Id);

        try
        {
            var krav = new Arbejdskrav(Maskinid.Id, DateTimeOffset.Now, DateTimeOffset.Now);

            using var s = new FileStream(fil, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var w = new StreamWriter(s, new UTF8Encoding(false));

            w.Write(JsonSerializer.Serialize(krav, Format));

            return true;
        }
        catch (IOException)
        {
            // En anden har den. Eller et gammelt krav ligger der - saa
            // overtages den kun, naar hjerteslaget er stoppet.
            return Overtag(delt, fil, opgave);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool Overtag(string delt, string fil, Arbejdsopgave opgave)
    {
        var krav = Taget(delt, opgave.Id);

        if (krav is null) return false;

        if (krav.Maskine == Maskinid.Id) return true;

        if (DateTimeOffset.Now - krav.Hjerteslag < Kravet_udloeber) return false;

        // ============ DEN ANDEN GIK NED MIDT I DET ============
        //
        // Hjerteslaget er stoppet. Opgaven skal ikke blive liggende for evigt,
        // fordi en maskine blev lukket - saa ville den bruger sidde og vente
        // paa en udskrift, der aldrig kommer.
        try
        {
            Skriv(fil, new Arbejdskrav(Maskinid.Id, DateTimeOffset.Now, DateTimeOffset.Now));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Hvem har taget opgaven? Null, hvis den er ledig.</summary>
    public static Arbejdskrav? Taget(string delt, string id)
    {
        try
        {
            var fil = Kravfil(delt, id);

            if (!File.Exists(fil)) return null;

            return JsonSerializer.Deserialize<Arbejdskrav>(File.ReadAllText(fil, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Siger at vi stadig er i gang. Uden det bliver opgaven ledig igen.</summary>
    public static void Slaa(string id)
    {
        if (Delt.Mappe is not { } delt) return;

        var krav = Taget(delt, id);

        if (krav is null || krav.Maskine != Maskinid.Id) return;

        try { Skriv(Kravfil(delt, id), krav with { Hjerteslag = DateTimeOffset.Now }); }
        catch (Exception) { /* naeste slag om lidt. */ }
    }

    // ====================================================================== svar

    /// <summary>Lægger svaret — udskriften og hvordan det gik.</summary>
    public static void Svar(Arbejdsopgave opgave, string? tekstfil, string? jsonfil,
                           string sprog, string motor, double sekunder, string fejl = "")
    {
        if (Delt.Mappe is not { } delt) return;

        if (tekstfil is { Length: > 0 } t && File.Exists(t))
            File.Copy(t, Udskriftsti(delt, opgave.Id, ".txt"), overwrite: true);

        if (jsonfil is { Length: > 0 } j && File.Exists(j))
            File.Copy(j, Udskriftsti(delt, opgave.Id, ".json"), overwrite: true);

        var svar = new Arbejdssvar(
            opgave.Id, Maskinid.Id, opgave.Fra, sprog, motor,
            Math.Round(sekunder, 1), fejl, DateTimeOffset.Now);

        var modtager = Delt.Alle().FirstOrDefault(m => m.Id == opgave.Fra);

        if (modtager is not null)
            svar = svar with { Maerke = Segl(svar, modtager.Noegle) };

        Skriv(Svarfil(delt, opgave.Id), svar);

        // LYDEN RYDDES MED DET SAMME. Den har gjort sit, den fylder mest af
        // alt, og den er det eneste i mappen, nogen kan lytte til.
        Ryd(Lydsti(delt, opgave.Id));
    }

    /// <summary>Svaret på en opgave, hvis det er kommet — og hvis seglet passer.</summary>
    public static Arbejdssvar? Svar(string id)
    {
        if (Delt.Mappe is not { } delt) return null;

        Arbejdssvar? svar;

        try
        {
            var fil = Svarfil(delt, id);

            if (!File.Exists(fil)) return null;

            svar = JsonSerializer.Deserialize<Arbejdssvar>(File.ReadAllText(fil, Encoding.UTF8));
        }
        catch (Exception)
        {
            return null;
        }

        if (svar is null) return null;

        // ET SVAR SKAL OGSAA VAERE FRA DEN, DER SIGER DET. Ellers kunne en
        // tredje lægge en udskrift, der ikke er af vores lyd.
        var afsender = Delt.Alle().FirstOrDefault(m => m.Id == svar.Fra);

        if (afsender is null || !Parring.MaaUdveksle(afsender)) return null;

        return Passer(svar, afsender.Noegle) ? svar : null;
    }

    /// <summary>Fjerner en opgave med alt, hvad der ligger i den.</summary>
    public static void Fjern(string id)
    {
        if (Delt.Mappe is not { } delt) return;

        try
        {
            var mappe = Mappe(delt, id);

            if (Directory.Exists(mappe)) Directory.Delete(mappe, recursive: true);
        }
        catch (IOException)
        {
            // Laast lige nu - en anden maskine laeser maaske. Naeste gang.
        }
    }

    /// <summary>
    /// Rydder op efter det, ingen henter.
    /// </summary>
    /// <remarks>
    /// EN POSTKASSE, DER ALDRIG TOEMMES, ER ET LAGER. En bærbar, der blev
    /// væk, ville ellers efterlade sin lyd i mappen for altid.
    /// </remarks>
    public static int Ryd_gamle()
    {
        if (Delt.Mappe is not { } delt) return 0;

        var ryddet = 0;

        foreach (var opgave in Alle(delt).ToList())
        {
            if (DateTimeOffset.Now - opgave.Oprettet < Levetid) continue;

            Fjern(opgave.Id);
            ryddet++;
        }

        return ryddet;
    }

    // ================================================================== seglet

    /// <summary>Nøglen, seglet laves med. Én pr. modpart, aldrig sendt nogen steder.</summary>
    private static byte[] Seglnoegle(string modpartensOffentlige) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256,
                       Maskinid.Faellesnoegle(modpartensOffentlige),
                       32, null, Encoding.UTF8.GetBytes(Formaal));

    private static string Segl(Arbejdsopgave o, string modpartensOffentlige) =>
        Segl(Grundlag(o), modpartensOffentlige);

    private static string Segl(Arbejdssvar s, string modpartensOffentlige) =>
        Segl(Grundlag(s), modpartensOffentlige);

    private static string Segl(string grundlag, string modpartensOffentlige)
    {
        using var h = new HMACSHA256(Seglnoegle(modpartensOffentlige));

        return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(grundlag)));
    }

    /// <summary>Passer seglet? Sammenlignes tidskonstant.</summary>
    private static bool Passer(Arbejdsopgave o, string noegle) =>
        Passer(Grundlag(o), o.Maerke, noegle);

    private static bool Passer(Arbejdssvar s, string noegle) =>
        Passer(Grundlag(s), s.Maerke, noegle);

    private static bool Passer(string grundlag, string maerke, string noegle)
    {
        if (maerke.Length == 0) return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(Segl(grundlag, noegle)),
                Convert.FromBase64String(maerke));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Det, seglet dækker.
    /// </summary>
    /// <remarks>
    /// ALLE FELTER MED, OG LYDENS SUM MED. Uden summen kunne lydfilen byttes
    /// ud, mens opgaven stod uroert - og saa ville den kraftige maskine skrive
    /// en fremmeds lyd ud og lægge svaret et sted, det kan læses.
    /// </remarks>
    private static string Grundlag(Arbejdsopgave o) =>
        string.Join("|", Formaal, "opgave", o.Id, o.Fra, o.Til, o.Moede, o.Spor,
                    o.Sprog, o.Model, o.Ledetraad, o.Lydsum,
                    o.Sekunder.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    o.Oprettet.ToString("o"));

    private static string Grundlag(Arbejdssvar s) =>
        string.Join("|", Formaal, "svar", s.Id, s.Fra, s.Til, s.Sprog, s.Motor,
                    s.Sekunder.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    s.Fejl, s.Faerdig.ToString("o"));

    /// <summary>SHA-256 af en fil, som den står i opgaven.</summary>
    public static string Filsum(string sti)
    {
        using var s = File.OpenRead(sti);

        return Convert.ToBase64String(SHA256.HashData(s));
    }

    /// <summary>Er lyden den, opgaven blev skrevet om?</summary>
    public static bool Lyden_passer(string delt, Arbejdsopgave opgave)
    {
        try { return Filsum(Lydsti(delt, opgave.Id)) == opgave.Lydsum; }
        catch (Exception) { return false; }
    }

    // ================================================================== det indre

    private static void Skriv<T>(string fil, T hvad)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fil)!);

        var midlertidig = fil + ".ny";

        File.WriteAllText(midlertidig, JsonSerializer.Serialize(hvad, Format), new UTF8Encoding(false));
        File.Move(midlertidig, fil, overwrite: true);
    }

    private static void Ryd(string fil)
    {
        try { if (File.Exists(fil)) File.Delete(fil); }
        catch (IOException) { /* laast lige nu. */ }
    }
}
