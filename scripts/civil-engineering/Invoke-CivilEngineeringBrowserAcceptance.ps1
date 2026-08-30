[CmdletBinding()]
param(
    [string]$SqlServer = 'localhost\SQL2022',
    [int]$ApiPort = 5017,
    [int]$FrontendPort = 3017
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$databaseName = "RhemaERP_CivilBrowser_$([Guid]::NewGuid().ToString('N'))"
if (-not $databaseName.StartsWith('RhemaERP_CivilBrowser_', [StringComparison]::Ordinal)) {
    throw 'The disposable Civil database name is outside the approved prefix.'
}

$runDirectory = Join-Path ([IO.Path]::GetTempPath()) "rhema-civil-browser-$([Guid]::NewGuid().ToString('N'))"
$resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$resolvedRunDirectory = [IO.Path]::GetFullPath($runDirectory)
if (-not $resolvedRunDirectory.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Civil browser run directory did not resolve beneath the system temporary directory.'
}
New-Item -ItemType Directory -Path $resolvedRunDirectory | Out-Null

$apiLog = Join-Path $resolvedRunDirectory 'api.out.log'
$apiErrorLog = Join-Path $resolvedRunDirectory 'api.err.log'
$frontendLog = Join-Path $resolvedRunDirectory 'frontend.out.log'
$frontendErrorLog = Join-Path $resolvedRunDirectory 'frontend.err.log'
$buildLog = Join-Path $resolvedRunDirectory 'build.log'
$seedLog = Join-Path $resolvedRunDirectory 'seed.log'
$seedErrorLog = Join-Path $resolvedRunDirectory 'seed.err.log'
$apiProcess = $null
$frontendProcess = $null
$databaseCreated = $false
$environmentNames = [Collections.Generic.List[string]]::new()

function Set-RunEnvironment([string]$Name, [string]$Value) {
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
    $environmentNames.Add($Name)
}

function New-EphemeralPassword {
    $bytes = New-Object byte[] 24
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

function Get-LogTail([string[]]$LogPaths) {
    return (($LogPaths | ForEach-Object {
        if (Test-Path $_) { Get-Content $_ -Tail 40 }
    }) -join [Environment]::NewLine)
}

function Wait-HttpReady([string]$Url, [string]$ProcessName, [Diagnostics.Process]$Process, [string[]]$LogPaths) {
    for ($attempt = 1; $attempt -le 180; $attempt++) {
        if ($Process.HasExited) {
            $tail = Get-LogTail $LogPaths
            throw "$ProcessName exited before becoming ready.$([Environment]::NewLine)$tail"
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { return }
        } catch {
            # Readiness is polled until the bounded startup window expires.
        }
        Start-Sleep -Seconds 1
    }
    throw "$ProcessName did not become ready within 180 seconds."
}

try {
    $apiProject = Join-Path $repoRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
    # Browser acceptance materializes the current model with EnsureCreated and stamps
    # migration history through the same supported helper as the rebuild-db command.
    # The separately reported SQL migration/trigger gates cover forward operations.
    & dotnet build $apiProject --no-restore -m:1 -p:UseSharedCompilation=false -p:BuildInParallel=false -p:WarningLevel=0 -p:TdcFastEfBuild=true *> $buildLog
    if ($LASTEXITCODE -ne 0) {
        throw "The Civil browser API build failed.$([Environment]::NewLine)$((Get-Content $buildLog -Tail 100) -join [Environment]::NewLine)"
    }

    & sqlcmd -S $SqlServer -E -C -d master -b -Q "CREATE DATABASE [$databaseName];" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the disposable Civil browser database.' }
    $databaseCreated = $true

    $connection = "Server=$SqlServer;Database=$databaseName;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
    $assignerPassword = New-EphemeralPassword
    $assigneePassword = New-EphemeralPassword
    $reviewerPassword = New-EphemeralPassword
    $unauthorizedPassword = New-EphemeralPassword
    $jwtSecret = New-EphemeralPassword
    $portalSecret = New-EphemeralPassword
    $encryptionKey = [Convert]::ToBase64String((New-EphemeralBytes 32))

    Set-RunEnvironment 'ConnectionStrings__DefaultConnection' $connection
    Set-RunEnvironment 'Database__Provider' 'SqlServer'
    Set-RunEnvironment 'ASPNETCORE_ENVIRONMENT' 'Testing'
    Set-RunEnvironment 'SkipStartupInitialization' 'true'
    Set-RunEnvironment 'BackgroundServices__Enabled' 'false'
    Set-RunEnvironment 'JwtSettings__SecretKey' $jwtSecret
    Set-RunEnvironment 'JwtSettings__Issuer' 'Rhema.Civil.Browser.Acceptance'
    Set-RunEnvironment 'JwtSettings__Audience' 'Rhema.Civil.Browser.Client'
    Set-RunEnvironment 'JwtSettings__PortalSecretKey' $portalSecret
    Set-RunEnvironment 'JwtSettings__PortalAudience' 'Rhema.Civil.Browser.Portal'
    Set-RunEnvironment 'CandidatePortal__PortalUrl' 'https://candidate.test/'
    Set-RunEnvironment 'Security__EncryptionKey' $encryptionKey
    Set-RunEnvironment 'FileVirusScan__ClamAv__Host' '127.0.0.1'
    Set-RunEnvironment 'CorsSettings__AllowedOrigins__0' "http://127.0.0.1:$FrontendPort"
    Set-RunEnvironment 'RHEMA_CIVIL_E2E_ASSIGNER_PASSWORD' $assignerPassword
    Set-RunEnvironment 'RHEMA_CIVIL_E2E_ASSIGNEE_PASSWORD' $assigneePassword
    Set-RunEnvironment 'RHEMA_CIVIL_E2E_REVIEWER_PASSWORD' $reviewerPassword
    Set-RunEnvironment 'RHEMA_CIVIL_E2E_UNAUTHORIZED_PASSWORD' $unauthorizedPassword

    $apiDll = Join-Path $repoRoot 'src\ErpSystem.Api\bin\Debug\net8.0\ErpSystem.Api.dll'
    if (-not (Test-Path $apiDll)) { throw 'The Civil browser API build did not produce its expected assembly.' }
    $seedProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll, 'seed-civil-e2e') `
        -WorkingDirectory $repoRoot -RedirectStandardOutput $seedLog -RedirectStandardError $seedErrorLog `
        -WindowStyle Hidden -Wait -PassThru
    if ($seedProcess.ExitCode -ne 0) {
        throw "Civil browser fixture seeding failed.$([Environment]::NewLine)$(Get-LogTail @($seedLog, $seedErrorLog))"
    }

    Set-RunEnvironment 'ASPNETCORE_URLS' "http://127.0.0.1:$ApiPort"
    $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList @($apiDll) -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $apiLog -RedirectStandardError $apiErrorLog -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$ApiPort/health/live" 'Civil API' $apiProcess @($apiLog, $apiErrorLog)

    Set-RunEnvironment 'NEXT_PUBLIC_API_URL' "http://127.0.0.1:$ApiPort/api"
    $nextCli = Join-Path $repoRoot 'frontend\node_modules\next\dist\bin\next'
    $frontendProcess = Start-Process -FilePath 'node' -ArgumentList @($nextCli, 'dev', '--hostname', '127.0.0.1', '--port', $FrontendPort) `
        -WorkingDirectory (Join-Path $repoRoot 'frontend') -RedirectStandardOutput $frontendLog -RedirectStandardError $frontendErrorLog `
        -WindowStyle Hidden -PassThru
    Wait-HttpReady "http://127.0.0.1:$FrontendPort/login" 'Civil frontend' $frontendProcess @($frontendLog, $frontendErrorLog)

    Set-RunEnvironment 'E2E_API_URL' "http://127.0.0.1:$ApiPort"
    Set-RunEnvironment 'E2E_BASE_URL' "http://127.0.0.1:$FrontendPort"
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ENABLED' '1'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_PROJECT_ID' 'c1b5f34d-618c-42af-95ea-744e9fe32b32'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_PROJECT_LABEL' 'CIV-E2E-001 · Civil Engineering Browser Acceptance'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ASSIGNEE_LABEL' 'Jane Employee · TDC_CIVIL_TECHNICIAN'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ASSIGNER_USERNAME' 'manager'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ASSIGNER_PASSWORD' $assignerPassword
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ASSIGNEE_USERNAME' 'employee'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_ASSIGNEE_PASSWORD' $assigneePassword
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_REVIEWER_USERNAME' 'helpdesk.supervisor'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_REVIEWER_PASSWORD' $reviewerPassword
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_UNAUTHORIZED_USERNAME' 'external'
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_UNAUTHORIZED_PASSWORD' $unauthorizedPassword
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_TENANT_CODE' 'DEFAULT'
    $taskTitle = "Civil browser acceptance $([Guid]::NewGuid().ToString('N'))"
    Set-RunEnvironment 'CIVIL_ACCEPTANCE_TASK_TITLE' $taskTitle

    Push-Location (Join-Path $repoRoot 'e2e-tests')
    try {
        & npx.cmd playwright test tests/civil-engineering-authenticated-smoke.spec.ts --project=chromium --workers=1
        if ($LASTEXITCODE -ne 0) { throw 'Civil Engineering authenticated Playwright acceptance failed.' }
    } finally {
        Pop-Location
    }

    $databaseAssertion = @"
SET NOCOUNT ON;
DECLARE @title nvarchar(200)=N'$taskTitle';
DECLARE @taskId uniqueidentifier=(
    SELECT control.Id
    FROM dbo.ProjectCivilDirectTaskControls control
    JOIN dbo.ProjectWorkItems workItem ON workItem.Id=control.WorkItemId AND workItem.TenantId=control.TenantId
    WHERE control.TenantId='00000000-0000-0000-0000-000000000001'
      AND control.ProjectId='c1b5f34d-618c-42af-95ea-744e9fe32b32'
      AND workItem.Title=@title
      AND control.IsDeleted=0 AND workItem.IsDeleted=0
);
IF @taskId IS NULL THROW 53001, 'Civil browser database read-back could not find the fresh task.', 1;
IF (SELECT COUNT(*) FROM dbo.ProjectCivilDirectTaskControls WHERE Id=@taskId) <> 1
    THROW 53002, 'Civil browser database read-back found duplicate task controls.', 1;
IF NOT EXISTS (
    SELECT 1 FROM dbo.ProjectCivilDirectTaskControls
    WHERE Id=@taskId AND Status='Accepted' AND ApprovalStatus='Approved' AND ProgressPercent=100
      AND WorkflowInstanceId IS NOT NULL AND AcceptedAt IS NOT NULL AND AcceptedById IS NOT NULL
      AND CreatedById <> AssignedToUserId AND CreatedById <> AcceptedById AND AssignedToUserId <> AcceptedById)
    THROW 53003, 'Civil browser final state or maker-assignee-reviewer separation is invalid.', 1;
IF (SELECT COUNT(*) FROM dbo.ProjectCivilDirectTaskFeedbackEntries WHERE DirectTaskControlId=@taskId AND IsDeleted=0) <> 3
    THROW 53004, 'Civil browser feedback history is incomplete or duplicated.', 1;
IF (SELECT COUNT(DISTINCT ActorUserId) FROM dbo.ProjectCivilDirectTaskFeedbackEntries WHERE DirectTaskControlId=@taskId AND IsDeleted=0) <> 2
    THROW 53005, 'Civil browser assignee and reviewer feedback actors are not separated.', 1;
IF (SELECT COUNT(DISTINCT ActorUserId) FROM dbo.ProjectCivilDirectTaskRevisions WHERE DirectTaskControlId=@taskId AND IsDeleted=0) <> 3
    THROW 53006, 'Civil browser immutable revisions do not retain all three business actors.', 1;
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID(N'dbo.ProjectCivilDirectTaskControls'),
        OBJECT_ID(N'dbo.ProjectCivilDirectTaskFeedbackEntries'),
        OBJECT_ID(N'dbo.ProjectCivilDirectTaskRevisions'))
      AND (is_disabled=1 OR is_not_trusted=1))
    THROW 53007, 'Civil browser direct-task foreign keys are disabled or untrusted.', 1;
"@
    & sqlcmd -S $SqlServer -E -C -d $databaseName -b -Q $databaseAssertion | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Civil Engineering final database assertions failed.' }
    Write-Output 'CIVIL_BROWSER_DATABASE_ASSERTIONS_PASS'

    Write-Output 'CIVIL_BROWSER_E2E_PASS'
} finally {
    if ($frontendProcess -and -not $frontendProcess.HasExited) { Stop-Process -Id $frontendProcess.Id -Force -ErrorAction SilentlyContinue }
    if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue }

    if ($databaseCreated) {
        if (-not $databaseName.StartsWith('RhemaERP_CivilBrowser_', [StringComparison]::Ordinal)) {
            throw 'Refusing to drop a database outside the disposable Civil browser prefix.'
        }
        & sqlcmd -S $SqlServer -E -C -d master -b -Q "IF DB_ID(N'$databaseName') IS NOT NULL BEGIN ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName]; END;" | Out-Null
        if ($LASTEXITCODE -eq 0) { Write-Output 'CIVIL_BROWSER_DATABASE_DROPPED' }
    }

    foreach ($name in $environmentNames | Select-Object -Unique) {
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    if (Test-Path -LiteralPath $resolvedRunDirectory) {
        Remove-Item -LiteralPath $resolvedRunDirectory -Recurse -Force
    }
}
