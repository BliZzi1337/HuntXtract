# Test helper: screenshot of the Kopfgeld UI through the DevTools port (KOPFGELD_DEBUG_PORT).
param([int]$Port = 9230, [Parameter(Mandatory = $true)][string]$Out)
$ErrorActionPreference = 'Stop'
$list = Invoke-RestMethod "http://127.0.0.1:$Port/json/list"
$page = @($list) | Where-Object { $_.type -eq 'page' -and $_.url -like 'https://bountyledger.example/*' } | Select-Object -First 1
if (-not $page) { throw 'HuntXtract-Seite nicht gefunden' }
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait()
$request = [Text.Encoding]::UTF8.GetBytes('{"id":1,"method":"Page.captureScreenshot","params":{"format":"png"}}')
$ws.SendAsync((New-Object 'ArraySegment[byte]' -ArgumentList @(, $request)), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
$buffer = New-Object byte[] 1048576
$ms = New-Object IO.MemoryStream
do {
    $segment = New-Object 'ArraySegment[byte]' -ArgumentList @(, $buffer)
    $result = $ws.ReceiveAsync($segment, [Threading.CancellationToken]::None).Result
    $ms.Write($buffer, 0, $result.Count)
} while (-not $result.EndOfMessage)
$text = [Text.Encoding]::UTF8.GetString($ms.ToArray())
$match = [regex]::Match($text, '"data":"([^"]+)"')
if (-not $match.Success) { throw "Keine Bilddaten: $($text.Substring(0, [Math]::Min(300, $text.Length)))" }
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
[IO.File]::WriteAllBytes($Out, [Convert]::FromBase64String($match.Groups[1].Value))
$ws.Dispose()
Write-Output "Gespeichert: $Out"
