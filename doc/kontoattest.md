# De tre kontrolpunkter — hvad appen kan efterprøve, og hvad den ikke kan

*Skrevet 03-09-2026. Hører til fanen «Kontrolpunkter» på Compliance-skærmen.*

Tre ting afgør, om bearbejdningen hos Mistral holder det, produktet lover.
Appen kan efterprøve **den ene** af dem. De to andre er kontoindstillinger, og
appen kan hverken sætte dem eller læse, om de er sat.

Det er hele grunden til, at fanen findes. En skærm med tre grønne flueben
ville lyve om to af dem.

| Kontrolpunkt | Grundlag | Hvem afgør det |
|---|---|---|
| EU-regional inferens | **Verificeret af endepunktet** | Koden, ved hver afsendelse |
| Zero Data Retention | Kræver manuel aktivering | Et menneske, i Mistral-kontoen |
| Fravalg af modeltræning | Kræver manuel aktivering | Et menneske, i Mistral-kontoen |

Rækkefølgen og grundlaget står i `Kontoattester.Kontrolpunkter` og er prøvet i
`KontoattestTest`. Bliver de tre linjer en dag lige grønne, falder prøven.

---

## 1. EU-regional inferens — verificeret

`SkyKatalog.KraevEuropa` kaster, hvis værten ikke er `api.eu.mistral.ai`.
Kontrollen ligger i `SendAsync`, altså på det ene sted, alt går igennem — ikke
ved kaldstederne, hvor et nyt kald kunne glemme den.

Det er en rigtig verifikation: den sker hver gang, den sker før afsendelsen, og
den stopper med en fejl frem for at lade kaldet gå igennem.

## 2 og 3. Zero Data Retention og fravalg af modeltræning — attest

Begge sættes hos Mistral under **Workspace → Data**. Der findes ikke et
endepunkt, appen kan spørge, og appen har ikke adgang til kontoadministrationen
med den API-nøgle, brugeren indtaster.

Det, der **kan** lade sig gøre, er at gøre påstanden revisionsklar. Brugeren
registrerer tre ting pr. indstilling:

- **dato** — hvornår den blev slået til
- **ansvarlig** — hvem der står inde for det
- **dokumentationsreference** — hvor brugerens eget bilag ligger, lokalt

Skærmen skriver **REGISTRERET**, ikke **SLÅET TIL**. Forskellen er ikke
sproglig pedanteri: appen har ikke set indstillingen, og må ikke skrive, at den
har. Mærkatet er af samme grund gult, ikke grønt.

### Hvad der ikke gemmes

Intet fra kontoen hos leverandøren. Ikke konto-id, ikke workspace-id, ikke
nøglen, ikke organisationens navn. Tre felter, brugeren selv skriver, i
`kontoattest.json` i datamappen.

`Kontoattester.Fejl` afviser en værdi, der indeholder tyve eller flere
sammenhængende bogstaver og tal. En Mistral-nøgle er 32 tegn i én ubrudt blok
(efterprøvet 03-09-2026 mod en nøgle fra console.mistral.ai), og et
workspace-id er en UUID. Et sagsnummer som `SAG-2026-0142` og et bilagsnavn som
`bilag-4.pdf` har begge skilletegn og bliver ikke ramt.

Kontrollen står ét sted og kaldes både af skærmen — så beskeden kommer, mens
feltet er åbent — og af `Kontoattester.Gem`, så et nyt kaldsted ikke kan komme
udenom den. Samme opbygning som `KraevEuropa`, og af samme grund.

---

## Hvad EU-endepunktet ikke dækker

Det her afsnit er det vigtigste i noten, fordi det er dét, der er nemmest at
sige for meget om.

EU-endepunktet afgør, **hvor selve bearbejdningen af teksten sker**. Det er
ikke det samme som, at alt hos leverandøren ligger i Europa:

- **Kontrolplanet** — det, der tager imod anmodningen, ruter den og håndhæver
  rettigheder — er ikke nødvendigvis regionalt.
- **Kontoadministration** — konto, fakturering, support — er det heller ikke.
  Underdatabehandlerlisten har Stripe og Twilio i USA netop til det formål; se
  `doc/mistral-dpa.md`.
- **Driftsmetadata** — logning, overvågning, misbrugsdetektion — er ikke
  beskrevet regionalt nogen steder, vi har kunnet læse.

Appen kan ikke se, hvor nogen af de tre ligger. Compliance-skærmen siger derfor
ikke mere end det: endepunktet styrer inferensgeografien, og resten står som et
forbehold, ikke som en påstand.

Det gælder også den anden vej: en registreret ZDR-attest fjerner ikke
forbeholdet. Den siger, at nogen har slået indstillingen til og skrevet under
på det.

---

## Kvitteringerne

Hver afsendelse bogføres i `kvitteringer\ÅÅÅÅ-MM.jsonl`. Felterne, der gør en
kvittering brugbar i en revision:

| Felt | Hvad det svarer på |
|---|---|
| `Tidspunkt` | Hvornår |
| `Endepunkt` | Den fulde adresse, der blev kaldt — ikke «Mistral» |
| `Model` | Hvilken model |
| `Tegn` | Hvor meget der gik |
| `Sum` | SHA-256 af de nøjagtige bytes — beviset for **hvad** |
| `Anmodningsid` | Leverandørens eget id, når der kom et |

`Anmodningsid` kom til 03-09-2026. Kontrolsummen beviser, hvad *denne* maskine
sendte; anmodnings-id'et er det, **begge parter** kan slå op på, hvis en
hændelse skal følges op hos Mistral. Uden det er svaret en dato og et
klokkeslæt, og det peger ikke på én anmodning ud af mange.

Id'et læses af svarets headere. Mistral dokumenterer ikke headeren; set
03-09-2026 mod `api.eu.mistral.ai` kom det som `x-kong-request-id`.
`Kvitteringer.LaesAnmodningsid` prøver de gængse navne i rækkefølge, og kom der
intet, står feltet **tomt**. Et id, appen selv fandt på, kunne slås op nul
steder og ville ligne noget, det ikke er.

Id'et læses **før** fejlkontrollen. En afvist anmodning er den, der oftest skal
følges op — og det ville være den eneste kvittering uden id, hvis rækkefølgen
var omvendt.

Kvitteringer skrevet før 03-09-2026 har ikke feltet. De læses uændret, og
linjen vises slet ikke på skærmen: en tom etiket ville ligne en oplysning, der
mangler hos leverandøren, og ikke en, der aldrig blev gemt.

Anmodningsteksten og nøglen gemmes ikke — hverken før eller nu. Se
`Kvitteringer` for begrundelsen og for prisen ved det valg.
