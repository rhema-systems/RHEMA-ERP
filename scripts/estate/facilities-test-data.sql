SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

IF DB_NAME() <> N'RhemaERP'
    THROW 59000, 'Facilities fixtures require the local RhemaERP test database.', 1;

DECLARE @tenant uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @provider uniqueidentifier = 'fa510001-0000-4000-8000-000000000001';
DECLARE @supplier uniqueidentifier = 'fa510001-0000-4000-8000-000000000002';
DECLARE @link uniqueidentifier = 'fa510001-0000-4000-8000-000000000003';
DECLARE @rateCleaning uniqueidentifier = 'fa510001-0000-4000-8000-000000000004';
DECLARE @rateMaintenance uniqueidentifier = 'fa510001-0000-4000-8000-000000000005';
DECLARE @invoice uniqueidentifier = 'fa510001-0000-4000-8000-000000000006';
DECLARE @invoiceLine uniqueidentifier = 'fa510001-0000-4000-8000-000000000007';
DECLARE @dimensionValue uniqueidentifier = 'fa510001-0000-4000-8000-000000000008';
DECLARE @dimensionSet uniqueidentifier = 'fa510001-0000-4000-8000-000000000009';
DECLARE @dimensionItem uniqueidentifier = 'fa510001-0000-4000-8000-000000000010';
DECLARE @scenario uniqueidentifier = 'fa510001-0000-4000-8000-000000000011';
DECLARE @budgetReturn uniqueidentifier = 'fa510001-0000-4000-8000-000000000012';
DECLARE @controlDimension uniqueidentifier = 'fa510001-0000-4000-8000-000000000013';
DECLARE @attendance uniqueidentifier = 'fa510001-0000-4000-8000-000000000014';
DECLARE @now datetime2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @tenant)
    THROW 59001, 'Expected local test tenant was not found.', 1;
IF EXISTS (SELECT 1 FROM BusinessPartners WHERE PartnerCode = N'FAC-TEST-CLEAN-001' AND Id <> @provider)
    THROW 59002, 'Facilities test provider code is already used by another record.', 1;
IF EXISTS (SELECT 1 FROM BudgetScenarios WHERE FiscalYearId IN
    (SELECT Id FROM FiscalYears WHERE TenantId = @tenant AND FiscalYearCode = N'FY2025')
    AND IsActive = 1 AND Id <> @scenario AND IsDeleted = 0)
    THROW 59003, 'An official FY2025 budget already exists; refusing to replace it.', 1;

IF NOT EXISTS (SELECT 1 FROM BusinessPartners WHERE Id = @provider)
    INSERT INTO BusinessPartners
        (Id, PartnerCode, PartnerName, PartnerType, RegistrationStatus, ApprovalStatus,
         IsPreferred, IsActive, IsBlacklisted, IsTaxExempt, IsOnCreditHold,
         PrimaryEmail, ComplianceValidUntilUtc, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@provider, N'FAC-TEST-CLEAN-001', N'TEST Facilities Cleaning Services', N'Supplier',
         N'Approved', N'Approved', 0, 1, 0, 0, 0,
         N'facilities-test@example.invalid', '2028-12-31', @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM Suppliers WHERE Id = @supplier)
    INSERT INTO Suppliers
        (Id, SupplierCode, Name, SupplierType, LeadTimeDays, IsActive, IsPreferred,
         Status, IsBlacklisted, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@supplier, N'FAC-TEST-AP-001', N'TEST Facilities Cleaning Services', N'Vendor',
         7, 1, 0, N'Active', 0, @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM ApSupplierIdentityLinks WHERE Id = @link)
    INSERT INTO ApSupplierIdentityLinks
        (Id, BusinessPartnerId, SupplierId, MappingSource, IsVerified, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@link, @provider, @supplier, N'VerifiedManual', 1, @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM EstateFacilityProviderRates WHERE Id = @rateCleaning)
    INSERT INTO EstateFacilityProviderRates
        (Id, BusinessPartnerId, ServiceName, UnitOfMeasure, Rate, Currency,
         EffectiveFrom, EffectiveTo, IsActive, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@rateCleaning, @provider, N'Common-area cleaning', N'visit', 240.0000, N'GHS',
         '2026-09-01', '2027-08-31', 1, @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM EstateFacilityProviderRates WHERE Id = @rateMaintenance)
    INSERT INTO EstateFacilityProviderRates
        (Id, BusinessPartnerId, ServiceName, UnitOfMeasure, Rate, Currency,
         EffectiveFrom, EffectiveTo, IsActive, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@rateMaintenance, @provider, N'Periodic deep cleaning', N'visit', 480.0000, N'GHS',
         '2026-09-01', '2027-08-31', 1, @now, 0, @tenant);

-- Draft direct AP invoice only: no posting, approval, receipt or payment is implied.
IF NOT EXISTS (SELECT 1 FROM VendorInvoice WHERE Id = @invoice)
    INSERT INTO VendorInvoice
        (Id, InvoiceNumber, SupplierInvoiceNumber, SupplierId, SupplierName, InvoiceDate,
         DueDate, SubTotal, TaxAmount, DiscountAmount, TotalAmount, PaidAmount,
         CurrencyCode, ExchangeRate, BaseCurrencyAmount, PaymentTermsDays,
         EarlyPaymentDiscountPercentage, EarlyPaymentDiscountAmount, WithholdingTaxRate,
         WithholdingTaxAmount, MatchingType, MatchingStatus, Status, ApprovalStatus,
         ApprovalRequired, IsOpeningBalance, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@invoice, N'FAC-TEST-AP-001', N'TEST-CLEAN-SEP-2026', @supplier,
         N'TEST Facilities Cleaning Services', '2026-09-25', '2026-10-25',
         480.00, 0, 0, 480.00, 0, N'GHS', 1, 480.00, 30,
         0, 0, 0, 0, 0, 0, 1, N'Draft', 1, 0, @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM VendorInvoiceLineItem WHERE Id = @invoiceLine)
    INSERT INTO VendorInvoiceLineItem
        (Id, VendorInvoiceId, LineItemType, Description, Quantity, UnitPrice,
         TaxRate, TaxAmount, DiscountPercentage, DiscountAmount, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@invoiceLine, @invoice, N'Service', N'TEST: two common-area cleaning visits',
         2, 240.00, 0, 0, 0, 0, @now, 0, @tenant);

DECLARE @fiscalYear uniqueidentifier =
    (SELECT Id FROM FiscalYears WHERE TenantId = @tenant AND FiscalYearCode = N'FY2025' AND IsDeleted = 0);
DECLARE @department uniqueidentifier =
    (SELECT Id FROM FinanceDimensionDefinitions WHERE TenantId = @tenant AND Code = N'DEPARTMENT' AND IsDeleted = 0);
DECLARE @utilities uniqueidentifier =
    (SELECT Id FROM Accounts WHERE TenantId = @tenant AND AccountCode = N'6200' AND IsDeleted = 0);
DECLARE @services uniqueidentifier =
    (SELECT Id FROM Accounts WHERE TenantId = @tenant AND AccountCode = N'6500' AND IsDeleted = 0);

IF @fiscalYear IS NULL OR @department IS NULL OR @utilities IS NULL OR @services IS NULL
    THROW 59004, 'FY2025 or Finance dimension/account prerequisites are missing.', 1;
IF (SELECT COUNT(*) FROM FiscalPeriods WHERE TenantId = @tenant AND FiscalYearId = @fiscalYear AND IsDeleted = 0) <> 12
    THROW 59005, 'FY2025 must have all twelve fiscal periods.', 1;

-- Historical test-only baseline: FY2025 avoids changing current-year posting controls.
IF NOT EXISTS (SELECT 1 FROM FinanceDimensionValues WHERE Id = @dimensionValue)
    INSERT INTO FinanceDimensionValues
        (Id, FinanceDimensionDefinitionId, Code, Name, EffectiveDate, IsActive,
         DisplayOrder, TenantId, CreatedAt, IsDeleted)
    VALUES
        (@dimensionValue, @department, N'FACILITIES_TEST', N'Facilities Test Department',
         '2025-01-01', 1, 900, @tenant, @now, 0);

IF NOT EXISTS (SELECT 1 FROM FinanceDimensionSets WHERE Id = @dimensionSet)
    INSERT INTO FinanceDimensionSets
        (Id, CombinationHash, DisplayValue, TenantId, CreatedAt, IsDeleted)
    VALUES
        (@dimensionSet, CONVERT(varchar(64), HASHBYTES('SHA2_256', N'DEPARTMENT:FACILITIES_TEST'), 2),
         N'DEPARTMENT: Facilities Test Department', @tenant, @now, 0);

IF NOT EXISTS (SELECT 1 FROM FinanceDimensionSetItems WHERE Id = @dimensionItem)
    INSERT INTO FinanceDimensionSetItems
        (Id, FinanceDimensionSetId, FinanceDimensionDefinitionId, FinanceDimensionValueId,
         DimensionCodeSnapshot, DimensionNameSnapshot, DimensionValueCodeSnapshot,
         DimensionValueNameSnapshot, SnapshotSource, SnapshotCapturedAt, SnapshotQuality,
         HistoricalNameReconstructed, TenantId, CreatedAt, IsDeleted)
    VALUES
        (@dimensionItem, @dimensionSet, @department, @dimensionValue,
         N'DEPARTMENT', N'Department / Cost Centre', N'FACILITIES_TEST',
         N'Facilities Test Department', N'CanonicalResolution', @now, N'Exact',
         0, @tenant, @now, 0);

IF NOT EXISTS (SELECT 1 FROM BudgetScenarios WHERE Id = @scenario)
    INSERT INTO BudgetScenarios
        (Id, Name, Description, VersionType, VersionNumber, FiscalYearId, BaseCurrencyCode,
         IsActive, Status, LockedDate, AdoptedAt, AdoptionEffectiveDate, AdoptionReason,
         CreatedAt, IsDeleted, TenantId)
    VALUES
        (@scenario, N'TEST Facilities Budget FY2025',
         N'Historical test fixture for Facilities budget versus actual reporting.',
         N'Original', 1, @fiscalYear, N'GHS', 1, N'Approved',
         '2025-01-01', '2025-01-01', '2025-01-01',
         N'TEST DATA ONLY; not an operational Finance approval.', @now, 0, @tenant);

IF NOT EXISTS (SELECT 1 FROM BudgetScenarioControlDimensions WHERE Id = @controlDimension)
    INSERT INTO BudgetScenarioControlDimensions
        (Id, BudgetScenarioId, FinanceDimensionDefinitionId, DisplayOrder,
         TenantId, CreatedAt, IsDeleted)
    VALUES
        (@controlDimension, @scenario, @department, 1, @tenant, @now, 0);

IF NOT EXISTS (SELECT 1 FROM BudgetReturns WHERE Id = @budgetReturn)
    INSERT INTO BudgetReturns
        (Id, BudgetScenarioId, Status, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@budgetReturn, @scenario, N'Approved', @now, 0, @tenant);

INSERT INTO BudgetEntries
    (Id, BudgetReturnId, AccountId, FiscalPeriodId, FinanceDimensionSetId,
     CurrencyCode, ExchangeRate, Amount, AmountBase, CreatedAt, IsDeleted, TenantId)
SELECT NEWID(), @budgetReturn, planned.AccountId, period.Id, @dimensionSet,
       N'GHS', 1, planned.Amount, planned.Amount, @now, 0, @tenant
FROM FiscalPeriods period
CROSS JOIN (VALUES (@services, CONVERT(decimal(18,2), 3000.00)),
                   (@utilities, CONVERT(decimal(18,2), 1500.00))) planned(AccountId, Amount)
WHERE period.TenantId = @tenant AND period.FiscalYearId = @fiscalYear AND period.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM BudgetEntries existing
                  WHERE existing.TenantId = @tenant AND existing.BudgetReturnId = @budgetReturn
                    AND existing.FiscalPeriodId = period.Id AND existing.AccountId = planned.AccountId
                    AND existing.FinanceDimensionSetId = @dimensionSet AND existing.IsDeleted = 0);

DECLARE @roster uniqueidentifier =
    (SELECT Id FROM EstateFacilityDutyRosters WHERE TenantId = @tenant
       AND RosterReference = N'FAC-DR-20260926-001' AND IsDeleted = 0);
IF @roster IS NOT NULL AND NOT EXISTS (SELECT 1 FROM EstateFacilityDutyAttendances WHERE Id = @attendance)
    INSERT INTO EstateFacilityDutyAttendances
        (Id, DutyRosterId, DutyDate, AttendanceStatus, CompletionStatus, QualityStatus,
         Notes, RecordedAt, CreatedAt, IsDeleted, TenantId)
    VALUES
        (@attendance, @roster, '2026-09-26', N'Present', N'Completed', N'Passed',
         N'TEST DATA: historical Facilities attendance.', '2026-09-26T17:00:00', @now, 0, @tenant);

COMMIT TRANSACTION;

SELECT N'TEST provider' AS Fixture, COUNT(*) AS RecordCount FROM BusinessPartners WHERE Id = @provider
UNION ALL SELECT N'Provider service rates', COUNT(*) FROM EstateFacilityProviderRates WHERE BusinessPartnerId = @provider AND IsDeleted = 0
UNION ALL SELECT N'Linked draft AP invoices', COUNT(*) FROM VendorInvoice WHERE SupplierId = @supplier AND IsDeleted = 0
UNION ALL SELECT N'Historical budget lines', COUNT(*) FROM BudgetEntries WHERE BudgetReturnId = @budgetReturn AND IsDeleted = 0
UNION ALL SELECT N'Cleaner attendance records', COUNT(*) FROM EstateFacilityDutyAttendances WHERE Id = @attendance;
