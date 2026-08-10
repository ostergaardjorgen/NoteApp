# NoteApp (Windows) — samlet spec v1

Konsolideret 2026-08-07 ud fra samtalehistorikken. Erstatter den oprindelige iOS-plan.

Projektmappe: `C:\NoteApp\` — kildekode, modeller, whisper-binærer og optagelser. NAS'en (`\\NAS_HOME\home\Drive\Vagtsom\NoteApp.git`) er backup via git, ikke arbejdsdrev. Begrundelsen er målt, ikke antaget — se afsnit 8.

---

## 1. Kerneidé og arkitekturvalg

Windows-desktop frem for iOS, fordi Windows kan optage **to separate spor**:

| Spor | Kilde | Indhold |
|---|---|---|
| Mikrofon (`WasapiCapture`) | Dig i rummet | Dig |
| Loopback (`WasapiLoopbackCapture`) | Systemlyd | De andre deltagere via Teams |

To spor giver **gratis diarisering på øverste niveau** — man ved per definition hvem der er på hvilket spor. Det var det svære problem i iOS-planen. Samtidig har laptoppen CPU/RAM til en stor Whisper-model, hvor telefonen tvang kvaliteten ned.

Fysiske møder: kun mikrofon-sporet. Samme app, ét toggle.

---

## 2. Mødetype som første skærm

Ingen default — aktivt valg. Forhindrer den værste fejl: at optage et onlinemøde og opdage bagefter at loopback-sporet er tomt.

| | Fysisk møde | Onlinemøde |
|---|---|---|
| Mikrofon | Ja | Ja |
| Loopback | Nej | Ja |
| Talerlabels | Ingen (ét spor) | "Mig" / "Deltagere" |
| Resumé-prompt | Rummøde, flere talere | Teams-møde, to sider |

Valget skrives til `meeting.json` og styrer: hvilke `IWaveIn`-instanser der startes, hvordan sporene flettes i fase 2, og hvilket prompt-skema der bruges ved eksport.

### Sanity-checks ved start
- **Onlinemøde:** mål loopback-niveau i 3 sekunder før rigtig optagelse. Stilhed → advarsel (Teams ikke startet, eller lyd går til et headset uden for standard-renderenheden). Det er fejlen der koster et helt møde.
- **Fysisk møde:** tjek at mikrofonen ikke er et glemt headset, og vis hvilken enhed der optages fra. Laptoppens indbyggede array-mikrofon er bedre til et rum end en headset-bøjle der peger væk fra bordet.

### Model
```csharp
public enum MeetingType { Physical, Online }

public record MeetingSession(
    Guid Id,
    MeetingType Type,
    DateTimeOffset StartedAt,
    string? Title,
    string MicDeviceName,
    string? LoopbackDeviceName);   // null ved fysiske møder = flettning springes over i fase 2
```

### UI
Startskærm = to store knapper ("Fysisk møde" / "Onlinemøde") + valgfrit titelfelt. Derefter: enhedsstatus, niveaumålere for aktive spor, én stor optageknap. Ingen indstillinger, ingen dropdowns — enhedsvalg tages fra Windows' standardenheder, appen viser kun hvad der blev valgt.

**Titelfelt fra start**, selv om det føles overflødigt — efter tyve møder er `2026-08-07_14-30` ubrugeligt som filnavn.

---

## 3. Diarisering og navngivning

Fase 2 er ikke "transskription", men "transskription + diarisering". Navngivning er et rent mapping-lag ovenpå.

**Vigtigste designbeslutning: navne skrives aldrig ind i transskriptionen.** Transskriptionen gemmer anonyme taler-ID'er; `meeting.json` holder tabellen `speakerId → navn`. Navne renderes først ved visning og eksport. Derfor er omdøbning øjeblikkelig, sker alle steder på én gang, og kan fortrydes.

### Sådan opstår talerne
1. Mikrofonsporet er dig — kendt på forhånd, ingen analyse.
2. Loopback-sporet indeholder alle andre og skal klynges:
   - Whisper giver segmenter med tidsstempler
   - Stemme-embedding pr. segment (ECAPA-TDNN eller WeSpeaker som ONNX, kører fint i .NET via ONNX Runtime)
   - Agglomerativ klyngning på cosinus-afstand → Taler 1, 2, 3 …
   - Du sætter navne én gang

Antallet af talere kendes ikke på forhånd → brug **afstandstærskel**, ikke fast antal. Men spørg "hvor mange deltagere?" ved start (deltagerlisten er ofte synlig i Teams) og brug det som hint — det løfter præcisionen mærkbart.

### Datamodel
```csharp
public record Speaker(string Id, string? Name, float[] Centroid, int SegmentCount);

public record Segment(
    TimeSpan Start, TimeSpan End,
    string SpeakerId, string Text, AudioTrack Track);
```

### Obligatoriske UI-funktioner (ikke nice-to-have)
Klyngning over-segmenterer altid — Taler 3 og Taler 7 er ofte samme person der skiftede tonefald eller headset.

1. **Navngiv** — klik på taler-chip, skriv navn, alle segmenter opdateres
2. **Flet** — træk Taler 7 over på Taler 3 (eller giv samme navn) → de smelter sammen
3. **Ret enkeltsegment** — omfordel ét segment uden at røre resten
4. **Autocomplete** — foreslå navne fra tidligere møder

**Fletning bruges mest. Byg den før noget som helst pynt.**

### Realistiske forventninger
Loopback-lyd fra Teams er godt materiale: rent, komprimeret, sjældent overlappende tale, fordi Teams selv styrer hvem der kommer igennem. Regn med brugbar klyngning ved 3-6 deltagere.

Fysiske møder med laptop-mikrofon i et rum er markant sværere: rumklang, folk der taler i munden på hinanden, kollegaer med samme stemmeleje ender i samme klynge. Der er manuel rettelse ikke en undtagelse, men en fast del af arbejdsgangen.

**Overlappende tale bliver aldrig løst korrekt i denne arkitektur. Accepter det.**

---

## 4. Eksport = det egentlige produkt

Da referatet arbejdes igennem i Claude bagefter, er eksporten produktet.

Markdown med **kontekstblok øverst** — mødetype, dato, varighed, deltagerliste med roller — derefter transskriptionen som:

```
**Navn** [12:34]: tekst
```

Kontekstblokken er det der får Claude til at levere et brugbart referat i stedet for et generisk resumé, og koster ti sekunders udfyldning.

**Fase 3 (auto-resumé) beholdes** — besluttet 2026-08-07. Eksport-markdownen er stadig en selvstændig vej ud af appen: du kan enten lade appen lave resuméet, eller kopiere eksporten ind i Claude og arbejde den igennem manuelt. De to udelukker ikke hinanden.

---

## 5. De 10 ekstra features, indplaceret

### Byg med det samme (ændrer produktet fundamentalt)

| # | Feature | Hvorfor | Fase |
|---|---|---|---|
| 1 | **Live-noter under mødet** | Granolas kerneidé og den enkeltfeature der løfter kvaliteten mest. Stikord mens mødet kører, hver note tidsstemplet, flettes ind i transskriptionen på rette sted ved eksport. Din opmærksomhed i mødet er bedre kontekst end nogen model — den ved hvad der var vigtigt. Tekstfelt + tidsstempling, en dags arbejde. | 1 |
| 2 | **Global hotkey "markér nu"** | Én tast = bogmærke. Bruges når nogen siger noget vigtigt og du ikke kan nå at skrive. Markeringer bliver til overskrifter i eksporten. Trivielt at bygge, bruges konstant. | 1 |
| 3 | **Ordliste / custom vocabulary** | Whisper tager `initial_prompt` — fyld den med IAM-termer, kundenavne, kollegaers navne, produktnavne. "Entra ID", "SCIM", "Zitadel", "provisionering" bliver ellers volapyk på dansk. Billigste kvalitetsgevinst i hele projektet. | 0 (test) → 2a |
| 4 | **Chunked autosave** | WAV i 30-sekunders segmenter frem for én stor fil. Crash eller fladt batteri efter 70 min koster 30 sekunder, ikke hele mødet. Ikke en feature — en forudsætning for at turde bruge værktøjet til noget der betyder noget. | 1 |
| 5 | **Afspilning synkroniseret med transskript** | Klik på en sætning, hør lyden. Uden det kan du ikke verificere om Whisper hørte "skal ikke" eller "skal" — og så tør du ikke sende referatet videre. Afgørende når outputtet bruges professionelt. | 4 |

### Byg i v1.1

| # | Feature | Note |
|---|---|---|
| 6 | **Søgning på tværs af møder** | SQLite + FTS5 over alle transskriptioner. Værdien vokser med arkivet, så det haster ikke i MVP — **men indlæs metadata i en database fra dag ét**, så mappestrukturen ikke skal migreres senere. |
| 7 | **Stemmeprofiler på tværs af møder** | Gem klynge-centroiderne fra navngivne talere, match mod dem næste gang. Sparer manuel navngivning i de 80% af møder med de samme mennesker. |
| 8 | **Auto-detect mødestart** | Har `ms-teams.exe` aktiv lydsession → diskret notifikation "Møde i gang — start optagelse?". Fjerner fejlen hvor du husker det tyve minutter inde. Kalenderintegration er den finere version; procesdetektion koster ingenting. |
| 9 | **Taletidsfordeling** | Procent pr. deltager. Lyder som pynt, men er reelt signal i konsulentsammenhæng — talte du 70% i et behovsafdækningsmøde, gjorde du det forkert. |

### Byg det ikke

10. **Kapitel-/emneopdeling.** Automatisk emnesegmentering på dansk er upålidelig, og dine egne markeringer (#2) gør det bedre. Samme gælder sentiment-analyse, filler-word-fjernelse, "engagement scores" og integrationer til Notion/Slack/CRM — features der findes fordi SaaS-produkter skal retfærdiggøre en abonnementspris, ikke fordi de gør referatet bedre.

**Hvis kun én:** #1 Live-noter. Det er forskellen mellem et transskriptionsværktøj og et mødeværktøj. Alle de andre gør transskriptionen pænere; den gør referatet rigtigt.

---

## 6. Faseplan

### Fase 0 — Feasibility gate (2-3 timer, før alt andet)
Optag et rigtigt Teams-møde: mikrofon + loopback som to WAV-filer. Kør begge gennem whisper.cpp i `medium` og `large-v3` med `--language da`. Test samtidig ordliste (#3) via `initial_prompt`.

Vurdér: er navne, fagtermer og forkortelser brugbare, og kan en LLM lave et retvisende resumé af det. **Mål transskriptionstiden i forhold til mødets længde** — over 1:1 på din hardware ⇒ planlæg det som natjob frem for noget du venter på.

### Fase 1 — Optagelse (uge 1)
.NET 8 + WPF (WinUI 3 hvis moderne look ønskes, men WPF er hurtigere at komme i gang med). NAudio til begge streams. Skriv til to WAV-filer med **fælles starttidsstempel**.

Indhold: start/stop, timer, niveaumålere pr. spor, liste over optagelser, **chunked autosave (#4)**, **live-notefelt (#1)**, **global hotkey (#2)**.

Test: 90 minutters møde, skift lydenhed midtvejs (headset til/fra), maskinen går i dvale.

### Fase 2 — Transskription + diarisering (2 uger)
- **2a Transskription:** Whisper.net (.NET-bindings til whisper.cpp). Begge spor hver for sig, segmenter flettes på tidsstempel til én transskription med talerlabels. Baggrundsjob med progress. CUDA-runtime hvis NVIDIA-GPU. Ordliste (#3) i `initial_prompt`.
- **2b Diarisering:** embeddings + agglomerativ klyngning på loopback-sporet (afsnit 3).
- **2c Navngivnings-UI:** navngiv, flet, ret enkeltsegment, autocomplete.

### Fase 3 — Auto-resumé (uge 3) — BESLUTTET: BEHOLDES
Transskription til Claude via API med fast skema: **resumé, beslutninger, action points med ejer, åbne spørgsmål**. Prompt-skemaet varierer efter mødetype (afsnit 2).

Dette er **det eneste sted data forlader maskinen**. Derfor:
- **Eksplicit valg pr. møde, ikke en global indstilling.** Et afkryds i UI'et før afsendelse, med tydelig visning af hvad der sendes.
- **API-nøgle i Windows Credential Manager**, aldrig i en config-fil eller i kildekoden på NAS'en.
- Eksport-markdown (afsnit 4) bevares som selvstændig vej — auto-resuméet erstatter den ikke. Du kan stadig arbejde referatet igennem manuelt bagefter.

API-detaljer der gælder for den model der er default i dag (`claude-opus-5`):
- **Struktureret output** via `output_config.format` med et JSON-skema for de fire felter — ikke fritekst der skal parses, og ikke prefill (prefill giver 400 på denne model).
- **Adaptiv tænkning** er slået til som standard. `max_tokens` dækker tænkning + svar tilsammen, så sæt den rundhåndet — et snævert loft klipper svaret midt over.
- **`temperature`, `top_p` og `top_k` afvises med 400.** Styr output med prompten, ikke med sampling-parametre.
- **Stream** ved høje `max_tokens`, ellers rammer kaldet HTTP-timeout.
- Håndtér `stop_reason == "refusal"` **før** du læser indholdet — svaret kan være tomt.

Et 90-minutters møde bliver en stor transskription; tjek token-antallet med `count_tokens` mod `claude-opus-5` frem for at gætte, inden prisen vurderes.

### Fase 4 — Output og lagring (uge 4)
Alt i `%LOCALAPPDATA%\<app>\` — lydfil, transskription, `meeting.json` med metadata. Markdown-eksport og kopiér-knap. **Afspilning synkroniseret med transskript (#5).** SQLite-metadata fra dag ét (forberedelse til #6), men ingen tung DB i v1 — mappestruktur pr. møde rækker til indholdet.

### Fase 5 (v1.1)
#6 søgning, #7 stemmeprofiler, #8 auto-detect, #9 taletidsfordeling.

---

## 7. Udenfor scope (v1)
Teams-kalenderintegration, auto-start ved mødestart, live-transkription, søgning på tværs, redigering af transskript-tekst, installer og signering.

---

## 8. Levering
`dotnet publish -r win-x64 --self-contained` → én mappe du selv kører. Ingen signering, ingen MSIX, ingen Store.

**Placering: `C:\NoteApp\` — lokal disk.** Projektet lå kortvarigt på NAS'en. Fase 0-målingerne 8. august 2026 lukkede den mulighed:

| Måling | NAS over SMB | Lokal SSD |
|---|---|---|
| Indlæsning af `ggml-large-v3.bin` (2,9 GB) | **248 sek** | **4,9 sek** |
| Gennemløb | 13 MB/s | — |

50 gange langsommere, og det er en omkostning der betales **hver gang** en transskription starter. NAS'en er ikke direkte-tilsluttet-hurtig i praksis, uanset kabling.

**NAS'en bruges som backup:** et bart git-repo på `\\NAS_HOME\home\Drive\Vagtsom\NoteApp.git`. `git push` efter hver arbejdssession. Det giver durabiliteten uden at betale SMB-prisen ved hver kørsel.

Modellerne (4,4 GB) er bevidst uden for versionsstyring — de hentes fra Hugging Face med scriptet, ikke fra backup.

Runtime-stier (`%LOCALAPPDATA%` i afsnit 6, Fase 4) ændres ikke af dette. Optagelser skrives fortsat lokalt under mødet; et netværksudfald må aldrig kunne afbryde en igangværende optagelse.

---

## 9. Hardware — de to åbne spørgsmål er besvaret

Målt på maskinen 2026-08-07/08:

| | |
|---|---|
| GPU | **NVIDIA GeForce RTX 2060**, 6143 MiB VRAM, driver 591.44 (+ Intel Iris Xe) |
| CPU | Intel Core i7-1165G7, 4 kerner / 8 tråde |
| RAM | 32 GB |
| .NET SDK | **8.0.423 installeret** 8. august 2026 |
| whisper.cpp | v1.9.2, CUDA 12.4-build, `C:\NoteApp\tools\whisper\` |
| Modeller | `ggml-large-v3.bin` (2,9 GB) + `ggml-medium.bin` (1,4 GB), `C:\NoteApp\models\` |

**`large-v3` er afgjort — den kører.** Ikke som skøn, men målt: whisper.cpp rapporterer `CUDA0 total size = 3094.36 MB` af de 6143 MiB, og encoderen bruger 208 ms pr. 30-sekunders vindue. Der er margin nok til at compute-bufferne (ca. 200 MB) også får plads. Ingen grund til at falde tilbage på `medium` af hensyn til VRAM — vælg den kun hvis kvalitetsforskellen på dansk viser sig at være lille nok til at det ikke betaler sig.

**Modellen skal ligge lokalt.** Se afsnit 8: 4,9 sek indlæsning lokalt mod 248 sek fra NAS.

**Det udestående tal er RTF på et rigtigt møde.** Røgtesten kørte på 12 sekunders lyd, hvor modelindlæsningen dominerer totalen — den siger at kæden virker, ikke hvad 90 minutter koster. Det måles i Fase 0 trin 2.

---

## 10. Lokal læring — arkitekturbeslutning

Appen skal blive bedre til navne og fagtermer, efterhånden som du retter dem. Den læring må **ikke** ligge i modellen, men i et datalag ved siden af. Fuldt design: `doc\laering-og-vedligehold.md`. Skema: `src\schema\learning.sql` (valideret 10. august 2026).

Tre regler der er strukturelle og ikke kan eftermonteres:

1. **Rettelser skrives aldrig ind i den rå transskription.** Samme princip som talernavne i afsnit 3. Rå tekst er hvad motoren sagde; rettelser er et lag ovenpå; visningen er summen. Det er dét, der gør Whisper udskiftelig.
2. **`learning.db` indeholder ingen modelartefakter.** `engine_id` er proveniens, aldrig en betingelse for om en rettelse må bruges. En rettelse lært under `large-v3` gælder også under efterfølgeren.
3. **Ingen finjustering af Whisper.** Det ville binde al læring til én basismodel, og hver opdatering ville koste alt. Det er præcis dét, arkitekturen findes for at undgå.

Bemærk at der er **to modeller med hver sin begrænsning**: Whisper har ca. 224 tokens til ordlisten og begynder at hallucinere prompten ind i transskriptionen hvis den overfyldes, mens Claude i Fase 3 kan få hele ordbogen med uden risiko. Samme datalag, to forskellige udtræk.

**Sidegevinst:** arkivet af rettelser bliver en regressionstest for Whisper-opdateringer. Kør gamle møder gennem en ny version og tæl, hvor mange gemte rettelser der ikke længere er nødvendige — det måler forbedringen på dit eget domæne i stedet for på en generisk benchmark. Stiger tallet i stedet, er opdateringen en regression.

---

## 11. Juridisk note
Loopback-optagelse fanger de andre deltageres lyd **uden at Teams signalerer det**. Til egne møder på egen maskine er det uproblematisk, men **oplys deltagerne** — og skal outputtet nogensinde ind i en kundeleverance, er det et krav, ikke en høflighed.
