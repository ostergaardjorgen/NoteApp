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
        Lav       = $delt[0].Trim().ToLowerInvariant()
        Forklaring = if ($delt.Count -gt 1) { $delt[1].Trim() } else { '' }
    }
}
if (-not $termer) { throw "Termlisten er tom: $TermListe" }

# --- Undtagelser -----------------------------------------------------------
# Udtryk, der indeholder en forbudt term som delstreng, men som er noget
# helt andet — fx navnet på en åben login-standard (OIDC), der rummer en af
# termerne som en del af ordet. Termen skrives ikke her: så ville scriptet
# selv blive et fund, når det ligger i kontinuitet/ i et repo.
# Jørgen besluttede 21-09-2026, at standardens navn skal kunne stå, hvor det
# handler om løsning og arkitektur. Udtrykkene fjernes fra teksten, FØR der
# søges efter forbudte termer; alt andet på samme linje tjekkes stadig.
# Listen ligger i undtagelser.txt ved siden af scriptet — ét udtryk pr.
# linje, med en begrundelse efter kolon.
$undtagelsesFil = Join-Path (Split-Path -Parent $TermListe) 'undtagelser.txt'
$undtagelser = @()
if (Test-Path $undtagelsesFil) {
    foreach ($linje in Get-Content $undtagelsesFil -Encoding UTF8) {
        $l = $linje.Trim()
        if (-not $l -or $l.StartsWith('#')) { continue }
        $udtryk = ($l -split ':', 2)[0].Trim()
        if ($udtryk) { $undtagelser += [regex]::new([regex]::Escape($udtryk), 'IgnoreCase') }
    }
}
function Rens([string] $tekst) {
    if (-not $tekst) { return $tekst }
    foreach ($u in $undtagelser) { $tekst = $u.Replace($tekst, '') }
    return $tekst
}

# Alle termer som ét saet argumenter til git grep: -e term1 -e term2 ...
# Et kald pr. term gennemgik det samme materiale forfra hver gang. Maalt
# 04-09-2026 paa HeyPia: 414 sek med seks kald mod 65 sek med ét — samme fund.
# Prisen er, at git grep ikke siger HVILKEN term der ramte; det findes
# bagefter i selve traeflinjen.
$termArgs = @()
foreach ($t in $termer) { $termArgs += '-e'; $termArgs += $t.Term }

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
    $træf = @(git grep -n -i -F @termArgs 2>$null)
    foreach ($linje in $træf) {
        $lav = (Rens $linje).ToLowerInvariant()
        foreach ($t in $termer) {
            if ($lav.Contains($t.Lav)) {
                Tilføj 'forbudt-term' $linje "$($t.Term) — $($t.Forklaring)"
            }
        }
    }

    # Et navn delt over et linjeskift ("...ID" / "// Connect...") ses ikke af
    # en linjebaseret søgning. Fundet 21-09-2026 i seks kommentarer, der havde
    # bestået tjekket i ugevis. Flerordstermer søges derfor også på tværs af
    # linjer, med mellemrum og kommentarmarkører imellem. Kun fund, der faktisk
    # spænder over et linjeskift, meldes her — resten er fanget ovenfor.
    $flerord = @($termer | Where-Object { $_.Term.Trim() -match '\s' })
    if ($flerord.Count -gt 0) {
        $mønstre = foreach ($t in $flerord) {
            $ord = $t.Term.Trim() -split '\s+' | ForEach-Object { [regex]::Escape($_) }
            @{ T = $t; Rx = [regex]::new(($ord -join '[\s/*#-]+'), 'IgnoreCase') }
        }
        foreach ($rel in @(git ls-files 2>$null)) {
            $fuld = Join-Path $Sti $rel
            $fi = Get-Item -LiteralPath $fuld -ErrorAction SilentlyContinue
            if (-not $fi -or $fi.Length -gt 2MB) { continue }
            $indhold = try { [IO.File]::ReadAllText($fuld) } catch { $null }
            if (-not $indhold -or $indhold.IndexOf([char]0) -ge 0) { continue }
            $indhold = Rens $indhold
            foreach ($m in $mønstre) {
                foreach ($hit in $m.Rx.Matches($indhold)) {
                    if ($hit.Value -notmatch "`n") { continue }
                    $linjenr = ($indhold.Substring(0, $hit.Index) -split "`n").Count
                    $vist = $hit.Value -replace '\s+', ' '
                    Tilføj 'forbudt-term' "${rel}:${linjenr}: (delt over linjeskift) $vist" "$($m.T.Term) — $($m.T.Forklaring)"
                }
            }
        }
    }
} else {
    $filer = Get-ChildItem $Sti -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(\.git|node_modules|bin|obj|models|tools)\\' }
    foreach ($f in $filer) {
        $indhold = try { Get-Content $f.FullName -Raw -ErrorAction Stop } catch { $null }
        if (-not $indhold) { continue }
        $indhold = Rens $indhold
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
        $lav = (Rens $tilføjede).ToLowerInvariant()
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
    $revs = @(git rev-list --all 2>$null)
    if ($revs.Count -eq 0) {
        Write-Host "springes over (ingen commits)" -ForegroundColor DarkGray
    }
    else {
        # Alle termer i hvert kald. Loftet paa 1000 linjer er en sikkerhedsventil:
        # i et staerkt forurenet repo standser den gennemgangen i stedet for at
        # laese titusinder af traef ind, som ingen alligevel naar at se paa.
        # Revisionerne gives i portioner: alle paa én kommandolinje sprænger
        # Windows' graense paa 32.767 tegn ("Filnavnet eller filtypenavnet er
        # for langt") i et repo med tusindvis af commits, fx plane-dk.
        $portion = 200
        $træf = @()
        for ($i = 0; $i -lt $revs.Count -and $træf.Count -lt 1000; $i += $portion) {
            $slut = [Math]::Min($i + $portion, $revs.Count) - 1
            Write-Progress -Activity 'Git-historikken' -Status "revision $($i + 1)-$($slut + 1) af $($revs.Count)" `
                -PercentComplete ([int](100 * $i / $revs.Count))
            $træf += @(git grep -i -F @termArgs $revs[$i..$slut] 2>$null |
                Select-Object -First (1000 - $træf.Count))
        }
        Write-Progress -Activity 'Git-historikken' -Completed

        # Hoejst 5 fund pr. term, som foer — resten er den samme historie igen.
        $prTerm = @{}
        foreach ($linje in $træf) {
            $lav = (Rens $linje).ToLowerInvariant()
            foreach ($t in $termer) {
                if (-not $lav.Contains($t.Lav)) { continue }
                if (-not $prTerm.ContainsKey($t.Term)) { $prTerm[$t.Term] = 0 }
                if ($prTerm[$t.Term] -ge 5) { continue }
                $prTerm[$t.Term]++
                Tilføj 'historik-term' $linje "$($t.Term) — findes stadig i historikken" 'ADVARSEL'
                $antal3++
            }
        }

        # Samme blinde plet som i tjek 1: git grep er linjebaseret og ser ikke
        # et navn delt over et linjeskift. Den linje, der indfoerte teksten,
        # staar i en patch som to paa hinanden foelgende '+'-linjer, saa
        # patch-teksten for hele historikken soeges med en flerlinjet regel.
        $flerordH = @($termer | Where-Object { $_.Term.Trim() -match '\s' })
        if ($flerordH.Count -gt 0) {
            $patch = (git log --all -p --no-color --format='@@LEVERANCETJEK-COMMIT@@ %H' 2>$null) -join "`n"
            $patch = Rens $patch
            foreach ($t in $flerordH) {
                $ord = $t.Term.Trim() -split '\s+' | ForEach-Object { [regex]::Escape($_) }
                $rx = [regex]::new(($ord -join '[ \t/*#-]*\r?\n[+ \t/*#-]*'), 'IgnoreCase')
                $vist = 0
                foreach ($hit in $rx.Matches($patch)) {
                    if ($vist -ge 5) { break }
                    $foer = $patch.LastIndexOf("@@LEVERANCETJEK-COMMIT@@ ", $hit.Index)
                    $commit = if ($foer -ge 0) { $patch.Substring($foer + 25, 12) } else { '?' }
                    Tilføj 'historik-term' "${commit}: (delt over linjeskift) $($hit.Value -replace '\s+', ' ')" "$($t.Term) — findes stadig i historikken" 'ADVARSEL'
                    $antal3++; $vist++
                }
            }
        }
        Write-Host $(if ($antal3 -eq 0) { 'rent' } else { "$antal3 fund" }) -ForegroundColor $(if ($antal3 -eq 0) { 'Green' } else { 'Yellow' })
    }
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
        if ((Rens $n).ToLowerInvariant().Contains($t.Term.ToLowerInvariant())) {
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
