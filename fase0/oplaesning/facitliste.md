# Facitliste til oplæsningstesten

Teksten er skrevet med et facit indbygget. Den tester **to ting hver for sig**, og de skal bedømmes hver for sig:

1. **Transskriptionen** — hørte Whisper de rigtige ord? Måles automatisk med `scripts\maal-noejagtighed.ps1`.
2. **Referatet** — fandt Claude det rigtige indhold? Måles ved at holde referatet op mod listerne nedenfor.

Et perfekt referat af en dårlig transskription er ikke muligt. Men en perfekt transskription giver ikke automatisk et godt referat — derfor de to niveauer.

---

## Niveau 1 — kritiske ord i transskriptionen

### Fagtermer der typisk går galt på dansk

| Skal stå der | Typiske fejlgengivelser |
|---|---|
| Entra ID | "entre id", "Entra i D", "Ente ID" |
| SCIM | "skim", "skimme", "S-K-I-M" |
| Microsoft Graph | "Microsoft graf" |
| Exchange Online | "Exchange on line" |
| MitID Erhverv | "mit id erhverv", "MitID hverv" |
| Kernesys | "kerne sys", "Kerne Cys" |
| provisionering / deprovisionering | "provisionering" gengives ofte som "provision" |
| attestering | "attestation", "at testering" |
| offboardet | "off boardet", "offboarded" |
| rate limiting | "rate limitning" |
| NIS2 | "nis to", "NIS 2", "niste" |
| CPR-nummer | "C P R nummer" |
| CVR-dimension | "C V R dimension" |
| sikkerhedsgrupper | — |
| modtagersystemer | — |
| JIT-adgang | "jit adgang", "J I T adgang" |
| PIM | "pim", "pin" |
| ISO 27001 | "ISO 27.001", "iso syvogtyve tusind" |
| UTC | "U T C", "utsi" |

Sammenlign `medOrdliste` mod `udenOrdliste`. **Forskellen på netop disse rækker er hele værdien af feature nummer 3.** Er den lille, er ordlisten ikke pengene værd. Er den stor, skal ordlisten udvides med kollegaers og kunders navne, før noget andet bygges.

### Tal og datoer

Disse er svære for al talegenkendelse, og de er dem, der gør et referat farligt hvis de tager fejl:

- 11. juni (den ellevte juni)
- 412 brugere, 388 automatisk, 24 manuelt
- under 6 procent fejlrate, mod 10 procent i business casen
- 7 forældreløse servicekonti
- 16 timers vindue → 15 minutter
- 120 timer, 950 kroner i timen, godt 114.000 kroner
- 4,2 megabyte, 12 sekunder, 96 kørsler i døgnet
- faktor 64
- 23. juli, svarprocent 97
- 14 dage
- 24 timer, 3 forlængelser
- 33 supportsager, heraf 24 samme sag, 9 rigtige fejl (4 + 2 + 3)
- 381 timer brugt mod et budget på 450
- 35 timer internt til kvartersløsningen
- 9.995 kroner i opstartsgebyr

### Negationer — den farligste fejlklasse

Hører Whisper "skal" hvor der står "skal ikke", bliver referatet ikke bare unøjagtigt, det bliver **modsat**. Tjek hver af disse:

| Sted | Sætningen skal indeholde |
|---|---|
| Blok 1 | "det er **ikke** acceptabelt" |
| Blok 1 | "heller **ikke** noget, vi kan forklare os ud af" |
| Blok 2 | "jeg vil **ikke** anbefale det" |
| Blok 2 | "jeg vil **ikke** love en dato" |
| Blok 3 | "vi skal **ikke** lave om på attesteringsintervallet" |
| Blok 3 | "adgangen **må ikke** bare fortsætte" |
| Blok 3 | "tavshed skal **ikke** være det samme som en godkendelse" |
| Blok 3 | "den skal **ikke** kunne gives ubegrænset" |
| Blok 3 | "den skal **ikke** kunne forlænges i det uendelige" |
| Blok 3 | "det er **ikke** en bekvemmelighed" |
| Blok 3 | "det er **ikke** rigtigt" (om at standardroller er låste) |
| Blok 3 | "den **ikke** nulstiller kundens ændringer" |
| Blok 4 | "involverer **ikke** leverandøren" |
| Blok 4 | "**ikke** af hensyn til pris" |
| Blok 4 | "så hun **ikke** går og regner med noget andet" |
| Blok 4 | "den skalerer **ikke**" |
| Blok 4 | "jeg vil **ikke** sige det højt til en kunde" |

**Er bare én af disse forsvundet, er feature nummer 5 — afspilning synkroniseret med transskript — ikke valgfri.** Uden mulighed for at klikke på en sætning og høre lyden kan du ikke verificere det, og så tør du ikke sende referatet videre.

---

## Niveau 2 — hvad referatet SKAL fange

Kopiér transskriptionen ind i Claude, bed om et referat med resumé, beslutninger, action points med ejer og åbne spørgsmål. Hold svaret op mod listerne her.

Det er ikke en midlertidig arbejdsgang, mens vi venter på en funktion i appen. Efter at Fase 3 er fjernet, **er** det arbejdsgangen: appen leverer eksporten, du flytter den selv. Derfor er det også den, der skal testes her.

### De 6 beslutninger

1. Kvartersløsning på ERP-filen; **leverandøren involveres ikke** i denne omgang. API-løsningen fravalgt af hensyn til **tid, ikke pris** — tages op igen til foråret.
2. Ubesvaret attestering efter 14 dage → **suspendering, ikke fjernelse**, med mulighed for at genåbne.
3. Nødadgang: loft på **24 timer, maks. 3 forlængelser**, skiftende godkender.
4. **MitID Erhverv udskydes** til efter årsskiftet; Malene får besked denne uge.
5. De **7 forældreløse servicekonti** ryddes op inden udgangen af august.
6. Sprogbrug rettes til **modtagersystemer**.

Nuancen i punkt 1 er den sværeste: skelnen mellem *tid* og *pris* som begrundelse. Et middelmådigt referat skriver bare "for dyrt". Tjek specifikt efter den.

### De 7 action points med ejer

De fem første læses tydeligt op som en liste i blok 5. **De to sidste nævnes i forbifarten i blok 4** — det er dér, referatet viser sin værdi eller falder igennem.

| Ejer | Opgave | Frist | Hvor |
|---|---|---|---|
| Anders | Estimat på kvartersløsningen til ERP-filen | fredag | oplistet |
| Malene | Skriver til kunden om MitID Erhverv | inden onsdag | oplistet |
| Thomas | Rydder de 7 servicekonti op og dokumenterer, hvor de kom fra | — | oplistet |
| Sofie | Opdaterer manualen med ny attesteringsregel, **dansk og engelsk i samme udgivelse** | — | oplistet |
| Mig selv | Får revisoren til at bekræfte, at suspendering opfylder kravet | — | oplistet |
| Rasmus | Skriver vejledning om whitelisting af afsenderadressen, til brug ved onboarding | — | **kun i forbifarten** |
| Camilla | Bygger fast kontrol der fejler bygget, hvis demodata-navne dukker op | — | **kun i forbifarten** |

**Ejerne er den vigtigste test.** Et referat der samler opgaverne uden at holde styr på hvem der har hvad, er ubrugeligt — og det er præcis dét, en transskription uden talernavne ikke kan hjælpe med. Her er det nemt, fordi navnene siges højt.

### De 7 åbne spørgsmål

Igen: fire læses op som en samlet liste, tre rejses undervejs uden at blive gentaget til sidst.

| # | Spørgsmål | Hvor |
|---|---|---|
| 1 | Eksterne konsulenter uden CPR-nummer — løsningen **skalerer ikke** ved flere hundrede | oplistet |
| 2 | Skal kvartersløsningen tilbydes andre kunder med samme ERP-integration? | oplistet |
| 3 | Skal det gamle importformat med semikolon-separerede filer fortsat understøttes? To kunder tilbage | oplistet |
| 4 | Er den nuværende logning tilstrækkelig til NIS2? | oplistet |
| 5 | Kan opstartsgebyret på 9.995 kr. indregnes i den løbende betaling? Skal vendes med bogholderiet | **kun i forbifarten** |
| 6 | Indsigtsudtræk og sletning på anmodning trækker i hver sin retning mod revisionssporet — ingen løsning endnu, skal afklares inden årsskiftet | **kun i forbifarten** |
| 7 | Hvor ofte bruges PIM-erstatningen egentlig? To gange om måneden eller halvtreds gange om ugen ændrer konklusionen | **kun i forbifarten** |

**Skelnen mellem "oplistet" og "i forbifarten" er den mest afslørende del af hele testen.** Et referat der kun fanger det, der blev sagt under overskriften "her er mine åbne spørgsmål", er en oplistning — ikke et referat. Værdien ligger i at fange nummer 5, 6 og 7.

### Ting referatet IKKE bør gøre

- Blande de tre grupper af manuelle sager sammen med de tre løsningsforslag på ERP-problemet. Det er to forskellige tredelinger, og en LLM med en dårlig transskription bytter gerne rundt på dem.
- Præsentere "administratorer skal kunne alt" som en beslutning. Det er en principafklaring, ikke noget der blev besluttet på mødet.
- Angive et beløb for kvartersløsningen. Der er ikke nævnt noget.

---

## Sådan scorer du det

| Måling | Godt | Acceptabelt | Ikke godt nok |
|---|---|---|---|
| Ordfejlrate (WER) | under 5 % | 5-12 % | over 12 % |
| Fagtermer ramt | 18-19 af 19 | 15-17 | under 15 |
| Negationer bevaret | **17 af 17** | 16 af 17 | 15 eller færre |
| Beslutninger i referatet | 6 af 6 | 5 af 6 | 4 eller færre |
| Action points med rigtig ejer | 7 af 7 | 6 af 7 | 5 eller færre |
| Åbne spørgsmål fanget | 7 af 7 | 6 af 7 | 5 eller færre |

De tre første rækker måles automatisk af `scripts\maal-noejagtighed.ps1`. De tre sidste kræver, at du læser referatet igennem — det er tyve minutters arbejde, og det er den eneste måde at afgøre, om transskriptionen er god nok til at bære et referat. Det er selve Fase 0-gaten: falder de tre rækker igennem, er det ikke referatet, der skal rettes, men kvaliteten af transskriptionen.

Negationsrækken har ingen "acceptabel" tolerance ved lavere tal med vilje. En mistet negation vender betydningen om, og et referat der siger det modsatte af hvad der blev sagt, er værre end intet referat.
