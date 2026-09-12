param([Parameter(Mandatory)][System.Data.Common.DbConnection]$Connection)

$ErrorActionPreference = 'Stop'
if ($Connection.State -ne [System.Data.ConnectionState]::Open) { throw 'An open connection is required.' }
if ($Connection.Database -ne 'RhemaERP') { throw 'This migration is restricted to the local RhemaERP UAT database.' }

$taskMigrationId = '20260908220000_ScopePlannedLandedCostsToPurchaseOrderLines'
$taskTables = @('PurchaseOrderLandedCostPlanItems', 'LandedCostItems')
$taskTransaction = $Connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
function Invoke-LandedCostScopeSql([string]$Sql, [switch]$Scalar) {
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
    Invoke-LandedCostScopeSql "SET XACT_ABORT ON; IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017' THROW 51000, 'Unexpected SQL Server; no changes applied.', 1;"
    if ([int](Invoke-LandedCostScopeSql "SELECT COUNT(*) FROM dbo.Tenants WHERE Id='00000000-0000-0000-0000-000000000001'" -Scalar) -ne 1) { throw 'Expected local UAT tenant is missing.' }
    $taskApplied = [int](Invoke-LandedCostScopeSql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WITH (UPDLOCK,HOLDLOCK) WHERE MigrationId='$taskMigrationId'" -Scalar)
    $taskRowsBefore = @{}
    foreach ($taskTable in @('PurchaseOrderItems') + $taskTables) {
        if ((Invoke-LandedCostScopeSql "SELECT OBJECT_ID('dbo.$taskTable','U')" -Scalar) -is [DBNull]) { throw "Required table $taskTable is missing." }
        $taskRowsBefore[$taskTable] = [long](Invoke-LandedCostScopeSql "SELECT COUNT_BIG(*) FROM dbo.[$taskTable] WITH (HOLDLOCK)" -Scalar)
    }

    if ($taskApplied -eq 0) {
        foreach ($taskTable in $taskTables) {
            if ((Invoke-LandedCostScopeSql "SELECT COL_LENGTH('dbo.$taskTable','PurchaseOrderItemId')" -Scalar) -isnot [DBNull]) {
                throw "Unexpected existing $taskTable.PurchaseOrderItemId; investigate schema drift before applying."
            }
        }
        # Exact additive operations from the EF migration. No unrelated migrations or business-record updates.
        foreach ($taskTable in $taskTables) {
            Invoke-LandedCostScopeSql "ALTER TABLE dbo.[$taskTable] ADD [PurchaseOrderItemId] uniqueidentifier NULL;"
            Invoke-LandedCostScopeSql "CREATE INDEX [IX_${taskTable}_PurchaseOrderItemId] ON dbo.[$taskTable] ([PurchaseOrderItemId]);"
            Invoke-LandedCostScopeSql "ALTER TABLE dbo.[$taskTable] WITH CHECK ADD CONSTRAINT [FK_${taskTable}_PurchaseOrderItems_PurchaseOrderItemId] FOREIGN KEY ([PurchaseOrderItemId]) REFERENCES dbo.[PurchaseOrderItems] ([Id]) ON DELETE NO ACTION;"
            if ([long](Invoke-LandedCostScopeSql "SELECT COUNT_BIG(*) FROM dbo.[$taskTable] WHERE PurchaseOrderItemId IS NOT NULL" -Scalar) -ne 0) {
                throw 'Existing cost scope unexpectedly changed.'
            }
        }
        Invoke-LandedCostScopeSql "INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES('$taskMigrationId','8.0.0');"
    }

    foreach ($taskTable in $taskTables) {
        $taskSchemaCheck = @"
SELECT CASE WHEN
 EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.$taskTable') AND name='PurchaseOrderItemId' AND system_type_id=36 AND max_length=16 AND is_nullable=1 AND default_object_id=0)
 AND EXISTS (
   SELECT 1 FROM sys.indexes i
   JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
   JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
   WHERE i.object_id=OBJECT_ID('dbo.$taskTable') AND i.name='IX_${taskTable}_PurchaseOrderItemId'
     AND i.is_unique=0 AND i.is_disabled=0 AND i.has_filter=0
     AND ic.key_ordinal=1 AND c.name='PurchaseOrderItemId'
     AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id)=1)
 AND EXISTS (
   SELECT 1 FROM sys.foreign_keys fk
   JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
   JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
   JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
   WHERE fk.parent_object_id=OBJECT_ID('dbo.$taskTable') AND fk.name='FK_${taskTable}_PurchaseOrderItems_PurchaseOrderItemId'
     AND fk.referenced_object_id=OBJECT_ID('dbo.PurchaseOrderItems') AND pc.name='PurchaseOrderItemId' AND rc.name='Id'
     AND fk.is_disabled=0 AND fk.is_not_trusted=0 AND fk.delete_referential_action=0 AND fk.update_referential_action=0
     AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=fk.object_id)=1)
 THEN 1 ELSE 0 END;
"@
        if ([int](Invoke-LandedCostScopeSql $taskSchemaCheck -Scalar) -ne 1) { throw "Migration schema verification failed for $taskTable." }
    }
    foreach ($taskTable in @('PurchaseOrderItems') + $taskTables) {
        if ([long](Invoke-LandedCostScopeSql "SELECT COUNT_BIG(*) FROM dbo.[$taskTable]" -Scalar) -ne $taskRowsBefore[$taskTable]) { throw "$taskTable row count unexpectedly changed." }
    }
    if ([int](Invoke-LandedCostScopeSql "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='$taskMigrationId'" -Scalar) -ne 1) { throw 'Migration history verification failed.' }
    $taskTransaction.Commit()
    if ($taskApplied -eq 0) {
        'Applied line-specific landed-cost scope: two nullable columns, two indexes, two trusted foreign keys, and the matching EF migration-history entry. Existing rows preserved.'
    } else {
        'Already applied; verified both columns, indexes, foreign keys and migration history. No changes made.'
    }
} catch {
    try { $taskTransaction.Rollback() } catch {}
    throw
} finally { $taskTransaction.Dispose() }
