---
name: noteapp-modeller-eet-sted
description: "Whisper-modeller ligger kun i C:\AppNoter\motor\modeller — aldrig i kodelageret"
metadata:
  node_type: memory
  type: project
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-25T15:05:00.000Z
---

Whisper-modellerne (`ggml-*.bin`) ligger ét sted:
`C:\AppNoter\motor\modeller` — altså `{datamappe}\motor\modeller`.
`WhisperInstall.ModelDirectory` peger derhen, og der ledes ikke andre steder.

**Why:** Koden ledte før også i `C:\NoteApp\models`, en hårdkodet sti ind i
kodelageret. På en kundes maskine findes den mappe ikke, så opslaget kunne
aldrig give noget — men på udviklingsmaskinen virkede det, og så endte
modellerne spredt: large-v3 og medium i kodelageret, small i datamappen.
Så kan man ikke svare på, hvad maskinen har, uden at lede to steder og huske
begge. Filerne er op til 2,9 GB stykket og hører uden for git.

**How to apply:** Led aldrig efter en model andre steder, og læg aldrig en
model i `C:\NoteApp`. Skal en model hentes, er `ModelDestination` svaret på
hvorhen. Katalogets `Length` er det byte-tal, en hentet fil skal ramme
præcist — ellers er filen ikke hel, og en halv model giver volapyk frem for
en fejl.
