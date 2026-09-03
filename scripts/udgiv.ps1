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
    # DER KIGGES TILBAGE, IKKE KUN PAA DEN SIDSTE.
    #
    # Ikke alle commits er udgivelser. En rettet proeve, en note i doc eller
    # en oprydning har ingen "vX.Y.ZZ:" i sig - og saa stod scriptet af med
    # "kunne ikke laese versionen", selv om der laa en udgivelse to commits
    # laengere tilbage. Set 26-08-2026, hvor en rettelse ikke kunne udgives,
    # fordi den var fulgt af en proeverettelse.
    #
    # Der ses paa de tyve seneste. Findes der ingen version i dem, er det en
    # rigtig fejl - saa er der ikke udgivet laenge, og nummeret skal saettes i
    # haanden.
    $beskeder = git log -20 --format=%s 2>$null

    foreach ($besked in $beskeder) {
        # Tre led: v1.1.25. Det er formen, og den staar ogsaa i csproj.
        if ($besked -match '^v(\d+\.\d+\.\d+)\s*:') {
            $Version = $Matches[1]
            Write-Host "Version fra commit: v$Version"
            break
        }

        # To led: v0.39. Gammelt format, laeses stadig.
        if ($besked -match '^v(\d+\.\d+)\s*:') {
            $Version = $Matches[1]
            Write-Host "Version fra commit: v$Version (gammelt format med to led)"
            break
        }
    }

    if (-not $Version) {
        throw "Ingen af de 20 seneste commits har en 'vX.Y.ZZ:'-besked. Angiv -Version, fx 1.1.26."
    }
}

$fuld = if ($Version -match '^\d+\.\d+$') { "$Version.0" } else { $Version }

# 99 ER HOEJESTE TAL I HVERT LED.
#
# Tredje led loeb til 108, foer det blev opdaget. Det er ikke bare grimt:
# 1.0.108 sorterer FOER 1.0.99 i alt, der sammenligner tekst - og det er det
# meste. Udgivelserne kom i forkert raekkefoelge overalt, hvor de blev stillet
# op. Naar tredje led naar 99, ruller andet led.
if ($fuld -match '^\d+\.\d+\.(\d+)$' -and [int]$Matches[1] -gt 99) {
    throw ("Tredje led er $($Matches[1]) - 99 er hoejeste tal. Rul andet led: " +
           "naeste efter 1.0.99 er 1.1.00. Ret commit-beskeden og koer igen.")
}

# Foranstillede nuller findes ikke i en .NET-version: 1.1.07 BLIVER til 1.1.7,
# uanset hvad der staar i csproj. Derfor skrives det normaliserede tal ned, og
# sidebjaelken saetter nullet paa igen, naar den viser det. Ellers ville
# efterproevningen nedenfor sammenligne 1.1.7.0 med 1.1.07 og melde fejl paa
# et byg, der er helt i orden.
$normaliseret = ([version]$fuld).ToString(3)
if ($normaliseret -ne $fuld) {
    Write-Host "Version $fuld gemmes som $normaliseret og vises som v$fuld."
    $fuld = $normaliseret
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
    # ET OVERLOEB ER IKKE ET SPRING. Gaar man fra 1.0.99 til 1.1.0, er det
    # ikke en fejl - det er aftalen om, at 99 er hoejeste tal. Der spoerges
    # kun, naar andet led rykker UDEN at tredje led var loebet fuldt.
    $nuLed = $nu -split '\.'
    $nyLed = $fuld -split '\.'
    $erOverloeb = $nuLed.Count -ge 3 -and $nyLed.Count -ge 3 -and
                  $nuLed[0] -eq $nyLed[0] -and
                  [int]$nyLed[1] -eq [int]$nuLed[1] + 1 -and
                  [int]$nuLed[2] -ge 90 -and [int]$nyLed[2] -lt 10

    if ($erOverloeb) {
        Write-Host "Tredje led var loebet fuldt - andet led ruller: $nu -> $fuld."
    }
    elseif ($nuMM -ne $nyMM) {
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
Get-Process -Name HeyPia -ErrorAction SilentlyContinue |
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

# --- TALERGENKENDELSEN FOELGER MED --------------------------------------
#
# Vaerktoejet og de to modeller fylder 48 MB og hentes ikke. Taersklen paa
# 0,80 er maalt mod praecis de to modelfiler; skiftes de ud, gaelder maalingen
# ikke, og den samme optagelse kan gaa fra to talere til toogfirs uden at
# nogen har roert en indstilling. Model og indstilling er een ting.
#
# De kopieres ind i app-mappen, hvor appen leder efter dem - og hvor
# installationspakken samler op ("..\app\**"), saa der kun er eet sted at
# vedligeholde dem.
$talerKilde = Join-Path $Rod 'talere'
if (Test-Path $talerKilde) {
    $talerMaal = Join-Path $udgivTil 'talere'
    Copy-Item $talerKilde $udgivTil -Recurse -Force

    # LISTEN OVER DE NOEDVENDIGE FILER STOD HER FOER, og en kopi af den stod i
    # byg-installer.ps1. To lister om det samme driver fra hinanden. Nu staar
    # den ét sted - i tjek-licenser.ps1 - og gaten laengere nede kontrollerer
    # baade den og licensnotitserne.

    $mb = [math]::Round(((Get-ChildItem $talerMaal -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
    Write-Host "Talergenkendelsen kopieret med ($mb MB)"
}
else {
    Write-Warning ("Mappen $talerKilde findes ikke. Appen udgives UDEN " +
                   "talergenkendelse — udskrifter faar ingen navne paa talerne.")
}

# --- LICENSNOTITSERNE FOELGER MED ---------------------------------------
#
# Compliance-skaermen lovede allerede, at licensteksten "ligger i
# installationsmappen" - for sherpa-onnx (Apache-2.0) og for krediteringen af
# NVIDIA (CC-BY-4.0). Det passede ikke: der laa ingen licensfiler nogen
# steder i repoet 03-09-2026.
#
# En app, der paastaar at overholde en attributionsbetingelse uden at goere
# det, er ikke bare uryddelig - det er et brud paa den licens, komponenten er
# brugt under. Nu kopieres notitserne med, og gaten nedenfor fejler, hvis de
# ikke er der.
$licensKilde = Join-Path $Rod 'licenser'
if (Test-Path $licensKilde) {
    Copy-Item $licensKilde $udgivTil -Recurse -Force
    $antal = (Get-ChildItem (Join-Path $udgivTil 'licenser') -Recurse -File).Count
    Write-Host "Licensnotitser kopieret med ($antal filer)"
}
else {
    throw ("Mappen $licensKilde findes ikke. Appen maa ikke udgives uden " +
           "licensnotitser - Compliance-skaermen lover, at de ligger i " +
           "installationsmappen.")
}

# --- SPAERRE: LICENSNOTITSER OG TALERADSKILLELSE -------------------------
#
# Listen staar i tjek-licenser.ps1 og kun dér. To lister driver fra hinanden -
# det skete for taleradskillelsen, som havde sin egen kopi baade her og i
# byg-installer.ps1.
& powershell -NoProfile -File (Join-Path $PSScriptRoot 'tjek-licenser.ps1') -Mappe $udgivTil
if ($LASTEXITCODE -ne 0) {
    throw "Licenstjekket fejlede. Se linjerne ovenfor."
}

# --- Googles klient-id -----------------------------------------------------
#
# APPENS EGET ID HOS GOOGLE, ikke brugerens. Kunden trykker Forbind, logger ind
# og er faerdig - den, der skal optage et moede om fem minutter, opretter ikke
# et cloud-projekt foerst.
#
# Filen ligger UDEN FOR versionsstyringen. Ikke fordi den er hemmelig - Google
# kalder det en offentlig klient, og id og hemmelighed kan laeses ud af ethvert
# installeret program - men fordi en legitimation i et repo bliver liggende i
# historikken for evigt, ogsaa efter den er fjernet.
#
# Mangler den, udgives appen UDEN Google-integrationen, og fanen siger det paa
# den maade. Det er ikke en fejl; det er en udgave uden den funktion.
#
# BEGGE FILNAVNE TAGES MED. Googles egen fil hedder
# «client_secret_386...apps.googleusercontent.com.json», og appen laeser den
# nu, som den er - at kraeve en omdoebning foerst var en fejlkilde uden
# gevinst.
$hemmeligMappe = Join-Path $Rod 'hemmeligheder'

$googleFiler = @()
if (Test-Path $hemmeligMappe) {
    $googleFiler = @(Get-ChildItem $hemmeligMappe -File |
                     Where-Object { $_.Name -eq 'google-klient.json' -or
                                    $_.Name -like 'client_secret*.json' })
}

if ($googleFiler.Count -gt 0) {
    foreach ($g in $googleFiler) { Copy-Item $g.FullName $udgivTil -Force }
    Write-Host "Googles klient-id kopieret med ($($googleFiler.Count) fil)"
}
else {
    Write-Host "Intet Google-klient-id - udgives uden kalenderintegration"
}

# --- Efterproev ------------------------------------------------------------
# Et byg, der siger "faerdig" uden at filen er skiftet, er vaerre end et, der
# fejler. Derfor kontrolleres resultatet frem for at blive antaget.
$exe = Join-Path $udgivTil 'HeyPia.exe'
if (-not (Test-Path $exe)) { throw "HeyPia.exe blev ikke lagt i $udgivTil" }

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
# Meldes som appen SKRIVER det - med to cifre i tredje led. Rapporteres
# der v1.1.7 her og v1.1.07 paa skaermen, ser det ud som to udgivelser.
$vist = if ($fuld -match '^(\d+)\.(\d+)\.(\d+)$') { "{0}.{1}.{2:00}" -f $Matches[1], $Matches[2], [int]$Matches[3] } else { $fuld }
Write-Host ("PRODUKTION: v{0} · bygget {1:dd-MM HH:mm}" -f $vist, $fil.LastWriteTime) -ForegroundColor Cyan

if ($snavset) {
    Write-Host ("  Bemaerk: der er uommitede aendringer. Versionsnummeret staar stille," +
                " indtil der commites med en ny 'vX.Y.Z:'-besked — brug tidsstemplet til" +
                " at se, hvad der koerer.") -ForegroundColor Yellow
}

# Genvejen paa skrivebordet peger paa en FAST sti, saa den behoever ikke
# aendres. Men det skal kontrolleres, at den stadig goer det — flyttes mappen,
# aabner genvejen ingenting, og det opdages foerst naar man har brug for den.
#
# NAVNET ER IKKE GIVET, OG DER ER MERE END EEN GENVEJ.
#
# Der blev foer kun set efter 'HeyPia.lnk' paa brugerens eget skrivebord. Det
# var forkert paa to maader. Udviklingsgenvejen hedder 'HeyPia_dev.lnk' — den
# er doebt om med vilje, saa de to udgaver kan kendes fra hinanden — og saa
# meldte scriptet "ingen skrivebordsgenvej fundet", mens den stod og pegede
# rigtigt. Omvendt er 'HeyPia.lnk' den INSTALLEREDE udgave, der ligger paa
# faellesskrivebordet og med rette peger paa Program Files; havde scriptet
# fundet den, ville det have advaret om det, der er helt som det skal vaere.
#
# Der ses derfor paa begge skriveborde og paa alle genveje, der peger paa en
# HeyPia.exe. De sorteres efter HVAD de peger paa, ikke efter hvad de hedder.
$skriveborde = @(
    [Environment]::GetFolderPath('Desktop'),
    (Join-Path $env:PUBLIC 'Desktop')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

$sh = New-Object -ComObject WScript.Shell
$genveje = foreach ($sted in $skriveborde) {
    foreach ($fil in Get-ChildItem $sted -Filter '*.lnk' -ErrorAction SilentlyContinue) {
        $maal = $sh.CreateShortcut($fil.FullName).TargetPath
        if ($maal -like '*\HeyPia.exe') {
            [pscustomobject]@{ Navn = $fil.BaseName; Maal = $maal; Her = ($maal -eq $exe) }
        }
    }
}

$herhen = @($genveje | Where-Object Her)

if ($herhen.Count -gt 0) {
    foreach ($g in $herhen) {
        Write-Host "  «$($g.Navn)» peger hertil — den åbner det, der lige er bygget"
    }
}
else {
    Write-Warning ("Ingen genvej på skrivebordet peger på $exe. " +
                   "Appen startes derfra, indtil der laves en.")
}

foreach ($g in @($genveje | Where-Object { -not $_.Her })) {
    Write-Host "  «$($g.Navn)» peger på den installerede udgave: $($g.Maal)"
}

# --- Er der pushet? --------------------------------------------------------
#
# DER COMMITTES OG PUSHES HVER DAG. Er det ikke sket dagen foer, er det den
# foerste handling paa en ny dag.
#
# Reglen kom 23-08-2026, hvor der laa 75 commits, der aldrig var pushet. De
# var ikke tabt - de fandtes bare ét sted, paa den her maskine. Et push er
# noget, man husker, lige indtil man har travlt.
#
# Der pushes IKKE herfra. Leverancetjekket skal koeres foerst, og et push midt
# i en udgivelse ville sende arbejde af sted, ingen har set efter. Der siges
# til, og saa er det et valg.
try {
    git -C $Rod fetch origin --quiet 2>$null

    $ikkePushet = 0
    try { $ikkePushet = [int](git -C $Rod rev-list --count origin/main..HEAD 2>$null) } catch { }

    if ($ikkePushet -gt 0) {
        # Hvor gammel er den aeldste? En commit fra i dag er ikke et problem;
        # en fra i forgaars er.
        $aeldst = git -C $Rod log -1 --format=%cI "origin/main..HEAD" --reverse 2>$null |
                  Select-Object -First 1

        $dage = 0
        if ($aeldst) {
            try { $dage = [int]((Get-Date) - [datetimeoffset]::Parse($aeldst).LocalDateTime).TotalDays }
            catch { }
        }

        Write-Host ''
        if ($dage -ge 1) {
            Write-Host "  $ikkePushet commit(s) er IKKE pushet - den aeldste er $dage dag(e) gammel." -ForegroundColor Yellow
            Write-Host '  Push er dagens foerste handling:' -ForegroundColor Yellow
        }
        else {
            Write-Host "  $ikkePushet commit(s) er ikke pushet endnu." -ForegroundColor Yellow
            Write-Host '  Husk det inden fyraften:' -ForegroundColor Yellow
        }
        Write-Host '      powershell -File C:\NoteApp\scripts\sikker-kode.ps1 -Push'
    }
}
catch {
    # Intet netvaerk eller intet fjernlager. Udgivelsen er faerdig; det her er
    # en paamindelse, ikke et krav.
}
