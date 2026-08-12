---
sprog: da
---

# Blandet prøvetekst — dansk møde med engelsk gæst

**Læs blok 1 og 3 på dansk, og blok 2 på engelsk.** Blokkens titel står på skærmen hele tiden, så du kan se, hvornår du skal skifte. Læs i ét stræk uden pause mellem sprogene — præcis som når en udenlandsk kollega kobler sig på undervejs.

Det er den vigtigste af de tre oplæsninger. Whisper detekterer sproget **én gang**, ud fra de første tredive sekunder, så teorien siger, at den engelske del bliver til volapyk. Det står allerede i spec'en — uden at være målt.

Beslutningen om mellemløsningen ligger med vilje i den engelske del. Så kan det ses direkte, om et sprogskifte koster en beslutning i referatet.

Facit står i `facitliste-blandet.md`.

---

## Blok 1 — dansk [0:00 - 1:50]

Godmorgen. Vi starter, selvom Priya først kommer om lidt — hun sidder i et andet møde, der trak ud.

Første punkt er migreringen. Vi er igennem første bølge. Fire hundrede og tolv konti blev flyttet i weekenden, og tre hundrede og otteogfirs af dem gik helt automatisk. Det efterlader fireogtyve, der krævede manuel behandling.

Alle fireogtyve fejlede af samme grund: afdelingsfeltet stod tomt i kildesystemet. Så løsningen er ikke teknisk. Den er, at nogen skal udfylde det felt inden anden bølge.

Anden bølge kører den fjortende, så alt inden den tolvte er fint. Og hvis der er mere end halvtreds tomme, skal jeg vide det med det samme, for så har vi et større problem end et tastejob.

Næste punkt er budgettet. Vi har brugt tre hundrede og en timer indtil nu, mod et budget på fire hundrede og halvtreds. Det ser fint ud på papiret.

Hagen er de hundrede og tyve timer til ERP-connectoren, som ikke ligger i budgettet, fordi vi regnede med at bygge den selv.

## Blok 2 — SKIFT TIL ENGELSK her [1:50 - 3:40]

Sorry I'm late, the other meeting ran over. What did I miss?

We were just getting to the budget. Give me the short version — where are we on the connector?

The short version is that we are either a hundred and twenty hours over budget, or we do not build the connector at all. There is no third option, and I would rather we said that out loud than pretended otherwise.

And if we skip it, what actually breaks?

Deprovisioning stays manual. The window between somebody leaving and their access being removed stays at sixteen hours. With the connector it would be under a minute. So it is a security trade, not a convenience one.

Could we flag the urgent cases separately? Not everybody leaving is a risk. The ones that matter are the terminations, and there are maybe three or four of those a month.

That would work. If we mark the urgent rows in the nightly file and handle those first, we get to fifteen minutes instead of sixteen hours for the cases that actually matter. It costs about eight hours of work instead of a hundred and twenty.

Then that is what we should do. It is not perfect, but it is a factor of sixty-four better, and we can live with that.

## Blok 3 — TILBAGE TIL DANSK [3:40 - 5:00]

Godt, så gør vi det. Marcus, skriv begrundelsen ned, for der er nogen, der spørger om det her om et halvt år, og så kan ingen af os huske den.

Sidste punkt er nødadgang. Den kan servicedesken give fra den første, men den skal have et loft. Fireogtyve timer og maksimalt tre forlængelser, og derefter eskaleres det til en navngiven person.

Der er én ting, vi ikke har fået afklaret, og det er, hvad der sker, når servicedesken er lukket. Det er et rigtigt hul, og jeg har ikke et svar i dag. Vi tager den udenfor mødet.

Andet? Nej? Godt, tak.
