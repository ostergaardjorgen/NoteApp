<#
.SYNOPSIS
Sammenligner et mødereferat med et facit — og siger hvor det svigter.

.DESCRIPTION
Findes, fordi «det er for ringe» ikke er noget, man kan arbejde ud fra. Et
referat kan være for kort, mangle deltagere, tabe tal, eller — værst —
tillægge én person en andens udtalelser. De fire ting kræver hver sin rettelse,
og uden at vide hvilken af dem der fejler, retter man i blinde.

Facit er et referat, man ved er godt. Her: det, en stor sprogmodel lavede af
den samme udskrift.

MÅLINGEN ER GROV MED VILJE. Den tæller navne, tal og længde — den kan ikke
vurdere sprog eller dømmekraft. Den fanger de fejl, der ER talbare, så man kan
holde op med at diskutere dem og kigge på resten.

.EXAMPLE
powershell -File scripts\bedoem-referat2.ps1 -Referat udkast.md -Facit reference.txt
#>
param(
    [Parameter(Mandatory)] [string] $Referat,
    [Parameter(Mandatory)] [string] $Facit
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Referat)) { throw "Findes ikke: $Referat" }
if (-not (Test-Path $Facit))   { throw "Findes ikke: $Facit" }

$r = Get-Content $Referat -Raw -Encoding UTF8
$f = Get-Content $Facit   -Raw -Encoding UTF8

function Ord($t) { ($t -split '\s+' | Where-Object { $_ }).Count }

Write-Host ""
Write-Host "BEDOEMMELSE" -ForegroundColor Cyan
Write-Host ("  referat : {0} tegn, {1} ord" -f $r.Length, (Ord $r))
Write-Host ("  facit   : {0} tegn, {1} ord" -f $f.Length, (Ord $f))
Write-Host ("  laengde : {0:N0} % af facit" -f (100 * (Ord $r) / (Ord $f)))

# --- Egennavne -------------------------------------------------------------
# Navne med stort begyndelsesbogstav midt i en saetning. Groft, men det fanger
# personer, firmaer og produkter — og det er dem, et referat skal have ret.
$rx = [regex]'(?<![.!?]\s)(?<!^)\b([A-ZÆØÅ][a-zæøåA-ZÆØÅ0-9]{2,})\b'

function Navne($t) {
    $s = @{}
    foreach ($m in $rx.Matches($t)) {
        $n = $m.Groups[1].Value
        if ($n -in 'Der','Det','Han','Hun','De','Han','Jeg','Vi','Men','Og','Som','For') { continue }
        $s[$n] = ($s[$n] + 1)
    }
    $s
}

$rn = Navne $r
$fn = Navne $f

$mangler = $fn.Keys | Where-Object { $_ -notin $rn.Keys } | Sort-Object
$opfundet = $rn.Keys | Where-Object { $_ -notin $fn.Keys } | Sort-Object

Write-Host ""
Write-Host "NAVNE" -ForegroundColor Cyan
Write-Host ("  i facit    : {0}" -f $fn.Count)
Write-Host ("  i referatet: {0}" -f $rn.Count)

if ($mangler) {
    Write-Host ("  MANGLER ({0}):" -f @($mangler).Count) -ForegroundColor Yellow
    Write-Host ("    " + ($mangler -join ', '))
}

if ($opfundet) {
    # Et navn, der ikke staar i facit, er enten en hoerefejl eller noget
    # modellen har fundet paa. Begge dele er alvorligt i et referat.
    Write-Host ("  IKKE I FACIT ({0}) — hoerefejl eller opfundet:" -f @($opfundet).Count) -ForegroundColor Red
    Write-Host ("    " + ($opfundet -join ', '))
}

# --- Tal -------------------------------------------------------------------
# Tal er det, man handler paa. Et referat, der taber dem, er en fortaelling.
$rxTal = [regex]'\b\d[\d.,]*\s*(?:%|procent|mio|mia|kr|NOK|DKK|EUR|USD|aar|år|måneder|maaneder|uger|dage|timer)?\b'

$rt = @($rxTal.Matches($r) | ForEach-Object { $_.Value.Trim() } | Sort-Object -Unique)
$ft = @($rxTal.Matches($f) | ForEach-Object { $_.Value.Trim() } | Sort-Object -Unique)

$talMangler = $ft | Where-Object { $_ -notin $rt }

Write-Host ""
Write-Host "TAL" -ForegroundColor Cyan
Write-Host ("  i facit    : {0}" -f $ft.Count)
Write-Host ("  i referatet: {0}" -f $rt.Count)
if ($talMangler) {
    Write-Host ("  MANGLER ({0}):" -f @($talMangler).Count) -ForegroundColor Yellow
    Write-Host ("    " + (($talMangler | Select-Object -First 25) -join ', '))
}

# --- Struktur --------------------------------------------------------------
$ro = @([regex]::Matches($r, '(?m)^#{1,3}\s*(.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() })

Write-Host ""
Write-Host "STRUKTUR" -ForegroundColor Cyan
Write-Host ("  overskrifter i referatet: {0}" -f $ro.Count)
foreach ($o in $ro) { Write-Host "    $o" }

# --- Dommen ----------------------------------------------------------------
$andel = 100 * (Ord $r) / (Ord $f)
$navnetab = if ($fn.Count -gt 0) { 100 * @($mangler).Count / $fn.Count } else { 0 }

Write-Host ""
Write-Host "SAMLET" -ForegroundColor Cyan
Write-Host ("  laengde   : {0:N0} % af facit" -f $andel)
Write-Host ("  navnetab  : {0:N0} % af facits navne mangler" -f $navnetab)
Write-Host ("  opfundne  : {0} navne staar ikke i facit" -f @($opfundet).Count)
Write-Host ""

if ($andel -ge 70 -and $navnetab -le 25 -and @($opfundet).Count -le 3) {
    Write-Host "  Brugbart." -ForegroundColor Green
} elseif ($andel -lt 50) {
    Write-Host "  For kort. Under halvdelen af facit — der er tabt indhold." -ForegroundColor Yellow
} elseif ($navnetab -gt 40) {
    Write-Host "  Navnene mangler. Et referat uden deltagere kan ikke bruges." -ForegroundColor Red
} else {
    Write-Host "  Ikke i maal endnu." -ForegroundColor Yellow
}

Write-Host ""
