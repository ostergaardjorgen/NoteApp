---
name: feedback-leverancetjek-som-port
description: "Ved navneskifte eller udskillelse af et repo: gør leverancetjek til den målbare definition af færdig, og kør dekontamineringen før første commit"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 3be0811d-95c3-4771-8ac4-70e6d51317b8
  modified: 2026-09-21T14:40:55.337Z
---

Når et repo skal skifte navn eller skilles ud fra en tidligere identitet:
**kør dekontamineringen FØR første commit, og lad `leverancetjek` være
definitionen af færdig** — ikke min vurdering.

**Why:** en oprydningscommit bagefter hjælper ikke. Termen ligger så i
historikken hos alle med adgang, og det kræver historik-omskrivning og
tvunget push at fjerne. Ved Vagtsom IAM (2. september 2026) stod det
gamle navn i ~8.000 commit-beskeder — historikken kunne ikke reddes, kun
undgås. Målt virkede porten: 3.221 fund i udgangspunktet, 0 i resultatet.

**How to apply:**

1. Kopiér med `git archive HEAD` — så følger `node_modules` og byggerester
   ikke med.
2. Tæl alle skrivemåder først (`grep -rhoiE` + `sort | uniq -c`). Der var
   21 af dem. En blind søg-og-erstat på den mest almindelige rammer under
   halvdelen.
3. Erstat efter et **ordnet** kort, ikke et alfabetisk: repo-URL'er og
   domæner før det generiske produktnavn, ellers brydes de.
4. Tjek for kollisioner, før du kører: her kunne "ID Connect" have ramt
   Microsofts eget "Entra Connection". Det gjorde det ikke — men det skal
   efterprøves, ikke antages.
5. Husk de fire kategorier, en navneerstatning **ikke** fanger:
   driftsidentitet (domæner, bøtter, serverstier), fremmed sporbarhed
   (sagsnumre, fremmede stier), binære Office-filer, og skærmbilleder af
   den gamle brugerflade.
6. Pas på det, der *består* tjekket, men bliver usandt: branding-skillen
   fik nyt navn og beskrev stadig den gamle farvepalet.

7. **Forkortelser tæller også.** 21. september 2026 fandt vi 400 rester af
   `IDC` i VagtsomIAM — i konstanter, data-id'er, MCP-værktøjsnavne,
   HTTP-headere og i18n-nøgler. Jørgen vil have dem væk, og han accepterer
   både omskrivning af historik og sletning + genoprettelse af repoet for
   at nå det. Ordlisten kan ikke rumme `IDC` alene: scriptet matcher som
   delstreng og ville flage hver `OIDC`. Skriv de præcise former
   (`idc-role-`, `X-IDC-`, `idc_tenant`, `IDC_SYSTEM` …).
8. Erstat med en **eksplicit ordliste**, ikke et bredt mønster. Et
   `IDC_` → `VAGTSOM_` ramte `OIDC_RESPONSE_TYPE_CODE`; `appRoleIdCache`
   er samme fælde. Brug negativt lookbehind på `[oO]` til de generelle.

9. **Navne kan være delt over et linjeskift** ("…ID" / "// Connect…" i en
   kommentar). En linjebaseret søgning ser dem ikke — seks stod i
   VagtsomIAM i ugevis, i alle 24 commits. Tjekket blev 21-09-2026 udvidet,
   så flerordstermer også søges på tværs af linjer i filer og historik.
   Ved oprydning: søg med `grep -Pz` over linjeskift, ikke kun `grep`.

10. **Kør aldrig tjekket parallelt med commit/push.** 21-09-2026 blev v8.13
    committet og pushet i samme værktøjsomgang som tjekket, der fejlede —
    fundet var "OpenID Connect", som indeholder firmanavnet som delstreng.
    Tjekket skal køre alene, og commit først efter at resultatet er læst.
    Jørgen besluttede samme dag, at "OpenID Connect" er en standard og skal
    kunne stå, hvor det handler om løsning og arkitektur. Tjekket har derfor
    en undtagelsesliste (`undtagelser.txt` ved siden af scriptet); udtryk
    dér fjernes fra teksten, før der søges.

Kør til sidst `-Historik`. Og kør tjekket mod udgangspunktet også — hvis
det ikke fejler dér, måler porten ikke noget. Husk at `refs/original/*` og
reflog skal ryddes efter en omskrivning, før `-Historik` bliver ren — og at
GitHubs `refs/pull/N/head` kun forsvinder ved at slette repoet.

Se [[project-vagtsom-iam]].
