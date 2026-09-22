---
name: noteapp-version-fra-commit
description: "NoteApps versionsnummer udledes af seneste commit-besked; 99 er højeste tal i hvert led, og tredje led vises med to cifre"
metadata:
  node_type: memory
  type: project
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-25T15:05:00.000Z
---

`scripts\udgiv.ps1` læser versionen ud af den seneste commit-besked, der skal
have formen `vX.Y.ZZ: beskrivelse`. Den skrives ind i csproj og ender i appens
sidebjælke.

**99 er højeste tal i hvert led.** Når tredje led når 99, ruller andet led:
efter `1.0.99` kommer `1.1.00`. Tredje led skrives med to cifre — `v1.1.07`,
ikke `v1.1.7`.

**Why:** To grunde, begge set i praksis. Tredje led løb til 108, og `1.0.108`
sorterer FØR `1.0.99` i alt, der sammenligner tekst — så udgivelserne stod i
forkert rækkefølge overalt, hvor de blev stillet op. Og flere udgivelser uden
en commit imellem får alle det SAMME nummer; under en arbejdsdag med tyve
builds står der det samme på dem alle.

**How to apply:** .NET kender ikke foranstillede nuller i en `Version` —
skriver man `1.1.07` i csproj, bliver det til `1.1.7` uden at sige det.
Derfor gemmes `1.1.7`, og sidebjælken sætter nullet på, når den viser det.
`udgiv.ps1` spærrer for et tredje led over 99 og lukker overløbet igennem
uden at spørge. Meld altid tidsstemplet sammen med nummeret
(se [[meld-altid-release-nummer]]). Kør `leverancetjek` før hver commit.
