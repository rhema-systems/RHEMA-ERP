param(
    [ValidateSet('RhemaERP', 'RhemaERP_PO_Rehearsal_20260909')]
    [string]$Database = 'RhemaERP'
)
$ErrorActionPreference = 'Stop'
$parityRepo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$parityExpectedSlice = @(
    '20260909213000_LinkLandedCostsToSupplierInvoiceLines',
    '20260911210000_PhysicalCountReviewDecisions',
    '20260912003000_WarehouseDefaultLocations',
    '20260912013000_AlignStockAdjustmentLocationValuation'
)
$paritySourceMigrations = @(Get-ChildItem -LiteralPath (Join-Path $parityRepo 'src/ErpSystem.Data/Migrations') -File |
    Where-Object { $_.Name -match '^\d{14}_.+\.cs$' -and $_.Name -notlike '*.Designer.cs' } |
    ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_.Name) } | Sort-Object)
Add-Type -AssemblyName System.Data
$parityConnection = [System.Data.SqlClient.SqlConnection]::new("Server=RHEMA-MICHAEL\SQL2017;Database=$Database;Integrated Security=True;ApplicationIntent=ReadOnly;Application Name=LocalUatMigrationParityReadOnly")

function Read-ParityRows([string]$Sql) {
    $parityCommand = $parityConnection.CreateCommand()
    $parityCommand.CommandTimeout = 60
    $parityCommand.CommandText = $Sql
    $parityReader = $parityCommand.ExecuteReader()
    try {
        while ($parityReader.Read()) {
            $parityValues = [ordered]@{}
            for ($parityIndex=0; $parityIndex -lt $parityReader.FieldCount; $parityIndex++) {
                $parityValues[$parityReader.GetName($parityIndex)] = if ($parityReader.IsDBNull($parityIndex)) { $null } else { $parityReader.GetValue($parityIndex) }
            }
            [pscustomobject]$parityValues
        }
    } finally { $parityReader.Dispose(); $parityCommand.Dispose() }
}

try {
    $parityConnection.Open()
    $parityIdentity = @(Read-ParityRows "SELECT DB_NAME() AS DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName")[0]
    if ($parityIdentity.DatabaseName -ne $Database -or $parityIdentity.ServerName -ne 'RHEMA-MICHAEL\SQL2017') {
        throw 'The exact permitted local database/server was not verified.'
    }
    $parityApplied = @(Read-ParityRows 'SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId' | ForEach-Object { $_.MigrationId })
    $parityPending = @($paritySourceMigrations | Where-Object { $_ -notin $parityApplied })
    $paritySchema = @(Read-ParityRows @'
SELECT COL_LENGTH(N'dbo.VendorInvoiceLineItem',N'LandedCostItemId') AS InvoiceLandedCostLinkBytes,
 COL_LENGTH(N'dbo.WarehouseLocations',N'IsDefault') AS WarehouseDefaultFlagBytes,
 COLUMNPROPERTY(OBJECT_ID(N'dbo.StockAdjustmentItems'),N'UnitCost',N'Scale') AS AdjustmentUnitCostScale,
 (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.VendorInvoiceLineItem')
  AND name=N'IX_VendorInvoiceLineItem_LandedCostItemId' AND is_unique=1 AND is_disabled=0) AS ActiveUniqueInvoiceLinkIndexes,
 (SELECT COUNT(*) FROM sys.foreign_keys WHERE name=N'FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId'
  AND is_disabled=0 AND is_not_trusted=0) AS TrustedInvoiceLinkForeignKeys,
 (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.WarehouseLocations')
  AND name=N'UX_WarehouseLocations_Default' AND is_unique=1 AND is_disabled=0) AS ActiveUniqueWarehouseDefaultIndexes;
'@)[0]
    $parityDefaultBins = @()
    if ($null -ne $paritySchema.WarehouseDefaultFlagBytes) {
        $parityDefaultBins = @(Read-ParityRows @'
SELECT w.Id AS WarehouseId,w.Code AS WarehouseCode,w.Name AS WarehouseName,
 COUNT(l.Id) AS ActiveDefaultCount,MAX(l.LocationCode) AS DefaultLocationCode,
 SUM(CASE WHEN l.LocationType=N'Bin' AND l.IsConsignmentBin=0 AND l.ConsignmentWarehouseId IS NULL
  AND l.IsQuarantineLocation=0 AND l.IsInspectionLocation=0 AND l.IsInTransitLocation=0
  AND l.IsShippingLocation=0 AND l.IsStagingLocation=0 AND l.IsReturnLocation=0 AND l.IsDamageLocation=0 THEN 1 ELSE 0 END) AS EligibleDefaultCount
FROM dbo.Warehouses w LEFT JOIN dbo.WarehouseLocations l ON l.WarehouseId=w.Id AND l.TenantId=w.TenantId
 AND l.IsDefault=1 AND l.IsActive=1 AND l.IsDeleted=0
WHERE w.IsDeleted=0 GROUP BY w.Id,w.Code,w.Name ORDER BY w.Code;
'@)
    }
    $parityGuards = @(Read-ParityRows @'
SELECT name,is_disabled,CONVERT(varchar(64),HASHBYTES('SHA2_256',OBJECT_DEFINITION(object_id)),2) AS DefinitionHash
FROM sys.triggers WHERE name IN(N'TR_StockAdjustmentItems_ControlledMutation',N'TR_PhysicalCountItems_ControlledMutation',
 N'TR_PhysicalCounts_ControlledLifecycle',N'TR_PhysicalCountActions_AppendOnly',N'TR_WarehouseQuantities_PhysicalCountFreeze',
 N'TR_InventoryItems_PhysicalCountFreeze',N'TR_StockMovements_PhysicalCountFreeze') ORDER BY name;
'@)
    $parityBaseline = @(Read-ParityRows @'
SELECT (SELECT COUNT(*) FROM dbo.PhysicalCounts) AS PhysicalCounts,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.PhysicalCounts) AS PhysicalCountChecksum,
 (SELECT COUNT(*) FROM dbo.PhysicalCountItems) AS CountItems,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.PhysicalCountItems) AS CountItemChecksum,
 (SELECT COUNT(*) FROM dbo.InventoryBalances) AS InventoryBalances,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.InventoryBalances) AS BalanceChecksum,
 (SELECT COUNT(*) FROM dbo.StockMovements) AS StockMovements,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.StockMovements) AS MovementChecksum,
 (SELECT COUNT(*) FROM dbo.StockAdjustments) AS StockAdjustments,
 (SELECT CHECKSUM_AGG(BINARY_CHECKSUM(*)) FROM dbo.StockAdjustments) AS AdjustmentChecksum;
'@)[0]
    [pscustomobject]@{
        Database=$Database; ReadOnly=$true; AppliedMigrationCount=$parityApplied.Count;
        LatestRecordedMigration=($parityApplied | Select-Object -Last 1);
        PendingMigrations=$parityPending;
        ExpectedSlice=@($parityExpectedSlice | ForEach-Object { [pscustomobject]@{Migration=$_;Applied=($_ -in $parityApplied)} });
        UnexpectedPendingMigrations=@($parityPending | Where-Object { $_ -notin $parityExpectedSlice });
        Schema=$paritySchema; DefaultBins=$parityDefaultBins; Guards=$parityGuards; PreservedDataBaseline=$parityBaseline
    } | ConvertTo-Json -Depth 6
} finally { $parityConnection.Dispose() }
