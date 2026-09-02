[CmdletBinding()]
param(
    [string]$SqlServer = 'localhost\SQL2022',
    [string]$DatabaseName = "RhemaQsUatAssurance_$((Get-Date).ToString('yyyyMMdd'))",
    [int]$ApiPort = 5200,
    [int]$FrontendPort = 3200
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ($DatabaseName -notmatch '^RhemaQsUatAssurance_[0-9]{8}$') {
    throw 'The disposable QS database name must match RhemaQsUatAssurance_YYYYMMDD.'
}

$existing = (& sqlcmd -S $SqlServer -E -C -d master -b -h -1 -W -Q `
    "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$DatabaseName') IS NULL THEN 0 ELSE 1 END;").Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the disposable QS database target.' }
if ($existing -ne '0') {
    throw "QS acceptance refused because database '$DatabaseName' already exists."
}

$runDirectory = Join-Path ([IO.Path]::GetTempPath()) "rhema-qs-browser-$([Guid]::NewGuid().ToString('N'))"
$resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedRunDirectory = [IO.Path]::GetFullPath($runDirectory)
if (-not $resolvedRunDirectory.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The QS browser run directory did not resolve beneath the system temporary directory.'
}
New-Item -ItemType Directory -Path $resolvedRunDirectory | Out-Null

$apiProcess = $null
$frontendProcess = $null
$databaseCreated = $false
$environmentNames = [Collections.Generic.List[string]]::new()

function Set-RunEnvironment([string]$Name, [string]$Value) {
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    $environmentNames.Add($Name)
}

function New-EphemeralSecret {
    return "$([Guid]::NewGuid().ToString('N'))$([Guid]::NewGuid().ToString('N'))Aa1!"
}

function New-EphemeralBytes([int]$Length) {
    $bytes = New-Object byte[] $Length
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return $bytes
}

function Get-LogTail([string[]]$LogPaths) {
    return (($LogPaths | ForEach-Object {
        if (Test-Path $_) { Get-Content $_ -Tail 50 }
    }) -join [Environment]::NewLine)
}

function Wait-HttpReady(
    [string]$Url,
    [string]$ProcessName,
    [Diagnostics.Process]$Process,
    [string[]]$LogPaths
) {
    for ($attempt = 1; $attempt -le 180; $attempt++) {
        if ($Process.HasExited) {
            throw "$ProcessName exited before becoming ready.$([Environment]::NewLine)$(Get-LogTail $LogPaths)"
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { return }
        } catch {
            # Poll until the bounded startup window expires.
        }
        Start-Sleep -Seconds 1
    }
    throw "$ProcessName did not become ready within 180 seconds."
}

try {
    $connection = "Server=$SqlServer;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    Set-RunEnvironment 'ConnectionStrings__DefaultConnection' $connection
    Set-RunEnvironment 'Database__Provider' 'SqlServer'
    Set-RunEnvironment 'ASPNETCORE_ENVIRONMENT' 'Testing'
    Set-RunEnvironment 'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' 'true'
    Set-RunEnvironment 'SkipStartupInitialization' 'true'
    Set-RunEnvironment 'BackgroundServices__Enabled' 'false'
    Set-RunEnvironment 'JwtSettings__SecretKey' (New-EphemeralSecret)
    Set-RunEnvironment 'JwtSettings__Issuer' 'Rhema.QS.Browser.Acceptance'
    Set-RunEnvironment 'JwtSettings__Audience' 'Rhema.QS.Browser.Client'
    Set-RunEnvironment 'JwtSettings__PortalSecretKey' (New-EphemeralSecret)
    Set-RunEnvironment 'JwtSettings__PortalAudience' 'Rhema.QS.Browser.Portal'
    Set-RunEnvironment 'CandidatePortal__PortalUrl' 'https://candidate.test/'
    Set-RunEnvironment 'Security__EncryptionKey' ([Convert]::ToBase64String((New-EphemeralBytes 32)))
    Set-RunEnvironment 'FileVirusScan__ClamAv__Host' '127.0.0.1'
    Set-RunEnvironment 'FileStorage__Local__PrivateBasePath' (Join-Path $resolvedRunDirectory 'secure-file-storage')
    Set-RunEnvironment 'CorsSettings__AllowedOrigins__0' "http://127.0.0.1:$FrontendPort"

    $apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
    $buildLog = Join-Path $resolvedRunDirectory 'build.log'
    & dotnet build $apiProject --no-restore -m:1 -p:UseSharedCompilation=false `
        -p:BuildInParallel=false -p:WarningLevel=0 -p:TdcFastEfBuild=true *> $buildLog
    if ($LASTEXITCODE -ne 0) {
        throw "The QS browser API build failed.$([Environment]::NewLine)$(Get-LogTail @($buildLog))"
    }

    $apiDll = Join-Path $repoRoot 'src\ErpSystem.Api\bin\Debug\net8.0\ErpSystem.Api.dll'
    $rebuildLog = Join-Path $resolvedRunDirectory 'rebuild.log'
    & dotnet $apiDll rebuild-db *> $rebuildLog
    if ($LASTEXITCODE -ne 0) {
        throw "The disposable QS database rebuild failed.$([Environment]::NewLine)$(Get-LogTail @($rebuildLog))"
    }
    $databaseCreated = $true

    $hrSeedLog = Join-Path $resolvedRunDirectory 'seed-hr.log'
    & dotnet $apiDll seed-hr-all *> $hrSeedLog
    if ($LASTEXITCODE -ne 0) {
        throw "The QS HR/location seed failed.$([Environment]::NewLine)$(Get-LogTail @($hrSeedLog))"
    }

    $apiLog = Join-Path $resolvedRunDirectory 'api.out.log'
    $apiErrorLog = Join-Path $resolvedRunDirectory 'api.err.log'
    Set-RunEnvironment 'ASPNETCORE_URLS' "http://127.0.0.1:$ApiPort"
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll) `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $apiLog -RedirectStandardError $apiErrorLog `
        -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$ApiPort/health/live" 'QS API' $apiProcess @($apiLog, $apiErrorLog)

    $frontendLog = Join-Path $resolvedRunDirectory 'frontend.out.log'
    $frontendErrorLog = Join-Path $resolvedRunDirectory 'frontend.err.log'
    Set-RunEnvironment 'NEXT_PUBLIC_API_URL' "http://127.0.0.1:$ApiPort/api"
    $nextCli = Join-Path $repoRoot 'frontend\node_modules\next\dist\bin\next'
    if (-not (Test-Path $nextCli)) { throw 'Install the branch-locked frontend dependencies before QS browser acceptance.' }
    $frontendProcess = Start-Process -FilePath 'node' `
        -ArgumentList @($nextCli, 'dev', '--hostname', '127.0.0.1', '--port', $FrontendPort) `
        -WorkingDirectory (Join-Path $repoRoot 'frontend') `
        -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrorLog `
        -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$FrontendPort/login" 'QS frontend' $frontendProcess `
        @($frontendLog, $frontendErrorLog)

    Push-Location (Join-Path $repoRoot 'e2e-tests')
    try {
        & node .qs-assurance-runner.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Quantity Survey authenticated browser acceptance failed.' }
    } finally {
        Pop-Location
    }

    Write-Output 'QS_BROWSER_E2E_PASS'
} finally {
    if ($frontendProcess -and -not $frontendProcess.HasExited) {
        Stop-Process -Id $frontendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($databaseCreated) {
        if ($DatabaseName -notmatch '^RhemaQsUatAssurance_[0-9]{8}$') {
            throw 'Refusing to drop a database outside the disposable QS prefix.'
        }
        & sqlcmd -S $SqlServer -E -C -d master -b -Q `
            "IF DB_ID(N'$DatabaseName') IS NOT NULL BEGIN ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName]; END;" | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Output 'QS_BROWSER_DATABASE_DROPPED' }
    }

    foreach ($name in $environmentNames | Select-Object -Unique) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    if (Test-Path -LiteralPath $resolvedRunDirectory) {
        Remove-Item -LiteralPath $resolvedRunDirectory -Recurse -Force
    }
}
