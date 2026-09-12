[CmdletBinding()]
param(
    [ValidateSet('Plan','Execute')][string]$Mode = 'Plan',
    [ValidatePattern('^RhemaERP_FIFO_Regression_[0-9]{8}$')][string]$DatabaseName = 'RhemaERP_FIFO_Regression_20260912',
    [Parameter(Mandatory)][string]$SqlPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSqlSha256
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
$fifoSqlFile = (Resolve-Path -LiteralPath $SqlPath).Path
if ((Get-FileHash -LiteralPath $fifoSqlFile -Algorithm SHA256).Hash -ne $ExpectedSqlSha256) { throw 'Reviewed FIFO migration SQL hash mismatch.' }
$fifoSql = Get-Content -LiteralPath $fifoSqlFile -Raw
if ($fifoSql -notmatch '20260912235000_StockAdjustmentSequentialFifoValuation' -or
    $fifoSql -notmatch 'InventoryAdjustmentExpectedLineValue' -or
    $fifoSql -match '(?im)^\s*(COMMIT|ROLLBACK|BEGIN\s+TRANSACTION|USE\s+)') {
    throw 'Supply only the reviewed485 migration SQL generated with --no-transactions; no transaction ownership or database switches are allowed.'
}

$fifoFixture = @'
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE N'RhemaERP_FIFO_Regression_[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'RHEMA-MICHAEL\SQL2017'
 THROW 51000,'SQL FIFO tests require the separately approved local regression clone.',1;
DECLARE @sourceItem uniqueidentifier,@tenant uniqueidentifier,@warehouse uniqueidentifier,@location uniqueidentifier,@actor uniqueidentifier,@uom nvarchar(50);
SELECT TOP(1) @sourceItem=i.Id,@tenant=i.TenantId,@warehouse=b.WarehouseId,@location=b.LocationId
FROM dbo.InventoryItems i JOIN dbo.InventoryBalances b ON b.InventoryItemId=i.Id AND b.TenantId=i.TenantId AND b.IsDeleted=0
JOIN dbo.WarehouseLocations l ON l.Id=b.LocationId AND l.TenantId=i.TenantId AND l.IsActive=1 AND l.IsDeleted=0 AND l.IsConsignmentBin=0
JOIN dbo.Warehouses w ON w.Id=b.WarehouseId AND w.TenantId=i.TenantId AND w.IsDeleted=0 AND w.IsActive=1 AND w.IsConsignmentWarehouse=0
WHERE i.IsDeleted=0 AND EXISTS(SELECT 1 FROM dbo.InventoryLocations p WHERE p.InventoryItemId=i.Id AND p.LocationId=l.Id AND p.IsDeleted=0)
 AND EXISTS(SELECT 1 FROM dbo.InventoryLayers p WHERE p.InventoryItemId=i.Id AND p.LocationId=l.Id AND p.IsDeleted=0)
 AND EXISTS(SELECT 1 FROM dbo.UnitsOfMeasure u WHERE u.TenantId=i.TenantId AND u.IsDeleted=0 AND u.IsActive=1)
 AND EXISTS(SELECT 1 FROM dbo.InventoryCategories c WHERE c.Id=i.CategoryId AND c.TenantId=i.TenantId AND c.IsDeleted=0 AND c.IsActive=1)
 AND (i.UnitOfMeasureScheduleId IS NULL OR EXISTS(SELECT 1 FROM dbo.UnitOfMeasureSchedules s WHERE s.Id=i.UnitOfMeasureScheduleId AND s.TenantId=i.TenantId AND s.IsDeleted=0 AND s.IsActive=1))
ORDER BY i.Id,b.Id;
IF @sourceItem IS NULL THROW 51000,'No complete owned item/bin fixture exists in this clone.',1;
SELECT TOP(1) @uom=Code FROM dbo.UnitsOfMeasure WHERE TenantId=@tenant AND IsDeleted=0 AND IsActive=1 ORDER BY Code,Id;
IF EXISTS(SELECT 1 FROM dbo.PhysicalCounts WHERE TenantId=@tenant AND WarehouseId=@warehouse AND IsDeleted=0
 AND Status NOT IN(N'Posted',N'Cancelled')) THROW 51000,'The fixture warehouse has an unfinished count; choose a reviewed clone without a frozen scope.',1;
SELECT TOP(1) @actor=u.Id FROM dbo.Users u JOIN dbo.UserTenants t ON t.UserId=u.Id
WHERE t.TenantId=@tenant AND t.IsDeleted=0 AND t.Status=0 AND u.IsActive=1
 AND (t.ExpiresAt IS NULL OR t.ExpiresAt>SYSUTCDATETIME()) ORDER BY u.Id;
IF @actor IS NULL THROW 51000,'No active fixture actor exists.',1;
INSERT #FifoScope VALUES(@tenant,@warehouse,@location,@actor);

DECLARE @columns nvarchar(max),@expressions nvarchar(max),@sql nvarchar(max);
SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP(ORDER BY column_id),
 @expressions=STRING_AGG(CONVERT(nvarchar(max),CASE name
 WHEN N'Id' THEN N'@item' WHEN N'ItemCode' THEN N'N''FIFO-REG-''+CONVERT(nvarchar(36),@item)'
 WHEN N'Barcode' THEN CASE WHEN is_nullable=1 THEN N'NULL' ELSE N'N''FIFO-REG-''+CONVERT(nvarchar(36),@item)' END
 WHEN N'Name' THEN N'N''Rollback-only FIFO fixture''' WHEN N'ValuationMethod' THEN N'2'
 WHEN N'UnitOfMeasure' THEN N'@uom' WHEN N'UnitOfMeasureScheduleId' THEN N'NULL'
 WHEN N'IsValuationLocked' THEN N'0' WHEN N'AverageCost' THEN N'40' WHEN N'StandardCost' THEN N'50'
 WHEN N'LastPurchaseCost' THEN N'60' WHEN N'CurrentStock' THEN N'@opening' WHEN N'AvailableStock' THEN N'@opening'
 WHEN N'AllocatedStock' THEN N'0' WHEN N'IsDeleted' THEN N'0' ELSE QUOTENAME(name) END),N',') WITHIN GROUP(ORDER BY column_id)
FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryItems') AND is_computed=0 AND is_identity=0 AND system_type_id<>189;
SET @sql=N'INSERT dbo.InventoryItems('+@columns+N') SELECT '+@expressions+N' FROM dbo.InventoryItems WHERE Id=@source';
EXEC sys.sp_executesql @sql,N'@item uniqueidentifier,@source uniqueidentifier,@opening decimal(18,4),@uom nvarchar(50)',@item,@sourceItem,@opening,@uom;

DECLARE @table sysname;
DECLARE sourceTables CURSOR LOCAL FAST_FORWARD FOR SELECT name FROM (VALUES(N'InventoryLocations'),(N'InventoryBalances')) x(name);
OPEN sourceTables; FETCH NEXT FROM sourceTables INTO @table;
WHILE @@FETCH_STATUS=0
BEGIN
 SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP(ORDER BY column_id),
  @expressions=STRING_AGG(CONVERT(nvarchar(max),CASE name WHEN N'Id' THEN N'NEWID()' WHEN N'InventoryItemId' THEN N'@item'
   WHEN N'Quantity' THEN N'@opening' WHEN N'AvailableQuantity' THEN N'@opening' WHEN N'AllocatedQuantity' THEN N'0'
   WHEN N'QuantityOnHand' THEN N'@opening' WHEN N'QuantityAvailable' THEN N'@opening' WHEN N'QuantityAllocated' THEN N'0'
   WHEN N'AverageCost' THEN N'40' WHEN N'AverageUnitCost' THEN N'40' WHEN N'TotalValue' THEN N'@opening*40'
   WHEN N'IsDeleted' THEN N'0' ELSE QUOTENAME(name) END),N',') WITHIN GROUP(ORDER BY column_id)
 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.'+@table) AND is_computed=0 AND is_identity=0 AND system_type_id<>189;
 SET @sql=N'INSERT dbo.'+QUOTENAME(@table)+N'('+@columns+N') SELECT TOP(1) '+@expressions+N' FROM dbo.'+QUOTENAME(@table)+N' WHERE InventoryItemId=@source AND LocationId=@location AND IsDeleted=0';
 EXEC sys.sp_executesql @sql,N'@item uniqueidentifier,@source uniqueidentifier,@location uniqueidentifier,@opening decimal(18,4)',@item,@sourceItem,@location,@opening;
 FETCH NEXT FROM sourceTables INTO @table;
END
CLOSE sourceTables; DEALLOCATE sourceTables;

SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(name)),N',') WITHIN GROUP(ORDER BY column_id),
 @expressions=STRING_AGG(CONVERT(nvarchar(max),CASE name WHEN N'Id' THEN N'NEWID()' WHEN N'InventoryItemId' THEN N'@item'
 WHEN N'LayerNumber' THEN N'N''FIFO-REG-''+CONVERT(nvarchar(36),NEWID())' WHEN N'LayerDate' THEN N'@date'
 WHEN N'OriginalQuantity' THEN N'@quantity' WHEN N'RemainingQuantity' THEN N'@quantity'
 WHEN N'UnitCost' THEN N'@cost' WHEN N'RemainingValue' THEN N'@value'
 WHEN N'SourceType' THEN N'N''RegressionFixture''' WHEN N'SourceId' THEN N'NULL'
 WHEN N'IsFullyConsumed' THEN N'0' WHEN N'IsDeleted' THEN N'0' WHEN N'IsActive' THEN N'1'
 ELSE QUOTENAME(name) END),N',') WITHIN GROUP(ORDER BY column_id)
FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryLayers') AND is_computed=0 AND is_identity=0 AND system_type_id<>189;
SET @sql=N'INSERT dbo.InventoryLayers('+@columns+N') SELECT TOP(1) '+@expressions+N' FROM dbo.InventoryLayers WHERE InventoryItemId=@source AND LocationId=@location AND IsDeleted=0';
DECLARE @quantity decimal(18,4),@cost decimal(18,4),@value decimal(18,4),@date datetime2(7);
DECLARE fixtureLayers CURSOR LOCAL FAST_FORWARD FOR SELECT Quantity,UnitCost,RemainingValue,LayerDate FROM #FifoLayers ORDER BY LayerDate;
OPEN fixtureLayers; FETCH NEXT FROM fixtureLayers INTO @quantity,@cost,@value,@date;
WHILE @@FETCH_STATUS=0
BEGIN
 EXEC sys.sp_executesql @sql,N'@item uniqueidentifier,@source uniqueidentifier,@location uniqueidentifier,@quantity decimal(18,4),@cost decimal(18,4),@value decimal(18,4),@date datetime2(7)',@item,@sourceItem,@location,@quantity,@cost,@value,@date;
 FETCH NEXT FROM fixtureLayers INTO @quantity,@cost,@value,@date;
END
CLOSE fixtureLayers; DEALLOCATE fixtureLayers;
INSERT dbo.StockAdjustments(Id,AdjustmentNumber,WarehouseId,Reference,AdjustmentDate,ReasonCode,Description,Status,RequestedById,
 TotalAdjustmentValue,IdempotencyKey,PayloadHash,CorrelationId,IntegrityHash,BookClassification,CreatedAt,IsDeleted,TenantId)
VALUES(@adjustment,N'FIFO-REG-'+CONVERT(nvarchar(36),@adjustment),@warehouse,N'FIFO-ROLLBACK',SYSUTCDATETIME(),N'PHYSICAL_COUNT',
 N'Rollback-only sequential FIFO test',N'Draft',@actor,0,N'fifo-reg:'+CONVERT(nvarchar(36),@adjustment),
 REPLICATE('0',64),N'fifo-reg',REPLICATE('0',64),N'IFRS',SYSUTCDATETIME(),0,@tenant);
'@
$fifoLine = @'
INSERT dbo.StockAdjustmentItems(Id,AdjustmentId,InventoryItemId,LocationId,SystemQuantity,PhysicalQuantity,AdjustmentQuantity,UnitCost,AdjustmentValue,LotNumber,CreatedAt,IsDeleted,TenantId)
SELECT @line,@adjustment,@item,LocationId,@opening,@opening+@delta,@delta,@cost,@value,N'LOT-'+CONVERT(nvarchar(36),@line),@created,0,TenantId FROM #FifoScope;
'@
$fifoCases = @(
    @{ Name='Distinct tracking rows consume older layers once'; Layers=@(1,10,10,1,20,20); Lines=@(@(-1,10,-10),@(-1,20,-20)); Error=0 },
    @{ Name='Repeated first-layer price on second row is rejected'; Layers=@(1,10,10,1,20,20); Lines=@(@(-1,10,-10),@(-1,10,-10)); Error=51692 },
    @{ Name='Final row receives the retained cent remainder'; Layers=@(3,0.33,1); Lines=@(@(-1,0.33,-0.33),@(-1,0.33,-0.33),@(-1,0.34,-0.34)); Error=0 },
    @{ Name='Fractional rows round independently and settle the remainder'; Layers=@(1,0.05,0.05); Lines=@(@(-0.3333,0.06,-0.02),@(-0.3333,0.06,-0.02),@(-0.3334,0.03,-0.01)); Error=0 },
    @{ Name='Legacy remainder uses established item fallback40'; Opening=6; Layers=@(2,10,20,1,12,12); Lines=@(@(-2,10,-20),@(-3,30.6667,-92),@(-1,40,-40)); Error=0 },
    @{ Name='Exact layer value is not rounded display cost times quantity'; Layers=@(15311,1,15311,4689,2,9378); Lines=@(,@(-20000,1.2345,-24689)); Error=0 },
    @{ Name='Tampered authoritative value is rejected'; Layers=@(1,10,10); Lines=@(,@(-1,10,-11)); Error=51692 },
    @{ Name='Unchanged editable lines can be retired and surviving lines recosted'; Layers=@(1,10,10,1,20,20); Lines=@(@(-1,10,-10),@(-1,20,-20)); Retire=$true; Replacement=@(-1,10,-10); Error=0 },
    @{ Name='Retirement cannot hide simultaneous value tampering'; Layers=@(1,10,10); Lines=@(,@(-1,10,-10)); Retire=$true; TamperRetirement=$true; Error=51980 },
    @{ Name='Retired line evidence cannot be restored'; Layers=@(1,10,10); Lines=@(,@(-1,10,-10)); Retire=$true; Restore=$true; Error=51980 },
    @{ Name='New positive execution layer follows older stock'; Layers=@(2,10,20,1,20,20); Lines=@(@(-1,10,-10),@(1,40,40),@(-2,15,-30),@(-1,40,-40)); Error=0 },
    @{ Name='CreatedAt ties use SQL uniqueidentifier order'; Layers=@(1,10,10,1,20,20); Lines=@(@(-1,10,-10),@(-1,20,-20)); Tie=$true; Error=0 }
)
if ($Mode -eq 'Plan') {
    [pscustomobject]@{Mode='Plan';Database=$DatabaseName;Server='RHEMA-MICHAEL\SQL2017';SqlSha256=$ExpectedSqlSha256;Cases=@($fifoCases.Name);WritesExecuted=$false;RequiresApprovedRegressionClone=$true} | ConvertTo-Json -Depth 4
    return
}
$fifoConnection = [System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$DatabaseName;Integrated Security=True;Application Name=SequentialFifoRollbackRegression")
$fifoConnection.Open()
function Read-FifoSnapshot {
    # No operational fields are omitted. The fixture and protected inventory,
    # count, approval and Finance rows must be identical after EVERY rollback.
    $snapshotCommand=$fifoConnection.CreateCommand(); $snapshotCommand.CommandTimeout=180
    try {
        $snapshotCommand.CommandText=@'
DECLARE @result TABLE(Kind nvarchar(20),Name nvarchar(128),[RowCount] bigint,Hash varchar(64));
INSERT @result
SELECT N'Module',o.name,ISNULL(CONVERT(bigint,t.is_disabled),0),
 CONVERT(varchar(64),HASHBYTES('SHA2_256',m.definition),2)
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id
LEFT JOIN sys.triggers t ON t.object_id=o.object_id WHERE OBJECT_SCHEMA_NAME(o.object_id)=N'dbo';
INSERT @result VALUES(N'Schema',N'Columns',0,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(
 SELECT t.name TableName,c.column_id,c.name ColumnName,TYPE_NAME(c.user_type_id) SqlType,c.max_length,c.precision,c.scale,c.is_nullable,dc.definition
 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
 LEFT JOIN sys.default_constraints dc ON dc.parent_object_id=t.object_id AND dc.parent_column_id=c.column_id
 WHERE SCHEMA_NAME(t.schema_id)=N'dbo' ORDER BY t.name,c.column_id FOR JSON PATH,INCLUDE_NULL_VALUES))),2));
INSERT @result VALUES(N'History',N'__EFMigrationsHistory',(SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory),
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),(
 SELECT * FROM dbo.__EFMigrationsHistory ORDER BY MigrationId FOR JSON PATH,INCLUDE_NULL_VALUES))),2));
SELECT * FROM @result ORDER BY Kind,Name;
'@
        $snapshotTable=[System.Data.DataTable]::new(); $snapshotReader=$snapshotCommand.ExecuteReader()
        try { $snapshotTable.Load($snapshotReader) } finally { $snapshotReader.Dispose() }
        $snapshotRows=@($snapshotTable.Rows | ForEach-Object { [ordered]@{Kind=[string]$_.Kind;Name=[string]$_.Name;RowCount=[long]$_.RowCount;Hash=[string]$_.Hash} })
        $snapshotTables=@('InventoryItems','Warehouses','WarehouseLocations','WarehouseQuantities','InventoryLocations',
            'InventoryBalances','InventoryLayers','InventoryMovements','StockMovements','StockAdjustments','StockAdjustmentItems',
            'StockAdjustmentActions','StockAdjustmentEvidence','PhysicalCounts','PhysicalCountItems','PhysicalCountActions',
            'InventoryDisposalCases','InventoryDisposalLines','InventoryDisposalActions','JournalEntries','AccountTransactions',
            'FinancePostingEvents','WorkflowInstances','WorkflowDefinitions','ProcurementControlEvents','AuditLogs')
        foreach ($snapshotName in $snapshotTables) {
            $snapshotCommand.CommandText="SELECT OBJECT_ID(N'dbo.[$snapshotName]',N'U')"
            if ($snapshotCommand.ExecuteScalar() -is [System.DBNull]) { throw "Protected table missing: $snapshotName" }
            $snapshotCommand.CommandText="SELECT COUNT_BIG(*) [RowCount],CONVERT(varchar(64),HASHBYTES('SHA2_256',COALESCE(CONVERT(nvarchar(max),(SELECT * FROM dbo.[$snapshotName] ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES)),N'[]')),2) RecordHash FROM dbo.[$snapshotName]"
            $snapshotReader=$snapshotCommand.ExecuteReader()
            try { [void]$snapshotReader.Read(); $snapshotRows+= [ordered]@{Kind='Business';Name=$snapshotName;RowCount=$snapshotReader.GetInt64(0);Hash=$snapshotReader.GetString(1)} } finally { $snapshotReader.Dispose() }
        }
        return ($snapshotRows | ConvertTo-Json -Compress -Depth 5)
    } finally { $snapshotCommand.Dispose() }
}
try {
    $fifoCheck = $fifoConnection.CreateCommand()
    $fifoCheck.CommandText = 'SELECT DB_NAME(),CONVERT(nvarchar(128),SERVERPROPERTY(''ServerName''))'
    $fifoReader=$fifoCheck.ExecuteReader(); [void]$fifoReader.Read()
    if ($fifoReader.GetString(0) -ne $DatabaseName -or $fifoReader.GetString(1) -ne 'RHEMA-MICHAEL\SQL2017') { $fifoReader.Close(); throw 'Regression clone identity mismatch.' }
    $fifoReader.Close()
    $fifoCheck.CommandText="SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId<=N'20260912234000_InventoryDisposalOptionalApprovalAndDrafts'; SELECT MAX(MigrationId) FROM dbo.__EFMigrationsHistory"
    $fifoReader=$fifoCheck.ExecuteReader(); [void]$fifoReader.Read(); $fifoBaselineCount=$fifoReader.GetInt32(0)
    [void]$fifoReader.NextResult(); [void]$fifoReader.Read(); $fifoBaselineId=$fifoReader.GetString(0); $fifoReader.Close(); $fifoCheck.Dispose()
    if ($fifoBaselineCount -ne 484 -or $fifoBaselineId -ne '20260912234000_InventoryDisposalOptionalApprovalAndDrafts') { throw 'The separately restored regression clone must be at the reviewed484 baseline, not an applied485 or later database.' }
    $fifoInitialSnapshot=Read-FifoSnapshot
    foreach ($fifoCase in $fifoCases) {
        if ((Read-FifoSnapshot) -cne $fifoInitialSnapshot) { throw 'Regression clone changed concurrently; the next case is refused.' }
        $fifoTransaction = $fifoConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        $fifoCommand = $fifoConnection.CreateCommand(); $fifoCommand.Transaction=$fifoTransaction; $fifoCommand.CommandTimeout=180
        try {
            foreach ($fifoBatch in [regex]::Split($fifoSql,'(?im)^\s*GO\s*(?:--[^\r\n]*)?$')) {
                if (-not [string]::IsNullOrWhiteSpace($fifoBatch)) { $fifoCommand.CommandText=$fifoBatch; [void]$fifoCommand.ExecuteNonQuery() }
            }
            $fifoCommand.CommandText='CREATE TABLE #FifoScope(TenantId uniqueidentifier,WarehouseId uniqueidentifier,LocationId uniqueidentifier,ActorId uniqueidentifier); CREATE TABLE #FifoLayers(Quantity decimal(18,4),UnitCost decimal(18,4),RemainingValue decimal(18,4),LayerDate datetime2(7));'
            [void]$fifoCommand.ExecuteNonQuery()
            for ($fifoIndex=0; $fifoIndex -lt $fifoCase.Layers.Count; $fifoIndex+=3) {
                $fifoCommand.Parameters.Clear(); $fifoCommand.CommandText='INSERT #FifoLayers VALUES(@quantity,@cost,@value,@date)'
                [void]$fifoCommand.Parameters.AddWithValue('@quantity',[decimal]$fifoCase.Layers[$fifoIndex]); [void]$fifoCommand.Parameters.AddWithValue('@cost',[decimal]$fifoCase.Layers[$fifoIndex+1]); [void]$fifoCommand.Parameters.AddWithValue('@value',[decimal]$fifoCase.Layers[$fifoIndex+2]); [void]$fifoCommand.Parameters.AddWithValue('@date',[datetime]::new(2000,1,1).AddDays($fifoIndex)); [void]$fifoCommand.ExecuteNonQuery()
            }
            $fifoCommand.Parameters.Clear(); $fifoItem=[guid]::NewGuid(); $fifoAdjustment=[guid]::NewGuid()
            $fifoOpening=[decimal]0
            for($fifoIndex=0;$fifoIndex -lt $fifoCase.Layers.Count;$fifoIndex+=3){$fifoOpening += [decimal]$fifoCase.Layers[$fifoIndex]}
            if($fifoCase.ContainsKey('Opening')){$fifoOpening=[decimal]$fifoCase.Opening}
            [void]$fifoCommand.Parameters.AddWithValue('@item',$fifoItem); [void]$fifoCommand.Parameters.AddWithValue('@adjustment',$fifoAdjustment)
            [void]$fifoCommand.Parameters.AddWithValue('@opening',$fifoOpening)
            $fifoCommand.CommandText=$fifoFixture; [void]$fifoCommand.ExecuteNonQuery()
            $fifoError=0; $fifoOrdinal=0; $fifoBaseTime=[datetime]::UtcNow
            try {
                foreach ($fifoValues in $fifoCase.Lines) {
                    $fifoCommand.Parameters.Clear()
                    $fifoLineId=[guid]::NewGuid()
                    if($fifoCase.Tie){$fifoLineId=if($fifoOrdinal -eq 0){[guid]'ffffffff-ffff-ffff-ffff-000000000001'}else{[guid]'00000000-0000-0000-0000-000000000002'}}
                    [void]$fifoCommand.Parameters.AddWithValue('@item',$fifoItem); [void]$fifoCommand.Parameters.AddWithValue('@adjustment',$fifoAdjustment); [void]$fifoCommand.Parameters.AddWithValue('@line',$fifoLineId)
                    [void]$fifoCommand.Parameters.AddWithValue('@opening',$fifoOpening)
                    [void]$fifoCommand.Parameters.AddWithValue('@delta',[decimal]$fifoValues[0]); [void]$fifoCommand.Parameters.AddWithValue('@cost',[decimal]$fifoValues[1]); [void]$fifoCommand.Parameters.AddWithValue('@value',[decimal]$fifoValues[2])
                    $fifoDateParameter=$fifoCommand.Parameters.Add('@created',[System.Data.SqlDbType]::DateTime2); $fifoDateParameter.Scale=7; $fifoDateParameter.Value=if($fifoCase.Tie){$fifoBaseTime}else{$fifoBaseTime.AddTicks($fifoOrdinal)}; $fifoOrdinal++
                    $fifoCommand.CommandText=$fifoLine; [void]$fifoCommand.ExecuteNonQuery()
                }
                if ($fifoCase.Retire) {
                    $fifoCommand.Parameters.Clear(); [void]$fifoCommand.Parameters.AddWithValue('@adjustment',$fifoAdjustment)
                    $fifoCommand.CommandText='UPDATE dbo.StockAdjustmentItems SET IsDeleted=1 WHERE AdjustmentId=@adjustment'
                    if ($fifoCase.TamperRetirement) { $fifoCommand.CommandText='UPDATE dbo.StockAdjustmentItems SET IsDeleted=1,AdjustmentValue=AdjustmentValue-1 WHERE AdjustmentId=@adjustment' }
                    [void]$fifoCommand.ExecuteNonQuery()
                    if ($fifoCase.Restore) { $fifoCommand.CommandText='UPDATE dbo.StockAdjustmentItems SET IsDeleted=0 WHERE AdjustmentId=@adjustment'; [void]$fifoCommand.ExecuteNonQuery() }
                    if ($fifoCase.Replacement) {
                        [void]$fifoCommand.Parameters.AddWithValue('@item',$fifoItem); [void]$fifoCommand.Parameters.AddWithValue('@line',[guid]::NewGuid())
                        [void]$fifoCommand.Parameters.AddWithValue('@opening',$fifoOpening)
                        [void]$fifoCommand.Parameters.AddWithValue('@delta',[decimal]$fifoCase.Replacement[0]); [void]$fifoCommand.Parameters.AddWithValue('@cost',[decimal]$fifoCase.Replacement[1]); [void]$fifoCommand.Parameters.AddWithValue('@value',[decimal]$fifoCase.Replacement[2])
                        $fifoDateParameter=$fifoCommand.Parameters.Add('@created',[System.Data.SqlDbType]::DateTime2); $fifoDateParameter.Scale=7; $fifoDateParameter.Value=$fifoBaseTime.AddTicks($fifoOrdinal++)
                        $fifoCommand.CommandText=$fifoLine; [void]$fifoCommand.ExecuteNonQuery()
                    }
                }
            } catch [System.Data.SqlClient.SqlException] { $fifoError=$_.Exception.Number }
            if ($fifoError -ne $fifoCase.Error) { throw "$($fifoCase.Name): expected SQL error $($fifoCase.Error), got $fifoError." }
        } finally {
            try {
                if ($fifoTransaction.Connection) { $fifoTransaction.Rollback() }
            } finally {
                $fifoTransaction.Dispose(); $fifoCommand.Dispose()
                if ((Read-FifoSnapshot) -cne $fifoInitialSnapshot) { throw 'CRITICAL: regression rollback did not restore every protected schema, module, migration-history and business fingerprint.' }
            }
        }
        [pscustomobject]@{Case=$fifoCase.Name;Passed=$true;ObservedSqlError=$fifoError;Committed=$false;ProtectedBusinessTables=26;AllRollbackFingerprintsRestored=$true} | ConvertTo-Json -Compress
    }
} finally { $fifoConnection.Dispose() }
