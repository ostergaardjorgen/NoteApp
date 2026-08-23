---
name: office-paa-maskinen
description: "Word, Excel og PowerPoint er installeret og kan styres via COM — brug dem til at lave og efterprøve Office-filer"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 9fc2ba28-72ae-4174-b54e-b644ecd310cd
  modified: 2026-08-20T06:34:33.893Z
---

Microsoft Office 2016/365 er installeret i `C:\Program Files\Microsoft Office\root\Office16\` — WINWORD.EXE, EXCEL.EXE og POWERPNT.EXE. Alle tre svarer på COM-automatisering (`Word.Application`, `Excel.Application`, `PowerPoint.Application`, version 16.0). Efterprøvet 20-08-2026.

**Why:** Der er hverken LibreOffice, pandoc eller Poppler på maskinen, så den sædvanlige vej til at gengive og efterprøve et Office-dokument findes ikke. Office selv kan gøre det.

**How to apply:** Lav en .docx eller .xlsx som normalt, og efterprøv den derefter frem for at antage, at den ser rigtig ud:

```powershell
$w = New-Object -ComObject Word.Application
$w.Visible = $false; $w.DisplayAlerts = 0
$d = $w.Documents.Open($docx, [ref]$false, [ref]$true)
$d.ComputeStatistics(2)          # antal sider
$d.ExportAsFixedFormat($pdf, 17) # 17 = PDF
$d.Close([ref]$false); $w.Quit()
```

PDF'en gengives til billeder med Windows' egen motor (`Windows.Data.Pdf` via WinRT) — se `pdf2png.ps1`-mønstret. Så kan siderne læses med Read og faktisk ses efter.

Husk `$w.Visible = $false` og `DisplayAlerts = 0`, og luk altid med `Quit()` — en efterladt Word-proces holder filen låst.

Leveres en fil til brugeren, skal den ligge et sted, der kan nås — ikke i scratchpad-mappen. Se [[dansk-tegn-i-brugerflade]] for sprogkravene til indholdet.
