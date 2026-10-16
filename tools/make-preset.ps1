# Builds the setup new users with a 16:9 screen get ready to use (web\vorgabe, or
# web\vorgabe\<language> for Hunt in another language than English, named as in Hunt's log, e.g.
# "german"): the calibration of a finished setup and clean pictures of its pages - Mission
# Summary, Team Details (all player names and the party pixelated, those are other people) and
# the map on Tab. Source is the data folder of a setup made on 1920 x 1080 (kalibrierung.json,
# referenz.png, referenz-team.png, referenz-karte.png). The bands below match the Team Details
# layout of Hunt 1896 in 16:9.
param(
    [Parameter(Mandatory = $true)][string]$DataDir,
    [string]$Language = 'english',
    [string]$OutDir = '',
    [int]$Width = 1600
)
$ErrorActionPreference = 'Stop'
if (-not $OutDir) {
    $OutDir = Join-Path $PSScriptRoot '..\web\vorgabe'
    if ($Language -ne 'english') { $OutDir = Join-Path $OutDir $Language }
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
Add-Type -AssemblyName System.Drawing

function Pixelate($bmp, [double]$x1, [double]$y1, [double]$x2, [double]$y2) {
    $r = New-Object System.Drawing.Rectangle ([int]($x1 * $bmp.Width)), ([int]($y1 * $bmp.Height)), ([int](($x2 - $x1) * $bmp.Width)), ([int](($y2 - $y1) * $bmp.Height))
    $block = 14
    $small = New-Object System.Drawing.Bitmap ([Math]::Max(1, [int]($r.Width / $block))), ([Math]::Max(1, [int]($r.Height / $block)))
    $g = [System.Drawing.Graphics]::FromImage($small)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBilinear
    $g.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $small.Width, $small.Height), $r, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($small, $r)
    $g.Dispose(); $small.Dispose()
}

# A page scaled to $Width (16:9 stays 16:9), optionally edited, as JPEG.
function Save-Page([string]$source, [string]$target, [scriptblock]$edit) {
    $bytes = [IO.File]::ReadAllBytes($source)
    $ms = New-Object IO.MemoryStream(, $bytes)
    $src = [System.Drawing.Image]::FromStream($ms)
    $height = [int][Math]::Round($src.Height * $Width / $src.Width)
    $bmp = New-Object System.Drawing.Bitmap $Width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $Width, $height)
    $g.Dispose(); $src.Dispose(); $ms.Dispose()
    if ($edit) { & $edit $bmp }
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $params = New-Object System.Drawing.Imaging.EncoderParameters(1)
    $params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, [long]90)
    $bmp.Save($target, $codec, $params)
    $bmp.Dispose()
    '{0}  {1}x{2}' -f (Split-Path $target -Leaf), $Width, $height
}

$cal = Get-Content (Join-Path $DataDir 'kalibrierung.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($cal.FrameWidth -ne 1920 -or $cal.FrameHeight -ne 1080) { throw ('The setup is from a {0} x {1} screen, not 1920 x 1080' -f $cal.FrameWidth, $cal.FrameHeight) }
if (-not $cal.Team -or -not $cal.Map -or -not $cal.Map.AnchorPrint) { throw 'The setup needs the Team Details and the map too' }
Copy-Item (Join-Path $DataDir 'kalibrierung.json') (Join-Path $OutDir 'kalibrierung.json') -Force
'kalibrierung.json'
Save-Page (Join-Path $DataDir 'referenz.png') (Join-Path $OutDir 'summary.jpg') $null
Save-Page (Join-Path $DataDir 'referenz-team.png') (Join-Path $OutDir 'team.jpg') {
    param($bmp)
    Pixelate $bmp 0.080 0.360 0.300 0.655   # team 1: names
    Pixelate $bmp 0.080 0.720 0.300 0.940   # team 2: names (the key bar below stays readable)
    Pixelate $bmp 0.780 0.015 0.875 0.100   # party with avatar, top right
}
Save-Page (Join-Path $DataDir 'referenz-karte.png') (Join-Path $OutDir 'karte.jpg') $null
