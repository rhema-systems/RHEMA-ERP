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
    WHERE MigrationId = '20260811010013_AddQuantitySurveyContractClaimLifecycle')
    THROW 51998, 'QS-0509 migration is not applied.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.QuantitySurveyContractClaims'),
        OBJECT_ID('dbo.QuantitySurveyContractClaimEvidence'),
        OBJECT_ID('dbo.QuantitySurveyContractClaimRevisions'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 51998, 'QS-0509 has a disabled or untrusted foreign key.', 1;

IF EXISTS (
    SELECT required.Name
    FROM (VALUES
        ('TR_QS0509_ContractClaims_Governance'),
        ('TR_QS0509_ContractClaimEvidence_AppendOnly'),
        ('TR_QS0509_ContractClaimRevisions_AppendOnly')) required(Name)
    LEFT JOIN sys.triggers actual
      ON actual.name = required.Name AND actual.is_disabled = 0
    WHERE actual.object_id IS NULL)
    THROW 51998, 'A required QS-0509 SQL guard is missing or disabled.', 1;

DECLARE @ClaimGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0509_ContractClaims_Governance'));
IF @ClaimGuard NOT LIKE '%t.SourcePurchaseRequisitionId%'
   OR @ClaimGuard NOT LIKE '%pr.ProjectId = i.ProjectId%'
   OR @ClaimGuard NOT LIKE '%c.ContractType = ''Works''%'
   OR @ClaimGuard NOT LIKE '%c.Status = ''Active''%'
   OR @ClaimGuard NOT LIKE '%QS-DEC-011%'
   OR @ClaimGuard NOT LIKE '%wet.Code = ''QS_CLAIM''%'
   OR @ClaimGuard NOT LIKE '%i.ApprovedById = i.SubmittedById%'
   OR @ClaimGuard NOT LIKE '%i.ApprovedById = i.QsVettedById%'
   OR @ClaimGuard NOT LIKE '%wi.Status NOT IN (3,4)%'
    THROW 51998, 'QS-0509 tenant, Works-contract, policy, workflow-outcome or SOD guards are incomplete.', 1;

DECLARE @EvidenceGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0509_ContractClaimEvidence_AppendOnly'));
IF @EvidenceGuard NOT LIKE '%VirusScanStatus = 2%'
   OR @EvidenceGuard NOT LIKE '%CentralDocumentVersions%'
   OR @EvidenceGuard NOT LIKE '%IF EXISTS (SELECT 1 FROM deleted)%'
    THROW 51998, 'QS-0509 clean-DMS or append-only evidence guards are incomplete.', 1;

DECLARE @RevisionGuard nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID('dbo.TR_QS0509_ContractClaimRevisions_AppendOnly'));
IF @RevisionGuard NOT LIKE '%IF EXISTS (SELECT 1 FROM deleted)%'
   OR @RevisionGuard NOT LIKE '%ActorBusinessPartnerId%'
   OR @RevisionGuard NOT LIKE '%CorrelationId%'
    THROW 51998, 'QS-0509 immutable revision or actor lineage guards are incomplete.', 1;

PRINT 'QS0509_SCHEMA_TRIGGERS_LINEAGE_WORKFLOW_SOD_DMS=PASS';

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
    THROW 51998, 'QS-0509 SQL gate requires one existing project and active tenant user.', 1;

DECLARE @LineageError int = 0;
BEGIN TRANSACTION;
BEGIN TRY
    INSERT dbo.QuantitySurveyContractClaims
    (
        Id, ProjectId, ContractId, ContractorBusinessPartnerId, ClientRequestId, RequestHash,
        ClaimNumber, ClaimType, Title, Basis, Status, ApprovalStatus, Currency, ClaimedAmount,
        DisputeStatus, SettlementStatus, SettledAmount, ConfigurationProfileId, VariationDecisionId,
        ApprovalWorkflowDefinitionId, EvidenceMetadataTemplateId, PolicyHash,
        SubmittedBusinessPartnerId, CorrelationId, CreatedAt, CreatedBy, CreatedById,
        IsDeleted, TenantId
    )
    VALUES
    (
        NEWID(), @ProjectId, NEWID(), NEWID(), NEWID(), REPLICATE('a', 64),
        'QS0509-GATE', 1, 'Invalid direct contractor claim',
        'Rollback-only proof that exact governed lineage cannot be bypassed.',
        'Draft', 'Draft', 'GHS', 100, 0, 0, 0,
        NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('b', 64),
        NEWID(), 'qs0509-sql-gate', SYSUTCDATETIME(), 'QS0509 SQL gate', @UserId,
        0, @TenantId
    );
END TRY
BEGIN CATCH
    SET @LineageError = ERROR_NUMBER();
END CATCH;
IF XACT_STATE() <> 0 ROLLBACK;

IF @LineageError NOT IN (547, 51991)
    THROW 51998, 'QS-0509 accepted a claim without exact contract, contractor, policy, workflow and DMS lineage.', 1;

IF EXISTS (SELECT 1 FROM dbo.QuantitySurveyContractClaims WHERE CorrelationId = 'qs0509-sql-gate')
    THROW 51998, 'QS-0509 rollback-only SQL proof retained a fixture row.', 1;

PRINT 'QS0509_INCOMPLETE_GOVERNED_LINEAGE=BLOCKED';
PRINT 'QS0509_ROLLBACK_CLEAN=PASS';
