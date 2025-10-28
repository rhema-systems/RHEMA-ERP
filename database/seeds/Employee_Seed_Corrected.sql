-- Employee Seeding Script - Corrected Version
-- This script creates positions, employees, technical skills, and skill assignments

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @CurrentTime DATETIME2 = GETUTCDATE();

-- Use existing department IDs
DECLARE @MaintenanceDeptId UNIQUEIDENTIFIER = 'B092E0ED-242F-4A59-85A0-1984121CF671';
DECLARE @SafetyDeptId UNIQUEIDENTIFIER = '406C7DB9-6738-428B-A932-2623B8CE13C4';
DECLARE @EngineeringDeptId UNIQUEIDENTIFIER = '3FD9D9FA-2D6A-4CCF-92DB-33606DF51AD6';
DECLARE @FacilitiesDeptId UNIQUEIDENTIFIER = 'B06704F0-65DB-49F6-BEED-E384FBF0A1CF';

-- First, insert Employee Positions
INSERT INTO EmployeePositions (Id, Title, Description, Code, Level, DepartmentId, RequiresCertification, IsActive, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Senior Maintenance Technician', 'Experienced maintenance professional with leadership responsibilities', 'SR-MAINT-TECH', 3, @MaintenanceDeptId, 1, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Maintenance Technician', 'General maintenance and repair specialist', 'MAINT-TECH', 2, @MaintenanceDeptId, 0, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Junior Maintenance Technician', 'Entry-level maintenance technician', 'JR-MAINT-TECH', 1, @MaintenanceDeptId, 0, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Maintenance Supervisor', 'Supervises maintenance operations and staff', 'MAINT-SUPER', 4, @MaintenanceDeptId, 1, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Safety Inspector', 'Conducts safety inspections and audits', 'SAFETY-INSP', 3, @SafetyDeptId, 1, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Quality Control Inspector', 'Performs quality control inspections', 'QC-INSP', 2, @SafetyDeptId, 1, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Facilities Coordinator', 'Coordinates facilities maintenance and operations', 'FACIL-COORD', 3, @FacilitiesDeptId, 0, 1, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Engineering Technician', 'Provides technical support and engineering assistance', 'ENG-TECH', 2, @EngineeringDeptId, 0, 1, @TenantId, @CurrentTime, 0);

-- Get Position IDs for reference
DECLARE @SrMaintenanceTechId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'SR-MAINT-TECH');
DECLARE @MaintenanceTechId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'MAINT-TECH');
DECLARE @JrMaintenanceTechId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'JR-MAINT-TECH');
DECLARE @MaintenanceSupervisorId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'MAINT-SUPER');
DECLARE @SafetyInspectorId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'SAFETY-INSP');
DECLARE @QCInspectorId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'QC-INSP');
DECLARE @FacilitiesCoordId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'FACIL-COORD');
DECLARE @EngineeringTechId UNIQUEIDENTIFIER = (SELECT Id FROM EmployeePositions WHERE Code = 'ENG-TECH');

-- Insert Employees
INSERT INTO Employees (Id, EmployeeNumber, CorporateEmployeeID, FirstName, MiddleName, LastName, Title, Gender, DateOfBirth, Address, DateEmployed, DepartmentId, PositionId, Salary, IsActive, IsFullTime, EmailAddress, ContractType, ProbationPeriodDays, StaffStatus, CurrentWorkload, MaxWorkload, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'EMP001', 'CORP001', 'John', 'Michael', 'Smith', 'Senior Maintenance Technician', 0, '1985-03-15', '123 Main St, City, State', '2020-01-15', @MaintenanceDeptId, @SrMaintenanceTechId, 65000.00, 1, 1, 'john.smith@company.com', 'Permanent', 90, 'Active', 80, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP002', 'CORP002', 'Sarah', 'Jane', 'Johnson', 'Maintenance Technician', 1, '1990-07-22', '456 Oak Ave, City, State', '2021-03-10', @MaintenanceDeptId, @MaintenanceTechId, 52000.00, 1, 1, 'sarah.johnson@company.com', 'Permanent', 90, 'Active', 75, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP003', 'CORP003', 'Robert', 'William', 'Davis', 'Maintenance Supervisor', 0, '1978-11-08', '789 Pine Dr, City, State', '2018-05-01', @MaintenanceDeptId, @MaintenanceSupervisorId, 75000.00, 1, 1, 'robert.davis@company.com', 'Permanent', 90, 'Active', 85, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP004', 'CORP004', 'Emily', 'Rose', 'Wilson', 'Quality Control Inspector', 1, '1987-09-14', '321 Elm St, City, State', '2019-08-20', @SafetyDeptId, @QCInspectorId, 58000.00, 1, 1, 'emily.wilson@company.com', 'Permanent', 90, 'Active', 70, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP005', 'CORP005', 'Michael', 'James', 'Brown', 'Junior Maintenance Technician', 0, '1995-02-28', '654 Maple Ave, City, State', '2022-09-05', @MaintenanceDeptId, @JrMaintenanceTechId, 42000.00, 1, 1, 'michael.brown@company.com', 'Permanent', 90, 'Active', 60, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP006', 'CORP006', 'Lisa', 'Marie', 'Anderson', 'Safety Inspector', 1, '1982-06-17', '987 Cedar Ln, City, State', '2017-11-12', @SafetyDeptId, @SafetyInspectorId, 62000.00, 1, 1, 'lisa.anderson@company.com', 'Permanent', 90, 'Active', 78, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP007', 'CORP007', 'David', 'Paul', 'Taylor', 'Facilities Coordinator', 0, '1986-04-03', '147 Birch St, City, State', '2020-07-18', @FacilitiesDeptId, @FacilitiesCoordId, 55000.00, 1, 1, 'david.taylor@company.com', 'Permanent', 90, 'Active', 72, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP008', 'CORP008', 'Jennifer', 'Nicole', 'Martinez', 'Engineering Technician', 1, '1991-12-09', '258 Spruce Ave, City, State', '2021-10-25', @EngineeringDeptId, @EngineeringTechId, 60000.00, 1, 1, 'jennifer.martinez@company.com', 'Permanent', 90, 'Active', 76, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP009', 'CORP009', 'Christopher', 'Lee', 'Garcia', 'Maintenance Technician', 0, '1988-08-31', '369 Walnut Dr, City, State', '2019-12-03', @MaintenanceDeptId, @MaintenanceTechId, 53000.00, 1, 1, 'christopher.garcia@company.com', 'Permanent', 90, 'Active', 74, 100, @TenantId, @CurrentTime, 0),
    (NEWID(), 'EMP010', 'CORP010', 'Amanda', 'Kay', 'Rodriguez', 'Junior Maintenance Technician', 1, '1993-05-26', '741 Hickory Ln, City, State', '2023-02-14', @MaintenanceDeptId, @JrMaintenanceTechId, 43000.00, 1, 1, 'amanda.rodriguez@company.com', 'Permanent', 90, 'Active', 65, 100, @TenantId, @CurrentTime, 0);

-- Insert Technical Skills
INSERT INTO TechnicalSkills (Id, Name, Code, Description, Category, SkillLevel, Complexity, RiskLevel, Prerequisites, Certifications, EstimatedLearningHours, ToolsRequired, SafetyRequirements, CompetencyAreas, RelatedMaintenanceTypes, IsActive, IsFromHRModule, TenantId, CreatedAt, IsDeleted)
VALUES 
    (NEWID(), 'Electrical Systems', 'ELEC-SYS', 'Installation, maintenance, and repair of electrical systems', 'Electrical', 'Advanced', 'High', 'Medium', 'Basic electrical knowledge', 'Electrician License', 120, 'Multimeter, Wire strippers, Electrical tools', 'LOTO procedures, PPE required', 'Electrical troubleshooting, Circuit analysis', 'Electrical, Preventive', 1, 0, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Hydraulic Systems', 'HYD-SYS', 'Maintenance and repair of hydraulic equipment', 'Mechanical', 'Intermediate', 'Medium', 'Medium', 'Basic mechanical knowledge', 'Hydraulics Certificate', 80, 'Hydraulic tools, Pressure gauges', 'Pressure safety, PPE required', 'Hydraulic troubleshooting, Fluid systems', 'Mechanical, Predictive', 1, 0, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Welding', 'WELD', 'Various welding techniques and applications', 'Fabrication', 'Intermediate', 'Medium', 'High', 'Basic metalworking', 'Welding Certification', 100, 'Welding equipment, Safety gear', 'Fire safety, Ventilation, PPE', 'Arc welding, Gas welding, Safety', 'Corrective, Emergency', 1, 0, @TenantId, @CurrentTime, 0),
    (NEWID(), 'HVAC Systems', 'HVAC', 'Heating, ventilation, and air conditioning systems', 'HVAC', 'Advanced', 'High', 'Low', 'Basic HVAC knowledge', 'HVAC Technician License', 150, 'HVAC tools, Refrigerant recovery', 'Refrigerant handling, PPE', 'System diagnostics, Refrigeration', 'Preventive, Seasonal', 1, 0, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Pump Maintenance', 'PUMP-MAINT', 'Maintenance of various pump systems', 'Mechanical', 'Intermediate', 'Medium', 'Medium', 'Basic mechanical knowledge', 'Pump Maintenance Certificate', 60, 'Mechanical tools, Alignment tools', 'Machinery safety, PPE', 'Pump troubleshooting, Alignment', 'Preventive, Predictive', 1, 0, @TenantId, @CurrentTime, 0),
    (NEWID(), 'Safety Inspection', 'SAFETY-INSP', 'Conducting comprehensive safety inspections', 'Safety', 'Advanced', 'Medium', 'Low', 'Safety training', 'Safety Inspector Certification', 40, 'Inspection tools, Measuring devices', 'All safety protocols', 'Risk assessment, Compliance', 'Safety, Inspection', 1, 0, @TenantId, @CurrentTime, 0);

-- Get Employee and Skill IDs for skill assignments
DECLARE @JohnSmithId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP001');
DECLARE @SarahJohnsonId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP002');
DECLARE @RobertDavisId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP003');
DECLARE @EmilyWilsonId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP004');
DECLARE @MichaelBrownId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP005');
DECLARE @LisaAndersonId UNIQUEIDENTIFIER = (SELECT Id FROM Employees WHERE EmployeeNumber = 'EMP006');

DECLARE @ElectricalSkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'ELEC-SYS');
DECLARE @HydraulicSkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'HYD-SYS');
DECLARE @WeldingSkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'WELD');
DECLARE @HVACSkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'HVAC');
DECLARE @PumpSkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'PUMP-MAINT');
DECLARE @SafetySkillId UNIQUEIDENTIFIER = (SELECT Id FROM TechnicalSkills WHERE Code = 'SAFETY-INSP');

-- Insert Technician Skill Assignments
INSERT INTO TechnicianSkillAssignments (Id, TechnicianId, SkillId, ProficiencyLevel, ProficiencyDescription, AcquiredDate, IsVerified, VerifiedBy, Notes, TenantId, CreatedAt, IsDeleted)
VALUES 
    -- John Smith (Senior Technician) - Multiple skills
    (NEWID(), @JohnSmithId, @ElectricalSkillId, 4, 'Expert', '2020-06-01', 1, 'System Administrator', 'Certified electrician with 5+ years experience', @TenantId, @CurrentTime, 0),
    (NEWID(), @JohnSmithId, @HydraulicSkillId, 3, 'Advanced', '2021-01-15', 1, 'System Administrator', 'Hydraulics certification completed', @TenantId, @CurrentTime, 0),
    (NEWID(), @JohnSmithId, @PumpSkillId, 4, 'Expert', '2020-08-10', 1, 'System Administrator', 'Pump specialist certification', @TenantId, @CurrentTime, 0),
    
    -- Sarah Johnson (Technician) - Electrical and HVAC
    (NEWID(), @SarahJohnsonId, @ElectricalSkillId, 3, 'Advanced', '2021-09-01', 1, 'System Administrator', 'Electrical systems training completed', @TenantId, @CurrentTime, 0),
    (NEWID(), @SarahJohnsonId, @HVACSkillId, 3, 'Advanced', '2022-03-15', 1, 'System Administrator', 'HVAC technician licensed', @TenantId, @CurrentTime, 0),
    
    -- Robert Davis (Supervisor) - Leadership and multiple technical skills
    (NEWID(), @RobertDavisId, @ElectricalSkillId, 4, 'Expert', '2018-08-01', 1, 'System Administrator', 'Master electrician with management experience', @TenantId, @CurrentTime, 0),
    (NEWID(), @RobertDavisId, @SafetySkillId, 4, 'Expert', '2018-10-01', 1, 'System Administrator', 'Certified safety inspector and trainer', @TenantId, @CurrentTime, 0),
    (NEWID(), @RobertDavisId, @HVACSkillId, 3, 'Advanced', '2019-02-01', 1, 'System Administrator', 'HVAC systems expertise', @TenantId, @CurrentTime, 0),
    
    -- Emily Wilson (QC Inspector) - Safety and inspection skills
    (NEWID(), @EmilyWilsonId, @SafetySkillId, 4, 'Expert', '2019-12-01', 1, 'System Administrator', 'Quality control specialist with safety focus', @TenantId, @CurrentTime, 0),
    
    -- Michael Brown (Junior Tech) - Basic skills
    (NEWID(), @MichaelBrownId, @HydraulicSkillId, 2, 'Intermediate', '2023-01-15', 0, '', 'Currently in training program', @TenantId, @CurrentTime, 0),
    (NEWID(), @MichaelBrownId, @WeldingSkillId, 2, 'Intermediate', '2023-03-01', 0, '', 'Basic welding certification in progress', @TenantId, @CurrentTime, 0),
    
    -- Lisa Anderson (Safety Inspector) - Safety expertise
    (NEWID(), @LisaAndersonId, @SafetySkillId, 4, 'Expert', '2017-12-01', 1, 'System Administrator', 'Senior safety inspector with extensive experience', @TenantId, @CurrentTime, 0);

PRINT 'Employee seeding completed successfully!';
PRINT 'Created:';
PRINT '- 8 Employee Positions';
PRINT '- 10 Employees across 4 departments';
PRINT '- 6 Technical Skills';
PRINT '- 12 Technician Skill Assignments';

-- Verify the data
SELECT 'Employees Created:' as Result, COUNT(*) as Count FROM Employees WHERE TenantId = @TenantId
UNION ALL
SELECT 'Positions Created:', COUNT(*) FROM EmployeePositions WHERE TenantId = @TenantId
UNION ALL
SELECT 'Skills Created:', COUNT(*) FROM TechnicalSkills WHERE TenantId = @TenantId
UNION ALL
SELECT 'Skill Assignments Created:', COUNT(*) FROM TechnicianSkillAssignments WHERE TenantId = @TenantId;