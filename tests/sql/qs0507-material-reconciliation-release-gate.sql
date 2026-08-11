SET NOCOUNT ON;
SET XACT_ABORT OFF;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = '20260810223350_AddQuantitySurveyMaterialReconciliationLifecycle')
    THROW 51901, 'QS-0507 migration is not applied.', 1;

IF (SELECT COUNT(*) FROM sys.tables WHERE name IN (
        'QuantitySurveyMaterialReconciliations',
        'QuantitySurveyMaterialReconciliationLines',
        'QuantitySurveyMaterialReconciliationRevisions')) <> 3
    THROW 51902, 'QS-0507 tables are incomplete.', 1;

IF (SELECT COUNT(*) FROM sys.triggers WHERE name LIKE 'TR_QS0507_%' AND is_disabled = 0) <> 4
    THROW 51903, 'QS-0507 governance triggers are incomplete or disabled.', 1;

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliations'),
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliationLines'),
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliationRevisions'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 51904, 'QS-0507 contains a disabled or untrusted foreign key.', 1;

DECLARE @TenantId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Tenants ORDER BY Id);
DECLARE @UserId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Users WHERE TenantId = @TenantId ORDER BY Id);
IF @TenantId IS NULL OR @UserId IS NULL
    THROW 51905, 'QS-0507 release probe requires one configured tenant user.', 1;

DECLARE @LifecycleRejected bit = 0;
BEGIN TRY
    BEGIN TRANSACTION;
    ALTER TABLE dbo.QuantitySurveyMaterialReconciliations NOCHECK CONSTRAINT ALL;
    DECLARE @LifecycleId uniqueidentifier = NEWID();
    INSERT dbo.QuantitySurveyMaterialReconciliations (
        Id, ProjectId, ContractId, ValuationWorksheetId, ContractorBusinessPartnerId,
        ClientRequestId, RequestHash, ReconciliationNumber, Status, ApprovalStatus,
        ContractNumberSnapshot, ContractorNameSnapshot, CurrencyCodeSnapshot, ValuationBasis,
        InventoryReconciliationRequired, MaterialOnSiteAmount, MaterialOffSiteAmount,
        TdcSuppliedDeductionAmount, ConfigurationProfileId, MaterialDecisionId,
        ApprovalWorkflowDefinitionId, PolicyHash, PreparedById, PreparedAt, CorrelationId,
        CreatedAt, IsDeleted, TenantId)
    VALUES (
        @LifecycleId, NEWID(), NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('A',64),
        CONCAT('SQL-', LEFT(CONVERT(varchar(36), NEWID()), 8)), 'Draft', 'Draft',
        'SQL-CONTRACT', 'SQL Contractor', 'GHS', 0, 0, 0, 0, 0,
        NEWID(), NEWID(), NEWID(), REPLICATE('B',64), @UserId, SYSUTCDATETIME(),
        'qs0507-release-gate', SYSUTCDATETIME(), 0, @TenantId);
    UPDATE dbo.QuantitySurveyMaterialReconciliations
       SET Status = 'Approved', ApprovalStatus = 'Approved', ApprovedById = NEWID(),
           ApprovedAt = SYSUTCDATETIME(), WorkflowInstanceId = NEWID()
     WHERE Id = @LifecycleId;
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51873 SET @LifecycleRejected = 1;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
END CATCH;
IF @LifecycleRejected = 0
    THROW 51906, 'QS-0507 invalid lifecycle transition was not rejected by SQL.', 1;

DECLARE @RevisionRejected bit = 0;
BEGIN TRY
    BEGIN TRANSACTION;
    ALTER TABLE dbo.QuantitySurveyMaterialReconciliations NOCHECK CONSTRAINT ALL;
    ALTER TABLE dbo.QuantitySurveyMaterialReconciliationRevisions NOCHECK CONSTRAINT ALL;
    DECLARE @RevisionParentId uniqueidentifier = NEWID();
    DECLARE @RevisionId uniqueidentifier = NEWID();
    INSERT dbo.QuantitySurveyMaterialReconciliations (
        Id, ProjectId, ContractId, ValuationWorksheetId, ContractorBusinessPartnerId,
        ClientRequestId, RequestHash, ReconciliationNumber, Status, ApprovalStatus,
        ContractNumberSnapshot, ContractorNameSnapshot, CurrencyCodeSnapshot, ValuationBasis,
        InventoryReconciliationRequired, MaterialOnSiteAmount, MaterialOffSiteAmount,
        TdcSuppliedDeductionAmount, ConfigurationProfileId, MaterialDecisionId,
        ApprovalWorkflowDefinitionId, PolicyHash, PreparedById, PreparedAt, CorrelationId,
        CreatedAt, IsDeleted, TenantId)
    VALUES (
        @RevisionParentId, NEWID(), NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('C',64),
        CONCAT('SQL-', LEFT(CONVERT(varchar(36), NEWID()), 8)), 'Draft', 'Draft',
        'SQL-CONTRACT', 'SQL Contractor', 'GHS', 0, 0, 0, 0, 0,
        NEWID(), NEWID(), NEWID(), REPLICATE('D',64), @UserId, SYSUTCDATETIME(),
        'qs0507-release-gate', SYSUTCDATETIME(), 0, @TenantId);
    INSERT dbo.QuantitySurveyMaterialReconciliationRevisions (
        Id, ReconciliationId, ClientRequestId, RequestHash, Action, ActorUserId,
        ActorName, CorrelationId, AfterJson, CreatedAt, IsDeleted, TenantId)
    VALUES (
        @RevisionId, @RevisionParentId, NEWID(), REPLICATE('E',64), 'Prepared', @UserId,
        'SQL release gate', 'qs0507-release-gate', '{}', SYSUTCDATETIME(), 0, @TenantId);
    UPDATE dbo.QuantitySurveyMaterialReconciliationRevisions
       SET Action = 'Tampered'
     WHERE Id = @RevisionId;
    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 51884 SET @RevisionRejected = 1;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
END CATCH;
IF @RevisionRejected = 0
    THROW 51907, 'QS-0507 append-only revision mutation was not rejected by SQL.', 1;

DECLARE @PaymentRejected bit = 0;
IF EXISTS (SELECT 1 FROM dbo.ProjectPaymentCertificates)
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
        ALTER TABLE dbo.ProjectPaymentCertificates NOCHECK CONSTRAINT ALL;
        UPDATE dbo.ProjectPaymentCertificates
           SET QuantitySurveyMaterialReconciliationId = NULL,
               MaterialOnSiteAmount = 1
         WHERE Id = (SELECT TOP (1) Id FROM dbo.ProjectPaymentCertificates ORDER BY Id);
        ROLLBACK TRANSACTION;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 51885 SET @PaymentRejected = 1;
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    END CATCH;
    IF @PaymentRejected = 0
        THROW 51908, 'QS-0507 ungoverned payment-certificate material value was not rejected by SQL.', 1;
END;

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliations'),
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliationLines'),
        OBJECT_ID('dbo.QuantitySurveyMaterialReconciliationRevisions'),
        OBJECT_ID('dbo.ProjectPaymentCertificates'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 51909, 'QS-0507 release probe left a disabled or untrusted foreign key.', 1;

SELECT 'QS0507_SQL_RELEASE_GATE_PASS' AS Result,
       @LifecycleRejected AS LifecycleRejected,
       @RevisionRejected AS RevisionRejected,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.ProjectPaymentCertificates) THEN @PaymentRejected ELSE NULL END AS PaymentRejected;
