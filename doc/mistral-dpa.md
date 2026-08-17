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

### Underdatabehandlerne

Fra Trust Center, 17-08-2026:

| Underdatabehandler | Rolle | Placering |
|---|---|---|
| Mistral Compute | Infrastruktur | Frankrig |
| Microsoft Inc. | Infrastruktur | Sverige, **Norge** |
| Google LLC | Infrastruktur | Holland, Belgien, **USA (US-endepunktet)** |
| Ory Corp. | Godkendelse af brugere | Belgien, Tyskland |

To ting at bemærke:

**Norge er EØS, ikke EU.** «100 % europæisk» er rigtigt. «100 % EU» ville ikke
være det.

**Google LLC og Microsoft Inc. er amerikansk ejede** — også når serverne står i
Sverige og Holland. Diskussionen om CLOUD Act gælder derfor stadig. Deres
USA-placering er knyttet til US-endepunktet, som vi ikke bruger, men ejerskabet
forsvinder ikke af, at hardwaren står i Europa.

Vil man kunne sige «ingen amerikansk virksomhed rører data», holder det ikke.
Vil man sige «bearbejdningen sker i Europa hos en europæisk leverandør», holder
det.

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
