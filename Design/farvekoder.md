# Farvekoder — HeyPia

*Skrevet 28-08-2026, lagt om 04-09-2026. Alle tal er regnet efter, ikke skønnet.*

Mærkets seks farver ligger til grund. De kan ikke dække en app alene — en app
har dæmpet tekst, deaktiveret tekst, kanter, svæveflader, fire
betydningsfarver og et mørkt tema. Derfor er der to slags farver her:

- **Mærkets egne** bruges, hvor de klarer opgaven
- **Afledte** er bygget ud af mærkets farver — samme kulør, anden lysstyrke —
  hvor mærkets egen ikke kan bære den

Kravene er WCAG 2.1: brødtekst over 4,5:1, kanter om felter over 3:1. Appens
egen brødtekst holdes over 7:1, som er AAA — der læses lange stræk i den.

Tallene nedenfor er mod den **værste** af de to flader (baggrund og panel),
undtagen der hvor farven altid står på én bestemt flade — så måles der mod
den. Prøverne i `TemaTest` afviser en palet, der falder under.

---

## Hvad hver farve BETYDER

Det her er det vigtigste i dokumentet, og det var dét, der manglede.

| Farve | Betyder | Betyder IKKE |
|---|---|---|
| Blå (`Accent`) | tryk her · du står her · feltet har fokus | at det gik godt |
| Grøn (`Godkendt`) | gennemført · forbundet · klar | at man kan trykke |
| Koral (`Optager`) | der optages nu · noget gik galt | pas på |
| Gul (`Advarsel`) | kræver din opmærksomhed | en fejl |

**I det mørke tema var `Accent` `#02C39A` og `Godkendt` `#02C59B`.** To farver,
der ikke er til at skelne. Grøn betød derfor på én gang «tryk her», «du står
her», «den arbejder» og «det gik godt» — og en farve, der betyder fire ting,
betyder ingenting. Accenten er nu blå i begge temaer, og
`Handling_og_succes_er_ikke_den_samme_farve` i `TemaTest` holder de to fra
hinanden.

---

## Mærkets farver

| | HEX | Hvor den kan bruges |
|---|---|---|
| Dyb mørkeblå | `#0D3B66` | **Lyst tema:** accent, knapper, links (10,8:1) — og sidebjælkens flade. **Mørkt:** kun som kulør til afledte, 1,4:1 mod baggrunden |
| Cyan / teablå | `#00A896` | **Kan ikke bære tekst på lyst** (2,98:1). Bruges ikke længere som feltkant — de er gråblå nu |
| Frisk grøn | `#02C39A` | Udgangspunkt for `Godkendt` i begge temaer. **Ikke accent mere** — se afsnittet ovenfor |
| Natblå / skifer | `#101828` | Brødtekst i lyst tema (16,7:1) |
| Isblå | `#F0F4F8` | Udgangspunkt for brødteksten i mørkt tema |
| Ren hvid | `#FFFFFF` | Kort og paneler i lyst tema |

**Det vigtigste forbehold:** en CTA-knap i den friske grønne med hvid skrift
er ulæselig — 2,26:1. Skal den grønne være knapfarve, skal skriften være
natblå (7,84:1).

---

## Lyst tema

Arbejdsfladen er neutral, panelerne hvide, kanterne gråblå — og
**sidebjælken er mørkeblå**. Det er dét ene greb, der giver appen et ansigt.
Før var alt blåt: fladen, kanterne, felterne og knapperne, og når alt er
brandfarve, siger brandfarven ingenting.

**Sidebjælken er dybere end mærkets egen #0D3B66.** Målestokken er det mørke
tema: dér står et hvilende menupunkt på 9,7:1 og et valgt på 10,3:1, og det
er dét, der gør bjælken nem at læse. På mærkets #0D3B66 kunne den lyse palet
ikke nå højere end 7,7 og 6,6 — det valgte punkt skal jo stå på en *lysere*
flade, og så er der ikke plads. En dybere række giver plads til begge dele.
Mærkets blå er stadig `Accent`; den er ikke væk, den er flyttet derhen, hvor
der klikkes.

| Nøgle | HEX | Kontrast | Rolle |
|---|---|---|---|
| Baggrund | `#F6F8FB` | — | arbejdsfladen |
| Panel | `#FFFFFF` | — | kort og bokse |
| PanelKant | `#9BC0E5` | 1,8 | kant om et kort |
| InputKant | `#7E8FA3` | 3,1 | kant om et felt i hvile |
| InputFokusKant | `#2A6FAD` | 5,0 | kant om det felt, der skrives i |
| Svaev | `#EDF2F8` | — | musen over |
| Trykket | `#E3EAF3` | — | knappen nede — og bunden i et skrivefelt |
| Valgt | `#DCE9F6` | — | valgt række |
| Tekst | `#101828` | 16,7 | brødtekst |
| TekstSvag | `#3E5061` | 7,8 | underoverskrifter |
| TekstMeget | `#53687D` | 5,4 | forklarende linjer |
| Slukket | `#728699` | 3,5 | deaktiveret |
| Accent | `#0D3B66` | 10,8 | HANDLING og fokus — knapper, links, markering |
| PaaAccent | `#FFFFFF` | 11,4 | skrift på accenten |
| Optager | `#B83B34` | 5,3 | der optages nu — og noget gik galt |
| PaaOptager | `#FFFFFF` | 5,7 | skrift på den røde |
| Godkendt | `#08765D` | 5,2 | SUCCES — og kun det |
| Advarsel | `#876208` | 5,2 | pas på |
| FejlTekst | `#B83B34` | 5,3 | noget gik galt |
| AccentFlade | `#DEE9F5` | — | tonet kasse, blå |
| GodkendtFlade | `#D6F0E7` | — | tonet kasse, grøn |
| AdvarselFlade | `#F7ECD1` | — | tonet kasse, gul |
| FejlFlade | `#F8E6E5` | — | tonet kasse, rød |
| OptagerFlade | `#FBE4E5` | — | fladen bag «der optages» |
| NavigationFlade | `#092845` | — | sidebjælken — mørkeblå i BEGGE temaer |
| NavigationValgt | `#15476C` | 1,5 | det menupunkt, man står på |
| NavigationKant | `#11375C` | 1,2 | skillelinje og svæveflade i bjælken |
| PaaNavigation | `#F4F8FC` | 9,2 | skrift i sidebjælken |
| PaaNavigationSvag | `#C7DAEC` | 10,5 | version og datamappe i bjælken |
| Dokument | `#7A45C0` | 5,7 | dokumenter — skal kunne skelnes fra accenten |
| Fremhaev | `#F1DFA1` | — | fundet i en søgning |
| PaaFremhaev | `#101828` | 13,4 | skrift på det fundne |

## Mørkt tema

Baggrunden er sænket fra `#101828` til `#0B1220` og panelet fra `#112538` til
`#111C2D`. Ikke for mørkets skyld: de tre flader lå tæt og var alle stærkt
blåmættede, så lange indstillingsskærme blev ét fladt stykke. Nu er trinene
større og kuløren roligere, så dybden gør arbejdet i stedet for kanterne.

| Nøgle | HEX | Kontrast | Rolle |
|---|---|---|---|
| Baggrund | `#0B1220` | — | arbejdsfladen |
| Panel | `#111C2D` | — | kort og bokse |
| PanelKant | `#10497F` | 1,9 | kant om et kort |
| InputKant | `#556D86` | 3,2 | kant om et felt i hvile |
| InputFokusKant | `#73C2FB` | 8,8 | kant om det felt, der skrives i |
| Svaev | `#17283B` | — | musen over |
| Trykket | `#0E1A2B` | — | knappen nede — og bunden i et skrivefelt |
| Valgt | `#1B3A5C` | — | valgt række |
| Tekst | `#EFF5FA` | 15,6 | brødtekst |
| TekstSvag | `#B3C7DB` | 9,9 | underoverskrifter |
| TekstMeget | `#8CABC9` | 7,2 | forklarende linjer |
| Slukket | `#64788A` | 3,7 | deaktiveret |
| Accent | `#73C2FB` | 8,8 | HANDLING og fokus — knapper, links, markering |
| PaaAccent | `#0B1220` | 9,7 | skrift på accenten |
| Optager | `#FF7770` | 6,6 | der optages nu — og noget gik galt |
| PaaOptager | `#0B1220` | 7,2 | skrift på den røde |
| Godkendt | `#30C993` | 8,1 | SUCCES — og kun det |
| Advarsel | `#EFAE31` | 8,8 | pas på |
| FejlTekst | `#FF7770` | 6,6 | noget gik galt |
| AccentFlade | `#0F2E4A` | — | tonet kasse, blå |
| GodkendtFlade | `#0B3C31` | — | tonet kasse, grøn |
| AdvarselFlade | `#523B0F` | — | tonet kasse, gul |
| FejlFlade | `#5A1512` | — | tonet kasse, rød |
| OptagerFlade | `#4A1416` | — | fladen bag «der optages» |
| NavigationFlade | `#0D1524` | — | sidebjælken — mørkeblå i BEGGE temaer |
| NavigationValgt | `#1B3C5C` | 1,6 | det menupunkt, man står på |
| NavigationKant | `#1B2A3E` | 1,3 | skillelinje og svæveflade i bjælken |
| PaaNavigation | `#EFF5FA` | 10,3 | skrift i sidebjælken |
| PaaNavigationSvag | `#A9C0D6` | 9,7 | version og datamappe i bjælken |
| Dokument | `#CB90F0` | 7,2 | dokumenter — skal kunne skelnes fra accenten |
| Fremhaev | `#F4CB23` | — | fundet i en søgning |
| PaaFremhaev | `#0B1220` | 12,0 | skrift på det fundne |

---

## Fem ting, der gik galt undervejs

De står her, fordi de er nemme at gå i igen.

**Et minimum er ikke et mål.** Første udkast søgte «mindst 4,5:1», og da
mærkets mørkeblå allerede lå på 11,45, stoppede søgningen med det samme —
PanelKant, TekstSvag, TekstMeget og Slukket blev **alle** den samme farve.
Prøven sagde ja, og der var intet trin tilbage mellem dem. Nu sigter hver
nøgle mod sit eget tal.

**Kravet gælder begge flader.** Der blev først målt mod panelet alene, og så
lå alt en anelse for lavt mod baggrunden. Der måles nu mod den værste af de
to.

**Sekundær tekst må ikke ligne et link.** De dæmpede tekstfarver blev til
klar blå — `#12508A` og `#1768B3` — og så læste hvert eneste forklarende
afsnit som noget, man kunne klikke på. Mærkets kulør blev, mætningen blev
skruet ned.

**Én farve til fire roller er ingen farve.** Se afsnittet om betydninger
ovenfor. Kontrastprøver kan ikke fange det: begge farver bestod hver for sig.
Det, der manglede, var en prøve på, at de var forskellige *fra hinanden*.

**En kant, alle felter har hele tiden, er ingen besked.** `FeltKant` var én
mættet blå, som stod på hvert eneste felt, også de tomme — og så kunne man
ikke se, hvor markøren var. Den er delt i `InputKant` (gråblå, i hvile) og
`InputFokusKant` (klar blå, kun hvor der skrives).

---

## Hvor de står i koden

`src/NoteApp.Core/Tema.cs` — som data, så de kan regnes på.
`src/NoteApp.Desktop/App.xaml` har frøet: de værdier, der gælder det øjeblik,
før temaet er sat. En prøve holder de to i trit.

Tabellerne her er skrevet ud af `Tema.cs`, ikke tastet af.
