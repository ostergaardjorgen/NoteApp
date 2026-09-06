# Tredjepartskomponenter i HeyPia

Denne mappe følger med installationen og ligger i programmappen ved siden af
`HeyPia.exe`. Den indeholder licensteksterne for de komponenter, HeyPia
bruger, og den kreditering, nogle af licenserne kræver.

Licensteksterne i `tekster\` er ophavsmændenes egne, hentet fra deres
respektive projekter 03-09-2026. De er ikke omskrevet.

---

## Følger med i installationen

Disse ligger i programmappen og installeres sammen med HeyPia.

| Komponent | Licens | Ophav | Licenstekst |
|---|---|---|---|
| sherpa-onnx | Apache-2.0 | k2-fsa | `tekster\sherpa-onnx-Apache-2.0.txt` |
| ONNX Runtime | MIT | Microsoft | `tekster\onnxruntime-MIT.txt` |
| pyannote segmentation 3.0 | MIT | pyannote-projektet (Hervé Bredin m.fl.) | `tekster\pyannote-MIT.txt` |
| NVIDIA NeMo TitaNet | CC-BY-4.0 | NVIDIA Corporation | `tekster\nvidia-titanet-CC-BY-4.0.txt` |
| NAudio | MIT | Mark Heath | `tekster\naudio-MIT.txt` |
| SQLitePCLRaw | Apache-2.0 | SourceGear, LLC | `tekster\sqlitepclraw-Apache-2.0.txt` |
| PdfPig | Apache-2.0 | UglyToad og bidragydere | `tekster\pdfpig-Apache-2.0.txt` |
| SQLite | Offentligt domæne | D. Richard Hipp m.fl. | — se nedenfor |
| .NET og WPF | MIT | .NET Foundation og bidragydere | `tekster\dotnet-og-wpf-MIT.txt` |

## Hentes ned af programmet

Disse følger ikke med installationen. HeyPia henter dem, når brugeren beder om
det, og de lander i datamappen. Licensteksterne står her alligevel, så det kan
gøres op, hvad der kommer til at ligge på maskinen.

| Komponent | Licens | Ophav | Licenstekst |
|---|---|---|---|
| whisper.cpp | MIT | Georgi Gerganov og bidragydere | `tekster\whisper.cpp-MIT.txt` |
| Whisper-modellerne (ggml) | MIT | OpenAI | `tekster\whisper-modeller-OpenAI-MIT.txt` |
| Silero VAD | MIT | Silero Team | `tekster\silero-vad-MIT.txt` |

---

## Kreditering, der er et krav

### NVIDIA NeMo TitaNet — CC-BY-4.0

Modellen, der afgør, om to stykker tale kommer fra den samme stemme, er
NVIDIA NeMo TitaNet. Ophavet er **NVIDIA Corporation**. Modellen er stillet
til rådighed under Creative Commons Attribution 4.0 International
(CC-BY-4.0), som tillader kommerciel brug og videredistribution mod
kreditering.

Modellen er ikke ændret. Den er konverteret til ONNX-format af
sherpa-onnx-projektet og indgår uændret.

Den fulde licenstekst står i `tekster\nvidia-titanet-CC-BY-4.0.txt`.

### Apache-2.0 — sherpa-onnx og SQLitePCLRaw

Apache-2.0 kræver, at licensteksten følger med, og at ændringer oplyses.
**Ingen af de to komponenter er ændret.** De indgår, som de blev udgivet af
deres ophav.

### SQLite

SQLite er i det offentlige domæne. Ophavsmændene har fravalgt ophavsret, og
der er ingen licensbetingelser at videregive. Det er taget med her, fordi en
liste, der springer en komponent over, ikke kan bruges til at gøre noget op.

---

## Det, der ikke står her

**Mistral** er en tjeneste, ikke en licens. Der hentes ingen vægte, og der
installeres ingenting. Det, der gælder, er databehandleraftalen og vilkårene
— se Compliance-skærmen i programmet.

**Google Kalender og Google Tasks** bruges kun efter brugerens egen
forbindelse og godkendelse. Der redistribueres ingen Google-kode.

---

*Listen holdes i trit med det, der faktisk følger med: `scripts\tjek-licenser.ps1`
fejler, hvis en af filerne ovenfor mangler i den udgivne app eller i
installationspakken. Gaten køres af både `udgiv.ps1` og `byg-installer.ps1`.*
