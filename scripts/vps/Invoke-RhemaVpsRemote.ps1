[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Preflight', 'Backup', 'Apply', 'Verify')]
    [string]$Action,

    [string]$DeploymentId,
    [string]$ExpectedCommit,
    [string]$ExpectedBuildId,
    [string]$ExpectedCacheVersion,
    [string]$ApiPackageName,
    [string]$FrontendPackageName,
    [string]$ApiSha256,
    [string]$FrontendSha256,
    [int]$ApiReadyTimeoutSeconds = 420
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$RhemaRoot = 'C:\RhemaERP'
$ApiRoot = Join-Path $RhemaRoot 'api'
$FrontendRoot = Join-Path $RhemaRoot 'frontend'
$PackagesRoot = Join-Path $RhemaRoot 'packages'
$BackupsRoot = Join-Path $RhemaRoot 'backups'
$LogsRoot = Join-Path $RhemaRoot 'logs'
$ApiServiceXml = Join-Path $RhemaRoot 'services\api\RhemaERPAPI.xml'
$ExpectedPublicOrigin = 'https://149.102.145.190:8443'

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-DeploymentId {
    Assert-True (-not [string]::IsNullOrWhiteSpace($DeploymentId)) `
        'DeploymentId is required for this action.'
    Assert-True ($DeploymentId -match '^[a-zA-Z0-9-]+$') `
        'DeploymentId contains unsupported characters.'
}

function Get-ApiConfigurationXml {
    Assert-True (Test-Path -LiteralPath $ApiServiceXml) `
        "API service configuration is missing: $ApiServiceXml"
    return [xml](Get-Content -LiteralPath $ApiServiceXml)
}

function Get-DatabaseConnectionString {
    $xml = Get-ApiConfigurationXml
    $value = [string](($xml.service.env | Where-Object {
        $_.name -eq 'ConnectionStrings__DefaultConnection'
    }).value)
    Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
        'Database connection setting is missing from the API service configuration.'
    return $value
}

function Invoke-DatabaseTable {
    param([string]$Query, [int]$TimeoutSeconds = 120)

    $connection = New-Object System.Data.SqlClient.SqlConnection `
        (Get-DatabaseConnectionString)
    $command = $connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = $TimeoutSeconds
    $connection.Open()
    try {
        $reader = $command.ExecuteReader()
        $table = New-Object System.Data.DataTable
        $table.Load($reader)
        return $table
    }
    finally {
        $connection.Close()
    }
}

function Invoke-DatabaseNonQuery {
    param([string]$Query, [int]$TimeoutSeconds = 1800)

    $connection = New-Object System.Data.SqlClient.SqlConnection `
        (Get-DatabaseConnectionString)
    $command = $connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = $TimeoutSeconds
    $connection.Open()
    try {
        [void]$command.ExecuteNonQuery()
    }
    finally {
        $connection.Close()
    }
}

function Invoke-RobocopyChecked {
    param([string[]]$Arguments)

    & robocopy.exe @Arguments | Out-Null
    $code = $LASTEXITCODE
    if ($code -gt 7) {
        throw "Robocopy failed with exit code $code."
    }
}

function Set-ServiceEnvironmentValue {
    param(
        [xml]$Xml,
        [string]$Name,
        [string]$Value
    )

    $node = @($Xml.service.env | Where-Object { $_.name -eq $Name })[0]
    if ($null -eq $node) {
        $node = $Xml.CreateElement('env')
        $node.SetAttribute('name', $Name)
        [void]$Xml.service.AppendChild($node)
    }
    $node.SetAttribute('value', $Value)
}

function Set-TestServerConfiguration {
    $xml = Get-ApiConfigurationXml
    @($xml.service.env | Where-Object {
        $_.name -like 'CorsSettings__AllowedOrigins__*'
    }) | ForEach-Object { [void]$xml.service.RemoveChild($_) }

    Set-ServiceEnvironmentValue $xml 'CorsSettings__AllowedOrigins__0' `
        $ExpectedPublicOrigin
    Set-ServiceEnvironmentValue $xml `
        'StartupInitialization__SeedDevelopmentData' 'true'
    Set-ServiceEnvironmentValue $xml `
        'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' 'true'
    Set-ServiceEnvironmentValue $xml 'CandidatePortal__PortalUrl' `
        $ExpectedPublicOrigin
    Set-ServiceEnvironmentValue $xml 'FrontendUrl' $ExpectedPublicOrigin

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create($ApiServiceXml, $settings)
    try { $xml.Save($writer) }
    finally { $writer.Close() }
}

function Get-MigrationHistory {
    return Invoke-DatabaseTable @"
SELECT MigrationId
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
"@
}

function Get-MigrationGuardResults {
    return Invoke-DatabaseTable @"
SET NOCOUNT ON;
DECLARE @R TABLE (CheckName nvarchar(160), AffectedRows bigint);
IF OBJECT_ID(N'EmployeeShiftPreferences',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeShiftPreferences'',COUNT_BIG(*) FROM EmployeeShiftPreferences');
IF OBJECT_ID(N'Shifts',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''Shifts'',COUNT_BIG(*) FROM Shifts');
IF OBJECT_ID(N'WorkStations',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''WorkStations'',COUNT_BIG(*) FROM WorkStations');
IF COL_LENGTH('ShiftAssignments','StartDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''ShiftAssignments.StartDate'',COUNT_BIG(*) FROM ShiftAssignments WHERE StartDate IS NOT NULL');
IF COL_LENGTH('LeaveRequests','ApprovalNotes') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveRequests.ApprovalNotes'',COUNT_BIG(*) FROM LeaveRequests WHERE ApprovalNotes IS NOT NULL');
IF COL_LENGTH('LeaveRequests','RejectionDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveRequests.RejectionDate'',COUNT_BIG(*) FROM LeaveRequests WHERE RejectionDate IS NOT NULL');
IF OBJECT_ID(N'ShiftAssignments',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''ShiftAssignments rows'',COUNT_BIG(*) FROM ShiftAssignments');
IF COL_LENGTH('LeavePlans','DepartmentId') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeavePlans.DepartmentId'',COUNT_BIG(*) FROM LeavePlans WHERE DepartmentId IS NOT NULL');
IF COL_LENGTH('LeaveBalances','AdjustmentReason') IS NOT NULL INSERT @R EXEC(N'SELECT ''LeaveBalances.AdjustmentReason'',COUNT_BIG(*) FROM LeaveBalances WHERE AdjustmentReason IS NOT NULL');
IF COL_LENGTH('EmployeePositions','MinSalary') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeePositions legacy fields'',COUNT_BIG(*) FROM EmployeePositions WHERE MinSalary IS NOT NULL OR MaxSalary IS NOT NULL OR Requirements IS NOT NULL OR Responsibilities IS NOT NULL');
IF COL_LENGTH('EmployeeIdentificationCards','DocumentType') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeIdentificationCards legacy fields'',COUNT_BIG(*) FROM EmployeeIdentificationCards WHERE DocumentType IS NOT NULL OR IssuingAuthority IS NOT NULL');
IF COL_LENGTH('EmployeeDependents','IsEmergencyContact') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeDependents legacy fields'',COUNT_BIG(*) FROM EmployeeDependents WHERE IsEmergencyContact=1 OR IsStudentDependent=1');
IF OBJECT_ID(N'PublicHolidays',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PublicHolidays'',COUNT_BIG(*) FROM PublicHolidays');
IF OBJECT_ID(N'KpiEvaluationRecords',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''KpiEvaluationRecords'',COUNT_BIG(*) FROM KpiEvaluationRecords');
IF OBJECT_ID(N'EmployeeKpiTargets',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''EmployeeKpiTargets'',COUNT_BIG(*) FROM EmployeeKpiTargets');
IF OBJECT_ID(N'AppraisalCriterias',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalCriterias'',COUNT_BIG(*) FROM AppraisalCriterias');
IF OBJECT_ID(N'PositionCriteriaMappings',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PositionCriteriaMappings'',COUNT_BIG(*) FROM PositionCriteriaMappings');
IF OBJECT_ID(N'MappingGradeRanges',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''MappingGradeRanges'',COUNT_BIG(*) FROM MappingGradeRanges');
IF OBJECT_ID(N'PerformanceAppraisals',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''PerformanceAppraisals'',COUNT_BIG(*) FROM PerformanceAppraisals');
IF COL_LENGTH('EvaluatorEvaluations','EvaluationDate') IS NOT NULL INSERT @R EXEC(N'SELECT ''EvaluatorEvaluations.EvaluationDate'',COUNT_BIG(*) FROM EvaluatorEvaluations WHERE EvaluationDate IS NOT NULL');
IF OBJECT_ID(N'CriterionScores',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''CriterionScores'',COUNT_BIG(*) FROM CriterionScores');
IF OBJECT_ID(N'AppraisalEmployeeResponses',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalEmployeeResponses'',COUNT_BIG(*) FROM AppraisalEmployeeResponses');
IF OBJECT_ID(N'AppraisalAttachments',N'U') IS NOT NULL INSERT @R EXEC(N'SELECT ''AppraisalAttachments'',COUNT_BIG(*) FROM AppraisalAttachments');
SELECT CheckName,AffectedRows FROM @R WHERE AffectedRows > 0 ORDER BY CheckName;
"@
}

function Get-DatabaseControlSummary {
    return Invoke-DatabaseTable @"
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) AS MigrationCount,
    (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory) AS LatestMigration,
    (SELECT COUNT_BIG(*) FROM sys.foreign_keys WHERE is_not_trusted = 1) AS UntrustedForeignKeys,
    (SELECT COUNT_BIG(*) FROM sys.foreign_keys WHERE is_disabled = 1) AS DisabledForeignKeys,
    CASE WHEN OBJECT_ID(N'FileUploadPolicies',N'U') IS NULL THEN 0 ELSE
        (SELECT COUNT_BIG(*) FROM FileUploadPolicies
         WHERE ISNULL(AllowedExtensionsCsv,'') LIKE '%.svg%'
            OR ISNULL(AllowedMimeTypesCsv,'') LIKE '%image/svg+xml%') END AS SvgFileUploadPolicies,
    CASE WHEN OBJECT_ID(N'ProcurementSupplierEvidenceRequirements',N'U') IS NULL THEN 0 ELSE
        (SELECT COUNT_BIG(*) FROM ProcurementSupplierEvidenceRequirements
         WHERE AllowedMimeTypesJson LIKE '%image/svg+xml%') END AS SvgSupplierRequirements;
"@
}

function Write-ServiceState {
    Get-Service RhemaERPAPI,RhemaERPFrontend,RhemaERPHTTPSIPProxy |
        ForEach-Object { Write-Output "SERVICE|$($_.Name)|$($_.Status)" }
}

function Invoke-Preflight {
    foreach ($path in @($RhemaRoot, $ApiRoot, $FrontendRoot, $PackagesRoot,
            $BackupsRoot, $LogsRoot, $ApiServiceXml)) {
        Assert-True (Test-Path -LiteralPath $path) "Required VPS path is missing: $path"
    }

    $services = Get-Service RhemaERPAPI,RhemaERPFrontend,RhemaERPHTTPSIPProxy
    $notRunning = @($services | Where-Object { $_.Status -ne 'Running' })
    Assert-True ($notRunning.Count -eq 0) `
        "Preflight requires all deployed services running. Not running: $($notRunning.Name -join ', ')"

    $drive = Get-PSDrive -Name ([System.IO.Path]::GetPathRoot($RhemaRoot).TrimEnd(':\'))
    $freeGb = [Math]::Round($drive.Free / 1GB, 2)
    Write-Output "DISK_FREE_GB|$freeGb"
    Assert-True ($freeGb -ge 5) 'Less than 5 GB of free disk space remains on the VPS.'

    $xml = Get-ApiConfigurationXml
    $requiredSettings = @{
        'CorsSettings__AllowedOrigins__0' = $ExpectedPublicOrigin
        'StartupInitialization__SeedDevelopmentData' = 'true'
        'StartupInitialization__AllowDevelopmentDataSeedingOutsideDevelopment' = 'true'
        'CandidatePortal__PortalUrl' = $ExpectedPublicOrigin
        'FrontendUrl' = $ExpectedPublicOrigin
    }
    foreach ($entry in $requiredSettings.GetEnumerator()) {
        $actual = [string](($xml.service.env | Where-Object {
            $_.name -eq $entry.Key
        }).value)
        Write-Output "CONFIG|$($entry.Key)|$actual"
        Assert-True ($actual -eq $entry.Value) `
            "Test VPS configuration is invalid for $($entry.Key)."
    }
    $corsNodes = @($xml.service.env | Where-Object {
        $_.name -like 'CorsSettings__AllowedOrigins__*'
    })
    Assert-True ($corsNodes.Count -eq 1) `
        'The test VPS must expose exactly one HTTPS CORS origin.'

    $history = @(Get-MigrationHistory)
    foreach ($row in $history) { Write-Output "MIGRATION_ID|$($row.MigrationId)" }
    Write-Output 'GUARD_COVERAGE|20260720181131_AddHRModule'
    Write-Output 'GUARD_COVERAGE|20260720193903_AddHRPerformanceModule'
    # These migrations only define immutable-row trigger bodies. Their THROW statements
    # are not executed while applying the migrations and have no legacy-data precondition.
    Write-Output 'GUARD_COVERAGE|20260807202000_AddQuantitySurveyConfigurationRegister'
    Write-Output 'GUARD_COVERAGE|20260808124500_AddProjectBoqApprovalPublication'
    $guards = @(Get-MigrationGuardResults)
    foreach ($guard in $guards) {
        Write-Output "MIGRATION_GUARD|$($guard.CheckName)|$($guard.AffectedRows)"
    }
    Assert-True ($guards.Count -eq 0) `
        'One or more guarded HR migrations would halt. Resolve and archive the reported data before deployment.'

    $summary = @(Get-DatabaseControlSummary)[0]
    Write-Output "MIGRATION_COUNT|$($summary.MigrationCount)"
    Write-Output "LATEST_MIGRATION|$($summary.LatestMigration)"
    Write-Output "UNTRUSTED_FKS|$($summary.UntrustedForeignKeys)"
    Write-Output "DISABLED_FKS|$($summary.DisabledForeignKeys)"
    Write-Output "SVG_FILE_UPLOAD_POLICIES|$($summary.SvgFileUploadPolicies)"
    Write-Output "SVG_SUPPLIER_REQUIREMENTS|$($summary.SvgSupplierRequirements)"
    Assert-True ([long]$summary.UntrustedForeignKeys -eq 0) `
        'One or more database foreign keys are untrusted.'
    Assert-True ([long]$summary.DisabledForeignKeys -eq 0) `
        'One or more database foreign keys are disabled.'
    Assert-True ([long]$summary.SvgFileUploadPolicies -eq 0) `
        'Active SVG entries exist in a shared file-upload policy.'
    Assert-True ([long]$summary.SvgSupplierRequirements -eq 0) `
        'Active SVG entries exist in a supplier evidence requirement.'

    $configSvgMatches = @(Get-ChildItem $ApiRoot -File -Filter 'appsettings*.json' |
        Select-String -Pattern 'image/svg\+xml|\.svg' -AllMatches)
    Write-Output "SVG_RUNTIME_CONFIG_MATCHES|$($configSvgMatches.Count)"
    Assert-True ($configSvgMatches.Count -eq 0) `
        'Active SVG entries exist in deployed runtime configuration.'

    Write-ServiceState
    Write-Output 'PREFLIGHT|PASS'
}

function Invoke-Backup {
    Assert-DeploymentId
    $backupRoot = Join-Path $BackupsRoot "deploy-$DeploymentId"
    Assert-True (-not (Test-Path -LiteralPath $backupRoot)) `
        "Backup path already exists: $backupRoot"

    $apiBackup = Join-Path $backupRoot 'api'
    $frontendBackup = Join-Path $backupRoot 'frontend'
    $serviceBackup = Join-Path $backupRoot 'services\api'
    New-Item -ItemType Directory -Path $apiBackup,$frontendBackup,$serviceBackup |
        Out-Null

    Invoke-RobocopyChecked @(
        $ApiRoot, $apiBackup, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
        '/NJH', '/NJS', '/NP',
        '/XD', (Join-Path $ApiRoot 'logs'),
        (Join-Path $ApiRoot 'secure-file-storage'),
        (Join-Path $ApiRoot 'wwwroot\uploads')
    )
    Invoke-RobocopyChecked @(
        $FrontendRoot, $frontendBackup, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
        '/NJH', '/NJS', '/NP',
        '/XD', (Join-Path $FrontendRoot 'node_modules'),
        (Join-Path $FrontendRoot 'logs')
    )
    Copy-Item -LiteralPath $ApiServiceXml -Destination `
        (Join-Path $serviceBackup 'RhemaERPAPI.xml') -Force

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder `
        (Get-DatabaseConnectionString)
    $databaseName = $builder.InitialCatalog
    Assert-True (-not [string]::IsNullOrWhiteSpace($databaseName)) `
        'Database name is missing from the configured connection string.'
    $defaultBackup = @(Invoke-DatabaseTable `
        "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupPath;")[0].BackupPath
    Assert-True (-not [string]::IsNullOrWhiteSpace($defaultBackup)) `
        'SQL Server did not report its default backup path.'
    $sqlBackupPath = Join-Path $defaultBackup `
        ("{0}_pre_{1}.bak" -f $databaseName, $DeploymentId)
    Assert-True (-not (Test-Path -LiteralPath $sqlBackupPath)) `
        "SQL backup already exists: $sqlBackupPath"

    $safeDatabase = $databaseName.Replace(']', ']]')
    $safeBackupPath = $sqlBackupPath.Replace("'", "''")
    Invoke-DatabaseNonQuery @"
BACKUP DATABASE [$safeDatabase]
TO DISK = N'$safeBackupPath'
WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT, STATS = 10;
RESTORE VERIFYONLY FROM DISK = N'$safeBackupPath' WITH CHECKSUM;
"@ 3600
    Assert-True (Test-Path -LiteralPath $sqlBackupPath) `
        'SQL backup did not appear at the expected path.'

    $manifest = [ordered]@{
        deploymentId = $DeploymentId
        commit = $ExpectedCommit
        createdUtc = [DateTime]::UtcNow.ToString('o')
        applicationBackup = $backupRoot
        databaseBackup = $sqlBackupPath
        databaseBackupVerified = $true
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $backupRoot 'backup-manifest.json'),
        ($manifest | ConvertTo-Json -Depth 4),
        (New-Object System.Text.UTF8Encoding($false)))

    Write-Output "APPLICATION_BACKUP|$backupRoot"
    Write-Output "DATABASE_BACKUP|$sqlBackupPath"
    Write-Output 'BACKUP|PASS'
}

function Get-NewFatalStartupLine {
    param([DateTime]$StartedAt)

    $log = Get-ChildItem (Join-Path $ApiRoot 'logs\erp-api-*.log') -File `
        -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    if ($null -eq $log -or $log.LastWriteTime -lt $StartedAt.AddSeconds(-2)) {
        return $null
    }
    $cutoff = $StartedAt.TimeOfDay
    $fatal = Get-Content -LiteralPath $log.FullName | Where-Object {
        if ($_ -notmatch '^\[(?<time>\d{2}:\d{2}:\d{2}) (ERR|FTL)\]') {
            return $false
        }
        $time = [TimeSpan]::Parse($Matches.time)
        return $time -ge $cutoff -and $_ -match `
            'Hosting failed|upgrade halted|OptionsValidationException|failed to start'
    } | Select-Object -Last 1
    return $fatal
}

function Wait-ApiReady {
    param([DateTime]$StartedAt)

    $deadline = (Get-Date).AddSeconds($ApiReadyTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $fatal = Get-NewFatalStartupLine $StartedAt
        if ($null -ne $fatal) {
            throw "API startup reported a fatal condition: $($fatal.Substring(0, [Math]::Min(400, $fatal.Length)))"
        }
        try {
            $response = Invoke-WebRequest 'http://127.0.0.1:5000/health/live' `
                -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-Output "API_READY|200|$($response.Content.Trim())"
                return
            }
        }
        catch {
            # Migrations and seed initialization run before the HTTP listener binds.
        }
        Start-Sleep -Seconds 5
    }
    throw "API readiness exceeded $ApiReadyTimeoutSeconds seconds."
}

function Wait-FrontendReady {
    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest 'http://127.0.0.1:3001/login' `
                -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                Write-Output 'FRONTEND_READY|200'
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 3
    }
    throw 'Frontend readiness exceeded two minutes.'
}

function Invoke-Apply {
    Assert-DeploymentId
    foreach ($value in @($ApiPackageName, $FrontendPackageName, $ApiSha256,
            $FrontendSha256, $ExpectedBuildId, $ExpectedCacheVersion)) {
        Assert-True (-not [string]::IsNullOrWhiteSpace($value)) `
            'Apply requires package names, hashes, build ID, and cache version.'
    }
    Assert-True ($ApiPackageName -eq [System.IO.Path]::GetFileName($ApiPackageName)) `
        'ApiPackageName must be a file name, not a path.'
    Assert-True ($FrontendPackageName -eq [System.IO.Path]::GetFileName($FrontendPackageName)) `
        'FrontendPackageName must be a file name, not a path.'

    $apiZip = Join-Path $PackagesRoot $ApiPackageName
    $frontendZip = Join-Path $PackagesRoot $FrontendPackageName
    Assert-True (Test-Path -LiteralPath $apiZip) "API package is missing: $apiZip"
    Assert-True (Test-Path -LiteralPath $frontendZip) `
        "Frontend package is missing: $frontendZip"
    Assert-True ((Get-FileHash $apiZip -Algorithm SHA256).Hash -eq $ApiSha256) `
        'Uploaded API package hash does not match the release manifest.'
    Assert-True ((Get-FileHash $frontendZip -Algorithm SHA256).Hash -eq $FrontendSha256) `
        'Uploaded frontend package hash does not match the release manifest.'

    $stage = Join-Path $PackagesRoot "stage-$DeploymentId"
    $retired = Join-Path $PackagesRoot "retired-$DeploymentId"
    $failed = Join-Path $PackagesRoot "failed-$DeploymentId"
    $backup = Join-Path $BackupsRoot "deploy-$DeploymentId"
    Assert-True (Test-Path -LiteralPath $backup) `
        'The matching verified backup must exist before apply.'
    Assert-True (-not (Test-Path -LiteralPath $stage)) `
        "Deployment stage already exists: $stage"
    New-Item -ItemType Directory -Path (Join-Path $stage 'api'), `
        (Join-Path $stage 'frontend'), $retired | Out-Null
    Expand-Archive -LiteralPath $apiZip -DestinationPath (Join-Path $stage 'api') -Force
    Expand-Archive -LiteralPath $frontendZip `
        -DestinationPath (Join-Path $stage 'frontend') -Force

    $stageApi = Join-Path $stage 'api'
    $stageFrontend = Join-Path $stage 'frontend'
    Assert-True (Test-Path -LiteralPath (Join-Path $stageApi 'ErpSystem.Api.exe')) `
        'Staged API executable is missing.'
    Assert-True (Test-Path -LiteralPath (Join-Path $stageFrontend 'server.js')) `
        'Staged frontend server.js is missing.'
    $forbiddenApiConfig = @(Get-ChildItem $stageApi -File -Force | Where-Object {
        $_.Name -like 'appsettings*.json' -or $_.Name -like '.env*'
    })
    Assert-True ($forbiddenApiConfig.Count -eq 0) `
        'API package contains protected runtime configuration.'
    $stagedBuildId = (Get-Content `
        (Join-Path $stageFrontend '.next\BUILD_ID') -Raw).Trim()
    Assert-True ($stagedBuildId -eq $ExpectedBuildId) `
        'Staged frontend build ID differs from the release manifest.'
    $serviceWorker = Get-Content (Join-Path $stageFrontend 'public\sw.js') -Raw
    Assert-True ($serviceWorker -match [regex]::Escape($ExpectedCacheVersion)) `
        'Staged service-worker cache version differs from the release manifest.'

    Set-TestServerConfiguration
    $apiStartedAt = Get-Date
    try {
        Stop-Service RhemaERPAPI -Force
        (Get-Service RhemaERPAPI).WaitForStatus(
            'Stopped', [TimeSpan]::FromMinutes(2))
        Invoke-RobocopyChecked @(
            $stageApi, $ApiRoot, '/E', '/R:2', '/W:2', '/NFL', '/NDL',
            '/NJH', '/NJS', '/NP',
            '/XF', 'appsettings.json', 'appsettings.Production.json',
            'appsettings.Development.json', 'appsettings.AntiSpam.json',
            '.env', '.env.production',
            '/XD', (Join-Path $ApiRoot 'wwwroot\uploads'),
            (Join-Path $ApiRoot 'logs'), (Join-Path $ApiRoot 'secure-file-storage')
        )
        Start-Service RhemaERPAPI
        Wait-ApiReady $apiStartedAt
    }
    catch {
        Stop-Service RhemaERPAPI -Force -ErrorAction SilentlyContinue
        Invoke-RobocopyChecked @(
            (Join-Path $backup 'api'), $ApiRoot, '/E', '/R:2', '/W:2',
            '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
            '/XD', (Join-Path $ApiRoot 'wwwroot\uploads'),
            (Join-Path $ApiRoot 'logs'), (Join-Path $ApiRoot 'secure-file-storage')
        )
        $serviceBackup = Join-Path $backup 'services\api\RhemaERPAPI.xml'
        if (Test-Path -LiteralPath $serviceBackup) {
            Copy-Item -LiteralPath $serviceBackup -Destination $ApiServiceXml -Force
        }
        Start-Service RhemaERPAPI -ErrorAction SilentlyContinue
        throw "API apply failed and application files were rolled back: $($_.Exception.Message)"
    }

    try {
        Stop-Service RhemaERPFrontend -Force
        (Get-Service RhemaERPFrontend).WaitForStatus(
            'Stopped', [TimeSpan]::FromMinutes(2))
        if (Test-Path (Join-Path $FrontendRoot '.next')) {
            Move-Item (Join-Path $FrontendRoot '.next') (Join-Path $retired '.next')
        }
        if (Test-Path (Join-Path $FrontendRoot 'public')) {
            Move-Item (Join-Path $FrontendRoot 'public') (Join-Path $retired 'public')
        }
        Copy-Item (Join-Path $FrontendRoot 'server.js') `
            (Join-Path $retired 'server.js') -Force
        Copy-Item (Join-Path $FrontendRoot 'package.json') `
            (Join-Path $retired 'package.json') -Force
        Move-Item (Join-Path $stageFrontend '.next') (Join-Path $FrontendRoot '.next')
        Move-Item (Join-Path $stageFrontend 'public') (Join-Path $FrontendRoot 'public')
        Copy-Item (Join-Path $stageFrontend 'server.js') `
            (Join-Path $FrontendRoot 'server.js') -Force
        Copy-Item (Join-Path $stageFrontend 'package.json') `
            (Join-Path $FrontendRoot 'package.json') -Force
        Start-Service RhemaERPFrontend
        Wait-FrontendReady
    }
    catch {
        Stop-Service RhemaERPFrontend -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $failed -Force | Out-Null
        foreach ($name in @('.next', 'public')) {
            $livePath = Join-Path $FrontendRoot $name
            if (Test-Path $livePath) { Move-Item $livePath (Join-Path $failed $name) -Force }
            $oldPath = Join-Path $retired $name
            if (Test-Path $oldPath) { Move-Item $oldPath $livePath }
        }
        Copy-Item (Join-Path $retired 'server.js') `
            (Join-Path $FrontendRoot 'server.js') -Force
        Copy-Item (Join-Path $retired 'package.json') `
            (Join-Path $FrontendRoot 'package.json') -Force
        Start-Service RhemaERPFrontend -ErrorAction SilentlyContinue
        throw "Frontend apply failed and was rolled back: $($_.Exception.Message)"
    }

    $release = [ordered]@{
        deploymentId = $DeploymentId
        commit = $ExpectedCommit
        buildId = $ExpectedBuildId
        cacheVersion = $ExpectedCacheVersion
        deployedUtc = [DateTime]::UtcNow.ToString('o')
        apiSha256 = $ApiSha256
        frontendSha256 = $FrontendSha256
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $LogsRoot 'current-release.json'),
        ($release | ConvertTo-Json -Depth 4),
        (New-Object System.Text.UTF8Encoding($false)))
    Write-Output 'APPLY|PASS'
}

function Invoke-Verify {
    Write-ServiceState
    $notRunning = @(Get-Service RhemaERPAPI,RhemaERPFrontend,RhemaERPHTTPSIPProxy |
        Where-Object { $_.Status -ne 'Running' })
    Assert-True ($notRunning.Count -eq 0) 'One or more VPS services are not running.'

    foreach ($item in @(
            @{ Uri = 'http://127.0.0.1:5000/health'; Name = 'api-health' },
            @{ Uri = 'http://127.0.0.1:5000/health/ready'; Name = 'api-ready' },
            @{ Uri = 'http://127.0.0.1:5000/health/live'; Name = 'api-live' },
            @{ Uri = 'http://127.0.0.1:3001/login'; Name = 'frontend-login' })) {
        $response = Invoke-WebRequest $item.Uri -UseBasicParsing -TimeoutSec 15
        Write-Output "LOCAL_ROUTE|$($item.Name)|$($response.StatusCode)"
        Assert-True ($response.StatusCode -eq 200) `
            "Local route failed: $($item.Uri)"
    }

    $history = @(Get-MigrationHistory)
    foreach ($row in $history) { Write-Output "MIGRATION_ID|$($row.MigrationId)" }
    $summary = @(Get-DatabaseControlSummary)[0]
    Write-Output "MIGRATION_COUNT|$($summary.MigrationCount)"
    Write-Output "LATEST_MIGRATION|$($summary.LatestMigration)"
    Write-Output "UNTRUSTED_FKS|$($summary.UntrustedForeignKeys)"
    Write-Output "DISABLED_FKS|$($summary.DisabledForeignKeys)"
    Write-Output "SVG_FILE_UPLOAD_POLICIES|$($summary.SvgFileUploadPolicies)"
    Write-Output "SVG_SUPPLIER_REQUIREMENTS|$($summary.SvgSupplierRequirements)"
    Assert-True ([long]$summary.UntrustedForeignKeys -eq 0) `
        'One or more database foreign keys are untrusted.'
    Assert-True ([long]$summary.DisabledForeignKeys -eq 0) `
        'One or more database foreign keys are disabled.'
    Assert-True ([long]$summary.SvgFileUploadPolicies -eq 0) `
        'Active SVG entries exist in a shared file-upload policy.'
    Assert-True ([long]$summary.SvgSupplierRequirements -eq 0) `
        'Active SVG entries exist in a supplier evidence requirement.'

    $buildId = (Get-Content (Join-Path $FrontendRoot '.next\BUILD_ID') -Raw).Trim()
    Write-Output "DEPLOYED_BUILD_ID|$buildId"
    if (-not [string]::IsNullOrWhiteSpace($ExpectedBuildId)) {
        Assert-True ($buildId -eq $ExpectedBuildId) `
            'Deployed frontend build ID differs from the expected release.'
    }
    $serviceWorker = Get-Content (Join-Path $FrontendRoot 'public\sw.js') -Raw
    if (-not [string]::IsNullOrWhiteSpace($ExpectedCacheVersion)) {
        Assert-True ($serviceWorker -match [regex]::Escape($ExpectedCacheVersion)) `
            'Deployed service-worker version differs from the expected release.'
    }
    Assert-True (Test-Path (Join-Path $ApiRoot 'wwwroot\uploads')) `
        'Runtime uploads directory is missing.'
    Assert-True (Test-Path (Join-Path $ApiRoot 'secure-file-storage')) `
        'Secure DMS storage directory is missing.'

    Write-Output 'VERIFY|PASS'
}

switch ($Action) {
    'Preflight' { Invoke-Preflight }
    'Backup' { Invoke-Backup }
    'Apply' { Invoke-Apply }
    'Verify' { Invoke-Verify }
}
