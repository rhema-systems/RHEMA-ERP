-- Comprehensive Admin Tables Seed Script for Maintenance System
-- This script seeds all the admin/lookup tables needed for the maintenance module

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @CurrentTime DATETIME2 = GETUTCDATE();

-- ================================
-- 1. DEPARTMENTS (HR Admin)
-- ================================
INSERT INTO Departments (Id, Code, Name, Description, DepartmentType, IsActive, TenantId, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
    (NEWID(), 'MAINT', 'Maintenance Department', 'Facility maintenance and repairs', 'Operations', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'FACIL', 'Facilities Management', 'Building and facility management', 'Operations', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'ENG', 'Engineering', 'Technical engineering services', 'Engineering', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'SAFE', 'Safety & Compliance', 'Workplace safety and regulatory compliance', 'Safety', 1, @TenantId, @CurrentTime, @CurrentTime, 0);

PRINT 'Departments seeded: 4 records';

-- ================================
-- 2. EMPLOYEE POSITIONS (HR Admin)
-- ================================
INSERT INTO EmployeePositions (Id, Title, Code, Department, Level, IsActive, TenantId, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Maintenance Technician I', 'MTECH1', 'Maintenance', 'Entry', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Maintenance Technician II', 'MTECH2', 'Maintenance', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Senior Maintenance Technician', 'MTECH3', 'Maintenance', 'Senior', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Maintenance Supervisor', 'MSUP', 'Maintenance', 'Supervisor', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Maintenance Manager', 'MMGR', 'Maintenance', 'Manager', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'HVAC Specialist', 'HVAC', 'Maintenance', 'Specialist', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Electrical Technician', 'ELEC', 'Maintenance', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Plumbing Technician', 'PLUMB', 'Maintenance', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0);

PRINT 'Employee Positions seeded: 8 records';

-- ================================
-- 3. SKILLS (HR Admin)
-- ================================
INSERT INTO Skill (Id, Name, Description, Category, SkillLevel, IsActive, TenantId, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Electrical Systems', 'Electrical installation and repair', 'Technical', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'HVAC Maintenance', 'Heating, ventilation, and air conditioning', 'Technical', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Plumbing Repair', 'Water and sewage system maintenance', 'Technical', 'Beginner', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Mechanical Systems', 'General mechanical equipment repair', 'Technical', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Welding & Fabrication', 'Metal joining and fabrication skills', 'Technical', 'Advanced', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Safety Protocols', 'Workplace safety and OSHA compliance', 'Safety', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Preventive Maintenance', 'Scheduled maintenance procedures', 'Process', 'Intermediate', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Equipment Diagnostics', 'Troubleshooting and problem identification', 'Analytical', 'Advanced', 1, @TenantId, @CurrentTime, @CurrentTime, 0);

PRINT 'Skills seeded: 8 records';

-- ================================
-- 4. TECHNICAL SKILLS (Maintenance Admin)
-- ================================
INSERT INTO TechnicalSkills (Id, Code, Name, Description, Category, SkillLevel, Complexity, RiskLevel, IsActive, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'ELEC_BASIC', 'Basic Electrical', 'Basic electrical maintenance and troubleshooting', 'Electrical', 'Beginner', 'Low', 'Medium', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'ELEC_ADV', 'Advanced Electrical', 'Complex electrical system maintenance and installation', 'Electrical', 'Expert', 'High', 'High', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'HVAC_BASIC', 'Basic HVAC', 'Basic HVAC system maintenance and filter changes', 'HVAC', 'Beginner', 'Low', 'Low', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'HVAC_ADV', 'Advanced HVAC', 'Complex HVAC system repair and refrigeration', 'HVAC', 'Expert', 'High', 'Medium', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'PLUMB_BASIC', 'Basic Plumbing', 'Basic plumbing repairs and maintenance', 'Plumbing', 'Beginner', 'Low', 'Low', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'MECH_BASIC', 'Basic Mechanical', 'General mechanical equipment maintenance', 'Mechanical', 'Intermediate', 'Medium', 'Medium', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'WELD_BASIC', 'Basic Welding', 'Basic welding and metal fabrication', 'Welding', 'Intermediate', 'Medium', 'High', 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'SAFETY_LOTO', 'Lockout/Tagout', 'Energy isolation safety procedures', 'Safety', 'Intermediate', 'Medium', 'Critical', 1, @TenantId, @CurrentTime, 0);

PRINT 'Technical Skills seeded: 8 records';

-- ================================
-- 5. TECHNICIAN TEAMS (Maintenance Admin)
-- ================================
INSERT INTO TechnicianTeams (Id, Name, Description, Status, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'HVAC Team', 'Heating, Ventilation & Air Conditioning specialists', 'Active', @TenantId, @CurrentTime, 0),
    (NEWID(), 'Electrical Team', 'Electrical systems maintenance and repair team', 'Active', @TenantId, @CurrentTime, 0),
    (NEWID(), 'General Maintenance', 'General facility maintenance and repairs', 'Active', @TenantId, @CurrentTime, 0),
    (NEWID(), 'Emergency Response', '24/7 emergency maintenance response team', 'Active', @TenantId, @CurrentTime, 0),
    (NEWID(), 'Preventive Maintenance', 'Scheduled preventive maintenance team', 'Active', @TenantId, @CurrentTime, 0),
    (NEWID(), 'Plumbing Team', 'Water systems and plumbing specialists', 'Active', @TenantId, @CurrentTime, 0);

PRINT 'Technician Teams seeded: 6 records';

-- ================================
-- 6. WORKFLOW ENTITY TYPES (System Admin)
-- ================================
INSERT INTO WorkflowEntityTypes (Id, Name, Description, EntityTypeName, IsActive, TenantId, CreatedAt, UpdatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Job Card', 'Maintenance job card workflow', 'JobCard', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Work Order', 'Maintenance work order workflow', 'WorkOrder', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Asset Inspection', 'Asset inspection approval workflow', 'AssetInspection', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Safety Protocol', 'Safety protocol approval workflow', 'SafetyProtocol', 1, @TenantId, @CurrentTime, @CurrentTime, 0),
    (NEWID(), 'Maintenance Schedule', 'Maintenance schedule approval workflow', 'MaintenanceSchedule', 1, @TenantId, @CurrentTime, @CurrentTime, 0);

PRINT 'Workflow Entity Types seeded: 5 records';

-- ================================
-- 7. ADDITIONAL REFERENCE DATA
-- ================================

-- Ensure we have comprehensive Work Order Types
INSERT INTO WorkOrderTypes (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'CALIB', 'Calibration', 'Equipment calibration and testing', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE Code = 'CALIB');

INSERT INTO WorkOrderTypes (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'INSTALL', 'Installation', 'New equipment installation', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE Code = 'INSTALL');

INSERT INTO WorkOrderTypes (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'UPGRADE', 'Upgrade', 'Equipment upgrade or modernization', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM WorkOrderTypes WHERE Code = 'UPGRADE');

PRINT 'Additional Work Order Types seeded';

-- Ensure we have comprehensive Maintenance Types
INSERT INTO MaintenanceTypes (Id, Code, Name, Description, Category, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'EM', 'Emergency Maintenance', 'Urgent breakdown or failure repair', 'Emergency', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceTypes WHERE Code = 'EM');

INSERT INTO MaintenanceTypes (Id, Code, Name, Description, Category, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'PD', 'Predictive Maintenance', 'Condition-based maintenance using sensors/data', 'Predictive', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceTypes WHERE Code = 'PD');

INSERT INTO MaintenanceTypes (Id, Code, Name, Description, Category, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'IN', 'Inspection', 'Safety and compliance inspections', 'Inspection', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceTypes WHERE Code = 'IN');

PRINT 'Additional Maintenance Types seeded';

-- Add more Asset Categories if missing
INSERT INTO MaintenanceAssetCategories (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'SAFETY', 'Safety Systems', 'Safety and security equipment', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceAssetCategories WHERE Code = 'SAFETY');

INSERT INTO MaintenanceAssetCategories (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'IT', 'IT Equipment', 'Information technology assets', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceAssetCategories WHERE Code = 'IT');

INSERT INTO MaintenanceAssetCategories (Id, Code, Name, Description, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 'VEHICLE', 'Vehicles', 'Company vehicles and transportation', 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM MaintenanceAssetCategories WHERE Code = 'VEHICLE');

PRINT 'Additional Asset Categories seeded';

-- Ensure we have a Scheduled priority level
INSERT INTO PriorityLevels (Id, Level, Name, Description, Color, ResponseTimeHours, IsActive, TenantId, CreatedAt)
SELECT NEWID(), 5, 'Scheduled', 'Scheduled maintenance work', '#0000FF', 168, 1, @TenantId, @CurrentTime
WHERE NOT EXISTS (SELECT 1 FROM PriorityLevels WHERE Level = 5);

PRINT 'Additional Priority Level seeded';

-- ================================
-- 8. SUMMARY REPORT
-- ================================

PRINT '';
PRINT '=== ADMIN TABLES SEEDING COMPLETED ===';
PRINT 'Tables seeded for admin sections:';

SELECT 'Departments' as AdminSection, COUNT(*) as RecordCount FROM Departments
UNION ALL
SELECT 'Employee Positions', COUNT(*) FROM EmployeePositions  
UNION ALL
SELECT 'Skills', COUNT(*) FROM Skill
UNION ALL
SELECT 'Technical Skills', COUNT(*) FROM TechnicalSkills
UNION ALL
SELECT 'Technician Teams', COUNT(*) FROM TechnicianTeams
UNION ALL
SELECT 'Workflow Entity Types', COUNT(*) FROM WorkflowEntityTypes
UNION ALL
SELECT 'Work Order Types', COUNT(*) FROM WorkOrderTypes
UNION ALL
SELECT 'Maintenance Types', COUNT(*) FROM MaintenanceTypes
UNION ALL
SELECT 'Asset Categories', COUNT(*) FROM MaintenanceAssetCategories
UNION ALL
SELECT 'Priority Levels', COUNT(*) FROM PriorityLevels
UNION ALL
SELECT 'Asset Types', COUNT(*) FROM AssetTypes
ORDER BY AdminSection;

PRINT '';
PRINT 'Admin sections now have comprehensive reference data!';
PRINT 'These tables will populate dropdowns and admin management interfaces.';

COMMIT TRANSACTION;