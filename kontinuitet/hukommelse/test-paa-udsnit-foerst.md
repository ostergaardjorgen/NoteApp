---
name: test-paa-udsnit-foerst
description: "Nye ting proeves altid af paa et kort udsnit af lydfilen, aldrig paa hele optagelsen foerst"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-20T08:12:52.655Z
---

Skal noget nyt afproeves paa en stor lydfil — en model, et flag, en indstilling —
klippes der **altid** et kort udsnit ud foerst, og der maales paa det. Hele filen
koeres foerst, naar indstillingen er fundet.

**Hvorfor:** en fuld optagelse tager titusinder af sekunder at behandle. At vente
16 minutter paa at opdage, at et flag hedder noget andet, eller at en klyngning
kollapser, er spildtid — og svaret var det samme efter 30 sekunder paa et udsnit.
Det skete konkret under maalingen af talergenkendelse: to fulde koersler gik tabt
paa noget, et fireminutters udsnit afgjorde paa under et minut.

**Saadan gribes det an:** vaelg et udsnit, hvor facit er kendt (fx et sted hvor to
bestemte personer taler efter hinanden), klip det ud, og gennemloeb indstillingerne
der. Er en fuld koersel alligevel noedvendig, saettes den i baggrunden, saa den
ikke spaerrer for andet arbejde.

Se ogsaa [[meld-altid-release-nummer]].
