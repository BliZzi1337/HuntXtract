# Builds out\HuntXtract with the C# compiler that ships with Windows (.NET Framework 4.8).
# No SDK needed: the WebView2 DLLs come from Microsoft's NuGet package (vendor\), the OCR
# is Windows' own (referenced through the metadata in System32\WinMetadata).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$winmd = Join-Path $env:WINDIR 'System32\WinMetadata'
$out = Join-Path $root 'out\HuntXtract'
$icon = Join-Path $root 'app.ico'

New-Item -ItemType Directory -Force $out | Out-Null
if (-not (Test-Path $icon)) { & (Join-Path $root 'tools\make-icon.ps1') -Path $icon | Out-Null }

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

# Every file below web\ is embedded under its relative path, e.g. "app/index.html" – except
# fonts, pictures and the ready-made setup for 1920 x 1080 (vorgabe): they go into the assets
# folder next to the exe (see below).
$webRoot = Join-Path $root 'web'
$assetDirs = 'fonts', 'img', 'vorgabe'
$resources = Get-ChildItem $webRoot -Recurse -File | ForEach-Object {
    $name = $_.FullName.Substring($webRoot.Length + 1).Replace('\', '/')
    if ($assetDirs -notcontains $name.Split('/')[0]) { "/resource:$($_.FullName),$name" }
}

$arguments = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001', '/warn:4',
    "/out:$out\HuntXtract.exe",
    "/win32icon:$icon",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.Security.dll',
    '/reference:System.Runtime.dll',
    '/reference:System.Runtime.InteropServices.WindowsRuntime.dll',
    '/reference:System.Threading.Tasks.dll',
    "/reference:$winmd\Windows.Foundation.winmd",
    "/reference:$winmd\Windows.Globalization.winmd",
    "/reference:$winmd\Windows.Graphics.winmd",
    "/reference:$winmd\Windows.Media.winmd",
    "/reference:$winmd\Windows.Storage.winmd",
    "/reference:$root\vendor\Microsoft.Web.WebView2.Core.dll",
    "/resource:$icon,app.ico"
) + $resources + $sources

& $csc @arguments
if ($LASTEXITCODE -ne 0) { throw 'Kompilieren fehlgeschlagen' }

Copy-Item (Join-Path $root 'vendor\Microsoft.Web.WebView2.Core.dll'), (Join-Path $root 'vendor\WebView2Loader.dll') $out -Force
foreach ($doc in 'LIESMICH.txt', 'README.txt') {
    if (Test-Path (Join-Path $root $doc)) { Copy-Item (Join-Path $root $doc) $out -Force }
}
# Fonts and pictures as plain files; the program serves them from assets\fonts and assets\img
# and takes the setup for new users from assets\vorgabe.
$assets = Join-Path $out 'assets'
if (Test-Path $assets) { Remove-Item $assets -Recurse -Force }
foreach ($dir in $assetDirs) { Copy-Item (Join-Path $webRoot $dir) (Join-Path $assets $dir) -Recurse -Force }
Get-ChildItem (Join-Path $assets 'fonts') -Filter 'OFL-*.txt' | Remove-Item -Force
# The licence of the fonts (SIL OFL) has to travel with them.
$licenses = Join-Path $out 'Lizenzen'
New-Item -ItemType Directory -Force $licenses | Out-Null
Copy-Item (Join-Path $webRoot 'fonts\OFL-*.txt') $licenses -Force
Write-Output "Fertig: $out"
