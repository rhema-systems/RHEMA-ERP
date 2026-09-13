[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('InspectSource', 'RehearseEmpty', 'RehearseClone', 'RehearseFinalClone', 'ResetDisposableDevelopment', 'DropRehearsal')]
    [string]$Mode,

    [string]$TargetConnectionEnvironmentVariable = 'RHEMA_GL_REHEARSAL_CONNECTION',
    [string]$SourceConnectionEnvironmentVariable = 'RHEMA_GL_SOURCE_READONLY_CONNECTION',
    [string]$EvidenceDirectory,
    [switch]$ConfirmDrop,
    [switch]$ConfirmDisposableDevelopmentReset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiProject = Join-Path $repositoryRoot 'src\ErpSystem.Api\ErpSystem.Api.csproj'
$dataProject = Join-Path $repositoryRoot 'src\ErpSystem.Data\ErpSystem.Data.csproj'
$baselineMigration = '20260313114533_InitialBaseline'
$baselinePredecessor = '20260312013725_AddProjectResourceRoutingRequirements'
$supersededMigrations = @(
    '20260304155434_RecreateHRTables',
    '20260311184920_AddProjectMaterialCostLedger',
    '20260311213738_AddProjectDeliverableExternalReviews',
    '20260312013725_AddProjectResourceRoutingRequirements'
)
$approvedNamePattern = '^RHEMAERP_GL_REHEARSAL_[A-Z0-9_]{1,64}$'
$authoritativeMigrationCount = 456
$authoritativeLatestMigration = '20260908120000_AddProducerIntentGroupsC8'
$sqlcmdMaxVariableWidth = 8000
$sqlcmdScreenWidth = 8000
$finalCutoverFlags = @(
    'Finance__AccountingEvents__Enabled',
    'Finance__ProducerIntents__Enabled',
    'Finance__ProducerIntentGroups__Enabled'
)
$finalReviewedCommitVariable = 'RHEMA_GL_REVIEWED_COMMIT'
$finalReviewedTreeVariable = 'RHEMA_GL_REVIEWED_TREE'
$disposableConnectionVariable = 'RHEMA_GL_DISPOSABLE_DEVELOPMENT_CONNECTION'
$disposableAttestationVariable = 'RHEMA_GL_DISPOSABLE_DEVELOPMENT_RESET_ATTESTATION'
$disposableAttestationValue = 'I_ATTEST_RHEMAERP_DEVELOPMENT_DATA_IS_DISPOSABLE'
$script:sensitiveEvidenceTokens = [System.Collections.Generic.List[string]]::new()
$script:finalReviewedGitState = $null

function Register-SensitiveEvidenceToken([string]$value) {
    if (-not [string]::IsNullOrWhiteSpace($value) -and -not $script:sensitiveEvidenceTokens.Contains($value)) {
        $script:sensitiveEvidenceTokens.Add($value)
    }
}

function ConvertTo-SanitizedEvidenceLine([string]$value) {
    $line = [string]$value
    $line = $line.Replace($repositoryRoot, '<REPOSITORY>', [StringComparison]::OrdinalIgnoreCase)
    $line = [regex]::Replace($line, 'C:\\Users\\[^\\\r\n]+', '<USER_PROFILE>', 'IgnoreCase')
    $line = [regex]::Replace($line, '(?i)\b[A-Z]:\\[^;\r\n]+', '<LOCAL_PATH>')
    foreach ($token in $script:sensitiveEvidenceTokens) {
        $line = $line.Replace($token, '<LOCAL_SQL_SERVER>', [StringComparison]::OrdinalIgnoreCase)
    }
    $line = [regex]::Replace($line, '(?i)(Password|Pwd|User ID|UID|Data Source|Server|Integrated Security|Trusted_Connection)\s*=\s*[^;\r\n]+', '$1=<REDACTED>')
    $line = [regex]::Replace($line, '(?i)ClientConnectionId:[0-9a-f-]+', 'ClientConnectionId:<REDACTED>')
    return $line
}

function Get-SanitizedExceptionMessage([System.Exception]$exception) {
    ConvertTo-SanitizedEvidenceLine $exception.Message
}

function Assert-FinalReviewedGitState([string]$operation = 'RehearseFinalClone') {
    $reviewedCommit = [Environment]::GetEnvironmentVariable($finalReviewedCommitVariable, 'Process')
    $reviewedTree = [Environment]::GetEnvironmentVariable($finalReviewedTreeVariable, 'Process')
    if ($reviewedCommit -notmatch '^[0-9a-fA-F]{40}$' -or $reviewedTree -notmatch '^[0-9a-fA-F]{40}$') {
        throw "$operation requires exact 40-hex $finalReviewedCommitVariable and $finalReviewedTreeVariable process values from independent review."
    }

    Push-Location $repositoryRoot
    try {
        $executedCommit = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve executed HEAD.' }
        $executedTree = (& git rev-parse 'HEAD^{tree}').Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve executed HEAD tree.' }
        if (-not [string]::Equals($executedCommit, $reviewedCommit, [StringComparison]::OrdinalIgnoreCase) -or
            -not [string]::Equals($executedTree, $reviewedTree, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Executed HEAD/tree does not exactly match the independently reviewed commit/tree; descendants and other unreviewed states are forbidden.'
        }
        $dirty = @(& git status --porcelain=v1 --untracked-files=all)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to prove repository cleanliness.' }
        if ($dirty.Count -ne 0) {
            throw "$operation requires a completely clean tracked and untracked repository before any SQL contact."
        }
        $ignoredRelevant = @(& git ls-files --others --ignored --exclude-standard -- '*.cs' '*.csproj' '*.props' '*.targets' `
            '*.json' '*.config' '*.ps1' '*.psm1' '*.sql' '*.cshtml' '*.ts' '*.tsx' '*.js' '*.jsx' '*.user' '*.suo' '.env' '.env.*' | Where-Object {
            $normalized = $_.Replace('\','/')
            $isGeneratedPath = $normalized -match '(?i)(^|/)(bin|obj|out|publish|debug|debugpublic|release|releases|outputs|x64|x86|bld|log|artifacts|\.artifacts|node_modules|\.next|dist|coverage|testresults[^/]*|\.vs|\.idea|\.cache)/'
            -not $isGeneratedPath
        })
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect ignored files for relevant workspace changes.' }
        if ($ignoredRelevant.Count -ne 0) {
            throw "$operation found ignored code, migration, seeder, configuration, or script files outside the generated-output allowlist."
        }
    }
    finally { Pop-Location }

    [pscustomobject]@{
        reviewedCommit = $reviewedCommit.ToLowerInvariant()
        reviewedTree = $reviewedTree.ToLowerInvariant()
        executedCommit = $executedCommit.ToLowerInvariant()
        executedTree = $executedTree.ToLowerInvariant()
        repositoryClean = $true
    }
}

function Get-ProcessConnectionString([string]$variableName) {
    $value = [Environment]::GetEnvironmentVariable($variableName, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Process environment variable '$variableName' is required. User/machine fallbacks are deliberately forbidden."
    }
    return $value
}

function ConvertTo-ConnectionTarget([string]$connectionString, [bool]$requireRehearsalName) {
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionString)
    if ([string]::IsNullOrWhiteSpace($builder.DataSource)) { throw 'Connection string has no SQL Server target.' }
    if ([string]::IsNullOrWhiteSpace($builder.InitialCatalog)) { throw 'Connection string has no Database/Initial Catalog.' }
    if (-not [string]::IsNullOrWhiteSpace($builder.AttachDBFilename)) { throw 'AttachDbFilename is forbidden for GL rehearsals.' }
    if ($builder.UserInstance) { throw 'User Instance connections are forbidden for GL rehearsals.' }
    if ($builder.DataSource.IndexOfAny([char[]]"`r`n") -ge 0 -or $builder.InitialCatalog.IndexOfAny([char[]]"`r`n") -ge 0) {
        throw 'Connection target contains control characters.'
    }

    $database = $builder.InitialCatalog.ToUpperInvariant()
    if ($requireRehearsalName -and $database -notmatch $approvedNamePattern) {
        throw "Refusing database '$($builder.InitialCatalog)'. Rehearsal targets must match $approvedNamePattern."
    }

    [pscustomobject]@{
        Builder = $builder
        Server = $builder.DataSource
        Database = $builder.InitialCatalog
    }
}

function Write-TargetLog([string]$operation, $target) {
    Write-Host (ConvertTo-SanitizedEvidenceLine "[$operation] SQL Server: $($target.Server)")
    Write-Host "[$operation] Database:   $($target.Database)"
}

function Invoke-Native([string]$filePath, [string[]]$arguments) {
    & $filePath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Native command failed with exit code ${LASTEXITCODE}: $filePath"
    }
}

function Write-AtomicNativeCommandEvidence([string]$evidenceFile, [string[]]$sanitizedOutput,
    [string]$commandName, [int]$exitCode, [bool]$allowFailure) {
    $evidenceDirectory = Split-Path -Parent $evidenceFile
    if ([string]::IsNullOrWhiteSpace($evidenceDirectory) -or -not (Test-Path -LiteralPath $evidenceDirectory -PathType Container)) {
        throw "Native command evidence directory does not exist: $evidenceDirectory"
    }
    $status = if ($exitCode -eq 0) { 'SUCCESS' } elseif ($allowFailure) { 'ALLOWED_FAILURE' } else { 'FAILURE' }
    $safeCommandName = ConvertTo-SanitizedEvidenceLine ([System.IO.Path]::GetFileName($commandName))
    $marker = "RHEMA_NATIVE_COMMAND_EVIDENCE_V1|STATUS=$status|EXIT_CODE=$exitCode|COMMAND=$safeCommandName"
    $temporaryFile = Join-Path $evidenceDirectory ('.' + [System.IO.Path]::GetFileName($evidenceFile) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = [System.IO.File]::Open($temporaryFile, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
            try {
                foreach ($line in $sanitizedOutput) { $writer.WriteLine($line) }
                $writer.WriteLine($marker)
                $writer.Flush()
                $stream.Flush($true)
            }
            finally { $writer.Dispose() }
        }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
        [System.IO.File]::Move($temporaryFile, $evidenceFile, $false)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) { Remove-Item -LiteralPath $temporaryFile -Force }
    }
}

function Invoke-NativeWithEvidence([string]$filePath, [string[]]$arguments, [string]$evidenceFile,
    [switch]$AllowFailure) {
    $priorNativeErrorPreference = $PSNativeCommandUseErrorActionPreference
    try {
        $PSNativeCommandUseErrorActionPreference = $false
        $output = @(& $filePath @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $PSNativeCommandUseErrorActionPreference = $priorNativeErrorPreference
    }

    $sanitizedOutput = @($output | ForEach-Object { ConvertTo-SanitizedEvidenceLine ([string]$_) })
    Write-AtomicNativeCommandEvidence $evidenceFile $sanitizedOutput $filePath $exitCode ([bool]$AllowFailure)
    $sanitizedOutput | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Native command failed with exit code ${exitCode}: $filePath. Sanitized output: $evidenceFile"
    }
    return $exitCode
}

function Assert-SqlcmdOutputWidth([string]$outputFile) {
    if ([string]::IsNullOrWhiteSpace($outputFile) -or -not (Test-Path -LiteralPath $outputFile -PathType Leaf)) { return }
    $atRisk = Get-Content -LiteralPath $outputFile | Where-Object { $_.Length -ge $sqlcmdMaxVariableWidth } | Select-Object -First 1
    if ($null -ne $atRisk) {
        throw "sqlcmd evidence contains a line at the configured ${sqlcmdMaxVariableWidth}-character maximum; truncation cannot be excluded."
    }
}

function Invoke-Sql([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database,
    [string]$query, [string]$inputFile = '', [string]$outputFile = '') {
    # -I keeps QUOTED_IDENTIFIER enabled for the generated baseline's filtered and
    # computed-column indexes. sqlcmd otherwise defaults it off for input scripts.
    # sqlcmd defaults variable-length display to 256 characters and screen width to 80 characters.
    # Canonical SQL emits fixed-width SHA-256 row hashes, but all shared evidence transport still pins
    # both supported maxima. -W conflicts with -y and is deliberately absent.
    $arguments = @('-S', $builder.DataSource, '-d', $database, '-b', '-C', '-I', '-h', '-1',
        '-y', $sqlcmdMaxVariableWidth.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-w', $sqlcmdScreenWidth.ToString([Globalization.CultureInfo]::InvariantCulture))
    $oldPassword = [Environment]::GetEnvironmentVariable('SQLCMDPASSWORD', 'Process')
    try {
        if ($builder.IntegratedSecurity) {
            $arguments += '-E'
        }
        else {
            if ([string]::IsNullOrWhiteSpace($builder.UserID)) { throw 'SQL authentication requires User ID.' }
            [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $builder.Password, 'Process')
            $arguments += @('-U', $builder.UserID)
        }
        if (-not [string]::IsNullOrWhiteSpace($inputFile)) { $arguments += @('-i', $inputFile) }
        else { $arguments += @('-Q', $query) }
        if (-not [string]::IsNullOrWhiteSpace($outputFile)) { $arguments += @('-o', $outputFile) }
        Invoke-Native 'sqlcmd' $arguments
        Assert-SqlcmdOutputWidth $outputFile
    }
    finally {
        [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $oldPassword, 'Process')
    }
}

function Invoke-SqlScalar([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database,
    [string]$query) {
    $connectionBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($builder.ConnectionString)
    $connectionBuilder.set_InitialCatalog($database)
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionBuilder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 600
        $command.CommandText = $query
        return $command.ExecuteScalar()
    }
    finally { $connection.Dispose() }
}

function Get-SourceFingerprint([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database) {
    [string](Invoke-SqlScalar $builder $database @"
SELECT CONCAT(
    (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory), '|',
    (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory), '|',
    (SELECT COUNT_BIG(*) FROM dbo.FxRevaluationBatches WHERE IsDeleted=0), '|',
    (SELECT COUNT_BIG(*) FROM dbo.AccountCurrencyLinks WHERE IsDeleted=0 AND IsActive=1), '|',
    (SELECT COUNT_BIG(*) FROM dbo.JournalEntries WHERE IsDeleted=0));
"@)
}

function Invoke-SqlWithSanitizedEvidence([System.Data.SqlClient.SqlConnectionStringBuilder]$builder,
    [string]$database, [string]$query, [string]$inputFile, [string]$evidenceFile) {
    $rawOutput = [System.IO.Path]::GetTempFileName()
    try {
        $captured = @()
        try { $captured = @(Invoke-Sql $builder $database $query $inputFile $rawOutput 2>&1) }
        catch {
            $captured += $_.Exception.Message
            @((Get-Content -LiteralPath $rawOutput -ErrorAction SilentlyContinue), $captured) |
                ForEach-Object { ConvertTo-SanitizedEvidenceLine ([string]$_) } |
                Set-Content -Encoding utf8 -LiteralPath $evidenceFile
            throw "SQL command failed; only sanitized evidence was retained at $evidenceFile."
        }
        @((Get-Content -LiteralPath $rawOutput -ErrorAction SilentlyContinue), $captured) |
            ForEach-Object { ConvertTo-SanitizedEvidenceLine ([string]$_) } |
            Set-Content -Encoding utf8 -LiteralPath $evidenceFile
    }
    finally {
        if (Test-Path -LiteralPath $rawOutput -PathType Leaf) { Remove-Item -LiteralPath $rawOutput -Force }
    }
}

function Get-MigrationHistory([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database) {
    $connectionBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($builder.ConnectionString)
    $connectionBuilder.set_InitialCatalog($database)
    $connection = [System.Data.SqlClient.SqlConnection]::new($connectionBuilder.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 600
        $command.CommandText = @"
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51000, 'Migration history table dbo.__EFMigrationsHistory is missing.', 1;
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
"@
        $reader = $command.ExecuteReader()
        $history = @()
        while ($reader.Read()) { $history += $reader.GetString(0) }
        return $history
    }
    finally { $connection.Dispose() }
}

function Get-DiscoveredMigrationIds([string]$path) {
    $content = @(
        Get-Content -LiteralPath $path | ForEach-Object {
            if ($_.Trim() -match '^(?<id>\d{14}_[^\s]+)') { $Matches.id }
        }
    )
}

function Assert-FinalCutoverFlagsDisabled {
    foreach ($variableName in $finalCutoverFlags) {
        $value = [Environment]::GetEnvironmentVariable($variableName, 'Process')
        if (-not [string]::Equals($value, 'false', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Process environment variable '$variableName' must be explicitly set to false for final clone rehearsal. Configuration fallbacks are forbidden."
        }
    }
}

function Write-FinalCutoverFlagsEvidence([string]$directory) {
    [ordered]@{
        accountingEvents = $false
        producerIntents = $false
        producerIntentGroups = $false
        source = 'explicit process environment variables'
    } | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $directory 'feature-flags.json')
}

function Write-FinalSummary([string]$directory, [string]$status, $source, $target,
    [string[]]$pendingMigrations, [string]$sourceFingerprint, [hashtable]$extra = @{}) {
    $summary = [ordered]@{
        status = $status
        gitHead = $script:finalReviewedGitState.executedCommit
        gitTree = $script:finalReviewedGitState.executedTree
        reviewedCommit = $script:finalReviewedGitState.reviewedCommit
        reviewedTree = $script:finalReviewedGitState.reviewedTree
        repositoryClean = $script:finalReviewedGitState.repositoryClean
        sourceServer = '<REDACTED_SAME_SERVER>'
        targetServer = '<REDACTED_SAME_SERVER>'
        sameServer = $true
        sourceDatabase = $source.Database
        targetDatabase = $target.Database
        repositoryMigrationCount = $authoritativeMigrationCount
        latestMigration = $authoritativeLatestMigration
        pendingMigrationCount = $pendingMigrations.Count
        pendingMigrations = @($pendingMigrations)
        sourceFingerprint = $sourceFingerprint
        cutoverFlagsExplicitlyFalse = $true
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
    }
    foreach ($key in $extra.Keys) { $summary[$key] = $extra[$key] }
    $artifactSha256 = [ordered]@{}
    Get-ChildItem -LiteralPath $directory -File | Where-Object Name -notin @('summary.json','manifest.sha256') |
        Sort-Object Name | ForEach-Object { $artifactSha256[$_.Name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    $summary['artifactSha256'] = $artifactSha256
    $summary | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 -LiteralPath (Join-Path $directory 'summary.json')
}

function Get-ServerDefaultPath([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$property) {
    $allowed = @('InstanceDefaultBackupPath', 'InstanceDefaultDataPath', 'InstanceDefaultLogPath')
    if ($property -notin $allowed) { throw "Unsupported SQL Server path property '$property'." }
    $value = [string](Invoke-SqlScalar $builder 'master' "SELECT CAST(SERVERPROPERTY('$property') AS nvarchar(4000));")
    if ([string]::IsNullOrWhiteSpace($value)) { throw "SQL Server did not expose $property." }
    return $value.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Get-DatabaseLogicalFiles([System.Data.SqlClient.SqlConnectionStringBuilder]$builder, [string]$database) {
    $master = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($builder.ConnectionString)
    $master.set_InitialCatalog('master')
    $connection = [System.Data.SqlClient.SqlConnection]::new($master.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT name, type FROM sys.master_files WHERE database_id=DB_ID(@database) ORDER BY file_id;'
        [void]$command.Parameters.AddWithValue('@database', $database)
        $reader = $command.ExecuteReader()
        $files = @()
        while ($reader.Read()) {
            $files += [pscustomobject]@{ Name = $reader.GetString(0); Type = $reader.GetByte(1) }
        }
        return $files
    }
    finally { $connection.Dispose() }
}

function Get-CloneBackupPath($target) {
    $backupRoot = Get-ServerDefaultPath $target.Builder 'InstanceDefaultBackupPath'
    Join-Path $backupRoot "$($target.Database)_COPYONLY.bak"
}

function Assert-TargetAbsent($target) {
    $master = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($target.Builder.ConnectionString)
    $master.set_InitialCatalog('master')
    $connection = [System.Data.SqlClient.SqlConnection]::new($master.ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT COUNT_BIG(*) FROM sys.databases WHERE name=@database;'
        [void]$command.Parameters.AddWithValue('@database', $target.Database)
        $count = [long]$command.ExecuteScalar()
    }
    finally { $connection.Dispose() }
    if ($count -ne 0) {
        throw "Rehearsal target '$($target.Database)' already exists. This harness never overwrites or auto-drops a database."
    }
}

function Set-ApplicationConnection([string]$connectionString, [scriptblock]$action) {
    $oldConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')
    $oldEnvironment = [Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Process')
    try {
        [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $connectionString, 'Process')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
        & $action
    }
    finally {
        [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $oldConnection, 'Process')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', $oldEnvironment, 'Process')
    }
}

function Get-DefaultEvidenceDirectory([string]$database) {
    Join-Path $repositoryRoot ".artifacts\finance-gl-rehearsal\$database"
}

function Assert-LocalDisposableSqlServer([string]$dataSource) {
    $normalized = $dataSource.Trim()
    if ($normalized -notmatch '^(?i)(?:tcp:)?(?:localhost|127\.0\.0\.1|\.|\(local\)|\[::1\])(?:\\[A-Za-z0-9_]+)?(?:,\d{1,5})?$') {
        throw 'ResetDisposableDevelopment requires an explicitly local SQL Server data source.'
    }
}

function Assert-DisposableConnectionUnambiguous([System.Data.SqlClient.SqlConnectionStringBuilder]$builder) {
    if (-not [string]::IsNullOrWhiteSpace($builder.FailoverPartner)) {
        throw 'ResetDisposableDevelopment forbids Failover Partner routing.'
    }
    if ($builder.MultiSubnetFailover) {
        throw 'ResetDisposableDevelopment forbids MultiSubnetFailover routing.'
    }
    if ($builder.ApplicationIntent -ne [System.Data.SqlClient.ApplicationIntent]::ReadWrite) {
        throw 'ResetDisposableDevelopment requires ApplicationIntent=ReadWrite.'
    }
}

function Assert-DisposableRawConnectionUnambiguous([string]$connectionString) {
    if ($connectionString -match '(?i)(?:^|;)\s*(?:Network\s+Library|Network)\s*=') {
        throw 'ResetDisposableDevelopment forbids Network Library routing overrides.'
    }
    if ($connectionString -match '(?i)(?:^|;)\s*Application\s+Intent\s*=') {
        throw 'ResetDisposableDevelopment forbids Application Intent routing overrides.'
    }
}

function Get-DisposableBackupPath($databaseTarget) {
    $backupRoot = Get-ServerDefaultPath $databaseTarget.Builder 'InstanceDefaultBackupPath'
    Join-Path $backupRoot 'RhemaERP_DISPOSABLE_RESET_COPYONLY.bak'
}

function Get-TextSha256([string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    [Convert]::ToHexString($hash)
}

function New-AtomicBackupReservation([string]$path) {
    $stream = $null
    try {
        # FileMode.CreateNew is the OS-level create-if-absent boundary. SQL then writes the
        # already-reserved medium with NOINIT/NOSKIP and can never overwrite another file.
        $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        if ($stream.Length -ne 0) { throw 'Atomic backup reservation was unexpectedly nonempty.' }
        $stream.Flush($true)
    }
    finally { if ($null -ne $stream) { $stream.Dispose() } }
}

function Write-AtomicTextFile([string]$path, [string]$content, [bool]$replaceExisting = $true) {
    $directory = Split-Path -Parent $path
    $temporaryPath = Join-Path $directory ('.' + [System.IO.Path]::GetFileName($path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $stream = [System.IO.File]::Open($temporaryPath, [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
            try {
                $writer.Write($content)
                if (-not $content.EndsWith("`n", [StringComparison]::Ordinal)) { $writer.WriteLine() }
                $writer.Flush()
                $stream.Flush($true)
            }
            finally { $writer.Dispose() }
        }
        finally { if ($null -ne $stream) { $stream.Dispose() } }
        if ($replaceExisting) { [System.IO.File]::Move($temporaryPath, $path, $true) }
        else { [System.IO.File]::Move($temporaryPath, $path, $false) }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}

function Write-DisposablePhaseMarker([string]$directory, [int]$ordinal, [string]$phase,
    [hashtable]$details = @{}) {
    $expectedOrdinal = @(Get-ChildItem -LiteralPath $directory -File -Filter 'phase-*.json').Count + 1
    if ($ordinal -ne $expectedOrdinal) {
        throw "Disposable reset phase markers must be monotonic and contiguous; expected $expectedOrdinal, got $ordinal."
    }
    $payload = [ordered]@{ schema = 'RHEMA_DISPOSABLE_RESET_PHASE_V1'; ordinal = $ordinal; phase = $phase }
    foreach ($key in $details.Keys) { $payload[$key] = $details[$key] }
    $path = Join-Path $directory ("phase-{0:D2}.json" -f $ordinal)
    Write-AtomicTextFile $path ($payload | ConvertTo-Json -Depth 8) $false
}

function Write-DisposableResetStatus([string]$directory, [string]$status, [string]$phase,
    [bool]$backupCreated, [bool]$backupVerified, [bool]$resetStarted, [hashtable]$extra = @{}) {
    $payload = [ordered]@{
        mode = 'ResetDisposableDevelopment'
        status = $status
        phase = $phase
        database = 'RhemaERP'
        server = '<REDACTED_LOCAL_SERVER>'
        repositoryClean = $true
        reviewedCommit = $script:finalReviewedGitState.reviewedCommit
        reviewedTree = $script:finalReviewedGitState.reviewedTree
        backupCreated = $backupCreated
        backupVerified = $backupVerified
        resetStarted = $resetStarted
        automaticRetry = $false
        automaticCleanup = $false
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
    }
    foreach ($key in $extra.Keys) { $payload[$key] = $extra[$key] }
    $artifactSha256 = [ordered]@{}
    Get-ChildItem -LiteralPath $directory -File | Where-Object Name -notin @('reset-status.json','manifest.sha256') |
        Sort-Object Name | ForEach-Object { $artifactSha256[$_.Name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    $payload['artifactSha256'] = $artifactSha256
    Write-AtomicTextFile (Join-Path $directory 'reset-status.json') ($payload | ConvertTo-Json -Depth 8) $true
}

function Write-DisposableRecoveryInstructions([string]$directory, [bool]$backupVerified, [string]$phase) {
    @(
        '# Disposable RhemaERP reset recovery',
        '',
        "Status phase: $phase",
        "Verified backup available: $($backupVerified.ToString().ToLowerInvariant())",
        '',
        'Do not rerun this reset automatically.',
        'Keep this evidence directory and the exact RhemaERP_DISPOSABLE_RESET_COPYONLY.bak file.',
        'A DBA must independently verify the recorded SHA-256 and RESTORE VERIFYONLY evidence.',
        'If recovery is required, restore that exact backup under explicit DBA control; this harness never restores or drops after a partial reset.',
        'Before any new attempt, use a new empty external evidence directory and obtain review of a new exact commit/tree.'
    ) -join "`n"
    Write-AtomicTextFile (Join-Path $directory 'RECOVERY.md') $content $true
}

function Write-DisposableResetEvidenceManifest([string]$directory) {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-GlCutoverEvidencePackage.ps1') `
        -EvidenceDirectory $directory -PackageKind DisposableReset -WriteManifest
    if ($LASTEXITCODE -ne 0) { throw 'Disposable reset evidence package validation or manifest creation failed.' }
}

function Invoke-DisposableDevelopmentReset($databaseTarget, [string]$connectionString, [string]$evidenceDirectory) {
    $phase = 'OFFLINE_GATES'
    $backupCreated = $false
    $backupCompleted = $false
    $backupVerified = $false
    $resetStarted = $false
    $backupPath = ''
    $backupMediaId = ''
    $backupSha256 = ''
    $sourceFingerprint = ''
    $sourceHistory = @()
    try {
        $script:finalReviewedGitState | ConvertTo-Json | Set-Content -Encoding utf8 `
            -LiteralPath (Join-Path $evidenceDirectory 'reviewed-git-state.json')
        Write-FinalCutoverFlagsEvidence $evidenceDirectory
        Push-Location $repositoryRoot
        try {
            $null = Invoke-NativeWithEvidence 'git' @('diff', '--check') (Join-Path $evidenceDirectory 'git-diff-check.log')
            $null = Invoke-NativeWithEvidence 'git' @('rev-list', '--parents', 'HEAD') (Join-Path $evidenceDirectory 'commit-ancestry.txt')
            $null = Invoke-NativeWithEvidence 'git' @('rev-parse', 'HEAD', 'HEAD^{tree}') (Join-Path $evidenceDirectory 'git-head-tree.txt')
            $null = Invoke-NativeWithEvidence 'dotnet' @('build', $apiProject, '--configuration', 'Debug', '--nologo') `
                (Join-Path $evidenceDirectory 'reset-build.log')
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'has-pending-model-changes',
                '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
                '--context', 'ApplicationDbContext', '--no-build') (Join-Path $evidenceDirectory 'ef-no-pending-model.log')
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'list', '--project', $dataProject,
                '--startup-project', $apiProject, '--configuration', 'Debug', '--context', 'ApplicationDbContext',
                '--no-build', '--no-connect') (Join-Path $evidenceDirectory 'migration-discovery.log')
        }
        finally { Pop-Location }
        $repositoryMigrations = @(Get-DiscoveredMigrationIds (Join-Path $evidenceDirectory 'migration-discovery.log'))
        $repositoryLatest = if ($repositoryMigrations.Count) { $repositoryMigrations[-1] } else { '<none>' }
        if ($repositoryMigrations.Count -ne $authoritativeMigrationCount -or $repositoryLatest -ne $authoritativeLatestMigration) {
            throw "Disposable reset requires authoritative 456/C8 repository history; found $($repositoryMigrations.Count)/$repositoryLatest."
        }
        if (@($repositoryMigrations | Sort-Object -Unique).Count -ne $authoritativeMigrationCount -or
            (@($repositoryMigrations | Sort-Object) -join "`n") -cne ($repositoryMigrations -join "`n")) {
            throw 'Disposable reset requires the exact unique ordered authoritative 456 migration identity list before DROP.'
        }
        $repositoryMigrations | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectory 'repository-migration-history.txt')
        $repositoryHistoryHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'repository-migration-history.txt')).Hash
        Write-DisposablePhaseMarker $evidenceDirectory 1 'OFFLINE_GATES_COMPLETE' @{
            repositoryMigrationCount = $authoritativeMigrationCount
            latestMigration = $authoritativeLatestMigration
            repositoryHistorySha256 = $repositoryHistoryHash
        }

        $phase = 'SOURCE_CAPTURE'
        $sourceHistory = @(Get-MigrationHistory $databaseTarget.Builder 'RhemaERP')
        if (@($sourceHistory | Sort-Object -Unique).Count -ne $sourceHistory.Count -or
            (@($sourceHistory | Sort-Object) -join "`n") -cne ($sourceHistory -join "`n") -or
            @($sourceHistory | Where-Object { $_ -notmatch '^\d{14}_[A-Za-z0-9_]+$' }).Count -ne 0) {
            throw 'Disposable reset source migration history is not a unique ordered safe identity list.'
        }
        $sourceHistory | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectory 'source-migration-history.txt')
        $sourceFingerprint = Get-SourceFingerprint $databaseTarget.Builder 'RhemaERP'
        $sourceFingerprint | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectory 'source-fingerprint-before.txt')
        $serverInstanceIdentity = [string](Invoke-SqlScalar $databaseTarget.Builder 'master' @"
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));
"@)
        if ([string]::IsNullOrWhiteSpace($serverInstanceIdentity)) {
            throw 'Could not bind the disposable connection to one actual local SQL Server instance.'
        }
        Register-SensitiveEvidenceToken $serverInstanceIdentity
        $databaseIdentity = [string](Invoke-SqlScalar $databaseTarget.Builder 'master' @"
SELECT CONCAT(CONVERT(nvarchar(20),database_id),N'|',CONVERT(nvarchar(33),create_date,126))
FROM sys.databases
WHERE name COLLATE Latin1_General_100_BIN2 = N'RhemaERP' COLLATE Latin1_General_100_BIN2;
"@)
        if ($databaseIdentity -notmatch '^(?<id>\d+)\|(?<created>[^|]+)$') {
            throw 'Could not bind the exact disposable RhemaERP database identity.'
        }
        $expectedDatabaseId = [int]$Matches.id
        $expectedCreateDate = $Matches.created
        "RHEMAERP_DATABASE_IDENTITY_SHA256=$(Get-TextSha256 $databaseIdentity)" | Set-Content -Encoding ascii `
            -LiteralPath (Join-Path $evidenceDirectory 'source-database-identity.sha256')
        "LOCAL_SQL_INSTANCE_IDENTITY_SHA256=$(Get-TextSha256 $serverInstanceIdentity)" | Set-Content -Encoding ascii `
            -LiteralPath (Join-Path $evidenceDirectory 'source-server-identity.sha256')
        Write-DisposablePhaseMarker $evidenceDirectory 2 'SOURCE_CAPTURE_COMPLETE' @{
            sourceFingerprint = $sourceFingerprint
            sourceHistorySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'source-migration-history.txt')).Hash
            databaseIdentitySha256 = (Get-TextSha256 $databaseIdentity)
            serverIdentitySha256 = (Get-TextSha256 $serverInstanceIdentity)
        }

        $phase = 'BACKUP'
        $backupPath = Get-DisposableBackupPath $databaseTarget
        $escapedBackupPath = $backupPath.Replace("'", "''")
        $backupExists = [int](Invoke-SqlScalar $databaseTarget.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
SELECT COALESCE(MAX(FileExists),0) FROM @exists;
"@)
        if ($backupExists -ne 0) {
            throw 'The exact RhemaERP disposable-reset backup already exists; it will never be overwritten.'
        }
        $backupMediaId = [Guid]::NewGuid().ToString('N')
        New-AtomicBackupReservation $backupPath
        $backupCreateEvidence = Join-Path $evidenceDirectory 'backup-create.txt'
        try {
            Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'master' @"
SET NOCOUNT ON;
SELECT N'DATABASE=RhemaERP';
SELECT N'BACKUP_MEDIA_ID=$backupMediaId';
SELECT N'BACKUP_PATH_ATOMICALLY_RESERVED';
DECLARE @reserved table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @reserved EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
IF NOT EXISTS (SELECT 1 FROM @reserved WHERE FileExists=1 AND FileIsDirectory=0)
    THROW 51200, 'DISPOSABLE_RESET_BACKUP_RESERVATION_LOST', 1;
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_START';
BACKUP DATABASE [RhemaERP] TO DISK=N'$escapedBackupPath'
WITH COPY_ONLY, CHECKSUM, NOINIT, NOSKIP, MEDIANAME=N'$backupMediaId', NAME=N'RhemaERP disposable development reset';
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE';
"@ '' $backupCreateEvidence
        }
        finally {
            # The local file length is checked even when sqlcmd/marker handling fails. Once SQL has
            # written any backup bytes, recovery evidence must never fall back to backupCreated=false.
            if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
                $backupFileLength = (Get-Item -LiteralPath $backupPath).Length
                if ($backupFileLength -gt 0) {
                    $backupCreated = $true
                    if (-not (Test-Path -LiteralPath (Join-Path $evidenceDirectory 'phase-03.json') -PathType Leaf)) {
                        Write-DisposablePhaseMarker $evidenceDirectory 3 'BACKUP_CREATED' @{
                            backupMediaId = $backupMediaId
                            backupByteLength = $backupFileLength
                        }
                    }
                    Write-DisposableRecoveryInstructions $evidenceDirectory $false 'BACKUP_CREATED_VERIFY_PENDING'
                    Write-DisposableResetStatus $evidenceDirectory 'BACKUP_CREATED_VERIFY_PENDING' 'BACKUP_CREATED' $true $false $false @{
                        sourceFingerprint = $sourceFingerprint; repositoryMigrationCount = 456; latestMigration = $authoritativeLatestMigration
                        backupMediaId = $backupMediaId; backupPreserved = $true; backupCompleted = $false
                    }
                }
            }
        }
        $backupCreateLines = @(Get-Content -LiteralPath $backupCreateEvidence | ForEach-Object { $_.Trim() })
        foreach ($marker in @('BACKUP_PATH_ATOMICALLY_RESERVED','BACKUP_COPY_ONLY_CHECKSUM_COMPLETE')) {
            if ($backupCreateLines -cnotcontains $marker) {
                throw "Disposable reset backup creation evidence lacks required marker '$marker'; reset is forbidden."
            }
        }
        $backupCompleted = $true
        if (-not $backupCreated) { throw 'Disposable reset backup did not create a nonempty recovery artifact.' }
        $backupVerifyEvidence = Join-Path $evidenceDirectory 'backup-verify.txt'
        Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'master' @"
SET NOCOUNT ON;
SELECT N'DATABASE=RhemaERP';
SELECT N'BACKUP_MEDIA_ID=$backupMediaId';
RESTORE VERIFYONLY FROM DISK=N'$escapedBackupPath' WITH CHECKSUM;
SELECT N'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE';
"@ '' $backupVerifyEvidence
        $backupVerifyLines = @(Get-Content -LiteralPath $backupVerifyEvidence | ForEach-Object { $_.Trim() })
        if ($backupVerifyLines -cnotcontains 'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE') {
            throw 'Disposable reset backup VERIFYONLY evidence lacks its completion marker; reset is forbidden.'
        }
        if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
            throw 'Verified reset backup is not locally readable for SHA-256; reset is forbidden.'
        }
        $backupSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath).Hash
        if ($backupSha256 -notmatch '^[0-9A-F]{64}$') { throw 'Verified reset backup SHA-256 is invalid; reset is forbidden.' }
        "$backupSha256  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" | Set-Content -Encoding ascii `
            -LiteralPath (Join-Path $evidenceDirectory 'backup.sha256')
        $backupStillExists = [int](Invoke-SqlScalar $databaseTarget.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
SELECT COALESCE(MAX(FileExists),0) FROM @exists;
"@)
        if ($backupStillExists -ne 1) { throw 'Verified reset backup disappeared before mutation; reset is forbidden.' }
        $backupVerified = $true
        Write-DisposablePhaseMarker $evidenceDirectory 4 'BACKUP_VERIFIED' @{
            backupSha256 = $backupSha256
            backupVerifyEvidenceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupVerifyEvidence).Hash
        }
        Write-DisposableRecoveryInstructions $evidenceDirectory $true 'BACKUP_VERIFIED'
        Write-DisposableResetStatus $evidenceDirectory 'READY_TO_RESET' 'BACKUP_VERIFIED' $true $true $false @{
            sourceFingerprint = $sourceFingerprint; repositoryMigrationCount = 456; latestMigration = $authoritativeLatestMigration
            backupSha256 = $backupSha256; backupMediaId = $backupMediaId; backupCompleted = $true
        }

        $phase = 'IDENTITY_RECHECK_AND_RESET'
        $preMutationGitState = Assert-FinalReviewedGitState 'ResetDisposableDevelopment'
        if ($preMutationGitState.executedCommit -ne $script:finalReviewedGitState.executedCommit -or
            $preMutationGitState.executedTree -ne $script:finalReviewedGitState.executedTree) {
            throw 'Reviewed repository identity changed before disposable reset mutation.'
        }
        $preMutationGitState | ConvertTo-Json | Set-Content -Encoding utf8 `
            -LiteralPath (Join-Path $evidenceDirectory 'pre-mutation-reviewed-git-state.json')
        $backupHashEvidencePath = Join-Path $evidenceDirectory 'backup.sha256'
        if (-not $backupVerified -or -not (Test-Path -LiteralPath $backupHashEvidencePath -PathType Leaf) -or
            -not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
            throw 'Disposable reset cannot start without verified backup markers and SHA-256 evidence.'
        }
        $recordedBackupHash = (Get-Content -Raw -LiteralPath $backupHashEvidencePath).Trim()
        $currentBackupHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath).Hash
        if ($recordedBackupHash -cne "$backupSha256  RhemaERP_DISPOSABLE_RESET_COPYONLY.bak" -or
            $currentBackupHash -cne $backupSha256) {
            throw 'Disposable reset backup SHA-256 proof changed before mutation; reset is forbidden.'
        }
        $escapedCreateDate = $expectedCreateDate.Replace("'", "''")
        $escapedServerInstanceIdentity = $serverInstanceIdentity.Replace("'", "''")
        $sourceFingerprintParts = @($sourceFingerprint -split '\|')
        if ($sourceFingerprintParts.Count -ne 5 -or
            $sourceFingerprintParts[0] -notmatch '^\d+$' -or
            $sourceFingerprintParts[1] -notmatch '^\d{14}_[A-Za-z0-9_]+$' -or
            $sourceFingerprintParts[2] -notmatch '^\d+$' -or
            $sourceFingerprintParts[3] -notmatch '^\d+$' -or
            $sourceFingerprintParts[4] -notmatch '^\d+$') {
            throw 'Captured disposable source fingerprint is not safe to bind into the destructive boundary.'
        }
        $historyRows = @($sourceHistory | ForEach-Object { "(N'$($_)')" })
        $expectedHistorySql = if ($historyRows.Count -eq 0) { '' } else {
            "INSERT INTO #ExpectedHistory(MigrationId) VALUES " + ($historyRows -join ',') + ';'
        }
        $destructiveSql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource=N'RHEMAERP_DISPOSABLE_DEVELOPMENT_RESET', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=0;
IF @lockResult < 0 THROW 51203, 'DISPOSABLE_RESET_QUIESCENCE_LOCK_UNAVAILABLE', 1;
DECLARE @backupProof table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @backupProof EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
IF NOT EXISTS (SELECT 1 FROM @backupProof WHERE FileExists=1 AND FileIsDirectory=0)
    THROW 51202, 'DISPOSABLE_RESET_VERIFIED_BACKUP_MISSING', 1;
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) COLLATE Latin1_General_100_BIN2 <>
   N'$escapedServerInstanceIdentity' COLLATE Latin1_General_100_BIN2
    THROW 51204, 'DISPOSABLE_RESET_SERVER_IDENTITY_DRIFT', 1;
IF (SELECT COUNT_BIG(*) FROM sys.databases
    WHERE name COLLATE Latin1_General_100_BIN2 = N'RhemaERP' COLLATE Latin1_General_100_BIN2) <> 1 OR
   NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id=$expectedDatabaseId
      AND name COLLATE Latin1_General_100_BIN2 = N'RhemaERP' COLLATE Latin1_General_100_BIN2
      AND CONVERT(nvarchar(33),create_date,126)=N'$escapedCreateDate')
    THROW 51201, 'DISPOSABLE_RESET_IDENTITY_DRIFT', 1;
SELECT N'DISPOSABLE_RESET_IDENTITY_RECHECK_COMPLETE';
ALTER DATABASE [RhemaERP] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
SELECT N'DISPOSABLE_RESET_SOURCE_QUIESCED';
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE database_id=$expectedDatabaseId
      AND name COLLATE Latin1_General_100_BIN2 = N'RhemaERP' COLLATE Latin1_General_100_BIN2
      AND CONVERT(nvarchar(33),create_date,126)=N'$escapedCreateDate')
    THROW 51205, 'DISPOSABLE_RESET_POST_QUIESCENCE_IDENTITY_DRIFT', 1;
CREATE TABLE #ExpectedHistory(MigrationId nvarchar(150) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY);
$expectedHistorySql
IF EXISTS (
    SELECT MigrationId COLLATE Latin1_General_100_BIN2 FROM [RhemaERP].dbo.__EFMigrationsHistory
    EXCEPT SELECT MigrationId FROM #ExpectedHistory) OR EXISTS (
    SELECT MigrationId FROM #ExpectedHistory
    EXCEPT SELECT MigrationId COLLATE Latin1_General_100_BIN2 FROM [RhemaERP].dbo.__EFMigrationsHistory)
    THROW 51206, 'DISPOSABLE_RESET_FINAL_HISTORY_DRIFT', 1;
DECLARE @actualMigrationCount bigint=(SELECT COUNT_BIG(*) FROM [RhemaERP].dbo.__EFMigrationsHistory);
DECLARE @actualLatest nvarchar(150)=(SELECT MAX(MigrationId) FROM [RhemaERP].dbo.__EFMigrationsHistory);
DECLARE @actualFx bigint=(SELECT COUNT_BIG(*) FROM [RhemaERP].dbo.FxRevaluationBatches WHERE IsDeleted=0);
DECLARE @actualLinks bigint=(SELECT COUNT_BIG(*) FROM [RhemaERP].dbo.AccountCurrencyLinks WHERE IsDeleted=0 AND IsActive=1);
DECLARE @actualJournals bigint=(SELECT COUNT_BIG(*) FROM [RhemaERP].dbo.JournalEntries WHERE IsDeleted=0);
IF @actualMigrationCount <> $($sourceFingerprintParts[0]) OR
   @actualLatest COLLATE Latin1_General_100_BIN2 <> N'$($sourceFingerprintParts[1])' COLLATE Latin1_General_100_BIN2 OR
   @actualFx <> $($sourceFingerprintParts[2]) OR @actualLinks <> $($sourceFingerprintParts[3]) OR
   @actualJournals <> $($sourceFingerprintParts[4])
    THROW 51207, 'DISPOSABLE_RESET_FINAL_FINGERPRINT_DRIFT', 1;
SELECT N'DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE';
SELECT N'SOURCE_FINAL_FINGERPRINT=$sourceFingerprint';
DROP DATABASE [RhemaERP];
CREATE DATABASE [RhemaERP];
SELECT N'DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED';
"@
        $destructiveSqlPath = Join-Path ([System.IO.Path]::GetTempPath()) ("rhema-disposable-reset-$([Guid]::NewGuid().ToString('N')).sql")
        Write-AtomicTextFile $destructiveSqlPath $destructiveSql $false
        $resetStarted = $true
        $phase = 'RESET_STARTED'
        Write-DisposablePhaseMarker $evidenceDirectory 5 'RESET_STARTED' @{
            backupSha256 = $backupSha256
            repositoryHistorySha256 = $repositoryHistoryHash
            sourceFingerprint = $sourceFingerprint
        }
        Write-DisposableRecoveryInstructions $evidenceDirectory $true 'RESET_STARTED'
        Write-DisposableResetStatus $evidenceDirectory 'RESET_STARTED' 'RESET_STARTED' $true $true $true @{
            sourceFingerprint = $sourceFingerprint; repositoryMigrationCount = 456; latestMigration = $authoritativeLatestMigration
            backupSha256 = $backupSha256; backupMediaId = $backupMediaId; backupPreserved = $true; backupCompleted = $true
        }
        try {
            Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'master' '' $destructiveSqlPath `
                (Join-Path $evidenceDirectory 'reset-database.log')
        }
        finally {
            if (Test-Path -LiteralPath $destructiveSqlPath -PathType Leaf) { Remove-Item -LiteralPath $destructiveSqlPath -Force }
        }
        $resetDatabaseLines = @(Get-Content -LiteralPath (Join-Path $evidenceDirectory 'reset-database.log') | ForEach-Object { $_.Trim() })
        foreach ($marker in @('DISPOSABLE_RESET_SOURCE_QUIESCED','DISPOSABLE_RESET_FINAL_SOURCE_RECHECK_COMPLETE','DISPOSABLE_RESET_EMPTY_DATABASE_RECREATED')) {
            if ($resetDatabaseLines -cnotcontains $marker) { throw "Disposable reset destructive-boundary evidence lacks '$marker'." }
        }
        Write-DisposablePhaseMarker $evidenceDirectory 6 'DATABASE_RECREATED' @{
            destructiveBoundaryEvidenceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'reset-database.log')).Hash
        }

        $phase = 'APPLY_MIGRATIONS'
        Push-Location $repositoryRoot
        try {
            Set-ApplicationConnection $connectionString {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'apply-migrations') (Join-Path $evidenceDirectory 'reset-apply-migrations.log')
            }
        }
        finally { Pop-Location }
        $finalHistory = @(Get-MigrationHistory $databaseTarget.Builder 'RhemaERP')
        $finalHistory | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectory 'target-migration-history.txt')
        if (($finalHistory -join "`n") -ne ($repositoryMigrations -join "`n")) {
            throw 'Reset RhemaERP history is not exactly authoritative repository 456/C8 with zero orphans.'
        }
        Write-DisposablePhaseMarker $evidenceDirectory 7 'MIGRATIONS_APPLIED' @{
            finalMigrationCount = $finalHistory.Count
            targetHistorySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'target-migration-history.txt')).Hash
        }

        $phase = 'SEED_AND_INVARIANTS'
        Push-Location $repositoryRoot
        try {
            Set-ApplicationConnection $connectionString {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'seed-db') (Join-Path $evidenceDirectory 'reset-seed-pass-1.log')
            }
            Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'RhemaERP' '' `
                (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql') (Join-Path $evidenceDirectory 'reset-invariants-pass-1.txt')
            Set-ApplicationConnection $connectionString {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'seed-db') (Join-Path $evidenceDirectory 'reset-seed-pass-2.log')
            }
            Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'RhemaERP' '' `
                (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql') (Join-Path $evidenceDirectory 'reset-invariants-pass-2.txt')
        }
        finally { Pop-Location }
        $firstInvariant = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'reset-invariants-pass-1.txt')).Hash
        $secondInvariant = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'reset-invariants-pass-2.txt')).Hash
        if ($firstInvariant -ne $secondInvariant) { throw 'Second disposable reset seed changed canonical Finance invariants.' }
        @("$firstInvariant  reset-invariants-pass-1.txt","$secondInvariant  reset-invariants-pass-2.txt") |
            Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectory 'reset-invariants.sha256')
        Write-DisposablePhaseMarker $evidenceDirectory 8 'SEED_INVARIANTS_VERIFIED' @{
            seedPasses = 2
            invariantSha256 = $firstInvariant
        }

        $phase = 'DBCC'
        Invoke-SqlWithSanitizedEvidence $databaseTarget.Builder 'master' @"
DBCC CHECKDB(N'RhemaERP') WITH NO_INFOMSGS;
SELECT N'DBCC_CHECKDB_COMPLETE';
"@ '' (Join-Path $evidenceDirectory 'reset-dbcc.txt')
        if ((Get-Content -Raw -LiteralPath (Join-Path $evidenceDirectory 'reset-dbcc.txt')) -notmatch 'DBCC_CHECKDB_COMPLETE') {
            throw 'Disposable reset DBCC completion marker is missing.'
        }
        Write-DisposablePhaseMarker $evidenceDirectory 9 'DBCC_COMPLETE' @{
            dbccEvidenceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectory 'reset-dbcc.txt')).Hash
        }
        $finalGitState = Assert-FinalReviewedGitState 'ResetDisposableDevelopment'
        if ($finalGitState.executedCommit -ne $script:finalReviewedGitState.executedCommit -or
            $finalGitState.executedTree -ne $script:finalReviewedGitState.executedTree) {
            throw 'Reviewed repository identity changed during disposable reset.'
        }
        Write-DisposablePhaseMarker $evidenceDirectory 10 'COMPLETE' @{
            repositoryClean = $true
            finalMigrationCount = $authoritativeMigrationCount
        }
        Write-DisposableRecoveryInstructions $evidenceDirectory $true 'PASS'
        Write-DisposableResetStatus $evidenceDirectory 'PASS' 'COMPLETE' $true $true $true @{
            sourceFingerprint = $sourceFingerprint; repositoryMigrationCount = 456; finalMigrationCount = 456
            latestMigration = $authoritativeLatestMigration; orphanMigrationCount = 0; backupSha256 = $backupSha256
            invariantSha256 = $firstInvariant; seedPasses = 2; dbcc = 'PASS'; backupMediaId = $backupMediaId
            backupCompleted = $true
        }
        Write-DisposableResetEvidenceManifest $evidenceDirectory
        Write-Host "Disposable RhemaERP development reset passed. Evidence: $evidenceDirectory"
    }
    catch {
        $safeFailure = Get-SanitizedExceptionMessage $_.Exception
        Write-DisposableRecoveryInstructions $evidenceDirectory $backupVerified $phase
        Write-DisposableResetStatus $evidenceDirectory 'FAILED_NO_AUTOMATIC_RETRY' $phase $backupCreated $backupVerified $resetStarted @{
            sourceFingerprint = $sourceFingerprint; failure = $safeFailure; backupPreserved = $backupCreated
            backupMediaId = $backupMediaId; backupSha256 = $backupSha256; backupCompleted = $backupCompleted
        }
        try { Write-DisposableResetEvidenceManifest $evidenceDirectory }
        catch { Write-Warning (Get-SanitizedExceptionMessage $_.Exception) }
        Write-Error "Disposable RhemaERP reset stopped at $phase. Backup/evidence are preserved; no retry, restore, or cleanup was attempted. $safeFailure"
        throw $safeFailure
    }
}

if ($Mode -eq 'ResetDisposableDevelopment') {
    if (-not $ConfirmDisposableDevelopmentReset) {
        throw 'ResetDisposableDevelopment requires -ConfirmDisposableDevelopmentReset.'
    }
    $attestation = [Environment]::GetEnvironmentVariable($disposableAttestationVariable, 'Process')
    if ($attestation -cne $disposableAttestationValue) {
        throw "ResetDisposableDevelopment requires exact process attestation $disposableAttestationVariable=$disposableAttestationValue."
    }
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
        throw 'ResetDisposableDevelopment requires an explicit new empty external -EvidenceDirectory.'
    }
    $script:finalReviewedGitState = Assert-FinalReviewedGitState 'ResetDisposableDevelopment'
    Assert-FinalCutoverFlagsDisabled
    $evidenceFullPath = [System.IO.Path]::GetFullPath($EvidenceDirectory)
    $repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($evidenceFullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ResetDisposableDevelopment evidence must be external to the repository.'
    }
    if (Test-Path -LiteralPath $evidenceFullPath) {
        throw 'ResetDisposableDevelopment evidence directory must be new and absent.'
    }
    $connectionString = Get-ProcessConnectionString $disposableConnectionVariable
    Assert-DisposableRawConnectionUnambiguous $connectionString
    $databaseTarget = ConvertTo-ConnectionTarget $connectionString $false
    if (-not [string]::Equals($databaseTarget.Database, 'RhemaERP', [StringComparison]::Ordinal)) {
        throw 'ResetDisposableDevelopment permits only the exact case-sensitive local database RhemaERP.'
    }
    Assert-DisposableConnectionUnambiguous $databaseTarget.Builder
    Assert-LocalDisposableSqlServer $databaseTarget.Server
    Register-SensitiveEvidenceToken $databaseTarget.Server
    New-Item -ItemType Directory -Path $evidenceFullPath | Out-Null
    $evidenceFullPath = (Resolve-Path -LiteralPath $evidenceFullPath).Path
    Write-TargetLog 'DISPOSABLE DEVELOPMENT RESET' $databaseTarget
    Invoke-DisposableDevelopmentReset $databaseTarget $connectionString $evidenceFullPath
    return
}

if ($Mode -eq 'InspectSource') {
    $sourceConnection = Get-ProcessConnectionString $SourceConnectionEnvironmentVariable
    $source = ConvertTo-ConnectionTarget $sourceConnection $false
    Write-TargetLog 'READ-ONLY SOURCE INSPECTION' $source
    $evidence = if ($EvidenceDirectory) { $EvidenceDirectory } else { Get-DefaultEvidenceDirectory 'source-readiness' }
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    Invoke-Sql $source.Builder $source.Database '' (Join-Path $PSScriptRoot 'sql\gl-source-readiness.sql') `
        (Join-Path $evidence 'source-readiness.txt')
    Write-Host "Source readiness evidence: $evidence"
    return
}

if ($Mode -eq 'RehearseFinalClone' -and
    ($TargetConnectionEnvironmentVariable -ne 'RHEMA_GL_REHEARSAL_CONNECTION' -or
     $SourceConnectionEnvironmentVariable -ne 'RHEMA_GL_SOURCE_READONLY_CONNECTION')) {
    throw 'RehearseFinalClone requires the exact process variables RHEMA_GL_REHEARSAL_CONNECTION and RHEMA_GL_SOURCE_READONLY_CONNECTION; alternate variable names are forbidden.'
}
if ($Mode -eq 'RehearseFinalClone') {
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) {
        throw 'RehearseFinalClone requires an explicit new or empty -EvidenceDirectory.'
    }
    # This is deliberately before connection parsing, evidence creation, or any SQL access.
    $script:finalReviewedGitState = Assert-FinalReviewedGitState
}

$targetConnection = Get-ProcessConnectionString $TargetConnectionEnvironmentVariable
$target = ConvertTo-ConnectionTarget $targetConnection $true
Register-SensitiveEvidenceToken $target.Server
Write-TargetLog $Mode $target

if ($Mode -eq 'DropRehearsal') {
    if (-not $ConfirmDrop) { throw 'DropRehearsal requires -ConfirmDrop.' }
    $escapedName = $target.Database.Replace(']', ']]')
    Write-Host '[DROP] Prefix assertion passed; dropping the exact resolved rehearsal database.'
    Invoke-Sql $target.Builder 'master' "IF DB_ID(N'$($target.Database.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$escapedName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$escapedName]; END;"
    $backupPath = Get-CloneBackupPath $target
    $escapedBackupPath = $backupPath.Replace("'", "''")
    # Clone backups are named only from the already prefix-validated target. Deleting this exact
    # COPY_ONLY artifact makes cleanup reproducible without accepting an operator-supplied path.
    Invoke-Sql $target.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
IF EXISTS (SELECT 1 FROM @exists WHERE FileExists=1 AND FileIsDirectory=0)
    EXEC master.dbo.xp_delete_file 0, N'$escapedBackupPath', N'bak';
"@
    $remainingDatabase = [long](Invoke-SqlScalar $target.Builder 'master' "SELECT COUNT_BIG(*) FROM sys.databases WHERE name=N'$($target.Database.Replace("'", "''"))';")
    $remainingBackup = [int](Invoke-SqlScalar $target.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
SELECT COALESCE(MAX(FileExists),0) FROM @exists;
"@)
    if ($remainingDatabase -ne 0 -or $remainingBackup -ne 0) {
        throw 'Guarded cleanup did not remove both the exact database and target-derived backup.'
    }
    $cleanupEvidence = if ($EvidenceDirectory) { $EvidenceDirectory } else { Get-DefaultEvidenceDirectory $target.Database }
    New-Item -ItemType Directory -Force -Path $cleanupEvidence | Out-Null
    [ordered]@{
        status = 'CLEAN'
        database = $target.Database
        databaseExists = $false
        targetDerivedBackupExists = $false
        completedAtUtc = [DateTime]::UtcNow.ToString('O')
    } | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $cleanupEvidence 'cleanup.json')
    return
}

$cloneSource = $null
if ($Mode -in @('RehearseClone', 'RehearseFinalClone')) {
    if ($Mode -eq 'RehearseFinalClone') {
        Assert-FinalCutoverFlagsDisabled
    }
    $sourceConnection = Get-ProcessConnectionString $SourceConnectionEnvironmentVariable
    $cloneSource = ConvertTo-ConnectionTarget $sourceConnection $false
    Register-SensitiveEvidenceToken $cloneSource.Server
    Write-TargetLog 'READ-ONLY SOURCE + COPY_ONLY BACKUP' $cloneSource
    if ($cloneSource.Database.ToUpperInvariant() -match $approvedNamePattern) {
        throw 'Clone source must be the retained development database, not another rehearsal database.'
    }
    if (-not [string]::Equals($cloneSource.Database, 'RhemaERP', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Guarded clone source must be the exact configured RhemaERP catalog. Resolved '$($cloneSource.Database)'."
    }
    if (-not [string]::Equals($cloneSource.Server, $target.Server, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Clone source and rehearsal target must resolve to the same SQL Server instance.'
    }
    if ([string]::Equals($cloneSource.Database, $target.Database, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Clone source and rehearsal target must be different databases.'
    }
}

$evidenceDirectoryResolved = if ($EvidenceDirectory) { $EvidenceDirectory } else { Get-DefaultEvidenceDirectory $target.Database }
if ($Mode -eq 'RehearseFinalClone') {
    $evidenceFullPath = [System.IO.Path]::GetFullPath($evidenceDirectoryResolved)
    $repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $approvedInternalEvidencePrefix = (Join-Path $repositoryRoot '.artifacts\finance-gl-rehearsal').TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($evidenceFullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not $evidenceFullPath.StartsWith($approvedInternalEvidencePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Final-clone evidence inside the repository is allowed only below .artifacts/finance-gl-rehearsal; use an external directory otherwise.'
    }
}
if (Test-Path -LiteralPath $evidenceDirectoryResolved) {
    if (Get-ChildItem -LiteralPath $evidenceDirectoryResolved -Force | Select-Object -First 1) {
        throw "Evidence directory must be new or empty to prevent stale evidence mixing: $evidenceDirectoryResolved"
    }
}
New-Item -ItemType Directory -Force -Path $evidenceDirectoryResolved | Out-Null
$evidenceDirectoryResolved = (Resolve-Path $evidenceDirectoryResolved).Path
$escapedDatabase = $target.Database.Replace(']', ']]')
if ($Mode -ne 'RehearseFinalClone') { Assert-TargetAbsent $target }

if ($Mode -eq 'RehearseFinalClone') {
    $source = $cloneSource
    $pendingMigrations = @()
    $sourceFingerprintBefore = ''
    try {
        $script:finalReviewedGitState | ConvertTo-Json | Set-Content -Encoding utf8 `
            -LiteralPath (Join-Path $evidenceDirectoryResolved 'reviewed-git-state.json')
        Write-FinalCutoverFlagsEvidence $evidenceDirectoryResolved
        Push-Location $repositoryRoot
        try {
            $null = Invoke-NativeWithEvidence 'git' @('diff', '--check') (Join-Path $evidenceDirectoryResolved 'git-diff-check.log')
            $null = Invoke-NativeWithEvidence 'git' @('rev-list', '--parents', 'HEAD') `
                (Join-Path $evidenceDirectoryResolved 'commit-ancestry.txt')
            $null = Invoke-NativeWithEvidence 'git' @('rev-parse', 'HEAD', 'HEAD^{tree}') (Join-Path $evidenceDirectoryResolved 'git-head-tree.txt')
            $cloneBuildLog = Join-Path $evidenceDirectoryResolved 'clone-build.log'
            $null = Invoke-NativeWithEvidence 'dotnet' @('build', $apiProject, '--configuration', 'Debug', '--nologo') $cloneBuildLog
            $modelLog = Join-Path $evidenceDirectoryResolved 'ef-no-pending-model.log'
            $migrationListLog = Join-Path $evidenceDirectoryResolved 'migration-discovery.log'
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'has-pending-model-changes',
                '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
                '--context', 'ApplicationDbContext', '--no-build') $modelLog
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'list',
                '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
                '--context', 'ApplicationDbContext', '--no-build', '--no-connect') $migrationListLog
        }
        finally { Pop-Location }

        $repositoryMigrations = @(Get-DiscoveredMigrationIds (Join-Path $evidenceDirectoryResolved 'migration-discovery.log'))
        $repositoryLatestMigration = if ($repositoryMigrations.Count -gt 0) { $repositoryMigrations[-1] } else { '<none>' }
        if ($repositoryMigrations.Count -ne $authoritativeMigrationCount -or
            $repositoryLatestMigration -ne $authoritativeLatestMigration) {
            throw "Final clone assembly mismatch. Expected $authoritativeMigrationCount migrations ending at $authoritativeLatestMigration; discovered $($repositoryMigrations.Count) ending at $repositoryLatestMigration."
        }

        # Recheck the exact reviewed state immediately before the first SQL contact so a concurrent
        # repository change cannot pass on the strength of the earlier process-entry check.
        $finalPreSqlGitState = Assert-FinalReviewedGitState
        if ($finalPreSqlGitState.executedCommit -ne $script:finalReviewedGitState.executedCommit -or
            $finalPreSqlGitState.executedTree -ne $script:finalReviewedGitState.executedTree) {
            throw 'Reviewed repository identity changed before SQL contact.'
        }
        # Only after every repository-only gate passes may the harness contact SQL Server.
        Assert-TargetAbsent $target
        $sourceHistory = @(Get-MigrationHistory $source.Builder $source.Database)
        $sourceHistory | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-migration-history.txt')
        $pendingMigrations = @($repositoryMigrations | Where-Object { $_ -notin $sourceHistory })
        $orphanHistory = @($sourceHistory | Where-Object { $_ -notin $repositoryMigrations })
        $pendingMigrations | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'pending-migrations.txt')
        $orphanHistory | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'orphan-history.txt')
        $sourceFingerprintBefore = Get-SourceFingerprint $source.Builder $source.Database
        $sourceFingerprintBefore | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-before.txt')
        $idempotentScript = Join-Path $evidenceDirectoryResolved 'pending-migrations-idempotent.sql'
        if ($pendingMigrations.Count -eq 0) {
            '-- NO PENDING MIGRATIONS AT FRESH DISCOVERY' | Set-Content -Encoding ascii -LiteralPath $idempotentScript
            'RHEMA_IDEMPOTENT_SCRIPT_GENERATION_V1|STATUS=NOT_REQUIRED|REASON=ZERO_PENDING_MIGRATIONS|PENDING_COUNT=0' | Set-Content -Encoding utf8 `
                -LiteralPath (Join-Path $evidenceDirectoryResolved 'idempotent-script-generation.log')
        }
        else {
            $firstPendingIndex = [Array]::IndexOf($repositoryMigrations, $pendingMigrations[0])
            if ($firstPendingIndex -le 0) {
                throw 'Fresh discovery requires migration-zero script generation, which is not an authorized existing-database cutover path.'
            }
            $fromMigration = $repositoryMigrations[$firstPendingIndex - 1]
            Push-Location $repositoryRoot
            try {
                $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'script', $fromMigration,
                    $authoritativeLatestMigration, '--idempotent', '--project', $dataProject,
                    '--startup-project', $apiProject, '--configuration', 'Debug', '--context',
                    'ApplicationDbContext', '--no-build', '--output', $idempotentScript) `
                    (Join-Path $evidenceDirectoryResolved 'idempotent-script-generation.log')
            }
            finally { Pop-Location }
        }
        $idempotentSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $idempotentScript).Hash
        "$idempotentSha256  pending-migrations-idempotent.sql" | Set-Content -Encoding ascii `
            -LiteralPath (Join-Path $evidenceDirectoryResolved 'pending-migrations-idempotent.sha256')
        Invoke-SqlWithSanitizedEvidence $source.Builder $source.Database '' (Join-Path $PSScriptRoot 'sql\gl-source-readiness.sql') `
            (Join-Path $evidenceDirectoryResolved 'source-readiness.txt')

        $readinessText = Get-Content -Raw -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-readiness.txt')
        if ($readinessText -match '(?im)\b(BLOCKER|REVIEW)\b') {
            $sourceFingerprintAfterPreflight = Get-SourceFingerprint $source.Builder $source.Database
            $sourceFingerprintAfterPreflight | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-after.txt')
            if ($sourceFingerprintAfterPreflight -ne $sourceFingerprintBefore) {
                throw 'Configured source fingerprint changed during final clone preflight.'
            }
            Write-FinalSummary $evidenceDirectoryResolved 'NO_GO_PREFLIGHT' $source $target $pendingMigrations `
                $sourceFingerprintAfterPreflight @{ targetCreated = $false; backupCreated = $false }
            throw 'Final clone preflight returned blocker or review-required evidence. No backup or rehearsal database was created; deployment remains NO-GO.'
        }

        $sourceHistoryBeforeBackup = @(Get-MigrationHistory $source.Builder $source.Database)
        if (($sourceHistoryBeforeBackup -join "`n") -ne ($sourceHistory -join "`n")) {
            throw 'Source migration history changed after fresh pending-delta discovery; no backup was attempted.'
        }

        $backupPath = Get-CloneBackupPath $target
        $escapedBackupPath = $backupPath.Replace("'", "''")
        $backupExists = [int](Invoke-SqlScalar $target.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
SELECT COALESCE(MAX(FileExists),0) FROM @exists;
"@)
        if ($backupExists -ne 0) {
            throw "The exact target-derived rehearsal backup already exists. This harness never overwrites it: $backupPath"
        }

        $logicalFiles = @(Get-DatabaseLogicalFiles $source.Builder $source.Database)
        $dataFiles = @($logicalFiles | Where-Object Type -eq 0)
        $logFiles = @($logicalFiles | Where-Object Type -eq 1)
        if ($dataFiles.Count -ne 1 -or $logFiles.Count -ne 1) {
            throw 'Guarded final clone requires exactly one ROWS file and one LOG file; no backup or restore was attempted.'
        }
        $dataRoot = Get-ServerDefaultPath $target.Builder 'InstanceDefaultDataPath'
        $logRoot = Get-ServerDefaultPath $target.Builder 'InstanceDefaultLogPath'
        $dataPath = (Join-Path $dataRoot "$($target.Database).mdf").Replace("'", "''")
        $logPath = (Join-Path $logRoot "$($target.Database)_log.ldf").Replace("'", "''")
        $sourceDatabase = $source.Database.Replace(']', ']]')
        $sourceDataLogical = $dataFiles[0].Name.Replace("'", "''")
        $sourceLogLogical = $logFiles[0].Name.Replace("'", "''")
        # CreateNew is the atomic no-overwrite boundary after the SQL-side advisory preflight.
        # The unique media identity and NOINIT/NOSKIP then bind SQL to this invocation's reservation.
        $backupMediaId = [Guid]::NewGuid().ToString('N')
        New-AtomicBackupReservation $backupPath
        $backupRestoreEvidence = Join-Path $evidenceDirectoryResolved 'backup-restore-checkdb.txt'
        Invoke-SqlWithSanitizedEvidence $source.Builder 'master' @"
SET NOCOUNT ON;
SELECT N'SOURCE_DATABASE=RhemaERP';
SELECT N'TARGET_DATABASE=$($target.Database)';
SELECT N'BACKUP_MEDIA_ID=$backupMediaId';
SELECT N'BACKUP_PATH_ATOMICALLY_RESERVED';
DECLARE @backupExists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @backupExists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
IF NOT EXISTS (SELECT 1 FROM @backupExists WHERE FileExists=1 AND FileIsDirectory=0)
    THROW 51000, 'The exact atomically reserved rehearsal backup disappeared before SQL backup.', 1;
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_START';
BACKUP DATABASE [$sourceDatabase] TO DISK=N'$escapedBackupPath'
WITH COPY_ONLY, CHECKSUM, NOINIT, NOSKIP, MEDIANAME=N'$backupMediaId', NAME=N'GL cutover guarded final clone';
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE';
RESTORE VERIFYONLY FROM DISK=N'$escapedBackupPath' WITH CHECKSUM;
SELECT N'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE';
RESTORE DATABASE [$escapedDatabase] FROM DISK=N'$escapedBackupPath'
WITH MOVE N'$sourceDataLogical' TO N'$dataPath',
     MOVE N'$sourceLogLogical' TO N'$logPath', CHECKSUM, RECOVERY;
SELECT N'RESTORE_TARGET_COMPLETE';
DBCC CHECKDB(N'$($target.Database.Replace("'", "''"))') WITH PHYSICAL_ONLY, NO_INFOMSGS;
SELECT N'DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE';
"@ '' $backupRestoreEvidence
        foreach ($marker in @('BACKUP_COPY_ONLY_CHECKSUM_COMPLETE', 'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE',
            'RESTORE_TARGET_COMPLETE', 'DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE')) {
            if ((Get-Content -Raw -LiteralPath $backupRestoreEvidence) -notmatch [regex]::Escape($marker)) {
                throw "Backup/restore evidence did not contain required marker '$marker'."
            }
        }
        $restoredHistory = @(Get-MigrationHistory $target.Builder $target.Database)
        if (($restoredHistory -join "`n") -ne ($sourceHistory -join "`n")) {
            throw 'Restored clone migration history differs from the freshly discovered source history; refusing migration application.'
        }
        if (-not (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
            throw 'The SQL Server backup path is not locally readable, so its SHA-256 cannot be recorded. The target and backup are preserved; deployment remains NO-GO.'
        }
        $backupSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath).Hash
        "$backupSha256  $($target.Database)_COPYONLY.bak" | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'backup.sha256')

        Push-Location $repositoryRoot
        try {
            $applyLog = Join-Path $evidenceDirectoryResolved 'clone-apply-migrations.log'
            Set-ApplicationConnection $targetConnection {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'apply-migrations') $applyLog
            }
        }
        finally { Pop-Location }

        $finalHistory = @(Get-MigrationHistory $target.Builder $target.Database)
        $finalHistory | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'target-migration-history.txt')
        $missingAfterApply = @($repositoryMigrations | Where-Object { $_ -notin $finalHistory })
        $missingOriginalHistory = @($sourceHistory | Where-Object { $_ -notin $finalHistory })
        $unexpectedAfterApply = @($finalHistory | Where-Object { $_ -notin $sourceHistory -and $_ -notin $pendingMigrations })
        if ($missingAfterApply.Count -ne 0 -or $missingOriginalHistory.Count -ne 0 -or $unexpectedAfterApply.Count -ne 0) {
            throw "Exact pending migration delta did not complete cleanly. MissingRepository=$($missingAfterApply.Count); MissingOriginal=$($missingOriginalHistory.Count); Unexpected=$($unexpectedAfterApply.Count)."
        }

        Push-Location $repositoryRoot
        try {
            Set-ApplicationConnection $targetConnection {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'seed-db') (Join-Path $evidenceDirectoryResolved 'seed-pass-1.log')
            }
            Invoke-SqlWithSanitizedEvidence $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql') `
                (Join-Path $evidenceDirectoryResolved 'invariants-pass-1.txt')
            Set-ApplicationConnection $targetConnection {
                $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'seed-db') (Join-Path $evidenceDirectoryResolved 'seed-pass-2.log')
            }
            Invoke-SqlWithSanitizedEvidence $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-final-clone-invariants.sql') `
                (Join-Path $evidenceDirectoryResolved 'invariants-pass-2.txt')
        }
        finally { Pop-Location }
        $first = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectoryResolved 'invariants-pass-1.txt')).Hash
        $second = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectoryResolved 'invariants-pass-2.txt')).Hash
        if ($first -ne $second) { throw 'Second final-clone seed changed the canonical Finance invariant snapshot.' }
        @("$first  invariants-pass-1.txt", "$second  invariants-pass-2.txt") |
            Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'checksums.sha256')

        $sourceFingerprintAfter = Get-SourceFingerprint $source.Builder $source.Database
        $sourceFingerprintAfter | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-after.txt')
        if ($sourceFingerprintAfter -ne $sourceFingerprintBefore) {
            throw 'Configured source fingerprint changed during guarded final clone rehearsal.'
        }
        $finalPostRunGitState = Assert-FinalReviewedGitState
        if ($finalPostRunGitState.executedCommit -ne $script:finalReviewedGitState.executedCommit -or
            $finalPostRunGitState.executedTree -ne $script:finalReviewedGitState.executedTree) {
            throw 'Reviewed repository identity changed during final clone rehearsal.'
        }
        Write-FinalSummary $evidenceDirectoryResolved 'PASS' $source $target $pendingMigrations $sourceFingerprintAfter @{
            targetCreated = $true
            backupCreated = $true
            backupSha256 = $backupSha256
            backupMediaId = $backupMediaId
            backupReservation = 'FILEMODE_CREATE_NEW'
            backupRestoreEvidenceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupRestoreEvidence).Hash
            targetMigrationHistorySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $evidenceDirectoryResolved 'target-migration-history.txt')).Hash
            pendingMigrationScriptSha256 = $idempotentSha256
            invariantPass1Sha256 = $first
            invariantPass2Sha256 = $second
            backupPolicy = 'COPY_ONLY_CHECKSUM_VERIFYONLY'
            seedPasses = 2
        }
        Write-Host "Final GL cutover clone rehearsal passed. Evidence: $evidenceDirectoryResolved"
        return
    }
    catch {
        if (-not (Test-Path -LiteralPath (Join-Path $evidenceDirectoryResolved 'summary.json'))) {
            try {
                if ($sourceFingerprintBefore) {
                    $sourceFingerprintAfterFailure = Get-SourceFingerprint $source.Builder $source.Database
                    $sourceFingerprintAfterFailure | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-after.txt')
                    Write-FinalSummary $evidenceDirectoryResolved 'NO_GO' $source $target $pendingMigrations `
                        $sourceFingerprintAfterFailure @{ failure = (Get-SanitizedExceptionMessage $_.Exception) }
                }
            }
            catch { }
        }
        $sanitizedFailure = Get-SanitizedExceptionMessage $_.Exception
        Write-Error "Final clone rehearsal did not pass. Nothing is dropped or overwritten automatically; any exact target/backup is preserved for inspection. $sanitizedFailure"
        throw $sanitizedFailure
    }
}

if ($Mode -eq 'RehearseClone') {
    $source = $cloneSource

    Push-Location $repositoryRoot
    try {
        $cloneBuildLog = Join-Path $evidenceDirectoryResolved 'clone-build.log'
        $null = Invoke-NativeWithEvidence 'dotnet' @('build', $apiProject, '--configuration', 'Debug', '--nologo') $cloneBuildLog
    }
    finally { Pop-Location }

    $sourceFingerprintBefore = Get-SourceFingerprint $source.Builder $source.Database
    $sourceFingerprintBefore | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-before.txt')
    $backupPath = Get-CloneBackupPath $target
    $escapedBackupPath = $backupPath.Replace("'", "''")
    $backupExists = [int](Invoke-SqlScalar $target.Builder 'master' @"
DECLARE @exists table(FileExists int, FileIsDirectory int, ParentDirectoryExists int);
INSERT @exists EXEC master.dbo.xp_fileexist N'$escapedBackupPath';
SELECT COALESCE(MAX(FileExists),0) FROM @exists;
"@)
    if ($backupExists -ne 0) {
        throw "The exact target-derived rehearsal backup already exists. Run guarded DropRehearsal first: $backupPath"
    }

    $logicalFiles = @(Get-DatabaseLogicalFiles $source.Builder $source.Database)
    $dataFiles = @($logicalFiles | Where-Object Type -eq 0)
    $logFiles = @($logicalFiles | Where-Object Type -eq 1)
    if ($dataFiles.Count -ne 1 -or $logFiles.Count -ne 1) {
        throw 'Guarded clone currently requires exactly one ROWS file and one LOG file; no backup or restore was attempted.'
    }
    $dataRoot = Get-ServerDefaultPath $target.Builder 'InstanceDefaultDataPath'
    $logRoot = Get-ServerDefaultPath $target.Builder 'InstanceDefaultLogPath'
    $dataPath = (Join-Path $dataRoot "$($target.Database).mdf").Replace("'", "''")
    $logPath = (Join-Path $logRoot "$($target.Database)_log.ldf").Replace("'", "''")
    $sourceDatabase = $source.Database.Replace(']', ']]')
    $sourceDataLogical = $dataFiles[0].Name.Replace("'", "''")
    $sourceLogLogical = $logFiles[0].Name.Replace("'", "''")

    try {
        $backupRestoreEvidence = Join-Path $evidenceDirectoryResolved 'backup-restore-checkdb.txt'
        Invoke-Sql $source.Builder 'master' @"
SET NOCOUNT ON;
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_START';
BACKUP DATABASE [$sourceDatabase] TO DISK=N'$escapedBackupPath'
WITH COPY_ONLY, CHECKSUM, INIT, NAME=N'GL cutover guarded representative clone';
SELECT N'BACKUP_COPY_ONLY_CHECKSUM_COMPLETE';
RESTORE VERIFYONLY FROM DISK=N'$escapedBackupPath' WITH CHECKSUM;
SELECT N'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE';
RESTORE DATABASE [$escapedDatabase] FROM DISK=N'$escapedBackupPath'
WITH MOVE N'$sourceDataLogical' TO N'$dataPath',
     MOVE N'$sourceLogLogical' TO N'$logPath', CHECKSUM, RECOVERY;
SELECT N'RESTORE_TARGET_COMPLETE';
DBCC CHECKDB(N'$($target.Database.Replace("'", "''"))') WITH PHYSICAL_ONLY, NO_INFOMSGS;
SELECT N'DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE';
"@ '' $backupRestoreEvidence
        $backupRestoreText = Get-Content -Raw -LiteralPath $backupRestoreEvidence
        foreach ($marker in @('BACKUP_COPY_ONLY_CHECKSUM_COMPLETE', 'RESTORE_VERIFYONLY_CHECKSUM_COMPLETE',
            'RESTORE_TARGET_COMPLETE', 'DBCC_CHECKDB_PHYSICAL_ONLY_COMPLETE')) {
            if ($backupRestoreText -notmatch [regex]::Escape($marker)) {
                throw "Backup/restore evidence did not contain required marker '$marker'."
            }
        }

        Push-Location $repositoryRoot
        try {
            $applyLog = Join-Path $evidenceDirectoryResolved 'clone-apply-migrations.log'
            $exitCode = Set-ApplicationConnection $targetConnection {
                Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                    '--project', $apiProject, '--', 'apply-migrations') $applyLog -AllowFailure
            }
            $applyText = Get-Content -Raw -LiteralPath $applyLog
            if ($exitCode -eq 0 -or $applyText -notmatch 'Phase 4 preflight failed: existing FX revaluation batches') {
                throw 'Representative clone did not stop at the required Phase 4 historical-FX-evidence guard.'
            }
        }
        finally { Pop-Location }

        $cloneState = [string](Invoke-SqlScalar $target.Builder $target.Database @"
SELECT CONCAT(
    (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory), '|',
    (SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory), '|',
    CASE WHEN EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260903130000_AddFinancialStatementClassificationSnapshots') THEN 1 ELSE 0 END, '|',
    CASE WHEN EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260903190453_AddBookScopedFxRevaluationPolicy') THEN 1 ELSE 0 END, '|',
    (SELECT COUNT_BIG(*) FROM dbo.FxRevaluationBatches WHERE IsDeleted=0));
"@)
        if ($cloneState -notmatch '^449\|20260903130000_AddFinancialStatementClassificationSnapshots\|1\|0\|1$') {
            throw "Representative clone stopped in an unexpected state: $cloneState"
        }
        $cloneState | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'clone-state-at-stop.txt')

        $sourceFingerprintAfter = Get-SourceFingerprint $source.Builder $source.Database
        $sourceFingerprintAfter | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'source-fingerprint-after.txt')
        if ($sourceFingerprintAfter -ne $sourceFingerprintBefore) {
            throw 'Configured source fingerprint changed during guarded clone rehearsal.'
        }

        [ordered]@{
            status = 'EXPECTED_PHASE4_STOP'
            gitHead = (git rev-parse HEAD).Trim()
            sourceDatabase = $source.Database
            targetDatabase = $target.Database
            sourceFingerprint = $sourceFingerprintAfter
            cloneState = $cloneState
            backupPolicy = 'COPY_ONLY_CHECKSUM_VERIFYONLY'
            completedAtUtc = [DateTime]::UtcNow.ToString('O')
        } | ConvertTo-Json | Set-Content -Encoding utf8 -LiteralPath (Join-Path $evidenceDirectoryResolved 'summary.json')
        Write-Host "GL cutover representative clone reached its expected Phase 4 stop. Evidence: $evidenceDirectoryResolved"
        return
    }
    catch {
        Write-Error "Clone rehearsal failed or stopped unexpectedly. The prefix-safe target and exact target-derived backup were preserved for inspection: $($target.Database). $($_.Exception.Message)"
        throw
    }
}

$baselineScript = Join-Path $evidenceDirectoryResolved 'initial-baseline.sql'

Write-Host '[CREATE] Prefix assertion passed; creating the exact resolved rehearsal database.'
Invoke-Sql $target.Builder 'master' "CREATE DATABASE [$escapedDatabase];"

try {
    Push-Location $repositoryRoot
    $oldFocusedTooling = [Environment]::GetEnvironmentVariable('TdcFocusedEfToolingBuild', 'Process')
    try {
        # The full historical designers exceed hundreds of MB of generated C#. The repository's
        # focused tooling mode compiles the authoritative current snapshot plus lightweight
        # discovery metadata, whose parity is separately tested below.
        [Environment]::SetEnvironmentVariable('TdcFocusedEfToolingBuild', 'true', 'Process')
        Invoke-Native 'git' @('diff', '--check')
        Invoke-Native 'dotnet' @('build', $apiProject, '--configuration', 'Debug', '--nologo',
            "-p:TdcEfToolingMigrationDesigner=Migrations\20260313114533_InitialBaseline.Designer.cs")
        Invoke-Native 'dotnet' @('ef', 'migrations', 'script', $baselinePredecessor, $baselineMigration,
            '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
            '--context', 'ApplicationDbContext', '--no-build', '--output', $baselineScript)
        Invoke-Sql $target.Builder $target.Database @"
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory]
    (
        [MigrationId] nvarchar(150) NOT NULL CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY,
        [ProductVersion] nvarchar(32) NOT NULL
    );
END;
"@
        Invoke-Sql $target.Builder $target.Database '' $baselineScript

        $stampedMigrations = @($supersededMigrations) + $baselineMigration
        $stampValues = ($stampedMigrations | ForEach-Object {
            "IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId]=N'$_') INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES (N'$_', N'8.0.0');"
        }) -join "`n"
        Invoke-Sql $target.Builder $target.Database $stampValues

        Set-ApplicationConnection $targetConnection {
            # Exercise the same controlled deployment entry point operators use. It runs
            # Database.MigrateAsync without starting the HTTP host or invoking seeders.
            $migrationLog = Join-Path $evidenceDirectoryResolved 'migration-apply.log'
            $seedPassOneLog = Join-Path $evidenceDirectoryResolved 'seed-pass-1.log'
            $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                '--project', $apiProject, '--', 'apply-migrations') $migrationLog
            $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                '--project', $apiProject, '--', 'seed-db') $seedPassOneLog
        }

        $passOne = Join-Path $evidenceDirectoryResolved 'invariants-pass-1.txt'
        Invoke-Sql $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-rehearsal-invariants.sql') $passOne

        Set-ApplicationConnection $targetConnection {
            $seedPassTwoLog = Join-Path $evidenceDirectoryResolved 'seed-pass-2.log'
            $null = Invoke-NativeWithEvidence 'dotnet' @('run', '--no-build', '--configuration', 'Debug',
                '--project', $apiProject, '--', 'seed-db') $seedPassTwoLog
        }

        $passTwo = Join-Path $evidenceDirectoryResolved 'invariants-pass-2.txt'
        Invoke-Sql $target.Builder $target.Database '' (Join-Path $PSScriptRoot 'sql\gl-rehearsal-invariants.sql') $passTwo
        $first = (Get-FileHash -Algorithm SHA256 $passOne).Hash
        $second = (Get-FileHash -Algorithm SHA256 $passTwo).Hash
        if ($first -ne $second) { throw 'Second seed changed the canonical Finance invariant snapshot.' }

        Set-ApplicationConnection $targetConnection {
            $modelLog = Join-Path $evidenceDirectoryResolved 'ef-no-pending-model.log'
            $migrationListLog = Join-Path $evidenceDirectoryResolved 'migration-discovery.log'
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'has-pending-model-changes',
                '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
                '--context', 'ApplicationDbContext', '--no-build') $modelLog
            $null = Invoke-NativeWithEvidence 'dotnet' @('ef', 'migrations', 'list',
                '--project', $dataProject, '--startup-project', $apiProject, '--configuration', 'Debug',
                '--context', 'ApplicationDbContext', '--no-build', '--no-connect') $migrationListLog
        }

        $migrationCount = @(Get-Content -LiteralPath (Join-Path $evidenceDirectoryResolved 'migration-discovery.log') |
            Where-Object { $_.Trim() -match '^\d{14}_.+' }).Count
        $appliedMigrationCount = [long](Invoke-SqlScalar $target.Builder $target.Database 'SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory;')
        if ($migrationCount -ne 448 -or $appliedMigrationCount -ne 448) {
            throw "Migration discovery/history mismatch. Discovered=$migrationCount; Applied=$appliedMigrationCount."
        }

        @(
            "$first  invariants-pass-1.txt",
            "$second  invariants-pass-2.txt"
        ) | Set-Content -Encoding ascii -LiteralPath (Join-Path $evidenceDirectoryResolved 'checksums.sha256')

        $summary = [ordered]@{
            status = 'PASS'
            gitHead = (git rev-parse HEAD).Trim()
            database = $target.Database
            migrationCount = $migrationCount
            latestMigration = '20260904003118_AddGovernedAccountSegmentIdentity'
            invariantPass1Sha256 = $first
            invariantPass2Sha256 = $second
            applicationMigrationCommand = 'dotnet run --no-build --configuration Debug --project src/ErpSystem.Api/ErpSystem.Api.csproj -- apply-migrations'
            seedPasses = 2
            completedAtUtc = [DateTime]::UtcNow.ToString('O')
        }
        $summary | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $evidenceDirectoryResolved 'summary.json')
        Write-Host "GL cutover empty-database rehearsal passed. Evidence: $evidenceDirectoryResolved"
    }
    finally {
        [Environment]::SetEnvironmentVariable('TdcFocusedEfToolingBuild', $oldFocusedTooling, 'Process')
        Pop-Location
    }
}
catch {
    Write-Error "Rehearsal failed. The safe-named database was preserved for inspection: $($target.Database). $($_.Exception.Message)"
    throw
}
