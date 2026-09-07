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
rejser med en journal. Samme regel som alt andet i mappen: **der skrives aldrig
i andres filer.**

```
journal/<fra>/<til>.jsonl     kun tilføjelser, én linje pr. ændring, HMAC pr. linje
```

- **Den nyeste vinder.** `Aendret` sættes af `Kalender.Gem` og `Opgavelager.Gem`
  — ikke af kalderen, for et felt, hver kalder skal huske, bliver glemt.
- **Sletninger rejser som gravsten.** Uden dem dukker den slettede aftale op
  igen, næste gang den anden maskine skriver sin journal.
- **Intet ekko.** `Journal.Anvender` er sat, mens en fremmed ændring lægges ind,
  så den ikke skrives tilbage. Uden den ville de to kaste den samme aftale frem
  og tilbage, så længe de begge var tændt.
- **Google-poster rejser ikke.** Begge maskiner henter dem selv; en aftale, der
  kom to veje, ville blive til to.

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

**Sådan går det til.** Der trykkes på «Skriv ud på ‹navn›» ved optagelsen.
Sproget spørges der om i det samme vindue som ved en
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

**Det kan sættes til at ske af sig selv** — et hak på delingsfanen, **fra som
standard**. Lyden er det mest private, appen har, og den skal ikke begynde at
rejse, fordi to maskiner engang blev godkendt. To grænser, og de er begge målt
frem: kun optagelser fra **det seneste døgn** (ellers ville dagen, hakket blev
sat, sende hele arkivet over netværket på én gang), og **kun når sproget er
kendt** — fra optagelsen, fra aftalen eller fra indstillingerne. Et gæt, der
ligner et resultat, er den værste slags fejl.

**Lyden er ikke krypteret.** Den ligger på dit eget drev, og delingsskærmen
siger lige ud, at den, der kan læse mappen, kan læse det, der ligger i den.
Seglet beskytter mod at få lagt arbejde ind — ikke mod at nogen kigger med.

## Arkivet — historikken, der kan hentes hjem

**Bygget.** Alt det ovenfor er en postkasse: det, der ligger i den, er på vej et
sted hen, og det ryddes, når det er kommet frem. Arkivet er det modsatte. Det
bliver liggende.

```
arkiv/<maskinid>/
   arkiv.json      navn, rolle, hvornår sidst, hvor mange filer, hvor meget
   optagelser/     hele mødemappen: lyd, udskrifter, noter, segmenter
   projekter/
   skabeloner/
   dokumenter/
```

En ny bærbar henter hele historikken hjem. En stationær, der brænder sammen,
kommer tilbage. Det er dét, arkivet er til, og de to er det samme problem.

**Der arkiveres efter en hvidliste** — fire mapper, nævnt ved navn. Ikke
«datamappen minus nogle undtagelser». I datamappen ligger `deling\noegle.txt`,
maskinens **private nøgle**, den ene ting der aldrig må forlade maskinen. En
sortliste, der glemte den, ville lægge den et sted, alle på delingen kan læse,
og ingen ville opdage det. En hvidliste kan glemme at tage noget **med**; det er
den fejl, man vil have. Der er en prøve på netop den fil.

Udenfor står derfor også `learning.db` (en åben SQLite-fil, der kopieres i
stykker), `indstillinger.json` (lydenheder og stier, der hører til den maskine),
`log\` og `motor\` (modellerne fylder gigabyte og kan hentes igen).

**En sletning rejser ikke.** Sletter du en optagelse hjemme hos dig selv, bliver
den liggende i arkivet. Journalen ovenfor gør det **modsatte** og skriver
gravsten — og det er rigtigt hver sit sted: en aflyst aftale, der kommer
tilbage, er en fejl; en optagelse, der stadig kan hentes hjem, er en redning. Et
arkiv, der sletter det, du slettede, er ikke en sikkerhedskopi, det er en
spejling.

**Det lette først, lyden bagefter.** Målt 07-09-2026 på den stationære:

| | Filer | Fylder |
|---|---|---|
| Lyd (`*.wav`) | 55 | 1.852 MB |
| Alt det skrevne | 297 | 24,8 MB |

Sendes de imellem hinanden, er man en time inde i den første kørsel, før den
første udskrift er nået frem. Sådan her efterlader en afbrudt kørsel **alt det,
man kan læse** — og det er den rigtige halvdel at mangle. Lyden kan slås fra
helt; så arkiveres udskrifter, noter, referater og projekter stadig.

**En optagelse, der er i gang, røres ikke.** Wav-filen vokser, mens der optages.
En halv wav i arkivet ser hel ud — størrelsen passer med det, der blev læst — og
den ville blive liggende sådan for evigt. Et møde uden sluttidspunkt springes
derfor over — **men kun en time**. Går appen ned midt i en optagelse, får mødet
aldrig et sluttidspunkt, og et krav om et ville holde netop det møde ude af
arkivet for evigt. Segmenterne fra et nedbrud er dét, genopretningen skal bruge,
og de findes kun ét sted. Der spørges i stedet, hvornår mappen sidst blev
skrevet i: der optages til disken hele tiden, så en time uden en eneste
skrivning er ikke en optagelse, der er i gang.

**Der skrives aldrig direkte på målets navn.** Hver fil kopieres til
`<navn>.delvis` og flyttes på plads bagefter. Falder netværket ud midt i en wav
på 200 MB, er der ingen halv fil med det rigtige navn.

**Der overskrives aldrig noget hjemme.** «Hent hjem» tager kun det, du mangler
— også når arkivets udgave er nyere. Det lokale er det, der er i brug, og en
gendannelse, der kan skrive hen over dagens arbejde, er en, man ikke tør trykke
på.

**Arkivet er ikke forseglet**, og grunden er ikke sjusk: en stationær, der er
brændt sammen, tog sin private nøgle med sig. Den nye installation er en fremmed
for det arkiv, den skal gendanne fra — den kan per definition ikke have en
parring med en maskine, der er væk. Et segl ville gøre arkivet ubrugeligt
præcis den dag, det skulle bruges. Det er mappens egne rettigheder, der er
grænsen, og det står på delingsskærmen.

**Det skrevne hentes hjem af sig selv; lyden gør ikke.** Udskrifter, referater,
noter, projekter og skabeloner fra den anden maskine kommer hjem, så et møde
optaget på den bærbare kan læses på den stationære — 25 MB. Lyden bliver stående
på drevet, til nogen trykker: hentedes den også, ville hver optagelse ligge tre
steder — begge maskiner og drevet — uden at nogen havde bedt om det.

**Vagten viger.** Der arkiveres ikke, mens der optages, og ikke, mens der kører
noget tungt. Arkiveringen kan altid tages om; optagelsen kan ikke.

### Hvor tit

`Arkivvagt`s ur slår hvert minut, men det koster ét opslag på klokken. Selve
kørslen spørger filsystemet om størrelse og dato på hver eneste fil — 352 den
07-09-2026 — og de spørgsmål går over SMB. Det er den, der skal være sjælden, og
hvor sjælden bestemmer brugeren (`Arkivplan`):

| Takt | Betyder |
|---|---|
| Hvert 15. minut | standard |
| Hver time / hver 4. time | |
| To gange om dagen | to faste klokkeslæt, fx 8 og 17 |
| Én gang om dagen | ét fast klokkeslæt |
| Kun når jeg trykker | |

De hyppige takter kan holdes inden for et **tidsrum**: en maskine, der står
tændt om natten, har ingen grund til at gennemgå 352 filer kl. 03. Tidsrummet må
gå over midnat — «22 til 6» er et rigtigt svar for den, der arbejder om aftenen.

**De faste tidspunkter glider ikke.** Der spørges, om der er kørt siden *dagens*
klokkeslæt, ikke om der er gået 24 timer. Med et mellemrum ville kl. 8 blive til
kl. 11 inden for en uge. Var maskinen slukket kl. 8, tages turen, når den
tændes — at springe dagen over er den slags, man opdager en uge senere.

Uret slår hvert minut netop for at kunne ramme et klokkeslæt: slog det hvert
kvarter, ville «kl. 8» blive til «engang mellem 8 og 8.15».

### Det er ikke den eneste forbindelse

Delingsfanen skriver de øvrige takter op ved siden af, selvom de ikke kan
justeres. Uden dem ser arkivets kvarter ud som den eneste vej mellem de to
maskiner, og så undrer man sig over, at en aftale er der med det samme, mens en
optagelse ikke er.

| Hvad | Hvor tit | Vagt |
|---|---|---|
| Aftaler, opgaver, færdige udskrifter, lyd der skal skrives ud | hvert minut | `Arbejdsvagt` |
| Om den anden er tændt, og om der venter en opsætning | hvert 5. minut | `Delingsvagt` |
| Arkivet | efter takten ovenfor | `Arkivvagt` |

Tallene i den tabel hentes fra vagternes egne felter og ikke fra en tekst: en
tabel, der er skrevet af i hånden, holder op med at passe den dag, et af tallene
ændres — og det ser stadig rigtigt ud.

## Det, der stadig mangler

**Talergenkendelsen** kører hjemme hos den, der optog. Den er tung og kunne
sendes med som sin egen slags opgave.

**Journalen beskæres ikke endnu.** Den vokser med én linje pr. ændring. Den
burde kunne skæres, når begge maskiner har bekræftet, at de har læst frem til
et punkt.

**Det, der aldrig skal i mappen:** lyd under optagelse, `learning.db` og
indstillinger. Se `Delt` og `Arkiv` og advarslen på delingsfanen om, hvem der
kan læse med.
