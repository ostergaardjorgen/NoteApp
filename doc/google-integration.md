# Google Kalender — det, der skal gøres én gang

*Skrevet 23-08-2026. Til den, der udgiver appen — ikke til brugeren.*

## Hvorfor det her dokument findes

Brugeren skal trykke **Forbind**, logge ind hos Google og godkende. Det er
hele forløbet, og der er ikke mere at gøre.

For at det kan lade sig gøre, skal appen have sit eget klient-id hos Google.
Det er et krav fra Google: der findes ingen OAuth uden en registreret app.
Registreringen sker **én gang** af den, der udgiver programmet, og aldrig af
kunden.

Første udgave bad kunden om at oprette et projekt i Google Cloud Console. Det
virkede teknisk og var forkert som produkt: den, der skal optage et møde om
fem minutter, opretter ikke et cloud-projekt først.

## De fire trin

1. **Opret et projekt** på `console.cloud.google.com`.
2. **Slå Google Calendar API til** under APIs & Services → Library.
3. **Konfigurer samtykkeskærmen.** Google har delt den op i flere sider under
   **Google Auth Platform**, og de tre, der skal bruges, hedder:

   | Side | Hvad der skal stå |
   |---|---|
   | **Branding** | Appens navn, support-mail og udviklerkontakt |
   | **Audience** | Brugertype **External** — og **Test users** |
   | **Data Access** | Områderne |

   - **Brugertype: External.** Internal virker kun for konti i din egen
     organisation, og appen skal ud til studerende og selvstændige.
   - **Test users → Add users:** den adresse, hvis kalender der skal læses.
     Står den ikke der, afviser Google login'et med `access_denied`, og
     fejlteksten nævner ikke testbrugere med et ord. **Det er det trin, der
     bliver overset** — ikke mindst fordi det lå på selve samtykkeskærmen i
     den gamle udgave af konsollen og nu ligger under Audience.
   - **Områder: kun `.../auth/calendar.events`.** Den rummer læsning i
     forvejen, så `calendar.readonly` skal IKKE med — hvert ekstra område er
     noget, brugeren skal godkende, og noget, Google skal efterprøve.
4. **Opret et klient-id** under Credentials → Create credentials → OAuth
   client ID → **Desktop app**.

## Hvor filen skal ligge

```
C:\NoteApp\hemmeligheder\google-klient.json
```

```json
{
  "KlientId": "1234567890-abcdefghijklmnop.apps.googleusercontent.com",
  "Hemmelighed": "GOCSPX-…"
}
```

`udgiv.ps1` kopierer den med til `C:\NoteApp\app\`, hvor appen leder efter
den. Mappen `hemmeligheder/` er holdt uden for versionsstyringen.

**Ikke fordi den er hemmelig.** Google kalder en desktop-app for en *offentlig
klient*, og deres egen dokumentation siger, at hemmeligheden i den klienttype
ikke behandles som fortrolig — den kan læses ud af ethvert installeret
program. Grunden er en anden: en legitimation, der først er committet, ligger
i historikken for evigt, også efter den er fjernet.

Sikkerheden ligger i tre andre ting, og de står i koden:

- Godkendelsen sker i **brugerens egen browser**. Appen ser aldrig en adgangskode.
- Svaret sendes kun til maskinens **loopback-adresse** på en tilfældig port,
  der lukkes igen med det samme.
- Der bruges **PKCE** (S256), så en opsnappet kode ikke kan byttes til en
  nøgle af noget andet program på maskinen.

## Mangler filen

Så udgives appen uden Google-integrationen. Fanen viser «IKKE SLÅET TIL» og
siger, at det er appen, der ikke er sat op — ikke at brugeren mangler at gøre
noget. `udgiv.ps1` skriver det også i sin egen udskrift.

Det er en gyldig udgave. Kalenderen virker uden integration.

## De tre tilstande hos Google

*Efterprøvet mod Googles egen dokumentation 27-08-2026. Kilderne står nederst.*

Appen kan stå i tre tilstande, og forskellen mellem dem er ikke en detalje —
den afgør, om integrationen kan sælges.

| | Testing | In production, uverificeret | Verificeret |
|---|---|---|---|
| Hvem kan forbinde | kun adresser, du selv skriver ind | enhver med en Google-konto | enhver |
| Loft | 100 testbrugere | 100 nye brugere i alt | intet |
| Advarselsskærm | ja | ja — «Avanceret → Fortsæt» | nej |
| **Adgangen udløber efter syv dage** | **ja** | nej | nej |

### Hvor vi står: In production, uverificeret

**Efterprøvet i konsollen 27-08-2026.** Publishing status er *In production*,
brugertypen er *External*, og tælleren står på **1 af 100**. Der er trykket
Publish, og det trin er altså gjort.

Det betyder, at **syv-dages-udløbet ikke længere gælder**. Kommer beskeden
«Forbindelsen til Google er udløbet» alligevel, er det ikke den regel — så er
nøglen trukket tilbage af en anden grund, og det skal undersøges som en fejl
og ikke affejes som noget forventet.

Til gengæld står der en gul advarsel i konsollen: *«Your app requires
verification.»* Den forsvinder først, når appen er sendt til gennemgang og
godkendt.

### Testing — den, vi kom fra

Googles ordlyd er, at *«authorizations by a test user will expire seven days
from the time of consent»*, og det gælder opdateringsnøglen med. Derfor skulle
der trykkes **Forbind** igen hver uge, så længe projektet stod som *Testing*.

`Googlekalender.FriskNoegle` kender fejlen, skelner `invalid_grant` fra en
netværksfejl og siger det rent til brugeren. Den besked er stadig den rigtige —
kun forklaringen bag den er skiftet.

### Loftet: 100 KONTI, ikke 100 forbindelser

Det tæller **forskellige Google-konti**, der har givet samtykke. Konsollens
egen tekst: *«the user cap limits the number of users that can grant
permission to your app»*. Forbinder den samme konto igen — efter en genlogin,
en ny maskine, en geninstallation — bruges der ikke en plads til.

Det tælles over **projektets levetid og kan ikke nulstilles**. Verifikationen
løfter loftet, men de forbrugte pladser kommer ikke tilbage. Brug dem derfor
ikke på prøvekonti.

### Advarselsskærmen er den egentlige pris

Loftet er ikke det, der gør ondt ved 100 kunder — det er, at hver eneste af
dem møder «Google har ikke verificeret denne app» og skal klikke sig forbi et
punkt, der hedder noget i retning af *«Gå til appen (usikkert)»*. Det er en
salgsspærring længe før det er en teknisk spærring.

### Et nyt område sender alle tilbage til start

Konsollen skriver det selv: ser brugerne den uverificerede skærm, er det,
fordi forespørgslen indeholder områder, der ikke er godkendt. Tilføjes der et
område senere, skal **hver eneste**, der har forbundet, igennem godkendelsen
igen. Det er derfor, `calendar.readonly` blev byttet til `calendar.events`,
mens der var nul brugere.

### Verifikationen: papirarbejde, ikke penge

Appen beder om `calendar.events` og `tasks`. Begge er **følsomme**
(*sensitive*) og ingen af dem **begrænsede** (*restricted*). Forskellen er
afgørende:

| | Følsom — det er os | Begrænset — Gmail, Drive |
|---|---|---|
| Gennemgang af app og varemærke | ja | ja |
| Ejet domæne, hjemmeside og privatlivspolitik | ja | ja |
| Demovideo af hele samtykkeforløbet | ja | ja |
| Tredjeparts sikkerhedsvurdering (CASA) | **nej** | ja, og den koster årligt |

Vi slipper for sikkerhedsvurderingen. Det er den post, der plejer at lukke
ned for små udgivere. Vores verifikation koster **tid og papirarbejde**.

### Én spærring rammer alle

Bliver klient-id'et suspenderet, holder integrationen op med at virke for alle
på én gang. Det er prisen for, at kunden ikke skal oprette noget selv, og den
er værd at betale — men den skal være kendt.

### Resten af appen er upåvirket

Optagelse, transskription, opsummering og dokumenter rører aldrig Google.
Mangler `google-klient.json`, siger fanen «IKKE SLÅET TIL», og alt andet
kører. Verifikationen spærrer for **én fane** — ikke for produktet.

## Navnet og logoet på samtykkeskærmen

Appen skiftede navn fra NoteApp til HeyPia den 28. august 2026, og
samtykkeskærmen er det ENESTE sted, kunden møder navnet uden for appen. Stod
der stadig «NoteApp», ville hun blive bedt om at give adgang til et program,
hun ikke har hørt om.

Rettes under **Branding**: feltet *App name* og knappen *Change logo*.

**Logoet ligger klar i `design/HeyPia-logo-google-120.png`.** Det er lavet
efter Googles krav, og de er ikke til at gætte:

| Krav | Værdi |
|---|---|
| Format | JPG, PNG eller BMP |
| Størrelse | 120 × 120 px |
| Filstørrelse | under 1 MB (vores er 20 KB) |

Filen er skaleret ned fra `src/NoteApp.Desktop/app.png` med LANCZOS.
Nedskalering med nærmeste nabo gør skriften i flisen grynet, og det ses først
på skærmen hos kunden, hvor der ikke er noget at gøre ved det. Skal den laves
om, så brug samme fremgangsmåde.

Hjørnerne er gennemsigtige, og det skal de blive ved med. Flisen er rundet,
og lægges der hvidt bag, bliver hjørnerne firkantede på Googles hvide skærm.

### Rækkefølgen betyder noget

Konsollen skriver *«After you upload a logo, you will need to submit your app
for verification»* — og separat *«Your branding needs to be verified before
it's shown to users»*. Logoet vises altså ikke til nogen, før appen er
godkendt.

Derfor er navn og logo skiftet **før** ansøgningen sendes, ikke efter. Så
bærer ansøgningen den rigtige identitet fra start. Var det gjort omvendt,
ville en godkendt app skulle gennem en ny runde for et navneskift.

### To steder mere, men kun i konsollen

Cloud-projektet hedder stadig «NoteApp calendar sync», og OAuth-klienten under
**Clients** bærer også det gamle navn. Ingen af dem vises til kunden — de står
kun i konsollen. De kan rettes for sammenhængens skyld, men de haster ikke.

## Hvor det gøres

| Hvad | Hvor |
|---|---|
| Udgiv til produktion, testbrugere | [console.cloud.google.com/auth/audience](https://console.cloud.google.com/auth/audience) |
| Navn, support-mail, logo | [console.cloud.google.com/auth/branding](https://console.cloud.google.com/auth/branding) |
| Områder | [console.cloud.google.com/auth/scopes](https://console.cloud.google.com/auth/scopes) |
| Send til verifikation, følg status | [console.cloud.google.com/auth/verification](https://console.cloud.google.com/auth/verification) |
| Klient-id'et selv | [console.cloud.google.com/apis/credentials](https://console.cloud.google.com/apis/credentials) |

Googles egne sider:

- [Tilstandene og hvad de betyder](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview)
- [Testing, produktion og syv-dages-udløbet](https://support.google.com/cloud/answer/15549945)
- [Advarselsskærmen og loftet på 100](https://support.google.com/cloud/answer/7454865)
- [Verifikation af følsomme områder — trin for trin](https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification)
- [Verifikation af varemærke](https://developers.google.com/identity/protocols/oauth2/production-readiness/brand-verification)
- [Når verifikation ikke er nødvendig](https://support.google.com/cloud/answer/13464323)

## Microsoft 365

Bygges, når Google-vejen står og virker. Formen bliver den samme: en
registreret app hos Microsoft, en fil ved siden af programmet, og den samme
knap. `Integrationer.Alle` er skrevet, så den næste er en post på en liste og
ikke et særtilfælde.
