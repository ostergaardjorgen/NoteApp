---
name: feedback-vis-altid-hvad-der-koerer
description: "Strategien er at holde brugeren orienteret hele vejen — ét sted, der bevæger sig, og aldrig et tal, der står stille"
metadata:
  node_type: memory
  type: feedback
  originSessionId: fcf79dd9-960e-41ff-a342-4fa7d5ee2bfe
  modified: 2026-09-04T09:11:43.749Z
---

**Strategien er at holde brugeren orienteret hele vejen.** Sætter en handling
noget i gang, der tager tid, skal der fra trykket til resultatet være noget,
der bevæger sig og siger, hvad der sker.

**Why:** Jørgen har måttet påpege det gentagne gange — 2., 3. og to gange 4.
september 2026. En kørsel uden synligt spor får brugeren til at tro, at appen
er gået i stå, eller at handlingen slet ikke blev udført. Så trykker man igen,
eller giver op. Det er ikke pynt; det afgør, om man tør bruge funktionen.

**How to apply:**

1. **Efterprøv fremdriften i en kørsel, ikke i koden.** Er der ingen, så byg
   en. Flytter jeg en handling, skal fremdriften med, og teksten skal pege på
   det sted, den faktisk vises.
2. **Ét sted, ikke fire.** 04-09-2026 blev den samme kørsel vist fire steder på
   én gang — to af dem blev aldrig færdige. I HeyPia er stedet **jobbjælken
   nederst i vinduet**: den følger kørslen hele vejen, også afslutningen, og
   den er synlig på alle skærme. Statuslinjen ved teksten må gerne supplere.
3. **Et tal, der står stille, er værre end intet tal.** Sekunderne stod bagt
   ind i motorens egne meldinger; motoren meldte én gang, så linjen stod på
   «· 0 sek» i 53 sekunder og sprang så til 53. Uret skal gå for sig selv —
   i `BackgroundJobs` er det en timer på ét sekund, standset før den
   afsluttende melding, så et tik ikke kan overskrive «Færdigt: …».
4. **Pas på flag, der ryddes i et `finally`.** De ryddes EFTER den sidste
   melding, så alt, der tænder på dem, hænger fast i «i gang» for evigt.
   Det var præcis fejlen i `BackgroundJobs.Moedetype`.

Se [[meld-altid-release-nummer]] og [[feedback-byg-efter-commit]] — samme
grundregel: brugeren skal ikke gætte på, hvad der foregår.
