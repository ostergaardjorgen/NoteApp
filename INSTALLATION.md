# Installation af NoteApp

## Hvad appen gør

1. **Optager** et møde — mikrofon, og systemlyden med, hvis mødet er online.
2. **Skriver det ud** til tekst med Whisper, som kører på din egen maskine.
3. **Laver et dokument** ud af udskriften — et referat, en dokumentation eller
   hvad du har lavet en skabelon til.

Kun trin 3 bruger internettet til andet end at hente. Se **Hvor dine data er**
i [`README.md`](README.md), og **Compliance** inde i appen.

---

## Installationsfil

```powershell
powershell -File C:\NoteApp\scripts\byg-installer.ps1
```

Der kommer to filer i `C:\NoteApp\installer\`:

- **NoteApp-setup.exe** (ca. 53 MB) — den, der skal sendes eller
  dobbeltklikkes. Beder selv om administratorrettigheder undervejs.
- **NoteApp.msi** (ca. 52 MB) — nyttelasten, til automatisk udrulning.

Efter installation ligger programmet i `C:\Program Files\NoteApp` (ca. 160 MB),
med genveje i Startmenuen og på skrivebordet, og en post under **Tilføj/fjern
programmer**.

**Byggeværktøj:** WiX 5, installeret som .NET-værktøj med
`dotnet tool install --global wix --version 5.0.2`. Versionen er bevidst
bundet: WiX 6 og 7 kræver, at man accepterer en betalt licensaftale (Open
Source Maintenance Fee), og det er en beslutning, der skal træffes bevidst
frem for af et byggescript.

### Udvikling og installation lever side om side

| | Sti | Bruges til |
|---|---|---|
| Udvikling | `C:\NoteApp\app\NoteApp.exe` | Køres direkte, mens der bygges og prøves af |
| Installation | `C:\Program Files\NoteApp\NoteApp.exe` | Den rigtige installation |

Begge læser den **samme datamappe**. Det er med vilje: optagelser, dokumenter
og indstillinger skal være de samme, uanset hvilken kopi der startes.

Har du begge, har du **to genveje med samme navn**, der åbner hver sin udgave
af koden. Omdøb udviklingsgenvejen, så du kan se forskel uden at åbne
egenskaber.

### Hvad der sker ved en ny installation oven på en gammel

| Situation | Hvad installationsfilen gør |
|---|---|
| Ældre version installeret | Opdaterer. Den gamle fjernes automatisk, så der kun bliver én post under Tilføj/fjern |
| **Samme** version installeret | Siger, at den allerede er installeret, og tilbyder at reparere eller afinstallere |
| Ingen installation | Installerer |

**Versionen skal derfor være hævet, før du bygger en opdatering.** Gør du ikke
det, tilbyder installationsprogrammet at reparere frem for at opdatere, og man
tror, at ændringen ikke virkede.

Versionen sættes af `scripts\udgiv.ps1` ud fra den seneste commit-besked
(`vX.Y.Z: ...`), så nummeret på skærmen ikke kan komme ud af trit med
historikken. Skemaet er tre led, og det er tredje led, udgivelserne tæller på:
1.0.1, 1.0.2, 1.0.3. Scriptet spørger, hvis major eller minor springer.

**Afinstallation rører ikke dine data.** Pakken kender kun til Program Files.
Optagelser, udskrifter, dokumenter, skabeloner og indstillinger bliver liggende
i datamappen.

---

## Første start

Appen guider dig gennem **to trin**: hvor dine filer skal ligge, og hentning af
Whisper.

Whisper følger ikke med i installationsfilen — standardmodellen fylder 2,9 GB
og skal vælges efter maskinen:

| | Størrelse | Hvornår |
|---|---|---|
| `large-v3` | 2,9 GB | Standardvalget. Den eneste, der er målt på dansk i dette projekt |
| `large-v3-turbo` | 1,6 GB | Halv størrelse, omtrent dobbelt hastighed. Til en maskine, der ikke kan holde large-v3 |

Motoren vælges efter maskinen: er der et NVIDIA-kort, hentes CUDA-udgaven.
På ren CPU er `large-v3` for langsom til daglig brug — vælg da den mindre.

Størrelsen vises, **før** der hentes noget. Findes motor eller model allerede,
springes den over.

**Vil du også lave dokumenter,** sætter du din Mistral-API-nøgle ind under
**AI-modeller**. Optagelse og udskrift virker uden — mærkatet ved menupunktet
står, indtil nøglen er sat, fordi det ellers først opdages den dag, du vil have
et referat.

---

## Skærmene

| Skærm | Hvad den er til |
|---|---|
| **Optagelser** | Optagelserne i et træ. Vælg én, og skriv den ud til tekst |
| **Dokumenter** | De færdige tekster, gemt som Word-dokumenter (.docx) |
| **Skabeloner** | Hvad der skal laves ud af en optagelse |
| **AI-modeller** | Whisper-motor og -model, og API-nøglen til sprogmodellen |
| **Compliance** | Hvor data går hen, leverandøren, underdatabehandlere, forbehold |
| **Historik** | Hvad appen har lavet |
| **Indstillinger** | Opstart, Lyd, Filer, Backup |

Optagebjælken øverst er den samme på alle skærme. Genvejstasten står med fed
øverst til højre — som standard **Ctrl+Shift+0**, men appen tager den næste
ledige, hvis den er optaget af et andet program, og viser så den, der faktisk
blev registreret.

### Skabeloner

Fire faner pr. skabelon:

- **Indstillinger** — navn, temperatur, maksimal længde, beskrivelse
- **Instruktion** — reglerne, modellen arbejder efter. Her rettes en fejl i et
  referat som regel
- **Hvad modellen får** — materialet: hvilke oplysninger der overhovedet når
  frem. Mangler `{{transskription}}`, kan skabelonen ikke gemmes
- **Agenda** — en dagsorden, du kan kopiere ind i mødeindkaldelsen

Deltagerreglerne er **fælles** for alle skabeloner og sættes ind med feltet
`{{deltagerregler}}` i instruktionen. Rettes de ét sted, følger alle skabeloner
med.

Dagsordenen hører til skabelonen, fordi et referat, en dokumentation og en
brainstorm har brug for vidt forskellige ting sagt højt undervejs. Har en
skabelon ikke sin egen, gælder standarden. Den kan tilpasses med en knap eller
rettes i hånden — den ligger som `navn.agenda.md` ved siden af skabelonen.

**Ny skabelon** spørger om fem ting, du kender, og lader Mistral skrive både
skabelonen og en dagsorden til den. Resultatet er et udkast, der åbnes til
redigering — en skabelon, man ikke selv har set igennem, er en, man ikke
opdager fejl i.

---

## Datamappen

Alt ligger i `C:\AppNoter` — eller den mappe, du vælger under **Indstillinger →
Filer**. Miljøvariablen `NOTEAPP_DATA` vinder over begge dele.

Mappen ligger uden for kode-repoet med vilje, så et møde ikke kan komme med i
en git-push.

### Backup

Tages under **Indstillinger → Backup**. Vælg mappe, se de gemte arkiver, og tag
en kopi med det samme.

**Motor- og modelfiler er aldrig med.** De fylder næsten 7 GB og kan hentes
igen med to klik; tages de med, bliver arkivet så stort, at man holder op med
at tage backup. Uden dem er en kopi under 1 MB og tager under et sekund.

**Lydfiler er som standard heller ikke med.** En times optagelse fylder over
100 MB, mens noter, udskrifter, dokumenter og indstillinger tilsammen er få MB.
Vil du have lyden med, er der et afkrydsningsfelt, og appen viser forskellen i
størrelse, før du vælger.

Arkivet bygges til en `.part`-fil og omdøbes først, når det er færdigt. Et
afbrudt byg efterlader derfor aldrig et arkiv, der ser gyldigt ud.

---

## Efter en kodeændring

```powershell
powershell -File C:\NoteApp\scripts\udgiv.ps1
```

Scriptet sætter versionen ud fra den seneste commit, udgiver til
`C:\NoteApp\app` — dér hvor udviklingsgenvejen peger — og efterprøver, at
filen kom, at versionen passer, og at genvejen stadig rammer den.

Bygger du kun til `bin\Release`, bliver genvejen ved med at åbne en gammel
udgave, uden at noget siger det. Det er sket.

---

## Installation forfra på en anden pc

**1. Forudsætninger**

```powershell
winget install --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
```

En NVIDIA-GPU er ikke et krav, men den er forskellen på at vente og at lade den
køre om natten.

**2. Hent koden**

```powershell
git clone https://github.com/ostergaardjorgen/NoteApp.git C:\NoteApp
```

Repoet er privat. Bemærk at **kun koden ligger der** — dine data er aldrig i
git og skal hentes fra din egen backup, se [`doc/mine-data.md`](doc/mine-data.md).

**3. Byg og udgiv**

```powershell
powershell -File C:\NoteApp\scripts\udgiv.ps1
```

**4. Hent motor og model i appen**

Start appen og følg opsætningen. Motor og modeller ligger bevidst uden for
versionsstyring — flere GB binærer hører ikke i et git-repo.

---

## Vigtigt om placering

**Læg ikke modellerne på et netværksdrev.** Målt på denne maskine tog
indlæsning af `ggml-large-v3.bin` **248 sekunder over SMB mod 4,9 sekunder fra
lokal SSD** — og den omkostning betales, hver eneste gang en udskrift starter.

## Juridisk note

Optager du et onlinemøde, fanges de andre deltageres lyd, **uden at
mødeprogrammet signalerer det**. Til dine egne møder på egen maskine er det
uproblematisk, men oplys deltagerne — og skal outputtet nogensinde ind i en
kundeleverance, er det et krav, ikke en høflighed.

Sender du en udskrift til sprogmodellen, sender du også dét, de andre
deltagere sagde. De har ikke sagt ja til det. Hvad det indebærer, står under
**Compliance** i appen.
