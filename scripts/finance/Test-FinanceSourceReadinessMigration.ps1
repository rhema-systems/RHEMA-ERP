param([Parameter(Mandatory)][string]$ConnectionString)
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260920140000_ReconcileFinanceSourceReadinessForExistingDatabases.cs') -Raw
$sql = [regex]::Match($source, '(?s)ReconciliationSql = """\s*(.*?)\s*""";').Groups[1].Value
if (-not $sql) { throw 'Migration SQL not found.' }
$db = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
try {
    $db.Open()
    $db.ChangeDatabase('tempdb')
    $tx = $db.BeginTransaction()
    function Run([string]$text) {
        $cmd = $db.CreateCommand()
        try { $cmd.Transaction = $tx; $cmd.CommandText = $text; $cmd.CommandTimeout = 60; $cmd.ExecuteScalar() }
        finally { $cmd.Dispose() }
    }
    $schema = 'qs_readiness_' + [guid]::NewGuid().ToString('N')
    [void](Run "CREATE SCHEMA [$schema]")
    [void](Run "CREATE TABLE [$schema].Accounts(Id uniqueidentifier PRIMARY KEY);
        CREATE TABLE [$schema].FinanceSourceDimensionAssignments(Id uniqueidentifier PRIMARY KEY, TenantId uniqueidentifier NOT NULL, LegacyValue nvarchar(40), RowVersion rowversion);
        INSERT [$schema].FinanceSourceDimensionAssignments(Id,TenantId,LegacyValue) VALUES(NEWID(),NEWID(),N'Preserve existing evidence');")
    $rows = "SELECT Id,TenantId,LegacyValue,RowVersion FROM [$schema].FinanceSourceDimensionAssignments FOR JSON PATH"
    $before = Run $rows
    $scoped = $sql.Replace('dbo.', "$schema.")
    [void](Run $scoped)
    [void](Run $scoped)
    if ((Run $rows) -ne $before) { throw 'Legacy row or rowversion changed.' }
    if ((Run "SELECT COUNT(*) FROM [$schema].FinanceSourceDimensionAssignments WHERE ExpectedSourceLineCount IS NULL AND ResolvedAccountId IS NULL AND SourceDocumentDate IS NULL AND SourceLineManifestHash IS NULL") -ne 1) { throw 'Legacy evidence was fabricated.' }
    if ((Run "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('$schema.FinanceSourceDimensionAssignments') AND is_disabled=0 AND is_not_trusted=0") -ne 1) { throw 'Trusted account FK missing.' }
    [void](Run "DECLARE @account uniqueidentifier=NEWID(); INSERT [$schema].Accounts VALUES(@account); UPDATE [$schema].FinanceSourceDimensionAssignments SET ResolvedAccountId=@account,ExpectedSourceLineCount=1,SourceDocumentDate='20260920',SourceLineManifestHash=REPLICATE('a',64);")
    $rejected = $false
    try { [void](Run "UPDATE [$schema].FinanceSourceDimensionAssignments SET ResolvedAccountId=NEWID()") }
    catch [System.Data.SqlClient.SqlException] { if ($_.Exception.Number -ne 547) { throw }; $rejected = $true }
    if (-not $rejected) { throw 'Invalid account reference was accepted.' }
    [void](Run $scoped)
    if ((Run "SELECT COUNT(*) FROM [$schema].FinanceSourceDimensionAssignments WHERE ExpectedSourceLineCount=1 AND LEN(SourceLineManifestHash)=64 AND ResolvedAccountId IS NOT NULL") -ne 1) { throw 'Current readiness evidence changed on repeat migration.' }
    Write-Output 'PASS: repeat migration, legacy rows/rowversion, nullable legacy evidence, trusted FK, valid/invalid references, current evidence preservation.'
}
finally { if ($tx) { $tx.Rollback() }; $db.Dispose() }
