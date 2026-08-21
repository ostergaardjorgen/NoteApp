# Måling: kan whisper høre danske kommandoer godt nok?

*Oprettet 21-08-2026. Facit står i denne fil; resultaterne kommer nedenunder.*

Målingen afgør, om stemmestyring kan bygges — og hvilken model der skal bruges.
Den er sat op, fordi de tre ting, der skal vides, ikke kan slås op: hvor godt
whisper hører **din** stemme på **din** mikrofon på **dansk**.

## Hvad der måles

| Mål | Grænse for «det virker» |
|---|---|
| Kommandoer forstået rigtigt | ≥ 95 % (24 af 25) |
| Almindelige sætninger, der udløser en handling | **0 %** — den vigtigste |
| VRAM under kørsel | under 5.600 MiB |

Den anden række er den kritiske. En stemmestyring, der af og til gør noget
uopfordret, bliver slukket efter to dage, uanset hvor godt resten virker.
Derfor er halvdelen af sætningerne herunder **ikke** kommandoer — og flere af
dem ligner en kommando med vilje.

## Sådan optager du

*(Trinene står med punkttegn og ikke tal — måleprogrammet læser facit ud af
denne fil, og alt med et tal foran ville blive læst som en sætning.)*

- Start en optagelse i NoteApp med **Ctrl+Shift+,**
- Læs de 50 sætninger op i rækkefølge
- **Hold en pause på cirka to sekunder mellem hver sætning** — det er dét,
  opdelingen bruger til at finde grænserne
- Læs i almindeligt tempo, som du ville tale til appen. Overartikulér ikke;
  det er den rigtige stemme, der skal måles
- Stop og gem. Kald mødet **Dikteringstest**

Sig ikke numrene højt.

---

# DE 25 KOMMANDOER

Disse skal forstås rigtigt.

1. Start optagelsen
2. Stop optagelsen og gem
3. Kassér optagelsen
4. Hold pause
5. Fortsæt optagelsen
6. Opret en opgave
7. Opret en opgave til Espen
8. Opret en opgave med frist på fredag
9. Opret en aftale i morgen klokken ti
10. Opret en aftale på tirsdag klokken halv tre
11. Lav en kort opsummering
12. Lav et dokument
13. Skriv optagelsen ud
14. Søg efter pipeline
15. Søg efter Omada i udskriften
16. Åbn den seneste optagelse
17. Vis opgaverne
18. Vis tallene
19. Navngiv taleren Alexander
20. Gå til dokumenter
21. Gå til indstillinger
22. Luk vinduet
23. Skriv en note
24. Sæt et bogmærke
25. Ryd søgningen

---

# DE 25 ALMINDELIGE SÆTNINGER

Disse må **ikke** udløse noget. Flere af dem ligner en kommando — det er med
vilje, og de er de vigtigste i hele målingen.

26. Jeg tænker, vi skal starte med at kigge på pipeline i Danmark
27. Vi bør nok stoppe her og tage resten på næste møde
28. Kan du sende mig det materiale, du nævnte?
29. Det er en god idé at oprette en fælles mappe til det
30. Jeg vender tilbage med et forslag i næste uge
31. Han sagde, at de allerede har flere kunder i Norge
32. Vi skal huske at få talt med Espen om det
33. Der er en aftale i morgen, som jeg ikke kan flytte
34. Hvad synes du selv om den løsning?
35. Det lyder som noget, vi kan bruge
36. Jeg har arbejdet med identitetsstyring i mange år
37. Vi holder pause om et kvarter, tror jeg
38. Optagelsen fra sidste møde var svær at høre
39. Kunne du gentage det sidste, du sagde?
40. Der står noget om det i dokumentet, jeg sendte
41. Vi søger stadig efter den rigtige profil
42. Min kalender er ret fuld i næste uge
43. Det giver mening at samle det i én oversigt
44. Jeg er ikke sikker på, at tallene passer
45. Lad os tage en snak om det bagefter
46. Hun nævnte noget om en frist på fredag
47. Det var faktisk en rigtig god opsummering
48. Vi plejer at gemme referaterne på drevet
49. Jeg skal lige finde min note fra sidst
50. Tak for i dag, det var en god samtale

---

## Hvorfor netop de sætninger

**Kommandoerne** dækker det, appen faktisk kan: optagelsen (1–5), opgaver og
aftaler (6–10), det der laves bagefter (11–13), at finde rundt (14–18, 20–21,
25), og de to ting man gør undervejs (23–24). Nummer 19 er med, fordi
navngivning af talere er den funktion, der oftest skal bruges lige efter et
møde.

**Fælderne** blandt de almindelige sætninger:

| # | Ligner | Men er |
|---|---|---|
| 27 | «Stop optagelsen» | et forslag om at slutte mødet |
| 29 | «Opret …» | en idé om en mappe |
| 32 | «… til Espen» | almindelig mødetale |
| 33 | «en aftale i morgen» | en oplysning, ikke en ordre |
| 37 | «Hold pause» | en forudsigelse |
| 38 | «optagelsen» | en bemærkning om lydkvalitet |
| 41 | «Søg …» | en beskrivelse af en rekrutteringssituation |
| 46 | «frist på fredag» | et referat af, hvad en anden sagde |
| 47 | «opsummering» | en ros |
| 49 | «min note» | en handling, brugeren selv udfører |

Rammer nogen af de ti, er det en falsk accept, og den vejer tungere end en
kommando, der bliver misforstået. En misforstået kommando gør ingenting; en
falsk accept gør noget, ingen har bedt om.

---

## Resultater

*Målt 21-08-2026 på en optagelse på 3:06 med Jørgens stemme og Jabra SPEAK 510.*

### 1. Whisper kan dansk — når den har sammenhæng

Hele optagelsen gennem large-v3 på én gang gav stort set perfekt tekst:

```
Start optagelsen · Stop optagelsen og gemt · Kasser optagelsen · Hold pause
Fortsæt optagelsen · Opret en opgave · Opret en opgave til Asten
Opret en opgave med frist på fredag · opret en aftale i morgen kl. 10
opret en aftale på tirsdag kl. halv 3 · lav en kort opsummering …
```

Kun to fejl i de første ti: «gemt» for «gem», og «Asten» for «Espen».

### 2. Men en enkelt kommando alene falder fra hinanden

De samme sætninger, klippet ud hver for sig og kørt enkeltvis:

| Facit | Hørt |
|---|---|
| Opret en opgave | «Opretten opgav» |
| Stop optagelsen og gem | «Stop. Stop. Stop. Stop. Stop.» |
| Fortsæt optagelsen | «Fortsat optagelsen» |

**Det er ikke lydlængden.** Klippene blev polstret med tre sekunders stilhed i
begge ender, så de lignede det 30-sekunders vindue, whisper er trænet på — og
resultatet blev det samme. Det er den **sproglige sammenhæng**, der mangler:
i hele filen får hver sætning de foregående med som kontekst, og de er alle
sammen kommandoer, så de bekræfter hinanden.

**Det er den realistiske situation.** En kommando sagt til en tom app har
ingen forudgående sætninger. Målingen på klip er altså den rigtige, og den
siger: rå transskription af en enkelt dansk kommando er ikke god nok.

### 3. Grammatikken virker — men kun med `--grammar-rule`

`--grammar` alene bliver **ignoreret lydløst**. Udgangen var tegn for tegn
identisk med og uden. Først med `--grammar-rule root` sker der noget.

Målt på seks kommandoer med en GBNF over alle 25:

| Facit | Uden | Med grammatik |
|---|---|---|
| Kassér optagelsen | Kasser optagelsen | **Kassér optagelsen** ✓ |
| Hold pause | Hold pause | **Hold pause** ✓ |
| Opret en opgave | Opretten opgav | **Opret en opgave** ✓ |
| Fortsæt optagelsen | Fortsat optagelsen | **Hold pause** ✗ |
| Start optagelsen | Start optagelsen | *(intet)* |
| Stop optagelsen og gem | Stop. Stop. Stop… | *(intet)* |

### 4. Den farlige fejl er ny — og den kan fanges

Grammatikken tvinger udgangen ind i kommandosættet. Når den rammer forkert,
bliver resultatet derfor ikke volapyk, men **en anden gyldig kommando** — som
ville blive udført med fuld sikkerhed. «Fortsæt optagelsen» blev til «Hold
pause».

Det er værre end ingen stemmestyring, og det er ikke noget, en bedre model
løser: det er en følge af at begrænse udfaldsrummet.

**Men de to udskrifter er uenige, netop når det går galt.** Den frie sagde
«Fortsat optagelsen», den bundne sagde «Hold pause». Reglen skriver sig selv:

> Kør begge dele. Handl kun, når de peger på den samme kommando.

På de seks ovenfor ville reglen have udført to rigtigt, afvist den farlige, og
afvist tre, hvor der ikke var enighed — altså nul forkerte handlinger.

### Det, der mangler at blive målt

Reglen om enighed er kun prøvet på seks. Den skal køres på alle 25 kommandoer
**og** alle 25 almindelige sætninger, før den kan bruges — det er de
almindelige, der afgør, om den holder, for det er dem, der ikke må udløse noget.

Opdelingen skal rettes først: 31 vinduer gav 50 klip, men det var et
sammenfald. Sætning 8 blev delt i to, og to andre smeltede sammen, så
alignementet skrider fra klip 9.
