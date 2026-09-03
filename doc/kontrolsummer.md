# Kontrolsummer — hvorfor filstørrelse ikke er integritetskontrol

*Skrevet 03-09-2026. Hører til `Komponentmanifest`, `Downloader` og fanen
«Modeller og licenser» på Compliance-skærmen.*

Appen hentede motor og modeller ned og kontrollerede kun, at filen havde den
størrelse, den skulle. Det er ikke integritetskontrol.

## Målingen, der afgjorde det

Stemmevagtens to udgaver hos Hugging Face, slået op 03-09-2026:

| Fil | Størrelse | SHA-256 |
|---|---|---|
| `ggml-silero-v5.1.2.bin` | 885.098 | `29940d98…04ea2cf` |
| `ggml-silero-v6.2.0.bin` | 885.098 | `2aa269b7…7fb6987` |

**Nøjagtig samme størrelse. To forskellige modeller.** Den kontrol, appen
havde, kunne ikke se forskel på dem. Det er ikke et konstrueret eksempel — det
er de to filer, der ligger i den mappe, appen selv henter fra.

En forkert modelfil fejler i øvrigt ikke med det samme. Den giver volapyk i
udskriften, og det viser sig langt fra sin årsag — hvis det overhovedet
opdages.

---

## Hvad der er lavet

### Manifestet

`src/NoteApp.Core/manifest/komponenter.json` — versioneret i repoet, indlejret
i bygget. Ni komponenter: fire whisper.cpp-udgaver (v1.9.2), fire
Whisper-modeller og stemmevagten. Hver linje har filnavn, størrelse, SHA-256,
hvor **filen** kommer fra, hvor **summen** kommer fra, og hvad der er gjort for
at bekræfte den.

Filen er indlejret og ikke kopieret ned ved siden af exe'en. En fil, brugeren
kan rette, er ikke en kontrol: den, der ville lukke en anden model ind, ville
bare rette summen først.

### Summerne er leverandørens egne

- **Hugging Face**: LFS-oid'et, som *er* filens SHA-256, slået op på deres API.
- **GitHub**: `digest`-feltet på udgivelsens aktiver.

De fem modelfiler er **desuden** efterprøvet mod de filer, der lå i datamappen
03-09-2026 — alle fem stemte, byte for byte. Motorens zip-filer lå ikke på
maskinen og er derfor kun bekræftet af GitHub selv; det står på hver linje i
manifestet, så forskellen ikke går tabt.

En sum, der regnes ud af den fil, appen lige har hentet, efterprøver kun sig
selv. Derfor slås de op hos leverandøren, og derfor ligger de fast i koden.

### Kontrollen ligger i `Downloader`, ikke ved kaldstederne

Manifestet slås op på filnavnet inde i `DownloadAsync`. Et nyt kaldsted kan
glemme en kontrol; det kan ikke undgå den her. Samme opbygning som
`SkyKatalog.KraevEuropa`, og af samme grund.

En hentning er først fuldført, når **både** størrelsen og summen passer:

| Situation | Hvad der sker |
|---|---|
| Rigtig sum | Filen omdøbes på plads og bruges |
| Forkert sum | `.delvis` **slettes**, tydelig fejl med begge summer, filen bruges aldrig |
| Afbrudt hentning | `.delvis` **beholdes**, så næste forsøg fortsætter |
| Fil ligger der med rigtig størrelse og forkert indhold | Slettes og hentes om |
| Fil ligger der og er rigtig | Ingenting — der hentes ikke igen |

De to slettebeslutninger er forskellige med vilje. Ved en afbrudt hentning er
det hentede brugbart, og resume er hele grunden til, at en model på 2,9 GB
overhovedet kan hentes på en ustabil forbindelse. Ved en forkert sum ville en
fortsættelse give en forkert fil igen, hver gang, indtil nogen slettede den i
hånden.

**Resume er bevaret. Den færdige fil verificeres altid.**

### Stemmevagten

`WhisperInstall.HentVadAsync` havde kontrollen `data.Length != VadStoerrelse`
— præcis den kontrol, der ikke kan skelne v5.1.2 fra v6.2.0. Den hedder nu
`VadPasser`, og den kræver begge dele. Kan summen ikke slås op — manifestet
kunne ikke læses — siges der nej. Modellen er et tilvalg, transskriptionen
kører uden den, og at køre videre på en fil, der ikke kunne efterprøves, er den
forkerte vej at tage fejl.

### Fremdrift under kontrollen

At læse 2,9 GB igennem tager sekunder nok til, at en skærm uden bevægelse
ligner en app, der er gået i stå. Bjælken bruges derfor til begge dele — men
`DownloadProgress.Kontrollerer` siger hvilken, så der ikke står «Henter …» og
en hastighed, mens der ikke hentes noget.

---

## Hvad der ikke er dækket

**Alt, manifestet ikke kender, bliver ikke verificeret.** Det står på
Compliance-skærmen som det, det er: en forsyningskæderisiko, ikke en
verifikation. `Komponentmanifest.Ukendt` er teksten, og den er skrevet, så den
ikke kan læses som en beroligelse.

**Taleradskillelsen hentes ikke.** sherpa-onnx, pyannote-segmenteringen og
NVIDIA TitaNet følger med installationspakken og er dækket af pakkens egen
kontrol. Det står på skærmen, fordi en liste over hentede komponenter ellers
får en læser til at spørge, hvor resten blev af.

**Manifestet siger ikke noget om, hvad filen indeholder.** Det siger, at den er
den samme fil som den, der blev målt på. Det er en anden — og mindre — påstand
end «filen er sikker», og den skal ikke blandes sammen med den.

---

## Når der skal tilføjes en komponent

1. Slå summen op **hos leverandøren**. Ikke af den fil, appen lige har hentet.
2. Skriv hvor summen kom fra, i `sumkilde`.
3. Skriv i `bekraeftelse`, hvad der ellers er gjort — eller at der ikke er.
4. Sæt datoen.

`KomponentsumTest` holder fast i, at alt i modelkataloget har en kendt sum, og
at manifestets størrelser er enige med katalogets. Kommer der en model uden
sum, falder prøven — den ville ellers blive hentet uden verifikation, og det
ville ingen opdage, for den ville virke.
