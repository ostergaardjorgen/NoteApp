# Fase 0 — feasibility gate

Formålet er at afgøre **to ting**, før der bygges noget som helst UI:

1. **Er dansk Whisper-output brugbart?** Navne, fagtermer og forkortelser skal kunne læses af et menneske, og en LLM skal kunne lave et retvisende referat af det.
2. **Hvor lang tid tager transskriptionen i forhold til mødets længde?** Er realtidsfaktoren (RTF) over 1,0, skal transskription planlægges som **natjob** frem for noget du venter på. Det ændrer hele UI-designet i Fase 1 og 2.

Spørgsmål 2 er delvist besvaret (se nedenfor). Spørgsmål 1 kræver et **rigtigt møde** — det kan kun du afgøre.

---

## Hvad der allerede er målt (8. august 2026)

| Måling | Resultat | Betydning |
|---|---|---|
| CUDA-backend | Aktiv, `ggml-cuda.dll` indlæst | GPU'en bruges, ikke CPU |
| `large-v3` VRAM-forbrug | **3094 MB af 6143 MB** | Passer med god margin. Det åbne spørgsmål fra speccen er afgjort: `large-v3` er realistisk |
| Encoder pr. 30-sekunders vindue | 208 ms | Hurtigt nok til at RTF bliver lav |
| Modelindlæsning, lokal SSD | **4,9 sek** | Engangsomkostning pr. kørsel |
| Modelindlæsning, NAS over SMB | **248 sek** | 50× langsommere — derfor ligger projektet lokalt, se nedenfor |
| SMB-gennemløb til NAS | 13 MB/s | Fint til dokumenter, ubrugeligt til 3 GB modeller |
| Ende-til-ende gennem scriptet | OK, både `medium` og `large-v3` | Kæden virker |

**Forbeholdet:** ovenstående RTF-tal stammer fra en 12-sekunders røgtest, hvor modelindlæsningen dominerer totalen. De siger at *kæden virker*, ikke hvad et 90-minutters møde koster. Det tal får du først i trin 2.

## Hardware

| | |
|---|---|
| GPU | NVIDIA GeForce RTX 2060, 6143 MiB VRAM, driver 591.44 |
| CPU | Intel i7-1165G7, 4 kerner / 8 tråde |
| RAM | 32 GB |

## Placering

Alt ligger i `C:\NoteApp\` — kildekode, modeller, whisper-binærer og optagelser. Målingen ovenfor viser hvorfor der ikke arbejdes fra et netværksdrev. Koden backes op til GitHub (privat repo).

---

## Trin 1 — optag oplæsningen

Start **NoteApp** fra skrivebordet og bliv på skærmen *Oplæsning*. Den viser teksten ét afsnit ad gangen, holder øje med tempoet og sætter blokmærker undervejs.

**Ret navnene i `oplaesning\testtekst.md` først.** De opdigtede navne kan ikke afsløre det, testen skal måle — det er dine rigtige kollegaers og kunders navne, Whisper staver forkert. Genstart appen efter rettelsen.

Læs i normalt taletempo, og hold øje med tempoindikatoren øverst til højre. Målet er 15-20 minutters lyd; under 15 minutter giver RTF-tal, hvor modelindlæsningen fylder for meget.

Alternativt, til et rigtigt møde frem for oplæsningen:

```bash
C:\NoteApp\app\Fase0Recorder.exe --type online --title "Kundemøde Nordby"
```

Mødetypen skal vælges aktivt — der er ingen default. `--type fysisk` optager kun mikrofonen.

Ved onlinemøde måler optageren loopback-niveauet i 3 sekunder først. **Får du "STILHED", så stop og ret det**: enten kører Teams ikke endnu, eller lyden går til en anden enhed end standard-afspilningsenheden. Det er den fejl der koster et helt møde.

Under optagelsen vises niveaumålere pr. spor. ENTER stopper.

Output i `C:\NoteApp\fase0\optagelser\<dato>_<titel>\`:
- `mikrofon.wav` — dig i rummet, 16 kHz mono
- `loopback.wav` — de andre deltagere via Teams (kun onlinemøde)
- `meeting.json` — mødetype, varighed, enhedsnavne, starttidsstempel

**Optag mindst 20-30 minutter.** Et kort testmøde giver ubrugelige RTF-tal, fordi modelindlæsningen så dominerer, og siger intet om hvordan kvaliteten holder over tid.

Skal optageren bygges igen efter en kodeændring:

```bash
dotnet build C:\NoteApp\src\Fase0Recorder\Fase0Recorder.csproj -c Release
```

---

## Trin 2 — kør gaten

```bash
powershell -File C:\NoteApp\scripts\koer-fase0-test.ps1
```

Uden argumenter tager den den nyeste optagelse og kører alle fire kombinationer pr. spor: `medium` og `large-v3`, hver med og uden IAM-ordliste.

Den leder begge steder, der kan ligge optagelser: `fase0\optagelser\` (konsol-optageren) og din datamappe `%LOCALAPPDATA%\NoteApp\moeder\` (appen). Ordlisten tages fra datamappen, hvis den findes — det er den, appens ordbog skriver til. En bestemt optagelse vælges med `-Session <mappenavn>` eller `-Sti <fuld sti>`.

Kun én model, og tving CPU for at se hvad GPU'en er værd:

```bash
powershell -File C:\NoteApp\scripts\koer-fase0-test.ps1 -Modeller large-v3 -KunCpu
```

Resultater:
- `fase0\transskriptioner\` — `.txt` og `.json` pr. kørsel, plus `.log` med whisper.cpp's egen output
- `fase0\resultater\rtf_<tidspunkt>.csv` — måletabellen

---

## Trin 3 — vurdér (det er dig, ikke scriptet)

Scriptet måler tid. **Kvaliteten skal du læse dig til.** Konkret:

- **Fagtermer.** Blev "Entra ID", "SCIM", "provisionering", "attestering" til volapyk? Sammenlign `medOrdliste` mod `udenOrdliste` — forskellen dér er hele værdien af feature #3.
- **Navne.** Kollegaer, kunder, produktnavne. Tilføj dem til `..\ordliste.txt` og kør igen; ordlisten er billigste kvalitetsgevinst i hele projektet.
- **Negationer.** Hørte Whisper "skal ikke" eller "skal"? Det er dét, feature #5 (afspilning synkroniseret med transskript) findes for — er der mange af dem, er #5 ikke valgfri.
- **medium vs. large-v3.** Er forskellen stor nok til at retfærdiggøre tidsforskellen? Er medium brugbar, er den det rigtige valg til hverdagsbrug.
- **LLM-testen.** Kopiér en transskription ind i Claude og bed om et referat. Er det retvisende? Det er den egentlige gate — transskriptionen er ikke produktet, referatet er.

Skriv konklusionen ned i `resultater\konklusion.md`. Fase 1 starter først derefter.

---

## Ordliste

`..\ordliste.txt` fyldes ind i Whispers `initial_prompt`. Hold den under ca. 200 ord — prompten stjæler plads fra selve transskriptionen. Skriv den som sammenhængende dansk tekst, ikke som en punktopstilling; Whisper konditionerer på sprogtone, ikke på lister.

Den nuværende liste dækker IAM-termer og produktnavne. **Kollegaers og kunders navne mangler** — dem kender jeg ikke, og de er præcis dem Whisper oftest staver forkert.

---

## Faldgruber der allerede er ryddet af vejen

Skrevet ned, så de ikke skal findes igen:

- **PowerShell 5.1 læser `.ps1` uden BOM som ANSI.** En tankestreg i en dansk kommentar bliver da til et smart citationstegn, som PowerShell opfatter som streng-afslutning — scriptet fejler med uforståelige parse-fejl langt fra den egentlige linje. `koer-fase0-test.ps1` er gemt med UTF-8 BOM og skal blive ved med at være det.
- **`main.exe` i whisper-udgivelsen er forældet.** Den skriver kun en advarsel og transskriberer intet. Scriptet vælger nu `whisper-cli.exe` eksplicit.
- **whisper.cpp skriver al fremdrift til stderr.** Med `$ErrorActionPreference = 'Stop'` pakker PowerShell 5.1 hver stderr-linje fra en native exe ind som en terminerende fejl. Scriptet sænker preferencen omkring selve kaldet.
- **`BufferedWaveProvider.ReadFully` er `true` som standard** og returnerer stilhed i stedet for 0 bytes når bufferen er tom. Første version af optageren skrev derfor 192 MB på 9 sekunder. Sat til `false`.
