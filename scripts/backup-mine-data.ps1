<#
.SYNOPSIS
    Tager backup af dine HeyPia-data: ordbog, indlærte rettelser, optagelser,
    noter og transskriptioner.

.DESCRIPTION
    Scriptet kopierer lokalt og sender ingenting nogen steder hen. Der er
    ingen netværkskald, ingen skytjeneste og ingen telemetri i det.

    Vælger du selv en netværkssti som destination, forlader arkivet maskinen.
    Så stopper scriptet, viser hvad beslutningen indebærer, og kræver at du
    skriver JA. Uden et vindue at spørge i — fx som planlagt opgave — nægter
    det at køre frem for at gætte sig til et ja.

    Kilden er den datamappe, appen bruger — som standard C:\AppNoter, eller
    den mappe du har valgt i appen. Den slås op på samme måde som i appen, så
    de to aldrig kan pege forskellige steder hen. Mappen ligger med vilje uden
    for kode-repoet, så dine data ikke kan komme med i en git-push.

.PARAMETER Destination
    Hvor arkivet skal ligge. Standard er %USERPROFILE%\NoteApp-backup — altså
    på din egen maskine.

.PARAMETER Behold
    Antal arkiver der bevares. Ældre slettes. Standard 8.

.EXAMPLE
    .\backup-mine-data.ps1

.EXAMPLE
    # Et lokalt drev. Tjek at drevbogstavet faktisk ER lokalt — mappede shares
    # ser ud som almindelige drev, og paa denne maskine er E: og H: netvaerk.
    .\backup-mine-data.ps1 -Destination 'D:\Krypteret\NoteApp' -Behold 20
#>
[CmdletBinding()]
param(
    [string] $Destination = (Join-Path $env:USERPROFILE 'HeyPia-backup'),
    [int]    $Behold = 8,

    # Eksplicit datamappe. Planlagte opgaver SKAL sætte denne: en opgave kan
    # køre med et andet miljø end den session der oprettede den, og så peger
    # LOCALAPPDATA et andet sted hen.
    [string] $DataMappe,

    # Forhaandsgodkendelse af en destination uden for maskinen. Findes KUN for
    # at en planlagt opgave kan koere en destination, du allerede har set
    # risikolisten for og sagt ja til med aabne oejne. Saet den aldrig for at
    # slippe for spoergsmaalet.
    [switch] $JegGodkenderAtArkivetForladerMaskinen,

    [switch] $Stille
)

$ErrorActionPreference = 'Stop'

# Haandhaevelsen af datagraensen ligger for sig selv, saa dette script og
# planlaeg-backup.ps1 bruger praecis samme regel og samme risikoliste.
$datagraense = Join-Path $PSScriptRoot 'lib-datagraense.ps1'
if (-not (Test-Path $datagraense)) { throw "Fandt ikke $datagraense — uden den kan datagrænsen ikke håndhæves." }
. $datagraense

# En planlagt opgave der fejler stille er varre end ingen backup: man tror
# man er daekket, indtil man ikke er. Alt der gaar galt skal ende i loggen,
# ogsaa naar der ikke er et vindue at skrive til.
$fejlLogRod = $DataMappe
if ([string]::IsNullOrWhiteSpace($fejlLogRod)) { $fejlLogRod = $env:NOTEAPP_DATA }
if ([string]::IsNullOrWhiteSpace($fejlLogRod)) { $fejlLogRod = Get-HeyPiaDataRod }
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
if ([string]::IsNullOrWhiteSpace($kilde)) { $kilde = Get-HeyPiaDataRod }
$kilde = [IO.Path]::GetFullPath($kilde)

if (-not (Test-Path $kilde)) {
    throw "Datamappen findes ikke: $kilde`nEr appen kørt mindst én gang?"
}

# En planlagt opgave kan koere med et andet miljoe end den session, opgaven
# blev oprettet fra — og saa peger LOCALAPPDATA et andet sted hen. Resultatet
# er en backup der ser ud til at lykkes, men indeholder en naesten tom mappe.
# Derfor: kraev at ordbogen faktisk er der, foer vi kalder det en backup.
$ordbog = Join-Path $kilde 'learning.db'
$optagelser = Join-Path $kilde 'Optagelser'
if (-not (Test-Path $ordbog) -and -not (Test-Path $optagelser)) {
    throw ("Datamappen $kilde indeholder hverken learning.db eller Optagelser\.`n" +
           "Det ligner den forkerte mappe. Er backup planlagt, saa saet " +
           "NOTEAPP_DATA eksplicit i den planlagte opgave.")
}

# --- Destination ----------------------------------------------------------
$destinationFuld = [IO.Path]::GetFullPath($Destination)

# Grundprincippet (doc\mine-data.md, afsnit 0 i specen): intet forlader den
# maskine appen koerer paa. En netvaerkssti bryder det. Det maa gerne vaere dit
# valg — men saa skal det vaere en BESLUTNING, ikke en advarsel man laeser
# forbi mens kopieringen alligevel koerer. Reglen ligger i lib-datagraense.ps1,
# saa den er den samme her og i planlaeg-backup.ps1.
if (Test-ForladerMaskinen $destinationFuld) {
    $maaFortsaette = Confirm-ForladerMaskinen -Sti $destinationFuld `
        -Forhaandsgodkendt:$JegGodkenderAtArkivetForladerMaskinen `
        -Uinteraktiv:$Stille

    if (-not $maaFortsaette) {
        Write-Host "Afbrudt. Der er ikke taget backup, og intet har forladt maskinen." -ForegroundColor Green
        return
    }

    # Beslutningen skal kunne genfindes bagefter — ogsaa hvis selve backuppen
    # fejler. Derfor logges den her og ikke sammen med resultatet.
    $hvordan = if ($JegGodkenderAtArkivetForladerMaskinen) { 'forhåndsgodkendt' } else { 'bekræftet i dialog' }
    Write-DatagraenseLog -DataMappe $kilde `
        -Tekst ('GODKENDT: arkiv til destination uden for maskinen -> {0}  ({1})' -f $destinationFuld, $hvordan)
}

New-Item -ItemType Directory -Force $destinationFuld | Out-Null

# --- Kører appen? ---------------------------------------------------------
# SQLite-filer kopieret midt i en skrivning kan blive inkonsistente.
$kørende = Get-Process -Name 'HeyPia', 'Fase0Recorder' -ErrorAction SilentlyContinue
if ($kørende) {
    Skriv "ADVARSEL: HeyPia kører ($($kørende.Name -join ', '))." 'Yellow'
    Skriv "  Backup tages alligevel, men luk appen for et helt rent øjebliksbillede." 'Yellow'
    Skriv ""
}

# --- Byg arkivet ----------------------------------------------------------
$stempel = Get-Date -Format 'yyyy-MM-dd_HHmm'
$arkiv = Join-Path $destinationFuld "heypia-data_$stempel.zip"

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
$gamle = Get-ChildItem $destinationFuld -Filter 'heypia-data_*.zip' |
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

$bevaret = (Get-ChildItem $destinationFuld -Filter 'heypia-data_*.zip').Count
Skriv ""
Skriv "Færdig. $bevaret arkiver i $destinationFuld" 'Green'
