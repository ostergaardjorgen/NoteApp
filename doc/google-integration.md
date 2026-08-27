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

### Testing er den, der gør ondt nu

Det er den, appen står i. Googles ordlyd er, at *«authorizations by a test
user will expire seven days from the time of consent»* — og at det gælder
opdateringsnøglen med. Derfor skal der trykkes **Forbind** igen hver uge.

Det er ikke en fejl i appen. `Googlekalender.FriskNoegle` kender den, skelner
`invalid_grant` fra en netværksfejl og siger det rent til brugeren. Men en
integration, der falder ud hver uge, er ikke en integration, man kan sælge.

### Mellemtrinnet: udgiv uden at være verificeret

**Publish app** under **Audience** flytter projektet til *In production*. Det
kan gøres uden verifikation, og det fjerner de to værste ting: syv-dages-udløbet
og den manuelle liste over testbrugere. Prisen er en advarselsskærm, hver ny
bruger skal klikke sig forbi.

**Loftet er det, man skal passe på.** Google skriver «100 new users in total,
after the app presents the unverified app screen» — det tælles over projektets
levetid og kan ikke nulstilles. Verifikationen fjerner skærmen og dermed
loftet, men de brugere, der allerede er brugt, kommer ikke tilbage. Brug dem
derfor ikke på prøvekørsler.

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
