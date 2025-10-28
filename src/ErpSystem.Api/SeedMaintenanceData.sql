-- Comprehensive Maintenance Data Seeding Script
-- This script seeds all maintenance-related tables with sample data

USE [RHEMA-ERP];
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Now DATETIME2 = GETUTCDATE();

-- 1. Asset Types (foundational)
INSERT INTO AssetTypes (Id, Name, [Description], Icon, Color, IsActive, SortOrder, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), 'HVAC', 'Heating, Ventilation, and Air Conditioning systems', 'hvac-icon', '#4CAF50', 1, 1, @Now, @Now, @TenantId),
    (NEWID(), 'Elevator', 'Vertical transportation systems', 'elevator-icon', '#2196F3', 1, 2, @Now, @Now, @TenantId),
    (NEWID(), 'Generator', 'Emergency power generation equipment', 'generator-icon', '#FF9800', 1, 3, @Now, @Now, @TenantId),
    (NEWID(), 'Pump', 'Water and fluid pumping systems', 'pump-icon', '#9C27B0', 1, 4, @Now, @Now, @TenantId),
    (NEWID(), 'Lighting', 'Lighting systems and fixtures', 'light-icon', '#FFC107', 1, 5, @Now, @Now, @TenantId);

-- 2. Maintenance Asset Categories
DECLARE @HvacCategoryId UNIQUEIDENTIFIER = NEWID();
DECLARE @ElevatorCategoryId UNIQUEIDENTIFIER = NEWID();
DECLARE @GeneratorCategoryId UNIQUEIDENTIFIER = NEWID();
DECLARE @PumpCategoryId UNIQUEIDENTIFIER = NEWID();
DECLARE @LightingCategoryId UNIQUEIDENTIFIER = NEWID();

INSERT INTO MaintenanceAssetCategories (Id, Name, [Description], Color, Icon, IsActive, SortOrder, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@HvacCategoryId, 'HVAC Systems', 'Heating, Ventilation, and Air Conditioning equipment', '#4CAF50', 'hvac', 1, 1, @Now, @Now, @TenantId),
    (@ElevatorCategoryId, 'Vertical Transportation', 'Elevators, escalators, and lifts', '#2196F3', 'elevator', 1, 2, @Now, @Now, @TenantId),
    (@GeneratorCategoryId, 'Power Generation', 'Generators and backup power systems', '#FF9800', 'generator', 1, 3, @Now, @Now, @TenantId),
    (@PumpCategoryId, 'Fluid Systems', 'Pumps and fluid handling equipment', '#9C27B0', 'pump', 1, 4, @Now, @Now, @TenantId),
    (@LightingCategoryId, 'Lighting Systems', 'Interior and exterior lighting', '#FFC107', 'light', 1, 5, @Now, @Now, @TenantId);

-- 3. Priority Levels
DECLARE @CriticalPriorityId UNIQUEIDENTIFIER = NEWID();
DECLARE @HighPriorityId UNIQUEIDENTIFIER = NEWID();
DECLARE @MediumPriorityId UNIQUEIDENTIFIER = NEWID();
DECLARE @LowPriorityId UNIQUEIDENTIFIER = NEWID();

INSERT INTO PriorityLevels (Id, Name, Level, [Description], Color, ResponseTimeHours, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@CriticalPriorityId, 'Critical', 1, 'Immediate response required - system down', '#F44336', 1, 1, @Now, @Now, @TenantId),
    (@HighPriorityId, 'High', 2, 'High priority - affects operations', '#FF9800', 4, 1, @Now, @Now, @TenantId),
    (@MediumPriorityId, 'Medium', 3, 'Medium priority - scheduled maintenance', '#2196F3', 24, 1, @Now, @Now, @TenantId),
    (@LowPriorityId, 'Low', 4, 'Low priority - routine maintenance', '#4CAF50', 72, 1, @Now, @Now, @TenantId);

-- 4. Maintenance Types
DECLARE @PreventiveTypeId UNIQUEIDENTIFIER = NEWID();
DECLARE @CorrectiveTypeId UNIQUEIDENTIFIER = NEWID();
DECLARE @EmergencyTypeId UNIQUEIDENTIFIER = NEWID();
DECLARE @InspectionTypeId UNIQUEIDENTIFIER = NEWID();

INSERT INTO MaintenanceTypes (Id, Name, Code, [Description], Category, IsActive, Color, Icon, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@PreventiveTypeId, 'Preventive Maintenance', 'PM', 'Scheduled preventive maintenance', 'Preventive', 1, '#4CAF50', 'calendar', @Now, @Now, @TenantId),
    (@CorrectiveTypeId, 'Corrective Maintenance', 'CM', 'Repair work to fix issues', 'Corrective', 1, '#FF9800', 'wrench', @Now, @Now, @TenantId),
    (@EmergencyTypeId, 'Emergency Maintenance', 'EM', 'Urgent repairs for critical issues', 'Emergency', 1, '#F44336', 'emergency', @Now, @Now, @TenantId),
    (@InspectionTypeId, 'Inspection', 'INS', 'Regular inspection and assessment', 'Inspection', 1, '#2196F3', 'search', @Now, @Now, @TenantId);

-- 5. Work Order Types
DECLARE @PreventiveWOTypeId UNIQUEIDENTIFIER = NEWID();
DECLARE @CorrectiveWOTypeId UNIQUEIDENTIFIER = NEWID();
DECLARE @EmergencyWOTypeId UNIQUEIDENTIFIER = NEWID();

INSERT INTO WorkOrderTypes (Id, Name, Code, [Description], Color, Icon, IsActive, RequiresApproval, DefaultPriority, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@PreventiveWOTypeId, 'Preventive Work Order', 'PWO', 'Scheduled preventive maintenance work orders', '#4CAF50', 'calendar-check', 1, 0, 3, @Now, @Now, @TenantId),
    (@CorrectiveWOTypeId, 'Corrective Work Order', 'CWO', 'Corrective maintenance work orders', '#FF9800', 'tool', 1, 1, 2, @Now, @Now, @TenantId),
    (@EmergencyWOTypeId, 'Emergency Work Order', 'EWO', 'Emergency maintenance work orders', '#F44336', 'alert', 1, 0, 1, @Now, @Now, @TenantId);

-- 6. Technical Skills
DECLARE @HvacSkillId UNIQUEIDENTIFIER = NEWID();
DECLARE @ElectricalSkillId UNIQUEIDENTIFIER = NEWID();
DECLARE @MechanicalSkillId UNIQUEIDENTIFIER = NEWID();
DECLARE @PlumbingSkillId UNIQUEIDENTIFIER = NEWID();

INSERT INTO TechnicianSkills (Id, Name, [Description], Category, Complexity, RiskLevel, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@HvacSkillId, 'HVAC Systems', 'Heating, ventilation, and air conditioning systems', 'HVAC', 'Intermediate', 'Medium', 1, @Now, @Now, @TenantId),
    (@ElectricalSkillId, 'Electrical Systems', 'Electrical maintenance and repair', 'Electrical', 'Advanced', 'High', 1, @Now, @Now, @TenantId),
    (@MechanicalSkillId, 'Mechanical Systems', 'Mechanical equipment maintenance', 'Mechanical', 'Intermediate', 'Medium', 1, @Now, @Now, @TenantId),
    (@PlumbingSkillId, 'Plumbing Systems', 'Water and drainage systems', 'Plumbing', 'Basic', 'Low', 1, @Now, @Now, @TenantId);

-- 7. Technician Teams
DECLARE @MaintenanceTeamId UNIQUEIDENTIFIER = NEWID();
DECLARE @HvacTeamId UNIQUEIDENTIFIER = NEWID();
DECLARE @ElectricalTeamId UNIQUEIDENTIFIER = NEWID();

INSERT INTO TechnicianTeams (Id, Name, [Description], TeamLeaderId, MaxMembers, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@MaintenanceTeamId, 'General Maintenance', 'General maintenance team for routine work', NULL, 10, 1, @Now, @Now, @TenantId),
    (@HvacTeamId, 'HVAC Specialists', 'Specialized team for HVAC systems', NULL, 5, 1, @Now, @Now, @TenantId),
    (@ElectricalTeamId, 'Electrical Team', 'Specialized electrical maintenance team', NULL, 6, 1, @Now, @Now, @TenantId);

-- 8. Technicians (using existing Employee records)
DECLARE @Tech1Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Tech2Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Tech3Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Tech4Id UNIQUEIDENTIFIER = NEWID();

INSERT INTO Technicians (Id, EmployeeId, BadgeNumber, HireDate, [Status], Specialization, CertificationLevel, HourlyRate, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@Tech1Id, @AdminUserId, 'TECH001', DATEADD(YEAR, -2, @Now), 'Active', 'HVAC', 'Certified', 35.00, 1, @Now, @Now, @TenantId),
    (@Tech2Id, @AdminUserId, 'TECH002', DATEADD(YEAR, -1, @Now), 'Active', 'Electrical', 'Expert', 42.00, 1, @Now, @Now, @TenantId),
    (@Tech3Id, @AdminUserId, 'TECH003', DATEADD(MONTH, -8, @Now), 'Active', 'Mechanical', 'Certified', 38.00, 1, @Now, @Now, @TenantId),
    (@Tech4Id, @AdminUserId, 'TECH004', DATEADD(MONTH, -6, @Now), 'Active', 'General', 'Apprentice', 28.00, 1, @Now, @Now, @TenantId);

-- 9. Maintenance Assets
DECLARE @Asset1Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset2Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset3Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset4Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset5Id UNIQUEIDENTIFIER = NEWID();
DECLARE @Asset6Id UNIQUEIDENTIFIER = NEWID();

INSERT INTO MaintenanceAssets (Id, AssetNumber, Name, [Description], AssetCategoryId, AssetType, Model, Manufacturer, SerialNumber, PurchaseDate, PurchaseCost, [Location], [Status], InstallationDate, WarrantyExpirationDate, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@Asset1Id, 'HVAC-001', 'Main HVAC Unit Building A', 'Central air conditioning system for Building A', @HvacCategoryId, 'HVAC', 'CAC-5000', 'Carrier', 'CAR123456789', DATEADD(YEAR, -3, @Now), 75000.00, 'Building A - Roof', 'Active', DATEADD(YEAR, -3, @Now), DATEADD(YEAR, 2, @Now), @Now, @Now, @TenantId),
    (@Asset2Id, 'HVAC-002', 'HVAC Unit Building B', 'Backup HVAC system for Building B', @HvacCategoryId, 'HVAC', 'CAC-3000', 'Trane', 'TRN987654321', DATEADD(YEAR, -2, @Now), 45000.00, 'Building B - Roof', 'Active', DATEADD(YEAR, -2, @Now), DATEADD(YEAR, 3, @Now), @Now, @Now, @TenantId),
    (@Asset3Id, 'ELEV-001', 'Main Elevator Building A', 'Primary passenger elevator for Building A', @ElevatorCategoryId, 'Elevator', 'E-2000', 'Otis', 'OTIS567890123', DATEADD(YEAR, -5, @Now), 125000.00, 'Building A - Floors 1-10', 'Active', DATEADD(YEAR, -5, @Now), DATEADD(YEAR, 0, @Now), @Now, @Now, @TenantId),
    (@Asset4Id, 'GEN-001', 'Emergency Generator', 'Main emergency backup generator', @GeneratorCategoryId, 'Generator', 'DG-500', 'Cummins', 'CUM345678901', DATEADD(YEAR, -4, @Now), 85000.00, 'Building C - Generator Room', 'Active', DATEADD(YEAR, -4, @Now), DATEADD(YEAR, 1, @Now), @Now, @Now, @TenantId),
    (@Asset5Id, 'PUMP-001', 'Water Circulation Pump', 'Main water circulation system pump', @PumpCategoryId, 'Pump', 'P-1200', 'Grundfos', 'GRU789012345', DATEADD(YEAR, -2, @Now), 15000.00, 'Building A - Mechanical Room', 'Active', DATEADD(YEAR, -2, @Now), DATEADD(YEAR, 3, @Now), @Now, @Now, @TenantId),
    (@Asset6Id, 'LIGHT-001', 'LED Lighting System', 'Main building LED lighting system', @LightingCategoryId, 'Lighting', 'LED-PRO', 'Philips', 'PHI901234567', DATEADD(YEAR, -1, @Now), 25000.00, 'Buildings A-C - All Floors', 'Active', DATEADD(YEAR, -1, @Now), DATEADD(YEAR, 4, @Now), @Now, @Now, @TenantId);

-- 10. Maintenance Schedules
INSERT INTO MaintenanceSchedules (Id, AssetId, MaintenanceTypeId, Name, [Description], FrequencyDays, FrequencyWeeks, FrequencyMonths, NextDueDate, LastCompletedDate, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @Asset1Id, @PreventiveTypeId, 'HVAC Quarterly Maintenance', 'Quarterly preventive maintenance for HVAC system', NULL, NULL, 3, DATEADD(MONTH, 1, @Now), DATEADD(MONTH, -2, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Asset2Id, @PreventiveTypeId, 'HVAC Filter Replacement', 'Monthly filter replacement', NULL, NULL, 1, DATEADD(DAY, 15, @Now), DATEADD(DAY, -15, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Asset3Id, @InspectionTypeId, 'Elevator Safety Inspection', 'Monthly elevator safety inspection', NULL, NULL, 1, DATEADD(DAY, 10, @Now), DATEADD(DAY, -20, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Asset4Id, @PreventiveTypeId, 'Generator Load Test', 'Monthly generator load testing', NULL, NULL, 1, DATEADD(DAY, 5, @Now), DATEADD(DAY, -25, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Asset5Id, @PreventiveTypeId, 'Pump Maintenance', 'Quarterly pump maintenance', NULL, NULL, 3, DATEADD(MONTH, 2, @Now), DATEADD(MONTH, -1, @Now), 1, @Now, @Now, @TenantId);

-- 11. Work Orders
DECLARE @WO1Id UNIQUEIDENTIFIER = NEWID();
DECLARE @WO2Id UNIQUEIDENTIFIER = NEWID();
DECLARE @WO3Id UNIQUEIDENTIFIER = NEWID();
DECLARE @WO4Id UNIQUEIDENTIFIER = NEWID();
DECLARE @WO5Id UNIQUEIDENTIFIER = NEWID();

INSERT INTO WorkOrders (Id, WorkOrderNumber, Title, [Description], AssetId, WorkOrderTypeId, MaintenanceTypeId, PriorityLevelId, [Status], AssignedTechnicianId, RequestedStartDate, ActualStartDate, ActualCompletionDate, EstimatedCost, ActualCost, EstimatedHours, ActualHours, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (@WO1Id, 'WO-2024-001', 'HVAC Unit Maintenance', 'Quarterly maintenance for main HVAC unit', @Asset1Id, @PreventiveWOTypeId, @PreventiveTypeId, @MediumPriorityId, 'Completed', @Tech1Id, DATEADD(DAY, -30, @Now), DATEADD(DAY, -30, @Now), DATEADD(DAY, -29, @Now), 500.00, 475.00, 4.0, 3.5, DATEADD(DAY, -35, @Now), DATEADD(DAY, -29, @Now), @TenantId),
    (@WO2Id, 'WO-2024-002', 'Elevator Repair', 'Fix elevator door sensor issue', @Asset3Id, @CorrectiveWOTypeId, @CorrectiveTypeId, @HighPriorityId, 'Completed', @Tech2Id, DATEADD(DAY, -20, @Now), DATEADD(DAY, -20, @Now), DATEADD(DAY, -19, @Now), 800.00, 750.00, 6.0, 5.5, DATEADD(DAY, -22, @Now), DATEADD(DAY, -19, @Now), @TenantId),
    (@WO3Id, 'WO-2024-003', 'Generator Load Test', 'Monthly load testing for emergency generator', @Asset4Id, @PreventiveWOTypeId, @PreventiveTypeId, @MediumPriorityId, 'Completed', @Tech3Id, DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), 200.00, 180.00, 2.0, 1.5, DATEADD(DAY, -18, @Now), DATEADD(DAY, -15, @Now), @TenantId),
    (@WO4Id, 'WO-2024-004', 'Pump Filter Replacement', 'Replace water pump filters', @Asset5Id, @PreventiveWOTypeId, @PreventiveTypeId, @LowPriorityId, 'InProgress', @Tech4Id, @Now, @Now, NULL, 150.00, 0.00, 2.0, 0.0, DATEADD(DAY, -2, @Now), @Now, @TenantId),
    (@WO5Id, 'WO-2024-005', 'LED System Inspection', 'Inspect and test LED lighting system', @Asset6Id, @PreventiveWOTypeId, @InspectionTypeId, @LowPriorityId, 'Assigned', @Tech1Id, DATEADD(DAY, 3, @Now), NULL, NULL, 100.00, 0.00, 1.5, 0.0, DATEADD(DAY, -1, @Now), DATEADD(DAY, -1, @Now), @TenantId);

-- 12. Work Order Tasks
INSERT INTO WorkOrderTasks (Id, WorkOrderId, TaskName, [Description], Sequence, [Status], EstimatedHours, ActualHours, AssignedTechnicianId, StartedAt, CompletedAt, IsRequired, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @WO1Id, 'Filter Replacement', 'Replace HVAC air filters', 1, 'Completed', 1.0, 0.5, @Tech1Id, DATEADD(DAY, -30, @Now), DATEADD(DAY, -30, @Now), 1, DATEADD(DAY, -35, @Now), DATEADD(DAY, -30, @Now), @TenantId),
    (NEWID(), @WO1Id, 'Coil Cleaning', 'Clean evaporator and condenser coils', 2, 'Completed', 2.0, 2.0, @Tech1Id, DATEADD(DAY, -30, @Now), DATEADD(DAY, -29, @Now), 1, DATEADD(DAY, -35, @Now), DATEADD(DAY, -29, @Now), @TenantId),
    (NEWID(), @WO1Id, 'System Testing', 'Test system operation and performance', 3, 'Completed', 1.0, 1.0, @Tech1Id, DATEADD(DAY, -29, @Now), DATEADD(DAY, -29, @Now), 1, DATEADD(DAY, -35, @Now), DATEADD(DAY, -29, @Now), @TenantId),
    (NEWID(), @WO4Id, 'Remove Old Filters', 'Remove and dispose of old pump filters', 1, 'Completed', 0.5, 0.5, @Tech4Id, @Now, @Now, 1, DATEADD(DAY, -2, @Now), @Now, @TenantId),
    (NEWID(), @WO4Id, 'Install New Filters', 'Install new pump filters', 2, 'InProgress', 1.0, 0.0, @Tech4Id, @Now, NULL, 1, DATEADD(DAY, -2, @Now), @Now, @TenantId),
    (NEWID(), @WO4Id, 'Test System', 'Test pump operation after filter replacement', 3, 'Pending', 0.5, 0.0, @Tech4Id, NULL, NULL, 1, DATEADD(DAY, -2, @Now), @Now, @TenantId);

-- 13. Work Order Labor Records
INSERT INTO WorkOrderLabor (Id, WorkOrderId, TechnicianId, StartTime, EndTime, Hours, HourlyRate, TotalCost, LaborType, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @WO1Id, @Tech1Id, DATEADD(DAY, -30, @Now), DATEADD(HOUR, 3.5, DATEADD(DAY, -30, @Now)), 3.5, 35.00, 122.50, 'Regular', DATEADD(DAY, -29, @Now), DATEADD(DAY, -29, @Now), @TenantId),
    (NEWID(), @WO2Id, @Tech2Id, DATEADD(DAY, -20, @Now), DATEADD(HOUR, 5.5, DATEADD(DAY, -20, @Now)), 5.5, 42.00, 231.00, 'Regular', DATEADD(DAY, -19, @Now), DATEADD(DAY, -19, @Now), @TenantId),
    (NEWID(), @WO3Id, @Tech3Id, DATEADD(DAY, -15, @Now), DATEADD(HOUR, 1.5, DATEADD(DAY, -15, @Now)), 1.5, 38.00, 57.00, 'Regular', DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), @TenantId);

-- 14. Asset Inspections
INSERT INTO AssetInspections (Id, AssetId, InspectionDate, InspectorId, InspectionType, [Status], OverallCondition, Notes, NextInspectionDate, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @Asset1Id, DATEADD(DAY, -30, @Now), @Tech1Id, 'Preventive', 'Completed', 'Good', 'All systems operating within normal parameters', DATEADD(MONTH, 3, @Now), DATEADD(DAY, -30, @Now), DATEADD(DAY, -30, @Now), @TenantId),
    (NEWID(), @Asset3Id, DATEADD(DAY, -20, @Now), @Tech2Id, 'Safety', 'Completed', 'Fair', 'Door sensor required adjustment, now fixed', DATEADD(MONTH, 1, @Now), DATEADD(DAY, -20, @Now), DATEADD(DAY, -19, @Now), @TenantId),
    (NEWID(), @Asset4Id, DATEADD(DAY, -15, @Now), @Tech3Id, 'Performance', 'Completed', 'Excellent', 'Generator performing at optimal levels', DATEADD(MONTH, 1, @Now), DATEADD(DAY, -15, @Now), DATEADD(DAY, -15, @Now), @TenantId);

-- 15. Asset Downtimes
INSERT INTO AssetDowntimes (Id, AssetId, DowntimeStart, DowntimeEnd, DowntimeMinutes, DowntimeType, Priority, EstimatedCostImpact, ActualCostImpact, [Status], CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @Asset3Id, DATEADD(DAY, -20, @Now), DATEADD(HOUR, 6, DATEADD(DAY, -20, @Now)), 360, 'Repair', 'High', 2000.00, 1500.00, 'Completed', DATEADD(DAY, -20, @Now), DATEADD(DAY, -19, @Now), @TenantId),
    (NEWID(), @Asset1Id, DATEADD(DAY, -30, @Now), DATEADD(HOUR, 4, DATEADD(DAY, -30, @Now)), 240, 'Maintenance', 'Medium', 800.00, 600.00, 'Completed', DATEADD(DAY, -30, @Now), DATEADD(DAY, -29, @Now), @TenantId);

-- 16. Technician Team Members
INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, JoinDate, [Role], IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @MaintenanceTeamId, @Tech1Id, DATEADD(YEAR, -2, @Now), 'Senior Technician', 1, @Now, @Now, @TenantId),
    (NEWID(), @HvacTeamId, @Tech1Id, DATEADD(YEAR, -2, @Now), 'HVAC Specialist', 1, @Now, @Now, @TenantId),
    (NEWID(), @ElectricalTeamId, @Tech2Id, DATEADD(YEAR, -1, @Now), 'Lead Electrician', 1, @Now, @Now, @TenantId),
    (NEWID(), @MaintenanceTeamId, @Tech3Id, DATEADD(MONTH, -8, @Now), 'Technician', 1, @Now, @Now, @TenantId),
    (NEWID(), @MaintenanceTeamId, @Tech4Id, DATEADD(MONTH, -6, @Now), 'Junior Technician', 1, @Now, @Now, @TenantId);

-- 17. Technician Skill Assignments
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, CertifiedDate, ExpirationDate, IsActive, CreatedAt, UpdatedAt, TenantId)
VALUES 
    (NEWID(), @Tech1Id, @HvacSkillId, 'Expert', DATEADD(YEAR, -1, @Now), DATEADD(YEAR, 2, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Tech2Id, @ElectricalSkillId, 'Expert', DATEADD(YEAR, -2, @Now), DATEADD(YEAR, 1, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Tech3Id, @MechanicalSkillId, 'Advanced', DATEADD(MONTH, -6, @Now), DATEADD(YEAR, 2, @Now), 1, @Now, @Now, @TenantId),
    (NEWID(), @Tech4Id, @PlumbingSkillId, 'Intermediate', DATEADD(MONTH, -3, @Now), DATEADD(YEAR, 2, @Now), 1, @Now, @Now, @TenantId);

PRINT 'Maintenance data seeding completed successfully!';
PRINT 'Created:';
PRINT '- 5 Asset Types';
PRINT '- 5 Asset Categories'; 
PRINT '- 4 Priority Levels';
PRINT '- 4 Maintenance Types';
PRINT '- 3 Work Order Types';
PRINT '- 4 Technical Skills';
PRINT '- 3 Technician Teams';
PRINT '- 4 Technicians';
PRINT '- 6 Maintenance Assets';
PRINT '- 5 Maintenance Schedules';
PRINT '- 5 Work Orders with Tasks and Labor';
PRINT '- 3 Asset Inspections';
PRINT '- 2 Asset Downtimes';
PRINT '- Team and Skill Assignments';

GO