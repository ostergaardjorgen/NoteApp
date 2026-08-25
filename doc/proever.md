# Prøver

*Skrevet 24-08-2026. 63 prøver, alle består.*

```bash
powershell -File C:\NoteApp\scripts\proev.ps1
```

Scriptet afslutter med kode 1, hvis noget fejler, så det kan bruges som en
gate før en commit — på samme måde som leverancetjekket.

---

## Hvad der prøves, og hvorfor lige det

Der prøves kun på `NoteApp.Core`. Skærmene er WPF og kræver en vinduestråd;
det, der er værd at spænde fast, er i forvejen flyttet ned i kernen, netop
fordi det skulle kunne prøves.

Udvælgelsen er ikke «så meget som muligt». Det er **de steder, hvor en fejl
ikke opdages, før skaden er sket**.

| Område | Prøver | Hvad der ville gå galt uden |
|---|---|---|
| **Oprydning af lydfiler** | 9 | En optagelse slettes for altid, og det opdages måneder senere |
| **Overvågede mapper** | 14 | Filer tilbydes igen og igen, eller slet ikke |
| **Lydfiler udefra** | 15 | En halv optagelse i listen, som hverken kan skrives ud eller forklares |
| **Kalenderens afløsning** | 7 | Et møde bliver ikke optaget, selv om hakket ser ud til at stå |
| **Navne og tider** | 18 | Mapper, der ikke kan oprettes, og datoer, der lyver |

### Oprydningen er de vigtigste prøver i hele projektet

Tre regler, én prøve hver, og de er skrevet som regler frem for som funktioner:

1. **Aldrig uden en udskrift.** Uden teksten er der ikke noget tilbage
   bagefter. Så er det ikke oprydning — så er det sletning.
2. **Aldrig ved nul dage eller mindre.** Nul kunne læses som «slet alt med det
   samme». Præcis den læsning skal være umulig: en indstilling, der ved et
   uheld bliver nul, må ikke tømme arkivet.
3. **Aldrig andet end lyd.** Noter, dokumenter, `meeting.json` og udskriften
   står urørt.

Dertil en prøve på **grænsetilfældet**: en udskrift på præcis 365 dage ved en
grænse på 365 skal ryddes. Det er det eneste sted, en fortegnsfejl kan gemme
sig.

### Kalenderen: det, der skal overleve en hentning

Hvert kvarter kastes alt fra Google væk og erstattes af det hentede. Det, der
skal overleve den udskiftning, er appens egne felter — mødetype, mappe, sprog
og **især flaget om automatisk optagelse**. Går det tabt, står aftalen der
stadig, den ser rigtig ud, og der bliver bare ikke optaget.

Der prøves også på det modsatte: flyttes mødet, skal `Startet` nulstilles, så
vagten optager på det nye tidspunkt. Ellers ville en flytning stille og roligt
afmelde optagelsen, og det ville se ud som en fejl i vagten.

---

## De rører aldrig rigtige data

`UserDataPaths` lader miljøvariablen `NOTEAPP_DATA` vinde over alt andet, og
den læses forfra ved hvert opslag. Hver prøve peger den på sin egen mappe under
Temp og rydder op efter sig — også når prøven fejler.

**Det er efterprøvet, ikke antaget.** `proev.ps1` tæller optagelserne i den
rigtige datamappe før og efter hver kørsel og fejler, hvis listen har ændret
sig. Kørt 24-08-2026: otte optagelser før, otte efter, ingen forskel.

Prøverne kører **én ad gangen**. `NOTEAPP_DATA` er global for processen, så to
klasser samtidig ville pege ind i hinandens datamapper og fejle på skift, uden
at der var noget galt med koden. En prøve, der fejler tilfældigt, er værre end
ingen prøve — så holder man op med at tro på den.

---

## Det, der IKKE prøves her

**Selve omsætningen af lyd.** Den kræver en rigtig lydfil og Windows' kodeks.
Den er efterprøvet i hånden på en talememo fra en iPhone: 29 sekunders lyd
læst ind på 0,4 sekund og skrevet ud som dansk tekst med 99 % sprogsikkerhed.

**Google-hentningen.** Den kræver et login og et rigtigt svar fra Google. Den
er efterprøvet mod den rigtige konto — se `noteapp google` i
kommandolinjeværktøjet.

**Skærmene.** WPF kræver en vinduestråd, og en prøve, der klikker på knapper,
er langsom og skør. Der er i stedet lagt vægt på at holde logikken ude af dem.

**Nøjagtigheden af transskriptionen.** Den måles med `maal-noejagtighed.ps1`
mod kendt facit og hører til i `findings.md`, ikke her. En prøve svarer ja
eller nej; en måling svarer med et tal.

---

## En fælde, der kostede tid — skrevet ned

Første kørsel havde tre fejlende prøver på `MeetingStore.Slug`. **Fejlen var
prøvens, ikke appens.**

`Assert.DoesNotContain(string, string)` i xUnit sammenligner **kulturfølsomt**,
og ICU regner styretegn som ignorerbare. En søgning efter `"\0"` rammer derfor
i en hvilken som helst streng. Mappenavnet var helt rent:
`2026-08-24_14-30_Hvad-Nu-Igen`.

Rettelsen er at sammenligne på **tegn** frem for på strenge. Det står som en
kommentar i prøven, så det ikke skal findes ud af igen.

---

## Når der bygges noget nyt

En prøve hører til, hvor svaret er **ja eller nej**, og hvor et forkert svar
ikke råber op. Er der tvivl, er spørgsmålet: *hvis den her regel blev brudt i
morgen, hvor længe ville der gå, før nogen opdagede det?*

Går der mere end en dag, skal der en prøve på.
