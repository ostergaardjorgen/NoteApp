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
er lokale i datasættet. De rejser ikke endnu — se «Det, der stadig mangler».

## Etape 2 — møderne rejser

**Bygget.** Den bærbare optager, den kraftige skriver ud.

```
arbejde/<opgaveid>/
   opgave.json     hvem, hvilket møde, hvilket spor, sprog, modelønske, lydens sum, segl
   lyd.wav         selve lyden — slettes i samme øjeblik, svaret lægges
   krav.json       hvem der har taget den, og hvornår den sidst blev rørt
   svar.json       sprog, motor, tid — og fejlen, hvis det gik galt
   udskrift.txt / udskrift.json
```

**Sådan går det til.** Der trykkes på «Skriv ud på ‹navn›» ved optagelsen; der sendes
aldrig noget af sig selv. Sproget spørges der om i det samme vindue som ved en
lokal kørsel — vælges det forkert, er hvert eneste ord forkert, også når den
anden maskine skriver ud. Et møde med to spor bliver til to opgaver, og de
samles først hjemme, når begge svar er der: en udskrift af det halve møde ser
færdig ud og er det ikke.

Modtageren henter lyden **hjem**, før whisper kører — at læse hundrede megabyte
gennem netværket, mens modellen arbejder, er ikke det samme som at have filen.
Den tager kun én ad gangen og kun bag `HeavyJobLock`: to whisper-kørsler på ét
grafikkort er ikke dobbelt så hurtigt, det er to, der løber tør for hukommelse.

Hjemme får filerne **de navne, en lokal kørsel ville have givet dem**
(`mikrofon_large-v3.json`). Resten af appen leder efter dem dér, og en udskrift,
der ligger et andet sted, findes ikke.

**Seglet.** Den, der kan skrive i mappen, kan lægge en opgave og skrive «fra den
bærbare» på den. Derfor mærkes både opgave og svar med en HMAC under en nøgle,
der er udledt af parringen. Seglet dækker alle felter **og lydfilens sum**:
byttes lyden ud efter opgaven er lagt, passer det ikke. Prøverne dækker begge
dele, og også den anden vej — et svar fra en fremmed hentes ikke hjem.

**Kravet er en fil, der kun kan laves én gang.** `CreateNew` afgør, hvem der får
opgaven — filsystemet, ikke en aftale mellem to programmer. Går maskinen ned
midt i arbejdet, står hjerteslaget stille, og opgaven bliver ledig igen efter en
halv time. En bruger skal ikke vente på en slukket maskine.

**Lyden er ikke krypteret.** Den ligger på dit eget drev, og delingsskærmen
siger lige ud, at den, der kan læse mappen, kan læse det, der ligger i den.
Seglet beskytter mod at få lagt arbejde ind — ikke mod at nogen kigger med.

## Det, der stadig mangler

**Talergenkendelsen** kører hjemme hos den, der optog. Den er tung og kunne
sendes med som sin egen slags opgave.

**Kalender og opgaver uden Google.** Aftaler i `kalender.json` og opgaver i
`Opgaver\opgaver.json` er lokale i datasættet. Samme regel som alt andet i
mappen: **der skrives aldrig i andres filer.** Hver maskine ejer sin egen
ændringsjournal — én fil pr. maskine, kun tilføjelser — og den anden læser den
og anvender ændringerne. To skrivere på den samme fil gennem en
synkroniseringsklient bliver til en «conflicted copy», og den slags opdager
ingen.

- Hver post har et id og et tidsstempel. Er den samme post rettet begge steder,
  vinder den nyeste.
- Sletninger rejser som gravsten. Uden dem dukker en slettet aftale op igen,
  næste gang den anden maskine skriver sin journal.
- Journalen beskæres, når begge maskiner har bekræftet, at de har læst frem til
  et punkt.
- Opgaver, appen selv har fundet i et møde, følger mødet og ikke journalen.

**At sende af sig selv.** I dag trykker man på knappen. En indstilling om at
gøre det automatisk, når den primære er vågen, hører til — men den skal være et
valg, ikke en standard.

**Det, der aldrig skal i mappen:** lyd under optagelse, `learning.db` og
indstillinger. Se `Delt` og advarslen på delingsfanen om, hvem der kan læse med.
