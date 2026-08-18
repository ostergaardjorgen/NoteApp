<#
.SYNOPSIS
Måler om et referat tillægger de rigtige personer de rigtige udsagn.

.DESCRIPTION
Findes, fordi den fejl, der gjorde det første referat ubrugeligt, ikke kunne
ses af nogen af de andre målinger: «det citerer Espen for at sige ting, som jeg
selv har sagt».

bedoem-referat2.ps1 tæller, om et navn er NÆVNT. Den kan ikke se, om det står
det rigtige sted. Et referat kan ramme længden, tallene og alle navne og
stadig hæfte den ene deltagers livsforløb på den anden — og så er det værre
end et referat, der mangler noget, for man opdager det ikke ved at læse det.

METODEN er den samme, projektet allerede bruger ved oplæsning: en håndlavet
facitliste, og så tælles der. Her er facit en liste over udsagn med hvem der
sagde dem; se facit-udsagn.md for formatet.

FIRE UDFALD PR. UDSAGN
  RIGTIG        fundet, og tilskrevet den rigtige
  FORKERT       fundet, men tilskrevet en anden deltager   <- den alvorlige
  UTILSKREVET   fundet, men ingen deltager i nærheden
  MANGLER       ikke fundet

«Utilskrevet» er ikke en fejl i sig selv. Et referat kan skrive «det blev
nævnt, at …» uden at hænge det på nogen, og det er ærligt. Det tælles for sig,
så det ikke forveksles med en forkert tilskrivning.

.EXAMPLE
powershell -File scripts\bedoem-tilskrivning.ps1 -Referat udkast.md -Facit facit-udsagn.md
#>
param(
    [Parameter(Mandatory)] [string] $Referat,
    [Parameter(Mandatory)] [string] $Facit,
    [switch] $Detaljer
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Referat)) { throw "Findes ikke: $Referat" }
if (-not (Test-Path $Facit))   { throw "Findes ikke: $Facit" }

# --- Facitlisten -----------------------------------------------------------
$deltagere = @()
$udsagn = @()

foreach ($linje in Get-Content $Facit -Encoding UTF8) {
    $l = $linje.Trim()
    if ($l.Length -eq 0 -or $l.StartsWith('#')) { continue }

    if ($l -match '^deltagere:\s*(.+)$') {
        $deltagere = $Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
        continue
    }

    $dele = $l -split '\|', 2
    if ($dele.Count -ne 2) { continue }

    $udsagn += [pscustomobject]@{
        Person = $dele[0].Trim()
        Ord    = ($dele[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
}

if ($deltagere.Count -eq 0) { throw "Facitlisten mangler en 'deltagere:'-linje." }
if ($udsagn.Count -eq 0)    { throw "Facitlisten indeholder ingen udsagn." }

# --- Referatet -------------------------------------------------------------
# Frontmatter taeller ikke med - se bedoem-referat2.ps1 for hvorfor.
$raa = (Get-Content $Referat -Raw -Encoding UTF8) -replace "`r`n", "`n"
$graenser = [regex]::Matches($raa, '(?m)^---\s*$')
if ($graenser.Count -ge 2 -and $graenser[0].Index -le 3) {
    $raa = $raa.Substring($graenser[1].Index + $graenser[1].Length).Trim()
}

# Afsnit er enheden. Et udsagn og navnet paa den, der sagde det, staar i det
# samme afsnit - eller under den samme overskrift.
$afsnit = $raa -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 0 }

# --- Maalingen -------------------------------------------------------------
function HvemTilskrives($nr) {
    # Foerst afsnittet selv. Derefter tilbage gennem de foregaaende afsnit,
    # men KUN til naermeste overskrift - en overskrift er et emneskift, og et
    # navn paa den anden side af den hoerer til et andet emne.
    for ($i = $nr; $i -ge 0; $i--) {
        $t = $afsnit[$i]

        $fundne = @($deltagere | Where-Object { $t -match [regex]::Escape($_) })
        if ($fundne.Count -eq 1) { return $fundne[0] }
        if ($fundne.Count -gt 1) { return '(flere)' }

        # Stop ved overskriften, men laes den foerst - "### Jørgen" er en
        # tilskrivning af alt, der staar under den.
        if ($i -lt $nr -and $t.StartsWith('#')) { break }
    }
    return $null
}

$resultat = foreach ($u in $udsagn) {
    $traeffer = -1

    for ($i = 0; $i -lt $afsnit.Count; $i++) {
        $t = $afsnit[$i]
        $antal = @($u.Ord | Where-Object { $t -match [regex]::Escape($_) }).Count

        # Alle noegleord skal vaere der. Med faerre ville "2018" alene kunne
        # ramme et hvilket som helst afsnit med et aarstal i.
        if ($antal -eq $u.Ord.Count) { $traeffer = $i; break }
    }

    if ($traeffer -lt 0) {
        [pscustomobject]@{ Udsagn = ($u.Ord -join ', '); Ventet = $u.Person; Fik = ''; Udfald = 'MANGLER' }
        continue
    }

    $hvem = HvemTilskrives $traeffer

    $udfald = if ($null -eq $hvem -or $hvem -eq '(flere)') { 'UTILSKREVET' }
              elseif ($hvem -eq $u.Person) { 'RIGTIG' }
              else { 'FORKERT' }

    [pscustomobject]@{ Udsagn = ($u.Ord -join ', '); Ventet = $u.Person; Fik = $hvem; Udfald = $udfald }
}

# --- Svaret ----------------------------------------------------------------
$rigtig = @($resultat | Where-Object Udfald -eq 'RIGTIG').Count
$forkert = @($resultat | Where-Object Udfald -eq 'FORKERT').Count
$uden = @($resultat | Where-Object Udfald -eq 'UTILSKREVET').Count
$mangler = @($resultat | Where-Object Udfald -eq 'MANGLER').Count
$ialt = $resultat.Count

Write-Host ""
Write-Host "TILSKRIVNING" -ForegroundColor Cyan
Write-Host ("  referat : {0}" -f (Split-Path -Leaf $Referat))
Write-Host ("  udsagn  : {0}" -f $ialt)
Write-Host ""
Write-Host ("  RIGTIG      : {0,3}   tilskrevet den rigtige" -f $rigtig) -ForegroundColor Green
Write-Host ("  FORKERT     : {0,3}   tilskrevet en anden deltager" -f $forkert) -ForegroundColor $(if ($forkert -gt 0) { 'Red' } else { 'Gray' })
Write-Host ("  UTILSKREVET : {0,3}   fundet, men uden navn i naerheden" -f $uden)
Write-Host ("  MANGLER     : {0,3}   slet ikke med" -f $mangler) -ForegroundColor Yellow

if ($forkert -gt 0 -or $Detaljer) {
    Write-Host ""
    foreach ($r in $resultat | Where-Object { $_.Udfald -ne 'RIGTIG' -or $Detaljer }) {
        $farve = switch ($r.Udfald) { 'FORKERT' { 'Red' } 'MANGLER' { 'Yellow' } 'RIGTIG' { 'Green' } default { 'Gray' } }
        $fik = if ($r.Fik) { $r.Fik } else { '-' }
        Write-Host ("  {0,-12} ventet {1,-10} fik {2,-10} {3}" -f $r.Udfald, $r.Ventet, $fik, $r.Udsagn) -ForegroundColor $farve
    }
}

Write-Host ""
Write-Host "SAMLET" -ForegroundColor Cyan

# Det tal, der betyder noget: af de udsagn, referatet FAKTISK tilskriver
# nogen, hvor mange rammer saa den rigtige? Et referat, der udelader
# halvdelen, skal ikke belaennes for at have faa fejl blandt resten - derfor
# staar daekningen ved siden af.
$tilskrevet = $rigtig + $forkert
Write-Host ("  daekning    : {0:N0} % af udsagnene er med" -f (100 * ($ialt - $mangler) / $ialt))
if ($tilskrevet -gt 0) {
    Write-Host ("  praecision  : {0:N0} % af de tilskrevne rammer den rigtige" -f (100 * $rigtig / $tilskrevet))
}
Write-Host ""

if ($forkert -eq 0 -and $mangler * 100 / $ialt -le 25) {
    Write-Host "  Ingen forkerte tilskrivninger." -ForegroundColor Green
} elseif ($forkert -gt 0) {
    Write-Host ("  {0} udsagn er haeftet paa den forkerte person. Det er den alvorlige fejl." -f $forkert) -ForegroundColor Red
} else {
    Write-Host "  Ingen forkerte tilskrivninger, men meget mangler." -ForegroundColor Yellow
}
Write-Host ""
