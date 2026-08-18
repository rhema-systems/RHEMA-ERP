SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;

IF (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory
    WHERE MigrationId IN (
        '20260811050412_AddQuantitySurveySubcontractLifecycle',
        '20260811061000_HardenQuantitySurveySubcontractCertificates')) <> 2
    THROW 52050, 'QS-0521 migrations are not both applied.', 1;

IF (SELECT COUNT(*) FROM sys.tables
    WHERE schema_id = SCHEMA_ID(N'dbo')
      AND name IN (
          'QuantitySurveySubcontracts',
          'QuantitySurveySubcontractValuations',
          'QuantitySurveySubcontractEvidence',
          'QuantitySurveySubcontractRevisions')) <> 4
    THROW 52051, 'QS-0521 tables are incomplete.', 1;

IF (SELECT COUNT(*) FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ProjectPaymentCertificates')
      AND name IN ('QuantitySurveySubcontractValuationId', 'SubcontractorBusinessPartnerId')) <> 2
    THROW 52052, 'QS-0521 payment-certificate lineage columns are incomplete.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id IN (
        OBJECT_ID(N'dbo.QuantitySurveySubcontracts'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractValuations'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractEvidence'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractRevisions'),
        OBJECT_ID(N'dbo.ProjectPaymentCertificates'))
      AND (is_disabled = 1 OR is_not_trusted = 1))
    THROW 52053, 'QS-0521 has a disabled or untrusted foreign key.', 1;

IF (SELECT COUNT(*) FROM sys.check_constraints
    WHERE parent_object_id IN (
        OBJECT_ID(N'dbo.QuantitySurveySubcontracts'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractValuations'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractEvidence'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractRevisions'))
      AND is_disabled = 0 AND is_not_trusted = 0) <> 12
    THROW 52054, 'QS-0521 check constraints are incomplete, disabled, or untrusted.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.ProjectPaymentCertificates')
      AND name = 'CK_ProjectPaymentCertificates_QsSubcontractLineage'
      AND is_disabled = 0 AND is_not_trusted = 0)
    THROW 52055, 'QS-0521 payment-certificate lineage constraint is missing, disabled, or untrusted.', 1;

IF (SELECT COUNT(*) FROM sys.indexes
    WHERE object_id IN (
        OBJECT_ID(N'dbo.QuantitySurveySubcontracts'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractValuations'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractEvidence'),
        OBJECT_ID(N'dbo.QuantitySurveySubcontractRevisions'))
      AND name LIKE 'IX_QuantitySurveySubcontract%') <> 43
    THROW 52056, 'QS-0521 controlled-master, lineage, or idempotency indexes are incomplete.', 1;

DECLARE @expectedTriggers TABLE (Name sysname NOT NULL PRIMARY KEY);
INSERT INTO @expectedTriggers (Name) VALUES
    ('TR_QS0521_Subcontracts_Governance'),
    ('TR_QS0521_SubcontractValuations_Governance'),
    ('TR_QS0521_SubcontractEvidence_AppendOnly'),
    ('TR_QS0521_SubcontractRevisions_AppendOnly'),
    ('TR_QS0521_SubcontractCertificates_Governance');

IF EXISTS (
    SELECT 1 FROM @expectedTriggers e
    LEFT JOIN sys.triggers t ON t.name = e.Name AND t.parent_class = 1
    WHERE t.object_id IS NULL OR t.is_disabled = 1)
    THROW 52057, 'QS-0521 governance triggers are missing or disabled.', 1;

DECLARE @subcontractTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0521_Subcontracts_Governance'));
DECLARE @valuationTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0521_SubcontractValuations_Governance'));
DECLARE @certificateTrigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_QS0521_SubcontractCertificates_Governance'));

IF @subcontractTrigger IS NULL
   OR CHARINDEX('c.ContractType <> ''Works''', @subcontractTrigger) = 0
   OR CHARINDEX('c.Status <> ''Active''', @subcontractTrigger) = 0
   OR CHARINDEX('c.AllowSubcontracting = 0', @subcontractTrigger) = 0
   OR CHARINDEX('c.SubcontractPaymentTermId <> i.PaymentTermId', @subcontractTrigger) = 0
    THROW 52058, 'QS-0521 subcontract contract and controlled-payment-term gate is incomplete.', 1;

IF @valuationTrigger IS NULL
   OR CHARINDEX('i.ApprovedBackChargeAmount <> 0', @valuationTrigger) = 0
   OR CHARINDEX('i.ApprovedContraChargeAmount <> 0', @valuationTrigger) = 0
   OR CHARINDEX('Approved or paid QS subcontract valuation', @valuationTrigger) = 0
    THROW 52059, 'QS-0521 valuation charge boundary or immutability gate is incomplete.', 1;

IF @certificateTrigger IS NULL
   OR CHARINDEX('QS subcontract certificate totals do not reconcile', @certificateTrigger) = 0
   OR CHARINDEX('CASE WHEN i.TaxHandling = ''Inclusive'' THEN 0 ELSE i.TaxAmount END', @certificateTrigger) = 0
   OR CHARINDEX('linked Finance AP invoice must belong to the same tenant', @certificateTrigger) = 0
    THROW 52060, 'QS-0521 certificate reconciliation or Finance lineage gate is incomplete.', 1;

SELECT 'QS0521_RELEASE_GATE_PASS' AS Result,
       (SELECT COUNT(*) FROM dbo.__EFMigrationsHistory
        WHERE MigrationId IN (
            '20260811050412_AddQuantitySurveySubcontractLifecycle',
            '20260811061000_HardenQuantitySurveySubcontractCertificates')) AS AppliedMigrations,
       (SELECT COUNT(*) FROM sys.triggers t
        JOIN @expectedTriggers e ON e.Name = t.name
        WHERE t.is_disabled = 0) AS EnabledGovernanceTriggers,
       (SELECT COUNT(*) FROM dbo.Contracts c
        JOIN dbo.Tenders t ON t.Id = c.TenderId AND t.TenantId = c.TenantId AND t.IsDeleted = 0
        JOIN dbo.PurchaseRequisitions pr ON pr.Id = t.SourcePurchaseRequisitionId
            AND pr.TenantId = c.TenantId AND pr.IsDeleted = 0
        JOIN dbo.Projects p ON p.Id = pr.ProjectId AND p.TenantId = c.TenantId AND p.IsDeleted = 0
        WHERE c.IsDeleted = 0 AND c.ContractType = 'Works' AND c.Status = 'Active'
          AND c.AllowSubcontracting = 1) AS EligibleActiveWorksContracts;
