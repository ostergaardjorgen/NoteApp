# ============================== PRØVERNE ==============================
#
# Koerer alle proever i tests\NoteApp.Tests.
#
#   powershell -File C:\NoteApp\scripts\proev.ps1
#
# Afslutter med kode 1, hvis noget fejler, saa den kan bruges som en gate
# foer en commit - paa samme maade som leverancetjekket.
#
# ============ PROEVERNE MAA ALDRIG ROERE RIGTIGE DATA ============
#
# Scriptet saetter NOTEAPP_DATA til en tom mappe under Temp, FOER proeverne
# starter. Saa kan selv en proeve, der har glemt sin proevemappe, ikke naa
# C:\AppNoter.
#
# DET ER IKKE OVERFLOEDIGT. 25-08-2026 skrev proeverne i den rigtige
# datamappe: seks proever paa "sidst hentet" stod uden proevemappe, og da den
# tekst blev oversat, gik de gennem Sprog - som lagde sprogfilerne ud. De
# roerte ingen data, da de blev skrevet; det gjorde de, da noget UNDER dem
# blev lavet om.
#
# Et vaern i scriptet virker ogsaa for den proeve, ingen har skrevet endnu.

param(
    [switch]$Detaljer,
    [string]$Filter = ''
)

$ErrorActionPreference = 'Stop'
$Rod = Split-Path -Parent $PSScriptRoot
$Projekt = Join-Path $Rod 'tests\NoteApp.Tests\NoteApp.Tests.csproj'

if (-not (Test-Path $Projekt)) {
    Write-Host "FANDT IKKE proeveprojektet: $Projekt" -ForegroundColor Red
    exit 1
}

# Den rigtige datamappe - den, der SKAL vaere uroert bagefter.
$Rigtig = if ($env:NOTEAPP_DATA) { $env:NOTEAPP_DATA } else { 'C:\AppNoter' }

function Aftryk($mappe) {
    if (-not (Test-Path $mappe)) { return @() }

    Get-ChildItem $mappe -Recurse -File -Force -ErrorAction SilentlyContinue |
        ForEach-Object { '{0}|{1}|{2:o}' -f $_.FullName, $_.Length, $_.LastWriteTimeUtc } |
        Sort-Object
}

Write-Host ''
Write-Host 'Koerer proeverne ...' -ForegroundColor Cyan
Write-Host ''

# HELE MAPPEN MAALES, ikke kun optagelserne. Foerste udgave taelte mapper
# under Optagelser og saa derfor ikke, at sprogfilerne blev skrevet.
$foer = Aftryk $Rigtig

# Proevernes egen datamappe. Den ryddes bagefter.
$Sandkasse = Join-Path ([IO.Path]::GetTempPath()) ("heypia-proev-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $Sandkasse | Out-Null

$gemt = $env:NOTEAPP_DATA
$env:NOTEAPP_DATA = $Sandkasse

try {
    $argumenter = @($Projekt, '--nologo')
    if ($Detaljer) { $argumenter += @('-v', 'n') } else { $argumenter += @('-v', 'q') }
    if ($Filter)   { $argumenter += @('--filter', $Filter) }

    & dotnet test @argumenter
    $kode = $LASTEXITCODE
}
finally {
    $env:NOTEAPP_DATA = $gemt
    try { Remove-Item $Sandkasse -Recurse -Force -ErrorAction SilentlyContinue } catch {}
}

$efter = Aftryk $Rigtig
$forskel = Compare-Object $foer $efter -ErrorAction SilentlyContinue

Write-Host ''

if ($forskel) {
    Write-Host 'ADVARSEL: den rigtige datamappe aendrede sig under proeverne!' -ForegroundColor Red
    Write-Host "  $Rigtig" -ForegroundColor Red
    Write-Host ''

    $forskel | Select-Object -First 12 | ForEach-Object {
        $tegn = if ($_.SideIndicator -eq '=>') { 'NY/AENDRET' } else { 'VAEK      ' }
        Write-Host ("  {0}  {1}" -f $tegn, ($_.InputObject -split '\|')[0]) -ForegroundColor Red
    }

    if ($forskel.Count -gt 12) { Write-Host "  ... og $($forskel.Count - 12) mere" -ForegroundColor Red }

    exit 1
}

Write-Host "Datamappen er uroert ($($foer.Count) filer foer og efter)." -ForegroundColor DarkGray

if ($kode -eq 0) {
    Write-Host 'ALLE PROEVER BESTAAET.' -ForegroundColor Green
} else {
    Write-Host 'PROEVER FEJLEDE. Ret dem foer commit.' -ForegroundColor Red
}

exit $kode
