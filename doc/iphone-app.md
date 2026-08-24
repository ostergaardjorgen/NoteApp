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
«spørg efter samtalen» ikke kan bygges for et **almindeligt** opkald.

**Men de kan alle sammen bygges, hvis appen selv ringer op.** Reglen gælder
en anden apps lyd — sin egen må en app godt optage. Det ændrer billedet nok
til, at det har sit eget afsnit: «Vejen udenom».

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

## Vejen udenom: appen skal selv ringe op

Alt ovenfor handler om **Telefon-appens** opkald. Reglen er, at ingen app må
røre en anden apps lyd — og det gælder begge veje: heller ikke NoteApp må
lytte med på Telefon-appen.

Men **sin egen lyd må en app godt røre.** Går opkaldet gennem en VoIP-app,
ligger begge stemmer inde i den app selv, og så er optagelse en helt
almindelig ting at gøre. Det er også forklaringen på, at der ligger apps i
App Store, der optager opkald: de ringer selv op, eller de kobler en
konferencebro ind på linjen.

**Og der er en gevinst oveni.** En VoIP-app må bruge CallKit, og så opfører
opkaldet sig som et rigtigt opkald: det kommer frem på låseskærmen, det kan
tages i CarPlay, og det kan startes med Siri. Den CarPlay-mur, der står i
afsnit 3, gælder ikke for opkald. **Det løser håndfri-problemet i bilen.**

### Fire veje, og de koster noget forskelligt

**1. Lån en app, der optager i forvejen**
*Lille arbejde · og det står og falder med, hvor lyden lander*

HubSpot, Aircall, Dialpad, RingCentral og flere har opkald i deres egen
mobilapp, optager samtalen, og lader dig hente filen bagefter. HubSpots
opkaldstjeneste i mobilappen optager, og lydfilens adresse ligger på
`hs_call_recording_url`, som kan hentes med
`GET /crm/v3/objects/calls?properties=hs_call_recording_url`.

Der skal **slet ingen iPhone-app bygges**. Der skal bygges et stykke på
Windows-siden, der henter optagelserne ned og lægger dem ind — og indlæsningen
findes allerede.

**Men lyden ligger i deres sky**, for HubSpots vedkommende typisk i USA. Det
er præcis dét, NoteApp sælges på ikke at gøre.

**Og dét er grunden til at kigge dansk og nordisk.** Vejen holder, hvis
udbyderen er i EU:

- **Flexfone** (drevet af Dstny, dansk, med TDC som netværk) og **Telavox**
  har begge optagelse som en del af løsningen. Telavox optager både ind- og
  udgående og lader dig hente filerne i appen eller portalen.
- **Om der findes et åbent API til at hente dem automatisk, kunne jeg ikke
  efterprøve.** Hos den slags erhvervsudbydere er API-adgang som regel en
  partneraftale og ikke en selvbetjeningsside. Det er et opkald til en
  sælger, ikke en søgning — og det er det første, der skal afklares, hvis
  vejen skal bruges.

**2. En nordisk udbyder med et åbent API — den vej, der kan bygges i morgen**
*Lille til middel · og den kræver ingen iPhone-app overhovedet*

**46elks** er svensk og har et offentligt API, der er efterprøvet:

- `recordcall` optager **hele samtalen** og sender et webhook med et link,
  når opkaldet slutter.
- Optagelsen kan leveres **direkte til en adresse, du selv vælger** — altså
  til din egen maskine eller server, i stedet for at blive liggende hos dem.
- Ellers hentes den med `GET https://api.46elks.com/a1/recordings/{id}`, og
  **der er 72 timer til at gøre det.** Efter det er den væk.
- Både SIP og WebRTC understøttes, så NoteApp kan enten være telefonen selv
  eller bare være det, opkaldet føres igennem.

**Den billigste første prøve ligger her, og den kræver ingen app:** et nummer
hos udbyderen, der viderestiller til din mobil og optager undervejs. Filen
lander på din egen maskine, og NoteApp læser den ind. Så kan det måles, om
optagne opkald overhovedet er noget værd i praksis — før der bygges en
iPhone-app for at få dem.

**3. NoteApp ringer selv op**
*Stor · og den, der giver den fulde oplevelse*

NoteApp bliver sin egen lille telefon: et nummer hos en udbyder, opkald ud og
ind, CallKit så det ser ud som et rigtigt opkald.

Det afgørende er, at optagelsen kan ske **på telefonen** — appen har jo lyden
selv — og gå direkte i mappen i iCloud. Udbyderen transporterer samtalen, men
gemmer den ikke.

Det flytter grænsen ét sted, og det skal formuleres ærligt i
salgsargumenterne: et telefonopkald bliver altid båret af et teleselskab. Det
har det altid gjort, også uden NoteApp. Argumentet er og bliver, at
**optagelsen** ikke uploades — og det holder her.

Arbejdet er til gengæld rigtigt: en VoIP-klient er WebRTC eller SIP,
push-beskeder for indgående opkald, lydsessioner, netværk der falder ud
midt i en køretur. Det er større end resten af iPhone-appen tilsammen.

**4. Egen central**
*Størst · fuld kontrol*

3CX eller Asterisk på en server, du selv ejer. Optagelsen sker på centralen og
forlader aldrig noget, du ikke bestemmer over. Det er svaret, hvis en kunde
med en it-afdeling spørger — ikke svaret for en selvstændig med en iPhone.

### Hvad det ikke løser

Ringer nogen dig op på dit **almindelige nummer**, er du tilbage i
Telefon-appen, og så gælder alle tre mure igen. En VoIP-vej virker kun for de
opkald, der føres gennem den. Skal det være alle, skal nummeret flyttes — og
det er en beslutning om ens telefoni, ikke om en app.

### Juraen bliver ikke lettere

At appen **må** optage, betyder ikke, at man må lade være med at sige det.
App Store-retningslinje 2.5.14 kræver samtykke og en tydelig besked under hele
optagelsen, og modparten sidder på en almindelig telefon og kan ikke se en
skærm. Beskeden skal derfor være **hørbar** — en linje, der læses op, når
optagelsen begynder. Det er også sådan, Apples egen funktion gør det.

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
- [Receive calls in HubSpot when using calling apps, HubSpot docs](https://developers.hubspot.com/docs/guides/apps/extensions/calling-extensions/receive-incoming-calls)
  og [Fetching Call Recording Over API, HubSpot Community](https://community.hubspot.com/t/fetching-call-recording-over-api/115431)
  — `hs_call_recording_url` på opkaldsobjektet
- [HubSpot Calls from Mobile (iOS og Android), HubSpot Community](https://community.hubspot.com/t5/Releases-and-Updates/Live-HubSpot-Calls-from-Mobile-iOS-amp-Android/ba-p/713273)
  — opkald og optagelse fra mobilappen på betalte pladser
- [Mastering VoIP Audio with CallKit and WebRTC on iOS](https://medium.com/@tsivilko/mastering-voip-audio-with-callkit-and-webrtc-on-ios-0f2092402331)
  — en VoIP-app ejer sin egen lyd
- [3CX self-hosted, 3CX Forums](https://www.3cx.com/community/threads/3cx-integration-with-a-self-hosted-ai-eu-data-protection-considerations.136377/)
  — central på egen server
- [46elks: Recordcall — record an entire call](https://46elks.com/docs/voice-recordcall),
  [View phone call recording using ID](https://46elks.com/docs/get-recording-id) og
  [API-overblik](https://46elks.com/docs/overview) — webhook med link, 72 timers frist,
  levering til egen adresse, SIP og WebRTC
- [Flexfone](https://www.flexfone.dk/) — dansk, drevet af Dstny, TDC som netværk
- [Telavox: IP-telefoni](https://telavox.dk/funktioner/ip-telefoni/) — optager både ind-
  og udgående, filerne hentes i app eller portal
- [iPhone Recording Stops After a Phone Call, BlackBox](https://blackboxrecorder.in/fix/iphone-recording-stops-after-call) — opkaldet tager mikrofonen eksklusivt
