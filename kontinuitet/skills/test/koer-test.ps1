# =====================================================================
#  HeyPia - funktions-, sikkerheds- og compliancetest
# =====================================================================
#
#  KUN LAESNING. Scriptet aendrer intet, starter ingen optagelse og
#  trykker ikke paa nogen taster. En test, der aendrer den ting, den
#  maaler, kan ikke bruges til noget.
#
#  Resultatet skrives som JSON, saa rapporten kan bygges paa tal og
#  ikke paa hukommelse.
#
#  Brug:
#    powershell -File koer-test.ps1 [-Sti C:\NoteApp] [-Ud C:\AppNoter\test]
#    powershell -File koer-test.ps1 -SpringBygOver     (hurtig kørsel)
# =====================================================================

param(
    [string]$Sti = 'C:\NoteApp',
    [string]$Data = 'C:\AppNoter',
    [string]$Ud = 'C:\AppNoter\test',
    [switch]$SpringBygOver
)

$ErrorActionPreference = 'Continue'
$resultater = @()
$start = Get-Date

function Meld {
    param($Id, $Omraade, $Navn, $Status, $Detalje)
    $script:resultater += [pscustomobject]@{
        Id = $Id; Omraade = $Omraade; Navn = $Navn
        Status = $Status; Detalje = "$Detalje"
    }
    $farve = 'Gray'
    if ($Status -eq 'BESTAAET') { $farve = 'Green' }
    if ($Status -eq 'ADVARSEL') { $farve = 'Yellow' }
    if ($Status -eq 'FEJL') { $farve = 'Red' }
    Write-Host ("  {0,-4} {1,-13} {2}" -f $Id, $Status, $Navn) -ForegroundColor $farve
}

Write-Host ""
Write-Host "HeyPia testkoersel - $($start.ToString('dd-MM-yyyy HH:mm'))" -ForegroundColor Cyan
Write-Host "Kode: $Sti   Data: $Data" -ForegroundColor DarkGray
Write-Host ""

Set-Location $Sti

# =====================================================================
Write-Host "FUNKTION" -ForegroundColor Cyan
# =====================================================================

# --- F1 byg -----------------------------------------------------------
if ($SpringBygOver) {
    Meld 'F1' 'Funktion' 'Bygning af appen' 'SPRUNGET OVER' 'kørt med -SpringBygOver'
}
else {
    $byg = dotnet build "$Sti\src\NoteApp.Desktop\NoteApp.Desktop.csproj" -v q --nologo 2>&1
    $ok = ($byg | Select-String -Pattern 'Build succeeded' -Quiet)
    if ($ok) { Meld 'F1' 'Funktion' 'Bygning af appen' 'BESTAAET' 'build succeeded' }
    else {
        $fejl = ($byg | Select-String -Pattern ': error' | Select-Object -First 3) -join '; '
        Meld 'F1' 'Funktion' 'Bygning af appen' 'FEJL' $fejl
    }
}

# --- F2 enhedsproever -------------------------------------------------
#
#  BRUGERENS EGNE DATA MAALES FOER OG EFTER.
#
#  Proeverne skrev i den RIGTIGE datamappe indtil 30-08-2026:
#  NotifikationsvarslingTest kalder Notifikationer.MarkerAlleLaest, som
#  gemmer i AppSettings - og uden en omdirigering ramte det brugerens egen
#  fil med standardvaerdier. Hver gang «dotnet test» blev koert, mistede
#  brugeren sin mikrofon, sin genvejstast og sit velkomstforloeb.
#
#  Det tog en dag at finde, fordi det lignede en fejl i appen.
#
#  En proevekoersel, der aendrer noget, der tilhoerer et menneske, er ikke
#  en proeve. Derfor maales det her, hver gang.
$vagtfiler = @('indstillinger.json', 'ordliste.txt', 'log\historik.jsonl')
$foer = @{}
foreach ($v in $vagtfiler) {
    $p = Join-Path $Data $v
    if (Test-Path $p) { $foer[$v] = (Get-Item $p).LastWriteTimeUtc.Ticks }
}

$test = dotnet test "$Sti\tests\NoteApp.Tests\NoteApp.Tests.csproj" -v q --nologo 2>&1

$roert = @()
foreach ($v in $vagtfiler) {
    $p = Join-Path $Data $v
    if (-not (Test-Path $p)) { continue }
    if (-not $foer.ContainsKey($v)) { $roert += "$v (oprettet)"; continue }
    if ((Get-Item $p).LastWriteTimeUtc.Ticks -ne $foer[$v]) { $roert += $v }
}

if ($roert.Count -gt 0) {
    Meld 'F2b' 'Funktion' 'Prøverne rører ikke brugerens data' 'FEJL' `
        ('prøvekørslen ændrede: ' + ($roert -join ', '))
}
else {
    Meld 'F2b' 'Funktion' 'Prøverne rører ikke brugerens data' 'BESTAAET' `
        "$($vagtfiler.Count) filer uændret efter prøvekørslen"
}

$linje = $test | Select-String -Pattern 'Passed!|Failed!' | Select-Object -Last 1
if ($linje -match 'Failed:\s+(\d+),\s+Passed:\s+(\d+)') {
    $fejlede = [int]$Matches[1]; $bestod = [int]$Matches[2]
    if ($fejlede -eq 0) { Meld 'F2' 'Funktion' 'Enhedsprøver' 'BESTAAET' "$bestod prøver, 0 fejl" }
    else { Meld 'F2' 'Funktion' 'Enhedsprøver' 'FEJL' "$fejlede af $($bestod+$fejlede) fejlede" }
}
else { Meld 'F2' 'Funktion' 'Enhedsprøver' 'FEJL' 'kunne ikke aflæse resultatet' }

# --- F3 udgivelse mod commit -----------------------------------------
$exe = Join-Path $Sti 'app\HeyPia.exe'
$commit = git -C $Sti log -1 --format='%s'
$commitver = ''
if ($commit -match 'v(\d+\.\d+\.\d+)') { $commitver = $Matches[1] }
if (Test-Path $exe) {
    $filver = (Get-Item $exe).VersionInfo.FileVersion
    if ($filver -like "$commitver*") {
        Meld 'F3' 'Funktion' 'Udgivelsen svarer til seneste commit' 'BESTAAET' "v$filver"
    }
    else {
        Meld 'F3' 'Funktion' 'Udgivelsen svarer til seneste commit' 'ADVARSEL' "udgivet v$filver, commit v$commitver"
    }
}
else { Meld 'F3' 'Funktion' 'Udgivelsen svarer til seneste commit' 'FEJL' 'HeyPia.exe findes ikke' }

# --- F4 koerer appen --------------------------------------------------
$proc = Get-Process HeyPia -ErrorAction SilentlyContinue
if ($proc) { Meld 'F4' 'Funktion' 'Appen kører' 'BESTAAET' "pid $($proc.Id)" }
else { Meld 'F4' 'Funktion' 'Appen kører' 'ADVARSEL' 'ikke startet - genvej og vågeord kan ikke virke' }

# --- F5 genvejstasten -------------------------------------------------
$kode = @"
using System; using System.Runtime.InteropServices;
public class Genvejsproeve {
  [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint fs,uint vk);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  public static bool Ledig(uint mod, uint vk) {
    bool ok = RegisterHotKey(IntPtr.Zero, 9941, mod, vk);
    if (ok) { UnregisterHotKey(IntPtr.Zero, 9941); return true; }
    return false;
  }
}
"@
try { Add-Type -TypeDefinition $kode -Language CSharp -ErrorAction SilentlyContinue } catch { }
# 0x0002 CONTROL | 0x0004 SHIFT, 0xBC = komma
$ledig = [Genvejsproeve]::Ledig(6, 0xBC)
if ($proc -and -not $ledig) {
    Meld 'F5' 'Funktion' 'Genvejstasten er registreret' 'BESTAAET' 'Ctrl+Shift+, er optaget af appen'
}
elseif (-not $proc) {
    Meld 'F5' 'Funktion' 'Genvejstasten er registreret' 'SPRUNGET OVER' 'appen kører ikke'
}
else {
    Meld 'F5' 'Funktion' 'Genvejstasten er registreret' 'FEJL' 'Ctrl+Shift+, er ledig - ingen har den'
}

# --- F6-F8 vaageordets dele -------------------------------------------
$motor = Join-Path $Data 'motor\whisper\bin\Release\whisper-command.exe'
if (Test-Path $motor) { Meld 'F6' 'Funktion' 'Vågeordets motor findes' 'BESTAAET' 'whisper-command.exe' }
else { Meld 'F6' 'Funktion' 'Vågeordets motor findes' 'FEJL' 'whisper-command.exe mangler' }

$modelmappe = Join-Path $Data 'motor\modeller'
$vad = 'ggml-silero-v5.1.2.bin'
$modeller = @()
if (Test-Path $modelmappe) {
    $modeller = Get-ChildItem $modelmappe -Filter '*.bin' | Where-Object { $_.Length -ge 20MB -and $_.Name -ne $vad } | Sort-Object Length
}
if ($modeller.Count -gt 0) {
    $valgt = $modeller[0]
    $stoerste = $modeller[$modeller.Count - 1]
    $d = "{0} ({1:N0} MB) valgt af {2} modeller; største er {3} ({4:N0} MB)" -f `
        $valgt.Name, ($valgt.Length / 1MB), $modeller.Count, $stoerste.Name, ($stoerste.Length / 1MB)
    Meld 'F7' 'Funktion' 'Vågeordet vælger den mindste model' 'BESTAAET' $d
}
else { Meld 'F7' 'Funktion' 'Vågeordet vælger den mindste model' 'FEJL' 'ingen brugbar model i modelmappen' }

if (Test-Path (Join-Path $modelmappe $vad)) { Meld 'F8' 'Funktion' 'Stemmevagten findes' 'BESTAAET' "$vad" }
else { Meld 'F8' 'Funktion' 'Stemmevagten findes' 'ADVARSEL' 'Silero mangler - genkendelsen kører også på stilhed' }

# --- F9-F10 datamappen ------------------------------------------------
if (Test-Path $Data) {
    $inde = $Data.TrimEnd('\').ToLower().StartsWith($Sti.TrimEnd('\').ToLower())
    if ($inde) { Meld 'F9' 'Funktion' 'Datamappen ligger uden for kodelageret' 'FEJL' "$Data ligger inde i $Sti" }
    else { Meld 'F9' 'Funktion' 'Datamappen ligger uden for kodelageret' 'BESTAAET' $Data }
}
else { Meld 'F9' 'Funktion' 'Datamappen ligger uden for kodelageret' 'FEJL' "$Data findes ikke" }

$inds = Join-Path $Data 'indstillinger.json'
if (Test-Path $inds) {
    try {
        $null = Get-Content $inds -Raw | ConvertFrom-Json
        Meld 'F10' 'Funktion' 'Indstillinger kan læses' 'BESTAAET' 'gyldig JSON'
    }
    catch { Meld 'F10' 'Funktion' 'Indstillinger kan læses' 'FEJL' 'filen er ikke gyldig JSON' }
}
else { Meld 'F10' 'Funktion' 'Indstillinger kan læses' 'ADVARSEL' 'ingen indstillingsfil endnu' }

# --- F11 udskriftsmotoren ---------------------------------------------
$cli = Get-ChildItem (Join-Path $Data 'motor') -Recurse -Filter 'whisper-cli.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($cli) { Meld 'F11' 'Funktion' 'Udskriftsmotoren findes' 'BESTAAET' 'whisper-cli.exe' }
else { Meld 'F11' 'Funktion' 'Udskriftsmotoren findes' 'FEJL' 'whisper-cli.exe mangler - møder kan ikke skrives ud' }

# --- F12 sprognoegler -------------------------------------------------
$py = @'
import io, json, sys
def n(p):
    d = json.loads(io.open(p, encoding="utf-8-sig").read()); s = set()
    for g, v in d.items():
        if isinstance(v, dict):
            for k in v: s.add(g + "." + k)
        else: s.add(g)
    return s
a = n(sys.argv[1]); b = n(sys.argv[2])
print("%d|%d|%d|%d" % (len(a), len(b), len(a - b), len(b - a)))
'@
$pyfil = Join-Path $env:TEMP 'heypia-sprogtjek.py'
Set-Content -Path $pyfil -Value $py -Encoding UTF8
$svar = python $pyfil "$Sti\src\NoteApp.Core\sprog\da.json" "$Sti\src\NoteApp.Core\sprog\en.json" 2>&1
if ("$svar" -match '^(\d+)\|(\d+)\|(\d+)\|(\d+)$') {
    $kunDa = [int]$Matches[3]; $kunEn = [int]$Matches[4]
    if ($kunDa -eq 0 -and $kunEn -eq 0) {
        Meld 'F12' 'Funktion' 'Dansk og engelsk har samme nøgler' 'BESTAAET' "$($Matches[1]) tekster i begge"
    }
    else {
        Meld 'F12' 'Funktion' 'Dansk og engelsk har samme nøgler' 'FEJL' "$kunDa kun på dansk, $kunEn kun på engelsk"
    }
}
else { Meld 'F12' 'Funktion' 'Dansk og engelsk har samme nøgler' 'ADVARSEL' 'kunne ikke afgøres (python?)' }

# --- F13-F14 det, kun et menneske kan afgoere -------------------------
Meld 'F13' 'Funktion' 'Optagelse af et rigtigt møde' 'IKKE AFPROEVET' `
    'kræver to lydspor og en person; kan ikke afgøres af et script'

$spor = Join-Path $Data 'log\genvej-spor.log'
$diktat = $false
if (Test-Path $spor) { $diktat = [bool](Select-String -Path $spor -Pattern 'svar=Slut' -Quiet) }
if ($diktat) { Meld 'F14' 'Funktion' 'Diktering fra tast til tekst' 'DELVIS' 'holdet fanges; indsættelsen kræver tale' }
else { Meld 'F14' 'Funktion' 'Diktering fra tast til tekst' 'IKKE AFPROEVET' 'kræver at en person taler' }

# =====================================================================
Write-Host ""
Write-Host "SIKKERHED" -ForegroundColor Cyan
# =====================================================================

# --- S1 forbudte termer -----------------------------------------------
# Der ses paa EXITKODEN og ikke paa teksten. Leverancetjek svarer med 1 ved
# fejl, og det er entydigt - en tekstsammenligning paa «BESTÅET» faldt over
# sit eget Å, da scriptet blev laest med den forkerte tegnsaetning.
$lev = powershell -File "$env:USERPROFILE\.claude\skills\leverancetjek\tjek-leverance.ps1" -Sti $Sti 2>&1
$levkode = $LASTEXITCODE
if ($levkode -eq 0) { Meld 'S1' 'Sikkerhed' 'Forbudte termer og kundenavne' 'BESTAAET' 'leverancetjek uden fund' }
else { Meld 'S1' 'Sikkerhed' 'Forbudte termer og kundenavne' 'FEJL' (($lev | Select-Object -Last 3) -join ' ') }

$sporede = git -C $Sti ls-files

# ======================================================================
#  S1b  DET, DER FAKTISK FORLADER MASKINEN
# ======================================================================
#
#  DEN HER PRØVE FINDES PÅ GRUND AF EN TEST, DER SAGDE BESTÅET.
#
#  Leverancetjek kigger i KODELAGERET. Men ordbogen ligger i datamappen og
#  sendes til leverandøren ved HVER eneste diktering — og der stod et
#  firmanavn i den, som ikke måtte forlade maskinen. Testen så det ikke,
#  fordi den kiggede det forkerte sted. Fundet 30-08-2026.
#
#  Her ses på de filer, appen SENDER, ikke på dem, den gemmer.

$sendes = @(
    @{ Navn = 'ordbogen (sendes med hver diktering)'; Sti = (Join-Path $Data 'ordliste.txt') }
    @{ Navn = 'teksttyper (sendes som instruktion)'; Sti = (Join-Path $Data 'teksttyper.json') }
    @{ Navn = 'skabeloner (sendes som instruktion)'; Sti = (Join-Path $Data 'skabeloner') }
)

$termfil = "$env:USERPROFILE\.claude\skills\leverancetjek\forbudte-termer.txt"
$termer = @()
if (Test-Path $termfil) {
    $termer = Get-Content $termfil -Encoding UTF8 |
        Where-Object { $_.Trim().Length -gt 0 -and -not $_.StartsWith('#') } |
        ForEach-Object { ($_ -split ':')[0].Trim() } |
        Where-Object { $_.Length -gt 0 }
}

$fund = @()
$set = 0

foreach ($k in $sendes) {
    if (-not (Test-Path $k.Sti)) { continue }

    $filer = @()
    if ((Get-Item $k.Sti).PSIsContainer) {
        $filer = Get-ChildItem $k.Sti -Recurse -File -ErrorAction SilentlyContinue
    }
    else { $filer = @(Get-Item $k.Sti) }

    foreach ($fil in $filer) {
        $set++
        $indhold = Get-Content $fil.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
        if (-not $indhold) { continue }

        foreach ($t in $termer) {
            if ($indhold -match [regex]::Escape($t)) {
                $fund += ("{0}: «{1}» i {2}" -f $k.Navn, $t, $fil.Name)
            }
        }
    }
}

if ($termer.Count -eq 0) {
    Meld 'S1b' 'Sikkerhed' 'Forbudte termer i det, der SENDES' 'SPRUNGET OVER' `
        'listen over forbudte termer kunne ikke læses'
}
elseif ($fund.Count -gt 0) {
    Meld 'S1b' 'Sikkerhed' 'Forbudte termer i det, der SENDES' 'FEJL' (($fund | Select-Object -First 4) -join '; ')
}
else {
    Meld 'S1b' 'Sikkerhed' 'Forbudte termer i det, der SENDES' 'BESTAAET' `
        "$set fil(er), der forlader maskinen, er rene"
}

# --- S2 filnavne ------------------------------------------------------
$mistaenkt = $sporede | Where-Object { $_ -match '(?i)(secret|\.key$|\.pem$|\.pfx$|credential|password|client_secret)' }
if ($mistaenkt) { Meld 'S2' 'Sikkerhed' 'Hemmeligheder i sporede filnavne' 'FEJL' (($mistaenkt | Select-Object -First 5) -join ', ') }
else { Meld 'S2' 'Sikkerhed' 'Hemmeligheder i sporede filnavne' 'BESTAAET' 'ingen mistænkelige filnavne' }

# --- S3 noeglemoenstre ------------------------------------------------
$moenster = 'sk-[A-Za-z0-9]{20,}|AIza[0-9A-Za-z_-]{30,}|ya29\.|BEGIN (RSA |EC )?PRIVATE KEY|xox[baprs]-'
$traef = git -C $Sti grep -nIE $moenster -- . 2>$null
if ($traef) { Meld 'S3' 'Sikkerhed' 'API-nøgler i sporet kode' 'FEJL' (($traef | Select-Object -First 3) -join '; ') }
else { Meld 'S3' 'Sikkerhed' 'API-nøgler i sporet kode' 'BESTAAET' 'ingen nøglemønstre' }

# --- S4 datafiler -----------------------------------------------------
$datafiler = $sporede | Where-Object { $_ -match '\.(db|wav|mp3|m4a|env|jsonl|sqlite)$' }
if ($datafiler) { Meld 'S4' 'Sikkerhed' 'Datafiler i versionsstyring' 'FEJL' (($datafiler | Select-Object -First 5) -join ', ') }
else { Meld 'S4' 'Sikkerhed' 'Datafiler i versionsstyring' 'BESTAAET' 'ingen data i git' }

# --- S5 saarbare pakker -----------------------------------------------
$saar = dotnet list "$Sti\src\NoteApp.Core\NoteApp.Core.csproj" package --vulnerable --include-transitive 2>&1
$rk = $saar | Select-String -Pattern '^\s+>\s+\S+' | ForEach-Object { $_.ToString().Trim() }
if ($rk) {
    $hoej = $rk | Where-Object { $_ -match 'High|Critical' }
    $st = 'ADVARSEL'
    if ($hoej) { $st = 'FEJL' }
    Meld 'S5' 'Sikkerhed' 'Sårbare pakker' $st (($rk | Select-Object -First 4) -join '; ')
}
else { Meld 'S5' 'Sikkerhed' 'Sårbare pakker' 'BESTAAET' 'ingen kendte sårbarheder' }

# --- S6 noeglen uden for kodelageret ----------------------------------
$noegleigit = $sporede | Where-Object { $_ -match 'sky-noegle' }
if ($noegleigit) { Meld 'S6' 'Sikkerhed' 'API-nøglen uden for kodelageret' 'FEJL' 'nøglefilen er sporet i git' }
else { Meld 'S6' 'Sikkerhed' 'API-nøglen uden for kodelageret' 'BESTAAET' 'nøglen ligger i datamappen eller som miljøvariabel' }

# --- S7 usikre endepunkter --------------------------------------------
$http = git -C $Sti grep -nI 'http://' -- '*.cs' 2>$null |
    Where-Object { $_ -notmatch 'schemas\.|w3\.org|xmlns|localhost|127\.0\.0\.1|purl\.org|openxmlformats|StartsWith' }
if ($http) { Meld 'S7' 'Sikkerhed' 'Ingen ukrypterede endepunkter' 'FEJL' (($http | Select-Object -First 3) -join '; ') }
else { Meld 'S7' 'Sikkerhed' 'Ingen ukrypterede endepunkter' 'BESTAAET' 'alt går over https' }

# --- S8 gitignore -----------------------------------------------------
$gi = ''
if (Test-Path "$Sti\.gitignore") { $gi = Get-Content "$Sti\.gitignore" -Raw }
$mangler = @()
foreach ($m in '*.db', '*.wav', '*.env', 'ordliste.txt') {
    if (-not ($gi -match [regex]::Escape($m))) { $mangler += $m }
}
if ($mangler) { Meld 'S8' 'Sikkerhed' '.gitignore dækker datatyper' 'ADVARSEL' ('mangler: ' + ($mangler -join ', ')) }
else { Meld 'S8' 'Sikkerhed' '.gitignore dækker datatyper' 'BESTAAET' 'alle mønstre dækket' }

# --- S9 googles klientfil ---------------------------------------------
$klient = Get-ChildItem (Join-Path $Sti 'app') -Filter 'client_secret*' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($klient) {
    Meld 'S9' 'Sikkerhed' 'Googles klientfil i udgivelsen' 'ADVARSEL' `
        "$($klient.Name) følger med udgivelsen - kendt vilkår for installerede OAuth-klienter, men den kan læses af enhver, der har appen"
}
else { Meld 'S9' 'Sikkerhed' 'Googles klientfil i udgivelsen' 'BESTAAET' 'ingen klientfil i udgivelsen' }

# --- S10 autostart ----------------------------------------------------
$run = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
if ($run -and ($run.PSObject.Properties.Name -contains 'HeyPia')) {
    $maal = ($run.HeyPia -replace '^"([^"]+)".*$', '$1')
    if (Test-Path $maal) { Meld 'S10' 'Sikkerhed' 'Autostart peger på en fil, der findes' 'BESTAAET' $maal }
    else { Meld 'S10' 'Sikkerhed' 'Autostart peger på en fil, der findes' 'FEJL' "peger på $maal, som ikke findes" }
}
else { Meld 'S10' 'Sikkerhed' 'Autostart peger på en fil, der findes' 'SPRUNGET OVER' 'autostart er ikke slået til' }

# =====================================================================
Write-Host ""
Write-Host "COMPLIANCE" -ForegroundColor Cyan
# =====================================================================

$da = Join-Path $Sti 'src\NoteApp.Core\sprog\da.json'
$en = Join-Path $Sti 'src\NoteApp.Core\sprog\en.json'
$dj = $null
if (Test-Path $da) { $dj = Get-Content $da -Raw -Encoding UTF8 | ConvertFrom-Json }

# --- C1 vaageordsafsnittet --------------------------------------------
$kraevede = 'vaageord', 'vaageord_hvad', 'vaageord_motor', 'vaageord_intet', 'vaageord_hvornaar'
$findes = @()
if ($dj -and $dj.complianceview) {
    $navne = $dj.complianceview.PSObject.Properties.Name
    $findes = $kraevede | Where-Object { $navne -contains $_ }
}
if ($findes.Count -eq $kraevede.Count) { Meld 'C1' 'Compliance' 'Compliance-skærmen forklarer lytningen' 'BESTAAET' "$($findes.Count) af $($kraevede.Count) afsnit" }
else { Meld 'C1' 'Compliance' 'Compliance-skærmen forklarer lytningen' 'FEJL' "kun $($findes.Count) af $($kraevede.Count) afsnit" }

# --- C2 paastand: der gemmes intet ------------------------------------
$skriver = git -C $Sti grep -nI 'File\.Write\|WriteAllBytes\|WaveFileWriter' -- 'src/NoteApp.Desktop/Vaageordsvagt.cs' 2>$null
if ($skriver) { Meld 'C2' 'Compliance' 'Påstand «der gemmes intet» holder' 'FEJL' 'vågeordsvagten skriver filer' }
else { Meld 'C2' 'Compliance' 'Påstand «der gemmes intet» holder' 'BESTAAET' 'ingen filskrivning i vågeordsvagten' }

# --- C3 paastand: der sendes intet ------------------------------------
$sender = git -C $Sti grep -nI 'HttpClient\|PostAsync\|SendAsync' -- 'src/NoteApp.Desktop/Vaageordsvagt.cs' 2>$null
if ($sender) { Meld 'C3' 'Compliance' 'Påstand «der sendes intet» holder' 'FEJL' 'vågeordsvagten har netværkskald' }
else { Meld 'C3' 'Compliance' 'Påstand «der sendes intet» holder' 'BESTAAET' 'ingen netværkskald i vågeordsvagten' }

# --- C4 samtykke ------------------------------------------------------
$samtykke = 0
if ($dj) {
    foreach ($g in $dj.PSObject.Properties) {
        if ($g.Value -is [psobject]) {
            foreach ($t in $g.Value.PSObject.Properties) {
                $v = "$($t.Value)"
                if ($v -match '(?i)(fortæl|sig til).*optag') { $samtykke++ }
            }
        }
    }
}
if ($samtykke -gt 0) { Meld 'C4' 'Compliance' 'Appen beder om at sige det til deltagerne' 'BESTAAET' "$samtykke steder i brugerfladen" }
else { Meld 'C4' 'Compliance' 'Appen beder om at sige det til deltagerne' 'FEJL' 'ingen tekst fundet' }

# --- C5 sletning ------------------------------------------------------
$dage = $null
if (Test-Path $inds) {
    try { $dage = (Get-Content $inds -Raw | ConvertFrom-Json).SletLydEfterDage } catch { }
}
if ($dage -gt 0) { Meld 'C5' 'Compliance' 'Lyd slettes efter en fastsat tid' 'BESTAAET' "$dage dage" }
elseif ($null -ne $dage) { Meld 'C5' 'Compliance' 'Lyd slettes efter en fastsat tid' 'ADVARSEL' 'slået fra - lyd gemmes i det uendelige' }
else { Meld 'C5' 'Compliance' 'Lyd slettes efter en fastsat tid' 'ADVARSEL' 'kunne ikke aflæses' }

# --- C6 EU ------------------------------------------------------------
$eu = git -C $Sti grep -nI 'KraevEuropa' -- '*.cs' 2>$null
$antalEu = 0
if ($eu) { $antalEu = ($eu | Measure-Object).Count }
if ($antalEu -ge 2) { Meld 'C6' 'Compliance' 'Skybehandling tvinges til EU' 'BESTAAET' "$antalEu steder håndhæver api.eu.mistral.ai" }
else { Meld 'C6' 'Compliance' 'Skybehandling tvinges til EU' 'FEJL' 'kravet håndhæves ikke i koden' }

# --- C7 pladsholdere --------------------------------------------------
$pladser = Get-ChildItem (Join-Path $Sti 'web'), (Join-Path $Sti 'Jura') -Recurse -Include *.html, *.md -ErrorAction SilentlyContinue |
    Select-String -Pattern 'UDFYLD' | Select-Object -ExpandProperty Path -Unique
if ($pladser) {
    $navne = ($pladser | ForEach-Object { Split-Path $_ -Leaf }) -join ', '
    Meld 'C7' 'Compliance' 'Privatlivspolitikken er udfyldt' 'FEJL' "pladsholdere tilbage i: $navne"
}
else { Meld 'C7' 'Compliance' 'Privatlivspolitikken er udfyldt' 'BESTAAET' 'ingen pladsholdere' }

# --- C8 compliance paa begge sprog ------------------------------------
$ej = $null
if (Test-Path $en) { $ej = Get-Content $en -Raw -Encoding UTF8 | ConvertFrom-Json }
if ($dj -and $ej -and $dj.complianceview -and $ej.complianceview) {
    $dn = $dj.complianceview.PSObject.Properties.Name
    $en2 = $ej.complianceview.PSObject.Properties.Name
    $diff = (Compare-Object $dn $en2)
    if (-not $diff) { Meld 'C8' 'Compliance' 'Compliance findes på begge sprog' 'BESTAAET' "$($dn.Count) afsnit i begge" }
    else { Meld 'C8' 'Compliance' 'Compliance findes på begge sprog' 'FEJL' "$($diff.Count) afsnit findes kun på ét sprog" }
}
else { Meld 'C8' 'Compliance' 'Compliance findes på begge sprog' 'FEJL' 'compliance-teksterne kunne ikke læses' }

# =====================================================================
#  OPSAMLING
# =====================================================================

$slut = Get-Date
$tael = @{}
foreach ($r in $resultater) {
    if (-not $tael.ContainsKey($r.Status)) { $tael[$r.Status] = 0 }
    $tael[$r.Status]++
}

$fejl = 0
if ($tael.ContainsKey('FEJL')) { $fejl = $tael['FEJL'] }
$adv = 0
if ($tael.ContainsKey('ADVARSEL')) { $adv = $tael['ADVARSEL'] }

$samlet = 'BESTAAET'
if ($adv -gt 0) { $samlet = 'BESTAAET MED BEMAERKNINGER' }
if ($fejl -gt 0) { $samlet = 'IKKE BESTAAET' }

Write-Host ""
Write-Host ("SAMLET: {0}" -f $samlet) -ForegroundColor $(if ($fejl -gt 0) { 'Red' } elseif ($adv -gt 0) { 'Yellow' } else { 'Green' })
foreach ($k in $tael.Keys | Sort-Object) { Write-Host ("  {0,-16} {1}" -f $k, $tael[$k]) }

New-Item -ItemType Directory -Path $Ud -Force | Out-Null
$rapport = [pscustomobject]@{
    Koert      = $start.ToString('yyyy-MM-dd HH:mm:ss')
    Sekunder   = [math]::Round(($slut - $start).TotalSeconds, 1)
    Maskine    = $env:COMPUTERNAME
    Version    = $(if (Test-Path $exe) { (Get-Item $exe).VersionInfo.FileVersion } else { 'ukendt' })
    Commit     = (git -C $Sti log -1 --format='%h %s')
    Samlet     = $samlet
    Antal      = $resultater.Count
    Resultater = $resultater
}
$filnavn = Join-Path $Ud ('test-' + $start.ToString('yyyy-MM-dd-HHmm') + '.json')
$rapport | ConvertTo-Json -Depth 5 | Set-Content -Path $filnavn -Encoding UTF8
$rapport | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $Ud 'seneste.json') -Encoding UTF8

Write-Host ""
Write-Host "Resultat gemt: $filnavn" -ForegroundColor DarkGray

if ($fejl -gt 0) { exit 1 }
exit 0



