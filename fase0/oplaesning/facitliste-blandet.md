# Facitliste — blandet prøvetekst

Hører til `testtekst-blandet.md`. Denne facitliste er anderledes end de to andre: den måler ikke først og fremmest, hvor godt referatet blev, men **hvad et sprogskifte koster**.

## De tre spørgsmål, målingen skal svare på

**1. Hvilket sprog blev detekteret?**

Teorien siger `da`, fordi de første tredive sekunder er dansk. Noter sandsynligheden. Bliver det `en`, er teorien om de første tredive sekunder forkert, og så skal `SPEC_NoteApp_v1.md` rettes.

**2. Hvor ulæselig blev den engelske del?**

Læs den midterste del af transskriptionen igennem — den svarer til blok 2, og blokskiftet er skrevet som bogmærke i `notes.jsonl`, så den er nem at finde.

Tre mulige udfald, og de fører hver sit sted hen:

| Udfald | Hvad det betyder |
|---|---|
| Volapyk — danske ord, der lyder som engelsk lyd | Teorien holder. Sprogskift skal håndteres, ikke ignoreres. |
| Genkendeligt engelsk trods `da` | Whisper er mere robust end antaget. Begrænsningen i spec'en skal blødes op. |
| Blandet — tal rigtigt, sætninger forkert | Det værste udfald: referatet ser rigtigt ud og er det ikke. |

**3. Overlevede beslutningen sprogskiftet?**

Beslutningen om **mellemløsningen** — markering af hastesager i natfilen frem for at bygge connectoren — bliver truffet midt i den engelske del. Mangler den i referatet, kostede sprogskiftet en beslutning, og det er den konkrete pris, målingen sætter tal på.

## Tal, der skal stå rigtigt

412, 388, 24, 50, 301, 450, 120, 16 timer, 15 minutter, 8 timer, 64, 24 timer, 3 forlængelser. Datoerne den tolvte og den fjortende.

Tallene i den engelske del (120, 16 timer, 15 minutter, 8, 64) er de interessante. Overlever de sprogskiftet, mens sætningerne omkring dem ikke gør, er det udfald tre i tabellen ovenfor.

## Indhold, der skal med i referatet

**Beslutninger:**

1. Afdelingsfeltet udfyldes inden anden bølge — truffet på **dansk**.
2. Mellemløsningen med markering i natfilen — truffet på **engelsk**.
3. Nødadgang med loft på 24 timer og maksimalt 3 forlængelser — truffet på **dansk**.

**Opgave med ejer:** Marcus — skrive begrundelsen for mellemløsningen ned.

**Åbent spørgsmål:** hvad der sker med nødadgang, når servicedesken er lukket.

## Hvis udfaldet er dårligt

Det er ikke en fejl i appen, det er en egenskab ved Whisper. Vejen videre er at dele lyden op og detektere stykkevis, og det er en beslutning, der skal tages bevidst — ikke noget, der skal bygges, fordi én måling så skidt ud. Skriv målingen ned først.
