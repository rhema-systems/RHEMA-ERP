-- Simple Employee Seeding Script
-- This script creates just employees using existing departments

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @CurrentTime DATETIME2 = GETUTCDATE();

-- Use existing department IDs
DECLARE @MaintenanceDeptId UNIQUEIDENTIFIER = 'B092E0ED-242F-4A59-85A0-1984121CF671';
DECLARE @SafetyDeptId UNIQUEIDENTIFIER = '406C7DB9-6738-428B-A932-2623B8CE13C4';
DECLARE @EngineeringDeptId UNIQUEIDENTIFIER = '3FD9D9FA-2D6A-4CCF-92DB-33606DF51AD6';
DECLARE @FacilitiesDeptId UNIQUEIDENTIFIER = 'B06704F0-65DB-49F6-BEED-E384FBF0A1CF';

-- Create one position for each department
DECLARE @MaintenancePositionId UNIQUEIDENTIFIER = NEWID();
DECLARE @SafetyPositionId UNIQUEIDENTIFIER = NEWID();
DECLARE @EngineeringPositionId UNIQUEIDENTIFIER = NEWID();
DECLARE @FacilitiesPositionId UNIQUEIDENTIFIER = NEWID();

-- First, insert Employee Positions
INSERT INTO EmployeePositions (Id, Title, Description, Code, Level, DepartmentId, RequiresCertification, IsActive, TenantId, CreatedAt, IsDeleted)
VALUES 
    (@MaintenancePositionId, 'Maintenance Technician', 'General maintenance and repair specialist', 'MAINT-TECH', 2, @MaintenanceDeptId, 0, 1, @TenantId, @CurrentTime, 0),
    (@SafetyPositionId, 'Safety Inspector', 'Conducts safety inspections and audits', 'SAFETY-INSP', 3, @SafetyDeptId, 1, 1, @TenantId, @CurrentTime, 0),
    (@EngineeringPositionId, 'Engineering Technician', 'Provides technical support and engineering assistance', 'ENG-TECH', 2, @EngineeringDeptId, 0, 1, @TenantId, @CurrentTime, 0),
    (@FacilitiesPositionId, 'Facilities Coordinator', 'Coordinates facilities maintenance and operations', 'FACIL-COORD', 3, @FacilitiesDeptId, 0, 1, @TenantId, @CurrentTime, 0);

-- Insert Employees
INSERT INTO Employees (Id, EmployeeNumber, CorporateEmployeeID, FirstName, MiddleName, LastName, Title, Gender, DateOfBirth, Address, DateEmployed, DepartmentId, PositionId, Salary, IsActive, IsFullTime, EmailAddress, ContractType, ProbationPeriodDays, StaffStatus, CurrentWorkload, MaxWorkload, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'EMP001', 'CORP001', 'John', 'Michael', 'Smith', 'Maintenance Technician', 0, '1985-03-15', '123 Main St, City, State', '2020-01-15', @MaintenanceDeptId, @MaintenancePositionId, 65000.00, 1, 1, 'john.smith@company.com', 1, 90, 1, 80, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP002', 'CORP002', 'Sarah', 'Jane', 'Johnson', 'Maintenance Technician', 1, '1990-07-22', '456 Oak Ave, City, State', '2021-03-10', @MaintenanceDeptId, @MaintenancePositionId, 52000.00, 1, 1, 'sarah.johnson@company.com', 1, 90, 1, 75, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP003', 'CORP003', 'Robert', 'William', 'Davis', 'Maintenance Technician', 0, '1978-11-08', '789 Pine Dr, City, State', '2018-05-01', @MaintenanceDeptId, @MaintenancePositionId, 75000.00, 1, 1, 'robert.davis@company.com', 1, 90, 1, 85, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP004', 'CORP004', 'Emily', 'Rose', 'Wilson', 'Safety Inspector', 1, '1987-09-14', '321 Elm St, City, State', '2019-08-20', @SafetyDeptId, @SafetyPositionId, 58000.00, 1, 1, 'emily.wilson@company.com', 1, 90, 1, 70, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP005', 'CORP005', 'Michael', 'James', 'Brown', 'Maintenance Technician', 0, '1995-02-28', '654 Maple Ave, City, State', '2022-09-05', @MaintenanceDeptId, @MaintenancePositionId, 42000.00, 1, 1, 'michael.brown@company.com', 1, 90, 1, 60, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP006', 'CORP006', 'Lisa', 'Marie', 'Anderson', 'Safety Inspector', 1, '1982-06-17', '987 Cedar Ln, City, State', '2017-11-12', @SafetyDeptId, @SafetyPositionId, 62000.00, 1, 1, 'lisa.anderson@company.com', 1, 90, 1, 78, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP007', 'CORP007', 'David', 'Paul', 'Taylor', 'Facilities Coordinator', 0, '1986-04-03', '147 Birch St, City, State', '2020-07-18', @FacilitiesDeptId, @FacilitiesPositionId, 55000.00, 1, 1, 'david.taylor@company.com', 1, 90, 1, 72, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP008', 'CORP008', 'Jennifer', 'Nicole', 'Martinez', 'Engineering Technician', 1, '1991-12-09', '258 Spruce Ave, City, State', '2021-10-25', @EngineeringDeptId, @EngineeringPositionId, 60000.00, 1, 1, 'jennifer.martinez@company.com', 1, 90, 1, 76, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP009', 'CORP009', 'Christopher', 'Lee', 'Garcia', 'Maintenance Technician', 0, '1988-08-31', '369 Walnut Dr, City, State', '2019-12-03', @MaintenanceDeptId, @MaintenancePositionId, 53000.00, 1, 1, 'christopher.garcia@company.com', 1, 90, 1, 74, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP010', 'CORP010', 'Amanda', 'Kay', 'Rodriguez', 'Maintenance Technician', 1, '1993-05-26', '741 Hickory Ln, City, State', '2023-02-14', @MaintenanceDeptId, @MaintenancePositionId, 43000.00, 1, 1, 'amanda.rodriguez@company.com', 1, 90, 1, 65, 100, @TenantId, @CurrentTime, 0);

PRINT 'Employee seeding completed successfully!';
PRINT 'Created:';
PRINT '- 4 Employee Positions';
PRINT '- 10 Employees across 4 departments';

-- Verify the data
SELECT 'Employees Created:' as Result, COUNT(*) as Count FROM Employees WHERE TenantId = @TenantId
UNION ALL
SELECT 'Positions Created:', COUNT(*) FROM EmployeePositions WHERE TenantId = @TenantId;