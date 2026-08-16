<#
.SYNOPSIS
Kører det samme møde gennem begge europæiske modeller og bedømmer dem mod facit.

.DESCRIPTION
Findes, fordi to modeller kun kan sammenlignes, hvis de får præcis det samme:
den samme udskrift, den samme skabelon, det samme facit og den samme bedømmelse.
Køres de i hånden hver for sig, ender man med at sammenligne to kørsler, der
adskilte sig ved noget, man ikke skrev ned.

Facit er det referat, Claude lavede af den samme udskrift. Claude er ikke en
kandidat til produktet — den er målestokken.

BEMÆRK: kommandoen sender mødeudskriften til Mistral i Frankrig. Det er hele
pointen med målingen, men det skal være et bevidst tryk.

.EXAMPLE
powershell -File scripts\maal-sky.ps1 -Moede C:\AppNoter\Optagelser\2026-08-13_09-57
#>
param(
    [Parameter(Mandatory)] [string] $Moede,
    [string] $Facit,
    [string[]] $Modeller = @('mistral-medium', 'mistral-large')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Moede)) { throw "Findes ikke: $Moede" }

if (-not $Facit) { $Facit = Join-Path $Moede 'facit-claude.txt' }
if (-not (Test-Path $Facit)) { throw "Facit findes ikke: $Facit" }

$rod = Split-Path -Parent $PSScriptRoot
$cli = Join-Path $rod 'src\NoteApp.Tools\NoteApp.Tools.csproj'

Write-Host ""
Write-Host "MAALING — europaeiske modeller mod Claude-facit" -ForegroundColor Cyan
Write-Host ("  moede : {0}" -f $Moede)
Write-Host ("  facit : {0} ({1:N0} tegn)" -f $Facit, (Get-Item $Facit).Length)
Write-Host ""

$udkastMappe = Join-Path $Moede 'udkast'
$resultater = @()

foreach ($m in $Modeller) {
    Write-Host ("=== {0} ===" -f $m) -ForegroundColor Yellow

    # Hvilke udkast fandtes FOER koerslen? Det nye er det, der er kommet til.
    # At tage "det nyeste" ville plukke et gammelt udkast, hvis koerslen fejler.
    $foer = @()
    if (Test-Path $udkastMappe) { $foer = Get-ChildItem $udkastMappe -Filter *.md | ForEach-Object { $_.FullName } }

    dotnet run --project $cli -c Release --no-build -- sky referat $Moede $m
    if ($LASTEXITCODE -ne 0) { Write-Host "  Koerslen fejlede — springes over." -ForegroundColor Red; continue }

    $nye = Get-ChildItem $udkastMappe -Filter *.md | Where-Object { $_.FullName -notin $foer }
    if (-not $nye) { Write-Host "  Der kom intet nyt udkast." -ForegroundColor Red; continue }

    $udkast = ($nye | Sort-Object LastWriteTime -Descending)[0].FullName
    Write-Host ""
    Write-Host ("  udkast: {0}" -f $udkast)

    & (Join-Path $PSScriptRoot 'bedoem-referat2.ps1') -Referat $udkast -Facit $Facit

    $resultater += [pscustomobject]@{ Model = $m; Udkast = $udkast }
    Write-Host ""
}

Write-Host "KOERT" -ForegroundColor Cyan
foreach ($r in $resultater) { Write-Host ("  {0,-16} {1}" -f $r.Model, (Split-Path -Leaf $r.Udkast)) }
Write-Host ""
Write-Host "Sammenlign med den lokale model ved at bedoemme et af de aeldre udkast" -ForegroundColor DarkGray
Write-Host "i den samme mappe mod det samme facit." -ForegroundColor DarkGray
Write-Host ""
