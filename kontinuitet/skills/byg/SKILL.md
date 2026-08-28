---
name: byg
description: Bygger HeyPias installationsfil (HeyPia-setup.exe) ud fra den nuværende kode, så den "rigtige" installation kan opdateres. Brug når brugeren siger "byg", "byg installer", "lav en ny installationsfil", "opdater min installation" eller vil sende appen til en kollega.
---

# Byg HeyPias installationsfil

Bygger `C:\NoteApp\installer\NoteApp-setup.exe` ud fra den kode, der ligger nu.

## Forskellen på udvikling og installation

De to lever side om side og skal blive ved med det:

| | Sti | Bruges til |
|---|---|---|
| Udvikling | `C:\NoteApp\app\NoteApp.exe` | Køres direkte, når der bygges og prøves af |
| Installation | `C:\Program Files\NoteApp\NoteApp.exe` | Den rigtige installation, opdateret med setup.exe |

Begge læser og skriver den **samme datamappe** (`C:\AppNoter`). Det er med vilje: ordbogen, optagelserne og indstillingerne skal være de samme, uanset hvilken kopi der startes.

## Efter HVER ændring i appen: kør udgiv.ps1

Skrivebordsgenvejen peger på `C:\NoteApp\app\NoteApp.exe`. Bygger du kun til `bin\Release`, bliver genvejen ved med at åbne en **gammel udgave** — uden at noget siger det. Det er sket: en udgave med transskriptions-loopet i sig blev ved med at ligge der en halv dag.

```powershell
powershell -File C:\NoteApp\scripts\udgiv.ps1
```

Scriptet gør tre ting, der ikke kan gøres halvt:

1. **Sætter versionen** ud fra den seneste commit (`vX.YY: ...`), så nummeret i appens sidebjælke ikke kan komme ud af trit med historikken. Det var det i en periode: appen skrev v0.21, mens commits var nået til v0.39.
2. **Udgiver til `C:\NoteApp\app`** — dér hvor genvejen peger.
3. **Efterprøver resultatet:** at filen kom, at dens version svarer til den, der blev sat, og at skrivebordsgenvejen stadig peger på den. Et byg, der siger «færdig» uden at filen er skiftet, er værre end et, der fejler.

Genvejen peger på en **fast sti** og skal derfor aldrig ændres. Det er publiceringen, der skal huskes — og den er nu ét skridt.

## Sådan bygger du installationsfilen

### 1. Versionen

`udgiv.ps1` har allerede sat den. Bygger du installeren uden at have kørt den, så hæv `<Version>` i `src\NoteApp.Desktop\NoteApp.Desktop.csproj` i hånden.

**Versionen SKAL være hævet siden sidste installationsfil.** Gør du ikke det, opdager installationsprogrammet, at samme version allerede ligger der, og tilbyder at reparere frem for at opdatere — og så tror man, at ændringen ikke virkede.

Følg repoets `vX.YY`, samme tal som commit-beskeden.

### 2. Byg

```powershell
powershell -File C:\NoteApp\scripts\byg-installer.ps1
```

Scriptet udgiver appen først og pakker derefter præcis de filer ned. Det tager et par minutter, mest fordi der komprimeres 300 MB.

Resultatet er to filer i `C:\NoteApp\installer\`:

- **HeyPia-setup.exe** — den, der skal sendes eller køres. Beder selv om administratorrettigheder.
- **HeyPia.msi** — nyttelasten. Kan bruges direkte til automatisk udrulning.

### 3. Rapportér

Sig hvad der blev bygget, hvilken version, og hvor stor filen er. Nævn kun problemer, hvis der var nogen.

## Hvad installationsprogrammet gør ved en eksisterende installation

- **Ældre version installeret:** opdaterer den. Den gamle fjernes automatisk, og der bliver kun én post under Tilføj/fjern programmer.
- **Samme version installeret:** siger, at den allerede er installeret, og tilbyder at reparere eller afinstallere. Det er derfor, versionen skal hæves.
- **Ingen installation:** installerer.

**Afinstallation rører aldrig data.** Pakken kender kun til Program Files. Optagelser, ordbog, noter og indstillinger bliver liggende i datamappen.

## Faldgruber

- **Appen må ikke køre, mens der bygges.** En exe i brug kan ikke overskrives. Scriptet lukker `HeyPia.exe` selv, men ikke en kopi, der kører fra Program Files — luk den i hånden.
- **Byg aldrig pakken uden at udgive først.** Scriptet gør det som standard; `-SpringUdgivelseOver` findes kun til fejlsøgning. Bruger man den ved en fejl, pakkes gårsdagens program ned i en ny installationsfil, og det opdages først på en anden maskine.
- **WiX er bundet til version 5.** Version 6 og 7 kræver, at man accepterer Open Source Maintenance Fee-aftalen. Opgradér ikke uden at spørge Jørgen — det er en licensaftale, ikke en teknisk detalje.
- **`installer\*.exe` og `*.msi` er gitignoreret.** Kilderne (`HeyPia.wxs`, `Bundle.wxs`, `tema.thm`, `da-DK.wxl`, `vilkaar.rtf`) hører i git; de byggede filer gør ikke.
- **`tema.thm` og `da-DK.wxl` er kopier, vi selv ejer.** Temaet er WiX 5's `RtfLargeTheme` med ét ændret mål (760×560 i stedet for 500×390); tekstfilen **erstatter** temaets egen, så enhver streng, der mangler, vises som sit rå navn på en knap. Opdateres WiX, skal begge sammenholdes med originalerne igen. De ligger i `WixToolset.BootstrapperApplications.wixext.dll` som en indlejret ressource — den er en ZIP og kan pakkes ud med `ZipFile.OpenRead`, hvor `RtfLargeTheme.xml` og `RtfTheme.wxl` findes.
- **Efter en ændring i installationsprogrammets udseende: kør `HeyPia-setup.exe` og se på den.** Fejlen med en manglende tekst viser sig kun som en knap med rå kode på — den fanges ikke af et build, der lykkes.
