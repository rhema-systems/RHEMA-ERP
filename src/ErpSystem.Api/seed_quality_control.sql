USE [RHEMA-ERP]
GO

-- Set variables for consistent data
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001'
DECLARE @CurrentTime DATETIME = GETUTCDATE()
DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'admin@example.com')

-- If UserId is null, create a default one
IF @UserId IS NULL
    SET @UserId = NEWID()

-- Insert Quality Control Checklists
INSERT INTO QualityControlChecklists (Id, Name, Description, WorkOrderType, AssetCategory, MaintenanceType, IsMandatory, IsActive, ChecklistItems, MinimumPassingScore, Version, CreatedDate, CreatedById, TenantId)
VALUES 
    (NEWID(), 'HVAC Repair Quality Check', 'Quality checklist for HVAC repair work', 'REPAIR', 'HVAC', 'CM', 1, 1, '[{"item": "System operates within specified parameters", "weight": 20}, {"item": "All connections are secure", "weight": 15}, {"item": "No refrigerant leaks detected", "weight": 20}, {"item": "Electrical connections are proper", "weight": 15}, {"item": "System controls function correctly", "weight": 20}, {"item": "Area is clean and organized", "weight": 10}]', 85, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'Electrical Work Safety Check', 'Safety quality check for electrical work', 'REPAIR', 'ELEC', 'CM', 1, 1, '[{"item": "Proper lockout/tagout procedures followed", "weight": 25}, {"item": "All electrical connections are secure", "weight": 20}, {"item": "Ground fault protection verified", "weight": 20}, {"item": "Circuit labeling is accurate", "weight": 15}, {"item": "No exposed conductors", "weight": 20}]', 90, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'General Maintenance Quality Check', 'Standard quality checklist for general maintenance', 'MAINT', 'General', 'PM', 1, 1, '[{"item": "Work completed per specifications", "weight": 25}, {"item": "All safety protocols followed", "weight": 20}, {"item": "Equipment operates properly", "weight": 20}, {"item": "Work area clean and organized", "weight": 15}, {"item": "Documentation complete", "weight": 20}]', 80, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'Mechanical Equipment Quality Check', 'Quality checklist for mechanical equipment maintenance', 'REPAIR', 'MECH', 'CM', 1, 1, '[{"item": "Moving parts properly lubricated", "weight": 20}, {"item": "No unusual vibration or noise", "weight": 20}, {"item": "All fasteners properly torqued", "weight": 15}, {"item": "Alignment within specifications", "weight": 20}, {"item": "Safety guards in place", "weight": 15}, {"item": "Operating parameters normal", "weight": 10}]', 85, 1, @CurrentTime, @UserId, @TenantId),
    
    (NEWID(), 'Preventive Maintenance Quality Check', 'Quality checklist for preventive maintenance tasks', 'MAINT', 'General', 'PM', 1, 1, '[{"item": "All scheduled tasks completed", "weight": 25}, {"item": "Consumables replaced as required", "weight": 20}, {"item": "Equipment cleaned properly", "weight": 15}, {"item": "Inspection points checked", "weight": 20}, {"item": "Documentation updated", "weight": 20}]', 80, 1, @CurrentTime, @UserId, @TenantId);

PRINT 'Quality Control Checklists seeded successfully'