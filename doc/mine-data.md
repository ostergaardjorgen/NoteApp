# Mine data — hvor de ligger, og hvad der forlader maskinen

> ## ⚠️ Grundprincippet er ændret 19. august 2026
>
> Dokumentet herunder blev skrevet, da **alt** kørte lokalt, og det bærer
> stadig præg af det. Læs det som en protokol over, hvad der blev besluttet
> hvornår — ikke som en beskrivelse af, hvad appen gør i dag.
>
> **Sådan er det nu:**
>
> | | |
> |---|---|
> | Lyd | Forlader **aldrig** maskinen. Optagelse og udskrift sker lokalt med Whisper. |
> | Udskriften | Sendes til Mistral AI's europæiske endepunkt, **når du beder om et dokument** — og kun da. |
> | Noter, skabeloner, dokumenter, indstillinger | Bliver på maskinen. |
> | Telemetri, fejlrapportering, skysynkronisering | Findes ikke. |
>
> Skiftet var et bevidst valg, ikke et brud: dokumentdelen kunne ikke laves
> lokalt i en kvalitet, der var til at bruge — det er målt, se
> `maaling-sky.md`. Til gengæld er dokumentdelen den **eneste** vej ud, den
> kræver din egen API-nøgle, og appen kan bruges til optagelse og udskrift
> helt uden den.
>
> Hvad vi ved og ikke ved om leverandøren står under **Compliance** i appen
> og i `mistral-dpa.md`. Dér står også det, vi ikke kan garantere.

## Det oprindelige grundprincip (10. august 2026, delvist ophævet)

**Ingen feature i NoteApp må introducere en risiko for, at data kan forlade den pc, appen er installeret på.** Det er ikke en anbefaling og ikke en standardindstilling, der kan skrues på — det er en grænse, der ligger fast. En feature, der bryder den, bliver ikke bygget, uanset hvor nyttig den er.

Besluttet 10. august 2026. Det kostede Fase 3, auto-resuméet — se afsnittet nederst. **Ophævet for udskriften 19. august 2026**, se rammen øverst. Reglen gælder uændret for lyd, noter og alt andet.

### Retningen er det afgørende

Præciseret 10. august 2026. Reglen hed først "ingen netværkskald overhovedet", og det var for groft: den forbød også at hente motor og model ned, og dermed at appen kunne installeres af andre end den, der i forvejen havde en 4 GB modelfil liggende.

| Retning | Regel |
|---|---|
| **Ud af maskinen** | Forbudt for lyd, noter og metadata. Siden 19. august 2026 gælder én undtagelse: **udskriften**, når du selv beder om et dokument. Se rammen øverst. |
| **Ned på maskinen** | Tilladt for **Whisper-motoren og sprogmodellen** — og kun efter en informeret godkendelse. |

Hentningen sender intet om dig med. Den beder om en navngiven fil fra en navngiven adresse, og appen viser begge dele, før den spørger. Det eneste, modtageren kan udlede, er at nogen på din internetforbindelse har hentet en Whisper-model — samme spor som at hente et hvilket som helst program.

Netværkskoden ligger to steder, og det er med vilje kun to: `src\NoteApp.Core\Downloader.cs` henter ned (motor og model), og `src\NoteApp.Core\Llm\SkyRunner.cs` er det eneste sted, der sender noget ud. Skal der sendes fra et tredje sted, er svaret nej.

### Når noget alligevel kan få data ud

Enkelte funktioner kan i sagens natur ende med at flytte data væk fra maskinen. Backup til en netværkssti er det ene reelle eksempel: det er brugerens eget valg af destination, ikke noget appen gør af sig selv. Dér gælder tre krav uden undtagelse:

1. **Det sker kun ved en aktiv brugerbeslutning.** Aldrig som standard, aldrig som en indstilling man slår til én gang og glemmer, aldrig som en sideeffekt af noget andet man bad om.
2. **Beslutningen kræver en godkendelse i selve øjeblikket.** En advarsel, man kan læse forbi mens handlingen alligevel kører videre, er ikke en godkendelse. Handlingen skal stoppe og vente på et svar.
3. **Godkendelsen skal være informeret.** Teksten skal på skærmen liste problemstillingen konkret: *hvad* der sendes, *hvorhen*, om det er krypteret undervejs og på destinationen, *hvem* der derefter kan læse det, og hvad alternativet er, hvis man vil holde alt lokalt. Uden den liste er det ikke en beslutning — det er et klik.

**Kan de tre krav ikke opfyldes, skal funktionen nægte at køre.** En planlagt opgave kører uden nogen til at godkende; derfor må den ikke kunne skrive til en netværkssti, med mindre valget er truffet og bekræftet på forhånd, mens du sad ved maskinen.

## Grænsen er fysisk, ikke en regel man skal huske

```
C:\NoteApp\                        KODE — git-repo, ligger på GitHub
  src\  scripts\  doc\  fase0\

C:\AppNoter\                       DINE DATA — aldrig i git, aldrig på GitHub
  learning.db                      ordbog og indlærte rettelser
  ordliste.txt                     din ordliste
  indstillinger.json               valgt model, backupmappe
  Optagelser\<møde>\               lyd, noter, transskriptioner, metadata
  log\
```

De to mapper overlapper ikke. Der findes **ingen `.gitignore`-fejl der kan lække en optagelse**, fordi filerne ikke ligger i arbejdstræet til at begynde med. `.gitignore` er et sikkerhedsnet, ikke den primære beskyttelse.

**Placeringen kan ændres i appen** under *Filer og backup* → *Skift mappe*. Filerne kopieres til det nye sted, og først derefter ryddes det gamle — en afbrudt flytning skal efterlade data ét sted, ikke ingen. Valget skrives i `%APPDATA%\NoteApp\datasti.txt`, som med vilje ligger **uden for** datamappen: lå valget inde i den mappe, det selv udpeger, kunne appen ikke finde det igen efter en flytning.

Rækkefølgen er: miljøvariablen `NOTEAPP_DATA` (vinder altid), derefter pegefilen, derefter standarden `C:\AppNoter`. `UserDataPaths.AssertOutsideRepository` fejler højlydt, hvis stien nogensinde peger ind i repoet.

## Hvad der ligger på GitHub

Kildekode, spec, dokumentation og scripts. Repoet er **privat**.

Fjernet fra GitHub 10. august 2026: `ordliste.txt`. Den er nu dine data. I stedet ligger `ordliste.eksempel.txt` som skabelon.

> **Bemærk:** den oprindelige `ordliste.txt` findes stadig i git-historikken (commit v0.01-v0.05). Den indeholder kun generiske IAM-fagtermer — ingen navne, kunder eller personoplysninger. Vil du have den helt væk, kan historikken skrives om; sig til.

Testteksten `fase0\oplaesning\testtekst.md` bruger **opdigtede navne** og kan ligge offentligt. Udskifter du dem med rigtige kollegaer og kunder, så gem din version som `testtekst.personlig.md` — det mønster er allerede i `.gitignore`.

## Backup

**Sikkerhedskopien tages i appen** under *Indstillinger* → *Backup*. Der kan du vælge mappe, se de gemte arkiver og tage en kopi med det samme.

**Lydfiler er som standard IKKE med.** Det er ikke en spareøvelse: en times optagelse fylder over 100 MB, mens noter, udskrifter, dokumenter, skabeloner og indstillinger tilsammen er få MB. Lyden er samtidig det, der er lettest at undvære — arbejdet ligger i udskriften og i de dokumenter, der er lavet ud af den. Tages lyden med hver uge, bliver arkivet så stort, at man holder op med at tage backup, og så er man dårligere stillet end med en lille kopi hver gang. Vil du have lyden med, er der et afkrydsningsfelt, og appen viser forskellen i størrelse, før du vælger.

Loggen skriver, om lyden var med. Uden det kan man ikke bagefter afgøre, om et arkiv rækker til at gendanne et helt møde eller kun teksten.

Alt sker lokalt. Hverken appen eller scripterne har netværkskald, skytjeneste eller telemetri i backup-vejen.

```bash
powershell -File C:\NoteApp\scripts\backup-mine-data.ps1
```

Arkivet lander som standard i `%USERPROFILE%\NoteApp-backup`, altså på din egen maskine.

Vælger du en destination **uden for maskinen**, stopper scriptet og beder om en skrevet bekræftelse først.

> **Et drevbogstav siger intet om, hvor drevet ligger.** På denne maskine er `E:` og `H:` mappede netværksdrev — de ser ud som almindelige diske, men en backup dertil forlader pc'en. Derfor tjekker scriptet drevtypen i stedet for at se på stien, og det viser dig, hvilke drev der rent faktisk er lokale, når det spørger.

Bekræftelsen fungerer sådan: Det lister samtidig, hvad beslutningen indebærer: hvad arkivet indeholder, at det ikke er krypteret, at det derefter kan læses af enhver med adgang til den share, og hvad du kan gøre i stedet. Svarer du ikke `JA`, tages der ingen backup. Kører scriptet uden et vindue at spørge i — fx som planlagt opgave — nægter det at bruge netværksstien i stedet for at gætte sig til et ja. Det er de tre krav i grundprincippet, håndhævet i kode og ikke kun i dokumentation.

Ugentlig automatik:

```bash
powershell -File C:\NoteApp\scripts\planlaeg-backup.ps1
```

Standard er hver mandag 09:00. Er maskinen slukket, køres den ved næste opstart. Fjern den igen med `-Fjern`.

Gendannelse — **prøv den mindst én gang, før du får brug for den:**

```bash
powershell -File C:\NoteApp\scripts\gendan-mine-data.ps1 -Proeve
```

`-Proeve` pakker ud i en midlertidig mappe, tjekker at `learning.db` er en gyldig SQLite-fil, og rører ikke dine nuværende data. Uden flaget gendannes for alvor — og der tages automatisk et sikkerhedsarkiv af det nuværende først.

### Tre ting backup-scriptet gør, som ikke er selvfølgelige

1. **Det verificerer arkivet efter oprettelse.** Et arkiv der ikke kan åbnes er ikke en backup, og det skal opdages nu — ikke den dag du får brug for det.
2. **Det logger hvilken mappe der blev taget backup af.** En backup-log der ikke siger *hvad* den sikrede, kan ikke afsløre at den sikrede den forkerte mappe.
3. **Det nægter at køre, hvis datamappen hverken har `learning.db` eller `Optagelser\`.** En planlagt opgave kan køre med et andet miljø end den session, der oprettede den, og så peger den et andet sted hen. Uden dette værn ville du få en stribe grønne "backup gennemført" af en tom mappe. Derfor skriver `planlaeg-backup.ps1` også datamappen eksplicit ind i opgaven i stedet for at lade den slå den op selv.

## Fase 3 er fjernet — og hvorfor

Fase 3 var auto-resumé i appen: transskriptionen sendt til Claudes API, svaret tilbage som referat. Den var besluttet beholdt 7. august 2026.

**Den er fjernet 10. august 2026.** Den var det eneste netværkskald i hele appen og dermed det eneste sted, data kunne forlade maskinen. Ingen indpakning gjorde den forenelig med grundprincippet øverst: et eksplicit valg pr. møde reducerer risikoen, men fjerner den ikke — koden til at sende ville stadig ligge i appen, og en fejl, en genvej eller en senere ændring kunne aktivere den. Grænsen holder kun, hvis muligheden ikke findes.

**Det du mister, er mindre end det lyder.** Eksport-markdownen med kontekstblok var aldrig et fallback — den var altid den primære vej ud. Du kopierer eksporten ind i Claude og arbejder referatet igennem, præcis som du gør i dag. Forskellen er, at *du* flytter teksten, bevidst, for det møde du har valgt. Appen gør det aldrig af sig selv.

Skulle auto-resumé blive relevant igen, er den eneste vej en **lokal model på din egen maskine**. Det er en ny beslutning, ikke en genoplivning af den gamle.

Optagelse, transskription og eksport kører udelukkende på din maskine. Whisper er en lokal binær, og modellen ligger på din disk.

*Sætningen sluttede oprindeligt «og der er intet kald ud af huset overhovedet». Det gælder ikke længere for dokumentdelen — se rammen øverst i dokumentet.*
