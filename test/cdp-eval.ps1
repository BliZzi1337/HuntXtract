# Test helper: evaluates JavaScript in the Kopfgeld UI through the DevTools port
# (KOPFGELD_DEBUG_PORT) and prints the result as JSON. Promises are awaited.
param([int]$Port = 9230, [Parameter(Mandatory = $true)][string]$Expression)
$ErrorActionPreference = 'Stop'
$list = Invoke-RestMethod "http://127.0.0.1:$Port/json/list"
$page = @($list) | Where-Object { $_.type -eq 'page' -and $_.url -like 'https://bountyledger.example/*' } | Select-Object -First 1
if (-not $page) { throw 'HuntXtract-Seite nicht gefunden' }
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait()
$payload = @{ id = 1; method = 'Runtime.evaluate'; params = @{ expression = $Expression; awaitPromise = $true; returnByValue = $true } } | ConvertTo-Json -Depth 5 -Compress
$request = [Text.Encoding]::UTF8.GetBytes($payload)
$ws.SendAsync((New-Object 'ArraySegment[byte]' -ArgumentList @(, $request)), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
$buffer = New-Object byte[] 1048576
$ms = New-Object IO.MemoryStream
do {
    $segment = New-Object 'ArraySegment[byte]' -ArgumentList @(, $buffer)
    $result = $ws.ReceiveAsync($segment, [Threading.CancellationToken]::None).Result
    $ms.Write($buffer, 0, $result.Count)
} while (-not $result.EndOfMessage)
$ws.Dispose()
$response = [Text.Encoding]::UTF8.GetString($ms.ToArray()) | ConvertFrom-Json
if ($response.result.exceptionDetails) { throw ($response.result.exceptionDetails.exception.description) }
$response.result.result.value | ConvertTo-Json -Depth 8 -Compress
