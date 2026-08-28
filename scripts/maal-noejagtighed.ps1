<#
.SYNOPSIS
    Måler hvor godt Whisper ramte oplæsningsteksten: ordfejlrate, fagtermer og
    negationer.

.DESCRIPTION
    Sammenligner en transskription med referenceteksten i
    fase0\oplaesning\testtekst.md og giver tre tal:

      1. WER (word error rate) — andelen af ord der blev indsat, slettet eller
         byttet. Det brede mål.
      2. Fagtermer — ramte den de IAM-ord, ordlisten er sat i verden for?
      3. Negationer — overlevede hver eneste "ikke"? En mistet negation vender
         betydningen om, og det er den farligste fejlklasse i hele projektet.

    Tal skrives i teksten med bogstaver, fordi det er sådan de udtales.
    Whisper skriver dem med cifre. Scriptet oversætter cifrene tilbage, så WER
    ikke straffer noget, der faktisk blev hørt rigtigt.

.EXAMPLE
    .\maal-noejagtighed.ps1

.EXAMPLE
    .\maal-noejagtighed.ps1 -Transskription 'C:\NoteApp\fase0\transskriptioner\...large-v3_medOrdliste.txt'
#>
[CmdletBinding()]
param(
    # Udelades den, sammenlignes alle .txt-filer for den nyeste optagelse.
    [string] $Transskription,

    [string] $Reference = (Join-Path (Split-Path -Parent $PSScriptRoot) 'fase0\oplaesning\testtekst.md')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Reference)) { throw "Referencetekst findes ikke: $Reference" }

# --- Hurtig Levenshtein på ordniveau -------------------------------------
# PowerShell er for langsom til en 2700 x 2700 matrix. C# klarer den på et
# øjeblik.
if (-not ('HeyPia.Wer' -as [type])) {
    Add-Type -TypeDefinition @'
namespace HeyPia {
    public static class Wer {
        public static int[] Distance(string[] reference, string[] hypothesis) {
            int n = reference.Length, m = hypothesis.Length;
            int[,] d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++) {
                    int cost = reference[i - 1] == hypothesis[j - 1] ? 0 : 1;
                    int del = d[i - 1, j] + 1;
                    int ins = d[i, j - 1] + 1;
                    int sub = d[i - 1, j - 1] + cost;
                    d[i, j] = System.Math.Min(System.Math.Min(del, ins), sub);
                }
            // Gå baglaens gennem matricen og tael hver fejltype for sig.
            int subs = 0, dels = 0, inss = 0;
            int x = n, y = m;
            while (x > 0 || y > 0) {
                if (x > 0 && y > 0 && d[x, y] == d[x - 1, y - 1] && reference[x - 1] == hypothesis[y - 1]) { x--; y--; }
                else if (x > 0 && y > 0 && d[x, y] == d[x - 1, y - 1] + 1) { subs++; x--; y--; }
                else if (x > 0 && d[x, y] == d[x - 1, y] + 1) { dels++; x--; }
                else { inss++; y--; }
            }
            return new int[] { d[n, m], subs, dels, inss };
        }
    }
}
'@
}

# --- Normalisering --------------------------------------------------------
# Whisper skriver cifre; teksten er skrevet med bogstaver, fordi det er sådan
# den laeses op. Uden denne oversaettelse ville hvert korrekt hoert tal taelle
# som en fejl.
$talOversaettelse = [ordered]@{
    '114.000' = 'hundrede og fjorten tusind'
    '950'     = 'ni hundrede og halvtreds'
    '412'     = 'fire hundrede og tolv'
    '388'     = 'tre hundrede og otteogfirs'
    '120'     = 'hundrede og tyve'
    '97'      = 'syvoghalvfems'
    '96'      = 'seksoghalvfems'
    '64'      = 'fireogtres'
    '24'      = 'fireogtyve'
    '23.'     = 'treogtyvende'
    '16'      = 'seksten'
    '15'      = 'femten'
    '14'      = 'fjorten'
    '12'      = 'tolv'
    '11.'     = 'ellevte'
    '10'      = 'ti'
    '7'       = 'syv'
    '6'       = 'seks'
    '4,2'     = 'fire komma to'
    '4'       = 'fire'
    '3'       = 'tre'
    '2'       = 'to'
}

function ConvertTo-Normaliseret([string] $tekst) {
    $t = $tekst.ToLowerInvariant()
    foreach ($n in $talOversaettelse.Keys) {
        # \w og ikke \d i lookaround'en: ellers bliver 2-tallet i "NIS2"
        # oversat til "to", saa fagtermen hedder "nisto" og aldrig kan findes.
        # Tal skal kun oversaettes naar de staar alene, ikke naar de sidder
        # fast paa et ord.
        $t = $t -replace ('(?<![\w.,])' + [regex]::Escape($n) + '(?![\w.,])'), $talOversaettelse[$n]
    }
    # Behold bogstaver, tal og mellemrum. Bindestreg bliver til mellemrum, saa
    # "CPR-nummer" og "CPR nummer" taeller som det samme.
    $t = $t -replace '[-–—/]', ' '
    $t = $t -replace '[^\p{L}\p{Nd} ]', ' '
    return ($t -replace '\s+', ' ').Trim()
}

function Get-Referencetekst([string] $sti) {
    $linjer = Get-Content $sti -Encoding UTF8
    # Alt foer den foerste blok er laesevejledning, ikke noget der laeses op.
    $start = ($linjer | Select-String -Pattern '^## Blok 1' | Select-Object -First 1).LineNumber
    if (-not $start) { throw "Fandt ikke '## Blok 1' i $sti" }
    $krop = $linjer[$start..($linjer.Count - 1)]
    # Overskrifter laeses ikke op.
    $krop = $krop | Where-Object { $_ -notmatch '^#{1,6} ' -and $_ -notmatch '^---\s*$' }
    return ($krop -join ' ')
}

$refOrd = (ConvertTo-Normaliseret (Get-Referencetekst $Reference)) -split ' '
Write-Host "Reference: $($refOrd.Count) ord" -ForegroundColor DarkGray

# --- Find transskriptioner ------------------------------------------------
if ($Transskription) {
    $filer = @(Get-Item $Transskription)
} else {
    $nyesteOptagelse = Get-ChildItem (Join-Path $root 'fase0\optagelser') -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $nyesteOptagelse) { throw "Ingen optagelser fundet." }
    $filer = @(Get-ChildItem (Join-Path $root 'fase0\transskriptioner') -Filter "$($nyesteOptagelse.Name)*.txt" -ErrorAction SilentlyContinue)
    if (-not $filer) { throw "Ingen transskriptioner for $($nyesteOptagelse.Name). Kør koer-fase0-test.ps1 først." }
}

# --- Facit ----------------------------------------------------------------
$fagtermer = @(
    'entra id', 'scim', 'microsoft graph', 'exchange online', 'mitid erhverv',
    'kernesys', 'provisionering', 'deprovisionering', 'attestering',
    'offboardet', 'rate limiting', 'nis2', 'cpr nummer', 'cvr dimension',
    'modtagersystemer', 'jit adgang', 'pim', 'iso 27001', 'utc'
)

$negationer = @(
    'det er ikke acceptabelt'
    'heller ikke noget vi kan forklare'
    'jeg vil ikke anbefale det'
    'jeg vil ikke love en dato'
    'vi skal ikke lave om på attesteringsintervallet'
    'adgangen må ikke bare fortsætte'
    'tavshed skal ikke være det samme'
    'den skal ikke kunne gives ubegrænset'
    'den skal ikke kunne forlænges i det uendelige'
    'det er ikke en bekvemmelighed'
    'og det er ikke rigtigt'
    'den ikke nulstiller kundens ændringer'
    'involverer ikke leverandøren'
    'ikke af hensyn til pris'
    'så hun ikke går og regner med noget andet'
    'den skalerer ikke'
    'jeg vil ikke sige det højt til en kunde'
)

$negNorm = $negationer | ForEach-Object { ConvertTo-Normaliseret $_ }
$fagNorm = $fagtermer | ForEach-Object { ConvertTo-Normaliseret $_ }

$rapport = @()

foreach ($fil in $filer) {
    $raa = Get-Content $fil.FullName -Raw -Encoding UTF8
    $normTekst = ConvertTo-Normaliseret $raa
    $hypOrd = $normTekst -split ' '

    $r = [HeyPia.Wer]::Distance($refOrd, $hypOrd)
    $wer = [math]::Round(100 * $r[0] / $refOrd.Count, 2)

    $fagRamt = @($fagNorm | Where-Object { $normTekst -like "*$_*" })
    $fagMistet = @($fagNorm | Where-Object { $normTekst -notlike "*$_*" })
    $negRamt = @($negNorm | Where-Object { $normTekst -like "*$_*" })
    $negMistet = @()
    for ($i = 0; $i -lt $negNorm.Count; $i++) {
        if ($normTekst -notlike "*$($negNorm[$i])*") { $negMistet += $negationer[$i] }
    }

    Write-Host ""
    Write-Host "=== $($fil.BaseName) ===" -ForegroundColor Cyan
    Write-Host ("  Ord i transskription : {0}" -f $hypOrd.Count)
    $werFarve = if ($wer -lt 5) { 'Green' } elseif ($wer -lt 12) { 'Yellow' } else { 'Red' }
    Write-Host ("  Ordfejlrate (WER)    : {0} %   ({1} bytninger, {2} manglende, {3} ekstra)" -f $wer, $r[1], $r[2], $r[3]) -ForegroundColor $werFarve
    $fagFarve = if ($fagRamt.Count -ge 18) { 'Green' } elseif ($fagRamt.Count -ge 15) { 'Yellow' } else { 'Red' }
    Write-Host ("  Fagtermer ramt       : {0} af {1}" -f $fagRamt.Count, $fagNorm.Count) -ForegroundColor $fagFarve
    $negFarve = if ($negMistet.Count -eq 0) { 'Green' } else { 'Red' }
    Write-Host ("  Negationer bevaret   : {0} af {1}" -f $negRamt.Count, $negNorm.Count) -ForegroundColor $negFarve

    if ($fagMistet) {
        Write-Host "  Mistede fagtermer:" -ForegroundColor Yellow
        $fagMistet | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
    }
    if ($negMistet) {
        Write-Host "  MISTEDE NEGATIONER (betydningen kan være vendt om):" -ForegroundColor Red
        $negMistet | ForEach-Object { Write-Host "    - $_" -ForegroundColor Red }
    }

    $rapport += [pscustomobject]@{
        Transskription = $fil.BaseName
        Ord            = $hypOrd.Count
        WER            = $wer
        Bytninger      = $r[1]
        Manglende      = $r[2]
        Ekstra         = $r[3]
        FagtermerRamt  = "$($fagRamt.Count)/$($fagNorm.Count)"
        NegationerOK   = "$($negRamt.Count)/$($negNorm.Count)"
    }
}

Write-Host ""
Write-Host "=== Samlet ===" -ForegroundColor Cyan
$rapport | Sort-Object WER | Format-Table -AutoSize

$ud = Join-Path $root "fase0\resultater\noejagtighed_$(Get-Date -Format 'yyyy-MM-dd_HH-mm').csv"
New-Item -ItemType Directory -Force (Split-Path $ud) | Out-Null
$rapport | Export-Csv $ud -NoTypeInformation -Encoding UTF8
Write-Host "Gemt: $ud"

$bedst = $rapport | Sort-Object WER | Select-Object -First 1
Write-Host ""
Write-Host "Bedste kørsel: $($bedst.Transskription) med $($bedst.WER) % WER." -ForegroundColor Green
Write-Host "Sammenlign medOrdliste mod udenOrdliste — forskellen dér er værdien af ordlisten."
Write-Host "Er en negation mistet, er afspilning synkroniseret med transskript ikke valgfri."
