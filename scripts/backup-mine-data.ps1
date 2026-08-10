<#
.SYNOPSIS
    Tager backup af dine NoteApp-data: ordbog, indlærte rettelser, optagelser,
    noter og transskriptioner.

.DESCRIPTION
    Scriptet kopierer lokalt og sender ingenting nogen steder hen. Der er
    ingen netværkskald, ingen skytjeneste og ingen telemetri i det. Vælger du
    selv en netværkssti som destination, siger scriptet det højt først.

    Kilden er %LOCALAPPDATA%\NoteApp (eller NOTEAPP_DATA, hvis den er sat).
    Det er den samme mappe, appen bruger, og den ligger med vilje uden for
    kode-repoet, så dine data ikke kan komme med i en git-push.

.PARAMETER Destination
    Hvor arkivet skal ligge. Standard er %USERPROFILE%\NoteApp-backup — altså
    på din egen maskine.

.PARAMETER Behold
    Antal arkiver der bevares. Ældre slettes. Standard 8.

.EXAMPLE
    .\backup-mine-data.ps1

.EXAMPLE
    .\backup-mine-data.ps1 -Destination 'E:\Krypteret\NoteApp' -Behold 20
#>
[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $env:USERPROFILE 'NoteApp-backup'),
    [int]    $Behold = 8,

    # Eksplicit datamappe. Planlagte opgaver SKAL sætte denne: en opgave kan
    # køre med et andet miljø end den session der oprettede den, og så peger
    # LOCALAPPDATA et andet sted hen.
    [string] $DataMappe,

    [switch] $Stille
)

$ErrorActionPreference = 'Stop'

# En planlagt opgave der fejler stille er varre end ingen backup: man tror
# man er daekket, indtil man ikke er. Alt der gaar galt skal ende i loggen,
# ogsaa naar der ikke er et vindue at skrive til.
$fejlLogRod = $DataMappe
if ([string]::IsNullOrWhiteSpace($fejlLogRod)) { $fejlLogRod = $env:NOTEAPP_DATA }
if ([string]::IsNullOrWhiteSpace($fejlLogRod)) { $fejlLogRod = Join-Path $env:LOCALAPPDATA 'NoteApp' }
$fejlLog = Join-Path $fejlLogRod 'log\backup.log'

trap {
    try {
        New-Item -ItemType Directory -Force (Split-Path $fejlLog) | Out-Null
        $besked = '{0}  FEJL: {1} (linje {2})' -f (Get-Date -Format 's'),
                  $_.Exception.Message, $_.InvocationInfo.ScriptLineNumber
        Add-Content -Path $fejlLog -Value $besked -Encoding UTF8
    } catch { }
    Write-Error $_
    exit 1
}

function Skriv {
    param([string] $Tekst, [string] $Farve = 'Gray')
    if (-not $Stille) { Write-Host $Tekst -ForegroundColor $Farve }
}

# --- Kilde ----------------------------------------------------------------
$kilde = $DataMappe
if ([string]::IsNullOrWhiteSpace($kilde)) { $kilde = $env:NOTEAPP_DATA }
if ([string]::IsNullOrWhiteSpace($kilde)) { $kilde = Join-Path $env:LOCALAPPDATA 'NoteApp' }
$kilde = [IO.Path]::GetFullPath($kilde)

if (-not (Test-Path $kilde)) {
    throw "Datamappen findes ikke: $kilde`nEr appen kørt mindst én gang?"
}

# En planlagt opgave kan koere med et andet miljoe end den session, opgaven
# blev oprettet fra — og saa peger LOCALAPPDATA et andet sted hen. Resultatet
# er en backup der ser ud til at lykkes, men indeholder en naesten tom mappe.
# Derfor: kraev at ordbogen faktisk er der, foer vi kalder det en backup.
$ordbog = Join-Path $kilde 'learning.db'
$moeder = Join-Path $kilde 'moeder'
if (-not (Test-Path $ordbog) -and -not (Test-Path $moeder)) {
    throw ("Datamappen $kilde indeholder hverken learning.db eller moeder\.`n" +
           "Det ligner den forkerte mappe. Er backup planlagt, saa saet " +
           "NOTEAPP_DATA eksplicit i den planlagte opgave.")
}

# --- Destination ----------------------------------------------------------
$destinationFuld = [IO.Path]::GetFullPath($Destination)

# Netvaerksstier forlader maskinen. Det er dit valg, men det skal siges hoejt.
if ($destinationFuld.StartsWith('\\')) {
    Skriv ""
    Skriv "BEMÆRK: destinationen er en netværkssti." 'Yellow'
    Skriv "  $destinationFuld" 'Yellow'
    Skriv "  Arkivet forlader dermed denne maskine. Det er ikke krypteret." 'Yellow'
    Skriv "  Vil du holde alt lokalt, så brug en sti på C: eller en ekstern disk." 'Yellow'
    Skriv ""
}

New-Item -ItemType Directory -Force $destinationFuld | Out-Null

# --- Kører appen? ---------------------------------------------------------
# SQLite-filer kopieret midt i en skrivning kan blive inkonsistente.
$kørende = Get-Process -Name 'NoteApp', 'Fase0Recorder' -ErrorAction SilentlyContinue
if ($kørende) {
    Skriv "ADVARSEL: NoteApp kører ($($kørende.Name -join ', '))." 'Yellow'
    Skriv "  Backup tages alligevel, men luk appen for et helt rent øjebliksbillede." 'Yellow'
    Skriv ""
}

# --- Byg arkivet ----------------------------------------------------------
$stempel = Get-Date -Format 'yyyy-MM-dd_HHmm'
$arkiv = Join-Path $destinationFuld "noteapp-data_$stempel.zip"

$filer = Get-ChildItem $kilde -Recurse -File -ErrorAction SilentlyContinue
if (-not $filer) { throw "Ingen filer at tage backup af i $kilde" }

$bytes = ($filer | Measure-Object Length -Sum).Sum
Skriv "Kilde      : $kilde"
Skriv "Indhold    : $($filer.Count) filer, $([math]::Round($bytes/1MB,1)) MB"
Skriv "Arkiv      : $arkiv"
Skriv ""

$ur = [Diagnostics.Stopwatch]::StartNew()
Compress-Archive -Path (Join-Path $kilde '*') -DestinationPath $arkiv -CompressionLevel Optimal -Force
$ur.Stop()

# --- Verificér ------------------------------------------------------------
# Et arkiv der ikke kan aabnes er ikke en backup. Tjek det med det samme,
# ikke den dag du faktisk faar brug for det.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$antalIArkiv = 0
try {
    $zip = [IO.Compression.ZipFile]::OpenRead($arkiv)
    try { $antalIArkiv = $zip.Entries.Count } finally { $zip.Dispose() }
} catch {
    throw "Arkivet kunne ikke åbnes efter oprettelse: $($_.Exception.Message)"
}

$arkivMB = [math]::Round((Get-Item $arkiv).Length / 1MB, 1)
if ($antalIArkiv -lt $filer.Count) {
    Skriv "ADVARSEL: arkivet indeholder $antalIArkiv poster, men kilden har $($filer.Count) filer." 'Yellow'
} else {
    Skriv "Verificeret: $antalIArkiv poster læsbare, $arkivMB MB, $([math]::Round($ur.Elapsed.TotalSeconds,1)) sek." 'Green'
}

# --- Rotation -------------------------------------------------------------
$gamle = Get-ChildItem $destinationFuld -Filter 'noteapp-data_*.zip' |
    Sort-Object LastWriteTime -Descending | Select-Object -Skip $Behold
foreach ($g in $gamle) {
    [IO.File]::Delete($g.FullName)
    Skriv "Slettet gammelt arkiv: $($g.Name)" 'DarkGray'
}

# --- Log ------------------------------------------------------------------
$logMappe = Join-Path $kilde 'log'
New-Item -ItemType Directory -Force $logMappe | Out-Null
# Kilden skrives med i loggen. En backup-log der ikke siger HVAD den tog
# backup af, kan ikke afsloere at den har taget backup af den forkerte mappe
# — og det er praecis den fejl der er umulig at opdage bagefter.
$linje = '{0}  {1} filer  {2} MB  fra {3}  -> {4}' -f (Get-Date -Format 's'),
         $filer.Count, $arkivMB, $kilde, (Split-Path $arkiv -Leaf)
Add-Content -Path (Join-Path $logMappe 'backup.log') -Value $linje -Encoding UTF8

$bevaret = (Get-ChildItem $destinationFuld -Filter 'noteapp-data_*.zip').Count
Skriv ""
Skriv "Færdig. $bevaret arkiver i $destinationFuld" 'Green'
