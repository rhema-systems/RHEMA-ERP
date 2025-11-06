-- Script to seed tools and equipment into the system
-- Part 1: Seed FixedAsset items into InventoryItems
-- Part 2: Create MaintenanceTools from those items

-- Get the first tenant and a tools category
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Tenants ORDER BY CreatedAt);
DECLARE @CategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM InventoryCategories WHERE TenantId = @TenantId);

-- If no category exists, create a Tools & Equipment category
IF @CategoryId IS NULL
BEGIN
    SET @CategoryId = NEWID();
    INSERT INTO InventoryCategories (Id, Name, Code, Description, IsActive, DefaultSerialTracking, DefaultLotTracking, DefaultRequiresInspection, IsDeleted, TenantId, CreatedAt, CreatedBy)
    VALUES (@CategoryId, 'Tools & Equipment', 'TOOLS', 'Maintenance tools and equipment', 1, 0, 0, 0, 0, @TenantId, GETUTCDATE(), 'System');
END

PRINT 'Using TenantId: ' + CAST(@TenantId AS NVARCHAR(50));
PRINT 'Using CategoryId: ' + CAST(@CategoryId AS NVARCHAR(50));

-- Step 1: Insert FixedAsset (tools) into InventoryItems
INSERT INTO InventoryItems (
    Id, ItemCode, Name, Description, CategoryId, Manufacturer, Model, 
    UnitOfMeasure, StandardCost, AverageCost, LastPurchaseCost, SalePrice, CurrentStock, AvailableStock,
    AllocatedStock, OnOrderStock, SafetyStock, MinimumLevel, MaximumLevel, ReorderLevel, ReorderQuantity,
    LeadTimeDays, SafetyLeadTimeDays, ItemType, ABCClass, Status, 
    IsSerialTracked, IsLotTracked, IsExpirationTracked, IsLocationTracked, RequiresInspection, IsDeleted,
    TenantId, CreatedAt, CreatedBy
)
VALUES
-- Hand Tools
(NEWID(), 'TOOL-001', 'Digital Torque Wrench', 'High-precision digital torque wrench, 0-100 Nm range', @CategoryId, 'Snap-On', 'TECH3FR100', 
 'EA', 450.00, 450.00, 450.00, 500.00, 1, 1, 0, 0, 0, 1, 5, 1, 1, 7, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-002', 'Hydraulic Jack 5-Ton', 'Heavy-duty hydraulic floor jack, 5-ton capacity', @CategoryId, 'Milwaukee', 'HJ-5000',
 'EA', 350.00, 350.00, 350.00, 400.00, 2, 2, 0, 0, 0, 1, 5, 1, 1, 7, 0, 4, 'B', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-003', 'Impact Wrench Cordless', 'Cordless impact wrench with battery and charger', @CategoryId, 'DeWalt', 'DCF899B',
 'EA', 280.00, 280.00, 280.00, 350.00, 3, 3, 0, 0, 0, 1, 10, 2, 2, 7, 0, 4, 'B', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

-- Power Tools
(NEWID(), 'TOOL-004', 'Industrial Drill Press', 'Variable speed industrial drill press with laser guide', @CategoryId, 'DeWalt', 'DWE1622K',
 'EA', 1200.00, 1200.00, 1200.00, 1500.00, 1, 1, 0, 0, 0, 1, 3, 1, 1, 14, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-005', 'Angle Grinder 9-inch', 'Heavy-duty angle grinder with variable speed control', @CategoryId, 'Makita', 'GA9040S',
 'EA', 280.00, 280.00, 280.00, 350.00, 2, 2, 0, 0, 0, 1, 5, 1, 1, 7, 0, 4, 'B', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-006', 'Welding Machine MIG', 'MIG welding machine with digital display, 200A output', @CategoryId, 'Lincoln Electric', 'PowerMIG 210',
 'EA', 2500.00, 2500.00, 2500.00, 3000.00, 1, 0, 1, 0, 0, 1, 3, 1, 1, 14, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

-- Diagnostic Equipment
(NEWID(), 'TOOL-007', 'Multimeter Digital', 'Professional digital multimeter with auto-ranging', @CategoryId, 'Fluke', '87V',
 'EA', 400.00, 400.00, 400.00, 500.00, 3, 3, 0, 0, 0, 1, 10, 2, 2, 7, 0, 4, 'B', 1, 
 0, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-008', 'Thermal Imaging Camera', 'Professional thermal camera for electrical inspections', @CategoryId, 'FLIR', 'E60',
 'EA', 3500.00, 3500.00, 3500.00, 4000.00, 1, 1, 0, 0, 0, 1, 2, 1, 1, 14, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-009', 'OBD-II Scanner Professional', 'Advanced diagnostic scanner with live data', @CategoryId, 'Autel', 'MaxiCOM MK808',
 'EA', 550.00, 550.00, 550.00, 700.00, 2, 2, 0, 0, 0, 1, 5, 1, 1, 7, 0, 4, 'B', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

-- Specialized Equipment
(NEWID(), 'TOOL-010', 'Pipe Threading Machine', 'Portable pipe threading machine for 1/2" to 2" pipes', @CategoryId, 'RIDGID', '300-T2',
 'EA', 1800.00, 1800.00, 1800.00, 2200.00, 1, 1, 0, 0, 0, 1, 3, 1, 1, 14, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-011', 'Air Compressor 30-Gallon', 'Stationary air compressor, 30-gallon tank, 150 PSI max', @CategoryId, 'Ingersoll Rand', 'SS3F2-GM',
 'EA', 950.00, 950.00, 950.00, 1200.00, 1, 1, 0, 0, 0, 1, 3, 1, 1, 14, 0, 4, 'A', 1, 
 1, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System'),

(NEWID(), 'TOOL-012', 'Precision Level Digital', 'Digital precision level with 0.1-degree accuracy', @CategoryId, 'Bosch', 'DNM 120 L',
 'EA', 320.00, 320.00, 320.00, 400.00, 2, 2, 0, 0, 0, 1, 5, 1, 1, 7, 0, 4, 'C', 1, 
 0, 0, 0, 1, 0, 0, @TenantId, GETUTCDATE(), 'System');

PRINT 'Inserted 12 tool items into InventoryItems';

-- Step 2: Populate MaintenanceTools from the FixedAsset inventory items
INSERT INTO MaintenanceTools (
    Id, ToolCode, Name, Description, Category, Manufacturer, Model, SerialNumber,
    Status, CurrentLocation, HomeLocation, PurchasePrice, CurrentValue, DailyRentalRate,
    TotalUsageDays, LastUsedDate, RequiresCertification, RequiresTraining, SafetyNotes,
    IsActive, IsDeleted, TenantId, CreatedAt, CreatedBy
)
SELECT 
    NEWID() as Id,
    ii.ItemCode as ToolCode,
    ii.Name,
    ISNULL(ii.Description, '') as Description,
    CASE 
        WHEN ii.Name LIKE '%Wrench%' OR ii.Name LIKE '%Jack%' THEN 'Hand Tools'
        WHEN ii.Name LIKE '%Drill%' OR ii.Name LIKE '%Grinder%' OR ii.Name LIKE '%Welding%' THEN 'Power Tools'
        WHEN ii.Name LIKE '%Multimeter%' OR ii.Name LIKE '%Thermal%' OR ii.Name LIKE '%Scanner%' THEN 'Diagnostic Equipment'
        WHEN ii.Name LIKE '%Compressor%' THEN 'Power Equipment'
        WHEN ii.Name LIKE '%Level%' THEN 'Measuring Tools'
        ELSE 'Specialized Tools'
    END as Category,
    ISNULL(ii.Manufacturer, '') as Manufacturer,
    ISNULL(ii.Model, '') as Model,
    'SN-2024-' + RIGHT('000' + CAST(ROW_NUMBER() OVER (ORDER BY ii.ItemCode) AS NVARCHAR), 3) as SerialNumber,
    CASE 
        WHEN ii.AvailableStock > 0 THEN 'Available'
        WHEN ii.CurrentStock > 0 AND ii.AvailableStock = 0 THEN 'InUse'
        ELSE 'Maintenance'
    END as Status,
    'Tool Room A - Bay 1' as CurrentLocation,
    'Tool Room A - Bay 1' as HomeLocation,
    ii.StandardCost as PurchasePrice,
    ii.StandardCost as CurrentValue,
    ROUND(ii.StandardCost * 0.05, 2) as DailyRentalRate,  -- 5% daily rate
    CASE 
        WHEN ii.AvailableStock = 0 THEN 30  -- In use items have usage days
        ELSE 0
    END as TotalUsageDays,
    CASE 
        WHEN ii.AvailableStock = 0 THEN GETUTCDATE()  -- Last used today if in use
        ELSE DATEADD(DAY, -ABS(CHECKSUM(NEWID()) % 14), GETUTCDATE())  -- Random last 14 days
    END as LastUsedDate,
    CASE 
        WHEN ii.Name LIKE '%Welding%' OR ii.Name LIKE '%Jack%' THEN 1
        ELSE 0
    END as RequiresCertification,
    CASE 
        WHEN ii.Name LIKE '%Power%' OR ii.Name LIKE '%Drill%' OR ii.Name LIKE '%Grinder%' OR ii.Name LIKE '%Welding%' THEN 1
        ELSE 0
    END as RequiresTraining,
    CASE 
        WHEN ii.Name LIKE '%Welding%' THEN 'Welding shield required. Adequate ventilation mandatory. Fire extinguisher nearby.'
        WHEN ii.Name LIKE '%Jack%' THEN 'Inspect before use. Do not exceed weight capacity. Use jack stands for support.'
        WHEN ii.Name LIKE '%Grinder%' THEN 'Safety glasses and gloves required. Check disc before use.'
        ELSE 'Always follow safety guidelines in equipment manual.'
    END as SafetyNotes,
    1 as IsActive,
    0 as IsDeleted,
    ii.TenantId,
    GETUTCDATE() as CreatedAt,
    'System' as CreatedBy
FROM InventoryItems ii
WHERE ii.ItemType = 4  -- FixedAsset
  AND ii.ItemCode LIKE 'TOOL-%'
  AND NOT EXISTS (
      SELECT 1 FROM MaintenanceTools mt 
      WHERE mt.ToolCode = ii.ItemCode AND mt.TenantId = ii.TenantId
  );

PRINT 'Populated MaintenanceTools from InventoryItems';

-- Step 3: Show results
SELECT 
    'Inventory Items (Tools)' as TableName,
    COUNT(*) as RecordCount,
    SUM(StandardCost * CurrentStock) as TotalValue
FROM InventoryItems
WHERE ItemType = 4;

SELECT 
    'Maintenance Tools' as TableName,
    COUNT(*) as RecordCount,
    SUM(PurchasePrice) as TotalValue
FROM MaintenanceTools;

-- Step 4: Show tool details
SELECT 
    mt.ToolCode,
    mt.Name,
    mt.Category,
    mt.Status,
    mt.PurchasePrice,
    mt.DailyRentalRate,
    mt.RequiresCertification,
    mt.RequiresTraining,
    ii.CurrentStock,
    ii.AvailableStock
FROM MaintenanceTools mt
INNER JOIN InventoryItems ii ON mt.ToolCode = ii.ItemCode
ORDER BY mt.Category, mt.Name;

PRINT 'Seeding completed successfully!';
