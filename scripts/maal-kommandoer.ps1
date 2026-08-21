<#
.SYNOPSIS
    Maaler reglen om ENIGHED: handl kun, naar den frie og den grammatikbundne
    udskrift peger paa den samme kommando.

.DESCRIPTION
    Maalt 21-08-2026 viste sig to ting:

    En enkelt dansk kommando, sagt til en tom app, transskriberes daarligt -
    "Opret en opgave" blev til "Opretten opgav". Det er ikke lydlaengden;
    klippene blev polstret med stilhed uden virkning. Det er den sproglige
    sammenhaeng, der mangler.

    En GBNF-grammatik retter det, men indfoerer en vaerre fejl: naar den rammer
    forkert, bliver resultatet ikke volapyk, men EN ANDEN GYLDIG KOMMANDO.
    "Fortsaet optagelsen" blev til "Hold pause" - og ville vaere blevet udfoert.

    De to udskrifter er uenige netop, naar det gaar galt. Derfor: koer begge,
    og handl kun ved enighed. Er de uenige, siger appen "kommando ikke
    forstaaet" - et aerligt afslag, som brugeren kan rette sig efter.

    Det her script maaler, om den regel holder paa alle 50 saetninger.

.PARAMETER Mappe
    Optagelsen med de 50 oplaeste saetninger.
#>

param(
  [Parameter(Mandatory=$true)][string] $Mappe,
  [string] $Model  = 'C:\NoteApp\models\ggml-large-v3.bin',
  [string] $Facit  = 'C:\NoteApp\doc\maaling-diktering.md',

  # Hvor meget den frie udskrift maa afvige fra kommandoen og stadig regnes
  # som enig. 0,5 = halvdelen af ordene skal passe.
  [double] $Enighed = 0.5
)

$ErrorActionPreference = 'Stop'

$cli = 'C:\AppNoter\motor\whisper\bin\Release\whisper-cli.exe'
$wav = Join-Path $Mappe 'mikrofon.wav'
$ud  = Join-Path $Mappe 'kommandoer'
New-Item -ItemType Directory -Force -Path $ud | Out-Null

# ------------------------------------------------------------------- facit
$saetninger = @()
foreach ($l in (Get-Content $Facit -Encoding UTF8)) {
    if ($l -match '^(\d{1,2})\.\s+(.+?)\s*$') {
        $nr = [int]$Matches[1]
        if ($nr -ge 1 -and $nr -le 50) {
            $saetninger += [pscustomobject]@{
                Nr = $nr; Tekst = $Matches[2]; Kommando = ($nr -le 25)
            }
        }
    }
}
$saetninger = $saetninger | Sort-Object Nr -Unique
if ($saetninger.Count -ne 50) { throw "Facit blev laest som $($saetninger.Count) saetninger." }

$kommandoer = ($saetninger | Where-Object Kommando).Tekst

# --------------------------------------------------------------- grammatik
$g = "root ::= kommando`n`nkommando ::= " +
     (($kommandoer | ForEach-Object { '"' + $_ + '"' }) -join " | ")
$gbnf = Join-Path $ud 'kommandoer.gbnf'
[IO.File]::WriteAllText($gbnf, $g, (New-Object Text.UTF8Encoding($false)))

# ---------------------------------------------------------------- opdeling
#
# DE 49 LAENGSTE PAUSER - IKKE EN TAERSKEL.
#
# Foerste udgave delte op, hvor der var stille i mere end X vinduer, og X
# skulle findes ved at proeve sig frem. Ved 33 blev det 49 klip, ved 25 blev
# det 51, og ved 31 blev det 50 - men kun fordi een saetning blev delt i to og
# to andre smeltede sammen. Alignementet skred fra klip 9, og maalingen maalte
# noget andet, end den troede.
#
# Antallet er kendt: der er 50 saetninger, altsaa 49 grænser. Saa find ALLE
# pauser, sortér dem efter laengde, og tag de 49 laengste. Saa kan det ikke
# blive 49 eller 51, og graenserne er de mest sandsynlige, der findes.
$b = [IO.File]::ReadAllBytes($wav)

$data = 12
while ($data -lt 400) {
    $id = [Text.Encoding]::ASCII.GetString($b, $data, 4)
    $sz = [BitConverter]::ToInt32($b, $data + 4)
    if ($id -eq 'data') { $data += 8; break }
    $data += 8 + $sz + ($sz % 2)
}

$rate   = 16000
$vindue = 480
$antal  = [int](($b.Length - $data) / 2)
$sidste = $b.Length - 2
$n      = [Math]::Floor($antal / $vindue)

$styrke = New-Object 'double[]' $n
for ($v = 0; $v -lt $n; $v++) {
    $sum = 0.0; $t = 0
    $fra = $data + $v * $vindue * 2
    for ($i = 0; $i -lt $vindue; $i += 4) {
        $ved = $fra + $i * 2
        if ($ved -gt $sidste) { break }
        $sum += [Math]::Abs([BitConverter]::ToInt16($b, $ved)); $t++
    }
    $styrke[$v] = if ($t) { $sum / $t } else { 0 }
}

$top = ($styrke | Measure-Object -Maximum).Maximum
$gr  = $top * 0.03

# Alle sammenhaengende stille strækninger.
$pauser = New-Object 'System.Collections.Generic.List[object]'
$start = -1
for ($v = 0; $v -lt $n; $v++) {
    if ($styrke[$v] -le $gr) { if ($start -lt 0) { $start = $v } }
    elseif ($start -ge 0) {
        $pauser.Add([pscustomobject]@{ Fra = $start; Til = ($v - 1); Laengde = ($v - $start) })
        $start = -1
    }
}

# Pauser i selve begyndelsen og slutningen er ikke graenser mellem saetninger.
$indre = $pauser | Where-Object { $_.Fra -gt 0 -and $_.Til -lt ($n - 1) }

$graenser = $indre | Sort-Object Laengde -Descending | Select-Object -First 49 |
            Sort-Object Fra

Write-Host ("Lyd: {0:N0} sek · {1} pauser · korteste valgte graense: {2:N2} sek" -f
            ($antal / $rate), $pauser.Count,
            (($graenser | Measure-Object Laengde -Minimum).Minimum * 0.03))

# Klippene ligger mellem graenserne, med et snit midt i hver pause.
$snit = @(0) + ($graenser | ForEach-Object { [int](($_.Fra + $_.Til) / 2) }) + @($n - 1)

function Gem-Klip($nr, $fraV, $tilV) {
    $fra = [Math]::Max(0, $fraV * $vindue * 2)
    $laengde = [Math]::Min($antal * 2 - $fra, ($tilV - $fraV + 1) * $vindue * 2)

    $ms = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($ms)
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int]($laengde + 36))
    $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $w.Write([int]16)
    $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$rate)
    $w.Write([int]($rate * 2)); $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]$laengde)
    $w.Write($b, $data + $fra, $laengde); $w.Flush()

    $sti = Join-Path $ud ("{0:00}.wav" -f $nr)
    [IO.File]::WriteAllBytes($sti, $ms.ToArray())
    return $sti
}

$filer = @()
for ($i = 0; $i -lt 50; $i++) { $filer += Gem-Klip ($i + 1) $snit[$i] $snit[$i + 1] }

Write-Host ("{0} klip skrevet" -f $filer.Count) -ForegroundColor Cyan

# -------------------------------------------------------------------- maal
function Rens($s) {
    if (-not $s) { return "" }
    $s = $s.ToLowerInvariant() -replace '[^\wæøåÆØÅ ]', ' '
    return ($s -replace '\s+', ' ').Trim()
}

function Ordfejl($a1, $b1) {
    $a = (Rens $a1) -split ' '; $c = (Rens $b1) -split ' '
    if ($a.Count -eq 0 -or $a[0] -eq '') { return 1 }
    $d = New-Object 'int[,]' ($a.Count + 1), ($c.Count + 1)
    for ($i = 0; $i -le $a.Count; $i++) { $d[$i, 0] = $i }
    for ($j = 0; $j -le $c.Count; $j++) { $d[0, $j] = $j }
    # MELLEMVARIABLER, IKKE REGNESTYKKER I INDEKSET.
    #
    # PowerShell kan ikke laese "$d[$i-1, $j]" - heller ikke med parenteser om
    # regnestykket. Fejlen er "Missing ']' after array index expression", og
    # den kommer ved indlaesningen, saa scriptet doer foer foerste linje koeres.
    for ($i = 1; $i -le $a.Count; $i++) {
        $im = $i - 1

        for ($j = 1; $j -le $c.Count; $j++) {
            $jm = $j - 1

            $p = if ($a[$im] -eq $c[$jm]) { 0 } else { 1 }

            $slet   = $d[$im, $j] + 1
            $indsat = $d[$i, $jm] + 1
            $byttet = $d[$im, $jm] + $p

            $d[$i, $j] = [Math]::Min([Math]::Min($slet, $indsat), $byttet)
        }
    }
    return $d[$a.Count, $c.Count] / [double]$a.Count
}

function Koer($fil, $navn, $medGrammatik) {
    $base = Join-Path $ud $navn
    $tidligere = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    if ($medGrammatik) {
        & $cli -m $Model -f $fil -l da -otxt -of $base -mc 0 --no-prints `
               --grammar $gbnf --grammar-rule root --grammar-penalty 100 2>$null | Out-Null
    } else {
        & $cli -m $Model -f $fil -l da -otxt -of $base -mc 0 --no-prints 2>$null | Out-Null
    }

    $ErrorActionPreference = $tidligere

    # En TOM fil giver null fra -Raw, ikke en tom streng - og saa fejler
    # .Trim(). Det sker rutinemaessigt her: grammatikken afviser lyd, den ikke
    # kan tvinge ind i kommandosaettet, og efterlader en tom udskrift.
    if (-not (Test-Path "$base.txt")) { return "" }

    $t = Get-Content "$base.txt" -Raw -Encoding UTF8
    if ($null -eq $t) { return "" }

    return $t.Trim()
}

$alt = @()
$sw = [Diagnostics.Stopwatch]::StartNew()

for ($i = 0; $i -lt 50; $i++) {
    $nr = $i + 1
    $f  = $saetninger | Where-Object Nr -eq $nr

    $fri  = Koer $filer[$i] ("f{0:00}" -f $nr) $false
    $bnd  = Koer $filer[$i] ("b{0:00}" -f $nr) $true

    # Grammatikken tvinger udgangen ind i kommandosaettet. Find hvilken.
    $bundet = $null
    if ($bnd) {
        $bundet = $kommandoer | Sort-Object { Ordfejl $_ $bnd } | Select-Object -First 1
        if ((Ordfejl $bundet $bnd) -gt 0.34) { $bundet = $null }
    }

    # ER DE ENIGE? Den frie udskrift skal ligne DEN SAMME kommando.
    $enige = $false
    if ($bundet) { $enige = (Ordfejl $bundet $fri) -le $Enighed }

    $handling = if ($enige) { $bundet } else { $null }

    $alt += [pscustomobject]@{
        Nr = $nr; Kommando = $f.Kommando; Facit = $f.Tekst
        Fri = $fri; Bundet = $bundet; Handling = $handling
        Rigtig = ($handling -eq $f.Tekst)
    }

    $m = if (-not $handling) { 'afvist ' }
         elseif ($handling -eq $f.Tekst) { 'UDFOERT' }
         else { 'FORKERT' }

    "{0} {1,2} {2}  {3,-40} | fri: {4}" -f $m, $nr,
        $(if ($f.Kommando) { 'K' } else { '.' }),
        $(if ($handling) { $handling } else { '-' }), $fri
}

# ---------------------------------------------------------------- opgoerelse
$k = $alt | Where-Object Kommando
$a = $alt | Where-Object { -not $_.Kommando }

$udfoert  = ($k | Where-Object { $_.Handling }).Count
$rigtige  = ($k | Where-Object Rigtig).Count
$forkerte = ($k | Where-Object { $_.Handling -and -not $_.Rigtig }).Count
$falske   = ($a | Where-Object { $_.Handling }).Count

Write-Host ""
Write-Host "==================== OPGOERELSE ====================" -ForegroundColor Cyan
Write-Host ("tid                              : {0:N0} sekunder" -f $sw.Elapsed.TotalSeconds)
Write-Host ""
Write-Host ("KOMMANDOER (25)")
Write-Host ("  udfoert rigtigt                : {0}" -f $rigtige)
Write-Host ("  udfoert FORKERT                : {0}" -f $forkerte)
Write-Host ("  afvist - ikke forstaaet        : {0}" -f (25 - $udfoert))
Write-Host ""
Write-Host ("ALMINDELIGE SAETNINGER (25)")
Write-Host ("  FALSK ACCEPT - udloeste noget  : {0}   <- skal vaere 0" -f $falske)
Write-Host ("  korrekt ignoreret              : {0}" -f (25 - $falske))

$alt | Export-Csv (Join-Path $ud 'resultat.csv') -NoTypeInformation -Encoding UTF8
Write-Host "`nAlt ligger i $ud" -ForegroundColor Green
