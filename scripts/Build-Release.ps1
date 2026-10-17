[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '2.0.0',
    [ValidatePattern('^8\.0\.\d+$')]
    [string]$RuntimeVersion = '8.0.32',
    [string]$OutputDirectory,
    [string]$InnoCompilerPath
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne 'Win32NT') { throw 'Build the Windows release on Windows x64.' }
$repoRoot = Split-Path $PSScriptRoot -Parent

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw ($Command + ' failed with exit code ' + $LASTEXITCODE) }
}

Push-Location $repoRoot
try {
    $status = @(git status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Git status.' }
    if ($status.Count) { throw 'Commit tracked changes before building a release; source ZIP and binaries must share one commit.' }
    $sourceCommit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the source commit.' }
    $assemblyVersion = [xml](Get-Content -LiteralPath 'InventoryApp/InventoryApp.csproj' -Raw)
    if ($assemblyVersion.Project.PropertyGroup.Version -ne $Version) { throw 'Release version must match InventoryApp.csproj.' }
    if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot ('.artifacts/releases/' + $Version) }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $OutputDirectory) {
        if (@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count) { throw 'Output directory is not empty. Choose a new -OutputDirectory.' }
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $stage = Join-Path $repoRoot ('.artifacts/release-build/' + [guid]::NewGuid().ToString('N'))
    $clientDir = Join-Path $stage 'client'
    $serverDir = Join-Path $stage 'server'
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    # Build the committed archive, never loose files or local IDE settings from the checkout.
    $sourceZip = Join-Path $OutputDirectory ('StockFlow-' + $Version + '-Source.zip')
    Invoke-Checked 'git' @('archive', '--format=zip', ('--prefix=StockFlow-' + $Version + '/'), ('--output=' + $sourceZip), $sourceCommit)
    Expand-Archive -LiteralPath $sourceZip -DestinationPath (Join-Path $stage 'source')
    $sourceRoot = Join-Path $stage ('source/StockFlow-' + $Version)

    # Compiler is cached in portable mode: no installed application, associations or shortcuts.
    if (-not $InnoCompilerPath) {
        $toolDir = Join-Path $repoRoot '.artifacts/release-tools'
        $InnoCompilerPath = Join-Path $toolDir 'inno/ISCC.exe'
        if (-not (Test-Path -LiteralPath $InnoCompilerPath)) {
            New-Item -ItemType Directory -Path $toolDir -Force | Out-Null
            $download = Join-Path $toolDir 'innosetup-6.7.3.exe'
            $previousTls = [Net.ServicePointManager]::SecurityProtocol
            try {
                [Net.ServicePointManager]::SecurityProtocol = $previousTls -bor [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
            } finally { [Net.ServicePointManager]::SecurityProtocol = $previousTls }
            $expected = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Inno Setup SHA-256 mismatch.' }
            $signature = Get-AuthenticodeSignature -LiteralPath $download
            if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Pyrsys B.V.') { throw 'Inno Setup publisher signature is not valid.' }
            $compilerDir = Join-Path $toolDir 'inno'
            $extractArguments = @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', ('/DIR="' + $compilerDir + '"'))
            $extract = Start-Process -FilePath $download -ArgumentList $extractArguments -Wait -PassThru -WindowStyle Hidden
            if ($extract.ExitCode -ne 0) { throw ('Portable Inno Setup extraction failed: ' + $extract.ExitCode) }
        }
    }
    if (-not (Test-Path -LiteralPath $InnoCompilerPath -PathType Leaf)) { throw 'ISCC.exe not found.' }
    $InnoCompilerPath = (Resolve-Path -LiteralPath $InnoCompilerPath).Path

    $publishOptions = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        ('-p:RuntimeFrameworkVersion=' + $RuntimeVersion), '-p:PublishTrimmed=false', '-p:PublishSingleFile=false',
        '-p:DebugType=None', '-p:DebugSymbols=false')
    Invoke-Checked 'dotnet' (@('publish', (Join-Path $sourceRoot 'InventoryApp/InventoryApp.csproj')) + $publishOptions + @('-o', $clientDir))
    Invoke-Checked 'dotnet' (@('publish', (Join-Path $sourceRoot 'Inventory.Server/Inventory.Server.csproj')) + $publishOptions + @('-o', $serverDir))
    Copy-Item -LiteralPath (Join-Path $sourceRoot 'packaging/windows/START-HERE.txt') -Destination $clientDir
    foreach ($file in @('START-HERE.md', 'Start-Server.ps1', 'Start-Server.cmd')) {
        Copy-Item -LiteralPath (Join-Path $sourceRoot ('packaging/server/' + $file)) -Destination $serverDir
    }

    $manifest = [ordered]@{
        product = 'StockFlow'; version = $Version; sourceCommit = $sourceCommit
        runtimeIdentifier = 'win-x64'; dotnetRuntime = $RuntimeVersion; selfContained = $true
        installerCompiler = 'Inno Setup 6.7.3'; applicationCodeSigned = $false
    } | ConvertTo-Json
    foreach ($folder in @($clientDir, $serverDir)) {
        [IO.File]::WriteAllText((Join-Path $folder 'release-manifest.json'), $manifest, [Text.UTF8Encoding]::new($false))
    }

    # Reject accidental server credentials or database dependencies in the client package.
    $clientSettings = Get-Content -LiteralPath (Join-Path $clientDir 'appsettings.json') -Raw | ConvertFrom-Json
    if ($clientSettings.PSObject.Properties.Name -contains 'ConnectionStrings') { throw 'Database settings found in client package.' }
    if (Test-Path -LiteralPath (Join-Path $clientDir 'MySqlConnector.dll')) { throw 'MySQL driver found in client package.' }
    $serverSettings = Get-Content -LiteralPath (Join-Path $serverDir 'appsettings.json') -Raw | ConvertFrom-Json
    if ($serverSettings.ConnectionStrings.Default) { throw 'A database connection string must not be distributed.' }
    foreach ($folder in @($clientDir, $serverDir)) {
        $privateFiles = @(Get-ChildItem -LiteralPath $folder -File -Recurse -Force | Where-Object {
            $_.Extension -in @('.pfx', '.p12', '.pem', '.key') -or $_.Name -like '.env*' -or $_.Name -like 'appsettings.*.local.json'
        })
        if ($privateFiles.Count) { throw 'Private configuration or certificates found in a publish folder.' }
        if (-not (Test-Path -LiteralPath (Join-Path $folder 'coreclr.dll'))) { throw 'Self-contained .NET runtime is missing.' }
    }

    Invoke-Checked $InnoCompilerPath @('/Qp', ('/DAppVersion=' + $Version), ('/DClientDir=' + $clientDir),
        ('/DOutputDir=' + $OutputDirectory), (Join-Path $sourceRoot 'packaging/windows/StockFlow.iss'))
    $serverZip = Join-Path $OutputDirectory ('StockFlow-' + $Version + '-Server-win-x64.zip')
    Compress-Archive -Path (Join-Path $serverDir '*') -DestinationPath $serverZip -CompressionLevel Optimal

    $hashLines = @(Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
    })
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'), $hashLines, [Text.UTF8Encoding]::new($false))
    Write-Output ('Release files: ' + $OutputDirectory)
    Write-Output ('Client staging: ' + $clientDir)
    Write-Output ('Server staging: ' + $serverDir)
    Get-ChildItem -LiteralPath $OutputDirectory -File | Select-Object Name, Length
} finally { Pop-Location }
