# Hjælpen i appen

*Bygget 25-08-2026. Ti afsnit på dansk og engelsk.*

Spørgsmålstegnet øverst til højre, ved siden af flaget.

---

## Hvorfor den ligger i appen

Den virker **uden net**, den kan ikke komme ud af trit med den udgave, man har
installeret, og den **sender ingenting** — heller ikke hvad man søgte efter.

En hjælp, der ligger på nettet, er et sted mere, brugerens spørgsmål kan
havne. For en app, hvis hele argument er, at data bliver på maskinen, er det
en dårlig undtagelse at lave.

---

## Sådan retter du en tekst

Filerne ligger i datamappen:

```
C:\AppNoter\hjaelp\da\10-kom-i-gang.md
C:\AppNoter\hjaelp\en\10-kom-i-gang.md
```

Ret dem med hvad som helst. **Appen skriver dem ikke over** — den lægger kun
en fil ud, hvis den ikke findes i forvejen.

Sletter du en fil, kommer den tilbage ved næste start. Det er med vilje: de
filer, der følger med appen, skal være der.

### Formen

```markdown
# Titlen på afsnittet

*Én linje, der siger hvad det handler om*

Resten er almindelig Markdown.
```

Filnavnets tal bestemmer rækkefølgen: `10-` kommer før `20-`. Navnet uden tal
er afsnittets id, og **det skal være det samme på alle sprog** — det er
sådan, de parres.

### Det, visningen forstår

Overskrifter, afsnit, punktopstillinger, `**fed**`, `*kursiv*`, `` `kode` ``,
`> citat` og `---`.

Ikke tabeller. Markdown-visningen er skrevet i huset frem for hentet som en
pakke: det, hjælpen bruger, er halvfjerds linjer, og en pakke ville koste en
afhængighed, der skal holdes opdateret og gøres rede for under Compliance.

Det, der ikke forstås, vises som almindelig tekst. En hjælpefil, hvor nogen
har skrevet en tabel, ender ikke som en tom side — den ser bare lidt kedelig
ud.

---

## Sproget

Hjælpen følger sprogindstillingen og skifter med det samme, også mens vinduet
er åbent.

**Mangler et afsnit på det valgte sprog, vises det danske.** Halv hjælp slår
ingen hjælp. Det er den samme regel som for teksterne i brugerfladen.

Et nyt sprog er en mappe mere — `hjaelp\de\` — og de afsnit, der ikke er
oversat endnu, kommer på dansk.

---

## Søgningen

**Alle ord skal være der.** En søgning på «slet lyd» skal ikke svare med hvert
afsnit, hvor ordet «slet» tilfældigvis står. De behøver til gengæld ikke stå
ved siden af hinanden — hjælpen er korte tekster, og et afsnit, der handler om
begge dele, er det rigtige svar.

**Titlen vejer tyve gange tungere end brødteksten.** Står ordet i
overskriften, *handler* afsnittet om det; står det i brødteksten, bliver det
måske bare nævnt.

Hvert træf viser den linje, ordet stod i, med Markdown-tegnene renset væk — så
man kan se, om træffet er det rigtige, uden at åbne afsnittet.

Escape rydder søgningen. Er der ingenting at rydde, lukker den vinduet.

---

## Vinduet er ikke modalt

Man slår noget op **mens** man arbejder. Et vindue, der spærrer for appen,
tvinger en til at lukke hjælpen for at prøve det, man lige har læst.

Der åbnes kun ét. Trykker man på spørgsmålstegnet igen, hentes det frem frem
for at der kommer et til — to hjælpevinduer med hvert sit afsnit ser ud som en
fejl.

`HjaelpWindow.Aabn(ejer, afsnit)` kan åbne på et bestemt afsnit. Det er
forberedt til «hjælp om det her»-knapper ude på skærmene; der er ingen endnu.

---

## Prøver

12 prøver i `tests\NoteApp.Tests\HjaelpTest.cs`:

- at afsnittene lægges i datamappen ved første brug
- at hvert afsnit har en titel og en tekst
- at rækkefølgen følger filnavnet
- at engelsk har præcis de samme afsnit som dansk
- at engelsk **ikke bare er en kopi** — titlerne skal være forskellige
- at et nyt sprog med kun ét oversat afsnit fyldes op med dansk
- at søgningen kræver alle ord, vægter titlen, og giver et læsbart uddrag

**To af dem fandt noget, da de blev skrevet:**

Prøverne bestod hver for sig og faldt samlet. `Hjaelp` cacher afsnittene
statisk, og prøvemappen ryddede kun `Sprog`. Samme fælde som ved
sprogstyringen — nu skrevet ned begge steder.

Og en prøve slettede den engelske fil og ventede dansk. Den fejlede, fordi
`Udpak` lægger de filer, der følger med appen, tilbage. **Opførslen var
rigtig; prøven var forkert.** Den prøver nu et nyt sprog med ét oversat
afsnit, hvilket er præcis det tilfælde, reglen findes for.

---

## Når der bygges noget nyt i appen

Hjælpen er ikke færdig, fordi den er skrevet. Kommer der en ny skærm eller en
ny funktion, hører den til i et af de ti afsnit — eller i et ellevte.

Rækkefølgen er: skriv den danske fil, skriv den engelske med **samme
filnavn**, og kør prøverne. De fanger både et manglende afsnit og en engelsk
fil, der stadig indeholder dansk.
