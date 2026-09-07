using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NoteApp.Core.Deling;

/// <summary>Én ændring, som den står i journalen.</summary>
/// <param name="Slags">«aftale» eller «opgave».</param>
/// <param name="Id">Postens eget id — det, der genkender den på den anden maskine.</param>
/// <param name="Aendret">Hvornår ændringen skete. Den nyeste vinder.</param>
/// <param name="Slettet">Gravstenen. Uden den dukker en slettet aftale op igen.</param>
/// <param name="Data">Selve posten som JSON. Tom, når den er slettet.</param>
/// <param name="Maerke">Seglet. Se <see cref="Journal"/>.</param>
public sealed record Journalpost(
    string Slags,
    string Id,
    DateTimeOffset Aendret,
    bool Slettet,
    string Data,
    string Maerke = "");

/// <summary>
/// Aftaler og opgaver, der rejser mellem to computere.
/// </summary>
/// <remarks>
/// ============ DEN ER TIL DEM UDEN GOOGLE ============
///
/// Har man Google, henter begge maskiner fra det samme sted, og der er intet
/// at synkronisere. Har man ikke, er kalenderen og opgavelisten appens egne —
/// og så står en aftale, man lavede på den bærbare, kun dér.
///
/// ============ DER SKRIVES ALDRIG I ANDRES FILER ============
///
/// Hver maskine ejer sin egen journal og lægger kun til. To skrivere på den
/// samme fil gennem et synkroniseret drev bliver til en «conflicted copy», og
/// den slags opdager ingen. Det er den samme regel som for
/// <see cref="Delt"/>s maskinfiler.
///
/// Filen er én pr. modtager: <c>journal/&lt;fra&gt;/&lt;til&gt;.jsonl</c>. Det
/// er ikke for at skjule noget — det er, fordi seglet laves med den nøgle, de
/// TO maskiner deler. En tredje computer i den samme mappe har sin egen.
///
/// ============ DEN NYESTE VINDER ============
///
/// Bliver den samme aftale rettet begge steder, før de har talt sammen, kan
/// der kun være ét svar. Tidsstemplet på ændringen afgør det, og det står på
/// posten selv — ikke på filen, som et drev kan finde på at røre.
///
/// ============ DER LÆSES FREM, IKKE FORFRA ============
///
/// Hvor langt vi er nået i hver maskines journal står lokalt. Uden det ville
/// hver kørsel anvende hele historikken igen — og en aftale, man havde slettet
/// bagefter, ville komme tilbage hver gang.
/// </remarks>
public static class Journal
{
    /// <summary>Mappen i delingen.</summary>
    public const string Mappenavn = "journal";

    /// <summary>Poster ældre end det her tages ikke med. Journalen er ikke et arkiv.</summary>
    public static readonly TimeSpan Levetid = TimeSpan.FromDays(30);

    private const string Formaal = "heypia-journal-v1";

    /// <summary>
    /// Sat, mens en post fra den anden maskine lægges ind.
    /// </summary>
    /// <remarks>
    /// UDEN DEN SKREV VI DET, VI LIGE HAVDE LÆST, TILBAGE I VORES EGEN
    /// JOURNAL — og de to maskiner ville kaste den samme aftale frem og
    /// tilbage, så længe de begge var tændt.
    /// </remarks>
    [ThreadStatic]
    private static bool _anvender;

    /// <summary>Er vi i gang med at lægge en fremmed ændring ind?</summary>
    public static bool Anvender => _anvender;

    private static readonly JsonSerializerOptions Format = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ==================================================================== stierne

    private static string Rod(string delt) => Path.Combine(delt, Mappenavn);

    private static string Fil(string delt, string fra, string til) =>
        Path.Combine(Rod(delt), fra, til + ".jsonl");

    private static string Laestfil =>
        Path.Combine(UserDataPaths.Maskinrod, "deling", "journal-laest.json");

    // ===================================================================== skriv

    /// <summary>
    /// Skriver en ændring til de computere, vi deler med.
    /// </summary>
    /// <remarks>
    /// DEN MÅ ALDRIG KUNNE VÆLTE EN GEMNING. Er drevet væk, eller er der ingen
    /// deling, sker der ingenting — aftalen er gemt lokalt, og det er dét, der
    /// betyder noget. Journalen er en bekvemmelighed, ikke en betingelse.
    /// </remarks>
    public static void Skriv(string slags, string id, string? data, bool slettet)
    {
        if (_anvender) return;

        // ============ DEMODATA REJSER IKKE ============
        //
        // Peger appen et andet sted hen end brugerens egen datamappe, er det
        // ikke hans kalender, der aendrer sig - og saa har vi intet at
        // fortaelle den anden maskine. Se UserDataPaths.EgneData for, hvad det
        // kostede at opdage.
        if (!UserDataPaths.EgneData) return;

        try
        {
            if (Delt.Mappe is not { } delt || !Delt.Er(delt)) return;

            var modtagere = Delt.Andre().Where(Parring.MaaUdveksle).ToList();

            if (modtagere.Count == 0) return;

            var post = new Journalpost(slags, id, DateTimeOffset.Now, slettet, data ?? "");

            foreach (var m in modtagere)
            {
                var fil = Fil(delt, Maskinid.Id, m.Id);

                Directory.CreateDirectory(Path.GetDirectoryName(fil)!);

                var linje = JsonSerializer.Serialize(
                    post with { Maerke = Segl(post, m.Noegle) }, Format);

                // TILFOEJES, ikke skrives. Filen er vores egen, og den anden
                // maskine laeser den samtidig.
                File.AppendAllText(fil, linje + "\n", new UTF8Encoding(false));
            }
        }
        catch (Exception)
        {
            // Et drev, der ikke svarer. Aendringen staar lokalt, og den naeste
            // aendring paa den samme post tager den med sig.
        }
    }

    // ====================================================================== læs

    /// <summary>Det, en post skal gøre ved den her maskine.</summary>
    public sealed record Aendring(string Slags, string Id, bool Slettet, string Data);

    /// <summary>
    /// Læser det nye fra de andre maskiners journaler.
    /// </summary>
    /// <remarks>
    /// Der læses KUN fra maskiner, vi har godkendt, og kun poster, hvis segl
    /// passer. En linje, der ikke gør, springes over — den ryddes ikke: filen
    /// er en andens, og vi skriver ikke i andres filer.
    /// </remarks>
    public static IReadOnlyList<Aendring> Nyt()
    {
        var ud = new List<Aendring>();

        // ============ OG DEMOEN LAESER HELLER IKKE ============
        //
        // Laesepositionen ligger i BRUGERENS maskinmappe, ogsaa mens demoen
        // koerer. Laeste demoen journalen, ville den rykke positionen frem for
        // brugeren - og hans rigtige app ville springe de poster over FOR
        // ALTID. En aftale, der aldrig kom frem, og ingen fejl at lede efter.
        if (!UserDataPaths.EgneData) return ud;

        if (Delt.Mappe is not { } delt) return ud;

        var laest = Laest();

        foreach (var m in Delt.Andre().Where(Parring.MaaUdveksle))
        {
            var fil = Fil(delt, m.Id, Maskinid.Id);

            if (!File.Exists(fil)) continue;

            string[] linjer;

            try { linjer = File.ReadAllLines(fil, Encoding.UTF8); }
            catch (Exception) { continue; }

            var fra = laest.TryGetValue(m.Id, out var n) ? n : 0;

            // ============ EN JOURNAL, DER ER BLEVET KORTERE ============
            //
            // Er den beskaaret eller lavet forfra, staar vores taeller for
            // langt fremme. Saa laeses der forfra frem for at springe alt
            // over for evigt.
            if (fra > linjer.Length) fra = 0;

            for (var i = fra; i < linjer.Length; i++)
            {
                if (Post(linjer[i], m.Noegle) is not { } post) continue;

                if (DateTimeOffset.Now - post.Aendret > Levetid) continue;

                ud.Add(new Aendring(post.Slags, post.Id, post.Slettet, post.Data));
            }

            laest[m.Id] = linjer.Length;
        }

        Gemlaest(laest);

        return ud;
    }

    /// <summary>Kører en ændring ind uden at journalisere den videre.</summary>
    /// <remarks>
    /// Kalderen giver selv handlingen. Journalen kender hverken kalenderen
    /// eller opgavelisten — den bærer poster, den ved ikke hvad de betyder.
    /// </remarks>
    public static void Anvend(Action hvad)
    {
        _anvender = true;

        try { hvad(); }
        finally { _anvender = false; }
    }

    private static Journalpost? Post(string linje, string noegle)
    {
        try
        {
            var post = JsonSerializer.Deserialize<Journalpost>(linje);

            if (post is null || post.Maerke.Length == 0) return null;

            return Passer(post, noegle) ? post : null;
        }
        catch (Exception)
        {
            // En halvskrevet linje fra en fil, der stadig synkroniseres. Den
            // er hel naeste gang - og taelleren er ikke rykket forbi den.
            return null;
        }
    }

    // ================================================================ hvor langt

    private static Dictionary<string, int> Laest()
    {
        try
        {
            if (!File.Exists(Laestfil)) return new Dictionary<string, int>();

            return JsonSerializer.Deserialize<Dictionary<string, int>>(
                       File.ReadAllText(Laestfil, Encoding.UTF8))
                   ?? new Dictionary<string, int>();
        }
        catch (Exception)
        {
            return new Dictionary<string, int>();
        }
    }

    private static void Gemlaest(Dictionary<string, int> hvor)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Laestfil)!);

            var midlertidig = Laestfil + ".ny";

            File.WriteAllText(midlertidig, JsonSerializer.Serialize(hvor, Format),
                              new UTF8Encoding(false));

            File.Move(midlertidig, Laestfil, overwrite: true);
        }
        catch (Exception)
        {
            // Kan den ikke skrives, laeses de samme linjer igen naeste gang.
            // Det er ubehageligt, men ikke farligt: den nyeste vinder, og en
            // post, der allerede er lagt ind, ender som sig selv.
        }
    }

    // ================================================================== seglet

    private static byte[] Seglnoegle(string modpartensOffentlige) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256,
                       Maskinid.Faellesnoegle(modpartensOffentlige),
                       32, null, Encoding.UTF8.GetBytes(Formaal));

    private static string Segl(Journalpost p, string modpartensOffentlige)
    {
        using var h = new HMACSHA256(Seglnoegle(modpartensOffentlige));

        return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(Grundlag(p))));
    }

    private static bool Passer(Journalpost p, string noegle)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(Segl(p, noegle)),
                Convert.FromBase64String(p.Maerke));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Alt i posten er dækket — også selve indholdet.</summary>
    private static string Grundlag(Journalpost p) =>
        string.Join("|", Formaal, p.Slags, p.Id, p.Aendret.ToString("o"), p.Slettet, p.Data);
}
