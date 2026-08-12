---
navn: Blandet prøvetekst — dansk møde med engelsk gæst
sprog: da+en
læsetid: ca. 5 minutter
formål: måle hvad der sker, når mødet skifter sprog undervejs
---

# Blandet prøvetekst

**Dette er den vigtigste af de tre oplæsninger**, fordi det er den, der måler en begrænsning, jeg har skrevet ned uden at have målt den.

Whisper detekterer sproget **én gang**, ud fra de første tredive sekunder. Skifter mødet sprog undervejs, opdager den det ikke. Teorien siger, at den engelske del bliver skrevet ned som dansk volapyk. Om det holder — og hvor slemt det er — kan kun måles.

**Sådan læses den:** Læs de danske afsnit på dansk og de engelske på engelsk, i ét stræk uden pauser mellem sprogene. Præcis som et rigtigt møde, hvor en udenlandsk kollega kobler sig på undervejs. Skiftene er markeret, men læs ikke markeringerne højt.

---

**[DANSK]**

Godmorgen. Vi starter, selvom Priya først kommer om lidt — hun sidder i et andet møde, der trak ud.

Første punkt er migreringen. Vi er igennem første bølge. Fire hundrede og tolv konti blev flyttet i weekenden, og tre hundrede og otteogfirs af dem gik helt automatisk. Det efterlader fireogtyve, der krævede manuel behandling.

Alle fireogtyve fejlede af samme grund: afdelingsfeltet stod tomt i kildesystemet. Så løsningen er ikke teknisk, den er, at nogen skal udfylde det felt inden anden bølge.

Anden bølge kører den fjortende, så alt inden den tolvte er fint. Og hvis der er mere end halvtreds tomme, skal jeg vide det med det samme, for så har vi et større problem end en tastejob.

Næste punkt er budgettet. Vi har brugt tre hundrede og en timer indtil nu mod et budget på fire hundrede og halvtreds. Det ser fint ud på papiret. Hagen er de hundrede og tyve timer til ERP-connectoren, som ikke ligger i budgettet, fordi vi regnede med at bygge den selv.

**[ENGELSK — Priya kobler sig på her]**

Sorry I'm late, the other meeting ran over. What did I miss?

We were just getting to the budget. Give me the short version — where are we on the connector?

The short version is that we are either a hundred and twenty hours over budget, or we do not build the connector at all. There is no third option, and I would rather we said that out loud than pretended otherwise.

And if we skip it, what actually breaks?

Deprovisioning stays manual. The window between somebody leaving and their access being removed stays at sixteen hours. With the connector it would be under a minute. So it is a security trade, not a convenience one.

Could we flag the urgent cases separately? Not everybody leaving is a risk. The ones that matter are the terminations, and there are maybe three or four of those a month.

That would work. If we mark the urgent rows in the nightly file and handle those first, we get to fifteen minutes instead of sixteen hours for the cases that actually matter. It costs about eight hours of work instead of a hundred and twenty.

Then that is what we should do. It is not perfect, but it is a factor of sixty-four better, and we can live with that.

**[DANSK igen]**

Godt, så gør vi det. Marcus, skriv begrundelsen ned, for der er nogen, der spørger om det her om et halvt år, og så kan ingen af os huske den.

Sidste punkt er nødadgang. Den kan servicedesken give fra den første, men den skal have et loft. Fireogtyve timer og maksimalt tre forlængelser, og derefter eskaleres det til en navngiven person.

Der er én ting, vi ikke har fået afklaret, og det er, hvad der sker, når servicedesken er lukket. Det er et rigtigt hul, og jeg har ikke et svar i dag. Vi tager den udenfor mødet.

Andet? Nej? Godt, tak.

---

## Facitliste — kig først her efter oplæsningen

**Det, målingen skal svare på:**

1. Hvilket sprog blev detekteret? Teorien siger `da`, fordi de første tredive sekunder er dansk.
2. Hvor ulæselig blev den engelske del? Læs den midterste del af transskriptionen igennem. Er den ordret rigtig, er min dokumenterede begrænsning forkert, og det skal rettes.
3. Fik referatet indholdet med fra **begge** dele? Beslutningen om mellemløsningen bliver truffet i den engelske del. Mangler den i referatet, kostede sprogskiftet en beslutning.

**Tal, der skal stå rigtigt:** 412, 388, 24, 50, 301, 450, 120, 16 timer, 15 minutter, 8 timer, 64, 24 timer, 3 forlængelser. Det tolvte og det fjortende som datoer.

**Beslutningen truffet på engelsk:** markering af hastesager i natfilen frem for at bygge connectoren. Denne ene er den vigtigste at tjekke — den ligger midt i det sprogskifte, Whisper ikke opdager.

**Opgave med ejer:** Marcus — skrive begrundelsen ned.

**Åbent spørgsmål:** hvad der sker, når servicedesken er lukket.
