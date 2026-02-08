-- ============================================================================
-- Migrate Existing Warehouse Quantities to Inventory Balances
-- ============================================================================
-- This script populates the new InventoryBalances table from existing
-- WarehouseQuantities data to support the new valuation system.
-- ============================================================================

PRINT 'Starting migration to InventoryBalances...';

-- Insert balances from WarehouseQuantities
INSERT INTO InventoryBalances (
    Id,
    InventoryItemId,
    WarehouseId,
    LocationId,
    QuantityOnHand,
    QuantityAllocated,
    QuantityAvailable,
    QuantityOnOrder,
    TotalValue,
    AverageUnitCost,
    LastMovementDate,
    LastReceiptDate,
    LastIssueDate,
    LastCountDate,
    LastRecalculatedAt,
    CreatedAt,
    UpdatedAt,
    CreatedBy,
    UpdatedBy,
    CreatedById,
    LastModifiedById,
    IsDeleted,
    DeletedAt,
    DeletedBy,
    TenantId
)
SELECT 
    NEWID() as Id,
    wq.InventoryItemId,
    wq.WarehouseId,
    NULL as LocationId, -- WarehouseQuantities doesn't track location
    wq.CurrentStock as QuantityOnHand,
    wq.AllocatedStock as QuantityAllocated,
    wq.AvailableStock as QuantityAvailable,
    0 as QuantityOnOrder, -- Not tracked in WarehouseQuantities
    wq.CurrentStock * wq.AverageCost as TotalValue,
    wq.AverageCost as AverageUnitCost,
    wq.LastMovementDate,
    NULL as LastReceiptDate, -- Not tracked separately
    NULL as LastIssueDate, -- Not tracked separately
    wq.LastStockTakeDate as LastCountDate,
    GETUTCDATE() as LastRecalculatedAt,
    wq.CreatedAt,
    wq.UpdatedAt,
    wq.CreatedBy,
    wq.UpdatedBy,
    wq.CreatedById,
    wq.LastModifiedById,
    wq.IsDeleted,
    wq.DeletedAt,
    wq.DeletedBy,
    wq.TenantId
FROM WarehouseQuantities wq
WHERE NOT EXISTS (
    SELECT 1 
    FROM InventoryBalances ib 
    WHERE ib.InventoryItemId = wq.InventoryItemId 
    AND ib.WarehouseId = wq.WarehouseId
    AND ib.LocationId IS NULL
);

PRINT 'Migrated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' warehouse quantities to InventoryBalances';

-- Update InventoryItems with consolidated quantities from InventoryBalances
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

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR) + ' inventory items with consolidated quantities';

-- Verify results
SELECT 
    'InventoryBalances' as TableName,
    COUNT(*) as RecordCount,
    SUM(QuantityOnHand) as TotalQuantity,
    SUM(TotalValue) as TotalValue
FROM InventoryBalances
WHERE IsDeleted = 0;

SELECT 
    'InventoryItems' as TableName,
    COUNT(*) as RecordCount,
    SUM(CurrentStock) as TotalQuantity,
    COUNT(CASE WHEN CurrentStock < 0 THEN 1 END) as NegativeStockItems
FROM InventoryItems
WHERE IsDeleted = 0;

PRINT 'Migration to InventoryBalances completed successfully!';
