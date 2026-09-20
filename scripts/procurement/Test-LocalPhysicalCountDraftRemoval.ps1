param(
    [Parameter(Mandatory)]
    [ValidateSet('RhemaERP', 'RhemaERP_PO_Rehearsal_20260909')]
    [string]$Database,
    [switch]$BeforeFix
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$countConnection = [System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;Application Name=PhysicalCountDraftRemovalRegression")
$countFixtureSql = @'
SET XACT_ABORT ON;
IF DB_NAME() NOT IN (N'RhemaERP',N'RhemaERP_PO_Rehearsal_20260909')
   OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017'
    THROW 51000, 'Unexpected test target.', 1;
DECLARE @tenant uniqueidentifier, @warehouse uniqueidentifier, @actor uniqueidentifier,
        @inventoryItem uniqueidentifier, @unitCost decimal(18,4), @systemQty decimal(18,4);
SELECT TOP(1) @tenant=p.TenantId,@warehouse=p.WarehouseId,@actor=p.InitiatedById,
              @inventoryItem=i.InventoryItemId,@unitCost=i.UnitCost,@systemQty=i.SystemQuantity
FROM dbo.PhysicalCounts p
JOIN dbo.PhysicalCountItems i ON i.PhysicalCountId=p.Id AND i.TenantId=p.TenantId AND i.IsDeleted=0
JOIN dbo.Warehouses w ON w.Id=p.WarehouseId AND w.TenantId=p.TenantId AND w.IsActive=1 AND w.IsDeleted=0
JOIN dbo.InventoryItems item ON item.Id=i.InventoryItemId AND item.TenantId=i.TenantId AND item.IsDeleted=0
WHERE p.IsDeleted=0 AND p.Status=N'Posted'
ORDER BY p.CountNumber,i.ItemCode;
IF @tenant IS NULL OR @actor IS NULL THROW 51000, 'No safe source for the rolled-back test fixture.', 1;
INSERT dbo.PhysicalCounts
(Id,CountNumber,CountType,WarehouseId,CountDate,Status,FreezeInventory,IncludeZeroStock,BlindCount,
 InitiatedById,TotalItems,CountedItems,VarianceItems,TotalSystemQuantity,TotalCountedQuantity,
 TotalVarianceQuantity,TotalVarianceValue,CreatedAt,IsDeleted,TenantId)
VALUES
(@countId,N'TEST-DRAFT-'+CONVERT(nvarchar(36),@countId),0,@warehouse,SYSUTCDATETIME(),N'Draft',0,0,0,
 @actor,1,0,0,@systemQty,0,0,0,SYSUTCDATETIME(),0,@tenant);
INSERT dbo.PhysicalCountItems
(Id,PhysicalCountId,InventoryItemId,ItemCode,SystemQuantity,CountedQuantity,VarianceQuantity,
 UnitCost,VarianceValue,IsCounted,CountAttempts,RequiresRecount,CreatedAt,IsDeleted,TenantId)
VALUES
(@itemId,@countId,@inventoryItem,N'TEST-DRAFT-LINE',@systemQty,0,0,@unitCost,0,0,0,0,SYSUTCDATETIME(),0,@tenant);
'@
$countCases = @(
    @{ Name='Draft removal'; Error=$(if ($BeforeFix) { 51923 } else { 0 }); Sql=@'
UPDATE dbo.PhysicalCountItems SET IsDeleted=1,DeletedAt=SYSUTCDATETIME() WHERE Id=@itemId;
UPDATE dbo.PhysicalCounts SET TotalItems=TotalItems-1 WHERE Id=@countId;
IF NOT EXISTS(SELECT 1 FROM dbo.PhysicalCountItems WHERE Id=@itemId AND IsDeleted=1)
    OR NOT EXISTS(SELECT 1 FROM dbo.PhysicalCounts WHERE Id=@countId AND TotalItems=0)
    THROW 51000, 'Draft removal did not persist in the transaction.', 1;
'@ },
    @{ Name='Draft cost protection'; Error=51923; Sql='UPDATE dbo.PhysicalCountItems SET UnitCost=UnitCost+1 WHERE Id=@itemId;' },
    @{ Name='Draft snapshot protection'; Error=51923; Sql='UPDATE dbo.PhysicalCountItems SET SystemQuantity=SystemQuantity+1 WHERE Id=@itemId;' },
    @{ Name='Draft tenant foreign-key protection'; Error=547; Sql='UPDATE dbo.PhysicalCountItems SET TenantId=NEWID() WHERE Id=@itemId;' },
    @{ Name='First-count action protection'; Error=51924; Sql='UPDATE dbo.PhysicalCountItems SET CountedQuantity=1,IsCounted=1 WHERE Id=@itemId;' },
    @{ Name='Recount action protection'; Error=51925; Sql="UPDATE dbo.PhysicalCountItems SET RecountedQuantity=1,InvestigationNotes=N'TEST' WHERE Id=@itemId;" },
    @{ Name='Started removal protection'; Error=51923; Sql=@'
UPDATE i SET IsDeleted=1 FROM dbo.PhysicalCountItems i
WHERE i.Id=(SELECT TOP(1) x.Id FROM dbo.PhysicalCountItems x JOIN dbo.PhysicalCounts p ON p.Id=x.PhysicalCountId WHERE p.Status=N'Posted' AND x.IsDeleted=0 ORDER BY x.Id);
'@ },
    @{ Name='Started physical deletion protection'; Error=51921; Sql=@'
DELETE i FROM dbo.PhysicalCountItems i
WHERE i.Id=(SELECT TOP(1) x.Id FROM dbo.PhysicalCountItems x JOIN dbo.PhysicalCounts p ON p.Id=x.PhysicalCountId WHERE p.Status=N'Posted' AND x.IsDeleted=0 ORDER BY x.Id);
'@ },
    @{ Name='Mixed draft/started removal protection'; Error=51923; Sql=@'
UPDATE i SET IsDeleted=1 FROM dbo.PhysicalCountItems i
WHERE i.Id=@itemId OR i.Id=(SELECT TOP(1) x.Id FROM dbo.PhysicalCountItems x JOIN dbo.PhysicalCounts p ON p.Id=x.PhysicalCountId WHERE p.Status=N'Posted' AND x.IsDeleted=0 ORDER BY x.Id);
'@ }
)
if (-not $BeforeFix) {
    $countCases += @{ Name='Deleted-line restoration protection'; Error=51923; Sql='UPDATE dbo.PhysicalCountItems SET IsDeleted=1 WHERE Id=@itemId; UPDATE dbo.PhysicalCountItems SET IsDeleted=0 WHERE Id=@itemId;' }
}
try {
    $countConnection.Open()
    foreach ($countCase in $countCases) {
        $countTransaction = $countConnection.BeginTransaction()
        $countCommand = $countConnection.CreateCommand()
        $countCommand.Transaction = $countTransaction
        $countCommand.CommandTimeout = 30
        [void]$countCommand.Parameters.AddWithValue('@countId',[Guid]::NewGuid())
        [void]$countCommand.Parameters.AddWithValue('@itemId',[Guid]::NewGuid())
        try {
            $countCommand.CommandText = $countFixtureSql
            [void]$countCommand.ExecuteNonQuery()
            $countCommand.CommandText = $countCase.Sql
            $countActualError = 0
            try { [void]$countCommand.ExecuteNonQuery() }
            catch [System.Data.SqlClient.SqlException] { $countActualError = $_.Exception.Number }
            if ($countActualError -ne $countCase.Error) {
                throw "$Database / $($countCase.Name): expected SQL error $($countCase.Error), got $countActualError."
            }
            [pscustomobject]@{Database=$Database;Test=$countCase.Name;Passed=$true;ExpectedSqlError=$countCase.Error;RolledBack=$true}
        } finally {
            try { $countTransaction.Rollback() } catch { }
            $countCommand.Dispose()
            $countTransaction.Dispose()
        }
    }
} finally { $countConnection.Dispose() }
