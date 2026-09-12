param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$reviewRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$reviewMigration = Get-Content -LiteralPath (Join-Path $reviewRepo 'src/ErpSystem.Data/Migrations/20260911210000_PhysicalCountReviewDecisions.cs') -Raw
$reviewMatch = [regex]::Match($reviewMigration, '(?s)public const string UpgradeSql = """\r?\n(.*?)\r?\n\s*""";')
if (-not $reviewMatch.Success) { throw 'Migration SQL not found.' }
$reviewSql = $reviewMatch.Groups[1].Value
$reviewFixture = @'
SET XACT_ABORT ON;
IF DB_NAME()<>N'RhemaERP_PO_Rehearsal_20260909' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017'
    THROW 51000, 'Unexpected test target.', 1;
DECLARE @tenant uniqueidentifier,@warehouse uniqueidentifier,@actor uniqueidentifier,@inventoryItem uniqueidentifier;
SELECT TOP(1) @tenant=p.TenantId,@warehouse=p.WarehouseId,@actor=p.InitiatedById,@inventoryItem=i.InventoryItemId
FROM dbo.PhysicalCounts p JOIN dbo.PhysicalCountItems i ON i.PhysicalCountId=p.Id AND i.TenantId=p.TenantId AND i.IsDeleted=0
JOIN dbo.Users u ON u.Id=p.InitiatedById AND u.TenantId=p.TenantId AND u.IsActive=1
WHERE p.Status=N'Posted' AND p.IsDeleted=0 ORDER BY p.CountNumber,i.ItemCode;
IF @tenant IS NULL THROW 51000, 'No fixture reference available.', 1;
INSERT dbo.PhysicalCounts
(Id,CountNumber,CountType,WarehouseId,CountDate,Status,FreezeInventory,IncludeZeroStock,BlindCount,InitiatedById,
 TotalItems,CountedItems,VarianceItems,TotalSystemQuantity,TotalCountedQuantity,TotalVarianceQuantity,TotalVarianceValue,CreatedAt,IsDeleted,TenantId)
VALUES(@countId,N'TEST-REVIEW-'+CONVERT(nvarchar(36),@countId),0,@warehouse,SYSUTCDATETIME(),N'Draft',1,0,1,@actor,1,0,0,10,0,0,0,SYSUTCDATETIME(),0,@tenant);
INSERT dbo.PhysicalCountItems
(Id,PhysicalCountId,InventoryItemId,ItemCode,SystemQuantity,CountedQuantity,VarianceQuantity,UnitCost,VarianceValue,
 IsCounted,CountAttempts,RequiresRecount,CreatedAt,IsDeleted,TenantId)
VALUES(@itemId,@countId,@inventoryItem,N'TEST-REVIEW-LINE',10,0,0,1,0,0,0,0,SYSUTCDATETIME(),0,@tenant);
INSERT dbo.PhysicalCountActions
(Id,PhysicalCountId,Sequence,ActionType,ActorUserId,ActorRole,OccurredAtUtc,IdempotencyKey,PayloadHash,CorrelationId,SnapshotJson,IntegrityHash,CreatedAt,IsDeleted,TenantId)
VALUES(NEWID(),@countId,1,3,@actor,N'Counter',SYSUTCDATETIME(),N'fixture-start',REPLICATE('0',64),N'rollback-test',N'{}',REPLICATE('0',64),SYSUTCDATETIME(),0,@tenant),
(NEWID(),@countId,2,4,@actor,N'Counter',SYSUTCDATETIME(),N'fixture-count',REPLICATE('0',64),N'rollback-test',N'{}',REPLICATE('0',64),SYSUTCDATETIME(),0,@tenant);
UPDATE dbo.PhysicalCounts SET Status=N'InProgress',CountedById=@actor,StartedDate=SYSUTCDATETIME(),FreezeStartedAtUtc=SYSUTCDATETIME() WHERE Id=@countId;
UPDATE dbo.PhysicalCountItems SET CountedQuantity=10,FirstCountQuantity=10,IsCounted=1,CountAttempts=1,CountedById=@actor WHERE Id=@itemId;
'@
$reviewAction = @'
INSERT dbo.PhysicalCountActions
(Id,PhysicalCountId,Sequence,ActionType,ActorUserId,ActorRole,OccurredAtUtc,IdempotencyKey,PayloadHash,CorrelationId,SnapshotJson,IntegrityHash,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,3,14,CountedById,N'Counter',SYSUTCDATETIME(),N'fixture-review',REPLICATE('0',64),N'rollback-test',N'{}',REPLICATE('0',64),SYSUTCDATETIME(),0,TenantId FROM dbo.PhysicalCounts WHERE Id=@countId;
UPDATE dbo.PhysicalCounts SET Status=N'UnderReview' WHERE Id=@countId;
'@
$reviewSubmit = @'
INSERT dbo.PhysicalCountActions
(Id,PhysicalCountId,Sequence,ActionType,ActorUserId,ActorRole,OccurredAtUtc,IdempotencyKey,PayloadHash,CorrelationId,SnapshotJson,IntegrityHash,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,4,7,CountedById,N'Counter',SYSUTCDATETIME(),N'fixture-submit',REPLICATE('0',64),N'rollback-test',N'{}',REPLICATE('0',64),SYSUTCDATETIME(),0,TenantId FROM dbo.PhysicalCounts WHERE Id=@countId;
UPDATE dbo.PhysicalCounts SET Status=N'PendingStoresApproval' WHERE Id=@countId;
'@
$reviewInvestigate = @'
INSERT dbo.PhysicalCountActions
(Id,PhysicalCountId,Sequence,ActionType,ActorUserId,ActorRole,OccurredAtUtc,IdempotencyKey,PayloadHash,CorrelationId,SnapshotJson,IntegrityHash,CreatedAt,IsDeleted,TenantId)
SELECT NEWID(),Id,5,11,CountedById,N'Fixture',SYSUTCDATETIME(),N'fixture-investigate',REPLICATE('0',64),N'rollback-test',N'{}',REPLICATE('0',64),SYSUTCDATETIME(),0,TenantId FROM dbo.PhysicalCounts WHERE Id=@countId;
UPDATE dbo.PhysicalCounts SET Status=N'UnderInvestigation' WHERE Id=@countId;
'@
# Fixture actions intentionally use synthetic hashes; application tests separately exercise signatures, permissions and independent approvers.
$reviewCases = @(
    @{Name='Review requires audit action';Setup='';Sql="UPDATE dbo.PhysicalCounts SET Status=N'UnderReview' WHERE Id=@countId";Error=51935},
    @{Name='Counter can review';Setup='';Sql=$reviewAction;Error=0},
    @{Name='Counter can correct in review';Setup=$reviewAction;Sql="UPDATE dbo.PhysicalCountItems SET CountedQuantity=11,VarianceQuantity=1,VarianceValue=1 WHERE Id=@itemId";Error=0},
    @{Name='Original first count cannot be rewritten';Setup=$reviewAction;Sql='UPDATE dbo.PhysicalCountItems SET FirstCountQuantity=11 WHERE Id=@itemId';Error=51923},
    @{Name='System snapshot cannot be rewritten';Setup=$reviewAction;Sql='UPDATE dbo.PhysicalCountItems SET SystemQuantity=11 WHERE Id=@itemId';Error=51923},
    @{Name='Review cannot skip submission';Setup=$reviewAction;Sql="UPDATE dbo.PhysicalCounts SET Status=N'PendingStoresApproval' WHERE Id=@countId";Error=51935},
    @{Name='Reviewed zero-variance count can submit';Setup=$reviewAction;Sql=$reviewSubmit;Error=0},
    @{Name='Submitted quantities are locked';Setup=$reviewAction+$reviewSubmit;Sql='UPDATE dbo.PhysicalCountItems SET CountedQuantity=11 WHERE Id=@itemId';Error=51924},
    @{Name='Decision can enter investigation';Setup=$reviewAction+$reviewSubmit;Sql=$reviewInvestigate;Error=0},
    @{Name='Investigation quantities stay locked';Setup=$reviewAction+$reviewSubmit+$reviewInvestigate;Sql='UPDATE dbo.PhysicalCountItems SET CountedQuantity=11 WHERE Id=@itemId';Error=51924},
    @{Name='Investigation cannot post';Setup=$reviewAction+$reviewSubmit+$reviewInvestigate;Sql="UPDATE dbo.PhysicalCounts SET Status=N'Posted' WHERE Id=@countId";Error=51935},
    @{Name='Investigation cannot resume with a stale review event';Setup=$reviewAction+$reviewSubmit+$reviewInvestigate;Sql="UPDATE dbo.PhysicalCounts SET Status=N'UnderReview' WHERE Id=@countId";Error=51935},
    @{Name='Review audit cannot be deleted';Setup=$reviewAction;Sql='DELETE dbo.PhysicalCountActions WHERE PhysicalCountId=@countId AND ActionType=14';Error=51911}
)
$reviewConnection = [System.Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP_PO_Rehearsal_20260909;Integrated Security=True;Application Name=PhysicalCountReviewRollbackTests')
try {
    $reviewConnection.Open()
    foreach ($reviewCase in $reviewCases) {
        $reviewTransaction=$reviewConnection.BeginTransaction()
        $reviewCommand=$reviewConnection.CreateCommand()
        $reviewCommand.Transaction=$reviewTransaction
        $reviewCommand.CommandTimeout=60
        try {
            $reviewCommand.CommandText="IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260911210000_PhysicalCountReviewDecisions') BEGIN $reviewSql END"
            [void]$reviewCommand.ExecuteNonQuery()
            [void]$reviewCommand.Parameters.AddWithValue('@countId',[Guid]::NewGuid())
            [void]$reviewCommand.Parameters.AddWithValue('@itemId',[Guid]::NewGuid())
            $reviewCommand.CommandText=$reviewFixture
            [void]$reviewCommand.ExecuteNonQuery()
            if ($reviewCase.Setup) { $reviewCommand.CommandText=$reviewCase.Setup; [void]$reviewCommand.ExecuteNonQuery() }
            $reviewCommand.CommandText=$reviewCase.Sql
            $reviewError=0
            try { [void]$reviewCommand.ExecuteNonQuery() } catch [System.Data.SqlClient.SqlException] { $reviewError=$_.Exception.Number }
            if ($reviewError -ne $reviewCase.Error) { throw "$($reviewCase.Name): expected $($reviewCase.Error), got $reviewError" }
            [pscustomobject]@{Test=$reviewCase.Name;Passed=$true;RolledBack=$true}
        } finally {
            try { $reviewTransaction.Rollback() } catch { }
            $reviewCommand.Dispose(); $reviewTransaction.Dispose()
        }
    }
} finally { $reviewConnection.Dispose() }
