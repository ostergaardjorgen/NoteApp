# NoteApp til iPhone — plan

*Skrevet 24-08-2026. Alle påstande om, hvad iOS kan og ikke kan, er slået op
og har en kilde nederst.*

---

## Det korte svar

Ønsket falder i to halvdele, og de er ikke lige lette.

**Mødeoptageren kan bygges.** En app, der optager et møde og lægger filen i en
mappe ved navn NoteApp i iCloud Drive, er almindeligt iOS-arbejde. Den kan
startes håndfrit med Siri, den kan vise en stor stopknap på låseskærmen, og
filen dukker op på pc'en af sig selv. Det hænger direkte sammen med det, der
blev bygget i dag: NoteApp på Windows kan nu læse en `.m4a` eller `.wav` ind
som en optagelse.

**Telefonsamtalen kan ikke.** Ikke «det er svært» — det kan ikke lade sig
gøre. Der er tre mure, og de er alle tre Apples, ikke vores:

1. Ingen app må læse lyden fra et telefonopkald. Der findes ingen adgang til
   den.
2. Under et opkald har opkaldet mikrofonen alene. En optagelse, der kørte i
   forvejen, bliver afbrudt af iOS.
3. En app kan ikke åbne sig selv. Der findes ingen måde at få NoteApp frem på
   skærmen, fordi et opkald begynder.

Siri kan heller ikke diktere samtalen — og Apples egen optagefunktion fra
iOS 18.1 er slået fra i hele EU og kan ikke dansk. Se afsnit 1.

Det betyder, at punkterne om «dukker selv op», «Optagelse accepteres ikke» og
«spørg efter samtalen» ikke kan bygges, som de er stillet op. Men **det, de
skal opnå, kan næsten opnås ad en anden vej** — se afsnittet om diktatet.

---

## De tre mure, hver for sig

### 1. Lyden fra et opkald er lukket land

iOS giver ingen app adgang til hverken det, du siger, eller det, den anden
siger, under et telefonopkald. Der er ingen API, hverken i CallKit eller
AVFoundation. Apple lagde selv en optagefunktion ind i Telefon-appen i iOS
18.1 — med en hørbar besked til begge parter — og den er ikke åbnet for
andre.

**Kan man snyde med højttaleren?** Nej. Under et opkald har opkaldet
mikrofonen eksklusivt. En optagelse, der allerede kørte, bliver afbrudt af
iOS og får hverken lov til at fortsætte eller starte igen, før opkaldet er
slut. De apps, der markedsfører sig med opkaldsoptagelse, gør det ved at
koble en tredjepart ind på linjen — en konferencebro, som samtalen føres
igennem — eller ved at bruge en anden enhed. Ingen af delene sker på
telefonen.

**Selv hvis det kunne lade sig gøre**, ville App Store-godkendelsen være det
næste. Retningslinje 2.5.14 kræver udtrykkeligt samtykke og en tydelig
visuel eller hørbar besked under hele optagelsen. En app, hvis formål er at
optage opkald, kommer ikke igennem.

**Kan Siri diktere samtalen?** Nej, og der er to grunde, der hver for sig er
nok.

Diktering skriver det ned, **mikrofonen** hører. Modpartens stemme kommer
aldrig forbi mikrofonen — den går ud gennem ørestykket eller højttaleren. Og
sætter man samtalen på højttaler, arbejder iOS' ekkoundertrykkelse aktivt
imod: dens hele opgave er at trykke modpartens lyd ud af mikrofonsignalet, så
den anden ikke hører sig selv. Selv i det bedste tilfælde ville dikteringen
altså kun få **din egen halvdel** af samtalen.

Dertil kommer den samme mur som ovenfor: dikteringen skal bruge mikrofonen,
og den har opkaldet.

**Apple kan det selv — men ikke her.** Fra iOS 18.1 kan Telefon-appen optage
et opkald og skrive det ud i Noter. Det er nøjagtig det, der efterspurgtes,
og det er lukket ad to veje på én gang:

- Funktionen er **slået fra i hele EU**, Danmark iberegnet. Knappen findes
  ikke på telefonen.
- Udskrivningen kan **ikke dansk**. Den findes på engelsk, spansk,
  mandarin, kantonesisk og koreansk.

Det er værd at vide, før der bruges tid på at lede: når Apples egen
funktion er lukket i Danmark, er der ingen bagdør for andre.

### 2. Appen kan ikke dukke op af sig selv

iOS lader ikke en app komme i forgrunden uden at brugeren beder om det. Der
findes ingen undtagelse.

`CXCallObserver` kan fortælle om et opkald — men **kun mens appen allerede
kører**. Er appen suspenderet, sker der ingenting, og den bliver ikke vækket.
Kører den i baggrunden på grund af en optagelse, får den beskeden, og så
bliver den suspenderet omkring tredive sekunder efter.

**Genveje kan heller ikke.** Personlige automatiseringer i Genveje har ingen
udløser for telefonopkald overhovedet — hverken start eller slut. Der er
udløsere for beskeder og mail, og det er det.

Det tætteste, der findes, er en **lokal notifikation** eller en **Live
Activity** på låseskærmen. Begge dele kræver, at appen kører i forvejen.

### 3. Under kørsel er der endnu et lag

CarPlay er den rigtige ramme for noget, der skal betjenes i en bil — men
CarPlay-tilladelsen gives kun til bestemte kategorier, og en app til
mødenoter er ikke en af dem. Den skal derfor kunne betjenes med **Siri
alene**, og det kan den godt: en App Intent kan udføres på stemmen uden at
appen åbnes.

---

## Det, der kan bygges

### Etape A — Mødeoptageren
*Middel · ingen forhindringer*

Optag et møde med telefonen i stedet for iPhones egen optager. En stor knap,
en tydelig visning af at der optages, og en stopknap.

- Optagelse fortsætter, når skærmen slukkes, med `UIBackgroundModes: audio`.
- **Live Activity på låseskærmen og i Dynamic Island** med tiden og en
  stopknap, der virker uden at låse op. Interaktive knapper i en Live
  Activity har været mulige siden iOS 17.
- Filen skrives løbende i stumper, så et sammenbrud højst koster det sidste
  stykke. Det er den samme regel som på Windows.

**Formatet er en beslutning med en pris.** NoteApp på Windows skal bruge 16
kHz mono. Optager telefonen direkte i det format, er der ingen omsætning og
intet tab — men det fylder ca. 115 MB i timen. AAC ved 64 kbit/s fylder ca.
28 MB, og det er ikke målt, hvad det koster i nøjagtighed. Det, der **er**
målt, er, at 24 og 32 kbit/s ødelægger blandet dansk-engelsk tale — fra
20,08 % til 40,57 % ordfejl, se `findings.md` §9.2. Anbefalingen er derfor
ukomprimeret, og en indstilling til den, der har lidt plads i iCloud.

### Etape B — Mappen i iCloud
*Lille · bygger på A*

Filen lægges i en mappe, der hedder NoteApp og står i iCloud Drive ved siden
af alt andet.

Det gøres med tre nøgler i appens `Info.plist`: `NSUbiquitousContainerName`
sætter navnet, brugeren ser, `NSUbiquitousContainerIsDocumentScopePublic`
gør mappen synlig i Filer-appen, og `NSUbiquitousContainerSupportedFolderLevels`
bestemmer, om der må være undermapper. Kun det, der ligger i `Documents`
inde i beholderen, bliver vist.

**Så er ringen sluttet.** Mappen synkroniserer til `C:\iCloudDrive\NoteApp`
på pc'en, og NoteApp kan læse filen ind — det blev bygget i dag.

**Én ting skal huskes på Windows-siden:** iCloud lægger kun en pladsholder på
disken. Filen fylder nul, indtil nogen læser den. Det er allerede håndteret i
indlæsningen, og det er den vigtigste detalje i roadmappens punkt 2.2 om en
overvåget mappe.

### Etape C — Håndfri med Siri
*Middel · bygger på A*

«Hey Siri, optag et møde i NoteApp» starter en optagelse, uden at telefonen
skal røres og uden at appen åbnes. Det bygges med App Intents og App
Shortcuts, og Siri kan spørge og få svar undervejs.

Det er det eneste, der virker forsvarligt bag rattet. Alt, der kræver et
kig på skærmen, hører ikke hjemme i en bil.

### Etape D — Diktatet efter samtalen
*Middel · bygger på A og C · **det her er svaret på telefonsamtalen***

Samtalen kan ikke optages. Men det, der skulle komme ud af den — de vigtige
punkter og opgaverne — kan tages med det samme bagefter, og det kan gøres
håndfrit:

> «Hey Siri, diktér noter til NoteApp»

Appen optager, lægger lyden i den samme mappe i iCloud, og NoteApp på pc'en
skriver den ud og finder opgaverne i den, ligesom den gør med et møde.

**Og der findes en halv vej til at blive mindet om det.** Kører appen
allerede — fordi den optog noget, eller fordi den lige er brugt — kan
`CXCallObserver` se, at et opkald sluttede, og lægge en notifikation frem:
«Skal der diktat på?». Trykker man på den, åbner appen i diktattilstand.

Det er ærligt at sige, hvad det er: **det virker ikke hver gang.** Har
telefonen suspenderet appen, kommer der ingen påmindelse. En vane og en
Siri-sætning er det, man kan regne med.

### Etape E — Det, der ikke kan bygges
*Kan ikke*

| Ønske | Hvorfor ikke |
|---|---|
| Optage en telefonsamtale | Ingen adgang til opkaldets lyd; mikrofonen er opkaldets alene |
| Appen dukker op, når samtalen bliver aktiv | En app kan ikke åbne sig selv, og Genveje har ingen udløser for opkald |
| «Optagelse accepteres ikke» under et opkald | Der er ingen optagelse at stoppe |
| Sikker påmindelse, når samtalen slutter | Kun hvis appen tilfældigvis kører i forvejen |

**Knappen giver stadig mening — bare et andet sted.** «Optagelsen accepteres
ikke» hører til på mødeoptageren: en deltager siger fra, man rammer den store
knap, optagelsen stopper og det optagede slettes med det samme. Det er en god
funktion, og den passer til reglen om, at appen altid opfordrer til at
fortælle deltagerne, at der optages.

---

## Hvad det kræver af dig

| | |
|---|---|
| **Apple Developer Program** | 99 USD om året. Uden det kan appen kun ligge på telefonen i syv dage ad gangen |
| **En Mac** | Xcode kører ikke på Windows. Der er ingen vej udenom, heller ikke en delvis |
| **App Store-godkendelse** | Kun nødvendig, hvis appen skal ud til andre end dig. Til eget brug er TestFlight nok |

Skal den kun bruges af dig selv, er de 99 USD og en Mac hele udgiften.

---

## Juraen, kort

Optagelse af et møde, du selv deltager i, er lovligt i Danmark. Det er
videregivelsen, der er reguleret — og for en app, der skal sælges, kommer
GDPR oveni, fordi en optagelse med andres stemmer er personoplysninger.

For telefonsamtaler er spørgsmålet i praksis afgjort af, at det ikke kan lade
sig gøre. Skulle det nogensinde blive muligt, skal det bygges med den samme
regel som resten af NoteApp: **der oplyses altid om, at der optages.**

---

## Rækkefølgen, hvis det skal bygges

1. **A + B** giver hele værdien af «jeg vil ikke bruge iPhones egen optager».
   Filen lander i NoteApp på pc'en, og det virker fra dag ét.
2. **C** gør den brugbar i bilen.
3. **D** er det, der erstatter drømmen om at optage samtalen — og det er
   værd at bygge, netop fordi det andet ikke kan.

Punkt 2.2 i roadmappen, den overvågede mappe, bør bygges **før** iPhone-appen.
Uden den skal filen læses ind i hånden hver gang, og så er halvdelen af
gevinsten væk.

---

## Kilder

- [App Review Guidelines, Apple](https://developer.apple.com/app-store/review/guidelines/) — 2.5.14 om samtykke og tydelig besked ved optagelse
- [AVAudioSession, Apple](https://developer.apple.com/documentation/AVFAudio/AVAudioSession) — lydsessioner og afbrydelser
- [Responding to Interruptions, Apple](https://developer.apple.com/library/archive/documentation/Audio/Conceptual/AudioSessionProgrammingGuide/HandlingAudioInterruptions/HandlingAudioInterruptions.html) — et opkald afbryder en kørende optagelse
- [App suspended when interrupt CXCallObserver in Background Mode, Apple Developer Forums](https://developer.apple.com/forums/thread/664277) — appen suspenderes omkring tredive sekunder efter
- [Event triggers in Shortcuts, Apple Support](https://support.apple.com/guide/shortcuts/event-triggers-apd932ff833f/ios) og [Communication triggers](https://support.apple.com/guide/shortcuts/communication-triggers-apdd711f9dff/ios) — ingen udløser for telefonopkald
- [In-Depth Guide to iCloud Documents, fatbobman](https://fatbobman.com/en/posts/in-depth-guide-to-icloud-documents/) — `NSUbiquitousContainerName` og synlighed i Filer-appen
- [iOS 18 — Call Recording, Wikipedia](https://en.wikipedia.org/wiki/IOS_18) og
  [iOS 18.1 Call Record — how to test when based in Europe, Apple Developer Forums](https://developer.apple.com/forums/thread/764221)
  — optagefunktionen er slået fra i EU, og udskrivningen findes ikke på dansk
- [iPhone Recording Stops After a Phone Call, BlackBox](https://blackboxrecorder.in/fix/iphone-recording-stops-after-call) — opkaldet tager mikrofonen eksklusivt
