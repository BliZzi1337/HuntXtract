# Renders the two example pictures new users can open in the setup ("Beispiel ansehen"): a
# Mission Summary and a Team Details screen with the finished frames drawn on them. Source is a
# data folder of a finished setup (kalibrierung.json, referenz.png, referenz-team.png).
# In the Team Details all player names and the party avatar are pixelated - those are other
# people. The bands below match the Team Details layout of Hunt 1896 in 16:9.
param(
    [Parameter(Mandatory = $true)][string]$DataDir,
    [string]$OutDir = '',
    [int]$Width = 1600
)
$ErrorActionPreference = 'Stop'
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\web\img' }
Add-Type -AssemblyName System.Drawing

$cal = Get-Content (Join-Path $DataDir 'kalibrierung.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$palette = '#e2b25c', '#6fc48d', '#6fa8e0', '#e2705f', '#b48be0', '#e0cf5f', '#5fd0c4', '#e07fb0', '#9fd35f', '#d9975f', '#8fa0e8'

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

function Draw-Frame($g, $w, $h, $area, [string]$label, [System.Drawing.Color]$color, [bool]$dashed) {
    $x = [single]($area.X * $w); $y = [single]($area.Y * $h); $fw = [single]($area.W * $w); $fh = [single]($area.H * $h)
    $shadow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200, 0, 0, 0)), 5
    $g.DrawRectangle($shadow, $x, $y, $fw, $fh)
    $pen = New-Object System.Drawing.Pen $color, 3
    if ($dashed) { $pen.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash }
    $g.DrawRectangle($pen, $x, $y, $fw, $fh)
    $font = New-Object System.Drawing.Font 'Segoe UI Semibold', 13, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $size = $g.MeasureString($label, $font)
    $tx = [single][Math]::Max(0, $x - 1); $ty = [single]($y - $size.Height - 3)
    if ($ty -lt 0) { $ty = $y + $fh + 3 }
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $color), $tx, $ty, $size.Width + 6, $size.Height + 1)
    $g.DrawString($label, $font, [System.Drawing.Brushes]::Black, $tx + 3, $ty + 1)
}

function Render([string]$image, $anchor, $fields, [string]$out, [scriptblock]$redact) {
    $src = [System.Drawing.Image]::FromFile((Join-Path $DataDir $image))
    $h = [int]($src.Height * $Width / $src.Width)
    $bmp = New-Object System.Drawing.Bitmap $Width, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, $Width, $h)
    $g.Dispose(); $src.Dispose()
    if ($redact) { & $redact $bmp }
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    if ($anchor) { Draw-Frame $g $Width $h $anchor 'Wiedererkennung' ([System.Drawing.Color]::White) $true }
    $i = 0
    foreach ($f in $fields) {
        if (-not $f.Area -or $f.Derived) { continue }
        $color = [System.Drawing.ColorTranslator]::FromHtml($palette[$i % $palette.Count])
        Draw-Frame $g $Width $h $f.Area $f.Label $color $false
        $i++
    }
    $g.Dispose()
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 85L
    $bmp.Save($out, $codec, $ep)
    $bmp.Dispose()
    Write-Output ("{0}: {1:N0} Bytes" -f $out, (Get-Item $out).Length)
}

Render 'referenz.png' $cal.Anchor $cal.Fields (Join-Path $OutDir 'vorlage-summary.jpg') $null
Render 'referenz-team.png' $cal.Team.Anchor $cal.Team.Fields (Join-Path $OutDir 'vorlage-team.jpg') {
    param($bmp)
    Pixelate $bmp 0.080 0.360 0.300 0.655   # team 1: names
    Pixelate $bmp 0.080 0.720 0.300 0.940   # team 2: names (the key bar below stays readable)
    Pixelate $bmp 0.780 0.015 0.875 0.100   # party with avatar, top right
}
