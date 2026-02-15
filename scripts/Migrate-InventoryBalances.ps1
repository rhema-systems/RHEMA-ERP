# ============================================================================
# Migrate Existing Warehouse Quantities to Inventory Balances
# ============================================================================
# This script populates the new InventoryBalances table from existing
# WarehouseQuantities data to support the new valuation system.
# ============================================================================

Write-Host "Starting migration to InventoryBalances..." -ForegroundColor Cyan

# Get the connection string from appsettings
$appsettingsPath = "src/ErpSystem.Api/appsettings.Development.json"
if (Test-Path $appsettingsPath) {
    $appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
    $connectionString = $appsettings.ConnectionStrings.DefaultConnection
    Write-Host "Using connection string from appsettings.Development.json" -ForegroundColor Green
} else {
    Write-Host "Error: appsettings.Development.json not found" -ForegroundColor Red
    exit 1
}

# SQL Script
$sqlScript = @"
-- Insert balances from WarehouseQuantities
INSERT INTO InventoryBalances (
    Id, InventoryItemId, WarehouseId, LocationId,
    QuantityOnHand, QuantityAllocated, QuantityAvailable, QuantityOnOrder,
    TotalValue, AverageUnitCost,
    LastMovementDate, LastReceiptDate, LastIssueDate, LastCountDate, LastRecalculatedAt,
    CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
    IsDeleted, DeletedAt, DeletedBy, TenantId
)
SELECT 
    NEWID(), wq.InventoryItemId, wq.WarehouseId, NULL,
    wq.CurrentStock, wq.AllocatedStock, wq.AvailableStock, 0,
    wq.CurrentStock * wq.AverageCost, wq.AverageCost,
    wq.LastMovementDate, NULL, NULL, wq.LastStockTakeDate, GETUTCDATE(),
    wq.CreatedAt, wq.UpdatedAt, wq.CreatedBy, wq.UpdatedBy, wq.CreatedById, wq.LastModifiedById,
    wq.IsDeleted, wq.DeletedAt, wq.DeletedBy, wq.TenantId
FROM WarehouseQuantities wq
WHERE NOT EXISTS (
    SELECT 1 FROM InventoryBalances ib 
    WHERE ib.InventoryItemId = wq.InventoryItemId 
    AND ib.WarehouseId = wq.WarehouseId
    AND ib.LocationId IS NULL
);

-- Update InventoryItems with consolidated quantities
UPDATE ii
SET 
    ii.CurrentStock = ISNULL(consolidated.TotalOnHand, 0),
    ii.AvailableStock = ISNULL(consolidated.TotalAvailable, 0),
    ii.AllocatedStock = ISNULL(consolidated.TotalAllocated, 0),
    ii.AverageCost = ISNULL(consolidated.WeightedAvgCost, ii.AverageCost)
FROM InventoryItems ii
LEFT JOIN (
    SELECT 
        InventoryItemId,
        SUM(QuantityOnHand) as TotalOnHand,
        SUM(QuantityAllocated) as TotalAllocated,
        SUM(QuantityAvailable) as TotalAvailable,
        CASE 
            WHEN SUM(QuantityOnHand) > 0 
            THEN SUM(TotalValue) / SUM(QuantityOnHand)
            ELSE 0 
        END as WeightedAvgCost
    FROM InventoryBalances
    WHERE IsDeleted = 0
    GROUP BY InventoryItemId
) consolidated ON ii.Id = consolidated.InventoryItemId;

-- Verification
SELECT 
    'Migration Results' as Status,
    (SELECT COUNT(*) FROM InventoryBalances WHERE IsDeleted = 0) as BalanceRecords,
    (SELECT SUM(QuantityOnHand) FROM InventoryBalances WHERE IsDeleted = 0) as TotalQuantity,
    (SELECT SUM(TotalValue) FROM InventoryBalances WHERE IsDeleted = 0) as TotalValue,
    (SELECT COUNT(*) FROM InventoryItems WHERE CurrentStock < 0 AND IsDeleted = 0) as NegativeStockItems;
"@

try {
    # Execute using dotnet ef dbcontext scaffold or direct SQL
    Write-Host "Executing migration script..." -ForegroundColor Yellow
    
    # Use SqlServer module if available
    if (Get-Module -ListAvailable -Name SqlServer) {
        Import-Module SqlServer
        Invoke-Sqlcmd -ConnectionString $connectionString -Query $sqlScript -Verbose
        Write-Host "Migration completed successfully!" -ForegroundColor Green
    } else {
        # Fallback: Save script and provide instructions
        $scriptPath = "scripts/MigrateToInventoryBalances_Generated.sql"
        $sqlScript | Out-File -FilePath $scriptPath -Encoding UTF8
        Write-Host "SQL script saved to: $scriptPath" -ForegroundColor Yellow
        Write-Host "Please run this script manually using SQL Server Management Studio or:" -ForegroundColor Yellow
        Write-Host "  sqlcmd -S localhost -d ErpSystemDb -E -i $scriptPath" -ForegroundColor Cyan
    }
} catch {
    Write-Host "Error during migration: $_" -ForegroundColor Red
    Write-Host "SQL script has been saved to scripts/MigrateToInventoryBalances.sql" -ForegroundColor Yellow
    Write-Host "Please run it manually" -ForegroundColor Yellow
}
