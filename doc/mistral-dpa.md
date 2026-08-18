# Holder EU-budskabet? Gennemgang af Mistrals vilkår

*Læst 17-08-2026 på legal.mistral.ai, trust.mistral.ai og docs.mistral.ai.*

Budskabet, der skal bæres:

> Mødeoptagelse og transskription 100 % lokalt.
> Bearbejdning af indhold og dokumentskabeloner 100 % europæisk.

Første halvdel er vores egen kode og holder pr. konstruktion. Denne note handler
om anden halvdel.

---

## Det, der afgjorde sagen

**Vi kaldte det forkerte endepunkt.** Mistral har tre:

| Endepunkt | Geografi | Pris |
|---|---|---|
| `api.mistral.ai` | **ingen forpligtelse** | standard |
| `api.eu.mistral.ai` | Europa | +10 % |
| `api.us.mistral.ai` | USA | +10 % |

Om det globale skriver Mistral ordret, at de **ikke forpligter sig på en bestemt
geografi**. Det er det, der står i alle kodeeksempler, og det var derfor det, vi
brugte indtil 17-08-2026.

Alt virkede. Målingerne var gode. Bearbejdningen kunne være foregået hvor som
helst — og en påstand om europæisk bearbejdning ville have været usand, uden at
noget så forkert ud.

**Rettet.** `SkyKatalog.Endpoint` peger nu på `api.eu.mistral.ai`, og
EU-tillægget er ganget ind i prisberegningen. Begge modeller er bekræftet
tilgængelige på EU-endepunktet.

---

## Det, der holder

| | |
|---|---|
| **Leverandør** | Mistral AI, fransk selskab med adresse i Paris. Underlagt GDPR direkte, ikke via en aftalekonstruktion. |
| **Databehandleraftale** | Findes, og den gælder **automatisk** — «incorporated to the Agreement by reference». Der skal ikke kontaktes salg, og den skal ikke underskrives særskilt. |
| **Certificeringer** | ISO/IEC 27001, ISO/IEC 27701, SOC 2 Type II. Attester ligger i Trust Center. |
| **Bearbejdningens geografi** | Bundet til Europa, når EU-endepunktet bruges. |

---

## Det, der kræver præcision i formuleringen

### Underdatabehandlerne — hele listen

Der er **25**. Fra Trust Center, 17-08-2026. Her er de, der kan røre et
mødereferat sendt til `api.eu.mistral.ai`:

| Underdatabehandler | Rolle | Placering | Ejerskab |
|---|---|---|---|
| Mistral Compute | Infrastruktur | Frankrig | fransk |
| Microsoft Inc. | Infrastruktur (Azure) | Sverige, **Norge** | **amerikansk** |
| CoreWeave Inc. | Inferens | EØS | **amerikansk** |
| Cloudflare Inc. | Trafikstyring (CDN) | **Verdensomspændende** | **amerikansk** |
| Kong Inc. | API-sikkerhed | EØS | **amerikansk** |
| Sentry | Fejlhåndtering | EØS | **amerikansk** |
| Wiz, CrowdStrike | Sikkerhed | EØS (CrowdStrike også USA) | **amerikanske** |
| Ory Corp. | Godkendelse af brugere | Belgien, Tyskland | tysk |

Og på kontoen, uden om selve referatet: Stripe (betaling, **USA**), Twilio
(telefonbekræftelse, **USA**), Lago og Intercom og Resend (Irland).

**Google LLC** står med Holland, Belgien og «USA (US-endepunktet)», og
produktkolonnen siger «Studio/API (US API)». Deres USA-rolle er altså knyttet
til det amerikanske endepunkt, som vi ikke bruger.

Ikke i vores vej: Brave (websøgning), Blackforest Labs (billeder), E2B
(kodefortolker), Merge (Vibe-connectors), OVH/Vultr/Megaport/Backblaze (Vibe
Code Web).

### Hvad det betyder — og hvad det ikke betyder

**Er problemet kun det globale endepunkt? Nej.**

Det globale endepunkt afgør, hvor bearbejdningen GEOGRAFISK sker, og det er en
reel og vigtig forskel. Men amerikansk EJEDE virksomheder er i kæden også på
EU-endepunktet: Microsoft driver infrastrukturen i Sverige, CoreWeave leverer
inferens i EØS, Cloudflare styrer trafikken.

Det er ikke særligt for Mistral — det er sådan set hele den europæiske
cloud-virkelighed. Men det betyder, at der er tre forskellige påstande, og kun
de to første holder:

| Påstand | Holder? |
|---|---|
| «Bearbejdningen sker i Europa» | **Ja** — med EU-endepunktet |
| «Leverandøren er europæisk» | **Ja** — Mistral er fransk |
| «Ingen amerikansk virksomhed er involveret» | **Nej** |

**Norge er desuden EØS, ikke EU.** «Europæisk» er rigtigt; «EU» ville ikke være
det.

### Hvis den tredje påstand skal holde

Så er Mistral ikke vejen — og det er ikke sikkert, nogen vej er det. Det ville
kræve en leverandør, der kører åbne modeller på egen europæisk-ejet
infrastruktur: Scaleway (fransk), IONOS eller STACKIT (tyske), OVHcloud
(fransk). De kører åbne vægte frem for egne frontier-modeller, så det ville
koste kvalitet — hvor meget er ikke målt.

Det er en beslutning om, hvor stramt «ægte EU» skal forstås, ikke en teknisk
detalje.

### Privatlivspolitikkens forbehold

Politikken siger, at Mistral «prioriterer leverandører i EU», men at de «i
undtagelsestilfælde kan vælge leverandører uden for EU». DPA'en tillader
overførsler til lande med tilstrækkelighedsafgørelse og under SCC'er.

Det er standardformuleringer, og de er ikke i sig selv alarmerende. Men de
betyder, at **«100 %» er en absolut påstand, som dokumenterne ikke bakker
absolut op om.** Det er værd at vide, før ordet står i en salgspræsentation.

### Træning på data

DPA'en siger, at Mistral kan bruge persondata som **dataansvarlig** til at
træne deres modeller — **medmindre kunden har frameldt sig**. Der findes en
kontrol i kontoen til det.

Du har oplyst, at du har efterprøvet dette på din pay-as-you-go-konto, og du
står på mål for det. Det tager jeg for gode varer.

Én ting at være opmærksom på alligevel: **det er en indstilling, ikke en
egenskab ved planen.** Den kan skifte ved en kontoændring, et nyt workspace
eller en ny nøgle, uden at nogen får besked. Skal påstanden bruges i salg, er
den værd at dokumentere med et skærmbillede og efterse med jævne mellemrum.

Bemærk desuden, at DPA'en særskilt nævner, at **feedback-knapper** (tommel
op/ned) medfører, at både input og output bruges til træning. Det er ikke
noget, appen bruger, men det er værd at kende, hvis nogen senere åbner Le Chat
med de samme data.

---

## Anbefalet formulering

Den stærkeste påstand, der kan dokumenteres:

> **Optagelse og transskription forlader aldrig din pc.
> Bearbejdningen sker i Europa — hos Mistral AI i Frankrig, på deres
> EU-endepunkt, under databehandleraftale og GDPR.**

Den siger det samme som det oprindelige budskab, men hver del kan efterprøves.
«100 % europæisk» er ikke forkert, men det er et absolut ord om noget, hvor
leverandørens egne dokumenter tager forbehold — og en påstand, der ikke kan
holdes hele vejen, er dyrere end en, der siger lidt mindre.

---

## Det, der stadig ikke er efterprøvet

- **Underdatabehandlerlisten kan ændre sig.** Mistral varsler ændringer med ti
  dages indsigelsesfrist. Der kan abonneres på varsler i Trust Center — det bør
  nogen gøre.
- **Opbevaringstiden** for API-anmodninger er ikke slået op her.
- Trust Center havde 14-05-2026 en sikkerhedshændelse (forsyningskædeangreb).
  Mistral oplyser, at hverken hostede tjenester eller kundedata blev berørt.
  Nævnt for fuldstændighedens skyld, ikke som en indvending.

---

## RETTELSE 18-08-2026 — de bindende dokumenter er læst

*Læst direkte: `legal.mistral.ai/terms/data-processing-addendum`,
`legal.mistral.ai/terms/commercial-terms-of-service` og
`legal.mistral.ai/terms/eu-consumers-terms-of-service`.*

Gennemgangen 17-08 byggede på Trust Center og produktdokumentationen. De
bindende dokumenter er nu læst, og de ændrer tre ting.

### 1. Den geografiske binding står ikke i aftalerne

Hverken databehandleraftalen eller de kommercielle vilkår nævner regionale
endepunkter eller forpligter behandlingen til et bestemt område.
Databehandleraftalen tillader tværtimod udtrykkeligt overførsel til lande med
et af Kommissionen anerkendt beskyttelsesniveau samt internationale
overførsler på standardkontraktbestemmelser.

Det, der findes om `api.eu.mistral.ai`, er en **beskrivelse af tjenesten** i
produktdokumentationen — ikke en kontraktbestemmelse.

**Konsekvens.** Formuleringen «bearbejdningen sker i Europa … under
databehandleraftale» læses let, som om aftalen er dét, der binder geografien.
Det gør den ikke. Skal påstanden bruges i et tilbud eller en fortegnelse over
behandlingsaktiviteter, bør den bekræftes skriftligt af leverandøren først.

### 2. Aftalen skelner ikke mellem endepunkter

Spørgsmålet var, om de amerikansk ejede underdatabehandlere er opført som
gældende for EU-endepunktet. Svaret er, at aftalen slet ikke opdeler dem:
den henviser samlet til listen i Trust Center
(`trust.mistral.ai/subprocessors`) og opregner hverken underdatabehandlere
eller datalokationer pr. endepunkt.

Man kan altså **ikke** af aftalen udlede, at et navn på listen er ude af
billedet, fordi EU-endepunktet bruges. Afgrænsningen i tabellen ovenfor er en
fortolkning fra 17-08 og skal læses som sådan.

Selve listen kunne ikke hentes udefra — Trust Center kræver JavaScript.

### 3. Forbrugervilkårene gælder ikke

`eu-consumers-terms-of-service` undtager udtrykkeligt API-adgang: de dækker
personlig brug af Vibe og øvrige tjenester, «but excluding Mistral AI Studio
and access to any of our APIs». Det, der gælder her, er de **kommercielle
vilkår** sammen med databehandleraftalen.

### Det, der blev bekræftet

| | |
|---|---|
| Træning | «unless Customer is or has opted-out of training». Fravalget er brugerens eget, som antaget. Feedback-funktioner giver derudover samtykke til træning på input og output. |
| Opbevaring | Punkt 10.1: personoplysninger er utilgængelige senest tredive dage efter aftalens ophør. Der står fortsat ingen opbevaringstid for den enkelte API-anmodning. |
| Labs og Preview | Egne vilkår: her MÅ data bruges til træning. Appen kalder ikke sådanne modeller. |

---

## Underdatabehandlerne, gennemgået navn for navn — 18-08-2026

*Listen fra `trust.mistral.ai/subprocessors`. Den er offentlig; grunden til at
den ikke kunne hentes automatisk er, at siden bygges med JavaScript.*

Listen opdeler efter **produkt** — Vibe, Studio/API, Vibe Code Web — ikke efter
endepunkt. Kun Google har en endepunktsangivelse. Produktopdelingen kan bruges:
appen kalder alene API'et, og det tager ni navne ud.

### I behandlingskæden for API'et

| Navn | Rolle | Placering | Ejerskab |
|---|---|---|---|
| Mistral Compute | Infrastruktur | Frankrig | fransk |
| Mistral AI Affiliates | — | — | fransk |
| **CoreWeave Inc.** | **Inferens** | EØS | **amerikansk** |
| Microsoft Inc. | Infrastruktur | Sverige, Norge | **amerikansk** |
| Ory Corp. | Brugergodkendelse | Belgien, Tyskland | tysk |
| Kong Inc. | API-sikkerhed | EØS | **amerikansk** |
| Functional Software (Sentry) | Fejlhåndtering | EØS | **amerikansk** |
| Wiz Inc. | Sikkerhed | EØS | **amerikansk** |
| **CrowdStrike Inc.** | Sikkerhed | **EØS, United States** | **amerikansk** |
| **Cloudflare Inc.** | CDN og trafikstyring | **Worldwide** («Local to Customer») | **amerikansk** |
| Google LLC | Infrastruktur | Holland, Belgien | **amerikansk** |

Googles amerikanske placering står udtrykkeligt som «United States (US API
endpoint)» med produktet «Studio/API (US API)». Den hører til det amerikanske
endepunkt, som appen hverken bruger eller kan bruge.

### De to, der stikker ud

**CrowdStrike** er opført med «EØS, United States» for *alle* produkter. Det er
ikke kun amerikansk ejerskab — det er en amerikansk **placering** i kæden.

**Cloudflare** er opført «Worldwide» med bemærkningen «Local to Customer». Det
peger på, at trafikken føres til nærmeste knudepunkt, men ordet i tabellen er
«Worldwide».

Ingen af de to kan efterprøves udefra. De er det stærkeste argument for, at
«data bliver i Europa» ikke kan siges uden en skriftlig bekræftelse.

### Ude af billedet — hører til andre produkter

Blackforest Labs (billeder), Brave (websøgning), Foundrylabs/E2B
(kodefortolker) og Merge API (Vibe-connectors) hører til Vibe og Studio/API's
øvrige funktioner, som appen ikke bruger. Scaleway, Megaport, OVH, Backblaze
og The Constant Company/Vultr hører alle til **Vibe Code Web**.

### Kun konto og betaling — ikke mødeindhold

Get Lago (Irland), Stripe (**USA**), Twilio (**USA**), Resend (Irland) og
Intercom (Irland).
