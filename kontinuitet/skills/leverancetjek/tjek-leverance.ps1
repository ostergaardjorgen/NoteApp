<#
.SYNOPSIS
    Leverancetjek: kontrollerer at forbudte termer og persondata ikke er på
    vej ud af maskinen.

.DESCRIPTION
    Generel kontrol, der gælder på tværs af alle udviklingsprojekter.
    Køres før commit, før push og før enhver leverance.

    Tjek 1  Forbudte termer i sporede filer
    Tjek 2  Forbudte termer i det, der er staged lige nu
    Tjek 3  Forbudte termer i git-historikken (med -Historik)
    Tjek 4  Datafiler på vej i versionsstyring
    Tjek 5  Forbudte termer i filnavne og mappenavne

    Termlisten ligger i forbudte-termer.txt ved siden af dette script og er
    med vilje LOKAL. Den må aldrig committes — det ville udstille præcis det,
    den er sat i verden for at skjule.

.PARAMETER Sti
    Projektmappen der skal kontrolleres. Standard er nuværende mappe.

.PARAMETER Historik
    Gennemsøg også hele git-historikken. Langsommere, men det er dér, en
    term overlever, efter den er fjernet fra HEAD.

.EXAMPLE
    .\tjek-leverance.ps1 -Sti C:\NoteApp

.EXAMPLE
    .\tjek-leverance.ps1 -Sti C:\NoteApp -Historik
#>
[CmdletBinding()]
param(
    [string] $Sti = (Get-Location).Path,
    [switch] $Historik,
    [string] $TermListe
)

$ErrorActionPreference = 'Stop'

# --- Termlisten -----------------------------------------------------------
# $PSScriptRoot udfyldes IKKE, mens param-blokken bliver evalueret under alle
# kaldemaader — den stod her som standardvaerdi, og den dokumenterede kommando
# faldt derfor med "Cannot bind argument to parameter 'Path'". En gate, der
# fejler paa sin egen brugsanvisning, bliver sprunget over frem for rettet, og
# saa er den ikke laengere en gate. Stien findes derfor efter param-blokken,
# hvor $PSScriptRoot altid er sat, med kommandoens egen sti som reserve.
if (-not $TermListe) {
    $rod = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
    $TermListe = Join-Path $rod 'forbudte-termer.txt'
}

if (-not (Test-Path $TermListe)) { throw "Termlisten findes ikke: $TermListe" }

$termer = @()
foreach ($linje in Get-Content $TermListe -Encoding UTF8) {
    $l = $linje.Trim()
    if (-not $l -or $l.StartsWith('#')) { continue }
    $delt = $l -split ':', 2
    $termer += [pscustomobject]@{
        Term      = $delt[0].Trim()
        Forklaring = if ($delt.Count -gt 1) { $delt[1].Trim() } else { '' }
    }
}
if (-not $termer) { throw "Termlisten er tom: $TermListe" }

# --- Datafiler der aldrig hører i versionsstyring -------------------------
$dataMønstre = @(
    '\.db$', '\.db-wal$', '\.db-shm$',
    '\.wav$', '\.mp3$', '\.m4a$',
    'notes\.jsonl$', 'meeting\.json$',
    '\.pfx$', '\.pem$', '\.key$',
    '\.env$', 'secrets?\.(json|ya?ml)$'
)

Push-Location $Sti
$fund = @()
$erGit = $false
try { $erGit = (git rev-parse --is-inside-work-tree 2>$null) -eq 'true' } catch { }

function Tilføj($tjek, $hvor, $detalje, $alvor = 'FEJL') {
    $script:fund += [pscustomobject]@{ Tjek = $tjek; Alvor = $alvor; Hvor = $hvor; Detalje = $detalje }
}

Write-Host ""
Write-Host "Leverancetjek: $Sti" -ForegroundColor Cyan
Write-Host "Termer i listen: $($termer.Count)" -ForegroundColor DarkGray
Write-Host ""

# --- Tjek 1: forbudte termer i sporede filer ------------------------------
Write-Host "1  Forbudte termer i sporede filer ... " -NoNewline
if ($erGit) {
    foreach ($t in $termer) {
        $træf = git grep -n -i -F -- $t.Term 2>$null
        foreach ($linje in $træf) {
            Tilføj 'forbudt-term' $linje "$($t.Term) — $($t.Forklaring)"
        }
    }
} else {
    $filer = Get-ChildItem $Sti -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(\.git|node_modules|bin|obj|models|tools)\\' }
    foreach ($f in $filer) {
        $indhold = try { Get-Content $f.FullName -Raw -ErrorAction Stop } catch { $null }
        if (-not $indhold) { continue }
        foreach ($t in $termer) {
            if ($indhold -like "*$($t.Term)*") {
                Tilføj 'forbudt-term' $f.FullName "$($t.Term) — $($t.Forklaring)"
            }
        }
    }
}
$antal1 = ($fund | Where-Object Tjek -eq 'forbudt-term').Count
Write-Host $(if ($antal1 -eq 0) { 'rent' } else { "$antal1 fund" }) -ForegroundColor $(if ($antal1 -eq 0) { 'Green' } else { 'Red' })

# --- Tjek 2: forbudte termer i staged ændringer ---------------------------
Write-Host "2  Forbudte termer i staged ændringer ... " -NoNewline
$antal2 = 0
if ($erGit) {
    # KUN tilfoejede linjer. En diff viser ogsaa det der FJERNES, med '-'
    # foran — og en oprydningscommit fjerner netop forbudte termer. Uden
    # denne skelnen ville vaerktoejet fejle praecis naar man goer det
    # rigtige, og saa holder folk op med at koere det.
    $tilføjede = (git diff --cached --unified=0 2>$null |
        Where-Object { $_ -match '^\+' -and $_ -notmatch '^\+\+\+' }) -join "`n"

    if ($tilføjede) {
        $lav = $tilføjede.ToLowerInvariant()
        foreach ($t in $termer) {
            if ($lav.Contains($t.Term.ToLowerInvariant())) {
                Tilføj 'staged-term' 'tilføjet linje i staged diff' "$($t.Term) — $($t.Forklaring)"
                $antal2++
            }
        }
    }
}
Write-Host $(if ($antal2 -eq 0) { 'rent' } else { "$antal2 fund" }) -ForegroundColor $(if ($antal2 -eq 0) { 'Green' } else { 'Red' })

# --- Tjek 3: git-historikken ----------------------------------------------
Write-Host "3  Forbudte termer i git-historikken ... " -NoNewline
$antal3 = 0
if (-not $erGit) {
    Write-Host "springes over (ikke et git-repo)" -ForegroundColor DarkGray
} elseif (-not $Historik) {
    Write-Host "springes over (brug -Historik)" -ForegroundColor DarkGray
} else {
    foreach ($t in $termer) {
        $træf = git grep -i -F -- $t.Term $(git rev-list --all 2>$null) 2>$null | Select-Object -First 5
        foreach ($linje in $træf) {
            Tilføj 'historik-term' $linje "$($t.Term) — findes stadig i historikken" 'ADVARSEL'
            $antal3++
        }
    }
    Write-Host $(if ($antal3 -eq 0) { 'rent' } else { "$antal3 fund" }) -ForegroundColor $(if ($antal3 -eq 0) { 'Green' } else { 'Yellow' })
}

# --- Tjek 4: datafiler i versionsstyring ----------------------------------
Write-Host "4  Datafiler i versionsstyring ... " -NoNewline
$antal4 = 0
if ($erGit) {
    $sporede = git ls-files 2>$null
    foreach ($f in $sporede) {
        foreach ($m in $dataMønstre) {
            if ($f -match $m) { Tilføj 'datafil' $f "matcher $m"; $antal4++; break }
        }
    }
}
Write-Host $(if ($antal4 -eq 0) { 'rent' } else { "$antal4 fund" }) -ForegroundColor $(if ($antal4 -eq 0) { 'Green' } else { 'Red' })

# --- Tjek 5: forbudte termer i fil- og mappenavne -------------------------
Write-Host "5  Forbudte termer i fil- og mappenavne ... " -NoNewline
$antal5 = 0
$navne = if ($erGit) { git ls-files 2>$null } else {
    Get-ChildItem $Sti -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\\.git\\' } | ForEach-Object { $_.FullName }
}
foreach ($n in $navne) {
    foreach ($t in $termer) {
        if ($n.ToLowerInvariant().Contains($t.Term.ToLowerInvariant())) {
            Tilføj 'filnavn' $n "$($t.Term) i stien"; $antal5++; break
        }
    }
}
Write-Host $(if ($antal5 -eq 0) { 'rent' } else { "$antal5 fund" }) -ForegroundColor $(if ($antal5 -eq 0) { 'Green' } else { 'Red' })

Pop-Location

# --- Resultat -------------------------------------------------------------
Write-Host ""
$fejl = @($fund | Where-Object Alvor -eq 'FEJL')
$advarsler = @($fund | Where-Object Alvor -eq 'ADVARSEL')

if ($fund) {
    Write-Host "=== Fund ===" -ForegroundColor Yellow
    foreach ($f in $fund) {
        $farve = if ($f.Alvor -eq 'FEJL') { 'Red' } else { 'Yellow' }
        Write-Host "  [$($f.Alvor)] $($f.Tjek)" -ForegroundColor $farve
        Write-Host "    $($f.Hvor)"
        Write-Host "    $($f.Detalje)" -ForegroundColor DarkGray
    }
    Write-Host ""
}

if ($fejl.Count -gt 0) {
    Write-Host "LEVERANCETJEK FEJLEDE: $($fejl.Count) fejl, $($advarsler.Count) advarsler." -ForegroundColor Red
    Write-Host "Ret dem før commit eller leverance." -ForegroundColor Red
    exit 1
}

if ($advarsler.Count -gt 0) {
    Write-Host "LEVERANCETJEK BESTÅET med $($advarsler.Count) advarsler." -ForegroundColor Yellow
    Write-Host "Historik-fund kræver en omskrivning af historikken for at forsvinde." -ForegroundColor Yellow
    exit 0
}

Write-Host "LEVERANCETJEK BESTÅET. Intet forbudt fundet." -ForegroundColor Green
exit 0
