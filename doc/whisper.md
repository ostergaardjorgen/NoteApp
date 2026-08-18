# Whisper — hvad appen kører, og hvordan du undersøger det selv

Appen vælger både motor og model for dig. Denne fil er til den, der vil vide
hvorfor, eller vil efterprøve det.

*Skrevet 18-08-2026, da valget af Whisper-model blev fjernet fra skærmen.*

---

## Kort fortalt

| | Hvad | Hvor |
|---|---|---|
| Motor | whisper.cpp, CUDA-udgaven hvis maskinen har et NVIDIA-kort | `%LOCALAPPDATA%\NoteApp\motor\whisper` |
| Model | Røst v3 (dansk) eller large-v3-turbo (øvrige sprog) | `%LOCALAPPDATA%\NoteApp\motor\modeller` |
| Version | I `manifest.json` ved siden af motoren | skrives af appen ved installation |

Er datamappen flyttet, ligger begge dele under den mappe i stedet.

---

## Hvorfor der ikke er noget at vælge

Skærmen «AI-modeller» viste før seks Whisper-modeller med fordele og ulemper
ved hver. Valget er fjernet, fordi hvert alternativ gjorde resultatet
ringere:

| Model | Hvorfor den ikke er med |
|---|---|
| `medium`, `small` | Mærkbart dårligere dansk. Negationer forsvinder, og det er den fejl, der vender betydningen om |
| `small.en`, `base.en` | Kan **ikke** dansk. Bruges de til et dansk møde, kommer der volapyk ud — ikke en fejlmeddelelse |
| `large-v3` | Afløst af Røst v3, som er den samme model finjusteret på dansk |

Tilbage står to, og det ene er ikke et kompromis mod det andet: Røst til
danske møder, `large-v3-turbo` når mødet holdes på et andet sprog.

---

## Kan appen se, om der er kommet en ny motor?

**Ja.** Det blev antaget, at den ikke kunne, og det passer ikke.

whisper.cpp udgives på GitHub, og opslaget efter den seneste udgivelse er
nogle få kilobyte JSON med et versionsnummer i (`tag_name`). Knappen
«Opdatér motoren» slår det op **først** og henter kun, hvis versionen er en
anden end den, der står i manifestet. Er den den samme, siges det, og der
hentes ingenting.

Det, der **ikke** kan lade sig gøre, er at læse versionen ud af de filer, der
allerede ligger på disken. whisper.cpp stempler hverken sin `.exe` eller sit
output med en version. Derfor skriver appen selv `manifest.json`, når den
installerer — og derfor kan versionen ikke aflæses, hvis motoren er lagt der
i hånden. Det står der så, frem for at blive gættet.

### Hvorfor filsammenligning ikke er svaret

Man kunne sammenligne filerne i stedet for versionsnumre. Det er teknisk
muligt og praktisk ubrugeligt:

- Udgivelserne er zip-arkiver, der pakkes ud. Tidsstempler og rækkefølge i
  arkivet ændrer sig, uden at indholdet gør.
- En byte-for-byte forskel siger, at *noget* er anderledes — ikke om det er
  en ny udgivelse, en anden byggevariant (CUDA mod BLAS) eller en fil, der er
  blevet rørt lokalt.
- Man skal hente hele arkivet for at kunne sammenligne. Så er hentningen
  allerede sket, og der var intet sparet.

Versionsnummeret fra udgivelsen svarer på det spørgsmål, man faktisk har, og
koster nogle få kilobyte.

---

## Undersøg Whisper selv

Alt herunder kan gøres uden at røre appen. Motoren og modellerne ligger som
almindelige filer.

### 1. Kør en model direkte

```bash
C:\NoteApp\tools\whisper\Release\whisper-cli.exe -m <model.bin> -f <lyd.wav> -l da -otxt -of <ud>
```

Lyden skal være 16 kHz mono WAV — det er præcis det format, appen selv gemmer
sporene i, så `mikrofon.wav` fra en mødemappe kan bruges direkte.

### 2. Mål resultatet mod et facit

Der ligger en oplæsningstekst på 2.349 ord med indbygget facitliste:

- Manuskript: `fase0\oplaesning\testtekst.md`
- Facitliste: `fase0\oplaesning\facitliste.md`

Læs den højt, optag den i appen, og mål:

```bash
powershell -File scripts\maal-noejagtighed.ps1 -Transskription <udskrift.txt> -Reference fase0\oplaesning\testtekst.md
```

Bemærk `-Reference`: parameterens standardværdi bruger `$PSScriptRoot` i
`param`-blokken, hvor den er tom. Udelades den, fejler scriptet.

Der kommer tre tal ud, og de skal læses hver for sig:

| Tal | Hvad det fanger |
|---|---|
| **WER** — ordfejlrate | Det brede mål. Godt til at sammenligne to modeller |
| **Fagtermer** | Ramte den de IAM-ord, en almindelig model aldrig har set? |
| **Negationer** | Overlevede hvert eneste «ikke»? **Den farligste fejlklasse** — en mistet negation vender betydningen om, og udskriften ser stadig rigtig ud |

En model kan have lavere WER og alligevel være dårligere, hvis den taber
negationer. Læs de tre tal sammen.

### 3. Sammenlign to modeller ordentligt

Samme lyd, samme facit, samme kommando — kun modelfilen skiftes. Kør begge
mindst to gange: whisper.cpp er ikke deterministisk på tværs af kørsler, og
en enkelt kørsel kan pege forkert.

---

## Røst v3 — hvad den er, og hvad den koster

`CoRal-project/roest-v3-whisper-1.5b` er OpenAI's Whisper large-v3,
finjusteret på CoRal-v3: dansk samtale og oplæsning på tværs af aldre, køn og
dialekter. Den er lavet af Alexandra Instituttet / Alvenir ApS.

Appen henter en GGML-konvertering (`q8_0`, 1,66 GB), fordi whisper.cpp ikke
kan læse safetensors. Den officielle udgivelse er kun safetensors.

**Filen er verificeret på SHA-256** mod den, konverteringens udgiver har
offentliggjort:

```
aa2239e350f6296b23b0cae91b1db1c4f21cdb8501d7b343a912ebea0dd7b501
```

Det viser, at filen ikke er ændret undervejs. Det viser **ikke**, at
konverteringen er lavet rigtigt — det gør kun en måling, og den står i
`doc/maaling-whisper.md`.

Vil man konvertere selv, ligger fremgangsmåden i whisper.cpp:

```bash
python models/convert-h5-to-ggml.py <hf-model-mappe> <whisper-repo> <ud-mappe>
```

### Licensen skal med videre

Røst er under **OpenRAIL-M**, ikke MIT eller Apache. Kommerciel brug er
tilladt, men licensen har brugsbaserede begrænsninger i Attachment A, og
**de skal videregives til dem, der får produktet.** Det er en anden slags
forpligtelse end de øvrige modeller i projektet, og den skal håndteres i
leverancen — ikke kun i koden.

---

## Det, der er målt til nul

Tre håndtag er prøvet af og fjernet igen. De står her, så de ikke bliver
genopfundet:

| Håndtag | Hvad der blev målt | Udfald |
|---|---|---|
| Ordliste til Whispers `initial_prompt` | 41 termer, ingen effekt — se `findings.md` afsnit 8 | Fjernet |
| Sprogmodel retter udskriften bagefter | 2 ord ud af 3.418 | Fravalgt |
| Ordbog til sprogmodellen | Ingen målbar forskel over fire kørsler | Fjernet |

`initial_prompt` er stadig et parameter i `TranscriptionRequest`
(`SendPromptTilWhisper`), men den står på `false`, og der er ikke noget at
sende længere.
