<#
.SYNOPSIS
    Efterprøver Google Kalender-forbindelsen hele vejen, uden at starte appen.

.DESCRIPTION
    Kører præcis de to kald, appen bruger, når man trykker Forbind:
    godkendelsen i browseren og en hentning af aftalerne. Virker det her,
    virker knappen.

    HVORFOR DEN FINDES

    Integrationen kunne ellers kun prøves ved at åbne programmet og trykke
    Forbind — og det kan man ikke, mens der optages. Optagelse må aldrig kunne
    blokeres, heller ikke af en afprøvning.

    HVORFOR SCRIPTET OG IKKE BARE VÆRKTØJET

    Klient-id'et ligger ét sted: `hemmeligheder\google-klient.json`. Appen får
    det kopieret med af udgiv.ps1, men værktøjet leder i sin egen bin-mappe.
    Frem for at bede om en kopi mere — som ville kunne komme ud af trit med
    originalen — læses filen her og gives videre som miljøvariabler, der kun
    findes, så længe kommandoen kører.

    DER GEMMES INGENTING. Opdateringsnøglen skrives ikke ned, og appens
    indstillinger røres ikke. En afprøvning, der efterlader noget, gør, at man
    bagefter ikke ved, om appen virker, eller om afprøvningen fiksede det.

.PARAMETER Dage
    Hvor mange dage frem der hentes. Standard 14.

.EXAMPLE
    .\proev-google.ps1
#>
[CmdletBinding()]
param(
    [int] $Dage = 14
)

$ErrorActionPreference = 'Stop'
$Rod = Split-Path $PSScriptRoot -Parent

# Googles egen fil eller vores eget format. Den, der lige har hentet filen fra
# konsollen, har den liggende med Googles navn - og appen laeser den nu, som
# den er.
$hemmeligMappe = Join-Path $Rod 'hemmeligheder'

$fil = $null
if (Test-Path $hemmeligMappe) {
    $fil = Get-ChildItem $hemmeligMappe -File |
           Where-Object { $_.Name -eq 'google-klient.json' -or
                          $_.Name -like 'client_secret*.json' } |
           Sort-Object Name | Select-Object -First 1 -ExpandProperty FullName
}

if (-not $fil) {
    Write-Host ''
    Write-Host '  Der er ikke sat et klient-id op endnu.' -ForegroundColor Yellow
    Write-Host ''
    Write-Host "  Laeg Googles egen fil i denne mappe - navnet er ligegyldigt:"
    Write-Host "      $hemmeligMappe"
    Write-Host ''
    Write-Host '  Enten Googles egen (client_secret_....json), eller:'
    Write-Host '      {'
    Write-Host '        "KlientId": "....apps.googleusercontent.com",'
    Write-Host '        "Hemmelighed": "GOCSPX-..."'
    Write-Host '      }'
    Write-Host ''
    Write-Host '  De fire trin hos Google staar i doc\google-integration.md.'
    Write-Host ''
    exit 1
}

# [IO.File]::ReadAllText og ikke Get-Content: Get-Content -Encoding utf8
# mangler aeoeaa i det her miljoe, og en oedelagt fil ville her se ud som et
# forkert klient-id - den vildeste af alle fejlsoegninger.
$raa = [IO.File]::ReadAllText($fil, [Text.UTF8Encoding]::new($false)) | ConvertFrom-Json

# Googles format har alt inde i «installed». Vores eget har det oeverst.
if ($raa.installed) {
    $json = [pscustomobject]@{
        KlientId     = $raa.installed.client_id
        Hemmelighed  = $raa.installed.client_secret
    }
}
elseif ($raa.web) {
    $json = [pscustomobject]@{
        KlientId     = $raa.web.client_id
        Hemmelighed  = $raa.web.client_secret
    }
}
else {
    $json = $raa
}

Write-Host ''
Write-Host "  Laeser $(Split-Path $fil -Leaf)"

if (-not $json.KlientId) {
    Write-Host ''
    Write-Host '  Filen findes, men der staar intet KlientId i den.' -ForegroundColor Yellow
    Write-Host "      $fil"
    Write-Host ''
    exit 1
}

$vaerktoej = Join-Path $Rod 'src\NoteApp.Tools\bin\Release\net8.0-windows\heypia.exe'

if (-not (Test-Path $vaerktoej)) {
    Write-Host ''
    Write-Host '  Vaerktoejet er ikke bygget. Byg det med:' -ForegroundColor Yellow
    Write-Host '      dotnet build src\NoteApp.Tools\NoteApp.Tools.csproj -c Release'
    Write-Host ''
    exit 1
}

# Kun i den her proces. Saetter man dem varigt, ligger legitimationen i
# brugerens miljoe bagefter, og det er ikke noget, en afproevning skal
# efterlade.
$env:NOTEAPP_GOOGLE_CLIENT_ID     = $json.KlientId
$env:NOTEAPP_GOOGLE_CLIENT_SECRET = $json.Hemmelighed

& $vaerktoej google $Dage
exit $LASTEXITCODE
