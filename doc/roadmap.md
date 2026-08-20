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
