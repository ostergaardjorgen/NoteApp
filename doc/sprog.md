# Sprog

*Bygget 25-08-2026. Dansk og engelsk følger med. Et sprog mere er én fil.*

Knappen med flaget står øverst til højre, ved siden af klokken. Der skiftes med
det samme — ingen genstart.

---

## Sådan tilføjer du et sprog

1. Klik på flaget → **«Tilføj et sprog …»**. Mappen åbner:
   `C:\AppNoter\sprog`
2. Kopiér `da.json` til fx `de.json`.
3. Ret de tre felter øverst:

```json
"_sprog": { "kode": "de", "navn": "Deutsch", "flag": "DE" }
```

4. Oversæt resten af teksterne. Lad nøglerne stå.
5. Genstart appen. Sproget står i menuen.

Der skal **intet bygges**. Filen er alt.

### Flaget

`_sprog.flag` er en landekode. Dannebrog og Union Jack er tegnet i appen; for
alle andre koder vises koden i en lille afrundet kasse — det samme, Windows
selv gør med flag-emoji.

Et nyt sprog virker altså med det samme, og et rigtigt flag kan tegnes
bagefter, hvis nogen får lyst. Det gøres i `Flagikon.xaml`.

**Flagene er tegnet og ikke skrevet som emoji med vilje.** Windows viser ikke
nationale flag: 🇩🇰 bliver til bogstaverne «DK» i en kasse. Skal der være et
flag på knappen, skal det tegnes.

---

## Hvorfor JSON og ikke .resx

.NET's egen vej er ressourcefiler og satellit-assemblies. Den er udmærket —
bortset fra præcis det, der skulle kunne lade sig gøre her: **at tilføje et
sprog kræver Visual Studio og et nyt byg.** En oversætter kan ikke aflevere et
sprog uden en udvikler.

Med én JSON-fil pr. sprog er et nyt sprog én fil, der lægges i mappen. Det er
den samme beslutning som med mødetyperne: det, brugeren skal kunne ændre, er
filer — ikke kode.

Nøglerne er punktopdelte — `nav.cockpit`, `topbar.optag` — som det er skik i
i18n. Formen er den samme, uanset hvilket værktøj en oversætter er vant til.
Både indlejret og fladt JSON virker:

```json
{ "nav": { "cockpit": "Cockpit" } }
{ "nav.cockpit": "Cockpit" }
```

---

## Dansk er kildesproget

Mangler en nøgle i det valgte sprog, hentes **den danske tekst**. Ikke nøglen.

Det betyder, at et halvt oversat sprog viser dansk dér, hvor der mangler noget
— og det er brugbart. `nav.cockpit` på en knap er det ikke.

Findes nøglen slet ikke, kommer den selv tilbage. Det er med vilje synligt
grimt: en manglende nøgle skal opdages, mens der bygges, ikke af en kunde.

**Der er en prøve på, at engelsk har alle danske nøgler.** Den fanger den fejl,
ingen ellers opdager: en ny dansk tekst kommer ind, og engelsk står tilbage med
dansk på den ene linje. Falder prøven, skal `en.json` have nøglen med.

---

## Hvad der er oversat indtil videre

| | |
|---|---|
| Navigationen i venstre side | ✔ |
| Topbjælken — optag, webinar, pause, stop, klokken | ✔ |
| Cockpittet — overskrifter, søgefelt, filtre, tom skærm | ✔ |
| Kalenderen og opgavepanelet | ✔ |
| «Sidst hentet»-linjerne | ✔ |
| Optagelser, Dokumenter, Mødetyper, AI-modeller | mangler |
| Compliance, Historik, Indstillinger | mangler |
| Dialoger og vinduer | mangler |

Der er omkring 600 tekster i appen i alt. **Mekanikken er færdig; oversættelsen
er ikke.** Vælger du engelsk i dag, får du engelsk chrome og dansk indhold på
de skærme, der ikke er nået endnu.

Det er ikke en fejl, der skal skjules — det er en opgave, der kan tages
skærm for skærm. Fremgangsmåden er:

1. Læg nøglerne i `da.json` og `en.json`.
2. Byt teksten i XAML ud med `{local:Oversat noegle}`.
3. Bygges teksten i C#, brug `Sprog.T("noegle")`.
4. Bygger skærmen tekst med tal i, skal den lytte på `Sprog.Aendret` og skrive
   sig om — se `SearchView.Sprogskiftet`.

---

## Sådan skifter teksterne uden genstart

`{local:Oversat nav.cockpit}` er ikke et opslag. Det er en **binding** til en
indekser på et objekt, der melder `Item[]` ændret, når sproget skifter — så
spørger WPF om alle bundne tekster igen på én gang.

Et almindeligt opslag ville have sat teksten én gang, da vinduet blev bygget,
og en sprogknap, der kræver genstart, er ikke en sprogknap.

Bindingen virker alle steder, der tager en streng: `Text`, `Content`,
`ToolTip`, `Header`, `AutomationProperties.Name`.

---

## Sproget i appen er ikke sproget i mødet

To ting, der lyder ens og ikke har noget med hinanden at gøre:

- **`AppSettings.Sprog`** er brugerfladen. Det, den her side handler om.
- **`AppSettings.MitSprog`** og **`MitAndetSprog`** er det, du og gæsterne
  *taler*. Det bruges til at skrive lyden ud.

Man kan udmærket køre appen på engelsk og holde sine møder på dansk — og det
gør man, hvis man har en udenlandsk kollega, der kigger med.

---

## Prøver

11 prøver i `tests\NoteApp.Tests\SprogTest.cs`. De prøver **mekanikken**, ikke
oversættelserne — dem kan en prøve ikke bedømme:

- at filerne lægges i mappen ved første brug
- at et nyt sprog kun kræver én fil
- at et halvt oversat sprog falder tilbage på dansk
- at en ødelagt sprogfil ikke vælter noget
- at valget huskes på disken
- at en forkert formatstreng fra en oversætter ikke vælter skærmen
- at engelsk har alle danske nøgler

**Én af dem fandt en rigtig fejl, da den blev skrevet:** `Genindlaes` nulstillede
teksterne, men ikke sprogkoden, og `Skift` sammenlignede mod den forældede
værdi, før filerne var læst. Et skift til det sprog, der allerede stod i
indstillingerne, blev derfor sprunget over, og valget aldrig skrevet. Rettet
begge steder.
