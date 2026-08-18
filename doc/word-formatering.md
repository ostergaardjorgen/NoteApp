# Sådan skal Word-dokumenter se ud

Standarden for alle dokumenter, appen genererer. Værdierne er ikke valgt af
mig — de er **aflæst af et rigtigt dokument**, brugeren selv har udpeget, så
et referat fra appen ser ud som resten af det, der bliver sendt ud.

Kilden ligger uden for repoet. Værdierne nedenfor er det, der blev læst ud af
den 18-08-2026, og de er implementeret i
`src/NoteApp.Core/Documents/DocxWriter.cs`.

---

## Skrifttyper

| Rolle | Skrift | Hvor |
|---|---|---|
| Brødtekst | **Aptos** | `docDefaults` |
| Overskrifter og titel | **Aptos Display** | Title, Heading1-3 |

Aptos er Microsofts standardskrift fra 2024 og frem — den, der afløste
Calibri. Aptos Display er dens overskriftsvariant: samme familie, lidt
strammere bogstavafstand.

**Skrifterne skrives med navn, ikke som temaskrift.** Et dokument, der peger
på `majorHAnsi`, kræver en `theme1.xml` for at kunne slå navnet op; skriver man
navnet direkte, virker filen uden. Prisen er, at dokumentet ikke følger med,
hvis modtageren skifter tema — og det er den rigtige pris at betale for et
referat, der skal se ens ud hos alle.

## Størrelser

Word regner i **halve point**. Tallet i XML er altså det dobbelte af punkter.

| Typografi | Punkter | XML (`w:sz`) |
|---|---|---|
| Titel | 28 pt | 56 |
| Overskrift 1 | 16 pt | 32 |
| Overskrift 2 | 13 pt | 26 |
| Overskrift 3 | 12 pt | 24 |
| Brødtekst | 11 pt | 22 |

## Farver

| Element | Farve | |
|---|---|---|
| Titel | sort (arvet) | |
| Overskrift 1 og 2 | `#2E74B5` | mellemblå |
| Overskrift 3 | `#1F4D78` | mørkere blå |
| Brødtekst | sort (arvet) | |
| Proveniens («Kilde») | `#666666` | grå, 9 pt |

**Titlen er sort.** Det er referencedokumentets valg og et rigtigt et: en
farvet titel over farvede overskrifter gør, at ingen af dem skiller sig ud.

**Overskrifter er ikke fede.** De skiller sig ud på størrelse og farve. Fed
oveni gør dem tunge i et langt referat.

## Side

| | |
|---|---|
| Format | A4 (11906 × 16838 twips) |
| Margen foroven og forneden | 850 twips ≈ 1,5 cm |
| Margen i siderne | 1100 twips ≈ 1,94 cm |
| Sidehoved og -fod | 708 twips |

Smallere end Words standard på 2,5 cm hele vejen rundt. Det giver plads til
mere tekst pr. side, uden at linjerne bliver for lange til at læse.

**Sidestørrelsen SKAL stå i filen.** Uden `sectPr` vælger Word sin egen
standard, som på en maskine med amerikansk regionsopsætning er Letter — og så
falder sidebrud et andet sted, end man har set dem.

## Afsnit

| | |
|---|---|
| Afstand efter afsnit | 160 twips (8 pt) |
| Linjeafstand | 259 (1,08) |
| Punktopstilling | rigtige punkttegn via `numbering.xml`, indryk 720 twips |
| Forsideblok («Kilde») | `contextualSpacing` — linjerne står tæt, blokken slipper teksten under sig |

---

## Regler, der ikke handler om udseende

Tre ting, der er lige så vigtige, og som hver kostede en fejl:

**1. `settings.xml` med `compatibilityMode 15`.** Uden den antager Word
kompatibilitetstilstand 12 (Word 2007) og spørger, om filformatet skal
opdateres, første gang man gemmer. Det ser ud, som om filen er gammel eller
forkert.

**2. Tomme linjer bliver ikke til tomme afsnit.** Markdown bruger den tomme
linje som skilletegn; Word bruger afstand efter hvert afsnit. Oversætter man
den ene til den anden, får man begge dele — dobbelt luft hele vejen ned. Målt:
90 afsnit blev til 48, og fem sider til fire.

**3. `xml:space="preserve"` på hvert tekststykke.** Uden den æder Word
mellemrum i begyndelsen og slutningen af hvert stykke, og så klistrer ordene
omkring **fed** sammen.

---

## Hvis standarden skal ændres

Værdierne står som konstanter øverst i `DocxWriter.Styles()`. Ret dem der, og
ret dette dokument samtidig — to steder, der siger hver sit om, hvordan
dokumenter ser ud, er værre end ingen af dem.

Skal der hentes værdier fra et nyt referencedokument, er fremgangsmåden:
pak `.docx`-filen ud som ZIP, og læs `word/theme/theme1.xml` (skrifter og
temafarver) og `word/styles.xml` (størrelser, farver og afstande pr.
typografi).
