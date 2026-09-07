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


function Rettigheder_foerst
{
    <#
    .SYNOPSIS
        Faar installationsvinduet til at bede om administratorrettigheder med
        det samme - saa det ikke ender bagerst.

    .DESCRIPTION
        VINDUET LAA BAGERST, MENS DER BLEV INSTALLERET.

        Bundtet koerer som «asInvoker». Naar man trykker Installer, starter det
        en proces MED rettigheder, Windows viser sin godkendelse - og bagefter
        maa det oprindelige vindue ikke laengere tage forgrunden. Det er
        Windows' egen regel om, hvem der maa komme frem. Vinduet havner
        bagerst, mens kopieringen sker i det skjulte.

        TO VEJE ER PROEVET OG MAALT 07-09-2026:

          1. HexExtendedStyle="8" (WS_EX_TOPMOST) paa <Window> i temaet.
             Resultat: wixstdba oprettede slet ikke noget vindue - kun
             WixBurnMessageWindow var tilbage, og der stod intet i loggen.

          2. Rette manifestet i den FAERDIGE setup.exe med detach, mt.exe og
             reattach. Resultat: «wix burn extract» kunne ikke laese
             nyttelasten bagefter. mt.exe bygger ressourcerne om, filen
             skifter stoerrelse, og de henvisninger, reattach skriver, passer
             ikke laengere. Bundtet var i stykker.

        Den her vej retter STUBBEN, foer bundtet bygges. Saa bygges hele filen
        oven paa den rettede motor, og der er ingenting at komme bagefter og
        rette i.

        MOTOREN ER WiX' EGEN FIL i brugerens profil. Originalen laegges ved
        siden af som .asinvoker foerste gang, saa den kan lægges tilbage. En
        opdatering af WiX nulstiller den - og saa saetter scriptet den bare
        igen ved naeste byg.
    #>
    param([Parameter(Mandatory)] [string] $Motor)

    if (-not (Test-Path $Motor)) { return $false }

    $bytes = [IO.File]::ReadAllBytes($Motor)
    $tekst = [Text.Encoding]::UTF8.GetString($bytes)

    if ($tekst -match 'requireAdministrator') { return $true }

    $mt = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter 'mt.exe' `
              -ErrorAction SilentlyContinue |
          Where-Object { $_.FullName -like '*\x64\*' } |
          Sort-Object FullName -Descending |
          Select-Object -First 1

    if (-not $mt)
    {
        Write-Host "  mt.exe fra Windows SDK mangler - installationsvinduet kan lande bagerst." -ForegroundColor Yellow
        return $false
    }

    $sikkerhed = "$Motor.asinvoker"
    if (-not (Test-Path $sikkerhed)) { Copy-Item $Motor $sikkerhed }

    $manifest = Join-Path $env:TEMP 'heypia-burn.manifest'

    try
    {
        & $mt.FullName "-inputresource:$Motor;#1" "-out:$manifest" -nologo | Out-Null
        if (-not (Test-Path $manifest)) { throw "motoren havde intet manifest." }

        $m = Get-Content $manifest -Raw
        if ($m -notmatch 'asInvoker') { throw "manifestet ser ikke ud som ventet." }

        Set-Content $manifest $m.Replace('level="asInvoker"', 'level="requireAdministrator"') -Encoding UTF8

        & $mt.FullName -manifest $manifest "-outputresource:$Motor;#1" -nologo | Out-Null

        $nu = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($Motor))
        if ($nu -notmatch 'requireAdministrator') { throw "ordet kom ikke med." }

        return $true
    }
    catch
    {
        Write-Host "  Kunne ikke rette motoren: $($_.Exception.Message)" -ForegroundColor Yellow

        # Den oprindelige laegges tilbage. En halvt rettet motor er vaerre end
        # en, der bare virker som foer.
        if (Test-Path $sikkerhed) { Copy-Item $sikkerhed $Motor -Force }

        return $false
    }
    finally
    {
        Remove-Item $manifest -ErrorAction SilentlyContinue
    }
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

    # ============ -arch x64: HVOR PROGRAMMET LANDER ============
    #
    # Uden den bygger wix en 32-bit pakke, og saa peger
    # ProgramFiles6432Folder paa C:\Program Files (x86) - ogsaa naar filerne
    # indeni er win-x64. Maalt paa maskinen 07-09-2026: 26 filer, 240,4 MB i
    # (x86), mens installationsteksten sagde noget andet.
    #
    # SKIFTET KOSTER EEN OPGRADERING. En 64-bit pakke er en anden pakke end
    # den 32-bit, der ligger; den gamle skal afinstalleres foerst, ellers kan
    # der staa to poster under Tilfoej/fjern. Derefter opgraderer den som
    # foer. Det blev gjort, mens der var to maskiner i verden.
    & $wix build 'HeyPia.wxs' -arch x64 -ext WixToolset.UI.wixext -d "Version=$msiVersion" -o 'HeyPia.msi'
    if ($LASTEXITCODE -ne 0) { throw "wix build af MSI fejlede." }

    # ============ MICROSOFTS C++-KOMPONENT SKAL MED I BUNDTET ============
    #
    # Whisper kan ikke starte uden den, og en frisk Windows har den ikke. Se
    # Bundle.wxs. Filen hentes fra Microsofts egen adresse og holdes uden for
    # git - den er 25 MB og er ikke vores.
    $cpp = Join-Path $installer 'vc_redist.x64.exe'

    if (-not (Test-Path $cpp))
    {
        Write-Host "Henter Microsofts C++-komponent ..." -ForegroundColor Cyan

        $kilde = 'https://aka.ms/vs/17/release/vc_redist.x64.exe'

        try
        {
            Invoke-WebRequest -Uri $kilde -OutFile $cpp -UseBasicParsing
        }
        catch
        {
            throw ("Kunne ikke hente $kilde - $($_.Exception.Message). " +
                   "Hent filen i haanden og laeg den i installer\vc_redist.x64.exe.")
        }
    }

    $cppMb = [math]::Round((Get-Item $cpp).Length / 1MB, 1)
    Write-Host "  C++-komponenten: $cppMb MB" -ForegroundColor DarkGray

    # Indpakningen: en setup.exe, der baerer MSI'en. Den beder selv om
    # administratorrettigheder, saa modtageren ikke skal vide, at en .msi skal
    # hoejreklikkes — og mange mailfiltre lukker en .exe igennem, hvor en .msi
    # bliver stoppet.
    Write-Host "Bygger setup.exe ..." -ForegroundColor Cyan

    # Vinduet skal staa forrest, mens der installeres - se funktionen.
    $burn = Join-Path $env:USERPROFILE '.dotnet\tools\.store\wix\5.0.2\wix\5.0.2\tools\net6.0\any\x86\burn.exe'

    if (Rettigheder_foerst -Motor $burn)
    {
        Write-Host "  Installationsvinduet beder om rettigheder foerst." -ForegroundColor DarkGray
    }

    & $wix extension add --global WixToolset.BootstrapperApplications.wixext/5.0.2
    & $wix extension add --global WixToolset.Util.wixext/5.0.2
    & $wix build 'Bundle.wxs' -ext WixToolset.BootstrapperApplications.wixext -ext WixToolset.Util.wixext -d "Version=$msiVersion" -o 'HeyPia-setup.exe'
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
