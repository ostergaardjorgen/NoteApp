# Diktering

Besluttet 28-08-2026. Appen skal kunne diktere: du taler, og der står brugbar
tekst — i en mail, i en prompt, i en opgave, i et notefelt. Forbilledet er
Wispr Flow, og motoren er Mistrals Voxtral.

## Det, der er efterprøvet

Alt herunder er målt mod `api.eu.mistral.ai` den 28-08-2026 med et klip på 12
sekunder. Det er ikke slået op, og det er ikke gættet.

| Spørgsmål | Svar |
|---|---|
| Findes lyd-til-tekst på EU-endepunktet? | Ja. `POST /v1/audio/transcriptions`, HTTP 200 |
| Formen på kaldet | multipart med `file` og `model` |
| Svaret indeholder | `text`, `segments`, `language`, `usage`, `finish_reason` |
| Genkendes dansk uden at sproget oplyses? | Ja |
| Lydmodeller på EU-endepunktet | ni |

**Modelnavnene i oplægget findes ikke.** Det gør disse:

| Formål | Model |
|---|---|
| Diktering, en optagelse ad gangen | `voxtral-mini-latest` |
| Streaming, mens der tales | `voxtral-mini-realtime-latest` |
| Tale ud af appen | `voxtral-mini-tts-latest` |
| Tekstpudsning | `mistral-small-latest` |

`voxtral-mini-transcribe-realtime-2602` afviser et almindeligt kald med
*«only supports realtime transcription»*. Streaming kræver WebSocket og er
en anden opgave end den første udgave — den skal ikke blandes ind i etape 1.

## To ting, der skal afklares først

### 1. Løftet om lyden bliver usandt

Det står i koden, i hjælpen, på hjemmesiden og i privatlivspolitikken, der
blev skrevet i dag:

> Lyden forlader aldrig din maskine.

**Diktering sender lyd til Mistral.** Det er hele pointen med Voxtral. Så
snart etape 1 er tændt, er sætningen ovenfor forkert alle de steder, den står.

Beskeden «al lyd bliver lokalt, al tekst behandles kun af europæisk AI» kan
altså ikke bruges som den er. Den, der kan, skelner mellem de to slags lyd:

> **Møder optages og skrives ud på din egen maskine.** Dikterer du, sendes
> netop det klip til europæisk AI — din egen stemme, dine egne sekunder.

Det er stadig et stærkt løfte, og det har den fordel at være sandt. Et møde
med fem mennesker, der ikke er blevet spurgt, er noget andet end en sætning,
du selv taler ind i en mail.

**Skal afgøres, før etape 1 tændes**, ikke bagefter: privatlivspolitikken
ligger til grund for Googles verifikation, og den skal passe på dagen, den
sendes ind.

### 2. «Sig HeyPia» kan ikke lyttes efter i skyen

Et vågeord kræver, at mikrofonen lytter hele tiden. Sendes den lyd løbende
til Mistral, koster det penge døgnet rundt og sender alt, hvad der siges i
rummet, ud af huset — også når der ikke bliver dikteret.

**Vågeordet skal genkendes lokalt.** Først når det er hørt, må der sendes
noget. Det er en anden slags motor end Voxtral, og den skal vælges for sig.

Derfor ligger vågeordet i etape 3 og ikke i etape 1. De to andre udløsere —
optageknappen og Ctrl+, holdt nede — har ikke problemet: der er et tryk, der
siger, hvornår der skal lyttes.

## Sådan hænger det sammen

Tre trin, og kun det midterste er nyt for appen:

1. **Optag** — lokalt, som appen allerede kan.
2. **Skriv ud** — klippet sendes til `voxtral-mini-latest`. Ud kommer rå tekst
   med «øh» og gentagelser i.
3. **Puds af** — den rå tekst sendes til `mistral-small-latest` med en
   instruktion, der afhænger af, hvad teksten skal bruges til.

Trin 3 er det, der skiller diktering fra transskription. Rå tale er ikke
brugbar tekst: den har fyldord, halve sætninger og ingen tegnsætning.

### Formen følger opgaven

Det samme talte indhold skal se forskelligt ud alt efter, hvor det lander:

| Sted | Hvad teksten skal blive til |
|---|---|
| Mail | Hel tekst med indledning og afslutning |
| Prompt | Instruktion, ingen høflighed |
| Opgave | Kort, i bydeform, én linje |
| Note | Tæt på det talte, kun ryddet op |

Appen ved, hvad du trykkede på, og skal bruge det. Det er dét, der gør Wispr
Flow bedre end en diktafon — ikke selve udskriften.

### Fagord skal med

Voxtral tager en liste af ord, den skal kende. Appen har allerede sådan en:
den indlærte ordliste, der bliver bedre af de rettelser, du selv laver. Den
skal sendes med, ikke bygges forfra.

## Etaper

**1 — diktering ind i appen.** Optageknap og Ctrl+, holdt nede. Ud kommer
pudset tekst i notefeltet. Ingen indsættelse i andre programmer endnu.

**2 — ud i andre programmer.** Teksten lægges, hvor markøren står, uanset
hvilket program der er fremme. Formen vælges efter programmet.

**3 — vågeordet.** Lokal genkendelse af «HeyPia», med lyden slukket som
standard og en tydelig visning af, at der lyttes.

**4 — opgaver og beskeder ud af tale.** «Mind mig om at ringe til Ibrar på
tirsdag» bliver til en opgave med en dato, ikke til en sætning.

**5 — AI-søgning.** Søgningen i Cockpittet forstår spørgsmålet frem for at
lede efter ordene. Kræver, at teksterne sendes til Mistral, og det er en
tredje ændring af, hvad der forlader maskinen — den skal skrives i teksterne,
inden den tændes.

Rækkefølgen er ikke tilfældig: hver etape kan bruges alene, og hver af dem
gør den næste lettere.
