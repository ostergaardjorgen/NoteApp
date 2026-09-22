---
name: project-heypia-aabne-punkter
description: "Åbne punkter i HeyPia pr. 22-09-2026 (v1.3.78) — det, der ikke er efterprøvet, det, der venter, og vaner der virker"
metadata: 
  node_type: memory
  type: project
  modified: 2026-09-22T07:39:07.879Z
  originSessionId: 905423e3-639c-4277-a132-b2fe72a78504
---

Status 22-09-2026, efter **v1.3.78**. Alt er committet og pushet. Indgangen for
en ny session er `C:\NoteApp\doc\fortsaet-her.md`.

## Ikke efterprøvet endnu

- **Et rigtigt opkald med v1.3.78.** Motorlåsen (kun én whisper ad gangen) er
  prøvet på motoren, ikke under et opkald. Spørgsmålet er, om samtalen nu
  bliver skrevet ud undervejs. 21-09-2026 nåede medskrivningen kun 4 af 41
  minutter, fordi tre large-v3 kørte samtidig på et 6 GB-kort.
- **En ny aftale i Google Kalender efter v1.3.77** (tidszonen sendes nu som
  IANA-navn). Der er ikke oprettet en rigtig aftale — Google sender
  invitationer.

## Venter på Jørgen

- **Talergenkendelse på den anden maskine** — sidste punkt i etape 2.
- **Omdøbning NoteApp → HeyPia** i kode, mapper og repo. Venter på besked.
- **Installeren signeres** — Smart App Control blokerer den. Står i roadmap.
- **Skærmbilleder til produktarket** (`Salg/HeyPia-produktark.docx`). Sidst
  kendte status 04-09-2026: tre pladsholdere. Ikke fulgt op siden.
- **Talergenkendelse — to steder siger ikke det samme** (salgsargumenter mod
  Compliance-skærmen). Sidst kendte status 04-09-2026.

## Vaner, der virker i dette projekt

- WPF-skærme efterprøves med et STA-konsolprojekt i scratchpad, der bygger
  `NoteApp.Desktop`, viser skærmen og måler den. **Prøven skal kunne fejle**:
  lav en kontrol, der genskaber fejlen, før en grøn prøve tæller.
- **`udgiv.ps1` LUKKER den kørende app.** Tjek, at der hverken optages eller
  kører `whisper-cli`, før der udgives.
- Leverancetjekket skriver med `Write-Host` — fang det med `*>&1 | Out-String`,
  ellers ser et bestået tjek ud som et fejlet.
- `sikker-kode.ps1 -Push` holder `kontinuitet/` ajour med hukommelse og skills.

Se [[feedback-vis-altid-hvad-der-koerer]] og [[feedback-byg-efter-commit]].
