# Måling: hvor langt et møde kan Mistral Medium tage?

*Målt 07-09-2026 mod `api.eu.mistral.ai` med `mistral-medium-latest`.*

Baggrunden er, at modellisten på EU-endepunktet oplyser
`max_context_length: 32768` for `mistral-medium-latest` — otte gange mindre
end for Small 4 og Large 3, der begge står med 262.144. Hvis det tal gjaldt,
ville et langt webinar ikke kunne blive til et referat, og appen skulle enten
afkorte udskriften eller skifte model.

**Det tal gælder ikke ved kaldet.** Der blev sendt 138.759 tokens igennem, og
svaret kom tilbage.

---

## Resultatet

| | Målt |
|---|---|
| Dansk tale: tegn pr. token | **3,17** |
| Største kald, der gik igennem | **138.759 tokens** (~440.000 tegn) |
| Katalogets `max_context_length` | 32.768 |
| Æder `max_tokens` af vinduet? | **Nej** — 29.745 ind + `max_tokens` 32.000 gik igennem |

Målingen kostede 0,31 € (~2,28 kr.) i input-tokens.

---

## Hvad det betyder for et rigtigt møde

Målt på de 20 udskrifter, der lå i datamappen:

| | Tegn | Tokens | Længde |
|---|---|---|---|
| Længste udskrift | 59.931 | ~18.900 | 51 min |
| Talehastighed, gennemsnit | 865 tegn/min | ~273 tokens/min | — |

Ved 273 tokens i minuttet:

- **32.768 tokens** ville svare til omkring **to timers** møde.
- **138.759 tokens** svarer til omkring **otte en halv times** møde.

Den længste udskrift i huset bruger **14 %** af det, der faktisk gik igennem.

**Der er derfor ingen grund til at afkorte udskriften eller skifte til Large
på lange møder.** Det var ellers den nærliggende konklusion at drage af
katalogets tal, og den ville have været forkert.

---

## Det, målingen IKKE siger

At kvaliteten holder ved 138.000 tokens. Der er målt, at kaldet **accepteres**
og svarer — ikke at modellen bruger hele materialet lige godt. En model kan
tage imod mere, end den kan holde styr på.

For HeyPia er det uden betydning i dag: et rigtigt møde ligger under 20.000
tokens, og dér er der målt kvalitet i forvejen — se `maaling-sky.md`. Skulle
det en dag blive aktuelt at sende noget, der er mange gange længere, er det en
ny måling, ikke en antagelse man kan bygge videre på herfra.

## Fyldteksten

Der blev ikke sendt noget af brugerens eget materiale. Fyldteksten er otte
danske replikker, skrevet til formålet og gentaget — samme sætningslængde og
samme ordforråd som en rigtig mødeudskrift, så forholdet mellem tegn og tokens
er sammenligneligt.

Skriptet ligger ikke i repoet: det er en engangsmåling mod et endepunkt, og
tallene her er resultatet. Skal den gentages, står fremgangsmåden ovenfor.
