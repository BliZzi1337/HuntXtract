# Test helper: picture of a whole window including its title bar, taken with PrintWindow – the
# window does not have to be in front and nothing else on the screen ends up in the picture.
param([Parameter(Mandatory = $true)][int]$ProcessId, [Parameter(Mandatory = $true)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WinShot {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
'@
$handle = (Get-Process -Id $ProcessId).MainWindowHandle
if ($handle -eq [IntPtr]::Zero) { throw 'Fenster nicht gefunden' }
$r = New-Object WinShot+RECT
[void][WinShot]::GetWindowRect($handle, [ref]$r)
$bmp = New-Object Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$g = [Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[void][WinShot]::PrintWindow($handle, $hdc, 2)   # PW_RENDERFULLCONTENT: includes the WebView2 content
$g.ReleaseHdc($hdc)
$g.Dispose()
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "Gespeichert: $Out"
