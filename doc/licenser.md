# Licenser og attribution — komponent for komponent

*Kortlagt 03-09-2026. Hører til `licenser\NOTICE.md`, `scripts\tjek-licenser.ps1`
og fanen «Modeller og licenser» på Compliance-skærmen.*

## Det, der var galt

Compliance-skærmen lovede allerede, at licensteksten «ligger i
installationsmappen» — ved sherpa-onnx (Apache-2.0) og ved krediteringen af
NVIDIA (CC-BY-4.0).

**Der lå ingen licensfiler nogen steder i repoet.** Ikke i app-mappen, ikke i
pakken, ikke i `installer\`. Skærmen beskrev en tilstand, der ikke fandtes.

Det er ikke en skønhedsfejl. Apache-2.0 kræver, at licensteksten følger med;
CC-BY-4.0 kræver kreditering af ophavet. Uden dem er komponenterne ikke
lovligt redistribueret — og appen sagde samtidig, at de var.

---

## Kortlægningen

Licenserne er slået op hos ophavsmændene 03-09-2026, ikke husket. NuGet-pakkernes
licenser er læst ud af de pakker, bygget faktisk bruger (`project.assets.json`,
ikke en formodning om versionen).

### Redistribueres — følger med installationen

| Komponent | Fil | Licens | Efterprøvet |
|---|---|---|---|
| sherpa-onnx | `talere\bin\sherpa-onnx-offline-speaker-diarization.exe` | Apache-2.0 | k2-fsa/sherpa-onnx, LICENSE |
| ONNX Runtime | `talere\bin\onnxruntime.dll` | MIT | microsoft/onnxruntime, LICENSE |
| pyannote segmentation 3.0 | `talere\segmentering.onnx` | MIT | Hugging Face-modelkortet: `license: mit` |
| NVIDIA NeMo TitaNet | `talere\stemmer.onnx` | **CC-BY-4.0** | Hugging Face-modelkortet: `license: cc-by-4.0` |
| NAudio 2.2.1 | I exe'en | MIT | Pakkens egen `license.txt` |
| SQLitePCLRaw 2.1.12 | I exe'en | Apache-2.0 | Pakkens nuspec: `Apache-2.0` |
| Microsoft.Data.Sqlite 10.0.11 | I exe'en | MIT | Pakkens nuspec |
| System.Management 10.0.0 | I exe'en | MIT | Pakkens nuspec |
| SQLite | I exe'en | Offentligt domæne | Ingen betingelser at videregive |
| .NET og WPF | Selvstændig udgivelse | MIT | dotnet/runtime, LICENSE.TXT |

### Hentes ned — lander i datamappen

| Komponent | Licens | Efterprøvet |
|---|---|---|
| whisper.cpp v1.9.2 | MIT | ggerganov/whisper.cpp, LICENSE |
| Whisper-modellerne (ggml) | MIT | openai/whisper, LICENSE |
| Silero VAD | MIT | snakers4/silero-vad, LICENSE; HF-kortet: `license: mit` |

De hentede står med i `NOTICE.md`, selv om de ikke følger med pakken. De ender
på brugerens maskine, og en opgørelse, der springer dem over, kan ikke bruges
til at svare på, hvad der ligger der.

---

## De to betingelser, der koster noget

### CC-BY-4.0 — NVIDIA NeMo TitaNet

Den eneste komponent, der ikke er MIT eller Apache. Betingelsen er
**kreditering**: NVIDIA Corporation skal nævnes som ophav. Det står i
`NOTICE.md` sammen med den fulde licenstekst, og det står på
Compliance-skærmen.

Modellen er **ikke ændret**. Den er konverteret til ONNX af
sherpa-onnx-projektet og indgår uændret. Det skal siges, fordi CC-BY-4.0
kræver, at ændringer oplyses.

### Apache-2.0 — sherpa-onnx og SQLitePCLRaw

Licensteksten skal følge med, og ændringer skal oplyses. Ingen af de to er
ændret; de indgår, som de blev udgivet.

---

## Gaten

`scripts\tjek-licenser.ps1` fejler, hvis en krævet fil mangler. Den kender to
slags kontrol:

| Kald | Hvad der kontrolleres |
|---|---|
| `-Mappe <sti>` | Filerne i den udgivne app-mappe |
| `-Msi <sti>` | Filerne i den byggede pakke, læst af Windows Installers eget fil-katalog |

**De to er ikke det samme spørgsmål.** `..\app\**` i `HeyPia.wxs` samler op,
men en `Exclude`-regel eller en fil, wix springer over, ville give en pakke,
der mangler noget — uden at bygningen fejler.

Gaten køres af `udgiv.ps1` (mod app-mappen) og af `byg-installer.ps1` (mod
app-mappen før pakningen og mod `.msi` efter).

**Listen står ét sted.** Den stod før i to: `udgiv.ps1` og `byg-installer.ps1`
havde hver sin kopi af taleradskillelsens filer. To lister om det samme driver
fra hinanden.

`LicensgateTest` kører det rigtige script — ikke en C#-kopi af logikken —
bygger en komplet mappe, fjerner hver krævet fil **ét ad gangen** og
kontrollerer, at gaten siger nej og nævner netop den fil. En gate, ingen har
set fejle, er ikke en gate.

---

## Sidegevinst: installationspakken kunne ikke bygges

Navneskiftet 28-08-2026 omdøbte indholdet, men ikke filerne.
`byg-installer.ps1` og `Bundle.wxs` pegede på `HeyPia.wxs` og `HeyPia.msi`,
mens filen hed `NoteApp.wxs`. Byggescriptet ville stoppe med «filen findes
ikke».

Det var ikke opdaget, fordi de sidste byggede artefakter i `installer\` var
fra 25-08 — altså fra før navneskiftet. De så rigtige ud, og de lå det rigtige
sted.

`NoteApp.wxs` hedder nu `HeyPia.wxs`, og de forældede byggeartefakter er
slettet (de er i forvejen uden for versionsstyring). Pakken er bygget igen
03-09-2026: `HeyPia-setup.exe` på 109 MB og `HeyPia.msi` på 107 MB, begge med
alle 15 krævede filer.
