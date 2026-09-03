<#
.SYNOPSIS
    Bygger HeyPia.msi ud fra den udgivne app-mappe.

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
$wxs = Join-Path $installer 'HeyPia.wxs'
$msi = Join-Path $installer 'HeyPia.msi'

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
#
# DER UDGIVES GENNEM udgiv.ps1 OG IKKE MED ET EGET "dotnet publish".
#
# Her stod en kopi af udgivelsen: luk appen, ryd obj\Release, publish. Den
# gjorde tre ting faerre end udgiv.ps1, og de tre kostede en installationsfil
# den 20-08-2026:
#
#   1. VERSIONEN blev ikke sat ud fra seneste commit. Pakken blev bygget som
#      1.0.33, selv om koden var 1.0.34 - og en pakke med en version, der
#      allerede er installeret, tilbyder at REPARERE frem for at opdatere.
#      Man tror, aendringen ikke virkede.
#   2. TALERGENKENDELSEN blev ikke kopieret med. Pakken var 61 MB for lille
#      og ville have installeret en app, der ikke kunne saette navne paa
#      talere - uden at sige det.
#   3. Genvejen blev ikke efterproevet.
#
# To scripts, der begge udgiver, driver fra hinanden. Nu er der eet.
if (-not $SpringUdgivelseOver) {
    Write-Host "Udgiver appen ..." -ForegroundColor Cyan

    & powershell -NoProfile -File (Join-Path $rod 'scripts\udgiv.ps1') -Rod $rod
    if ($LASTEXITCODE -ne 0) { throw "Udgivelsen fejlede." }
}

$exe = Join-Path $app 'HeyPia.exe'
if (-not (Test-Path $exe)) { throw "Fandt ikke $exe. Udgiv appen først." }

$version = (Get-Item $exe).VersionInfo.FileVersion

# -Recurse: talergenkendelsen ligger i en undermappe. Uden den taeller
# opgoerelsen 61 MB for lidt og ser rigtig ud, selv om noget mangler.
$filer = Get-ChildItem $app -Recurse -File
$mb = [math]::Round(($filer | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Host ""
Write-Host "Programfiler : $($filer.Count) filer, $mb MB"
Write-Host "Version      : $version"

# --- SPAERRE: LICENSNOTITSER OG TALERGENKENDELSE I APP-MAPPEN ------------
#
# Begge dele ligger i app\ og samles op af "..\app\**" i HeyPia.wxs. Mangler
# de, bygger pakken uden fejl og installerer en app, der stiltiende holder op
# med at kunne skille stemmer ad - eller som lover licensnotitser, der ikke er
# der.
#
# LISTEN STOD HER FOER, og en kopi af den stod i udgiv.ps1. To lister om det
# samme driver fra hinanden. Nu staar den ét sted.
& powershell -NoProfile -File (Join-Path $PSScriptRoot 'tjek-licenser.ps1') -Mappe $app
if ($LASTEXITCODE -ne 0) { throw "Licenstjekket paa app-mappen fejlede." }

# --- SPAERRE: WPF'S NATIVE DLL'ER SKAL VAERE DER -------------------------
#
# PublishSingleFile pakker ikke disse ind i exe'en - de skal ligge ved siden
# af. Mangler de, bygger og starter appen ikke: den doer med
# System.DllNotFoundException, foer der er noget at se paa skaermen.
#
# Det skete 19-08-2026 og gav tre installationsfiler, der ikke kunne starte.
# Ingen af dem fejlede ved bygningen - de var bare 5 MB for smaa.
$paakraevet = @(
    'PresentationNative_cor3.dll',
    'wpfgfx_cor3.dll',
    'vcruntime140_cor3.dll',
    'D3DCompiler_47_cor3.dll',
    'PenImc_cor3.dll'
)
$mangler = $paakraevet | Where-Object { -not (Test-Path (Join-Path $app $_)) }
if ($mangler) {
    throw ("Udgivelsen mangler $($mangler.Count) native DLL(er): $($mangler -join ', ').`n" +
           "Appen ville bygge uden fejl og doe ved opstart. Slet " +
           "src\NoteApp.Desktop\obj og koer igen.")
}


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

    & $wix build 'HeyPia.wxs' -ext WixToolset.UI.wixext -d "Version=$msiVersion" -o 'HeyPia.msi'
    if ($LASTEXITCODE -ne 0) { throw "wix build af MSI fejlede." }

    # Indpakningen: en setup.exe, der baerer MSI'en. Den beder selv om
    # administratorrettigheder, saa modtageren ikke skal vide, at en .msi skal
    # hoejreklikkes — og mange mailfiltre lukker en .exe igennem, hvor en .msi
    # bliver stoppet.
    Write-Host "Bygger setup.exe ..." -ForegroundColor Cyan
    & $wix extension add --global WixToolset.BootstrapperApplications.wixext/5.0.2
    & $wix build 'Bundle.wxs' -ext WixToolset.BootstrapperApplications.wixext -d "Version=$msiVersion" -o 'HeyPia-setup.exe'
    if ($LASTEXITCODE -ne 0) { throw "wix build af setup.exe fejlede." }
}
finally
{
    Pop-Location
}

$setup = Join-Path $installer 'HeyPia-setup.exe'

# --- EFTERPROEV, FREM FOR AT ANTAGE -------------------------------------
#
# wix build kan fejle paa bundle-trinnet uden at scriptet stopper, og saa
# bliver den GAMLE setup.exe liggende. Den ser rigtig ud: rigtigt navn,
# rigtig stoerrelse, samme mappe. Kun tidsstemplet og versionen afsloerer
# den - og dem kigger man ikke paa, naar der lige stod "Faerdig".
#
# Det skete to gange den 19-08-2026, og begge gange blev en fil paa vej ud
# ad doeren stoppet af en tilfaeldighed frem for af en kontrol.
if (-not (Test-Path $setup)) { throw "HeyPia-setup.exe blev ikke bygget." }

$setupVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup).FileVersion
if (($setupVersion -split '\.')[0..2] -join '.' -ne $msiVersion) {
    throw ("HeyPia-setup.exe er version $setupVersion, men der blev bygget $msiVersion. " +
           "Bundle-trinnet er fejlet, og den gamle fil ligger der stadig. Koer scriptet igen.")
}

# --- SPAERRE: ER DET OGSAA I PAKKEN? -------------------------------------
#
# App-mappen blev kontrolleret ovenfor. Det er IKKE det samme spoergsmaal.
# "..\app\**" i HeyPia.wxs samler op, men en Exclude-regel, en tom mappe
# eller en fil, wix springer over, ville give en pakke, der mangler noget -
# uden at bygningen fejler.
#
# Her laeses pakkens eget fil-katalog, altsaa noejagtig den liste,
# installationen kommer til at laegge paa disken.
& powershell -NoProfile -File (Join-Path $PSScriptRoot 'tjek-licenser.ps1') -Msi $msi
if ($LASTEXITCODE -ne 0) { throw "Licenstjekket paa den byggede pakke fejlede." }

Write-Host ""
Write-Host "Færdig." -ForegroundColor Green
foreach ($f in @($setup, $msi)) {
    if (Test-Path $f) {
        Write-Host ("  {0,-22} {1,7:0.0} MB" -f (Split-Path $f -Leaf), ((Get-Item $f).Length / 1MB))
    }
}
Write-Host ""
Write-Host "Send HeyPia-setup.exe til den, der skal installere. Den beder selv om" -ForegroundColor DarkGray
Write-Host "administratorrettigheder og installerer MSI'en." -ForegroundColor DarkGray
Write-Host "Afinstallation fjerner kun programmet — datamappen bliver liggende." -ForegroundColor DarkGray
