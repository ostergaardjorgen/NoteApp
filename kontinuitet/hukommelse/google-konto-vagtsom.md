---
name: google-konto-vagtsom
description: Google-kontoen og kalenderen er jorgen@vagtsom.com; jorgen@ic67.dk bruges kun til Claude
metadata: 
  node_type: memory
  type: user
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-24T05:13:14.689Z
---

**Google-kontoen er `jorgen@vagtsom.com`.** Det er login til Google, og det er
den kalender, NoteApp skal læse. Det er også den konto, der ejer Google Cloud-
projektet bag appens klient-id — og dermed den, Google henvender sig til ved
verifikation eller spærring.

`jorgen@ic67.dk` er **kun** login til Claude. Der er ingen Google-konto bag
den.

**Why:** De to adresser ligner hinanden nok til at blive blandet sammen, og
konsekvensen er ikke synlig med det samme: står den forkerte adresse som
testbruger på Googles samtykkeskærm, afviser Google login'et med
`access_denied`, og fejlteksten nævner ikke testbrugere med et ord.

**How to apply:** Alt, der har med Google at gøre — Cloud Console, testbrugere,
samtykkeskærmens support-mail, den konto der trykker Forbind — er
`jorgen@vagtsom.com`. Brug kun `ic67.dk` til at identificere brugeren i Claude
selv.

Se også [[lokalt-foerst]] om hvorfor kalenderintegrationen alligevel er i orden,
selvom den henter fra en amerikansk leverandør.
