# Roadmap

Idéer, der er gode, men ikke skal laves nu. Se `roadmap`-færdigheden.

## Næste

### Notifikationsklokke øverst til højre
*Foreslået 14-08-2026*

En klokke i topbjælken, der giver besked, når noget, man har sat i gang, er
færdigt — transskription, dokument, sikkerhedskopi. Med en tæller, så man kan se,
at der er sket noget, uden at have skærmen fremme.

Kunne også bruges til andet end kørsler: en optagelse, der aldrig blev skrevet
ud, en model, der er blevet forældet, en sikkerhedskopi, der ikke er taget i
tre uger.

**Hvorfor:** Et referat af et 61-minutters møde tog 28 minutter i én kørsel, og
selv med mødet delt op i blokke tager det tid. Uden en besked skal man selv
huske at kigge efter — og så opdager man det først dagen efter. Jobbjælken
nederst virker kun, mens man har appen fremme.

### Bearbejdning hos en europæisk sky-model
*Foreslået 16-08-2026 · under arbejde*

**Budskabet:** Mødeoptagelse og transskription 100 % lokalt. Bearbejdning af
indhold og dokumentskabeloner 100 % europæisk.

Det er en skarpere position end «valgfrit sky-tilvalg», og den gør noget ved
valget af leverandør: Claude og ChatGPT kan ikke bruges til bearbejdningen, for
så holder anden halvdel af sætningen ikke. Mistral (Frankrig) er den eneste af
de undersøgte, hvor «europæisk» og «brugerens egen nøgle» kan være sandt
samtidig. Se `sky-api-priser.md`.

Claude bliver derfor ikke et produkt-tilvalg, men **målestokken**: facit i
`reference.txt` er lavet af Claude, og hver europæisk model måles op mod det.

**Det, der skal efterprøves, før budskabet må bruges:** at Mistral faktisk
hoster i EU, og at der findes en databehandleraftale. «100 % europæisk» er en
compliance-påstand, ikke en markedsføringsvending — den skal stå på skrift fra
leverandøren, før den står i appen.

**Hvorfor:** Den lokale 8B-model rammer et loft, der ikke er til at prompte sig
ud af. Målt på et rigtigt møde mod et facit lavet af en stor model: 66 % af
facits længde, 10 af 34 tal fundet, og omkring **ni opfundne navne** —
«Entropic» for Anthropic, «Nomada» for Omada, «Savion», «Healthspot»,
«BioTrust», «Joachim», «Peng».

**RETTELSE 17-08-2026.** Her stod tidligere «35 opfundne navne» og «ordet
Qwen3 endte i selve referatet». Begge dele var forkerte, og fejlen var min
måling, ikke modellen: bedømmelsen læste udkastets frontmatter med, hvor
modellens eget navn står som proveniens. Facit har ingen frontmatter, så
fejlen ramte kun den ene side af sammenligningen. Qwen3 stod aldrig i
referatet. De ni navne ovenfor er efterprøvet i selve teksten.

**Hvad det koster:** Løftet. Appen siger «Intet forlader denne pc», og det er
hele compliance-vinklen. Sender man udskrifter afsted, gælder det ikke længere —
og det er ikke kun brugerens egne ord, men også mødedeltagernes, som ikke har
sagt ja.

**Hvordan det i givet fald skal bygges:** Lokalt som standard og som forvalg.
Sky som et bevidst tilvalg pr. dokument, aldrig som en indstilling, man sætter
én gang og glemmer. Med en klar besked om præcis hvad der sendes, hvorhen, og
hvad der ikke gør. Nøglen i brugerens eget tastatur, aldrig indbygget.

## Idéer

### Find ud af, hvad der egentlig gør en lang kørsel langsom
*Foreslået 14-08-2026*

Et 61-minutters møde tog 28 minutter i én kørsel — 1162 tokens ud på 1681
sekunder. Det blev regnet om til «0,7 tokens i sekundet» og brugt som argument
for, at modellen lå på processoren.

**Hvorfor det skal undersøges:** Tallet er ikke troværdigt. Det er svarets
tokens delt med HELE forløbet, og forløbet indeholder også indlæsningen af
12.513 tokens prompt — en fase, hvor der ikke kommer tokens ud. De to ting er
blandet sammen, og resultatet ligner en langsom model uden at bevise det.

Mistanken om, at video i baggrunden pressede modellen af kortet, er afvist:
målt med YouTube kørende bruger kortet 401 MiB, præcis som når den er slukket.
Videoafkodning tager næsten ingen VRAM.

Det, der mangler, er en måling, der skiller de to faser: hvor lang tid går til
at læse prompten ind, og hvor lang tid til at skrive svaret. llama.cpp skriver
begge dele ud — de skal bare aflæses og gemmes hver for sig.

### Stykkevis sprogdetektering ved sprogskifte
*Foreslået 13-08-2026*

Whisper finder sproget én gang ud fra de første tredive sekunder. Skifter mødet
sprog undervejs, opdager den det ikke, og resten skrives ud på det forkerte
sprog.

Den rigtige løsning er at dele lyden op og detektere stykkevis.

**Hvorfor:** Målt på den blandede prøvetekst: 83,1 % mod 92,3 % for den rene
danske. Ni procentpoint er det største enkeltudslag, vi har målt — men det er
ikke afklaret, om det skyldes sprogskiftet eller lyden den dag. Det bør måles,
før der bygges: læs teksten op igen med bevidst god mikrofonafstand og se, om
tallet flytter sig.

### Måle om mikrofon og afstand er det store håndtag
*Foreslået 14-08-2026*

Læs den blandede tekst op igen med bevidst god mikrofonafstand og sammenlign med
de 83,1 %.

**Hvorfor:** Alle de håndtag, vi har prøvet, er målt til næsten ingenting —
ordlisten til Whisper gav nul, sprogmodellen over udskriften rettede to ord ud
af 3418. Lydkvaliteten er den eneste tilbageværende mulighed for et spring frem
for en decimal, og den er aldrig blevet målt.

## Lagt væk

### Sprogmodellen skal rette Whispers fejl
*Prøvet af og fravalgt 14-08-2026*

Idéen: giv Qwen den rå udskrift og bed den rette åbenlyse fejlhøringer ud fra
sammenhængen, før der laves dokumenter.

**Hvorfor ikke:** Målt på alle tre prøvetekster. Dansk 92,3 → 92,4 %, engelsk
90,5 → 90,7 %, blandet 83,1 → 83,1 %. **To ord ud af 3418** for 5 minutter og
22 sekunders GPU-tid.

Grunden er værd at huske: en sprogmodel kan kun rette det, der SER forkert ud.
«cybernummer» ser forkert ud — men «går» i stedet for «gik», «af» i stedet for
«at» læser fuldstændig naturligt. Der er intet signal at gå efter, og det er
dér, tre fjerdedele af fejlene ligger.

Kommandoen `noteapp llmret` er beholdt, så forsøget kan gentages med en anden
model eller en anden formulering.
