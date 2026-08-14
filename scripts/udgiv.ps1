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

    Versionen læses som standard af den seneste commit ("vX.YY: ..."), så
    nummeret i appen ikke kan komme ud af trit med historikken.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\udgiv.ps1

.EXAMPLE
    powershell -File C:\NoteApp\scripts\udgiv.ps1 -Version 0.40
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

    if ($besked -match '^v(\d+\.\d+)') {
        $Version = $Matches[1]
        Write-Host "Version fra seneste commit: v$Version"
    }
    else {
        throw "Kunne ikke læse versionen af seneste commit ('$besked'). Angiv -Version."
    }
}

$fuld = if ($Version -match '^\d+\.\d+$') { "$Version.0" } else { $Version }

$tekst = [IO.File]::ReadAllText($csproj, [Text.Encoding]::UTF8)
$nu = if ($tekst -match '<Version>([^<]+)</Version>') { $Matches[1] } else { '(ingen)' }

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
& dotnet publish $csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $udgivTil --nologo -v q | Out-Null

if ($LASTEXITCODE -ne 0) { throw "dotnet publish fejlede med kode $LASTEXITCODE" }

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
                " indtil der commites med en ny 'vX.YY:'-besked — brug tidsstemplet til" +
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
