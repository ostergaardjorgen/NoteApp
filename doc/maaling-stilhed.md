# Whisper digter, når ingen siger noget

Målt 25-08-2026 på et rigtigt møde: 32,7 minutter, rent dansk, mikrofonsporet.
Model `large-v3`, appens egne argumenter.

## Hvad der blev fundet

Transskriptionen indeholdt to sætninger, som ingen havde sagt:

```
[19:48 --> 20:18]  Danske tekster af Jesper Buhl Scandinavian Text Service 2018
[32:29 --> 32:59]  Danske tekster af Nicolai Winther
```

Dertil **66 segmenter, der kun var «Ja.»** — spredt gennem hele optagelsen.

Whisper er trænet på undertekster. Får den tredive sekunder uden tale, finder
den ikke ingenting; den finder det, der plejer at stå, hvor der ikke bliver
sagt noget. Det ender i transskriptionen som alt andet, og derfra går det
videre til opsummeringen som noget, der ligner indhold.

## Hypotesen: bliver modellen træt?

Begge opdigtninger lå sent i optagelsen, og det ligner en model, der kører af
sporet, jo længere den arbejder. Det blev prøvet direkte: den samme lyd delt i
**syv uafhængige bidder af fem minutter**, hver kørt som sin egen fil.

| | Opdigtninger |
|---|---|
| Én kørsel på 32 minutter | 2 |
| Syv bidder af fem minutter | 2 |

Nøjagtig lige mange, og den ene på samme absolutte sted. **At dele lyden op
ændrer ingenting.**

Det er der en grund til: whisper.cpp arbejder i forvejen i uafhængige
30-sekunders vinduer, og appen kører med `-mc 0`, så intet bæres med videre
mellem dem. Der findes ingen mekanisme, modellen kan blive træt af.

## Hvad der så udløser det

Vinduer **uden tale**. Målt som andelen af 20-ms rammer over talegrænse:

| Vindue | Taleandel | |
|---|---|---|
| Gennemsnit | 34,9 % | |
| 19:30 | 6,5 % | her digtede large-v3 |
| 32:30 | 0,0 % | her digtede begge modeller |
| 02:00–08:00 | 1,3–4,5 % | **de tætteste tomme vinduer i hele optagelsen** |

At det lignede et sent problem, skyldes at slutningen af et møde er dér, den
længste ægte stilhed er. De tommeste vinduer lå i begyndelsen.

## Rettelsen

Stilhedsdetektion — `--vad` med Silero-modellen, 885 KB. Tomme vinduer når
aldrig frem til modellen.

| | Tid | Opdigtninger | Tomme «Ja» | Negationer |
|---|---|---|---|---|
| Uden | 7:27 | 2 | 66 | 19 |
| Med, 30 ms luft | 1:48 | 0 | 1 | 19 |
| **Med, 200 ms luft** | **1:57** | **0** | **0** | **20** |
| Med, 400 ms luft | 5:28 | 0 | 7 | 19 |

**Om hastigheden: tallet holder ikke som en generel påstand.** Det er målt på
mikrofonsporet. Det ANDET spor i samme møde — samme længde, samme indstillinger,
samme sprog, sammenligneligt antal talestykker (258 mod 294) og ord (2.508 mod
2.964) — tog **8 minutter 9 sekunder** målt 25-08-2026 kl. 17:56.

Forskellen ligger ikke i arbejdsmængden, men i hastigheden pr. kørsel:
indkodning 282 ms mod 1.563 ms, batch 3,75 ms mod 14,79 ms. Begge kørsler
brugte CUDA på samme kort med samme opsætning. **Hvorfor er ikke afklaret**, og
der er gættet forkert to gange allerede (først "den hænger", derefter "det ene
spor har mindre tale" — begge afvist af måling).

Indtil det er undersøgt på en rolig maskine: **lov ikke en hastighed.**
Stilhedsmodellen beholdes for det, der ER efterprøvet — nul opdigtninger mod
to, og nul tomme «Ja» mod 66.

**Luften er sat til 200 ms.** Standarden er 30 ms og klipper for tæt; 400 ms
er næsten tre gange langsommere uden at være bedre.

## Det ene omstridte sted — afgjort ved gennemlytning

Ét sted er de to læsninger uenige:

- uden VAD: «Men jeg tror **ikke** du kan være helt sikker på»
- med VAD (alle tre kørsler): «Men jeg tror du kan være helt sikker på»

Klippet blev lyttet igennem 25-08-2026. Der bliver sagt:

> «**Tror du ikke** at du kan være helt sikker på at får du...»

**Begge transskriptioner er forkerte, og den, der havde flest negationer, var
den, der forstod dårligst.**

- Uden VAD: «jeg tror **ikke** du kan være helt sikker på». Ordet er der, men
  det er blevet til en førstepersons **påstand**. Meningen er vendt om.
- Med VAD: «jeg tror du kan være helt sikker på». Ordet mangler, men meningen
  rammer.

Det siger noget vigtigt om, hvordan det her måles. **Et retorisk spørgsmål med
«ikke» betyder det modsatte af en benægtelse.** «Tror du ikke, at du kan være
sikker?» betyder, at man kan. At tælle forekomster af «ikke» er derfor ikke et
mål for, om betydningen er bevaret — det kan pege stik modsat, og det gjorde
det her.

Den, der talte, kalder selv passagen «meget utydelig». Det er altså ikke
bevis for, at VAD taber tale i almindelighed; det er ét sted, hvor lyden er
dårlig, og hvor ingen af indstillingerne rammer.

### Kan en lavere taletærskel redde den?

VAD har sin egen grænse for, hvad der tæller som tale (`--vad-threshold`,
standard 0,50). Utydelig tale er præcis det, den grænse afviser. Prøvet:

| Tærskel | Tid | Opdigtninger | Det omstridte sted |
|---|---|---|---|
| **0,50** | **1:57** | **0** | uden «ikke» |
| 0,30 | 2:02 | 0 | uden «ikke» |
| 0,15 | **12:09** | 0 | uden «ikke» |
| *ingen VAD* | *7:27* | *2* | *«ikke», men forkert mening* |

Tærsklen flytter det ikke. Over et spænd på mere end tre gange giver alle tre
den samme læsning: ordet er for utydeligt til, at VAD hører det som tale. Det
stemmer med, hvad taleren selv siger om optagelsen.

Og 0,15 viser, hvorfor man ikke bare skruer ned: **12:09 er langsommere end
slet ikke at bruge stilhedsdetektion** (7:27). En lav tærskel lukker ikke bare
stilheden ind igen — den lader VAD hakke den op i mange små stykker først, og
så betaler man for begge dele.

**Indstillingen bliver på 0,50.** En lavere tærskel køber ingenting og koster
alt.
