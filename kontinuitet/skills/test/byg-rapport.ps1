# =====================================================================
#  Testrapport i Word ud fra en testkørsel
# =====================================================================
#
#  Læser resultatet fra koer-test.ps1 og skriver et Word-dokument.
#
#  TALLENE KOMMER FRA JSON-FILEN. Der skrives intet tal i hånden her -
#  en rapport, der siger noget andet end kørslen, er værre end ingen.
#
#  Til sidst åbnes dokumentet igen og læses igennem. Et afsnit, der
#  forsvinder undervejs, kan ikke ses på et script, der sagde "færdig".
# =====================================================================

param(
    [string]$Resultat = 'C:\AppNoter\test\seneste.json',
    [string]$Ud = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Resultat)) { throw "Findes ikke: $Resultat. Kør koer-test.ps1 først." }

$r = Get-Content $Resultat -Raw -Encoding UTF8 | ConvertFrom-Json
$dato = [datetime]::ParseExact($r.Koert, 'yyyy-MM-dd HH:mm:ss', $null)

if ($Ud -eq '') {
    $Ud = 'C:\NoteApp\Jura\Testrapport HeyPia ' + $dato.ToString('yyyy-MM-dd') + '.docx'
}

# Word-stilenes indbyggede numre. Sproguafhaengige - navnene skifter med
# Words sprog, tallene goer ikke.
$Normal = -1; $H1 = -2; $H2 = -3; $H3 = -4; $Titel = -63

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$doc = $word.Documents.Add()
$v = $doc.Content

function Skriv {
    param([string]$Tekst, [int]$Stil = -1, [bool]$Fed = $false)
    $p = $script:doc.Paragraphs.Add()
    $p.Range.Text = $Tekst
    $p.Range.Style = $Stil
    if ($Fed) { $p.Range.Font.Bold = $true } else { $p.Range.Font.Bold = $false }
    $p.Range.InsertParagraphAfter()
}

function Farve {
    param($Status)
    if ($Status -eq 'FEJL') { return 192 }               # roed (BGR)
    if ($Status -eq 'ADVARSEL') { return 26367 }          # orange
    if ($Status -eq 'BESTAAET') { return 25600 }          # groen
    return 8421504                                        # graa
}

# JSON-filen bruger ASCII i statuskoderne, saa den kan laeses af hvad som
# helst. RAPPORTEN LAESES AF ET MENNESKE, og der staves der rigtigt.
function Vis {
    param([string]$Status)
    $o = @{
        'BESTAAET'                    = 'BESTÅET'
        'IKKE AFPROEVET'              = 'IKKE AFPRØVET'
        'BESTAAET MED BEMAERKNINGER'  = 'BESTÅET MED BEMÆRKNINGER'
        'IKKE BESTAAET'               = 'IKKE BESTÅET'
    }
    if ($o.ContainsKey($Status)) { return $o[$Status] }
    return $Status
}

# ---------------------------------------------------------------- forside
Skriv 'Testrapport' $Titel
Skriv ('HeyPia ' + $r.Version) $H2
Skriv ''
Skriv ('Kørt ' + $dato.ToString('d. MMMM yyyy \k\l. HH:mm')) $Normal
Skriv ('Maskine: ' + $r.Maskine) $Normal
Skriv ('Kodeversion: ' + $r.Commit) $Normal
Skriv ('Varighed: ' + ("$($r.Sekunder)" -replace '\.', ',') + ' sekunder') $Normal
Skriv ''

$p = $doc.Paragraphs.Add()
$p.Range.Text = 'Samlet resultat: ' + (Vis $r.Samlet)
$p.Range.Style = $H1
$p.Range.Font.Color = (Farve $(if ($r.Samlet -like 'IKKE*') { 'FEJL' } elseif ($r.Samlet -like '*BEMAERK*') { 'ADVARSEL' } else { 'BESTAAET' }))
$p.Range.InsertParagraphAfter()

# ---------------------------------------------------------------- optaelling
$tael = @{}
foreach ($t in $r.Resultater) {
    if (-not $tael.ContainsKey($t.Status)) { $tael[$t.Status] = 0 }
    $tael[$t.Status]++
}

Skriv 'Oversigt' $H2
$raekker = $tael.Keys.Count + 1
$t1 = $doc.Tables.Add($doc.Paragraphs.Add().Range, $raekker, 2)
$t1.Borders.Enable = $true
$t1.Cell(1, 1).Range.Text = 'Resultat'
$t1.Cell(1, 2).Range.Text = 'Antal'
$t1.Rows.Item(1).Range.Font.Bold = $true
$i = 2
foreach ($k in ($tael.Keys | Sort-Object)) {
    $t1.Cell($i, 1).Range.Text = (Vis $k)
    $t1.Cell($i, 2).Range.Text = "$($tael[$k])"
    $i++
}
$doc.Content.InsertParagraphAfter()

# ---------------------------------------------------------------- fund
$fejl = @($r.Resultater | Where-Object { $_.Status -eq 'FEJL' })
$adv = @($r.Resultater | Where-Object { $_.Status -eq 'ADVARSEL' })

if ($fejl.Count -gt 0 -or $adv.Count -gt 0) {
    Skriv 'Fund, der kræver handling' $H2
    foreach ($f in ($fejl + $adv)) {
        $p = $doc.Paragraphs.Add()
        $p.Range.Text = $f.Id + ' - ' + $f.Navn
        $p.Range.Style = $H3
        $p.Range.Font.Color = (Farve $f.Status)
        $p.Range.InsertParagraphAfter()
        Skriv ((Vis $f.Status) + ': ' + $f.Detalje) $Normal
    }
}

# ---------------------------------------------------------------- alle proever
foreach ($omr in 'Funktion', 'Sikkerhed', 'Compliance') {
    $del = @($r.Resultater | Where-Object { $_.Omraade -eq $omr })
    if ($del.Count -eq 0) { continue }

    Skriv $omr $H2
    $tb = $doc.Tables.Add($doc.Paragraphs.Add().Range, $del.Count + 1, 4)
    $tb.Borders.Enable = $true
    $tb.Cell(1, 1).Range.Text = 'Id'
    $tb.Cell(1, 2).Range.Text = 'Prøve'
    $tb.Cell(1, 3).Range.Text = 'Resultat'
    $tb.Cell(1, 4).Range.Text = 'Detalje'
    $tb.Rows.Item(1).Range.Font.Bold = $true

    $n = 2
    foreach ($d in $del) {
        $tb.Cell($n, 1).Range.Text = $d.Id
        $tb.Cell($n, 2).Range.Text = $d.Navn
        $tb.Cell($n, 3).Range.Text = (Vis $d.Status)
        $tb.Cell($n, 3).Range.Font.Color = (Farve $d.Status)
        $tb.Cell($n, 4).Range.Text = $d.Detalje
        $n++
    }
    $tb.Columns.Item(1).Width = 30
    $tb.Columns.Item(2).Width = 150
    $tb.Columns.Item(3).Width = 80
    $doc.Content.InsertParagraphAfter()
}

# ---------------------------------------------------------------- forbehold
Skriv 'Det, testen ikke kan svare på' $H2
Skriv ('En test, der skriver BESTÅET om noget, den ikke har målt, er værre end ingen test. ' +
    'To ting kan et script ikke afgøre, og de står derfor som IKKE AFPRØVET, indtil et ' +
    'menneske har set dem.') $Normal

$ikke = @($r.Resultater | Where-Object { $_.Status -eq 'IKKE AFPROEVET' -or $_.Status -eq 'DELVIS' -or $_.Status -eq 'SPRUNGET OVER' })
foreach ($d in $ikke) {
    Skriv ($d.Id + ' - ' + $d.Navn + ' (' + (Vis $d.Status) + '): ' + $d.Detalje) $Normal
}

# ---------------------------------------------------------------- gem
$mappe = Split-Path $Ud -Parent
if (-not (Test-Path $mappe)) { New-Item -ItemType Directory -Path $mappe -Force | Out-Null }
if (Test-Path $Ud) { Remove-Item $Ud -Force }

$doc.SaveAs([ref]$Ud, [ref]16)   # 16 = wdFormatDocumentDefault (.docx)
$doc.Close()
$word.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null

# ---------------------------------------------------------------- efterproev
# DOKUMENTET AABNES IGEN OG LAESES. Et afsnit, der forsvandt undervejs,
# kan ikke ses paa et script, der sagde "faerdig". Det er sket foer.
$word2 = New-Object -ComObject Word.Application
$word2.Visible = $false
$d2 = $word2.Documents.Open($Ud, [ref]$false, [ref]$true)
$tekst = $d2.Content.Text
$antalTabeller = $d2.Tables.Count
$d2.Close([ref]$false)
$word2.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($word2) | Out-Null

$mangler = @()
foreach ($t in $r.Resultater) {
    if ($tekst -notlike ('*' + $t.Id + '*')) { $mangler += $t.Id }
}
if ($tekst -notlike ('*' + (Vis $r.Samlet) + '*')) { $mangler += 'samlet resultat' }

Write-Host ""
Write-Host "Rapport skrevet: $Ud"
Write-Host ("  {0:N0} tegn, {1} tabeller, {2} prøver" -f $tekst.Length, $antalTabeller, $r.Antal)
if ($mangler.Count -gt 0) {
    Write-Host ("  EFTERPROEVNING FEJLEDE - mangler i dokumentet: " + ($mangler -join ', ')) -ForegroundColor Red
    exit 1
}
Write-Host "  efterproevet: alle proever og det samlede resultat staar i dokumentet" -ForegroundColor Green
exit 0


