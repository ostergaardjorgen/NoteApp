# Produkt — hvem det er til, og hvad det skal kunne

*Skrevet 21-08-2026 efter en afklaring af retningen. Denne fil er interne
noter, ikke tekst til brugerfladen.*

Filen findes, fordi de fleste beslutninger i appen er lette at træffe, når man
ved, hvem der sidder i den anden ende — og umulige, når man ikke gør. Er der
tvivl om en funktion, er det her, den skal afgøres.

---

## Målgruppen

**Studerende, iværksættere og mindre selvstændige.**

Fælles for dem: de har ingen it-afdeling, intet indkøbsbudget og ingen
compliance-funktion. De har til gengæld mange løsrevne online-aktiviteter —
møder, webinarer, samtaler optaget på telefonen — og ingen samlet plads at
lægge dem.

Det følger heraf:

- **Ingen konto er en forudsætning.** Skal man logge ind eller oprette noget
  for at komme i gang, er brugeren væk. Alt bærende skal virke uden.
- **Prisen skal kunne være nul.** Den lokale vej — udskrift, søgning, kort
  opsummering — koster ikke andet end strøm. Skyen er tilvalget, ikke motoren.
- **Der er ingen til at rydde op bagefter.** Struktur skal opstå af sig selv,
  mens man arbejder, ikke som et oprydningsarbejde man udskyder.

## Hvad appen i virkeligheden er

**Ét fælles fundament under alle online-aktiviteter.**

Ikke en mødeoptager. En mødeoptager laver et referat og er færdig. Det her skal
kunne svare på spørgsmål **på tværs af historikken** — og altid kunne føre
tilbage til den oprindelige optagelse, så man kan se udsagnet i sin sammenhæng
og sammen med de øvrige optagelser om samme emne.

De tre led, i den rækkefølge:

1. **Alt kommer ind.** Møder på pc'en, webinarer man ikke selv deltager i,
   lydfiler optaget på en helt almindelig telefon og lagt ind via upload.
2. **Alt bliver søgbart det samme sted.** Én søgning på tværs af det hele.
3. **Alt kan føres tilbage.** Et søgeresultat er ikke et svar, det er en
   henvisning — til minuttet i optagelsen, til de andre gange emnet blev nævnt.

Det er led 3, der adskiller produktet fra et referatværktøj. Et referat er en
fortolkning; kilden er beviset.

## Hvem vi måler os op mod

**Granola.** Det er sammenligningen, der skal bruges — ikke Otter, ikke Teams'
egne referater.

Hvorfor det er den rigtige: Granola har ramt tonen (den er ikke i vejen under
mødet) og har vist, at et redigerbart, personligt notat slår et maskinreferat.
Det er den standard, brugeren kender og forventer.

Hvor der skal vindes, er de tre steder, Granola ikke er:

| | Granola | Her |
|---|---|---|
| Hvor lyden ligger | i skyen | på maskinen |
| Hvad der er dækket | møder | møder, webinarer og uploadede lydfiler |
| Hvad der kan søges | et møde ad gangen | på tværs, tilbage til kilden |

Nyt, der bygges, skal kunne besvare: **gør det os bedre end Granola på ét af de
tre punkter — eller lukker det et hul, hvor Granola er bedre?** Er svaret
ingen af delene, er punktet ikke vigtigt nu.

## Databehandling — den lokale vej er hovedvejen

Målgruppen går op i, hvor deres materiale ender. Det er ikke en juridisk
øvelse for dem; det er tillid.

1. **Kan noget gøres lokalt, gøres det lokalt.** Lyden forlader aldrig
   maskinen. Udskrift, søgning, talergenkendelse, opgavefund og den korte
   opsummering kører alle på maskinen.
2. **Skal en sky-model bruges, skal det være en EU-løsning.** Det er derfor
   Mistral er valgt til dokumenterne. En amerikansk leverandør er ikke et
   spørgsmål om pris eller kvalitet — den falder på placeringen.
3. **Skyen er altid et valg, brugeren træffer i situationen.** Aldrig en
   automatik, aldrig en standardindstilling.

Rækkefølgen i brugerfladen skal afspejle det: den lokale mulighed står først og
er standardvalget, hver gang begge dele findes.
