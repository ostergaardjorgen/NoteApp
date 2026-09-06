# Integration med cloud services

*OneDrive, Google Drev, iCloud, Dropbox — fra din Apple- eller Android-enhed ind i HeyPia af sig selv*

Cloud services — eller skytjenester, som de også kaldes — er det, der
binder din telefon sammen med din pc.

De fleste møder holdes ikke ved skrivebordet. En samtale i bilen, en aftale
på gangen, en gennemgang hos kunden — telefonen er optageren, og den ligger i
lommen i forvejen.

Det her afsnit beskriver, hvordan du sætter kæden op én gang. Derefter er der
ingen manuelle trin: du optager på telefonen, og optagelsen står klar i
HeyPia, næste gang du åbner appen.

## Hele vejen på fire led

1. **Du optager** på din iPhone, iPad eller Android-telefon med den optager,
   der allerede er på enheden.
2. **Optagelsen gemmes i en cloud-mappe** — iCloud Drive, OneDrive, Google Drev
   eller Dropbox.
3. **Cloud servicens program på pc'en** synkroniserer mappen ned på disken.
4. **HeyPia holder øje med mappen** og tilbyder filen på Optagelser.

Led 1 og 2 gør du på telefonen. Led 3 og 4 sætter du op én gang på pc'en.

## Hvorfor der ikke logges ind nogen steder

HeyPia beder aldrig om adgang til din Google-, Microsoft- eller Apple-konto
for at hente optagelser. Der er ingen adgangskode, intet samtykke og ingen
ekstra leverandør, der får adgang til dine data.

Grunden er enkel: iCloud, OneDrive, Google Drev, Dropbox, Nextcloud og
Synology Drive har alle et program til Windows, der synkroniserer til en helt
almindelig mappe på disken. HeyPia ser en mappe. Ikke andet.

Det betyder også, at kæden virker med enhver tjeneste, der kan lægge en mappe
på din pc — også en, vi aldrig har hørt om.

## Trin 1 — vælg tjenesten, du allerede bruger

Vælg den, du har i forvejen. Der er ingen fordel ved at tilføje en ny.

| Tjeneste | Passer typisk til | Programmet på pc'en |
| --- | --- | --- |
| iCloud Drive | iPhone og iPad | iCloud til Windows |
| Google Drev | Android | Google Drive til computer |
| OneDrive | Windows og Microsoft 365 | Følger med Windows |
| Dropbox | Begge dele | Dropbox til Windows |
| Nextcloud, Synology Drive | Egen server eller NAS | Tjenestens eget program |

## Trin 2 — installér cloud servicens program på pc'en

Uden det ligger filerne kun i skyen, og så er der ingen mappe at holde øje
med. Programmet henter du hos tjenesten selv:

- **Apple:** «iCloud til Windows» fra Microsoft Store.
- **Google:** «Google Drive til computer» fra google.com/drive/download.
- **Microsoft:** OneDrive er allerede på maskinen; du skal blot logge ind.
- **Dropbox:** Dropbox-programmet fra dropbox.com/install.

Log ind med den **samme konto som på telefonen**. Det er hele pointen: det er
kontoen, der binder de to enheder sammen.

## Trin 3 — peg HeyPia på mappen

Åbn **Indstillinger → Filer**. Under «Overvågede mapper» finder appen selv de
cloud services, der er installeret, og foreslår én mappe i hver:
`<cloud service>\HeyPia`.

For hver række kan du:

- **rette stien** — forslaget er kun et forslag. Bruger du allerede en anden
  mappe til dine optagelser, så skriv den i stedet.
- **Gennemse** — find mappen frem i stedet for at skrive den. Vælgeren åbner
  dér, hvor stien peger hen.
- **Gem** — opretter mappen, hvis den mangler, og begynder at holde øje med
  den. Linjen nedenunder bekræfter, hvad der blev gemt.
- **fjerne rækken** — bruger du ikke tjenesten, skal den ikke stå der.

Sæt hak i **«Læg ind automatisk»**, hvis optagelserne skal ind uden at du
tager stilling. Uden hak bliver hver fil tilbudt øverst på Optagelser, og du
siger ja eller nej. Standarden er uden hak, fordi en delt mappe kan modtage
en lydfil, der ikke er et møde.

Se **Lydfiler udefra** for detaljerne om, hvordan mapperne overvåges.

## Trin 4 — optag på telefonen

### iPhone og iPad

1. Åbn **Memoer** (Voice Memos) og optag.
2. Tryk på optagelsen, vælg **Del → Arkiver i Filer**.
3. Vælg **iCloud Drive → HeyPia**.

Bruger du OneDrive, Google Drev eller Dropbox på din iPhone i stedet, står de
samme steder i delemenuen. Vælg HeyPia-mappen i den tjeneste, du satte op i
trin 3.

Optager du meget, kan det betale sig at oprette en genvej i Genveje-appen,
der gemmer direkte i mappen. Så er det ét tryk i stedet for tre.

### Android

1. Optag med **Optager** (Google Recorder), **Diktafon** eller den optager,
   der fulgte med telefonen.
2. Tryk **Del**, og vælg **Google Drev**, **OneDrive** eller **Dropbox**.
3. Vælg HeyPia-mappen.

På mange Android-telefoner kan optageren indstilles til at gemme direkte i en
synkroniseret mappe. Er den mulighed der, så brug den — så forsvinder trin 2
og 3 helt.

## Hvad der så sker

HeyPia kigger i mapperne **hvert minut**, så længe appen er åben, og med det
samme når den starter. En fil skal have ligget stille i **tyve sekunder**, før
den regnes som færdig — ellers kunne den blive læst ind, mens den stadig
bliver hentet ned.

Optagelsen dateres efter **filens egen dato**. En samtale fra i tirsdags
sorterer som i tirsdags, også selv om den først når frem til pc'en om
torsdagen.

Hver fil tilbydes **én gang**. Siger du nej, kommer den ikke igen.

## Filer, der kun ligger i skyen

iCloud og OneDrive lader ofte filen blive i skyen og viser kun en pladsholder
på disken: rigtigt navn, rigtig størrelse, intet indhold.

HeyPia **læser aldrig i en fil** under scanningen — kun navn, størrelse og
dato. Ellers ville appen hente hele cloud-mappen ned, bare fordi den kiggede
efter. En pladsholder er stadig et fund; den bliver hentet, når du lægger den
ind, og det bliver sagt.

## Optagelser fra en telefon har ét spor

En optagelse, HeyPia selv laver, har to sider: din mikrofon og
mødedeltagerne. En fil udefra har én. Alt står under den samme taler, uanset
hvor mange der var med, og det kan ikke skilles ad bagefter.

Det betyder ikke, at udskriften bliver dårligere — kun at der ikke står,
hvem der sagde hvad.

## Hvad der ikke sker

- **Din kildefil røres aldrig.** Der kopieres. Originalen bliver liggende i
  cloud-mappen, så telefonen beholder den.
- **Der sendes ingenting til cloud servicen.** HeyPia læser en mappe; den
  skriver ikke i den og logger ikke på noget.
- **Lyden forlader ikke din maskine.** Optagelsen skrives ud lokalt. Kun
  teksten sendes videre, og kun når du beder om et dokument.

## Når det ikke virker

**Filen dukker ikke op.** Se efter, at cloud servicens program kører, og at
mappen faktisk findes på disken — åbn den i Stifinder. Står der en sky ud for
filen, er den ikke hentet ned endnu; det er stadig et fund, men vent til
synkroniseringen er færdig.

**Mappen står ikke på listen.** Appen finder cloud services ved at spørge
Windows om, hvor de synkroniserer hen. Er programmet lige installeret, så
genstart HeyPia. Ellers kan du altid tilføje mappen med **Tilføj mappe**.

**Filen blev tilbudt, men jeg sagde nej ved en fejl.** Brug **Glem hvad der
er set** under Indstillinger → Filer. Så bliver alle filer i mapperne tilbudt
igen.

**Forkert filtype.** Der læses `.m4a`, `.mp3`, `.wav`, `.aac`, `.mp4`, `.wma`
og `.flac`. Både iPhone og Android optager som standard i et af dem.
