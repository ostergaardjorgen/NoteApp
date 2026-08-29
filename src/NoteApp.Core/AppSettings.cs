using System.Text;
using System.Text.Json;

namespace NoteApp.Core;

/// <summary>
/// Appens indstillinger. Ligger i datamappen sammen med resten af det, der er
/// brugerens — ikke i registreringsdatabasen og ikke ved siden af exe'en, så
/// en backup af datamappen også tager indstillingerne med.
///
/// Klassen ligger i Core og ikke i UI-projektet, fordi kommandolinjeværktøjet
/// skal læse præcis de samme valg. Lå den i WPF-projektet, ville 'heypia
/// motor' vise en anden model end den, appen faktisk bruger — og det er den
/// slags uoverensstemmelse, man bruger en time på at forstå.
///
/// Der gemmes intet her, der kan identificere nogen: valgt model, valgte
/// lydenheders Windows-ID, backupmappe. Ikke andet.
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string? PreferredModel { get; set; }

    public bool SetupCompleted { get; set; }

    /// <summary>
    /// Sproget, BRUGERFLADEN vises på — «da», «en» og hvad der ellers ligger
    /// i sprogmappen. Null betyder dansk.
    ///
    /// FORVEKSLES IKKE MED <see cref="MitSprog"/>. Det er sproget, du TALER,
    /// og det bruges til at skrive lyden ud. De to har intet med hinanden at
    /// gøre: man kan udmærket køre appen på engelsk og holde sine møder på
    /// dansk — og det gør man, hvis man har en udenlandsk kollega kigge med.
    /// </summary>
    public string? Sprog { get; set; }

    public string? Industry { get; set; }

    public string? BackupDestination { get; set; }

// HER LAA OpsummerLokalt.
    //
    // Den afgjorde, om den korte opsummering blev lavet paa maskinen eller
    // hos leverandoeren. Der er kun een vej nu: den lokale er fjernet
    // 25-08-2026 sammen med motoren og sprogmodellen - 4 GB af det, en ny
    // bruger skulle hente.
    //
    // Feltet fjernes helt frem for at blive staaende ubrugt. En indstilling,
    // der ikke goer noget, er en, nogen finder og tror paa. Gamle
    // indstillingsfiler med noeglen i laeses uden problemer; ukendte felter
    // springes over.


    /// <summary>
    /// Skriv mødet ud af sig selv, så snart optagelsen er gemt.
    ///
    /// SAND SOM STANDARD, og det er en beslutning om, hvad appen ER. En
    /// optagelse uden tekst kan hverken søges, laves til et dokument eller
    /// bruges til noget. Skridtet fra lyd til tekst er ikke et valg, man
    /// træffer — det er det, man kom for.
    ///
    /// Før stod der et spørgsmål efter hvert møde. Et spørgsmål lige efter et
    /// møde bliver besvaret med nej, fordi nej lyder uforpligtende — og så
    /// ligger optagelsen og bliver aldrig til noget.
    ///
    /// Den kan slås fra af den, der vil bestemme selv.
    /// </summary>
    public bool SkrivUdAutomatisk { get; set; } = true;

    /// <summary>
    /// Lyd med i sikkerhedskopien. Falsk som standard: lyden er tusind gange
    /// større end alt det andet tilsammen, og den er også det, der er lettest
    /// at undvære — transskriptionen og de indlærte rettelser er arbejdet.
    /// </summary>
    public bool BackupIncludeAudio { get; set; }

    /// <summary>
    /// Valgte lydenheder, gemt som Windows-enheds-ID. Navnet duer ikke som
    /// nøgle: to headset af samme model hedder præcis det samme. Er null,
    /// bruges Windows' standard.
    /// </summary>
    public string? MicrophoneId { get; set; }

    public string? SpeakerId { get; set; }

    /// <summary>
    /// Sproget, DU taler. Bruges til mikrofonsporet.
    ///
    /// HVORFOR DET ER EN INDSTILLING OG IKKE ET GÆT
    ///
    /// Whispers egen sprogdetektering blev målt forkert på et rigtigt møde:
    /// mikrofonsporet blev bedømt til ENGELSK med 42 % sikkerhed, mens der
    /// blev talt dansk, og hele udskriften blev derfor engelsk vrøvl. Fejlen
    /// så ikke ud som en fejl — den så ud som et referat.
    ///
    /// Mikrofonsporet er per definition din egen stemme og dem, der sidder i
    /// samme lokale. Hvilket sprog det er, ved du; det er ikke noget, en model
    /// skal gætte sig til ud fra de første tredive sekunder, hvor der som
    /// regel ikke bliver sagt noget.
    ///
    /// Det ANDET spor detekteres frit, og det er med vilje: modparten skifter
    /// fra møde til møde. Et dansk-norsk møde giver dansk på det ene spor og
    /// norsk på det andet — og det er det rigtige svar, ikke et problem.
    ///
    /// «auto» lader appen gætte, som den gjorde før. Null betyder dansk.
    /// </summary>
    public string? MitSprog { get; set; }

    /// <summary>
    /// Sproget, de ØVRIGE mødedeltagere taler. Bruges til loopback-sporet.
    ///
    /// HVORFOR OGSÅ DET ER EN INDSTILLING
    ///
    /// Først lod appen dette spor detektere frit, ud fra den antagelse at det
    /// er det rene digitale signal og derfor til at stole på. Det holdt ikke:
    /// på et dansk-norsk møde blev de norske gæster bedømt til ENGELSK med
    /// 26–34 % sandsynlighed — målt fra fire forskellige steder i optagelsen,
    /// med samme forkerte svar hver gang.
    ///
    /// Det lave tal er selve signalet. Whisper gætter, og et gæt på sproget
    /// ødelægger hele udskriften, ikke bare et ord.
    ///
    /// «auto» lader appen gætte. Null betyder «samme som mit».
    /// </summary>
    public string? DeresSprog { get; set; }

    /// <summary>
    /// Skift afsnit automatisk under oplæsning. Kræver en lille model ved
    /// siden af den store — se LiveListener. Slået fra som standard: den
    /// koster GPU-tid, og en fejltolkning midt i en oplæsning er irriterende.
    /// </summary>
    public bool AutoAdvance { get; set; }

    // HER LAA LiveModel = "small".
    //
    // Feltet blev aldrig laest. Der er ingen live-lytning i appen, og der har
    // ikke vaeret det. Det stod og lignede en forklaring paa, hvorfor
    // ggml-small.bin laa i modelmappen - og den forklaring var forkert.
    // Fjernet 18-08-2026.

    /// <summary>
    /// Den genvejstast, der lynstarter en optagelse — gemt som id, ikke som
    /// tastekombination, så navnet kan skrives om uden at valget går tabt.
    ///
    /// Null betyder «tag den første ledige». Genvejstaster er optaget af vidt
    /// forskellige programmer fra maskine til maskine, og et fast valg, der
    /// ikke kan lade sig gøre, er ingen genvej.
    /// </summary>
    public string? HotkeyId { get; set; }

    /// <summary>
    /// Den kombination, brugeren selv har TRYKKET. Tom = ikke valgt.
    /// </summary>
    /// <remarks>
    /// DEN VINDER OVER <see cref="HotkeyId"/>. Listen dér er forslag, og et
    /// forslag kan ikke skelne mellem to taster, der hedder det samme.
    ///
    /// Det kostede en formiddag 28-08-2026. Appen lyttede efter kommaet ved
    /// siden af M; brugeren trykkede paa kommaet paa TALTASTATURET, som er
    /// dét, der staar et komma paa et dansk tastatur. Det samme gaelder
    /// tallene: talraekkens 1 og taltastaturets 1 er to forskellige taster,
    /// og listen viser dem begge som «1».
    ///
    /// Trykker man kombinationen i stedet for at vaelge den, findes den
    /// forskel ikke laengere. Se <see cref="Genvejstast"/>.
    /// </remarks>
    public string? Genvejskombi { get; set; }

    // ======================= DIKTERING =======================

    /// <summary>
    /// Skal et hold på genvejstasten starte en diktering?
    /// </summary>
    /// <remarks>
    /// TIL SOM STANDARD — MEN DEN VAR FRA, OG DET VAR FORKERT.
    ///
    /// Første udgave stod fra «for en sikkerheds skyld». Resultatet var, at
    /// den, der havde bedt om dikteringen, holdt tasten nede og fik en
    /// mødeoptagelse — to gange, før nogen tænkte på at kigge i
    /// Indstillinger. En funktion, man har bedt om, skal virke, når den
    /// kommer.
    ///
    /// DET KOSTER 350 ms PÅ AT STARTE EN MØDEOPTAGELSE. Skal appen kunne
    /// skelne et tryk fra et hold, kan den ikke handle på trykket, før tasten
    /// er sluppet. Slås dikteringen fra, går trykket igennem med det samme,
    /// præcis som før den fandtes.
    ///
    /// Prisen betales kun, når der er en nøgle. Uden en nøgle kan der ikke
    /// dikteres, og så ville ventetiden være ren udgift — se
    /// <c>MainWindow</c>, hvor de to ting ganges sammen.
    ///
    /// Diktering SENDER lyd til leverandøren. Mødernes lyd gør ikke, og de to
    /// ting skal blive ved at være forskellige — men det er holdet på tasten,
    /// der er samtykket, ikke et flueben, man har glemt.
    /// </remarks>
    public bool DikteringTil { get; set; } = true;

    /// <summary>
    /// Længste diktering i minutter. Derefter slippes der af sig selv.
    /// </summary>
    /// <remarks>
    /// EN TAST KAN SIDDE FAST. Sker det — fysisk, eller fordi et andet program
    /// spiser slippet — ville appen ellers optage og sende, til nogen opdagede
    /// det. Det koster penge hos leverandøren og er ikke til at se på skærmen.
    ///
    /// Tallet holdes inden for det, der giver mening, af
    /// <see cref="Holdvurdering.LoftFra"/>. En indstillingsfil kan indeholde
    /// hvad som helst.
    /// </remarks>
    public int DikteringLoftMinutter { get; set; } = Holdvurdering.StandardLoftMinutter;

    /// <summary>
    /// Hvad dikteringen som udgangspunkt skal blive til: note, mail, prompt
    /// eller opgave.
    /// </summary>
    /// <remarks>
    /// Formen følger opgaven — det er dét, der skiller diktering fra en
    /// diktafon. Står her som tekst frem for som et tal, så en indstillingsfil
    /// kan læses af et menneske.
    /// </remarks>
    public string? DikteringFormaal { get; set; }

    /// <summary>
    /// Skal den rå udskrift pudses af, før den lægges ind?
    /// </summary>
    /// <remarks>
    /// TIL SOM STANDARD. Rå tale har fyldord, halve sætninger og ingen
    /// tegnsætning; uden pudsningen er det en diktafon.
    ///
    /// Den kan slås fra, og det er ikke en teoretisk mulighed: skal man citere
    /// nogen ordret, er det netop det rå, man vil have. Pudsningen koster
    /// også et kald mere og et halvt sekund.
    /// </remarks>
    public bool DikteringPuds { get; set; } = true;

    /// <summary>
    /// Skal den indlærte ordliste sendes med som fagord?
    /// </summary>
    /// <remarks>
    /// TIL SOM STANDARD. Ordlisten er blevet bedre af de rettelser, du selv
    /// har lavet, og uden den skal dikteringen lære navnene forfra.
    ///
    /// Den sendes med til leverandøren sammen med lyden. Det er ord, ikke
    /// indhold — men det er en liste over, hvad du taler om, og derfor skal
    /// den kunne slås fra.
    /// </remarks>
    public bool DikteringFagord { get; set; } = true;

    /// <summary>
    /// Skal teksten lægges ind, hvor markøren står?
    /// </summary>
    /// <remarks>
    /// TIL SOM STANDARD. Det er hele forskellen på en diktering og en
    /// diktafon med en udklipsholder: man taler, og teksten er der.
    ///
    /// Slås den fra, ligger teksten i udklipsholderen, og man sætter selv ind.
    /// Det er det rigtige valg i et program, hvor et Ctrl+V betyder noget
    /// andet end at sætte ind — og i det hele taget, hvis man hellere vil se
    /// teksten, før den lander.
    /// </remarks>
    public bool DikteringIndsaet { get; set; } = true;

    /// <summary>
    /// Skal formen følge det program, der er fremme?
    /// </summary>
    /// <remarks>
    /// TIL SOM STANDARD. Er du i din mail, bliver det en mail; er du i en
    /// AI-assistent, bliver det en prompt.
    ///
    /// Gættet er BEVIDST FORSIGTIGT — kun programmer, der er til at kende. Er
    /// det ikke genkendt, bruges <see cref="DikteringFormaal"/>. En forkert
    /// gætning er værre end ingen, fordi man skal opdage den og skrive om.
    /// </remarks>
    public bool DikteringEfterProgram { get; set; } = true;

    /// <summary>
    /// Egne instruktioner til teksttyperne. Tom betyder «brug standarden».
    /// </summary>
    /// <remarks>
    /// KUN DET, DER ER RETTET, STÅR HER. En kopi af standarden gemmes ikke —
    /// gjorde den det, ville typen være «rettet» for altid, og en forbedring i
    /// en ny udgave af appen ville aldrig nå frem, uden at nogen havde valgt
    /// det. Se <see cref="Llm.Teksttyper.Saet"/>.
    /// </remarks>
    public Dictionary<string, string> Teksttyper { get; set; } = new();

    /// <summary>
    /// Er menuen klappet ind, så kun ikonerne står?
    /// </summary>
    /// <remarks>
    /// Den huskes, fordi den er et valg om plads, ikke om opgave. Klappede
    /// man den ind for at få bredde til en udskrift, ville den være ude igen
    /// ved næste opstart — og så klapper man den ind hver morgen.
    /// </remarks>
    public bool MenuSammenklappet { get; set; }

    /// <summary>
    /// Hvornår klokken sidst blev åbnet. Alt nyere end det er ulæst.
    ///
    /// Sættes kun, når man ÅBNER klokken — ikke ved opstart. En besked, man
    /// aldrig nåede at se, skal ikke forsvinde, fordi man genstartede appen.
    /// </summary>
    public DateTimeOffset NotifikationerSetTil { get; set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// De enkelte beskeder, man har trykket sig igennem — nyere end
    /// <see cref="NotifikationerSetTil"/>.
    /// </summary>
    /// <remarks>
    /// ET VANDMAERKE ALENE KAN IKKE SIGE «DEN HER, MEN IKKE DEN UNDER».
    /// Aabner man klokken og laeser den ene besked, man ventede paa, skal de
    /// to aeldre blive staaende som ulaeste — ellers forsvinder de, fordi man
    /// kiggede forbi.
    ///
    /// Listen kan ikke vokse uden ende: den ryddes, naar vandmaerket flyttes,
    /// og alt under vandmaerket luges ud ved hver skrivning.
    /// </remarks>
    public List<DateTimeOffset> NotifikationerLaeste { get; set; } = new();

    /// <summary>
    /// Hvor optagebåndet stod sidst — i skærmkoordinater.
    ///
    /// Det huskes, fordi det ikke er en pyntedetalje: har man to skærme, ligger
    /// mødet på den ene og båndet skal ligge på den anden. Skulle det trækkes
    /// derover ved hvert eneste møde, ville det være hurtigere at lade være.
    ///
    /// Null betyder «aldrig flyttet» — så lægger båndet sig selv øverst midt
    /// på den skærm, appen står på.
    /// </summary>
    public double? BaandX { get; set; }

    public double? BaandY { get; set; }

    /// <summary>
    /// Holder appen øje med, om der er startet et møde?
    ///
    /// SLÅET TIL FRA BEGYNDELSEN — og det er en bevidst ændring.
    ///
    /// Den var slået fra først, ud fra at en app, der kigger efter, hvad man
    /// laver, er noget man skal slå til selv. Det var forkert som standard:
    /// funktionen findes for at redde de møder, man glemmer at optage, og en
    /// funktion, man skal finde og slå til, redder ingen af dem. Hvert møde,
    /// der ikke bliver optaget, er et hul i det arkiv, hele appen lever af.
    ///
    /// Den bliver ved at være noget, man kan slå FRA under Indstillinger, og
    /// dér står det stadig præcist, hvad der bliver kigget på: Windows' eget
    /// register over, hvilke programmer der bruger mikrofonen. Den samme
    /// oplysning, brugeren selv kan se i Windows. Der lyttes ikke med, og der
    /// optages aldrig af sig selv — vagten kan kun spørge.
    ///
    /// Værdien sættes én gang ved første start, se <see cref="StandardvalgSat"/>.
    /// </summary>
    public bool MoedevagtTil { get; set; }

    /// <summary>
    /// Er standardvalgene sat én gang?
    ///
    /// HVORFOR DER SKAL ET FLAG TIL
    ///
    /// To ting skal være slået TIL fra begyndelsen: at appen starter med
    /// Windows, og at den spørger, når et program bruger mikrofonen. Begge er
    /// noget, man skal kunne slå FRA — ikke noget, man skal finde og slå til.
    ///
    /// En almindelig standardværdi kan ikke bruges til det. Slår man
    /// mødevagten fra, gemmes «false» — og det er præcis den samme værdi som
    /// «aldrig taget stilling». Uden flaget her ville appen slå den til igen
    /// ved næste start, og et hak, der kommer tilbage af sig selv, er værre
    /// end intet hak.
    ///
    /// Flaget sættes én gang. Derefter er det brugerens valg, der gælder.
    /// </summary>
    public bool StandardvalgSat { get; set; }

    /// <summary>
    /// Programmer, der aldrig skal spørges om igen.
    ///
    /// Findes, fordi det ellers bliver en vagt, man slår fra. Discord, et
    /// spil eller en diktafon bruger mikrofonen uden at være et møde, og et
    /// spørgsmål, man afviser hver dag, er værre end intet spørgsmål.
    ///
    /// Der gemmes programmets sti eller pakkenavn — ikke et proces-id, som
    /// skifter ved hver start.
    /// </summary>
    public List<string> MoedevagtAldrig { get; set; } = new();

    /// <summary>
    /// Har brugeren kvitteret for at have læst, hvad en integration betyder?
    ///
    /// EN INTEGRATION KAN IKKE SLÅS TIL, FØR DEN ER SAT.
    ///
    /// Alt andet i appen bliver på maskinen. En kalenderintegration er det
    /// ene sted, hvor appen taler med en amerikansk leverandør, og det er en
    /// beslutning — ikke en indstilling. En knap, man kan trykke på uden at
    /// have læst noget, gør teksten ovenover til pynt.
    ///
    /// Den spørges ÉN gang og huskes. Et spørgsmål, der kommer igen hver
    /// gang, læses ikke anden gang.
    ///
    /// Den blokerer ikke noget som helst andet. Kalenderen, optagelsen og
    /// resten af appen virker uændret uden den.
    /// </summary>
    public bool IntegrationerLaest { get; set; }

    /// <summary>
    /// Bredden på Cockpittets venstre spalte — kalenderen.
    ///
    /// SPALTEBREDDER ER ET VALG, DER SKAL OVERLEVE EN GENSTART. En bredde,
    /// man selv har trukket på plads, og som er væk i morgen, er værre end en
    /// fast bredde: så trækker man den samme spalte hver dag.
    ///
    /// Nul betyder «aldrig rørt» og giver standardbredden. Det er ikke det
    /// samme som en spalte, nogen har trukket helt sammen — den får sin
    /// mindstebredde.
    /// </summary>
    public double CockpitVenstre { get; set; }

    /// <summary>Bredden på Cockpittets højre spalte — opgaverne.</summary>
    public double CockpitHoejre { get; set; }

    /// <summary>
    /// Hovedvinduets størrelse, som det stod, da appen sidst blev lukket.
    ///
    /// STØRRELSEN ER ET VALG, DER SKAL OVERLEVE EN GENSTART — af samme grund
    /// som spaltebredderne. Har man trukket vinduet ud over en bred skærm,
    /// fordi der skal være plads til kalenderen, er det irriterende at gøre
    /// det igen hver morgen.
    ///
    /// Positionen gemmes IKKE. En skærm, der er koblet fra siden i går, ville
    /// betyde et vindue, ingen kan se — og det er en langt værre fejl end at
    /// åbne midt på skærmen. Windows placerer det selv.
    ///
    /// Nul betyder «aldrig gemt» og giver målene fra XAML'en.
    /// </summary>
    public double VinduesBredde { get; set; }

    public double VinduesHoejde { get; set; }

    /// <summary>Var vinduet maksimeret? Så åbnes det maksimeret igen.</summary>
    public bool VinduetMaksimeret { get; set; }

    /// <summary>
    /// Teksten, der skrives i mødeindkaldelsen, når appen laver et Meet-link.
    /// Tom betyder «brug standarden» — se Googlekalender.StandardOptagenote.
    ///
    /// ORDLYDEN HØRER TIL DEN, DER HOLDER MØDET. Et firma har sin egen
    /// formulering, en underviser en anden, og en tekst, man ikke må røre,
    /// bliver til en, man arbejder udenom.
    ///
    /// Den kan rettes, men ikke fjernes: står feltet tomt, bruges standarden.
    /// Appen opfordrer ALTID til at fortælle deltagerne, at der optages.
    /// </summary>
    public string Optagenote { get; set; } = "";

    /// <summary>
    /// Den engelske udgave. Bruges, når mødets sprog ikke er dansk.
    ///
    /// EN INDKALDELSE PÅ DANSK TIL EN, DER IKKE LÆSER DANSK, ER IKKE EN
    /// OPLYSNING. Retten til at sige fra er kun værd at have, hvis den kan
    /// læses.
    /// </summary>
    public string OptagenoteEn { get; set; } = "";

    /// <summary>
    /// Hvor mange dage efter udskriften lydfilen ryddes. 0 = aldrig.
    ///
    /// ET ÅR SOM STANDARD, og det er et bevidst langt tal.
    ///
    /// Lyden fylder titusind gange så meget som teksten, og den kan ikke
    /// komprimeres uden at ødelægge udskriften af møder med flere sprog — se
    /// findings 9.2. Så er sletning det eneste håndtag, der er tilbage.
    ///
    /// Men uden lyden kan man ikke skrive optagelsen ud igen med en bedre
    /// model, ikke køre talergenkendelsen om, og ikke høre efter, om maskinen
    /// hørte rigtigt. Det sidste er ikke bygget endnu — at klikke på en
    /// sætning og høre den — og det er netop dét, der bliver umuligt for
    /// gamle optagelser, hvis lyden er væk.
    ///
    /// Et år er langt nok til, at man har brugt mødet færdigt, og kort nok
    /// til, at disken ikke løber fuld af noget, ingen åbner.
    ///
    /// NUL BETYDER «LAD VÆRE» — ikke «med det samme». Et felt, hvor en tom
    /// værdi sletter alt, er en fælde.
    /// </summary>
    public int SletLydEfterDage { get; set; } = 365;

    /// <summary>
    /// Hvor langt et møde skal være, før appen spørger om at rydde lyden med
    /// det samme. Minutter. 0 = spørg aldrig.
    ///
    /// TO TIMER SOM STANDARD. Et almindeligt møde fylder ikke nok til at være
    /// værd at tage stilling til; et heldagsseminar fylder en halv gigabyte,
    /// og det er dét, man opdager en dag, disken er fuld.
    ///
    /// Der spørges KUN én gang, lige efter udskriften — dér, hvor man netop
    /// har set teksten og kan bedømme, om den er god nok. Et spørgsmål, der
    /// kommer en uge senere, kan man ikke svare på.
    /// </summary>
    public int SpoergOmLydOverMinutter { get; set; } = 120;

    /// <summary>
    /// Lyst eller mørkt tema.
    /// </summary>
    /// <remarks>
    /// MØRKT ER STANDARD. Appen var mørk uden valg indtil 28-08-2026, og
    /// begrundelsen står stadig: udskriften læses i lange stræk, og lys tekst
    /// på mørk bund trætter mindre.
    ///
    /// «Følg Windows» var standard i et par timer og blev valgt fra igen samme
    /// dag. Den lyder rigtig — den, der har sat sin maskine mørk, har svaret
    /// på spørgsmålet — men den lader et program, appen ikke kender, bestemme
    /// udseendet af det skærmbillede, man læser længst på. Begge dele står
    /// stadig som valg under Indstillinger og på knappen i topbjælken.
    /// </remarks>
    public Temavalg Tema { get; set; } = Temavalg.Moerkt;

    private static string Path => System.IO.Path.Combine(UserDataPaths.Root, "indstillinger.json");

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path, Encoding.UTF8)) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // En ødelagt indstillingsfil må ikke forhindre appen i at starte.
            // Standardværdier er altid brugbare.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(UserDataPaths.Root);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, Options), Encoding.UTF8);
    }

    /// <summary>Tvinger næste læsning til at gå på disken igen — fx efter en gendannelse.</summary>
    public static void Reload() => _current = null;
}
