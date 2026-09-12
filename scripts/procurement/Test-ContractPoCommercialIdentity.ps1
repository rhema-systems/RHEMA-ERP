param([Parameter(Mandatory)][System.Data.Common.DbConnection]$SourceConnection)
$ErrorActionPreference = 'Stop'
$taskOriginalDatabase = $SourceConnection.Database
$taskDatabase = 'RhemaERP_ContractIdentityTest_' + [guid]::NewGuid().ToString('N')
if ($taskDatabase -notmatch '^RhemaERP_ContractIdentityTest_[a-f0-9]{32}$') { throw 'Invalid owned test database name.' }
function Invoke-TestSql([string]$sql) {
    $taskCmd = $SourceConnection.CreateCommand()
    try { $taskCmd.CommandText=$sql; $taskCmd.CommandTimeout=60; [void]$taskCmd.ExecuteNonQuery() }
    finally { $taskCmd.Dispose() }
}
function Read-TestScalar([string]$sql) {
    $taskCmd=$SourceConnection.CreateCommand()
    try { $taskCmd.CommandText=$sql; $taskCmd.ExecuteScalar() } finally { $taskCmd.Dispose() }
}
function Assert-Blocked([string]$name,[string]$sql,[int]$number) {
    Invoke-TestSql 'BEGIN TRANSACTION'
    $taskCaught=$false
    try { Invoke-TestSql $sql }
    catch {
        $taskException=$_.Exception
        while($taskException.InnerException){$taskException=$taskException.InnerException}
        if($taskException.Number -ne $number){throw}
        $taskCaught=$true
    } finally { Invoke-TestSql 'IF @@TRANCOUNT>0 ROLLBACK TRANSACTION' }
    if(-not $taskCaught){throw "Guard bypassed: $name"}
    "PASS: $name ($number)"
}
$taskCreated=$false
try {
    $SourceConnection.ChangeDatabase('master')
    Invoke-TestSql "CREATE DATABASE [$taskDatabase]"
    $taskCreated=$true
    $SourceConnection.ChangeDatabase($taskDatabase)
    Invoke-TestSql (Get-Content (Join-Path $PSScriptRoot '../../tests/sql/contract-po-commercial-identity-fixture.sql') -Raw)
    $taskBaseline=Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260730204500_TDC0403CommercialCapacityHardStops.cs') -Raw
    $taskRepairSource=Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260908153000_AlignContractPurchaseOrderCommercialIdentity.cs') -Raw
    $taskRepair=[regex]::Match($taskRepairSource,'(?s)internal const string RepairSql = """\s*(.*?)\s*""";').Groups[1].Value
    if(!$taskRepair){throw 'Production migration SQL not found'}
    $taskRfqSource=Get-Content (Join-Path $PSScriptRoot '../../src/ErpSystem.Data/Migrations/20260827211500_AlignRfqCommercialIdentityWithReceiptItemMaster.cs') -Raw
    $taskFunction=[regex]::Match($taskRfqSource,'(?s)private const string CreateIdentityFunctionSql = """\s*(.*?)\s*""";').Groups[1].Value
    Invoke-TestSql $taskFunction
    $taskLegacyCase=@'
CASE
-- TDC0502_RFQ_ITEM_MASTER_LINEAGE
WHEN item.SourceRfqItemId IS NOT NULL THEN dbo.fn_ProcurementRfqSourceLineIdentity(item.TenantId,item.SourceRfqItemId)
WHEN item.InventoryItemId IS NOT NULL THEN CONCAT('inventory:',LOWER(CONVERT(varchar(36),item.InventoryItemId)))
ELSE CONCAT('description:',LOWER(LTRIM(RTRIM(ISNULL(item.ItemDescription,''))))) END
'@
    $taskNames=@('TR_PurchaseOrders_ApprovedCommercialCapacity','TR_PurchaseOrderItems_ApprovedCommercialCapacity')
    $taskBaselineTriggers=@{}
    $taskRfqPrefixes=@{}
    foreach($taskName in $taskNames){
        $taskTrigger=[regex]::Match($taskBaseline,'(?s)CREATE OR ALTER TRIGGER \['+[regex]::Escape($taskName)+'\].*?(?=\s*""")').Value
        if(!$taskTrigger){throw "Production trigger missing: $taskName"}
        # Install original RFQ lineage patch on the original RFQ clauses.
        $taskTrigger=$taskTrigger.Replace('WHEN item.InventoryItemId IS NOT NULL',"WHEN item.SourceRfqItemId IS NOT NULL THEN dbo.fn_ProcurementRfqSourceLineIdentity(item.TenantId,item.SourceRfqItemId) WHEN item.InventoryItemId IS NOT NULL")
        $taskBaselineTriggers[$taskName]=$taskTrigger
        Invoke-TestSql $taskTrigger
    }
    Invoke-TestSql $taskRepair
    'PASS: Already-correct source baseline is accepted without weakening guards'
    # Reproduce the legacy deployed contract expressions in BOTH triggers.
    foreach($taskName in $taskNames){
        $taskTrigger=$taskBaselineTriggers[$taskName]
        $taskStart=$taskTrigger.IndexOf('purchaseOrder.ProcurementSourceId AS ContractId,')
        $taskEnd=$taskTrigger.IndexOf(') actual',$taskStart)
        $taskActual=$taskTrigger.Substring($taskStart,$taskEnd-$taskStart)
        $taskPattern="CONCAT\(\s*'description:',\s*LOWER\(LTRIM\(RTRIM\(\s*ISNULL\(item.ItemDescription,\s*''\)\)\)\)\)"
        if([regex]::Matches($taskActual,$taskPattern).Count -ne 2){throw 'Expected two original contract identities'}
        $taskLegacy=[regex]::Replace($taskActual,$taskPattern,$taskLegacyCase)
        $taskTrigger=$taskTrigger.Substring(0,$taskStart)+$taskLegacy+$taskTrigger.Substring($taskEnd)
        Invoke-TestSql $taskTrigger
        $taskStored=[string](Read-TestScalar "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.$taskName'))")
        $taskRfqPrefixes[$taskName]=$taskStored.Substring(0,$taskStored.IndexOf('purchaseOrder.ProcurementSourceId AS ContractId,'))
    }
    $taskInsert=@'
INSERT PurchaseOrderItems
SELECT NEWID(),ti.TenantId,'30000000-0000-0000-0000-000000000001',NEWID(),NULL,ti.Description,ti.UnitOfMeasure,bi.UnitPrice,bi.OfferedQuantity,bi.UnitPrice*bi.OfferedQuantity,0
FROM TenderItems ti JOIN TenderBidItems bi ON bi.TenderItemId=ti.Id;
'@
    Assert-Blocked 'Legacy mapped contract draft reproduces reported save failure' $taskInsert 51228
    Invoke-TestSql $taskRepair
    Invoke-TestSql $taskRepair
    'PASS: Repair is idempotent on installed legacy triggers'
    foreach($taskName in $taskNames){
        $taskStored=[string](Read-TestScalar "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.$taskName'))")
        $taskPrefix=$taskStored.Substring(0,$taskStored.IndexOf('purchaseOrder.ProcurementSourceId AS ContractId,'))
        # ALTER changes the declaration only; RFQ and header currency protections remain byte-for-byte unchanged.
        $taskBefore=$taskRfqPrefixes[$taskName]
        $taskBefore=$taskBefore.Substring($taskBefore.IndexOf('TRIGGER'))
        $taskPrefix=$taskPrefix.Substring($taskPrefix.IndexOf('TRIGGER'))
        if($taskPrefix -cne $taskBefore){throw "Non-contract trigger prefix changed: $taskName"}
    }
    'PASS: RFQ clauses, currency and header total guards are unchanged'
    Invoke-TestSql $taskInsert
    Invoke-TestSql "UPDATE PurchaseOrders SET Status='Submitted' WHERE ProcurementSourceType=2"
    if((Read-TestScalar 'SELECT COUNT(*) FROM PurchaseOrderItems') -ne 2){throw 'Valid mapped lines not retained'}
    'PASS: Mapped EA and EACH draft lines save and header submission succeeds'
    Assert-Blocked 'Over-quantity item update' "UPDATE PurchaseOrderItems SET OrderedQuantity=21,LineTotal=21*UnitPrice WHERE UnitOfMeasure='EACH'" 51228
    Assert-Blocked 'Unapproved unit' "UPDATE PurchaseOrderItems SET UnitOfMeasure='EA' WHERE UnitOfMeasure='EACH'" 51228
    Assert-Blocked 'Unapproved price' 'UPDATE PurchaseOrderItems SET UnitPrice=UnitPrice+1,LineTotal=OrderedQuantity*(UnitPrice+1)' 51228
    Assert-Blocked 'Unapproved description' "UPDATE PurchaseOrderItems SET ItemDescription='Substitute'" 51228
    Assert-Blocked 'Forged line total' 'UPDATE PurchaseOrderItems SET LineTotal=LineTotal-1' 51226
    Assert-Blocked 'Header currency mismatch' "UPDATE PurchaseOrders SET Currency='USD' WHERE ProcurementSourceType=2" 51224
    Assert-Blocked 'Header total over contract value' 'UPDATE PurchaseOrders SET TotalAmount=52001 WHERE ProcurementSourceType=2' 51224
    Assert-Blocked 'Cross-tenant approved line cannot satisfy contract capacity' "UPDATE TenderItems SET TenantId=NEWID(); UPDATE PurchaseOrderItems SET ItemDescription=ItemDescription" 51228
    Assert-Blocked 'Header transition checks the same quantity ceiling' "UPDATE TenderBidItems SET OfferedQuantity=19; UPDATE PurchaseOrders SET Status='Approved' WHERE ProcurementSourceType=2" 51225
    Assert-Blocked 'A different mapped stock ID cannot evade cumulative capacity' @'
UPDATE PurchaseOrders SET TotalAmount=51000 WHERE ProcurementSourceType=2;
INSERT PurchaseOrders SELECT '30000000-0000-0000-0000-000000000003',TenantId,2,ProcurementSourceId,BusinessPartnerId,'GHS',700,'Draft',0 FROM PurchaseOrders WHERE ProcurementSourceType=2;
INSERT PurchaseOrderItems SELECT NEWID(),TenantId,'30000000-0000-0000-0000-000000000003',NEWID(),NULL,ItemDescription,UnitOfMeasure,UnitPrice,1,UnitPrice,0 FROM PurchaseOrderItems WHERE UnitOfMeasure='EA';
'@ 51228
    Invoke-TestSql @'
UPDATE PurchaseOrderItems SET OrderedQuantity=10,LineTotal=10*UnitPrice;
UPDATE PurchaseOrders SET TotalAmount=26000 WHERE ProcurementSourceType=2;
INSERT PurchaseOrders SELECT '30000000-0000-0000-0000-000000000003',TenantId,2,ProcurementSourceId,BusinessPartnerId,'GHS',26000,'Draft',0 FROM PurchaseOrders WHERE ProcurementSourceType=2;
INSERT PurchaseOrderItems SELECT NEWID(),TenantId,'30000000-0000-0000-0000-000000000003',NEWID(),NULL,ItemDescription,UnitOfMeasure,UnitPrice,10,10*UnitPrice,0 FROM PurchaseOrderItems;
'@
    'PASS: Two partial contract orders can cumulatively consume the exact awarded quantities'
    Invoke-TestSql @'
INSERT PurchaseOrderItems SELECT NEWID(),TenantId,'30000000-0000-0000-0000-000000000002',NEWID(),Id,'RFQ mapped item','EA',50,2,100,0 FROM RequestForQuotationItems;
UPDATE PurchaseOrders SET Status='Submitted' WHERE ProcurementSourceType=0;
'@
    'PASS: RFQ mapped stock still uses retained RFQ source-line identity'
    Assert-Blocked 'RFQ excess quantity still rejected' "UPDATE PurchaseOrderItems SET OrderedQuantity=3,LineTotal=150 WHERE SourceRfqItemId IS NOT NULL" 51227
    Assert-Blocked 'RFQ supplier ownership still checked' "UPDATE PurchaseOrders SET BusinessPartnerId=NEWID() WHERE ProcurementSourceType=0" 51222
    # New classifications remain subject to the same approved-source commercial caps.
    Invoke-TestSql 'ALTER TABLE PurchaseOrderItems ADD LineType int NOT NULL DEFAULT 1;'
    Invoke-TestSql 'ALTER TABLE PurchaseOrderItems ADD CONSTRAINT CK_PurchaseOrderItems_LineType CHECK (LineType IN (1,2,3,4));'
    if ([int](Read-TestScalar 'SELECT COUNT(*) FROM PurchaseOrderItems WHERE LineType<>1') -ne 0) { throw 'Historic classification was changed' }
    foreach ($taskLineType in @(1,2,3)) {
        Invoke-TestSql "UPDATE PurchaseOrderItems SET InventoryItemId=NULL, LineType=$taskLineType WHERE SourceRfqItemId IS NULL; UPDATE PurchaseOrders SET Status='Draft' WHERE ProcurementSourceType=2;"
        Assert-Blocked "Unmapped line type $taskLineType still enforces approved quantity" 'UPDATE PurchaseOrderItems SET OrderedQuantity=OrderedQuantity+1,LineTotal=(OrderedQuantity+1)*UnitPrice WHERE SourceRfqItemId IS NULL' 51228
        "PASS: Descriptive PO type $taskLineType persists without an inventory ID"
    }
    Assert-Blocked 'Unknown line classification rejected' 'UPDATE PurchaseOrderItems SET LineType=99' 547
    # A drifted key is rejected instead of silently removing unknown logic.
    $taskStored=[string](Read-TestScalar "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.TR_PurchaseOrderItems_ApprovedCommercialCapacity'))")
    $taskStored=$taskStored.Replace("ISNULL(item.ItemDescription,'')","ISNULL(item.ItemDescription,'unexpected')")
    Invoke-TestSql ($taskStored -replace '^CREATE OR ALTER','ALTER' -replace '^CREATE','ALTER')
    Assert-Blocked 'Unrecognized trigger drift fails closed' $taskRepair 51984
    'PASSED: Contract PO commercial identity SQL Server regression suite'
} finally {
    $SourceConnection.ChangeDatabase('master')
    if($taskCreated){Invoke-TestSql "ALTER DATABASE [$taskDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$taskDatabase]"}
    $SourceConnection.ChangeDatabase($taskOriginalDatabase)
}
