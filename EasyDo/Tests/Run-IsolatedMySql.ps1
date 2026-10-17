[CmdletBinding()]
param(
    [string]$MySqlBase = 'C:/Program Files/MySQL/MySQL Server 8.4',
    [ValidateRange(1024, 65535)][int]$Port = 33317,
    [switch]$IncludeApi,
    [string]$ApiSpecsPath,
    [string]$ApiDotnetPath = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$mysqlExecutable = Join-Path $MySqlBase 'bin/mysqld.exe'
$mysqlAdmin = Join-Path $MySqlBase 'bin/mysqladmin.exe'
$specs = Join-Path $PSScriptRoot 'StockFlow.Specs/bin/Debug/net8.0-windows/StockFlow.Specs.dll'
if (-not (Test-Path -LiteralPath $mysqlExecutable) -or -not (Test-Path -LiteralPath $mysqlAdmin)) {
    throw 'Set -MySqlBase to an installed MySQL Server directory.'
}
if (-not (Test-Path -LiteralPath $specs)) { throw 'Build InventorySystem.slnx in Debug before running this script.' }

$testRoot = Join-Path $projectRoot ('.artifacts/mysql-' + [Guid]::NewGuid().ToString('N'))
$testData = Join-Path $testRoot 'data'
New-Item -ItemType Directory -Path $testData -Force | Out-Null
& $mysqlExecutable --no-defaults --initialize-insecure "--basedir=$MySqlBase" "--datadir=$testData" "--log-error=$testRoot/initialize.log"
if ($LASTEXITCODE -ne 0) { Get-Content "$testRoot/initialize.log" -Tail 20; throw 'MySQL test initialization failed.' }

$testServer = Start-Process -FilePath $mysqlExecutable -ArgumentList @('--no-defaults', "--basedir=`"$MySqlBase`"", "--datadir=`"$testData`"", "--log-error=`"$testRoot/server.log`"", "--port=$Port", '--bind-address=127.0.0.1', '--mysqlx=OFF') -WindowStyle Hidden -PassThru
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($testServer.HasExited) { Get-Content "$testRoot/server.log" -Tail 20; throw 'MySQL test server exited.' }
        # First prove that OUR server bound the port. A ping alone could reach another server.
        $ownServerReady = (Test-Path -LiteralPath "$testRoot/server.log") -and
            (Select-String -LiteralPath "$testRoot/server.log" -Pattern "ready for connections.*port: $Port " -Quiet)
        if ($ownServerReady) {
            & $mysqlAdmin --no-defaults --host=127.0.0.1 "--port=$Port" --user=root --connect-timeout=1 ping 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'MySQL test server did not become ready.' }
    $testSettings = @{ ConnectionStrings = @{ Default = "Server=127.0.0.1;Port=$Port;Database=unused;User ID=root;Password=;CharSet=utf8mb4;" }; SeedDemoData = $false }
    $settingsPath = Join-Path $testRoot 'settings.json'
    $testSettings | ConvertTo-Json -Depth 3 | Set-Content $settingsPath -Encoding utf8
    & dotnet $specs --mysql --settings $settingsPath
    if ($LASTEXITCODE -ne 0) { throw "Integration suite failed ($LASTEXITCODE)." }
    if ($IncludeApi) {
        $apiSpecs = if ($ApiSpecsPath) { (Resolve-Path -LiteralPath $ApiSpecsPath).Path } else { Join-Path $projectRoot 'tests/Inventory.Api.Specs/bin/Debug/net8.0/Inventory.Api.Specs.dll' }
        & $ApiDotnetPath $apiSpecs --settings $settingsPath
        if ($LASTEXITCODE -ne 0) { throw "HTTPS API suite failed ($LASTEXITCODE)." }
    }
} finally {
    if (-not $testServer.HasExited) {
        if ($ready) {
            & $mysqlAdmin --no-defaults --host=127.0.0.1 "--port=$Port" --user=root --connect-timeout=1 shutdown 2>$null | Out-Null
        }
        if (-not $testServer.WaitForExit(5000)) { Stop-Process -Id $testServer.Id -Force }
    }
}
# Data and logs remain in .artifacts for inspection; no system service is changed.
