# Optagelse

*To spor, fire måder at starte på — og hvad der sker, hvis noget går galt*

## De fire måder at starte

- **Knappen** øverst til venstre. Den virker altid.
- **Genvejstasten**, hvor som helst i Windows. Den står ved siden af knappen,
  så du kan se, hvad den er sat til.
- **Vågeordet.** Sig «Hej Pia, optag møde» uden at røre noget. Se nedenfor.
- **En aftale i kalenderen**, der er markeret til automatisk optagelse. Så går
  den i gang to minutter før mødet — hvis appen kører.

## Møde eller webinar

Et **møde** optager to spor: din mikrofon og det, computeren afspiller. Begge
skrives ud, så det tager omtrent dobbelt så lang tid — til gengæld er begge
sider med, og hver replik viser, hvilken side der talte.

Et **webinar** optager kun det, computeren afspiller. Du lytter; du taler
ikke. Det fylder det halve, skrives ud på det halve af tiden, og bliver
bedre: der er intet ekko fra din egen mikrofon og ingen overlappende tale.

Var mødet fysisk, og der ikke kom lyd på højttalersporet, bliver det tomme
spor slettet af sig selv, når du stopper. Du skal ikke vælge «fysisk» eller
«online» på forhånd.

## Telefonopkald fra din mobil

Med **Telefonlink** kan du tage telefonen på computeren — og så kan HeyPia
optage samtalen som ethvert andet møde.

### Hvad Telefonlink er — og hvad HeyPia gør ved det

Telefonlink er Microsofts egen app. Den er med i Windows 11 i forvejen; er den
væk, kan den hentes i Microsoft Store. Den forbinder din telefon til
computeren, så opkald, beskeder og notifikationer kommer frem på skærmen.

**HeyPia rører ikke Telefonlink.** De to taler ikke sammen, og der er ingen
opsætning mellem dem. Når et opkald går i gang, kobler Windows telefonens lyd
til computeren over Bluetooth — og det er dét, HeyPia lægger mærke til. Derfra
er det en optagelse som enhver anden: lyden bliver på maskinen, den skrives ud
til tekst her, og du kan lave et referat af den.

Det betyder også, at rækkefølgen er ligegyldig. Har du Telefonlink kørende i
forvejen, virker det med det samme, du installerer HeyPia — og omvendt.

### Sæt Telefonlink op — Android

Kravene er en pc med Windows 10 (opdateringen fra oktober 2022 eller nyere)
eller Windows 11, og en telefon med **Android 10 eller nyere**. Telefon og pc
skal være på **samme wi-fi**.

1. Installér **Link til Windows** på telefonen — fra Google Play eller Galaxy
   Store. På mange Samsung-telefoner er den der i forvejen.
2. Åbn **Telefonlink** på computeren og vælg **Android**.
3. Log ind i appen på telefonen med **den samme Microsoft-konto** som på pc'en.
4. Scan QR-koden på pc-skærmen med telefonen, og godkend de tilladelser, den
   beder om.
5. Åbn **Opkald** i Telefonlink og tryk **Kom i gang**. Der kommer en besked på
   telefonen — tryk **Tillad**.

Opkald kræver **Bluetooth** mellem telefonen og pc'en. Wi-fi alene er ikke nok:
beskeder og billeder kommer over nettet, men selve lyden går over Bluetooth.

### Sæt Telefonlink op — iPhone

Kravene er en pc med **Bluetooth Low Energy (BLE)** og en iPhone med **iOS 16
eller nyere**. Du skal bruge en **personlig** Microsoft-konto — en arbejds-
eller skolekonto virker ikke.

1. Tænd **Bluetooth** på både pc og iPhone.
2. Åbn **Telefonlink** på computeren og vælg **iPhone**.
3. Scan QR-koden med iPhonens **kamera** — du skal ikke installere en app
   først. *Link til Windows* findes i App Store, men er ikke nødvendig for at
   parre.
4. Godkend på telefonen. Slå derefter tilladelserne til under **Indstillinger →
   Bluetooth → ⓘ ud for din pc**: systemnotifikationer, beskeder og kontakter.

En iPhone giver mindre end en Android — Apple lukker mindre op. Opkald,
beskeder og notifikationer virker; apps og billeder gør ikke.

### Microsofts egne vejledninger

Detaljerne skifter, og Microsoft er stedet, der har dem først:

- [Krav og opsætning](https://support.microsoft.com/da-dk/windows/apps/phonelink/phone-link-requirements-and-setup)
- [Opsætning af opkald](https://support.microsoft.com/da-dk/windows/apps/phonelink/setting-up-calls-in-the-phone-link)
- [Ofte stillede spørgsmål](https://support.microsoft.com/da-dk/windows/apps/phonelink/frequently-asked-questions-about-the-phone-link)

### Så skal HeyPia have to ting

Når telefonen er koblet på, mangler der to indstillinger i Windows. Begge
handler om lyd, og begge overses.

1. **Giv Telefonlink lov til at bruge mikrofonen:** **Indstillinger →
   Beskyttelse af personlige oplysninger → Mikrofon → Telefonlink**.

   Uden den kan Telefonlink godt *afspille* opkaldet, men ikke *optage* dig.
   Det lyder som en fejl i headsettet, og det er det ikke: du kan høre den
   anden, men den anden kan ikke høre dig.

2. **Vælg dit headset som standardkommunikationsenhed.** Det her er det
   afgørende skridt, og det er ikke det samme som at vælge standardenhed.

### De to standarder

Windows har **to** standardhøjttalere og **to** standardmikrofoner:

| | Bruges af |
|---|---|
| **Standardenhed** | musik, video, YouTube |
| **Standardkommunikationsenhed** | opkald — Telefonlink, Teams, Zoom |

De kan pege hvert sit sted, og det gør de tit: skærmens højttalere til musik,
headsettet til møder. Sætter du kun den ene, ender opkaldet et andet sted, end
du tror.

Sådan sætter du dem:

1. Højreklik på højttalerikonet ved uret → **Lydindstillinger**
2. Rul ned til **Flere lydindstillinger**
3. Fanerne **Afspilning** og **Optagelse**
4. Højreklik på dit headset i hver fane → **Angiv som standardkommunikationsenhed**

Har du parret telefonen direkte over Bluetooth ved siden af Telefonlink, står
den også i listen som en lydenhed — typisk *«… Hands-Free»*. Er den valgt som
kommunikationsenhed, forsøger Windows at bruge **telefonen** som mikrofon i
stedet for dit headset, og så virker headsettets mikrofon ikke.

### Sådan sikrer du, at hele samtalen kommer med

Et opkald har to stemmer, og de kommer to forskellige veje ind: **din** gennem
mikrofonen, **modpartens** ud af højttaleren. HeyPia optager begge, men den
optager fra de enheder, der er valgt i appen — under **Indstillinger → Lyd**.

**Peger de to steder ikke det samme sted hen, mangler modparten i optagelsen.**
Din egen stemme er der, udskriften ser hel ud, og du opdager det først, når du
leder efter noget, den anden sagde.

Derfor: vælg **det samme headset** tre steder — som standardkommunikationsenhed
i Windows (begge faner), og som mikrofon og højttaler i HeyPia.

Gør du det ikke, siger appen selv til. Boblen, der spørger, om samtalen skal
optages, får en gul linje: *«Opkald kører på ‹enhed›, men HeyPia optager fra en
anden højttaler.»* Ret det, før du trykker optag — bagefter er det for sent.

### Appen spørger selv, når du ringer eller bliver ringet op

Så snart samtalen er i gang, spørger HeyPia nede ved uret, om den skal optages.
Det gælder både, når du ringer op, og når du tager telefonen.

Sproget vælger du i samme boble: **dansk** er valgt på forhånd, og ét tryk
skifter til **engelsk**. Mere spørges der ikke om — ingen mappe og intet navn,
hverken før eller efter samtalen.

**Der optages aldrig af sig selv.** Appen kan kun spørge. Og sig det til den, du
taler med — reglerne for at optage en telefonsamtale er ikke de samme alle
steder.

Optagelsen lander i folderen **Opkald** i optagelseslisten, med det
nyeste øverst. Kun opkald gennem Telefonlink havner der af sig selv — et
Teams-møde er et møde og bliver, hvor møder er.

Den får navn efter, hvornår du talte: **08-09-2026_22:45**. Man navngiver ikke
et opkald, mens telefonen ringer, og uden et navn ville de alle sammen stå som
«Uden navn». Passer folderen ikke, kan du trække optagelsen over i en anden.

## Vågeordet

Vil du hverken trykke eller lede efter en tast, kan du sige det i stedet: sig
vågeordet og derefter, hvad der skal ske. «Hej Pia, optag møde.»

Kommandoerne er din egen liste under **Diktering → Kommandoer**, og du kan
selv skrive flere. Der kan ikke køre noget, som ikke står på listen.

Ordet **vælges mellem to** — «Hej Pia» og «Hey Pia». Det står som to knapper
under **Diktering → Kommandoer**, og der er ikke et felt at skrive i. Grunden
er målt: motoren fordeler sin sikkerhed mellem alt, den lytter efter, så to
stavemåder af det samme ord konkurrerer med hinanden og ser svage ud hver for
sig.

Under knapperne lærer du appen din udtale ved at sige **det valgte ord tre
gange**. Så leder den efter det, din stemme og din mikrofon faktisk lyder som,
og ikke kun efter bogstaverne. Skifter du ord bagefter, skal du træne igen —
det gamle ord ville ellers stjæle sikkerhed fra det, du nu siger.

**Under et webinar virker vågeordet.** Et webinar optager højttaleren og ikke
din mikrofon, så det, du siger, forstyrrer ingenting — og det er netop dér,
man sidder og lytter til noget andet og kommer i tanker om noget. Under et
almindeligt møde er det slået fra: dér er mikrofonen i brug til noget
vigtigere.

## Noter undervejs

Skriv en note og tryk Enter. Den får det tidspunkt, den blev skrevet, så den
kan flettes ind i transkriptionen det rigtige sted bagefter.

**Ctrl+B** sætter et bogmærke uden tekst — til når der ikke er tid til at
skrive.

## Pause

Uret standser sammen med lyden, så noternes tidsstempler bliver ved med at
passe. Der optages intet imens; mikrofonen slippes.

## Sådan står optagelserne i træet

Til venstre under **Optagelser** står dine mapper — «Møder», «Webinarer» og
dem, du selv har lavet. Nederst står to, som appen selv fylder: **Telefon
opkald** og **Arkiv**. Der er ikke længere en
«Foldere»-rod over dem alle sammen. Den svarede på et spørgsmål, ingen
stiller, og den kostede et helt niveau indrykning i en smal spalte.

Hver optagelse fylder **én linje**. Dato og længde står i boblen, når musen
hviler på den — de er rigtige nok, men det er sjældent dét, man leder efter,
og de fordoblede højden på hver eneste række.

Skal en optagelse ud af en mappe eller frem fra arkivet, så slip den ved siden
af træet.

## Hvad der er sket med den her optagelse

Vælger du en optagelse, kommer der tre faner frem over indholdet:
**Udskrift**, **Dokumenter** og **Historik**. De vises først dér. Uden en
valgt optagelse ville de være tre knapper, der skifter mellem tre tomme ruder.

**Historik** er optagelsens egen: hvad den hed før, hvornår lyden blev skrevet
ud, hvilke dokumenter der kom ud af den, og hvornår den blev flyttet eller
lagt i arkivet. Nyeste øverst.

Det er ikke den samme som **Historik** i menuen. Den er et driftslog over,
hvad appen har lavet, hvor længe det tog, og om det gik galt. Den her er en
sagsmappe for ét møde.

## Hvis maskinen går ned

Lyden skrives i stumper af tredive sekunder undervejs. Går strømmen, eller
lukker appen uventet, mister du højst det sidste stykke — ikke hele mødet.

Næste gang appen starter, samler den de efterladte stumper til en optagelse.

## Hvor meget fylder det

En times møde med to spor er cirka 220 MB, fordi lyden gemmes ukomprimeret.
Det giver den bedste udskrift. Teksten fylder til sammenligning 56 KB.

Lyden kan slettes, når mødet er skrevet ud — se **Plads og lydfiler**.
