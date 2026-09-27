[CmdletBinding()]
param(
    [string]$ApiDirectory,
    [ValidateRange(2,16)][int]$HeapLimitGiB = 4,
    [string]$DiagnosticResumeEvidence,
    [ValidateRange(60,1000)][int]$ExpectedMigrationCount = 60,
    [ValidatePattern('^20\d{12}_[A-Za-z][A-Za-z0-9_]+$')]
    [string]$ExpectedLastMigration = '20260927021852_InventoryIssueActualReceipts',
    [switch]$Execute
)

# PowerShell 7. Creates a NEW isolated database only, using an already-built API.
# Never drops databases, seeds data, changes user secrets, or repoints the app.
$ErrorActionPreference = 'Stop'
if (!$Execute) { throw 'Supply -Execute to create a new isolated acceptance database.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$ApiDirectory) { $ApiDirectory = Join-Path $repo 'src\ErpSystem.Api\bin\Debug\net8.0' }
$apiRoot = (Resolve-Path -LiteralPath $ApiDirectory).Path
$apiDll = Join-Path $apiRoot 'ErpSystem.Api.dll'
$dataDll = Join-Path $apiRoot 'ErpSystem.Data.dll'
foreach ($file in @($apiDll,$dataDll,(Join-Path $apiRoot 'ErpSystem.Api.runtimeconfig.json'))) {
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw 'Full prebuilt API artifacts are required.' }
}
$expected = @(Get-ChildItem (Join-Path $repo 'src\ErpSystem.Data\Migrations') -Filter '*.cs' -File |
    Where-Object { $_.BaseName -cmatch '^20\d{12}_[A-Za-z][A-Za-z0-9_]+$' } | ForEach-Object BaseName | Sort-Object)
if ($expected.Count -ne $ExpectedMigrationCount -or $expected[-1] -cne $ExpectedLastMigration) {
    throw 'The source migration chain differs from the explicitly expected acceptance baseline.'
}
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$target = 'RhemaERP_ReceiptFresh_' + $stamp
$server = 'RHEMA-MICHAEL\SQL2017'
$resume=$null
if ($DiagnosticResumeEvidence) {
    $resume=Get-Content -LiteralPath $DiagnosticResumeEvidence -Raw | ConvertFrom-Json
    if ($resume.Passed -or !$resume.DatabaseCreated -or $resume.Server -cne $server -or
        $resume.InitialMigrationCount -ne 0 -or $resume.InitialUserTableCount -ne 0 -or
        $resume.ApiSha256 -cne (Get-FileHash $apiDll -Algorithm SHA256).Hash -or
        $resume.DataSha256 -cne (Get-FileHash $dataDll -Algorithm SHA256).Hash) {
        throw 'Diagnostic resume requires failed owned fresh-target evidence and identical API/Data binaries.'
    }
    $target=[string]$resume.Target
}
if ($target -cnotmatch '^RhemaERP_ReceiptFresh_[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') { throw 'Invalid fresh target name.' }
$evidenceRoot = Join-Path $repo ('tmp\rf-' + $stamp)
[void][IO.Directory]::CreateDirectory($evidenceRoot)
$evidencePath = Join-Path $evidenceRoot 'fresh-install.json'
$report = [ordered]@{
    Passed=$false; Server=$server; Target=$target; StartedUtc=[DateTime]::UtcNow.ToString('o');
    ApiSha256=(Get-FileHash $apiDll -Algorithm SHA256).Hash;
    DataSha256=(Get-FileHash $dataDll -Algorithm SHA256).Hash;
    ExpectedMigrationCount=$expected.Count; ExistingDatabaseWrites=$false; SourceOrApplicationDatabaseWrites=$false;
    RuntimeConfigurationChanged=$false; SeedingRun=$false; ConnectionStringsPersisted=$false;
    HeapLimitGiB=$HeapLimitGiB
}
$connection = $null; $stage = 'configuration'; $created = $false
if ($resume) {
    $report.DiagnosticOnly=$true
    $report.AcceptanceEligible=$false
    $report.ExistingDatabaseWrites=$true
    $report.OnlyOwnedDiagnosticTargetWritten=$true
    $report.ResumeEvidence=(Resolve-Path -LiteralPath $DiagnosticResumeEvidence).Path
}
function Query([string]$Text, [switch]$Scalar, [switch]$Rows) {
    $cmd = $connection.CreateCommand(); $cmd.CommandTimeout=600; $cmd.CommandText=$Text
    try {
        if ($Scalar) { return $cmd.ExecuteScalar() }
        if ($Rows) {
            $table=[Data.DataTable]::new(); $reader=$cmd.ExecuteReader()
            try { $table.Load($reader) } finally { $reader.Dispose() }
            return ,$table
        }
        [void]$cmd.ExecuteNonQuery()
    } finally { $cmd.Dispose() }
}
function Assert-Target {
    if (!$created -or $connection.Database -cne $target -or
        [string](Query 'SELECT DB_NAME()' -Scalar) -cne $target -or
        [string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar) -ine $server) {
        throw 'Fresh target ownership assertion failed.'
    }
}
try {
    [xml]$project = [IO.File]::ReadAllText((Join-Path $repo 'src\ErpSystem.Api\ErpSystem.Api.csproj'))
    $secretId = [string](@($project.Project.PropertyGroup.UserSecretsId | Where-Object { $_ })[0])
    $secrets = [IO.File]::ReadAllText((Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json")) | ConvertFrom-Json
    $value = [string]$secrets.'ConnectionStrings:DefaultConnection'
    if (!$value -and $secrets.ConnectionStrings) { $value=[string]$secrets.ConnectionStrings.DefaultConnection }
    if (!$value) { throw 'User-secret connection is unavailable.' }
    $builder = [Data.SqlClient.SqlConnectionStringBuilder]::new($value); $value=$null; $secrets=$null
    if ($builder.DataSource -notin @($server,'.\SQL2017','localhost\SQL2017')) { throw 'SQL instance is outside acceptance scope.' }
    $builder['Data Source']=$server; $builder['Initial Catalog']='master'; $builder['TrustServerCertificate']=$true
    $connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $connection.Open()
    if ([string](Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar) -ine $server) { throw 'Physical SQL instance mismatch.' }
    if ($resume) {
        $stage='verify owned failed diagnostic target'
        if ([int](Query "SELECT COUNT(*) FROM sys.databases WHERE name=N'$target' AND state_desc='ONLINE'" -Scalar) -ne 1) { throw 'Owned diagnostic target is unavailable.' }
        $created=$true; $connection.ChangeDatabase($target); Assert-Target
        $before=Query 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId' -Rows
        $beforeIds=@($before.Rows | ForEach-Object { [string]$_.MigrationId })
        if (!$beforeIds.Count -or $beforeIds.Count -ge $expected.Count -or
            ($beforeIds -join "`n") -cne ($expected[0..($beforeIds.Count-1)] -join "`n")) {
            throw 'Diagnostic target history is not an incomplete prefix of the exact migration chain.'
        }
        $report.InitialMigrationCount=$beforeIds.Count; $report.InitialMigrationIds=$beforeIds
        Write-Output "DIAGNOSTIC|Resuming owned incomplete target|Migrations=$($beforeIds.Count)|$target"
    } else {
        $stage='create new empty database'
        Query "IF DB_ID(N'$target') IS NOT NULL THROW 51999,'Target already exists.',1; CREATE DATABASE [$target];"
        $created=$true; $report.DatabaseCreated=$true; $connection.ChangeDatabase($target); Assert-Target
        if ([int](Query 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0' -Scalar) -ne 0) { throw 'New target is not empty.' }
        $report.InitialUserTableCount=0; $report.InitialMigrationCount=0
    }
    $builder['Initial Catalog']=$target

    # Configuration and subprocess output never contain persisted credentials.
    @{Serilog=@{MinimumLevel=@{Default='Warning'};WriteTo=@(@{Name='Console'})};Logging=@{LogLevel=@{Default='Warning'}}} |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $evidenceRoot 'appsettings.json') -Encoding utf8
    $start=[Diagnostics.ProcessStartInfo]::new(); $start.FileName=(Get-Command dotnet).Source
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WindowStyle='Hidden'
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true; $start.WorkingDirectory=$evidenceRoot
    foreach ($arg in @($apiDll,'apply-migrations','--migration-command-timeout-seconds','600')) { $start.ArgumentList.Add($arg) }
    $start.Environment.Clear()
    foreach ($name in @('PATH','SystemRoot','WINDIR','TEMP','TMP','USERPROFILE','APPDATA','LOCALAPPDATA','ProgramData','ProgramFiles','ProgramFiles(x86)','COMSPEC','HOMEDRIVE','HOMEPATH','DOTNET_ROOT','DOTNET_ROOT(x86)')) {
        $entry=[Environment]::GetEnvironmentVariable($name,'Process'); if ($null -ne $entry) { $start.Environment[$name]=$entry }
    }
    $settings=@{
        ASPNETCORE_ENVIRONMENT='CanonicalVerification'; DOTNET_ENVIRONMENT='CanonicalVerification';
        ConnectionStrings__DefaultConnection=$builder.ConnectionString; Database__Provider='SqlServer';
        SkipStartupInitialization='true'; BackgroundServices__Enabled='false';
        HTTP_PROXY='http://127.0.0.1:9'; HTTPS_PROXY='http://127.0.0.1:9'; ALL_PROXY='http://127.0.0.1:9'; NO_PROXY='';
        DOTNET_CLI_TELEMETRY_OPTOUT='1'; DOTNET_PROCESSOR_COUNT='2'; DOTNET_GCHeapHardLimit=('0x{0:X}' -f ([long]$HeapLimitGiB * 1GB))
    }
    foreach ($entry in $settings.GetEnumerator()) { $start.Environment[$entry.Key]=[string]$entry.Value }
    $stage='apply complete migration chain'; $process=[Diagnostics.Process]::new(); $process.StartInfo=$start; $childStarted=$false
    try {
        $childStarted=$process.Start(); $report.ChildProcessId=$process.Id
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        $observedPeakWorkingSet=[long]0
        while (!$process.WaitForExit(1000)) {
            try {
                $process.Refresh()
                $observedPeakWorkingSet=[Math]::Max($observedPeakWorkingSet,$process.PeakWorkingSet64)
            } catch { } # A child can exit between the wait and the process sample.
        }
        $raw=$stdout.GetAwaiter().GetResult()+"`n"+$stderr.GetAwaiter().GetResult()
        $report.CliExitCode=$process.ExitCode
        $report.CliExitCodeHex=('0x{0:X8}' -f ([long]$process.ExitCode -band 0xFFFFFFFFL))
        $report.ObservedPeakWorkingSetBytes=$observedPeakWorkingSet
        $report.OutputSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($raw)))
        $report.ExceptionTypes=@([regex]::Matches($raw,'\b(?:System|Microsoft|ErpSystem)\.[A-Za-z.]+Exception\b') | ForEach-Object Value | Sort-Object -Unique)
        $report.SqlErrorNumbers=@([regex]::Matches($raw,'Error Number:\s*(\d+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $report.GuardCodes=@([regex]::Matches($raw,'\b(?:CANONICAL|FINANCE|INVENTORY|INV|AP|AR|PROCUREMENT|ESTATE)_[A-Z0-9_]{3,90}:') | ForEach-Object { $_.Value.TrimEnd(':') } | Sort-Object -Unique)
        # Persist only fixed signal labels, never surrounding exception text or SQL.
        $signalPatterns=[ordered]@{
            OutOfMemory='(?i)out of memory|OutOfMemoryException|insufficient memory';
            StackOverflow='(?i)stack overflow|StackOverflowException';
            AccessViolation='(?i)AccessViolationException|access violation';
            FatalClrError='(?i)fatal (?:internal )?(?:CLR|runtime) error|internal CLR error';
            NativeLibraryLoad='(?i)DllNotFoundException|Unable to load (?:shared library|DLL)';
            SqlTimeout='(?i)Execution Timeout Expired|SqlException[^\r\n]*timeout';
            ConnectionFailure='(?i)network-related or instance-specific|Login failed for user|Cannot open database';
            RuntimeMissing='(?i)You must install or update .NET|compatible framework version';
            UnhandledException='(?i)Unhandled exception';
            ProcessAborted='(?i)process was terminated|process terminated|FailFast'
        }
        $report.RuntimeFailureSignals=@($signalPatterns.Keys | Where-Object { $raw -match $signalPatterns[$_] })
        $raw=$null
        if ($process.ExitCode -ne 0) { throw 'Migration CLI failed; safe evidence retained.' }
    } finally {
        # Cancellation must not leave this harness's migration process running in the background.
        if ($childStarted -and !$process.HasExited) {
            $process.Kill($true)
            [void]$process.WaitForExit(5000)
        }
        $process.Dispose(); $settings=$null; $start=$null
    }
    $stage='verify complete history and schema'; Assert-Target
    $history=Query 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId' -Rows
    $actual=@($history.Rows | ForEach-Object { [string]$_.MigrationId }); $report.MigrationIds=$actual; $report.MigrationCount=$actual.Count
    if (($actual -join "`n") -cne ($expected -join "`n")) { throw 'Installed history differs from the complete source migration chain.' }
    Query @'
IF OBJECT_ID(N'dbo.InventoryIssueVoucherReceiptLines',N'U') IS NULL
 OR COL_LENGTH(N'dbo.InventoryIssueVouchers',N'ReceiptSequence') IS NULL
 OR ISNULL(COL_LENGTH(N'dbo.InventoryIssueVoucherActions',N'ReceiptIdempotencyKey'),-1)<>200
 OR ISNULL(COL_LENGTH(N'dbo.InventoryIssueVoucherActions',N'ReceiptPayloadHash'),-1)<>128
 THROW 51010,'Receipt schema missing after migration.',1;
IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVoucherReceiptLines'))<>14
 OR NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVoucherReceiptLines') AND name=N'ReceivedQuantity' AND precision=18 AND scale=4 AND is_nullable=0)
 THROW 51011,'Receipt column schema differs.',1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID(N'dbo.InventoryIssueVoucherReceiptLines') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)<>3
 THROW 51012,'Receipt restricted foreign keys missing or untrusted.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVoucherActions') AND name=N'IX_InventoryIssueVoucherActions_TenantId_InventoryIssueVoucherId_ReceiptIdempotencyKey' AND is_unique=1 AND has_filter=1 AND is_disabled=0)
 OR NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.InventoryIssueVoucherReceiptLines') AND name=N'IX_InventoryIssueVoucherReceiptLines_TenantId_InventoryIssueVoucherActionId_InventoryIssueVoucherLineId' AND is_unique=1 AND is_disabled=0)
 THROW 51013,'Receipt replay/line uniqueness indexes missing.',1;
IF (SELECT COUNT(*) FROM sys.triggers WHERE name IN(N'TR_InventoryIssueVouchers_ControlledLifecycle',N'TR_InventoryIssueVoucherActions_AppendOnly',N'TR_InventoryIssueVoucherReceiptLines_AppendOnly') AND is_disabled=0)<>3
 OR CHARINDEX(N'INV_RECEIPT_INITIAL_SEQUENCE_INVALID',OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_InventoryIssueVouchers_ControlledLifecycle')))=0
 THROW 51014,'Receipt lifecycle guards missing or disabled.',1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN(N'CK_InventoryIssueVoucherReceiptLines_Quantity',N'CK_InventoryIssueVouchers_ReceiptSequence',N'CK_InventoryIssueVoucherActions_ActionType') AND is_disabled=0 AND is_not_trusted=0)<>3
 THROW 51015,'Receipt check constraints missing or untrusted.',1;
'@
    $report.SchemaAndGuardsVerified=$true
    if ($ExpectedLastMigration -match '_InventoryControlledWorkflowsAndAccounting$') {
        Query ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'InventoryWorkflowsSchemaAssertions.sql')))
        $report.InventoryWorkflowSchemaAndGuardsVerified=$true
    }
    Query "DBCC CHECKDB ([$target]) WITH PHYSICAL_ONLY,NO_INFOMSGS;"
    $report.PhysicalCheckPassed=$true
    if ($resume) {
        $report.DiagnosticCompleted=$true
        Write-Output "DIAGNOSTIC_COMPLETE|Resume succeeded; not fresh-install acceptance|Migrations=$($actual.Count)|$target"
    } else {
        $report.Passed=$true
        Write-Output "PASS|Full fresh install|Migrations=$($actual.Count)|$target"
    }
} catch {
    $report.FailureStage=$stage; $report.ErrorType=$_.Exception.GetType().FullName
    $inner=$_.Exception; while ($inner.InnerException) { $inner=$inner.InnerException }
    if ($inner -is [Data.SqlClient.SqlException]) { $report.SqlErrorNumber=$inner.Number }
    # The migration CLI may fail after committing earlier migrations. Capture
    # history from this owned target only, without attempting resume or repair.
    if ($created -and $connection -and $connection.State -eq 'Open') {
        try {
            Assert-Target
            if ([int](Query "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory',N'U') IS NULL THEN 0 ELSE 1 END" -Scalar) -eq 1) {
                $failedHistory=Query 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId' -Rows
                $report.MigrationIds=@($failedHistory.Rows | ForEach-Object { [string]$_.MigrationId })
                $report.MigrationCount=$report.MigrationIds.Count
                if ($report.MigrationCount) { $report.LastAppliedMigration=$report.MigrationIds[-1] }
            }
        } catch { $report.FailureHistoryProbeSucceeded=$false }
    }
    Write-Output "FAIL|Fresh install|Stage=$stage|Type=$($report.ErrorType)"
} finally {
    if ($connection) { $connection.Dispose() }; $builder=$null
    $report.FinishedUtc=[DateTime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $evidencePath -Encoding utf8
    Write-Output "EVIDENCE|$evidencePath"
    if ($created) { Write-Output "TARGET_RETAINED|$target" }
}
if (!$report.Passed -and !$report.DiagnosticCompleted) { exit 1 }
