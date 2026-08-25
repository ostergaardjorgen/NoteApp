# ============================== PRØVERNE ==============================
#
# Koerer alle proever i tests\NoteApp.Tests.
#
#   powershell -File C:\NoteApp\scripts\proev.ps1
#
# Afslutter med kode 1, hvis noget fejler, saa den kan bruges som en gate
# foer en commit - paa samme maade som leverancetjekket.
#
# DE ROERER ALDRIG RIGTIGE DATA. Hver proeve peger NOTEAPP_DATA paa sin egen
# mappe under Temp. Det er efterproevet: proeverne blev koert med en
# foer/efter-optaelling af C:\AppNoter\Optagelser, og listen var uaendret.

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

Write-Host ''
Write-Host 'Koerer proeverne ...' -ForegroundColor Cyan
Write-Host ''

# MAALES FOER OG EFTER.
#
# Proeverne SKAL ikke roere den rigtige datamappe, og det er den slags, der
# skal efterprøves frem for antages. Gaar noget galt med tilsidesaettelsen af
# NOTEAPP_DATA, opdages det her - ikke naar en optagelse mangler.
$Data = if ($env:NOTEAPP_DATA) { $env:NOTEAPP_DATA } else { 'C:\AppNoter' }
$Optagelser = Join-Path $Data 'Optagelser'

$foer = @()
if (Test-Path $Optagelser) { $foer = @(Get-ChildItem $Optagelser -Directory).Name | Sort-Object }

$argumenter = @($Projekt, '--nologo')
if ($Detaljer) { $argumenter += @('-v', 'n') } else { $argumenter += @('-v', 'q') }
if ($Filter)   { $argumenter += @('--filter', $Filter) }

& dotnet test @argumenter
$kode = $LASTEXITCODE

$efter = @()
if (Test-Path $Optagelser) { $efter = @(Get-ChildItem $Optagelser -Directory).Name | Sort-Object }

$forskel = Compare-Object $foer $efter -ErrorAction SilentlyContinue

Write-Host ''

if ($forskel) {
    Write-Host 'ADVARSEL: den rigtige datamappe aendrede sig under proeverne!' -ForegroundColor Red
    $forskel | Format-Table -AutoSize
    Write-Host "  $Optagelser" -ForegroundColor Red
    exit 1
}

Write-Host "Datamappen er uroert ($($foer.Count) optagelser foer og efter)." -ForegroundColor DarkGray

if ($kode -eq 0) {
    Write-Host 'ALLE PROEVER BESTAAET.' -ForegroundColor Green
} else {
    Write-Host 'PROEVER FEJLEDE. Ret dem foer commit.' -ForegroundColor Red
}

exit $kode
