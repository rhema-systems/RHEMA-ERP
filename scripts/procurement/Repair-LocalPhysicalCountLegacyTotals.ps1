param(
    [ValidateSet('RhemaERP_PO_Rehearsal_20260909','RhemaERP')][string]$Database='RhemaERP_PO_Rehearsal_20260909',
    [switch]$Apply,
    [string]$ExpectedPreviewHash,
    [Guid]$MaintenanceActorUserId=[Guid]::Empty,
    [ValidateSet('PendingStoresApproval','PendingFinanceApproval','PendingAuditAttestation','ReadyToPost')]
    [string]$ExpectedRehearsalCountStatus='PendingStoresApproval',
    [Guid]$MainActiveCountId=[Guid]::Empty,
    [string]$ExpectedMainCountStatus,
    [string]$BackupDirectory='C:/Program Files/Microsoft SQL Server/MSSQL15.SQL2017/MSSQL/Backup'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
if ($Apply -and ($ExpectedPreviewHash -notmatch '^[A-Fa-f0-9]{64}$' -or $MaintenanceActorUserId -eq [Guid]::Empty)) {
    throw 'Apply requires the reviewed ExpectedPreviewHash and an explicit active MaintenanceActorUserId.'
}
$legacyGuard=@'
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017'
 OR DB_NAME()<>@database OR @database NOT IN(N'RhemaERP_PO_Rehearsal_20260909',N'RhemaERP')
 THROW 51000,'Unexpected legacy reconciliation server or database.',1;
DECLARE @tenant uniqueidentifier='00000000-0000-0000-0000-000000000001';
DECLARE @warehouse uniqueidentifier='39e1b1fb-17c8-41cb-a703-f0a6e740acc8';
IF NOT EXISTS(SELECT 1 FROM dbo.Warehouses WHERE Id=@warehouse AND TenantId=@tenant AND Code=N'DEMO-PM'
 AND IsDeleted=0 AND IsActive=1 AND IsConsignmentWarehouse=0)
 THROW 51000,'The exact owned Project Demo warehouse was not found.',1;
'@
$legacyScope=@'
IF OBJECT_ID(N'tempdb..#LegacyScope') IS NOT NULL DROP TABLE #LegacyScope;
IF OBJECT_ID(N'tempdb..#LegacyTargets') IS NOT NULL DROP TABLE #LegacyTargets;
CREATE TABLE #LegacyTargets(Id uniqueidentifier NOT NULL PRIMARY KEY,ItemCode nvarchar(100) NOT NULL);
INSERT #LegacyTargets VALUES
 ('00000008-0000-0000-0000-000000000001',N'OIL-5W30-5L'),
 ('00000008-0000-0000-0000-000000000002',N'FILTER-OIL-001'),
 ('00000008-0000-0000-0000-000000000003',N'FILTER-AIR-001'),
 ('00000008-0000-0000-0000-000000000004',N'FLUID-BRAKE-DOT4'),
 ('00000008-0000-0000-0000-000000000005',N'SPARK-PLUG-001'),
 ('00000008-0000-0000-0000-000000000006',N'TOOL-SCAN-001');
IF (SELECT COUNT(*) FROM #LegacyTargets t JOIN dbo.InventoryItems i ON i.Id=t.Id AND i.ItemCode=t.ItemCode
 AND i.TenantId=@tenant AND i.IsDeleted=0)<>6 THROW 51000,'The six exact legacy inventory items were not found.',1;
SELECT i.Id,i.ItemCode,i.CurrentStock BeforeCurrent,i.AllocatedStock BeforeAllocated,i.AvailableStock BeforeAvailable,
 ISNULL(wq.OwnedRows,0) OwnedWarehouseRows,ISNULL(wq.OutsideRows,0) OutsideWarehouseRows,
 wq.CurrentStock SourceWarehouseCurrent,wq.AllocatedStock SourceWarehouseAllocated,wq.AvailableStock SourceWarehouseAvailable,
 ISNULL(bins.LocationRows,0) LocationRows,bins.Quantity SourceBinCurrent,bins.AllocatedQuantity SourceBinAllocated,
 bins.AvailableQuantity SourceBinAvailable,
 pristine.AllWarehouseRows,pristine.AllLocationRows,pristine.BalanceRows,pristine.LayerRows,pristine.MovementRows,
 snapshot.SourceSnapshotRows,snapshot.SourceSnapshotQuantity,overlap.ActiveCountOverlap,
 CASE WHEN wq.OwnedRows=1 AND wq.OutsideRows=0 AND bins.LocationRows>0
  AND wq.CurrentStock=bins.Quantity AND wq.AllocatedStock=bins.AllocatedQuantity
  AND wq.AvailableStock=bins.AvailableQuantity
  AND wq.AvailableStock=wq.CurrentStock-wq.AllocatedStock
  AND wq.CurrentStock>=0 AND wq.AllocatedStock>=0 AND wq.AvailableStock>=0 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END CanApply
INTO #LegacyScope
FROM #LegacyTargets t JOIN dbo.InventoryItems i ON i.Id=t.Id AND i.TenantId=@tenant
OUTER APPLY(SELECT COUNT(*) OwnedRows,SUM(CASE WHEN q.WarehouseId<>@warehouse THEN 1 ELSE 0 END) OutsideRows,
 SUM(q.CurrentStock) CurrentStock,SUM(q.AllocatedStock) AllocatedStock,SUM(q.AvailableStock) AvailableStock
 FROM dbo.WarehouseQuantities q JOIN dbo.Warehouses w ON w.Id=q.WarehouseId AND w.TenantId=q.TenantId
 WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0 AND w.IsDeleted=0 AND w.IsConsignmentWarehouse=0) wq
OUTER APPLY(SELECT COUNT(*) LocationRows,SUM(q.Quantity) Quantity,SUM(q.AllocatedQuantity) AllocatedQuantity,SUM(q.AvailableQuantity) AvailableQuantity
 FROM dbo.InventoryLocations q JOIN dbo.WarehouseLocations l ON l.Id=q.LocationId AND l.TenantId=q.TenantId
 WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0 AND l.WarehouseId=@warehouse
 AND l.IsDeleted=0 AND l.IsActive=1 AND l.IsConsignmentBin=0) bins
OUTER APPLY(SELECT
 (SELECT COUNT(*) FROM dbo.WarehouseQuantities q WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0) AllWarehouseRows,
 (SELECT COUNT(*) FROM dbo.InventoryLocations q WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0) AllLocationRows,
 (SELECT COUNT(*) FROM dbo.InventoryBalances q WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0) BalanceRows,
 (SELECT COUNT(*) FROM dbo.InventoryLayers q WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0) LayerRows,
 (SELECT COUNT(*) FROM dbo.StockMovements q WHERE q.InventoryItemId=i.Id AND q.TenantId=@tenant AND q.IsDeleted=0) MovementRows) pristine
OUTER APPLY(SELECT COUNT(*) SourceSnapshotRows,MAX(ci.SystemQuantity) SourceSnapshotQuantity
 FROM dbo.PhysicalCounts c JOIN dbo.PhysicalCountItems ci ON ci.PhysicalCountId=c.Id AND ci.TenantId=c.TenantId
 WHERE c.Id='d53efdd4-0d2b-4214-9fe5-b334f01bd38b' AND c.CountNumber=N'PC-20260906-0001'
 AND c.TenantId=@tenant AND c.WarehouseId=@warehouse AND c.LocationId IS NULL AND c.Status=N'Cancelled'
 AND c.StockAdjustmentId IS NULL AND c.IsDeleted=0 AND ci.IsDeleted=0 AND ci.InventoryItemId=i.Id
 AND ci.LocationId IS NULL) snapshot
OUTER APPLY(SELECT COUNT(*) ActiveCountOverlap FROM dbo.PhysicalCounts c
 WHERE c.TenantId=@tenant AND c.IsDeleted=0 AND c.Status NOT IN(N'Cancelled',N'Posted')
 AND (c.WarehouseId=@warehouse OR EXISTS(SELECT 1 FROM dbo.PhysicalCountItems ci
  WHERE ci.PhysicalCountId=c.Id AND ci.TenantId=c.TenantId AND ci.InventoryItemId=i.Id AND ci.IsDeleted=0))) overlap;
ALTER TABLE #LegacyScope ADD SourceMode nvarchar(50) NULL;
UPDATE #LegacyScope SET SourceMode=CASE WHEN CanApply=1 THEN N'WarehouseBinAgreement' ELSE N'Unresolved' END;
-- Main UAT retained these six seeded warehouse balances without any bin/valuation/movement history.
-- This branch repairs the item aggregate only. Normal future count preparation assigns the default bin.
-- Rehearsal's original strict warehouse/bin agreement predicate above is unchanged.
IF DB_NAME()=N'RhemaERP'
 UPDATE #LegacyScope SET CanApply=1,SourceMode=N'PristineUnlocatedWarehouse'
 WHERE CanApply=0 AND OwnedWarehouseRows=1 AND OutsideWarehouseRows=0 AND AllWarehouseRows=1
  AND AllLocationRows=0 AND BalanceRows=0 AND LayerRows=0 AND MovementRows=0 AND ActiveCountOverlap=0
  AND SourceSnapshotRows=1 AND SourceSnapshotQuantity=SourceWarehouseCurrent
  AND SourceWarehouseCurrent>=0 AND SourceWarehouseAllocated>=0 AND SourceWarehouseAvailable>=0
  AND SourceWarehouseAvailable=SourceWarehouseCurrent-SourceWarehouseAllocated;
DECLARE @scopeJson nvarchar(max)=(SELECT * FROM #LegacyScope ORDER BY ItemCode FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @scopeHash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),DB_NAME()+N'|'+@scopeJson)),2);
'@
$legacyApply=@'
IF @scopeHash<>@expectedHash THROW 51000,'The reviewed reconciliation preview changed. Preview and review again.',1;
IF EXISTS(SELECT 1 FROM #LegacyScope WHERE CanApply=0) THROW 51000,'The reviewed warehouse/bin or exact main pristine-unlocated evidence was not verified.',1;
DECLARE @pristineMain bit=CASE WHEN DB_NAME()=N'RhemaERP'
 AND (SELECT COUNT(*) FROM #LegacyScope WHERE SourceMode=N'PristineUnlocatedWarehouse')=6 THEN 1 ELSE 0 END;
IF EXISTS(SELECT 1 FROM #LegacyScope WHERE SourceMode=N'PristineUnlocatedWarehouse') AND @pristineMain=0
 THROW 51000,'Pristine-unlocated reconciliation requires all six exact main-UAT items, never a mixed/rehearsal scope.',1;
IF @pristineMain=1 AND (@mainCountId<>'00000000-0000-0000-0000-000000000000' OR LEN(@expectedMainStatus)>0)
 THROW 51000,'Pristine-unlocated main repair cannot target or advance an active count.',1;
IF EXISTS(SELECT 1 FROM #LegacyScope WHERE BeforeCurrent=SourceWarehouseCurrent AND BeforeAllocated=SourceWarehouseAllocated
 AND BeforeAvailable=SourceWarehouseAvailable) THROW 51000,'Expected six unreconciled items; at least one has changed or was already reconciled.',1;
DECLARE @actorName nvarchar(255);
SELECT @actorName=u.UserName FROM dbo.Users u JOIN dbo.UserTenants ut ON ut.UserId=u.Id AND ut.TenantId=@tenant
WHERE u.Id=@actor AND u.IsActive=1 AND ut.IsDeleted=0 AND ut.Status=0 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt>SYSUTCDATETIME());
IF @actorName IS NULL THROW 51000,'Maintenance audit actor must be an explicitly supplied active member of this tenant.',1;
DECLARE @countId uniqueidentifier=CASE WHEN DB_NAME()=N'RhemaERP_PO_Rehearsal_20260909'
 THEN 'cf9c91fc-95be-4ce2-b56e-ae27932a89a8' WHEN @pristineMain=1 THEN NULL ELSE @mainCountId END;
IF DB_NAME()=N'RhemaERP_PO_Rehearsal_20260909' AND NOT EXISTS(
 SELECT 1 FROM dbo.PhysicalCounts c WHERE c.Id=@countId AND c.TenantId=@tenant AND c.WarehouseId=@warehouse
 AND c.CountNumber=N'PC-20260911-0004' AND c.Status=@expectedCountStatus AND c.IsDeleted=0
 AND c.StockAdjustmentId='44c7b4aa-21cd-4a21-8879-eb7174c209e0')
 THROW 51000,'The exact rehearsal count, status or linked adjustment changed.',1;
IF DB_NAME()=N'RhemaERP' AND @mainCountId<>'00000000-0000-0000-0000-000000000000' AND NOT EXISTS(
 SELECT 1 FROM dbo.PhysicalCounts c WHERE c.Id=@mainCountId AND c.TenantId=@tenant AND c.WarehouseId=@warehouse
 AND c.IsDeleted=0 AND c.Status=@expectedMainStatus)
 THROW 51000,'The explicitly reviewed main-UAT count or status was not found.',1;
IF EXISTS(SELECT 1 FROM dbo.PhysicalCounts c JOIN dbo.PhysicalCountItems ci ON ci.PhysicalCountId=c.Id AND ci.TenantId=c.TenantId
 JOIN #LegacyTargets t ON t.Id=ci.InventoryItemId WHERE c.TenantId=@tenant AND c.IsDeleted=0 AND ci.IsDeleted=0
 AND c.FreezeInventory=1 AND c.FreezeStartedAtUtc IS NOT NULL AND c.FreezeReleasedAtUtc IS NULL
 AND c.Status IN(N'InProgress',N'UnderReview',N'UnderInvestigation',N'RecountRequired',N'PendingStoresApproval',N'PendingFinanceApproval',N'PendingAuditAttestation',N'ReadyToPost')
 AND c.Id<>@countId) THROW 51000,'Another active count covers these items; its source scope was not approved for this repair.',1;
IF EXISTS(SELECT 1 FROM dbo.StockAdjustmentItems ai JOIN dbo.StockAdjustments a ON a.Id=ai.AdjustmentId AND a.TenantId=ai.TenantId
 JOIN #LegacyTargets t ON t.Id=ai.InventoryItemId WHERE a.TenantId=@tenant AND a.IsDeleted=0 AND ai.IsDeleted=0
 AND a.Status=N'Posted' AND a.Id='44c7b4aa-21cd-4a21-8879-eb7174c209e0')
 THROW 51000,'The rehearsal adjustment has already posted; this pre-post reconciliation is not applicable.',1;
DECLARE @freezeId int=OBJECT_ID(N'dbo.TR_InventoryItems_PhysicalCountFreeze');
IF @freezeId IS NULL OR NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=@freezeId AND parent_id=OBJECT_ID(N'dbo.InventoryItems') AND is_disabled=0)
 THROW 51000,'The exact enabled item count-freeze trigger was not found.',1;
IF OBJECT_DEFINITION(@freezeId) NOT LIKE N'%INV_COUNT_ITEM_FROZEN%' OR OBJECT_DEFINITION(@freezeId) NOT LIKE N'%StockAdjustmentItems%'
 THROW 51000,'Unexpected count-freeze trigger definition; review before maintenance.',1;
SELECT object_id,name,is_disabled,HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(object_id))) DefinitionHash
INTO #LegacyTriggerState FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.InventoryItems');
IF NOT EXISTS(SELECT 1 FROM #LegacyTriggerState WHERE name=N'TR_InventoryItems_NegativeStockGuard' AND is_disabled=0)
 THROW 51000,'The negative-stock guard must remain enabled.',1;
DECLARE @countsBefore nvarchar(max)=(SELECT c.* FROM dbo.PhysicalCounts c WHERE c.TenantId=@tenant ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @countLinesBefore nvarchar(max)=(SELECT c.* FROM dbo.PhysicalCountItems c WHERE c.TenantId=@tenant ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @warehouseBefore nvarchar(max)=(SELECT q.* FROM dbo.WarehouseQuantities q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @binsBefore nvarchar(max)=(SELECT q.* FROM dbo.InventoryLocations q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @valuationBefore nvarchar(max)=(SELECT q.* FROM dbo.InventoryBalances q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @layersBefore nvarchar(max)=(SELECT q.* FROM dbo.InventoryLayers q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @movementsBefore nvarchar(max)=(SELECT q.* FROM dbo.StockMovements q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @journalBefore nvarchar(max)=(SELECT q.* FROM dbo.JournalEntries q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @countActionsBefore nvarchar(max)=(SELECT q.* FROM dbo.PhysicalCountActions q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @adjustmentsBefore nvarchar(max)=(SELECT q.* FROM dbo.StockAdjustments q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @approvalsBefore nvarchar(max)=(SELECT q.* FROM dbo.WorkflowApprovals q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
DECLARE @othersBefore nvarchar(max)=(SELECT i.* FROM dbo.InventoryItems i WHERE NOT EXISTS(SELECT 1 FROM #LegacyTargets t WHERE t.Id=i.Id) ORDER BY i.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
-- The exclusive owner-table lock was acquired before preview validation. No concurrent item write can
-- run while rehearsal's one freeze trigger is disabled. All other inventory guards remain enabled.
-- Main pristine-unlocated has no active count overlap: its freeze trigger stays enabled throughout.
IF @pristineMain=0 DISABLE TRIGGER dbo.TR_InventoryItems_PhysicalCountFreeze ON dbo.InventoryItems;
UPDATE i SET CurrentStock=s.SourceWarehouseCurrent,AllocatedStock=s.SourceWarehouseAllocated,AvailableStock=s.SourceWarehouseAvailable
FROM dbo.InventoryItems i JOIN #LegacyScope s ON s.Id=i.Id WHERE i.TenantId=@tenant AND i.IsDeleted=0
 AND i.CurrentStock=s.BeforeCurrent AND i.AllocatedStock=s.BeforeAllocated AND i.AvailableStock=s.BeforeAvailable;
IF @@ROWCOUNT<>6 THROW 51000,'Reconciliation did not update exactly six reviewed item aggregates.',1;
IF @pristineMain=0 ENABLE TRIGGER dbo.TR_InventoryItems_PhysicalCountFreeze ON dbo.InventoryItems;
IF EXISTS(SELECT 1 FROM #LegacyTriggerState b FULL JOIN sys.triggers a ON a.object_id=b.object_id AND a.parent_id=OBJECT_ID(N'dbo.InventoryItems')
 WHERE (b.object_id IS NOT NULL OR a.parent_id=OBJECT_ID(N'dbo.InventoryItems')) AND
 (a.object_id IS NULL OR b.object_id IS NULL OR a.is_disabled<>b.is_disabled OR HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(a.object_id)))<>b.DefinitionHash))
 THROW 51000,'An item trigger definition or enabled state changed unexpectedly.',1;
IF EXISTS(SELECT 1 FROM #LegacyScope s JOIN dbo.InventoryItems i ON i.Id=s.Id
 WHERE i.CurrentStock<>s.SourceWarehouseCurrent OR i.AllocatedStock<>s.SourceWarehouseAllocated OR i.AvailableStock<>s.SourceWarehouseAvailable)
 THROW 51000,'Aggregate postconditions failed.',1;
IF @countsBefore<>(SELECT c.* FROM dbo.PhysicalCounts c WHERE c.TenantId=@tenant ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @countLinesBefore<>(SELECT c.* FROM dbo.PhysicalCountItems c WHERE c.TenantId=@tenant ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @warehouseBefore<>(SELECT q.* FROM dbo.WarehouseQuantities q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @binsBefore<>(SELECT q.* FROM dbo.InventoryLocations q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @valuationBefore<>(SELECT q.* FROM dbo.InventoryBalances q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @layersBefore<>(SELECT q.* FROM dbo.InventoryLayers q JOIN #LegacyTargets t ON t.Id=q.InventoryItemId WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @movementsBefore<>(SELECT q.* FROM dbo.StockMovements q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @journalBefore<>(SELECT q.* FROM dbo.JournalEntries q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @countActionsBefore<>(SELECT q.* FROM dbo.PhysicalCountActions q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @adjustmentsBefore<>(SELECT q.* FROM dbo.StockAdjustments q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @approvalsBefore<>(SELECT q.* FROM dbo.WorkflowApprovals q WHERE q.TenantId=@tenant ORDER BY q.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 OR @othersBefore<>(SELECT i.* FROM dbo.InventoryItems i WHERE NOT EXISTS(SELECT 1 FROM #LegacyTargets t WHERE t.Id=i.Id) ORDER BY i.Id FOR JSON PATH,INCLUDE_NULL_VALUES)
 THROW 51000,'A preserved count, source balance, valuation or unrelated item changed.',1;
INSERT dbo.AuditLogs(Id,UserId,Username,Action,Resource,ResourceId,OldValues,NewValues,IpAddress,UserAgent,Timestamp,TenantId,CreatedAt,CreatedBy,IsDeleted)
SELECT NEWID(),@actor,@actorName,N'Inventory.LegacyAggregateReconciled',N'InventoryItem',CONVERT(nvarchar(36),s.Id),
 (SELECT s.BeforeCurrent CurrentStock,s.BeforeAllocated AllocatedStock,s.BeforeAvailable AvailableStock FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT s.SourceWarehouseCurrent CurrentStock,s.SourceWarehouseAllocated AllocatedStock,s.SourceWarehouseAvailable AvailableStock,
  @warehouse WarehouseId,@countId CountId,@scopeHash ReviewedPreviewHash,@backupPath VerifiedCopyOnlyBackup,
  s.SourceMode SourceMode,CASE WHEN @pristineMain=1 THEN N'PC-20260906-0001' END RetainedCancelledSnapshot,
  CASE WHEN @pristineMain=1 THEN N'One owned warehouse; no bin, valuation, layer or movement rows; retained cancelled count agrees; no active count overlap'
   ELSE N'Owned warehouse and existing bin totals agree' END SourceEvidence,
  N'User-authorized Codex local maintenance; derived aggregate only; no count, stock movement or GL posting' MaintenanceReason,
  CAST(1 AS bit) NegativeStockGuardPreserved,CAST(1 AS bit) FreezeTriggerRestored,@pristineMain FreezeTriggerKeptEnabled FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 N'127.0.0.1',N'Codex authorized local maintenance script; not an operational approval or posting',SYSUTCDATETIME(),@tenant,SYSUTCDATETIME(),ORIGINAL_LOGIN(),0
FROM #LegacyScope s;
IF @@ROWCOUNT<>6 THROW 51000,'Expected six reconciliation audit records.',1;
SELECT DB_NAME() DatabaseName,@scopeHash ReviewedPreviewHash,6 ReconciledItems,@backupPath VerifiedCopyOnlyBackup,
 CAST(1 AS bit) NegativeStockGuardPreserved,CAST(1 AS bit) FreezeTriggerRestored,@pristineMain FreezeTriggerKeptEnabled,
 CASE WHEN @pristineMain=1 THEN N'PristineUnlocatedWarehouse' ELSE N'WarehouseBinAgreement' END SourceMode;
'@
$legacyConnection=[System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;Application Name=AuthorizedLegacyInventoryReconciliation")
try {
    $legacyConnection.Open()
    $legacyCommand=$legacyConnection.CreateCommand()
    $legacyCommand.CommandTimeout=180
    [void]$legacyCommand.Parameters.AddWithValue('@database',$Database)
    $legacyCommand.CommandText=$legacyGuard+$legacyScope+"`nSELECT DB_NAME() DatabaseName,@scopeHash PreviewHash,@scopeJson PreviewJson;"
    $legacyPreview=[System.Data.DataTable]::new()
    $legacyAdapter=[System.Data.SqlClient.SqlDataAdapter]::new($legacyCommand)
    [void]$legacyAdapter.Fill($legacyPreview)
    [pscustomobject]@{Database=$Database;PreviewHash=$legacyPreview.Rows[0].PreviewHash;Items=($legacyPreview.Rows[0].PreviewJson | ConvertFrom-Json);Applied=$false}
    if (-not $Apply) { return }
    if ($legacyPreview.Rows[0].PreviewHash -ne $ExpectedPreviewHash) { throw 'The supplied preview hash does not match this database current evidence.' }
    if (($legacyPreview.Rows[0].PreviewJson | ConvertFrom-Json | Where-Object { -not $_.CanApply }).Count -gt 0) { throw 'Preview contains unresolved source evidence; no repair was applied.' }
    if (-not (Test-Path -LiteralPath $BackupDirectory -PathType Container)) { throw 'The SQL Server backup directory does not exist.' }
    $legacyBackupPath=Join-Path ([IO.Path]::GetFullPath($BackupDirectory)) ($Database+'_before_legacy_aggregate_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff')+'.bak')
    [void]$legacyCommand.Parameters.AddWithValue('@backupPath',$legacyBackupPath)
    $legacyCommand.CommandText=$legacyGuard+"`nBACKUP DATABASE [$Database] TO DISK=@backupPath WITH COPY_ONLY,CHECKSUM,COMPRESSION; RESTORE VERIFYONLY FROM DISK=@backupPath WITH CHECKSUM;"
    [void]$legacyCommand.ExecuteNonQuery()
    Write-Output "Verified COPY_ONLY backup: $legacyBackupPath"
    $legacyTransaction=$legacyConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    try {
        $legacyCommand.Transaction=$legacyTransaction
        [void]$legacyCommand.Parameters.AddWithValue('@expectedHash',$ExpectedPreviewHash)
        [void]$legacyCommand.Parameters.AddWithValue('@actor',$MaintenanceActorUserId)
        [void]$legacyCommand.Parameters.AddWithValue('@expectedCountStatus',$ExpectedRehearsalCountStatus)
        [void]$legacyCommand.Parameters.AddWithValue('@mainCountId',$MainActiveCountId)
        [void]$legacyCommand.Parameters.AddWithValue('@expectedMainStatus',$(if($ExpectedMainCountStatus){$ExpectedMainCountStatus}else{''}))
        $legacyCommand.CommandText=@'
SET XACT_ABORT ON;
DECLARE @lockResult int;
EXEC @lockResult=sys.sp_getapplock @Resource=N'local-physical-count-legacy-aggregate-reconciliation',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
IF @lockResult<0 THROW 51000,'Could not acquire exclusive local maintenance ownership.',1;
DECLARE @lockedItem uniqueidentifier;
SELECT TOP(1) @lockedItem=Id FROM dbo.InventoryItems WITH(TABLOCKX,HOLDLOCK);
'@+$legacyGuard+$legacyScope+$legacyApply
        $legacyResult=[System.Data.DataTable]::new()
        $legacyApplyAdapter=[System.Data.SqlClient.SqlDataAdapter]::new($legacyCommand)
        [void]$legacyApplyAdapter.Fill($legacyResult)
        $legacyTransaction.Commit()
        $legacyResult | Format-List
        Write-Output 'Committed only six item aggregate reconciliations and six audit records. Count, bins, warehouse quantities and valuation were preserved.'
    } catch {
        try { $legacyTransaction.Rollback() } catch { }
        throw
    } finally { $legacyTransaction.Dispose() }
} finally { if($legacyCommand){$legacyCommand.Dispose()}; $legacyConnection.Dispose() }
