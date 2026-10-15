$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $repoRoot "Joshsora's Unfinished Free Realms Server\FreeRealmsServer"
$solution = Join-Path $serverRoot "FreeRealmsServer.sln"
$runtimeRoot = Join-Path $serverRoot "PrebuiltServer"
$rolesDirectory = Join-Path $runtimeRoot "Daemon\Roles"
$gatewayOutput = Join-Path $serverRoot "Gateway\bin\Release"

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Could not find the server solution at '$solution'. Keep FreeRealmsLocal beside the repository's client and server folders."
}

$msbuild = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $installation = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($installation) { $msbuild = @{ Source = $installation } }
    }
}
if (-not $msbuild) {
    throw "MSBuild was not found. Install Visual Studio Build Tools with the .NET desktop build tools, then rerun this script."
}

Write-Host "Building the server with local-login support..."
& $msbuild.Source $solution /m /p:Configuration=Release "/p:Platform=Any CPU" /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "The server build failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath (Join-Path $gatewayOutput "Gateway.dll"))) {
    throw "The build completed but Gateway.dll was not found in '$gatewayOutput'."
}

New-Item -ItemType Directory -Force -Path $rolesDirectory | Out-Null
Get-ChildItem -LiteralPath $gatewayOutput -Filter "*.dll" -File | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $rolesDirectory -Force
}
Write-Host "Copied the rebuilt Gateway and its local dependencies into the runtime roles folder."

Write-Host "Starting the bundled server. Leave its console open to watch Gateway logs..."
Start-Process -FilePath $env:ComSpec -ArgumentList '/k "run_cluster.bat"' -WorkingDirectory $runtimeRoot | Out-Null

$deadline = (Get-Date).AddSeconds(45)
$gatewayReady = $false
while ((Get-Date) -lt $deadline) {
    $tcp = New-Object System.Net.Sockets.TcpClient
    try {
        $pending = $tcp.BeginConnect("127.0.0.1", 20260, $null, $null)
        if ($pending.AsyncWaitHandle.WaitOne(500, $false)) {
            $tcp.EndConnect($pending)
            $gatewayReady = $true
            break
        }
    }
    catch {
        # The server may still be starting; retry until the timeout.
    }
    finally {
        $tcp.Close()
    }
    Start-Sleep -Seconds 1
}

if (-not $gatewayReady) {
    throw "The Gateway did not open port 20260 within 45 seconds. Check the server console for startup errors."
}

Write-Host "Gateway is listening. Launching the client..."
& (Join-Path $PSScriptRoot "Launch-Client.ps1")
