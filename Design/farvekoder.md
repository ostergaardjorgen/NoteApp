# Farvekoder — Pia

*Skrevet 28-08-2026. Alle tal er regnet efter, ikke skønnet.*

Mærkets seks farver ligger til grund. De kan ikke dække en app alene — en app
har dæmpet tekst, deaktiveret tekst, kanter, svæveflader, fire
betydningsfarver og et mørkt tema. Derfor er der to slags farver her:

- **Mærkets egne** bruges, hvor de klarer opgaven
- **Afledte** er bygget ud af mærkets farver — samme kulør, anden lysstyrke —
  hvor mærkets egen ikke kan bære den

Kravene er WCAG 2.1: brødtekst over 4,5:1, kanter om felter over 3:1. Appens
egen brødtekst holdes over 7:1, som er AAA — der læses lange stræk i den.

Tallene nedenfor er mod den **værste** af de to flader (baggrund og panel).
Prøverne i `TemaTest` afviser en palet, der falder under.

---

## Mærkets farver

| | HEX | Hvor den kan bruges |
|---|---|---|
| Dyb mørkeblå | `#0D3B66` | **Lyst tema:** accent, knapper, links (10,4:1). **Mørkt:** kun som kulør til afledte — 1,55:1 mod natblå, så den kan ikke ses på den |
| Cyan / teablå | `#00A896` | Feltkanter i mørkt tema. **Kan ikke bære tekst på lyst** (2,98:1) |
| Frisk grøn | `#02C39A` | **Mørkt tema:** accent (6,9:1). **Lyst:** kun som flade med mørk skrift — 2,26:1 mod hvidt |
| Natblå / skifer | `#101828` | Brødtekst i lyst tema (16,1:1). Baggrund i mørkt tema |
| Isblå | `#F0F4F8` | Baggrund i lyst tema. Brødtekst i mørkt tema (14,1:1) |
| Ren hvid | `#FFFFFF` | Kort og paneler i lyst tema |

**Det vigtigste forbehold:** en CTA-knap i den friske grønne med hvid skrift
er ulæselig — 2,26:1. Skal den grønne være knapfarve, skal skriften være
natblå (7,84:1). I appen er den derfor accent i det **mørke** tema, hvor den
har luft omkring sig, og den dybe mørkeblå bærer det lyse.

---

## Lyst tema

| Nøgle | HEX | Kontrast | Rolle |
|---|---|---|---|
| Baggrund | `#F0F4F8` | — | mærket · isblå |
| Panel | `#FFFFFF` | — | mærket · kort og bokse |
| PanelKant | `#A1CBF3` | 1,7 | kant om et kort |
| FeltKant | `#2C8BE4` | 3,2 | kant om noget, man kan skrive i |
| Svaev | `#EFF5FB` | — | musen over |
| Trykket | `#E2EEF8` | — | knappen nede |
| Valgt | `#C9DEF2` | — | valgt række |
| Tekst | `#101828` | 16,1 | mærket · brødtekst |
| TekstSvag | `#3E5061` | 7,5 | underoverskrifter |
| TekstMeget | `#53687D` | 5,2 | forklarende linjer |
| Slukket | `#728699` | 3,4 | deaktiveret |
| Accent | `#0D3B66` | 10,4 | mærket · knapper, links, markering |
| PaaAccent | `#FFFFFF` | 10,4 | skrift på accenten |
| Optager | `#C72C22` | 5,0 | der optages |
| PaaOptager | `#FFFFFF` | 5,0 | skrift på den røde |
| Godkendt | `#01775E` | 5,0 | afledt af den friske grønne |
| Advarsel | `#876208` | 5,0 | pas på |
| FejlTekst | `#C72C22` | 5,0 | noget gik galt |
| AccentFlade | `#D5E6F5` | — | tonet kasse, blå |
| GodkendtFlade | `#D0F9F0` | — | tonet kasse, grøn |
| AdvarselFlade | `#F7ECD1` | — | tonet kasse, gul |
| FejlFlade | `#F8E6E5` | — | tonet kasse, rød |
| Dokument | `#7A45C0` | 5,5 | dokumenter — skal kunne skelnes fra accenten |
| Fremhaev | `#F1DFA1` | — | fundet i en søgning |
| PaaFremhaev | `#101828` | 14,3 | skrift på det fundne |

## Mørkt tema

| Nøgle | HEX | Kontrast | Rolle |
|---|---|---|---|
| Baggrund | `#101828` | — | mærket · natblå |
| Panel | `#112538` | — | natblå løftet et nøk |
| PanelKant | `#10497F` | 1,7 | kant om et kort |
| FeltKant | `#008072` | 3,2 | afledt af teablå |
| Svaev | `#112D48` | — | musen over |
| Trykket | `#0C2134` | — | knappen nede |
| Valgt | `#095149` | — | valgt række |
| Tekst | `#F0F4F8` | 14,1 | mærket · isblå |
| TekstSvag | `#B3C7DB` | 9,0 | underoverskrifter |
| TekstMeget | `#8CABC9` | 6,5 | forklarende linjer |
| Slukket | `#64788A` | 3,4 | deaktiveret |
| Accent | `#02C39A` | 6,9 | mærket · frisk grøn |
| PaaAccent | `#101828` | 6,9 | skrift på accenten |
| Optager | `#FF676B` | 5,5 | der optages |
| PaaOptager | `#101828` | 5,5 | skrift på den røde |
| Godkendt | `#02C59B` | 7,0 | mærkets grønne |
| Advarsel | `#EFAE31` | 8,0 | pas på |
| FejlTekst | `#FF676B` | 5,5 | noget gik galt |
| AccentFlade | `#08443D` | — | tonet kasse, teal |
| GodkendtFlade | `#09473A` | — | tonet kasse, grøn |
| AdvarselFlade | `#523B0F` | — | tonet kasse, gul |
| FejlFlade | `#5F0B0D` | — | tonet kasse, rød |
| Dokument | `#CB90F0` | 6,5 | dokumenter |
| Fremhaev | `#F4CB23` | — | fundet i en søgning |
| PaaFremhaev | `#101828` | 10,0 | skrift på det fundne |

---

## Tre ting, der gik galt undervejs

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

---

## Hvor de står i koden

`src/NoteApp.Core/Tema.cs` — som data, så de kan regnes på.
`src/NoteApp.Desktop/App.xaml` har frøet: de værdier, der gælder det øjeblik,
før temaet er sat. En prøve holder de to i trit.
