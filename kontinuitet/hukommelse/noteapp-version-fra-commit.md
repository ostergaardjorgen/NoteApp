---
name: noteapp-version-fra-commit
description: "NoteApps versionsnummer udledes af seneste commit-besked, så gentagne udgivelser uden commit får samme nummer"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-14T09:45:10.795Z
---

`scripts\udgiv.ps1` læser versionen ud af den seneste commit-besked, der skal
have formen `vX.YY: beskrivelse`. Den skrives ind i csproj og ender i appens
sidebjælke.

**Why:** Konsekvensen er, at flere udgivelser uden en commit imellem alle får
det SAMME versionsnummer. Under en lang arbejdsdag med tyve builds står der
v0.54 på dem alle, og så kan man ikke se på nummeret, hvad der kører.

**How to apply:** Meld altid tidsstemplet sammen med nummeret
(se [[meld-altid-release-nummer]]). Skal nummeret selv rykke sig, kræver det en
commit med en ny `vX.YY:`-besked — og commits laves kun, når brugeren beder om
det. Kør `leverancetjek` før hver commit.
