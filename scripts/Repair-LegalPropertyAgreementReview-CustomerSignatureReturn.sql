/*
    Repairs the Legal Property Agreement Review workflow on an existing server database.

    What it does:
    1. Adds/repairs the "Customer Signature Return" stage between "Agreement Vetting"
       and "Head of Legal Signature".
    2. Makes "Head of Legal Signature" an approval stage with an electronic signature
       attestation policy.
    3. Moves the current live LegalPropertyAgreementReview case back from
       "Head of Legal Signature" to "Customer Signature Return" when @MoveCurrentCase = 1.
    4. Marks the linked Estate case agreement signing location as "Legal" so the
       customer-signature return and Head of Legal e-signature happen in Legal.

    Usage:
    - Keep @PreviewOnly = 1 first and run the script to inspect the output.
    - Set @PreviewOnly = 0 and run again to commit.
    - If the server has more than one active LegalPropertyAgreementReview case, set either
      @CaseId or @CaseReference before running.
*/

SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @TenantCode nvarchar(50) = NULL;
DECLARE @TenantId uniqueidentifier = NULL;
DECLARE @CaseId uniqueidentifier = NULL;
DECLARE @CaseReference nvarchar(80) = NULL;
DECLARE @MoveCurrentCase bit = 1;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @SystemUser nvarchar(max) = N'Repair-LegalPropertyAgreementReview-CustomerSignatureReturn.sql';

BEGIN TRANSACTION;

IF @TenantId IS NULL AND @TenantCode IS NOT NULL
BEGIN
    SELECT @TenantId = t.Id
    FROM dbo.Tenants t
    WHERE t.IsDeleted = 0 AND t.Code = @TenantCode;
END;

IF @TenantId IS NULL
BEGIN
    IF (SELECT COUNT(*) FROM dbo.Tenants WHERE IsDeleted = 0 AND Status = 1) <> 1
    BEGIN
        RAISERROR('Set @TenantCode or @TenantId because the database does not have exactly one active tenant.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    SELECT @TenantId = t.Id
    FROM dbo.Tenants t
    WHERE t.IsDeleted = 0 AND t.Status = 1;
END;

DECLARE @EntityTypeId uniqueidentifier;
SELECT TOP (1) @EntityTypeId = et.Id
FROM dbo.WorkflowEntityTypes et
WHERE et.TenantId = @TenantId
  AND et.IsDeleted = 0
  AND (et.Code = N'LegalPropertyAgreementReview' OR et.Name = N'LegalPropertyAgreementReview');

IF @EntityTypeId IS NULL
BEGIN
    RAISERROR('Workflow entity type LegalPropertyAgreementReview was not found for this tenant.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @WorkflowDefinitionId uniqueidentifier;
SELECT TOP (1) @WorkflowDefinitionId = wd.Id
FROM dbo.WorkflowDefinitions wd
WHERE wd.TenantId = @TenantId
  AND wd.IsDeleted = 0
  AND wd.EntityTypeId = @EntityTypeId
  AND wd.IsActive = 1
  AND wd.LifecycleStatus = 1
ORDER BY wd.Version DESC, wd.PublishedAt DESC, wd.CreatedAt DESC;

IF @WorkflowDefinitionId IS NULL
BEGIN
    RAISERROR('No active published LegalPropertyAgreementReview workflow definition was found.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @LegalIntakeStepId uniqueidentifier;
DECLARE @AgreementStepId uniqueidentifier;
DECLARE @CustomerStepId uniqueidentifier;
DECLARE @HeadStepId uniqueidentifier;
DECLARE @AgreementOrder int;

SELECT @LegalIntakeStepId = Id
FROM dbo.WorkflowSteps
WHERE TenantId = @TenantId AND WorkflowDefinitionId = @WorkflowDefinitionId AND IsDeleted = 0 AND Name = N'Legal Intake';

SELECT @AgreementStepId = Id, @AgreementOrder = [Order]
FROM dbo.WorkflowSteps
WHERE TenantId = @TenantId AND WorkflowDefinitionId = @WorkflowDefinitionId AND IsDeleted = 0 AND Name = N'Agreement Vetting';

SELECT @CustomerStepId = Id
FROM dbo.WorkflowSteps
WHERE TenantId = @TenantId AND WorkflowDefinitionId = @WorkflowDefinitionId AND IsDeleted = 0 AND Name = N'Customer Signature Return';

SELECT @HeadStepId = Id
FROM dbo.WorkflowSteps
WHERE TenantId = @TenantId AND WorkflowDefinitionId = @WorkflowDefinitionId AND IsDeleted = 0 AND Name = N'Head of Legal Signature';

IF @AgreementStepId IS NULL OR @HeadStepId IS NULL
BEGIN
    RAISERROR('Agreement Vetting and Head of Legal Signature must exist before this repair can run.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END;

DECLARE @CustomerStepConfig nvarchar(max) =
N'{
  "qualityConfig": {
    "qualityChecks": [
      {"id":"CUSTOMERSIGNATURERETURN-CHECK-1","name":"Confirm agreement is released to the customer portal","description":"Confirm agreement is released to the customer portal","isRequired":true,"requiresDocument":false},
      {"id":"CUSTOMERSIGNATURERETURN-CHECK-2","name":"Confirm customer signed agreement upload","description":"Confirm customer signed agreement upload","isRequired":true,"requiresDocument":false},
      {"id":"CUSTOMERSIGNATURERETURN-CHECK-3","name":"Forward returned agreement to Head of Legal","description":"Forward returned agreement to Head of Legal","isRequired":true,"requiresDocument":false}
    ]
  },
  "formFields": [],
  "taskConfig": {
    "taskActionType":"legal-property-agreement-review",
    "requiresDocument":false,
    "instructions":"Complete the Customer Signature Return task for the property agreement review workflow.",
    "documentRequirements":[
      {"id":"CUSTOMERSIGNATURERETURN-DOC-1","requirementKey":"CUSTOMERSIGNATURERETURN-DOC-1","documentName":"Customer signed agreement","documentType":"LegalAgreementReviewEvidence","isRequired":true}
    ]
  }
}';

DECLARE @HeadStepConfig nvarchar(max) =
N'{
  "approvalConfig": {
    "approvalType":"Single",
    "activationMode":"Parallel",
    "approverRules":[{"approvalGroup":1,"assignmentType":"Role","role":"Head of Legal","priority":0}],
    "minApprovalsRequired":1,
    "rejectionHandling":"StopWorkflow",
    "preventInitiatorApproval":false,
    "requireDistinctApprovers":false,
    "conflictRules":[],
    "signaturePolicy":{
      "isRequired":true,
      "method":"Attestation",
      "requiredSigningRole":"Head of Legal",
      "requireValidCertificateChain":false,
      "attestationText":"I confirm that I reviewed the customer-signed agreement and apply the Head of Legal electronic signature."
    },
    "evidenceRequirements":[],
    "allowEvidenceException":false,
    "evidenceExceptionApproverRole":"Managing Director",
    "minimumExceptionReasonLength":30,
    "requiresManagingDirectorApproval":false,
    "managingDirectorApproverRole":"Managing Director"
  },
  "qualityConfig": {
    "qualityChecks": [
      {"id":"HEADOFLEGALSIGNATURE-CHECK-1","name":"Confirm customer signed agreement","description":"Confirm customer signed agreement","isRequired":true,"requiresDocument":false},
      {"id":"HEADOFLEGALSIGNATURE-CHECK-2","name":"Apply Head of Legal signature","description":"Apply Head of Legal signature","isRequired":true,"requiresDocument":false},
      {"id":"HEADOFLEGALSIGNATURE-CHECK-3","name":"Return final signed agreement to Property Management","description":"Return final signed agreement to Property Management","isRequired":true,"requiresDocument":false}
    ]
  },
  "formFields": [],
  "taskConfig": {
    "taskActionType":"legal-property-agreement-review",
    "requiresDocument":false,
    "instructions":"Complete the Head of Legal Signature task for the property agreement review workflow.",
    "documentRequirements":[
      {"id":"HEADOFLEGALSIGNATURE-DOC-1","requirementKey":"HEADOFLEGALSIGNATURE-DOC-1","documentName":"Head of Legal signed agreement","documentType":"LegalAgreementReviewEvidence","isRequired":true}
    ]
  }
}';

IF @CustomerStepId IS NULL
BEGIN
    SET @CustomerStepId = NEWID();

    UPDATE dbo.WorkflowSteps
    SET [Order] = [Order] + 1,
        UpdatedAt = @Now,
        UpdatedBy = @SystemUser
    WHERE TenantId = @TenantId
      AND WorkflowDefinitionId = @WorkflowDefinitionId
      AND IsDeleted = 0
      AND [Order] > @AgreementOrder;

    INSERT INTO dbo.WorkflowSteps
    (
        Id, WorkflowDefinitionId, Name, Description, StepType, [Order],
        IsStartStep, IsEndStep, AssignmentType, AssignmentConfiguration,
        IsRequired, RequiredRole, EstimatedHours, Configuration,
        CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
        IsDeleted, DeletedAt, DeletedBy, TenantId
    )
    VALUES
    (
        @CustomerStepId, @WorkflowDefinitionId, N'Customer Signature Return',
        N'The vetted agreement is available in the customer portal and Legal waits for the customer-signed upload.',
        0, @AgreementOrder + 1,
        0, 0, N'Role', NULL,
        1, N'Legal Admin Assistant', NULL, @CustomerStepConfig,
        @Now, NULL, @SystemUser, NULL, NULL, NULL,
        0, NULL, NULL, @TenantId
    );
END
ELSE
BEGIN
    UPDATE dbo.WorkflowSteps
    SET Description = N'The vetted agreement is available in the customer portal and Legal waits for the customer-signed upload.',
        StepType = 0,
        AssignmentType = N'Role',
        RequiredRole = N'Legal Admin Assistant',
        Configuration = @CustomerStepConfig,
        UpdatedAt = @Now,
        UpdatedBy = @SystemUser
    WHERE Id = @CustomerStepId;
END;

UPDATE dbo.WorkflowSteps
SET [Order] = CASE Name
        WHEN N'Legal Intake' THEN 1
        WHEN N'Agreement Vetting' THEN 2
        WHEN N'Customer Signature Return' THEN 3
        WHEN N'Head of Legal Signature' THEN 4
        ELSE [Order]
    END,
    IsStartStep = CASE WHEN Name = N'Legal Intake' THEN 1 ELSE 0 END,
    IsEndStep = CASE WHEN Name = N'Head of Legal Signature' THEN 1 ELSE 0 END,
    UpdatedAt = @Now,
    UpdatedBy = @SystemUser
WHERE TenantId = @TenantId
  AND WorkflowDefinitionId = @WorkflowDefinitionId
  AND IsDeleted = 0
  AND Name IN (N'Legal Intake', N'Agreement Vetting', N'Customer Signature Return', N'Head of Legal Signature');

UPDATE dbo.WorkflowSteps
SET Description = N'After the customer returns the signed agreement, the Head of Legal applies the final Legal signature.',
    StepType = 2,
    AssignmentType = N'Role',
    RequiredRole = N'Head of Legal',
    Configuration = @HeadStepConfig,
    UpdatedAt = @Now,
    UpdatedBy = @SystemUser
WHERE Id = @HeadStepId;

UPDATE dbo.WorkflowDefinitions
SET UpdatedAt = @Now,
    UpdatedBy = @SystemUser
WHERE Id = @WorkflowDefinitionId;

UPDATE dbo.WorkflowTransitions
SET ToStepId = @CustomerStepId,
    Name = N'Release to customer signature return',
    Description = N'Agreement has been vetted and is released for customer signature return.',
    UpdatedAt = @Now,
    UpdatedBy = @SystemUser
WHERE TenantId = @TenantId
  AND WorkflowDefinitionId = @WorkflowDefinitionId
  AND IsDeleted = 0
  AND FromStepId = @AgreementStepId
  AND ToStepId = @HeadStepId;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.WorkflowTransitions
    WHERE TenantId = @TenantId
      AND WorkflowDefinitionId = @WorkflowDefinitionId
      AND IsDeleted = 0
      AND FromStepId = @AgreementStepId
      AND ToStepId = @CustomerStepId
)
BEGIN
    INSERT INTO dbo.WorkflowTransitions
    (
        Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, Description,
        Condition, IsDefault, Priority, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
        CreatedById, LastModifiedById, IsDeleted, DeletedAt, DeletedBy, TenantId
    )
    VALUES
    (
        NEWID(), @WorkflowDefinitionId, @AgreementStepId, @CustomerStepId,
        N'Release to customer signature return',
        N'Agreement has been vetted and is released for customer signature return.',
        NULL, 1, 0, @Now, NULL, @SystemUser, NULL,
        NULL, NULL, 0, NULL, NULL, @TenantId
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.WorkflowTransitions
    WHERE TenantId = @TenantId
      AND WorkflowDefinitionId = @WorkflowDefinitionId
      AND IsDeleted = 0
      AND FromStepId = @CustomerStepId
      AND ToStepId = @HeadStepId
)
BEGIN
    INSERT INTO dbo.WorkflowTransitions
    (
        Id, WorkflowDefinitionId, FromStepId, ToStepId, Name, Description,
        Condition, IsDefault, Priority, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy,
        CreatedById, LastModifiedById, IsDeleted, DeletedAt, DeletedBy, TenantId
    )
    VALUES
    (
        NEWID(), @WorkflowDefinitionId, @CustomerStepId, @HeadStepId,
        N'Forward to Head of Legal signature',
        N'Customer-signed agreement has been returned and is ready for Head of Legal signature.',
        NULL, 1, 0, @Now, NULL, @SystemUser, NULL,
        NULL, NULL, 0, NULL, NULL, @TenantId
    );
END;

UPDATE dbo.WorkflowTransitions
SET IsDeleted = 1,
    DeletedAt = @Now,
    DeletedBy = @SystemUser,
    UpdatedAt = @Now,
    UpdatedBy = @SystemUser
WHERE TenantId = @TenantId
  AND WorkflowDefinitionId = @WorkflowDefinitionId
  AND IsDeleted = 0
  AND FromStepId = @AgreementStepId
  AND ToStepId = @HeadStepId;

UPDATE d
SET RequiredFrom = N'Customer Signature Return',
    UpdatedAt = @Now,
    UpdatedBy = @SystemUser
FROM dbo.ProcedureCaseDocuments d
INNER JOIN dbo.ProcedureCases pc ON pc.Id = d.ProcedureCaseId
WHERE pc.TenantId = @TenantId
  AND pc.IsDeleted = 0
  AND pc.EntityType = N'LegalPropertyAgreementReview'
  AND d.IsDeleted = 0
  AND d.Name IN (N'Customer signed agreement', N'Signed property agreement')
  AND (d.RequiredFrom IS NULL OR d.RequiredFrom <> N'Customer Signature Return');

DECLARE @ResolvedCaseId uniqueidentifier = NULL;
DECLARE @ResolvedWorkflowInstanceId uniqueidentifier = NULL;
DECLARE @SourceCaseId uniqueidentifier = NULL;
DECLARE @ActorId uniqueidentifier = NULL;

IF @MoveCurrentCase = 1
BEGIN
    DECLARE @CaseCount int;

    SELECT @CaseCount = COUNT(*)
    FROM dbo.ProcedureCases pc
    WHERE pc.TenantId = @TenantId
      AND pc.IsDeleted = 0
      AND pc.EntityType = N'LegalPropertyAgreementReview'
      AND pc.CurrentStageName = N'Head of Legal Signature'
      AND pc.Status NOT IN (N'Completed', N'Closed', N'Cancelled')
      AND (@CaseId IS NULL OR pc.Id = @CaseId)
      AND (@CaseReference IS NULL OR pc.ReferenceNumber = @CaseReference);

    IF @CaseCount = 0
    BEGIN
        RAISERROR('No active LegalPropertyAgreementReview case is currently at Head of Legal Signature for the supplied filters.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF @CaseCount > 1
    BEGIN
        RAISERROR('More than one matching case was found. Set @CaseId or @CaseReference before moving the case back.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    SELECT TOP (1)
        @ResolvedCaseId = pc.Id,
        @ResolvedWorkflowInstanceId = pc.WorkflowInstanceId,
        @ActorId = COALESCE(pc.LastActionById, pc.OpenedById)
    FROM dbo.ProcedureCases pc
    WHERE pc.TenantId = @TenantId
      AND pc.IsDeleted = 0
      AND pc.EntityType = N'LegalPropertyAgreementReview'
      AND pc.CurrentStageName = N'Head of Legal Signature'
      AND pc.Status NOT IN (N'Completed', N'Closed', N'Cancelled')
      AND (@CaseId IS NULL OR pc.Id = @CaseId)
      AND (@CaseReference IS NULL OR pc.ReferenceNumber = @CaseReference);

    SELECT TOP (1)
        @SourceCaseId = TRY_CONVERT(uniqueidentifier, f.Value)
    FROM dbo.ProcedureCaseFields f
    WHERE f.TenantId = @TenantId
      AND f.ProcedureCaseId = @ResolvedCaseId
      AND f.IsDeleted = 0
      AND f.[Key] = N'sourceProcedureCaseId'
      AND TRY_CONVERT(uniqueidentifier, f.Value) IS NOT NULL;

    UPDATE dbo.ProcedureCases
    SET CurrentStageIndex = 2,
        CurrentStageName = N'Customer Signature Return',
        CurrentStageOwner = N'Legal Admin Assistant',
        CurrentAssignedRole = N'Legal Admin Assistant',
        WorkflowDefinitionId = @WorkflowDefinitionId,
        WorkflowStepId = @CustomerStepId,
        UpdatedAt = @Now,
        UpdatedBy = @SystemUser,
        LastModifiedById = @ActorId
    WHERE Id = @ResolvedCaseId;

    IF @ResolvedWorkflowInstanceId IS NOT NULL
    BEGIN
        UPDATE dbo.WorkflowInstances
        SET CurrentStepId = @CustomerStepId,
            Status = 1,
            CompletedDate = NULL,
            CancelledDate = NULL,
            UpdatedAt = @Now,
            UpdatedBy = @SystemUser
        WHERE Id = @ResolvedWorkflowInstanceId
          AND TenantId = @TenantId
          AND IsDeleted = 0;

        UPDATE a
        SET Status = 4,
            ProcessedDate = @Now,
            Comments = COALESCE(NULLIF(a.Comments, N''), N'Cancelled by workflow repair before customer signature return.'),
            IsDeleted = 1,
            DeletedAt = @Now,
            DeletedBy = @SystemUser,
            UpdatedAt = @Now,
            UpdatedBy = @SystemUser
        FROM dbo.WorkflowApprovals a
        INNER JOIN dbo.WorkflowStepInstances si ON si.Id = a.StepInstanceId
        WHERE si.WorkflowInstanceId = @ResolvedWorkflowInstanceId
          AND si.WorkflowStepId = @HeadStepId
          AND si.Status IN (0, 1)
          AND a.IsDeleted = 0
          AND a.Status IN (0, 6);

        UPDATE dbo.WorkflowStepInstances
        SET WorkflowStepId = @CustomerStepId,
            Status = 0,
            CompletedDate = NULL,
            ResultData = NULL,
            Comments = N'Moved back to Customer Signature Return by workflow repair.',
            AssignedToId = NULL,
            UpdatedAt = @Now,
            UpdatedBy = @SystemUser
        WHERE WorkflowInstanceId = @ResolvedWorkflowInstanceId
          AND WorkflowStepId = @HeadStepId
          AND Status IN (0, 1)
          AND IsDeleted = 0;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.WorkflowStepInstances
            WHERE TenantId = @TenantId
              AND WorkflowInstanceId = @ResolvedWorkflowInstanceId
              AND WorkflowStepId = @CustomerStepId
              AND Status IN (0, 1)
              AND IsDeleted = 0
        )
        BEGIN
            INSERT INTO dbo.WorkflowStepInstances
            (
                Id, WorkflowInstanceId, WorkflowStepId, Status, AssignedToId,
                CreatedDate, StartedDate, CompletedDate, DueDate, ResultData, Comments,
                RetryCount, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById,
                LastModifiedById, IsDeleted, DeletedAt, DeletedBy, TenantId
            )
            VALUES
            (
                NEWID(), @ResolvedWorkflowInstanceId, @CustomerStepId, 0, NULL,
                @Now, @Now, NULL, NULL, NULL, N'Moved back to Customer Signature Return by workflow repair.',
                0, @Now, NULL, @SystemUser, NULL, NULL,
                NULL, 0, NULL, NULL, @TenantId
            );
        END;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.ProcedureCaseChecklistItems
        WHERE TenantId = @TenantId AND ProcedureCaseId = @ResolvedCaseId AND IsDeleted = 0
          AND StageName = N'Customer Signature Return'
          AND Text = N'Confirm agreement is released to the customer portal'
    )
    BEGIN
        INSERT INTO dbo.ProcedureCaseChecklistItems
        (
            Id, ProcedureCaseId, StageIndex, StageName, Text, IsCompleted, CompletedById, CompletedAt,
            CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
            IsDeleted, DeletedAt, DeletedBy, TenantId
        )
        VALUES
        (NEWID(), @ResolvedCaseId, 2, N'Customer Signature Return', N'Confirm agreement is released to the customer portal', 0, NULL, NULL, @Now, NULL, @SystemUser, NULL, NULL, NULL, 0, NULL, NULL, @TenantId),
        (NEWID(), @ResolvedCaseId, 2, N'Customer Signature Return', N'Confirm customer signed agreement upload', 0, NULL, NULL, @Now, NULL, @SystemUser, NULL, NULL, NULL, 0, NULL, NULL, @TenantId),
        (NEWID(), @ResolvedCaseId, 2, N'Customer Signature Return', N'Forward returned agreement to Head of Legal', 0, NULL, NULL, @Now, NULL, @SystemUser, NULL, NULL, NULL, 0, NULL, NULL, @TenantId);
    END;

    INSERT INTO dbo.ProcedureCaseActivities
    (
        Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
        CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
        IsDeleted, DeletedAt, DeletedBy, TenantId
    )
    VALUES
    (
        NEWID(), @ResolvedCaseId, N'Workflow repaired', N'Customer Signature Return',
        N'Workflow was repaired to add Customer Signature Return before Head of Legal Signature, and this case was moved back for customer signature upload.',
        @ActorId, @Now, @Now, NULL, @SystemUser, NULL, NULL, NULL,
        0, NULL, NULL, @TenantId
    );

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ProcedureCaseFields
        WHERE TenantId = @TenantId
          AND ProcedureCaseId = @ResolvedCaseId
          AND [Key] = N'agreementSigningLocation'
          AND IsDeleted = 0
    )
    BEGIN
        INSERT INTO dbo.ProcedureCaseFields
        (
            Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
            CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
            IsDeleted, DeletedAt, DeletedBy, TenantId
        )
        VALUES
        (
            NEWID(), @ResolvedCaseId, N'agreementSigningLocation', N'Agreement signing location',
            N'select', N'Legal', N'["Legal","Estate"]',
            @Now, NULL, @SystemUser, NULL, NULL, @ActorId,
            0, NULL, NULL, @TenantId
        );
    END
    ELSE
    BEGIN
        UPDATE dbo.ProcedureCaseFields
        SET Label = N'Agreement signing location',
            FieldType = N'select',
            Value = N'Legal',
            OptionsJson = N'["Legal","Estate"]',
            UpdatedAt = @Now,
            UpdatedBy = @SystemUser,
            LastModifiedById = @ActorId
        WHERE TenantId = @TenantId
          AND ProcedureCaseId = @ResolvedCaseId
          AND [Key] = N'agreementSigningLocation'
          AND IsDeleted = 0;
    END;

    IF @SourceCaseId IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.ProcedureCaseFields
            WHERE TenantId = @TenantId
              AND ProcedureCaseId = @SourceCaseId
              AND [Key] = N'agreementSigningLocation'
              AND IsDeleted = 0
        )
        BEGIN
            INSERT INTO dbo.ProcedureCaseFields
            (
                Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
                CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
                IsDeleted, DeletedAt, DeletedBy, TenantId
            )
            VALUES
            (
                NEWID(), @SourceCaseId, N'agreementSigningLocation', N'Agreement signing location',
                N'select', N'Legal', N'["Legal","Estate"]',
                @Now, NULL, @SystemUser, NULL, NULL, @ActorId,
                0, NULL, NULL, @TenantId
            );
        END
        ELSE
        BEGIN
            UPDATE dbo.ProcedureCaseFields
            SET Label = N'Agreement signing location',
                FieldType = N'select',
                Value = N'Legal',
                OptionsJson = N'["Legal","Estate"]',
                UpdatedAt = @Now,
                UpdatedBy = @SystemUser,
                LastModifiedById = @ActorId
            WHERE TenantId = @TenantId
              AND ProcedureCaseId = @SourceCaseId
              AND [Key] = N'agreementSigningLocation'
              AND IsDeleted = 0;
        END;
    END;
END;

SELECT
    wd.Id AS WorkflowDefinitionId,
    wd.Name AS WorkflowName,
    wd.Version,
    wd.LifecycleStatus
FROM dbo.WorkflowDefinitions wd
WHERE wd.Id = @WorkflowDefinitionId;

SELECT
    s.[Order],
    s.Name,
    s.StepType,
    s.RequiredRole,
    JSON_VALUE(s.Configuration, '$.approvalConfig.signaturePolicy.isRequired') AS SignatureRequired,
    JSON_VALUE(s.Configuration, '$.approvalConfig.signaturePolicy.requiredSigningRole') AS RequiredSigningRole
FROM dbo.WorkflowSteps s
WHERE s.WorkflowDefinitionId = @WorkflowDefinitionId
  AND s.IsDeleted = 0
ORDER BY s.[Order];

SELECT
    t.Name,
    fs.Name AS FromStep,
    ts.Name AS ToStep,
    t.IsDefault,
    t.IsDeleted
FROM dbo.WorkflowTransitions t
INNER JOIN dbo.WorkflowSteps fs ON fs.Id = t.FromStepId
INNER JOIN dbo.WorkflowSteps ts ON ts.Id = t.ToStepId
WHERE t.WorkflowDefinitionId = @WorkflowDefinitionId
  AND t.IsDeleted = 0
ORDER BY fs.[Order], t.Priority;

IF @ResolvedCaseId IS NOT NULL
BEGIN
    SELECT
        pc.Id,
        pc.ReferenceNumber,
        pc.CurrentStageIndex,
        pc.CurrentStageName,
        pc.CurrentAssignedRole,
        pc.WorkflowStepId,
        @SourceCaseId AS LinkedEstateCaseId
    FROM dbo.ProcedureCases pc
    WHERE pc.Id = @ResolvedCaseId;

    SELECT
        d.Name,
        d.RequiredFrom,
        d.ProvidedBy,
        d.IsMandatory,
        d.FileName,
        CASE WHEN NULLIF(d.FileUrl, N'') IS NULL THEN 0 ELSE 1 END AS HasFile
    FROM dbo.ProcedureCaseDocuments d
    WHERE d.ProcedureCaseId = @ResolvedCaseId
      AND d.IsDeleted = 0
    ORDER BY d.RequiredFrom, d.Name;
END;

IF @PreviewOnly = 1
BEGIN
    ROLLBACK TRANSACTION;
    SELECT N'Preview only: transaction rolled back. Set @PreviewOnly = 0 to commit these changes.' AS Result;
END
ELSE
BEGIN
    COMMIT TRANSACTION;
    SELECT N'Committed: workflow repaired and matching case moved back if @MoveCurrentCase = 1.' AS Result;
END;
