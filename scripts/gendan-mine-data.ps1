<#
.SYNOPSIS
    Gendanner NoteApp-data fra et backup-arkiv.

.DESCRIPTION
    En backup, man aldrig har prøvet at gendanne fra, er et gæt — ikke en
    backup. Kør dette script mindst én gang med -Proeve, så du ved, at
    arkivet duer, inden du får brug for det.

.PARAMETER Proeve
    Pakker ud i en midlertidig mappe og viser hvad der ville blive gendannet,
    uden at røre dine nuværende data. Brug altid denne først.

.EXAMPLE
    .\gendan-mine-data.ps1 -Proeve

.EXAMPLE
    .\gendan-mine-data.ps1 -Arkiv 'C:\Users\oster\NoteApp-backup\noteapp-data_2026-08-10_0900.zip'
#>
[CmdletBinding()]
param(
    # Udelades det, bruges det nyeste arkiv i standard-backupmappen.
    [string] $Arkiv,

    [string] $BackupMappe = (Join-Path $env:USERPROFILE 'NoteApp-backup'),

    [switch] $Proeve
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $Arkiv) {
    $nyeste = Get-ChildItem $BackupMappe -Filter 'noteapp-data_*.zip' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $nyeste) { throw "Ingen arkiver fundet i $BackupMappe" }
    $Arkiv = $nyeste.FullName
}
if (-not (Test-Path $Arkiv)) { throw "Arkivet findes ikke: $Arkiv" }

# Samme opslag som appen: NOTEAPP_DATA, saa pegefilen, saa standarden.
# En gendannelse til den forkerte mappe ser ud til at lykkes og efterlader
# brugeren uden sine data dér, hvor appen leder.
. (Join-Path $PSScriptRoot 'lib-datagraense.ps1')
$mål = Get-NoteAppDataRod

Write-Host "Arkiv : $Arkiv"
Write-Host "Mål   : $mål"
Write-Host ""

# --- Kig i arkivet uden at pakke ud ---------------------------------------
$zip = [IO.Compression.ZipFile]::OpenRead($Arkiv)
try {
    $poster = $zip.Entries | Where-Object { $_.Length -gt 0 }
    $samlet = ($poster | Measure-Object Length -Sum).Sum
    Write-Host "Indhold: $($poster.Count) filer, $([math]::Round($samlet/1MB,1)) MB udpakket" -ForegroundColor Green

    $grupper = $poster | Group-Object { ($_.FullName -split '[/\\]')[0] } | Sort-Object Count -Descending
    foreach ($g in $grupper) {
        Write-Host ("  {0,-24} {1} filer" -f $g.Name, $g.Count)
    }
} finally {
    $zip.Dispose()
}

if ($Proeve) {
    $midlertidig = Join-Path ([IO.Path]::GetTempPath()) "noteapp-proeve-$(Get-Random)"
    Expand-Archive -Path $Arkiv -DestinationPath $midlertidig -Force

    $udpakket = Get-ChildItem $midlertidig -Recurse -File
    Write-Host ""
    Write-Host "PRØVE: pakket ud til en midlertidig mappe. Dine nuværende data er urørt." -ForegroundColor Cyan
    Write-Host "  $($udpakket.Count) filer udpakket uden fejl." -ForegroundColor Green

    $db = Join-Path $midlertidig 'learning.db'
    if (Test-Path $db) {
        $hoved = [IO.File]::ReadAllBytes($db)[0..14]
        $tekst = [Text.Encoding]::ASCII.GetString($hoved)
        if ($tekst.StartsWith('SQLite format 3')) {
            Write-Host "  learning.db er en gyldig SQLite-database." -ForegroundColor Green
        } else {
            Write-Host "  ADVARSEL: learning.db ser ikke ud som en SQLite-database." -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "Ligger her indtil du sletter den: $midlertidig" -ForegroundColor DarkGray
    return
}

# --- Rigtig gendannelse ---------------------------------------------------
Write-Host ""
Write-Host "Dette OVERSKRIVER filer i $mål med indholdet fra arkivet." -ForegroundColor Yellow
Write-Host "Filer der kun findes lokalt, bevares." -ForegroundColor Yellow
$svar = Read-Host "Skriv GENDAN for at fortsætte"
if ($svar -cne 'GENDAN') {
    Write-Host "Afbrudt. Intet er ændret." -ForegroundColor Green
    return
}

# Tag et sikkerhedsnet foerst. At gendanne oven i noget nyere uden en vej
# tilbage er den maade man mister data paa, mens man proever at redde dem.
if (Test-Path $mål) {
    $sikkerhed = Join-Path $BackupMappe "foer-gendannelse_$(Get-Date -Format 'yyyy-MM-dd_HHmmss').zip"
    Compress-Archive -Path (Join-Path $mål '*') -DestinationPath $sikkerhed -Force
    Write-Host "Nuværende data gemt som: $sikkerhed" -ForegroundColor DarkGray
}

New-Item -ItemType Directory -Force $mål | Out-Null
Expand-Archive -Path $Arkiv -DestinationPath $mål -Force

$nu = (Get-ChildItem $mål -Recurse -File).Count
Write-Host ""
Write-Host "Gendannet. $nu filer i $mål" -ForegroundColor Green
