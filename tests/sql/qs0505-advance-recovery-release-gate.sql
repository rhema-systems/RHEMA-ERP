SET NOCOUNT ON;
SET XACT_ABORT OFF;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID('dbo.QuantitySurveyAdvanceRecoveryAgreements', 'U') IS NULL
    THROW 55150, 'QS-0505 agreement table is missing.', 1;
IF OBJECT_ID('dbo.QuantitySurveyAdvanceRecoveryRevisions', 'U') IS NULL
    THROW 55151, 'QS-0505 revision table is missing.', 1;

DECLARE @Id uniqueidentifier = NEWID();
DECLARE @TenantId uniqueidentifier = NEWID();
DECLARE @ActorId uniqueidentifier = NEWID();

BEGIN TRY
    BEGIN TRANSACTION;
    ALTER TABLE dbo.QuantitySurveyAdvanceRecoveryAgreements NOCHECK CONSTRAINT ALL;

    INSERT dbo.QuantitySurveyAdvanceRecoveryAgreements
    (
        Id, ProjectId, ContractId, VendorPaymentId, ClientRequestId, RequestHash,
        RecoveryNumber, Status, ApprovalStatus, ContractNumberSnapshot,
        ContractorNameSnapshot, PaymentNumberSnapshot, PaymentDateSnapshot,
        CurrencyCodeSnapshot, OriginalAdvanceAmount, RecoveryPercentage,
        ConfigurationProfileId, ValuationDecisionId, PolicyHash, PreparedById,
        PreparedAt, CorrelationId, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        @Id, NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('A', 64),
        'ADVREC-SQL-GATE', 'Draft', 'Draft', 'WORKS-SQL-GATE',
        'SQL Gate Contractor', 'VP-SQL-GATE', SYSUTCDATETIME(),
        'GHS', 1000.00, 10.0000,
        NEWID(), NEWID(), REPLICATE('B', 64), @ActorId,
        SYSUTCDATETIME(), 'qs0505-sql-gate', SYSUTCDATETIME(), 0, @TenantId
    );

    THROW 55152, 'QS-0505 invalid Finance and contract lineage was accepted.', 1;
END TRY
BEGIN CATCH
    DECLARE @LineageError int = ERROR_NUMBER();
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF @LineageError <> 55053 THROW;
END CATCH;

SET @Id = NEWID();
BEGIN TRY
    BEGIN TRANSACTION;
    DISABLE TRIGGER dbo.TR_QsAdvanceRecoveryAgreements_QS0505Guard
        ON dbo.QuantitySurveyAdvanceRecoveryAgreements;
    ALTER TABLE dbo.QuantitySurveyAdvanceRecoveryAgreements NOCHECK CONSTRAINT ALL;

    INSERT dbo.QuantitySurveyAdvanceRecoveryAgreements
    (
        Id, ProjectId, ContractId, VendorPaymentId, ClientRequestId, RequestHash,
        RecoveryNumber, Status, ApprovalStatus, ContractNumberSnapshot,
        ContractorNameSnapshot, PaymentNumberSnapshot, PaymentDateSnapshot,
        CurrencyCodeSnapshot, OriginalAdvanceAmount, RecoveryPercentage,
        ConfigurationProfileId, ValuationDecisionId, PolicyHash, PreparedById,
        PreparedAt, CorrelationId, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        @Id, NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('C', 64),
        'ADVREC-IMMUTABLE-GATE', 'Draft', 'Draft', 'WORKS-SQL-GATE',
        'SQL Gate Contractor', 'VP-SQL-GATE', SYSUTCDATETIME(),
        'GHS', 1000.00, 10.0000,
        NEWID(), NEWID(), REPLICATE('D', 64), @ActorId,
        SYSUTCDATETIME(), 'qs0505-sql-gate', SYSUTCDATETIME(), 0, @TenantId
    );

    ENABLE TRIGGER dbo.TR_QsAdvanceRecoveryAgreements_QS0505Guard
        ON dbo.QuantitySurveyAdvanceRecoveryAgreements;
    UPDATE dbo.QuantitySurveyAdvanceRecoveryAgreements
       SET OriginalAdvanceAmount = 900.00
     WHERE Id = @Id;

    THROW 55153, 'QS-0505 immutable commercial terms were changed.', 1;
END TRY
BEGIN CATCH
    DECLARE @ImmutableError int = ERROR_NUMBER();
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF @ImmutableError <> 55052 THROW;
END CATCH;

SET @Id = NEWID();
DECLARE @RevisionId uniqueidentifier = NEWID();
BEGIN TRY
    BEGIN TRANSACTION;
    DISABLE TRIGGER dbo.TR_QsAdvanceRecoveryAgreements_QS0505Guard
        ON dbo.QuantitySurveyAdvanceRecoveryAgreements;
    ALTER TABLE dbo.QuantitySurveyAdvanceRecoveryAgreements NOCHECK CONSTRAINT ALL;
    ALTER TABLE dbo.QuantitySurveyAdvanceRecoveryRevisions NOCHECK CONSTRAINT ALL;

    INSERT dbo.QuantitySurveyAdvanceRecoveryAgreements
    (
        Id, ProjectId, ContractId, VendorPaymentId, ClientRequestId, RequestHash,
        RecoveryNumber, Status, ApprovalStatus, ContractNumberSnapshot,
        ContractorNameSnapshot, PaymentNumberSnapshot, PaymentDateSnapshot,
        CurrencyCodeSnapshot, OriginalAdvanceAmount, RecoveryPercentage,
        ConfigurationProfileId, ValuationDecisionId, PolicyHash, PreparedById,
        PreparedAt, CorrelationId, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        @Id, NEWID(), NEWID(), NEWID(), NEWID(), REPLICATE('E', 64),
        'ADVREC-AUDIT-GATE', 'Draft', 'Draft', 'WORKS-SQL-GATE',
        'SQL Gate Contractor', 'VP-SQL-GATE', SYSUTCDATETIME(),
        'GHS', 1000.00, 10.0000,
        NEWID(), NEWID(), REPLICATE('F', 64), @ActorId,
        SYSUTCDATETIME(), 'qs0505-sql-gate', SYSUTCDATETIME(), 0, @TenantId
    );

    INSERT dbo.QuantitySurveyAdvanceRecoveryRevisions
    (
        Id, AgreementId, Action, ActorUserId, ActorName, CorrelationId,
        AfterJson, CreatedAt, IsDeleted, TenantId
    )
    VALUES
    (
        @RevisionId, @Id, 'SqlReleaseGate', @ActorId, 'SQL release gate',
        'qs0505-sql-gate', '{}', SYSUTCDATETIME(), 0, @TenantId
    );

    UPDATE dbo.QuantitySurveyAdvanceRecoveryRevisions
       SET AfterJson = '{"changed":true}'
     WHERE Id = @RevisionId;

    THROW 55154, 'QS-0505 append-only revision was changed.', 1;
END TRY
BEGIN CATCH
    DECLARE @RevisionError int = ERROR_NUMBER();
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    IF @RevisionError <> 55057 THROW;
END CATCH;

IF EXISTS
(
    SELECT 1
    FROM sys.triggers
    WHERE name IN
    (
        'TR_QsAdvanceRecoveryAgreements_QS0505Guard',
        'TR_ProjectPaymentCertificates_QS0505AdvanceRecoveryGuard',
        'TR_QsAdvanceRecoveryRevisions_QS0505AppendOnly'
    )
      AND is_disabled = 1
)
    THROW 55155, 'A QS-0505 release trigger remained disabled after rollback.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE
        (
            parent_object_id = OBJECT_ID('dbo.QuantitySurveyAdvanceRecoveryAgreements')
            OR parent_object_id = OBJECT_ID('dbo.QuantitySurveyAdvanceRecoveryRevisions')
            OR name = 'FK_ProjectPaymentCertificates_QuantitySurveyAdvanceRecoveryAgreements_QuantitySurveyAdvanceRecoveryAgreementId'
        )
        AND (is_disabled = 1 OR is_not_trusted = 1)
)
    THROW 55156, 'A QS-0505 foreign key is disabled or untrusted after rollback.', 1;

SELECT
    CAST(1 AS bit) AS InvalidLineageBlocked,
    CAST(1 AS bit) AS ImmutableTermsBlocked,
    CAST(1 AS bit) AS AppendOnlyAuditBlocked,
    CAST(1 AS bit) AS TriggerAndForeignKeyTrustPreserved;
