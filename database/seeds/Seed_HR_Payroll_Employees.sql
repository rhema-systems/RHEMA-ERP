-- HR/payroll employee seed data for local payroll testing.
-- Target database used by the API/EF tooling on this machine:
--   Server: .\SQL2017
--   Database: RhemaERP
--
-- The script is idempotent. It updates existing seed rows by code/employee number
-- and inserts missing rows only.

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @Now datetime2 = SYSUTCDATETIME();

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId)
BEGIN
    THROW 51000, 'Default tenant was not found. Seed aborted.', 1;
END;

DECLARE @Departments table
(
    Id uniqueidentifier NOT NULL,
    Code nvarchar(20) NOT NULL,
    Name nvarchar(100) NOT NULL,
    DepartmentType int NOT NULL,
    Budget decimal(18,2) NULL,
    Color nvarchar(7) NULL,
    Icon nvarchar(50) NULL
);

INSERT INTO @Departments (Id, Code, Name, DepartmentType, Budget, Color, Icon)
VALUES
    ('11111111-1111-4111-8111-111111111111', N'HR', N'Human Resources', 3, 250000.00, N'#2563EB', N'users'),
    ('22222222-2222-4222-8222-222222222222', N'FIN', N'Finance', 4, 350000.00, N'#059669', N'calculator'),
    ('33333333-3333-4333-8333-333333333333', N'IT', N'Information Technology', 5, 300000.00, N'#7C3AED', N'laptop'),
    ('44444444-4444-4444-8444-444444444444', N'OPS', N'Operations', 1, 500000.00, N'#D97706', N'briefcase');

MERGE dbo.Departments AS target
USING @Departments AS source
    ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET
        target.Name = source.Name,
        target.Description = CONCAT(source.Name, N' department seeded for payroll testing.'),
        target.DepartmentType = source.DepartmentType,
        target.Budget = source.Budget,
        target.IsActive = 1,
        target.Color = source.Color,
        target.Icon = source.Icon,
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Code, Description, DepartmentType, Budget, IsActive, Color, Icon, CreatedAt, IsDeleted, TenantId)
    VALUES (source.Id, source.Name, source.Code, CONCAT(source.Name, N' department seeded for payroll testing.'), source.DepartmentType, source.Budget, 1, source.Color, source.Icon, @Now, 0, @TenantId);

DECLARE @HrDepartmentId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Departments WHERE Code = N'HR');
DECLARE @FinanceDepartmentId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Departments WHERE Code = N'FIN');
DECLARE @ItDepartmentId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Departments WHERE Code = N'IT');
DECLARE @OpsDepartmentId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Departments WHERE Code = N'OPS');

DECLARE @Positions table
(
    Id uniqueidentifier NOT NULL,
    Code nvarchar(20) NOT NULL,
    Title nvarchar(100) NOT NULL,
    DepartmentId uniqueidentifier NOT NULL,
    Level int NOT NULL,
    MinSalary decimal(18,2) NULL,
    MaxSalary decimal(18,2) NULL,
    ExpectedHeadcount int NOT NULL
);

INSERT INTO @Positions (Id, Code, Title, DepartmentId, Level, MinSalary, MaxSalary, ExpectedHeadcount)
VALUES
    ('55555555-5555-4555-8555-555555555551', N'HRM', N'HR Manager', @HrDepartmentId, 5, 7000.00, 12000.00, 1),
    ('55555555-5555-4555-8555-555555555552', N'PAYOFF', N'Payroll Officer', @HrDepartmentId, 3, 3500.00, 6500.00, 2),
    ('55555555-5555-4555-8555-555555555553', N'FINMGR', N'Finance Manager', @FinanceDepartmentId, 5, 8000.00, 14000.00, 1),
    ('55555555-5555-4555-8555-555555555554', N'ACCT', N'Accountant', @FinanceDepartmentId, 3, 3500.00, 7000.00, 3),
    ('55555555-5555-4555-8555-555555555555', N'ITSUP', N'IT Support Officer', @ItDepartmentId, 2, 2800.00, 5500.00, 3),
    ('55555555-5555-4555-8555-555555555556', N'OPSSUP', N'Operations Supervisor', @OpsDepartmentId, 3, 3500.00, 6500.00, 4),
    ('55555555-5555-4555-8555-555555555557', N'DRIVER', N'Driver', @OpsDepartmentId, 1, 1800.00, 3500.00, 5);

MERGE dbo.EmployeePositions AS target
USING @Positions AS source
    ON target.TenantId = @TenantId AND target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET
        target.Title = source.Title,
        target.Description = CONCAT(source.Title, N' seeded for payroll testing.'),
        target.DepartmentId = source.DepartmentId,
        target.Level = source.Level,
        target.MinimumExperienceYears = CASE WHEN source.Level >= 5 THEN 5 WHEN source.Level >= 3 THEN 2 ELSE 0 END,
        target.ExpectedHeadcount = source.ExpectedHeadcount,
        target.WorkMode = 1,
        target.MinSalary = source.MinSalary,
        target.MaxSalary = source.MaxSalary,
        target.RequiresCertification = 0,
        target.RequiresGuarantor = CASE WHEN source.Code = N'DRIVER' THEN 1 ELSE 0 END,
        target.NumberOfGuarantors = CASE WHEN source.Code = N'DRIVER' THEN 2 ELSE NULL END,
        target.RequiresLicense = CASE WHEN source.Code = N'DRIVER' THEN 1 ELSE 0 END,
        target.IsActive = 1,
        target.Responsibilities = N'Payroll seed role used for HR/payroll workflow testing.',
        target.Requirements = N'Generated seed role.',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, Title, Code, Description, DepartmentId, Level, MinimumExperienceYears,
        ExpectedHeadcount, WorkMode, MinSalary, MaxSalary, RequiresCertification,
        RequiresGuarantor, NumberOfGuarantors, RequiresLicense, IsActive,
        Responsibilities, Requirements, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.Title, source.Code, CONCAT(source.Title, N' seeded for payroll testing.'),
        source.DepartmentId, source.Level,
        CASE WHEN source.Level >= 5 THEN 5 WHEN source.Level >= 3 THEN 2 ELSE 0 END,
        source.ExpectedHeadcount, 1, source.MinSalary, source.MaxSalary, 0,
        CASE WHEN source.Code = N'DRIVER' THEN 1 ELSE 0 END,
        CASE WHEN source.Code = N'DRIVER' THEN 2 ELSE NULL END,
        CASE WHEN source.Code = N'DRIVER' THEN 1 ELSE 0 END,
        1, N'Payroll seed role used for HR/payroll workflow testing.', N'Generated seed role.',
        @Now, 0, @TenantId
    );

DECLARE @SeedEmployees table
(
    Id uniqueidentifier NOT NULL,
    EmployeeNumber nvarchar(50) NOT NULL,
    CorporateEmployeeID nvarchar(50) NOT NULL,
    FirstName nvarchar(100) NOT NULL,
    MiddleName nvarchar(100) NULL,
    LastName nvarchar(100) NOT NULL,
    Title nvarchar(100) NOT NULL,
    Gender int NOT NULL,
    DateOfBirth date NOT NULL,
    MaritalStatus int NOT NULL,
    EmailAddress nvarchar(200) NOT NULL,
    MobileNumber nvarchar(50) NOT NULL,
    DateEmployed date NOT NULL,
    DepartmentCode nvarchar(20) NOT NULL,
    PositionCode nvarchar(20) NOT NULL,
    Salary decimal(18,2) NOT NULL,
    PayTax bit NOT NULL,
    SSFund bit NOT NULL,
    GrossUp bit NOT NULL,
    Tier2Only bit NOT NULL,
    Overtime bit NOT NULL,
    BadgeNumber nvarchar(50) NOT NULL,
    SsfNumber nvarchar(50) NOT NULL,
    TinNumber nvarchar(50) NOT NULL,
    BankCode nvarchar(20) NOT NULL,
    BranchCode nvarchar(20) NOT NULL,
    AccountNumber nvarchar(50) NOT NULL
);

INSERT INTO @SeedEmployees
(
    Id, EmployeeNumber, CorporateEmployeeID, FirstName, MiddleName, LastName, Title, Gender,
    DateOfBirth, MaritalStatus, EmailAddress, MobileNumber, DateEmployed, DepartmentCode,
    PositionCode, Salary, PayTax, SSFund, GrossUp, Tier2Only, Overtime, BadgeNumber,
    SsfNumber, TinNumber, BankCode, BranchCode, AccountNumber
)
VALUES
    ('66666666-6666-4666-8666-666666666601', N'EMP0001', N'LEG-0001', N'Ama', NULL, N'Mensah', N'HR Manager', 2, '1984-02-12', 2, N'ama.mensah@rhema.local', N'0244000001', '2015-01-05', N'HR', N'HRM', 8500.00, 1, 1, 0, 0, 0, N'PAY001', N'SSNIT-0001', N'TIN-0001', N'03', N'0101', N'3030010001'),
    ('66666666-6666-4666-8666-666666666602', N'EMP0002', N'LEG-0002', N'Kofi', N'Yaw', N'Boateng', N'Payroll Officer', 1, '1990-06-22', 1, N'kofi.boateng@rhema.local', N'0244000002', '2018-03-12', N'HR', N'PAYOFF', 5200.00, 1, 1, 0, 0, 1, N'PAY002', N'SSNIT-0002', N'TIN-0002', N'03', N'0111', N'3030010002'),
    ('66666666-6666-4666-8666-666666666603', N'EMP0003', N'LEG-0003', N'Abena', NULL, N'Osei', N'Accountant', 2, '1992-09-18', 1, N'abena.osei@rhema.local', N'0244000003', '2019-07-01', N'FIN', N'ACCT', 4500.00, 1, 1, 0, 0, 1, N'PAY003', N'SSNIT-0003', N'TIN-0003', N'03', N'0112', N'3030010003'),
    ('66666666-6666-4666-8666-666666666604', N'EMP0004', N'LEG-0004', N'Kwame', NULL, N'Asare', N'IT Support Officer', 1, '1994-11-03', 1, N'kwame.asare@rhema.local', N'0244000004', '2020-02-17', N'IT', N'ITSUP', 3800.00, 1, 1, 0, 0, 1, N'PAY004', N'SSNIT-0004', N'TIN-0004', N'03', N'0121', N'3030010004'),
    ('66666666-6666-4666-8666-666666666605', N'EMP0005', N'LEG-0005', N'Esi', NULL, N'Appiah', N'Operations Supervisor', 2, '1989-05-28', 2, N'esi.appiah@rhema.local', N'0244000005', '2017-10-09', N'OPS', N'OPSSUP', 4200.00, 1, 1, 0, 0, 1, N'PAY005', N'SSNIT-0005', N'TIN-0005', N'03', N'0122', N'3030010005'),
    ('66666666-6666-4666-8666-666666666606', N'EMP0006', N'LEG-0006', N'Yaw', NULL, N'Owusu', N'Driver', 1, '1987-12-15', 2, N'yaw.owusu@rhema.local', N'0244000006', '2021-04-19', N'OPS', N'DRIVER', 2500.00, 1, 1, 0, 0, 1, N'PAY006', N'SSNIT-0006', N'TIN-0006', N'03', N'0135', N'3030010006'),
    ('66666666-6666-4666-8666-666666666607', N'EMP0007', N'LEG-0007', N'Akua', N'Afia', N'Nyarko', N'Finance Manager', 2, '1983-08-10', 2, N'akua.nyarko@rhema.local', N'0244000007', '2014-09-01', N'FIN', N'FINMGR', 9300.00, 1, 1, 0, 0, 0, N'PAY007', N'SSNIT-0007', N'TIN-0007', N'03', N'0136', N'3030010007'),
    ('66666666-6666-4666-8666-666666666608', N'EMP0008', N'LEG-0008', N'Michael', NULL, N'Tetteh', N'Payroll Officer', 1, '1995-01-30', 1, N'michael.tetteh@rhema.local', N'0244000008', '2022-01-10', N'HR', N'PAYOFF', 3900.00, 1, 1, 0, 0, 1, N'PAY008', N'SSNIT-0008', N'TIN-0008', N'03', N'0101', N'3030010008');

MERGE dbo.Employees AS target
USING
(
    SELECT
        e.Id,
        e.EmployeeNumber,
        e.CorporateEmployeeID,
        e.FirstName,
        e.MiddleName,
        e.LastName,
        e.Title,
        e.Gender,
        e.DateOfBirth,
        e.MaritalStatus,
        e.EmailAddress,
        e.MobileNumber,
        e.DateEmployed,
        d.Id AS DepartmentId,
        p.Id AS PositionId,
        e.Salary,
        e.PayTax,
        e.SSFund,
        e.GrossUp,
        e.Tier2Only,
        e.Overtime,
        e.BadgeNumber,
        e.SsfNumber,
        e.TinNumber
    FROM @SeedEmployees e
    INNER JOIN dbo.Departments d ON d.Code = e.DepartmentCode
    INNER JOIN dbo.EmployeePositions p ON p.TenantId = @TenantId AND p.Code = e.PositionCode
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeNumber = source.EmployeeNumber
WHEN MATCHED THEN
    UPDATE SET
        target.CorporateEmployeeID = source.CorporateEmployeeID,
        target.FirstName = source.FirstName,
        target.MiddleName = source.MiddleName,
        target.LastName = source.LastName,
        target.Title = source.Title,
        target.Gender = source.Gender,
        target.DateOfBirth = source.DateOfBirth,
        target.MaritalStatus = source.MaritalStatus,
        target.IsFullTime = 1,
        target.DateEmployed = source.DateEmployed,
        target.Address = N'Accra, Ghana',
        target.City = N'Accra',
        target.State = N'Greater Accra',
        target.EmailAddress = source.EmailAddress,
        target.MobileNumber = source.MobileNumber,
        target.EmploymentType = 1,
        target.ProbationPeriodDays = 90,
        target.ConfirmationDate = DATEADD(day, 90, source.DateEmployed),
        target.DepartmentId = source.DepartmentId,
        target.PositionId = source.PositionId,
        target.StaffStatus = 1,
        target.IsExpatriate = 0,
        target.TaxNumber = source.TinNumber,
        target.SocialSecurityNumber = source.SsfNumber,
        target.TINNumber = source.TinNumber,
        target.IsActive = 1,
        target.Salary = source.Salary,
        target.PayTax = source.PayTax,
        target.SSFund = source.SSFund,
        target.GrossUp = source.GrossUp,
        target.Tier2Only = source.Tier2Only,
        target.Overtime = source.Overtime,
        target.BadgeNumber = source.BadgeNumber,
        target.CurrentWorkload = 0,
        target.MaxWorkload = 100,
        target.Notes = N'Seeded for HR/payroll workflow testing.',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeNumber, CorporateEmployeeID, FirstName, MiddleName, LastName, Title,
        Gender, DateOfBirth, MaritalStatus, IsFullTime, DateEmployed, Address, City,
        State, EmailAddress, MobileNumber, EmploymentType, ProbationPeriodDays,
        ConfirmationDate, DepartmentId, PositionId, StaffStatus, TaxNumber,
        IsExpatriate, SocialSecurityNumber, TINNumber, IsActive, Salary, PayTax, SSFund, GrossUp,
        Tier2Only, Overtime, BadgeNumber, CurrentWorkload, MaxWorkload, Notes,
        CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeNumber, source.CorporateEmployeeID, source.FirstName,
        source.MiddleName, source.LastName, source.Title, source.Gender, source.DateOfBirth,
        source.MaritalStatus, 1, source.DateEmployed, N'Accra, Ghana', N'Accra',
        N'Greater Accra', source.EmailAddress, source.MobileNumber, 1, 90,
        DATEADD(day, 90, source.DateEmployed), source.DepartmentId, source.PositionId,
        1, source.TinNumber, 0, source.SsfNumber, source.TinNumber, 1, source.Salary,
        source.PayTax, source.SSFund, source.GrossUp, source.Tier2Only, source.Overtime,
        source.BadgeNumber, 0, 100, N'Seeded for HR/payroll workflow testing.',
        @Now, 0, @TenantId
    );

MERGE dbo.EmployeeContractDetails AS target
USING
(
    SELECT
        NEWID() AS Id,
        emp.Id AS EmployeeId,
        CONCAT(N'CNT-', emp.EmployeeNumber) AS ContractNumber,
        emp.DateEmployed AS StartDate,
        emp.Salary
    FROM dbo.Employees emp
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = emp.EmployeeNumber
    WHERE emp.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeId = source.EmployeeId AND target.ContractNumber = source.ContractNumber
WHEN MATCHED THEN
    UPDATE SET
        target.WorkSchedule = 1,
        target.StartDate = source.StartDate,
        target.Salary = source.Salary,
        target.CurrencyCode = N'GHS',
        target.EffectiveDate = source.StartDate,
        target.EmploymentType = 1,
        target.PayFrequency = 3,
        target.TaxTreatmentType = 1,
        target.IsPensionApplicable = 1,
        target.IsTaxExempt = 0,
        target.WorkingHoursPerWeek = 40,
        target.AnnualLeaveEntitlementDays = 20,
        target.VacationDaysPerYear = 20,
        target.SickDaysPerYear = 10,
        target.ProbationPeriodDays = 90,
        target.ConfirmationDate = DATEADD(day, 90, source.StartDate),
        target.Terms = N'Seeded active contract for payroll testing.',
        target.IsCurrent = 1,
        target.IsActive = 1,
        target.ContractStatus = 1,
        target.Notes = N'Seeded active contract for payroll testing.',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeId, ContractNumber, WorkSchedule, StartDate, Salary, PayFrequency,
        TaxTreatmentType, IsPensionApplicable, IsTaxExempt, WorkingHoursPerWeek,
        AnnualLeaveEntitlementDays,
        VacationDaysPerYear, SickDaysPerYear, ProbationPeriodDays, ConfirmationDate,
        Terms, IsCurrent, IsActive, ContractStatus, CurrencyCode, EffectiveDate,
        EmploymentType, Notes, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeId, source.ContractNumber, 1, source.StartDate, source.Salary, 3,
        1, 1, 0, 40, 20, 20, 10, 90, DATEADD(day, 90, source.StartDate),
        N'Seeded active contract for payroll testing.', 1, 1, 1, N'GHS',
        source.StartDate, 1, N'Seeded active contract for payroll testing.',
        @Now, 0, @TenantId
    );

MERGE dbo.EmployeeEmergencyContacts AS target
USING
(
    SELECT
        NEWID() AS Id,
        emp.Id AS EmployeeId,
        CASE WHEN seed.Gender = 1 THEN N'Akosua' ELSE N'Kojo' END AS FirstName,
        seed.LastName,
        seed.MobileNumber,
        seed.EmployeeNumber
    FROM dbo.Employees emp
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = emp.EmployeeNumber
    WHERE emp.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeId = source.EmployeeId AND target.IsPrimary = 1
WHEN MATCHED THEN
    UPDATE SET
        target.FirstName = source.FirstName,
        target.LastName = source.LastName,
        target.Relationship = N'Spouse',
        target.ContactType = 1,
        target.PhoneNumber = source.MobileNumber,
        target.Address = N'Accra, Ghana',
        target.City = N'Accra',
        target.IsActive = 1,
        target.Notes = N'Seeded primary emergency contact.',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeId, FirstName, LastName, Relationship, ContactType, PhoneNumber,
        Address, City, IsPrimary, IsActive, Notes, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeId, source.FirstName, source.LastName, N'Spouse', 1,
        source.MobileNumber, N'Accra, Ghana', N'Accra', 1, 1,
        N'Seeded primary emergency contact.', @Now, 0, @TenantId
    );

MERGE dbo.EmployeeDependents AS target
USING
(
    SELECT
        NEWID() AS Id,
        emp.Id AS EmployeeId,
        seed.EmployeeNumber,
        CASE WHEN seed.Gender = 1 THEN N'Adjoa' ELSE N'Kwesi' END AS FirstName,
        seed.LastName
    FROM dbo.Employees emp
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = emp.EmployeeNumber
    WHERE emp.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeId = source.EmployeeId AND target.FirstName = source.FirstName
WHEN MATCHED THEN
    UPDATE SET
        target.LastName = source.LastName,
        target.Relationship = 3,
        target.RelationshipDescription = N'Child',
        target.DateOfBirth = '2016-06-01',
        target.HasDisability = 0,
        target.IsStudentDependent = 1,
        target.IsEmergencyContact = 0,
        target.IsEligibleForBenefits = 1,
        target.IsDeceased = 0,
        target.Notes = N'Seeded dependent for payroll/benefit testing.',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeId, FirstName, LastName, Relationship, RelationshipDescription,
        DateOfBirth, HasDisability, IsStudentDependent, IsEmergencyContact,
        IsEligibleForBenefits, IsDeceased, Notes,
        CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeId, source.FirstName, source.LastName, 3, N'Child',
        '2016-06-01', 0, 1, 0, 1, 0, N'Seeded dependent for payroll/benefit testing.',
        @Now, 0, @TenantId
    );

MERGE dbo.PayrollEmployeeProfiles AS target
USING
(
    SELECT
        '77777777-7777-4777-8777-' + RIGHT(CONVERT(nvarchar(36), seed.Id), 12) AS ProfileIdText,
        emp.Id AS EmployeeId,
        seed.EmployeeNumber,
        seed.CorporateEmployeeID,
        seed.PayTax,
        seed.SSFund,
        seed.GrossUp,
        seed.Tier2Only,
        seed.Overtime,
        seed.SsfNumber,
        seed.TinNumber
    FROM dbo.Employees emp
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = emp.EmployeeNumber
    WHERE emp.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeId = source.EmployeeId
WHEN MATCHED THEN
    UPDATE SET
        target.EmployeeNumber = source.EmployeeNumber,
        target.LegacyEmployeeId = source.CorporateEmployeeID,
        target.LegacyEmployeeNumber = source.CorporateEmployeeID,
        target.PayrollActive = 1,
        target.PayTax = source.PayTax,
        target.SsfApplicable = source.SSFund,
        target.GrossUp = source.GrossUp,
        target.Tier2Only = source.Tier2Only,
        target.OvertimeEligible = source.Overtime,
        target.SsfNumber = source.SsfNumber,
        target.TinNumber = source.TinNumber,
        target.CurrencyCode = N'GHS',
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeId, EmployeeNumber, LegacyEmployeeId, LegacyEmployeeNumber,
        PayrollActive, PayTax, SsfApplicable, GrossUp, Tier2Only, OvertimeEligible,
        SsfNumber, TinNumber, CurrencyCode, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        CONVERT(uniqueidentifier, source.ProfileIdText), source.EmployeeId, source.EmployeeNumber,
        source.CorporateEmployeeID, source.CorporateEmployeeID, 1, source.PayTax,
        source.SSFund, source.GrossUp, source.Tier2Only, source.Overtime,
        source.SsfNumber, source.TinNumber, N'GHS', @Now, 0, @TenantId
    );

MERGE dbo.PayrollSalaryBases AS target
USING
(
    SELECT
        NEWID() AS Id,
        profile.Id AS EmployeeProfileId,
        seed.Salary AS MonthlyBasicSalary,
        seed.Salary * 12 AS AnnualBasicSalary,
        CAST(ROUND(seed.Salary / 173.3333, 2) AS decimal(18,2)) AS HourlyRate,
        seed.DateEmployed AS EffectiveFrom
    FROM dbo.PayrollEmployeeProfiles profile
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = profile.EmployeeNumber
    WHERE profile.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeProfileId = source.EmployeeProfileId
WHEN MATCHED THEN
    UPDATE SET
        target.MonthlyBasicSalary = source.MonthlyBasicSalary,
        target.AnnualBasicSalary = source.AnnualBasicSalary,
        target.HourlyRate = source.HourlyRate,
        target.CurrencyCode = N'GHS',
        target.EffectiveFrom = source.EffectiveFrom,
        target.EffectiveTo = NULL,
        target.IsActive = 1,
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeProfileId, MonthlyBasicSalary, AnnualBasicSalary, HourlyRate,
        CurrencyCode, EffectiveFrom, EffectiveTo, IsActive, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeProfileId, source.MonthlyBasicSalary,
        source.AnnualBasicSalary, source.HourlyRate, N'GHS', source.EffectiveFrom,
        NULL, 1, @Now, 0, @TenantId
    );

MERGE dbo.PayrollPaymentMethods AS target
USING
(
    SELECT
        NEWID() AS Id,
        profile.Id AS EmployeeProfileId,
        seed.BankCode,
        seed.BranchCode,
        seed.AccountNumber,
        seed.DateEmployed AS StartDate
    FROM dbo.PayrollEmployeeProfiles profile
    INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = profile.EmployeeNumber
    WHERE profile.TenantId = @TenantId
) AS source
    ON target.TenantId = @TenantId AND target.EmployeeProfileId = source.EmployeeProfileId AND target.SequenceNo = 1
WHEN MATCHED THEN
    UPDATE SET
        target.PaymentType = N'Bank',
        target.PaymentPercent = 100.00,
        target.Amount = NULL,
        target.BankCode = source.BankCode,
        target.BankBranchCode = source.BranchCode,
        target.AccountNumber = source.AccountNumber,
        target.CurrencyCode = N'GHS',
        target.StartDate = source.StartDate,
        target.EndDate = NULL,
        target.IsActive = 1,
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, EmployeeProfileId, PaymentType, PaymentPercent, Amount, BankCode,
        BankBranchCode, AccountNumber, CurrencyCode, SequenceNo, StartDate, EndDate,
        IsActive, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        source.Id, source.EmployeeProfileId, N'Bank', 100.00, NULL, source.BankCode,
        source.BranchCode, source.AccountNumber, N'GHS', 1, source.StartDate, NULL,
        1, @Now, 0, @TenantId
    );

UPDATE profile
SET
    DefaultPaymentMethodId = method.Id,
    UpdatedAt = @Now
FROM dbo.PayrollEmployeeProfiles profile
INNER JOIN dbo.PayrollPaymentMethods method
    ON method.TenantId = profile.TenantId
    AND method.EmployeeProfileId = profile.Id
    AND method.SequenceNo = 1
INNER JOIN @SeedEmployees seed ON seed.EmployeeNumber = profile.EmployeeNumber
WHERE profile.TenantId = @TenantId;

DECLARE @CurrencyCodeTypeId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.PayrollCodeTypes WHERE TenantId = @TenantId AND CodeType = N'CUR');
IF @CurrencyCodeTypeId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.PayrollCodeValues WHERE TenantId = @TenantId AND CodeType = N'CUR' AND ActualCode = N'GHS')
    BEGIN
        INSERT INTO dbo.PayrollCodeValues
        (
            Id, PayrollCodeTypeId, CodeType, ActualCode, Description, Blocked,
            CreatedAt, IsDeleted, TenantId
        )
        VALUES
        (
            '88888888-8888-4888-8888-888888888801', @CurrencyCodeTypeId, N'CUR',
            N'GHS', N'Ghana Cedi', 0, @Now, 0, @TenantId
        );
    END;
END;

DECLARE @Grades table
(
    GradeName nvarchar(60) NOT NULL,
    ReportingName nvarchar(100) NOT NULL,
    OrderField int NOT NULL,
    MonthlyValue decimal(18,2) NOT NULL
);

INSERT INTO @Grades (GradeName, ReportingName, OrderField, MonthlyValue)
VALUES
    (N'GRADE-1', N'GRADE-1', 1, 1800.00),
    (N'GRADE-2', N'GRADE-2', 2, 2200.00),
    (N'GRADE-3', N'GRADE-3', 3, 2800.00),
    (N'GRADE-4', N'GRADE-4', 4, 3500.00),
    (N'GRADE-5', N'GRADE-5', 5, 4200.00),
    (N'GRADE-6', N'GRADE-6', 6, 5200.00),
    (N'GRADE-7', N'GRADE-7', 7, 6500.00),
    (N'GRADE-8', N'GRADE-8', 8, 7800.00),
    (N'GRADE-9', N'GRADE-9', 9, 9300.00),
    (N'GRADE-10', N'GRADE-10', 10, 11000.00),
    (N'GRADE-11', N'ASSISTANT MGR', 11, 13000.00),
    (N'GRADE-12', N'MANAGER', 12, 15500.00);

MERGE dbo.PayrollGrades AS target
USING @Grades AS source
    ON target.TenantId = @TenantId
    AND target.GradeName = source.GradeName
    AND target.CurrencyCode = N'GHS'
    AND target.LegacyCompanyCode IS NULL
WHEN MATCHED THEN
    UPDATE SET
        target.GradeType = N'GRADED',
        target.ReportingName = source.ReportingName,
        target.MinValue = source.MonthlyValue,
        target.MaxValue = source.MonthlyValue * 1.5,
        target.MidPoint = source.MonthlyValue * 1.25,
        target.OrderField = source.OrderField,
        target.EnforceNotchConsistency = 1,
        target.IsActive = 1,
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, GradeId, GradeType, GradeName, SystemGradeName, MinValue, MaxValue,
        MidPoint, CurrencyCode, OrderField, ReportingName, EnforceNotchConsistency,
        IsActive, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), CONCAT(N'G', RIGHT(CONCAT(N'000', source.OrderField), 3)),
        N'GRADED', source.GradeName, source.GradeName, source.MonthlyValue,
        source.MonthlyValue * 1.5, source.MonthlyValue * 1.25, N'GHS',
        source.OrderField, source.ReportingName, 1, 1, @Now, 0, @TenantId
    );

;WITH NotchSeed AS
(
    SELECT
        grade.Id AS PayrollGradeId,
        grade.GradeId,
        grade.GradeName,
        grade.ReportingName,
        grade.OrderField,
        notch.Notch,
        CAST(grade.MinValue + ((CONVERT(int, notch.Notch) - 1) * ((grade.MaxValue - grade.MinValue) / 9.0)) AS decimal(18,2)) AS Value
    FROM dbo.PayrollGrades grade
    CROSS JOIN (VALUES (N'1'), (N'5'), (N'10')) notch(Notch)
    WHERE grade.TenantId = @TenantId
      AND grade.CurrencyCode = N'GHS'
      AND grade.GradeName LIKE N'GRADE-%'
)
MERGE dbo.PayrollGradeNotches AS target
USING NotchSeed AS source
    ON target.TenantId = @TenantId
    AND target.PayrollGradeId = source.PayrollGradeId
    AND target.Notch = source.Notch
    AND target.CurrencyCode = N'GHS'
    AND target.LegacyCompanyCode IS NULL
WHEN MATCHED THEN
    UPDATE SET
        target.GradeId = source.GradeId,
        target.SystemGradeName = source.GradeName,
        target.Value = source.Value,
        target.OrderField = source.OrderField,
        target.AnnualisedValue = source.Value * 12,
        target.GradeName = source.GradeName,
        target.ReportingName = source.ReportingName,
        target.HourlyRate = CAST(ROUND(source.Value / 173.3333, 2) AS decimal(18,2)),
        target.UpdatedAt = @Now,
        target.IsDeleted = 0,
        target.DeletedAt = NULL,
        target.DeletedBy = NULL
WHEN NOT MATCHED THEN
    INSERT
    (
        Id, PayrollGradeId, GradeId, SystemGradeName, Notch, Value, CurrencyCode,
        OrderField, AnnualisedValue, GradeName, ReportingName, HourlyRate,
        CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), source.PayrollGradeId, source.GradeId, source.GradeName,
        source.Notch, source.Value, N'GHS', source.OrderField, source.Value * 12,
        source.GradeName, source.ReportingName,
        CAST(ROUND(source.Value / 173.3333, 2) AS decimal(18,2)),
        @Now, 0, @TenantId
    );

COMMIT TRANSACTION;

SELECT 'Departments' AS SeedArea, COUNT(*) AS [RowCount]
FROM dbo.Departments
WHERE TenantId = @TenantId AND Code IN (N'HR', N'FIN', N'IT', N'OPS')
UNION ALL
SELECT 'Employee Positions', COUNT(*)
FROM dbo.EmployeePositions
WHERE TenantId = @TenantId AND Code IN (N'HRM', N'PAYOFF', N'FINMGR', N'ACCT', N'ITSUP', N'OPSSUP', N'DRIVER')
UNION ALL
SELECT 'Employees', COUNT(*)
FROM dbo.Employees
WHERE TenantId = @TenantId AND EmployeeNumber BETWEEN N'EMP0001' AND N'EMP0008'
UNION ALL
SELECT 'Payroll Employee Profiles', COUNT(*)
FROM dbo.PayrollEmployeeProfiles
WHERE TenantId = @TenantId AND EmployeeNumber BETWEEN N'EMP0001' AND N'EMP0008'
UNION ALL
SELECT 'Payroll Salary Bases', COUNT(*)
FROM dbo.PayrollSalaryBases salary
INNER JOIN dbo.PayrollEmployeeProfiles profile ON profile.Id = salary.EmployeeProfileId
WHERE salary.TenantId = @TenantId AND profile.EmployeeNumber BETWEEN N'EMP0001' AND N'EMP0008'
UNION ALL
SELECT 'Payroll Payment Methods', COUNT(*)
FROM dbo.PayrollPaymentMethods method
INNER JOIN dbo.PayrollEmployeeProfiles profile ON profile.Id = method.EmployeeProfileId
WHERE method.TenantId = @TenantId AND profile.EmployeeNumber BETWEEN N'EMP0001' AND N'EMP0008'
UNION ALL
SELECT 'Payroll Grades', COUNT(*)
FROM dbo.PayrollGrades
WHERE TenantId = @TenantId AND GradeName LIKE N'GRADE-%'
UNION ALL
SELECT 'Payroll Grade Notches', COUNT(*)
FROM dbo.PayrollGradeNotches
WHERE TenantId = @TenantId;

SELECT
    emp.EmployeeNumber,
    CONCAT(emp.FirstName, N' ', emp.LastName) AS EmployeeName,
    dept.Code AS DepartmentCode,
    pos.Code AS PositionCode,
    salary.MonthlyBasicSalary,
    profile.PayTax,
    profile.SsfApplicable,
    profile.OvertimeEligible,
    method.BankCode,
    method.BankBranchCode
FROM dbo.Employees emp
INNER JOIN dbo.Departments dept ON dept.Id = emp.DepartmentId
INNER JOIN dbo.EmployeePositions pos ON pos.Id = emp.PositionId
INNER JOIN dbo.PayrollEmployeeProfiles profile ON profile.EmployeeId = emp.Id
INNER JOIN dbo.PayrollSalaryBases salary ON salary.EmployeeProfileId = profile.Id
LEFT JOIN dbo.PayrollPaymentMethods method ON method.Id = profile.DefaultPaymentMethodId
WHERE emp.TenantId = @TenantId
  AND emp.EmployeeNumber BETWEEN N'EMP0001' AND N'EMP0008'
ORDER BY emp.EmployeeNumber;
