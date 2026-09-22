# Fortsæt her

*Opdateret 22-09-2026 ved v1.3.78. Læs den her først — så kan arbejdet
fortsætte uden samtalehistorikken, også med en ny Claude-konto.*

## Hvis du er en ny session

Læs de her, i den rækkefølge:

1. **[`../CLAUDE.md`](../CLAUDE.md)** — reglerne for repoet. Indlæses af sig
   selv, når Claude Code startes med `C:\NoteApp` som mappe.
2. **[`../kontinuitet/arbejdsmaade.md`](../kontinuitet/arbejdsmaade.md)** —
   hvordan der arbejdes, og hvordan Jørgen vil have svar. Reglerne kan ikke
   udledes af koden.
3. **[`roadmap.md`](roadmap.md)** — hvad der er på vej, og hvad der venter.
4. **[`findings.md`](findings.md)** — alt, der er målt. Kig HER, før noget
   bliver undersøgt igen.

Spørg Jørgen, hvis noget er uklart. **Gæt aldrig på et tal.**

## Ny Claude-konto — sådan kommer du i gang

### Samme maskine

1. Log ud og ind med den nye konto i Claude Code (`/login` i terminalen, eller
   log ud og ind i Claude-appen).
2. Hukommelse, skills og de globale regler ligger som filer i
   `%USERPROFILE%\.claude` og følger maskinen, ikke kontoen. Tjek, at de er
   der:

   ```powershell
   powershell -File C:\NoteApp\scripts\gendan-kontinuitet.ps1
   ```

   Den lægger kun det på plads, der mangler, og rører ikke en fil, der er
   anderledes — den kan være nyere end kopien i git.
3. Start Claude Code i `C:\ClaudeCode` med `C:\NoteApp` som ekstra mappe —
   det er den opsætning, hukommelsen hører til (`C--ClaudeCode`). Første
   besked: *«Læs C:\NoteApp\doc\fortsaet-her.md og fortsæt.»*

### Ny maskine

1. Installér Git, GitHub CLI, .NET 10 SDK og Claude Code.
2. `gh auth login` som **ostergaardjorgen** — repoet er privat.
3. Hent koden til **`C:\NoteApp`** (stien bruges i scripts og dokumentation):

   ```powershell
   git clone https://github.com/ostergaardjorgen/NoteApp.git C:\NoteApp
   ```

4. Læg hukommelse og skills på plads:

   ```powershell
   powershell -File C:\NoteApp\scripts\gendan-kontinuitet.ps1
   ```

5. Hent det, der **aldrig er i git**, fra den gamle maskines
   `C:\NoteApp-backup\lokalt-ikke-i-git\`:

   | Fil | Lægges i |
   |---|---|
   | `forbudte-termer.txt` | `%USERPROFILE%\.claude\skills\leverancetjek\` |
   | `globale-regler-CLAUDE.md` | `%USERPROFILE%\.claude\CLAUDE.md` |
   | `client_secret_*.json` | `C:\NoteApp\hemmeligheder\` |

   **Mappen ligger kun på den gamle maskine.** Er den væk, skal
   `forbudte-termer.txt` skrives igen i hånden — uden den godkender
   leverancetjekket alt. Formatet står i
   [`../kontinuitet/skills/leverancetjek/SKILL.md`](../kontinuitet/skills/leverancetjek/SKILL.md).
6. Data (`C:\AppNoter`) hentes med appens egen sikkerhedskopi, eller fra
   arkivet på fællesdrevet under Indstillinger → Deling. Whisper-motoren og
   modellerne hentes af appen selv ved første start.

## Hvad appen er

**HeyPia** — en Windows-app (WPF, .NET 10) til at optage møder, webinarer og
telefonopkald, skrive dem ud til tekst lokalt, og finde tilbage til det, der
blev sagt. Kunden er studerende, iværksættere og mindre selvstændige; der
måles op mod **Granola**.

**Transkription er ikke produktet — det er prisen for at komme ind.** Værdien
vokser med arkivet, og søgningen er derfor ikke en funktion i Cockpittet; den
ER Cockpittet.

## Hvor tingene ligger

| Hvad | Hvor |
|---|---|
| Kode | `C:\NoteApp` — GitHub `ostergaardjorgen/NoteApp` (privat) |
| Udviklingsudgaven | `C:\NoteApp\app\HeyPia.exe` — genvejen «HeyPia_dev» |
| Installeret udgave | `C:\Program Files\HeyPia\HeyPia.exe` |
| Brugerens data | `C:\AppNoter` — aldrig i git |
| Motor og modeller | `C:\AppNoter\motor` (`modeller\` har ggml-filerne) |
| Fællesdrev | `P:\HeyPia\deling` (arkiv og deling mellem maskiner) |
| Installationsfiler | `P:\HeyPia\setup` |
| Legitimationer | `C:\NoteApp\hemmeligheder\` — aldrig i git |
| Kode-backup | `C:\NoteApp-backup` — git-bundter og det, der ikke må i git |

Maskinen: RTX 2060 med 6 GB VRAM, i7-1165G7 (4 kerner), 32 GB RAM, Windows 11.
**Alle tal i `findings.md` er målt på den.**

Navnet i koden er stadig **NoteApp** (mapper, namespaces, repo). Omdøbningen
til HeyPia venter på Jørgens besked.

## Det daglige

| Hvad | Sådan |
|---|---|
| Prøverne | `powershell -File C:\NoteApp\scripts\proev.ps1` |
| Leverancetjek | `powershell -File "$env:USERPROFILE\.claude\skills\leverancetjek\tjek-leverance.ps1" -Sti C:\NoteApp` |
| Udgiv udviklingsudgaven | `powershell -File C:\NoteApp\scripts\udgiv.ps1` |
| Push (hver dag) | `powershell -File C:\NoteApp\scripts\sikker-kode.ps1 -Push` |
| Installationsfil | skill `byg` — kun når Jørgen beder om det |

Rækkefølgen: **prøver → leverancetjek → commit `vX.Y.Z: …` → udgiv → start
appen → meld versionsnummer og tidspunkt.** Versionsnummeret kommer fra den
seneste commit, der starter med `vX.Y.Z:`.

**`udgiv.ps1` lukker den kørende app.** Udgiv aldrig, mens der optages eller
skrives ud — tjek først, at ingen `whisper-cli` kører.

`sikker-kode.ps1 -Push` opdaterer også `kontinuitet/` fra `~/.claude`, så
hukommelse og skills følger med hvert push.

Udviklingsværktøjet (`src\NoteApp.Tools`, bygges til `heypia.exe`):

```bash
heypia status         # hvor data ligger, og hvad de indeholder
heypia motor          # hvilken whisper-motor og model der bruges
heypia maalsoegning   # søgeprøver med kendt facit
heypia maaldato       # datoer i talesprog
```

## Status ved v1.3.78

### Lavet siden v1.2.81 (04-09-2026)

- **Arkiv på fællesdrevet**: optagelser, projekter og skabeloner fra begge
  maskiner, med tidsplan og automatisk hentning. Se [`deling.md`](deling.md).
- **Telefonopkald via Telefonlink**: boble med sprogvalg, folderen **Opkald**,
  navn efter dato og klokkeslæt. Opkaldet kendes på, at Windows' lydtjeneste
  optager fra mikrofonen, mens en telefon er parret (Bluetooth 0x111F).
- **Fri søgning**: en hel sætning forstås — periode, type, sprog, mappe og
  projekt. Filtret **Typer** (møder, webinarer, opkald, noter). Tips til
  søgning i søgefeltet.
- **Svag tale forstærkes** før udskrift. Målt: 8,4 % → 6,1 % ordfejl.
  [`findings.md`](findings.md) afsnit 10.
- **Kun én udskrift ad gangen** (`Motorlaas`). Tre samtidige large-v3 låste
  maskinen 21-09-2026.
- **Google Kalender**: tidszonen sendes som IANA-navn; nye aftaler blev afvist.

### Ikke efterprøvet endnu

- Et rigtigt opkald med v1.3.78: bliver samtalen skrevet ud undervejs?
- En ny aftale oprettet i Google Kalender efter v1.3.77.

### Venter

- **Talergenkendelse på den anden maskine** — det sidste punkt i etape 2.
- **Omdøbning NoteApp → HeyPia** (kode, mapper, repo) — venter på Jørgen.
- **Installeren skal signeres** — Smart App Control blokerer den på en ny
  maskine. Se [`roadmap.md`](roadmap.md).
- Resten står i [`roadmap.md`](roadmap.md) og i hukommelsen
  (`project-heypia-aabne-punkter.md`).
