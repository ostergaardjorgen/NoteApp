# Prompt til næste session — punkt 2, 3 og 4

Punkt 1 (hemmeligheder) er færdigt i v1.2.72. Teksten herunder er skrevet til
at blive indsat som den første besked i en ny samtale.

---

Du skal forbedre compliance, forsyningskædesikkerhed og leverancekvalitet i
HeyPia. Arbejd direkte i projektet.

## Rammer, der ikke må brydes

- Kode i `C:\NoteApp`, brugerdata i `C:\AppNoter`. Datamappen må ikke flyttes.
- Den forbudte OneDrive-sti og det forbudte firmanavn står i de globale regler
  i `C:\Users\oster\.claude\CLAUDE.md` og i termlisten hos leverancetjekket.
  De må **aldrig** indgå i noget: ikke læsning, skrivning, henvisninger,
  links, kodeeksempler eller dokumentation — heller ikke via relative stier,
  der opløser til dem. **De gengives ikke her.** Denne note gjorde det indtil
  03-09-2026, og resultatet var, at repoets eget leverancetjek fejlede på
  den: en regel, der skriver sig selv ud, er ikke en regel, den er et fund.
- Kør `powershell -File "C:\Users\oster\.claude\skills\leverancetjek\tjek-leverance.ps1" -Sti C:\NoteApp`
  før hver commit. Den fejler ved forbudte termer.
- Udgiv med `powershell -File C:\NoteApp\scripts\udgiv.ps1`. Versionen sættes
  ud fra commit-beskeden (`vX.Y.Z: ...`), og tredje led skal stige.
- Meld versionsnummer og tidsstempel i chatten efter hver udgivelse.
- Al tekst, brugeren ser, er dansk med korrekt æ, ø og å. Kodekommentarer og
  commit-beskeder er ASCII (skriv `ae`, `oe`, `aa`).
- I brugerfladen bruges ikke «vi», «os» eller «vores» — teksten skrives til en
  kunde.
- Hårdkodede farver i XAML er forbudt; brug `{DynamicResource ...}`.
  `DynamicResource` må **kun** stå på afhængighedsegenskaber — ikke på
  `Binding.Source`. Det kostede et nedbrud 03-09-2026.
- Alle prøver skal bestå. Grundlinjen er **618**.

## Datagrænsen, der skal bevares

- Mødeoptagelser og uploadede lydfiler bliver på maskinen og skrives ud
  lokalt med whisper.cpp.
- Diktering skrives også ud **lokalt** — Voxtral afviser `da` som sprogvalg
  (efterprøvet mod endepunktet 30-08 og 31-08-2026, også mod
  `voxtral-mini-2602`), så sproget kunne kun påvirkes, aldrig vælges. Kun
  teksten sendes til Mistral for oprydning.
- Mistral-kald må kun gå til `api.eu.mistral.ai`. Låsen ligger i
  `SkyKatalog.KraevEuropa` og skal blive.
- Google Kalender og Google Tasks bruges kun efter brugerens egen forbindelse
  og OAuth-godkendelse.

## Det, der allerede er lavet (punkt 1 — rør det ikke)

`Hemmelighed.cs` og `Opstartsbeskyttelse.cs` i Core. Mistral-nøglen og
Googles opdateringsnøgler ligger nu beskyttet med DPAPI bundet til
Windows-brugeren, med migrering fra klartekst, atomisk skrivning, sikker
sletning og maskering i logs. 18 prøver i `HemmelighedTest.cs`.

---

## Punkt 2 — gør Mistrals ZDR og træningsfravalg revisionsklart

Appen kan ikke teknisk aktivere eller verificere kontoindstillinger hos
Mistral, og den må ikke påstå, at den kan.

- Tilføj en compliance-status i UI og dokumentation med tre linjer:
  - **EU-regional inferens** — verificeret af endepunktet
  - **Zero Data Retention** — kræver manuel aktivering i Mistral-kontoen
  - **Fravalg af modeltræning** — kræver manuel aktivering i Mistral-kontoen
- Lad brugeren registrere dato, ansvarlig og en lokal dokumentationsreference
  for de to sidste. Gem ingen konto-oplysninger.
- Udvid Mistral-kvitteringerne med endepunkt, model, tidsstempel og — hvis
  Mistral returnerer det — request-id fra svarets headers. Gem **aldrig**
  anmodningsteksten eller nøglen. Se `Kvitteringer.cs`; kvitteringerne findes
  allerede med tegn, tokens, pris og SHA-256 af den sendte tekst.
- Dokumentér præcist: EU-endepunktet styrer inferensgeografien, men gør ikke
  nødvendigvis kontrolplan, kontoadministration eller driftsmetadata
  regionale. Compliance-skærmen må ikke sige mere end det.

## Punkt 3 — verificér motorer og modeller ved download

Filstørrelse alene er ikke integritetskontrol.

- Indfør SHA-256 for whisper-motoren, whisper-modellerne, VAD-modellen og
  øvrige hentede komponenter, hvor en kendt sum kan angives.
- Gem forventet sum og kilde som versioneret manifestdata i repoet.
- En download er først fuldført, når **både** størrelse og sum passer.
- Ved forkert sum: slet filen, vis en tydelig fejl, kør den aldrig.
- Bevar resume-download, men verificér altid den færdige fil.
- Kan en leverandør ikke levere en troværdig sum, skal UI og dokumentation
  vise det som en eksplicit forsyningskæderisiko — ikke foregive verifikation.
- Prøver for: rigtig sum, forkert sum, afbrudt download, eksisterende
  ødelagt fil.

Relevant kode: `EngineInstaller`, `WhisperInstall`, `WhisperModel`,
`Diarisering`. Modellerne ligger i `C:\AppNoter\motor\modeller`
(`ggml-large-v3.bin`, `ggml-large-v3-turbo.bin`, `ggml-medium.bin`,
`ggml-small.bin`, `ggml-silero-v5.1.2.bin`), motoren i
`C:\AppNoter\motor\whisper\bin\Release`.

## Punkt 4 — lever licensnotitser med installationspakken

- Kortlæg licenser og attributionskrav for whisper.cpp, Whisper-modellerne,
  sherpa-onnx, ONNX Runtime, pyannote-segmentering, NVIDIA TitaNet og øvrige
  redistribuerede binærer og modeller.
- Læg de nødvendige LICENSE-, NOTICE- og attributionsfiler i den publicerede
  appmappe, så de følger med i MSI/setup.
- Ret compliance-skærmen og installerdokumentationen, så de kun lover
  notitser, der faktisk følger med.
- Tilføj en gate i build/installer, der **fejler**, hvis krævede licensfiler
  eller taleradskillelseskomponenter mangler fra den publicerede app eller
  pakken. Prøv gaten automatiseret.

Relevant: `scripts/udgiv.ps1`, `scripts/byg-installer.ps1`,
`installer/NoteApp.wxs`, og compliance-skærmen i
`src/NoteApp.Desktop/Compliance`.

---

## Arbejdsmåde, der har virket i dette projekt

- **Mål frem for at gætte.** Hver rettelse i koden har målingen skrevet ved
  siden af sig — tal, dato og hvad der blev observeret. Gør det samme.
- Kommentarer forklarer **hvorfor**, ikke hvad. Skriv, hvad der gik galt, og
  hvad der blev prøvet og forkastet.
- Skriv prøver, der fastholder målingen, så et tal ikke kan skride tilbage,
  uden at nogen ser hvad det så koster.
- Tag punkterne ét ad gangen: byg, prøv, leverancetjek, commit, udgiv, meld
  versionsnummer. Så kan hvert punkt rulles tilbage for sig.
- Sig det, hvis noget ikke kan afgøres, i stedet for at love det.
