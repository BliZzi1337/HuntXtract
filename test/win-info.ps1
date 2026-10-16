# Test helper: lists the visible top-level windows of a process (position, extended styles,
# capture exclusion) and the current foreground window. With -Shot, also grabs the screen
# area of each window the way a screen capture sees it.
param([Parameter(Mandatory = $true)][int]$ProcessId, [string]$Shot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('KgWin' -as [type])) {
    Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class KgWin
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint affinity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    public static List<IntPtr> Windows(uint processId)
    {
        var list = new List<IntPtr>();
        EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == processId && IsWindowVisible(h)) list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }
}
'@
}
$fg = [KgWin]::GetForegroundWindow()
$fgPid = [uint32]0
[void][KgWin]::GetWindowThreadProcessId($fg, [ref]$fgPid)
$fgName = try { (Get-Process -Id $fgPid).ProcessName } catch { '?' }
"Vordergrund: 0x{0:X} ({1}, PID {2})" -f $fg.ToInt64(), $fgName, $fgPid
$i = 0
foreach ($h in [KgWin]::Windows($ProcessId)) {
    $r = New-Object KgWin+RECT
    [void][KgWin]::GetWindowRect($h, [ref]$r)
    $ex = [KgWin]::GetWindowLong($h, -20)
    $aff = [uint32]0
    [void][KgWin]::GetWindowDisplayAffinity($h, [ref]$aff)
    $title = New-Object Text.StringBuilder 256
    [void][KgWin]::GetWindowText($h, $title, 256)
    $flags = @(); if ($ex -band 0x8) { $flags += 'TOPMOST' }; if ($ex -band 0x20) { $flags += 'TRANSPARENT' }; if ($ex -band 0x80) { $flags += 'TOOLWINDOW' }; if ($ex -band 0x80000) { $flags += 'LAYERED' }; if ($ex -band 0x08000000) { $flags += 'NOACTIVATE' }
    "Fenster 0x{0:X} '{1}' {2},{3} {4}x{5} Affinität 0x{6:X} [{7}]" -f $h.ToInt64(), $title, $r.Left, $r.Top, ($r.Right - $r.Left), ($r.Bottom - $r.Top), $aff, ($flags -join ' ')
    if ($Shot -and ($ex -band 0x20)) {
        $w = $r.Right - $r.Left + 40; $hgt = $r.Bottom - $r.Top + 40
        $bmp = New-Object Drawing.Bitmap $w, $hgt
        $g = [Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($r.Left - 20, $r.Top - 20, 0, 0, (New-Object Drawing.Size $w, $hgt))
        $g.Dispose()
        $file = $Shot -replace '\.png$', "-$i.png"
        $bmp.Save($file, [Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        "  Bildschirmaufnahme: $file"
        $i++
    }
}
