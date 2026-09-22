<#
.SYNOPSIS
    Laegger hukommelsen og skills fra kontinuitet/ paa plads i ~/.claude -
    paa en ny maskine eller efter et nyt login.

.DESCRIPTION
    Det modsatte af opdater-kontinuitet.ps1.

    DER OVERSKRIVES IKKE UDEN AT BEDE OM DET

    Ligger der allerede en fil i ~/.claude, og er den anderledes, er den
    maaske NYERE end kopien i git - rettet siden sidste push. En gendannelse
    hen over den ville rulle den tilbage i stilhed. Den springes derfor over
    og naevnes; -Overskriv tager kopien i git alligevel.

    DET, DER ALDRIG ER I GIT, KOMMER HERFRA IKKE MED

    forbudte-termer.txt skal hentes fra C:\NoteApp-backup\lokalt-ikke-i-git
    eller skrives igen i haanden. Scriptet siger til, hvis den mangler - uden
    den godkender leverancetjekket alt.

.PARAMETER Overskriv
    Skriv ogsaa hen over filer, der findes og er anderledes.

.PARAMETER Projektmappe
    Den mappe, Claude Code startes i. Hukommelsen ligger pr. mappe, og
    arbejdet her er koert fra C:\ClaudeCode. Startes der i C:\NoteApp, skal
    der staa det her.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\gendan-kontinuitet.ps1
#>
[CmdletBinding()]
param(
    [switch] $Overskriv,
    [string] $Projektmappe = 'C:\ClaudeCode'
)

$ErrorActionPreference = 'Stop'

$rod = Split-Path $PSScriptRoot -Parent
$kont = Join-Path $rod 'kontinuitet'

# Claude Code navngiver hukommelsens mappe efter arbejdsmappen: C:\ClaudeCode
# bliver til C--ClaudeCode.
$projektNavn = ($Projektmappe.TrimEnd('\') -replace '[:\\/]', '-')

$par = @(
    @{ Fra = Join-Path $kont 'hukommelse'
       Til = Join-Path $env:USERPROFILE ".claude\projects\$projektNavn\memory"
       Navn = 'hukommelse' }
    @{ Fra = Join-Path $kont 'skills'
       Til = Join-Path $env:USERPROFILE '.claude\skills'
       Navn = 'skills' }
)

$lagt = 0
$sprunget = 0

foreach ($p in $par) {
    if (-not (Test-Path $p.Fra)) { continue }

    foreach ($k in @(Get-ChildItem $p.Fra -Recurse -File)) {
        $rel = $k.FullName.Substring($p.Fra.Length + 1)
        $maal = Join-Path $p.Til $rel

        if (Test-Path $maal) {
            if ((Get-FileHash $maal).Hash -eq (Get-FileHash $k.FullName).Hash) { continue }

            if (-not $Overskriv) {
                $sprunget++
                Write-Host "  $($p.Navn)\$rel  findes og er anderledes - roeres ikke" -ForegroundColor Yellow
                continue
            }
        }

        New-Item -ItemType Directory -Force (Split-Path $maal) | Out-Null
        Copy-Item $k.FullName $maal -Force
        $lagt++
        Write-Host "  $($p.Navn)\$rel"
    }
}

Write-Host ''
Write-Host "  $lagt fil(er) lagt paa plads, $sprunget sprunget over." -ForegroundColor Green
if ($sprunget -gt 0) { Write-Host '  Koer med -Overskriv, hvis kopien i git skal vinde.' }

$termer = Join-Path $env:USERPROFILE '.claude\skills\leverancetjek\forbudte-termer.txt'
if (-not (Test-Path $termer)) {
    Write-Host ''
    Write-Host '  forbudte-termer.txt MANGLER. Uden den godkender leverancetjekket alt.' -ForegroundColor Red
    Write-Host '  Hent den fra C:\NoteApp-backup\lokalt-ikke-i-git\ og laeg den i:' -ForegroundColor Red
    Write-Host "  $termer" -ForegroundColor Red
}
