/*
    Repair portal visibility for one Sales-to-Estate property request.

    The portal dashboard displays BusinessPartners.PartnerCode as the
    "Business Partner Account Number", but My Property Requests requires the
    separate BusinessPartners.CustomerAccountNumber column to be populated.

    This script targets only:
      - Business Partner 43a92487-f256-476e-9035-b043b3a0b6fb (ABC Ltd)
      - Estate case EST-PM-20261007-0001

    Run with @PreviewOnly = 1 first. If the snapshots are correct, set it to 0
    and rerun. No workflow stage or agreement status is changed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @BusinessPartnerId uniqueidentifier = '43a92487-f256-476e-9035-b043b3a0b6fb';
DECLARE @EstateCaseReference nvarchar(80) = N'EST-PM-20261007-0001';
DECLARE @ExpectedPartnerCode nvarchar(50) = N'CUS260001';
DECLARE @Script nvarchar(100) = N'DB portal customer account repair';

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run this script outside an existing transaction.', 16, 1);
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TenantId uniqueidentifier;
    DECLARE @CaseId uniqueidentifier;
    DECLARE @ActorId uniqueidentifier;
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @PartnerMatches int;
    DECLARE @CaseMatches int;
    DECLARE @LinkedPortalUsers int;
    DECLARE @CurrentCustomerAccountNumber nvarchar(50);

    SELECT @PartnerMatches = COUNT(*)
    FROM dbo.BusinessPartners WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @BusinessPartnerId
      AND IsDeleted = 0;

    IF @PartnerMatches <> 1
        RAISERROR('Expected exactly one active Business Partner with the supplied ID.', 16, 1);

    SELECT
        @TenantId = TenantId,
        @CurrentCustomerAccountNumber = NULLIF(LTRIM(RTRIM(CustomerAccountNumber)), N'')
    FROM dbo.BusinessPartners
    WHERE Id = @BusinessPartnerId
      AND IsDeleted = 0;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.BusinessPartners
        WHERE Id = @BusinessPartnerId
          AND TenantId = @TenantId
          AND IsDeleted = 0
          AND PartnerCode = @ExpectedPartnerCode
          AND PartnerName = N'ABC Ltd')
        RAISERROR('The Business Partner does not match ABC Ltd / CUS260001.', 16, 1);

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.BusinessPartners
        WHERE Id = @BusinessPartnerId
          AND TenantId = @TenantId
          AND IsDeleted = 0
          AND IsActive = 1
          AND ApprovalStatus = N'Approved'
          AND PartnerType IN (N'Customer', N'Both', N'Customer & Supplier'))
        RAISERROR('ABC Ltd is not an active, approved, customer-capable Business Partner.', 16, 1);

    SELECT @LinkedPortalUsers =
        CASE WHEN bp.UserId IS NULL THEN 0 ELSE 1 END
        + (SELECT COUNT(*)
           FROM dbo.BusinessPartnerUsers bpu
           WHERE bpu.TenantId = bp.TenantId
             AND bpu.BusinessPartnerId = bp.Id
             AND bpu.IsDeleted = 0
             AND bpu.IsActive = 1)
    FROM dbo.BusinessPartners bp
    WHERE bp.Id = @BusinessPartnerId
      AND bp.TenantId = @TenantId
      AND bp.IsDeleted = 0;

    IF ISNULL(@LinkedPortalUsers, 0) = 0
        RAISERROR('ABC Ltd is not linked to any portal user. No change was made.', 16, 1);

    IF @CurrentCustomerAccountNumber IS NOT NULL
       AND @CurrentCustomerAccountNumber <> @ExpectedPartnerCode
        RAISERROR('ABC Ltd already has a different CustomerAccountNumber. No change was made.', 16, 1);

    IF EXISTS (
        SELECT 1
        FROM dbo.BusinessPartners
        WHERE TenantId = @TenantId
          AND Id <> @BusinessPartnerId
          AND IsDeleted = 0
          AND CustomerAccountNumber = @ExpectedPartnerCode)
        RAISERROR('CUS260001 is already assigned as another Business Partner customer account number.', 16, 1);

    SELECT @CaseMatches = COUNT(*)
    FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND EntityType = N'EstatePropertyManagementListingApplication'
      AND SourceDepartment = N'Sales - Estate Enquiry'
      AND ReferenceNumber = @EstateCaseReference;

    IF @CaseMatches <> 1
        RAISERROR('Expected exactly one Sales-to-Estate property case with reference EST-PM-20261007-0001.', 16, 1);

    SELECT
        @CaseId = Id,
        @ActorId = COALESCE(LastActionById, OpenedById, CreatedById)
    FROM dbo.ProcedureCases
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND EntityType = N'EstatePropertyManagementListingApplication'
      AND SourceDepartment = N'Sales - Estate Enquiry'
      AND ReferenceNumber = @EstateCaseReference;

    IF @ActorId IS NULL
        RAISERROR('The Estate case has no user available for the audit activity.', 16, 1);

    IF (SELECT COUNT(*)
        FROM dbo.ProcedureCaseFields WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId
          AND ProcedureCaseId = @CaseId
          AND IsDeleted = 0
          AND [Key] = N'sourceReference'
          AND Value = CONVERT(nvarchar(36), @BusinessPartnerId)) <> 1
        RAISERROR('The Estate case sourceReference does not uniquely identify ABC Ltd.', 16, 1);

    SELECT
        N'Before' AS Snapshot,
        bp.Id AS BusinessPartnerId,
        bp.PartnerCode,
        bp.CustomerAccountNumber,
        bp.PartnerName,
        bp.PartnerType,
        bp.ApprovalStatus,
        bp.IsActive,
        bp.UserId,
        @LinkedPortalUsers AS LinkedPortalUsers,
        pc.Id AS EstateCaseId,
        pc.ReferenceNumber AS EstateCaseReference,
        sourceField.Value AS SourceReference,
        accountField.Value AS CaseCustomerAccountReference
    FROM dbo.BusinessPartners bp
    JOIN dbo.ProcedureCases pc
      ON pc.Id = @CaseId
     AND pc.TenantId = bp.TenantId
    JOIN dbo.ProcedureCaseFields sourceField
      ON sourceField.ProcedureCaseId = pc.Id
     AND sourceField.TenantId = pc.TenantId
     AND sourceField.IsDeleted = 0
     AND sourceField.[Key] = N'sourceReference'
    LEFT JOIN dbo.ProcedureCaseFields accountField
      ON accountField.ProcedureCaseId = pc.Id
     AND accountField.TenantId = pc.TenantId
     AND accountField.IsDeleted = 0
     AND accountField.[Key] = N'customerAccountReference'
    WHERE bp.Id = @BusinessPartnerId
      AND bp.TenantId = @TenantId
      AND bp.IsDeleted = 0;

    UPDATE dbo.BusinessPartners
    SET CustomerAccountNumber = @ExpectedPartnerCode,
        UpdatedAt = @Now,
        UpdatedBy = @Script,
        LastModifiedById = @ActorId
    WHERE Id = @BusinessPartnerId
      AND TenantId = @TenantId
      AND IsDeleted = 0;

    IF @@ROWCOUNT <> 1
        RAISERROR('ABC Ltd was not updated exactly once.', 16, 1);

    UPDATE dbo.ProcedureCaseFields
    SET Value = @ExpectedPartnerCode,
        UpdatedAt = @Now,
        UpdatedBy = @Script,
        LastModifiedById = @ActorId
    WHERE TenantId = @TenantId
      AND ProcedureCaseId = @CaseId
      AND IsDeleted = 0
      AND [Key] = N'customerAccountReference';

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO dbo.ProcedureCaseFields
            (Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
             CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
             IsDeleted, DeletedAt, DeletedBy, TenantId)
        VALUES
            (NEWID(), @CaseId, N'customerAccountReference', N'Customer account reference',
             N'text', @ExpectedPartnerCode, NULL,
             @Now, NULL, @Script, NULL, @ActorId, NULL,
             0, NULL, NULL, @TenantId);
    END;

    INSERT INTO dbo.ProcedureCaseActivities
        (Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    VALUES
        (NEWID(), @CaseId, N'Customer portal account repaired', N'Management decision',
         N'Backfilled ABC Ltd CustomerAccountNumber and the Estate case customer account reference so the Sales handover is visible in My Property Requests.',
         @ActorId, @Now, @Now, NULL, @Script, NULL, NULL, NULL,
         0, NULL, NULL, @TenantId);

    SELECT
        N'After (committed only when PreviewOnly = 0)' AS Snapshot,
        bp.Id AS BusinessPartnerId,
        bp.PartnerCode,
        bp.CustomerAccountNumber,
        bp.PartnerName,
        bp.PartnerType,
        bp.ApprovalStatus,
        bp.IsActive,
        bp.UserId,
        @LinkedPortalUsers AS LinkedPortalUsers,
        pc.Id AS EstateCaseId,
        pc.ReferenceNumber AS EstateCaseReference,
        sourceField.Value AS SourceReference,
        accountField.Value AS CaseCustomerAccountReference
    FROM dbo.BusinessPartners bp
    JOIN dbo.ProcedureCases pc
      ON pc.Id = @CaseId
     AND pc.TenantId = bp.TenantId
    JOIN dbo.ProcedureCaseFields sourceField
      ON sourceField.ProcedureCaseId = pc.Id
     AND sourceField.TenantId = pc.TenantId
     AND sourceField.IsDeleted = 0
     AND sourceField.[Key] = N'sourceReference'
    LEFT JOIN dbo.ProcedureCaseFields accountField
      ON accountField.ProcedureCaseId = pc.Id
     AND accountField.TenantId = pc.TenantId
     AND accountField.IsDeleted = 0
     AND accountField.[Key] = N'customerAccountReference'
    WHERE bp.Id = @BusinessPartnerId
      AND bp.TenantId = @TenantId
      AND bp.IsDeleted = 0;

    IF @PreviewOnly = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'Preview rolled back. Set @PreviewOnly = 0 and rerun to apply the portal account repair.' AS Result;
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Committed. Sign out and back into the external portal, then open My Property Requests.' AS Result;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @Error nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR('%s', 16, 1, @Error);
END CATCH;
