# Arbejdsmåden i NoteApp

*Skrevet 23-08-2026. Den vigtigste fil i mappen: koden kan læses, men de her
regler kan ikke udledes af den.*

Reglerne står, fordi de hver især er lært af en fejl. Hvor det er tilfældet,
står fejlen med.

---

## 1. Nul tolerance for forkert information

**Dokumentationen og appen skal sige det samme som virkeligheden. Uden
undtagelse.** En forkert oplysning er værre end ingen: den bliver troet.

- Ændrer du adfærd, retter du dokumentationen **i samme arbejdsgang**. Ikke
  bagefter, ikke i commit-beskeden alene.
- Omgør du en beslutning, **rettes den gamle tekst** — den suppleres ikke. Lad
  ikke to modstridende afsnit stå og lade læseren gætte.
- Sig **«det ved jeg ikke»** frem for at gætte. Appen skriver «ukendt» om
  whisper.cpp's version, fordi motoren ikke stempler den.
- **Tal i UI og dokumentation skal være målt.** To modelstørrelser var gættet,
  og resultatet var, at appen ville have hentet en fil på 2,9 GB igen hver gang.
- **Påstå ikke noget om, hvad koden gør, uden at have kørt den.** «Bygger» er
  ikke «virker».

Finder du en uoverensstemmelse — også en, ingen har spurgt om — så ret den, og
sig det.

## 2. Mål frem for at gætte, og mål på et udsnit først

Enhver ny indstilling prøves af på **et kort klip**, aldrig på hele lydfilen
først. Ellers venter man tyve minutter på at konstatere noget, der kunne måles
på tredive sekunder.

Alt, der er målt, skrives i **[`../doc/findings.md`](../doc/findings.md)** med
tal og dato. Reglerne for den fil:

- Kun det, der er målt. Et skøn skrives som et skøn, med det ord.
- Hvert fund har en dato og en konsekvens. Et fund uden konsekvens er en anekdote.
- **Fravalg bliver stående.** Slettes de, bliver de gentaget om et halvt år af
  nøjagtig de samme gode grunde.
- Rettes et fund af en senere måling, bliver det gamle stående med en note.

**Tilpas aldrig koden til prøven.** To søgeprøver rammer ikke førstepladsen, og
de får lov: at tvinge dem på plads ville være at gøre målingen ubrugelig.

De to målinger, der kan køres igen:

```bash
noteapp maalsoegning
noteapp maaldato
```

## 3. Tekst i appen skrives til en kunde

Alt, brugeren kan læse, er kommunikation fra et produkt til den, der har valgt
at bruge det. Ikke en samtale mellem appen og den, der har bygget den.

| Skriv ikke | Skriv i stedet |
|---|---|
| «Det er vores egen kode» | «Koden kan efterprøves» |
| «den aftale, vi bygger på» | «den aftale, appen bruger» |
| «vi har målt», «vi ved ikke» | «det er målt», «det er ikke efterprøvet» |

**«Vi», «os» og «vores» hører ikke hjemme i brugerfladen.** Målinger og fejl
undervejs er værdifulde som BEGRUNDELSE — men de hører i kodekommentarerne,
ikke på skærmen.

**Æ, ø og å staves rigtigt i alt, brugeren ser.** ASCII kun i inline
kodekommentarer og commit-beskeder.

## 4. Optagelse må aldrig kunne blokeres

Ingen dialog, intet spørgsmål og ingen fejl må stå i vejen for, at en optagelse
kan begynde eller fortsætte.

Derfor: genvejstasten starter optagelsen **først** og spørger bagefter. Derfor
må mødevagten kun spørge, aldrig optage selv. Derfor står der «Behold
optagelsen» som standardvalg, når man trykker kassér.

## 5. Appen opfordrer ALTID til at fortælle, at der optages

Aldrig det modsatte, og aldrig en formulering, der kan læses som «det er
uproblematisk, så lad være med at nævne det».

Det er ikke kun jura. Det er produktets stærkeste argument: fordi lyden bliver
på maskinen, kan den, der optager, se de andre i øjnene og sige, hvor
optagelsen ender.

## 6. Lokalt før skyen

Kan noget gøres begge steder, står **den lokale mulighed først** og er
standardvalget. Skal en sky-model bruges, skal det være en **EU-løsning** —
derfor Mistral.

**Lyden forlader aldrig maskinen.** Udskrift, søgning, talergenkendelse,
opgavefund og den korte opsummering kører alle lokalt. Skyen er altid et valg,
brugeren træffer i situationen — aldrig en automatik.

Vær præcis om hvad der bliver hvor. «Alt bliver hos dig» er forkert, når et
dokument sendes til Mistral.

## 7. Der følger aldrig en nøgle med appen

API-nøgler og klient-id'er indtastes eller opsættes af brugeren selv. En
indbygget nøgle er den samme for alle, kan læses ud af filen og misbruges i
andres navn — og den dag den spærres, holder appen op med at virke for alle på
én gang.

**Undtagelsen er Googles klient-id**, og den er bevidst: en kalenderintegration,
hvor kunden selv skal oprette et cloud-projekt, bliver ikke brugt. Se
[`../doc/google-integration.md`](../doc/google-integration.md) for hvorfor det
er forsvarligt, og hvad der til gengæld skal være styr på.

## 8. Efter HVER ændring: udgiv, og meld nummeret

```powershell
powershell -File C:\NoteApp\scripts\udgiv.ps1
```

Skrivebordsgenvejen peger på `C:\NoteApp\app\NoteApp.exe`. Bygger du kun til
`bin\Release`, bliver genvejen ved med at åbne en **gammel udgave** — uden at
noget siger det. Det er sket: en udgave med et transskriptions-loop i sig blev
ved med at ligge der en halv dag.

Versionsnummeret sættes af scriptet ud fra den seneste commit (`vX.Y.Z: …`).
**Uden en ny commit med et nyt nummer får udgivelsen det samme nummer som sidst.**

**Meld altid versionsnummer og tidspunkt i chatten efter hver udgivelse.**

Udgiv aldrig, mens der optages — `udgiv.ps1` lukker appen.

## 9. Commit og push HVER DAG

Skete det ikke dagen før, er det den **første handling** på en ny dag — før
der bygges videre på noget.

```powershell
powershell -File C:\NoteApp\scripts\sikker-kode.ps1 -Push
```

Den kører leverancetjek med `-Historik` først og stopper ved ethvert fund,
laver et git-bundt og efterprøver, at det kan læses, og kopierer det, der
aldrig må i git.

**Reglen kom 23-08-2026, hvor der lå 75 commits, der aldrig var pushet.** De
var ikke tabt — de fandtes bare ét sted, på den ene maskine. Et push er noget,
man husker, lige indtil man har travlt.

`udgiv.ps1` siger til, når der er upushede commits, og skriver hvor gammel den
ældste er. Den pusher **ikke** selv: leverancetjekket skal køres først, og et
push midt i en udgivelse ville sende arbejde af sted, ingen har set efter.

Data er en anden sag og kører af sig selv — se den planlagte opgave
«NoteApp - ugentlig backup». Kode og data er to backups, og den ene erstatter
ikke den anden.

## 10. Salgsargumenterne skal følge produktet

[`../doc/salgsargumenter.md`](../doc/salgsargumenter.md) er argumenter, der
kan tages frem over for en kunde. Hvert punkt står med sit belæg — og med
**det, der ikke må siges**.

**Ændrer produktet sig, ændres argumentet i samme arbejdsgang.** Et
salgsargument, der var sandt i august, er det farligste sted at have en gammel
oplysning: det er dét, der bliver sagt højt til en kunde, og der er ingen, der
retter det undervejs.

Dokumentet indeholder også de påstande, vi IKKE kan komme med — «vi er
GDPR-compliant», «alt bliver på din maskine», et CO2-tal. De står der, så de
ikke bliver sagt ved et uheld.

Bliver noget målt, som i dag står som et skøn, skal skønnet erstattes.

## 11. Kør leverancetjek før hver commit og hvert push

```powershell
powershell -File "$env:USERPROFILE\.claude\skills\leverancetjek\tjek-leverance.ps1" -Sti C:\NoteApp
```

Uopfordret. Se [`skills/leverancetjek/SKILL.md`](skills/leverancetjek/SKILL.md).

## 12. Skriv kommentarer, der forklarer HVORFOR

Koden i det her projekt har usædvanligt fyldige kommentarer, og det er med
vilje. De forklarer ikke, hvad linjen gør — de forklarer, **hvilken fejl der
gjorde, at den ser sådan ud**, og hvad alternativet kostede.

Eksempel fra `Udskrift.Af`:

> Målt på et webinar 21-08-2026: 305 segmenter blev til én replik på 22
> minutter med ét tidsstempel, 00:00:00.

Uden den slags bliver den samme fejl lavet igen om et halvt år. **Sletter du en
mekanisme, så lad kommentaren om hvorfor blive stående.**

---

## Faldgruber, der har kostet tid

Værd at kende, før den samme time bruges igen.

### PowerShell 5.1

- Here-strings: `'@` skal stå **alene** på sin linje, i kolonne 0.
- `$d[$i-1, $j]` er en parsefejl — brug mellemregninger.
- `Get-Content`/`Set-Content -Encoding utf8` **mangler æøå**. Brug
  `[IO.File]::ReadAllText/WriteAllText` med `UTF8Encoding`.
- `&&` og `||` findes ikke. Brug `;` og `if ($?)`.
- Enkeltelement-arrays foldes ud: `@(@('a','b'))` bliver til to strenge.
  **Det kostede en tegn-for-tegn-erstatning i fire filer.** Brug `,@(...)`.
- Anførselstegn i en commit-besked knækker `git commit -m`. Brug `git commit -F <fil>`.

### WPF

- XML-kommentarer må ikke indeholde `--`.
- **Binding til en privat type fejler i stilhed** — felterne står bare tomme.
  Typer, XAML binder til, skal være `public`.
- `DisplayMemberPath` slog ikke igennem ét sted; en `ItemTemplate` med et
  `{Binding}` gør. Rullelisten viste «Punkt { Navn = …, Under = … }».
- `AppContext.BaseDirectory` er exe-mappen — også for en enkeltfils-udgivelse.
  Efterprøvet.
- Startskærmen sættes i `MainWindow`-konstruktøren, **ikke** af `Nav_Changed`.
  Ændrer du det markerede menupunkt i XAML, skal linjen i koden med — de to kom
  ud af trit, og menuen sagde Cockpit, mens skærmen viste Optagelser.

### Omdøbning med script

En søg-og-erstat over strenge ramte tre gange **kode inde i strenge**:
`{Udskrift.Navn(...)}` i en interpoleret streng, `{antalIUdskriften}` og
`{udskriftSti}`. Bygget fangede alle tre — men kun fordi der blev bygget.
**Byg altid efter en maskinel omdøbning, og læs diffen igennem.**

### Windows-specifikt

- **Ctrl+Shift+ciffer er reserveret af Windows**, så snart der er mere end ét
  tastaturlayout — også når kombinationen ikke står i inputtabellen.
  `RegisterHotKey` siger ja alligevel, og genvejen gør ingenting.
- **WASAPI loopback tages FØR endepunktets lydstyrke.** Målt: 0,36621 ved 50 %,
  0 % og mute. Men muter man selve **afspilleren**, er der intet at optage.
  De to slags mute er ikke det samme, og forskellen kostede to minutter af et
  rigtigt webinar.
- Sommertid: byg en dato med **den dags egen tidsforskel**, ikke dagens i dag.
  «I år» begyndte 31-12 kl. 23:00.

### Modeller

- whisper: sprogkoden for norsk er `no`, ikke `nb`. `nb` fejler i stilhed.
- llama.cpp: `/no_think` i systemprompten virker; `--reasoning-budget 0` gør
  ikke i dette byg. Modellen skriver stadig et tomt `</think>`, som skal
  **fjernes fra svaret** — det stod på skærmen.
- En model på 4B holder ikke til tyve tusind tokens og et løfte om at være
  præcis. Den går i ring. **Kør i blokke**, og efterprøv hvert svar mod kilden.
