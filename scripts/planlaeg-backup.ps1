<#
.SYNOPSIS
    Opretter en planlagt opgave i Windows, der tager backup af dine
    NoteApp-data automatisk.

.DESCRIPTION
    Opgaven kører backup-mine-data.ps1 lokalt på din maskine. Med en lokal
    destination kræver den ingen netværksadgang og sender ingenting nogen
    steder hen.

    Peger destinationen ud af maskinen, skal beslutningen træffes her ved
    oprettelsen — opgaven selv kører uden nogen til at godkende noget. Uden
    din bekræftelse oprettes opgaven ikke.

    Standard er hver mandag klokken 09:00. Er maskinen slukket på det
    tidspunkt, køres opgaven når den tændes igen — ellers ville en uges
    backup gå tabt hver gang du holder ferie.

.EXAMPLE
    .\planlaeg-backup.ps1

.EXAMPLE
    .\planlaeg-backup.ps1 -Ugedag Fredag -Klokken '16:30' -Destination 'E:\Backup\NoteApp'

.EXAMPLE
    # Fjern den planlagte opgave igen
    .\planlaeg-backup.ps1 -Fjern
#>
[CmdletBinding()]
param(
    [ValidateSet('Mandag','Tirsdag','Onsdag','Torsdag','Fredag','Loerdag','Soendag')]
    [string] $Ugedag = 'Mandag',

    [string] $Klokken = '09:00',

    [string] $Destination = (Join-Path $env:USERPROFILE 'NoteApp-backup'),

    [int] $Behold = 8,

    [switch] $Fjern
)

$ErrorActionPreference = 'Stop'
$opgaveNavn = 'NoteApp - ugentlig backup'

$datagraense = Join-Path $PSScriptRoot 'lib-datagraense.ps1'
if (-not (Test-Path $datagraense)) { throw "Fandt ikke $datagraense — uden den kan datagrænsen ikke håndhæves." }
. $datagraense

if ($Fjern) {
    $eksisterende = Get-ScheduledTask -TaskName $opgaveNavn -ErrorAction SilentlyContinue
    if ($eksisterende) {
        Unregister-ScheduledTask -TaskName $opgaveNavn -Confirm:$false
        Write-Host "Planlagt opgave fjernet: $opgaveNavn" -ForegroundColor Green
    } else {
        Write-Host "Der var ingen planlagt opgave at fjerne." -ForegroundColor Yellow
    }
    return
}

$script = Join-Path $PSScriptRoot 'backup-mine-data.ps1'
if (-not (Test-Path $script)) { throw "Fandt ikke backup-scriptet: $script" }

$engelskUgedag = @{
    Mandag='Monday'; Tirsdag='Tuesday'; Onsdag='Wednesday'; Torsdag='Thursday'
    Fredag='Friday'; Loerdag='Saturday'; Soendag='Sunday'
}[$Ugedag]

# Datamappen loeses HER, i din egen session, og skrives ind i opgaven.
# Opgaven maa ikke selv slaa LOCALAPPDATA op: den kan koere med et andet
# miljoe, og saa tager den backup af en tom mappe uden at fejle.
$dataMappe = [IO.Path]::GetFullPath((Get-NoteAppDataRod))
if (-not (Test-Path $dataMappe)) {
    throw "Datamappen findes ikke: $dataMappe`nKør 'noteapp init' først."
}

# En planlagt opgave koerer uden nogen til at godkende noget. Peger den ud af
# maskinen, skal beslutningen derfor traeffes HER, hvor du sidder foran
# skaermen — og den skal traeffes for hver eneste fremtidig koersel paa en
# gang. Goer den ikke det, oprettes opgaven ikke.
$destinationFuld = [IO.Path]::GetFullPath($Destination)
$arverGodkendelse = $false

if (Test-ForladerMaskinen $destinationFuld) {
    Write-Host ""
    Write-Host "  Du er ved at planlægge en opgave, der HVER $Ugedag sender et arkiv" -ForegroundColor Red
    Write-Host "  ud af maskinen — automatisk, uden at spørge igen." -ForegroundColor Red

    if (-not (Confirm-ForladerMaskinen -Sti $destinationFuld)) {
        Write-Host "Afbrudt. Der er ikke oprettet nogen opgave." -ForegroundColor Green
        return
    }

    $arverGodkendelse = $true
    Write-DatagraenseLog -DataMappe $dataMappe `
        -Tekst ('GODKENDT: planlagt ugentlig backup til destination uden for maskinen -> {0}' -f $destinationFuld)
}

$argumenter = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}" -DataMappe "{1}" -Destination "{2}" -Behold {3} -Stille' `
    -f $script, $dataMappe, $Destination, $Behold

# Godkendelsen skrives ind i opgaven. Uden den vil backup-scriptet naegte at
# koere uinteraktivt mod en destination uden for maskinen — og det er med
# vilje: flaget er beviset paa, at valget faktisk blev truffet foran skaermen.
if ($arverGodkendelse) { $argumenter += ' -JegGodkenderAtArkivetForladerMaskinen' }

$handling = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argumenter
$udloeser = New-ScheduledTaskTrigger -Weekly -DaysOfWeek $engelskUgedag -At $Klokken

# Koer som dig selv, kun naar du er logget paa. Opgaven skal ikke have flere
# rettigheder end noedvendigt for at kopiere dine egne filer.
$identitet = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -LogonType Interactive -RunLevel Limited

$indstillinger = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1) `
    -MultipleInstances IgnoreNew

$beskrivelse = if ($arverGodkendelse) {
    "Tager backup af NoteApp-data (ordbog, indlærte rettelser, optagelser, noter) til $destinationFuld — en destination UDEN FOR maskinen, godkendt ved oprettelsen."
} else {
    "Tager lokal backup af NoteApp-data (ordbog, indlærte rettelser, optagelser, noter). Sender intet nogen steder hen."
}

Register-ScheduledTask -TaskName $opgaveNavn `
    -Action $handling -Trigger $udloeser -Principal $identitet -Settings $indstillinger `
    -Description $beskrivelse `
    -Force | Out-Null

$opgave = Get-ScheduledTask -TaskName $opgaveNavn
$info = Get-ScheduledTaskInfo -TaskName $opgaveNavn

Write-Host ""
Write-Host "Planlagt opgave oprettet." -ForegroundColor Green
Write-Host "  Navn        : $opgaveNavn"
Write-Host "  Kører       : hver $Ugedag klokken $Klokken"
Write-Host "  Datamappe   : $dataMappe"
if ($arverGodkendelse) {
    Write-Host "  Destination : $destinationFuld  (UDEN FOR maskinen — godkendt)" -ForegroundColor Yellow
} else {
    Write-Host "  Destination : $destinationFuld  (lokal)"
}
Write-Host "  Beholder    : $Behold arkiver"
Write-Host "  Status      : $($opgave.State)"
Write-Host "  Næste kørsel: $($info.NextRunTime)"
Write-Host ""
Write-Host "Er maskinen slukket på tidspunktet, køres den ved næste opstart." -ForegroundColor DarkGray
Write-Host "Kør en gang med det samme:" -ForegroundColor DarkGray
Write-Host "  Start-ScheduledTask -TaskName '$opgaveNavn'" -ForegroundColor DarkGray
