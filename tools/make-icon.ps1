# Renders app.ico from the HuntXtract logo (art\logo.webp): the whole logo, lettering and fog
# included, in several sizes. Also writes the mark for the sidebar of the UI.
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\art\logo.webp'),
    [string]$Path = (Join-Path $PSScriptRoot '..\app.ico'),
    [string]$Mark = (Join-Path $PSScriptRoot '..\web\img\logo-mark.png'),
    [string]$PreviewPng = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing, PresentationCore, WindowsBase

# WebP only decodes through the Windows imaging codecs (WPF), not through System.Drawing.
function Read-Logo([string]$file) {
    $stream = [System.IO.File]::OpenRead($file)
    try {
        $frame = [System.Windows.Media.Imaging.BitmapDecoder]::Create($stream, 'PreservePixelFormat', 'OnLoad').Frames[0]
        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($frame))
        $ms = New-Object System.IO.MemoryStream
        $encoder.Save($ms)
        $ms.Position = 0
        return New-Object System.Drawing.Bitmap $ms
    } finally {
        $stream.Dispose()
    }
}

$logo = Read-Logo $Source

function New-IconBitmap([int]$s) {
    # Scale down in halving steps - one bicubic step from 1254 px to 16 px aliases.
    $cur = New-Object System.Drawing.Bitmap $logo
    while ($cur.Width -gt 2 * $s) {
        $w = [int]($cur.Width / 2)
        $next = New-Object System.Drawing.Bitmap $w, $w
        $g = [System.Drawing.Graphics]::FromImage($next)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($cur, 0, 0, $w, $w)
        $g.Dispose(); $cur.Dispose(); $cur = $next
    }
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($cur, 0, 0, $s, $s)
    $g.Dispose(); $cur.Dispose()
    return $bmp
}

# 32-bit DIB entry (what classic icon consumers, including System.Drawing, expect).
function Get-DibBytes($bmp) {
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $data.Stride
    $pixels = New-Object byte[] ($stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2))
    $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($row = $s - 1; $row -ge 0; $row--) { $bw.Write($pixels, $row * $stride, $s * 4) }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskRow * $s)))
    $bw.Flush()
    return , $ms.ToArray()
}

$entries = @()
foreach ($size in 16, 20, 24, 32, 40, 48, 64, 256) {
    $bmp = New-IconBitmap $size
    if ($size -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
        if ($PreviewPng) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    } else {
        $bytes = Get-DibBytes $bmp
    }
    $bmp.Dispose()
    $entries += , @($size, $bytes)
}

$fs = [System.IO.File]::Create($Path)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e[0] -ge 256) { 0 } else { $e[0] }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]$e[1].Length); $bw.Write([int]$offset)
    $offset += $e[1].Length
}
foreach ($e in $entries) { $bw.Write([byte[]]$e[1]) }
$bw.Close()
Write-Output "Icon geschrieben: $Path"

# The sidebar shows the mark at 36 px - drawn at 128 px so it stays sharp on high-DPI screens.
if ($Mark) {
    $bmp = New-IconBitmap 128
    $bmp.Save($Mark, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "Sidebar mark: $Mark"
}
$logo.Dispose()
