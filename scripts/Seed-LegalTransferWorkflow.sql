/*
Seeds the Legal workflow used when Property Management starts conveyance.

Target entity:
  Legal / LegalTransfer

Usage:
  1. Back up the database first.
  2. Set @TenantId or @TenantCode if you want one tenant only.
  3. Leave both NULL to seed all active tenants.
  4. Run in the RHEMA-ERP SQL Server database.

Notes:
  - This creates the published workflow definition required before opening
    a LegalTransfer procedure case.
  - Existing active published LegalTransfer workflows with the same nine
    transfer stages are left untouched.
  - Only older active system-created LegalTransfer workflow definitions are
    retired after the new baseline is created.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('tempdb..#LegalTransferWorkflowSeedSettings') IS NOT NULL
    DROP TABLE #LegalTransferWorkflowSeedSettings;

CREATE TABLE #LegalTransferWorkflowSeedSettings
(
    TenantId uniqueidentifier NULL,
    TenantCode nvarchar(50) NULL
);

/*
Edit only these two values:
  - Set TenantId for one exact tenant, or
  - Set TenantCode for one tenant code, or
  - Leave both NULL to seed all active tenants.
*/
INSERT INTO #LegalTransferWorkflowSeedSettings (TenantId, TenantCode)
VALUES (NULL, NULL);

GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TenantId uniqueidentifier;
DECLARE @TenantCode nvarchar(50);

SELECT TOP (1)
    @TenantId = TenantId,
    @TenantCode = TenantCode
FROM #LegalTransferWorkflowSeedSettings;

DECLARE @EntityCode nvarchar(50) = N'LegalTransfer';
DECLARE @EntityName nvarchar(100) = N'LegalTransfer';
DECLARE @DefinitionName nvarchar(100) = N'Legal Procedure - Transfers';
DECLARE @Description nvarchar(500) =
    N'Default Legal transfer workflow for conveyance, transfer-fee payment, execution, signatures, and Estate records return.';
DECLARE @Now datetime2 = SYSUTCDATETIME();

IF OBJECT_ID('tempdb..#TargetTenants') IS NOT NULL DROP TABLE #TargetTenants;
CREATE TABLE #TargetTenants (TenantId uniqueidentifier NOT NULL PRIMARY KEY);

INSERT INTO #TargetTenants (TenantId)
SELECT t.Id
FROM dbo.Tenants t
WHERE t.IsDeleted = 0
  AND t.Status = 1
  AND (@TenantId IS NULL OR t.Id = @TenantId)
  AND (@TenantCode IS NULL OR t.Code = @TenantCode);

IF NOT EXISTS (SELECT 1 FROM #TargetTenants)
    THROW 51000, 'No active tenant matched @TenantId/@TenantCode.', 1;

BEGIN TRANSACTION;

DECLARE tenant_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT TenantId FROM #TargetTenants;

DECLARE @Steps TABLE
(
    StepNo int NOT NULL PRIMARY KEY,
    StepId uniqueidentifier NOT NULL DEFAULT NEWID(),
    StepName nvarchar(100) NOT NULL,
    StepType int NOT NULL,
    RoleName nvarchar(100) NOT NULL,
    Instructions nvarchar(500) NOT NULL,
    ChecklistJson nvarchar(max) NOT NULL,
    FieldsJson nvarchar(max) NOT NULL,
    DocumentsJson nvarchar(max) NOT NULL
);

OPEN tenant_cursor;
FETCH NEXT FROM tenant_cursor INTO @TenantId;

WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @EntityTypeId uniqueidentifier;
    DECLARE @WorkflowDefinitionId uniqueidentifier;
    DECLARE @DefinitionKey uniqueidentifier;
    DECLARE @Version int;
    DECLARE @ResolvedDefinitionName nvarchar(100);

    SELECT @EntityTypeId = et.Id
    FROM dbo.WorkflowEntityTypes et
    WHERE et.TenantId = @TenantId
      AND et.IsDeleted = 0
      AND (et.Code = @EntityCode OR et.Name = @EntityCode);

    IF @EntityTypeId IS NULL
    BEGIN
        SET @EntityTypeId = NEWID();

        INSERT INTO dbo.WorkflowEntityTypes
            (Id, Code, Name, Description, EntityClassName, PropertySchema, IsActive, DisplayOrder,
             Icon, ColorCode, CreatedAt, CreatedBy, IsDeleted, TenantId)
        VALUES
            (@EntityTypeId, @EntityCode, @EntityName, @Description, NULL, NULL, 1, 128,
             N'ArrowRightLeft', N'#2563EB', @Now, N'System', 0, @TenantId);
    END
    ELSE
    BEGIN
        UPDATE dbo.WorkflowEntityTypes
        SET Name = @EntityName,
            Description = @Description,
            IsActive = 1,
            UpdatedAt = @Now,
            UpdatedBy = N'System'
        WHERE Id = @EntityTypeId;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.WorkflowDefinitions wd
        WHERE wd.TenantId = @TenantId
          AND wd.EntityTypeId = @EntityTypeId
          AND wd.IsDeleted = 0
          AND wd.IsActive = 1
          AND wd.LifecycleStatus = 1
          AND EXISTS (
              SELECT 1
              FROM dbo.WorkflowSteps ws
              WHERE ws.WorkflowDefinitionId = wd.Id
                AND ws.TenantId = @TenantId
                AND ws.IsDeleted = 0
                AND ws.Name = N'Head of Legal Minuting')
          AND EXISTS (
              SELECT 1
              FROM dbo.WorkflowSteps ws
              WHERE ws.WorkflowDefinitionId = wd.Id
                AND ws.TenantId = @TenantId
                AND ws.IsDeleted = 0
                AND ws.Name = N'Legal Admin Closeout')
          AND (
              SELECT COUNT(*)
              FROM dbo.WorkflowSteps ws
              WHERE ws.WorkflowDefinitionId = wd.Id
                AND ws.TenantId = @TenantId
                AND ws.IsDeleted = 0
          ) >= 9
    )
    BEGIN
        PRINT CONCAT('Tenant ', CONVERT(nvarchar(36), @TenantId), ': LegalTransfer workflow already exists. Skipped.');
        FETCH NEXT FROM tenant_cursor INTO @TenantId;
        CONTINUE;
    END;

    SET @WorkflowDefinitionId = NEWID();
    SET @DefinitionKey = @WorkflowDefinitionId;

    SELECT @Version = ISNULL(MAX(wd.Version), 0) + 1
    FROM dbo.WorkflowDefinitions wd
    WHERE wd.TenantId = @TenantId
      AND wd.EntityTypeId = @EntityTypeId
      AND wd.IsDeleted = 0
      AND wd.Name LIKE @DefinitionName + N'%';

    SET @ResolvedDefinitionName =
        CASE
            WHEN EXISTS (
                SELECT 1 FROM dbo.WorkflowDefinitions
                WHERE TenantId = @TenantId AND IsDeleted = 0 AND Name = @DefinitionName
            )
            THEN LEFT(CONCAT(@DefinitionName, N' v', @Version), 100)
            ELSE @DefinitionName
        END;

    INSERT INTO dbo.WorkflowDefinitions
        (Id, DefinitionKey, Name, Description, EntityTypeId, Version, LifecycleStatus, IsActive,
         ChangeSummary, SupersedesDefinitionId, PublishedAt, PublishedById, RetiredAt, RetiredById,
         Configuration, CreatedAt, CreatedBy, IsDeleted, TenantId)
    VALUES
        (@WorkflowDefinitionId, @DefinitionKey, @ResolvedDefinitionName, @Description, @EntityTypeId,
         @Version, 1, 1, N'Seeded LegalTransfer workflow for conveyance startup.', NULL,
         @Now, NULL, NULL, NULL, NULL, @Now, N'System', 0, @TenantId);

    DELETE FROM @Steps;

    INSERT INTO @Steps (StepNo, StepName, StepType, RoleName, Instructions, ChecklistJson, FieldsJson, DocumentsJson)
    VALUES
    (1, N'Head of Legal Minuting', 2, N'Head of Legal',
     N'Receive Estate file Minute payment requirement Assign Legal Admin',
     N'[{"id":"legaltransfer-1-check-1","name":"Receive Estate file","description":"Receive Estate file","isRequired":true},{"id":"legaltransfer-1-check-2","name":"Minute payment requirement","description":"Minute payment requirement","isRequired":true},{"id":"legaltransfer-1-check-3","name":"Assign Legal Admin","description":"Assign Legal Admin","isRequired":true}]',
     N'[{"name":"transferFeePayable","label":"transferFeePayable","fieldType":"Text","isRequired":false},{"name":"transferFeePaymentRequestReference","label":"transferFeePaymentRequestReference","fieldType":"Text","isRequired":false}]',
     N'[{"id":"legaltransfer-1-doc-1","requirementKey":"legaltransfer-1-doc-1","documentName":"Transfer file from Estate","documentType":"LegalProcedureEvidence","providedBy":"Estate / Property Management","appliesTo":"All","isRequired":true}]'),
    (2, N'Client Payment Call', 0, N'Legal Admin Assistant',
     N'Notify client Track 30-day payment window Capture payment evidence',
     N'[{"id":"legaltransfer-2-check-1","name":"Notify client","description":"Notify client","isRequired":true},{"id":"legaltransfer-2-check-2","name":"Track 30-day payment window","description":"Track 30-day payment window","isRequired":true},{"id":"legaltransfer-2-check-3","name":"Capture payment evidence","description":"Capture payment evidence","isRequired":true}]',
     N'[{"name":"clientPaymentDate","label":"clientPaymentDate","fieldType":"Text","isRequired":false},{"name":"paymentReceiptReference","label":"paymentReceiptReference","fieldType":"Text","isRequired":false},{"name":"transferFeeReceipt","label":"transferFeeReceipt","fieldType":"Text","isRequired":false}]',
     N'[{"id":"legaltransfer-2-doc-1","requirementKey":"legaltransfer-2-doc-1","documentName":"Transfer fee payment receipt","documentType":"LegalProcedureEvidence","providedBy":"Finance / Client","appliesTo":"All","isRequired":true}]'),
    (3, N'Transfer Drafting', 0, N'Legal Admin Assistant',
     N'Confirm payment Prepare transfer draft Send to Legal Officer',
     N'[{"id":"legaltransfer-3-check-1","name":"Confirm payment","description":"Confirm payment","isRequired":true},{"id":"legaltransfer-3-check-2","name":"Prepare transfer draft","description":"Prepare transfer draft","isRequired":true},{"id":"legaltransfer-3-check-3","name":"Send to Legal Officer","description":"Send to Legal Officer","isRequired":true}]',
     N'[{"name":"draftDocumentReference","label":"draftDocumentReference","fieldType":"Text","isRequired":false},{"name":"draftVersionStatus","label":"draftVersionStatus","fieldType":"Text","isRequired":false}]',
     N'[{"id":"legaltransfer-3-doc-1","requirementKey":"legaltransfer-3-doc-1","documentName":"Draft transfer form","documentType":"LegalProcedureEvidence","providedBy":"Legal Admin Assistant","appliesTo":"All","isRequired":true}]'),
    (4, N'Legal Vetting', 0, N'Legal Officer',
     N'Review parties Review property details Approve or return draft',
     N'[{"id":"legaltransfer-4-check-1","name":"Review parties","description":"Review parties","isRequired":true},{"id":"legaltransfer-4-check-2","name":"Review property details","description":"Review property details","isRequired":true},{"id":"legaltransfer-4-check-3","name":"Approve or return draft","description":"Approve or return draft","isRequired":true}]',
     N'[{"name":"legalVettingStatus","label":"legalVettingStatus","fieldType":"Text","isRequired":false},{"name":"vettingCommentStatus","label":"vettingCommentStatus","fieldType":"Text","isRequired":false}]',
     N'[]'),
    (5, N'Client Execution', 0, N'Legal Admin Assistant',
     N'Invite parties Capture signature date Attach executed copy',
     N'[{"id":"legaltransfer-5-check-1","name":"Invite parties","description":"Invite parties","isRequired":true},{"id":"legaltransfer-5-check-2","name":"Capture signature date","description":"Capture signature date","isRequired":true},{"id":"legaltransfer-5-check-3","name":"Attach executed copy","description":"Attach executed copy","isRequired":true}]',
     N'[{"name":"clientSignatureDate","label":"clientSignatureDate","fieldType":"Text","isRequired":false},{"name":"signatureStatus","label":"signatureStatus","fieldType":"Text","isRequired":false}]',
     N'[{"id":"legaltransfer-5-doc-1","requirementKey":"legaltransfer-5-doc-1","documentName":"Executed transfer form","documentType":"LegalProcedureEvidence","providedBy":"Client / Legal","appliesTo":"All","isRequired":true}]'),
    (6, N'Legal Officer Signature', 0, N'Legal Officer',
     N'Confirm execution Sign instrument Forward for LAA signature',
     N'[{"id":"legaltransfer-6-check-1","name":"Confirm execution","description":"Confirm execution","isRequired":true},{"id":"legaltransfer-6-check-2","name":"Sign instrument","description":"Sign instrument","isRequired":true},{"id":"legaltransfer-6-check-3","name":"Forward for LAA signature","description":"Forward for LAA signature","isRequired":true}]',
     N'[{"name":"legalOfficerSignatureDate","label":"legalOfficerSignatureDate","fieldType":"Text","isRequired":false}]',
     N'[]'),
    (7, N'Legal Admin Signature', 0, N'Legal Admin Assistant',
     N'Apply LAA signature Check execution pack Send to Head of Legal',
     N'[{"id":"legaltransfer-7-check-1","name":"Apply LAA signature","description":"Apply LAA signature","isRequired":true},{"id":"legaltransfer-7-check-2","name":"Check execution pack","description":"Check execution pack","isRequired":true},{"id":"legaltransfer-7-check-3","name":"Send to Head of Legal","description":"Send to Head of Legal","isRequired":true}]',
     N'[{"name":"legalAdminSignatureDate","label":"legalAdminSignatureDate","fieldType":"Text","isRequired":false}]',
     N'[]'),
    (8, N'Head of Legal Signature', 2, N'Head of Legal',
     N'Sign transfer Release for Legal Admin closeout',
     N'[{"id":"legaltransfer-8-check-1","name":"Sign transfer","description":"Sign transfer","isRequired":true},{"id":"legaltransfer-8-check-2","name":"Release for Legal Admin closeout","description":"Release for Legal Admin closeout","isRequired":true}]',
     N'[{"name":"headOfLegalSignatureDate","label":"headOfLegalSignatureDate","fieldType":"Text","isRequired":false},{"name":"signatureStatus","label":"signatureStatus","fieldType":"Text","isRequired":false}]',
     N'[]'),
    (9, N'Legal Admin Closeout', 0, N'Legal Admin Assistant',
     N'Distribute signed forms Return file to Estate Records Upload Estate return note',
     N'[{"id":"legaltransfer-9-check-1","name":"Distribute signed forms","description":"Distribute signed forms","isRequired":true},{"id":"legaltransfer-9-check-2","name":"Return file to Estate Records","description":"Return file to Estate Records","isRequired":true},{"id":"legaltransfer-9-check-3","name":"Upload Estate return note","description":"Upload Estate return note","isRequired":true}]',
     N'[{"name":"distributionStatus","label":"distributionStatus","fieldType":"Text","isRequired":false},{"name":"estateReturnStatus","label":"estateReturnStatus","fieldType":"Text","isRequired":false},{"name":"estateFileReturnDate","label":"estateFileReturnDate","fieldType":"Text","isRequired":false}]',
     N'[{"id":"legaltransfer-9-doc-1","requirementKey":"legaltransfer-9-doc-1","documentName":"Signed transfer distribution / Estate return note","documentType":"LegalProcedureEvidence","providedBy":"Legal Admin Assistant","appliesTo":"All","isRequired":true}]');

    INSERT INTO dbo.WorkflowSteps
        (Id, WorkflowDefinitionId, Name, Description, StepType, [Order], IsStartStep, IsEndStep,
         AssignmentType, AssignmentConfiguration, IsRequired, RequiredRole, EstimatedHours,
         Configuration, CreatedAt, CreatedBy, IsDeleted, TenantId)
    SELECT
        StepId,
        @WorkflowDefinitionId,
        StepName,
        Instructions,
        StepType,
        StepNo,
        CASE WHEN StepNo = 1 THEN 1 ELSE 0 END,
        CASE WHEN StepNo = 9 THEN 1 ELSE 0 END,
        N'Role',
        NULL,
        1,
        RoleName,
        NULL,
        CONCAT(
            N'{"qualityConfig":{"qualityChecks":', ChecklistJson,
            N'},"formFields":', FieldsJson,
            N',"taskConfig":{"taskActionType":"legal-procedure","requiresDocument":',
            CASE WHEN DocumentsJson = N'[]' THEN N'false' ELSE N'true' END,
            N',"instructions":',
            QUOTENAME(Instructions, '"'),
            N',"documentRequirements":', DocumentsJson,
            N'}',
            CASE WHEN StepType = 2 THEN CONCAT(
                N',"approvalConfig":{"approvalType":"Single","minApprovalsRequired":1,"rejectionHandling":"StopWorkflow","preventInitiatorApproval":false,"requireDistinctApprovers":false,"approverRules":[{"assignmentType":"Role","role":',
                QUOTENAME(RoleName, '"'),
                N'}]}'
            ) ELSE N'' END,
            N'}'
        ),
        @Now,
        N'System',
        0,
        @TenantId
    FROM @Steps;

    INSERT INTO dbo.WorkflowTransitions
        (Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, Description, Condition,
         IsDefault, Priority, CreatedAt, CreatedBy, IsDeleted, TenantId)
    SELECT
        NEWID(),
        @WorkflowDefinitionId,
        currentStep.StepId,
        nextStep.StepId,
        CASE
            WHEN nextStep.StepType = 2 THEN N'Submit for approval'
            WHEN currentStep.StepNo = 8 THEN N'Close'
            ELSE N'Complete step'
        END,
        NULL,
        NULL,
        1,
        0,
        @Now,
        N'System',
        0,
        @TenantId
    FROM @Steps currentStep
    JOIN @Steps nextStep ON nextStep.StepNo = currentStep.StepNo + 1;

    UPDATE wd
    SET IsActive = 0,
        LifecycleStatus = 2,
        RetiredAt = COALESCE(wd.RetiredAt, @Now),
        UpdatedAt = @Now,
        UpdatedBy = N'System'
    FROM dbo.WorkflowDefinitions wd
    WHERE wd.TenantId = @TenantId
      AND wd.EntityTypeId = @EntityTypeId
      AND wd.Id <> @WorkflowDefinitionId
      AND wd.IsDeleted = 0
      AND wd.IsActive = 1
      AND wd.CreatedBy = N'System'
      AND wd.Name LIKE @DefinitionName + N'%';

    PRINT CONCAT('Tenant ', CONVERT(nvarchar(36), @TenantId), ': seeded ', @ResolvedDefinitionName, '.');

    FETCH NEXT FROM tenant_cursor INTO @TenantId;
END;

CLOSE tenant_cursor;
DEALLOCATE tenant_cursor;

COMMIT TRANSACTION;

SELECT
    t.Code AS TenantCode,
    wd.Id AS WorkflowDefinitionId,
    wd.Name,
    wd.Version,
    wd.IsActive,
    wd.LifecycleStatus,
    COUNT(ws.Id) AS StepCount
FROM dbo.WorkflowDefinitions wd
JOIN dbo.WorkflowEntityTypes et ON et.Id = wd.EntityTypeId
JOIN dbo.Tenants t ON t.Id = wd.TenantId
LEFT JOIN dbo.WorkflowSteps ws ON ws.WorkflowDefinitionId = wd.Id AND ws.IsDeleted = 0
WHERE et.Code = N'LegalTransfer'
  AND wd.IsDeleted = 0
  AND wd.Name LIKE N'Legal Procedure - Transfers%'
GROUP BY t.Code, wd.Id, wd.Name, wd.Version, wd.IsActive, wd.LifecycleStatus
ORDER BY t.Code, wd.IsActive DESC, wd.Version DESC;

IF OBJECT_ID('tempdb..#TargetTenants') IS NOT NULL
    DROP TABLE #TargetTenants;

IF OBJECT_ID('tempdb..#LegalTransferWorkflowSeedSettings') IS NOT NULL
    DROP TABLE #LegalTransferWorkflowSeedSettings;

GO
