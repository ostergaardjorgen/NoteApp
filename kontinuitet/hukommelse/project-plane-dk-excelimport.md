---
name: project-plane-dk-excelimport
description: Plane DK - import af projektplan fra Excel; de trufne designvalg og hvor planen ligger
metadata:
  type: project
---

Besluttet 16-09-2026. Fuld plan: `C:\ClaudeCode\leverancer\Plane-DK-import-af-projektplan-2026-09-16.docx`.

- Importen bygges **ind i Plane DK**, ikke som eksternt script: proxyen svarer 404 paa
  `/api/v1`, saa API-noegler kan ikke bruges paa denne installation.
- Koden ligger i sin **egen Django-app** ved siden af `plane.mfa`, saa upstream-flet forbliver nem.
- Spor bliver moduler, WBS-nummer bliver `external_id` (noeglen til gen-import uden dubletter).
- Afhaengigheder: forgaenger bliver relationen `finish_before`, tvaergaaende milepaele bliver `blocked_by`.
- **Planlaegningsform pr. projekt**, gemt i egen tabel: `Faseplan` (standard - faser er cyklusser,
  fremdrift pr. fase) eller `Sprintplan` (faser er etiketter, cyklusser er sprints, til ren udvikling).
  En opgave kan kun ligge i **een** cyklus; det er det, der tvinger valget.
- Regnearket ejer strukturen, Plane ejer fremdriften: gen-import roerer ikke tilstand, tildeling,
  kommentarer eller estimat.

Se [[project-plane-dk]] og [[dansk-tegn-i-brugerflade]].
