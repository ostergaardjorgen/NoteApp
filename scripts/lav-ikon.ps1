<#
.SYNOPSIS
    Laver app-ikonet ud fra AppIkon.jpg.

.DESCRIPTION
    Kildebilledet er en JPEG med hvid baggrund omkring en blå, afrundet
    firkant. Tre ting skal ske, for at det bliver et ordentligt Windows-ikon:

      1. Beskæring til selve motivet. Den hvide luft omkring gør ikonet
         mindre, end pladsen tillader — på en 16x16 proceslinje er det
         forskellen på et genkendeligt mærke og en blå prik.
      2. Gennemsigtige hjørner. JPEG kan ikke gemme alfa, så hjørnerne er
         hvide. Uden maske får man hvide trekanter på en mørk proceslinje.
      3. Flere størrelser i én .ico. Windows vælger selv: 16 og 32 i
         proceslinje og titellinje, 48 i Stifinder, 256 i store ikoner.
         Er de ikke der, skalerer Windows selv, og resultatet er grødet.

    Kør scriptet igen, hvis AppIkon.jpg udskiftes.

.EXAMPLE
    powershell -File C:\NoteApp\scripts\lav-ikon.ps1
#>
[CmdletBinding()]
param(
    [string] $Kilde = 'C:\NoteApp\design\Pia App logo uden tekst.jpg',
    [string] $IkonFil = 'C:\NoteApp\src\NoteApp.Desktop\app.ico',
    [string] $PngFil = 'C:\NoteApp\src\NoteApp.Desktop\app.png'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Kilde)) { throw "Fandt ikke kildebilledet: $Kilde" }

$original = [Drawing.Bitmap]::FromFile($Kilde)
Write-Host "Kilde: $($original.Width) x $($original.Height)"

# --- 1. Find motivet -------------------------------------------------------
# MOTIVET ER MAETTET, BAGGRUNDEN ER DET IKKE.
#
# Foerste udgave regnede alt under 240 som motiv, fordi kildebilledet laa paa
# ren hvid. Pia-flisen ligger paa LYSEGRAAT med en skygge under, og saa taeller
# baade baggrund og skygge med - udsnittet ville blive hele billedet.
#
# Flisen er blaa og groen; baggrunden og skyggen er graa. Forskellen mellem
# hoejeste og laveste farvekanal skiller dem rent: over 40 er der kuloer i
# pixlen, under er der ikke.
#
# Den hvide skrift INDE i flisen har ingen maetning, men den er omgivet af
# flisen til alle sider, saa rammen daekker den alligevel.
$minX = $original.Width; $minY = $original.Height; $maxX = 0; $maxY = 0

for ($y = 0; $y -lt $original.Height; $y += 2) {
    for ($x = 0; $x -lt $original.Width; $x += 2) {
        $p = $original.GetPixel($x, $y)
        $hoej = [Math]::Max($p.R, [Math]::Max($p.G, $p.B))
        $lav  = [Math]::Min($p.R, [Math]::Min($p.G, $p.B))
        if (($hoej - $lav) -gt 40) {
            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

$bredde = $maxX - $minX + 1
$hoejde = $maxY - $minY + 1
Write-Host "Motiv : ($minX,$minY) til ($maxX,$maxY) = $bredde x $hoejde"

# Kvadratisk udsnit: et ikon skal vaere kvadratisk, og en skaevhed her giver
# en presset streg i den ene retning.
$side = [Math]::Max($bredde, $hoejde)
$cx = [int](($minX + $maxX) / 2)
$cy = [int](($minY + $maxY) / 2)
$x0 = [Math]::Max(0, $cx - [int]($side / 2))
$y0 = [Math]::Max(0, $cy - [int]($side / 2))
$side = [Math]::Min($side, [Math]::Min($original.Width - $x0, $original.Height - $y0))

$udsnit = New-Object Drawing.Bitmap $side, $side
$g = [Drawing.Graphics]::FromImage($udsnit)
$g.DrawImage($original, (New-Object Drawing.Rectangle 0, 0, $side, $side),
             (New-Object Drawing.Rectangle $x0, $y0, $side, $side), [Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$original.Dispose()

# --- 2. Gennemsigtige hjoerner --------------------------------------------
# Motivet er en afrundet firkant. Alt uden for den afrunding er hvid JPEG-
# baggrund og skal vaere gennemsigtigt.
$radius = [int]($side * 0.22)

function Test-UdenForAfrunding {
    <#
        Sand hvis punktet ligger uden for den afrundede firkant.

        Kun de fire hjoerneomraader kan ligge udenfor. For hvert af dem findes
        afrundingens centrum, og punktet er udenfor, hvis afstanden dertil er
        stoerre end radius. Ligger punktet ikke i et hjoerneomraade, er det
        altid indenfor.
    #>
    param([int] $x, [int] $y, [int] $side, [int] $radius)

    $centerX = $null
    if ($x -lt $radius) { $centerX = $radius }
    elseif ($x -gt ($side - $radius)) { $centerX = $side - $radius }

    $centerY = $null
    if ($y -lt $radius) { $centerY = $radius }
    elseif ($y -gt ($side - $radius)) { $centerY = $side - $radius }

    if ($null -eq $centerX -or $null -eq $centerY) { return $false }

    $dx = $x - $centerX
    $dy = $y - $centerY
    return (($dx * $dx) + ($dy * $dy)) -gt ($radius * $radius)
}

$medAlfa = New-Object Drawing.Bitmap $side, $side, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $side; $y++) {
    for ($x = 0; $x -lt $side; $x++) {
        $p = $udsnit.GetPixel($x, $y)
        if (Test-UdenForAfrunding -x $x -y $y -side $side -radius $radius) {
            $medAlfa.SetPixel($x, $y, [Drawing.Color]::Transparent)
        } else {
            $medAlfa.SetPixel($x, $y, $p)
        }
    }
}
$udsnit.Dispose()

# --- 3. Skriv PNG og ICO ---------------------------------------------------
$medAlfa.Save($PngFil, [Drawing.Imaging.ImageFormat]::Png)
Write-Host "PNG   : $PngFil"

$stoerrelser = @(256, 128, 64, 48, 32, 16)
$billeder = @()

function New-DibData {
    <#
        Bygger et ikonbillede i den gamle DIB-form: BITMAPINFOHEADER, 32-bits
        BGRA nedefra og op, og til sidst en 1-bits AND-maske.

        Hvorfor ikke bare PNG til alle størrelser: Windows' shell understøtter
        kun PNG-komprimerede rammer pålideligt i 256x256. Gemmes 16 og 32 som
        PNG, viser proceslinjen et tomt dokument i stedet for ikonet — og det
        var præcis fejlen i første udgave af dette script.
    #>
    param([Drawing.Bitmap] $Billede)

    $s = $Billede.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $ms

    # BITMAPINFOHEADER. Hoejden er dobbelt, fordi masken taeller med.
    $w.Write([UInt32]40)
    $w.Write([Int32]$s)
    $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1)
    $w.Write([UInt16]32)
    $w.Write([UInt32]0)          # BI_RGB, ingen komprimering
    $w.Write([UInt32]($s * $s * 4))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)

    # Farvedata, nederste raekke foerst.
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $p = $Billede.GetPixel($x, $y)
            $w.Write([Byte]$p.B); $w.Write([Byte]$p.G); $w.Write([Byte]$p.R); $w.Write([Byte]$p.A)
        }
    }

    # AND-maske: 1 bit pr. pixel, raekker fyldt op til 4 bytes. Ignoreres af
    # moderne Windows ved 32 bits, men formatet kraever den.
    $bytesPrRaekke = [Math]::Ceiling($s / 32.0) * 4
    for ($y = 0; $y -lt $s; $y++) {
        for ($i = 0; $i -lt $bytesPrRaekke; $i++) { $w.Write([Byte]0) }
    }

    $w.Flush()
    $data = $ms.ToArray()
    $w.Dispose(); $ms.Dispose()

    # Komma foran: uden det pakker PowerShell arrayet ud til enkelte bytes,
    # og kalderen faar en stroem af tal i stedet for en byte[]. Resultatet er
    # en .ico-fil, Windows slet ikke kan laese.
    return ,$data
}

foreach ($s in $stoerrelser) {
    $b = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gg = [Drawing.Graphics]::FromImage($b)
    $gg.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gg.DrawImage($medAlfa, 0, 0, $s, $s)
    $gg.Dispose()

    if ($s -ge 256) {
        # Kun den stoerste gemmes som PNG — der sparer komprimeringen plads,
        # og der er den understoettet.
        $ms = New-Object IO.MemoryStream
        $b.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        $billeder += ,@($s, $ms.ToArray(), $true)
        $ms.Dispose()
    } else {
        $billeder += ,@($s, (New-DibData -Billede $b), $false)
    }

    $b.Dispose()
}
$medAlfa.Dispose()

# ICO-formatet: 6 bytes header, 16 bytes pr. billede i kataloget, derefter
# billeddata. PNG-indhold er tilladt fra Windows Vista og fylder mindre end
# den gamle BMP-form.
$ud = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter $ud

$w.Write([UInt16]0)                      # reserveret
$w.Write([UInt16]1)                      # 1 = ikon
$w.Write([UInt16]$billeder.Count)

$offset = 6 + (16 * $billeder.Count)
foreach ($b in $billeder) {
    $s = $b[0]; $data = $b[1]
    # 0 i katalogets bredde/hoejde betyder 256 — feltet er kun een byte.
    $maal = [Byte]$(if ($s -ge 256) { 0 } else { $s })
    $w.Write($maal)
    $w.Write($maal)
    $w.Write([Byte]0)                    # farver i paletten
    $w.Write([Byte]0)                    # reserveret
    $w.Write([UInt16]1)                  # farveplaner
    $w.Write([UInt16]32)                 # bit pr. pixel
    $w.Write([UInt32]$data.Length)
    $w.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($b in $billeder) { $w.Write([Byte[]]$b[1], 0, $b[1].Length) }

$w.Flush()
[IO.File]::WriteAllBytes($IkonFil, $ud.ToArray())
$w.Dispose(); $ud.Dispose()

Write-Host "ICO   : $IkonFil ($([math]::Round((Get-Item $IkonFil).Length/1KB,1)) KB, $($billeder.Count) størrelser)"
