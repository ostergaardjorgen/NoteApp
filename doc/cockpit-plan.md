# Cockpit — plan til gennemgang

*Skrevet 21-08-2026. Et oplæg, ikke en beslutning.*

## Essensen, som jeg forstår den

**Transskription er ikke produktet. Det er prisen for at komme ind.**

Værdien vokser med tiden og med mængden: efter to hundrede møder og webinarer er
appen det eneste sted, hvor man kan finde ud af, hvad der *faktisk* blev sagt —
i en dialog for fire måneder siden, på et webinar man halvt husker. Ingen anden
kilde har det. Kalenderen siger, at mødet var der; referatet siger, hvad nogen
huskede bagefter. Udskriften siger, hvad der blev sagt.

Det har to konsekvenser, som resten af planen hænger på:

**Søgningen er ikke en funktion i cockpittet. Den ER cockpittet.** Alt andet —
dagens opgaver, webinarerne, dikteringen — er arrangement omkring den ene ting,
der skal virke.

**Værdien er bagudrettet, men arbejdet er fremadrettet.** Det, der gør appen
uundværlig om et år, er de optagelser, man laver i dag. Derfor skal alt, der
sænker tærsklen for at optage — webinarknappen, den planlagte optagelse — vægtes
højere, end det ser ud på overfladen. Hvert møde, der ikke bliver optaget, er et
hul i det arkiv, produktet lever af.

---

## Den beslutning, der skal træffes først

**Søgningen er i dag ren tekstsammenligning.** `Soegning.Soeg` deler
spørgsmålet i ord og kræver, at *alle* ord står i teksten. Det virker i dag med
fire møder. Det holder ikke til to hundrede.

Grunden er ikke hastighed — den er, at **man ikke husker ordene**. Man husker
«ham fra Norge sagde noget om, at de var underdog i Danmark». Man søger på
«underdog Danmark» og finder det. Man søger på «svag position i Danmark» og
finder intet, selvom det er det samme.

To veje:

| | Leksikalsk (i dag) | Semantisk (indlejringer) |
|---|---|---|
| Finder | de ord, du skriver | det, du mener |
| Koster | ingenting | en model på 100-500 MB, ét gennemløb pr. udskrift |
| Fejler ved | omskrivninger, synonymer, bøjninger | præcise navne og tal — dér er leksikalsk bedre |
| Kan måles | ja | ja |

**Min anbefaling: begge dele, og mål det, før der bygges.** Leksikalsk er
suverænt til «Omada», «Espen», «12 måneder». Semantisk er suverænt til «hvad
sagde de om prissætning». Det rigtige er at køre begge og blande resultaterne —
men det skal afgøres af en måling, ikke af en formodning.

**Målingen:** tyve rigtige spørgsmål til de møder, der allerede ligger, hvor
facit er kendt. Hvor mange finder den nuværende søgning i top tre? Det tal er
udgangspunktet, og uden det kan man ikke vide, om en indlejringsmodel var
pengene værd. Det er en halv dags arbejde og bør gøres før etape 1.

---

## Cockpittets opbygning

```
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│            ┌────────────────────────────────────┐            │
│        🔍  │  Hvad leder du efter?              │  🎤        │
│            └────────────────────────────────────┘            │
│         [ hele tiden ▾ ] [ alle personer ▾ ] [ alt ▾ ]       │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│  I DAG · torsdag den 21. august                              │
│                                                              │
│   09:00  ①  Send tilbuddet til Espen              [ ✓ ]      │
│   10:30  ▶  Møde med Cloudworks              [ optag nu ]     │
│   14:00  ◉  Webinar: Identity Trends 2026    [ optag nu ]     │
│   16:00  ③  Ring til leverandøren                 [ ✓ ]      │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│  OVERSKREDET (2)          DENNE UGE (5)        SENERE (11)   │
└──────────────────────────────────────────────────────────────┘
```

**Søgefeltet er skærmens tyngdepunkt.** Stort, midt på, det første øjet møder.
Ikke et felt i et hjørne.

**Én tidslinje for i dag — ikke tre kasser.** Møder, webinarer og opgaver med et
tidspunkt er alle sammen *ting, der sker på et klokkeslæt*. At give dem hver sin
kasse tvinger brugeren til at læse tre lister og selv flette dem sammen. Det er
den eneste af mine afvigelser fra oplægget, jeg vil argumentere for.

**Opgaver uden tidspunkt ligger under**, grupperet efter, hvor galt det står til.

---

## De fire områder

### 1 · Søgning

**Justeringer, som du beskrev dem:**

- **Periode** — hele tiden · denne uge · denne måned · i år · et selvvalgt spænd
- **Personer** — de navne, der findes på tværs af optagelser. De er der allerede
  i `meeting.json` under `Talere`
- **Type** — optagelser · webinarer · dokumenter · opgaver · noter

**Ensartet markering.** Den tværgående søgning viser i dag uddraget som ren
tekst. Udskriftssøgningen markerer med gult. Det er den samme kode — `Dele()`
og `Fremhaev_Ind` i `UdskriftView` — og den skal genbruges. **Det er den
billigste vinding i hele planen** og bør laves med det samme.

**Sammenhæng om træffet.** Udskriftssøgningen viser replikken før og efter i
blokke med «TRÆF 3 AF 6». Den tværgående viser et uddrag på én linje. Den samme
grund gælder begge steder: man kan ikke huske et ord uden det, der stod omkring.

### 2 · Webinarer

**Det væsentlige er ikke listen. Det er, at appen selv siger til.**

Man tilmelder sig et webinar tre uger før og har glemt det, når det starter.
Optagelsen, der aldrig blev lavet, er et hul i arkivet — og arkivet er
produktet.

- Læg webinaret ind: titel, dato, tidspunkt, sprog
- Appen melder sig, når det nærmer sig, med **én knap: optag**
- Sproget er valgt på forhånd, så der ikke skal svares på noget, mens det
  begynder

**Ét spor, og det skal forklares — for det er ikke bare hurtigere.**

Et webinar er envejs. Du lytter; du taler ikke. Optages kun højttalersporet:

| | To spor (møde) | Ét spor (webinar) |
|---|---|---|
| Fylder | 220 MB/time | 110 MB/time |
| Skrives ud på | ~9 minutter | ~4½ minutter |
| Ekko fra din mikrofon | skal fjernes | findes ikke |
| Talergenkendelse | to sider at holde adskilt | ren lyd, én kilde |

Det sidste punkt er det, der sjældent siges: uden dit eget spor er der intet
ekko, ingen overlappende tale og ingen tvivl om, hvilken side der talte.
**Udskriften bliver ikke bare hurtigere — den bliver bedre.**

Derfor skal knappen forklare det, første gang den bruges. Ellers trykker folk på
den almindelige optageknap, fordi den er den, de kender.

**Den skal kunne optage uden dig.**

Det er den egentlige pointe, og den er større end påmindelsen: *man får sjældent
set et webinar, man ikke prioriterer på tidspunktet.* Netop dér går viden tabt —
ikke i de webinarer, man deltager i, men i dem, man vælger fra.

Så: sæt det op i forvejen, gå til noget andet, og få udskriften bagefter.

Det kræver, at optagelsen kan **slutte af sig selv**:

- Kommer der ikke lyd på webinarsporet i **fem minutter**, stopper den
- De fem minutters stilhed **klippes af**, før optagelsen gemmes
- Det, der står i listen bagefter, er webinaret — ikke webinaret plus et kvarters
  tomhed, fordi ingen lukkede vinduet

Fem minutter er ikke tilfældigt: et webinar har pauser, en oplægsholder kan
tie, mens et videoklip loader, og en optagelse, der stopper efter tredive
sekunders stilhed, ville skære midt i. Tallet skal måles, når funktionen findes.

**Original og oversættelse ved siden af hinanden.**

Webinarer er tit på engelsk. Man vil have essensen på dansk — men man vil kunne
slå det efter i originalen, for det er dér, fagordene står rigtigt, og det er
originalen, man kan citere fra.

Udskriften skal derfor kunne vises begge veje: **originalsproget** som det blev
sagt, og **dansk** ved siden af. Oversættelsen kan laves lokalt af den samme
sprogmodel, der laver opsummeringen — det er en opgave, den er god til, og det
holder webinaret på maskinen.

*Bemærk:* whisper kan selv oversætte, men **kun til engelsk**. Dansk skal komme
fra sprogmodellen bagefter. Det er et ekstra gennemløb, ikke et flag.

### 3 · Opgaver

**Alle opgaver, på tværs.** De ligger i dag i `opgaver.json` ved hver optagelse.
Cockpittet samler dem og lader dig oprette nye direkte.

**Farver efter hvor galt det står til:**

| | |
|---|---|
| 🔴 rød | fristen er overskredet |
| 🟡 gul | forfalder i dag eller i morgen |
| 🟢 grøn | god tid |
| ⚪ grå | ingen frist sat |

**Prioritet ① ② ③** med cirkeltal. Tegnene findes i Unicode (`①`) og
kræver ingen skrifttype, der skal hentes.

Alt starter på **③**, som du foreslår. Siger man «det her er vigtigt», ryger den
til **①**. Det er rigtigt — men det har en konsekvens, der skal med i designet:
**når næsten alt er 3, er prioriteten ikke en sorteringsnøgle, den er et
filter.** Listen sorteres efter dato; ①'erne er dem, man kan trække frem alene.
Ellers bliver tallet støj.

**En opgave skal kunne pege tilbage på, hvor den kom fra.** Med tasks fra to
hundrede møder siger «send tilbuddet» ingenting uden mødet og replikken. Feltet
`Kilde` har tidsstemplet i dag; det skal have mødet med og et klik, der åbner
netop den replik.

**Opgaver, der starter en optagelse.** En opgave kan være «optag mødet med
Cloudworks kl. 10:30» eller «optag webinaret». Så er det den samme mekanisme som
webinarpåmindelsen, bare med en anden anledning.

**Kalenderen.** En rigtig månedsvisning, ikke et tekstfelt. Den skal kunne
betjenes med tastaturet, og «i morgen» og «på fredag» skal kunne skrives
direkte i feltet — det er den hurtigste vej for den, der ved, hvad han vil.

### 4 · Diktafonen

**To ting, den skal kunne.**

**Spørg om dagen.** «Hvad er vigtigt i dag?» → dagens tidslinje læses op og
vises. «Hvad er vigtigt i næste uge?» → samme for perioden. Det er en
forespørgsel med et tidsrum i, og den skal forstå de almindelige måder at sige
det på.

**Diktér en uges opgaver.** Du remser op; appen laver opgaver; **listen vises,
før noget gemmes.** Du retter tekst og datoer, tilføjer flere — i hånden eller
med stemmen — og godkender.

**Det gennemsynstrin er dét, der gør det forsvarligt**, og det er værd at sige
hvorfor: målingen 21-08 viste, at fri dansk diktering ikke er præcis nok til at
stole blindt på. Kommandoer kan gøres sikre med en lukket liste; en fri sætning
kan ikke. Men en liste, man selv har set igennem og rettet, behøver ikke være
rigtig i første forsøg. **Du har designet fejlen ud af det.**

---

## Det, der skal bygges nedenunder

Fire ting, som flere områder deler. De skal bygges én gang og bygges rigtigt.

| Byggesten | Bruges af | Bemærkning |
|---|---|---|
| **Dansk dato- og tidsforståelse** | opgaver, diktering, søgefiltre, webinarer | `Microsoft.Recognizers.Text` kan **ikke** dansk — efterprøvet. Skal skrives i C#: «i morgen», «på fredag», «om to uger», «den 3. marts», «kl. halv tre». Deterministisk og enhedstestbar |
| **Et register over opgaver på tværs** | opgaveområdet, tidslinjen, diktering | Læser `opgaver.json` fra alle optagelser plus cockpittets egne |
| **En planlægger** | webinarer, planlagte optagelser | Skal virke, når appen er lukket → Windows' opgavestyring, ikke en timer i appen |
| **Enspors-optagelse** | webinarer | Optageren tager to spor i dag. Skal kunne tage ét |

---

## Rækkefølge — og hvorfor

### Etape 0 · Cockpittet findes *(1-2 dage)* — GJORT 21-08-2026

Nyt menupunkt øverst. Søgningen flyttet derop og gjort til skærmens
tyngdepunkt. **Gul markering i den tværgående søgning** — samme kode som
udskriften.

*Hvorfor først:* det er den billigste vinding, der findes, og det gør skærmen
til det sted, resten kan bygges ind i.

### Etape 1 · Optag et webinar *(2-3 dage)* — GJORT 21-08-2026

**Rykket frem.** Ikke hele webinarområdet — kun det, der skal til for at optage
ét i dag og begynde at samle materiale:

- En webinarknap, der optager **ét spor** og spørger om sproget først
- Forklaringen af hvorfor, første gang den bruges
- **Stop af sig selv** efter fem minutters stilhed, med stilheden klippet af

Registeret, påmindelsen og oversættelsen kommer i etape 4.

**Afprøvet på to rigtige webinarer samme dag**, 22 og 54 minutter. Begge
stoppede selv, stilheden blev klippet af, og udskrivningen tog 0,09 gange
lydens længde. Talergenkendelsen fandt 1 stemme på det første og 4 på det
andet.

**Fem fejl, som kun kunne findes ved at bruge den:**

| Fundet | Rettet i |
|---|---|
| Et webinar kunne slet ikke skrives ud — koden gik ud fra, at `mikrofon.wav` altid fandtes | v1.0.54 |
| Hele optagelsen blev til ét afsnit med ét tidsstempel, når der kun var ét spor | v1.0.54 |
| «Du kan roligt slå lyden fra» — gjaldt kun Windows' lydstyrke, ikke afspillerens mute. Kostede to minutter af et rigtigt webinar | v1.0.53 |
| Tom fane uden forklaring, når en optagelse ikke var skrevet ud | v1.0.54 |
| Opsummeringen bad om beslutninger og aftaler på et webinar | v1.0.55 |

**Kom med undervejs, uden for planen:**

- **Mappe, mødetype og sprog vælges FØR optagelsen** — én dialog til både møde
  og webinar. Det ændrer hele dokumentflowet: mødetypen er valgt på forhånd,
  når man laver et dokument.
- **Skabeloner hedder mødetyper** alle de steder, brugeren læser det
- **Mødetypen «Webinar»** — læringsmål, 5-10 takeaways med tidspunkter,
  fagudtryk på originalsproget, skel mellem undervisning og salg
- **Sproget vælges pr. dokument** — dansk eller engelsk, ikke låst i skabelonen
- **Mødevagten**: appen kan spørge, når et andet program åbner mikrofonen. Målt
  til at skelne et møde fra et webinar, en video og en streamingtjeneste

*Hvorfor før søgningen:* værdien er bagudrettet, men arbejdet er fremadrettet.
Hvert webinar, der ikke bliver optaget i denne uge, er et hul i det arkiv,
søgningen senere skal lede i. En bedre søgning i fire møder er mindre værd end
en middelmådig søgning i fyrre.

### Etape 2 · Søgningen bliver god *(1-2 uger)* — GJORT 22-08-2026

Først **målingen**: tyve spørgsmål, hvor facit er kendt, mod det nuværende
system. Derefter filtrene — periode, person, type. Og først derefter beslutningen
om semantisk søgning, truffet på tallet.

*Hvorfor nummer to:* det er produktet. Alt andet er arrangement omkring det.

**Gjort — men i den omvendte rækkefølge, fordi fejlene viste sig ved brug:**

Søgningen fandt stederne på ÉT af ordene og brugte resten til at snævre ind.
«access indigo» gav otte steder med Indigo og ikke ét med access, selv om
ordet stod i udskriften 27 gange. Nu findes hvert sted for hvert ord, og
stederne vejes efter, hvor mange af ordene der står inden for hundrede tegn.
Kun de bedste steder vises.

Målt på to rigtige webinarer, andel af viste steder med alle søgeordene:

| Søgning | Før | Nu |
|---|---|---|
| orphaned accounts | 50 % | 100 % |
| access review | 26 % | 100 % |
| identity governance | 17 % | 100 % |

**Filtrene er lavet:** periode, mappe, mødetype og sprog. Perioden har faste
valg — i dag, denne uge, denne måned, i år — og en fra-til med to datovælgere.
Kun **person** mangler; den kræver talernavnene, som sættes i hånden.

**MÅLINGEN ER LAVET 22-08-2026.** Tyve spørgsmål med kendt facit, som kan
køres igen med `noteapp maalsoegning`:

| Mål | Resultat |
|---|---|
| Facit på førstepladsen | 18 af 20 — **90 %** |
| Facit i top tre | 20 af 20 — **100 %** |
| Slet ikke fundet | 0 |
| Tid i gennemsnit | 4,8 ms |

Den fandt to fejl: dokumenter lå foran den transkription, de var lavet af, og
«i år» begyndte 31-12 kl. 23 på grund af sommertid. Begge rettet.

**Færdiggjort samme dag:** faner over resultatet efter type, sortering på
dato, relevans og mødetype — og «udskrift» hedder nu «transkription» alle de
steder, brugeren læser det. Ordet blev læst som noget på papir.

**Det, tallet IKKE svarer på:** alle tyve prøver er ord, der STÅR i teksten,
og det er netop dét, ordsøgning er god til. Beslutningen om semantisk søgning
kræver tyve spørgsmål stillet med ANDRE ord end dem, der blev sagt — «hvad
sagde de om prissætning». Den måling kan først laves ærligt, når arkivet er
stort nok til, at man ikke selv kan huske svaret. Den venter derfor, og det er
et bevidst fravalg — ikke en glemt opgave.

### Etape 3 · Opgaver på tværs *(1 uge)* — HALVT GJORT 22-08-2026

Registeret, tidslinjen for i dag, farver og prioritet, kalenderen, tilbagelinket
til replikken. Kræver datoforståelsen, som bygges her.

**Gjort:**

- **Dansk datoforståelse.** `Microsoft.Recognizers.Text` kan fjorten sprog, og
  dansk er ikke et af dem — skrevet i hånden. Målt mod tyve kendte svar,
  regnet fra en fast onsdag: **20 af 20 rigtige, 0 forstået forkert, 0
  opfundne datoer.** Køres med `noteapp maaldato`. Tvetydige vendinger som «i
  næste uge» sættes alligevel, men mærkes usikre med et spørgsmålstegn — en
  frist, appen har gættet, må ikke se ud som en, nogen har sagt.
- **Fristen læses ud af det, der blev sagt**, når en opgave oprettes fra et
  forslag. Skulle den sættes i hånden bagefter, ville den ikke blive sat.
- **Registeret på tværs** af alle optagelser. Hver optagelse beholder sin egen
  opgavefil ved siden af lyden — ingen central fil, der kan blive uenig med
  virkeligheden.
- **Prioritet 1-3 og farver.** Farven kommer fra FRISTEN, ikke fra
  prioriteten: en etter om tre uger haster ikke i dag, en treer fra i mandags
  gør.
- **Cockpittet i tre spalter.** Midten er søgningen. Til venstre de seneste
  optagelser med gul kant, hvis de aldrig blev skrevet ud; til højre
  opgaverne og pladsen til kalenderen.
- **Tilbagelinket til replikken** — herkomsten under hver opgave er et link
  til det sted i transkriptionen, opgaven kom fra.

**Kalenderen kom 23-08-2026** (v1.0.64 og v1.0.65) og lukkede etapen:

- **Én liste til egne og hentede aftaler.** Hvem der har lagt en aftale ind, er
  ikke det, man leder efter, når man skal optage om fem minutter.
- **Aftalens egne valg følger med i optagelsen** — mødetype, mappe og sprog er
  allerede valgt, dengang aftalen blev lavet, og skal ikke vælges igen i det
  minut, mødet begynder.
- **Google Kalender med ét tryk.** Forbind, log ind, godkend. Der bedes om
  `calendar.readonly`, og der sendes ingenting op.
- **Hentede aftaler afløses ved hver hentning**, men det, brugeren selv har
  sat på dem, bæres over. Ellers ville en hentning nulstille en mødetype, man
  havde valgt.

**Tilbage i etapen: intet.** Den planlagte optagelse — at appen kan starte,
mens den er lukket — hører til etape 4 og står dér.

*Hvorfor før webinarer:* datoforståelsen skal bruges af alt det følgende, og den
er lettest at få rigtig, når den bygges til noget, der kan ses med det samme.

### Etape 4 · Webinarområdet gøres færdigt *(1 uge)*

Registeret over tilmeldte webinarer, påmindelsen, den planlagte optagelse — og oversættelsen med originalen ved siden af. Kræver planlæggeren.

*Hvorfor her og ikke i etape 1:* selve optagelsen er det, der får materiale ind, og
den kom med det samme. Resten — at appen selv siger til, og at den kan starte,
mens man er et andet sted — kræver en planlægger, der virker med appen lukket, og
den er lettere at bygge, når tidslinjen findes at vise resultatet i.

### Etape 5 · Diktafonen *(2-3 uger)*

Kommandostyring efter den målte opskrift, «hvad er vigtigt i dag», og
diktér-en-uges-opgaver med gennemsyn.

*Hvorfor sidst:* den er den sjoveste og den mest usikre. Den hviler på
datoforståelsen fra etape 3 og på opgaveregisteret. Bygges den først, bygges den
oven på noget, der ikke er der.

---

## Det, jeg vil advare imod

**Påmindelsen må aldrig afbryde en optagelse.** Et vindue, der popper op midt i
et møde, er præcis den fejl, der får folk til at slå funktionen fra. Den skal
vente eller lande på klokken.

**At afvise en påmindelse må ikke slette webinaret.** «Ikke nu» betyder ikke
«aldrig».

**Prioritet er kun værd at have, hvis den er sjælden.** Bliver alt til ①, er
tallet væk som oplysning.

**Semantisk søgning skal ikke erstatte den leksikalske.** Den er dårligere til
navne og tal — netop dét, man oftest leder efter i et møde. De skal supplere
hinanden.

**Omfanget er 6-10 uger som beskrevet.** Hver etape skal kunne stå alene og være
værd at bruge i sig selv. Ingen af dem må efterlade halve funktioner på skærmen.

---

## Det, der skal måles

| Hvad | Hvordan | Grænse |
|---|---|---|
| Finder søgningen det, man leder efter | 20 spørgsmål med kendt facit | i top 3 |
| Datoforståelsen | 50 danske formuleringer | ≥ 90 % rigtigt |
| Webinar mod møde | samme lyd, ét mod to spor | tid og størrelse halveret |
| Dikterede opgaver | 20 indtalte opgaver | hvor mange skal rettes i gennemsynet |
| Påmindelsen | fyrer den, når appen er lukket | 100 % |

---

## Integrationer — retningen, besluttet 22-08-2026

**Der kommer flere, og de skal ligge samme sted.** Google Kalender er den
første, Microsoft 365 den næste. Forskellen på dem er kun, hvor man henter en
nøgle og hvilken adresse der bliver spurgt; alt det andet — godkendelsen,
afløsningen ved hver hentning, at aftalerne forsvinder igen, når man slår fra
— er det samme. Skrives den ene som et særtilfælde, bliver den anden det også.

Derfor: **en egen fane under Indstillinger**, en liste over integrationer, og
den samme opsætning for hver. Microsoft står med som «kommer senere» frem for
slet ikke at stå der — den, der bruger Microsoft, skal kunne se, at det er på
vej, og ikke lede efter en indstilling, der ikke findes.

### Hvorfor det ikke bryder løftet om EU

Appens hovedregel er, at data bliver på maskinen, og at det, der skal
behandles i skyen, behandles i EU. En kalenderintegration vender den om:
aftalerne ligger **allerede** hos Google eller Microsoft, og appen henter dem
ned. Der sendes ingenting op.

Har kunden valgt Google Workspace eller Microsoft 365, har de selv taget
stilling til, at mødetitler og deltagere ligger hos en amerikansk leverandør.
Appen ændrer ikke på det; den læser det, der er der.

**Det, der aldrig sendes ad den vej:** lyden, transkriptionerne, noterne og
dokumenterne. Der bedes om ét område — `calendar.readonly` — og det står i
koden, hvor det kan efterprøves. Brugeren ser det desuden på Googles egen side,
når der godkendes; det er ikke appen, der fortæller, hvad den beder om.

### Kalenderen skal virke uden

Det er ikke en overgangsløsning. Målgruppen er studerende, iværksættere og
mindre selvstændige, og en del af dem har hverken Google Workspace eller
Microsoft 365. En kalender, der kræver en konto hos Google for at virke, er
ubrugelig for dem — og så er den ikke en kalender, den er en integration med
en forside.

Egne aftaler og hentede aftaler står derfor i **én liste**. Hvem der har lagt
dem ind, er ikke det, man leder efter, når man skal optage om fem minutter.

### Klient-id'et følger med appen — omgjort 23-08-2026

**Det stod her modsat indtil 23-08-2026:** at klient-id'et skulle hentes af
brugeren selv, ligesom nøglen til sprogmodellen. Det blev prøvet af, og det
holdt ikke. At oprette et cloud-projekt hos Google, slå et API til og lave et
sæt legitimationsoplysninger er en halv times arbejde for en, der kender
ordene. Målgruppen er studerende, iværksættere og mindre selvstændige. En
integration, der begynder dér, bliver ikke brugt — den bliver læst og lukket.

**Nu:** man trykker Forbind, logger ind hos Google, godkender, og forbindelsen
står. Klient-id'et er appens.

Forskellen på det og en API-nøgle er værd at holde fast i, for den er ikke
kosmetisk: en Mistral-nøgle er en **regning**, og misbruges den, betaler
nøglens ejer. Et OAuth-klient-id giver ikke adgang til noget som helst i sig
selv. Der skal en bruger til at logge ind og godkende, og det, der kommer ud,
er et token til DEN brugers egen kalender. Google kalder derfor selv
skrivebordsprogrammer «public clients» og regner med, at id'et kan læses ud af
filen.

Det, der til gengæld skal være styr på, står i
[`google-integration.md`](google-integration.md).
