# Kontinuitet — sådan fortsætter arbejdet efter et nyt login

*Oprettet 23-08-2026. Opdateret 22-09-2026: kopien holdes nu ajour af sig
selv ved hvert push.*

Start med **[`../doc/fortsaet-her.md`](../doc/fortsaet-her.md)**. Den er
indgangen og har tjeklisten til en ny konto og en ny maskine; det her er kun
kopierne.

## Hvad der ligger her, og hvorfor

Koden og beslutningerne ligger i repoet. Det, der **ikke** gør, ligger i
`%USERPROFILE%\.claude` på maskinen — og følger hverken en ny maskine eller et
repo, der hentes et andet sted:

| Mappe | Hvad | Hvorfor det er nødvendigt |
|---|---|---|
| `skills/` | Værktøjerne `byg`, `leverancetjek`, `roadmap` og `test` | Uden dem kender en ny session dem ikke |
| `hukommelse/` | Stående regler og præferencer | Lært over mange samtaler; kan ikke udledes af koden |
| `arbejdsmaade.md` | Hvordan der arbejdes, og hvordan der svares | Den vigtigste af de tre |

**Hukommelsen dækker alt arbejde startet fra `C:\ClaudeCode`**, ikke kun
HeyPia — også Plane DK og Vagtsom IAM. Indekset (`MEMORY.md`) er ét for dem
alle, og det deles ikke op.

## Holdes ajour af sig selv

`scripts\sikker-kode.ps1 -Push` kører `opdater-kontinuitet.ps1` først: det,
der er ændret i `~/.claude`, kopieres hertil, leverancetjekkes og committes,
før der pushes. En hukommelse, der er slettet i `~/.claude`, slettes også her
— git har den stadig.

Ved hånden:

```powershell
powershell -File C:\NoteApp\scripts\opdater-kontinuitet.ps1          # kopiér hertil
powershell -File C:\NoteApp\scripts\opdater-kontinuitet.ps1 -Tjek    # kun se forskellen
```

## Sådan lægges det på plads igen

```powershell
powershell -File C:\NoteApp\scripts\gendan-kontinuitet.ps1
```

Den lægger det på plads, der mangler i `~/.claude`. **Findes en fil og er
anderledes, røres den ikke** — den kan være nyere end kopien i git. Med
`-Overskriv` vinder kopien alligevel.

## Det, der IKKE er her — og hvorfor

**`forbudte-termer.txt`.** Listen over navne og stier, der ikke må ud af
maskinen. Den må aldrig committes: at lægge den i et repo ville udstille
præcis det, den er sat i verden for at holde ude. Begge scripts nægter at tage
den med.

Den ligger i `C:\NoteApp-backup\lokalt-ikke-i-git\` sammen med de globale
regler og Googles klientfil — **kun på den maskine**. Er den væk, skal den
skrives i hånden igen. Formatet står i
[`skills/leverancetjek/SKILL.md`](skills/leverancetjek/SKILL.md). **Uden den er
leverancetjekket en kontrol, der altid siger god for alt** — og så er den
farligere end ingen kontrol.

**`hemmeligheder/`.** Googles klientfil. En legitimation i et repo bliver
liggende i historikken for evigt. Se
[`../doc/google-integration.md`](../doc/google-integration.md).

**De globale regler (`~/.claude/CLAUDE.md`).** Kopieres til backup-mappen, ikke
hertil. Det vigtigste i dem står også i [`../CLAUDE.md`](../CLAUDE.md) og i
[`arbejdsmaade.md`](arbejdsmaade.md).

**Selve dataene.** Optagelser, transkriptioner, dokumenter og indstillinger
ligger i `C:\AppNoter` og har aldrig været i git. De tages med appens egen
sikkerhedskopi og arkivet på fællesdrevet.

**Den oprindelige leverancetjek-skill.** Den udgave, der ligger her, nævner
ikke de forbudte termer i sin egen tekst — ellers ville vejledningen selv være
det, den advarer imod.
