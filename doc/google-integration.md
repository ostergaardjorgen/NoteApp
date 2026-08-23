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
3. **Konfigurér samtykkeskærmen** (OAuth consent screen):
   - Brugertype: **External**, hvis appen skal kunne bruges af andre end din
     egen organisation.
   - Områder: kun `.../auth/calendar.readonly`. Ikke flere — hvert ekstra
     område er noget, brugeren skal godkende, og noget, Google skal
     efterprøve.
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

## To ting, der skal huskes, inden appen deles ud

**Google-verifikation.** Så længe samtykkeskærmen står som «Testing», er der
et loft på 100 brugere, og de skal tilføjes i hånden. Skal appen ud til
kunder, skal den igennem Googles verifikation. Det tager tid — det er ikke
noget, man gør dagen før.

**Én spærring rammer alle.** Bliver id'et suspenderet, holder integrationen op
med at virke for alle på én gang. Det er prisen for, at kunden ikke skal
oprette noget selv, og den er værd at betale — men den skal være kendt.

## Microsoft 365

Bygges, når Google-vejen står og virker. Formen bliver den samme: en
registreret app hos Microsoft, en fil ved siden af programmet, og den samme
knap. `Integrationer.Alle` er skrevet, så den næste er en post på en liste og
ikke et særtilfælde.
