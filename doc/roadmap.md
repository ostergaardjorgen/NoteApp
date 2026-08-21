# Roadmap

*Skrevet om 20-08-2026, efter at retningen blev lagt fast.*

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
*Stor · ingen afhængigheder · **start her***

Udskriften bliver en rigtig teksteditor: tidsstempler i margen, talerens navn
på hver replik, og mulighed for at rette. Gemmes løbende.

**Hvorfor først:** den gør alt det andet mere værd, og den er den eneste
funktion på listen, der kan reducere, hvor meget der sendes i skyen. Kan man
finpudse selv, behøver man ikke bede en model om at rydde op.

**Skal afklares:** beslutningen ovenfor. Og om der skal være fortryd på tværs
af sessioner.

### 1.2 Navne på talerne
*Middel · bygger på to-spor-udskriften (færdig)*

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
*Middel · ingen afhængigheder*

Træk en lydfil ind, og behandl den som en optagelse. Særligt fra telefon:
iPhones taleoptagelser er `.m4a`, og Whisper skal bruge 16 kHz mono WAV — så
der skal konverteres.

**Skal afklares:** hvad der konverterer. En ekstra binær (ffmpeg) er 40-80 MB
og en ny ting at holde opdateret; alternativet er at afkode `.m4a` med Windows'
egne kodeks, som er der i forvejen.

**Bemærk:** en optagelse fra telefonen har ét spor. Så er der ingen opdeling i
HERFRA og DERFRA — alt står som ét. Det skal siges i udskriften, ikke opdages.

### 2.2 Overvåget mappe (iCloud, Google Drev, OneDrive)
*Lille · bygger på 2.1*

Peg appen på en mappe. Dukker der en lydfil op, tilbyder den at lægge den ind.

**Den vigtige forenkling:** alle tre tjenester har en Windows-klient, der
synkroniserer til en helt almindelig lokal mappe. Derfor kræver det her
**ingen OAuth, ingen tokens og ingen ny leverandør i Compliance** — appen ser
kun en mappe på disken. Optager du på iPhone med iCloud slået til, ligger filen
på pc'en af sig selv.

Et rigtigt API mod de tre tjenester bliver først nødvendigt, hvis man vil
undvære synkroniseringsklienten. Det er en senere beslutning, ikke en
forudsætning.

---

## Etape 3 — Møderne får en tidslinje

### 3.1 Kalender, lokal
*Middel · ingen afhængigheder*

Opret og se møder i appen. Tidspunkt, titel, deltagere, sted eller link. Et
møde kan knyttes til en optagelse, og en optagelse til et møde.

**Version 1 fungerer helt uden integration.** Det er ikke en overgangsløsning:
en kalender, der kræver en konto hos Google for at virke, er ubrugelig for den,
der ikke vil have en.

### 3.2 Startside — dagens overblik
*Middel · bygger på 3.1*

Forsiden bliver dagen og den kommende uge: hvad der sker i dag, hvad der ligger
forude, hvad der er sket, og hvad der venter — en optagelse, der aldrig blev
skrevet ud, et dokument, der aldrig blev åbnet, et åbent punkt uden frist.

### 3.3 Google Kalender
*Stor · bygger på 3.1*

Læs møderne fra Google. Opret møder derfra — **og lad være, når man beder om
det.** Et møde, der er markeret som privat i appen, må aldrig havne i en delt
kalender.

**Compliance-konsekvens, der skal afklares først:** det bliver den anden
tjeneste, appen taler med, og den første, hvor appen holder et fremmed token.
Kalenderdata er personoplysninger — titler og deltagere. Det skal stå på
Compliance-siden, og kvitteringerne skal dække det, ligesom de dækker Mistral.

### 3.4 Microsoft 365-kalender
*Stor · efter 3.3 · afventer*

Samme som 3.3. Tages først, når Google-vejen står og virker.

---

## Etape 4 — Det hænger sammen

### 4.1 Søgning på datoer, personer og metadata
*Middel · bygger på 1.2 og 3.1*

Søgningen er i dag fritekst. Den skal kunne svare på «hvad talte vi med Espen
om i august» — altså søge på person, på dato og på mødetype, ikke kun på ord.

### 4.2 Mødeserier
*Middel · bygger på 3.1*

Et ugentligt statusmøde er ikke tredive løsrevne møder. Dokumentet skal vide,
hvad der blev besluttet sidst, og skabelonen skal kunne spørge: hvad er der
sket siden? Åbne punkter bæres videre, indtil nogen lukker dem.

### 4.3 Noterne bliver til strukturen
*Middel/stor · bygger på 1.1*

Skriver du «budget?» klokken 14:12, skal dokumentet have et afsnit om
budgettet. Dine noter er din prioritering — det eneste signal i hele kæden om,
hvad DU syntes var vigtigt.

Med redigeringen på plads kan noterne vises inde i udskriften, dér hvor de blev
skrevet. Det er halvdelen af arbejdet.

---

## Stadig på listen, uden for etaperne

| Punkt | Indsats | Bemærkning |
|---|---|---|
| **Åbne punkter som ét register** | Lille | Billigste vinding, der er tilbage. Bliver bedre efter 3.1, men kan laves nu |
| **Klik på en sætning, hør den** | Lille/middel | Tidsstemplerne findes. Hører naturligt sammen med 1.1 |
| **Mål mødet, ikke modellen** | Middel | Hvor meget var uhørligt, hvor mange talte i munden på hinanden, hvem sagde aldrig sit navn |
| **Maskering før afsendelse** | Stor | Bliver mindre presserende, hvis 1.1 betyder, at man tit slet ikke sender noget |
| **Forbrugsloft i appen** | Lille | Mistrals API har ingen vej til kontoens loft — efterprøvet 19-08, alle betalingsstier svarer 404. Skal derfor tastes ind |

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
