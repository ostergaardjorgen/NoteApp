# Installation af NoteApp

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

Dobbeltklik på **NoteApp**. Den har to skærme, og de svarer til de to ting, der skal gøres, før Fase 0-gaten kan bestås.

### Oplæsning

Teleprompteren til testoplæsningen. Den viser ét afsnit ad gangen i stor skrift, med det næste svagt nedenunder, så du kan tage tilløb uden at miste linjen.

- **Mellemrum** (eller pil ned/højre) går til næste afsnit. **Pil op/venstre** går tilbage.
- Uret viser både din faktiske tid og **hvor du burde være** — plus om du er foran eller bagud. Det er ikke pynt: læser du teksten på 12 minutter, er optagelsen for kort til at måle RTF på, og så skal den laves om.
- **Niveaumåleren bliver rød ved stilhed.** Det er den fejl, man ellers opdager bagefter.
- Ved hvert blokskift skrives et mærke i `notes.jsonl`, så transskriptionen kan holdes op mod facitlisten blok for blok i stedet for at skulle læses igennem i ét stræk.

Optagelsen er altid et **fysisk møde** — ét spor, kun mikrofonen. Der er ingen anden part at optage.

Teksten hentes fra `C:\NoteApp\fase0\oplaesning\testtekst.md`, hvis repoet er der. **Ret navnene i den fil til rigtige kollegaer og kunder, før du læser** — og genstart appen bagefter. Er repoet ikke der, bruger appen den kopi, der er indlejret i exe'en.

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

Panelet nederst til højre viser **den prompt, Whisper faktisk får**, og hvor mange af de 224 tokens den fylder. Bliver tallet gult, er der ord, der ikke kom med — så er det vægten, der afgør, hvem der ryger ud. Knappen **Gem ordlisten til Whisper** skriver den til `%LOCALAPPDATA%\NoteApp\ordliste.txt`, og det er den fil, gate-scriptet læser.

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

Det her er **Fase 0-værktøjet**, ikke det færdige produkt. Der er bevidst intet vindue, ingen knapper og ingen indstillinger — kun et konsolvindue der optager.

Følgende kommer i senere faser og findes ikke nu:

| Feature | Fase |
|---|---|
| Rigtigt WPF-vindue med start/stop-knap | 1 |
| Live-noter under mødet | 1 |
| Global hotkey til "markér nu" | 1 |
| Chunked autosave i 30-sekunders segmenter | 1 |
| Talergenkendelse og navngivning | 2 |
| Afspilning synkroniseret med transskript | 4 |

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
