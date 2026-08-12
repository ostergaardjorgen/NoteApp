# Installation af NoteApp

## Installationspakke (.msi)

NoteApp kan installeres som et almindeligt Windows-program. Pakken bygges med:

```bash
powershell -File C:\NoteApp\scripts\byg-installer.ps1
```

Scriptet udgiver appen først og pakker derefter præcis de filer ned. Resultatet er `C:\NoteApp\installer\NoteApp.msi` på cirka 107 MB.

Installation kræver administratorrettigheder, fordi programmet lander i Program Files:

```bash
msiexec /i C:\NoteApp\installer\NoteApp.msi
```

Efter installation ligger programmet i `C:\Program Files\NoteApp`, med genveje i Startmenuen og på skrivebordet, og en post under **Tilføj/fjern programmer**.

**Afinstallation rører ikke dine data.** Pakken kender kun til Program Files. Optagelser, noter, ordbog og indstillinger ligger i datamappen — som standard `C:\AppNoter` — og bliver liggende. Vil du af med dem, skal du selv slette mappen. Det står også i beskrivelsen under Tilføj/fjern programmer.

**Byggeværktøj:** WiX 5, installeret som .NET-værktøj med `dotnet tool install --global wix --version 5.0.2`. Versionen er bevidst bundet: WiX 6 og 7 kræver, at man accepterer en betalt licensaftale (Open Source Maintenance Fee), og det er en beslutning, der skal træffes bevidst frem for af et byggescript.

**Det pakken ikke indeholder:** Whisper-motoren. På en frisk maskine uden `C:\NoteApp\tools\whisper` vil skærmen *Motor og model* melde, at motoren mangler. Modeller kan appen hente selv; motoren skal endnu lægges på plads i hånden.

---


## Hvad der allerede er installeret på denne maskine

Alt er sat op og testet 8. august 2026. Du behøver ikke gøre noget for at komme i gang — spring til "Sådan bruger du den".

| Komponent | Placering | Version |
|---|---|---|
| .NET SDK | `C:\Program Files\dotnet` | 8.0.423 |
| whisper.cpp (CUDA 12.4) | `C:\NoteApp\tools\whisper\Release\` | v1.9.2 |
| Whisper-modeller | `C:\NoteApp\models\` | `ggml-large-v3.bin` (2,9 GB), `ggml-medium.bin` (1,4 GB) |
| **NoteApp (appen med UI)** | `C:\NoteApp\app\NoteApp.exe` | 147 MB, selvstændig — kræver ingen runtime |
| Konsol-optageren | `C:\NoteApp\app\Fase0Recorder.exe` | 147 MB, selvstændig |
| Kildekode | `C:\NoteApp\src\` | |
| Kodebackup | https://github.com/ostergaardjorgen/NoteApp (privat) | git remote `origin` |

**Genveje på skrivebordet:**

- **NoteApp** — appen med UI. Den skal du bruge til Fase 0-oplæsningen.
- **NoteApp - Optag onlinemøde** — konsol-optager, mikrofon + systemlyd fra Teams
- **NoteApp - Optag fysisk møde** — konsol-optager, kun mikrofon
- **NoteApp - Transskribér seneste** — kører nyeste optagelse gennem Whisper

---

## NoteApp — appen med UI

Dobbeltklik på **NoteApp**. Den har seks skærme: Oplæsning, Transskribér, Ordbog, Motor og model, Filer og backup, og Indstillinger.

### Oplæsning

Teleprompteren til testoplæsningen. Den viser **ét afsnit ad gangen** i stor hvid skrift — og kun det. Der står intet andet på skærmen, netop for at man ikke kommer til at læse hjælpeteksten med.

- **Mellemrum** (eller pil ned/højre) går til næste afsnit. **Pil op/venstre** går tilbage.
- **Skift afsnit automatisk** kan slås til: appen lytter med og henter selv det næste afsnit, når den har hørt slutningen af det aktuelle. Mellemrum virker uændret ved siden af.
- Uret viser både din faktiske tid og **hvor du burde være**, plus om du er foran eller bagud.
- **Niveaumåleren bliver rød ved stilhed.** Det er den fejl, man ellers opdager bagefter.
- **Pause** stopper optagelsen midlertidigt. Der optages intet imens, og uret står stille — så noternes tidsstempler bliver ved med at passe til lyden.
- Ved hvert blokskift skrives et mærke i `notes.jsonl`, så transskriptionen kan holdes op mod facitlisten blok for blok.

Optagelsen er altid et **fysisk møde** — ét spor, kun mikrofonen. Der er ingen anden part at optage.

Teksten hentes fra `C:\NoteApp\fase0\oplaesning\testtekst.md`, hvis repoet er der; ellers bruges den kopi, der er indlejret i exe'en. Navnene i teksten er opdigtede, og det er med vilje: det er din stemme og dine fagord, testen måler — ikke om lige de navne staves rigtigt.

### Ordbog

Ordene, der sendes med til Whisper, så den ved, hvad den skal lytte efter.

Kategorien er ikke en etiket til at sortere efter — **den afgør, hvem der kommer med**, når ordbogen er større end de ca. 224 tokens, Whisper har plads til:

| Kategori | Bruges til | Prioritet |
|---|---|---|
| Person | Kollegaer, kunder, mødedeltagere | Vælges først |
| Organisation | Firmaer, myndigheder, afdelinger | Vælges næst |
| Produkt | Produkt- og systemnavne, fx Entra ID | Efter vægt |
| Fagterm | Fagudtryk, fx provisionering, attestering | Efter vægt |
| Forkortelse | SCIM, IAM, JML | Efter vægt |

Der er ikke flere kategorier med vilje. En kategori, der ikke ændrer udvælgelsen, ville kun være mere arbejde ved indtastning.

Panelet nederst til højre viser **den prompt, Whisper faktisk får**, og hvor mange af de 224 tokens den fylder. Bliver tallet gult, er der ord, der ikke kom med — så er det vægten, der afgør, hvem der ryger ud. Knappen **Gem ordlisten til Whisper** skriver den til `C:\AppNoter\ordliste.txt` — den fil, transskriptionen læser.

### Transskribér

Kører en optagelse gennem Whisper på din egen maskine. Vælg optagelsen til venstre, tryk **Transskribér**, og følg fremdriften.

Bagefter vises fire tal: lydens længde, tiden det tog, **realtidsfaktoren** og antal ord. Realtidsfaktoren er den vigtigste — er den over 1,0, tager transskriptionen længere tid end mødet varede, og så er det et natjob frem for noget, man venter på. Appen regner om til, hvad et 90-minutters møde ville koste.

Optagelser kan også **slettes** herfra. Dialogen viser, hvad der forsvinder, og hvor meget det fylder. Der er ingen papirkurv.

### Motor og model

Viser hvad der faktisk kører: at det er whisper.cpp, hvor gammel motoren er, om beregningen sker på GPU eller CPU, og hvilken model.

Seks modeller kan hentes, hver med fordele og ulemper skrevet ud. Vær særligt opmærksom på mærkatet: modeller mærket **KUN ENGELSK** kan ikke dansk, og bruges de til et dansk møde, kommer der volapyk ud — ikke en fejlmeddelelse.

**Alder frem for version:** whisper.cpp stempler hverken sin exe-fil eller sit output med et versionsnummer. Appen kan derfor kun kende versionen af en motor, den selv har hentet — og der står **Version** i så fald. Er motoren lagt på maskinen i hånden, står der i stedet **Sidst opdateret** med datoen på filen, som faktisk kan aflæses. Der står aldrig "ukendt": et felt, der ikke kan svare, skal stille et andet spørgsmål frem for at vise sin egen uvidenhed.

Knappen **Søg efter opdatering** slår op hos GitHub. Det sker kun, når du trykker; appen kontakter aldrig nettet af sig selv.

### Filer og backup

Hvor dine filer ligger, og hvor sikkerhedskopien lander. Begge mapper kan skiftes, og skifter du datamappen, flyttes indholdet med.

**Lydfiler er som standard ikke med i backup.** En time optagelse fylder over 100 MB, mens ordbog, noter, transskriptioner og indstillinger tilsammen er få MB — og lyden er også det, der er lettest at undvære, for arbejdet ligger i teksten og i de indlærte rettelser. Der er et afkrydsningsfelt, hvis du vil have den med, og appen viser forskellen i størrelse først.

Herfra kan du også **gendanne**. Kør **Prøvekørsel** først: den pakker arkivet ud i en midlertidig mappe og kontrollerer, at ordbogen er en gyldig databasefil, uden at røre dine data. **Gendan** skriver arkivet ind og tager altid et fortrydelsesarkiv af det nuværende først.

### Indstillinger

Vælg mikrofon og højttaler blandt Windows' enheder. Måleren ved siden af hver viser, om der faktisk kommer lyd — sig noget, og se om den rører sig. Det er den eneste kontrol, der reelt afslører en forkert valgt enhed, for navnet siger intet om, hvor lyden går hen.

Er den valgte enhed taget ud siden sidst, falder appen tilbage på Windows' standard og siger det, **inden** optagelsen starter.

---

## Konsol-optageren

### 1. Start optagelsen når mødet begynder

Dobbeltklik på den rigtige genvej. Der er ingen default-mødetype med vilje: vælger du forkert på et onlinemøde, står du bagefter med et tomt loopback-spor, og det opdager du først når mødet er slut.

Appen spørger om en mødetitel. ENTER springer den over, men gør det ikke — efter tyve møder er `2026-08-08_14-30` ubrugeligt som mappenavn.

**Ved onlinemøde måler den loopback-niveauet i 3 sekunder først.** Får du `STILHED`, så stop og ret det, inden du fortsætter:
- Teams er ikke startet endnu, eller der er ingen lyd i mødet endnu
- eller lyden går til et headset, der ikke er Windows' standard-afspilningsenhed

Det er den fejl der koster et helt møde. Appen lader dig fortsætte alligevel, men spørger først.

Under optagelsen ser du niveaumålere pr. spor. **ENTER stopper.**

### 2. Transskribér bagefter

Dobbeltklik på **NoteApp - Transskribér seneste**. Den tager den nyeste optagelse og kører alle kombinationer: `medium` og `large-v3`, hver med og uden IAM-ordlisten.

Vinduet lukker ikke af sig selv, så du kan læse måletabellen.

### 3. Hvor tingene lander

```
C:\NoteApp\fase0\
  optagelser\<dato>_<titel>\
      mikrofon.wav        16 kHz mono — dig
      loopback.wav        16 kHz mono — de andre (kun onlinemøde)
      meeting.json        mødetype, varighed, enhedsnavne, tidsstempel
  transskriptioner\       .txt + .json + .log pr. kørsel
  resultater\             rtf_<tidspunkt>.csv med måletabellen
```

Et 90-minutters møde fylder ca. 100 MB pr. spor.

---

## Hvad der IKKE er der endnu

NoteApp har et rigtigt vindue med seks skærme (se ovenfor), men det er stadig ikke det færdige produkt. Følgende kommer i senere faser og findes ikke nu:

| Feature | Fase |
|---|---|
| Optagelse af et almindeligt møde fra UI'et — appen optager i dag kun oplæsningen; til møder bruges konsol-optageren | 1 |
| Live-noter under mødet | 1 |
| Global hotkey til "markér nu" | 1 |
| Talergenkendelse og navngivning | 2 |
| Afspilning synkroniseret med transskript | 4 |

**Chunked autosave er allerede på plads** — begge optagere skriver lyden som 30-sekunders segmenter undervejs og samler dem først ved stop. Et crash eller et fladt batteri koster højst det sidste segment, ikke hele mødet.

Auto-resumé i appen stod tidligere på listen som Fase 3. **Den er fjernet 10. august 2026:** den ville sende transskriptionen ud af maskinen, og ingen data må forlade den pc, appen kører på. Referatet laver du som hidtil ved at kopiere eksporten ind i Claude — forskellen er, at det er dig, der flytter teksten. Se `doc\mine-data.md`.

Fase 1 starter først når Fase 0-gaten er bestået — altså når du har optaget et rigtigt møde på 20-30+ minutter og vurderet om dansk Whisper-output er brugbart. Se `fase0\LÆS_MIG.md`.

---

## Efter en kodeændring

Genopbyg og genudgiv, så genvejene peger på den nye version:

```bash
dotnet publish C:\NoteApp\src\Fase0Recorder\Fase0Recorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o C:\NoteApp\app
```

Luk optageren først — `Fase0Recorder.exe` kan ikke overskrives mens den kører.

Push efter en arbejdssession:

```bash
git -C C:\NoteApp push origin main
```

---

## Installation forfra på en anden PC

Hvis maskinen skiftes ud, eller alt skal sættes op igen:

**1. Forudsætninger**

```bash
winget install --id Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
```

En NVIDIA-GPU er ikke et krav, men den er forskellen på at vente og at lade den køre om natten. Uden GPU: brug `medium` frem for `large-v3`.

**2. Hent koden**

```bash
git clone https://github.com/ostergaardjorgen/NoteApp.git C:\NoteApp
```

Repoet er privat, så det kræver, at du er logget på GitHub. Bemærk at **kun koden ligger der** — dine data (optagelser, `learning.db`, ordliste) er aldrig i git og skal hentes fra din egen backup, se `doc\mine-data.md`.

**3. Hent whisper.cpp og modellerne**

De ligger bevidst uden for versionsstyring — 5 GB binærer hører ikke i et git-repo. Kør i PowerShell:

```powershell
$ProgressPreference='SilentlyContinue'
New-Item -ItemType Directory -Force C:\NoteApp\tools, C:\NoteApp\models | Out-Null
Invoke-WebRequest 'https://github.com/ggml-org/whisper.cpp/releases/download/v1.9.2/whisper-cublas-12.4.0-bin-x64.zip' -OutFile C:\NoteApp\tools\whisper.zip
Expand-Archive C:\NoteApp\tools\whisper.zip C:\NoteApp\tools\whisper -Force
foreach ($m in 'ggml-large-v3.bin','ggml-medium.bin') {
    Invoke-WebRequest "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$m" -OutFile "C:\NoteApp\models\$m"
}
```

Modellerne fylder 4,4 GB og tager et stykke tid.

**4. Byg og udgiv**

```bash
dotnet publish C:\NoteApp\src\Fase0Recorder\Fase0Recorder.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o C:\NoteApp\app
```

**5. Verificér at GPU'en bruges**

```bash
C:\NoteApp\tools\whisper\Release\whisper-bench.exe -m C:\NoteApp\models\ggml-large-v3.bin -w 0
```

Se efter `using CUDA0 backend` og `CUDA0 total size`. Er totalen større end kortets VRAM, skal du bruge `medium`.

---

## Vigtigt om placering

**Læg ikke modellerne på et netværksdrev.** Målt på denne maskine tog indlæsning af `ggml-large-v3.bin` **248 sekunder over SMB mod 4,9 sekunder fra lokal SSD** — og den omkostning betales hver eneste gang en transskription starter.

## Juridisk note

Loopback-optagelse fanger de andre deltageres lyd, **uden at Teams signalerer det**. Til dine egne møder på egen maskine er det uproblematisk, men oplys deltagerne — og skal outputtet nogensinde ind i en kundeleverance, er det et krav, ikke en høflighed.
