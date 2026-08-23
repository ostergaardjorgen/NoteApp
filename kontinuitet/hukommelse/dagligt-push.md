---
name: dagligt-push
description: "Der skal committes og pushes hver dag; er det ikke sket dagen før, er det første handling på en ny dag"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-23T18:34:58.975Z
---

**Der committes og pushes hver dag.** Skete det ikke dagen før, er det den
**første handling** på en ny dag — før der bygges videre på noget.

Kør:

```powershell
powershell -File C:\NoteApp\scripts\sikker-kode.ps1 -Push
```

Den kører leverancetjek med `-Historik` først og stopper ved ethvert fund,
laver et git-bundt og efterprøver, at det kan læses, og kopierer det, der
aldrig må i git.

**Why:** 23-08-2026 lå der 75 commits, der aldrig var pushet. De var ikke
tabt, men de fandtes ét sted — på den ene maskine. Et push er noget, man
husker, lige indtil man har travlt.

**How to apply:** Ved dagens første opgave, se efter om der er upushede
commits (`git rev-list --count origin/main..HEAD`). Er der det, og er de fra
i går eller tidligere, så push dem, før du går i gang med noget nyt. Sig det
kort — det er ikke et spørgsmål, det er en rutine.

Se også [[meld-altid-release-nummer]] og [[noteapp-version-fra-commit]].
