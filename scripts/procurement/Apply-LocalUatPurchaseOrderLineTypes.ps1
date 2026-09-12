param([Parameter(Mandatory)][System.Data.Common.DbConnection]$Connection)
$ErrorActionPreference = 'Stop'
if ($Connection.Database -ne 'RhemaERP') { throw 'This narrowly scoped UAT migration requires the RhemaERP database.' }
$taskTransaction = $Connection.BeginTransaction()
function Invoke-LineTypeSql([string]$Sql, [switch]$Scalar) {
    $taskCommand = $Connection.CreateCommand()
    try {
        $taskCommand.Transaction = $taskTransaction
        $taskCommand.CommandText = $Sql
        $taskCommand.CommandTimeout = 60
        if ($Scalar) { return $taskCommand.ExecuteScalar() }
        [void]$taskCommand.ExecuteNonQuery()
    } finally { $taskCommand.Dispose() }
}
try {
    $taskServer = [string](Invoke-LineTypeSql "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))" -Scalar)
    if ($taskServer -ne 'RHEMA-MICHAEL\SQL2017') { throw 'Unexpected SQL Server; no changes applied.' }
    $taskMigrationId = '20260908170000_ClassifyPurchaseOrderLines'
    $taskApplied = [int](Invoke-LineTypeSql "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='$taskMigrationId'" -Scalar)
    if ($taskApplied -gt 0) {
        if ([int](Invoke-LineTypeSql "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PurchaseOrderItems') AND name='LineType' AND is_nullable=0" -Scalar) -ne 1) { throw 'Applied migration does not match the schema.' }
        $taskTransaction.Rollback()
        'Already applied; no changes made.'
        return
    }
    if ([int](Invoke-LineTypeSql "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId='20260908153000_AlignContractPurchaseOrderCommercialIdentity'" -Scalar) -ne 1) { throw 'The source-identity prerequisite is not applied.' }
    if ((Invoke-LineTypeSql "SELECT COL_LENGTH('dbo.PurchaseOrderItems','LineType')" -Scalar) -isnot [DBNull]) { throw 'Unexpected existing LineType column; investigate migration drift.' }
    $taskRowsBefore = [long](Invoke-LineTypeSql 'SELECT COUNT_BIG(*) FROM PurchaseOrderItems' -Scalar)
    # Same additive operations as the EF migration. Execute separately so SQL Server binds the new column after creation.
    Invoke-LineTypeSql 'ALTER TABLE dbo.PurchaseOrderItems ADD LineType int NOT NULL CONSTRAINT DF_PurchaseOrderItems_LineType DEFAULT 1;'
    Invoke-LineTypeSql 'ALTER TABLE dbo.PurchaseOrderItems ADD CONSTRAINT CK_PurchaseOrderItems_LineType CHECK (LineType IN (1,2,3,4));'
    if ([long](Invoke-LineTypeSql 'SELECT COUNT_BIG(*) FROM PurchaseOrderItems' -Scalar) -ne $taskRowsBefore) { throw 'PO line count changed unexpectedly.' }
    if ([long](Invoke-LineTypeSql 'SELECT COUNT_BIG(*) FROM PurchaseOrderItems WHERE LineType<>1' -Scalar) -ne 0) { throw 'Historic rows were reclassified unexpectedly.' }
    Invoke-LineTypeSql "INSERT __EFMigrationsHistory(MigrationId,ProductVersion) VALUES('$taskMigrationId','8.0.0');"
    $taskTransaction.Commit()
    'Applied PO line classification; existing PO lines retain stock behaviour. No commercial values or approvals changed.'
} catch {
    try { $taskTransaction.Rollback() } catch {}
    throw
} finally { $taskTransaction.Dispose() }
