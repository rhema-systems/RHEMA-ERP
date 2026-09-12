param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$valuationRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$valuationMigrationId = '20260912013000_AlignStockAdjustmentLocationValuation'
$valuationMigration = Get-Content -LiteralPath (Join-Path $valuationRepo "src/ErpSystem.Data/Migrations/$valuationMigrationId.cs") -Raw
$valuationMatch = [regex]::Match($valuationMigration, '(?s)public const string UpgradeSql = """\r?\n(.*?)\r?\n\s*""";')
if (-not $valuationMatch.Success) { throw 'Valuation migration UpgradeSql was not found.' }
$valuationSql = $valuationMatch.Groups[1].Value
$valuationGuard = @'
IF DB_NAME() <> N'RhemaERP_PO_Rehearsal_20260909'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) <> N'RHEMA-MICHAEL\SQL2017'
 THROW 51000, 'Unexpected rollback-test database or server. Main UAT is not an allowed target.', 1;
'@
$valuationFixture = @'
SET XACT_ABORT ON;
DECLARE @tenant uniqueidentifier, @warehouse uniqueidentifier, @location uniqueidentifier,
 @item uniqueidentifier, @actor uniqueidentifier, @localCost decimal(18,4), @globalCost decimal(18,4);
SELECT TOP (1) @tenant=i.TenantId, @warehouse=b.WarehouseId, @location=b.LocationId,
 @item=i.Id, @localCost=b.AverageUnitCost, @globalCost=i.AverageCost
FROM dbo.InventoryItems i
JOIN dbo.InventoryBalances b ON b.InventoryItemId=i.Id AND b.TenantId=i.TenantId AND b.IsDeleted=0
JOIN dbo.WarehouseLocations l ON l.Id=b.LocationId AND l.TenantId=i.TenantId AND l.IsDeleted=0 AND l.IsActive=1
JOIN dbo.Warehouses w ON w.Id=b.WarehouseId AND w.TenantId=i.TenantId AND w.IsDeleted=0 AND w.IsActive=1
WHERE i.ItemCode=N'SKU-001' AND i.IsDeleted=0 AND i.ValuationMethod=2
 AND l.LocationCode=N'LOC-001' AND l.IsConsignmentBin=0 AND w.IsConsignmentWarehouse=0
ORDER BY b.CreatedAt, b.Id;
IF @item IS NULL THROW 51000, 'SKU-001 / LOC-001 FIFO increase fixture is unavailable.', 1;
IF @localCost<>1918.85 OR @globalCost<>1907.09
 THROW 51000, 'The known rehearsal valuation fixture changed; review it before updating this test.', 1;
SELECT TOP (1) @actor=u.Id FROM dbo.Users u JOIN dbo.UserTenants ut ON ut.UserId=u.Id
WHERE ut.TenantId=@tenant AND ut.IsDeleted=0 AND ut.Status=0 AND u.IsActive=1
 AND (ut.ExpiresAt IS NULL OR ut.ExpiresAt>SYSUTCDATETIME()) ORDER BY u.Id;
IF @actor IS NULL THROW 51000, 'No active actor exists in the rehearsal fixture tenant.', 1;
INSERT #ValuationFixture VALUES(@tenant,@warehouse,@location,@item,@localCost,@globalCost);
INSERT dbo.StockAdjustments
 (Id,AdjustmentNumber,WarehouseId,Reference,AdjustmentDate,ReasonCode,Description,Status,RequestedById,
 TotalAdjustmentValue,IdempotencyKey,PayloadHash,CorrelationId,IntegrityHash,BookClassification,CreatedAt,IsDeleted,TenantId)
VALUES
 (@adjustmentId,N'TEST-VAL-'+CONVERT(nvarchar(36),@adjustmentId),@warehouse,N'ROLLBACK-ONLY',SYSUTCDATETIME(),
 CASE WHEN @opening=1 THEN N'INITIAL_STOCK' ELSE N'PHYSICAL_COUNT' END,N'Rollback-only SQL valuation regression',N'Draft',@actor,0,
 CASE WHEN @opening=1 THEN N'OPENING-STOCK:' ELSE N'valuation-test:' END+CONVERT(nvarchar(36),@adjustmentId),
 REPLICATE('0',64),CASE WHEN @opening=1 THEN N'opening-stock:' ELSE N'valuation-test:' END+CONVERT(nvarchar(36),@adjustmentId),
 REPLICATE('0',64),N'IFRS',SYSUTCDATETIME(),0,@tenant);
'@
$valuationInsert = @'
INSERT dbo.StockAdjustmentItems
 (Id,AdjustmentId,InventoryItemId,LocationId,SystemQuantity,PhysicalQuantity,AdjustmentQuantity,UnitCost,AdjustmentValue,CreatedAt,IsDeleted,TenantId)
SELECT @lineId,@adjustmentId,InventoryItemId,LocationId,0,2,2,LocalCost,ROUND(2*LocalCost,2),SYSUTCDATETIME(),0,TenantId
FROM #ValuationFixture;
'@
$valuationOpeningInsert = $valuationInsert.Replace('LocalCost,ROUND(2*LocalCost,2)', 'CAST(1.2345 AS decimal(18,4)),CAST(2.47 AS decimal(18,4))')
$valuationCases = @(
    @{ Name='Stored adjustment unit cost retains four decimal places'; Sql="IF COLUMNPROPERTY(OBJECT_ID(N'dbo.StockAdjustmentItems'),N'UnitCost','Scale')<>4 THROW 51001,'UnitCost precision is not four decimal places.',1;"; Error=0 },
    @{ Name='PVC exact-location cost 1918.85 is accepted'; Sql=$valuationInsert; Error=0 },
    @{ Name='PVC item-wide cost 1907.09 is rejected'; Sql=$valuationInsert.Replace('LocalCost,ROUND(2*LocalCost,2)', 'GlobalCost,ROUND(2*GlobalCost,2)'); Error=51692 },
    @{ Name='Tampered adjustment value is rejected'; Sql=$valuationInsert.Replace('ROUND(2*LocalCost,2)', 'ROUND(2*LocalCost,2)+1'); Error=51692 },
    @{ Name='Missing exact location is rejected'; Sql=$valuationInsert.Replace('InventoryItemId,LocationId,0,2,2', 'InventoryItemId,NULL,0,2,2'); Error=51692 },
    @{ Name='Existing location in another warehouse is rejected'; Setup=@'
DECLARE @otherLocation uniqueidentifier;
SELECT TOP (1) @otherLocation=l.Id FROM dbo.WarehouseLocations l CROSS JOIN #ValuationFixture f
WHERE l.TenantId=f.TenantId AND l.WarehouseId<>f.WarehouseId AND l.IsDeleted=0 AND l.IsActive=1 AND l.IsConsignmentBin=0 ORDER BY l.Id;
IF @otherLocation IS NULL THROW 51002,'No other-warehouse location is available for this fixture.',1;
UPDATE #ValuationFixture SET LocationId=@otherLocation;
'@; Sql=$valuationInsert; Error=51692 },
    @{ Name='Cross-tenant line cannot use the fixture adjustment'; Setup=@'
DECLARE @otherTenant uniqueidentifier;
SELECT TOP (1) @otherTenant=t.Id FROM dbo.Tenants t CROSS JOIN #ValuationFixture f WHERE t.Id<>f.TenantId ORDER BY t.Id;
IF @otherTenant IS NULL THROW 51002,'No second tenant exists for the cross-tenant fixture.',1;
UPDATE #ValuationFixture SET TenantId=@otherTenant;
'@; Sql=$valuationInsert; Error=51692 },
    @{ Name='Cancelled adjustment lines stay immutable'; Setup=$valuationInsert+"`nUPDATE dbo.StockAdjustments SET Status=N'Cancelled' WHERE Id=@adjustmentId;"; Sql="UPDATE dbo.StockAdjustmentItems SET Reason=N'tamper' WHERE Id=@lineId;"; Error=51691 },
    @{ Name='Opening stock still accepts attributable source-schedule cost'; Opening=$true; Sql=$valuationOpeningInsert; Error=0 },
    @{ Name='Opening stock source lines stay immutable'; Opening=$true; Setup=$valuationOpeningInsert; Sql="UPDATE dbo.StockAdjustmentItems SET Reason=N'tamper' WHERE Id=@lineId;"; Error=51693 },
    @{ Name='Opening stock still rejects negative quantity'; Opening=$true; Sql=$valuationOpeningInsert.Replace('LocationId,0,2,2,', 'LocationId,0,-2,-2,').Replace('CAST(2.47 AS decimal(18,4))', 'CAST(-2.47 AS decimal(18,4))'); Error=51692 },
    @{ Name='Standard-cost method ignores item average and exact-location average'; Sql=@'
DECLARE @standardItem uniqueidentifier,@standardCost decimal(18,4),@standardTenant uniqueidentifier;
SELECT TOP (1) @standardItem=i.Id,@standardCost=i.StandardCost,@standardTenant=i.TenantId
FROM dbo.InventoryItems i CROSS JOIN #ValuationFixture f
WHERE i.TenantId=f.TenantId AND i.IsDeleted=0 AND i.ValuationMethod=4 AND i.StandardCost>0 ORDER BY i.Id;
IF @standardItem IS NULL THROW 51002,'No standard-cost item exists for this fixture.',1;
IF EXISTS(SELECT 1 FROM #ValuationFixture f WHERE
 dbo.InventoryAdjustmentExpectedUnitCost(@standardTenant,@standardItem,f.WarehouseId,f.LocationId,2) IS NULL OR
 dbo.InventoryAdjustmentExpectedUnitCost(@standardTenant,@standardItem,f.WarehouseId,f.LocationId,2)<>ROUND(@standardCost,4))
 THROW 51001,'Standard-cost derivation differs from the configured standard cost.',1;
'@; Error=0 },
    @{ Name='FIFO decrease consumes ordered layers and values uncovered quantity at fallback'; Sql=@'
DECLARE @fifoItem uniqueidentifier,@fifoTenant uniqueidentifier,@fifoWarehouse uniqueidentifier,@fifoLocation uniqueidentifier,
 @fallback decimal(18,4),@quantity decimal(18,4),@expected decimal(18,4),@actual decimal(18,4);
SELECT TOP (1) @fifoItem=i.Id,@fifoTenant=i.TenantId,@fifoWarehouse=l.WarehouseId,@fifoLocation=l.LocationId,
 @fallback=CASE WHEN i.AverageCost>0 THEN i.AverageCost WHEN i.StandardCost>0 THEN i.StandardCost ELSE i.LastPurchaseCost END
FROM dbo.InventoryItems i JOIN dbo.InventoryLayers l ON l.InventoryItemId=i.Id AND l.TenantId=i.TenantId AND l.IsDeleted=0
CROSS JOIN #ValuationFixture f WHERE i.TenantId=f.TenantId AND i.IsDeleted=0 AND i.ValuationMethod=2
 AND l.LocationId IS NOT NULL AND l.IsFullyConsumed=0 AND l.RemainingQuantity>0 ORDER BY i.Id,l.LayerDate,l.CreatedAt,l.Id;
IF @fifoItem IS NULL THROW 51002,'No active FIFO exact-location layer exists for this fixture.',1;
SELECT @quantity=SUM(RemainingQuantity)+2 FROM dbo.InventoryLayers WHERE TenantId=@fifoTenant AND InventoryItemId=@fifoItem
 AND WarehouseId=@fifoWarehouse AND LocationId=@fifoLocation AND IsDeleted=0 AND IsFullyConsumed=0 AND RemainingQuantity>0;
SELECT @expected=ROUND((SUM(RemainingQuantity*UnitCost)+2*@fallback)/@quantity,4)
FROM dbo.InventoryLayers WHERE TenantId=@fifoTenant AND InventoryItemId=@fifoItem AND WarehouseId=@fifoWarehouse
 AND LocationId=@fifoLocation AND IsDeleted=0 AND IsFullyConsumed=0 AND RemainingQuantity>0;
SET @actual=dbo.InventoryAdjustmentExpectedUnitCost(@fifoTenant,@fifoItem,@fifoWarehouse,@fifoLocation,-@quantity);
IF @actual IS NULL OR @actual<>@expected THROW 51001,'FIFO layer-plus-fallback derivation mismatch.',1;
SELECT TOP (1) @quantity=CASE WHEN RemainingQuantity>1 THEN 1 ELSE RemainingQuantity END,@expected=ROUND(UnitCost,4)
FROM dbo.InventoryLayers WHERE TenantId=@fifoTenant AND InventoryItemId=@fifoItem AND WarehouseId=@fifoWarehouse
 AND LocationId=@fifoLocation AND IsDeleted=0 AND IsFullyConsumed=0 AND RemainingQuantity>0 ORDER BY LayerDate,CreatedAt,Id;
SET @actual=dbo.InventoryAdjustmentExpectedUnitCost(@fifoTenant,@fifoItem,@fifoWarehouse,@fifoLocation,-@quantity);
IF @actual IS NULL OR @actual<>@expected THROW 51001,'FIFO oldest partial-layer derivation mismatch.',1;
'@; Error=0 }
)
# These synthetic Drafts exercise database guards only, not workflow signatures or final stock posting.
# Each case gets an isolated transaction. No test writes migration history or commits fixtures.
$valuationConnection = [System.Data.SqlClient.SqlConnection]::new('Server=RHEMA-MICHAEL\SQL2017;Database=RhemaERP_PO_Rehearsal_20260909;Integrated Security=True;Application Name=StockAdjustmentValuationRollbackTests')
try {
    $valuationConnection.Open()
    $valuationTargetCommand=$valuationConnection.CreateCommand()
    try { $valuationTargetCommand.CommandText=$valuationGuard; [void]$valuationTargetCommand.ExecuteNonQuery() }
    finally { $valuationTargetCommand.Dispose() }
    foreach ($valuationCase in $valuationCases) {
        $valuationTransaction=$valuationConnection.BeginTransaction()
        $valuationCommand=$valuationConnection.CreateCommand()
        $valuationCommand.Transaction=$valuationTransaction
        $valuationCommand.CommandTimeout=90
        try {
            $valuationCommand.CommandText=$valuationGuard+"`nIF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'$valuationMigrationId') BEGIN`n$valuationSql`nEND"
            [void]$valuationCommand.ExecuteNonQuery()
            # Create at connection scope before SqlClient switches to parameterised
            # sp_executesql; a table created inside that scope disappears on return.
            $valuationCommand.CommandText='CREATE TABLE #ValuationFixture(TenantId uniqueidentifier, WarehouseId uniqueidentifier, LocationId uniqueidentifier, InventoryItemId uniqueidentifier, LocalCost decimal(18,4), GlobalCost decimal(18,4));'
            [void]$valuationCommand.ExecuteNonQuery()
            [void]$valuationCommand.Parameters.AddWithValue('@adjustmentId',[Guid]::NewGuid())
            [void]$valuationCommand.Parameters.AddWithValue('@lineId',[Guid]::NewGuid())
            [void]$valuationCommand.Parameters.AddWithValue('@opening',[bool]$valuationCase.Opening)
            $valuationCommand.CommandText=$valuationFixture
            [void]$valuationCommand.ExecuteNonQuery()
            if ($valuationCase.Setup) { $valuationCommand.CommandText=$valuationCase.Setup; [void]$valuationCommand.ExecuteNonQuery() }
            $valuationCommand.CommandText=$valuationCase.Sql
            $valuationError=0
            $valuationErrorMessage=''
            try { [void]$valuationCommand.ExecuteNonQuery() }
            catch [System.Data.SqlClient.SqlException] {
                $valuationError=$_.Exception.Number
                $valuationErrorMessage=$_.Exception.Message
                if ($valuationError -eq 51002) {
                    [pscustomobject]@{Test=$valuationCase.Name;Passed=$false;Skipped=$true;Reason=$_.Exception.Message;RolledBack=$true}
                    continue
                }
            }
            if ($valuationError -ne $valuationCase.Error) { throw "$($valuationCase.Name): expected SQL error $($valuationCase.Error), got $valuationError. $valuationErrorMessage" }
            [pscustomobject]@{Test=$valuationCase.Name;Passed=$true;Skipped=$false;RolledBack=$true}
        } catch [System.Data.SqlClient.SqlException] {
            if ($_.Exception.Number -ne 51002) { throw }
            [pscustomobject]@{Test=$valuationCase.Name;Passed=$false;Skipped=$true;Reason=$_.Exception.Message;RolledBack=$true}
        } finally {
            try { $valuationTransaction.Rollback() } catch { }
            $valuationCommand.Dispose()
            $valuationTransaction.Dispose()
        }
    }
} finally { $valuationConnection.Dispose() }
