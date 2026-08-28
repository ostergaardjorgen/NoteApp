# Måling: europæiske modeller mod Claude-facit

*Målt 17-08-2026 på Cloudworks-mødet (61 minutter, 9.493 tegn udskrift).*

Facit er det referat, Claude lavede af den samme udskrift. Claude er ikke en
kandidat til produktet — den er målestokken. Alle tre kørsler brugte den samme
udskrift, den samme skabelon og den samme bedømmelse.

Kør selv: `powershell -File scripts\maal-sky.ps1 -Moede <mødemappe>`

---

## Resultatet

| | Lokal Qwen3-8B | Mistral Medium 3.5 | Mistral Large 3 | Facit (Claude) |
|---|---|---|---|---|
| Længde | 66 % | **101 %** | 97 % | 100 % |
| Af facits 74 navne fundet | 21 | **55** | 51 | 74 |
| Af facits 34 tal fundet | 10 | **23** | 20 | 34 |
| Opfundne navne | ~9 | **0** | **0** | — |
| Tid | 59 min | **21 sek** | 58 sek | — |
| Pris | 0 kr. | 0,21 kr. | **0,05 kr.** | — |

*Priserne er på EU-endepunktet, som koster 10 % mere end det globale. Se
`mistral-dpa.md` — det globale endepunkt forpligter sig ikke på nogen geografi,
og de 2 øre er prisen for at kunne sige det, vi siger.*

---

## Tre kørsler af hver — hvad der er signal, og hvad der er støj

*Målt 18-08-2026. Samme udskrift, samme skabelon, samme facit, tre gange hver.*

| Kørsel | Længde | Navnetab | Tid | Pris |
|---|---|---|---|---|
| **Medium 3.5** #1 | 111 % | 23 % | 25,5 s | 0,21 kr. |
| **Medium 3.5** #2 | 111 % | 23 % | 23,9 s | 0,21 kr. |
| **Medium 3.5** #3 | 110 % | 24 % | 25,0 s | 0,21 kr. |
| **Large 3** #1 | 98 % | 35 % | 54,6 s | 0,04 kr. |
| **Large 3** #2 | 111 % | 28 % | 61,6 s | 0,05 kr. |
| **Large 3** #3 | 99 % | 30 % | 55,7 s | 0,04 kr. |
| **Lokal 8B** (én kørsel) | 66 % | 72 % | 59 min | 0 kr. |

**Medium er ikke bare bedre — den er stabil.** Længden varierer 1 procentpoint
over tre kørsler, navnetabet ét. Large svinger 13 procentpoint på længde og 7
på navnetab. Ved en enkelt kørsel kan Large ligne Medium; over tre gør den det
ikke.

**Afstanden til den lokale model kan ikke bortforklares med variation.**
Navnetabet er 72 % lokalt mod 23-24 % hos Medium. Spredningen inden for Medium
er ét procentpoint. De to tal er ikke i nærheden af hinanden.

Den lokale model er kun kørt én gang, fordi hver kørsel tager en time. Det er
en reel svaghed ved målingen — men den ville skulle variere med 45
procentpoint for at ændre konklusionen.

De to «fundet»-rækker tæller, hvor meget af facits indhold der er nået med —
ikke hvor mange navne udkastet indeholder i alt. Det sidste ville belønne en
model for at skrive flere navne, uanset om de var rigtige.

**Mistral Medium 3.5 vinder på alt, der handler om indhold.** Den er 170 gange
hurtigere end den lokale model og koster 18 øre.

---

## Det, der faktisk flyttede sig

**Længden.** Den lokale model skrev 66 % af facit. Begge Mistral-modeller
rammer facits længde. Det var den oprindelige klage — «for kort» — og den er
væk.

**De opfundne navne er væk. Helt.** Det er det vigtigste tal i tabellen. Den
lokale model skrev «Entropic» for Anthropic, «Nomada» for Omada, og fandt på
«Savion», «Healthspot», «BioTrust», «Joachim» og «Peng». Ingen af
Mistral-modellerne fandt på et eneste navn.

**Deltagerne.** Den lokale model tabte 72 % af facits navne og tillagde den ene
deltagers livsforløb til den anden. Begge Mistral-modeller har en
deltagerliste, der er rigtig.

**Tiden.** 59 minutter mod 21 sekunder. Hele opdelingen i blokke — og den fejl,
den medførte — er overflødig, når modellen kan se hele mødet på én gang.

---

## Medium er bedre end Large, og det er værd at bemærke

Large 3 er billigere (0,04 mod 0,18 kr.) og alligevel dårligere på alle
indholdsmål. Den er også tre gange langsommere. Navnet siger «large», men
prisen siger noget andet, og målingen giver prisen ret.

**Medium 3.5 er valget.** Forskellen er 14 øre pr. referat.

---

## Hvad tabellen IKKE siger

Bedømmelsen er grov med vilje — se hovedet i `scripts/bedoem-referat2.ps1`. To
ting skal læses med:

**«Opfundne navne» tæller også overskrifter og almindelige ord.** Rålisten for
Mistral indeholder «Beslutninger», «Gennemgang», «Hvordan», «Espens» — det er
skabelonens egne overskrifter, danske ord med stort begyndelsesbogstav, og
ejefald af rigtige navne. Efter en gennemgang i hånden er der **nul** rigtige
opfundne navne hos begge. Tallet i tabellen er det gennemgåede, ikke rålisten.

**«Navne fundet» straffer skabelonforskelle.** Facit har en hovedblok med
Emne, Dato, Form og Referent og tabeller med Ansvarlig og Frist. De ord tælles
som manglende navne hos alle tre, uden at der er tabt indhold. Det er derfor
23 % «navnetab» hos Medium ikke betyder, at hver fjerde person mangler.

---

## Det, målingen IKKE kan svare på

Tre ting, og den sidste er den vigtigste.

**1. Facit er selv en modeltekst.** Den er lavet af Claude ud fra den samme
udskrift. Den er god, men den er ikke sandheden — den er en fjerde models
mening om, hvad der blev sagt. Vi måler afstand til den, ikke til mødet.

**2. Kun ét møde.** Alle tal stammer fra Cloudworks-mødet. En anden mødetype —
teknisk gennemgang, forhandling, mange deltagere — kan give et andet billede.

**3. HVEM SAGDE HVAD måles slet ikke.** Og det var dét, der gjorde det første
referat ubrugeligt: «det citerer Espen for at sige ting, som jeg selv har
sagt». Bedømmelsen tæller, om et navn er nævnt — ikke om det står det rigtige
sted. Et referat kan score højt på alle tal ovenfor og stadig tillægge den ene
deltager den andens ord.

**Hullet er lukket 18-08-2026.** Se næste afsnit.

---

## Tilskrivning: hvem sagde hvad

*Målt 18-08-2026 med `scripts/bedoem-tilskrivning.ps1` mod `facit-udsagn.md` —
17 håndlavede udsagn med hvem der sagde dem, i mødets egen mappe.*

**Målestokken er kalibreret først.** Facit bedømt mod sig selv giver 17 af 17
rigtige og nul forkerte. En måling, der ikke kan ramme sit eget facit, kan man
ikke bruge til at dømme andre.

| | Rigtigt tilskrevet | **Forkert tilskrevet** | Uden navn | Mangler | Dækning |
|---|---|---|---|---|---|
| Lokal Qwen3-8B | 2 | **0** | 0 | 15 | 12 % |
| Mistral Medium 3.5 | 10 | **0** | 6 | 1 | **94 %** |
| Mistral Large 3 | 11 | **0** | 2 | 4 | 76 % |
| Facit (Claude) | 17 | 0 | 0 | 0 | 100 % |

### Det, målingen fandt — og det, den ikke fandt

**Ingen af modellerne tilskrev noget forkert.** Det er værd at sige lige ud,
også selvom målingen blev bygget for at fange præcis den fejl. Klagen, der
startede det hele — «det citerer Espen for at sige ting, som jeg selv har
sagt» — stammer fra en kørsel FØR v0.56, hvor hver blok blev læst i blinde
uden deltagerliste. Den rettelse ser ud til at have løst problemet. Målingen
bekræfter en rettelse frem for at afsløre en fejl, og det er også et resultat.

**Den lokale models fejl er en anden: den udelader.** 15 af 17 udsagn er slet
ikke med — 12 % dækning. Den skriver ikke forkert om hvem der sagde hvad; den
skriver næsten ikke om det. Et referat, der har to ud af sytten udsagn med,
kan ikke bruges til noget, uanset at det, der står, er rigtigt.

**Medium nævner mere, men hæfter det sjældnere på nogen.** Seks udsagn står
uden et navn i nærheden mod Large's to. Til gengæld har Medium 94 % dækning
mod Large's 76 %. Medium tager altså mere med; Large er lidt bedre til at
sige hvem, af det mindre den tager med.

Det er ikke en fejl at skrive «det blev nævnt, at …» — men det er værd at vide,
og det er et sted, skabelonen kan strammes, hvis tilskrivning bliver vigtigere
end dækning.

---

## Fejlen i den forrige måling

Første kørsel læste **udkastets frontmatter med**. Der står proveniens øverst i
hver fil — model, motor, temperatur, tid — så modellens eget navn blev talt som
et opfundet navn i referatet: «Qwen3», «Mistral», «Frankrig».

Facit har ingen frontmatter, så fejlen ramte kun den ene side af
sammenligningen. Det gav to forkerte konklusioner, som stod i `roadmap.md`:

- «35 opfundne navne» hos den lokale model. Det rigtige er omkring ni.
- «Ordet Qwen3 endte i selve referatet». Det gjorde det aldrig.

Bedømmelsen springer nu frontmatter over. Det er værd at huske hvorfor det ikke
blev opdaget: tallet så ikke forkert ud. Det pegede den rigtige vej, det var
bare for stort — og et tal, der bekræfter det, man i forvejen tror, bliver ikke
efterprøvet.

---

---

## Hjælper ordbogen fra træningen overhovedet?

*Målt 18-08-2026. Samme møde, samme model, to kørsler med og to uden.*

Skabelonen giver modellen en linje: «Fagord og navne, der kan optræde: …».
Ordene kommer fra de rettelser, brugeren har lavet under træningen. Det er
det eneste sted, træningens output rører det færdige referat.

| | Længde | Navnetab | Dækning | Forkert tilskrevet |
|---|---|---|---|---|
| Med ordbog #1 | 110 % | 23 % | 100 % | 0 |
| Med ordbog #2 | 107 % | 26 % | 100 % | 0 |
| **Uden** ordbog #1 | 112 % | 23 % | 100 % | 0 |
| **Uden** ordbog #2 | 112 % | 23 % | 100 % | 0 |

**Ingen forskel.** Forskellene er mindre end variationen mellem to kørsler af
den samme opsætning — og kørslerne uden ordbog er en anelse mere ensartede
end dem med.

Det er **tredje** håndtag i dette projekt, der måler nul:

| Håndtag | Målt | Udfald |
|---|---|---|
| Ordliste til Whisper | 41 termer, nul effekt | Fjernet |
| Sprogmodel retter udskriften | 2 ord ud af 3.418 | Fravalgt |
| **Ordbog til sprogmodellen** | **ingen målbar forskel** | **skal besluttes** |

Kør selv:

```
heypia sky referat <mødemappe> mistral-medium --uden-ordbog
```

## Det, der stadig mangler, før budskabet må bruges

Målingen viser, at bearbejdning i Europa kan lade sig gøre uden at gå på
kompromis med kvaliteten — den er tværtimod bedre end den lokale.

Vilkårene er gennemgået i `mistral-dpa.md`. Kort fortalt: databehandleraftalen
gælder automatisk, leverandøren er fransk, og EU-endepunktet binder
bearbejdningen til Europa. Det, der krævede en rettelse i koden, var, at vi
kaldte det globale endepunkt, som ikke forpligter sig på nogen geografi.

Den formulering, der kan dokumenteres hele vejen, står i `mistral-dpa.md` under
«Anbefalet formulering».
