$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$clientDirectory = Join-Path $repoRoot "Free Realms Reconnected Client"
$clientExecutable = Join-Path $clientDirectory "FreeRealms.exe"
$gatewayAddress = "127.0.0.1"
$gatewayPort = 20260

if (-not (Test-Path -LiteralPath $clientExecutable)) {
    throw "FreeRealms.exe was not found at '$clientExecutable'. Keep this folder beside the existing client folder."
}

$tcp = New-Object System.Net.Sockets.TcpClient
try {
    $pending = $tcp.BeginConnect($gatewayAddress, $gatewayPort, $null, $null)
    if (-not $pending.AsyncWaitHandle.WaitOne(1500, $false)) {
        throw "The local Gateway is not listening on $($gatewayAddress):$gatewayPort. Run Start-Local.ps1 first."
    }
    $tcp.EndConnect($pending)
}
catch {
    throw "Cannot reach the local Free Realms Gateway at $($gatewayAddress):$gatewayPort. Start the local server first. Details: $($_.Exception.Message)"
}
finally {
    $tcp.Close()
}

$arguments = @(
    "inifile=ClientConfig.ini"
    "Guid=1"
    "Server=$($gatewayAddress):$gatewayPort"
    "Ticket=LOCAL-ONLY"
    "ShowMemberLoadingScreen=1"
    "key=Sm9zaHNvcmE="
)

Write-Host "Launching Free Realms against $($gatewayAddress):$gatewayPort..."
Start-Process -FilePath $clientExecutable -WorkingDirectory $clientDirectory -ArgumentList $arguments
