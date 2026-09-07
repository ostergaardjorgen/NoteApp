using System.Text;
using System.Text.Json;
using NoteApp.Core.Documents;
using NoteApp.Core.Llm;

namespace NoteApp.Core;

/// <summary>
/// Bygger det datasæt, demotilstanden viser.
/// </summary>
/// <remarks>
/// ============ DET SKRIVES GENNEM APPENS EGNE LAGRE ============
///
/// Ikke som håndskrevet JSON. En generator, der selv kender filformaterne,
/// holder til den dag, et felt bliver omdøbt — og så viser demoen noget andet
/// end appen. Her kaldes <see cref="MeetingStore"/>, <see cref="Kalender"/>,
/// <see cref="Opgavelager"/> og de øvrige, præcis som skærmene gør.
///
/// ============ DATOERNE ER RELATIVE ============
///
/// Alt regnes ud fra i dag: møderne ligger de sidste tre uger, aftalerne i den
/// kommende. En demo med faste datoer ser forladt ud et halvt år senere — og
/// «senest ændret» er dét, listerne sorterer efter.
///
/// ============ INTET AF BRUGERENS EGET KOMMER MED ============
///
/// Her blev der klippet to korte lydeksempler ud af brugerens egne webinarer,
/// så demoen havde lyd. Det er taget ud igen 07-09-2026.
///
/// Grunden er, hvad en demo ER: noget, man viser frem. I det øjeblik den
/// indeholder et stykke af en rigtig optagelse, skal den, der trykker på
/// knappen, tænke over, hvem der lytter med — og det er præcis det, en demo
/// ikke skal kræve. Den skal kunne vises til hvem som helst uden at nogen har
/// set efter først.
///
/// Møderne har derfor udskrift, noter og dokumenter, men ingen lydfil. Det
/// står som en prøve, så ingen kommer til at lægge lyd ind igen ved et uheld.
/// </remarks>
public static class Demodata
{
    /// <summary>
    /// Hvilken udgave af demoen der ligger i mappen.
    /// </summary>
    /// <remarks>
    /// Taelles op, naar indholdet aendres. Saa bygges demoen forfra ved naeste
    /// start i stedet for at staa med gaarsdagens eksempler i en ny app.
    /// </remarks>
    public const int Udgave = 4;

    private static string Maerkefil => Path.Combine(UserDataPaths.Root, "demodata.json");

    private sealed record Maerke(int Udgave, DateTimeOffset Bygget);

    /// <summary>Ligger der allerede en demo af den her udgave?</summary>
    public static bool Findes
    {
        get
        {
            try
            {
                return File.Exists(Maerkefil)
                    && JsonSerializer.Deserialize<Maerke>(File.ReadAllText(Maerkefil))?.Udgave == Udgave;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Bygger demoen, hvis den ikke er der. Skriver dér, hvor
    /// <see cref="UserDataPaths.Root"/> peger hen — så den skal kaldes af en
    /// app, der ALLEREDE står i demotilstand.
    /// </summary>
    public static bool Byg(bool tvungen = false)
    {
        if (Findes && !tvungen) return false;

        // ET HALVT DATASAET ER VAERRE END INTET. Bygges der forfra, ryddes
        // det gamle vaek foerst - ellers ville en aendret demo ligge oven i
        // den forrige med dubletter af alt.
        Ryd();

        UserDataPaths.EnsureCreated();

        Indstillingerne();

        var idag = DateTimeOffset.Now.Date;

        Mapperne();
        DraftStore.SeedTemplates();

        var moeder = Moederne(idag);

        Dokumenterne(moeder);
        Projekterne(moeder, idag);
        Aftalerne(idag);
        Opgaverne(idag, moeder);
        Diktaterne(idag);
        Historikken(moeder);

        File.WriteAllText(Maerkefil,
            JsonSerializer.Serialize(new Maerke(Udgave, DateTimeOffset.Now),
                new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        return true;
    }

    /// <summary>Rydder alt i demomappen undtagen selve flaget, der tændte den.</summary>
    private static void Ryd()
    {
        if (!Directory.Exists(UserDataPaths.Root)) return;

        foreach (var mappe in Directory.GetDirectories(UserDataPaths.Root))
        {
            try { Directory.Delete(mappe, recursive: true); }
            catch (IOException) { /* en aaben fil. Resten ryddes. */ }
        }

        foreach (var fil in Directory.GetFiles(UserDataPaths.Root))
        {
            // Flaget skal blive: slettes det, er demoen slukket midt i, at
            // den bygges - og appen finder tilbage til brugerens egne data
            // med en halv demo bag sig.
            if (Path.GetFileName(fil).StartsWith("demo-er-taendt", StringComparison.OrdinalIgnoreCase))
                continue;

            try { File.Delete(fil); }
            catch (IOException) { /* som ovenfor */ }
        }
    }

    // ============================================================== indstillinger

    /// <summary>
    /// Giver demoen brugerens egne indstillinger — og markerer den som sat op.
    /// </summary>
    /// <remarks>
    /// ============ DEMOEN SKAL LIGNE DIN APP ============
    ///
    /// Indstillingerne ligger i datamappen, og demoen har sin egen. Uden det
    /// her ville demoen åbne i det lyse tema med en anden genvejstast og en
    /// anden mikrofon end den, man lige stod med — og så viser den ikke, hvad
    /// den skulle vise: den samme app med noget i.
    ///
    /// ============ OG DEN SKAL IKKE BEDE OM AT BLIVE SAT OP ============
    ///
    /// <c>SetupCompleted</c> er falsk i en frisk mappe, og så kom
    /// velkomstforløbet — med et tilbud om at hente 3,6 GB motor og model ned
    /// til en demo, der ikke skal transskribere noget. Motoren ligger nu under
    /// <see cref="UserDataPaths.Maskinrod"/> og er der i forvejen; det eneste,
    /// der manglede, var at sige, at appen ER sat op. Set 06-09-2026.
    /// </remarks>
    private static void Indstillingerne()
    {
        var fra = Path.Combine(UserDataPaths.Maskinrod, "indstillinger.json");
        var til = Path.Combine(UserDataPaths.Root, "indstillinger.json");

        // Er de to den samme fil, er vi ikke i en demo - og saa skal der ikke
        // kopieres noget oven i sig selv.
        if (!string.Equals(fra, til, StringComparison.OrdinalIgnoreCase) && File.Exists(fra))
        {
            try { File.Copy(fra, til, overwrite: true); }
            catch (IOException) { /* saa faar demoen standarden. Den virker ogsaa. */ }
        }

        AppSettings.Reload();

        AppSettings.Current.SetupCompleted = true;
        AppSettings.Current.Save();
    }

    // ==================================================================== mapper

    private static void Mapperne()
    {
        foreach (var m in new[] { "Kunder", "Møder", "Webinarer", "Undervisning" })
            Mapper.Opret(Mapper.Slags.Optagelser, m);

        foreach (var m in new[] { "Kunder", "Referater" })
            Mapper.Opret(Mapper.Slags.Dokumenter, m);
    }

    // ================================================================ optagelser

    /// <summary>Ét demomøde: hvad der blev sagt, og hvem der sagde det.</summary>
    private sealed record Demomoede(
        int DageSiden,
        int Time,
        int Minut,
        string Titel,
        string Mappe,
        string Moedetype,
        MeetingType Slags,
        string Modpart,
        (bool Herfra, string Tekst)[] Replikker,
        (int Sekund, string Note)[] Noter);

    /// <summary>Den, der optager. Står som HERFRA i udskriften.</summary>
    private const string Mig = "Jørgen";

    private static Demomoede[] Moedeliste() =>
    [
        new(18, 9, 0, "Opstartsmøde med Bakkegården", "Kunder", "Mødereferat",
            MeetingType.Online, "Mette",
            [
                (false, "Tak fordi du ville tage mødet. Vi står med et system, der er vokset ud af regnearket, og vi ved ikke helt, hvor vi skal begynde."),
                (true, "Det er et godt sted at begynde. Hvor mange er I, der arbejder i det til daglig?"),
                (false, "Vi er fjorten i alt, men det er kun fire, der taster. Resten kigger med."),
                (true, "Så er det de fires arbejdsgang, vi skal forstå først. Hvad er det, der tager længst tid?"),
                (false, "Det er bookingen. Vi har tre kalendere, og de taler ikke sammen. Én i Outlook, én på papir i køkkenet, og så det regneark."),
                (true, "Papiret i køkkenet er ikke et problem, det er et symptom. Nogen har haft brug for at kunne se det, uden at logge ind."),
                (false, "Det er præcis det, Kirsten siger. Hun vil ikke have en skærm i køkkenet."),
                (true, "Så skal løsningen kunne skrives ud på et A3-ark hver morgen. Det er en beslutning, ikke en teknisk detalje."),
                (false, "Hvad koster sådan noget cirka?"),
                (true, "Jeg vil ikke gætte i dag. Lad mig se de tre kalendere, og så sender jeg et tilbud i næste uge med en fast pris på analysen."),
                (false, "Hvad har du brug for fra os?"),
                (true, "Adgang til Outlook-kalenderen som gæst, en kopi af regnearket, og en halv time med Kirsten. Det sidste er det vigtigste."),
                (false, "Det kan jeg få på plads i denne uge. Skal vi sige, at vi mødes igen om fjorten dage?"),
                (true, "Ja. Så har jeg et udkast med, og så kan vi tale om, hvad der skal med i første omgang, og hvad der kan vente."),
            ],
            [
                (95, "Tre kalendere: Outlook, papir i køkkenet, regneark"),
                (410, "Kirsten vil IKKE have skærm i køkkenet — A3-udskrift hver morgen"),
                (600, "Fast pris på analysen. Intet gæt på totalen i dag."),
            ]),

        new(16, 13, 30, "Webinar: Persondata i små virksomheder", "Webinarer", "Webinar",
            MeetingType.Webinar, "Oplægsholder",
            [
                (false, "Velkommen til. Vi skal en time igennem det, de fleste små virksomheder faktisk bliver spurgt om, når der kommer en henvendelse."),
                (false, "Den første misforståelse er, at man skal have en fortegnelse, fordi loven siger det. Man skal have den, fordi man ikke kan svare på noget som helst uden den."),
                (false, "Får I en anmodning om indsigt, har I en måned. Uden en fortegnelse går de første to uger med at finde ud af, hvor tingene ligger."),
                (false, "Nummer to: databehandleraftaler. Alle jeres leverandører, der kan se personoplysninger, skal have en. Det gælder også bogholderen."),
                (false, "Og det gælder den, der laver jeres hjemmeside, hvis der er en formular på den."),
                (false, "Nummer tre, og det er den, der koster penge: sletning. I skal kunne sige, hvornår noget slettes, og det skal faktisk ske."),
                (false, "En sletteplan på et stykke papir, der aldrig er kørt, er værre end ingen — for så har I skrevet ned, at I ikke gør det, I siger."),
                (false, "Til sidst: hvis I bruger AI-værktøjer, så find ud af, hvor de kører henne. Kører de i EU, er samtalen kort. Gør de ikke, er den lang."),
                (false, "Der ligger en tjekliste på de fire punkter i materialet bagefter. Tak fordi I lyttede med."),
            ],
            [
                (140, "Fortegnelse: ikke for lovens skyld — for at kunne svare inden for en måned"),
                (520, "Databehandleraftale skal også dække bogholderen"),
                (960, "AI-værktøjer: find ud af hvor de kører henne"),
            ]),

        new(11, 10, 15, "Statusmøde med Bakkegården", "Kunder", "Mødereferat",
            MeetingType.Online, "Mette",
            [
                (true, "Jeg har set på de tre kalendere nu. Der er mere overlap, end I selv tror."),
                (false, "På hvilken måde?"),
                (true, "Fyrre procent af det, der står i regnearket, står også i Outlook. Det er dobbeltarbejde, ikke tre systemer."),
                (false, "Så kan vi droppe regnearket?"),
                (true, "Ikke endnu. Der er noget i regnearket, der ikke findes i Outlook: hvem der har nøglen. Det skal med et sted, før I lukker det."),
                (false, "Det er Kirstens kolonne. Hun kalder den «K»."),
                (true, "Så hedder feltet «Nøgleansvarlig» i den nye løsning, og det er ikke til diskussion. Det er den eneste oplysning, ingen andre steder har."),
                (false, "Hvad med udskriften til køkkenet?"),
                (true, "Den kan laves. Jeg har prøvet den af på gårsdagens data, og den fylder én side. Kirsten skal se den, før vi går videre."),
                (false, "Jeg fanger hende i morgen. Hvornår kan vi have et tilbud?"),
                (true, "I næste uge. Jeg deler det op i to: analysen, som er lavet, og selve opsætningen, som I kan sige ja eller nej til hver for sig."),
            ],
            [
                (75, "40 % overlap mellem regneark og Outlook — dobbeltarbejde, ikke tre systemer"),
                (300, "«Nøgleansvarlig» findes KUN i regnearket. Skal med."),
                (540, "Tilbud deles i to: analyse og opsætning"),
            ]),

        new(7, 8, 45, "Ugentlig planlægning", "Møder", "Mødereferat",
            MeetingType.Physical, "Anne",
            [
                (true, "Vi tager de tre ting, der skal ud af huset i denne uge, og så stopper vi."),
                (false, "Tilbuddet til Bakkegården, fakturaen til Vestergade, og så skal demovideoen optages."),
                (true, "Tilbuddet er det eneste med en dato på. Mette venter på det på fredag."),
                (false, "Så tager jeg fakturaen i dag, den er ti minutter. Videoen kan vente til næste uge."),
                (true, "Enig. Er der noget, der er blevet liggende fra sidste uge?"),
                (false, "Ja, tilmeldingen til efteruddannelsen. Fristen er den sidste i måneden."),
                (true, "Den sætter vi på i dag, ellers ryger den igen. Det er tredje uge, den står."),
            ],
            [
                (30, "Tre ting ud af huset: tilbud, faktura, demovideo"),
                (200, "Tilmelding til efteruddannelse — tredje uge den står. Gøres I DAG."),
            ]),

        new(4, 14, 0, "Forelæsning: Netværk og sikkerhed", "Undervisning", "Dokumentation",
            MeetingType.Webinar, "Underviser",
            [
                (false, "I dag handler det om segmentering, og hvorfor det næsten altid er dét, der mangler, når noget går galt."),
                (false, "Et fladt netværk betyder, at den, der kommer ind ét sted, kan nå alt. Det er ikke et hul — det er en indretning."),
                (false, "Segmentering koster noget i drift. Man skal vedligeholde regler, og der kommer fejl, som er svære at finde."),
                (false, "Til gengæld ændrer det, hvad et brud koster. Ikke om det sker, men hvad det koster, når det sker."),
                (false, "Til afleveringen skal I beskrive et netværk med mindst tre zoner og begrunde, hvor I lægger snittet."),
                (false, "Begrundelsen vejer tungest. En tegning uden begrundelse er ikke en besvarelse."),
                (false, "Afleveringen er om to uger. Litteraturen er kapitel fire og fem, plus artiklen i materialet."),
            ],
            [
                (60, "Fladt netværk er en indretning, ikke et hul"),
                (420, "AFLEVERING: netværk med mindst tre zoner + begrundelse for snittet"),
                (600, "Kapitel 4 og 5 plus artiklen"),
            ]),

        new(1, 11, 0, "Tilbudsgennemgang med Bakkegården", "Kunder", "Mødereferat",
            MeetingType.Online, "Mette",
            [
                (true, "Jeg har delt det i to, som vi aftalte. Analysen er den første del, og den er lavet."),
                (false, "Og den anden del er selve opsætningen?"),
                (true, "Ja. Der står et fast beløb på begge dele, og der står, hvad der ikke er med."),
                (false, "Hvad er ikke med?"),
                (true, "Oplæring af de fjorten, der kigger med. Det er et kursus, ikke en opsætning, og det skal prissættes for sig."),
                (false, "Det giver mening. Hvor lang tid tager opsætningen?"),
                (true, "Tre uger fra I siger ja, forudsat at jeg har adgangen den første dag. Står adgangen stille, står tidsplanen stille."),
                (false, "Det skriver jeg ind til bestyrelsen. De mødes på torsdag."),
                (true, "Så sender jeg tilbuddet i dag, så du har det inden. Sig til, hvis der er noget, der skal formuleres om."),
                (false, "Én ting: kan I skrive, hvad der sker med det gamle regneark?"),
                (true, "Ja. Det arkiveres, og det bliver skrivebeskyttet. Det kommer med som en linje under leverancerne."),
            ],
            [
                (180, "IKKE med i tilbuddet: oplæring af de 14. Prissættes for sig."),
                (330, "Tre uger fra ja — forudsat adgang dag ét"),
                (560, "Bakkegårdens bestyrelse mødes torsdag - tilbuddet skal være fremme inden"),
                (620, "Skal stå: det gamle regneark arkiveres skrivebeskyttet"),
            ]),
    ];

    /// <summary>Bygger optagelserne. Returnerer mappe og oplysninger for hver.</summary>
    private static List<(string Mappe, MeetingMetadata Meta, Demomoede Kilde)> Moederne(DateTime idag)
    {
        var ud = new List<(string, MeetingMetadata, Demomoede)>();

        foreach (var m in Moedeliste())
        {
            var start = new DateTimeOffset(idag.AddDays(-m.DageSiden).AddHours(m.Time).AddMinutes(m.Minut),
                                           DateTimeOffset.Now.Offset);

            var udskrift = new Udskrift();
            var ms = 4_000L;

            foreach (var (herfra, tekst) in m.Replikker)
            {
                // Cirka tre ord i sekundet. Det giver tidsstempler, der ser
                // rigtige ud, naar man laeser med i udskriften.
                var laengde = Math.Max(2_500, tekst.Split(' ').Length * 380);

                udskrift.Linjer.Add(new Udskriftslinje
                {
                    FraMs = ms,
                    TilMs = ms + laengde,
                    Spor = m.Slags == MeetingType.Webinar
                        ? Samtale.Derfra
                        : herfra ? Samtale.Herfra : Samtale.Derfra,
                    Tekst = tekst,
                });

                ms += laengde + 900;
            }

            var sekunder = Math.Round(ms / 1000.0 + 45, 1);

            var mappe = MeetingStore.CreateSessionDirectory(m.Titel, start);

            var meta = new MeetingMetadata
            {
                StartedAt = start,
                EndedAt = start.AddSeconds(sekunder),
                DurationSeconds = sekunder,
                Title = m.Titel,
                Type = m.Slags,
                Mappe = m.Mappe,
                Moedetype = m.Moedetype,
                Language = "da",
                ValgtSprogMik = "da",
                ValgtSprogLoop = m.Slags == MeetingType.Physical ? null : "da",
                Talere =
                {
                    [Samtale.Herfra] = Mig,
                    [Samtale.Derfra] = m.Modpart,
                },
            };

            MeetingStore.Save(mappe, meta);

            udskrift.GemMaskin(mappe, Model);

            File.WriteAllText(Path.Combine(mappe, $"udskrift_{Model}.txt"),
                              udskrift.SomTekst(meta.Talere), new UTF8Encoding(false));

            Noterne(mappe, start, m);

            ud.Add((mappe, meta, m));
        }

        return ud;
    }

    /// <summary>Modellen, udskrifterne står som skrevet med.</summary>
    private const string Model = "large-v3";

    /// <summary>
    /// Noterne, praecis som appen selv skriver dem.
    /// </summary>
    /// <remarks>
    /// TEGNENE MAA IKKE UNDVIGES. JsonSerializer skriver som standard æ, ø og
    /// å som «\u00e5» - og noterne SOEGES DER SOM RAA TEKST, ikke som JSON.
    /// Se Soegning: notes.jsonl laeses med File.ReadAllText.
    ///
    /// Uden den her indstilling kunne man søge på «meeting» og finde noget,
    /// men ikke på «møde». Det er samme indstilling, MeetingStore selv bruger
    /// når den skriver noter under en optagelse.
    /// </remarks>
    private static readonly JsonSerializerOptions Notejson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static void Noterne(string mappe, DateTimeOffset start, Demomoede m)
    {
        var sb = new StringBuilder();

        foreach (var (sekund, tekst) in m.Noter)
        {
            sb.AppendLine(JsonSerializer.Serialize(new MeetingNote
            {
                AtSeconds = sekund,
                WallClock = start.AddSeconds(sekund),
                Text = tekst,
            }, Notejson));
        }

        File.WriteAllText(Path.Combine(mappe, "notes.jsonl"), sb.ToString(), new UTF8Encoding(false));
    }

    // ============================================================== dokumenter

    private static void Dokumenterne(List<(string Mappe, MeetingMetadata Meta, Demomoede Kilde)> moeder)
    {
        // TRE AF SEKS HAR ET DOKUMENT. Ikke alle: en liste, hvor alt er
        // faerdigt, viser ikke, hvad appen er til for. Det, der mangler et
        // referat, er dét, man skal kunne se med det samme.
        foreach (var (nr, tekst) in new[]
        {
            (0, Referat1),
            (2, Referat2),
            (5, Referat3),
        })
        {
            var (mappe, meta, _) = moeder[nr];

            var info = new DocumentInfo
            {
                Title = meta.Title ?? "Referat",
                Created = meta.StartedAt.AddHours(2),
                SourceMeetingId = meta.Id.ToString(),
                SourceRecording = mappe,
                SourceTitle = meta.Title ?? "",
                Template = "Mødereferat",
                Model = "Mistral Medium 3.5",
                Seconds = 14.2,
                Mappe = meta.Mappe == "Kunder" ? "Kunder" : "Referater",
                Markdown = tekst,
                FileName = DocumentStore.FileNameFor(meta.Title ?? "referat", "Mødereferat"),
            };

            DocumentStore.Save(info);
        }
    }

    private const string Referat1 = """
## Resumé

Første møde med Bakkegården om bookingen. De arbejder i dag i tre systemer —
Outlook, et regneark og en papirkalender i køkkenet — og bruger tid på at
holde dem enige. Fire personer taster; ti kigger med.

## Beslutninger

- Løsningen skal kunne skrives ud på ét A3-ark hver morgen. Kirsten skal ikke
  have en skærm i køkkenet, og det er et krav, ikke en præference.
- Der gives ikke et samlet prisoverslag på dette møde. Analysen prissættes
  fast for sig.

## Aftalt

| Hvad | Hvem | Hvornår |
|---|---|---|
| Gæsteadgang til Outlook-kalenderen | Mette | Denne uge |
| Kopi af bookingregnearket | Mette | Denne uge |
| En halv time med Kirsten | Mette | Denne uge |
| Nyt møde med udkast | Jørgen | Om 14 dage |

## Åbne spørgsmål

- Hvad står der i regnearket, som ikke findes i Outlook?
- Hvem må se hvad, når de fjorten får adgang?
""";

    private const string Referat2 = """
## Resumé

Gennemgang af de tre kalendere. Cirka 40 % af indholdet i regnearket findes
også i Outlook — det er dobbeltarbejde snarere end tre uafhængige systemer.

## Det, der kun findes ét sted

Kolonnen «K» i regnearket er nøgleansvarlig. Den oplysning står ingen andre
steder, og den skal med i den nye løsning under et navn, folk kan læse:
**Nøgleansvarlig**.

## Beslutninger

- Regnearket lukkes ikke, før nøgleansvarlig er flyttet med.
- Morgenudskriften er prøvet af på gårsdagens data og fylder én side.
  Kirsten skal se den, før der bygges videre.
- Tilbuddet deles i to dele, der kan besluttes hver for sig: analysen og
  opsætningen.

## Næste skridt

Tilbud sendes i næste uge.
""";

    private const string Referat3 = """
## Resumé

Gennemgang af tilbuddet. Det er delt i to dele — analyse og opsætning — med
et fast beløb på hver. Bestyrelsen behandler det torsdag.

## Det er ikke med

Oplæring af de fjorten medarbejdere, der kun kigger med. Det er et kursus og
ikke en opsætning, og det prissættes for sig.

## Forudsætninger

Tidsplanen er tre uger fra accept, **forudsat at adgangen er på plads den
første dag**. Står adgangen stille, står tidsplanen stille. Det er skrevet
ind i tilbuddet, så det ikke bliver en diskussion senere.

## Tilføjet på mødet

Det gamle regneark arkiveres og gøres skrivebeskyttet. Det kommer med som en
linje under leverancerne.

## Næste skridt

- Tilbuddet sendes i dag.
- Bestyrelsen mødes torsdag.
""";

    // ================================================================ projekter

    private static void Projekterne(
        List<(string Mappe, MeetingMetadata Meta, Demomoede Kilde)> moeder, DateTime idag)
    {
        // ---------- kunden ----------
        var kunde = Projektlager.Opret("Bakkegården — booking");
        kunde.Beskrivelse =
            "Samle tre kalendere til én løsning. Fjorten medarbejdere, fire der taster. "
            + "Morgenudskrift på A3 er et krav.";

        foreach (var nr in new[] { 0, 2, 5 })
            kunde.Tilfoej(Medlemsslags.Optagelse, moeder[nr].Meta.Id.ToString());

        Projektlager.Gem(kunde);

        Fil(kunde, "grundlag-bakkegaarden.md", """
# Bakkegården — grundlag

## Nuværende systemer

| System | Bruges til | Hvem |
|---|---|---|
| Outlook-kalender | Aftaler med eksterne | Fire medarbejdere |
| Bookingregneark | Interne bookinger og nøgler | Kirsten og Mette |
| Papirkalender i køkkenet | Dagens overblik | Alle |

Cirka 40 % af indholdet i regnearket står også i Outlook.

## Krav, der ligger fast

1. Morgenudskrift på ét A3-ark. Ingen skærm i køkkenet.
2. Nøgleansvarlig skal med. Oplysningen findes kun i regnearket i dag.
3. Det gamle regneark arkiveres skrivebeskyttet — det slettes ikke.

## Tidsplan

Tre uger fra accept, forudsat adgang den første dag.

## Det er ikke med

Oplæring af de fjorten medarbejdere, der kun kigger med. Prissættes for sig
som et kursus.
""");

        Fil(kunde, "noter-fra-kirsten.md", """
# Halv time med Kirsten, køkkenet

Kirsten har haft papirkalenderen i elleve år. Hun læser den, mens hun går
forbi — hun sætter sig ikke ned ved den.

Det er derfor, en skærm ikke løser noget: problemet er ikke, hvor tallene
står, men at hun skal kunne se dem uden at stoppe op.

Kolonnen «K» er nøgleansvarlig. Hun skriver initialer. Der er fem personer,
der kan stå der.

Hun spørger, hvad der sker, hvis strømmen går. I dag hænger arket der. Det
skal der være et svar på.
""");

        // ---------- studiet ----------
        var studie = Projektlager.Opret("Datatekniker — 3. semester");
        studie.Beskrivelse =
            "Netværk og sikkerhed. Aflevering om segmentering med mindst tre zoner.";

        studie.Tilfoej(Medlemsslags.Optagelse, moeder[4].Meta.Id.ToString());
        Projektlager.Gem(studie);

        Fil(studie, "afleveringskrav.md", """
# Aflevering 2 — segmentering

## Opgaven

Beskriv et netværk med **mindst tre zoner**, og begrund, hvor snittet lægges.

Begrundelsen vejer tungest. En tegning uden begrundelse er ikke en
besvarelse.

## Formkrav

- Omfang: 5-8 sider inkl. figurer
- Afleveres som PDF
- Frist: to uger efter forelæsningen om segmentering

## Litteratur

- Kapitel 4: Zoner og tillid
- Kapitel 5: Regler og vedligehold
- Artiklen i materialet
""");

        Fil(studie, "egne-noter-segmentering.md", """
# Egne noter — segmentering

Det, der sad fast fra forelæsningen:

Et fladt netværk er ikke et hul. Det er en indretning, nogen har valgt — og
som regel har de valgt den, fordi det var nemmest at drive.

Segmentering ændrer ikke sandsynligheden for et brud. Den ændrer, hvad et
brud KOSTER. Det er den sætning, opgaven skal bygges op om.

Prisen er drift: regler, der skal vedligeholdes, og fejl, der er svære at
finde. Det skal med i begrundelsen, ellers ser det ud som om, valget er
gratis.
""");

        static void Fil(Projekt p, string navn, string indhold)
        {
            Directory.CreateDirectory(p.Dokumentmappe);
            File.WriteAllText(Path.Combine(p.Dokumentmappe, navn), indhold, new UTF8Encoding(false));
        }
    }

    // ================================================================== kalender

    private static void Aftalerne(DateTime idag)
    {
        var aftaler = new (int Dage, int Time, int Minut, int Varighed, string Titel, string Sted, string Link)[]
        {
            (0, 14, 0, 45, "Bakkegården — opfølgning på tilbud", "", "https://teams.microsoft.com/l/demo"),
            (1, 9, 30, 60, "Ugentlig planlægning", "Kontoret", ""),
            (2, 11, 0, 30, "Vestergade — kort status", "", "https://meet.google.com/demo"),
            (3, 13, 0, 90, "Forelæsning: Adgangsstyring", "Lokale 2.14", ""),
            (7, 10, 0, 60, "Bakkegården — opstart, hvis ja", "Bakkegården, Ringsted", ""),
            (9, 8, 30, 45, "Bogholder — kvartalsafslutning", "", "https://teams.microsoft.com/l/demo2"),
        };

        foreach (var a in aftaler)
        {
            var start = new DateTimeOffset(idag.AddDays(a.Dage).AddHours(a.Time).AddMinutes(a.Minut),
                                           DateTimeOffset.Now.Offset);

            Kalender.Gem(new Aftale
            {
                Titel = a.Titel,
                Start = start,
                Slut = start.AddMinutes(a.Varighed),
                Sted = a.Sted,
                Link = a.Link,
                Kilde = Kalenderkilde.Lokal,
            });
        }
    }

    // =================================================================== opgaver

    private static void Opgaverne(DateTime idag,
        List<(string Mappe, MeetingMetadata Meta, Demomoede Kilde)> moeder)
    {
        var opgaver = new List<Opgave>
        {
            Ny("Send tilbuddet til Bakkegården", 1, 0,
               "Begge dele: analyse og opsætning. Husk linjen om, at det gamle regneark arkiveres skrivebeskyttet.",
               moeder[5]),

            Ny("Tilmelding til efteruddannelse", 1, 4,
               "Fristen er den sidste i måneden. Har stået på listen i tre uger."),

            Ny("Faktura til Vestergade", 2, 1, "Ti minutter. Tages i dag."),

            Ny("Spørg Kirsten om morgenudskriften", 2, 2,
               "Hun skal se den, før der bygges videre. En side, prøvet af på gårsdagens data.",
               moeder[2]),

            Ny("Aflevering: segmentering med tre zoner", 1, 11,
               "Begrundelsen vejer tungest. Kapitel 4 og 5 plus artiklen.",
               moeder[4]),

            Ny("Optag demovideoen", 3, 8, "Kan vente til næste uge."),

            Ny("Databehandleraftale med bogholderen", 2, 6,
               "Kom frem på webinaret om persondata. Den mangler.",
               moeder[1]),

            Faerdig("Kopi af bookingregnearket modtaget", 2),
            Faerdig("Gæsteadgang til Outlook på plads", 3),
        };

        Opgavelager.Gem(opgaver);
        return;

        Opgave Ny(string navn, int prioritet, int forfalderOm, string tekst,
                  (string Mappe, MeetingMetadata Meta, Demomoede Kilde)? fra = null) => new()
        {
            Navn = navn,
            Tekst = tekst,
            Prioritet = prioritet,
            Deadline = new DateTimeOffset(idag.AddDays(forfalderOm).AddHours(12), DateTimeOffset.Now.Offset),
            Oprettet = new DateTimeOffset(idag.AddDays(-forfalderOm - 2).AddHours(9), DateTimeOffset.Now.Offset),
            MoedeId = fra?.Meta.Id.ToString() ?? "",
            Moedetitel = fra?.Meta.Title ?? "",
        };

        Opgave Faerdig(string navn, int dageSiden) => new()
        {
            Navn = navn,
            Prioritet = 3,
            Faerdig = true,
            Faerdiggjort = new DateTimeOffset(idag.AddDays(-dageSiden).AddHours(15), DateTimeOffset.Now.Offset),
            Oprettet = new DateTimeOffset(idag.AddDays(-dageSiden - 3).AddHours(9), DateTimeOffset.Now.Offset),
        };
    }

    // ================================================================= diktater

    private static void Diktaterne(DateTime idag)
    {
        var noter = new (int DageSiden, int Time, string Tekst)[]
        {
            (0, 8, "Husk at spørge Mette, om bestyrelsen har fået tilbuddet inden torsdag. "
                 + "Hvis ikke, så send det direkte til formanden."),

            (1, 16, "Idé til opsætningen: morgenudskriften kan også lægges som PDF i en mappe, "
                  + "de deler. Så har de den, hvis printeren driller."),

            (3, 7, "Til opgaven om segmentering — brug Bakkegården som eksempel. "
                 + "Tre zoner: gæster, kontor og drift. Det er en rigtig sag og ikke et konstrueret netværk."),

            (6, 12, "Ring til bogholderen om databehandleraftalen. Det tager fem minutter, "
                  + "og det har stået på listen i to uger."),
        };

        // AELDST FOERST. Tilfoej saetter hver ny note oeverst, saa listen
        // ender med den nyeste foerst - som i appen.
        foreach (var n in noter.OrderByDescending(n => n.DageSiden))
            Diktatnoter.Tilfoej(n.Tekst, tid: idag.AddDays(-n.DageSiden).AddHours(n.Time));
    }

    // ================================================================ historikken

    private static void Historikken(List<(string Mappe, MeetingMetadata Meta, Demomoede Kilde)> moeder)
    {
        foreach (var (mappe, meta, _) in moeder)
        {
            Historik.Skriv(HaendelseType.Transskription,
                $"Udskrift færdig: {meta.Title}",
                $"{meta.DurationSeconds / 60:0} minutter · dansk · large-v3",
                Udfald.Fuldført, "large-v3", mappe, meta.DurationSeconds / 12,
                kilde: meta.Id.ToString());
        }

        Historik.Skriv(HaendelseType.Dokument, "Referat oprettet: Tilbudsgennemgang med Bakkegården",
            "Skabelon «Mødereferat» · Mistral Medium 3.5 · 4.812 tokens sendt, 1.190 modtaget",
            Udfald.Fuldført, "Mistral Medium 3.5", sekunder: 14.2);

        Historik.Skriv(HaendelseType.Andet, "Eksemplet er klar",
            "Du står i demotilstand. Møderne, aftalerne og opgaverne er eksempler, og de "
            + "ligger for sig selv — dine egne optagelser og noter er urørte. Du er tilbage "
            + "i dem med knappen nederst i menuen.",
            Udfald.Fuldført);
    }
}
