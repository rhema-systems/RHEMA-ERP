SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @ProjectId uniqueidentifier = '3A52185F-5EA6-4C76-ACD0-291FD7221C88';
DECLARE @ContractId uniqueidentifier = 'CB8AF66A-941E-4DD1-8FBD-D05F7C0CB681';
DECLARE @BoqVersionId uniqueidentifier = '47EC31FE-A650-4892-93D3-6615E1FDFA82';
DECLARE @ValuationId uniqueidentifier = '1BF45471-16A6-4F43-B1AB-D7953DE4ECF2';
DECLARE @MakerId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Users WHERE TenantId = @TenantId AND UserName = 'employee');
DECLARE @CheckerId uniqueidentifier = (SELECT TOP (1) Id FROM dbo.Users WHERE TenantId = @TenantId AND UserName = 'manager');

IF @MakerId IS NULL OR @CheckerId IS NULL OR @MakerId = @CheckerId
    THROW 52990, 'QS acceptance requires distinct active employee maker and manager checker identities.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.UserRoles ur
    JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
    JOIN dbo.RolePermissions rp ON rp.RoleId = r.Id
    JOIN dbo.Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
    WHERE ur.UserId = @MakerId AND p.Name = 'quantity-survey.valuations.manage')
    THROW 52990, 'QS maker does not have the valuation-management permission.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.UserRoles ur
    JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
    JOIN dbo.Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
    WHERE ur.UserId = @MakerId AND p.Name = 'quantity-survey.transactions.approve')
    THROW 52990, 'QS maker incorrectly has the transaction-approval permission.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.UserRoles ur
    JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
    JOIN dbo.Permissions p ON p.Id = rp.PermissionId AND p.IsDeleted = 0
    WHERE ur.UserId = @CheckerId AND p.Name = 'quantity-survey.transactions.approve')
    THROW 52990, 'QS checker does not have the transaction-approval permission.', 1;

DECLARE @ProfileId uniqueidentifier = (
    SELECT TOP (1) Id
    FROM dbo.QuantitySurveyConfigurationProfiles
    WHERE TenantId = @TenantId
      AND ProfileCode = 'TDC-QUANTITY-SURVEY'
      AND LifecycleStatus = 1
      AND IsDeleted = 0
      AND EffectiveFrom <= @Now
      AND (EffectiveTo IS NULL OR EffectiveTo >= @Now)
    ORDER BY Version DESC);

IF @ProfileId IS NULL
    THROW 52990, 'No effective Published QS configuration profile exists.', 1;

IF (SELECT COUNT(*) FROM dbo.QuantitySurveyConfigurationDecisions
    WHERE TenantId = @TenantId AND ProfileId = @ProfileId AND IsDeleted = 0
      AND Status = 2 AND ApprovalStatus = 1 AND EvidenceStatus = 2
      AND EffectiveFrom <= @Now AND (EffectiveTo IS NULL OR EffectiveTo >= @Now)) <> 17
    THROW 52990, 'The effective QS profile does not have exactly 17 approved, verified decisions.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Projects WHERE Id = @ProjectId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52990, 'The governed QS acceptance project is unavailable.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Contracts WHERE Id = @ContractId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52990, 'The governed Works contract is unavailable or belongs to another tenant.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.ProjectBoqVersions
    WHERE Id = @BoqVersionId AND TenantId = @TenantId AND ProjectId = @ProjectId
      AND Status = 'Approved' AND PublishedAt IS NOT NULL AND IsDeleted = 0)
    THROW 52990, 'The governed approved BoQ version is unavailable.', 1;

IF (SELECT COUNT(*) FROM dbo.ProjectBoqVersionLines
    WHERE TenantId = @TenantId AND ProjectId = @ProjectId
      AND ProjectBoqVersionId = @BoqVersionId AND ItemType = 'Item' AND IsDeleted = 0) <> 14
    THROW 52990, 'The acceptance BoQ does not contain the expected 14 controlled item lines.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.ProjectInterimValuations
    WHERE Id = @ValuationId AND TenantId = @TenantId AND ProjectId = @ProjectId
      AND ContractId = @ContractId AND Status = 'Draft' AND IsDeleted = 0)
    THROW 52990, 'The governed Draft valuation source is unavailable.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.QuantitySurveyValuationWorksheets value
    LEFT JOIN dbo.Projects project ON project.Id = value.ProjectId
    LEFT JOIN dbo.ProjectInterimValuations valuation ON valuation.Id = value.ProjectInterimValuationId
    WHERE value.IsDeleted = 0
      AND (project.Id IS NULL OR valuation.Id IS NULL OR project.TenantId <> value.TenantId
           OR valuation.TenantId <> value.TenantId OR valuation.ProjectId <> value.ProjectId))
    THROW 52990, 'A QS valuation worksheet has invalid tenant/project lineage.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.ProjectPaymentCertificates certificate
    LEFT JOIN dbo.VendorInvoice invoice ON invoice.Id = certificate.VendorInvoiceId
    WHERE certificate.IsDeleted = 0 AND certificate.QuantitySurveyValuationWorksheetId IS NOT NULL
      AND certificate.VendorInvoiceId IS NOT NULL
      AND (invoice.Id IS NULL OR invoice.IsDeleted = 1 OR invoice.TenantId <> certificate.TenantId
           OR ABS(invoice.TotalAmount - certificate.NetCertifiedAmount) > 0.01
           OR invoice.PaidAmount < 0 OR invoice.PaidAmount > invoice.TotalAmount + 0.01))
    THROW 52990, 'A governed QS certificate does not reconcile to its Finance-owned AP invoice.', 1;

IF EXISTS (
    SELECT Reference
    FROM dbo.VendorInvoice
    WHERE IsDeleted = 0 AND Reference LIKE 'QS-CERT:%'
    GROUP BY TenantId, Reference
    HAVING COUNT(*) > 1)
    THROW 52990, 'Duplicate Finance AP invoices exist for a governed QS certificate reference.', 1;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
    THROW 52990, 'The configured database has a disabled or untrusted foreign key.', 1;

IF EXISTS (
    SELECT 1
    FROM sys.triggers
    WHERE parent_class = 1
      AND is_disabled = 1
      AND OBJECT_NAME(parent_id) LIKE 'QuantitySurvey%')
    THROW 52990, 'A QS SQL lifecycle trigger is disabled.', 1;

SELECT
    'QS_PHASES_0_6_ACCEPTANCE_READINESS_PASS' AS Result,
    @TenantId AS TenantId,
    @ProjectId AS ProjectId,
    @ContractId AS ContractId,
    @BoqVersionId AS ApprovedBoqVersionId,
    @ValuationId AS DraftInterimValuationId,
    @MakerId AS MakerUserId,
    @CheckerId AS CheckerUserId,
    17 AS ApprovedDecisionCount,
    14 AS ApprovedBoqItemCount;
