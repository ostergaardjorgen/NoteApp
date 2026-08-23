---
name: dansk-tegn-i-brugerflade
description: "Æ, Ø og Å skal altid staves korrekt i alt, brugeren ser — aldrig ae/oe/aa"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-13T09:26:33.146Z
---

Alt, en bruger kan se, skal skrives med **æ, ø og å**. Aldrig `ae`, `oe` eller `aa`.

Det gælder knaptekster, statuslinjer, dialoger, fejlbeskeder, filnavne og titler på optagelser og dokumenter — alt hvad der havner på en skærm eller i en filliste.

**Hvorfor:** Jørgen så «Oplaesning-blandet» i listen over optagelser. Det ser ud som en fejl i produktet. En app, der ikke kan stave på sit eget sprog, ser ufærdig ud, og det smitter af på tilliden til alt det andet, den påstår — for eksempel at den har hørt rigtigt efter.

**Sådan bruges det:** ASCII-omskrivning er kun tilladt de steder, hvor tegnene volder tekniske problemer, og de steder er få og kendte:

- **Git-commit-beskeder** — historikken skal kunne læses i ethvert værktøj.
- **Identifikatorer i kode** — variabelnavne, enum-værdier, nøgler i ordbøger.
- **PowerShell-output i konsollen**, hvor kodesiden ikke kan styres.

Er en streng både en nøgle og noget, brugeren ser, skal de skilles ad: nøglen i ASCII, visningsteksten med de rigtige tegn.

Se også [[noteapp-nul-tolerance-forkert-info]].
