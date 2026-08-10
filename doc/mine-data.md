# Mine data — hvor de ligger, og hvorfor de aldrig kan lække

## Grænsen er fysisk, ikke en regel man skal huske

```
C:\NoteApp\                        KODE — git-repo, ligger på GitHub
  src\  scripts\  doc\  fase0\

%LOCALAPPDATA%\NoteApp\            DINE DATA — aldrig i git, aldrig på GitHub
  learning.db                      ordbog og indlærte rettelser
  ordliste.txt                     din ordliste
  moeder\<møde>\                   lyd, noter, transskriptioner, metadata
  log\
```

De to mapper overlapper ikke. Der findes **ingen `.gitignore`-fejl der kan lække en optagelse**, fordi filerne ikke ligger i arbejdstræet til at begynde med. `.gitignore` er et sikkerhedsnet, ikke den primære beskyttelse.

Vil du flytte dine data — fx til en krypteret disk — så sæt miljøvariablen `NOTEAPP_DATA`. Alt følger med af sig selv, og `UserDataPaths.AssertOutsideRepository` fejler højlydt hvis stien nogensinde peger ind i repoet.

## Hvad der ligger på GitHub

Kildekode, spec, dokumentation og scripts. Repoet er **privat**.

Fjernet fra GitHub 10. august 2026: `ordliste.txt`. Den er nu dine data. I stedet ligger `ordliste.eksempel.txt` som skabelon.

> **Bemærk:** den oprindelige `ordliste.txt` findes stadig i git-historikken (commit v0.01-v0.05). Den indeholder kun generiske IAM-fagtermer — ingen navne, kunder eller personoplysninger. Vil du have den helt væk, kan historikken skrives om; sig til.

Testteksten `fase0\oplaesning\testtekst.md` bruger **opdigtede navne** og kan ligge offentligt. Udskifter du dem med rigtige kollegaer og kunder, så gem din version som `testtekst.personlig.md` — det mønster er allerede i `.gitignore`.

## Backup

Alt sker lokalt. Scripterne har ingen netværkskald, ingen skytjeneste og ingen telemetri.

```bash
powershell -File C:\NoteApp\scripts\backup-mine-data.ps1
```

Arkivet lander som standard i `%USERPROFILE%\NoteApp-backup`, altså på din egen maskine. Vælger du selv en netværkssti, siger scriptet det højt først — for så forlader arkivet maskinen, og det er ikke krypteret.

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
3. **Det nægter at køre, hvis datamappen hverken har `learning.db` eller `moeder\`.** En planlagt opgave kan køre med et andet miljø end den session der oprettede den, og så peger `LOCALAPPDATA` et andet sted hen. Uden dette værn ville du få en stribe grønne "backup gennemført" af en tom mappe. Derfor skriver `planlaeg-backup.ps1` også datamappen eksplicit ind i opgaven i stedet for at lade den slå den op selv.

## Det ene sted data kan forlade maskinen

**Fase 3, auto-resuméet.** Den sender transskriptionen til Claudes API. Det er det eneste netværkskald i hele appen, og det er derfor det er bygget som et **eksplicit valg pr. møde** med en tydelig visning af hvad der sendes — ikke som en global indstilling man slår til og glemmer.

Skal kravet "ingen af mine data forlader min pc" gælde **uden undtagelse**, så er Fase 3 i konflikt med det, og den bør droppes eller erstattes af en lokal model. Det er din beslutning, ikke en teknisk detalje — sig til, hvis den skal væk.

Alt andet — optagelse, transskription, diarisering, ordbog, rettelser, eksport — kører udelukkende på din maskine. Whisper er en lokal binær, modellerne ligger på din disk, og der er intet kald ud af huset.
