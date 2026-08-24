# Findings — hvad vi har målt, og hvad det betyder

Denne fil er hukommelsen på tværs af sessioner. Alt, der er blevet **målt**, står her med tal og dato, så beslutningerne kan efterprøves — og så den samme ting ikke bliver undersøgt to gange.

**Regler for filen:**

- Kun det, der er målt. Et skøn skrives som et skøn, med det ord.
- Hvert fund har en dato og en konsekvens. Et fund uden konsekvens er en anekdote.
- Fravalg bliver stående. Slettes de, bliver de gentaget om et halvt år af nøjagtig de samme gode grunde.
- Rettes et fund af en senere måling, bliver det gamle stående med en note. Historikken er en del af beslutningsgrundlaget.

**Maskinen, alle tal er målt på:** RTX 2060 med 6144 MiB VRAM, i7-1165G7 med 4 kerner, 31,7 GB RAM, Windows 11.

---

## Søgningen målt på tyve spørgsmål med kendt facit — 21-08-2026

*Det tal, alt senere arbejde med søgningen skal måles imod. Køres igen med
`noteapp maalsoegning`.*

Tyve spørgsmål til det arkiv, der lå: et møde på en time (dansk og norsk) og
to webinarer på 22 og 54 minutter (engelsk). Facit er den optagelse, svaret
står i, og prøven kontrollerer FØRST, at ordene rent faktisk står dér — er
facit forkert, er målingen værdiløs.

| Mål | Resultat |
|---|---|
| Facit på førstepladsen | **18 af 20 — 90 %** |
| Facit i top tre | **20 af 20 — 100 %** |
| Slet ikke fundet | **0** |
| Tid i gennemsnit | **4,8 ms** |

### Det, målingen fandt

**Dokumenter slog udskriften.** «crowdstrike» lå på tredjepladsen, fordi to
referater AF det samme møde lå foran mødet selv. Det er forkert efter
produktets eget princip: dokumentet er en genfortælling, udskriften er det,
der blev sagt. Rettet med en rangordning på kildetype — udskrift, så note, så
dokument. Det flyttede «crowdstrike» fra tredje til første og «webinar» fra
fjerde til anden, altså 85 % → 90 % på førstepladsen.

**«I år» begyndte 31-12-2025 kl. 23.** Datoen blev bygget med sommertidens
forskel på to timer, mens januar er på én. En time og ét døgn for meget i hver
ende af en sommertidsgrænse — lille nok til aldrig at blive opdaget, stor nok
til at et møde 31. december dukker op under «i år». Rettet ved at spørge
tidszonen om forskellen PÅ DEN DATO.

### De to, der ikke er nummer ét — og hvorfor de får lov

**«one identity»** — leverandøren One Identity står i mødet, men ordene
«one» og «identity» står også ved siden af hinanden i det ene webinar i
almindelig prosa. Søgningen kan ikke vide, hvilken der menes. At tvinge den
rigtige frem ville være at tilpasse koden til prøven.

**«webinar»** — ordet står i alle optagelser, fordi de handler om webinarer.
Det er ikke et spørgsmål med ét rigtigt svar.

### Hvad tallet betyder for semantisk søgning

90 % på førstepladsen med rene ordsøgninger. **Beslutningen om indlejringer
kan ikke træffes på det her tal alene** — prøverne er alle sammen ord, der
STÅR i teksten, og det er netop dét, leksikalsk søgning er god til. Den
måling, der mangler, er tyve spørgsmål stillet med ANDRE ord end dem, der
blev sagt: «hvad sagde de om prissætning», «ham fra Norge om at være
underdog». Det er dér, en indlejringsmodel enten tjener sig hjem eller ikke
gør, og det kan først måles ærligt, når arkivet er stort nok til, at man ikke
selv kan huske svaret.

---

## Udtræk af navne med en lokal model — målt 21-08-2026

*Spørgsmålet: kan appen svare på «find de leverandører, der er nævnt i denne
måneds møder» uden at sende noget ud af huset?*

Målt på tre rigtige optagelser med Qwen3-4B-Q4_K_M på RTX 2060.

### Hele udskriften på én gang — virker ikke

| Optagelse | Tid | Resultat |
|---|---|---|
| Møde med Cloudworks, 55 KB | 2,8 s | 1 navn, 0 % stod i udskriften |
| Webinar, 50 KB | 28,2 s | 113 navne, **45 % fundet på** |

De 113 var det samme navn igen og igen — «Limity», «Elimiti», «Limity» —
altså en gentagelsesløkke. Det er den samme grænse, opsummeringen stødte på
20-08: en model på 4B holder ikke til tyve tusind tokens og et løfte om at
være præcis.

### I blokke på 6.000 tegn — virker

| Optagelse | Blokke | Tid | Modellen sagde | Kasseret | Tilbage |
|---|---|---|---|---|---|
| Cloudworks, 55 KB | 10 | 60,4 s | 117 | 25 | 92 |
| Webinar, 22 KB | 4 | 21,4 s | 40 | 7 | 33 |
| Webinar, 50 KB | 9 | 48,5 s | 101 | 30 | 71 |

Ingen gentagelsesløkker. **Omtrent ét sekund pr. KB udskrift — cirka et minut
pr. times møde.** Til sammenligning tager selve udskrivningen fem minutter pr.
times lyd, så udtrækket er få procent oveni.

**Efterprøvningen mod kilden kasserede 18-30 % af det, modellen sagde.** Den
er ikke pynt: uden den ville hvert fjerde navn i registeret være opfundet, og
det ville se lige så rigtigt ud som resten.

### Det, der IKKE er løst

Det, der overlever, er en blanding af rigtige leverandører — Omada, SailPoint,
Okta, IBM, CrowdStrike, Elimity, SAP, Power BI — og almindelige ord: «dag»,
«uge», «kunder», «ansatte», «prøve», «governance».

Efterprøvningen svarer på «står ordet i udskriften», ikke på «er det et navn».
Det andet spørgsmål mangler, og det er dét, der afgør, om et register kan
bruges. Tre veje, alle billige og alle målbare:

1. Krav om stort begyndelsesbogstav midt i en sætning
2. Krav om at ordet optræder mindst to steder
3. En liste over almindelige ord, der aldrig er navne

**Konsekvens:** kraften er der, og arkitekturen er rigtig — træk ud ÉN gang,
når optagelsen skrives ud, og gem det på mødet. Så er spørgsmålet et opslag i
metadata og svarer på millisekunder, offline. Men udtrækket skal have et led
mere, før registeret er til at stole på, og det led skal måles på de samme tre
optagelser.

---

## 1. Sprogmodeller

### 1.1 Qwen3-8B Q4_K_M — i brug

*Målt 12. august 2026 på `testtekst.md` (20 minutters dansk møde, 13.445 tegn, ~4.481 tokens) holdt op mod `facitliste.md`.*

| Mål | Resultat |
|---|---|
| Tid | ca. 160 sekunder |
| Dansk | holder hele vejen, ingen drift til engelsk |
| Struktur | korrekt: resumé, beslutninger, opgaver med ejer, åbne spørgsmål |
| Beslutninger | 6 af 6 |
| Opgaver med ejer | 5 af 7 |
| Åbne spørgsmål | 4 af 7 |
| **Fejl** | **nødadgangens loft skrevet som 48 timer i alle 8 kørsler** |

Kilden siger *"et loft på fireogtyve timer"*, efterprøvet i `testtekst.md`. Fejlen blev gentaget efter at skabelonen udtrykkeligt fik besked på aldrig at skrive et tal, der ikke står i udskriften.

**Konsekvens:** kæden virker, modellen kan ikke bruges ubelæst. Et hallucineret tal i et referat er den dyreste fejl, der findes — det ser rigtigt ud. Skal blive stående som et krav til UI'et: et udkast skal præsenteres som et udkast, der skal læses igennem.

### 1.2 Mistral-Small-24B Q4_K_M — undersøgt og fravalgt

*Målt 12. august 2026 på præcis samme tekst og facitliste som Qwen3.*

To kørsler, afbrudt efter **50 og 20 minutter**, uden at have skrevet et referat færdigt. Qwen3-8B klarer samme tekst på 160 sekunder.

Målt under kørslen: **5,8 GB på grafikkortet, 14,1 GB i almindelig RAM.**

**Årsagen er ikke kvaliteten, men størrelsen.** Modellen fylder 14,3 GB og kortet har 6,1. llama.cpp lægger det, der kan være, på kortet og resten i RAM — og de lag, der ligger i RAM, sætter tempoet for det hele.

**Konsekvens:** fjernet fra standardlisten i `LlmCatalog.cs`, men **ikke** slettet. Begrundelsen står i `Rejected`-feltet, så den, der undrer sig over, hvorfor en kendt god model ikke er med, kan se svaret i appen frem for at prøve den af igen.

**Grænsen, dette sætter:** en model skal kunne ligge på kortet. Med 6 GB VRAM betyder det i praksis modeller op til cirka 5 GB på disken. Det er ikke en holdning, det er de 160 sekunder mod de 50 minutter.

*Ikke afklaret:* om Mistral-Small-24B ville være god nok fagligt. Det blev aldrig målt, fordi den aldrig blev færdig. Spørgsmålet er kun relevant igen på en maskine med mere VRAM.

**Filen er slettet 12. august** — 13,35 GB frigjort. Modellen kan heller ikke længere hentes: `noteapp sprogmodel mistral-small-24b` afviser med begrundelsen og henviser hertil. Skal den ind igen, skal fravalget fjernes bevidst i `LlmCatalog.cs`.

### 1.3 Muse-Glimmer-30B UD-Q3_K_XL — fravalgt på denne maskine

*Målt 12. august 2026 på præcis samme tekst og facitliste som de to andre.*

Første forsøg blev kasseret, ikke afsluttet: en transskription kørte samtidig og havde taget kortet først. Anden kørsel fik maskinen for sig selv.

**Efter syv minutter var modellen ikke engang færdig med at blive indlæst.** Der var endnu ikke skrevet et eneste ord. Qwen3-8B er helt færdig med referatet på 160 sekunder.

Målt: **18,9 GB RAM, 3,6 GB tilbage af 32,5.** Modellen fylder 12,4 GB og kan slet ikke ligge på et 6 GB-kort, så alt kørte i almindelig hukommelse. Maskinen begyndte at swappe og holdt op med at svare — se 3.2.

Filen er slettet, 12,44 GB frigjort.

**Kvaliteten er stadig ukendt.** Den blev aldrig målt, fordi modellen aldrig nåede at skrive noget. Det er værd at holde fast i: fravalget siger intet om, hvor gode referater den laver.

---

## 1.4 Den vigtigste læring af de to fravalg

**Begge fravalg gælder denne maskine, ikke modellerne.**

Grænsen, der blev fundet, er ikke "24B- og 30B-modeller er dårlige". Den er:

> **En model, der ikke kan ligge på grafikkortet, er ubrugelig i praksis.** Ikke langsom — ubrugelig. Forskellen er 160 sekunder mod «ikke færdig efter syv til halvtreds minutter».

På den nuværende hardware (RTX 2060, 6144 MiB) betyder det modeller op til cirka 5 GB på disken. Det er derfor Qwen3-8B er valget — ikke fordi den er den bedste model, men fordi den er den bedste af dem, der kan være der.

**Hvad en større maskine ville ændre:**

| Kort | VRAM | Hvad der bliver muligt |
|---|---|---|
| RTX 2060 (nu) | 6 GB | op til ca. 5 GB model — Qwen3-8B |
| 16 GB-kort | 16 GB | Mistral-Small-24B (14,3 GB) helt på GPU |
| 24 GB-kort | 24 GB | Muse-Glimmer-30B (12,4 GB) med rigelig plads til kontekst |

Med et kort, der kan rumme dem, ville begge køre helt på GPU'en, og så er spørgsmålet et helt andet: **er de fagligt bedre nok til at retfærdiggøre maskinen?** Det er ikke målt, og det kan ikke måles her.

Det er derfor, fravalgene bliver stående i kataloget med begrundelsen frem for at blive slettet. Skifter hardwaren, er de to modeller de første, der skal prøves igen — og så skal `LlmCatalog.cs` og denne fil læses sammen, ikke gættes forfra.

**Der er også en produktkonsekvens:** appen skal sælges til maskiner, vi ikke kender. Kravet om, at modellen skal kunne ligge på kortet, gør VRAM til det tal, der afgør, hvad en bruger kan bruge appen til. Det bør stå i systemkravene med rigtige tal frem for som «et NVIDIA-kort anbefales».

---

## 2. Rørføringen omkring llama.cpp

*Fundet 12. august 2026, hver enkelt ved at køre det.*

**`llama-completion` er en fortsættelses-motor, ikke en chat-klient.** Den anvender ikke chat-skabelonen, heller ikke med `--jinja`, og ignorerer systemprompten. Første kørsel gentog hele mødet ordret og gik derefter i ring, til tokenloftet stoppede den. `llama-cli` er chat-klienten og kører **fem gange hurtigere** (36 mod 6,9 tokens/sek).

**`-st` skal med**, ellers bliver `llama-cli` stående som chat og afslutter med kode 130, når stdin lukkes.

**`--jinja` skal med**, ellers bruges modellens chat-skabelon ikke.

**`--reasoning-budget 0` skal med.** Qwen3 tænker højt på **engelsk** og kan gå i ring i det. Virker kun sammen med `--jinja`.

**Prompten gengives trods `--no-display-prompt`, og gengivelsen afkortes ved lange prompter.** Derfor springes den over tegn for tegn med mellemrum ignoreret, og der klippes ved afvigelsen — dér stopper gengivelsen, og svaret begynder.

**Prompten sendes i en fil, ikke på kommandolinjen.** Windows knækker ved cirka 32.000 tegn, og et 90-minutters møde fylder mere end det.

**`-ngl 999` fejler ikke på et for lille kort.** llama.cpp lægger det, der kan være, på kortet og resten i RAM. Målt: 5.846 af 6.144 MiB brugt, resten i RAM. Antagelsen i koden var rigtig, men var uefterprøvet indtil nu.

---

## 3. Ressourcer og samtidighed

*Målt 11.-12. august 2026.*

| Opgave | GPU | RAM | Disk |
|---|---|---|---|
| Optagelse | 0 | ubetydelig | 31 KB/s |
| Transskription, whisper large-v3 | 3.094 MB | — | — |
| Udkast, Qwen3-8B | ca. 4.700 MB | — | — |
| Udkast, Mistral-24B | 5.800 MB | 14.100 MB | — |

**Transskription og udkast kan ikke køre samtidig** på 6 GB: 3.094 + 4.700 > 6.144.

**Optagelse kan altid køre.** Én tråd, 31 KB/s disk, nul GPU. Det er ikke et held, det er en egenskab, og den skal fremhæves frem for at blive taget for givet: man kan optage et møde, mens maskinen transskriberer det forrige.

**Konsekvens, tilføjet 12. august:** medlytningen under oplæsning bruger whisper-stream og konkurrerer altså om kortet. Fejlede den, blev det ikke opdaget — se 4.2.

### 3.1 Konflikten set i praksis

*Målt 12. august 2026, utilsigtet: en transskription blev startet i appen, mens en modelkørsel var i gang.*

Ingen af dem fejlede. **Den, der nåede kortet først, beholdt det** — transskriptionen var startet og lå på GPU'en, og sprogmodellen blev henvist til RAM. Den stod på **21,7 GB RAM** mod de 14,1 GB, den samme slags kørsel brugte alene.

Det er den venlige udgave af konflikten: intet går i stykker, men den ene bliver mange gange langsommere uden at sige det.

**Konsekvens:** advarslen skal ikke handle om, at noget fejler — det gør det ikke. Den skal handle om, at den anden opgave bliver drastisk langsommere, og om hvilken af dem der bliver ramt: den, der starter sidst.

**Konsekvens for målinger:** en modelmåling foretaget under en transskription er ubrugelig. Den siger noget om konflikten, ikke om modellen.

### 3.2 Den alvorlige konflikt er ikke om grafikkortet — den er om RAM

*Målt 12. august 2026: appen holdt op med at svare og måtte lukkes hårdt.*

`HeavyJobLock` beskytter grafikkortet. Den beskytter ikke mod det, der faktisk væltede maskinen.

En model, der ikke kan være på kortet, lander i almindelig RAM. Muse-Glimmer på 12,4 GB stod på **18,9 GB RAM** og efterlod **3,6 GB af 32,5**. Så begyndte Windows at swappe, og alt holdt op med at reagere — også vinduer, der intet havde med sagen at gøre.

| Model | Filstørrelse | RAM under kørsel |
|---|---|---|
| Mistral-24B (delvist på kort) | 14,3 GB | 14,1 GB |
| Muse-Glimmer-30B (ikke på kort) | 12,4 GB | 18,9 GB |

Vægtene alene siger det ikke: konteksten, tokenbufferne og selve programmet kommer oven i. Tommelfingerreglen fra de to målinger er **filstørrelsen gange 1,4**.

**Ingen data gik tabt.** Efterprøvet efter den hårde lukning: `meeting.json` læses, `segmenter\` er tom, lydfilen er hel (910 sek), transskriptionen er intakt (165 linjer, 165 unikke), `learning.db` har sine 58 termer og efterlod hverken `-wal` eller `-journal`. Der kørte ingen optagelse på tidspunktet.

**Konsekvens:** `LlmRunner` kontrollerer nu den frie hukommelse før start og **kaster** frem for at advare og fortsætte. Når maskinen først er gået i stå, er der ingen, der kan nå at trykke afbryd. Kravet er `filstørrelse × 1,4 + 2 GB` til Windows og appen selv. Kan hukommelsen ikke aflæses, blokeres der ikke — en advarsel bygget på et tal, vi ikke har, er værre end ingen advarsel.

---

## 4. Fejl i vores egen kode, fundet ved at bruge den

### 4.1 Hardkodede filstørrelser — tre gange forkert

12. august blev **alle fem** modelstørrelser i `LlmCatalog.cs` sammenlignet med serverens:

| Model | Hardkodet | Server |
|---|---|---|
| qwen3-8b | 4.700.000.000 | 5.027.783.488 |
| mistral-small-24b | 14.333.115.680 | 14.333.908.672 |
| muse-glimmer-30b | 13.314.397.920 | 13.360.983.072 |
| gemma-3-12b | 6.400.000.000 | 6.909.282.688 |
| llama-31-8b | 4.900.000.000 | 4.920.739.232 |

Tidligere var det sket for to Whisper-modeller. Denne gang gjorde det skade: qwen3 lå færdighentet på disken med en størrelse, koden ikke genkendte, så den blev slettet og hentet igen ved hver kørsel.

**Konsekvens:** størrelser hentes fra serveren. Der må aldrig kontrolleres mod et tal, vi selv har gættet — kun mod det, serveren oplyser. Første udgave af genoptagelsen havde netop sådan en kontrol, og den ville have slettet en halv hentning på 10 GB, fordi katalogets tal var 793 KB skævt.

### 4.2 Medlytningen kunne ikke fejle synligt

Fundet under en rigtig oplæsning på 20 minutter: afsnittene skiftede aldrig af sig selv, mens skærmen blev ved med at skrive "lytter med small".

To fejl:

- Ingen `Exited`-håndtering. Døde `whisper-stream`, blev det ikke opdaget.
- `stderr` blev kasseret — og det er dér, grunden står.

**Konsekvens:** afkrydsningsfeltet slår fra ved fejl, årsagen vises, de sidste otte stderr-linjer gemmes, og en vagthund melder, hvis der ikke er hørt noget på 25 sekunder. En medlytning, der kører uden at høre noget, ligner præcis en, der virker.

### 4.3 Ordlisten fik Whisper til at gå i ring — den alvorligste hidtil

*Fundet 12. august 2026 på den første rigtige oplæsning.*

Den 15 minutters optagelse gav **388 linjer, hvoraf 372 var den samme sætning.** Teksten var brugbar de første 14 linjer — cirka to minutter — og gik derefter i ring med *"Vi har haft en stor udfald til en købmekanisk køb"* resten af vejen.

Isoleret på et fire minutters udsnit af den samme lyd:

| Kørsel | Linjer | Unikke | Udfald |
|---|---|---|---|
| uden ordliste | 44 | 41 | ok |
| **med ordliste (som appen kørte)** | **61** | **16** | **gik i ring** |
| med ordliste og `-mc 0` | 31 | 31 | ok |
| med ordliste og `-et 3.0` | 47 | 47 | ok, men 68 sek mod 32 |

**Årsagen er et samspil mellem to ting, der hver for sig er fornuftige.** Ordlisten sendes som `initial_prompt` — det er det, der får fagord og navne til at blive stavet rigtigt. Og whisper.cpp bærer som standard sin **egen tidligere udgang** med videre som kontekst til næste vindue. Rammer den én gentagelse, fodrer den sig selv med den, og der er ingen vej tilbage.

**Rettelsen er `-mc 0`:** bær ikke tidligere tekst med over. Ordlisten beholdes, loopet forsvinder.

Prisen er længere afsnit — færre linjebrud, samme indhold. Efterprøvet: begge kørsler slutter på nøjagtig den samme sætning, så der klippes intet af. `-mc 0` var samtidig den hurtigste af de to rettelser og ramte "leverandøren" korrekt, hvor `-et 3.0` skrev "demandørerne".

**Efter rettelsen, hele optagelsen på ny:** 165 linjer, **165 unikke**, 2338 ord mod manuskriptets 2349 — 99,5 %. RTF 0,29 (260 sekunder på 910 sekunders lyd). Sproget detekteret som dansk med 97 % sikkerhed.

**Hvorfor det ikke blev opdaget før:** alle tidligere prøver var korte, og loopet sætter først ind efter et par minutter. En fejl, der kræver et rigtigt, langt møde for at vise sig, findes ikke ved at prøve appen af i fem minutter.

**Restfejl, ikke løst:** sidste linje blev *"Danske tekster af Nicolai Winther"* — en kendt Whisper-tilbøjelighed til at digte undertekst-kreditering på afsluttende stilhed. Kosmetisk, men den skal fjernes, før et referat sendes videre.

### 4.4 Bedømmelsen af referater fandt på fejl

Første udgave af `scripts/bedoem-referat.ps1` rapporterede to fejl, modellen ikke havde begået:

- Mønsteret `(\d+) timer` fandt "25 timer" inde i **"301,25 timer"**.
- Ejere blev talt i hele teksten, så deltagerlisten talte med, og alle kørsler fik 7 af 7.

**Konsekvens:** tal læses i deres sammenhæng, forankret til det ord de hører til, og afsnit holdes adskilt. En bedømmelse, der finder på fejl, er værre end ingen bedømmelse. Efter rettelsen: 5 af 7 — det samme som en manuel gennemlæsning gav.

### 4.5 En gate, der fejlede på sin egen brugsanvisning

`leverancetjek` faldt med *"Cannot bind argument to parameter 'Path'"*, når den blev kaldt præcis som dokumenteret. `$PSScriptRoot` var brugt som standardværdi i `param`-blokken.

**Konsekvens:** stien findes efter `param`-blokken. En gate, der fejler på sin egen brugsanvisning, bliver sprunget over frem for rettet — og så er den ikke længere en gate.

### 4.6 Downloads kunne ikke genoptages

En sprogmodel fylder 13-14 GB og tager tyve minutter. Blev hentningen afbrudt, blev alt hentet igen fra nul.

**Konsekvens:** `Range`-anmodning oven på `.delvis`-filen. Målt: afbrudt ved 10,6 GB, fortsatte derfra, endte på 14.333.908.672 byte præcis. En bruger på en ustabil forbindelse ville aldrig have fået modellen hentet.

---

## 5. Sprog

*Ændret 12. august 2026.*

Whisper var låst til `-l da`. Holdes mødet på engelsk, bliver det ikke afvist — det bliver skrevet ned som dansk volapyk. **En fejl, der ligner et resultat, er værre end en fejlmeddelelse.**

Formatet er efterprøvet mod `whisper.dll` frem for gættet: `%s: auto-detected language: %s (p = %f)`.

**Whisper kan kun oversætte til engelsk**, ikke til dansk. Oversættelse hører derfor til i skabelonen, som skriver referatet på dansk uanset mødets sprog.

**To begrænsninger, endnu ikke målt:**

- Detekteringen sker **én gang**, ud fra de første tredive sekunder. `testtekst-blandet.md` er skrevet for at måle, hvad det koster.
- Dansk, norsk og svensk ligner hinanden nok til at ramme forkert. Tærsklen for "usikker" står på 0,7 — valgt **uden data**. Skal sættes efter en rigtig måling.

---

## 6. Det, oplæsningen kan og ikke kan

*Noteret 12. august 2026.*

Oplæsning er en **måling**: den har en facitliste og kan give et tal. Rigtige møder er **tilpasning**: ordbogen lærer af rettelserne, men et møde har ingen facit og kan ikke give et tal. De to kan ikke erstatte hinanden.

**Oplæsning overvurderer kvaliteten.** Én taler, ingen der taler i munden på hinanden, tydelig udtale, ingen afbrydelser. Fejlraten fra en oplæsning er et bedste tilfælde — det rigtige møde bliver dårligere. Det skal stå i appen.

**Første rigtige oplæsning, 12. august:** 15:10 lyd, 45 afsnit, 5 blokmærker, tempo 265 sekunder hurtigere end de 120 ord i minuttet, teksten er sat efter. Blokmærkerne blev gemt korrekt.

---

## 7. Fase 0-gaten på rigtig lyd

*Målt 12. august 2026 på den første rigtige oplæsning, efter loop-rettelsen i 4.3.*

### Hastigheden — bestået

**RTF 0,29.** 260 sekunder på 910 sekunders lyd. Et 90-minutters møde ville tage cirka 26 minutter. Transskription er altså noget, man venter på over en kop kaffe, ikke et natjob. Det var gatens hovedspørgsmål, og svaret er positivt.

### Fuldstændigheden — bestået

2338 ord mod manuskriptets 2349, **99,5 %**. 165 linjer, alle unikke. Sproget detekteret som dansk med 97 % sikkerhed.

### Tallene — næsten, og "næsten" er problemet

| Fakta | Resultat |
|---|---|
| 412 brugere, 388 automatiske, 24 manuelle | rigtigt |
| 6 % fejlrate, 10 % i business casen | rigtigt |
| 16 timers vindue, 15 minutter, faktor 64 | rigtigt |
| **loft på 24 timer, maksimalt 3 forlængelser** | **rigtigt** |
| 120 timer til ERP, budget på 450 | rigtigt |
| **301 timer brugt** | **skrevet som 381** |
| **syv servicekonti** | **"syv" blev til "søv" — to gange** |

Nødadgangens loft — det tal Qwen3 tidligere hallucinerede som 48 — står korrekt i transskriptionen. Fejlen dér lå altså i sprogmodellen, ikke i Whisper.

Til gengæld er **381 mod 301** præcis den slags fejl, der er farlig: den ser ud som et tal, ingen opdager den ved gennemlæsning, og den går direkte videre i referatet.

"Syv" hørt som "søv" ødelagde to sætninger: *"alle undtagen syv objekter"* blev til *"alle underens søv objekter"*, og *"De syv er alle servicekonti"* blev til *"I søv er alle servicekonti"*. Faktum om de syv servicekonti overlevede altså ikke.

### Ordbogen — virker ikke som antaget

**Dette er det alvorligste fund.** Ordlisten sendes med som `initial_prompt` netop for at få fagord og navne rigtigt. Af otte kontrollerede termer, der alle **står i ordbogen**, blev seks ikke ramt:

| Term i ordbogen | I transskriptionen |
|---|---|
| Kernesys | "kerne, sys" |
| SCIM | "skim" |
| Entra ID | "N3ID" ét sted, "entra" et andet |
| MitID | slet ikke |
| deprovisionering | slet ikke |
| adgangsafstemning | slet ikke |
| attestering | ramt |
| funktionsroller | ramt |

**Konsekvens:** ordbogens virkning skal måles, ikke antages. Den koster os allerede noget — det var den, der udløste loopet i 4.3 — og indtil videre er der ikke belæg for, at den betaler for sig. Åbne spørgsmål, der skal afklares før ordbogen kan siges at virke:

1. Er 158 tokens for lidt til at flytte noget, eller for meget til at blive vægtet?
2. Virker `--prompt` overhovedet efter hensigten i denne whisper.cpp-udgave?
3. Ville en kortere liste med kun de ti vigtigste termer ramme bedre end 58?

Måles ved at køre den **samme** lyd med og uden ordliste og tælle ramte fagord. Det er billigt nu, hvor lyden findes.

### Samlet

Gaten er bestået på hastighed og fuldstændighed. Den er **ikke** bestået på pålidelighed: ét forkert tal og ét tabt faktum på 15 minutters ren oplæsning af én taler uden baggrundsstøj — altså det letteste tænkelige materiale. Et rigtigt møde bliver dårligere.

Det bekræfter kravet fra 1.1 fra en anden vinkel: **et udkast skal præsenteres som noget, der skal læses igennem mod lyden**, ikke som et resultat.

### 7.1 Referatet af den rigtige optagelse — og hvor fejlene kom fra

*Qwen3-8B, 172,8 sekunder, 640 tokens ud.*

Den automatiske bedømmelse gav 3 af 6 beslutninger og 3 af 7 opgaver. **Læst igennem er alle seks beslutninger der.** Bedømmelsen ledte efter ord, og ordene var allerede hørt forkert i transskriptionen:

| Sagt | Whisper skrev | Referatet skrev |
|---|---|---|
| nødadgang | (ikke genkendt) | "nødadmisse" |
| MitID Erhverv | (ikke genkendt) | "midt i det erhverv" |
| modtagersystemer | — | "modtagelsesystemer" |
| Malene | — | "Marlene" |
| NIS2 | — | "NIST 2" |

**Det afgørende:** næsten alle fejl i det færdige referat er **arvet fra transskriptionen**, ikke lavet af sprogmodellen.

Hvad sprogmodellen faktisk gjorde:

- Ingen talfejl. Loftet på 24 timer og de 3 forlængelser står korrekt — den 48-timers hallucination fra 1.1 kom ikke igen, fordi transskriptionen denne gang havde tallet rigtigt.
- **Den rettede "søv" til "syv"** af sig selv: *"De syv forældreløse servicekonti skal ryddes op"*, selvom transskriptionen sagde *"I søv er alle servicekonti"*. Sammenhængen bar den igennem.
- Den udelod de forkerte 381 timer helt frem for at bringe dem videre.
- Struktur, dansk og fristerne er i orden.

**Konsekvensen for hvad der skal laves nu:**

Det var forkert at bruge en dag på at lede efter en bedre sprogmodel. **Flaskehalsen er Whisper og ordbogen**, ikke sprogmodellen. Qwen3-8B laver et brugbart referat af en beskadiget tekst; en model ti gange så stor ville lave et pænere referat af den samme beskadigede tekst.

Rækkefølgen fremover er derfor: få ordbogen til at virke, mål den, og lad sprogmodellen være.

**Rettelse til vores egen målestok:** `bedoem-referat.ps1` straffer sprogmodellen for transskriptionens fejl. Tallene kan sammenlignes mellem **modeller på samme tekst**, men ikke bruges til at afgøre, om et referat er godt, når teksten under det er beskadiget. Begrænsningen står nu i scriptet.

---

## 8. Hvordan appen kan blive bedre — målt, ikke antaget

*Målt 12. august 2026. Dette afsnit besvarer spørgsmålet: hvad er mekanismen, der gør løsningen bedre og bedre?*

### 8.1 Ordlisten i Whispers initial_prompt gør ingen forskel

Samme lyd (15 minutter), samme indstillinger, kørt to gange — én gang med ordlisten på 58 termer, én gang uden:

| | Med ordliste | Uden ordliste |
|---|---|---|
| Ord | 2338 | 2338 |
| Linjer | 165 | 165 |
| **Forskelle** | **0** | |

Ikke «næsten ens». **Byte for byte identisk.**

### 8.2 Hvorfor: `-mc 0` og `--prompt` udelukker hinanden

Forklaringen er ikke, at ordlisten er dårlig. Den er, at den slet ikke bliver brugt.

`--prompt` leveres gennem den samme kanal som «tidligere tekst» — og `-mc 0` slår netop den kanal fra. Efterprøvet på et fire minutters udsnit: med `-mc 0` er output identisk med og uden ordliste. Uden `-mc 0` har ordlisten en effekt — men det er den, der udløser loopet i 4.3.

Mellemveje afprøvet på samme udsnit, alle med ordliste:

| Indstilling | Loop | Fagord ramt |
|---|---|---|
| standard (`-mc 16384`) | **ja** | — |
| `-mc 160` | nej | kun Entra |
| `-mc 64` | nej | kun Entra |
| `-mc 16` | nej | kun Entra |
| `-mc 0` | nej | kun Entra |

**Ordlisten henter ikke fagordene ved nogen indstilling.** Kernesys, SCIM og MitID blev ikke ramt i en eneste kørsel — heller ikke dem, hvor ordlisten beviseligt var aktiv. Entra rammes også *uden* ordliste.

**Konklusion:** ordlisten som mekanisme i Whisper er ikke vejen. Den koster (loopet) og leverer intet målbart. `-mc 0` beholdes, og prompten er dermed i praksis sat ud af kraft.

### 8.3 Vejen der virker: rettelser efter transskriptionen

Whisper kan ikke trænes. Vægtene er faste. Det eneste sted, der kan læres, er **efter** genkendelsen.

`TranscriptCorrector` anvender de rettelser, brugeren har lavet, på nye transskriptioner. Reglerne kommer fra mennesker, der har set både det hørte og det rigtige — der er ikke noget at gætte om.

Afprøvet på den rigtige transskription med syv lærte regler:

| Hørt | Rettet til | Antal |
|---|---|---|
| midt i det erhverv | MitID Erhverv | 2 |
| **søv** | **syv** | **2** |
| kerne sys | Kernesys | 1 |
| modtagelsesystemer | modtagersystemer | 1 |
| skim | SCIM | 1 |
| n3id | Entra ID | 1 |

**8 rettelser fra 6 regler.** Herunder «søv» → «syv», som var dét, der ødelagde faktummet om de syv servicekonti i 7.

Det er forskellen på de to mekanismer: ordlisten påvirker en model, vi ikke kan se ind i, og gjorde målt ingen forskel. Rettelserne gør præcis det, der står i dem, hver gang.

**Sikkerhedsnettet:** den rå tekst gemmes som `.raa.txt` ved siden af, og rettelserne vises i statuslinjen. Bliver teksten lavet om uden at det siges, ved man ikke, hvad man læser — og kan heller ikke opdage, at en regel er blevet forkert.

**Forfremmelse sker aldrig af sig selv.** En regel, appen selv fandt på, ville rette i noget, ingen har set efter. En forkert regel er værre end hørefejlen: hørefejlen ser man, rettelsen ligner det rigtige ord.

### 8.4 Det, der mangler for at sløjfen er hel

Tre ting findes nu: at lære en regel (`noteapp laer`), at anvende reglerne automatisk, og at prøve dem af på en fil (`noteapp laer --proev`).

Det, der mangler, er **opsamlingen**: der er endnu ingen skærm, hvor man retter et ord i en transskription og får det gemt som en regel. Indtil den findes, skal reglerne skrives ind i hånden, og så bliver de ikke skrevet ind. Det er det næste stykke arbejde, og det er dét, der afgør, om appen faktisk bliver bedre af at blive brugt.

---

## 9 · Hvad en times møde fylder — lyd mod tekst

*Målt 24-08-2026 på seks rigtige optagelser i `C:\AppNoter\Optagelser`.*

| Møde | Længde | Lyd (WAV) | Udskrift |
|---|---|---|---|
| Kundemøde, to spor | 61 min | 223 MB | 56,5 KB |
| Webinar, ét spor | 59 min | 98 MB | 50,5 KB |
| Møde, to spor | 27 min | 41 MB | 22,6 KB |

**Teksten fylder omkring en firetusindedel af lyden.** Det er den, der kan
søges i og gemmes i årevis uden at fylde noget.

**Konsekvensen — og den går imod et argument, der ellers lå lige for:** vores
lydfil fylder MERE end en videooptagelse i skyen. Zoom oplyser selv cirka
200 MB pr. time for video ([kilde](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0067670)),
og en times møde med to spor fylder 223 MB hos os. WAV er ukomprimeret med
vilje, fordi det giver den bedste udskrift.

«NoteApp fylder mindre end video» må derfor ikke siges om lydfilen. Det gælder
teksten, og det gælder, at lyden ligger på egen disk og kan slettes, når
teksten er i hus. Se [`salgsargumenter.md`](salgsargumenter.md), punkt 3.

### 9.1 Hvorfor lyd kan fylde det samme som video

*Målt 24-08-2026 direkte i WAV-hovederne på alle syv lydfiler.*

Alle optagelser er **16 000 Hz, 1 kanal, 16 bit, ukomprimeret PCM**:

    256 kbit/s  =  32 000 byte/s  =  109,9 MB pr. time pr. spor

Et onlinemøde har to spor — mikrofonen og det, computeren afspiller — så en
times møde er **220 MB**. Det passer med de 223 MB, der blev målt på filerne.

**Sammenligningen er ikke lyd mod video. Den er UKOMPRIMERET mod KOMPRIMERET.**

Rå video i 1280×720 ved 25 billeder i sekundet (4:2:0, 8 bit) fylder

    1280 × 720 × 1,5 byte × 25  =  34,6 MB/s  ≈  118 GB pr. time

Zoom oplyser cirka 200 MB pr. time. Deres H.264 skærer altså **omkring en
faktor 600** væk — mest fordi et møde er stillestående hoveder på en fast
baggrund, hvor næsten intet ændrer sig fra billede til billede.

Vores PCM skærer **ingenting** væk. Hver eneste sample ligger som den blev
målt. Det er ikke, fordi lyd er stort; det er, fordi vi ikke komprimerer.

**Hvad komprimering ville betyde:**

| | pr. time, ét spor | to spor |
|---|---|---|
| PCM 16 kHz 16 bit (i dag) | 110 MB | 220 MB |
| Opus 24 kbit/s | 10,8 MB | 21,6 MB |
| Opus 32 kbit/s | 14,4 MB | 28,8 MB |

Med Opus ved 24 kbit/s ville en times møde fylde **21,6 MB mod Zooms 200 MB** —
altså cirka en niendedel. Så holder «fylder mindre end video» også om lyden.

**Fravalg, der bliver stående:** komprimering EFTER udskriften er lavet er
ikke bygget. Whisper skal have 16 kHz PCM ind, så en komprimeret fil skal
pakkes ud igen før en ny udskrift — det er derfor, det skal ske bagefter og
ikke ved optagelsen.

**Det, der IKKE er målt:** om en udskrift af Opus-lyd er lige så god som af
PCM. Bitraten er valgt ud fra, hvad Opus regnes for at klare til tale — ikke
ud fra en måling på vores egne prøvetekster. Den måling skal laves, før noget
komprimeres for alvor; en udskrift, der bliver dårligere, koster mere end de
sparede gigabyte.
