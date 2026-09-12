[CmdletBinding()]
param(
    [string]$SqlServer = 'localhost\SQL2017',
    [string]$SqlDataDirectory = 'D:\SQLDATA',
    [int]$ApiPort = 5317,
    [int]$FrontendPort = 3317,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$databaseName = "RhemaERP_TenderBrowser_$([Guid]::NewGuid().ToString('N'))"
$databasePattern = '^RhemaERP_TenderBrowser_[0-9a-f]{32}$'
if ($databaseName -notmatch $databasePattern) {
    throw 'The disposable Tender database name is outside the approved prefix.'
}

$runDirectory = Join-Path ([IO.Path]::GetTempPath()) "rhema-tender-browser-$([Guid]::NewGuid().ToString('N'))"
$resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedRunDirectory = [IO.Path]::GetFullPath($runDirectory)
if (-not $resolvedRunDirectory.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Tender browser run directory did not resolve beneath the system temporary directory.'
}
New-Item -ItemType Directory -Path $resolvedRunDirectory | Out-Null

$apiProcess = $null
$frontendProcess = $null
$databaseCleanupAuthorized = $false
$environmentNames = [Collections.Generic.List[string]]::new()
$sensitiveValues = [Collections.Generic.List[string]]::new()

$apiBuildLog = Join-Path $resolvedRunDirectory 'api-build.log'
$frontendBuildLog = Join-Path $resolvedRunDirectory 'frontend-build.log'
$rebuildLog = Join-Path $resolvedRunDirectory 'database-migrations.log'
$baseSeedLog = Join-Path $resolvedRunDirectory 'base-seed.log'
$actorSeedLog = Join-Path $resolvedRunDirectory 'actor-seed.log'
$prepareLog = Join-Path $resolvedRunDirectory 'fixture-prepare.log'
$playwrightLog = Join-Path $resolvedRunDirectory 'playwright.log'
$verifyLog = Join-Path $resolvedRunDirectory 'fixture-verify.log'
$apiLog = Join-Path $resolvedRunDirectory 'api.out.log'
$apiErrorLog = Join-Path $resolvedRunDirectory 'api.err.log'
$frontendLog = Join-Path $resolvedRunDirectory 'frontend.out.log'
$frontendErrorLog = Join-Path $resolvedRunDirectory 'frontend.err.log'
$fixturePath = Join-Path $resolvedRunDirectory 'tender-lifecycle.fixture.json'
$resultPath = Join-Path $resolvedRunDirectory 'tender-lifecycle.result.json'

function Set-RunEnvironment([string]$Name, [string]$Value, [switch]$Sensitive) {
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    $environmentNames.Add($Name)
    if ($Sensitive -and -not [string]::IsNullOrEmpty($Value)) {
        $sensitiveValues.Add($Value)
    }
}

function New-EphemeralPassword {
    $bytes = New-Object byte[] 36
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return "$([Convert]::ToBase64String($bytes))Aa1!"
}

function New-EphemeralBytes([int]$Length) {
    $bytes = New-Object byte[] $Length
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    return $bytes
}

function Protect-RunLog([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $protected = $Text
    foreach ($value in $sensitiveValues) {
        if (-not [string]::IsNullOrEmpty($value)) {
            $protected = $protected -replace [Regex]::Escape($value), '<redacted>'
        }
    }
    $protected = $protected -replace '(?i)(?:Server|Data Source)=[^;\r\n]+;Database=[^;\r\n]+;[^\r\n]*', '<redacted-connection>'
    $protected
}

function Get-LogTail([string[]]$LogPaths) {
    $tail = ($LogPaths | ForEach-Object {
        if (Test-Path -LiteralPath $_) { Get-Content -LiteralPath $_ -Tail 60 }
    }) -join [Environment]::NewLine
    Protect-RunLog $tail
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

function Invoke-ApiCommand([string]$ApiDll, [string]$Command, [string]$LogPath, [string]$FailureMessage) {
    $process = Start-Process -FilePath 'dotnet' -ArgumentList @($ApiDll, $Command) `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $LogPath `
        -RedirectStandardError "$LogPath.err" -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$FailureMessage$([Environment]::NewLine)$(Get-LogTail @($LogPath, "$LogPath.err"))"
    }
}

try {
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        throw 'sqlcmd is required for the disposable SQL Server Tender acceptance database.'
    }

    $existing = (& sqlcmd -S $SqlServer -E -C -d master -b -h -1 -W -Q `
        "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$databaseName') IS NULL THEN 0 ELSE 1 END;").Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the disposable Tender database target.' }
    if ($existing -ne '0') { throw 'The uniquely named disposable Tender database already exists.' }
    $databaseCleanupAuthorized = $true

    $connection = "Server=$SqlServer;Database=$databaseName;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    Set-RunEnvironment 'ConnectionStrings__DefaultConnection' $connection -Sensitive
    Set-RunEnvironment 'TDC_TENDER_SQL_CONNECTION' $connection -Sensitive
    Set-RunEnvironment 'Database__Provider' 'SqlServer'
    Set-RunEnvironment 'ASPNETCORE_ENVIRONMENT' 'Testing'
    Set-RunEnvironment 'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' 'true'
    Set-RunEnvironment 'SkipStartupInitialization' 'true'
    Set-RunEnvironment 'BackgroundServices__Enabled' 'false'
    Set-RunEnvironment 'JwtSettings__SecretKey' (New-EphemeralPassword) -Sensitive
    Set-RunEnvironment 'JwtSettings__Issuer' 'Rhema.Tender.Browser.Acceptance'
    Set-RunEnvironment 'JwtSettings__Audience' 'Rhema.Tender.Browser.Client'
    Set-RunEnvironment 'JwtSettings__PortalSecretKey' (New-EphemeralPassword) -Sensitive
    Set-RunEnvironment 'JwtSettings__PortalAudience' 'Rhema.Tender.Browser.Portal'
    Set-RunEnvironment 'CandidatePortal__PortalUrl' 'https://candidate.test/'
    Set-RunEnvironment 'Security__EncryptionKey' ([Convert]::ToBase64String((New-EphemeralBytes 32))) -Sensitive
    Set-RunEnvironment 'FileVirusScan__ClamAv__Host' '127.0.0.1'
    Set-RunEnvironment 'FileStorage__Local__PrivateBasePath' (Join-Path $resolvedRunDirectory 'secure-file-storage')
    Set-RunEnvironment 'CorsSettings__AllowedOrigins__0' "http://127.0.0.1:$FrontendPort"
    Set-RunEnvironment 'NEXT_PUBLIC_API_URL' "http://127.0.0.1:$ApiPort/api"
    Set-RunEnvironment 'NEXT_TELEMETRY_DISABLED' '1'

    $actorPasswordNames = @(
        'TENDER_E2E_REQUESTER_PASSWORD',
        'TENDER_E2E_PR_APPROVER_PASSWORD',
        'TENDER_E2E_PROCUREMENT_OFFICER_PASSWORD',
        'TENDER_E2E_FEE_VERIFIER_PASSWORD',
        'TENDER_E2E_EVALUATOR_PASSWORD',
        'TENDER_E2E_EVALUATOR_B_PASSWORD',
        'TENDER_E2E_EVALUATOR_C_PASSWORD',
        'TENDER_E2E_APPROVER_PASSWORD',
        'TENDER_E2E_ETC_APPROVER_PASSWORD',
        'TENDER_E2E_CONTRACT_APPROVER_PASSWORD',
        'TENDER_E2E_SUPPLIER_PASSWORD',
        'TENDER_E2E_SUPPLIER_B_PASSWORD',
        'TENDER_E2E_SUPPLIER_BLOCKED_PASSWORD',
        'TENDER_E2E_UNAUTHORIZED_PASSWORD'
    )
    foreach ($name in $actorPasswordNames) {
        Set-RunEnvironment $name (New-EphemeralPassword) -Sensitive
    }
    Set-RunEnvironment 'TENDER_E2E_RUN_ID' "tender-browser-$([Guid]::NewGuid().ToString('N'))"

    Set-RunEnvironment 'TDC_TENDER_MAKER_USERNAME' 'accounts.officer'
    Set-RunEnvironment 'TDC_TENDER_EVALUATOR_USERNAME' 'helpdesk.agent'
    Set-RunEnvironment 'TDC_TENDER_SECRETARY_USERNAME' 'helpdesk.supervisor'
    Set-RunEnvironment 'TDC_TENDER_APPROVER_USERNAME' 'financial.controller'
    Set-RunEnvironment 'TDC_TENDER_SUPPLIER_USERNAME' 'external'
    Set-RunEnvironment 'TDC_TENDER_UNAUTHORIZED_USERNAME' 'employee'
    Set-RunEnvironment 'TENDER_E2E_REQUESTER_USERNAME' 'manager'
    Set-RunEnvironment 'TENDER_E2E_PR_APPROVER_USERNAME' 'finance.clerk'
    Set-RunEnvironment 'TENDER_E2E_FEE_VERIFIER_USERNAME' 'ap.officer'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_B_USERNAME' 'helpdesk.supervisor'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_C_USERNAME' 'helpdesk.manager'
    Set-RunEnvironment 'TENDER_E2E_ETC_APPROVER_USERNAME' 'tdc.tender.etc-approver'
    Set-RunEnvironment 'TENDER_E2E_CONTRACT_APPROVER_USERNAME' 'chief.accountant'
    Set-RunEnvironment 'TENDER_E2E_SUPPLIER_BLOCKED_USERNAME' 'tdc.tender.supplier-blocked'

    if (-not $SkipBuild) {
        $apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
        & dotnet build $apiProject --no-restore -m:1 -p:UseSharedCompilation=false `
            -p:BuildInParallel=false -p:WarningLevel=0 -p:TdcFastEfBuild=true *> $apiBuildLog
        if ($LASTEXITCODE -ne 0) {
            throw "The Tender browser API build failed.$([Environment]::NewLine)$(Get-LogTail @($apiBuildLog))"
        }

        Push-Location (Join-Path $repoRoot 'frontend')
        try {
            & npm.cmd run build *> $frontendBuildLog
            if ($LASTEXITCODE -ne 0) {
                throw "The Tender browser frontend build failed.$([Environment]::NewLine)$(Get-LogTail @($frontendBuildLog))"
            }
        } finally {
            Pop-Location
        }
    }

    $apiDll = Join-Path $repoRoot 'src\ErpSystem.Api\bin\Debug\net8.0\ErpSystem.Api.dll'
    if (-not (Test-Path -LiteralPath $apiDll)) {
        throw 'The Tender browser API build did not produce its expected assembly.'
    }

    $resolvedSqlDataDirectory = [IO.Path]::GetFullPath($SqlDataDirectory)
    if (-not (Test-Path -LiteralPath $resolvedSqlDataDirectory -PathType Container)) {
        throw "The SQL data directory '$resolvedSqlDataDirectory' does not exist."
    }
    $dataFile = Join-Path $resolvedSqlDataDirectory "$databaseName.mdf"
    $logFile = Join-Path $resolvedSqlDataDirectory "${databaseName}_log.ldf"
    if ((Test-Path -LiteralPath $dataFile) -or (Test-Path -LiteralPath $logFile)) {
        throw 'A disposable Tender database file already exists for the unique run name.'
    }
    $escapedDataFile = $dataFile.Replace("'", "''")
    $escapedLogFile = $logFile.Replace("'", "''")
    & sqlcmd -S $SqlServer -E -C -d master -b -Q @"
CREATE DATABASE [$databaseName]
ON PRIMARY
(
    NAME = N'${databaseName}_data',
    FILENAME = N'$escapedDataFile',
    SIZE = 64MB,
    FILEGROWTH = 64MB
)
LOG ON
(
    NAME = N'${databaseName}_log',
    FILENAME = N'$escapedLogFile',
    SIZE = 32MB,
    FILEGROWTH = 32MB
);
"@ | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create the guarded disposable Tender database files in the configured SQL data directory.'
    }

    # The repository's historical fresh-database migration chain predates its
    # later baseline migration and is currently not replayable. The guarded Tender
    # seeder therefore creates the current EF model in this disposable database and
    # stamps that exact model's migrations before creating prerequisite actors/data.
    Invoke-ApiCommand $apiDll 'seed-tender-e2e' $actorSeedLog 'The disposable Tender actor seed failed.'

    Set-RunEnvironment 'ASPNETCORE_URLS' "http://127.0.0.1:$ApiPort"
    Set-RunEnvironment 'TENDER_E2E_API_BASE_URL' "http://127.0.0.1:$ApiPort"
    Set-RunEnvironment 'TENDER_E2E_RUN' '1'
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll) `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $apiLog `
        -RedirectStandardError $apiErrorLog -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$ApiPort/health/live" 'Tender API' $apiProcess @($apiLog, $apiErrorLog)

    $prepareScript = Join-Path $repoRoot 'scripts\acceptance\tender-lifecycle\Invoke-TenderLifecyclePrepare.ps1'
    $verifyScript = Join-Path $repoRoot 'scripts\acceptance\tender-lifecycle\Invoke-TenderLifecycleVerify.ps1'
    if (-not (Test-Path -LiteralPath $prepareScript)) { throw 'The Tender lifecycle Prepare entrypoint is missing.' }
    if (-not (Test-Path -LiteralPath $verifyScript)) { throw 'The Tender lifecycle Verify entrypoint is missing.' }

    & $prepareScript -Apply -OutputPath $fixturePath *> $prepareLog
    if (-not (Test-Path -LiteralPath $fixturePath)) {
        throw "Tender lifecycle fixture preparation did not create its manifest.$([Environment]::NewLine)$(Get-LogTail @($prepareLog))"
    }
    if (-not (Select-String -LiteralPath $prepareLog -SimpleMatch 'TENDER-LIFECYCLE-PREPARED|' -Quiet)) {
        throw "Tender lifecycle fixture preparation did not emit its success marker.$([Environment]::NewLine)$(Get-LogTail @($prepareLog))"
    }

    $nextCli = Join-Path $repoRoot 'frontend\node_modules\next\dist\bin\next'
    if (-not (Test-Path -LiteralPath $nextCli)) {
        throw 'Install the branch-locked frontend dependencies before Tender browser acceptance.'
    }
    $frontendProcess = Start-Process -FilePath 'node' `
        -ArgumentList @($nextCli, 'start', '--hostname', '127.0.0.1', '--port', $FrontendPort) `
        -WorkingDirectory (Join-Path $repoRoot 'frontend') `
        -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrorLog `
        -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$FrontendPort/login" 'Tender frontend' $frontendProcess `
        @($frontendLog, $frontendErrorLog)

    Set-RunEnvironment 'E2E_API_URL' "http://127.0.0.1:$ApiPort"
    Set-RunEnvironment 'E2E_BASE_URL' "http://127.0.0.1:$FrontendPort"
    Set-RunEnvironment 'TENDER_E2E_FIXTURE' $fixturePath
    Set-RunEnvironment 'TENDER_E2E_RESULT' $resultPath
    Set-RunEnvironment 'TENDER_E2E_PROCUREMENT_OFFICER_USERNAME' 'accounts.officer'
    Set-RunEnvironment 'TENDER_E2E_PROCUREMENT_OFFICER_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_SUPPLIER_USERNAME' 'external'
    Set-RunEnvironment 'TENDER_E2E_SUPPLIER_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_SUPPLIER_B_USERNAME' 'tdc.tender.supplier-b'
    Set-RunEnvironment 'TENDER_E2E_SUPPLIER_B_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_USERNAME' 'helpdesk.agent'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_B_USERNAME' 'helpdesk.supervisor'
    Set-RunEnvironment 'TENDER_E2E_EVALUATOR_B_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_APPROVER_USERNAME' 'financial.controller'
    Set-RunEnvironment 'TENDER_E2E_APPROVER_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_ETC_APPROVER_USERNAME' 'tdc.tender.etc-approver'
    Set-RunEnvironment 'TENDER_E2E_ETC_APPROVER_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_CONTRACT_APPROVER_USERNAME' 'chief.accountant'
    Set-RunEnvironment 'TENDER_E2E_CONTRACT_APPROVER_TENANT_CODE' 'DEFAULT'
    Set-RunEnvironment 'TENDER_E2E_UNAUTHORIZED_USERNAME' 'employee'
    Set-RunEnvironment 'TENDER_E2E_UNAUTHORIZED_TENANT_CODE' 'DEFAULT'

    Push-Location (Join-Path $repoRoot 'e2e-tests')
    try {
        & npx.cmd playwright test tests/tender-lifecycle.real.spec.ts `
            --project=chromium --workers=1 --reporter=list *> $playwrightLog
        if ($LASTEXITCODE -ne 0) {
            throw "Tender authenticated Playwright lifecycle failed.$([Environment]::NewLine)$(Get-LogTail @($playwrightLog))"
        }
    } finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw 'Tender lifecycle Playwright completed without its database-verification result manifest.'
    }
    & $verifyScript -ManifestPath $fixturePath -ResultPath $resultPath -Stage Complete *> $verifyLog
    if (-not (Select-String -LiteralPath $verifyLog -SimpleMatch 'TENDER-LIFECYCLE-VERIFIED|Complete|' -Quiet)) {
        throw "Tender lifecycle verification did not emit its completion marker.$([Environment]::NewLine)$(Get-LogTail @($verifyLog))"
    }

    Write-Output 'TENDER_BROWSER_DATABASE_ASSERTIONS_PASS'
    Write-Output 'TENDER_BROWSER_E2E_PASS'
} finally {
    if ($frontendProcess -and -not $frontendProcess.HasExited) {
        Stop-Process -Id $frontendProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($databaseCleanupAuthorized) {
        if ($databaseName -notmatch $databasePattern) {
            throw 'Refusing to drop a database outside the disposable Tender browser prefix.'
        }
        & sqlcmd -S $SqlServer -E -C -d master -b -Q `
            "IF DB_ID(N'$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; END;" | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Output 'TENDER_BROWSER_DATABASE_DROPPED' }
    }

    foreach ($name in $environmentNames | Select-Object -Unique) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    for ($index = 0; $index -lt $sensitiveValues.Count; $index++) {
        $sensitiveValues[$index] = [string]::Empty
    }
    if (Test-Path -LiteralPath $resolvedRunDirectory) {
        Remove-Item -LiteralPath $resolvedRunDirectory -Recurse -Force
    }
}
