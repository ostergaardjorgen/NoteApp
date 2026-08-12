<#
.SYNOPSIS
    Giver et referat karakter ud fra facitlisten til testteksten.

.DESCRIPTION
    Bruges til at sammenligne sprogmodeller på det SAMME materiale. Uden en
    fast målestok bliver sammenligningen til smagsdommeri, og så vælger man
    den model, hvis sprog man bedst kan lide — ikke den, der får indholdet med.

    Facit står i fase0\oplaesning\facitliste.md og gælder kun testteksten.
    Køres scriptet på et rigtigt møde, giver tallene ingen mening.

    Det vigtigste tjek er ikke, hvor meget der er med, men om der står tal,
    som ikke blev sagt. Et opdigtet tal i et referat ser rigtigt ud og er
    derfor den dyreste fejl af dem alle.

    To ting læres af første udgave af dette script, og de er skrevet ind i
    koden nedenfor:

      Tal skal læses i deres sammenhæng. Et mønster på "(\d+) timer" fandt
      "25 timer" inde i "301,25 timer" og anklagede modellen for en fejl,
      den ikke havde begået. En bedømmelse, der finder på fejl, er værre
      end ingen bedømmelse.

      Afsnit skal holdes adskilt. Tælles ejere i hele teksten, tæller
      deltagerlisten med, og alle modeller får 7 ud af 7 uanset hvad de
      skrev under Opgaver.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\bedoem-referat.ps1 -Mappe "C:\AppNoter\Optagelser\proeve-referat\udkast"
#>
[CmdletBinding()]
param(
    [string] $Udkast,
    [string] $Mappe,
    [switch] $Detaljer
)

$ErrorActionPreference = 'Stop'

# --- Facit ----------------------------------------------------------------

# De seks beslutninger, genkendt paa ord der ikke kan skrives udenom.
$beslutninger = @(
    @{ navn = 'Kvartersloesning paa ERP-filen'; moenster = 'kvarter' },
    @{ navn = 'Attestering -> suspendering';    moenster = 'suspender' },
    @{ navn = 'Noedadgang med loft';            moenster = 'nødadgang' },
    @{ navn = 'MitID Erhverv udskydes';         moenster = 'MitID' },
    @{ navn = 'Servicekonti ryddes op';         moenster = 'servicekonti' },
    @{ navn = 'Sprogbrug -> modtagersystemer';  moenster = 'modtagersystem' }
)

# De syv opgaver kendes paa ejeren, og ejeren skal staa under Opgaver.
$ejere = @('Anders', 'Malene', 'Thomas', 'Sofie', 'Rasmus', 'Camilla', 'revisor')

# Tal der BLEV sagt, hver med det ord de hoerer til. Uden ankeret bliver
# ethvert tilfaeldigt tal i teksten sammenlignet med facit.
$tal = @(
    @{ navn = 'Noedadgang, timer'; anker = 'nødadgang'; enhed = 'timer'; facit = @('24','fireogtyve') },
    @{ navn = 'Attestering, dage'; anker = 'attester';  enhed = '(?:hverdage|dage)'; facit = @('14','fjorten') }
)

# Danske talord, saa "fireogtyve timer" ikke laeses som ingen angivelse.
$talord = 'nul|en|et|to|tre|fire|fem|seks|syv|otte|ni|ti|elleve|tolv|tretten|fjorten|femten|seksten|sytten|atten|nitten|tyve|enogtyve|toogtyve|treogtyve|fireogtyve|femogtyve|tredive|fyrre|toogfyrre|halvtreds'

# Ord der roeber, at modellen er drevet over i engelsk.
$engelsk = '\b(the|and|user|should|meeting|summary|decisions|action items|however|following|were)\b'

# --- Hjaelpere ------------------------------------------------------------

# Teksten mellem en overskrift og den naeste. Uden den taeller punkter fra
# de efterfoelgende afsnit med i det forrige.
function Sektion([string] $krop, [string] $overskrift) {
    $m = [regex]::Match($krop, "(?ms)^#{1,3}\s*$overskrift\b(.*?)(?=^#{1,3}\s|\z)")
    if ($m.Success) { return $m.Groups[1].Value }
    return ''
}

# Et tal naevnt taet paa sit anker. Vinduet er 200 tegn — laengere, og et
# tal fra den naeste saetning bliver taget til indtaegt for denne.
function TalNaer([string] $krop, [string] $anker, [string] $enhed) {
    foreach ($a in [regex]::Matches($krop, $anker, 'IgnoreCase')) {
        $fra = [Math]::Max(0, $a.Index - 200)
        $til = [Math]::Min($krop.Length, $a.Index + 200)
        $vindue = $krop.Substring($fra, $til - $fra)

        # (?<![\d,.]) holder tallet fri af "301,25" — der staar 301,25 timer
        # i teksten, og "25" derfra er ikke en angivelse af noget.
        $t = [regex]::Match($vindue, "(?<![\d,.])(\d{1,3}|$talord)\s*$enhed", 'IgnoreCase')
        if ($t.Success) { return $t.Groups[1].Value }
    }
    return $null
}

function Bedoem([string] $sti) {
    $raw  = Get-Content $sti -Raw -Encoding UTF8
    $dele = $raw -split '(?m)^---\s*$'
    $krop = if ($dele.Count -gt 2) { ($dele[2..($dele.Count-1)] -join "`n") } else { $raw }

    $model = if ($raw -match '(?m)^model:\s*(.+)$') { $Matches[1].Trim() } else { Split-Path $sti -Leaf }
    $tid   = if ($raw -match '(?m)^tid:\s*(.+)$')   { $Matches[1].Trim() } else { '' }

    $sResume  = Sektion $krop 'Resumé'
    $sBeslut  = Sektion $krop 'Beslutninger'
    $sOpgaver = Sektion $krop 'Opgaver'
    $sAabne   = Sektion $krop 'Åbne spørgsmål'

    # Manglende overskrifter er i sig selv en fejl — saa er strukturen brudt.
    $struktur = @('Resumé','Beslutninger','Opgaver','Åbne spørgsmål') |
        Where-Object { -not (Sektion $krop $_) }

    # En beslutning taeller, hvis den staar i resumeet eller under Beslutninger.
    $iBeslutning = "$sResume`n$sBeslut"
    $b = ($beslutninger | Where-Object { $iBeslutning -match $_.moenster }).Count

    # Ejere taelles KUN under Opgaver. Ellers taeller deltagerlisten med.
    $o = ($ejere | Where-Object { $sOpgaver -match $_ }).Count

    # Punkter under Aabne spoergsmaal, ikke resten af dokumentet.
    $aabne = ([regex]::Matches($sAabne, '(?m)^\s*(?:[-*•]|\d+\.)\s+\S')).Count

    $eng = ([regex]::Matches($krop, $engelsk, 'IgnoreCase')).Count

    $fejl = @()
    foreach ($t in $tal) {
        $fundet = TalNaer $krop $t.anker $t.enhed
        if (-not $fundet) { continue }
        if ($t.facit -notcontains $fundet.ToLower()) {
            $fejl += "$($t.navn): '$fundet' (facit $($t.facit[0]))"
        }
    }

    [PSCustomObject]@{
        Model        = $model
        Tid          = $tid
        Beslutninger = "$b/6"
        Opgaver      = "$o/7"
        AabneSp      = "$aabne/7"
        Engelsk      = $eng
        Talfejl      = $fejl.Count
        Mangler      = ($struktur -join ', ')
        Note         = ($fejl -join '; ')
        Fil          = Split-Path $sti -Leaf
    }
}

# --- Kør ------------------------------------------------------------------
$filer = @()
if     ($Mappe)  { $filer = Get-ChildItem $Mappe -Filter '*.md' | Sort-Object LastWriteTime | Select-Object -ExpandProperty FullName }
elseif ($Udkast) { $filer = @($Udkast) }
else   { throw 'Angiv enten -Udkast <fil> eller -Mappe <mappe>' }

if ($filer.Count -eq 0) { throw 'Ingen udkast fundet.' }

$resultater = foreach ($f in $filer) { Bedoem $f }

$resultater | Format-Table Model, Tid, Beslutninger, Opgaver, AabneSp, Engelsk, Talfejl -AutoSize

foreach ($r in ($resultater | Where-Object { $_.Mangler })) {
    Write-Host "MANGLER AFSNIT  $($r.Fil): $($r.Mangler)" -ForegroundColor Yellow
}

$medFejl = $resultater | Where-Object { $_.Talfejl -gt 0 }
if ($medFejl) {
    Write-Host ''
    Write-Host 'OPDIGTEDE TAL — den dyreste fejl i et referat:' -ForegroundColor Red
    foreach ($r in $medFejl) { Write-Host "  $($r.Fil): $($r.Note)" }
}
