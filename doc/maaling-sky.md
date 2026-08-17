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

**Bemærk variationen mellem kørsler.** Den samme udskrift kørt to gange gav
101 % og 111 % længde hos Medium. Tallene i tabellen er én kørsel, ikke et
gennemsnit — retningen er entydig, men en enkelt decimal er det ikke.

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
26 % «navnetab» hos Medium ikke betyder, at hver fjerde person mangler.

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

## Det, der stadig mangler, før budskabet må bruges

Målingen viser, at bearbejdning i Europa kan lade sig gøre uden at gå på
kompromis med kvaliteten — den er tværtimod bedre end den lokale.

Vilkårene er gennemgået i `mistral-dpa.md`. Kort fortalt: databehandleraftalen
gælder automatisk, leverandøren er fransk, og EU-endepunktet binder
bearbejdningen til Europa. Det, der krævede en rettelse i koden, var, at vi
kaldte det globale endepunkt, som ikke forpligter sig på nogen geografi.

Den formulering, der kan dokumenteres hele vejen, står i `mistral-dpa.md` under
«Anbefalet formulering».
