# Måling: Røst v3 mod Whisper large-v3

*Målt 18-08-2026 på Fase 0-oplæsningen: 15 minutter dansk tale, 2.365 ord med
facitliste. Samme maskine (RTX 2060), samme motor (whisper.cpp, CUDA), samme
lydfil.*

**Konklusionen først: Røst v3 kan ikke bruges gennem whisper.cpp.** Ikke fordi
modellen er dårlig — den er efter alt at dømme bedre end large-v3 — men fordi
vejen ind i whisper.cpp ikke bærer dens opsætning med.

---

## Hvorfor den blev undersøgt

`CoRal-project/roest-v3-whisper-1.5b` er OpenAI's Whisper large-v3,
finjusteret på dansk tale af Alexandra Instituttet / Alvenir. CoRal måler den
klart bedre end originalen:

| | Røst v3 | Whisper large-v3 |
|---|---|---|
| Samtale (CER) | 11,6 % | 27,5 % |
| Oplæsning (CER) | 4,5 % | 10,1 % |

Tallene er CoRal's egne, målt på CoRal's eget testsæt. Det er ikke
uafhængigt — modellen er trænet på træningsdelen af det samme datasæt. Det
gør dem ikke forkerte, men det gør dem til et argument for at måle selv.

---

## Resultatet

| | large-v3 | Røst v3 (fp16, `-nt`) |
|---|---|---|
| **Ordfejlrate (WER)** | **10,0 %** | **48,5 %** |
| Ord i udskriften | 2.375 | 1.713 |
| Manglende ord | 37 | **669** |
| Fagtermer ramt | 12 af 19 | 4 af 19 |
| **Negationer bevaret** | **11 af 17** | **5 af 17** |
| Tid (15 min lyd) | 4 min 20 s | 2 min 40 s |

Røst er hurtigere og taber en fjerdedel af teksten. Tolv af sytten
negationer er væk — det er den fejlklasse, der vender betydningen om, og det
er den, hele projektet er mest bange for.

---

## Hvad der gik galt, trin for trin

### 1. Med tidsstempler: den løber i ring

Første kørsel af den fulde optagelse blev afbrudt efter 45 minutter uden at
være færdig. large-v3 klarer den samme fil på 4 min 20 s.

Målt på et udsnit på 60 sekunder:

| Kørsel | Tid | Afkodningskald | Fallbacks |
|---|---|---|---|
| `small` (reference) | 4,9 s | 1.471 | 0 |
| Røst q8_0, standard | 116 s | 6.097 | 5 |
| Røst fp16, standard | 32 s | 5.611 | 5 |

Fire gange så mange tokens for den samme tale. Det kan ses direkte i teksten:

> …Provisioneringen må ændre id køre over skim og den må ændre id køre over
> skim og den må ændre id køre over skim…

Ni gentagelser i træk.

**Kvantiseringen var ikke årsagen.** q8_0 er fire gange langsommere pr. token
end fp16 (17,6 mod 4,27 ms), men begge løber i ring, og de producerer
praktisk talt den samme tekst. Kvantiseringen forklarer hastigheden, ikke
løkken.

**`-mc 0` hjalp ikke.** Det er den sædvanlige modgift mod gentagelsesløkker i
whisper.cpp — den slår tekstkonteksten mellem vinduer fra, og det var netop
den, der løste et tilsvarende problem 12-08-2026 (se `Transcriber.cs`). Her
gav den nøjagtig samme tekst og nøjagtig samme antal kald. Løkken sidder i
selve afkodningen.

### 2. Årsagen står i modellens egen opsætning

`generation_config.json` fra CoRal:

```json
"suppress_tokens": [],
"forced_decoder_ids": [[1, null], [2, 50360]]
```

Token 50360 er `<|notimestamps|>`. **Modellen er finjusteret til at køre uden
tidsstempler**, og dens suppress-liste er tømt — hvor stock Whisper har
omkring halvfems tokens, den aldrig må sige.

whisper.cpp beder altid om tidsstempler; den skal bruge dem til at dele
lyden op i segmenter. Man beder altså modellen om noget, den er trænet til
ikke at levere, og afkodningen bliver ustabil.

### 3. Uden tidsstempler: den holder op med at løbe i ring — og begynder at klippe

Med `-nt` forsvandt løkken helt:

| Røst fp16 | Tid | Afkodningskald | Fallbacks |
|---|---|---|---|
| standard | 32 s | 5.611 | 5 |
| **med `-nt`** | **10,4 s** | **1.211** | **0** |

Hurtigere end large-v3, og teksten gentog sig ikke længere. Men på den fulde
optagelse dukkede den næste fejl op: **hvert vindue bliver klippet over.**

> I den periode har vi kørt 412 broer igennem den nye onboarding proces af dem
> er 388 gået helt automatisk mens 24 krævede mål

Sætningen stopper midt i «manuel behandling». Uden tidsstempler kan
whisper.cpp ikke sætte grænserne mellem vinduerne, og resten af hvert vindue
falder på gulvet. Det er de 669 manglende ord.

---

## Hvad det betyder

Modellen og motoren passer ikke sammen. Røst forventer én afkodningskontrakt,
whisper.cpp kører en anden, og GGML-formatet bærer ikke forskellen med.

Der er to veje, hvis den skal bruges:

**1. En anden motor.** `pluttodk/roest-v3-whisper-1.5b-ct2` er konverteret til
CTranslate2 og virker med faster-whisper. Det ville koste en Python-runtime
ved siden af appen — i dag er hele appen én `.exe` plus en native
whisper.cpp-binær. Det er en arkitekturændring, ikke en indstilling.

**2. Vente på whisper.cpp.** Skulle den en dag kunne læse en models egen
`generation_config`, forsvinder problemet af sig selv.

Ingen af delene er noget, der skal laves nu. Whisper large-v3 er målt til
10 % ordfejlrate og bevarer 11 af 17 negationer, og bearbejdningen bagefter
sker hos Mistral, som forstår udskriften godt nok (se `maaling-sky.md`).

---

## Filerne, der blev hentet

Begge ligger stadig i modelmappen. De er **ikke** i appens katalog og bliver
ikke brugt — de kan slettes uden videre:

| Fil | Størrelse | SHA-256 verificeret |
|---|---|---|
| `roest-v3-q8_0.bin` | 1,66 GB | `aa2239e3…0dd7b501` ✓ |
| `roest-v3-fp16.bin` | 3,10 GB | `1e37548e…22acdbed` ✓ |

Begge hashes stemmer med dem, konverteringens udgiver har offentliggjort. Det
viser, at filerne ikke er ændret undervejs — det viser ikke, at
konverteringen er lavet rigtigt, og målingen ovenfor tyder på, at problemet
ligger et andet sted end i filerne.

---

## Kør målingen igen

```bash
whisper-cli.exe -m <model.bin> -f mikrofon.wav -l da -otxt -of ud
powershell -File scripts\maal-noejagtighed.ps1 -Transskription ud.txt -Reference fase0\oplaesning\testtekst.md
```

`-Reference` skal med. Standardværdien bruger `$PSScriptRoot` i `param`-blokken,
hvor den er tom, og så fejler scriptet.

---

## whisper.cpp v1.9.2 mod den tidligere motor

*Målt 19-08-2026 på Fase 0-oplæsningen. Samme maskine (RTX 2060), samme model
(large-v3), samme lydfil, samme flag.*

Motoren blev opdateret fra AI-modeller-skærmen. En ny motor kan ændre
udskriften til det bedre eller det værre, så den blev målt.

| | Tidligere motor | v1.9.2 |
|---|---|---|
| Ordfejlrate | 9,98 % | **9,98 %** |
| Ord i udskriften | 2.375 | 2.375 |
| Bytninger / manglende / ekstra | 152 / 37 / 47 | 152 / 37 / 47 |
| Fagtermer ramt | 12 af 19 | 12 af 19 |
| Negationer bevaret | 11 af 17 | 11 af 17 |
| **Tid (15 min lyd)** | **4 min 20 s** | **1 min 56 s** |

**Udskriften er identisk.** Ikke «næsten ens» — hvert eneste tal i
bedømmelsen er det samme. Der er ingen kvalitetsforskel at veje for eller
imod.

**Til gengæld er den 2,2 gange hurtigere.** 116 sekunder mod 260 på den samme
lyd. Det er den eneste forskel, og den er gratis.

### Fejlen undervejs, og hvad den viser om `-mc 0`

Første kørsel gav **87,7 % ordfejlrate** og så ud som et sammenbrud. Den var
kørt uden `-mc 0`, som appen altid sætter.

| Kørsel | Ord | WER | Fallbacks |
|---|---|---|---|
| uden `-mc 0` | 3.065 | 87,7 % | 4 p / 18 h |
| med `-mc 0` | 2.375 | 9,98 % | 0 / 0 |

Udskriften uden flaget læste stadig som god dansk — den gentog bare afsnit,
så der kom 700 ord for meget. Det er værd at kende: fejlen ligner ikke en
fejl, når man læser teksten. Kun tællingen afslører den.

Det er samme mekanisme, der blev fundet 12-08-2026 (se `Transcriber.cs`), og
den gælder altså stadig i v1.9.2. Flaget er ikke en historisk rest.
