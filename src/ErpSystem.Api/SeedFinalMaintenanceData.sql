-- Final Corrected Maintenance Data Seeding Script
-- This script creates essential data for maintenance analytics with all required fields

USE [RHEMA-ERP];
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Now DATETIME2 = GETUTCDATE();

-- 1. Priority Levels (if not exist)
IF NOT EXISTS (SELECT 1 FROM PriorityLevels WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO PriorityLevels (Id, Name, Level, [Description], Color, ResponseTimeHours, IsActive, IsDeleted, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'Critical', 1, 'Critical priority', '#F44336', 1, 1, 0, @Now, @Now, @TenantId),
        (NEWID(), 'High', 2, 'High priority', '#FF9800', 4, 1, 0, @Now, @Now, @TenantId),
        (NEWID(), 'Medium', 3, 'Medium priority', '#2196F3', 24, 1, 0, @Now, @Now, @TenantId),
        (NEWID(), 'Low', 4, 'Low priority', '#4CAF50', 72, 1, 0, @Now, @Now, @TenantId);
END

-- 2. Maintenance Types (if not exist)  
IF NOT EXISTS (SELECT 1 FROM MaintenanceTypes WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO MaintenanceTypes (Id, Name, Code, [Description], Category, IsActive, IsDeleted, Color, Icon, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'Preventive Maintenance', 'PM', 'Preventive maintenance', 'Preventive', 1, 0, '#4CAF50', 'calendar', @Now, @Now, @TenantId),
        (NEWID(), 'Corrective Maintenance', 'CM', 'Corrective maintenance', 'Corrective', 1, 0, '#FF9800', 'wrench', @Now, @Now, @TenantId);
END

-- 3. Work Order Types (if not exist)
IF NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO WorkOrderTypes (Id, Name, Code, [Description], Color, Icon, IsActive, IsDeleted, RequiresApproval, DefaultPriority, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'Preventive Work Order', 'PWO', 'Preventive work orders', '#4CAF50', 'calendar', 1, 0, 0, 3, @Now, @Now, @TenantId),
        (NEWID(), 'Corrective Work Order', 'CWO', 'Corrective work orders', '#FF9800', 'tool', 1, 0, 1, 2, @Now, @Now, @TenantId);
END

-- 4. Asset Categories (if not exist)
IF NOT EXISTS (SELECT 1 FROM MaintenanceAssetCategories WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO MaintenanceAssetCategories (Id, Name, [Description], Color, Icon, IsActive, IsDeleted, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'HVAC Systems', 'HVAC equipment', '#4CAF50', 'hvac', 1, 0, @Now, @Now, @TenantId),
        (NEWID(), 'Electrical Systems', 'Electrical equipment', '#FF9800', 'electrical', 1, 0, @Now, @Now, @TenantId),
        (NEWID(), 'Mechanical Systems', 'Mechanical equipment', '#2196F3', 'mechanical', 1, 0, @Now, @Now, @TenantId);
END

-- Wait a moment and then get IDs for foreign key references
WAITFOR DELAY '00:00:01';

DECLARE @CategoryId1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE TenantId = @TenantId AND Name = 'HVAC Systems');
DECLARE @CategoryId2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE TenantId = @TenantId AND Name = 'Electrical Systems');
DECLARE @CategoryId3 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE TenantId = @TenantId AND Name = 'Mechanical Systems');

DECLARE @PriorityId1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE TenantId = @TenantId AND Level = 1);
DECLARE @PriorityId2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE TenantId = @TenantId AND Level = 2);
DECLARE @PriorityId3 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE TenantId = @TenantId AND Level = 3);

DECLARE @MaintenanceTypeId1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE TenantId = @TenantId AND Code = 'PM');
DECLARE @MaintenanceTypeId2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE TenantId = @TenantId AND Code = 'CM');

DECLARE @WorkOrderTypeId1 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE TenantId = @TenantId AND Code = 'PWO');
DECLARE @WorkOrderTypeId2 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE TenantId = @TenantId AND Code = 'CWO');

-- 5. Maintenance Assets - Only if we have categories
IF @CategoryId1 IS NOT NULL AND @CategoryId2 IS NOT NULL AND @CategoryId3 IS NOT NULL
BEGIN
    DECLARE @Asset1Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @Asset2Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @Asset3Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @Asset4Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @Asset5Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @Asset6Id UNIQUEIDENTIFIER = NEWID();

    INSERT INTO MaintenanceAssets (Id, AssetNumber, Name, [Description], AssetCategoryId, Manufacturer, Model, SerialNumber, PurchaseDate, PurchasePrice, [Location], [Status], IsDeleted, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (@Asset1Id, 'HVAC-001', 'Main HVAC Unit Building A', 'Central HVAC system', @CategoryId1, 'Carrier', 'CAC-5000', 'CAR123456789', DATEADD(YEAR, -3, @Now), 75000.00, 'Building A - Roof', 1, 0, @Now, @Now, @TenantId),
        (@Asset2Id, 'HVAC-002', 'HVAC Unit Building B', 'Secondary HVAC system', @CategoryId1, 'Trane', 'CAC-3000', 'TRN987654321', DATEADD(YEAR, -2, @Now), 45000.00, 'Building B - Roof', 1, 0, @Now, @Now, @TenantId),
        (@Asset3Id, 'ELEV-001', 'Main Elevator Building A', 'Primary elevator', @CategoryId2, 'Otis', 'E-2000', 'OTIS567890123', DATEADD(YEAR, -5, @Now), 125000.00, 'Building A - Floors 1-10', 1, 0, @Now, @Now, @TenantId),
        (@Asset4Id, 'GEN-001', 'Emergency Generator', 'Backup generator', @CategoryId2, 'Cummins', 'DG-500', 'CUM345678901', DATEADD(YEAR, -4, @Now), 85000.00, 'Building C - Generator Room', 1, 0, @Now, @Now, @TenantId),
        (@Asset5Id, 'PUMP-001', 'Water Circulation Pump', 'Main water pump', @CategoryId3, 'Grundfos', 'P-1200', 'GRU789012345', DATEADD(YEAR, -2, @Now), 15000.00, 'Building A - Mechanical Room', 1, 0, @Now, @Now, @TenantId),
        (@Asset6Id, 'LIGHT-001', 'LED Lighting System', 'Building LED lights', @CategoryId2, 'Philips', 'LED-PRO', 'PHI901234567', DATEADD(YEAR, -1, @Now), 25000.00, 'Buildings A-C - All Floors', 1, 0, @Now, @Now, @TenantId);

    -- 6. Work Orders - Only if we have all required IDs
    IF @WorkOrderTypeId1 IS NOT NULL AND @MaintenanceTypeId1 IS NOT NULL AND @PriorityId3 IS NOT NULL
    BEGIN
        INSERT INTO WorkOrders (Id, WorkOrderNumber, Title, [Description], AssetId, WorkOrderTypeId, MaintenanceTypeId, PriorityLevelId, [Status], IsDeleted, RequestedStartDate, ActualStartDate, ActualCompletionDate, EstimatedCost, ActualCost, EstimatedHours, ActualHours, CreatedAt, UpdatedAt, TenantId)
        VALUES 
            (NEWID(), 'WO-2024-001', 'HVAC Unit Maintenance', 'Quarterly HVAC maintenance', @Asset1Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId3, 'Completed', 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, -30, @Now), DATEADD(DAY, -29, @Now), 500.00, 475.00, 4.0, 3.5, DATEADD(DAY, -35, @Now), DATEADD(DAY, -29, @Now), @TenantId),
            (NEWID(), 'WO-2024-002', 'Elevator Repair', 'Fix elevator door sensor', @Asset3Id, @WorkOrderTypeId2, @MaintenanceTypeId2, @PriorityId2, 'Completed', 0, DATEADD(DAY, -20, @Now), DATEADD(DAY, -20, @Now), DATEADD(DAY, -19, @Now), 800.00, 750.00, 6.0, 5.5, DATEADD(DAY, -22, @Now), DATEADD(DAY, -19, @Now), @TenantId),
            (NEWID(), 'WO-2024-003', 'Generator Load Test', 'Monthly generator test', @Asset4Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId3, 'Completed', 0, DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), 200.00, 180.00, 2.0, 1.5, DATEADD(DAY, -18, @Now), DATEADD(DAY, -15, @Now), @TenantId),
            (NEWID(), 'WO-2024-004', 'Pump Filter Replacement', 'Replace pump filters', @Asset5Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId1, 'InProgress', 0, @Now, @Now, NULL, 150.00, 0.00, 2.0, 0.0, DATEADD(DAY, -2, @Now), @Now, @TenantId),
            (NEWID(), 'WO-2024-005', 'LED System Inspection', 'Inspect LED system', @Asset6Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId1, 'Assigned', 0, DATEADD(DAY, 3, @Now), NULL, NULL, 100.00, 0.00, 1.5, 0.0, DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), @TenantId),
            (NEWID(), 'WO-2024-006', 'HVAC Filter Change', 'Change HVAC filters', @Asset2Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId3, 'Completed', 0, DATEADD(DAY, -45, @Now), DATEADD(DAY, -45, @Now), DATEADD(DAY, -44, @Now), 75.00, 68.00, 1.0, 0.75, DATEADD(DAY, -48, @Now), DATEADD(DAY, -44, @Now), @TenantId),
            (NEWID(), 'WO-2024-007', 'Emergency Generator Service', 'Annual generator service', @Asset4Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId2, 'Completed', 0, DATEADD(DAY, -60, @Now), DATEADD(DAY, -60, @Now), DATEADD(DAY, -58, @Now), 950.00, 1100.00, 8.0, 9.5, DATEADD(DAY, -65, @Now), DATEADD(DAY, -58, @Now), @TenantId),
            (NEWID(), 'WO-2024-008', 'Elevator Monthly Check', 'Monthly elevator inspection', @Asset3Id, @WorkOrderTypeId1, @MaintenanceTypeId1, @PriorityId3, 'Completed', 0, DATEADD(DAY, -40, @Now), DATEADD(DAY, -40, @Now), DATEADD(DAY, -40, @Now), 200.00, 185.00, 2.5, 2.0, DATEADD(DAY, -42, @Now), DATEADD(DAY, -40, @Now), @TenantId);
    END
END

PRINT 'Final maintenance data seeding completed successfully!';

-- Verify data
PRINT 'Verification:';
SELECT 'Assets' as TableName, COUNT(*) as RecordCount FROM MaintenanceAssets WHERE TenantId = @TenantId
UNION ALL
SELECT 'Work Orders', COUNT(*) FROM WorkOrders WHERE TenantId = @TenantId
UNION ALL  
SELECT 'Priority Levels', COUNT(*) FROM PriorityLevels WHERE TenantId = @TenantId
UNION ALL
SELECT 'Maintenance Types', COUNT(*) FROM MaintenanceTypes WHERE TenantId = @TenantId
UNION ALL
SELECT 'Work Order Types', COUNT(*) FROM WorkOrderTypes WHERE TenantId = @TenantId
UNION ALL
SELECT 'Asset Categories', COUNT(*) FROM MaintenanceAssetCategories WHERE TenantId = @TenantId;

GO