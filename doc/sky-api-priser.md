# Priser på sky-API'er og EU-alternativer

*Slået op 16-08-2026. Priserne ændrer sig — tallene skal kontrolleres, før de
bruges til noget bindende.*

Baggrunden er den målte begrænsning i den lokale 8B-model. Se `roadmap.md` og
`maaling-sky.md` for, hvad de europæiske modeller så gjorde ved den.

---

## Konklusionen først

**Prisen er ikke det, der afgør sagen.** Et mødereferat af et 61-minutters møde
koster mellem 3 øre og 1,50 kr., alt efter model. Tyve møder om måneden på den
dyreste fornuftige model er under 20 kr.

Det, der afgør sagen, er, hvad der forlader maskinen — og det er ikke et
prisspørgsmål.

---

## Regnestykket

Et 61-minutters møde er målt til **12.513 tokens** udskrift med Qwens
tokenizer. Referatet fylder **1.400–2.000 tokens**.

To forbehold:

- Claude Opus 4.7 og nyere bruger en anden tokenizer, der giver **ca. 30 % flere
  tokens** for den samme tekst. Dansk tokeniserer i forvejen dårligere end
  engelsk.
- Regnestykket nedenfor bruger **13.000 ind / 2.000 ud** som grundlag. Med den
  nye tokenizer bliver inputdelen snarere 16.000, altså ca. 20 % dyrere.

Priserne er per million tokens (MTok). Kolonnen «pr. referat» er
13.000 × input + 2.000 × output.

### Claude (Anthropic)

| Model | Ind $/MTok | Ud $/MTok | Pr. referat | Ca. kr. |
|---|---|---|---|---|
| Claude Fable 5 | 10,00 | 50,00 | $0,23 | 1,50 |
| Claude Opus 5 | 5,00 | 25,00 | $0,115 | 0,75 |
| Claude Sonnet 5 | 2,00 | 10,00 | $0,046 | 0,30 |
| Claude Haiku 4.5 | 1,00 | 5,00 | $0,023 | 0,15 |

### ChatGPT (OpenAI)

| Model | Ind $/MTok | Ud $/MTok | Pr. referat | Ca. kr. |
|---|---|---|---|---|
| GPT-5.6 Sol | 5,00 | 30,00 | $0,125 | 0,81 |
| GPT-5.6 Terra | 2,00 | 12,00 | $0,050 | 0,33 |
| GPT-5.6 Luna | 0,20 | 1,20 | $0,005 | 0,03 |
| GPT-4o | 2,50 | 10,00 | $0,053 | 0,34 |
| GPT-4.1 | 2,00 | 8,00 | $0,042 | 0,27 |

Terra- og Luna-priserne stammer fra opsummerende kilder, ikke fra OpenAIs egen
prisside, som kun bekræftede Sol, 4o, 4.1, 4o-mini og 5-nano. De skal
kontrolleres.

Kurs regnet med ca. 6,5 kr./USD. Det er et rundt tal, ikke en dagskurs.

### Hvad de to har til fælles

| | Claude | ChatGPT |
|---|---|---|
| Batch-API (asynkront) | −50 % | −50 % |
| Cache-læsning | 10 % af inputpris | ca. 10 % af inputpris |
| Cache-skrivning | 125 % (5 min) / 200 % (1 time) | — |

**Ingen af rabatterne hjælper her.** Batch er til store mængder, og cache virker
kun, når den samme tekst sendes igen og igen. Et mødereferat er én tekst, én
gang.

### Prisen er den samme størrelsesorden

Claude Opus 5 og GPT-5.6 Sol ligger inden for 10 % af hinanden i den ende, der
er interessant for et referat. Sonnet 5 og Terra ligger inden for 10 % af
hinanden i mellemklassen. Der er intet at spare ved at vælge den ene frem for
den anden — valget skal træffes på kvalitet og på, hvad man i forvejen har et
abonnement til.

---

## Findes der EU-alternativer?

Ja, men spørgsmålet dækker over to forskellige ting, og de skal skilles ad.

### 1. Europæiske leverandører (europæisk firma, europæisk hosting)

| Leverandør | Land | Bemærkning |
|---|---|---|
| **Mistral AI** | Frankrig | Den eneste med modeller i nærheden af Claude og GPT. Egen API, egne modeller, EU-hosting, databehandleraftale. |
| **Aleph Alpha** | Tyskland | Rettet mod myndigheder og regulerede brancher. On-premise, revisionsspor, dataisolering. Dyrere. |
| **OVHcloud, Scaleway** | Frankrig | Kører åbne modeller (Mistral, Llama, Qwen) på europæisk infrastruktur. |
| **IONOS, STACKIT** | Tyskland | Samme model: åbne vægte på tysk infrastruktur, OpenAI-kompatibelt API. |

Mistrals priser:

| Model | Ind $/MTok | Ud $/MTok | Pr. referat |
|---|---|---|---|
| Mistral Medium 3.5 | 1,50 | 7,50 | $0,035 |
| Mistral Large 3 | 0,50 | 1,50 | $0,010 |
| Mistral Small 4 | 0,15 | 0,60 | $0,003 |

Mistral er **billigere end begge amerikanske** og har batch (−50 %) og cache
(op til −90 % på input). Om kvaliteten på dansk holder, er ikke målt — og det
skal måles, ikke antages.

### 2. Amerikanske modeller kørt på europæisk infrastruktur

Det er ikke det samme. Firmaet er stadig amerikansk; kun stedet, hvor
beregningen sker, flytter.

- **Claude:** Anthropics eget API har kun `inference_geo` = `"us"` eller
  `"global"`. **Der er ingen EU-mulighed på det direkte API**, og
  arbejdsområdets datalagring kan kun sættes til `"us"`. Vil man have EU, skal
  det gå gennem **AWS Bedrock i en EU-region** eller **Google Cloud i en
  EU-region** — begge med 10 % tillæg for regionsbundne endpoints.
- **ChatGPT:** OpenAI har `eu.api.openai.com` for EØS og Schweiz, både lagring
  og behandling i regionen. Men det kræver **godkendelse**, og EU-regionen
  kræver at nul-datalagring eller ændret misbrugsovervågning er slået til. Det
  er ikke noget, man bare slår til i en konto.

---

## Hvad det betyder for NoteApp

Prisen betyder ingenting. Tre forhold betyder noget:

1. **Løftet.** Appen siger «Intet forlader denne pc». Hverken Mistral,
   AWS-i-Frankfurt eller `eu.api.openai.com` ændrer på, at udskriften forlader
   maskinen. EU-hosting er et **juridisk** svar på et **compliance**-spørgsmål —
   ikke et teknisk svar på løftet.

2. **Samtykket.** Det er ikke kun brugerens egne ord, der sendes af sted. Det er
   mødedeltagernes, og de har ikke sagt ja. Det gælder uanset hvilken
   verdensdel serveren står i.

3. **Adgangen.** EU-vejen hos begge amerikanske leverandører kræver aftaler og
   godkendelse — ikke en API-nøgle, brugeren selv taster ind. Mistral er det
   eneste alternativ, hvor en almindelig bruger kan komme i gang med sin egen
   nøgle og stadig have EU-hosting.

## Beslutningen, truffet 16-08-2026

> **Mødeoptagelse og transskription 100 % lokalt.
> Bearbejdning af indhold og dokumentskabeloner 100 % europæisk.**

Den formulering afgør leverandørvalget, og den skærer de amerikanske væk — ikke
på pris, men fordi anden halvdel af sætningen ellers ikke holder. Claude og
ChatGPT kan ikke bruges til bearbejdningen, uanset at Claude er det abonnement,
der er i huset.

**Mistral er derfor den, der bygges imod**, som den eneste af de undersøgte,
hvor «europæisk» og «brugerens egen nøgle» kan være sandt samtidig.

**Claude skifter rolle: fra kandidat til målestok.** Facit i `reference.txt` er
lavet af Claude, og hver europæisk model måles op mod det. Det er en bedre brug
af den end at sende kundedata til den.

**Det, der mangler, før budskabet må bruges:** skriftlig bekræftelse fra Mistral
på hosting i EU og en databehandleraftale. «100 % europæisk» er en
compliance-påstand, ikke en vending — den skal kunne dokumenteres, før den står
i appen.

Kravene fra roadmap'en står uændret: lokalt som forvalg, sky som bevidst tilvalg
pr. dokument, klar besked om hvad der sendes hvorhen, og nøglen i brugerens eget
tastatur.

---

## Kilder

- Anthropic: prisside og dokumentation på platform.claude.com (hentet 16-08-2026)
- OpenAI: developers.openai.com/api/docs/pricing og .../guides/your-data
- Mistral: mistral.ai/pricing/api
- Øvrige EU-leverandører: opslag, ikke bekræftet hos leverandøren selv
