# Whisper — hvad appen kører, og hvordan du undersøger det selv

Appen vælger både motor og model for dig. Denne fil er til den, der vil vide
hvorfor, eller vil efterprøve det.

*Skrevet 18-08-2026, da valget af Whisper-model blev fjernet fra skærmen.*

---

## Kort fortalt

| | Hvad | Hvor |
|---|---|---|
| Motor | whisper.cpp, CUDA-udgaven hvis maskinen har et NVIDIA-kort | `%LOCALAPPDATA%\NoteApp\motor\whisper` |
| Model | large-v3 (standard) eller large-v3-turbo | `%LOCALAPPDATA%\NoteApp\motor\modeller` |
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

Tilbage står `large-v3` som standard — den eneste, der faktisk er målt i dette
projekt (10,0 % ordfejlrate, 11 af 17 negationer bevaret) — og
`large-v3-turbo` til en maskine, der ikke kan holde den store. Turbo er
**ikke** målt her; det står også i kataloget.

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

## Røst v3 — prøvet af, og fravalgt

`CoRal-project/roest-v3-whisper-1.5b` er Whisper large-v3, finjusteret på
dansk tale. CoRal måler den klart bedre end originalen, og den blev derfor
undersøgt som standardmodel 18-08-2026.

**Den kan ikke bruges gennem whisper.cpp.** Modellen er finjusteret til at
køre uden tidsstempler; whisper.cpp beder altid om dem. Med tidsstempler
løber afkodningen i ring, uden dem bliver hvert vindue klippet over. Målt på
den samme oplæsning: 48,5 % ordfejlrate mod large-v3's 10,0 %, og 12 af 17
negationer tabt.

Hele målingen med tal og forklaring står i `maaling-whisper.md`. Skal den
bruges en dag, kræver det faster-whisper (CTranslate2) i stedet for
whisper.cpp — altså en anden motor, ikke en indstilling.

Bemærk også licensen, hvis det bliver aktuelt: Røst er under **OpenRAIL-M**,
ikke MIT eller Apache. Kommerciel brug er tilladt, men de brugsbaserede
begrænsninger i Attachment A skal videregives til dem, der får produktet.

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
