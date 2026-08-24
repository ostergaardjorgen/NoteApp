# Salgsargumenter

*Oprettet 24-08-2026. Skal vedligeholdes — se «Reglen for det her dokument» nederst.*

Argumenter, der kan tages frem når som helst, og som kan holde til at blive
sagt imod. Hvert punkt står med **belægget** og med **det, der ikke skal
siges** — et argument, der falder fra hinanden i det første modspørgsmål,
koster mere end det giver.

---

## Hovedargumentet

> **Hold op med at videooptage dine møder. Det, du har brug for, er en
> transskription af det, der blev sagt.**

Videooptagelse af onlinemøder er blevet standard, fordi knappen sad der — ikke
fordi nogen besluttede, at det var det rigtige. Ingen ser en times video igen.
Man leder efter én sætning: hvad blev der aftalt om prisen, hvem skulle sende
hvad, hvornår var det, de sagde.

Den sætning findes i teksten. Videoen er alt det, ingen skulle bruge — og alt
det, folk siger nej til.

---

## 1 · Det er lettere at sige ja til lyd

**Påstanden:** Deltagerne siger nemmere ja, når de ved, at der ikke filmes.

**Belægget:** Det er en forskel i, hvad man udleverer. En videooptagelse af et
onlinemøde viser ens hjem, ens tøj, ens ansigt gennem en time — og den kan ses
igen af nogen, man ikke kender. En lydoptagelse viser, hvad man sagde. Det er
det, mødet handlede om.

NoteApp optager **kun lyd**. Der findes ingen kodesti, der gemmer et billede;
appen har aldrig set skærmen eller kameraet. Det står i første linje af den
mødeindkaldelse, appen skriver — se Indstillinger → Integrationer.

**Sig ikke:** at det er «lovligt uden samtykke» eller lignende. Det afhænger af
land, sammenhæng og hvem der deltager, og det er ikke noget, en app kan
erklære på kundens vegne.

---

## 2 · Lyden forlader aldrig maskinen

**Påstanden:** Optagelsen bliver på kundens egen computer. Ikke i en amerikansk
sky — og ikke i nogen sky overhovedet.

**Belægget:** Udskrift, søgning, talergenkendelse, opgavefund og den korte
opsummering kører alle lokalt. Det står i koden og kan efterprøves.

**Vær præcis om det ene sted, hvor noget forlader maskinen:** vælger kunden at
få lavet et færdigt dokument, sendes **den udskrevne tekst** — ikke lyden — til
Mistral i Frankrig. Det er et valg, brugeren træffer i situationen, aldrig en
automatik.

**Sig ikke:** «alt bliver hos dig». Det er forkert i det øjeblik, et dokument
laves, og det er præcis dér, en indkøber ser efter.

**Modspørgsmålet, du får:** «Men I henter jo min kalender fra Google?» — ja, og
det er den anden vej rundt: aftalerne ligger der i forvejen, og appen læser
dem. Har kunden Google Workspace, har de allerede taget stilling. Se
Compliance → Andre integrationer.

---

## 3 · Det, du faktisk bruger, fylder 56 KB

**Påstanden:** Teksten er det brugbare, og den fylder ingenting.

**Belægget — målt på rigtige møder 24-08-2026:**

| Møde | Længde | Lyd (WAV) | Udskrift |
|---|---|---|---|
| Kundemøde, to spor | 61 min | 223 MB | 56,5 KB |
| Webinar, ét spor | 59 min | 98 MB | 50,5 KB |
| Møde, to spor | 27 min | 41 MB | 22,6 KB |

Teksten fylder **omkring en firetusindedel** af lyden. Det er den, der kan
søges i, kopieres fra og gemmes i årevis uden at fylde noget.

Til sammenligning oplyser Zoom selv **cirka 200 MB pr. time** for en
videooptagelse i skyen ([Zoom
Support](https://support.zoom.com/hc/en/article?id=zm_kb&sysparm_article=KB0067670)).

**Sig ikke, at lydfilen fylder mindre end video.** Det gør den ikke: NoteApps
WAV er ukomprimeret med vilje, fordi det giver den bedste udskrift, og en times
møde med to spor fylder mere end Zooms video. Forskellen er, **hvor** den
ligger, og at man selv kan slette den, når teksten er i hus. Videoen ligger hos
leverandøren, så længe abonnementet løber.

**Hvorfor lyden fylder så meget — hvis nogen spørger:** vores lyd er
ukomprimeret. 16 000 Hz, 16 bit, ét spor = 110 MB pr. time, og et møde har to
spor. Zooms video er komprimeret ned med omkring en faktor 600, fordi et møde
er stillestående hoveder på en fast baggrund. Rå video i 720p ville fylde 118 GB
i timen.

Det er altså ikke lyd mod video. Det er ukomprimeret mod komprimeret.

*Åbent punkt: med Opus ved 24 kbit/s ville en times møde fylde 21,6 MB mod
Zooms 200 MB — en niendedel. Ikke bygget, og det er ikke målt, om udskriften
bliver lige så god af komprimeret lyd. Se [`findings.md`](findings.md) 9.1.*

---

## 4 · Mindre at slæbe rundt på — også for klimaet

**Påstanden:** Færre gigabyte gemt og flyttet betyder mindre energiforbrug.

**Belægget:** Det er en retning, ikke et tal. En videooptagelse skal kodes,
sendes op, opbevares i et datacenter og sendes ned igen, hver gang nogen ser
den. En transskription på 56 KB skal ingen af delene.

**Sig ikke et gram-tal.** Vi har ikke målt CO2, og en beregning, der bygger på
en gennemsnitlig datacenterstrøm fra en artikel, er et gæt med to decimaler.
Bliver kunden ved, så sig, at vi ikke har målt det — og at vi hellere vil sige
det end at finde på et tal.

**Vær ærlig om vores eget forbrug:** udskriften kører på kundens grafikkort.
Målt til RTF 0,09 — en times møde tager omkring fem et halvt minuts kørsel. Det
er ikke ingenting. Det er bare langt mindre end det samme møde kodet, sendt og
opbevaret som video.

---

## 5 · Én søgning på tværs af det hele

**Påstanden:** Efter tredive møder er værdien ikke det enkelte referat. Det er
at kunne finde svaret.

**Belægget:** Søgningen læser alle transskriptioner, alle noter og alle
dokumenter, og hvert træf peger på **stedet i teksten** — ikke bare på filen.
Målt på tyve spørgsmål med kendt facit: 90 % på førstepladsen, 100 % i top tre.

Alt sammen på maskinen. Der sendes intet for at søge.

---

## 6 · Alt samlet ét sted

**Påstanden:** Møder, webinarer og optagelser fra telefonen ligger samme sted og
kan søges på tværs.

**Belægget:** Det er dét, produktet er sat i verden for. Kunden er studerende,
iværksættere og mindre selvstændige, som har deres online-liv spredt over fire
programmer.

**Modspørgsmålet:** «Hvorfor ikke bare Granola?» — se nedenfor.

---

## Op mod Granola

Granola er målestokken, og den er god. Forskellen at gå efter:

**Efterprøv kolonnen om Granola, før den bruges over for en kunde.** Den er
skrevet ud fra, hvad et sky-produkt af den type gør — ikke ud fra deres vilkår
læst igennem med en dato på. At tage fejl om en konkurrent er den dyreste måde
at miste troværdighed på, og det er den eneste tabel i dokumentet uden et
belæg, vi selv har målt.

| | Granola | NoteApp |
|---|---|---|
| Hvor lyden behandles | i skyen | på maskinen |
| Hvad der sendes op | lyden | intet — medmindre du beder om et dokument, og så er det teksten |
| Sky-leverandør | USA | EU (Mistral), og kun når du vælger det |
| Video | — | optages aldrig |
| Virker uden konto | nej | ja |

**Sig ikke noget nedsættende om Granola.** Den er et godt produkt, og en kunde,
der bruger den, har truffet et fornuftigt valg. Argumentet er ikke «de er
dårlige», det er «lyden bliver hos dig».

---

## Modspørgsmål, du får — og de ærlige svar

### «Zoom/Teams laver jo også en transskription. Hvad er forskellen?»

**Sig ikke, at deres video er ubrugelig, eller at man skal se den igennem for
at finde noget.** Det er forkert. Zoom, Teams og Google Meet laver alle tre
transskriptioner af deres skyoptagelser, og de er søgbare.

Lydsporet i en komprimeret mødeoptagelse er også rigeligt til talegenkendelse.
Whisper nedsampler selv alt til 16 kHz mono, så en AAC-lyd på 64 kbit/s taber
ikke noget, der betyder noget for teksten. En påstand om, at deres tekst er
dårligere, ville desuden være uden belæg — vi har ikke målt deres.

**Den rigtige forskel:** for at få den transskription skal lyden op i deres
sky. Der skal den behandles, og der bliver den liggende sammen med videoen, så
længe abonnementet løber. Hos os bliver lyden på maskinen, og videoen findes
ikke.

Og: deres transskription dækker det møde, den kom fra. NoteApp søger på tværs
af alle møder, alle noter og alle dokumenter — også dem fra telefonen og fra
webinarer, som deres værktøj aldrig så.

### «Fylder jeres lydfil ikke mere end deres video?»

Jo, i dag. Se punkt 3 — og sig det rent ud. Det er ukomprimeret mod
komprimeret, ikke lyd mod video, og filen ligger på kundens egen disk, hvor den
kan slettes, når teksten er i hus.

---

## Det, vi ikke kan påstå

Står her, så det ikke bliver sagt ved et uheld:

- **«Vi er GDPR-compliant.»** Det er ikke noget, en app kan erklære om sin
  bruger. Vi kan sige, hvad der sker med data. Konklusionen er kundens.
- **«Alt bliver på din maskine.»** Forkert i det øjeblik, et dokument laves.
- **«Transskriptionen er perfekt.»** Målt: 92,3 % på dansk, 90,5 % på engelsk,
  83,1 % på blandet dansk-engelsk. Navne og fagord er det, der oftest rammes
  forkert.
- **«Appen kan kende stemmerne fra hinanden.»** Den kan skille de to SIDER af et
  onlinemøde — din mikrofon og det, computeren afspiller. Ikke de enkelte
  personer i den anden ende.
- **Et CO2-tal.** Ikke målt.
- **«Vores transskription er bedre end Zooms/Teams'.»** Ikke målt. Vi kender
  vores egen nøjagtighed; vi kender ikke deres.
- **«Deres video kan man ikke søge i.»** Forkert — de laver transskriptioner
  af den. Se modspørgsmålene ovenfor.

---

## Reglen for det her dokument

**Et argument herinde skal have et belæg, og belægget skal kunne findes.**
Måletal kommer fra [`findings.md`](findings.md) med dato. Tal fra andre skal
have et link til kilden.

**Ændrer produktet sig, ændres argumentet i samme arbejdsgang.** Et
salgsargument, der var sandt i august, er det farligste sted at have en gammel
oplysning — det er det, der bliver sagt højt til en kunde.

Det gælder især:

- Komprimeres lyden en dag, skal punkt 3 skrives om
- Kommer der en Microsoft-integration, skal punkt 2 have den med
- Bliver noget målt, som i dag står som et skøn, skal skønnet erstattes
