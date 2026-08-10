<#
.SYNOPSIS
    Opretter en planlagt opgave i Windows, der tager backup af dine
    NoteApp-data automatisk.

.DESCRIPTION
    Opgaven kører backup-mine-data.ps1 lokalt på din maskine. Den kræver
    ingen netværksadgang og sender ingenting nogen steder hen.

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
$dataMappe = $env:NOTEAPP_DATA
if ([string]::IsNullOrWhiteSpace($dataMappe)) {
    $dataMappe = Join-Path $env:LOCALAPPDATA 'NoteApp'
}
$dataMappe = [IO.Path]::GetFullPath($dataMappe)
if (-not (Test-Path $dataMappe)) {
    throw "Datamappen findes ikke: $dataMappe`nKør 'noteapp init' først."
}

$argumenter = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}" -DataMappe "{1}" -Destination "{2}" -Behold {3} -Stille' `
    -f $script, $dataMappe, $Destination, $Behold

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

Register-ScheduledTask -TaskName $opgaveNavn `
    -Action $handling -Trigger $udloeser -Principal $identitet -Settings $indstillinger `
    -Description "Tager lokal backup af NoteApp-data (ordbog, indlærte rettelser, optagelser, noter). Sender intet nogen steder hen." `
    -Force | Out-Null

$opgave = Get-ScheduledTask -TaskName $opgaveNavn
$info = Get-ScheduledTaskInfo -TaskName $opgaveNavn

Write-Host ""
Write-Host "Planlagt opgave oprettet." -ForegroundColor Green
Write-Host "  Navn        : $opgaveNavn"
Write-Host "  Kører       : hver $Ugedag klokken $Klokken"
Write-Host "  Datamappe   : $dataMappe"
Write-Host "  Destination : $Destination"
Write-Host "  Beholder    : $Behold arkiver"
Write-Host "  Status      : $($opgave.State)"
Write-Host "  Næste kørsel: $($info.NextRunTime)"
Write-Host ""
Write-Host "Er maskinen slukket på tidspunktet, køres den ved næste opstart." -ForegroundColor DarkGray
Write-Host "Kør en gang med det samme:" -ForegroundColor DarkGray
Write-Host "  Start-ScheduledTask -TaskName '$opgaveNavn'" -ForegroundColor DarkGray
