<#
.SYNOPSIS
    Fejler, hvis licensnotitser eller taleradskillelse mangler i den udgivne
    app eller i installationspakken.

.DESCRIPTION
    HVORFOR DEN FINDES

    Compliance-skærmen lovede allerede, at licensteksten «ligger i
    installationsmappen» — for sherpa-onnx (Apache-2.0) og for krediteringen
    af NVIDIA (CC-BY-4.0). Det passede ikke: der lå ingen licensfiler nogen
    steder i repoet 03-09-2026. En app, der påstår at overholde en
    attributionsbetingelse uden at gøre det, er ikke bare uryddelig — det er
    et brud på den licens, komponenten er brugt under.

    En kontrol, der kun står i et dokument, bliver ikke kørt. Derfor er det
    en gate: den fejler bygget.

    SÅDAN VIRKER DEN

    Uden -Msi kontrolleres en MAPPE — den udgivne app. Med -Msi kontrolleres
    filerne inde i den byggede pakke, læst gennem Windows Installers eget
    fil-katalog. De to er ikke det samme spørgsmål: en fil kan ligge i
    app-mappen og alligevel ikke komme med i pakken.

.PARAMETER Mappe
    Den udgivne app-mappe. Som standard C:\NoteApp\app.

.PARAMETER Msi
    Sti til en bygget .msi. Kontrollerer pakkens indhold i stedet for en mappe.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\tjek-licenser.ps1

.EXAMPLE
    powershell -File C:\NoteApp\scripts\tjek-licenser.ps1 -Msi C:\NoteApp\installer\HeyPia.msi
#>
[CmdletBinding()]
param(
    [string] $Mappe,
    [string] $Msi,
    [switch] $Stille
)

$ErrorActionPreference = 'Stop'

$rod = Split-Path -Parent $PSScriptRoot
if (-not $Mappe) { $Mappe = Join-Path $rod 'app' }

# =============================== KRAVET ===============================
#
# LISTEN STAAR HER OG IKKE I HVERT SCRIPT, der bruger den. To lister driver
# fra hinanden - det er praecis dét, der skete med udgiv.ps1 og
# byg-installer.ps1, som begge havde deres egen kopi af taleradskillelsens
# filer.
#
# Licensfilerne: hver komponent, der REDISTRIBUERES, skal have sin
# licenstekst med. De hentede - whisper.cpp, modellerne, stemmevagten -
# staar ogsaa paa listen, fordi de ender paa brugerens maskine, og fordi
# NOTICE.md goer rede for dem.
$Licensfiler = @(
    'licenser\NOTICE.md',
    'licenser\tekster\sherpa-onnx-Apache-2.0.txt',
    'licenser\tekster\onnxruntime-MIT.txt',
    'licenser\tekster\pyannote-MIT.txt',
    'licenser\tekster\nvidia-titanet-CC-BY-4.0.txt',
    'licenser\tekster\naudio-MIT.txt',
    'licenser\tekster\sqlitepclraw-Apache-2.0.txt',
    'licenser\tekster\pdfpig-Apache-2.0.txt',
    'licenser\tekster\dotnet-og-wpf-MIT.txt',
    'licenser\tekster\whisper.cpp-MIT.txt',
    'licenser\tekster\whisper-modeller-OpenAI-MIT.txt',
    'licenser\tekster\silero-vad-MIT.txt'
)

# Taleradskillelsen. Uden den koerer appen videre UDEN navne paa talerne, og
# den siger det ikke. Det er den slags, der opdages hos kunden.
$Talerfiler = @(
    'talere\bin\sherpa-onnx-offline-speaker-diarization.exe',
    'talere\bin\onnxruntime.dll',
    'talere\segmentering.onnx',
    'talere\stemmer.onnx'
)

# =============================== KONTROLLEN ===============================

function Get-MsiFilnavne {
    param([string] $Sti)

    # Windows Installer har sit eget fil-katalog i pakken. Der laeses fra det
    # frem for at pakke MSI'en ud: det er hurtigt, og det er noejagtig den
    # liste, installationen kommer til at laegge paa disken.
    $wi = New-Object -ComObject WindowsInstaller.Installer

    # 0 = read-only. En kontrol maa ikke kunne aendre det, den kontrollerer.
    $db = $wi.GetType().InvokeMember('OpenDatabase', 'InvokeMethod', $null, $wi, @($Sti, 0))
    $visning = $db.GetType().InvokeMember('OpenView', 'InvokeMethod', $null, $db,
                                          @('SELECT `FileName` FROM `File`'))
    $visning.GetType().InvokeMember('Execute', 'InvokeMethod', $null, $visning, $null)

    $navne = New-Object System.Collections.Generic.List[string]

    while ($true) {
        $post = $visning.GetType().InvokeMember('Fetch', 'InvokeMethod', $null, $visning, $null)
        if (-not $post) { break }

        $vaerdi = $post.GetType().InvokeMember('StringData', 'GetProperty', $null, $post, 1)

        # Formen er «kort|langt», naar navnet ikke er 8.3. Det lange taeller.
        $navne.Add(($vaerdi -split '\|')[-1])
    }

    $visning.GetType().InvokeMember('Close', 'InvokeMethod', $null, $visning, $null) | Out-Null

    return $navne
}

$krav = $Licensfiler + $Talerfiler
$mangler = @()
$hvad = ''

if ($Msi) {
    if (-not (Test-Path $Msi)) { throw "Fandt ikke pakken: $Msi" }

    $hvad = "installationspakken $(Split-Path $Msi -Leaf)"

    # PAKKEN KENDER KUN FILNAVNE, IKKE MAPPER, i File-tabellen. Der
    # sammenlignes derfor paa filnavn. Det er svagere end en stikontrol - men
    # det, gaten skal fange, er en fil, der slet ikke kom med, og det fanger
    # den. En fil i den forkerte mappe ville kraeve, at Directory-tabellen
    # blev fulgt hele vejen, og den kompleksitet ville goere kontrollen
    # skroebelig frem for skarp.
    $iPakken = Get-MsiFilnavne -Sti $Msi

    foreach ($k in $krav) {
        $navn = Split-Path $k -Leaf
        if ($iPakken -notcontains $navn) { $mangler += $k }
    }
}
else {
    if (-not (Test-Path $Mappe)) { throw "Fandt ikke mappen: $Mappe" }

    $hvad = "den udgivne app i $Mappe"
    $mangler = $krav | Where-Object { -not (Test-Path (Join-Path $Mappe $_)) }
}

if ($mangler) {
    $linjer = ($mangler | ForEach-Object { "  - $_" }) -join "`n"

    throw ("$($mangler.Count) paakraevet fil(er) mangler i ${hvad}:`n$linjer`n`n" +
           "Licensfilerne SKAL foelge med: Compliance-skaermen lover, at " +
           "licensteksten ligger i installationsmappen, og Apache-2.0 og " +
           "CC-BY-4.0 kraever det. Taleradskillelsen skal med, ellers " +
           "installeres en app, der stiltiende ikke kan saette navne paa talere.")
}

if (-not $Stille) {
    Write-Host ("Licensnotitser og taleradskillelse: {0} filer paa plads i {1}" -f $krav.Count, $hvad)
}
