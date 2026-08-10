# Lokal læring — hvordan appen bliver klogere uden at blive låst

## Spørgsmålet

Kan appen lære af de ord, den misforstår, så den bliver bedre over tid — og kan den læring overleve, at Whisper opdateres?

Det korte svar: **ja, men ikke ved at træne Whisper.** Læringen skal ligge i et datalag ved siden af modellen, ikke i modellen. Gør man det, kan Whisper skiftes ud når som helst, og læringen følger med uændret. Gør man det modsatte — finjusterer modellen — er hver opdatering et tab af alt, hvad appen har lært.

---

## Der er to modeller, ikke én

Det er værd at holde adskilt, fordi de har vidt forskellige begrænsninger.

| | **Whisper** (tale → tekst) | **Claude** (tekst → referat) |
|---|---|---|
| Hvad den lærer af | En ordliste i `initial_prompt` | En ordliste + kontekstblok i prompten |
| Hård grænse | **Ca. 224 tokens.** Prompten deler kontekstvindue med selve transskriptionen | Praktisk talt ingen — hele ordbogen kan sendes med |
| Hvad der sker ved for meget | Whisper begynder at **hallucinere prompten ind i transskriptionen**, især i stilhed | Ingenting. Den ignorerer bare det irrelevante |
| Hvor svært er det at fejle | Let | Svært |

**Whisper er flaskehalsen.** Ordbogen kan vokse ubegrænset, men det, der sendes til Whisper for et givet møde, skal vælges — de mest relevante 200 tokens, ikke alle 4000. Claude får hele molevitten.

Det betyder også, at den samme ordbog tjener to formål med to forskellige udtræk. Ét datalag, to forbrugere.

---

## Hvor godt kan man egentlig "træne" undervejs?

Tre mekanismer, rangeret efter hvad de er værd i praksis:

### 1. Ordliste i Whispers prompt — virker med det samme, men er loftbegrænset

Whisper konditionerer på prompten og bliver mærkbart bedre til navne og fagtermer, den ellers ikke kender. Gevinsten kommer øjeblikkeligt, uden nogen form for træning.

Men den er **ikke kumulativ**. Har du 400 termer i ordbogen og kun plads til 200 tokens, skal der vælges. Og vælger man forkert — fylder prompten med termer, der ikke optræder i mødet — bliver det aktivt værre, fordi Whisper begynder at skrive prompten ind i transskriptionen under pauser.

Derfor bygges prompten **pr. møde**, ikke én gang for alle: deltagere og kunde først, derefter de termer der faktisk optrådte i de seneste møder, derefter globale højvægtstermer, indtil budgettet er brugt.

### 2. Rettelseslag — det egentlige svar, og det der bliver ved med at virke

Hver gang du retter et ord i appen, gemmes parret: *hørt form → rigtig form*. Næste gang samme fejl optræder, kan appen rette den selv.

Det her er ikke modeltræning, men det er den mekanisme, der bærer værdien:

- **Den er ubegrænset.** Ordbogen må gerne indeholde tusind termer.
- **Den er monotont forbedrende.** Ny viden kan ikke gøre gammel viden dårligere.
- **Den er fuldstændig uafhængig af Whisper.** Den kender kun tekst.
- **Den er læselig og redigerbar.** Du kan åbne den, se hvad appen tror, og rette den. Modelvægte kan man ikke inspicere.

Efter tyve møder med de samme mennesker og de samme systemer vil den dække langt det meste af det, der går galt.

### 3. Finjustering af Whisper — frarådes, og det er en arkitekturbeslutning, ikke en smagssag

Det kan lade sig gøre at finjustere Whisper på egne lydoptagelser, og gevinsten på et snævert domæne kan være reel. Men:

- Det kræver **timevis af transskriberet lyd**, ikke et par møder.
- Resultatet er en **ny modelfil, bundet til den basismodel, den blev trænet fra**. Kommer der en bedre Whisper, starter du forfra.
- Fejl i træningsdata bliver til fejl i vægtene, og de kan ikke rettes — kun trænes væk.

**Det er præcis dét, spørgsmålet handler om at undgå.** Derfor: ingen finjustering. Al læring i datalaget.

---

## Arkitekturen der giver garantien

Den bærende regel er den samme, som allerede gælder for talernavne i speccens afsnit 3:

> **Rettelser skrives aldrig ind i den rå transskription.**

Den rå transskription er, hvad motoren sagde. Rettelserne er et lag ovenpå. Visningen er de to lagt sammen.

```
   Lyd (WAV)                    uforanderlig, kilden til alt
      |
      v
   [ ITranscriptionEngine ]     udskiftelig: whisper.cpp i dag,
      |                         noget andet i morgen
      v
   Rå transskription            regenerérbar, må gerne smides væk
   + engine_id                  "whisper.cpp/large-v3/v1.9.2"
      |
      +---- Rettelseslag  <---- learning.db     ALDRIG udskiftelig,
      |                                          kender intet til modeller
      v
   Vist transskription          rå + rettelser, beregnet ved visning
      |
      v
   Eksport / referat
```

### Hvad garantien konkret består i

1. **`learning.db` indeholder ingen modelartefakter.** Kun tekst: kanoniske termer, hørte varianter, rettelsespar, vægte. Filen kan kopieres til en anden maskine med en anden Whisper-version og virker uændret.

2. **`engine_id` er proveniens, ikke en nøgle.** Hver transskription noterer hvilken motor der lavede den, og hver rettelse noterer hvilken motor der lavede fejlen. Det bruges til statistik — ikke til at afgøre, om en rettelse må anvendes. En rettelse lært under `large-v3` gælder også under efterfølgeren.

3. **Motoren sidder bag en grænseflade.** `ITranscriptionEngine` tager lyd ind og giver segmenter ud. Hvad der sker indeni, er ligegyldigt for resten af appen. Skift til en nyere whisper.cpp, til faster-whisper eller til noget helt tredje rører ikke ved læringen.

4. **Rå transskriptioner er regenerérbare.** Lyden er kilden. Kommer der en bedre model, kan gamle møder køres igennem igen, og rettelseslaget lægges ovenpå på ny.

### Bonussen: rettelserne bliver en regressionstest for Whisper-opdateringer

Det her er den del, der gør garantien til noget mere end sikkerhed.

Når en ny Whisper-version kommer, kør et udvalg af gamle møder igennem igen og tæl, hvor mange af de gemte rettelser der **ikke længere er nødvendige** — altså hvor mange fejl den nye model ikke længere laver.

Det tal er et direkte, målbart svar på "er den nye version bedre til mit domæne?" — ikke en generisk benchmark, men målt på din egen lyd, dine egne kollegaer og dine egne fagtermer. Og det virker kun, fordi rettelserne blev gemt adskilt fra transskriptionen.

Samtidig afslører den modsatte retning noget vigtigt: dukker der **nye** fejl op, som den gamle model ikke lavede, er opdateringen en regression, uanset hvad benchmarks siger.

---

## Sikkerhed: automatiske rettelser må ikke ødelægge tekst

Blind søg-og-erstat er farligere end den fejl, den retter. "Skim" er et rigtigt dansk ord. Retter appen automatisk hver forekomst til "SCIM", ødelægger den sætninger, der var korrekte.

Derfor:

- **En rettelse forfremmes ikke automatisk.** Efter tre forekomster foreslår appen: "Skal 'skim' altid rettes til 'SCIM'?" Du siger ja eller nej. Tavshed betyder nej. Det er samme princip som attesteringsreglen: ingen handling er ikke det samme som et ja.
- **Enkeltordsrettelser er strengere end flerordsrettelser.** "Mit id hverv" → "MitID Erhverv" er sikker, fordi sammensætningen er usandsynlig ved et tilfælde. Enkeltord kræver eksplicit accept.
- **Alt, der er rettet automatisk, er markeret i visningen** og kan slås fra med ét klik. Du skal kunne se, hvad appen har ændret, uden at lede efter det.
- **Intet er destruktivt.** Den rå transskription står uændret. Fortryd er altid muligt, også efter måneder.

---

## Vedligehold i praksis

Ordbogen skal kunne passes uden at åbne en database:

- **Ordbogsside i appen** med søgning, kategorier (person, organisation, produkt, fagterm, forkortelse) og antal forekomster.
- **Import og eksport som CSV**, så en liste af kollegaer eller kunder kan hældes ind uden at skrive dem én ad gangen.
- **Ryd op-visning**: termer der ikke er set i et år, aliaser der aldrig blev accepteret, dubletter.
- **Vis hvad der ryger med i prompten** for det næste møde, og hvor mange tokens der er brugt af budgettet. Ellers er det umuligt at fejlsøge, når Whisper pludselig opfører sig underligt.

Det sidste punkt er vigtigere, end det lyder. Prompten er den eneste del af systemet, hvor "mere læring" kan gøre resultatet dårligere. Den skal kunne inspiceres.

### En detalje, valideringen af skemaet afslørede

En term kan findes både globalt og knyttet til en bestemt kunde — `SCIM` generelt, og `SCIM` som den bruges hos en bestemt kunde. Det er med vilje tilladt, fordi den kundespecifikke kan have sin egen vægt og sine egne aliaser.

Men det betyder, at **promptbyggeren skal fjerne dubletter på kanonisk form**, ellers optager samme ord to pladser i et budget, der i forvejen er for lille. Reglen: den kundespecifikke vinder, når mødet handler om den kunde; ellers den globale. Det er tre linjers kode, men det er den slags, der aldrig bliver opdaget, fordi symptomet blot er en prompt, der er lidt dårligere end den kunne være.

---

## Hvad der bygges hvornår

| Del | Fase | Hvorfor dér |
|---|---|---|
| `learning.db` med skema, tom | **Nu, før Fase 1** | Skemaet kan ikke eftermonteres uden at migrere alt |
| `ITranscriptionEngine`-grænseflade | Fase 2a | Første gang der overhovedet er en motor at abstrahere |
| Ordliste bygget fra ordbogen til Whispers prompt | Fase 2a | Erstatter den statiske `ordliste.txt` |
| Rettelser opsamles når du retter i UI'et | Fase 2c | Samme skærm som talernavngivningen |
| Automatisk anvendelse med forfremmelse | v1.1 | Kræver data først — der er intet at forfremme i starten |
| Ordbogsside med import/eksport | v1.1 | |
| Regressionsmåling ved Whisper-opdatering | v1.1 | Kræver et arkiv af rettelser |

Det eneste, der **skal** ligge fast fra starten, er skemaet og reglen om, at rettelser aldrig skrives ind i den rå transskription. Resten kan bygges gradvist.
