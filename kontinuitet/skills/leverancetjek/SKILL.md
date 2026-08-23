---
name: leverancetjek
description: Kontrollerer at forbudte termer og persondata ikke er på vej ud af maskinen. Kør FØR hver commit, hver push og hver leverance i ethvert udviklingsprojekt. Bruges også når brugeren beder om "leverancetjek", "tjek før commit", "må det her gå ud", eller når kode, dokumenter eller artefakter skal deles, publiceres eller pushes til GitHub.
---

# Leverancetjek

Generel kontrol på tværs af **alle** udviklingsprojekter. Den findes, fordi den
fejl, den fanger, ikke kan fortrydes: når en term først er pushet til et
fjernlager, ligger den i historikken hos alle, der har hentet den.

> **Den her udgave er den, der ligger i git, og den er skrevet om med vilje.**
>
> Den oprindelige skill nævnte de forbudte termer i sin egen tekst — og så
> ville selve vejledningen være det, den advarer imod, i det øjeblik den blev
> committet. Funktionen er den samme; kun eksemplerne er væk.

## Reglen der bærer det hele

Der findes en liste over **firmanavne og stier, der ikke må stå nogen steder**
i kode eller leverancer — heller ikke i stavevarianter eller sammensætninger,
og heller ikke i dokumentation, kommentarer, kommandoer eller filnavne.

Listen ligger i `forbudte-termer.txt` ved siden af scriptet.

**Den fil må ALDRIG committes.** At lægge den i et repo ville udstille præcis
det, den er sat i verden for at holde ude. Den er derfor ikke med her — se
«Sådan sætter du den op igen» nedenfor.

## Sådan kører du den

```powershell
powershell -File "C:\Users\<bruger>\.claude\skills\leverancetjek\tjek-leverance.ps1" -Sti <projektmappe>
```

Tilføj `-Historik` for også at gennemsøge hele git-historikken. Det er
langsommere, men det er dér, en term overlever, efter den er fjernet fra HEAD.

Scriptet afslutter med kode 1 ved fejl, så det kan bruges direkte som en gate i
et pre-commit-hook eller i CI.

## Hvad den kontrollerer

| # | Tjek | Alvor |
|---|---|---|
| 1 | Forbudte termer i sporede filer | fejl |
| 2 | Forbudte termer i det, der er staged lige nu | fejl |
| 3 | Forbudte termer i git-historikken (`-Historik`) | advarsel |
| 4 | Datafiler på vej i versionsstyring (`.db`, `.wav`, `.env`, nøgler, `notes.jsonl`) | fejl |
| 5 | Forbudte termer i fil- og mappenavne | fejl |

Historik-fund er en advarsel og ikke en fejl, fordi de ikke kan rettes med en
almindelig commit. De kræver en omskrivning af historikken og et tvunget push —
en beslutning brugeren skal tage bevidst.

## Hvornår du skal køre den uopfordret

Kør den **uden at blive bedt om det**, før du:

- committer eller pusher til et fjernlager
- opretter et nyt repo eller tilføjer et nyt fjernlager
- afleverer filer til brugeren som en leverance
- publicerer noget, uanset form

Fejler den, så **stop og ret først**. Rapportér fundene med fil og linje, og
fortsæt ikke med at pushe i mellemtiden.

## Når et fund optræder

Vær konkret om hvad der skal ske, ikke bare at noget fejlede:

1. **Fund i sporede filer eller staged:** erstat termen med et neutralt navn.
   Fjern den ikke bare — en tom plads i en testtekst eller en ordliste
   ødelægger den test, den var en del af.
2. **Fund i filnavne:** omdøb med `git mv`, så historikken følger med.
3. **Fund i historikken:** oplys brugeren om, at de kræver en omskrivning af
   historikken og et tvunget push, og lad brugeren beslutte. Gør det aldrig af
   egen drift — det er destruktivt og rammer alle kloner.
4. **Datafiler:** fjern dem fra versionsstyring med `git rm --cached`, og
   tilføj mønsteret til `.gitignore`. Den holdbare løsning er dog at flytte
   data helt uden for arbejdstræet, så en ignore-fejl ikke kan lække noget.

## Sådan sætter du den op igen

`forbudte-termer.txt` er ikke i git og skal skrives i hånden. Formatet er én
term pr. linje med en forklaring efter kolon:

```
<term der ikke må stå nogen steder>: <hvorfor>
<sti der ikke må stå nogen steder>: <hvorfor>
```

Matchning er ikke-versalfølsom og delstrengs-baseret. Skriv derfor **hver
stavevariant** — en term med mellemrum fanger ikke den samme uden.

Filen skal indeholde de firmanavne og stier, der ikke må ud af maskinen.
Kender du dem ikke, så spørg brugeren — de kan ikke gættes, og en tom liste
gør scriptet til en kontrol, der altid siger god for alt.
