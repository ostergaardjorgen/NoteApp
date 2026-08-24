# Overvågede mapper

*Bygget 24-08-2026. Roadmap 2.2.*

Peg appen på en mappe. Dukker der en lydfil op, bliver den tilbudt — eller lagt
ind af sig selv, hvis du beder om det.

Det er den anden halvdel af punkt 2.1: filer udefra kunne læses ind i hånden,
men skulle trækkes ind hver gang. Med en overvåget mappe ligger optagelsen fra
telefonen i NoteApp, når du sætter dig ved computeren.

---

## Hvad der overvåges som standard

**Ingenting.** Listen er tom, indtil du selv lægger noget på den, og det er et
valg: en app, der begynder at kigge i mapper, uden at nogen har bedt om det,
er ikke en funktion — det er en overraskelse.

Til gengæld **finder appen selv skytjenesterne på maskinen** og tilbyder dem
under Indstillinger → Filer. For hver af dem foreslås én mappe:

```
<skytjenestens mappe>\NoteApp
```

Findes den ikke, står der en knap, der opretter den. Findes den, står der en
knap, der slår overvågningen til.

### Hvorfor lige den mappe, og ikke hele skytjenesten

At overvåge hele iCloud Drive ville betyde, at hver eneste lydfil nogen steder
i skyen skulle tages stilling til — en podcast, en ringetone, en optagelse fra
et helt andet program. En navngiven mappe er en aftale om, hvor tingene skal
ligge, og den aftale kan holdes fra begge ender: du lægger dem der, appen
kigger der.

Du kan udmærket pege på en anden mappe. Det er bare ikke det, appen foreslår
af sig selv.

### Hvordan skytjenesterne findes

Windows fører selv en liste over, hvor synkroniseringsklienterne lægger deres
filer, og det er den liste, appen spørger — ikke et gæt på stier under
brugermappen.

Det er ikke en detalje. Da funktionen blev bygget, blev der først ledt efter
iCloud Drive under `C:\Users\<dig>\`, sådan som al dokumentation antyder. Der
lå den ikke: den lå i **`C:\iCloudDrive`**, altså slet ikke under brugeren. Et
gæt havde ikke fundet den, og funktionen ville have virket, som om maskinen
ikke havde iCloud.

Det virker for iCloud, OneDrive, Google Drev, Dropbox, Nextcloud og alt andet,
der bruger Windows' egen synkroniseringsramme.

### Hvad der IKKE kan overvåges

Appens egen datamappe. Alle optagelser ligger der i forvejen som
`mikrofon.wav`, og hver eneste ville blive tilbudt som en ny fil udefra og
kunne lægges ind som en kopi af sig selv. Det afvises med en besked.

---

## Metoden

### Der kigges hvert minut

Så længe appen er åben. Der kigges også med det samme, når appen starter — den
fil, der kom i går aftes, skal ikke vente et minut mere.

**Hvorfor et ur og ikke en besked fra Windows.** Windows kan sige til, når
noget ændrer sig i en mappe, og det ville være billigere. Men det er ikke
pålideligt her: en synkroniseringsklient skriver en fil i etaper, omdøber den
undervejs og ændrer dens attributter bagefter, og beskederne kan gå tabt, når
der kommer mange på én gang. En mappe i skyen er præcis det tilfælde, hvor
beskederne svigter. Et kig hvert minut koster en filoptælling og kan ikke gå
glip af noget.

Et minut er valgt, fordi det er kortere end den tid, det tager at gå fra
telefonen til computeren.

### Der læses aldrig i en fil

Kun navn, størrelse og dato.

**Det er den vigtigste regel af dem alle.** En fil i iCloud eller OneDrive
ligger ikke nødvendigvis på disken. Der står en pladsholder med det rigtige
navn og den rigtige størrelse, og indholdet hentes først, når nogen **læser**
filen. En overvågning, der åbnede hver fil for at se, hvad den indeholdt,
ville hente hele skymappen ned — hver time, hele dagen.

En pladsholder er stadig et fund. Den kan lægges ind; det koster bare en
hentning, og **det bliver sagt**, før du trykker.

Det blev fundet ud af 24-08-2026: en talememo i iCloud var usynlig for enhver
søgning på disken, indtil den blev læst. Attributten hedder
`RECALL_ON_DATA_ACCESS`.

### En fil skal have ligget stille i tyve sekunder

En fil, der bliver kopieret ind eller synkroniseret ned, findes på disken
længe før den er hel. Læses den for tidligt, bliver den lagt ind halv — og en
halv optagelse ser ud som en optagelse, hvor mødet sluttede midt i en sætning.

### Hver fil tilbydes én gang

Appen fører en bog over, hvad der er taget stilling til. Nøglen er **sti,
størrelse og dato tilsammen**:

- Kun stien ville betyde, at en ny optagelse med samme navn aldrig blev
  opdaget.
- Alle tre betyder, at en ændret fil regnes som ny. Det er det rigtige: er
  filen skrevet om, er den ikke den samme optagelse.

Bogen svarer på «er der taget stilling til den her», ikke på «blev den brugt».
Derfor skrives både et ja og et nej ind, og derfor hedder knappen under
Indstillinger «Glem hvad der er set» og ikke «Ryd listen».

**Bogen skrives, før filen læses ind.** Går indlæsningen galt, er filen stadig
afgjort — ellers ville en fil, der ikke kan læses, blive tilbudt igen hvert
minut resten af dagen.

### Kildefilen røres aldrig

Der kopieres, aldrig flyttes, aldrig slettes. Filen ligger typisk i en
synkroniseret mappe, og at flytte den ville slette den på telefonen. Det er
ikke appens fil.

Det gælder også et nej: «Nej tak» betyder kun, at filen ikke bliver tilbudt
igen. Den bliver liggende, hvor den lå.

### Skjulte filer og systemfiler springes over

Synkroniseringsklienterne bruger dem selv. Det er ikke noget, nogen har lagt
der med vilje.

### Optagelse kan aldrig blokeres af det

Vagten læser ikke i filer, tager ingen lås og venter ikke på noget.
Optællingen sker på en anden tråd end skærmen. Går et kig galt, bliver der
kigget igen om et minut.

---

## Tilbyd eller læg ind af sig selv

Som standard bliver en fil **tilbudt**. Bjælken står øverst på Optagelser med
filens navn, størrelse, dato og hvilken mappe den kom fra, og to knapper: *Læg
ind* og *Nej tak*.

**Hvorfor en bjælke og ikke en notifikation.** En besked, der popper op,
kommer på et tidspunkt, man ikke selv har valgt — og er man midt i noget,
klikker man den væk uden at læse den. Bjælken bliver stående, indtil der er
taget stilling, og den står præcis der, hvor optagelserne er.

Hver mappe kan sættes til **læg ind automatisk**. Det er slået fra som
standard, og ikke af forsigtighed alene: en mappe, man deler med andre, kan få
en lydfil, der ikke er et møde. Bliver den lagt ind af sig selv, står den i
listen som en optagelse, ingen har bedt om.

Fejler en automatisk indlæsning, bliver filen stående som et tilbud. Så kan du
prøve selv og få fejlen at se — i stedet for at filen forsvinder tavst.

---

## Hvad der sker med filen, når den er lagt ind

Præcis det samme som ved en fil, du selv trækker ind — se punkt 2.1. Resultatet
er en helt almindelig optagelsesmappe med `meeting.json` og `mikrofon.wav`, så
transskription, søgning, dokumenter og lydoprydning ikke behøver at vide, hvor
lyden kom fra.

Optagelsen dateres efter **filens egen dato**, ikke efter hvornår den blev
lagt ind. En memo fra i tirsdags skal sortere som i tirsdags.

Og der står tre steder, at den kom udefra — på optagelsen i træet, øverst på
udskriften, og som sit eget ikon i listen. En fil fra en telefon har **ét
spor**, så alt står under den samme taler.

---

## Ingen konto, ingen nøgle, ingen ny leverandør

iCloud, Google Drev, OneDrive, Dropbox og Nextcloud har alle en klient til
Windows, der synkroniserer til en helt almindelig mappe på disken. Derfor
kræver det her **ingen OAuth, ingen tokens og ingen ny post på
Compliance-siden**. Appen ser en mappe. Ikke andet.

Det er også derfor, indstillingen ligger under *Filer* og ikke under
*Integrationer*: lå den dér, ville den love noget, den ikke gør.

Et rigtigt API mod de tre tjenester bliver først nødvendigt, hvis man vil
undvære synkroniseringsklienten. Det er en senere beslutning, ikke en
forudsætning.

---

## Efterprøvet 24-08-2026

Hele kæden kørt igennem med en talememo fra en iPhone:

| | |
|---|---|
| Skytjenester fundet på maskinen | 4, heriblandt iCloud Drive i `C:\iCloudDrive` |
| Filen fundet i den overvågede mappe | ja, med rigtig størrelse og dato |
| Lagt ind af sig selv ved appens start | ja, dateret efter filens egen tid |
| Kildefilen bagefter | uændret — samme størrelse, samme dato |
| Næste kig et minut senere | ingenting, filen står i bogen |
