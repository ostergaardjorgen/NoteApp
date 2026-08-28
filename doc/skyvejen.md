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
HeyPia på Mistral kan altså laves for engelske og tyske møder, men **ikke for
danske**. Det er kernemarkedet.

**Opsætningen er mindre, end den ser ud — og det, der kan fjernes, ligger et
andet sted end først antaget.**

Det, opsætningen faktisk henter:

| Maskine | Motor | Model | I alt |
|---|---|---|---|
| Med NVIDIA-kort | whisper.cpp CUDA, 1,10 GB | `large-v3`, 3,10 GB | **≈ 4,2 GB** |
| Uden NVIDIA-kort | whisper.cpp CPU, mindre | `large-v3-turbo`, 1,62 GB | **≈ 1,8 GB** |

**Alt sammen er tale til tekst.** Der er ikke noget at skære væk dér, så
længe udskriften laves lokalt.

Den lokale sprogmodel — llama-motoren på 1,68 GB og Qwen3-4B på 2,33 GB,
**4,01 GB tilsammen** — hentes ikke ved opsætningen. Den hentes først, hvis
man beder om en opsummering på maskinen.

> **To rettelser, begge mine.** Første optælling sagde 13 GB, hvoraf 11 GB var
> sprogmodeller. Det var målt på denne udviklingsmaskine, hvor der ligger
> rester fra tidligere målinger — Qwen3-8B og gemma-3-4b, som blev prøvet og
> fravalgt. Anden optælling sagde 8 GB og «halvér opsætningen». Også forkert:
> de 4 GB var aldrig en del af opsætningen.
>
> Tallene her er læst i `SetupWindow.ForberedHentning` og i katalogerne — ikke
> talt på en mappe.

**Det ændrer, hvad trin 1 er værd — men ikke om det skal gøres.** Trykker man
i dag på «Kort lokal opsummering» uden at have modellen, får man en blind vej
og et krav om 4 GB. Det er ikke en besparelse, der fjernes; det er en
forhindring midt i arbejdet.

**Og det gør skyvejen vigtigere, ikke mindre vigtig.** Opsætningens 4,2 GB er
transskriptionen, og de forsvinder kun, hvis den også flyttes.

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

4,2 GB hentet ned, før man har set et referat — 1,8 GB på en maskine uden
grafikkort. Det er ikke en opsætning; det er en beslutning, man skal tage
stilling til, og de fleste tager den ikke.

**Det hele er transskriptionen.** Der er ikke noget at skære væk, så længe
udskriften laves lokalt. Skal tallet ned, skal den flyttes.

---

## Hvad en skyvej ville ændre i oplevelsen

Det er ikke en indstilling. Det ændrer, hvad appen **er**, fra det øjeblik man
åbner den.

### Ved opsætningen

| I dag | Rent i skyen |
|---|---|
| 4,2 GB hentes | Ingenting hentes |
| Grafikkort tjekkes | Der spørges efter en API-nøgle |
| Klar efter 10-20 minutter | Klar efter to minutter |
| Virker uden konto | Kræver en konto hos leverandøren |

Den sidste linje er den, der gør ondt: **appen holder op med at virke uden en
konto et sted.** I dag kan man optage, skrive ud og søge uden at have en
nøgle overhovedet. Det er en del af, hvad produktet er.

Det gælder også trin 1 nedenfor: gøres opsummeringen til en skyting, holder
den op med at virke for den, der ikke har sat en nøgle op. Derfor skal den
lokale kunne hentes bagefter — ikke fjernes.

### Under et møde

Uændret. Optagelsen sker på maskinen uanset hvad — den kræver hverken
grafikkort eller net.

### Efter mødet

| I dag | Rent i skyen |
|---|---|
| Et kvarter for en time | Under et minut |
| Går altid igennem | Kræver net, og kan fejle |
| Koster ingenting | 1,20-2,34 kr. pr. times møde |
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

**Kunden bliver dataansvarlig for noget mere.** I dag er en HeyPia-bruger
dataansvarlig for tekst, der sendes. Med lyd i skyen bliver det
personoplysninger af en anden karakter, og for nogle kunder — kommuner,
sundhed, advokater — er det forskellen på ja og nej.

---

## Ordbogen.ai — brikken, der manglede

*Undersøgt 25-08-2026 på odincore.ai.*

**Der findes en dansk leverandør med en dansk Whisper og et OpenAI-kompatibelt
API.** Ordbogen A/S i Odense — 30 års leksikografisk arbejde bag sig — kører
platformen **OdinCore** på `api.ordbogen.ai`.

| Model | Pris | Hvad |
|---|---|---|
| `ordbogen/whisper` | **2,34 kr./time lyd** | Tale til tekst, med tidsstempler pr. ord |
| `odin-2` | 1,25 / 5,00 kr. pr. mio. tokens | Dansk-tunet sprogmodel |
| `odin-2-large` | 6,25 / 25,00 kr. pr. mio. tokens | Den store |
| `ordbogen/tts` | — | Dansk tale |

Endepunkterne er dem, alle kender: `v1/audio/transcriptions`,
`v1/chat/completions`. Klientkoden er den samme som til Mistral.

### Hvorfor det er vigtigere end prisen

De skriver: **«Dine data og dine kunders prompts forbliver i Danmark. Fuld
kontrol, ingen overførsel til udlandet og godkendte databehandleraftaler for
offentlige og statslige institutioner.»**

Det er en **stærkere** sætning, end den vi kan sige om Mistral i dag. Mistrals
egen underdatabehandlerliste indeholder amerikansk ejede selskaber, også når
serverne står i Europa — og derfor står der på Compliance-siden, at «ingen
amerikansk virksomhed er involveret» ikke holder.

Med en dansk leverandør, der kører på egen hardware i Danmark, kan den sætning
holde. **Skyvejen behøver altså ikke at være en svækkelse af budskabet. Den
kan være en styrkelse af det** — bare et andet sted end i dag.

De har desuden fået **Dataetikprisen 2026** af Dansk Industri og
Finansforbundet. Det er ikke et teknisk argument, men det er et, der kan stå i
et tilbud.

### Hvad der skal efterprøves, før noget bygges

1. **Nøjagtigheden på dansk.** Det er «Whisper», men hvilken udgave, og er den
   efterjusteret på dansk? Kør vores egen målefil og sammenlign med de 92,3 %,
   vi har målt lokalt. Det tager en time.
2. **Diarisering.** Den står ikke i modelbeskrivelsen, og et almindeligt
   Whisper-API kan det ikke. Talergenkendelsen fylder kun 0,06 GB og kan blive
   liggende lokalt — men så skal lyden stadig være på maskinen, når den kører.
   Det skal tænkes igennem.
3. **Databehandleraftalen, læst.** «Forbliver i Danmark» er en påstand på en
   hjemmeside, indtil den står i en aftale. Det er den samme prøve, Mistrals
   vilkår er blevet holdt op mod.
4. **Opbevaring og træning.** Gemmes lyden? Bruges den til at træne? Det står
   ikke på de sider, der er læst.

### Hvad det ville koste

Et møde på en time: **2,34 kr.** Til sammenligning koster referatet hos
Mistral omkring 21 øre i dag.

Skiftes både transskription og dokumenter til Ordbogen, er hele kæden dansk —
og prisen pr. møde lander omkring 2,50-3 kr. Det er stadig småpenge pr. møde,
men det er nu en løbende omkostning pr. bruger, som skal ind i prissætningen.

---

## Kan transskriptionen køre, mens mødet er i gang?

**Ja, og det er den bedste enkeltidé i hele den her omgang.**

Lyden skrives allerede i stumper af tredive sekunder undervejs — det er
maskineriet, der redder en optagelse, hvis strømmen går. De stumper kan
skrives ud, mens mødet kører.

**Regnestykket, hvis den holder sig fem minutter bagud:** ved mødets slutning
er der fem minutters lyd tilbage. På et grafikkort med RTF 0,29 er det halvdet
minut. Referatet er klart **omkring et minut efter, at man har trykket stop** —
mod et kvarter i dag.

### Det, der skal løses

- **Nøjagtigheden.** Whisper er kontekstfølsom, og en naiv opdeling i
  tredive-sekunders bidder klipper ord over og mister sammenhæng. Løsningen er
  større vinduer — to til fem minutter — med overlap, og at sy dem sammen på
  overlappet. Det skal måles mod den samlede kørsel, ikke antages.
- **Sproget skal vælges før mødet.** I dag spørges der, når man trykker skriv
  ud. Kører den undervejs, skal svaret være der på forhånd. Det er allerede
  halvt løst: aftalen kan bære et sprog, og der findes en indstilling for
  «det sprog, DU taler».
- **Optagelsen må aldrig blokeres.** Der findes en lås til tunge kørsler i
  forvejen. Transskriptionen skal kunne afbrydes, og optagelsen skal vinde.
- **De to spor.** Fletningen sker i dag, når begge er skrevet ud. Den skal
  kunne køre progressivt eller vente til sidst.

### Den ærlige begrænsning

**Det løser ikke problemet med lette bærbare.** Uden grafikkort er
transskriptionen langsommere end mødet — så bliver den aldrig færdig med at
indhente, og køen vokser, mens mødet står på.

Det er altså en **stor gevinst for dem, der allerede kan køre appen** — og
ingen hjælp til dem, der ikke kan. De to problemer skal løses hver for sig.

---

## Kan sprogvalget bestemme, om der køres lokalt eller i skyen?

**Ja — og det er den mest elegante måde at gøre det på, fordi brugeren ikke
får et nyt spørgsmål.** Sproget vælges i forvejen.

Med Mistral alene ville reglen skrive sig selv: dansk kan kun køres lokalt,
fordi Voxtral ikke kan dansk. Med Ordbogen i billedet er der frit valg — og så
er reglen ikke længere teknisk nødvendig, men et produktvalg.

### Det, der taler for

- Ingen ny beslutning at træffe. Sproget er allerede valgt.
- Reglen kan forklares på én linje: «Dansk køres her på maskinen. Engelsk
  køres i Europa.»
- Den passer til, hvor følsomheden faktisk ligger for de fleste danske kunder.

### Det, der taler imod — og som afgør det

**Det skjuler en beslutning om data bag et valg om sprog.** Vælger man
«engelsk» for at få et møde skrevet ud, har man netop sendt lyden ud af huset
uden at være blevet spurgt.

Det bryder med den regel, resten af appen er bygget på: at man ved, hvad der
sker, inden det sker.

**Hvis det bygges, skal det stå ved siden af sprogvalget** — ikke i en
indstilling, ikke i hjælpen. Én linje under valget: «Engelsk skrives ud hos
[leverandør] i [land]. Lyden sendes.» Og et hak, man selv kan flytte.

Så er sproget stadig det, der *foreslår* motoren — og brugeren det, der
*bestemmer*.

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

To udgaver af HeyPia: en **lokal** og en **let**. Samme app, forskellig
opsætning, forskellige argumenter, forskellig pris.

Det er den ærligste måde at sælge to ting på — men det er også to
produktbeskrivelser, to compliance-sider og to salgsargumenter, der skal
holdes adskilt. Sælges de som ét, ender begge med at love det stærkeste og
levere det svageste.

---

## Hvad der skal til i appen

Rækkefølgen er valgt efter, hvad der giver mest for mindst — og hvad der ikke
kræver, at budskabet ændres.

### Trin 1 — Opsummeringen skal virke ud af æsken *(gjort 25-08-2026)*

Den korte opsummering laves nu i skyen som standard. Den lokale er et
**tilvalg**, ikke en forudsætning.

- Ingen blind vej: opsummeringen virker fra første møde, uden at hente noget.
- **4,01 GB forsvinder fra rejsen** — ikke fra opsætningen, som aldrig
  indeholdt dem, men fra det, en bruger bliver bedt om undervejs.
- Nogle få sekunder i stedet for et halvt minut.
- Grafikkortet bruges ikke længere til opsummeringen. Whisper skal stadig
  køre, så kravet forsvinder ikke.
- **Compliance ændrer sig ikke.** Det er transkriptionens tekst, der sendes —
  præcis som når der laves et dokument, til den samme model på det samme
  endepunkt. Afsendelsen bogføres af SkyRunner med tidspunkt, model, tegn,
  pris og kontrolsum, som alle andre.

Skærmen siger nu, **hvor** opsummeringen bliver lavet, før man trykker — «I
EUROPA» eller «PÅ MASKINEN». En skærm, der handler om, hvor data går hen, må
ikke sige «på maskinen», når svaret laves i Europa.

Har man valgt den lokale uden at have hentet modellen, køres der i skyen, og
det står der. En indstilling, der tier, ser ud, som om den ikke virker.

### Trin 2 — Mål, om Whisper kan bære en let bærbar

Før der bygges en skyvej, skal det vides, hvad alternativet faktisk koster.
`large-v3-turbo` på processoren er aldrig målt i dette projekt, og
`medium`/`small` er målt til at være mærkbart dårligere på dansk —
negationerne forsvinder, og det er den fejl, der vender betydningen om.

Er turbo på en almindelig bærbar fx tre gange mødets længde, er svaret nej.
Er det halvanden, er der et produkt der.

Arbejde: lille. Det er en måling, ikke en funktion.

### Trin 3 — Mål Ordbogen.ai

Mistral kan ikke dansk. **Ordbogen.ai kan**, de er danske, og deres API er
OpenAI-kompatibelt — se afsnittet ovenfor.

Kør vores egen målefil igennem `ordbogen/whisper` og sammenlign med de 92,3 %,
vi har målt lokalt. Læs deres databehandleraftale. Find ud af, om de
diariserer.

De to andre kandidater er stadig værd at kende, hvis Ordbogen falder på
nøjagtigheden:

- **Gladia** (fransk, EU-dataresidens, diarisering, $0,61/time). Om dansk er
  med, er ikke bekræftet.
- **Whisper på egen EU-server** (Scaleway, OVHcloud). Beholder den
  nøjagtighed, vi kender — men så driver *vi* infrastruktur og bliver
  databehandler for vores kunder. En anden forretning end at sælge et program.

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

### Trin 4b — Kør udskriften, mens mødet er i gang

Se afsnittet ovenfor. Den er uafhængig af skyen og giver et referat omkring et
minut efter, at man har trykket stop — i stedet for et kvarter.

Arbejde: stort, og det meste af det er måling. Selve mekanikken er der
allerede; det er sammensyningen og nøjagtigheden, der skal på plads.

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
enkeltforhindring, der kan fjernes gratis — 4 GB og halvdelen af ventetiden — uden at røre ved
argumentet. Det er det billigste, du kan gøre for at komme ind på en let
bærbar.

**Mål Ordbogen.ai, før du beslutter noget om skyen.** De ændrer
forudsætningen: en dansk leverandør, der kører i Danmark, gør skyvejen til en
*styrkelse* af budskabet frem for en svækkelse. Det er den eneste af
kandidaterne, hvor «ingen overførsel til udlandet» kan komme til at holde —
og det kan det ikke i dag, heller ikke med Mistral.

**Byg model C, ikke model B, hvis det bliver til noget.** To udgaver med hvert
sit ærlige argument er nemmere at sælge og nemmere at stå på mål for end én
app, hvor det stærkeste argument kun gælder halvdelen af tiden.

**Lad sprogvalget foreslå motoren, men ikke bestemme den.** Reglen er god;
den må bare ikke skjule, at lyden rejser.

Argumentet «lyden forlader aldrig maskinen» er det eneste, ingen af de store
mødeværktøjer kan sige efter. Det skal ikke bruges op på at kunne sige det
halvt — men det kan udmærket stå ved siden af et andet, der også holder:
«og når den gør, bliver den i Danmark».

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
- [OdinCore.ai — modeller og priser](https://odincore.ai/docs/pricing) og
  [om os](https://odincore.ai/about) — `ordbogen/whisper` 2,34 kr./time,
  `odin-2` og `odin-2-large`, OpenAI-kompatibelt API paa `api.ordbogen.ai`,
  data i Danmark, Dataetikprisen 2026
- Appens egne målinger: `doc/whisper.md`, `doc/maaling-whisper.md`,
  `doc/findings.md`
