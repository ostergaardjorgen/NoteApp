---
name: feedback-powershell-hvor-staar-jeg
description: "Jørgen foretrækker altid PowerShell — enhver kommando skal være ægte PowerShell; shell, mappe og administrator står i teksten over blokken, aldrig inde i den"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 3be0811d-95c3-4771-8ac4-70e6d51317b8
  modified: 2026-09-19T17:32:10.303Z
---

**Jørgen foretrækker altid PowerShell.** Sagt udtrykkeligt 2. september 2026:
"og jeg foretrækker altid powershell". Det gælder uanset projekt.

**Hver gang jeg giver ham en kommando, skal der stå, hvor han skal stå:
hvilken shell, om den kræver administrator, og hvilken mappe. Det står i
teksten LIGE OVER kodeblokken — aldrig som kommentarlinje inde i blokken.**
Sagt 19. september 2026: "stop med at skrive tekst i samme boks som
kommandoen". Blokken indeholder kun det, der skal køres, så kopiér-knappen
giver ren kommando.

Bash-værktøjet må jeg fortsat bruge til mit eget arbejde — søgning,
filrettelser, scripts. Det er kun **det, han skal køre selv**, der altid er
PowerShell.

**Why:** 2. september 2026 gav jeg `rm -rf "$LOCALAPPDATA/Docker/run.gammel"`
i en bash-mærket blok. Jørgen kørte den i PowerShell, hvor `rm` er et alias
for `Remove-Item`, `-rf` ikke findes, og `$LOCALAPPDATA` er tom. Den fejlede
med `NamedParameterNotFound`. Han arbejder i PowerShell — ikke i Git Bash —
selv om jeg selv bruger Bash-værktøjet internt.

**How to apply:**

Skriv stedet som tekst over blokken, og hold blokken ren:

**På pc'en, i PowerShell som administrator (mappen er ligegyldig):**
```powershell
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Docker\run.gammel"
```

**På pc'en, i PowerShell i `C:\Vagtsom\VagtsomIAM`:**
```powershell
npm run dev
```

Oversættelser, jeg skal huske:

| Bash | PowerShell |
|---|---|
| `rm -rf sti` | `Remove-Item -Recurse -Force sti` |
| `$VAR` | `$env:VAR` |
| `cat`, `head -n`, `tail -n` | `Get-Content [-TotalCount n] [-Tail n]` |
| `mkdir -p` | `New-Item -ItemType Directory -Force` |
| `which x` | `(Get-Command x).Source` |
| `a && b` | `a; if ($?) { b }` — `&&` findes ikke i PS 5.1 |

Mærk blokken `powershell`, ikke `bash`, når indholdet er PowerShell. En
kør-knap, der kører i den forkerte shell, er værre end ingen knap.

## Kommandoer til NAS'en

**Første blok i et sæt NAS-kommandoer er altid login fra PowerShell.** Sagt
18. september 2026: "du skal altid have login på NAS med før dine NAS
kommandoer". Jeg må ikke gå ud fra, at han allerede har et SSH-vindue åbent.

**På pc'en, i PowerShell (mappen er ligegyldig):**
```powershell
ssh jorgen@192.168.1.155
```

Derefter kommer NAS-kommandoerne i `bash`-blokke. Prompten
`Jorgen@NAS_HOME:...$` er kendetegnet for, at han står på NAS'en; `PS C:\...>`
betyder pc'en. Han har forvekslet de to vinduer flere gange, så skriv hvert
trins overskrift med hvor det køres.

Fælder, der er gået galt før:
- `scp ... jorgen@192.168.1.155:~/` lander i `/volume1/Kameraer/` — SFTP-hjemmet
  er den delte mappe, ikke SSH-hjemmet.
- `/volume1/docker/plane-dk/.env` kan kun læses af root, så også `grep` skal
  have `sudo`.
- Tabulatorer i `--format 'table ...\t...'` overlever ikke; brug almindelig
  `docker compose ps`.

Se [[project-plane-dk]].

Se [[project-vagtsom-iam]].
