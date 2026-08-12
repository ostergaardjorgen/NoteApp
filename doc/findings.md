# Findings — hvad vi har målt, og hvad det betyder

Denne fil er hukommelsen på tværs af sessioner. Alt, der er blevet **målt**, står her med tal og dato, så beslutningerne kan efterprøves — og så den samme ting ikke bliver undersøgt to gange.

**Regler for filen:**

- Kun det, der er målt. Et skøn skrives som et skøn, med det ord.
- Hvert fund har en dato og en konsekvens. Et fund uden konsekvens er en anekdote.
- Fravalg bliver stående. Slettes de, bliver de gentaget om et halvt år af nøjagtig de samme gode grunde.
- Rettes et fund af en senere måling, bliver det gamle stående med en note. Historikken er en del af beslutningsgrundlaget.

**Maskinen, alle tal er målt på:** RTX 2060 med 6144 MiB VRAM, i7-1165G7 med 4 kerner, 31,7 GB RAM, Windows 11.

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

### 1.3 Muse-Glimmer-30B UD-Q3_K_XL — hentet, ikke målt endnu

13,4 GB. Samme størrelsesproblem som Mistral, og forventningen er derfor den samme. Måles for at have tallet frem for forventningen.

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

### 4.3 Bedømmelsen af referater fandt på fejl

Første udgave af `scripts/bedoem-referat.ps1` rapporterede to fejl, modellen ikke havde begået:

- Mønsteret `(\d+) timer` fandt "25 timer" inde i **"301,25 timer"**.
- Ejere blev talt i hele teksten, så deltagerlisten talte med, og alle kørsler fik 7 af 7.

**Konsekvens:** tal læses i deres sammenhæng, forankret til det ord de hører til, og afsnit holdes adskilt. En bedømmelse, der finder på fejl, er værre end ingen bedømmelse. Efter rettelsen: 5 af 7 — det samme som en manuel gennemlæsning gav.

### 4.4 En gate, der fejlede på sin egen brugsanvisning

`leverancetjek` faldt med *"Cannot bind argument to parameter 'Path'"*, når den blev kaldt præcis som dokumenteret. `$PSScriptRoot` var brugt som standardværdi i `param`-blokken.

**Konsekvens:** stien findes efter `param`-blokken. En gate, der fejler på sin egen brugsanvisning, bliver sprunget over frem for rettet — og så er den ikke længere en gate.

### 4.5 Downloads kunne ikke genoptages

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

**Første rigtige oplæsning, 12. august:** 15:10 lyd, 45 afsnit, 5 blokmærker, tempo 265 sekunder hurtigere end de 120 ord i minuttet, teksten er sat efter. Blokmærkerne blev gemt korrekt. Transskription og score mangler.
