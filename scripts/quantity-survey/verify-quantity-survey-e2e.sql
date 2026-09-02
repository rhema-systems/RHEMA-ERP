SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT LIKE N'RhemaQsUatAssurance[_][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
    THROW 51980, 'QS_E2E_DATABASE_REQUIRED: verification is restricted to a disposable RhemaQsUatAssurance_YYYYMMDD database.', 1;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @MeasurementId uniqueidentifier;
DECLARE @WorksheetId uniqueidentifier;
DECLARE @CertificateId uniqueidentifier;
DECLARE @ValuationWorkflowInstanceId uniqueidentifier;
DECLARE @CertificateWorkflowInstanceId uniqueidentifier;
DECLARE @BudgetCommitmentId uniqueidentifier;
DECLARE @GrossCertifiedAmount decimal(18,2);
DECLARE @NetCertifiedAmount decimal(18,2);

IF (SELECT COUNT(*) FROM dbo.QuantitySurveyMeasurementSheets
    WHERE TenantId=@TenantId AND ClientRequestId='d6000000-0000-4000-8000-000000000101' AND IsDeleted=0) <> 1
    THROW 51981, 'QS_E2E_MEASUREMENT_COUNT: exactly one governed acceptance measurement is required.', 1;

SELECT @MeasurementId=Id
FROM dbo.QuantitySurveyMeasurementSheets
WHERE TenantId=@TenantId AND ClientRequestId='d6000000-0000-4000-8000-000000000101' AND IsDeleted=0;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.QuantitySurveyMeasurementSheets
    WHERE Id=@MeasurementId AND Status=N'Recorded' AND TotalMeasuredQuantity > 0
      AND TotalMeasuredQuantity <= BoqQuantitySnapshot
)
    THROW 51982, 'QS_E2E_MEASUREMENT_STATE: measurement must be recorded and remain within the approved BoQ quantity.', 1;

IF (SELECT COUNT(*) FROM dbo.QuantitySurveyValuationWorksheets
    WHERE TenantId=@TenantId AND ClientRequestId='d6020000-0000-4000-8000-000000000010' AND IsDeleted=0) <> 1
    THROW 51983, 'QS_E2E_WORKSHEET_COUNT: exactly one governed acceptance valuation worksheet is required.', 1;

SELECT @WorksheetId=Id, @ValuationWorkflowInstanceId=WorkflowInstanceId
FROM dbo.QuantitySurveyValuationWorksheets
WHERE TenantId=@TenantId AND ClientRequestId='d6020000-0000-4000-8000-000000000010' AND IsDeleted=0;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.QuantitySurveyValuationWorksheets
    WHERE Id=@WorksheetId AND Status=N'Approved' AND ApprovalStatus=N'Approved'
      AND CertificateReady=1 AND CurrentCertifiedValue > 0 AND WorkflowInstanceId IS NOT NULL
)
    THROW 51984, 'QS_E2E_WORKSHEET_STATE: valuation must be approved, certificate-ready, and bound to its workflow.', 1;

IF (SELECT COUNT(*) FROM dbo.ProjectPaymentCertificates
    WHERE TenantId=@TenantId AND ClientRequestId='d6020000-0000-4000-8000-000000000020' AND IsDeleted=0) <> 1
    THROW 51985, 'QS_E2E_CERTIFICATE_COUNT: exactly one governed acceptance payment certificate is required.', 1;

SELECT
    @CertificateId=Id,
    @CertificateWorkflowInstanceId=WorkflowInstanceId,
    @GrossCertifiedAmount=GrossCertifiedAmount,
    @NetCertifiedAmount=NetCertifiedAmount
FROM dbo.ProjectPaymentCertificates
WHERE TenantId=@TenantId AND ClientRequestId='d6020000-0000-4000-8000-000000000020' AND IsDeleted=0;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.ProjectPaymentCertificates
    WHERE Id=@CertificateId AND Status=N'Approved' AND ApprovalStatus=N'Approved'
      AND GrossCertifiedAmount > 0 AND NetCertifiedAmount > 0 AND Currency=N'GHS'
      AND WorkflowInstanceId IS NOT NULL AND CentralDocumentRecordId IS NOT NULL
      AND CentralDocumentVersionId IS NOT NULL AND VendorInvoiceId IS NOT NULL
      AND ApHandoffStatus=N'Created' AND ApHandoffFailure IS NULL
)
    THROW 51986, 'QS_E2E_CERTIFICATE_STATE: certificate approval, document, budget, or AP handoff is incomplete.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ProjectPaymentCertificates certificate
    JOIN dbo.CentralDocumentMetadataTemplates template
      ON template.Id=certificate.CertificateMetadataTemplateId
     AND template.TenantId=certificate.TenantId AND template.IsDeleted=0 AND template.IsActive=1
    JOIN dbo.CentralDocumentRecords record
      ON record.Id=certificate.CentralDocumentRecordId
     AND record.TenantId=certificate.TenantId AND record.IsDeleted=0
     AND record.SourceModule=N'QuantitySurvey' AND record.SourceRecordId=certificate.Id
     AND record.MetadataTemplateCode=certificate.CertificateMetadataTemplateCodeSnapshot
     AND record.AccessProfile=template.AccessProfile
    JOIN dbo.CentralDocumentVersions version
      ON version.Id=certificate.CentralDocumentVersionId
     AND version.TenantId=certificate.TenantId AND version.IsDeleted=0
     AND version.DocumentRecordId=record.Id AND version.Status=N'Approved'
    WHERE certificate.Id=@CertificateId
)
    THROW 51987, 'QS_E2E_DOCUMENT_LINEAGE: the issued certificate must use the published metadata template and central DMS access profile.', 1;

IF (SELECT COUNT(*) FROM dbo.ProcurementBudgetCommitmentLedgerEntries
    WHERE TenantId=@TenantId AND SourceType=N'PaymentCertificate'
      AND SourceId=@CertificateId AND EntryType=2 AND IsDeleted=0) <> 1
    THROW 51988, 'QS_E2E_BUDGET_LEDGER_COUNT: payment-certificate utilization must post exactly once.', 1;

SELECT @BudgetCommitmentId=ProcurementBudgetCommitmentId
FROM dbo.ProcurementBudgetCommitmentLedgerEntries
WHERE TenantId=@TenantId AND SourceType=N'PaymentCertificate'
  AND SourceId=@CertificateId AND EntryType=2 AND IsDeleted=0;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.ProcurementBudgetCommitmentLedgerEntries
    WHERE TenantId=@TenantId AND SourceType=N'PaymentCertificate'
      AND SourceId=@CertificateId AND EntryType=2 AND IsDeleted=0
      AND FormalCommitmentEntryId IS NOT NULL AND Amount=@GrossCertifiedAmount AND Currency=N'GHS'
)
    THROW 51989, 'QS_E2E_BUDGET_LEDGER_STATE: utilization must reference the formal commitment and equal the gross certified amount.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.ProcurementBudgetCommitments
    WHERE Id=@BudgetCommitmentId AND TenantId=@TenantId AND IsDeleted=0
      AND Status=1 AND Currency=N'GHS' AND FormallyCommittedAmount > 0
      AND UtilizedAmount=@GrossCertifiedAmount
)
    THROW 51990, 'QS_E2E_COMMITMENT_STATE: formal Works commitment utilization is incorrect.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.ProjectPaymentCertificates certificate
    JOIN dbo.VendorInvoice invoice
      ON invoice.Id=certificate.VendorInvoiceId AND invoice.TenantId=certificate.TenantId AND invoice.IsDeleted=0
    WHERE certificate.Id=@CertificateId
      AND invoice.Reference=N'QS-CERT:'+LOWER(REPLACE(CONVERT(varchar(36), certificate.Id), '-', ''))
      AND invoice.TotalAmount=@NetCertifiedAmount AND invoice.CurrencyCode=N'GHS'
      AND invoice.AcceptedSupplyKind=3 AND invoice.AcceptedSupplySourceId=certificate.Id
      AND invoice.AcceptedSupplySourceReference=certificate.CertificateNumber
      AND LEN(invoice.AcceptedSupplySnapshotHash)=64
      AND invoice.Status=1 AND invoice.ApprovalStatus=N'Draft'
)
    THROW 51991, 'QS_E2E_AP_HANDOFF_STATE: the idempotent AP invoice does not match the approved certificate.', 1;

IF (SELECT COUNT(*) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'GeneratePaymentCertificate') <> 1
 OR (SELECT COUNT(*) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'SubmitPaymentCertificate') <> 1
 OR (SELECT COUNT(*) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'ApprovePaymentCertificate') <> 4
 OR (SELECT COUNT(DISTINCT ActorUserId) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'ApprovePaymentCertificate') <> 4
 OR (SELECT COUNT(*) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'IssuePaymentCertificateDocument') <> 1
 OR (SELECT COUNT(*) FROM dbo.QuantitySurveyPaymentCertificateRevisions
    WHERE TenantId=@TenantId AND PaymentCertificateId=@CertificateId AND IsDeleted=0
      AND Action=N'HandoffPaymentCertificateToAp') <> 1
    THROW 51992, 'QS_E2E_CERTIFICATE_AUDIT: certificate idempotency or four-stage role separation failed.', 1;

IF @ValuationWorkflowInstanceId IS NULL
 OR (SELECT COUNT(*) FROM dbo.WorkflowStepInstances
     WHERE TenantId=@TenantId AND WorkflowInstanceId=@ValuationWorkflowInstanceId AND Status=2 AND IsDeleted=0) <> 4
 OR (SELECT COUNT(DISTINCT PerformedById) FROM dbo.WorkflowActivityLogs
     WHERE TenantId=@TenantId AND WorkflowInstanceId=@ValuationWorkflowInstanceId
       AND ActivityType=20 AND PerformedById IS NOT NULL AND IsDeleted=0) <> 4
    THROW 51993, 'QS_E2E_VALUATION_WORKFLOW: valuation must complete four workflow stages with four performers.', 1;

IF @CertificateWorkflowInstanceId IS NULL
 OR (SELECT COUNT(*) FROM dbo.WorkflowStepInstances
     WHERE TenantId=@TenantId AND WorkflowInstanceId=@CertificateWorkflowInstanceId AND Status=2 AND IsDeleted=0) <> 4
 OR (SELECT COUNT(DISTINCT PerformedById) FROM dbo.WorkflowActivityLogs
     WHERE TenantId=@TenantId AND WorkflowInstanceId=@CertificateWorkflowInstanceId
       AND ActivityType=20 AND PerformedById IS NOT NULL AND IsDeleted=0) <> 4
    THROW 51994, 'QS_E2E_CERTIFICATE_WORKFLOW: certificate must complete four workflow stages with four performers.', 1;

SELECT
    N'QS-E2E-VERIFIED' AS Result,
    DB_NAME() AS DatabaseName,
    @MeasurementId AS MeasurementId,
    @WorksheetId AS WorksheetId,
    @CertificateId AS CertificateId,
    @GrossCertifiedAmount AS GrossCertifiedAmount,
    @NetCertifiedAmount AS NetCertifiedAmount,
    4 AS ValuationWorkflowStages,
    4 AS CertificateWorkflowStages;
