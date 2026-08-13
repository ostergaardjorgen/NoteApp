<#
.SYNOPSIS
    Tæller, hvilke SLAGS fejl der er i en transskription af en oplæsning.

.DESCRIPTION
    Spørgsmålet, scriptet svarer på, er ikke «hvor mange fejl er der» — det er
    «hvor stor en del af dem kan en rettelsesregel klare».

    Det afgør, om ordbogen er en god handel eller en trøstepræmie:

      NAVNEFEJL    fagord og navne hørt forkert. En regel retter dem præcist,
                   med det samme, og kan fortrydes.
      TALFEJL      et tal blev et andet tal. INGEN regel kan vide, hvilket der
                   var det rigtige — begge er gyldige. Den dyreste slags.
      ORDFEJL      et gyldigt dansk ord blev et andet gyldigt dansk ord. En
                   regel her ville rette rigtige ord til forkerte andre steder.
      SMÅTING      tegnsætning, sammenskrivning, fyldord.

    Kun oplæsningerne kan måles sådan, fordi manuskriptet er facit. Et rigtigt
    møde har intet at holde teksten op imod.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\tael-fejltyper.ps1 `
        -Manuskript C:\NoteApp\fase0\oplaesning\testtekst.md `
        -Udskrift  "C:\AppNoter\Optagelser\...\mikrofon_large-v3.txt"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Manuskript,
    [Parameter(Mandatory)] [string] $Udskrift,
    [switch] $Alle
)

$ErrorActionPreference = 'Stop'

# --- Manuskriptet: kun det, der faktisk blev læst højt ---------------------
# Overskrifter, vejledning og facitliste læses ikke op og skal derfor ikke
# tælle med. Alt før første blok er vejledning.
$linjer = Get-Content $Manuskript -Encoding UTF8
$iBlok = $false
$manus = foreach ($l in $linjer) {
    if ($l -match '^##\s') { $iBlok = $true; continue }
    if ($l -match '^#\s')  { continue }
    if ($iBlok) { $l }
}

function Ord($tekst) {
    ($tekst -join ' ') -replace '\*\*','' -split '[^\wæøåÆØÅ]+' |
        Where-Object { $_ -and $_.Length -gt 0 }
}

$a = @(Ord $manus)
$b = @(Ord (Get-Content $Udskrift -Encoding UTF8))

Write-Host ("Manuskript: {0} ord" -f $a.Count)
Write-Host ("Udskrift  : {0} ord" -f $b.Count)
Write-Host ""

# --- Ordret sammenligning -------------------------------------------------
# Compare-Object med -SyncWindow finder par, der er flyttet lidt. Uden det
# ville ét manglende ord få resten af teksten til at se forkert ud.
$diff = Compare-Object $a $b -SyncWindow 25 | Where-Object { $_.SideIndicator -ne '==' }

$mangler = @($diff | Where-Object SideIndicator -eq '<=' | ForEach-Object { $_.InputObject })
$ekstra  = @($diff | Where-Object SideIndicator -eq '=>' | ForEach-Object { $_.InputObject })

# --- Klassificering -------------------------------------------------------
$talord = 'nul|en|et|to|tre|fire|fem|seks|syv|otte|ni|ti|elleve|tolv|tretten|fjorten|femten|seksten|sytten|atten|nitten|tyve|tredive|fyrre|halvtreds|tres|halvfjerds|firs|halvfems|hundrede|tusind'

function Slags($ord) {
    if ($ord -match '^\d+$' -or $ord -match "^($talord)$") { return 'TAL' }
    if ($ord -cmatch '^[A-ZÆØÅ]') { return 'NAVN' }
    if ($ord.Length -le 3) { return 'SMAAT' }
    return 'ORD'
}

$tael = @{ TAL = 0; NAVN = 0; ORD = 0; SMAAT = 0 }
foreach ($o in $mangler) { $tael[(Slags $o)]++ }

$ialt = ($tael.Values | Measure-Object -Sum).Sum
if ($ialt -eq 0) { Write-Host "Ingen forskelle fundet."; return }

Write-Host "HVAD MANGLER ELLER BLEV HØRT FORKERT ($ialt ord)" -ForegroundColor Cyan
Write-Host ""

$rk = @(
    @{ n='NAVN';  t='Navne og fagord'; f='Green';  h='en regel retter dem præcist' }
    @{ n='TAL';   t='Tal';             f='Red';    h='INGEN regel kan vide, hvad der var rigtigt' }
    @{ n='ORD';   t='Almindelige ord'; f='Yellow'; h='en regel ville rette rigtige ord forkert andre steder' }
    @{ n='SMAAT'; t='Småord';          f='Gray';   h='fyldord og korte ord — betyder sjældent noget' }
)

foreach ($r in $rk) {
    $n = $tael[$r.n]
    $pct = if ($ialt -gt 0) { 100.0 * $n / $ialt } else { 0 }
    Write-Host ("  {0,-18} {1,4}   {2,5:0.0} %   {3}" -f $r.t, $n, $pct, $r.h) -ForegroundColor $r.f
}

Write-Host ""
$kan = $tael['NAVN']
Write-Host ("Kan rettes med en regel: {0} af {1} = {2:0} %" -f $kan, $ialt, (100.0*$kan/$ialt)) -ForegroundColor Green
Write-Host ("Kan IKKE: tal og gyldige ord: {0} = {1:0} %" -f ($tael['TAL']+$tael['ORD']), (100.0*($tael['TAL']+$tael['ORD'])/$ialt)) -ForegroundColor Red

if ($Alle) {
    Write-Host ""
    Write-Host "--- alle afvigelser ---"
    foreach ($o in $mangler) { "  manglede: {0,-24} [{1}]" -f $o, (Slags $o) }
    foreach ($o in $ekstra | Select-Object -First 40) { "  kom til : {0}" -f $o }
}
