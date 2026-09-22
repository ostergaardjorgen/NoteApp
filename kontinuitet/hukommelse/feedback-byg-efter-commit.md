---
name: feedback-byg-efter-commit
description: Udgiv dev-appen og start den efter hvert commit; Windows-installeren bygges KUN på Jørgens kommando
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 905423e3-639c-4277-a132-b2fe72a78504
  modified: 2026-09-04T12:20:01.318Z
---

**Efter hvert commit: udgiv dev-appen og start den. Byg IKKE installeren.**

`udgiv.ps1` tager under et minut; `byg-installer.ps1` tager flere minutter og
pakker 300 MB ned. Jørgen tester i dev-appen, og en frisk Windows-installer
laves kun, når han beder om den. Sagt 04-09-2026, efter at jeg havde bygget
installeren ved hver eneste af tolv udgivelser samme formiddag.

**Why:** et commit uden et byg er ikke færdigt — rettelsen findes så kun i
kildekoden, genvejen åbner den gamle udgave, og fejlsøgningen starter forfra på
noget, der allerede er rettet. Men det er dev-appen, der skal være frisk, ikke
installationsfilen.

**How to apply:**

1. `leverancetjek` → commit → `udgiv.ps1` → start appen igen.
2. `byg-installer.ps1` KUN når Jørgen siger til.
3. Commit-beskeden SKAL starte med `vX.Y.Z:`, ellers hæver `udgiv.ps1` ikke
   versionen. `udgiv.ps1` retter selv csproj bagefter — det bliver et commit
   mere, «vX.Y.Z: versionsnummer sat af udgiv.ps1».
4. `udgiv.ps1` LUKKER den kørende app. Derfor skal den startes igen bagefter;
   den udgivne exe ligger i mappen `app` under kodelageret.
5. Meld version og tidsstempel bagefter — se [[meld-altid-release-nummer]].
6. Push kan samles til sidst: `sikker-kode.ps1 -Push` kører leverancetjek med
   historik først. Se [[dagligt-push]].
