# Fortsæt her

*Skrevet 23-08-2026 ved v1.0.65. Læs den her først — så kan arbejdet fortsætte
uden samtalehistorikken.*

## Hvis du er en ny session

Læs de her fire, i den rækkefølge. Det tager et kvarter og sparer en dag.

1. **[`../kontinuitet/arbejdsmaade.md`](../kontinuitet/arbejdsmaade.md)** —
   reglerne. Den vigtigste af dem alle: koden kan læses, reglerne kan ikke
   udledes af den.
2. **[`produkt.md`](produkt.md)** — hvem appen er til, og hvad den skal kunne.
3. **[`cockpit-plan.md`](cockpit-plan.md)** — planen og hvor langt den er nået.
4. **[`findings.md`](findings.md)** — alt, der er målt. Kig HER, før noget
   bliver undersøgt igen.

Spørg brugeren, hvis noget er uklart. **Gæt aldrig på et tal.**

## Hvad appen er

En Windows-app (WPF, .NET 8) til at optage møder og webinarer, skrive dem ud
til tekst lokalt, og finde tilbage til det, der blev sagt.

**Transkription er ikke produktet — det er prisen for at komme ind.** Værdien
vokser med arkivet: efter to hundrede møder er appen det eneste sted, hvor man
kan finde ud af, hvad der *faktisk* blev sagt. Søgningen er derfor ikke en
funktion i cockpittet; den ER cockpittet.

Kunden er studerende, iværksættere og mindre selvstændige. Der måles op mod
**Granola**.

## Hvor tingene ligger

| Hvad | Hvor |
|---|---|
| Kode | `C:\NoteApp` (dette repo) |
| Brugerens data | `C:\AppNoter` — aldrig i git |
| Udgivet app | `C:\NoteApp\app\NoteApp.exe` — skrivebordsgenvejen peger her |
| Installeret app | `C:\Program Files (x86)\NoteApp` |
| Legitimationer | `C:\NoteApp\hemmeligheder\` — aldrig i git |
| Modeller | `C:\NoteApp\models`, motorer i `C:\AppNoter\motor` |

Maskinen: RTX 2060 med 6 GB VRAM, i7-1165G7, 32 GB RAM, Windows 11.
**Alle tal i `findings.md` er målt på den.**

## Sådan bygger og udgiver du

```powershell
powershell -File C:\NoteApp\scripts\udgiv.ps1
```

Versionsnummeret kommer fra den seneste commit (`vX.Y.Z: …`). Commit **først**,
udgiv bagefter — ellers får udgivelsen samme nummer som sidst. Meld altid
nummeret og tidspunktet i chatten.

Installationsfilen: `powershell -File C:\NoteApp\scripts\byg-installer.ps1`.

Kør altid leverancetjek før commit og push.

## Status ved v1.0.65

| Etape | Status |
|---|---|
| 0 · Cockpittet findes | Gjort |
| 1 · Optag et webinar | Gjort — afprøvet på to rigtige, 22 og 54 min |
| 2 · Søgningen bliver god | Gjort — målt til 90 % / 100 % |
| 3 · Opgaver på tværs + kalender | Gjort |
| 4 · Webinarområdet gøres færdigt | **Næste** |
| 5 · Diktafonen | Ikke begyndt |

### Det, der virker i dag

Optagelse af møder (to spor) og webinarer (ét spor, stopper selv efter fem
minutters stilhed og klipper stilheden af). Transkription lokalt med
whisper.cpp på GPU, 0,09 gange realtid. Talergenkendelse, der skiller stemmer
ad. Søgning på tværs med faner, sortering og fire filtre. Opgaver på tværs med
dansk datoforståelse. Kalender, lokal og med Google. Dokumenter via Mistral i
EU. Mødevagt, der spørger, når et program åbner mikrofonen.

### Det, der mangler i etape 4

- **Planlagt optagelse**, der virker med appen lukket — kræver en planlagt
  opgave i Windows. Det er den tunge del.
- **Påmindelse** før et webinar. Må aldrig afbryde en optagelse; den skal
  vente eller lande på klokken.
- **Oversættelse med originalen ved siden af.** Målt: lokalt ~3 min for et
  22-minutters webinar, ~11 min for 50 — men kvaliteten er synligt
  maskinoversat med modellerne på maskinen. Beslutningen er ikke truffet.
- **Microsoft 365-kalender.** `Integrationer.Alle` er skrevet, så den er en
  post på en liste og ikke et særtilfælde.

## De tre beslutninger, der venter

**Semantisk søgning.** Ikke afvist — den venter bevidst. De tyve prøver er alle
ord, der STÅR i teksten, og det er dét, ordsøgning er god til. Målingen, der
afgør spørgsmålet, er tyve spørgsmål stillet med ANDRE ord end dem, der blev
sagt («hvad sagde de om prissætning»). Den kan først laves ærligt, når arkivet
er stort nok til, at man ikke selv kan huske svaret.

**Navneudtræk** — «find leverandørerne nævnt i denne måned». Målt til at virke
i blokke, ~1 minut pr. times møde, og efterprøvningen kasserer 18-30 % som
opfundet. Men det, der overlever, blander rigtige navne med almindelige ord
(«dag», «uge», «kunder»). Der mangler ét led: at afgøre, om noget ER et navn.
Tre billige veje står i `findings.md`.

**Google-verifikation.** Så længe samtykkeskærmen er i testtilstand, er der et
loft på 100 brugere. Skal appen ud til kunder, skal den igennem Googles
verifikation, og det tager tid. Se `google-integration.md`.

## Roadmap uden for etaperne

Står i [`roadmap.md`](roadmap.md). Det største punkt: **appen justerer sig selv
ud fra brug og kan skrue tilbage.** Indstillingerne er målt på ÉN stemme, ÉN
mikrofon og ét møde; hos en anden kunde rammer de næppe lige så godt. Brugerens
egne rettelser ER målingen.

## Hvis noget skal efterprøves

```bash
noteapp maalsoegning   # tyve søgeprøver med kendt facit
noteapp maaldato       # tyve datoer i talesprog
noteapp motor          # hvilken whisper-motor og model der bruges
noteapp status         # hvor data ligger, og hvad de indeholder
```

Værktøjet bygges fra `src\NoteApp.Tools`.

## Det, der IKKE er i git

- `C:\AppNoter` — optagelser, transkriptioner, dokumenter, indstillinger
- `hemmeligheder/google-klient.json` — se `google-integration.md`
- `forbudte-termer.txt` i leverancetjek-skillen — se
  [`../kontinuitet/README.md`](../kontinuitet/README.md)
- Modelfilerne (flere GB)

**Data tages med appens egen sikkerhedskopi** under Indstillinger →
Sikkerhedskopi. Den er ikke det samme som git, og den ene erstatter ikke den
anden.
