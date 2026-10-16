# Test helper: a real mouse drag in the HuntXtract UI through the DevTools port
# (KOPFGELD_DEBUG_PORT) – press at (X1,Y1), move in steps to (X2,Y2), release.
param([int]$Port = 9230, [Parameter(Mandatory = $true)][double]$X1, [Parameter(Mandatory = $true)][double]$Y1,
      [Parameter(Mandatory = $true)][double]$X2, [Parameter(Mandatory = $true)][double]$Y2, [int]$Steps = 12)
$ErrorActionPreference = 'Stop'
$list = Invoke-RestMethod "http://127.0.0.1:$Port/json/list"
$page = @($list) | Where-Object { $_.type -eq 'page' -and $_.url -like 'https://bountyledger.example/*' } | Select-Object -First 1
if (-not $page) { throw 'HuntXtract-Seite nicht gefunden' }
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ws.ConnectAsync([Uri]$page.webSocketDebuggerUrl, [Threading.CancellationToken]::None).Wait()
$script:id = 0
function Send($type, $x, $y, $buttons) {
    $script:id++
    $payload = @{ id = $script:id; method = 'Input.dispatchMouseEvent'; params = @{ type = $type; x = $x; y = $y; button = 'left'; buttons = $buttons; clickCount = 1 } } | ConvertTo-Json -Depth 5 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $ws.SendAsync((New-Object 'ArraySegment[byte]' -ArgumentList @(, $bytes)), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).Wait()
    $buffer = New-Object byte[] 65536
    $segment = New-Object 'ArraySegment[byte]' -ArgumentList @(, $buffer)
    [void]$ws.ReceiveAsync($segment, [Threading.CancellationToken]::None).Result
}
Send 'mouseMoved' $X1 $Y1 0
Send 'mousePressed' $X1 $Y1 1
for ($i = 1; $i -le $Steps; $i++) {
    Send 'mouseMoved' ($X1 + ($X2 - $X1) * $i / $Steps) ($Y1 + ($Y2 - $Y1) * $i / $Steps) 1
    Start-Sleep -Milliseconds 15
}
Send 'mouseReleased' $X2 $Y2 0
$ws.Dispose()
Write-Output 'Maus gezogen'
