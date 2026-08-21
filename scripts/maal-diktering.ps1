<#
.SYNOPSIS
    Maaler, om whisper kan hoere danske kommandoer godt nok til stemmestyring.

.DESCRIPTION
    Facit staar i doc\maaling-diktering.md: 25 kommandoer og 25 almindelige
    saetninger, laest op i raekkefoelge med pauser imellem.

    Scriptet deler optagelsen op paa stilheden, koerer hvert klip gennem hver
    model, og holder resultatet op mod facit.

    HVORFOR DER MAALES PAA KLIP OG IKKE PAA HELE FILEN

    En kommando er een ytring. Skrives hele optagelsen ud paa een gang, faar
    whisper 50 saetningers sammenhaeng at gaette ud fra - og det er praecis den
    sammenhaeng, den ikke har, naar nogen siger "start optagelsen" til en tom
    app. Maalingen skal ligne den situation, der skal virke.

.PARAMETER Mappe
    Optagelsens mappe under C:\AppNoter\Optagelser.

.PARAMETER Modeller
    Hvilke whisper-modeller der proeves. Standard: small, medium, large-v3.
#>

param(
  [Parameter(Mandatory=$true)][string] $Mappe,
  [string[]] $Modeller = @('small','medium','large-v3'),
  [string]   $Facit    = 'C:\NoteApp\doc\maaling-diktering.md',

  # Hvor mange 30 ms-vinduer i traek der skal vaere stille, foer en saetning
  # regnes for slut. 33 er ca. et sekund.
  [int]    $Stille  = 33,

  # Hvor stor en del af klippets stoerste udsving der regnes som stilhed.
  [double] $Graense = 0.04,

  # Del kun op - spring maalingen over. Til at finde de rigtige to tal ovenfor
  # uden at vente paa tre modeller.
  [switch] $KunDel
)

$ErrorActionPreference = 'Stop'

$cli = 'C:\AppNoter\motor\whisper\bin\Release\whisper-cli.exe'
if (-not (Test-Path $cli)) { throw "Finder ikke whisper: $cli" }

$wav = Join-Path $Mappe 'mikrofon.wav'
if (-not (Test-Path $wav)) { throw "Finder ikke lyden: $wav" }

$ud = Join-Path $Mappe 'diktering'
New-Item -ItemType Directory -Force -Path $ud | Out-Null

# ---------------------------------------------------------------- facit
$linjer = Get-Content $Facit -Encoding UTF8
$saetninger = @()
foreach ($l in $linjer) {
    if ($l -match '^(\d{1,2})\.\s+(.+?)\s*$') {
        $nr = [int]$Matches[1]
        if ($nr -ge 1 -and $nr -le 50) {
            $saetninger += [pscustomobject]@{
                Nr      = $nr
                Tekst   = $Matches[2]
                Kommando = ($nr -le 25)
            }
        }
    }
}
$saetninger = $saetninger | Sort-Object Nr -Unique
Write-Host ("Facit: {0} saetninger ({1} kommandoer)" -f $saetninger.Count,
            ($saetninger | Where-Object Kommando).Count)

if ($saetninger.Count -ne 50) {
    throw "Facit skulle vaere 50 saetninger, men blev laest som $($saetninger.Count)."
}

# ------------------------------------------------- del optagelsen paa stilhed
#
# Der laeses raa 16-bit mono. Et vindue paa 30 ms regnes som stilhed, naar dets
# gennemsnitlige udsving er under en broekdel af klippets samlede maksimum -
# altsaa relativt, ikke en fast graense. En fast graense virker kun paa den
# mikrofon, den blev sat efter.
$b = [IO.File]::ReadAllBytes($wav)

$data = 12
while ($data -lt 400) {
    $id = [Text.Encoding]::ASCII.GetString($b, $data, 4)
    $sz = [BitConverter]::ToInt32($b, $data + 4)
    if ($id -eq 'data') { $data += 8; break }
    $data += 8 + $sz + ($sz % 2)
}

$rate    = 16000
$vindue  = [int]($rate * 0.03)          # 30 ms
$antal   = [int](($b.Length - $data) / 2)

# Der laeses aldrig ud over den sidste hele proeve. Regnestykket paa antal
# vinduer kan ligge eet vindue for hoejt, naar filen ikke gaar op i 30 ms, og
# saa laeser ToInt16 ud over bufferen. Graensen staettes eksplicit frem for at
# blive regnet ud - den slags fejl viser sig som et nedbrud, ikke som et skaevt
# tal.
$sidsteByte = $b.Length - 2

$styrke = New-Object 'double[]' ([Math]::Floor($antal / $vindue))

for ($v = 0; $v -lt $styrke.Length; $v++) {
    $sum = 0.0
    $talt = 0
    $fra = $data + $v * $vindue * 2

    for ($i = 0; $i -lt $vindue; $i += 4) {
        $ved = $fra + $i * 2
        if ($ved -gt $sidsteByte) { break }

        $sum += [Math]::Abs([BitConverter]::ToInt16($b, $ved))
        $talt++
    }

    $styrke[$v] = if ($talt -gt 0) { $sum / $talt } else { 0 }
}

$top     = ($styrke | Measure-Object -Maximum).Maximum
$graense = $top * $Graense
$stille  = $Stille

Write-Host ("Lyd: {0:N0} sekunder, {1} vinduer, graense {2:N0} af {3:N0}" -f
            ($antal / $rate), $styrke.Length, $graense, $top)

# NAVNGIVNE FELTER OG IKKE PAR I ET ARRAY.
#
# "$klip += , @($a, $b)" ser rigtigt ud og virker, indtil PowerShell flader
# arrayet ud - og saa er $klip[$i][0] pludselig et helt array frem for et tal.
# Fejlen viser sig langt fra aarsagen, som "does not contain a method
# op_Subtraction".
$klip = New-Object 'System.Collections.Generic.List[object]'
$start = -1
$tavse = 0

for ($v = 0; $v -lt $styrke.Length; $v++) {
    if ($styrke[$v] -gt $graense) {
        if ($start -lt 0) { $start = $v }
        $tavse = 0
    }
    elseif ($start -ge 0) {
        $tavse++
        if ($tavse -ge $stille) {
            $klip.Add([pscustomobject]@{ Fra = $start; Til = ($v - $tavse) })
            $start = -1
            $tavse = 0
        }
    }
}
if ($start -ge 0) {
    $klip.Add([pscustomobject]@{ Fra = $start; Til = ($styrke.Length - 1) })
}

Write-Host ("Fundet {0} klip" -f $klip.Count) -ForegroundColor Cyan

if ($klip.Count -ne 50) {
    Write-Warning ("Der blev fundet $($klip.Count) klip, ikke 50. " +
        "Enten er der laest for hurtigt mellem saetningerne, eller ogsaa er en " +
        "saetning delt i to. Justér `$stille eller `$graense i scriptet, " +
        "eller optag igen med tydeligere pauser.")
}

# ------------------------------------------------------------ skriv klippene
function Gem-Klip($nr, $fraVindue, $tilVindue) {
    $fra = $fraVindue * $vindue * 2
    $laengde = ($tilVindue - $fraVindue + 1) * $vindue * 2

    # Lidt luft i begge ender, saa foerste og sidste stavelse ikke klippes af.
    $luft = $rate * 2 / 5
    $fra = [Math]::Max(0, $fra - $luft)
    $laengde = [Math]::Min($antal * 2 - $fra, $laengde + $luft * 2)

    $ms = New-Object IO.MemoryStream
    $w  = New-Object IO.BinaryWriter($ms)
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([int]($laengde + 36))
    $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $w.Write([int]16)
    $w.Write([int16]1); $w.Write([int16]1); $w.Write([int]$rate)
    $w.Write([int]($rate * 2)); $w.Write([int16]2); $w.Write([int16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([int]$laengde)
    $w.Write($b, $data + $fra, $laengde)
    $w.Flush()

    $sti = Join-Path $ud ("{0:00}.wav" -f $nr)
    [IO.File]::WriteAllBytes($sti, $ms.ToArray())
    return $sti
}

$filer = @()
for ($i = 0; $i -lt $klip.Count; $i++) {
    $filer += Gem-Klip ($i + 1) $klip[$i].Fra $klip[$i].Til
}

# ------------------------------------------------------------------- maal
function Rens($s) {
    $s = $s.ToLowerInvariant()
    $s = $s -replace '[^\wæøåÆØÅ ]', ' '
    return ($s -replace '\s+', ' ').Trim()
}

# Ordfejlrate: hvor mange ord skal aendres for at komme fra det ene til det
# andet, delt med facits antal ord. Standardmaalet for talegenkendelse.
function Ordfejl($facit, $hoert) {
    $a = (Rens $facit) -split ' '
    $c = (Rens $hoert) -split ' '
    if ($a.Count -eq 0) { return 0 }

    # PARENTESERNE OM $i-1 ER IKKE PYNT.
    #
    # PowerShell laeser "$d[$i-1, $j]" som et forsoeg paa at trakke 1 fra hele
    # indekset og fejler med "Missing ']' after array index expression".
    # Indeksudtryk med regnestykker i skal have deres egne parenteser.
    $d = New-Object 'int[,]' ($a.Count + 1), ($c.Count + 1)

    for ($i = 0; $i -le $a.Count; $i++) { $d[$i, 0] = $i }
    for ($j = 0; $j -le $c.Count; $j++) { $d[0, $j] = $j }

    for ($i = 1; $i -le $a.Count; $i++) {
        for ($j = 1; $j -le $c.Count; $j++) {
            $pris = if ($a[($i - 1)] -eq $c[($j - 1)]) { 0 } else { 1 }

            $slet   = $d[($i - 1), $j] + 1
            $indsat = $d[$i, ($j - 1)] + 1
            $byttet = $d[($i - 1), ($j - 1)] + $pris

            $d[$i, $j] = [Math]::Min([Math]::Min($slet, $indsat), $byttet)
        }
    }

    return $d[$a.Count, $c.Count] / [double]$a.Count
}

$alt = @()

foreach ($m in $Modeller) {
    $modelfil = @(
        "C:\NoteApp\models\ggml-$m.bin",
        "C:\AppNoter\motor\modeller\ggml-$m.bin"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $modelfil) {
        Write-Warning "Springer $m over - modelfilen findes ikke."
        continue
    }

    Write-Host "`n=== $m ===" -ForegroundColor Cyan
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $rigtige = 0
    $wer = @()

    for ($i = 0; $i -lt $filer.Count; $i++) {
        $nr = $i + 1
        $f  = $saetninger | Where-Object Nr -eq $nr
        if (-not $f) { continue }

        $base = Join-Path $ud ("{0:00}-{1}" -f $nr, $m)
        # whisper skriver ALT til stderr, ogsaa naar det gaar godt. Med
        # ErrorActionPreference = Stop ville den foerste linje om CUDA-kortet
        # afbryde maalingen som en fejl.
        $tidligere = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $cli -m $modelfil -f $filer[$i] -l da -otxt -of $base -mc 0 --no-prints 2>$null | Out-Null
        $ErrorActionPreference = $tidligere

        $hoert = if (Test-Path "$base.txt") { (Get-Content "$base.txt" -Raw -Encoding UTF8).Trim() } else { "" }
        $fejl  = Ordfejl $f.Tekst $hoert
        $wer  += $fejl
        if ($fejl -eq 0) { $rigtige++ }

        $maerke = if ($fejl -eq 0) { 'ok ' } elseif ($fejl -le 0.34) { '~  ' } else { 'FEJL' }
        $slags  = if ($f.Kommando) { 'K' } else { '.' }

        "{0} {1,2} {2} {3,5:P0}  {4}" -f $maerke, $nr, $slags, $fejl, $hoert

        $alt += [pscustomobject]@{
            Model = $m; Nr = $nr; Kommando = $f.Kommando
            Facit = $f.Tekst; Hoert = $hoert; Wer = $fejl
        }
    }

    $k = $alt | Where-Object { $_.Model -eq $m -and $_.Kommando }
    $a = $alt | Where-Object { $_.Model -eq $m -and -not $_.Kommando }

    Write-Host ""
    Write-Host ("  {0}: {1:N1} sekunder i alt" -f $m, $sw.Elapsed.TotalSeconds)
    Write-Host ("  kommandoer ordret rigtigt : {0} af {1}" -f
                ($k | Where-Object { $_.Wer -eq 0 }).Count, $k.Count)
    Write-Host ("  kommandoer under 34 % fejl: {0} af {1}" -f
                ($k | Where-Object { $_.Wer -le 0.34 }).Count, $k.Count)
    Write-Host ("  ordfejlrate, kommandoer   : {0:P1}" -f
                (($k | Measure-Object Wer -Average).Average))
    Write-Host ("  ordfejlrate, almindelige  : {0:P1}" -f
                (($a | Measure-Object Wer -Average).Average))
}

$alt | Export-Csv (Join-Path $ud 'resultat.csv') -NoTypeInformation -Encoding UTF8
Write-Host "`nAlt ligger i $ud" -ForegroundColor Green
