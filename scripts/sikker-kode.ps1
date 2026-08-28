<#
.SYNOPSIS
    Sikrer koden: pusher til GitHub, laver et git-bundt, og tager kopi af det,
    der aldrig må i git.

.DESCRIPTION
    Data-backuppen (backup-mine-data.ps1) tager brugerens optagelser og
    indstillinger. Den her tager det ANDET — koden, historikken og de få
    filer, der hverken er i git eller i datamappen.

    HVORFOR DEN FINDES

    23-08-2026 lå der 75 commits, der aldrig var pushet. De var ikke tabt,
    men de fandtes ét sted: på den her maskine. Et push er noget, man husker,
    lige indtil man har travlt.

    TRE TING, OG DE DÆKKER HVER SIT TAB

      push      maskinen brænder      -> alt ligger på GitHub
      bundt     GitHub er utilgængelig -> hele historikken i én fil lokalt
      kopi      filer, der aldrig må i git, findes kun ét sted

    PUSH KRÆVER, AT DU BEDER OM DET

    Uden -Push laver scriptet kun bundt og kopi. Et automatisk push ville
    sende arbejde af sted, ingen har set efter — og leverancetjekket skal
    køres FØR, ikke opdages bagefter. Kører scriptet som planlagt opgave, er
    der ingen til at læse et fund.

.PARAMETER Push
    Push til GitHub. Kører leverancetjek med -Historik først og stopper ved
    ethvert fund.

.PARAMETER Destination
    Hvor bundtet og kopierne skal ligge. Standard C:\NoteApp-backup.

.PARAMETER Behold
    Antal bundter der bevares. Ældre slettes. Standard 5.

.EXAMPLE
    .\sikker-kode.ps1

.EXAMPLE
    .\sikker-kode.ps1 -Push
#>
[CmdletBinding()]
param(
    [switch] $Push,
    [string] $Destination = 'C:\NoteApp-backup',
    [int]    $Behold = 5
)

$ErrorActionPreference = 'Stop'
$Rod = Split-Path $PSScriptRoot -Parent

function Sig($tekst) { Write-Host $tekst }
function God($tekst) { Write-Host "  $tekst" -ForegroundColor Green }
function Advar($tekst) { Write-Host "  $tekst" -ForegroundColor Yellow }

Sig ''
Sig "Sikrer koden i $Rod"
Sig ''

# --- 1. Hvor staar vi? -----------------------------------------------------
Push-Location $Rod
try {
    $urene = @(git status --porcelain)
    if ($urene.Count -gt 0) {
        Advar "$($urene.Count) fil(er) er ikke committet. De kommer IKKE med i bundtet."
        Advar "Commit dem foerst, hvis de skal sikres."
    }

    git fetch origin --quiet 2>$null

    $foran = 0
    try { $foran = [int](git rev-list --count origin/main..HEAD 2>$null) } catch { }

    if ($foran -gt 0) { Advar "$foran commit(s) er ikke pushet til GitHub." }
    else { God 'Alt er pushet til GitHub.' }

    # --- 2. Push ------------------------------------------------------------
    if ($Push) {
        if ($foran -eq 0) {
            God 'Intet at pushe.'
        }
        else {
            Sig ''
            Sig 'Koerer leverancetjek med historik foer push ...'

            $tjek = Join-Path $env:USERPROFILE '.claude\skills\leverancetjek\tjek-leverance.ps1'

            if (-not (Test-Path $tjek)) {
                throw ("Leverancetjekket findes ikke: $tjek`n" +
                       "Der pushes ikke uden. Se kontinuitet/README.md for at " +
                       "laegge det paa plads igen.")
            }

            & powershell -File $tjek -Sti $Rod -Historik
            if ($LASTEXITCODE -ne 0) {
                throw ("Leverancetjekket fandt noget. Der pushes IKKE.`n" +
                       "Ret fundene foerst - en term, der er pushet, ligger i " +
                       "historikken hos alle, der har hentet den.")
            }

            Sig ''
            git push origin main
            if ($LASTEXITCODE -ne 0) { throw 'Push fejlede.' }

            God "$foran commit(s) pushet."
        }
    }

    # --- 3. Bundtet ---------------------------------------------------------
    New-Item -ItemType Directory -Force $Destination | Out-Null

    $navn = "heypia-$(Get-Date -Format 'yyyy-MM-dd').bundle"
    $bundt = Join-Path $Destination $navn

    Sig ''
    Sig 'Laver git-bundt ...'
    git bundle create $bundt --all | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Bundtet kunne ikke laves.' }

    # EFTERPROEV DET. Et bundt, der ikke kan pakkes ud, er ikke en backup -
    # det er en fil, man tror er en backup.
    #
    # DER MAA IKKE STAA 2>&1 HER. «git bundle verify» skriver sit OK-svar paa
    # stderr, og under $ErrorActionPreference = 'Stop' goer en omdirigering
    # hver linje til en NativeCommandError - saa faldt scriptet paa et bundt,
    # der var i orden. Exitkoden er svaret; stderr er stoej.
    git bundle verify $bundt | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Bundtet kan ikke laeses. Det er ikke en backup.' }

    $mb = [math]::Round((Get-Item $bundt).Length / 1MB, 1)
    God "$navn ($mb MB) - efterproevet laesbart"

    # Aeldre bundter ryddes. Uden det vokser mappen med 50 MB om ugen.
    $gamle = @(Get-ChildItem $Destination -Filter 'heypia-*.bundle' |
               Sort-Object LastWriteTime -Descending | Select-Object -Skip $Behold)

    foreach ($g in $gamle) {
        Remove-Item $g.FullName -Force
        Sig "  ryddet: $($g.Name)"
    }
}
finally {
    Pop-Location
}

# --- 4. Det, der aldrig maa i git ------------------------------------------
#
# Faa filer, og de kan ikke genskabes fra noget. forbudte-termer.txt er den
# vigtigste: uden den siger leverancetjekket god for alt, og saa er den
# farligere end ingen kontrol.
Sig ''
Sig 'Kopierer det, der ikke er i git ...'

$lokalt = Join-Path $Destination 'lokalt-ikke-i-git'
New-Item -ItemType Directory -Force $lokalt | Out-Null

$filer = @(
    @{ Fra = Join-Path $env:USERPROFILE '.claude\skills\leverancetjek\forbudte-termer.txt'
       Til = 'forbudte-termer.txt' }
    @{ Fra = Join-Path $env:USERPROFILE '.claude\CLAUDE.md'
       Til = 'globale-regler-CLAUDE.md' }
)

# Googles egen fil hedder client_secret_<tal>.apps.googleusercontent.com.json,
# og appen laeser den nu, som den hentes. Uden det her stod der «findes ikke
# endnu» om en legitimation, der laa lige der - og den er praecis den slags
# fil, det her script findes for: den kan ikke genskabes fra noget.
$hemmeligMappe = Join-Path $Rod 'hemmeligheder'

if (Test-Path $hemmeligMappe) {
    foreach ($g in @(Get-ChildItem $hemmeligMappe -File |
                     Where-Object { $_.Name -eq 'google-klient.json' -or
                                    $_.Name -like 'client_secret*.json' })) {
        $filer += @{ Fra = $g.FullName; Til = $g.Name }
    }
}

foreach ($f in $filer) {
    if (Test-Path $f.Fra) {
        Copy-Item $f.Fra (Join-Path $lokalt $f.Til) -Force
        God $f.Til
    }
    else {
        Sig "  findes ikke endnu: $($f.Til)"
    }
}

# Hukommelsen og skills ligger i git under kontinuitet/, men de RETTES i
# ~/.claude. Kopien her fanger aendringer, der endnu ikke er committet.
$hu = Join-Path $env:USERPROFILE '.claude\projects\C--ClaudeCode\memory'
if (Test-Path $hu) {
    $huMaal = Join-Path $lokalt 'hukommelse'
    New-Item -ItemType Directory -Force $huMaal | Out-Null
    Copy-Item "$hu\*.md" $huMaal -Force
    God "hukommelse ($((Get-ChildItem $huMaal -Filter *.md).Count) filer)"
}

Sig ''
Sig "Faerdig. Alt ligger i $Destination"

if (-not $Push -and $foran -gt 0) {
    Sig ''
    Advar "HUSK: $foran commit(s) er stadig kun paa den her maskine."
    Advar 'Koer med -Push, naar du vil sende dem af sted.'
}
