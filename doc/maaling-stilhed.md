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

Næsten fire gange hurtigere, fordi stilhed slet ikke sendes til modellen.

**Luften er sat til 200 ms.** Standarden er 30 ms og klipper for tæt; 400 ms
er næsten tre gange langsommere uden at være bedre.

## Det, der ikke er afgjort

Ét sted er de to læsninger uenige:

- uden VAD: «Men jeg tror **ikke** du kan være helt sikker på»
- med VAD (alle tre kørsler): «Men jeg tror du kan være helt sikker på»

Antallet af negationer er det samme i alle kørsler, så der er ikke tale om et
systematisk tab. Men hvilken af de to der er rigtig, kan ikke afgøres af
teksten — kun ved at høre lyden. Klippet er sendt til gennemlytning
25-08-2026; udfaldet skal skrives ind her.

Grammatikken taler for VAD-udgaven: det følgende led er «at hvis du får de
der succesoplevelser...», som hænger sammen med «du kan være helt sikker på»
og ikke med negationen.
