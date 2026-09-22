---
name: project-vagtsom-iam
description: "Vagtsom IAM — udviklingen er 21. september 2026 vendt tilbage til det modne kodegrundlag i C:\\Vagtsom\\VagtsomIAM; vagtsom-iam-3 er parkeret"
metadata: 
  node_type: memory
  type: project
  originSessionId: 3be0811d-95c3-4771-8ac4-70e6d51317b8
  modified: 2026-09-21T15:51:31.792Z
---

**Aktiv linje fra 21. september 2026: `C:\Vagtsom\VagtsomIAM`**
(privat `ostergaardjorgen/VagtsomIAM`, arbejdet ligger på grenen `test`,
`main` findes også). Det er det modne produkt ført over og renset: ~186.000
linjer, 1.105 TS-filer, 57 sider, 410 API-ruter, 117 datamodeller, dansk og
engelsk UI, rapporter, attestering, leverandørstyring.

**Hvorfor vi vendte tilbage:** Jørgen mente den nye greenfield-linje var
drevet langt væk fra den oprindelige brugerflade. Optællingen gav ham ret:
`vagtsom-iam-3` var 1.824 linjer, 1 side, 11 ruter — omkring 1 % af
produktet. Den er parkeret som referencerepo med historikken i behold.
Gode idéer derfra: kontokoblinger (login ↔ person), rollemodellen i ADR
0003 og det nordiske designoplæg.

**Køreplanen står i README'ets "Vejen frem".** Fase 1–5 er færdige (21.
sep. 2026, v8.08): kodegrundlag renset, lokal containerstak, OpenBao som KMS,
Zitadel som identitetsserver, og Microsoft 365 som modul med kontrollen i
`getGraphClient`. Appen har driftstilstanden `vagtsom` (egen
identitetsserver, intet Microsoft), og `npm run verify:microsoft-optional`
holder den påstand ved lige. Næste er fase 6 (NAS som døgnkørende testmiljø).

**Kerneposten hedder `Person`** (tidligere `EntraUser`), også i databasen.
`EntraGroup`/`EntraGroupMembership` og feltet `entraObjectId` hedder med vilje
stadig Entra — de ER Microsoft. En database fra før skal have
`prisma/sql/omdoeb-entrauser-til-person.sql` kørt FØR `prisma db push`;
omvendt ville Prisma oprette en ny tabel og droppe den gamle.

**Efter fase 5 (v8.09–v8.14, 21. sep.):** fundamentet er uden Microsoft —
Microsoft 365 er et plugin, der slås TIL (opt-in), og Microsoft-login følger
modulet med et værn mod at lukke sig ude. Login via egen identitetsserver
afgøres af kundens katalog (aktiv person med samme e-mail); `ALLOWED_EMAILS`
er kun nødliste. Opsætningsguiden fører til egen identitetsserver først.
Applikationer kan tilsluttes med SSO (OpenID Connect/SAML) under
Applikationer: én organisation pr. kunde i identitetsserveren, ét projekt
pr. applikation med rollekontrol, og adgang synkroniseres fra funktions- og
profilroller (ikke Microsoft-spejlrækker). Lokalt: `scripts/registrer-vaert-lokalt.mjs`
skriver værts-kunden i platformdatabasen.

**v8.15:** mangler en person med SSO-rolle en konto, opretter synken den
og identitetsserveren sender en dansk invitation (mailfælden lokalt:
http://post.vagtsom.local:8025). SSO-synk starter med appen hvert 15. min
(instrumentation.ts) — ingen ekstern planlægning nødvendig.

**Test API'et lokalt uden browser:** `MCP_TEST_TOKEN` i `.env.local` +
`Authorization: Bearer` mod `http://localhost:3000` (kun localhost, aldrig
prod) — så undgås browserens JavaScript-dialoger, som Jørgen ikke vil have.

**Sådan køres det lokalt** (PowerShell, fra `C:\Vagtsom\VagtsomIAM`):
containerne først — `docker compose -f docker/compose.yml -f
docker/compose.dev.yml --env-file docker/.env up -d` — og derefter
`npm run dev`. Appen svarer på http://app.vagtsom.local:3000 (aldrig
localhost: værtsnavnene er en del af designet, fordi identitetsserveren
gemmer sin issuer-URL). Compose-projektet hedder `vagtsom` og deler intet
med den gamle pilots `vagtsom2-local`. Zitadel er på
http://iam.vagtsom.local:8080, KMS på 8200, mailfælde på 8025, Adminer på
8090. Nøgler og PAT ligger i `C:\Vagtsom\_kms\udvikling\`, uden for repoet.

**Login lokalt:** Demo Administrator-knappen, eller **Log ind med Vagtsom**
som `vagtsom-admin@iam.vagtsom.local` (kodeord i `docker/.env`). Fra v8.16
afgøres administrator af rollen `vagtsom-admin` på projektet "Vagtsom IAM" i
identitetsserveren (opsætningsscriptet tildeler den `IAM_ADMIN_USER`); login
opretter selv brugerrækken. `ALLOWED_EMAILS` er kun nødliste.

**Historikken blev omskrevet 21. september 2026**, og repoet slettet og
oprettet på ny for at fjerne GitHubs pull request-referencer. Alle
commit-id'er er nye. Sikkerhedskopier: `C:\Vagtsom\_historik\foer-omskrivning\`
(både den gamle og den rensede historik som bundle).

**Rollemodel besluttet 7. september 2026:** Platform admin (teknisk drift),
Super admin / Vagtsom Admin (på tværs af kunder), Tenant admin (én kunde),
KMS-myndighed holdes særskilt. Roller bestemmer både modulsynlighed og
adgang, og det håndhæves på serveren.

Øvrige valg der står: OpenBao som KMS, Zitadel som identitetsserver,
domænet vagtsom.com, intet offentligt før Jørgen siger til.

Se [[feedback-leverancetjek-som-port]] og [[feedback-powershell-hvor-staar-jeg]].
