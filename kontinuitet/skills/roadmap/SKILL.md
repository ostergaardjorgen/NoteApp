---
name: roadmap
description: Holder styr på idéer, der er gode, men ikke skal laves nu. Brug den når brugeren siger "læg det på roadmap", "det kan vi vende tilbage til", "en roadmap-ting", "gem den idé" — eller beder om "oversigt over roadmap", "hvad ligger på roadmap", "hvad mangler vi". Bruges også når du selv støder på noget, der er værd at gøre, men ligger uden for det, der arbejdes på lige nu.
---

# Roadmap

Et sted at lægge de idéer, der er gode, men ikke skal laves nu.

Den findes, fordi de ellers går tabt. En idé, der bliver nævnt midt i noget andet, bliver hverken lavet eller skrevet ned — og et kvarter senere kan ingen huske den. Det er ikke en prioriteringsliste og ikke en aftale om, at noget bliver lavet. Det er en huskeseddel.

## Hvor den ligger

`doc\roadmap.md` i det projekt, der arbejdes i. Den følger med koden, så den kan læses af den, der overtager, og kommer med i en sikkerhedskopi.

Findes filen ikke, så opret den med overskriften og de tre afsnit fra skabelonen nedenfor.

## Sådan lægger du noget på

Når brugeren siger "læg det på roadmap" eller lignende, **skriv det ned med det samme** og kvittér kort. Spørg ikke ind til detaljer — det er hele pointen, at det ikke skal koste noget at få idéen gemt.

Én post ser sådan ud:

```markdown
### Notifikationsklokke øverst til højre
*Foreslået 14-08-2026*

Besked når noget, man har sat i gang, er færdigt — transskription, dokument,
backup. Kunne også bruges til andet: en optagelse, der aldrig blev skrevet ud,
eller en model, der er blevet forældet.

**Hvorfor:** Et referat af et langt møde tager tid. Uden en besked skal man selv
huske at kigge efter, og så opdager man det først dagen efter.
```

Skriv **hvorfor** med, ikke kun hvad. Om et halvt år er "notifikationsklokke" ikke nok til at genskabe, hvad problemet var — og en idé, man ikke kan huske begrundelsen for, bliver hverken lavet eller kasseret.

Er der en måling eller en konkret hændelse bag, så tag tallet med. Det er dét, der senere afgør, om idéen stadig er relevant.

## Sådan giver du oversigten

Når der bedes om en oversigt, så vis **hele filen** som en kort liste med overskrift og én linje pr. post — ikke hele teksten. Grupper efter de tre afsnit.

Sig til sidst, hvor mange der er i alt, og spørg om noget skal tages op nu.

## De tre afsnit

| Afsnit | Hvad der hører til |
|---|---|
| `## Næste` | Det, der skal laves, når der er tid. Bevidst valgt. |
| `## Idéer` | Alt andet. Her lander nye poster som standard. |
| `## Lagt væk` | Kasseret — med begrundelsen. Slet aldrig en post; flyt den hertil. |

En kasseret idé skal **blive stående** med sin begrundelse. Slettes den, er der intet, der forhindrer, at den bliver foreslået igen om et halvt år af præcis de samme gode grunde — og så koster den de samme timer at afvise en gang til.

## Når noget bliver lavet

Fjern posten fra roadmappen, og nævn i commit-beskeden, at den kom fra roadmappen. Så kan man senere se, hvad der faktisk blev til noget.

## Skabelon til en ny fil

```markdown
# Roadmap

Idéer, der er gode, men ikke skal laves nu. Se `roadmap`-færdigheden.

## Næste

## Idéer

## Lagt væk
```
