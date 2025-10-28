-- Comprehensive Maintenance Ecosystem Seed Data Script
-- This seeds ALL maintenance-related tables including JobCards, Technicians, Contractors, etc.

USE [RHEMA-ERP];
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Now DATETIME2 = GETUTCDATE();

PRINT 'Starting comprehensive maintenance data seeding...';

-- Get existing foundation IDs
DECLARE @CategoryId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceAssetCategories WHERE TenantId = @TenantId);
DECLARE @PriorityId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM PriorityLevels WHERE TenantId = @TenantId);
DECLARE @MaintenanceTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceTypes WHERE TenantId = @TenantId);
DECLARE @WorkOrderTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM WorkOrderTypes WHERE TenantId = @TenantId);

-- 1. Create Maintenance Contractors
IF NOT EXISTS (SELECT 1 FROM MaintenanceContractor WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO MaintenanceContractor (Id, Name, ContractorCode, [Description], ContactInfo, Capabilities, ServiceAreas, [Status], CreatedDate, TenantId)
    VALUES 
        (NEWID(), 'ABC HVAC Services', 'ABC-HVAC', 'Professional HVAC maintenance and repair', '{"phone": "555-0123", "email": "contact@abchvac.com"}', '["HVAC", "Refrigeration", "Ventilation"]', '["North", "Central"]', 'Active', @Now, @TenantId),
        (NEWID(), 'Elite Electrical Corp', 'ELITE-ELEC', 'Industrial electrical services', '{"phone": "555-0456", "email": "info@eliteelectrical.com"}', '["Electrical", "Power Systems", "Controls"]', '["All Areas"]', 'Active', @Now, @TenantId),
        (NEWID(), 'Precision Mechanical', 'PREC-MECH', 'Mechanical equipment specialists', '{"phone": "555-0789", "email": "service@precisionmech.com"}', '["Mechanical", "Pumps", "Motors"]', '["South", "East"]', 'Active', @Now, @TenantId);
    
    PRINT '✓ Created 3 Maintenance Contractors';
END

-- 2. Create Technical Skills
IF NOT EXISTS (SELECT 1 FROM TechnicianSkills WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TechnicianSkills (Id, Name, [Description], Category, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'HVAC Systems', 'Heating, Ventilation, and Air Conditioning maintenance', 'HVAC', @Now, @Now, @TenantId),
        (NEWID(), 'Electrical Systems', 'Electrical maintenance and troubleshooting', 'Electrical', @Now, @Now, @TenantId),
        (NEWID(), 'Mechanical Repair', 'General mechanical equipment repair', 'Mechanical', @Now, @Now, @TenantId),
        (NEWID(), 'Plumbing Systems', 'Water and drainage system maintenance', 'Plumbing', @Now, @Now, @TenantId),
        (NEWID(), 'Welding', 'Welding and fabrication skills', 'Fabrication', @Now, @Now, @TenantId),
        (NEWID(), 'Preventive Maintenance', 'Scheduled maintenance procedures', 'General', @Now, @Now, @TenantId);
    
    PRINT '✓ Created 6 Technical Skills';
END

-- 3. Create Technician Teams
IF NOT EXISTS (SELECT 1 FROM TechnicianTeams WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TechnicianTeams (Id, Name, [Description], CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), 'General Maintenance Team', 'Primary maintenance team for routine work', @Now, @Now, @TenantId),
        (NEWID(), 'HVAC Specialists', 'Specialized team for HVAC systems', @Now, @Now, @TenantId),
        (NEWID(), 'Electrical Team', 'Electrical maintenance specialists', @Now, @Now, @TenantId),
        (NEWID(), 'Emergency Response Team', 'Emergency and critical repairs', @Now, @Now, @TenantId);
    
    PRINT '✓ Created 4 Technician Teams';
END

-- 4. Create Technicians
IF NOT EXISTS (SELECT 1 FROM Technicians WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Technicians (Id, EmployeeId, CreatedAt, UpdatedAt, TenantId)
    VALUES 
        (NEWID(), @AdminUserId, @Now, @Now, @TenantId),
        (NEWID(), @AdminUserId, @Now, @Now, @TenantId),
        (NEWID(), @AdminUserId, @Now, @Now, @TenantId),
        (NEWID(), @AdminUserId, @Now, @Now, @TenantId),
        (NEWID(), @AdminUserId, @Now, @Now, @TenantId);
    
    PRINT '✓ Created 5 Technicians';
END

-- Get created IDs for relationships
DECLARE @ContractorId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MaintenanceContractor WHERE TenantId = @TenantId);
DECLARE @TeamId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianTeams WHERE TenantId = @TenantId);
DECLARE @TechnicianId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Technicians WHERE TenantId = @TenantId);
DECLARE @SkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianSkills WHERE TenantId = @TenantId);

-- 5. Create some basic maintenance assets with minimal required fields
DECLARE @Asset1Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset2Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset3Id UNIQUEIDENTIFIER = NEWID();

IF @CategoryId IS NOT NULL
BEGIN
    INSERT INTO MaintenanceAssets (Id, AssetNumber, Name, AssetCategoryId, [Status], IsDeleted, CreatedAt, TenantId) 
    VALUES 
        (@Asset1Id, 'HVAC-UNIT-001', 'Main HVAC System Building A', @CategoryId, 1, 0, @Now, @TenantId),
        (@Asset2Id, 'PUMP-SYS-001', 'Water Circulation System', @CategoryId, 1, 0, @Now, @TenantId),
        (@Asset3Id, 'ELEV-001', 'Primary Passenger Elevator', @CategoryId, 1, 0, @Now, @TenantId);
    
    PRINT '✓ Created 3 Maintenance Assets';
END

-- 6. Create Job Cards (the approval workflow before work orders)
IF @Asset1Id IS NOT NULL AND @MaintenanceTypeId IS NOT NULL AND @PriorityId IS NOT NULL
BEGIN
    DECLARE @JobCard1Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @JobCard2Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @JobCard3Id UNIQUEIDENTIFIER = NEWID();
    DECLARE @JobCard4Id UNIQUEIDENTIFIER = NEWID();

    INSERT INTO JobCard (
        Id, JobCardNumber, AssetId, MaintenanceTypeId, PriorityLevelId, Title, [Description], 
        MaintenanceLocation, RequestedById, RequestedDate, EstimatedHours, EstimatedCost,
        JobCardStatus, ApprovalStatus, ContractorId,
        CreatedAt, UpdatedAt, IsDeleted, TenantId
    )
    VALUES 
        (@JobCard1Id, 'JC-2024-001', @Asset1Id, @MaintenanceTypeId, @PriorityId, 'HVAC Unit Annual Service', 'Complete annual preventive maintenance service for main HVAC unit', 'Internal', @AdminUserId, DATEADD(DAY, -45, @Now), 6.0, 850.00, 'Approved', 'Approved', NULL, DATEADD(DAY, -45, @Now), DATEADD(DAY, -40, @Now), 0, @TenantId),
        (@JobCard2Id, 'JC-2024-002', @Asset2Id, @MaintenanceTypeId, @PriorityId, 'Pump System Maintenance', 'Quarterly maintenance for water circulation pump', 'Internal', @AdminUserId, DATEADD(DAY, -30, @Now), 4.0, 400.00, 'Approved', 'Approved', NULL, DATEADD(DAY, -30, @Now), DATEADD(DAY, -28, @Now), 0, @TenantId),
        (@JobCard3Id, 'JC-2024-003', @Asset3Id, @MaintenanceTypeId, @PriorityId, 'Elevator Safety Inspection', 'Monthly elevator safety inspection and testing', 'Internal', @AdminUserId, DATEADD(DAY, -15, @Now), 3.0, 250.00, 'Approved', 'Approved', NULL, DATEADD(DAY, -15, @Now), DATEADD(DAY, -14, @Now), 0, @TenantId),
        (@JobCard4Id, 'JC-2024-004', @Asset1Id, @MaintenanceTypeId, @PriorityId, 'HVAC Filter Replacement', 'Replace air filters in HVAC system', 'Internal', @AdminUserId, DATEADD(DAY, -5, @Now), 1.5, 120.00, 'UnderReview', 'Draft', NULL, DATEADD(DAY, -5, @Now), DATEADD(DAY, -5, @Now), 0, @TenantId);
    
    PRINT '✓ Created 4 Job Cards';

    -- 7. Create Work Orders from approved Job Cards
    IF @WorkOrderTypeId IS NOT NULL AND @TechnicianId IS NOT NULL
    BEGIN
        INSERT INTO WorkOrders (
            Id, WorkOrderNumber, Title, [Description], AssetId, WorkOrderTypeId, MaintenanceTypeId, PriorityLevelId,
            [Status], AssignedTechnicianId, RequestedStartDate, ActualStartDate, ActualCompletionDate,
            EstimatedCost, ActualCost, EstimatedHours, ActualHours, 
            RequiresPermit, RequiresLockout, RequiresConfinedSpaceEntry, IsRecurring, IsDeleted,
            CreatedAt, UpdatedAt, TenantId
        )
        VALUES 
            (NEWID(), 'WO-2024-001', 'HVAC Unit Annual Service', 'Generated from Job Card JC-2024-001', @Asset1Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'Completed', @TechnicianId, DATEADD(DAY, -40, @Now), DATEADD(DAY, -40, @Now), DATEADD(DAY, -39, @Now), 850.00, 820.00, 6.0, 5.8, 0, 0, 0, 0, 0, DATEADD(DAY, -40, @Now), DATEADD(DAY, -39, @Now), @TenantId),
            (NEWID(), 'WO-2024-002', 'Pump System Maintenance', 'Generated from Job Card JC-2024-002', @Asset2Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'Completed', @TechnicianId, DATEADD(DAY, -28, @Now), DATEADD(DAY, -28, @Now), DATEADD(DAY, -27, @Now), 400.00, 385.00, 4.0, 3.8, 0, 1, 0, 0, 0, DATEADD(DAY, -28, @Now), DATEADD(DAY, -27, @Now), @TenantId),
            (NEWID(), 'WO-2024-003', 'Elevator Safety Inspection', 'Generated from Job Card JC-2024-003', @Asset3Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'Completed', @TechnicianId, DATEADD(DAY, -14, @Now), DATEADD(DAY, -14, @Now), DATEADD(DAY, -14, @Now), 250.00, 240.00, 3.0, 2.9, 0, 0, 0, 0, 0, DATEADD(DAY, -14, @Now), DATEADD(DAY, -14, @Now), @TenantId),
            (NEWID(), 'WO-2024-004', 'Emergency HVAC Repair', 'Unplanned repair work', @Asset1Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'InProgress', @TechnicianId, DATEADD(DAY, -2, @Now), DATEADD(DAY, -2, @Now), NULL, 600.00, 0.00, 4.0, 1.5, 0, 0, 0, 0, 0, DATEADD(DAY, -3, @Now), DATEADD(DAY, -2, @Now), @TenantId),
            (NEWID(), 'WO-2024-005', 'Pump Filter Replacement', 'Routine filter replacement', @Asset2Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'Assigned', @TechnicianId, DATEADD(DAY, 2, @Now), NULL, NULL, 150.00, 0.00, 2.0, 0.0, 0, 0, 0, 0, 0, DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), @TenantId);
        
        PRINT '✓ Created 5 Work Orders';
    END

    -- 8. Create Job Card Comments (approval workflow comments)
    INSERT INTO JobCardComment (Id, JobCardId, CommentById, Comment, CommentType, IsInternal, CommentDate, CreatedAt, UpdatedAt, IsDeleted, TenantId)
    VALUES 
        (NEWID(), @JobCard1Id, @AdminUserId, 'Job card approved for execution. Scheduled for next maintenance window.', 'Approval', 1, DATEADD(DAY, -40, @Now), DATEADD(DAY, -40, @Now), DATEADD(DAY, -40, @Now), 0, @TenantId),
        (NEWID(), @JobCard2Id, @AdminUserId, 'Approved. Please coordinate with facilities team for water system shutdown.', 'Approval', 1, DATEADD(DAY, -28, @Now), DATEADD(DAY, -28, @Now), DATEADD(DAY, -28, @Now), 0, @TenantId),
        (NEWID(), @JobCard4Id, @AdminUserId, 'Under review. Need to verify filter specifications before approval.', 'Question', 1, DATEADD(DAY, -4, @Now), DATEADD(DAY, -4, @Now), DATEADD(DAY, -4, @Now), 0, @TenantId);
    
    PRINT '✓ Created 3 Job Card Comments';
END

-- 9. Create Technician Team Members
IF @TeamId IS NOT NULL AND @TechnicianId IS NOT NULL
BEGIN
    INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, CreatedAt, UpdatedAt, TenantId)
    SELECT NEWID(), @TeamId, Id, @Now, @Now, @TenantId 
    FROM Technicians WHERE TenantId = @TenantId;
    
    PRINT '✓ Created Technician Team Members';
END

-- 10. Create Technician Skill Assignments
IF @TechnicianId IS NOT NULL AND @SkillId IS NOT NULL
BEGIN
    INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, CreatedAt, UpdatedAt, TenantId)
    SELECT NEWID(), t.Id, s.Id, @Now, @Now, @TenantId
    FROM Technicians t
    CROSS JOIN (SELECT TOP 3 Id FROM TechnicianSkills WHERE TenantId = @TenantId) s
    WHERE t.TenantId = @TenantId;
    
    PRINT '✓ Created Technician Skill Assignments';
END

-- 11. Create Contractor Work Orders
IF @ContractorId IS NOT NULL AND @Asset1Id IS NOT NULL AND @WorkOrderTypeId IS NOT NULL
BEGIN
    DECLARE @ContractorWOId UNIQUEIDENTIFIER = NEWID();
    
    -- First create a work order for contractor
    INSERT INTO WorkOrders (
        Id, WorkOrderNumber, Title, [Description], AssetId, WorkOrderTypeId, MaintenanceTypeId, PriorityLevelId,
        [Status], ContractorId, RequestedStartDate, ActualStartDate, ActualCompletionDate,
        EstimatedCost, ActualCost, EstimatedHours, ActualHours,
        RequiresPermit, RequiresLockout, RequiresConfinedSpaceEntry, IsRecurring, IsDeleted,
        CreatedAt, UpdatedAt, TenantId
    )
    VALUES (
        @ContractorWOId, 'WO-2024-C001', 'External HVAC Overhaul', 'Major HVAC system overhaul by external contractor', 
        @Asset1Id, @WorkOrderTypeId, @MaintenanceTypeId, @PriorityId, 'Completed', @ContractorId,
        DATEADD(DAY, -60, @Now), DATEADD(DAY, -60, @Now), DATEADD(DAY, -58, @Now),
        5500.00, 5200.00, 20.0, 19.5, 1, 1, 0, 0, 0,
        DATEADD(DAY, -65, @Now), DATEADD(DAY, -58, @Now), @TenantId
    );
    
    -- Create the contractor work order relationship
    INSERT INTO ContractorWorkOrder (Id, ContractorId, WorkOrderId, AssignedDate, StartedDate, CompletedDate, [Status], EstimatedCost, ActualCost, WorkPerformed, TenantId)
    VALUES (NEWID(), @ContractorId, @ContractorWOId, DATEADD(DAY, -65, @Now), DATEADD(DAY, -60, @Now), DATEADD(DAY, -58, @Now), 'Completed', 5500.00, 5200.00, 'Complete HVAC system overhaul including ductwork cleaning, component replacement, and system testing', @TenantId);
    
    PRINT '✓ Created Contractor Work Order';
END

-- 12. Create some Technician Schedules and Availabilities
IF @TechnicianId IS NOT NULL
BEGIN
    INSERT INTO TechnicianSchedules (Id, TechnicianId, [Date], StartTime, EndTime, [Status], CreatedAt, UpdatedAt, TenantId)
    SELECT NEWID(), Id, CAST(@Now as DATE), '08:00:00', '17:00:00', 'Scheduled', @Now, @Now, @TenantId
    FROM Technicians WHERE TenantId = @TenantId;
    
    INSERT INTO TechnicianAvailabilities (Id, TechnicianId, [Date], IsAvailable, CreatedAt, UpdatedAt, TenantId)
    SELECT NEWID(), Id, CAST(@Now as DATE), 1, @Now, @Now, @TenantId
    FROM Technicians WHERE TenantId = @TenantId;
    
    PRINT '✓ Created Technician Schedules and Availabilities';
END

-- Final verification
PRINT '';
PRINT '=== MAINTENANCE ECOSYSTEM SEEDING COMPLETE ===';
PRINT 'Summary of created records:';

SELECT 'MaintenanceContractor' as TableName, COUNT(*) as RecordCount FROM MaintenanceContractor WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianSkills', COUNT(*) FROM TechnicianSkills WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianTeams', COUNT(*) FROM TechnicianTeams WHERE TenantId = @TenantId
UNION ALL SELECT 'Technicians', COUNT(*) FROM Technicians WHERE TenantId = @TenantId
UNION ALL SELECT 'MaintenanceAssets', COUNT(*) FROM MaintenanceAssets WHERE TenantId = @TenantId
UNION ALL SELECT 'JobCard', COUNT(*) FROM JobCard WHERE TenantId = @TenantId
UNION ALL SELECT 'WorkOrders', COUNT(*) FROM WorkOrders WHERE TenantId = @TenantId
UNION ALL SELECT 'JobCardComment', COUNT(*) FROM JobCardComment WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianTeamMembers', COUNT(*) FROM TechnicianTeamMembers WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianSkillAssignments', COUNT(*) FROM TechnicianSkillAssignments WHERE TenantId = @TenantId
UNION ALL SELECT 'ContractorWorkOrder', COUNT(*) FROM ContractorWorkOrder WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianSchedules', COUNT(*) FROM TechnicianSchedules WHERE TenantId = @TenantId
UNION ALL SELECT 'TechnicianAvailabilities', COUNT(*) FROM TechnicianAvailabilities WHERE TenantId = @TenantId
ORDER BY TableName;

PRINT '';
PRINT '✅ Analytics should now display REAL maintenance data instead of mock data!';

GO