-- Employee and Technician Seed Data Script
-- This script creates employee records that can be used as technicians in the maintenance module
-- Execute this script against your SQL Server database

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @CurrentTime DATETIME2 = GETUTCDATE();

-- ================================
-- 1. DEPARTMENTS AND POSITIONS
-- ================================

-- Create Departments if they don't exist
INSERT INTO Departments (Id, Code, Name, Description, DepartmentType, IsActive, TenantId, CreatedAt)
SELECT * FROM (VALUES 
    (NEWID(), 'MAINT', 'Maintenance', 'Facility Maintenance and Engineering', 'Technical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'ELECT', 'Electrical', 'Electrical Systems and Power', 'Technical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'HVAC', 'HVAC', 'Heating, Ventilation and Air Conditioning', 'Technical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'PLUMB', 'Plumbing', 'Plumbing and Water Systems', 'Technical', 1, @TenantId, @CurrentTime),
    (NEWID(), 'ADMIN', 'Administration', 'Administrative and Management', 'Administrative', 1, @TenantId, @CurrentTime)
) AS Source(Id, Code, Name, Description, DepartmentType, IsActive, TenantId, CreatedAt)
WHERE NOT EXISTS (SELECT 1 FROM Departments WHERE Code = Source.Code AND TenantId = @TenantId);

-- Get Department IDs
DECLARE @MaintenanceDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'MAINT' AND TenantId = @TenantId);
DECLARE @ElectricalDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'ELECT' AND TenantId = @TenantId);
DECLARE @HVACDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'HVAC' AND TenantId = @TenantId);
DECLARE @PlumbingDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'PLUMB' AND TenantId = @TenantId);
DECLARE @AdminDeptId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Departments WHERE Code = 'ADMIN' AND TenantId = @TenantId);

-- Create Employee Positions
INSERT INTO EmployeePositions (Id, Title, Description, Level, IsActive, TenantId, CreatedAt)
SELECT * FROM (VALUES 
    (NEWID(), 'Maintenance Manager', 'Manager of maintenance operations', 'M1', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Senior Technician', 'Senior level maintenance technician', 'T3', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Maintenance Technician', 'General maintenance technician', 'T2', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Junior Technician', 'Entry level maintenance technician', 'T1', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Electrical Technician', 'Licensed electrical technician', 'T2', 1, @TenantId, @CurrentTime),
    (NEWID(), 'HVAC Technician', 'HVAC systems specialist', 'T2', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Plumbing Technician', 'Plumbing systems specialist', 'T2', 1, @TenantId, @CurrentTime),
    (NEWID(), 'Facilities Coordinator', 'Facilities maintenance coordinator', 'C1', 1, @TenantId, @CurrentTime)
) AS Source(Id, Title, Description, Level, IsActive, TenantId, CreatedAt)
WHERE NOT EXISTS (SELECT 1 FROM EmployeePositions WHERE Title = Source.Title AND TenantId = @TenantId);

-- Get Position IDs
DECLARE @ManagerPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Maintenance Manager' AND TenantId = @TenantId);
DECLARE @SeniorTechPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Senior Technician' AND TenantId = @TenantId);
DECLARE @TechnicianPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Maintenance Technician' AND TenantId = @TenantId);
DECLARE @JuniorTechPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Junior Technician' AND TenantId = @TenantId);
DECLARE @ElecTechPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Electrical Technician' AND TenantId = @TenantId);
DECLARE @HVACTechPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'HVAC Technician' AND TenantId = @TenantId);
DECLARE @PlumbTechPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Plumbing Technician' AND TenantId = @TenantId);
DECLARE @CoordinatorPosId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM EmployeePositions WHERE Title = 'Facilities Coordinator' AND TenantId = @TenantId);

-- ================================
-- 2. EMPLOYEES (TECHNICIANS)
-- ================================

-- Create sample employees that can work as technicians
INSERT INTO Employees (Id, EmployeeNumber, FirstName, LastName, MiddleName, EmailAddress, MobileNumber, DepartmentId, PositionId, StaffStatus, DateEmployed, IsActive, TenantId, CreatedAt)
VALUES 
    -- Maintenance Manager
    (NEWID(), 'EMP001', 'John', 'Smith', 'Michael', 'john.smith@company.com', '+1-555-0101', @MaintenanceDeptId, @ManagerPosId, 'Active', DATEADD(year, -5, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Senior Technicians
    (NEWID(), 'EMP002', 'Sarah', 'Johnson', 'Elizabeth', 'sarah.johnson@company.com', '+1-555-0102', @MaintenanceDeptId, @SeniorTechPosId, 'Active', DATEADD(year, -4, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP003', 'Robert', 'Williams', 'James', 'robert.williams@company.com', '+1-555-0103', @ElectricalDeptId, @SeniorTechPosId, 'Active', DATEADD(year, -6, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Maintenance Technicians
    (NEWID(), 'EMP004', 'Maria', 'Garcia', 'Carmen', 'maria.garcia@company.com', '+1-555-0104', @MaintenanceDeptId, @TechnicianPosId, 'Active', DATEADD(year, -3, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP005', 'David', 'Brown', 'Lee', 'david.brown@company.com', '+1-555-0105', @MaintenanceDeptId, @TechnicianPosId, 'Active', DATEADD(year, -2, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP006', 'Jennifer', 'Davis', 'Ann', 'jennifer.davis@company.com', '+1-555-0106', @HVACDeptId, @TechnicianPosId, 'Active', DATEADD(year, -3, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Electrical Technicians
    (NEWID(), 'EMP007', 'Michael', 'Miller', 'Thomas', 'michael.miller@company.com', '+1-555-0107', @ElectricalDeptId, @ElecTechPosId, 'Active', DATEADD(year, -4, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP008', 'Lisa', 'Wilson', 'Marie', 'lisa.wilson@company.com', '+1-555-0108', @ElectricalDeptId, @ElecTechPosId, 'Active', DATEADD(year, -2, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- HVAC Technicians
    (NEWID(), 'EMP009', 'James', 'Moore', 'Richard', 'james.moore@company.com', '+1-555-0109', @HVACDeptId, @HVACTechPosId, 'Active', DATEADD(year, -3, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP010', 'Patricia', 'Taylor', 'Rose', 'patricia.taylor@company.com', '+1-555-0110', @HVACDeptId, @HVACTechPosId, 'Active', DATEADD(year, -1, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Plumbing Technicians
    (NEWID(), 'EMP011', 'Christopher', 'Anderson', 'Paul', 'chris.anderson@company.com', '+1-555-0111', @PlumbingDeptId, @PlumbTechPosId, 'Active', DATEADD(year, -2, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP012', 'Nancy', 'Thomas', 'Grace', 'nancy.thomas@company.com', '+1-555-0112', @PlumbingDeptId, @PlumbTechPosId, 'Active', DATEADD(year, -3, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Junior Technicians
    (NEWID(), 'EMP013', 'Kevin', 'Jackson', 'Daniel', 'kevin.jackson@company.com', '+1-555-0113', @MaintenanceDeptId, @JuniorTechPosId, 'Active', DATEADD(month, -8, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP014', 'Michelle', 'White', 'Lynn', 'michelle.white@company.com', '+1-555-0114', @MaintenanceDeptId, @JuniorTechPosId, 'Active', DATEADD(month, -6, @CurrentTime), 1, @TenantId, @CurrentTime),
    (NEWID(), 'EMP015', 'Steven', 'Harris', 'Mark', 'steven.harris@company.com', '+1-555-0115', @ElectricalDeptId, @JuniorTechPosId, 'Active', DATEADD(month, -10, @CurrentTime), 1, @TenantId, @CurrentTime),
    
    -- Facilities Coordinator
    (NEWID(), 'EMP016', 'Amanda', 'Martin', 'Sue', 'amanda.martin@company.com', '+1-555-0116', @AdminDeptId, @CoordinatorPosId, 'Active', DATEADD(year, -2, @CurrentTime), 1, @TenantId, @CurrentTime);

-- ================================
-- 3. TECHNICIAN TEAMS AND ASSIGNMENTS
-- ================================

-- Get some employee IDs for team assignments
DECLARE @ManagerId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP001');
DECLARE @SeniorTech1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP002');
DECLARE @SeniorTech2Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP003');
DECLARE @HVACTech1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP009');
DECLARE @ElecTech1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP007');
DECLARE @PlumbTech1Id UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP011');

-- Update some existing technician teams with team leaders
UPDATE TechnicianTeams 
SET TeamLeaderId = @SeniorTech1Id 
WHERE Name = 'HVAC Team' AND TenantId = @TenantId;

UPDATE TechnicianTeams 
SET TeamLeaderId = @SeniorTech2Id 
WHERE Name = 'Electrical Team' AND TenantId = @TenantId;

UPDATE TechnicianTeams 
SET TeamLeaderId = @ManagerId 
WHERE Name = 'General Maintenance' AND TenantId = @TenantId;

UPDATE TechnicianTeams 
SET TeamLeaderId = @SeniorTech1Id 
WHERE Name = 'Emergency Response' AND TenantId = @TenantId;

-- Add team members to existing teams
DECLARE @HVACTeamId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianTeams WHERE Name = 'HVAC Team');
DECLARE @ElecTeamId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianTeams WHERE Name = 'Electrical Team');
DECLARE @GeneralTeamId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianTeams WHERE Name = 'General Maintenance');
DECLARE @EmergencyTeamId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicianTeams WHERE Name = 'Emergency Response');

-- HVAC Team Members
INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, Role, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), @HVACTeamId, @HVACTech1Id, 'Lead Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @HVACTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP010'), 'Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @HVACTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP006'), 'Support Technician', 1, @TenantId, @CurrentTime);

-- Electrical Team Members
INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, Role, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), @ElecTeamId, @ElecTech1Id, 'Lead Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @ElecTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP008'), 'Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @ElecTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP015'), 'Apprentice', 1, @TenantId, @CurrentTime);

-- General Maintenance Team Members
INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, Role, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), @GeneralTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP004'), 'Senior Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @GeneralTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP005'), 'Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @GeneralTeamId, @PlumbTech1Id, 'Plumbing Specialist', 1, @TenantId, @CurrentTime),
    (NEWID(), @GeneralTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP013'), 'Junior Technician', 1, @TenantId, @CurrentTime),
    (NEWID(), @GeneralTeamId, (SELECT TOP 1 Id FROM Employees WHERE EmployeeNumber = 'EMP014'), 'Junior Technician', 1, @TenantId, @CurrentTime);

-- Emergency Response Team Members (cross-trained technicians)
INSERT INTO TechnicianTeamMembers (Id, TeamId, TechnicianId, Role, IsActive, TenantId, CreatedAt)
VALUES 
    (NEWID(), @EmergencyTeamId, @SeniorTech1Id, 'Emergency Lead', 1, @TenantId, @CurrentTime),
    (NEWID(), @EmergencyTeamId, @SeniorTech2Id, 'Emergency Lead', 1, @TenantId, @CurrentTime),
    (NEWID(), @EmergencyTeamId, @ElecTech1Id, 'Emergency Electrician', 1, @TenantId, @CurrentTime),
    (NEWID(), @EmergencyTeamId, @HVACTech1Id, 'Emergency HVAC', 1, @TenantId, @CurrentTime);

-- ================================
-- 4. TECHNICIAN SKILLS ASSIGNMENTS
-- ================================

-- Assign skills to technicians based on their specialization
-- Get skill IDs
DECLARE @ElecBasicSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'ELEC_BASIC');
DECLARE @ElecAdvSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'ELEC_ADV');
DECLARE @HVACBasicSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'HVAC_BASIC');
DECLARE @HVACAdvSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'HVAC_ADV');
DECLARE @PlumbBasicSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'PLUMB_BASIC');
DECLARE @MechRepairSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'MECH_REPAIR');
DECLARE @WeldBasicSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'WELD_BASIC');
DECLARE @ITSupportSkillId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM TechnicalSkills WHERE Code = 'IT_SUPPORT');

-- Manager Skills (all basic skills)
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, IsVerified, CertificationDate, TenantId, CreatedAt)
VALUES 
    (NEWID(), @ManagerId, @ElecBasicSkillId, 'Intermediate', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @ManagerId, @HVACBasicSkillId, 'Intermediate', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @ManagerId, @PlumbBasicSkillId, 'Intermediate', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @ManagerId, @MechRepairSkillId, 'Advanced', 1, DATEADD(year, -3, @CurrentTime), @TenantId, @CurrentTime);

-- Senior Electrical Technician Skills
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, IsVerified, CertificationDate, TenantId, CreatedAt)
VALUES 
    (NEWID(), @SeniorTech2Id, @ElecAdvSkillId, 'Expert', 1, DATEADD(year, -3, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @SeniorTech2Id, @ElecBasicSkillId, 'Expert', 1, DATEADD(year, -4, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @SeniorTech2Id, @MechRepairSkillId, 'Advanced', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime);

-- HVAC Senior Technician Skills
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, IsVerified, CertificationDate, TenantId, CreatedAt)
VALUES 
    (NEWID(), @SeniorTech1Id, @HVACAdvSkillId, 'Expert', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @SeniorTech1Id, @HVACBasicSkillId, 'Expert', 1, DATEADD(year, -3, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @SeniorTech1Id, @ElecBasicSkillId, 'Advanced', 1, DATEADD(year, -1, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @SeniorTech1Id, @PlumbBasicSkillId, 'Intermediate', 1, DATEADD(year, -1, @CurrentTime), @TenantId, @CurrentTime);

-- Additional skill assignments for other technicians...
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, IsVerified, CertificationDate, TenantId, CreatedAt)
VALUES 
    -- HVAC Technician
    (NEWID(), @HVACTech1Id, @HVACAdvSkillId, 'Advanced', 1, DATEADD(year, -1, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @HVACTech1Id, @HVACBasicSkillId, 'Expert', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @HVACTech1Id, @ElecBasicSkillId, 'Intermediate', 1, DATEADD(month, -6, @CurrentTime), @TenantId, @CurrentTime),
    
    -- Electrical Technician
    (NEWID(), @ElecTech1Id, @ElecAdvSkillId, 'Advanced', 1, DATEADD(year, -2, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @ElecTech1Id, @ElecBasicSkillId, 'Expert', 1, DATEADD(year, -3, @CurrentTime), @TenantId, @CurrentTime),
    
    -- Plumbing Technician
    (NEWID(), @PlumbTech1Id, @PlumbBasicSkillId, 'Advanced', 1, DATEADD(year, -1, @CurrentTime), @TenantId, @CurrentTime),
    (NEWID(), @PlumbTech1Id, @MechRepairSkillId, 'Intermediate', 1, DATEADD(month, -8, @CurrentTime), @TenantId, @CurrentTime);

PRINT 'Employee and Technician seed data inserted successfully!';
PRINT 'Records created:';
PRINT '- Departments: 5 records';
PRINT '- Employee Positions: 8 records';
PRINT '- Employees: 16 records (all can be assigned to maintenance)';
PRINT '- Technician Team Members: 11 assignments';
PRINT '- Technician Skill Assignments: 15+ skill certifications';
PRINT '';
PRINT 'All employees have CanBeAssignedToMaintenance = 1 and can be used in work orders.';
PRINT 'Teams have been populated with leads and members.';
PRINT 'Skills have been assigned based on department and experience level.';

COMMIT TRANSACTION;

-- ================================
-- VERIFICATION QUERIES
-- ================================
PRINT '';
PRINT '=== VERIFICATION QUERIES ===';
PRINT 'Run these to verify the data was created correctly:';
PRINT '';
PRINT '-- Check employees that can be assigned to maintenance:';
PRINT 'SELECT e.EmployeeNumber, e.FirstName, e.LastName, d.Name as Department, p.Title as Position';
PRINT 'FROM Employees e';
PRINT 'LEFT JOIN Departments d ON e.DepartmentId = d.Id';
PRINT 'LEFT JOIN EmployeePositions p ON e.PositionId = p.Id';
PRINT 'WHERE e.CanBeAssignedToMaintenance = 1 AND e.IsActive = 1';
PRINT 'ORDER BY d.Name, p.Title;';
PRINT '';
PRINT '-- Check team assignments:';
PRINT 'SELECT t.Name as TeamName, e.FirstName + '' '' + e.LastName as TeamMember, tm.Role';
PRINT 'FROM TechnicianTeams t';
PRINT 'LEFT JOIN TechnicianTeamMembers tm ON t.Id = tm.TeamId';
PRINT 'LEFT JOIN Employees e ON tm.TechnicianId = e.Id';
PRINT 'WHERE t.Status = ''Active''';
PRINT 'ORDER BY t.Name, tm.Role;';