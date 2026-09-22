---
name: test
description: Kører en fuld funktions-, sikkerheds- og compliancetest af HeyPia og skriver en professionel testrapport i Word. Brug den når brugeren siger "kør en test", "test appen", "/test", "testrapport", "er den klar til release", eller før noget sendes ud af huset.
---

# Test af HeyPia

En test, der kun kan køres af den, der skrev den, er ikke en test. Den her
består af et script, der måler, og en rapport, der forklarer — og de to ting
holdes adskilt med vilje: **tallene kommer fra kørslen, ikke fra hukommelsen.**

## Sådan kører du den

```powershell
powershell -File "C:\Users\oster\.claude\skills\test\koer-test.ps1"
```

| Flag | Betyder |
|---|---|
| `-SpringBygOver` | Springer bygningen over. Hurtigere, men så er F1 ikke afprøvet |
| `-Sti` | Kodelageret. Standard `C:\NoteApp` |
| `-Data` | Datamappen. Standard `C:\AppNoter` |
| `-Ud` | Hvor resultatet gemmes. Standard `C:\AppNoter\test` |

Scriptet **læser kun.** Det ændrer intet, starter ingen optagelse og trykker
ikke på nogen taster. Det er ikke forsigtighed: en test, der ændrer den ting,
den måler, kan ikke bruges til noget — og et syntetisk tastetryk starter en
mødeoptagelse hos den, der sidder ved maskinen.

Resultatet lander som JSON i `C:\AppNoter\test\seneste.json` og som en dateret
kopi ved siden af. Exitkode 1 betyder mindst én FEJL.

## Hvad den dækker

**Funktion (F1–F14)** — bygning, enhedsprøver, at udgivelsen svarer til seneste
commit, at appen kører, at genvejstasten faktisk er registreret hos Windows, at
vågeordets motor og model er på plads, at datamappen ligger uden for
kodelageret, og at dansk og engelsk har de samme nøgler.

**Sikkerhed (S1–S10)** — forbudte termer og kundenavne, hemmeligheder i
filnavne, API-nøglemønstre i sporet kode, datafiler i versionsstyring, sårbare
pakker, at nøglen ligger uden for kodelageret, ukrypterede endepunkter,
`.gitignore`, Googles klientfil i udgivelsen, og at autostart peger på en fil,
der findes.

### S1b — det, der faktisk forlader maskinen

**Den vigtigste af dem alle, og den kom til bagefter.**

Leverancetjek kigger i kodelageret. Men appen sender også filer fra
**datamappen**: ordbogen følger med hver eneste diktering som forhåndsviden,
og teksttyper og skabeloner går med som instruktion. De ligger uden for git og
bliver aldrig set af noget andet tjek.

30-08-2026 stod der et firmanavn i ordbogen, som ikke måtte forlade maskinen.
Det havde stået der i dagevis og var gået med ved hver diktering. **Testen
sagde BESTÅET**, fordi den kiggede det forkerte sted.

S1b holder de forbudte termer op mod de filer, appen **sender** — ikke mod dem,
den gemmer. Den skal aldrig fjernes, og en ny fil, der kommer til at følge med
ud af huset, skal tilføjes til listen `$sendes` samme dag, den bliver til.

**Compliance (C1–C8)** — at compliance-skærmen forklarer lytningen, at
påstandene «der gemmes intet» og «der sendes intet» **holder mod koden**, at
appen beder brugeren sige det til deltagerne, sletteopsætningen, at
skybehandling tvinges til EU, at privatlivspolitikken er udfyldt, og at
compliance findes på begge sprog.

### Det, et script ikke kan afgøre

To ting rapporteres altid som **IKKE AFPRØVET**, og de skal blive ved med at
gøre det, indtil et menneske har set dem:

- **Optagelse af et rigtigt møde** — kræver to lydspor og en person i den anden
  ende. Et script kan se, at motoren findes; ikke at optagelsen blev god.
- **Diktering fra tast til tekst** — holdet kan aflæses i sporet, men om teksten
  lander rigtigt ved markøren kræver, at nogen taler.

En test, der skriver BESTÅET om noget, den ikke har målt, er værre end ingen
test. Skriv aldrig disse to om.

## Statusserne

| Status | Betyder |
|---|---|
| `BESTAAET` | Målt og i orden |
| `ADVARSEL` | Virker, men noget bør laves om |
| `FEJL` | Skal rettes, før der leveres |
| `DELVIS` | Halvdelen er målt, resten kræver et menneske |
| `IKKE AFPROEVET` | Kan ikke afgøres af et script |
| `SPRUNGET OVER` | Fravalgt ved kørslen eller ikke relevant |

Samlet resultat: **IKKE BESTÅET** ved mindst én FEJL, **BESTÅET MED
BEMÆRKNINGER** ved mindst én ADVARSEL, ellers **BESTÅET**.

## Rapporten i Word

Efter kørslen bygges rapporten med:

```powershell
powershell -File "C:\Users\oster\.claude\skills\test\byg-rapport.ps1"
```

Den læser `seneste.json` og skriver
`C:\NoteApp\Jura\Testrapport HeyPia <dato>.docx` gennem Word selv. Rapporten
indeholder forside med samlet resultat, en oversigtstabel, alle prøver med
resultat og detalje, og et afsnit om det, der ikke er afprøvet.

**Efterprøv altid dokumentet bagefter.** Åbn det med Word-COM og læs teksten
igennem — et afsnit, der forsvandt undervejs, kan ikke ses på et script, der
sagde «færdig». Se `byg-rapport.ps1`, som gør det til sidst.

## Når noget fejler

Rapportér med prøvens id, hvad der blev målt, og hvad der skal gøres — ikke
bare at noget fejlede. Og lav ikke om på koden midt i en testkørsel: så måler
rapporten noget andet end det, der blev afleveret. Kør testen, skriv rapporten,
ret bagefter, kør igen.
