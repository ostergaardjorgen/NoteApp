# HeyPia

Windows-app, der optager møder og webinarer, skriver dem ud til tekst og
samler det hele ét sted, hvor man kan søge på tværs og altid finde tilbage til
det, der faktisk blev sagt.

```
Optagelse  →  Transkription (Whisper, lokalt)  →  Dokument (Mistral, EU)
```

> ### Skal du fortsætte udviklingen?
>
> Læs **[`doc/fortsaet-her.md`](doc/fortsaet-her.md)** først. Den siger, hvor
> arbejdet står, hvad der er målt, hvilke beslutninger der venter — og hvilke
> regler der gælder, som ikke kan læses ud af koden.

## Hvor dine data er

Det her er det vigtigste afsnit i hele repoet, så det står først:

| | Hvor |
|---|---|
| **Lyden** | Forlader **aldrig** maskinen. Optagelse og udskrift sker lokalt med Whisper. |
| **Udskriften** | Sendes til Mistral AI's **europæiske** endepunkt (`api.eu.mistral.ai`) — men kun når du selv beder om et dokument. |
| Noter, skabeloner, dokumenter, indstillinger | Bliver på maskinen. |
| Telemetri, fejlrapportering, skysynkronisering | Findes ikke. |

**Hvorfor det er lyden, der tæller.** Forskellen på en lydfil og en udskrift er
ikke en gradsforskel. Behandles en stemmeoptagelse med det formål at genkende,
hvem der taler, er den samtidig biometriske data efter databeskyttelses-
forordningens artikel 4, nr. 14 — og dermed en særlig kategori efter artikel 9.
En optagelse er råstoffet til den behandling; en udskrift er det ikke. Lyden
bærer desuden tonefald, accent og ting om helbred og sindstilstand, som ingen
har sagt højt. Intet af det følger med en tekst.

Det er et designvalg, ikke en begrænsning. Talegenkendelse i skyen ville være
hurtigere og er fravalgt; bliver udskriften for langsom, løses det med bedre
lokale modeller.

**Fortæl altid mødedeltagerne, at du optager.** Ved et onlinemøde giver
mødeprogrammet ikke besked, når lyden optages på denne måde — så beskeden skal
komme fra dig. Til gengæld kan du fortælle dem, hvor optagelsen ender: den
bliver på din maskine og lægges ikke i nogen skytjeneste. Det er en besked, de
færreste mødeværktøjer kan give.

Dokumentdelen kræver **din egen API-nøgle**. Der følger ingen med appen, og
appen opretter ingen konto for dig. Vil du ikke sende noget ud af huset, kan
du bruge appen til optagelse og udskrift alene.

Hvad vi ved og ikke ved om leverandøren — herunder de underdatabehandlere, der
gør, at «alt bliver i Europa» **ikke** kan siges uforbeholdent — står under
**Compliance** i appen og i [`doc/mistral-dpa.md`](doc/mistral-dpa.md).

> Appen sagde tidligere «intet forlader denne pc». Det gjaldt, indtil
> dokumentdelen kom til den 17. august 2026. Beslutningen er dokumenteret i
> [`doc/sky-api-priser.md`](doc/sky-api-priser.md) og
> [`doc/mine-data.md`](doc/mine-data.md) — den blev truffet, fordi et brugbart
> referat ikke kunne laves lokalt, og det er målt, ikke antaget.

## Installation

Byg installationsfilen og kør den:

```powershell
powershell -File C:\NoteApp\scripts\byg-installer.ps1
```

Se [`INSTALLATION.md`](INSTALLATION.md) for hele vejen — første start,
skærmene, datamappen, backup og opsætning forfra på en ny maskine.

## Dokumentation

| Fil | Hvad den svarer på |
|---|---|
| [`INSTALLATION.md`](INSTALLATION.md) | Installation, skærmene, datamappen, byg fra kildekode |
| [`doc/mine-data.md`](doc/mine-data.md) | Hvor filerne ligger, hvad der forlader maskinen, backup |
| [`doc/mistral-dpa.md`](doc/mistral-dpa.md) | Databehandleraftalen, og hvad den **ikke** dækker |
| [`doc/whisper.md`](doc/whisper.md) | Motor og modeller, og hvordan man undersøger dem |
| [`doc/maaling-whisper.md`](doc/maaling-whisper.md) | Målte ordfejlrater |
| [`doc/maaling-sky.md`](doc/maaling-sky.md) | Målinger af sprogmodellen |
| [`doc/findings.md`](doc/findings.md) | Fund undervejs — protokol, ikke aktuel tilstand |
| [`doc/roadmap.md`](doc/roadmap.md) | Idéer, der er gode, men ikke laves nu |

Dokumenter markeret som protokol beskriver, hvad der blev besluttet hvornår.
De rettes ikke, når verden skifter — der sættes en ramme øverst i stedet, så
det kan ses, hvad der er historik og hvad der gælder.

## Data er aldrig i git

Optagelser, udskrifter, dokumenter og indstillinger ligger i `C:\AppNoter`
(eller den mappe, du vælger under Indstillinger → Filer). Miljøvariablen
`NOTEAPP_DATA` vinder over begge dele.

De ligger uden for arbejdstræet med vilje, så en fejl i `.gitignore` ikke kan
lække et møde.
