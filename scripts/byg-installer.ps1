<#
.SYNOPSIS
    Bygger NoteApp.msi ud fra den udgivne app-mappe.

.DESCRIPTION
    Rækkefølgen betyder noget: appen udgives FØRST, og installationspakken
    bygges derefter oven på præcis de filer, der ligger i app-mappen. Bygger
    man pakken uden at udgive først, pakker man gårsdagens program ned i en
    ny installationsfil — og det opdager man først på en anden maskine.

    Resultatet lander i installer\NoteApp.msi. Den kan sendes til en kollega,
    som dobbeltklikker: programmet lander i Program Files, får en genvej i
    Startmenuen og en post under Tilføj/fjern programmer.

    Afinstallation fjerner KUN programmet. Datamappen bliver liggende.

.PARAMETER SpringUdgivelseOver
    Byg pakken på det, der allerede ligger i app-mappen. Bruges kun, når man
    lige har udgivet i hånden.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\byg-installer.ps1
#>
[CmdletBinding()]
param(
    [switch] $SpringUdgivelseOver
)

$ErrorActionPreference = 'Stop'

$rod = Split-Path -Parent $PSScriptRoot
$app = Join-Path $rod 'app'
$installer = Join-Path $rod 'installer'
$wxs = Join-Path $installer 'NoteApp.wxs'
$msi = Join-Path $installer 'NoteApp.msi'

# wix ligger som dotnet-vaerktoej i brugerens profil og er ikke altid i PATH,
# naar scriptet koeres fra en ny session.
$wix = Join-Path $env:USERPROFILE '.dotnet\tools\wix.exe'
if (-not (Test-Path $wix)) {
    # PowerShell 5.1 har ingen ?. — der skal spoerges i to trin.
    $fundet = Get-Command wix -ErrorAction SilentlyContinue
    $wix = if ($fundet) { $fundet.Source } else { $null }
}
if (-not $wix) {
    throw "Fandt ikke wix. Installér det med: dotnet tool install --global wix"
}

# --- 1. Udgiv appen -------------------------------------------------------
if (-not $SpringUdgivelseOver) {
    Write-Host "Udgiver appen ..." -ForegroundColor Cyan
    $proj = Join-Path $rod 'src\NoteApp.Desktop\NoteApp.Desktop.csproj'

    # Luk en koerende udgave: en exe i brug kan ikke overskrives, og fejlen
    # ser ud som en byggefejl.
    Get-Process NoteApp -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    dotnet publish $proj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o $app --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Udgivelsen fejlede." }
}

$exe = Join-Path $app 'NoteApp.exe'
if (-not (Test-Path $exe)) { throw "Fandt ikke $exe. Udgiv appen først." }

$version = (Get-Item $exe).VersionInfo.FileVersion
$filer = Get-ChildItem $app -File
$mb = [math]::Round(($filer | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Host ""
Write-Host "Programfiler : $($filer.Count) filer, $mb MB"
Write-Host "Version      : $version"

# --- 2. Byg pakken --------------------------------------------------------
Write-Host ""
Write-Host "Bygger installationspakken ..." -ForegroundColor Cyan

# UI-udvidelsen giver den almindelige installationsdialog med mappevalg.
# Versionen bindes til WiX 5: version 6 og 7 kraever, at man accepterer
# Open Source Maintenance Fee-aftalen, og det er en beslutning, der skal
# traeffes bevidst — ikke noget et byggescript goer paa nogens vegne.
& $wix extension add --global WixToolset.UI.wixext/5.0.2

# Stier i .wxs-filen loeses fra ARBEJDSMAPPEN, ikke fra filens egen placering.
# Uden dette skift leder wix efter ikonet og vilkaarene et helt andet sted.
Push-Location $installer
try
{
    # Versionen laeses af den udgivne exe og sendes ind. MSI tillader kun tre
    # tal, saa revisionen klippes af — 0.18.0.0 bliver til 0.18.0.
    $msiVersion = ($version -split '\.')[0..2] -join '.'

    & $wix build 'NoteApp.wxs' -ext WixToolset.UI.wixext -d "Version=$msiVersion" -o 'NoteApp.msi'
    if ($LASTEXITCODE -ne 0) { throw "wix build af MSI fejlede." }

    # Indpakningen: en setup.exe, der baerer MSI'en. Den beder selv om
    # administratorrettigheder, saa modtageren ikke skal vide, at en .msi skal
    # hoejreklikkes — og mange mailfiltre lukker en .exe igennem, hvor en .msi
    # bliver stoppet.
    Write-Host "Bygger setup.exe ..." -ForegroundColor Cyan
    & $wix extension add --global WixToolset.BootstrapperApplications.wixext/5.0.2
    & $wix build 'Bundle.wxs' -ext WixToolset.BootstrapperApplications.wixext -d "Version=$msiVersion" -o 'NoteApp-setup.exe'
    if ($LASTEXITCODE -ne 0) { throw "wix build af setup.exe fejlede." }
}
finally
{
    Pop-Location
}

$setup = Join-Path $installer 'NoteApp-setup.exe'

Write-Host ""
Write-Host "Færdig." -ForegroundColor Green
foreach ($f in @($setup, $msi)) {
    if (Test-Path $f) {
        Write-Host ("  {0,-22} {1,7:0.0} MB" -f (Split-Path $f -Leaf), ((Get-Item $f).Length / 1MB))
    }
}
Write-Host ""
Write-Host "Send NoteApp-setup.exe til den, der skal installere. Den beder selv om" -ForegroundColor DarkGray
Write-Host "administratorrettigheder og installerer MSI'en." -ForegroundColor DarkGray
Write-Host "Afinstallation fjerner kun programmet — datamappen bliver liggende." -ForegroundColor DarkGray
