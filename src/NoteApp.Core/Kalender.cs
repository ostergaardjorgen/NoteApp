using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>Hvor en aftale kommer fra.</summary>
public enum Kalenderkilde
{
    /// <summary>Oprettet i appen. Kan rettes og slettes her.</summary>
    Lokal,

    /// <summary>Hentet fra Google Kalender. Rettes dér, ikke her.</summary>
    Google,

    /// <summary>Hentet fra Microsoft 365. Rettes dér, ikke her.</summary>
    Microsoft
}

/// <summary>
/// En aftale i kalenderen.
///
/// FELTERNE ER FÅ, OG DET ER MED VILJE
///
/// Der er hvad, hvornår, hvor længe og et link. Ikke gentagelsesregler, ikke
/// svarstatus, ikke vedhæftninger. Appen er ikke en kalender — den er et sted,
/// hvor man kan se, hvad der skal optages i dag, og trykke på det.
///
/// Det, der SKAL kunne det hele, er Google og Microsoft. Derfor er de felter,
/// der findes her, netop dem, de to har til fælles — så en aftale, der kommer
/// derfra, kan vises uden at miste noget, man kan se på skærmen.
/// </summary>
public sealed record Aftale
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];

    public string Titel { get; set; } = "";

    public DateTimeOffset Start { get; set; }

    /// <summary>Slut. Er den ikke kendt, regnes der med en time.</summary>
    public DateTimeOffset? Slut { get; set; }

    /// <summary>Mødelinket — Teams, Meet, Zoom. Tomt ved et fysisk møde.</summary>
    public string Link { get; set; } = "";

    /// <summary>Stedet, hvis det er et fysisk møde.</summary>
    public string Sted { get; set; } = "";

    public Kalenderkilde Kilde { get; set; } = Kalenderkilde.Lokal;

    /// <summary>
    /// Aftalens id HOS leverandøren. Tomt for en lokal aftale.
    ///
    /// Den findes, fordi en synkronisering skal kunne genkende en aftale, den
    /// har hentet før. Uden den ville hver hentning lave dubletter af alt.
    /// </summary>
    public string FremmedId { get; set; } = "";

    /// <summary>
    /// Optagelsen, der kom ud af aftalen. Tom, indtil der er optaget.
    ///
    /// Det er DEN, der lukker ringen: kalenderen siger, at mødet var der,
    /// og optagelsen siger, hvad der blev sagt. Uden forbindelsen mellem dem
    /// er de to lister, man selv skal sammenholde.
    /// </summary>
    public string MoedeId { get; set; } = "";

    /// <summary>Mødetypen, valgt på forhånd. Følger med, når der optages.</summary>
    public string Moedetype { get; set; } = "";

    /// <summary>Mappen, optagelsen skal ligge i. Følger med, når der optages.</summary>
    public string Mappe { get; set; } = "";

    /// <summary>Sproget, der bliver talt. Følger med, når der optages.</summary>
    public string Sprog { get; set; } = "";

    /// <summary>Skal det optages som et webinar — ét spor?</summary>
    public bool ErWebinar { get; set; }

    public DateTimeOffset Slutter => Slut ?? Start.AddHours(1);

    public bool ErIGang(DateTimeOffset nu) => nu >= Start.AddMinutes(-5) && nu <= Slutter;

    /// <summary>Kan aftalen rettes her i appen?</summary>
    public bool KanRettes => Kilde == Kalenderkilde.Lokal;
}

/// <summary>
/// Kalenderen — appens egen, og det, der er hentet ind udefra.
///
/// DEN VIRKER UDEN INTEGRATION, OG DET ER IKKE EN OVERGANGSLØSNING
///
/// En kalender, der kræver en konto hos Google for at virke, er ubrugelig for
/// den, der ikke vil have en. Målgruppen er studerende og mindre selvstændige,
/// og en del af dem har hverken Google Workspace eller Microsoft 365.
///
/// Derfor er den lokale kalender den rigtige kalender, og de hentede aftaler
/// ligger side om side med den. Slår man en integration fra, forsvinder dens
/// aftaler — og ens egne bliver stående.
///
/// HVORFOR ALT LIGGER I ÉN FIL
///
/// Modsat opgaverne, som hører til hver sin optagelse, hører en aftale ikke
/// til noget. Den er noget, der skal ske — og først bagefter bliver den måske
/// til en optagelse. Én fil i datamappen, som følger med i sikkerhedskopien.
/// </summary>
public static class Kalender
{
    public static string Fil => Path.Combine(UserDataPaths.Root, "kalender.json");

    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static List<Aftale> Alle()
    {
        try
        {
            if (File.Exists(Fil))
                return JsonSerializer.Deserialize<List<Aftale>>(File.ReadAllText(Fil, Encoding.UTF8))
                       ?? new List<Aftale>();
        }
        catch (Exception)
        {
            // En oedelagt fil maa ikke forhindre appen i at starte. Saa er
            // kalenderen tom, og de hentede aftaler kommer igen ved naeste
            // synkronisering.
        }

        return new List<Aftale>();
    }

    private static void Gem(List<Aftale> aftaler)
    {
        Directory.CreateDirectory(UserDataPaths.Root);

        // Skriv til side og flyt paa plads, saa en afbrudt skrivning ikke
        // efterlader en halv fil.
        var midlertidig = Fil + ".ny";
        File.WriteAllText(midlertidig, JsonSerializer.Serialize(aftaler, Format),
                          new UTF8Encoding(false));
        File.Move(midlertidig, Fil, overwrite: true);
    }

    /// <summary>Aftalerne i et tidsrum, tidligst først.</summary>
    public static List<Aftale> IPerioden(DateTimeOffset fra, DateTimeOffset til) =>
        Alle().Where(a => a.Start >= fra && a.Start <= til)
              .OrderBy(a => a.Start)
              .ToList();

    /// <summary>
    /// Dagens aftaler — og de næste dages, hvis der ikke er flere i dag.
    ///
    /// EN TOM DAG SKAL IKKE VISES SOM EN TOM SKÆRM. Er dagen forbi, er det
    /// næste, man vil vide, hvad der kommer — ikke at der ikke er mere i dag.
    /// Derfor fyldes der op fremad, indtil der er noget at se på.
    /// </summary>
    public static List<Aftale> Kommende(DateTimeOffset nu, int maks = 6)
    {
        var alle = Alle().Where(a => a.Slutter >= nu).OrderBy(a => a.Start).ToList();

        var idag = alle.Where(a => a.Start.Date == nu.Date).ToList();

        return idag.Count >= maks ? idag.Take(maks).ToList() : alle.Take(maks).ToList();
    }

    public static void Gem(Aftale a)
    {
        var alle = Alle();
        var nr = alle.FindIndex(x => x.Id == a.Id);

        if (nr < 0) alle.Add(a);
        else alle[nr] = a;

        Gem(alle);
    }

    public static void Slet(string id)
    {
        var alle = Alle();
        alle.RemoveAll(a => a.Id == id);
        Gem(alle);
    }

    /// <summary>
    /// Lægger hentede aftaler ind fra en kilde og fjerner dem, der er væk dér.
    ///
    /// DET ER EN AFLØSNING, IKKE EN SAMMENFLETNING. Alt fra kilden erstattes,
    /// så en aftale, der er aflyst i Google, også forsvinder her. Alternativet
    /// — kun at lægge til — ville betyde, at en aflyst aftale blev stående for
    /// evigt, og så holder man op med at stole på listen.
    ///
    /// DET, BRUGEREN SELV HAR SAT PÅ, OVERLEVER. Mødetype, mappe og sprog er
    /// valgt her i appen og findes ikke hos leverandøren; de bæres over på den
    /// nye udgave af den samme aftale, genkendt på fremmed-id'et. Det samme
    /// gælder forbindelsen til en optagelse, der allerede er lavet.
    /// </summary>
    public static int Afloes(Kalenderkilde kilde, IEnumerable<Aftale> hentede)
    {
        var alle = Alle();

        var gamle = alle.Where(a => a.Kilde == kilde)
                        .ToDictionary(a => a.FremmedId, a => a, StringComparer.Ordinal);

        alle.RemoveAll(a => a.Kilde == kilde);

        // EGNE AFTALER, DER OGSAA ER LAGT OP, SKAL IKKE KOMME RETUR SOM EN
        // FREMMED AFTALE.
        //
        // Saetter man hak i «Opret ogsaa i Google», findes aftalen to steder:
        // her som en LOKAL aftale, og hos Google. Ved naeste hentning kommer
        // den tilbage - og uden det her ville den staa to gange i listen, med
        // den samme titel og det samme tidspunkt.
        //
        // Den lokale er den rigtige. Det er DEN, der kan rettes, og det er den,
        // moedetype, mappe og sprog haenger paa. Google har en kopi.
        var egneOppe = alle.Where(a => a.Kilde == Kalenderkilde.Lokal
                                    && a.FremmedId.Length > 0)
                           .Select(a => a.FremmedId)
                           .ToHashSet(StringComparer.Ordinal);

        var n = 0;

        foreach (var ny in hentede)
        {
            if (egneOppe.Contains(ny.FremmedId)) continue;

            if (gamle.TryGetValue(ny.FremmedId, out var gammel))
            {
                ny.Moedetype = gammel.Moedetype;
                ny.Mappe = gammel.Mappe;
                ny.Sprog = gammel.Sprog;
                ny.ErWebinar = gammel.ErWebinar;
                ny.MoedeId = gammel.MoedeId;
            }

            alle.Add(ny);
            n++;
        }

        Gem(alle);
        return n;
    }

    /// <summary>Fjerner alt fra én kilde. Kaldes, når en integration slås fra.</summary>
    public static void Fjern(Kalenderkilde kilde)
    {
        var alle = Alle();
        alle.RemoveAll(a => a.Kilde == kilde);
        Gem(alle);
    }
}
