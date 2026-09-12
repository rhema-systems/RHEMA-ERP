# Explicit, reviewed cache-only repair for the two locally rehearsed items.
# Plan is read-only. Apply requires the exact plan hash, a verified database backup
# and an explicit switch; this script never posts/revalues stock or changes history.
param(
    [Parameter(Mandatory)][ValidateSet('RhemaERP','RhemaERP_PO_Rehearsal_20260909')][string]$Database,
    [ValidateSet('Plan','RollbackProbe','Apply')][string]$Mode='Plan',
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ReviewedPlanSha256,
    [string]$VerifiedBackupPath,
    [switch]$ConfirmApply
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
$costTenant=[Guid]'00000000-0000-0000-0000-000000000001'
$costServer='RHEMA-MICHAEL\SQL2017'
$costConnection=[System.Data.SqlClient.SqlConnection]::new("Server=$costServer;Database=$Database;Integrated Security=True;Application Name=ReviewedInventoryCostCacheRepair")
$costTransaction=$null
$costCommitAttempted=$false
$costCommitted=$false
$costScriptHash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$costCacheColumns=[ordered]@{InventoryItems='AverageCost';WarehouseQuantities='AverageCost';InventoryLocations='AverageCost';InventoryBalances='AverageUnitCost'}
$costItemTables=@('InventoryItems','WarehouseQuantities','InventoryLocations','InventoryBalances','StockMovements','InventoryMovements','InventoryLayers','PhysicalCountItems','StockAdjustmentItems')
$costHistoryTables=@('PhysicalCounts','PhysicalCountActions','StockAdjustments','StockAdjustmentActions','JournalEntries','AccountTransactions','FinancePostingEvents')

function Read-CostRows([string]$Sql,[hashtable]$Parameters=@{}) {
    $costCommand=$costConnection.CreateCommand()
    $costCommand.CommandText=$Sql
    $costCommand.CommandTimeout=60
    if($costTransaction){$costCommand.Transaction=$costTransaction}
    foreach($costParameter in $Parameters.GetEnumerator()){[void]$costCommand.Parameters.AddWithValue($costParameter.Key,$costParameter.Value)}
    try {
        $costReader=$costCommand.ExecuteReader()
        try {
            while($costReader.Read()) {
                $costRow=[ordered]@{}
                for($costIndex=0;$costIndex -lt $costReader.FieldCount;$costIndex++) {
                    $costRow[$costReader.GetName($costIndex)]=if($costReader.IsDBNull($costIndex)){$null}else{$costReader.GetValue($costIndex)}
                }
                [pscustomobject]$costRow
            }
        } finally {$costReader.Dispose()}
    } finally {$costCommand.Dispose()}
}
function Get-CostHash($Value) {
    $costHasher=[Security.Cryptography.SHA256]::Create()
    try {[Convert]::ToHexString($costHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(($Value|ConvertTo-Json -Depth 20 -Compress))))}
    finally {$costHasher.Dispose()}
}
function Get-CostAverage($Rows) {
    [decimal]$costQuantity=0;[decimal]$costValue=0
    foreach($costBalance in $Rows){$costQuantity+=[decimal]$costBalance.QuantityOnHand;$costValue+=[decimal]$costBalance.TotalValue}
    if($costQuantity -gt 0){[decimal]::Round($costValue/$costQuantity,2,[MidpointRounding]::AwayFromZero)}else{[decimal]0}
}
function Read-CostPlan {
    $costItems=@(Read-CostRows "SELECT Id,TenantId,ItemCode,CurrentStock,AverageCost FROM dbo.InventoryItems WHERE TenantId=@tenant AND IsDeleted=0 AND ItemCode IN(N'SKU-001',N'PM-BARCODE-DEVICE') ORDER BY ItemCode,Id" @{tenant=$costTenant})
    if($costItems.Count -ne 2 -or @($costItems.ItemCode|Sort-Object -Unique).Count -ne 2){throw 'The exact two local item identities were not found. No repair is allowed.'}
    $costIds=($costItems|ForEach-Object{"'"+([Guid]$_.Id).ToString('D')+"'"}) -join ','
    # This deliberately narrow repair projects on-hand balances only. Refuse any
    # open/retained transit instead of silently excluding an owned asset.
    $costTransit=@(Read-CostRows @"
SELECT l.Id FROM dbo.InventoryTransferItems l
JOIN dbo.InventoryTransfers t ON t.Id=l.InventoryTransferId AND t.TenantId=@tenant AND t.IsDeleted=0
WHERE l.TenantId=@tenant AND l.IsDeleted=0 AND l.InventoryItemId IN($costIds)
AND ((l.ShippedQuantity>l.ReceivedQuantity AND (t.Status=5 OR t.HasOpenDiscrepancy=1))
 OR EXISTS(SELECT 1 FROM dbo.InventoryMovements m WHERE m.TenantId=@tenant AND m.IsDeleted=0 AND m.IsPosted=1
   AND m.ReferenceType=4 AND m.ReferenceId=l.Id AND m.Notes LIKE N'TransferAction:%'
   GROUP BY m.ReferenceId HAVING SUM(CASE WHEN m.Direction=2 THEN m.Quantity ELSE -m.Quantity END)<>0
     OR SUM(CASE WHEN m.Direction=2 THEN m.TotalValue ELSE -m.TotalValue END)<>0));
"@ @{tenant=$costTenant})
    if($costTransit.Count){throw 'A selected item has open or retained in-transit value. This on-hand-only cache repair requires zero transit; no repair is allowed.'}
    $costBalances=@(Read-CostRows @"
SELECT b.Id,b.InventoryItemId,b.WarehouseId,b.LocationId,b.QuantityOnHand,b.TotalValue,b.AverageUnitCost,
 w.Id OwnerWarehouseId,w.TenantId OwnerTenantId,w.IsConsignmentWarehouse
FROM dbo.InventoryBalances b LEFT JOIN dbo.Warehouses w ON w.Id=b.WarehouseId
WHERE b.TenantId=@tenant AND b.IsDeleted=0 AND b.InventoryItemId IN($costIds)
ORDER BY b.InventoryItemId,b.WarehouseId,b.LocationId,b.Id;
"@ @{tenant=$costTenant})
    if(@($costBalances|Where-Object{-not $_.OwnerWarehouseId -or $_.OwnerTenantId -ne $costTenant}).Count){throw 'A valuation balance has missing or foreign warehouse ownership.'}
    $costWarehouseRows=@(Read-CostRows "SELECT Id,InventoryItemId,WarehouseId,CurrentStock,AverageCost FROM dbo.WarehouseQuantities WHERE TenantId=@tenant AND IsDeleted=0 AND InventoryItemId IN($costIds) ORDER BY InventoryItemId,WarehouseId,Id" @{tenant=$costTenant})
    $costLocationRows=@(Read-CostRows "SELECT Id,InventoryItemId,LocationId,Quantity,AverageCost FROM dbo.InventoryLocations WHERE TenantId=@tenant AND IsDeleted=0 AND InventoryItemId IN($costIds) ORDER BY InventoryItemId,LocationId,Id" @{tenant=$costTenant})
    $costProjection=[System.Collections.Generic.List[object]]::new()
    foreach($costItem in $costItems) {
        $costItemBalances=@($costBalances|Where-Object InventoryItemId -eq $costItem.Id)
        if(-not $costItemBalances.Count){throw "Item $($costItem.ItemCode) has no authoritative valuation balance."}
        foreach($costWarehouseGroup in $costItemBalances|Group-Object WarehouseId) {
            $costScope=@($costWarehouseGroup.Group)
            if(@($costScope|Where-Object LocationId).Count -and @($costScope|Where-Object{-not $_.LocationId -and ($_.QuantityOnHand -ne 0 -or $_.TotalValue -ne 0)}).Count){throw 'Nonzero unlocated and exact-bin balances cannot be silently merged.'}
            if(@($costScope|Group-Object LocationId|Where-Object Count -gt 1).Count){throw 'Duplicate valuation scope must be reconciled before any cache repair.'}
        }
        $costOwned=@($costItemBalances|Where-Object{-not $_.IsConsignmentWarehouse})
        [decimal]$costOwnedQuantity=($costOwned|Measure-Object QuantityOnHand -Sum).Sum
        if([decimal]$costItem.CurrentStock -ne $costOwnedQuantity){throw "Item $($costItem.ItemCode) quantity does not reconcile with its owned valuation balances. This tool cannot repair quantities."}
        $costProjection.Add([pscustomobject]@{Table='InventoryItems';Column='AverageCost';Id=$costItem.Id;ItemCode=$costItem.ItemCode;Current=[decimal]$costItem.AverageCost;Expected=(Get-CostAverage $costOwned)})
        foreach($costWarehouse in $costWarehouseRows|Where-Object InventoryItemId -eq $costItem.Id) {
            $costScope=@($costItemBalances|Where-Object WarehouseId -eq $costWarehouse.WarehouseId)
            [decimal]$costScopeQuantity=($costScope|Measure-Object QuantityOnHand -Sum).Sum
            if([decimal]$costWarehouse.CurrentStock -ne $costScopeQuantity){throw "Warehouse quantity for $($costItem.ItemCode) does not reconcile. No cache-only repair is allowed."}
            $costProjection.Add([pscustomobject]@{Table='WarehouseQuantities';Column='AverageCost';Id=$costWarehouse.Id;ItemCode=$costItem.ItemCode;Current=[decimal]$costWarehouse.AverageCost;Expected=(Get-CostAverage $costScope)})
        }
        foreach($costLocation in $costLocationRows|Where-Object InventoryItemId -eq $costItem.Id) {
            $costScope=@($costItemBalances|Where-Object LocationId -eq $costLocation.LocationId)
            [decimal]$costScopeQuantity=($costScope|Measure-Object QuantityOnHand -Sum).Sum
            if([decimal]$costLocation.Quantity -ne $costScopeQuantity){throw "Bin quantity for $($costItem.ItemCode) does not reconcile. No cache-only repair is allowed."}
            $costProjection.Add([pscustomobject]@{Table='InventoryLocations';Column='AverageCost';Id=$costLocation.Id;ItemCode=$costItem.ItemCode;Current=[decimal]$costLocation.AverageCost;Expected=(Get-CostAverage $costScope)})
        }
        foreach($costBalance in $costItemBalances) {
            if(@($costWarehouseRows|Where-Object{$_.InventoryItemId -eq $costItem.Id -and $_.WarehouseId -eq $costBalance.WarehouseId}).Count -ne 1){throw 'A valuation warehouse must have exactly one existing quantity cache.'}
            if($costBalance.LocationId -and @($costLocationRows|Where-Object{$_.InventoryItemId -eq $costItem.Id -and $_.LocationId -eq $costBalance.LocationId}).Count -ne 1){throw 'A valuation bin must have exactly one existing quantity cache.'}
            $costProjection.Add([pscustomobject]@{Table='InventoryBalances';Column='AverageUnitCost';Id=$costBalance.Id;ItemCode=$costItem.ItemCode;Current=[decimal]$costBalance.AverageUnitCost;Expected=(Get-CostAverage @($costBalance))})
        }
    }
    $costFingerprints=@(foreach($costTable in @($costItemTables+$costHistoryTables|Sort-Object -Unique)) {
        $costColumns=@(Read-CostRows "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.$costTable') ORDER BY column_id")
        if(-not $costColumns.Count){throw "Protected table $costTable is missing."}
        $costExcluded=$costCacheColumns[$costTable]
        # SQL's engine-generated rowversion advances even for an average-only
        # UPDATE. Check it separately, and only for the explicitly verified
        # InventoryItems timestamp column; all business/audit columns stay here.
        $costSelect=($costColumns|Where-Object{$_.name -ne $costExcluded -and -not($costTable -eq 'InventoryItems' -and $_.name -eq 'RowVersion')}|ForEach-Object{'['+$_.name.Replace(']',']]')+']'}) -join ','
        $costWhere=if($costTable -in $costItemTables){if($costTable -eq 'InventoryItems'){"WHERE Id IN($costIds)"}else{"WHERE InventoryItemId IN($costIds)"}}else{''}
        Read-CostRows @"
SELECT N'$costTable' TableName,COUNT_BIG(*) [RowCount],
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(nvarchar(max),
 (SELECT $costSelect FROM dbo.[$costTable] $costWhere ORDER BY Id FOR JSON PATH,INCLUDE_NULL_VALUES))),2) RecordHash
FROM dbo.[$costTable] $costWhere;
"@
    })
    [pscustomobject]@{Database=$Database;Server=$costServer;TenantId=$costTenant;HelperSha256=$costScriptHash;
        Items=$costItems;Projections=@($costProjection|Sort-Object Table,Id);ProtectedRecords=$costFingerprints;
        ConcurrencyTokens=@(Read-CostRows "SELECT Id,CONVERT(varchar(16),CONVERT(binary(8),RowVersion),2) Token FROM dbo.InventoryItems WHERE Id IN($costIds) ORDER BY Id");
        History=@(Read-CostRows 'SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId')}
}
try {
    $costConnection.Open()
    $costIdentity=@(Read-CostRows "SELECT DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName")[0]
    if($costIdentity.DatabaseName -ne $Database -or $costIdentity.ServerName -ne $costServer){throw 'Exact local server/database identity was not verified.'}
    # These are the existing deployed precision contracts; never hide a schema mismatch.
    $costPrecision=@(Read-CostRows @'
SELECT OBJECT_NAME(object_id) TableName,name ColumnName,precision,scale FROM sys.columns
WHERE (object_id=OBJECT_ID(N'dbo.InventoryBalances') AND name IN(N'QuantityOnHand',N'TotalValue',N'AverageUnitCost'))
 OR (object_id IN(OBJECT_ID(N'dbo.InventoryItems'),OBJECT_ID(N'dbo.WarehouseQuantities'),OBJECT_ID(N'dbo.InventoryLocations')) AND name=N'AverageCost');
'@)
    if($costPrecision.Count -ne 6 -or @($costPrecision|Where-Object{$_.precision -ne 18 -or $_.scale -ne $(if($_.ColumnName -eq 'QuantityOnHand'){4}else{2})}).Count){throw 'Current valuation quantity/value/average precision is not the reviewed contract.'}
    $costVersionColumn=@(Read-CostRows "SELECT system_type_id,max_length,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.InventoryItems') AND name=N'RowVersion'")
    if($costVersionColumn.Count -ne 1 -or $costVersionColumn[0].system_type_id -ne 189 -or $costVersionColumn[0].max_length -ne 8 -or $costVersionColumn[0].is_nullable){throw 'Only the verified nonnullable SQL timestamp may be excluded from the business fingerprint.'}
    if($Mode -eq 'Apply') {
        if(-not $ConfirmApply -or -not $ReviewedPlanSha256 -or -not $VerifiedBackupPath){throw 'Apply requires ConfirmApply, ReviewedPlanSha256 and VerifiedBackupPath.'}
        $costBackup=(Resolve-Path -LiteralPath $VerifiedBackupPath).Path
        if(-not(Test-Path -LiteralPath $costBackup -PathType Leaf)){throw 'Verified database backup file is unavailable.'}
        $costBackupHeader=@(Read-CostRows 'RESTORE HEADERONLY FROM DISK=@backup' @{backup=$costBackup})
        if($costBackupHeader.Count -ne 1 -or $costBackupHeader[0].DatabaseName -ne $Database -or $costBackupHeader[0].BackupType -ne 1){throw 'Backup is not one full backup of the exact target database.'}
        [void](Read-CostRows 'RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM' @{backup=$costBackup})
    }
    $costTransaction=$costConnection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    [void](Read-CostRows 'SET XACT_ABORT ON; SET LOCK_TIMEOUT 5000; SET DEADLOCK_PRIORITY LOW;')
    if($Mode -ne 'Plan') {
        # Same per-item locking order as the application projection; retain locks
        # until the cache-only updates and preservation checks have all succeeded.
        [void](Read-CostRows "SELECT Id FROM dbo.InventoryItems WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@tenant AND ItemCode IN(N'SKU-001',N'PM-BARCODE-DEVICE') ORDER BY Id" @{tenant=$costTenant})
    }
    $costBefore=Read-CostPlan
    $costPlanHash=Get-CostHash $costBefore
    $costChanges=@($costBefore.Projections|Where-Object{$_.Current -ne $_.Expected})
    if($Mode -eq 'Plan') {
        $costTransaction.Rollback();$costTransaction.Dispose();$costTransaction=$null
        [pscustomobject]@{Mode='Plan';Database=$Database;PlanSha256=$costPlanHash;Changes=$costChanges;Applied=$false;QuantityAndHistoryChanges=0}|ConvertTo-Json -Depth 8
        return
    }
    if($costPlanHash -ne $ReviewedPlanSha256){throw 'The reviewed data/helper plan changed. No repair was performed; obtain a new Plan.'}
    foreach($costChange in $costChanges) {
        if($costCacheColumns[$costChange.Table] -ne $costChange.Column){throw 'Unreviewed cache column requested.'}
        $costUpdated=@(Read-CostRows "UPDATE dbo.[$($costChange.Table)] SET [$($costChange.Column)]=@value WHERE Id=@id AND TenantId=@tenant AND [$($costChange.Column)]=@before; SELECT @@ROWCOUNT AffectedRows;" @{value=$costChange.Expected;id=$costChange.Id;tenant=$costTenant;before=$costChange.Current})[0].AffectedRows
        if($costUpdated -ne 1){throw 'Exact cache row did not match the reviewed value.'}
    }
    $costAfter=Read-CostPlan
    if((Get-CostHash $costBefore.ProtectedRecords) -ne (Get-CostHash $costAfter.ProtectedRecords) -or
       (Get-CostHash $costBefore.History) -ne (Get-CostHash $costAfter.History)){
        $costChangedTables=@($costBefore.ProtectedRecords|Where-Object{$costOldRecord=$_;$costNewRecord=$costAfter.ProtectedRecords|Where-Object TableName -eq $costOldRecord.TableName;(Get-CostHash $costOldRecord) -ne (Get-CostHash $costNewRecord)}|ForEach-Object TableName)
        throw "Quantity, authoritative value, posting history, metadata or migration history changed ($($costChangedTables -join ', ')); cache repair is rolled back."
    }
    $costChangedItemIds=@($costChanges|Where-Object Table -eq 'InventoryItems'|ForEach-Object Id)
    foreach($costOldToken in $costBefore.ConcurrencyTokens){
        $costNewTokens=@($costAfter.ConcurrencyTokens|Where-Object Id -eq $costOldToken.Id)
        if($costNewTokens.Count -ne 1 -or $costNewTokens[0].Token -notmatch '^[A-Fa-f0-9]{16}$'){throw 'Inventory item concurrency identity was not retained.'}
        $costNewToken=$costNewTokens[0]
        if($costOldToken.Id -in $costChangedItemIds){
            if([UInt64]::Parse($costNewToken.Token,[Globalization.NumberStyles]::HexNumber) -le [UInt64]::Parse($costOldToken.Token,[Globalization.NumberStyles]::HexNumber)){throw 'The changed item did not receive the required SQL-generated newer concurrency token.'}
        } elseif($costOldToken.Token -ne $costNewToken.Token){throw 'An untouched item concurrency token changed; no repair is allowed.'}
    }
    if(@($costAfter.Projections|Where-Object{$_.Current -ne $_.Expected}).Count){throw 'Current costs do not reconcile after the proposed cache updates.'}
    if($Mode -eq 'RollbackProbe'){
        $costTransaction.Rollback();$costTransaction.Dispose();$costTransaction=$null
        $costAfterRollback=Read-CostPlan
        if((Get-CostHash $costAfterRollback) -ne $costPlanHash){throw 'Rollback preservation was not verified; inspect the database before proceeding.'}
        [pscustomobject]@{Mode='RollbackProbe';Database=$Database;Applied=$false;RowsWouldChange=$costChanges.Count;Changes=$costChanges;ProtectedRecordsStable=$true;ConcurrencyTokenRowsChanged=$costChangedItemIds.Count;RollbackVerified=$true;QuantityAndHistoryChanges=0}|ConvertTo-Json -Depth 8
        return
    }
    $costCommitAttempted=$true
    $costTransaction.Commit();$costCommitted=$true
    $costTransaction.Dispose();$costTransaction=$null
    [pscustomobject]@{Mode='Apply';Database=$Database;Applied=$true;RowsChanged=$costChanges.Count;Changes=$costChanges;QuantityAndHistoryChanges=0;VerifiedBackup=$costBackup}|ConvertTo-Json -Depth 8
} catch {
    if($costCommitAttempted){throw "Commit was attempted (confirmed=$costCommitted). Do not retry automatically; inspect current costs. Error: $($_.Exception.Message)"}
    if($costTransaction){$costTransaction.Rollback();$costTransaction.Dispose();$costTransaction=$null}
    throw
} finally {
    if($costTransaction){$costTransaction.Dispose()}
    $costConnection.Dispose()
}
