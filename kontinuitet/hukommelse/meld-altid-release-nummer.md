---
name: meld-altid-release-nummer
description: "Efter hver release skal versionsnummeret stå i chatten, så brugeren kan kontrollere at produktionen kører nyeste build"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-14T09:44:57.072Z
---

Hver gang der udgives en ny release af NoteApp, skal **versionsnummeret skrives
direkte i chatsvaret** — ikke kun i scriptets output.

**Why:** Brugeren åbner appen og læser versionen i sidebjælken for at
kontrollere, at det er den nyeste, der kører. Uden nummeret i chatten kan det
ikke sammenlignes, og man risikerer at teste på en gammel build og tro, at en
rettelse ikke virkede.

**How to apply:** Skriv version OG tidsstempel efter hver `udgiv.ps1`-kørsel,
fx «v0.55 · 14-08 11:43». Tidsstemplet er nødvendigt, fordi versionen udledes
af seneste commit-besked: udgives der flere gange uden at committe, står
nummeret stille, og så er tidsstemplet det eneste, der adskiller build'ene.
Se [[noteapp-version-fra-commit]].
