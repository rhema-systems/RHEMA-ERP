-- Sync tools from InventoryItems (ItemType = 4) to MaintenanceTools table
-- This allows WorkOrderTool foreign key constraint to be satisfied

-- Insert tools that don't already exist in MaintenanceTools
INSERT INTO MaintenanceTools (
    Id,
    TenantId,
    ToolCode,
    Name,
    Description,
    Category,
    Manufacturer,
    Model,
    SerialNumber,
    PurchaseDate,
    PurchaseCost,
    CurrentValue,
    DailyRentalRate,
    Status,
    CurrentLocation,
    IsActive,
    CreatedAt,
    CreatedBy,
    IsDeleted
)
SELECT 
    ii.Id,
    ii.TenantId,
    ii.ItemCode AS ToolCode,
    ii.Name,
    ii.Description,
    COALESCE(ic.Name, 'Uncategorized') AS Category,
    ii.Manufacturer,
    ii.Model,
    NULL AS SerialNumber, -- InventoryItems doesn't track serial numbers
    NULL AS PurchaseDate,
    ii.StandardCost AS PurchaseCost,
    ii.StandardCost AS CurrentValue,
    0 AS DailyRentalRate, -- Default to 0, can be updated later
    'Available' AS Status,
    NULL AS CurrentLocation,
    CASE WHEN ii.Status = 1 THEN 1 ELSE 0 END AS IsActive, -- Status 1 = Active
    GETUTCDATE() AS CreatedAt,
    'SYSTEM' AS CreatedBy,
    0 AS IsDeleted
FROM InventoryItems ii
LEFT JOIN InventoryCategories ic ON ii.CategoryId = ic.Id
WHERE ii.ItemType = 4 -- Tools
  AND ii.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1 
      FROM MaintenanceTools mt 
      WHERE mt.Id = ii.Id
  );

PRINT 'Synced ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' tools from InventoryItems to MaintenanceTools';
