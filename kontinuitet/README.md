# Kontinuitet — sådan fortsætter arbejdet efter et nyt login

*Skrevet 23-08-2026, fordi e-mailadressen skifter og samtalehistorikken
dermed ikke følger med.*

## Hvad der ligger her, og hvorfor

Koden og beslutningerne har hele tiden ligget i repoet. Det, der **ikke**
gjorde, er tre ting, som ellers ville forsvinde med logind'et:

| Mappe | Hvad | Hvorfor det er nødvendigt |
|---|---|---|
| `skills/` | De tre værktøjer, arbejdet bruger | Uden dem kender en ny session ikke `byg`, `leverancetjek` eller `roadmap` |
| `hukommelse/` | Stående regler og præferencer | De er lært over mange samtaler og kan ikke udledes af koden |
| `arbejdsmaade.md` | Hvordan der arbejdes her | Den er den vigtigste af de tre — se nedenfor |

Start med **[`../doc/fortsaet-her.md`](../doc/fortsaet-her.md)**. Den er
indgangen; det her er kun kopierne.

## Sådan lægges det på plads igen

Skulle mappen `C:\Users\<bruger>\.claude\` være tom efter et nyt logind:

```powershell
# 1. Værktøjerne
$maal = "$env:USERPROFILE\.claude\skills"
New-Item -ItemType Directory -Force $maal | Out-Null
Copy-Item C:\NoteApp\kontinuitet\skills\* $maal -Recurse -Force

# 2. Hukommelsen
$hu = "$env:USERPROFILE\.claude\projects\C--ClaudeCode\memory"
New-Item -ItemType Directory -Force $hu | Out-Null
Copy-Item C:\NoteApp\kontinuitet\hukommelse\*.md $hu -Force
```

**Maskinen er den samme**, så filerne ligger der sandsynligvis allerede. Kopiér
kun, hvis de mangler — en kopi hen over noget nyere ville rulle det tilbage.

## Det, der IKKE er her — og hvorfor

**`forbudte-termer.txt`.** Listen over firmanavne og stier, der ikke må ud af
maskinen. Den må aldrig committes: at lægge den i et repo ville udstille
præcis det, den er sat i verden for at holde ude. Den ligger kun lokalt.

Er den væk, skal den skrives i hånden igen. Formatet står i
[`skills/leverancetjek/SKILL.md`](skills/leverancetjek/SKILL.md). **Uden den
er leverancetjekket en kontrol, der altid siger god for alt** — og så er den
farligere end ingen kontrol.

**`hemmeligheder/google-klient.json`.** Appens klient-id hos Google. Ikke
hemmeligt i egentlig forstand, men en legitimation i et repo bliver liggende i
historikken for evigt. Se [`../doc/google-integration.md`](../doc/google-integration.md).

**Selve dataene.** Optagelser, transkriptioner, dokumenter og indstillinger
ligger i `C:\AppNoter` og har aldrig været i git. De tages med appens egen
sikkerhedskopi under Indstillinger → Sikkerhedskopi.

**Den oprindelige leverancetjek-skill.** Den udgave, der ligger her, er skrevet
om: den oprindelige nævnte de forbudte termer i sin egen tekst, og så ville
vejledningen selv være det, den advarer imod. Funktionen er den samme.
