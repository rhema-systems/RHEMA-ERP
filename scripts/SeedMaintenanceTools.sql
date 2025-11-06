-- Script to populate MaintenanceTools table from existing InventoryItems
-- This assumes you have tools/equipment already in the Inventory module

-- Step 1: Check existing inventory items that could be tools
SELECT 
    Id,
    ItemCode,
    Name,
    Description,
    Brand,
    Manufacturer,
    Model,
    ItemType,
    StandardCost,
    CurrentStock
FROM InventoryItems
WHERE ItemType = 4  -- FixedAsset
   OR Name LIKE '%tool%'
   OR Name LIKE '%equipment%'
   OR Name LIKE '%machine%'
   OR Category IN (SELECT Id FROM InventoryCategories WHERE Name LIKE '%Tool%' OR Name LIKE '%Equipment%');

-- Step 2: Insert tools into MaintenanceTools table from InventoryItems
-- Adjust this based on your actual inventory data
INSERT INTO MaintenanceTools (
    Id,
    ToolCode,
    Name,
    Description,
    Category,
    Manufacturer,
    Model,
    SerialNumber,
    Status,
    CurrentLocation,
    HomeLocation,
    PurchasePrice,
    CurrentValue,
    DailyRentalRate,
    TotalUsageDays,
    LastUsedDate,
    RequiresCertification,
    RequiresTraining,
    SafetyNotes,
    IsActive,
    TenantId,
    CreatedAt,
    CreatedBy
)
SELECT 
    NEWID() as Id,
    ii.ItemCode as ToolCode,
    ii.Name,
    ISNULL(ii.Description, '') as Description,
    ISNULL(ic.Name, 'General') as Category,
    ISNULL(ii.Manufacturer, '') as Manufacturer,
    ISNULL(ii.Model, '') as Model,
    CAST(ii.Id AS NVARCHAR(100)) as SerialNumber,  -- Using inventory ID as temp serial
    'Available' as Status,
    'Main Warehouse' as CurrentLocation,
    'Main Warehouse' as HomeLocation,
    ISNULL(ii.StandardCost, 0) as PurchasePrice,
    ISNULL(ii.StandardCost, 0) as CurrentValue,
    ROUND(ISNULL(ii.StandardCost, 0) * 0.05, 2) as DailyRentalRate,  -- 5% of cost per day
    0 as TotalUsageDays,
    NULL as LastUsedDate,
    CASE 
        WHEN ii.Name LIKE '%welding%' OR ii.Name LIKE '%plasma%' OR ii.Name LIKE '%lathe%' THEN 1
        ELSE 0
    END as RequiresCertification,
    CASE 
        WHEN ii.Name LIKE '%power%' OR ii.Name LIKE '%machine%' OR ii.Name LIKE '%drill%' THEN 1
        ELSE 0
    END as RequiresTraining,
    'Please refer to equipment manual for safety guidelines.' as SafetyNotes,
    1 as IsActive,
    ii.TenantId,
    GETUTCDATE() as CreatedAt,
    'System-Migration' as CreatedBy
FROM InventoryItems ii
LEFT JOIN InventoryCategories ic ON ii.CategoryId = ic.Id
WHERE 
    ii.ItemType = 4  -- FixedAsset type
    AND NOT EXISTS (
        SELECT 1 FROM MaintenanceTools mt 
        WHERE mt.ToolCode = ii.ItemCode AND mt.TenantId = ii.TenantId
    );

-- Step 3: Verify the inserted tools
SELECT 
    ToolCode,
    Name,
    Category,
    Status,
    PurchasePrice,
    DailyRentalRate,
    RequiresCertification,
    RequiresTraining
FROM MaintenanceTools
ORDER BY CreatedAt DESC;

-- Step 4: Show summary statistics
SELECT 
    Status,
    COUNT(*) as ToolCount,
    SUM(PurchasePrice) as TotalValue
FROM MaintenanceTools
GROUP BY Status
ORDER BY Status;
