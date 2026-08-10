<#
.SYNOPSIS
    Fase 0 feasibility gate for NoteApp: kører en optagelse gennem whisper.cpp
    i både medium og large-v3 på dansk, med og uden IAM-ordliste, og måler
    hvor lang tid transskriptionen tager i forhold til mødets længde.

.DESCRIPTION
    Det afgørende tal er realtidsfaktoren (RTF): transskriptionstid delt med
    lydens længde. Er den over 1,0 skal transskription planlægges som natjob
    frem for noget du venter på. Det er dét, denne gate skal afgøre.

.EXAMPLE
    .\koer-fase0-test.ps1 -Session '2026-08-08_14-30_Kundemoede'

.EXAMPLE
    # Kun large-v3, og kun mikrofonsporet
    .\koer-fase0-test.ps1 -Session '2026-08-08_14-30' -Modeller large-v3 -Spor mikrofon
#>
[CmdletBinding()]
param(
    # Mappenavn under fase0\optagelser\. Udelades den, bruges den nyeste optagelse.
    [string] $Session,

    [ValidateSet('medium', 'large-v3')]
    [string[]] $Modeller = @('medium', 'large-v3'),

    [ValidateSet('mikrofon', 'loopback')]
    [string[]] $Spor = @('mikrofon', 'loopback'),

    # Kør både med og uden ordliste, så gevinsten ved feature #3 kan aflæses.
    [switch] $SpringOrdlisteOver,

    # Tving CPU-kørsel — brug til at måle hvad GPU'en faktisk er værd.
    [switch] $KunCpu
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# --- Find whisper-binæren -------------------------------------------------
# whisper-cli.exe FØRST: main.exe findes stadig i udgivelsen, men er forældet
# og skriver kun en advarsel uden at transskribere noget som helst. Alfabetisk
# sortering ville ellers vælge den.
$whisper = Get-ChildItem (Join-Path $root 'tools\whisper') -Recurse -Filter 'whisper-cli.exe' |
    Select-Object -First 1
if (-not $whisper) {
    $whisper = Get-ChildItem (Join-Path $root 'tools\whisper') -Recurse -Filter 'main.exe' |
        Select-Object -First 1
}
if (-not $whisper) {
    throw "Fandt ingen whisper-cli.exe under $root\tools\whisper."
}
Write-Host "Whisper : $($whisper.FullName)" -ForegroundColor DarkGray

# --- Find optagelsen ------------------------------------------------------
$optagelser = Join-Path $root 'fase0\optagelser'
if ($Session) {
    $sessionDir = Join-Path $optagelser $Session
} else {
    $nyeste = Get-ChildItem $optagelser -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $nyeste) { throw "Ingen optagelser i $optagelser. Kør Fase0Recorder først." }
    $sessionDir = $nyeste.FullName
}
if (-not (Test-Path $sessionDir)) { throw "Findes ikke: $sessionDir" }

# --- Ordliste (feature #3: Whisper initial_prompt) ------------------------
$ordlistePath = Join-Path $root 'ordliste.txt'
$ordliste = if (Test-Path $ordlistePath) {
    ((Get-Content $ordlistePath -Raw -Encoding UTF8) -replace '\s+', ' ').Trim()
} else { '' }

$varianter = @(@{ Navn = 'medOrdliste'; Prompt = $ordliste })
if (-not $SpringOrdlisteOver) {
    $varianter += @{ Navn = 'udenOrdliste'; Prompt = '' }
}
if (-not $ordliste) {
    Write-Warning "ordliste.txt er tom — kører kun uden ordliste."
    $varianter = @(@{ Navn = 'udenOrdliste'; Prompt = '' })
}

$udDir = Join-Path $root 'fase0\transskriptioner'
New-Item -ItemType Directory -Force $udDir | Out-Null

function Get-WavSekunder([string] $sti) {
    # WAV-header: samplerate på byte 24, bytes/sekund på byte 28, datastørrelse
    # findes ved at lede efter 'data'-chunken. Rækker til 16 kHz mono PCM16.
    $fs = [System.IO.File]::OpenRead($sti)
    try {
        $h = New-Object byte[] 64
        [void]$fs.Read($h, 0, 64)
        $bytesPerSec = [BitConverter]::ToUInt32($h, 28)
        if ($bytesPerSec -eq 0) { return 0 }
        return [math]::Round(($fs.Length - 44) / $bytesPerSec, 1)
    } finally { $fs.Dispose() }
}

$resultater = @()

foreach ($sporNavn in $Spor) {
    $wav = Join-Path $sessionDir "$sporNavn.wav"
    if (-not (Test-Path $wav)) {
        Write-Host "  springer over: $sporNavn.wav findes ikke (fysisk møde?)" -ForegroundColor DarkGray
        continue
    }

    $lydSek = Get-WavSekunder $wav
    $lydMin = [math]::Round($lydSek / 60, 1)
    Write-Host ""
    Write-Host "=== $sporNavn ($lydMin min lyd) ===" -ForegroundColor Cyan

    foreach ($model in $Modeller) {
        $modelFil = Join-Path $root "models\ggml-$model.bin"
        if (-not (Test-Path $modelFil)) {
            Write-Warning "Model mangler: $modelFil — springer over."
            continue
        }

        foreach ($variant in $varianter) {
            $navn = "$(Split-Path $sessionDir -Leaf)_${sporNavn}_${model}_$($variant.Navn)"
            $ud = Join-Path $udDir $navn

            $argumenter = @(
                '-m', $modelFil
                '-f', $wav
                '-l', 'da'
                '-otxt'
                '-oj'
                '-of', $ud
                '-pp'
            )
            if ($variant.Prompt) { $argumenter += @('--prompt', $variant.Prompt) }
            if ($KunCpu) { $argumenter += '-ng' }

            Write-Host ("  {0,-9} {1,-13} ... " -f $model, $variant.Navn) -NoNewline

            # whisper.cpp skriver AL fremdrift til stderr. Med
            # $ErrorActionPreference = 'Stop' pakker PowerShell 5.1 hver
            # stderr-linje fra en native exe ind som en terminerende
            # NativeCommandError — scriptet ville dø på den første
            # statuslinje. Sænk kun preferencen omkring selve kaldet.
            $forrigeEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $ur = [Diagnostics.Stopwatch]::StartNew()
            & $whisper.FullName @argumenter *> "$ud.log"
            $ur.Stop()
            $exitKode = $LASTEXITCODE
            $ErrorActionPreference = $forrigeEap

            $ok = ($exitKode -eq 0) -and (Test-Path "$ud.txt")
            $rtf = if ($lydSek -gt 0) { [math]::Round($ur.Elapsed.TotalSeconds / $lydSek, 2) } else { 0 }

            if ($ok) {
                Write-Host ("{0,6:0.0}s   RTF {1}" -f $ur.Elapsed.TotalSeconds, $rtf) -ForegroundColor Green
            } else {
                Write-Host "FEJLEDE (se $ud.log)" -ForegroundColor Red
            }

            $resultater += [pscustomobject]@{
                Spor        = $sporNavn
                Model       = $model
                Variant     = $variant.Navn
                LydMin      = [math]::Round($lydSek / 60, 1)
                TidSek      = [math]::Round($ur.Elapsed.TotalSeconds, 1)
                RTF         = $rtf
                Natjob      = if ($rtf -gt 1) { 'JA' } else { 'nej' }
                Status      = if ($ok) { 'OK' } else { 'FEJL' }
                Transskript = if ($ok) { "$ud.txt" } else { "$ud.log" }
            }
        }
    }
}

if (-not $resultater) { throw "Ingen kørsler gennemført." }

Write-Host ""
Write-Host "=== Resultat ===" -ForegroundColor Cyan
$resultater | Format-Table Spor, Model, Variant, LydMin, TidSek, RTF, Natjob, Status -AutoSize

$rapport = Join-Path $root "fase0\resultater\rtf_$(Get-Date -Format 'yyyy-MM-dd_HH-mm').csv"
New-Item -ItemType Directory -Force (Split-Path $rapport) | Out-Null
$resultater | Export-Csv $rapport -NoTypeInformation -Encoding UTF8
Write-Host "Gemt: $rapport"

$vaerste = ($resultater | Where-Object Status -eq 'OK' | Measure-Object RTF -Maximum).Maximum
Write-Host ""
if ($vaerste -gt 1) {
    Write-Host "GATE: RTF op til $vaerste — transskription skal køre som natjob, ikke som noget du venter på." -ForegroundColor Yellow
} else {
    Write-Host "GATE: RTF max $vaerste — transskription kan køre mens du venter." -ForegroundColor Green
}
Write-Host "Læs transskriptionerne igennem og vurdér navne, fagtermer og forkortelser." -ForegroundColor Yellow
