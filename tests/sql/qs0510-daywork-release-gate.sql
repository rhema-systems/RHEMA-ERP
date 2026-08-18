SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF NOT EXISTS (
    SELECT 1 FROM __EFMigrationsHistory
    WHERE MigrationId = '20260811013838_AddQuantitySurveyDayworkLifecycle')
    THROW 52010, 'QS-0510 migration is not applied.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.QuantitySurveyDayworkSheets'),
        OBJECT_ID('dbo.QuantitySurveyDayworkLines'),
        OBJECT_ID('dbo.QuantitySurveyDayworkEvidence'),
        OBJECT_ID('dbo.QuantitySurveyDayworkRevisions'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 52010, 'QS-0510 has a disabled or untrusted foreign key.', 1;

IF EXISTS (
    SELECT required.Name
    FROM (VALUES
        ('TR_QS0510_DayworkSheets_Governance'),
        ('TR_QS0510_DayworkLines_Governance'),
        ('TR_QS0510_DayworkEvidence_AppendOnly'),
        ('TR_QS0510_DayworkRevisions_AppendOnly'),
        ('TR_QS0510_VariationDayworkEligibility')) required(Name)
    LEFT JOIN sys.triggers actual
      ON actual.name = required.Name AND actual.is_disabled = 0
    WHERE actual.object_id IS NULL)
    THROW 52010, 'A required QS-0510 SQL guard is missing or disabled.', 1;

DECLARE @SheetGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0510_DayworkSheets_Governance'));
IF @SheetGuard NOT LIKE '%c.ContractType = ''Works''%'
   OR @SheetGuard NOT LIKE '%c.Status = ''Active''%'
   OR @SheetGuard NOT LIKE '%pr.ProjectId = i.ProjectId%'
   OR @SheetGuard NOT LIKE '%QS-DEC-011%'
   OR @SheetGuard NOT LIKE '%BusinessPartnerUsers%'
   OR @SheetGuard NOT LIKE '%i.VerifiedById = i.ContractorSignedById%'
   OR @SheetGuard NOT LIKE '%QuantitySurveyDayworkEvidence%'
    THROW 52010, 'QS-0510 Works-contract, project, policy, contractor-signature, SOD or evidence lineage is incomplete.', 1;

DECLARE @LineGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0510_DayworkLines_Governance'));
IF @LineGuard NOT LIKE '%QuantitySurveyRateLibraryRates%'
   OR @LineGuard NOT LIKE '%dbo.UnitsOfMeasure%'
   OR @LineGuard LIKE '%dbo.UnitOfMeasures%'
   OR @LineGuard NOT LIKE '%r.LifecycleStatus = 1%'
   OR @LineGuard NOT LIKE '%ROUND(i.Quantity * i.UnitRate, 2)%'
    THROW 52010, 'QS-0510 Published-rate, UOM, category or amount controls are incomplete.', 1;

DECLARE @EvidenceGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0510_DayworkEvidence_AppendOnly'));
IF @EvidenceGuard NOT LIKE '%VirusScanStatus = 2%'
   OR @EvidenceGuard NOT LIKE '%CentralDocumentVersions%'
   OR @EvidenceGuard NOT LIKE '%IF EXISTS (SELECT 1 FROM deleted)%'
    THROW 52010, 'QS-0510 clean-DMS or append-only evidence controls are incomplete.', 1;

DECLARE @RevisionGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0510_DayworkRevisions_AppendOnly'));
IF @RevisionGuard NOT LIKE '%IF EXISTS (SELECT 1 FROM deleted)%'
   OR @RevisionGuard NOT LIKE '%ActorBusinessPartnerId%'
   OR @RevisionGuard NOT LIKE '%CorrelationId%'
    THROW 52010, 'QS-0510 immutable revision or actor lineage is incomplete.', 1;

DECLARE @ParentGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0510_VariationDayworkEligibility'));
IF @ParentGuard NOT LIKE '%i.Status IN (''PendingApproval'',''Approved'')%'
   OR @ParentGuard NOT LIKE '%s.Status <> 2%'
   OR @ParentGuard NOT LIKE '%i.EstimatedAmount%'
    THROW 52010, 'QS-0510 parent-variation detail readiness control is incomplete.', 1;

PRINT 'QS0510_SCHEMA_TRIGGERS_LINEAGE_RATES_SIGNATURES_SOD_DMS=PASS';

DECLARE @TenantId uniqueidentifier, @ProjectId uniqueidentifier, @UserId uniqueidentifier;
SELECT TOP (1)
    @TenantId = project.TenantId,
    @ProjectId = project.Id,
    @UserId = actor.Id
FROM Projects project
JOIN Users actor ON actor.TenantId = project.TenantId
WHERE project.IsDeleted = 0
  AND actor.IsActive = 1
ORDER BY project.CreatedAt, actor.Id;

IF @ProjectId IS NULL
    THROW 52010, 'QS-0510 SQL gate requires one existing project and active tenant user.', 1;

DECLARE @LineageError int = 0;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT dbo.QuantitySurveyDayworkSheets
    (
        Id, ProjectId, VariationOrderId, ContractId, ContractorBusinessPartnerId,
        ClientRequestId, RequestHash, SheetNumber, WorkDate, WorkLocation, Description,
        Status, Currency, TotalAmount, ConfigurationProfileId, VariationDecisionId,
        EvidenceMetadataTemplateId, PolicyHash, CorrelationId, CreatedAt, CreatedBy,
        CreatedById, IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), @ProjectId, NEWID(), NEWID(), NEWID(),
        NEWID(), REPLICATE('a', 64), 'QS0510-GATE', SYSUTCDATETIME(), 'SQL gate',
        'Rollback-only proof that incomplete governed daywork lineage cannot be bypassed.',
        0, 'GHS', 0, NEWID(), NEWID(), NEWID(), REPLICATE('b', 64),
        'qs0510-sql-gate', SYSUTCDATETIME(), 'QS0510 SQL gate', @UserId, 0, @TenantId
    );
END TRY
BEGIN CATCH
    SET @LineageError = ERROR_NUMBER();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK;

IF @LineageError NOT IN (547, 52001)
    THROW 52010, 'QS-0510 accepted a sheet without exact variation, Works-contract, contractor, policy and DMS lineage.', 1;

IF EXISTS (SELECT 1 FROM dbo.QuantitySurveyDayworkSheets WHERE CorrelationId = 'qs0510-sql-gate')
    THROW 52010, 'QS-0510 rollback-only SQL proof retained a fixture row.', 1;

PRINT 'QS0510_INCOMPLETE_GOVERNED_LINEAGE=BLOCKED';
PRINT 'QS0510_ROLLBACK_CLEAN=PASS';
