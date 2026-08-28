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

Beskeden «al lyd bliver lokalt» kan altså ikke bruges som den er.

**Afgjort 28-08-2026. Det er ét slogan med to halvdele, og begge skal med:**

> ## Din stemme bliver i europæisk sky. Dine kunders lyd bliver hos dig.

| Halvdel | Hvad den siger |
|---|---|
| **Dine kunders lyd bliver hos dig** | Mødernes lyd forlader aldrig maskinen. Sendes der noget videre fra et møde, er det **udelukkende transkriptionen** |
| **Din stemme bliver i europæisk sky** | Dikterer du, sendes netop det klip — din egen stemme, dine egne sekunder, fordi du selv trykkede |

De to trækker i hver sin retning, og det er meningen. Den ene er et løfte om
tilbageholdenhed, den anden om formåen: bliver dikteringen lige så god som
Wispr Flow **og** kører på europæisk AI, er der kommet noget, de færreste har.

Til et møde sidder der mennesker, der ikke selv har valgt noget. Deres stemmer
er ikke vores at flytte. Det er dét, den første halvdel handler om, og den må
ikke blive udvandet af den anden.

### Hvad der skal rettes, den dag etape 1 tændes

Løftet står **35 steder** i dag — i koden, i hjælpen, i appens tekster, på
hjemmesiden og i privatlivspolitikken. De er sande nu, fordi dikteringen ikke
findes, og de er derfor IKKE rettet på forskud: en app, der lover noget, den
ikke gør, er den samme fejl med omvendt fortegn.

Hjemmesiden og politikken er allerede formuleret, så de holder: de siger
«lyden fra dine **møder**» frem for «lyden». Når dikteringen lander, skal der
**tilføjes** et afsnit — der skal ikke rettes en usandhed.

Find dem med:

```
grep -rn "forlader aldrig\|aldrig din maskine\|never leaves" src web doc
```

Privatlivspolitikken ligger til grund for Googles verifikation og skal passe
på dagen, den sendes ind.

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

**1 — diktering ind i appen. FÆRDIG 28-08-2026.** Ctrl+, holdt nede giver en
diktering; et kort tryk starter stadig et møde. Udskrift hos
`voxtral-mini-latest`, oprydning hos `mistral-small-latest`, målt til
1,3 sekund for tolv sekunders tale.

**2 — ud i andre programmer. FÆRDIG 28-08-2026.** Teksten lægges, hvor
markøren står, i det program du var i gang med, og formen følger programmet.

Tre ting blev afgjort undervejs, og de er ikke til at gætte bagfra:

*Der sættes ind, der skrives ikke.* Teksten kunne sendes tegn for tegn som
tastetryk og lade udklipsholderen være i fred. Men et tastetryk pr. tegn er
hundredvis af beskeder til et fremmed program, og de programmer, der taber et
af dem — terminaler, ældre felter, alt med sin egen tastaturhåndtering — taber
det midt i et ord. En sætning med et bogstav for lidt er værre end ingen
indsættelse, fordi man ikke opdager den. Ctrl+V er ét tastetryk: enten virker
det, eller også sker der ingenting.

*Forgrunden læses ved starten, ikke ved slutningen.* Mellem de to ligger
udskriften på over et sekund. Teksten skal lande dér, hvor du talte — ikke
dér, hvor du nåede hen imens. Er du skiftet væk, indsættes der ikke, og
teksten ligger i udklipsholderen.

*Teksten bliver liggende i udklipsholderen bagefter.* Den kunne sættes tilbage
til det, der lå der før. Men det, man lige har dikteret, er dét, man vil sætte
ind igen, hvis det første forsøg landede et forkert sted — og blev det gamle
sat tilbage, ville teksten være væk i samme øjeblik, man opdagede fejlen.

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
