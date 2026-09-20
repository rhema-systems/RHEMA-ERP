param([Parameter(Mandatory=$true)][string]$ConnectionString)
$ErrorActionPreference = 'Stop'
# Rehearse the procurement-only DDL and guards in an empty disposable database.
# The configured database is read only; no application data is copied.
$source = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
$source.Open()
if ($source.Database -ne 'RhemaERP') { $source.Dispose(); throw 'Unexpected source database.' }
function Scalar($connection, [string]$sql) {
    $command = $connection.CreateCommand(); $command.CommandText = $sql; $command.CommandTimeout = 60
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Execute($connection, [string]$sql) {
    $command = $connection.CreateCommand(); $command.CommandText = $sql; $command.CommandTimeout = 60
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}
$name = 'TdcPettyLifecycle_' + [guid]::NewGuid().ToString('N')
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
$builder['Initial Catalog'] = 'master'
$master = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$master.Open()
$test = $null
try {
    $trigger = [string](Scalar $source "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_ProcurementExceptionalSourcingControls_Lifecycle'))")
    $core = [string](Scalar $source "SELECT definition FROM sys.check_constraints WHERE name='CK_ProcurementExceptionalSourcingControls_Core'")
    $lifecycle = [string](Scalar $source "SELECT definition FROM sys.check_constraints WHERE name='CK_ProcurementExceptionalSourcingControls_Lifecycle'")
    if (!$trigger -or !$core -or !$lifecycle) { throw 'Required baseline guards not found.' }
    Execute $master "CREATE DATABASE [$name]"
    $builder['Initial Catalog'] = $name
    $test = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString); $test.Open()
    Execute $test @"
SELECT TOP (0) * INTO dbo.ProcurementExceptionalSourcingControls FROM RhemaERP.dbo.ProcurementExceptionalSourcingControls;
CREATE TABLE dbo.Tenders(Id uniqueidentifier, TenantId uniqueidentifier, IsDeleted bit, SourcingCaseId uniqueidentifier);
CREATE TABLE dbo.ProcurementSourcingCases(Id uniqueidentifier, TenantId uniqueidentifier, IsDeleted bit, MethodRuleId uniqueidentifier, AuthorityRouteId uniqueidentifier NULL, SelectedMethod int, PolicySetId uniqueidentifier);
CREATE TABLE dbo.ProcurementPolicyMethodRules(Id uniqueidentifier, TenantId uniqueidentifier, PolicySetId uniqueidentifier);
CREATE TABLE dbo.ProcurementPolicyExceptionRules(Id uniqueidentifier, TenantId uniqueidentifier, PolicySetId uniqueidentifier);
CREATE TABLE dbo.ProcurementRequisitionAuthorityRoutes(Id uniqueidentifier, TenantId uniqueidentifier);
ALTER TABLE dbo.ProcurementExceptionalSourcingControls ADD CONSTRAINT CK_ProcurementExceptionalSourcingControls_Core CHECK ($core);
ALTER TABLE dbo.ProcurementExceptionalSourcingControls ADD CONSTRAINT CK_ProcurementExceptionalSourcingControls_Lifecycle CHECK ($lifecycle);
"@
    Execute $test $trigger
    Execute $test 'ALTER TABLE dbo.ProcurementExceptionalSourcingControls ALTER COLUMN AuthorityRouteId uniqueidentifier NULL;'
    $migration = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../src/ErpSystem.Data/Migrations/20260907033000_AlignPettyPurchaseQuotationLifecycle.cs') -Raw
    $guardSql = [regex]::Match($migration, '(?s)internal const string GuardSql = """\s*(.*?)\s*""";').Groups[1].Value
    if (!$guardSql) { throw 'Migration SQL was not found.' }
    Execute $test $guardSql
    $tenant = [guid]::NewGuid().ToString(); $tender = [guid]::NewGuid().ToString(); $case = [guid]::NewGuid().ToString()
    $method = [guid]::NewGuid().ToString(); $exception = [guid]::NewGuid().ToString(); $policy = [guid]::NewGuid().ToString()
    $control = [guid]::NewGuid().ToString(); $bid = [guid]::NewGuid().ToString()
    Execute $test @"
INSERT dbo.Tenders VALUES ('$tender','$tenant',0,'$case');
INSERT dbo.ProcurementSourcingCases VALUES ('$case','$tenant',0,'$method',NULL,5,'$policy');
INSERT dbo.ProcurementPolicyMethodRules VALUES ('$method','$tenant','$policy');
INSERT dbo.ProcurementPolicyExceptionRules VALUES ('$exception','$tenant','$policy');
"@
    $command = $test.CreateCommand()
    $command.CommandText = "SELECT name,TYPE_NAME(system_type_id) AS datatype,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('dbo.ProcurementExceptionalSourcingControls') AND is_computed=0 AND TYPE_NAME(system_type_id)<>'timestamp' ORDER BY column_id"
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($command); $columns = [System.Data.DataTable]::new(); [void]$adapter.Fill($columns)
    $overrides = @{ Id="'$control'"; TenantId="'$tenant'"; TenderId="'$tender'"; SourcingCaseId="'$case'";
        MethodRuleId="'$method'"; ExceptionRuleId="'$exception'"; AuthorityRouteId='NULL'; AuthorityRouteReference="''";
        Method='5'; Status='0'; SupplierSnapshotJson="'[]'"; EvidenceChecklistJson="'[]'"; ApprovalActorsJson="'[]'";
        LifecycleSnapshotJson="'{}'"; IntegrityHash="REPLICATE('a',64)" }
    $names = @(); $values = @()
    foreach($column in $columns.Rows) {
        if ($column.is_nullable -and !$overrides.ContainsKey($column.name)) { continue }
        $names += '['+$column.name+']'
        if ($overrides.ContainsKey($column.name)) { $values += $overrides[$column.name] }
        elseif ($column.datatype -eq 'uniqueidentifier') { $values += 'NEWID()' }
        elseif ($column.datatype -match 'date|time') { $values += 'SYSUTCDATETIME()' }
        elseif ($column.datatype -match 'char|text') { $values += "'UAT fixture'" }
        else { $values += '0' }
    }
    Execute $test ('INSERT dbo.ProcurementExceptionalSourcingControls ('+($names -join ',')+') VALUES ('+($values -join ',')+')')
    $script:rejected = 0
    function Reject([string]$label, [string]$sql) {
        $transaction = $test.BeginTransaction(); $command = $test.CreateCommand(); $command.Transaction = $transaction; $command.CommandText = $sql
        $blocked = $false
        try { [void]$command.ExecuteNonQuery() } catch [System.Data.SqlClient.SqlException] { $blocked = $true }
        finally { try { $transaction.Rollback() } catch {} $command.Dispose(); $transaction.Dispose() }
        if (!$blocked) { throw "Guard failed: $label" }; $script:rejected++; Write-Output "PASS: $label"
    }
    Reject 'Cannot skip independent approval' "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=4,RecommendedBidId='$bid',RecommendationReason='x',RecommendationEvidenceReference='e',RecommendedAtUtc=SYSUTCDATETIME(),RecommendedById=NEWID() WHERE Id='$control'"
    Reject 'Cannot rewrite prepared evidence' "UPDATE dbo.ProcurementExceptionalSourcingControls SET SupplierSelectionEvidenceReference='changed' WHERE Id='$control'"
    Reject 'Cannot change tenant' "UPDATE dbo.ProcurementExceptionalSourcingControls SET TenantId=NEWID() WHERE Id='$control'"
    Reject 'Cannot downgrade restricted method authority' "UPDATE dbo.ProcurementExceptionalSourcingControls SET Method=3 WHERE Id='$control'"
    Execute $test "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=1,WorkflowInstanceId=NEWID(),SubmittedForApprovalAtUtc=SYSUTCDATETIME(),SubmittedForApprovalById=NEWID() WHERE Id='$control'"
    Execute $test "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=2,ApprovedAtUtc=SYSUTCDATETIME(),ApprovedById=NEWID() WHERE Id='$control'"
    Reject 'Recommendation evidence remains required' "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=4 WHERE Id='$control'"
    Execute $test "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=4,RecommendedBidId='$bid',RecommendationReason='reviewed quote',RecommendationEvidenceReference='UAT evidence',RecommendedAtUtc=SYSUTCDATETIME(),RecommendedById=NEWID() WHERE Id='$control'"
    Reject 'Award must match recommendation' "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=5,AwardBidId=NEWID(),AwardReference='UAT',AwardEvidenceReference='UAT',AwardedAtUtc=SYSUTCDATETIME() WHERE Id='$control'"
    Execute $test "UPDATE dbo.ProcurementExceptionalSourcingControls SET Status=5,AwardBidId='$bid',AwardReference='UAT',AwardEvidenceReference='UAT',AwardedAtUtc=SYSUTCDATETIME() WHERE Id='$control'"
    $result = Scalar $test "SELECT COUNT(*) FROM dbo.ProcurementExceptionalSourcingControls WHERE Status=5 AND SuppliersInvitedAtUtc IS NULL AND NegotiationId IS NULL AND AuthorityRouteId IS NULL"
    if ($result -ne 1) { throw 'Expected one independently approved quoted award without fabricated tender stages.' }
    Write-Output "PASS: Petty preparation, approval, recommendation and award; $script:rejected negative SQL guards."
} finally {
    if ($test) { $test.Dispose() }
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    if ($name -notmatch '^TdcPettyLifecycle_[a-f0-9]{32}$') { throw 'Unsafe disposable database name.' }
    Execute $master "IF DB_ID('$name') IS NOT NULL BEGIN ALTER DATABASE [$name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$name]; END"
    $master.Dispose(); $source.Dispose()
    Write-Output "Removed disposable database $name; configured ERP data unchanged."
}
