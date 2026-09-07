# To computere

En bærbar, der optager, og en stationær, der skriver ud. De to mødes i **én
fælles mappe** — en postkasse, ikke en datamappe.

Datamappen bliver liggende lokalt på hver maskine. Grunden står i
`UserDataPaths`: en SQLite-database og en wav-fil, der vokser, mens der
optages, tåler ikke at ligge i en mappe, to maskiner synkroniserer.

## Primær og sekundær

**Primær er den kraftige med grafikkortet. Sekundær er den bærbare.**

Rollerne hed engang «arbejdsstation» og «let». Det er beskrivelser af en
maskine, og de hjælper ikke den, der står med sin computer nummer to:
«arbejdsstation» kan en bærbar også være, og «let» lyder som noget, man gerne
vil have. Grafikkortet er dét, forskellen ER — udskrivningen kører på det, og
uden et tager den mange gange så lang tid.

Velkomstforløbet spørger på trin 2 og **foreslår svaret ud fra maskinen**:
appen kigger alligevel efter et NVIDIA-kort for at vælge motor og model, så
den siger, hvad den fandt. En sekundær maskine får den lille model foreslået —
den skal ikke hente 2,9 GB ned for noget, den sender videre alligevel, men den
får ikke ingenting: uden en model kan den ikke skrive et møde ud i et tog.

Tallene bag rollerne (0 og 1) må aldrig byttes om. En maskine, der ikke er
opdateret endnu, læser den samme fil.

## Sådan sættes maskine nummer to op

Rækkefølgen står nu på skærmen — `Delingsguide` under **Indstillinger →
Deling**, med et tal ved menupunktet og på fanen for det, der mangler. Den, der
kun har én computer, ser aldrig tallet: det kræver enten en valgt fælles mappe
eller rollen «sekundær».

1. **Vælg den fælles mappe.** Den første maskine klargør den; den anden peger
   bare på den samme og finder mærket. Begge maskiner må gerne have hver sit
   drevbogstav.
2. **Åbn HeyPia på den anden computer** og peg på den samme mappe. Den melder
   sig inden for fem minutter (`Delingsvagt`).
3. **Godkend.** Der vises seks cifre begge steder, regnet ud af begge
   offentlige nøgler. Står der det samme, er det de rigtige to maskiner. **Det
   skal gøres på begge skærme** — hver maskine fæstner den andens nøgle.
4. **Send opsætningen** fra den maskine, der har den.

## Hvad der kan sendes over — og hvad der ikke kan

`Noegledeling` lægger en adgang i en lukket kuvert i den fælles mappe:
AES-256-GCM under en nøgle udledt af parringens fælles nøgle, nyt salt pr.
kuvert, afsender/modtager/udløb/**formål** bundet som AAD. Den lever en time,
åbnes én gang og slettes ved afhentning. En adgang, der allerede findes på
modtageren, byttes aldrig ud.

| Slags | Hvad | Filnavn |
|---|---|---|
| `Apinoegle` | Nøglen til sprogmodellen | `<id>.json` |
| `Googlekalender` | Opdateringsnøglen til Google Kalender | `<id>-google.json` |
| `Googleopgaver` | Opdateringsnøglen til Google Tasks | `<id>-google-opgaver.json` |

API-nøglen beholder sit gamle filnavn med vilje: en maskine på 1.3.54 ser kun
efter `<id>.json`, og et nyt navn ville stoppe nøgledelingen mellem to udgaver
uden at sige det.

**Formålet er bundet til krypteringen.** Døbes en Google-kuvert om til
API-nøglens navn, kan den ikke åbnes — ellers kunne en Google-adgang ende som
appens nøgle til sprogmodellen. Der er en prøve på netop det.

**DPAPI er grunden til, at det skal sendes og ikke kopieres.**
Opdateringsnøglen ligger bundet til Windows-brugeren på den maskine, der
gemte den (`Hemmelighed`). En kopieret fil kan ikke åbnes på den anden
computer.

## Kalender og opgaver på tværs

**Med Google:** forbindelsen sendes over én gang, og derefter henter begge
maskiner fra Google. De er i sync, fordi de henter fra det samme sted — der er
intet at vedligeholde, og man logger ind én gang i alt.

**Uden Google:** aftaler i `kalender.json` og opgaver i `Opgaver\opgaver.json`
er lokale i datasættet. De rejser ikke endnu. Det er etape 2.

## Etape 2 — det, der mangler

Alt herunder er ikke bygget.

**Møderne.** Lyd ud fra den sekundære, udskrift hjem fra den primære. Markører
i den fælles mappe, signeret med parringsnøglen; krav, kvitteringer og
oprydning, og en «skriv ud her alligevel»-vej, når den primære ikke kan nås.

**Kalender og opgaver uden Google.** Samme regel som alt andet i mappen:
**der skrives aldrig i andres filer.** Hver maskine ejer sin egen
ændringsjournal — én fil pr. maskine, kun tilføjelser — og den anden læser
den og anvender ændringerne. To skrivere på den samme fil gennem en
synkroniseringsklient bliver til en «conflicted copy», og den slags opdager
ingen.

- Hver post har et id og et tidsstempel. Er den samme post rettet begge
  steder, vinder den nyeste.
- Sletninger rejser som gravsten. Uden dem dukker en slettet aftale op igen,
  næste gang den anden maskine skriver sin journal.
- Journalen beskæres, når begge maskiner har bekræftet, at de har læst frem
  til et punkt.
- Opgaver, appen selv har fundet i et møde, følger mødet og ikke journalen.

**Det, der aldrig skal i mappen:** lyd under optagelse, `learning.db` og
indstillinger. Se `Delt` og advarslen på delingsfanen om, hvem der kan læse
med.
