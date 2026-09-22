---
name: feedback-plane-dk-upstream-aendringer
description: "Plane DK må gerne ændre Planes egne filer, når det dokumenteres og markeres; footprint-budgettet er ikke en grænse"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 10c65f2f-a8ae-4b95-82aa-392a350eade9
  modified: 2026-09-18T15:58:44.944Z
---

Fra 18-09-2026: det gør ikke længere noget, at Plane DK ændrer i Planes egne filer. Kravet er, at hver ændring dokumenteres og holdes styr på, så potentialet ved en senere Plane-opdatering kan vurderes.

**Why:** Jørgen vil hellere have den rigtige løsning end at bøje sig om footprint-budgettet (40 filer/650 linjer). Det spærrede for fx at flytte menupunkter og tegne afhængigheder i Planes Gantt.

**How to apply:** Ret gerne i upstream-filer, når det giver den bedste løsning. Behold `PLANE-DK(tag): why`-markøren på linjen over hver ændring, hæv `tools/upstream-footprint.json` efter behov med begrundelse i commit-beskeden, og hold `python tools/upstream-footprint.py --markers` rent. Logik i upstream-filer er nu ok, men læg stadig større logik i forkens egne filer, når det er lige så godt. Se [[project-plane-dk]] og [[feedback-plane-dk-uafhaengig-af-import]].
