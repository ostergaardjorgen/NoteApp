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

    /// <summary>
    /// Hvem der har indkaldt til mødet. Tom på en aftale, man selv har lavet.
    ///
    /// HVORFOR DEN STÅR PÅ SKÆRMEN
    ///
    /// «Catch-up – Relation» siger ingenting om, hvem man skal tale med, og
    /// det gør klokkeslættet heller ikke. Arrangøren er tit det eneste i en
    /// hentet aftale, der forklarer, hvad mødet er — især på de titler, folk
    /// giver deres gentagne møder.
    ///
    /// Det er også dét, der afgør, om man skal sige noget om optagelsen: er
    /// man selv arrangør, er det ens eget ansvar at nævne det.
    ///
    /// Navnet gemmes, ikke slås op. Google oplyser det ved hentningen, og en
    /// aftale skal kunne læses, også når der ikke er forbindelse.
    /// </summary>
    public string Arrangoer { get; set; } = "";

    /// <summary>Er det brugeren selv, der har indkaldt?</summary>
    public bool ErEgetMoede { get; set; }

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

    /// <summary>
    /// Skal optagelsen starte af sig selv, når mødet begynder?
    ///
    /// DEN KRÆVER, AT APPEN KØRER. Der findes ingen vej udenom: en optagelse
    /// er en proces, der skal være i gang, og et program, der ikke kører, kan
    /// ikke starte den. Det står i indstillingerne, så det ikke bliver noget,
    /// man opdager den dag, et møde ikke blev optaget.
    ///
    /// FLYTTES MØDET HOS GOOGLE, FLYTTER OPTAGELSEN MED. Markeringen hænger på
    /// aftalen, ikke på et klokkeslæt — tidspunktet læses forfra ved hver
    /// hentning, og vagten kigger på det, der står nu.
    ///
    /// Feltet er appens eget. Det findes ikke hos Google, og det bæres derfor
    /// over ved hver hentning som mødetype, mappe og sprog.
    /// </summary>
    public bool OptagAutomatisk { get; set; }

    /// <summary>
    /// Sat, når vagten har startet optagelsen — så den ikke gør det igen.
    ///
    /// Uden den ville et møde, man kasserede efter to minutter, blive startet
    /// forfra ved næste kig på uret. Den nulstilles, hvis mødet flyttes til et
    /// nyt tidspunkt: så er det en ny lejlighed, ikke den samme igen.
    /// </summary>
    public DateTimeOffset? Startet { get; set; }

    public DateTimeOffset Slutter => Slut ?? Start.AddHours(1);

    public bool ErIGang(DateTimeOffset nu) => nu >= Start.AddMinutes(-5) && nu <= Slutter;

    /// <summary>
    /// Er mødet forbi? Vises grå, så det er tydeligt, at det er sket.
    ///
    /// Kun brugbart om dagens egne aftaler — dem fra i går står slet ikke i
    /// listen. Se <see cref="Kalender.Kommende"/>.
    /// </summary>
    public bool ErOverstaaet(DateTimeOffset nu) => Slutter < nu;

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
    /// <summary>
    /// Det, der skal ske — og det, der ALLEREDE er sket i dag.
    ///
    /// DAGENS OVERSTÅEDE MØDER BLIVER STÅENDE, TIL DAGEN ER SLUT.
    ///
    /// Første udgave fjernede en aftale i det øjeblik, den var forbi. Det er
    /// forkert: klokken to om eftermiddagen er spørgsmålet ikke kun «hvad
    /// mangler jeg», men også «hvad nåede jeg» — og et møde, der forsvinder
    /// fra listen, ser ud som et møde, der aldrig var der. Det gælder især
    /// det, man skulle have optaget og glemte.
    ///
    /// De vises grå, så det er tydeligt, at de er overstået. Se
    /// <see cref="Aftale.ErOverstaaet"/>.
    ///
    /// I MORGEN ER DE VÆK. En liste, der bærer i går med sig, er ikke en
    /// kalender; den er en historik, og den findes et andet sted.
    ///
    /// Maksimum tælles på de KOMMENDE. Har man haft fem møder i dag og har to
    /// tilbage, skal begge de to kunne ses — ellers ville en travl formiddag
    /// skubbe eftermiddagen ud af skærmen.
    /// </summary>
    public static List<Aftale> Kommende(DateTimeOffset nu, int maks = 6)
    {
        var alle = Alle();

        var overstaaet = alle
            .Where(a => a.Slutter < nu && a.Start.Date == nu.Date)
            .OrderBy(a => a.Start)
            .ToList();

        var fremad = alle.Where(a => a.Slutter >= nu).OrderBy(a => a.Start).ToList();

        var idag = fremad.Where(a => a.Start.Date == nu.Date).ToList();

        var valgte = idag.Count >= maks ? idag.Take(maks) : fremad.Take(maks);

        return overstaaet.Concat(valgte).ToList();
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
                ny.OptagAutomatisk = gammel.OptagAutomatisk;

                // FLYTTES MOEDET, ER DET EN NY LEJLIGHED.
                //
                // «Startet» huskes kun, saa laenge tidspunktet er det samme.
                // Rykker moedet en time, skal vagten optage paa det nye
                // tidspunkt - ellers ville en flytning stille og roligt
                // afmelde optagelsen, og det ville se ud som en fejl i vagten.
                ny.Startet = ny.Start == gammel.Start ? gammel.Startet : null;
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
