---
name: project-plane-dk
description: Plane DK - dansk fork af Plane Community paa Synology NAS; hvor koden, releasen og driften ligger
metadata:
  type: project
---

Plane DK er Joergens danske fork af Plane Community 1.4.2 med obligatorisk MFA.
Kode: `C:\ClaudeCode\plane-dk`, remote `ostergaardjorgen/plane-dk` (upstream `makeplane/plane`).
Drift: Synology DS923+ paa `192.168.1.155`, login `jorgen`, domaene `projekt.vagtsom.com`.

- Runtime paa NAS: `/volume1/docker/plane-dk` (root, indeholder `.env` med `MFA_ENCRYPTION_KEY`).
- Opdateringspakker: `/volume1/docker/plane-dk-update-<kort-sha>`.
- Backup: `/volume1/Plane_backups` (krypteret delt mappe).
- `scp` til NAS'en lander i `/volume1/Kameraer/`, ikke i `/var/services/homes/Jorgen` - SFTP-hjemmemappen peger paa den delte mappe.
- Images bygges kun ved udgivelse: enten i GitHub Actions (workflow_dispatch, artefakter i 1 dag) eller lokalt med `tools\build-release.ps1 -Version x.y.z` (Docker Desktop) til `C:\ClaudeCode\plane-dk-release\<version>`. Almindelige push koerer kun testene. Actions-kvoten (2.000 min/md) blev brugt op 19-09-2026, saa lokalt byg er foerstevalget. NAS'en har ingen GitHub-adgang.
- Skift af version sker ved at rette `PLANE_TAG` i `.env` til den fulde commit-SHA og koere `docker compose up -d`.

Se [[feedback-plane-dk-samlede-updates]].
