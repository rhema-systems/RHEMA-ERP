SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = '20260811063100_AddQuantitySurveySubcontractChargeLifecycle')
    THROW 52061, 'QS-0522 migration is not applied.', 1;

IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN
    ('QuantitySurveySubcontractChargeNotices','QuantitySurveySubcontractChargeEvidence','QuantitySurveySubcontractChargeRevisions')) <> 3
    THROW 52062, 'QS-0522 tables are incomplete.', 1;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id IN
    (OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeNotices'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeEvidence'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeRevisions'))
    AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 52063, 'QS-0522 has a disabled or untrusted foreign key.', 1;

IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
    (OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeNotices'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeEvidence'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeRevisions'))
    AND is_disabled = 0 AND is_not_trusted = 0) <> 21
    THROW 52064, 'QS-0522 foreign-key lineage is incomplete.', 1;

IF (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id IN
    (OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeNotices'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeEvidence'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeRevisions'))
    AND is_disabled = 0 AND is_not_trusted = 0) <> 11
    THROW 52065, 'QS-0522 check constraints are incomplete, disabled, or untrusted.', 1;

IF (SELECT COUNT(*) FROM sys.indexes WHERE object_id IN
    (OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeNotices'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeEvidence'),
     OBJECT_ID(N'dbo.QuantitySurveySubcontractChargeRevisions'))
    AND name LIKE 'IX_QuantitySurveySubcontractCharge%') <> 28
    THROW 52066, 'QS-0522 idempotency, lineage, and lifecycle indexes are incomplete.', 1;

DECLARE @expectedTriggers TABLE (Name sysname NOT NULL PRIMARY KEY);
INSERT INTO @expectedTriggers (Name) VALUES
    ('TR_QS0522_SubcontractCharges_Governance'),
    ('TR_QS0522_SubcontractChargeEvidence_AppendOnly'),
    ('TR_QS0522_SubcontractChargeRevisions_AppendOnly'),
    ('TR_QS0521_SubcontractValuations_Governance');

IF EXISTS (SELECT 1 FROM @expectedTriggers e LEFT JOIN sys.triggers t ON t.name = e.Name AND t.parent_class = 1
    WHERE t.object_id IS NULL OR t.is_disabled = 1)
    THROW 52067, 'QS-0522 governance triggers are missing or disabled.', 1;

DECLARE @chargeTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0522_SubcontractCharges_Governance'));
DECLARE @evidenceTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0522_SubcontractChargeEvidence_AppendOnly'));
DECLARE @valuationTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0521_SubcontractValuations_Governance'));
IF @chargeTrigger IS NULL OR CHARINDEX('Invalid QS subcontract charge lifecycle transition', @chargeTrigger) = 0
   OR CHARINDEX('Issued QS subcontract charge commercial and policy lineage is immutable', @chargeTrigger) = 0
    THROW 52068, 'QS-0522 charge lifecycle or immutability gate is incomplete.', 1;
IF @evidenceTrigger IS NULL OR CHARINDEX('same-tenant central DMS lineage', @evidenceTrigger) = 0
    THROW 52069, 'QS-0522 central DMS lineage gate is incomplete.', 1;
IF @valuationTrigger IS NULL OR CHARINDEX('Status IN (''Allocated'',''Applied'')', @valuationTrigger) = 0
   OR CHARINDEX('BackChargeAmount', @valuationTrigger) = 0 OR CHARINDEX('ContraChargeAmount', @valuationTrigger) = 0
    THROW 52070, 'QS-0522 valuation deduction reconciliation gate is incomplete.', 1;

DECLARE @mutationTested bit = 0;
DECLARE @subcontractId uniqueidentifier, @tenantId uniqueidentifier, @profileId uniqueidentifier,
        @decisionId uniqueidentifier, @workflowId uniqueidentifier, @metadataId uniqueidentifier,
        @preparedById uniqueidentifier, @currency nvarchar(10), @policyHash varchar(64);
SELECT TOP (1) @subcontractId = s.Id, @tenantId = s.TenantId, @profileId = s.ConfigurationProfileId,
    @decisionId = s.ContractControlsDecisionId, @workflowId = s.ApprovalWorkflowDefinitionId,
    @preparedById = s.PreparedById, @currency = s.Currency, @policyHash = s.PolicyHash,
    @metadataId = mt.Id
FROM dbo.QuantitySurveySubcontracts s
CROSS APPLY (SELECT TOP (1) Id FROM dbo.CentralDocumentMetadataTemplates mt
             WHERE mt.TenantId = s.TenantId AND mt.IsDeleted = 0 ORDER BY mt.CreatedAt) mt
WHERE s.IsDeleted = 0
ORDER BY s.CreatedAt;

IF @subcontractId IS NOT NULL
BEGIN
    DECLARE @chargeId uniqueidentifier = NEWID(), @clientId uniqueidentifier = NEWID();
    BEGIN TRANSACTION;
    INSERT dbo.QuantitySurveySubcontractChargeNotices
    (Id, SubcontractId, ClientRequestId, RequestHash, NoticeNumber, ChargeType, Title, Reason,
     NoticeDate, ResponseDueDate, ProposedAmount, Currency, Status, ApprovalStatus, ResponseStatus,
     ConfigurationProfileId, ContractControlsDecisionId, ApprovalWorkflowDefinitionId,
     EvidenceMetadataTemplateId, PolicyHash, PreparedById, PreparedAt, CommunicationStatus,
     CommunicationRequestCount, CorrelationId, CreatedAt, IsDeleted, TenantId)
    VALUES
    (@chargeId, @subcontractId, @clientId, REPLICATE('a',64),
     CONCAT('QS0522-SQL-', CONVERT(varchar(36), @chargeId)), 'BackCharge', 'SQL release-gate notice',
     'Transactional release-gate evidence.', CONVERT(date,SYSUTCDATETIME()), DATEADD(day,7,CONVERT(date,SYSUTCDATETIME())),
     1, @currency, 'Draft', 'Draft', 'Pending', @profileId, @decisionId, @workflowId, @metadataId,
     @policyHash, @preparedById, SYSUTCDATETIME(), 'NotRequested', 0, 'qs0522-sql-gate',
     SYSUTCDATETIME(), 0, @tenantId);

    UPDATE dbo.QuantitySurveySubcontractChargeNotices
       SET Status='Issued', IssuedById=@preparedById, IssuedAt=SYSUTCDATETIME(), UpdatedAt=SYSUTCDATETIME()
     WHERE Id=@chargeId;

    BEGIN TRY
        UPDATE dbo.QuantitySurveySubcontractChargeNotices SET ProposedAmount=2 WHERE Id=@chargeId;
        THROW 52071, 'QS-0522 immutable commercial-lineage negative test unexpectedly succeeded.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 52046 THROW;
    END CATCH;
    SET @mutationTested = 1;
    ROLLBACK TRANSACTION;
END;

SELECT 'QS0522_RELEASE_GATE_PASS' AS Result,
       1 AS AppliedMigration,
       (SELECT COUNT(*) FROM @expectedTriggers e JOIN sys.triggers t ON t.name=e.Name WHERE t.is_disabled=0) AS EnabledGovernanceTriggers,
       21 AS TrustedForeignKeys,
       11 AS TrustedCheckConstraints,
       @mutationTested AS TransactionalMutationGateTested,
       (SELECT COUNT(*) FROM dbo.QuantitySurveySubcontracts WHERE IsDeleted=0) AS AvailableSubcontracts;
