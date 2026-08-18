SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT OFF;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = '20260811043000_ExtendWorksContractCommercialTerms')
    THROW 51950, 'QS-0520 migration is not applied.', 1;

IF (SELECT COUNT(*) FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Contracts')
      AND name IN ('AllowSectionalTakeover','AllowSubcontracting','ClaimClause','ClaimNoticePeriodDays',
                   'CommercialTermsClientRequestId','CommercialTermsConfiguredAt','CommercialTermsConfiguredById',
                   'CommercialTermsConfigurationProfileId','CommercialTermsContractDocumentId','CommercialTermsPolicyHash',
                   'CommercialTermsRequestHash','ContingencyAmount','ContractControlsDecisionId','DefectsLiabilityDays',
                   'PaymentTermId','ProvisionalSumAmount','RetentionClause','RetentionDecisionId','SectionalTakeoverClause',
                   'SubcontractPaymentTermId','SubcontractTerms')) <> 21
    THROW 51951, 'QS-0520 contract columns are incomplete.', 1;

IF (SELECT COUNT(*) FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Contracts')
      AND name IN ('FK_Contracts_PaymentTerms_PaymentTermId','FK_Contracts_PaymentTerms_SubcontractPaymentTermId',
                   'FK_Contracts_ContractDocuments_CommercialTermsContractDocumentId',
                   'FK_Contracts_QuantitySurveyConfigurationProfiles_CommercialTermsConfigurationProfileId',
                   'FK_Contracts_QuantitySurveyConfigurationDecisions_ContractControlsDecisionId',
                   'FK_Contracts_QuantitySurveyConfigurationDecisions_RetentionDecisionId',
                   'FK_Contracts_Users_CommercialTermsConfiguredById')
      AND is_disabled = 0 AND is_not_trusted = 0) <> 7
    THROW 51952, 'QS-0520 foreign keys are missing, disabled, or untrusted.', 1;

IF (SELECT COUNT(*) FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Contracts')
      AND name LIKE 'CK_Contracts_QS0520_%'
      AND is_disabled = 0 AND is_not_trusted = 0) <> 3
    THROW 51953, 'QS-0520 check constraints are missing, disabled, or untrusted.', 1;

IF (SELECT COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Contracts')
      AND name IN ('IX_Contracts_PaymentTermId','IX_Contracts_SubcontractPaymentTermId',
                   'IX_Contracts_CommercialTermsContractDocumentId','IX_Contracts_CommercialTermsConfigurationProfileId',
                   'IX_Contracts_ContractControlsDecisionId','IX_Contracts_RetentionDecisionId',
                   'IX_Contracts_CommercialTermsConfiguredById','IX_Contracts_TenantId_CommercialTermsClientRequestId')) <> 8
    THROW 51954, 'QS-0520 controlled-master or idempotency indexes are incomplete.', 1;

DECLARE @trigger nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_Contracts_QS0520CommercialTerms'));
IF @trigger IS NULL OR OBJECTPROPERTY(OBJECT_ID(N'dbo.TR_Contracts_QS0520CommercialTerms'), 'ExecIsTriggerDisabled') <> 0
    THROW 51955, 'QS-0520 governance trigger is missing or disabled.', 1;
IF CHARINDEX('SESSION_CONTEXT(N''qs_contract_terms_id'')', @trigger) = 0 OR
   CHARINDEX('governed QS service', @trigger) = 0 OR
   CHARINDEX('reclassified to or from Works', @trigger) = 0 OR
   CHARINDEX('u.IsActive = 1', @trigger) = 0 OR
   CHARINDEX('AND u.IsDeleted', @trigger) > 0
    THROW 51956, 'QS-0520 governance trigger does not match the service capability or identity contract.', 1;

DECLARE @configuredDraft uniqueidentifier = (
    SELECT TOP (1) Id FROM dbo.Contracts
    WHERE IsDeleted = 0 AND ContractType = 'Works' AND Status = 'Draft'
      AND CommercialTermsPolicyHash IS NOT NULL
    ORDER BY CreatedAt);
IF @configuredDraft IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.Contracts SET RetentionPercentage = RetentionPercentage + 0.01
        WHERE Id = @configuredDraft;
        ROLLBACK TRANSACTION;
        THROW 51957, 'QS-0520 direct governed mutation was unexpectedly allowed.', 1;
    END TRY
    BEGIN CATCH
        DECLARE @mutationError int = ERROR_NUMBER();
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF @mutationError <> 51944 THROW;
    END CATCH;
END;

DECLARE @nonWorks uniqueidentifier, @originalType nvarchar(100);
SELECT TOP (1) @nonWorks = Id, @originalType = ContractType
FROM dbo.Contracts WHERE IsDeleted = 0 AND ContractType <> 'Works' ORDER BY CreatedAt;
IF @nonWorks IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.Contracts SET ContractType = 'Works' WHERE Id = @nonWorks;
        ROLLBACK TRANSACTION;
        THROW 51958, 'QS-0520 reclassification into Works was unexpectedly allowed.', 1;
    END TRY
    BEGIN CATCH
        DECLARE @typeError int = ERROR_NUMBER();
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        IF @typeError <> 51943 THROW;
    END CATCH;
END;

SELECT 'QS0520_RELEASE_GATE_PASS' AS Result,
       (SELECT COUNT(*) FROM dbo.Contracts WHERE IsDeleted = 0 AND ContractType = 'Works') AS WorksContracts,
       (SELECT COUNT(*) FROM dbo.QuantitySurveyConfigurationProfiles
        WHERE IsDeleted = 0 AND LifecycleStatus = 1 AND PublishedAt IS NOT NULL
          AND EffectiveFrom <= SYSUTCDATETIME() AND (EffectiveTo IS NULL OR EffectiveTo >= SYSUTCDATETIME())) AS EffectiveProfiles,
       (SELECT COUNT(*) FROM dbo.PaymentTerms
        WHERE IsDeleted = 0 AND IsActive = 1 AND ApplicableTo IN ('All','Supplier','Contractor')) AS ControlledPaymentTerms;
