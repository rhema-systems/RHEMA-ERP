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
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = '20260811022852_AddQuantitySurveyVariationApplications')
    THROW 51940, 'QS-0511 migration is not applied.', 1;

IF EXISTS (
    SELECT required.Name
    FROM (VALUES
        ('ApplicationClientRequestId'), ('ApplicationHash'), ('ApplicationRequestHash'),
        ('AppliedAt'), ('AppliedById'), ('BudgetRevisionId'), ('ContractAmendmentId'),
        ('DownstreamApplicationStatus'), ('ForecastVersionId'), ('RevisedBoqVersionId')) required(Name)
    LEFT JOIN sys.columns actual
      ON actual.object_id = OBJECT_ID('dbo.ProjectVariationOrders')
     AND actual.name = required.Name
    WHERE actual.column_id IS NULL)
    THROW 51940, 'A required QS-0511 application-lineage column is missing.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ProjectVariationOrders')
      AND name IN (
        'FK_ProjectVariationOrders_ProjectBudgetRevisions_BudgetRevisionId',
        'FK_ProjectVariationOrders_ContractAmendments_ContractAmendmentId',
        'FK_ProjectVariationOrders_ProjectForecastVersions_ForecastVersionId',
        'FK_ProjectVariationOrders_ProjectBoqVersions_RevisedBoqVersionId')
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 51940, 'QS-0511 has a disabled or untrusted downstream foreign key.', 1;

IF (SELECT COUNT(*) FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.ProjectVariationOrders')
      AND name IN (
        'FK_ProjectVariationOrders_ProjectBudgetRevisions_BudgetRevisionId',
        'FK_ProjectVariationOrders_ContractAmendments_ContractAmendmentId',
        'FK_ProjectVariationOrders_ProjectForecastVersions_ForecastVersionId',
        'FK_ProjectVariationOrders_ProjectBoqVersions_RevisedBoqVersionId')) <> 4
    THROW 51940, 'A required QS-0511 downstream foreign key is missing.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID('dbo.ProjectVariationOrders')
      AND name = 'CK_QsVariation_Application'
      AND is_disabled = 0 AND is_not_trusted = 0)
    THROW 51940, 'The trusted QS-0511 application-state constraint is missing.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.triggers
    WHERE parent_id = OBJECT_ID('dbo.ProjectVariationOrders')
      AND name = 'TR_QS0511_VariationApplication_Governance'
      AND is_disabled = 0)
    THROW 51940, 'The QS-0511 application-governance trigger is missing or disabled.', 1;

DECLARE @Guard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0511_VariationApplication_Governance'));
IF @Guard NOT LIKE '%SESSION_CONTEXT(N''qs_variation_application_id'')%'
   OR @Guard NOT LIKE '%SESSION_CONTEXT(N''qs_variation_application_actor'')%'
   OR @Guard NOT LIKE '%SESSION_CONTEXT(N''qs_variation_application_hash'')%'
   OR @Guard NOT LIKE '%dbo.ContractAmendments%'
   OR @Guard NOT LIKE '%dbo.ProjectBoqVersions%'
   OR @Guard NOT LIKE '%dbo.ProjectBudgetRevisions%'
   OR @Guard NOT LIKE '%dbo.ProjectForecastVersions%'
   OR @Guard NOT LIKE '%ROUND((SELECT COALESCE(SUM(l.LineAmount), 0)%'
   OR @Guard NOT LIKE '%THROW 51931%'
   OR @Guard NOT LIKE '%THROW 51932%'
    THROW 51940, 'The QS-0511 capability, owner-lineage, reconciliation or immutability gate is incomplete.', 1;

IF (SELECT COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.ProjectVariationOrders')
      AND name IN (
        'IX_ProjectVariationOrders_TenantId_ApplicationClientRequestId',
        'IX_ProjectVariationOrders_TenantId_BudgetRevisionId',
        'IX_ProjectVariationOrders_TenantId_ContractAmendmentId',
        'IX_ProjectVariationOrders_TenantId_ForecastVersionId',
        'IX_ProjectVariationOrders_TenantId_RevisedBoqVersionId')
      AND is_unique = 1) <> 5
    THROW 51940, 'A required QS-0511 tenant/idempotency uniqueness index is missing.', 1;

PRINT 'QS0511_SCHEMA_CAPABILITY_OWNER_LINEAGE=PASS';

DECLARE @AppliedVariationId uniqueidentifier = (
    SELECT TOP (1) Id
    FROM dbo.ProjectVariationOrders
    WHERE IsQuantitySurveyGoverned = 1
      AND IsDeleted = 0
      AND DownstreamApplicationStatus IN ('AppliedPendingBoqApproval', 'Applied')
    ORDER BY AppliedAt, Id);

IF @AppliedVariationId IS NULL
BEGIN
    PRINT 'QS0511_UNGOVERNED_MUTATION=NOT_RUN_NO_APPLIED_SOURCE';
END
ELSE
BEGIN
    DECLARE @MutationError int = 0;
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.ProjectVariationOrders
        SET ApplicationHash = REPLICATE(CASE WHEN LEFT(ApplicationHash, 1) = 'A' THEN 'B' ELSE 'A' END, 64)
        WHERE Id = @AppliedVariationId;
    END TRY
    BEGIN CATCH
        SET @MutationError = ERROR_NUMBER();
    END CATCH;
    IF XACT_STATE() <> 0 ROLLBACK;

    IF @MutationError <> 51931
        THROW 51940, 'QS-0511 did not reject an ungoverned application-lineage mutation.', 1;

    PRINT 'QS0511_UNGOVERNED_MUTATION=BLOCKED';
END;

PRINT 'QS0511_RELEASE_GATE=PASS';
