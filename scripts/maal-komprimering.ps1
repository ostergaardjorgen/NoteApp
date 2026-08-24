<#
.SYNOPSIS
    Måler, hvad det koster i nøjagtighed at komprimere lyden.

.DESCRIPTION
    HVORFOR DEN FINDES

    En times møde fylder 220 MB som ukomprimeret lyd. Zoom oplyser 200 MB i
    timen for VIDEO. Spørgsmålet er, om vi kan lukke det hul uden at ødelægge
    udskriften — og det er et spørgsmål, der skal måles, ikke skønnes.

    HVAD DEN GØR

    For hver oplæsning med kendt facit:

      1. koder lyden til AAC ved de valgte bitrater (Windows' egen Media
         Foundation — ingen ffmpeg, ingen ny binær)
      2. pakker den ud igen til 16 kHz mono PCM, som whisper skal have den
      3. skriver alle udgaver ud MED SPROGET LÅST
      4. måler ordfejlraten mod facitlisten

    SPROGET LÅSES MED VILJE. Uden det kan sprogdetekteringen ramme forskelligt
    på komprimeret og ukomprimeret lyd, og så måler man to ting på én gang.
    Vil man vide, om komprimering påvirker DETEKTERINGEN, er det en anden
    måling.

    SAMMENLIGN MOD DET RÅ ENKELTSPOR. `udskrift_*.txt` er den sammenflettede
    to-spors udskrift MED talermærkater, og mærkaterne tæller med som ekstra
    ord. Første gang blev det overset, og resultatet var, at komprimeret lyd
    så ud til at være BEDRE end ukomprimeret. Et resultat, der vender den
    forventede retning om, er som regel en fejl i målingen.

.PARAMETER Bitrater
    Hvilke bitrater der prøves. Standard 24 og 32.

    Media Foundation klemmer ned til sit maksimum for 16 kHz mono: 48 og 64
    giver samme fil som 32. Det er ikke en fejl i scriptet.

.PARAMETER SpringUdskriftOver
    Genbrug de udskrifter, der allerede ligger. Til at måle igen uden at
    vente på grafikkortet.

.EXAMPLE
    .\maal-komprimering.ps1
#>
[CmdletBinding()]
param(
    [int[]] $Bitrater = @(24, 32),
    [switch] $SpringUdskriftOver
)

$ErrorActionPreference = 'Stop'
$Rod = Split-Path $PSScriptRoot -Parent
$Exe = Join-Path $Rod 'src\NoteApp.Tools\bin\Release\net8.0-windows\noteapp.exe'

if (-not (Test-Path $Exe)) {
    throw "Vaerktoejet er ikke bygget. Koer: dotnet build src\NoteApp.Tools\NoteApp.Tools.csproj -c Release"
}

# De tre oplaesninger med kendt facit. Sproget staar med, fordi det laases.
$Proever = @(
    @{ Mappe = '2026-08-12_15-16_Fase0-oplaesning'
       Facit = 'fase0\oplaesning\testtekst.md'
       Sprog = 'da'
       Navn  = 'Dansk' }

    @{ Mappe = '2026-08-12_17-57_Oplaesning-engelsk'
       Facit = 'fase0\oplaesning\testtekst-engelsk.md'
       Sprog = 'en'
       Navn  = 'Engelsk' }

    @{ Mappe = '2026-08-12_17-45_Oplaesning-blandet'
       Facit = 'fase0\oplaesning\testtekst-blandet.md'
       Sprog = 'da'
       Navn  = 'Blandet dansk-engelsk' }
)

$Data = Join-Path $env:LOCALAPPDATA 'NoteApp'
if ($env:NOTEAPP_DATA) { $Data = $env:NOTEAPP_DATA }
if (Test-Path 'C:\AppNoter') { $Data = 'C:\AppNoter' }

$Resultater = Join-Path $Rod 'fase0\resultater'

function Wer($tekstfil, $facitfil) {
    if (-not (Test-Path $tekstfil)) { return $null }

    # DER LAESES FRA CSV'EN, IKKE FRA SKAERMEN.
    #
    # maal-noejagtighed.ps1 skriver med Write-Host, og Write-Host gaar til
    # VAERTEN - ikke i pipelinen. Alt, hvad man forsoeger at fange, er derfor
    # tomt, uanset hvor tydeligt tallet staar paa skaermen. Tabellen stod tom
    # to gange, foer det gik op for mig.
    #
    # Scriptet gemmer til gengaeld en CSV, og den er den rigtige kilde: den
    # er lavet til at blive laest af noget andet.
    $foer = Get-ChildItem $Resultater -Filter 'noejagtighed_*.csv' -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName

    & (Join-Path $PSScriptRoot 'maal-noejagtighed.ps1') `
        -Transskription $tekstfil -Reference $facitfil | Out-Null

    $navn = [IO.Path]::GetFileNameWithoutExtension($tekstfil)

    $nyeste = Get-ChildItem $Resultater -Filter 'noejagtighed_*.csv' -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $nyeste) { return $null }

    foreach ($r in (Import-Csv $nyeste.FullName)) {
        if ($r.Transskription -eq $navn) {
            return [double](($r.WER) -replace ',', '.')
        }
    }

    return $null
}

$Raekker = @()

foreach ($p in $Proever) {
    $mappe = Join-Path $Data "Optagelser\$($p.Mappe)"
    $wav = Join-Path $mappe 'mikrofon.wav'
    $facit = Join-Path $Rod $p.Facit

    Write-Host ''
    Write-Host "=== $($p.Navn) ===" -ForegroundColor Cyan

    if (-not (Test-Path $wav)) { Write-Host "  optagelsen mangler: $wav" -ForegroundColor Yellow; continue }
    if (-not (Test-Path $facit)) { Write-Host "  facitlisten mangler: $facit" -ForegroundColor Yellow; continue }

    if (-not $SpringUdskriftOver) {
        & $Exe lydproeve $wav @Bitrater | Out-Null

        # Grundlinjen SKAL koeres med samme sprogindstilling som de oevrige.
        & $Exe transskriber $wav --sprog $p.Sprog | Out-Null

        foreach ($b in $Bitrater) {
            $u = Join-Path $mappe "lydproeve\proeve-$b.wav"
            if (Test-Path $u) { & $Exe transskriber $u --sprog $p.Sprog | Out-Null }
        }
    }

    # DET RAA ENKELTSPOR, ikke den sammenflettede udskrift.
    $grund = Wer (Join-Path $mappe 'mikrofon_large-v3.txt') $facit

    $r = [ordered]@{ Proeve = $p.Navn; PCM = $grund }

    foreach ($b in $Bitrater) {
        $r["AAC $b"] = Wer (Join-Path $mappe "lydproeve\proeve-${b}_large-v3.txt") $facit
    }

    $Raekker += [pscustomobject]$r
}

Write-Host ''
Write-Host 'ORDFEJLRATE I PROCENT — lavere er bedre' -ForegroundColor Green
Write-Host ''
$Raekker | Format-Table -AutoSize

Write-Host 'Plads pr. time, ét spor: PCM 109,9 MB · AAC 32 14,0 MB (7,9x) · AAC 24 10,5 MB (10,5x)'
Write-Host ''
Write-Host 'Er forskellen under et halvt procentpoint, er den sandsynligvis stoej.'
Write-Host 'Se om raekkefoelgen er monoton: er 24 bedre end 32 paa noget, maaler man stoej.'
