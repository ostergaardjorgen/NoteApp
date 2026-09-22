<#
.SYNOPSIS
    Kopierer hukommelsen og skills fra ~/.claude ind i kontinuitet/, saa de
    ligger i git.

.DESCRIPTION
    HVORFOR DEN FINDES

    Hukommelsen og skills RETTES i ~/.claude - det er der, Claude Code laeser
    dem. Kopien i kontinuitet/ blev kun opdateret, naar nogen huskede det, og
    22-09-2026 manglede 14 af 24 hukommelsesfiler og hele test-skillen. En ny
    konto eller en ny maskine ville have startet uden dem.

    Nu koeres den af sikker-kode.ps1 -Push, og kopien foelger med hvert push.

    SPEJLING, IKKE TILFOEJELSE

    En hukommelse, der er slettet i ~/.claude, slettes ogsaa her. Den var
    slettet, fordi den var forkert - og en forkert hukommelse, der kommer
    tilbage ved en gendannelse, er vaerre end ingen. Git har den stadig.

    forbudte-termer.txt KOPIERES ALDRIG. Den er listen over det, der ikke maa
    ud af maskinen, og i et repo ville den udstille praecis det.

.PARAMETER Tjek
    Kopierer ingenting. Siger kun, hvad der er forskelligt, og returnerer
    exitkode 1, hvis noget er.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\opdater-kontinuitet.ps1
#>
[CmdletBinding()]
param([switch] $Tjek)

$ErrorActionPreference = 'Stop'

$rod = Split-Path $PSScriptRoot -Parent
$kont = Join-Path $rod 'kontinuitet'

$par = @(
    @{ Fra = Join-Path $env:USERPROFILE '.claude\projects\C--ClaudeCode\memory'
       Til = Join-Path $kont 'hukommelse'
       Navn = 'hukommelse' }
    @{ Fra = Join-Path $env:USERPROFILE '.claude\skills'
       Til = Join-Path $kont 'skills'
       Navn = 'skills' }
)

# Filer, der ALDRIG maa i git - uanset hvor de ligger.
$aldrig = @('forbudte-termer.txt')

# ============ FILER MED DERES EGEN UDGAVE I KONTINUITET ============
#
# Leverancetjekkets SKILL.md i ~/.claude naevner selv de navne, tjekket skal
# holde ude. Kopien her er skrevet om, saa den ikke goer - og 22-09-2026
# skrev foerste udgave af det her script den gamle hen over den. Leverance-
# tjekket fangede det, foer det kom i git. De her roeres aldrig: hverken
# overskrives eller slettes.
$egneUdgaver = @('skills\leverancetjek\SKILL.md')

function Relativ($fil, $mappe) { $fil.Substring($mappe.TrimEnd('\').Length + 1) }

$aendret = 0

foreach ($p in $par) {
    if (-not (Test-Path $p.Fra)) {
        Write-Host "  $($p.Navn): $($p.Fra) findes ikke - springes over" -ForegroundColor Yellow
        continue
    }

    $kilder = @(Get-ChildItem $p.Fra -Recurse -File | Where-Object { $aldrig -notcontains $_.Name })
    $kildeNavne = @($kilder | ForEach-Object { Relativ $_.FullName $p.Fra })

    foreach ($k in $kilder) {
        $rel = Relativ $k.FullName $p.Fra
        $maal = Join-Path $p.Til $rel

        if ($egneUdgaver -contains "$($p.Navn)\$rel") { continue }

        $ens =(Test-Path $maal) -and
               ((Get-FileHash $maal).Hash -eq (Get-FileHash $k.FullName).Hash)

        if ($ens) { continue }

        $aendret++
        $hvad = if (Test-Path $maal) { 'aendret' } else { 'ny' }
        Write-Host "  $($p.Navn)\$rel  ($hvad)"

        if (-not $Tjek) {
            New-Item -ItemType Directory -Force (Split-Path $maal) | Out-Null
            Copy-Item $k.FullName $maal -Force
        }
    }

    # Det, der er vaek i ~/.claude, skal ogsaa vaek her.
    if (Test-Path $p.Til) {
        foreach ($g in @(Get-ChildItem $p.Til -Recurse -File)) {
            $rel = Relativ $g.FullName $p.Til
            if ($kildeNavne -contains $rel) { continue }
            if ($egneUdgaver -contains "$($p.Navn)\$rel") { continue }

            $aendret++
            Write-Host "  $($p.Navn)\$rel  (slettet i ~/.claude)"

            if (-not $Tjek) { Remove-Item $g.FullName -Force }
        }
    }
}

# Sikkerhedsnet: laa den forbudte liste der alligevel, er noget gaaet galt.
$laek = @(Get-ChildItem $kont -Recurse -File | Where-Object { $aldrig -contains $_.Name })
if ($laek.Count -gt 0) {
    $laek | Remove-Item -Force
    throw "forbudte-termer.txt laa i kontinuitet/ og er fjernet igen. Den maa aldrig i git."
}

if ($aendret -eq 0) {
    Write-Host '  kontinuitet er ajour' -ForegroundColor Green
}
elseif ($Tjek) {
    Write-Host "  $aendret fil(er) er ikke ajour i kontinuitet/" -ForegroundColor Yellow
    exit 1
}
else {
    Write-Host "  $aendret fil(er) opdateret i kontinuitet/" -ForegroundColor Green
}
