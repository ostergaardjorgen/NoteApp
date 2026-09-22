---
name: feedback-plane-dk-uafhaengig-af-import
description: Plane DK - alt skal virke paa et projekt oprettet i haanden; importen maa aldrig vaere forudsaetningen for en funktion
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 10c65f2f-a8ae-4b95-82aa-392a350eade9
  modified: 2026-09-18T07:15:08.092Z
---

Alt, hvad der bygges i Plane DK, skal ligge som standard i løsningen og virke på
et projekt, der er oprettet i hånden. Importen af et regneark må aldrig være
forudsætningen for, at en funktion er der. Sagt 18. september 2026: "Det vil
være meget klassisk man bare går igang med at oprette et projekt manuelt."

**Why:** De fleste projekter startes i hånden. Indtil da satte kun importen
sortering efter startdato, rækkefølgen af spor og faser, og slog cykler, moduler
og visninger til — så et manuelt projekt så anderledes og dårligere ud.

**How to apply:** Standarder sættes, når rækken oprettes (pre_save i forkens
`plane.dk`-app), ikke i importens writer. Importen må gerne gøre *mere*, men
aldrig være eneste vej til en indstilling eller en skærm. Test altid en ny
funktion på et projekt uden import. Stadig kun ved import (18-09-2026):
visningerne Vandfald og Agilt, og etiketten `blokeret` — afhængighedsskærmen er
den levende erstatning for etiketten.

Se [[project-plane-dk]] og [[project-plane-dk-excelimport]].
