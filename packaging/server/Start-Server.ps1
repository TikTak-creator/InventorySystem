[CmdletBinding()]
param(
    [ValidateSet('run', 'init-db', 'create-user')]
    [string]$Action = 'run'
)

$ErrorActionPreference = 'Stop'
$serverExe = Join-Path $PSScriptRoot 'Inventory.Server.exe'
if (-not (Test-Path -LiteralPath $serverExe -PathType Leaf)) {
    throw 'Extract the complete server ZIP before running this script.'
}

function Read-PrivateValue([string]$Prompt) {
    $secret = Read-Host $Prompt -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer); $secret.Dispose() }
}

# Settings are process-local and restored on exit. No passwords are written to disk.
$settingNames = @('ASPNETCORE_ENVIRONMENT', 'ConnectionStrings__Default', 'Urls', 'AllowedHosts',
    'Kestrel__Certificates__Default__Path', 'Kestrel__Certificates__Default__Password')
$previous = @{}
foreach ($name in $settingNames) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = 'Production' }
    if (-not $env:ConnectionStrings__Default) {
        $env:ConnectionStrings__Default = Read-PrivateValue 'MySQL connection string (Production requires SslMode=VerifyFull)'
    }
    if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Default)) { throw 'A MySQL connection string is required.' }

    if ($Action -eq 'run') {
        if (-not $env:Urls) {
            $listenAddress = Read-Host 'HTTPS listen address [https://localhost:7071]'
            $env:Urls = if ($listenAddress) { $listenAddress } else { 'https://localhost:7071' }
        }
        if (-not $env:AllowedHosts) {
            $allowedHosts = Read-Host 'Allowed DNS hostnames, separated by semicolons [localhost;127.0.0.1]'
            $env:AllowedHosts = if ($allowedHosts) { $allowedHosts } else { 'localhost;127.0.0.1' }
        }
        if (-not $env:Kestrel__Certificates__Default__Path) {
            $certificatePath = Read-Host 'Path to your HTTPS certificate (.pfx)'
            if (-not $certificatePath) { throw 'An HTTPS certificate is required. See START-HERE.md.' }
            $env:Kestrel__Certificates__Default__Path = (Resolve-Path -LiteralPath $certificatePath).Path
        }
        if (-not $env:Kestrel__Certificates__Default__Password) {
            $env:Kestrel__Certificates__Default__Password = Read-PrivateValue 'HTTPS certificate password (empty if unprotected)'
        }
        & $serverExe
    } else {
        & $serverExe $Action
    }
    if ($LASTEXITCODE -ne 0) { throw ('StockFlow server exited with code ' + $LASTEXITCODE) }
} finally {
    foreach ($name in $settingNames) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
