# Roadmap

*Skrevet om 20-08-2026, efter at retningen blev lagt fast.*
*Status efterprøvet mod koden 24-08-2026 — mærkaterne nedenfor er læst i kilden, ikke husket.*

## Hvor produktet er på vej hen

Indtil nu har appen været et værktøj: optag, skriv ud, lav et dokument. Herfra
bliver den et **sted, hvor mødeaktiviteten samles** — kalenderen, lyden,
udskriften, dokumenterne og opgaverne det samme sted, med et overblik over
dagen og ugen som forside.

Det ændrer, hvad der er vigtigt. Tre ting bliver bærende:

**Udskriften skal kunne rettes.** Den er i dag noget, maskinen har lavet, og
som man kun kan læse. Bliver den til noget, man kan finpudse — med tidsstempler
og navne på hvem der sagde hvad — er den ikke længere et halvfabrikat. Så kan
man i mange tilfælde nøjes med at kopiere det, man skal bruge, **uden at sende
noget i skyen overhovedet**. Det er både bedre for brugeren og for compliance.

**Materialet skal kunne komme ind udefra.** Møder optages ikke altid på pc'en.
En optagelse fra en telefon skal kunne lægges ind og behandles som alt andet.

**Møderne skal have en tidslinje.** Uden en kalender er der kun en liste over
fortiden. Med en kalender bliver «hvad sker der i dag» og «hvad blev aftalt
sidst» spørgsmål, appen kan svare på.

---

## Den beslutning, der skal træffes først
**AFKLARET · bygget som foreslået, alle tre punkter**

**Hvad sker der med en rettet udskrift, når man kører transskriptionen om?**

Det er ikke en detalje. I dag kan udskriften genskabes når som helst, og hele
maskineriet bygger på det: fletningen af de to spor, genbruget af et spor der
ikke har ændret sig, søgningens tegnnumre. Rettes teksten i hånden, holder den
antagelse op.

Vi har lavet den fejl før. Mekanismen, der anvendte lærte rettelser på
udskrifter, blev fjernet 18-08 — den ville have omskrevet enhver dansk udskrift
med ordet «eller» i, og den nåede kun at lade være, fordi der tilfældigvis ikke
lå nogen filer at røre.

**Forslaget:**

1. Rettelser gemmes som **deres egen fil** ved siden af den maskingenererede.
   Den rettede er den, resten af appen bruger — dokumenter, søgning, kopiering.
2. En ny transskription **overskriver den aldrig**. Der spørges, og den gamle
   rettede udgave bliver liggende som kopi.
3. **Talernavne bages ikke ind i teksten.** De gemmes som en tilknytning i
   `meeting.json` — HERFRA er Jørgen, DERFRA er Alexander og Espen — og sættes
   på, når teksten vises. Så overlever navnene en ny udskrivning.

Punkt 3 er det samme princip, der har reddet os hele vejen: gem id'et, slå
navnet op. Det gælder også her.

**Sådan blev det.** Rettelser ligger i deres egen fil (`Udskrift.GemRettet`), og gengivelsen `udskrift_<model>.txt` skrives om, så dokumenter og søgning læser det rettede. Talernavnene står i `meeting.json` og sættes på ved visning.

---

## Mappe og mødetype vælges, FØR der optages
*Besluttet 21-08-2026 · ændrer flowet omkring dokumenter og skabeloner*

I dag vælges mappen — og skabelonen — bagefter, når man skal lave et dokument.
Det er der to problemer med. Bagefter er der ingen, der gider rydde op, så alt
ender i den samme bunke; og dokumentdelen er svær at finde ud af, fordi
skabelonvalget kommer på et tidspunkt, hvor man tror, man er færdig.

**Ændringen:** når man trykker Optag — og når man starter et webinar — kommer
der en dialog, hvor man vælger **mappe**, **mødetype** og **sprog**. Alle tre
felter må kunne stå tomme eller stå på et fornuftigt udgangspunkt.

**Sproget skal blive ved at være der.** Webinardialogen spørger allerede, og
det samme spørgsmål hører til på en almindelig optagelse: appen gætter sproget
ud fra de første tredive sekunder, og gætter den forkert, er hele udskriften
ubrugelig. Det er også dét valg, der halverer tiden på et engelsk webinar. De
tre felter skal stå i den samme dialog — ikke to dialoger oven på hinanden,
før der overhovedet er trykket optag.

**Hvorfor mappen skal vælges først:** vælges den i situationen, hvor man ved,
hvad mødet handler om, lander materialet i den rigtige mappe fra begyndelsen.
Og det er dét, der giver den stærke søgning på tværs af historikken: «alt om
denne kunde», «alt fra dette fag». En søgning på tværs af en rodet bunke er en
søgning; en søgning inden for en mappe med to års møder er et opslagsværk.
Feltet findes allerede — `MeetingMetadata.Mappe` — så det er valget, der
flyttes, ikke datamodellen.

**Skabeloner hedder mødetyper.** Et statusmøde, en 1:1, et kundemøde, et
webinar — det er den slags, man kan svare på, før mødet går i gang. «Skabelon»
er et ord fra tekstbehandling, og det tvinger brugeren til at tænke i
dokumenter på et tidspunkt, hvor der ikke er noget dokument. Navnet ændres i
menuen og alle steder, brugeren ser det; de interne navne i koden kan blive
stående.

**Dokumentet bliver det, det er:** et tilvalg, der koster en sky-model. Så
spørgsmålet skal stilles rent — «Vil du have oprettet et komplet Word-dokument
med din online AI-model?» — og først dér, hvor man rent faktisk vil have et
dokument. Ikke som en skabelonliste, man skal forstå først.

**Omfang:** 305 forekomster af «skabelon» i 39 filer, hvoraf langtfra alle er
tekst til brugeren. Arbejdet er at gennemgå hvert sted, hvor dokumenter og
skabeloner nævnes, og få dialogen til at hænge sammen — ikke en søg-og-erstat.

---

## Etape 1 — Udskriften bliver til at stole på

### 1.1 Redigering af udskriften
**FÆRDIG** *Stor · ingen afhængigheder*

Udskriften bliver en rigtig teksteditor: tidsstempler i margen, talerens navn
på hver replik, og mulighed for at rette. Gemmes løbende.

**Hvorfor først:** den gør alt det andet mere værd, og den er den eneste
funktion på listen, der kan reducere, hvor meget der sendes i skyen. Kan man
finpudse selv, behøver man ikke bede en model om at rydde op.

Hver replik er et felt, man kan skrive i, med tidsstempel og taler ved siden af; der gemmes af sig selv kort efter sidste tastetryk.

**Tilbage:** fortryd på tværs af sessioner. Det er ikke bygget, og det er heller ikke afklaret, om det skal være der — den maskingenererede udgave ligger urørt ved siden af, så der er altid en vej tilbage til udgangspunktet.

### 1.2 Navne på talerne
**FÆRDIG** *Middel · bygger på to-spor-udskriften*

I dag står der HERFRA og DERFRA. Man skal kunne sætte navne på — sine egne og
gæsternes — og navnene skal huskes pr. person, så de foreslås næste gang de
samme folk er med.

**Hvorfor:** det er forudsætningen for at kunne søge på personer, og det er
det, der gør en udskrift læselig for andre end den, der var med.

**Grænsen skal stå:** vi kan skille de to SIDER ad, ikke de enkelte personer i
den anden ende. Navngivning er brugerens vurdering, ikke en måling — og det
skal fremgå, så ingen tror, maskinen har genkendt stemmerne.

---

## Etape 2 — Materialet kan komme ind udefra

### 2.1 Upload af lydfiler
**FÆRDIG 24-08** *Middel · ingen afhængigheder*

Træk en lydfil ind, og behandl den som en optagelse. Særligt fra telefon:
iPhones taleoptagelser er `.m4a`, og Whisper skal bruge 16 kHz mono WAV — så
der skal konverteres.

**Ffmpeg blev ikke nødvendig.** Windows' egne kodeks gennem Media Foundation læser `.m4a`, `.mp3`, `.aac`, `.wma`, `.flac`, `.mp4` og `.wav` og skriver dem om til de 16 kHz mono, whisper.cpp skal have. Ingen ekstra binær i installationen.

**Sådan blev det.** Knappen står ved siden af «Ny mappe» på Optagelser, og filer kan trækkes ind i listen fra Stifinderen — slippes de på en folder, lander de i den folder. Resultatet er en helt almindelig optagelsesmappe, så transskription, søgning, dokumenter og lydoprydning ikke behøver at vide, hvor lyden kom fra. Kildefilen kopieres og flyttes aldrig: den ligger typisk i en synkroniseret mappe, og en flytning ville slette den på telefonen.

**Efterprøvet 24-08** på en talememo fra en iPhone, hentet gennem iCloud: 29 sekunders lyd blev læst ind på 0,4 sekund og skrevet ud som dansk tekst med 99 % sprogsikkerhed. Optagelsen dateres efter filens egen dato, ikke efter hvornår den blev læst ind.

**En fil, der kun ligger i skyen**, hentes ned ved første læsning. Det siges, mens det sker — ellers ser en hentning ud som en app, der er gået i stå.

**Bemærket — og sagt:** en optagelse fra telefonen har ét spor. Så er der ingen opdeling i
HERFRA og DERFRA — alt står som ét. Det står tre steder: på optagelsen i træet, øverst på udskriften, og som sit eget ikon i listen.

### 2.2 Overvåget mappe (iCloud, Google Drev, OneDrive)
**FÆRDIG 24-08** *Lille · bygger på 2.1*

Peg appen på en mappe. Dukker der en lydfil op, tilbyder den at lægge den ind.

**Den vigtige forenkling:** alle tre tjenester har en Windows-klient, der
synkroniserer til en helt almindelig lokal mappe. Derfor kræver det her
**ingen OAuth, ingen tokens og ingen ny leverandør i Compliance** — appen ser
kun en mappe på disken. Optager du på iPhone med iCloud slået til, ligger filen
på pc'en af sig selv.

**Sådan blev det.** Metoden er skrevet ned i `overvaagede-mapper.md` — den står også kort på skærmen, hvor man slår den til. De fire regler, der bærer den: der kigges hvert minut, der læses aldrig i en fil, en fil skal have ligget stille i tyve sekunder, og hver fil tilbydes én gang.

**Pladsholderne blev håndteret, som 2.1 forudsagde.** iCloud lægger kun en pladsholder på disken; filen fylder nul, indtil nogen læser den. Derfor læses der aldrig i en fil under scanningen — kun navn, størrelse og dato. En pladsholder er stadig et fund, og at den skal hentes ned, står på skærmen, før man trykker.

**Skytjenesterne findes ved at spørge Windows**, ikke ved at gætte på stier under brugermappen. Det gæt blev prøvet og fejlede: iCloud Drive lå i `C:\iCloudDrive`, slet ikke under brugeren.

**Som standard overvåges ingenting.** Appen finder skytjenesterne og tilbyder én mappe i hver — `<skytjeneste>\NoteApp` — men listen er tom, indtil nogen siger ja. En app, der begynder at kigge i mapper af sig selv, er ikke en funktion.

Et rigtigt API mod de tre tjenester bliver først nødvendigt, hvis man vil
undvære synkroniseringsklienten. Det er en senere beslutning, ikke en
forudsætning.

**En iPhone-app, der optager direkte ind i mappen, er undersøgt og parkeret** — se `iphone-app.md` og posten på listen nedenfor. Den overvågede mappe står på egne ben og bliver ikke mindre værd af det: den virker for enhver telefon, enhver diktafon og enhver kollega, der sender en fil.

---

## Etape 3 — Møderne får en tidslinje

### 3.1 Kalender, lokal
**FÆRDIG** *Middel · ingen afhængigheder*

Opret og se møder i appen. Tidspunkt, titel, deltagere, sted eller link. Et
møde kan knyttes til en optagelse, og en optagelse til et møde.

**Version 1 fungerer helt uden integration.** Det er ikke en overgangsløsning:
en kalender, der kræver en konto hos Google for at virke, er ubrugelig for den,
der ikke vil have en.

### 3.2 Startside — dagens overblik
**FÆRDIG** *Middel · bygger på 3.1*

Forsiden bliver dagen og den kommende uge: hvad der sker i dag, hvad der ligger
forude, hvad der er sket, og hvad der venter — en optagelse, der aldrig blev
skrevet ud, et dokument, der aldrig blev åbnet, et åbent punkt uden frist.

### 3.3 Google Kalender
**FÆRDIG · verifikation hos Google udestår** *Stor · bygger på 3.1*

Læs møderne fra Google. Opret møder derfra — **og lad være, når man beder om
det.** Et møde, der er markeret som privat i appen, må aldrig havne i en delt
kalender.

**Compliance-konsekvensen er håndteret:** Google står på Compliance-siden under «Andre integrationer» med tilstand, områder og hvad der læses og skrives, og der er en popup, man skal kvittere for, før forbindelsen oprettes.

**Det, der mangler, er hos Google, ikke i koden:** verifikationen af appen. Projektet er udgivet til produktion og står som uverificeret — efterprøvet i konsollen 27-08-2026. Syv-dages-udløbet er dermed væk, og loftet på 100 konti er ikke bindende endnu. Tilbage står advarselsskærmen, som hver ny kunde skal klikke sig forbi; den er en salgsspærring og ikke en teknisk. Verifikationen kræver ingen sikkerhedsvurdering, fordi `calendar.events` og `tasks` er følsomme og ikke begrænsede områder; der ligger opgaver i Cockpittet om hjemmeside, privatlivspolitik og demovideo. Alt står i [`google-integration.md`](google-integration.md).

**Der hentes kun, når nogen beder om det.** Der er et synkroniseringsikon på kalenderen og på opgaverne i Cockpittet, med en linje under, der siger, hvornår der sidst blev hentet. Det kom 24-08, fordi en aftale oprettet direkte i Google ikke dukkede op — og den eneste vej frem var at gå to skærme væk til Indstillinger og trykke på en knap, man skulle vide fandtes.

**Og der hentes af sig selv hvert kvarter** samt ved opstart — også mens der optages. Vagten rører hverken mikrofonen, grafikkortet eller disken ud over to små filer, så der er intet, den kan komme i vejen for. Fejler en hentning, står fejlen på integrationens linje og under ikonet — der kommer aldrig en besked midt i et møde, fordi Google ikke svarede.

### 3.4 Microsoft 365-kalender
**IKKE BYGGET** *Stor · efter 3.3*

Samme som 3.3. Google-vejen står nu og virker, så spærringen er væk — men den bør vente, til Googles verifikation er i hus. Bliver der brug for at ændre på områder eller samtykke undervejs, er det bedre at lære det ét sted end to.

---

## Etape 4 — Det hænger sammen

### 4.1 Søgning på datoer, personer og metadata
**DELVIST** *Middel · bygger på 1.2 og 3.1*

Søgningen kan i dag filtrere på dato, mappe, mødetype og sprog. **Personen mangler** — «hvad talte vi med Espen om i august» kan endnu ikke besvares. Navnene findes nu (1.2), men de står i `meeting.json` og indgår ikke i det, der søges i.

### 4.2 Mødeserier
**IKKE BYGGET** *Middel · bygger på 3.1*

Et ugentligt statusmøde er ikke tredive løsrevne møder. Dokumentet skal vide,
hvad der blev besluttet sidst, og skabelonen skal kunne spørge: hvad er der
sket siden? Åbne punkter bæres videre, indtil nogen lukker dem.

### 4.3 Noterne bliver til strukturen
**IKKE BYGGET** *Middel/stor · bygger på 1.1, som nu er færdig*

Skriver du «budget?» klokken 14:12, skal dokumentet have et afsnit om
budgettet. Dine noter er din prioritering — det eneste signal i hele kæden om,
hvad DU syntes var vigtigt.

Med redigeringen på plads kan noterne vises inde i udskriften, dér hvor de blev
skrevet. Det er halvdelen af arbejdet.

---

## Stadig på listen, uden for etaperne

| Punkt | Indsats | Bemærkning |
|---|---|---|
| ~~**Åbne punkter som ét register**~~ | — | **FÆRDIG 24-08.** Alle opgaver ligger i én fil, `Opgaver/opgaver.json`, og vises samlet i Cockpittet |
| **Klik på en sætning, hør den** | Lille/middel | Tidsstemplerne findes. Hører naturligt sammen med 1.1 |
| **Mål mødet, ikke modellen** | Middel | Hvor meget var uhørligt, hvor mange talte i munden på hinanden, hvem sagde aldrig sit navn |
| **Maskering før afsendelse** | Stor | Bliver mindre presserende, hvis 1.1 betyder, at man tit slet ikke sender noget |
| **Forbrugsloft i appen** | Lille | Mistrals API har ingen vej til kontoens loft — efterprøvet 19-08, alle betalingsstier svarer 404. Skal derfor tastes ind |
| **Skal læseruden se anderledes ud?** | Lille/middel | Afventer, at lys/mørk er brugt i praksis. Uddybet nedenfor |

### Vågeordet skal kunne høres, MENS man taler
*Foreslået 31-08-2026*

whisper-command afgør først noget, når man holder pause. Dens lytteløkke henter
to sekunders lyd, spørger om de sidste 1000 ms er faldet til ro i forhold til
resten, og bedømmer først derefter vinduet mod listen af udtryk.

Det betyder, at man skal sige «Hej Pia», holde en kort pause, og så tale.
Rytmen står i brugerfladen, fordi den er nødvendig — men det er en
brugerflade, der beder om at blive tilpasset maskinen frem for omvendt.

**Hvorfor:** Målt 31-08-2026 sagde brugeren «Hej Pia» og talte videre uden
ophold. Så indeholdt det vindue, der blev bedømt, slutningen af sætningen og
ikke vågeordet — og der skete ingenting, uanset hvor tydeligt det blev sagt.
Han nåede at sige hele sætningen, før appen meldte, at den lyttede.

Den rigtige løsning er at lytte efter vågeordet i appen selv, på føroptagerens
ring, i overlappende vinduer på omkring halvandet sekund. Så opdages ordet
midt i en sætning, og pausen er ikke længere et krav. Ringen findes allerede
(15 sekunder, se `Foroptager`), og motoren ligger på grafikkortet i forvejen.

### Valgfri lokal diktering ved siden af skyen
*Foreslået 31-08-2026*

Brugeren skal kunne vælge motor — eller appen skal kunne falde tilbage til den
lokale, når der ikke er internet.

**Hvorfor:** Voxtral afviser `da` som sprogvalg. Sproget kan derfor kun
*påvirkes* gennem prompten, aldrig *vælges*. Målt 31-08-2026 kom en dansk
diktering tilbage på tysk — «Hi Pia, kannst du mir helfen» — fordi brugerens
egen ordliste på fyrre overvejende engelske fagord vejede tungere end den ene
danske ledetråd. Prompten er strammet siden, men det er stadig en påvirkning
og ikke en garanti.

Lokal whisper tager imod `-l da`. Det er den eneste måde at fjerne gættet helt
— og lyden ville slet ikke forlade maskinen, hvilket passer med, at lokalt går
forud for skyen.

**Skal undersøges først:** hvad det kræver af maskinen. Vågeordets model fylder
487 MB på grafikkortet og er målt til under otte procent af et RTX 2060.
Dikteringen ville bruge en større model og skal måles for sig — både i tid pr.
klip og i, om udskriften bliver bedre eller dårligere end Voxtrals. Bliver den
dårligere, er svaret to motorer at vælge imellem, ikke én ny.

### Appen justerer sig selv ud fra brug — og kan skrue tilbage
*Foreslået 21-08-2026*

Appens indstillinger er fundet ved måling: talergenkendelsens tærskel på 0,80,
kommandogrænsen på 0,30, støjgrænsen på 3 %. De er målt på ÉN stemme, ÉN
mikrofon og ét møde. Hos en anden kunde med en anden mikrofon rammer de
sandsynligvis ikke lige så godt.

Men brugeren afslører hele tiden, hvornår appen tog fejl — uden at gøre noget
ekstra:

| Handling, der allerede findes | Hvad den fortæller |
|---|---|
| «Læg alle replikker uden taler hos Espen» | Stemmerne blev delt for fint |
| Henfør én replik til en anden taler | Grænsetilfælde |
| «Kommando ikke forstået» → siges igen → rammer | Grænsen var for stram |
| En kommando udført, som straks fortrydes | Grænsen var for løs |

Efter hver tiende transskription tælles mønstrene op. Er der en systematisk
skævhed, kommer der et tal på klokken med **beviset**, ikke bare konklusionen:
«I dine sidste 12 møder har du 9 gange lagt en stemme sammen med en anden. Det
tyder på, at stemmerne bliver delt for fint.» Én knap ændrer det.

**Der skal kunne skrues tilbage.** Hver ændring gemmes med dato, den gamle
værdi og hvad der udløste den, og enhver af dem kan fortrydes. Uden det er en
selvjusterende app noget, man ikke tør sige ja til: har man først sagt ja tre
gange, aner man ikke, hvad indstillingerne står på, eller hvordan man kommer
tilbage til noget, der virkede. Historikken skal desuden kunne læses — «hvad
har appen ændret på mig?» er det første spørgsmål, nogen stiller.

**Stram frit, løsn kun efter at have spurgt.** Kommandogrænsen kan strammes
uden risiko — flere afvisninger er irriterende, ikke skadelige. At løsne den
bevæger sig mod klippekanten: målt 21-08-2026 ligger den ved 0,48, hvor «Hun
nævnte noget om en frist på fredag» begynder at udløse en opgave. En automatik,
der løsner for at fange flere kommandoer, ville gå netop den vej — og den fejl,
den så laver, er præcis den, hele designet er bygget for at undgå.

**Forslagene skal være få og diskrete.** Ti-tyve møder er nok til at se en
skævhed, ikke til at finjustere et tal. Altså 0,80 → 0,85, ikke 0,80 → 0,83.

**Og det skal siges, at det kun gælder fremad.** En ændret tærskel for
talergenkendelse virker først på kommende møder; de gamle skulle diariseres om,
og det tager ni minutter pr. møde.

**Hvorfor:** indstillingerne er i dag rigtige for én maskine, og der findes
ingen vej til at gøre dem rigtige for en anden uden at måle forfra. Brugerens
egne rettelser ER målingen — den laver sig selv, hvis der bliver talt efter.

**Hvad der IKKE kan justeres sådan:** opsummeringens kvalitet. Efterprøvningen
tæller ubelagte påstande, men et tal siger ikke, hvilken indstilling der skulle
være anderledes. Og whisper-modellen har ingen knap.

---

### HeyPia på iPhone — **PARKERET 24-08-2026**
*Foreslået og undersøgt 24-08-2026 · hele undersøgelsen står i `iphone-app.md`*

En app, der optager møder og telefonsamtaler på telefonen og lægger lyden i en
mappe ved navn HeyPia i iCloud. Undersøgt til bunds og lagt væk igen — ikke
fordi den er dårlig, men fordi den koster en Mac, 99 USD om året og et
selvstændigt produkt at vedligeholde, og fordi den halvdel, der var mest
efterspurgt, ikke kan lade sig gøre.

**Hvorfor den skal blive stående her:** spørgsmålet «kan vi ikke optage
telefonsamtaler på iPhone» kommer igen. Svaret er slået op og har kilder, og
det tager en halv dag at finde frem til anden gang.

**Det korte af, hvad vi lærte:**

- Ingen app kan optage lyden fra et almindeligt opkald. Mikrofonen er
  opkaldets alene, også på højttaler.
- En app kan ikke åbne sig selv, når et opkald begynder. Genveje har ingen
  udløser for opkald overhovedet.
- Apples egen opkaldsoptagelse fra iOS 18.1 er **slået fra i hele EU** og kan
  **ikke dansk**. Når Apples egen er lukket her, er der ingen bagdør.
- **Men en VoIP-app må optage sin egen lyd.** Ringer man op gennem appen,
  falder alle tre mure. Fire veje er beskrevet, og den billigste kræver
  ingen app: et nummer hos en nordisk udbyder, der viderestiller til mobilen
  og optager undervejs.

**Det, der IKKE er parkeret med den**, fordi det gælder Windows-siden: iCloud
lægger kun en pladsholder på disken, så en overvåget mappe (2.2), der måler
på filstørrelsen, ser ingenting. Det er noteret under 2.2.

**Hvad der skulle ændre sig, før den tages op igen:** at der er kunder nok til
at bære et produkt mere — eller at nogen efterspørger opkaldsoptagelse så
hårdt, at den nordiske VoIP-vej er en forretning i sig selv.

---

### Skal læseruden se anderledes ud end resten af appen?
*Foreslået 28-08-2026 · afventer, at omskifteren er brugt i praksis*

Udskriften og teleprompteren læses i lange stræk, og det er noget andet end at
klikke sig rundt i en app. To muligheder står åbne:

- **A — ruden bliver mørk, også når appen er lys.** Den oprindelige begrundelse
  taget alvorligt. Kræver et andet sæt pensler til netop den rude; paletten
  gælder hele appen på én gang, så det er reelt arbejde og ikke en indstilling.
- **B — ruden bliver papir.** Varm bund, større skrift og en begrænset
  linjebredde, så øjet ikke skal vandre helt ud til kanten. Tættere på det,
  Granola gør, og billigere.

**Hvorfor det venter:** Appen fik lys/mørk med et valg 28-08-2026, og
begrundelsen for at gøre den mørk fra begyndelsen stod i App.xaml:
«teleprompteren læses i lange stræk, og lys tekst på mørk bund trætter mindre».
Den begrundelse er ikke væk — men den kan nu løses ved at skifte tema om
aftenen i stedet for at bygge noget. Om det er nok, kan kun afgøres ved at
bruge appen en uges tid.

**Hvis den bliver taget op:** hælder til B. Den løser det egentlige problem ved
lange stræk — linjelængden — hvor A kun flytter farven. Og A gør appen
tosproget i sit eget udseende, hvilket er en pris, der skal tjenes ind.

---

## Bygget uden for etaperne, 24-08-2026

De står her, fordi de ikke var på listen, da den blev skrevet — og en roadmap,
der kun nævner det planlagte, giver et forkert billede af, hvor produktet er.

| Hvad | Hvorfor det kom nu |
|---|---|
| **Google Tasks** | Opgaver skrevet i Google dukker op i Cockpittet, og et flueben her krydser dem af der. Eget samtykke, eget område |
| **Sletning af lydfiler efter tid** | Standard 365 dage, og aldrig uden en udskrift. Med oversigt over diskplads i MB/GB og procent, og et spørgsmål straks efter lange møder |
| **Note om mødetransskription i invitationen** | Dansk og engelsk, kan rettes, og sættes kun på møder, der faktisk er markeret til optagelse |
| **Automatisk optagelse fra kalenderen** | Flytter mødet sig i Google, flytter optagelsen med. Kræver at appen kører — det står under Indstillinger |
| **Anbefalet minimumskonfiguration** | Læser maskinen og siger, hvad der er opfyldt, i stedet for at liste krav, brugeren selv skal måle |
| **Spalter, man kan trække i** | Bredden huskes. Kalender og opgaver starter fra toppen, søgning og resultat er sat sammen |
| **Salgsargumenter som dokument** | `doc/salgsargumenter.md` — hvert argument med sit belæg og med det, der **ikke** må siges |

**Målt og fravalgt: komprimering af lyden.** Ét sprog tåler 32 kbit/s uden tab,
men blandet dansk-engelsk går fra 20,08 % til 40,57 % ordfejl. Appen kan ikke
vide på forhånd, hvilken slags møde det er, så lyden bliver ukomprimeret.
Målingen står i `doc/findings.md` §9.2.

---

## Målinger, der mangler

### Find ud af, hvad der egentlig gør en lang kørsel langsom
*Foreslået 14-08-2026*

Et 61-minutters møde tog 28 minutter i én kørsel — 1162 tokens ud på 1681
sekunder. Det blev regnet om til «0,7 tokens i sekundet» og brugt som argument
for, at modellen lå på processoren.

**Hvorfor det skal undersøges:** Tallet er ikke troværdigt. Det er svarets
tokens delt med HELE forløbet, og forløbet indeholder også indlæsningen af
12.513 tokens prompt — en fase, hvor der ikke kommer tokens ud. De to ting er
blandet sammen, og resultatet ligner en langsom model uden at bevise det.

Mistanken om, at video i baggrunden pressede modellen af kortet, er afvist:
målt med YouTube kørende bruger kortet 401 MiB, præcis som når den er slukket.

### Stykkevis sprogdetektering ved sprogskifte
*Foreslået 13-08-2026 · delvist overhalet*

Whisper finder sproget én gang ud fra de første tredive sekunder. Skifter mødet
sprog undervejs, opdager den det ikke.

**Status:** to-spor-udskriften løser den almindelige udgave af problemet — et
dansk-norsk møde er nu to spor med hvert sit sprog. Tilbage står det tilfælde,
hvor den SAMME taler skifter sprog midt i mødet.

### Måle om mikrofon og afstand er det store håndtag
*Foreslået 14-08-2026*

Læs den blandede tekst op igen med bevidst god mikrofonafstand og sammenlign
med de 83,1 %.

**Hvorfor:** Alle de håndtag, vi har prøvet, er målt til næsten ingenting —
ordlisten til Whisper gav nul, sprogmodellen over udskriften rettede to ord ud
af 3418. Lydkvaliteten er den eneste tilbageværende mulighed for et spring frem
for en decimal, og den er aldrig blevet målt.

---

## Lagt væk

### Diktering ind i et hvilket som helst felt
*Foreslået 19-08-2026 · lagt væk 20-08-2026*

Tryk en tast, tal, og teksten står, hvor markøren er.

**Hvorfor ikke:** det kan ikke laves ordentligt her. Den del, der gør Wispr
Flow god — at ordbogen lærer af det, man retter bagefter i det program, man
skriver i — kræver, at man overvåger tastetryk på tværs af hele maskinen. Det
modsiger alt, appen ellers står for.

Og oprydningen af «øh» og selvrettelser kræver en sprogmodel over alt, hvad man
dikterer, halvtreds gange om dagen. Det er en langt bredere eksponering end en
mødeudskrift, og den er svær at forsvare.

Halvdelen af værdien kan hentes uden det — en ordbog, der retter EFTER
udskriften frem for at styre Whisper før. Den idé lever videre; det er
dikteringen, der er lagt væk.

### Sprogmodellen skal rette Whispers fejl
*Prøvet af og fravalgt 14-08-2026*

Idéen: giv Qwen den rå udskrift og bed den rette åbenlyse fejlhøringer ud fra
sammenhængen, før der laves dokumenter.

**Hvorfor ikke:** Målt på alle tre prøvetekster. Dansk 92,3 → 92,4 %, engelsk
90,5 → 90,7 %, blandet 83,1 → 83,1 %. **To ord ud af 3418** for 5 minutter og
22 sekunders GPU-tid.

Grunden er værd at huske: en sprogmodel kan kun rette det, der SER forkert ud.
«cybernummer» ser forkert ud — men «går» i stedet for «gik», «af» i stedet for
«at» læser fuldstændig naturligt. Der er intet signal at gå efter, og det er
dér, tre fjerdedele af fejlene ligger.
