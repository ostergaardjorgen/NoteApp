# Skyvejen — hvad den koster, hvad den giver, og hvad Mistral ikke kan

*Undersøgt 25-08-2026. Alle tal om appen er målt på denne installation;
alle tal om Mistral er slået op og har en kilde nederst.*

---

## Det korte svar

**Mistral kan ikke transskribere dansk.** Voxtral, deres tale-til-tekst-model,
understøtter 13 sprog — engelsk, kinesisk, hindi, spansk, arabisk, fransk,
portugisisk, russisk, tysk, japansk, koreansk, italiensk og hollandsk. Ingen
nordiske sprog.

Det er ikke til forhandling ved at prøve sig frem. En «rent skybaseret»
NoteApp på Mistral kan altså laves for engelske og tyske møder, men **ikke for
danske**. Det er kernemarkedet.

**Men to af dine tre problemer kan løses uden at røre lyden overhovedet.**
Ved en optælling af, hvad der faktisk hentes ned:

| Hvad | Fylder | Hvad det bruges til |
|---|---|---|
| Sprogmodeller (Qwen3, Gemma) | **9,33 GB** | Kun den korte opsummering |
| llama-motoren | **1,68 GB** | Kun at køre dem |
| Whisper-modellen | 1,10 GB | Tale til tekst |
| Talergenkendelsen | 0,06 GB | Hvem sagde hvad |
| Øvrige modeller | 0,45 GB | |
| **I alt** | **13 GB** | |

**11 af de 13 GB er den lokale sprogmodel.** Dokumenterne laves allerede i
skyen; det eneste, de 11 GB laver, er en ti-linjers opsummering, der også kan
laves hos Mistral for under en øre.

Fjernes det lokale sprogmodelvalg, falder installationen fra 13 GB til **1,6
GB**, og kravet om et grafikkort bliver mindre. Det er den billigste rettelse
på listen, og den koster ikke noget på compliance-siden — for den slags tekst
sendes allerede.

---

## De tre problemer, målt

### 1. Maskinkravene lukker døren for lette bærbare

Appen kræver i dag Windows 11, 16 GB RAM, **et NVIDIA-kort med mindst 4 GB**
og 15 GB fri plads.

Uden NVIDIA-kort kører transskriptionen på processoren. Den virker — og en
times møde tager længere, end mødet varede.

Det udelukker enhver MacBook, enhver bærbar med indbygget grafik, og hele
Surface-familien. Det er ikke et hjørne af markedet; det er størstedelen af
de bærbare, en selvstændig eller en studerende har.

### 2. Tiden

Med grafikkort: RTF 0,29 — en times møde tager omkring et kvarter. Det er
udmærket, når man ikke har travlt, og forkert, når man skal have et referat
med til det næste møde.

Uden grafikkort er det værre end mødets egen længde.

Til sammenligning tager Mistrals Voxtral **tre timers lyd i én forespørgsel**,
og prisen er $0,003 pr. minut. Et møde på en time koster 18 cent, cirka 1,20
kr.

### 3. Opsætningen

13 GB hentet ned, før man har set et referat. Det er ikke en opsætning; det er
en beslutning, man skal tage stilling til, og de fleste tager den ikke.

**Det er den, der er lettest at fjerne** — se tallet ovenfor.

---

## Hvad en skyvej ville ændre i oplevelsen

Det er ikke en indstilling. Det ændrer, hvad appen **er**, fra det øjeblik man
åbner den.

### Ved opsætningen

| I dag | Rent i skyen |
|---|---|
| 13 GB hentes | Ingenting hentes |
| Grafikkort tjekkes | Der spørges efter en API-nøgle |
| Klar efter 20-40 minutter | Klar efter to minutter |
| Virker uden konto | Kræver en konto hos Mistral |

Den sidste linje er den, der gør ondt: **appen holder op med at virke uden en
konto et sted.** I dag kan man optage, skrive ud og søge uden at have en
nøgle overhovedet. Det er en del af, hvad produktet er.

### Under et møde

Uændret. Optagelsen sker på maskinen uanset hvad — den kræver hverken
grafikkort eller net.

### Efter mødet

| I dag | Rent i skyen |
|---|---|
| Et kvarter for en time | Under et minut |
| Går altid igennem | Kræver net, og kan fejle |
| Koster ingenting | 1,20 kr. pr. times møde |
| Virker i et tog | Virker ikke i et tog |

Hastigheden er den store gevinst, og den er stor nok til at ændre, hvordan
man bruger appen: et referat, der er klart, mens man rydder op efter mødet,
bliver læst. Et, der er klart om et kvarter, bliver det tit ikke.

### Når nettet er væk

I dag: intet problem. I skyen: optagelsen ligger og venter. Det skal siges på
forhånd, ikke opdages i en lufthavn.

---

## Hvad det ville ændre i budskabet

Det her er den svære del, og den skal formuleres, **inden** der bygges noget.

### Det, der ikke længere kan siges

> «Optagelse og transskription forlader aldrig din pc.»

Den sætning står på Compliance-siden, i hjælpen, i salgsargumenterne og i
opsætningen. Den er det bærende argument i hele produktet.

Sendes lyden til en sprogmodel, holder den ikke. Og det er ikke en detalje,
der kan skrives udenom: **forskellen på en lydfil og en transkription er ikke
en gradsforskel.**

Lyden indeholder mere end ordene: hvem der talte kan høres, også når navnet
aldrig bliver sagt. Dertil tonefald, tøven, accent — og ting om helbred og
sindstilstand, som ingen har sagt højt.

Og en stemmeoptagelse kan blive til **biometriske data** efter
databeskyttelsesforordningens artikel 4, nr. 14, hvis den behandles med det
formål at genkende, hvem der taler. Tekst kan ikke laves om til et
stemmeaftryk.

Det argument står i appen i dag, det er rigtigt, og det vender sig mod os
selv, hvis lyden begynder at rejse.

### Det, der kan siges i stedet

Der findes et ærligt budskab, og det er ikke det samme:

> «Optagelsen bliver på din maskine, indtil du beder om en udskrift. Så sendes
> den til en europæisk leverandør under databehandleraftale, den behandles i
> Europa, og den bliver ikke gemt hos dem.»

Det er svagere. Det er stadig bedre end de fleste mødeværktøjer, som sender
lyden af sig selv og gemmer den. Men det er en anden sætning, og den skal
skrives i alle de dokumenter, der bygger på den gamle.

### To ting mere, der skal håndteres

**Samtykket bliver tungere.** Noten i mødeindkaldelsen siger i dag, at lyden
bliver på din computer. Den er nem at give ja til. Skal der stå, at lyden
sendes til en leverandør, er det et større ja at bede om — og det er præcis
det, teksten er sat i verden for at gøre ordentligt.

**Kunden bliver dataansvarlig for noget mere.** I dag er en NoteApp-bruger
dataansvarlig for tekst, der sendes. Med lyd i skyen bliver det
personoplysninger af en anden karakter, og for nogle kunder — kommuner,
sundhed, advokater — er det forskellen på ja og nej.

---

## Tre modeller for produktet

### A. Som i dag — lokal, med skyen kun til dokumenter

Argumentet er intakt. Markedet er dem, der har et grafikkort.

### B. Hybrid — brugeren vælger pr. optagelse

To knapper: **Skriv ud her** og **Skriv ud hurtigt**. Den ene bruger
maskinen, den anden skyen.

Fordelen er, at argumentet kan blive stående som **standarden**: lyden bliver
her, medmindre du beder om andet. Og at et fortroligt møde kan holdes lokalt,
mens et almindeligt kan gå hurtigt.

Ulempen er, at det er to veje at vedligeholde, to sæt målinger, to
fejlkilder — og at brugeren skal tage stilling til noget, de færreste har
forudsætninger for.

**Og maskinkravet forsvinder ikke.** En bærbar uden grafikkort har stadig kun
den ene knap i praksis, og så er «valget» en illusion.

### C. Rent i skyen — en anden udgave af produktet

To udgaver af NoteApp: en **lokal** og en **let**. Samme app, forskellig
opsætning, forskellige argumenter, forskellig pris.

Det er den ærligste måde at sælge to ting på — men det er også to
produktbeskrivelser, to compliance-sider og to salgsargumenter, der skal
holdes adskilt. Sælges de som ét, ender begge med at love det stærkeste og
levere det svageste.

---

## Hvad der skal til i appen

Rækkefølgen er valgt efter, hvad der giver mest for mindst — og hvad der ikke
kræver, at budskabet ændres.

### Trin 1 — Fjern de 11 GB *(gør det uanset hvad)*

Den korte opsummering laves i skyen som standard, med den lokale som et
fravalg for den, der vil have den.

- Installationen falder fra 13 GB til 1,6 GB.
- Opsætningen falder fra 20-40 minutter til nogle få.
- Grafikkortkravet bliver mindre, men forsvinder ikke.
- **Compliance ændrer sig ikke.** Den slags tekst sendes allerede i dag, når
  der laves et dokument.

Arbejde: middel. Sprogmodel-hentningen bliver valgfri, «AI-modeller» skrives
om, opsætningen får et andet forløb.

### Trin 2 — Mål, om Whisper kan bære en let bærbar

Før der bygges en skyvej, skal det vides, hvad alternativet faktisk koster.
`large-v3-turbo` på processoren er aldrig målt i dette projekt, og
`medium`/`small` er målt til at være mærkbart dårligere på dansk —
negationerne forsvinder, og det er den fejl, der vender betydningen om.

Er turbo på en almindelig bærbar fx tre gange mødets længde, er svaret nej.
Er det halvanden, er der et produkt der.

Arbejde: lille. Det er en måling, ikke en funktion.

### Trin 3 — Find ud af, hvem der kan dansk i skyen

Mistral kan ikke. Kandidaterne er:

- **Gladia** (fransk, EU-dataresidens, 100+ sprog, diarisering, $0,61/time).
  **Om dansk er med, er ikke bekræftet** — det skal måles på en rigtig
  lydfil, ikke læses på en markedsføringsside.
- **Whisper på egen EU-server** (Scaleway eller OVHcloud, begge franske).
  Beholder den danske nøjagtighed, vi kender. Men så driver *vi*
  infrastruktur, og vi bliver databehandler for vores kunder — en helt anden
  forretning end at sælge et program.

Arbejde: en dags måling for at få et svar, der kan bruges.

### Trin 4 — Byg vejen, hvis trin 3 giver et svar

Selve koden er den mindste del:

- Et `Skytransskription`-modul ved siden af `Transcriber`, med den samme
  udgang, så alt bagved er uændret.
- Lyden skal kunne sendes — i dag findes der ingen kode, der kan det, og det
  står som et argument på Compliance-siden.
- Kvitteringerne skal dække lyd: minutter, pris, kontrolsum.
- Diarisering fra leverandøren frem for lokalt.

Arbejde: stort, men lige ud ad landevejen.

### Trin 5 — Skriv budskabet om

Det, der skal rettes, hvis lyden begynder at rejse:

- `doc/salgsargumenter.md` — argument 1 falder
- `doc/produkt.md`
- Compliance-siden — hele afsnittet «Hvor data går hen»
- Hjælpen, afsnit 80 «Hvor dine data går hen»
- Noten i mødeindkaldelsen, på begge sprog
- Opsætningens tekst «Lyden bliver her. Teksten bliver i Europa.»

Det er ikke tekstarbejde. Det er en beslutning om, hvad produktet er, skrevet
ned tolv steder.

---

## Det, der skal måles, før nogen beslutter

1. **Kan Gladia dansk, og hvor godt?** Kør den samme lydfil, vi har målt
   Whisper på, og sammenlign ordfejlraten. Det tager en time.
2. **Hvad koster `large-v3-turbo` på en processor?** Uden svar på det er
   «lette bærbare kan ikke» en antagelse.
3. **Tager Mistrals EU-endepunkt lyd overhovedet?** Ikke bekræftet. Det er
   kun relevant, hvis produktet skal kunne engelsk og tysk uden dansk.
4. **Hvad siger de kunder, der siger nej i dag?** Er det maskinen, tiden
   eller opsætningen? De tre kræver hvert sit svar, og det ene af dem er
   gratis.

---

## Min anbefaling

**Gør trin 1 nu, uanset hvad du beslutter om skyen.** Det fjerner den største
enkeltforhindring — 11 GB og en halv times venten — uden at røre ved
argumentet. Det er det billigste, du kan gøre for at komme ind på en let
bærbar.

**Beslut ikke skyvejen, før trin 2 og 3 er målt.** Lige nu ved vi ikke, om
alternativet er nødvendigt (måske klarer turbo det), og vi ved ikke, om det er
muligt på dansk (Mistral kan ikke, og resten er ubekræftet).

**Og hvis det bliver til noget: byg model C, ikke model B.** To udgaver med
hvert sit ærlige argument er nemmere at sælge og nemmere at stå på mål for end
én app, hvor det stærkeste argument kun gælder halvdelen af tiden.

Argumentet «lyden forlader aldrig maskinen» er det eneste, ingen af de store
mødeværktøjer kan sige efter. Det skal ikke bruges op på at kunne sige det
halvt.

---

## Kilder

- [Voxtral transcribes at the speed of sound, Mistral AI](https://mistral.ai/news/voxtral-transcribe-2/)
  — 13 sprog, $0,003/minut, tre timer pr. forespørgsel, diarisering og
  tidsstempler, ~4 % ordfejl på FLEURS
- [Audio & Transcription, Mistral Docs](https://docs.mistral.ai/capabilities/audio/)
- [Audio Transcriptions Endpoints, Mistral Docs](https://docs.mistral.ai/api/endpoint/audio/transcriptions)
- [Gladia — Async Speech-to-Text](https://www.gladia.io/product/async-transcription)
  og [Gladia — Speech to Text](https://www.gladia.io/speech-to-text)
  — EU-dataresidens, diarisering, $0,61/time. Dansk **ikke** bekræftet
- [EU GDPR Cloud GPU 2026: Hetzner, Scaleway, OVHcloud](https://www.promptquorum.com/local-llms/eu-cloud-gpu-gdpr-2026)
  — priser på EU-GPU
- Appens egne målinger: `doc/whisper.md`, `doc/maaling-whisper.md`,
  `doc/findings.md`
