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
    WHERE MigrationId = '20260810234912_AddQuantitySurveyVariationLifecycle')
    THROW 51989, 'QS-0508 migration is not applied.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.ProjectVariationOrders'),
        OBJECT_ID('dbo.QuantitySurveyVariationValuationLines'),
        OBJECT_ID('dbo.QuantitySurveyVariationEvidence'),
        OBJECT_ID('dbo.QuantitySurveyVariationRevisions'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 51989, 'QS-0508 has a disabled or untrusted foreign key.', 1;

IF EXISTS (
    SELECT required.Name
    FROM (VALUES
        ('TR_QS0508_ProjectVariation_Governance'),
        ('TR_QS0508_VariationLines_Governance'),
        ('TR_QS0508_VariationEvidence_AppendOnly'),
        ('TR_QS0508_VariationRevisions_AppendOnly')) required(Name)
    LEFT JOIN sys.triggers actual
      ON actual.name = required.Name AND actual.is_disabled = 0
    WHERE actual.object_id IS NULL)
    THROW 51989, 'A required QS-0508 SQL guard is missing or disabled.', 1;

DECLARE @GuardDefinition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0508_ProjectVariation_Governance'));
IF @GuardDefinition NOT LIKE '%PurchaseRequisitions pr%'
   OR @GuardDefinition NOT LIKE '%pr.ProjectId = i.ProjectId%'
   OR @GuardDefinition LIKE '%p.BusinessPartnerId <> i.ContractorBusinessPartnerId%'
    THROW 51989, 'QS-0508 contract-to-project procurement lineage is not enforced.', 1;

PRINT 'QS0508_SCHEMA_TRIGGERS_AND_PROJECT_LINEAGE=PASS';

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
    THROW 51989, 'QS-0508 SQL gate requires one existing project and active tenant user.', 1;

DECLARE @LineageError int = 0;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT dbo.ProjectVariationOrders
    (
        Id, ProjectId, ClientRequestId, ReferenceNumber, Title, Description,
        VariationType, Status, ApprovalStatus, RequestedDate, EstimatedAmount,
        OriginalContractSumSnapshot, Currency, IsQuantitySurveyGoverned,
        PreparedById, CorrelationId, CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), @ProjectId, NEWID(), 'QS0508-GATE',
        'Invalid direct governed variation',
        'Rollback-only proof that controlled source lineage cannot be bypassed.',
        'Other', 'Draft', 'Draft', SYSUTCDATETIME(), 100, 1000, 'GHS', 1,
        @UserId, 'qs0508-sql-gate', SYSUTCDATETIME(), 'QS0508 SQL gate', @UserId,
        0, @TenantId
    );
END TRY
BEGIN CATCH
    SET @LineageError = ERROR_NUMBER();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK;

IF @LineageError <> 547
    THROW 51989, 'QS-0508 accepted a governed variation without exact contract, BoQ, workflow, DMS and policy lineage.', 1;

PRINT 'QS0508_INCOMPLETE_GOVERNED_LINEAGE=BLOCKED_547';

IF EXISTS (SELECT 1 FROM dbo.ProjectVariationOrders WHERE CorrelationId = 'qs0508-sql-gate')
    THROW 51989, 'QS-0508 rollback-only SQL proof retained a fixture row.', 1;

PRINT 'QS0508_ROLLBACK_CLEAN=PASS';
