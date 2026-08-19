<#
.SYNOPSIS
    Sætter versionen, bygger og lægger appen dér, hvor skrivebordsgenvejen peger.

.DESCRIPTION
    Scriptet findes, fordi to ting drev fra hinanden i praksis:

    1. Genvejen på skrivebordet peger på C:\NoteApp\app\NoteApp.exe. Byggede
       jeg kun til bin\Release, blev genvejen ved med at åbne en gammel udgave
       — uden at noget sagde det. Det skete med en udgave, der stadig havde
       transskriptions-loopet i sig.

    2. Versionen i csproj stod på 0.21, mens commits var nået til v0.39. Appen
       skrev altså et forkert versionsnummer i sidebjælken, og det er præcis
       den slags stille fejl, der gør, at man ikke kan stole på resten.

    Begge dele er nu ét skridt, der ikke kan gøres halvt.

    Versionen læses som standard af den seneste commit ("vX.Y.Z: ..."), så
    nummeret i appen ikke kan komme ud af trit med historikken.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\udgiv.ps1

.EXAMPLE
    powershell -File C:\NoteApp\scripts\udgiv.ps1 -Version 1.0.2
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $Rod = 'C:\NoteApp',
    [switch] $SpringByggeOver
)

$ErrorActionPreference = 'Stop'

$csproj  = Join-Path $Rod 'src\NoteApp.Desktop\NoteApp.Desktop.csproj'
$udgivTil = Join-Path $Rod 'app'

if (-not (Test-Path $csproj)) { throw "Finder ikke projektet: $csproj" }

# --- Versionen -------------------------------------------------------------
if (-not $Version) {
    Push-Location $Rod
    try { $besked = git log -1 --pretty=%s 2>$null } finally { Pop-Location }

    # TRE LED: vX.Y.Z. Konventionen var to cifre - v0.99, v1.00, v1.01 - og
    # den holdt indtil 1.0. Derefter dropper .NET det foranstillede nul, saa
    # v1.01 blev til filversion 1.1.0.0, og skaermen sagde noget andet end
    # commiten. Et versionsnummer, der ikke passer med historikken, kan man
    # ikke bruge til at afgoere, hvad der koerer.
    if ($besked -match '^v(\d+\.\d+\.\d+)') {
        $Version = $Matches[1]
        Write-Host "Version fra seneste commit: v$Version"
    }
    elseif ($besked -match '^v(\d+\.\d+)') {
        # De gamle to-leddede numre skal stadig kunne laeses, saa en aeldre
        # commit ikke stopper en udgivelse.
        $Version = $Matches[1]
        Write-Host "Version fra seneste commit: v$Version (gammelt format med to led)"
    }
    else {
        throw "Kunne ikke læse versionen af seneste commit ('$besked'). Angiv -Version, fx 1.0.2."
    }
}

$fuld = if ($Version -match '^\d+\.\d+$') { "$Version.0" } else { $Version }

# Advar, hvis .NET normaliserer nummeret vaek fra det, der blev bedt om.
# Foranstillede nuller forsvinder - 1.01 bliver til 1.1 - og saa passer
# skaermen ikke med commiten.
$normaliseret = ([version]$fuld).ToString(3)
if ($normaliseret -ne $fuld) {
    Write-Warning "Versionen $fuld normaliseres til $normaliseret. Undgaa foranstillede nuller - skriv fx 1.0.1, ikke 1.01."
}

$tekst = [IO.File]::ReadAllText($csproj, [Text.Encoding]::UTF8)
$nu = if ($tekst -match '<Version>([^<]+)</Version>') { $Matches[1] } else { '(ingen)' }

# SPAERRE MOD UTILSIGTET SPRING I MAJOR ELLER MINOR.
#
# Aftalen er tredje led: 1.0.1, 1.0.2, 1.0.3. En commit skrevet "v1.2.0:"
# i stedet for "v1.0.2:" slaar lige igennem uden en spaerre - appen kom til
# at hedde 1.2, og det passede hverken med aftalen eller med historikken.
#
# Der spaerres ikke for et spring, der ER meningen - der spoerges. Et
# bevidst skifte til 1.1.0 eller 2.0.0 skal stadig kunne lade sig goere.
if ($nu -match '^(\d+)\.(\d+)' -and $fuld -match '^(\d+)\.(\d+)') {
    $nuMM   = ($nu   -split '\.')[0..1] -join '.'
    $nyMM   = ($fuld -split '\.')[0..1] -join '.'
    if ($nuMM -ne $nyMM) {
        Write-Warning "Versionen springer fra $nuMM.x til $nyMM.x (fra $nu til $fuld)."
        Write-Warning "Aftalen er tredje led - fx $nuMM.2. Er springet med vilje?"
        $svar = Read-Host "Skriv JA for at fortsaette, eller Enter for at afbryde"
        if ($svar -ne 'JA') { throw "Afbrudt. Ret commit-beskeden til 'v$nuMM.<nummer>: ...' og koer igen." }
    }
}

if ($nu -ne $fuld) {
    $tekst = $tekst -replace '<Version>[^<]+</Version>', "<Version>$fuld</Version>"
    [IO.File]::WriteAllText($csproj, $tekst, (New-Object Text.UTF8Encoding $false))
    Write-Host "Version rettet: $nu -> $fuld"
}
else {
    Write-Host "Version er allerede $fuld"
}

if ($SpringByggeOver) { return }

# --- Byg og udgiv ----------------------------------------------------------
# Appen skal ikke koere, mens der skrives til den. Kun DESKTOP-processen
# stoppes; kommandolinjevaerktoejet kan koere et laengevarende job.
Get-Process -Name NoteApp -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like '*NoteApp\app\*' -or $_.Path -like '*NoteApp.Desktop*' } |
    ForEach-Object { Write-Host "Lukker koerende app (PID $($_.Id))"; $_ | Stop-Process }

Start-Sleep -Milliseconds 800

# --- Ryd foerst ------------------------------------------------------------
#
# Mappen toemmes, foer der udgives i den. Det lyder overfloedigt — dotnet
# publish skriver jo filerne — men det er det ikke:
#
# Skrives der oven i en exe, der lige har koert, kan Windows stadig holde fat i
# filen. Publiceringen melder saa "faerdig", mens en eller flere filer i
# virkeligheden ikke blev skiftet. Resultatet er en exe, der ser rigtig ud og
# har det rigtige tidsstempel, men falder med "DllNotFoundException" i WPF's
# native lag, saa snart den aabner et vindue. Det skete 14. august 2026, og
# fejlen peger ingen steder hen — man leder i sin egen kode efter en fejl, der
# ligger i pakken.
#
# En tom mappe kan ikke vaere halvt opdateret.
if (Test-Path $udgivTil) {
    try {
        Remove-Item (Join-Path $udgivTil '*') -Recurse -Force -ErrorAction Stop
        Write-Host "Ryddede $udgivTil"
    }
    catch {
        # Kan mappen ikke ryddes, koerer der stadig noget. Sig det frem for at
        # udgive oven i — det er praecis den situation, rydningen findes for.
        throw "Kunne ikke rydde $udgivTil — koerer appen stadig? ($($_.Exception.Message))"
    }
}

Write-Host "Bygger og udgiver til $udgivTil ..."


# RYD obj\Release FOER UDGIVELSEN.
#
# MSBuild husker i obj\, at de native DLL'er er kopieret. Ryddes output-
# mappen, men ikke obj\, springer den kopieringen over - og saa staar exe'en
# alene tilbage uden PresentationNative_cor3.dll og seks andre. Appen bygger
# uden fejl og doer ved opstart.
#
# "dotnet publish --no-incremental" findes ikke; det er en build-switch, og
# publish afviser den med MSB1001. Derfor slettes mappen i haanden.
$objRelease = Join-Path (Split-Path $csproj) 'obj\Release'
if (Test-Path $objRelease) {
    Remove-Item $objRelease -Recurse -Force -ErrorAction SilentlyContinue
}

& dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $udgivTil --nologo -v q | Out-Null

if ($LASTEXITCODE -ne 0) { throw "dotnet publish fejlede med kode $LASTEXITCODE" }

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
$mangler = $paakraevet | Where-Object { -not (Test-Path (Join-Path $udgivTil $_)) }
if ($mangler) {
    throw ("Udgivelsen mangler $($mangler.Count) native DLL(er): $($mangler -join ', ').`n" +
           "Appen ville bygge uden fejl og doe ved opstart. Slet " +
           "src\NoteApp.Desktop\obj og koer igen.")
}

# --- Efterproev ------------------------------------------------------------
# Et byg, der siger "faerdig" uden at filen er skiftet, er vaerre end et, der
# fejler. Derfor kontrolleres resultatet frem for at blive antaget.
$exe = Join-Path $udgivTil 'NoteApp.exe'
if (-not (Test-Path $exe)) { throw "NoteApp.exe blev ikke lagt i $udgivTil" }

$fil = Get-Item $exe
$ver = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion

Write-Host ""
Write-Host "Udgivet:" -ForegroundColor Green
Write-Host ("  {0}" -f $exe)
Write-Host ("  version {0}, {1:N1} MB, {2}" -f $ver, ($fil.Length/1MB), $fil.LastWriteTime)

if ($ver -notlike "$fuld*") {
    Write-Warning "Filens version ($ver) svarer ikke til den, der blev sat ($fuld). Byggede den overhovedet om?"
}

# --- Hvad koerer der EGENTLIG -----------------------------------------------
#
# Versionen kommer fra seneste commit-besked. Udgives der flere gange uden en
# commit imellem — hvilket er det normale under en arbejdsdag — faar alle
# build's det SAMME nummer. Aabner man saa appen og laeser "v0.54", ved man
# ikke, om det er formiddagens eller den nyeste.
#
# Derfor staar tidsstemplet altid, og der advares, naar der ligger uommitede
# aendringer: saa er nummeret ikke et svar paa "hvad kigger jeg paa".
$snavset = $false
try { $snavset = [bool](git -C $Rod status --porcelain 2>$null) } catch { }

Write-Host ""
Write-Host ("PRODUKTION: v{0} · bygget {1:dd-MM HH:mm}" -f $fuld, $fil.LastWriteTime) -ForegroundColor Cyan

if ($snavset) {
    Write-Host ("  Bemaerk: der er uommitede aendringer. Versionsnummeret staar stille," +
                " indtil der commites med en ny 'vX.Y.Z:'-besked — brug tidsstemplet til" +
                " at se, hvad der koerer.") -ForegroundColor Yellow
}

# Genvejen paa skrivebordet peger paa en FAST sti, saa den behoever ikke
# aendres. Men det skal kontrolleres, at den stadig goer det — flyttes mappen,
# aabner genvejen ingenting, og det opdages foerst naar man har brug for den.
$skrivebord = [Environment]::GetFolderPath('Desktop')
$lnk = Join-Path $skrivebord 'NoteApp.lnk'

if (Test-Path $lnk) {
    $sh = New-Object -ComObject WScript.Shell
    $maal = $sh.CreateShortcut($lnk).TargetPath

    if ($maal -eq $exe) {
        Write-Host "  skrivebordsgenvejen peger hertil — den er opdateret automatisk"
    }
    else {
        Write-Warning "Skrivebordsgenvejen peger på $maal, ikke på $exe. Den vil åbne en anden udgave."
    }
}
else {
    Write-Host "  (ingen skrivebordsgenvej fundet — appen startes fra $exe)"
}
