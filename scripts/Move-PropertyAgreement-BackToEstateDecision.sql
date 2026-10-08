/*
    Reset the single active property agreement test back to the Estate stage where
    "Agreement signing location" is selected.

    The script:
      - permanently deletes prior signed-agreement case document rows;
      - resets the customer response and execution fields;
      - retires the old linked Legal agreement-review case;
      - restores the linked Estate workflow to its agreement decision stage; and
      - clears agreementSigningLocation so Estate must choose Legal or Estate again.

    It retains the generated unsigned agreement and completed audit history.
    Run with @PreviewOnly = 1 first. Change it to 0 and rerun to commit.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @TenantId uniqueidentifier = NULL;
DECLARE @LegalCaseId uniqueidentifier = NULL;
DECLARE @LegalCaseReference nvarchar(80) = NULL;

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run this script outside an existing transaction.', 16, 1);
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @MatchCount int;
    SELECT @MatchCount = COUNT(*)
    FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
    WHERE IsDeleted = 0
      AND EntityType = N'LegalPropertyAgreementReview'
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled', N'Canceled')
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@LegalCaseId IS NULL OR Id = @LegalCaseId)
      AND (@LegalCaseReference IS NULL OR ReferenceNumber = @LegalCaseReference);

    IF @MatchCount <> 1
        RAISERROR('Expected exactly one active Legal property agreement case. Set the optional filters if necessary.', 16, 1);

    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @Script nvarchar(150) = N'Move-PropertyAgreement-BackToEstateDecision.sql';
    DECLARE @EstateCaseId uniqueidentifier;
    DECLARE @LegalWorkflowInstanceId uniqueidentifier;
    DECLARE @LegalWorkflowStepId uniqueidentifier;
    DECLARE @EstateWorkflowInstanceId uniqueidentifier;
    DECLARE @EstateWorkflowDefinitionId uniqueidentifier;
    DECLARE @EstateCurrentStepId uniqueidentifier;
    DECLARE @EstateTargetStepId uniqueidentifier;
    DECLARE @EstateTargetStepInstanceId uniqueidentifier;
    DECLARE @EstateTargetStageIndex int;
    DECLARE @EstateTargetStageName nvarchar(120);
    DECLARE @EstateTargetRole nvarchar(150);
    DECLARE @EstateTargetStepType int;
    DECLARE @ActorId uniqueidentifier;

    SELECT TOP (1)
        @LegalCaseId = pc.Id,
        @TenantId = pc.TenantId,
        @LegalWorkflowInstanceId = pc.WorkflowInstanceId,
        @LegalWorkflowStepId = pc.WorkflowStepId,
        @ActorId = COALESCE(pc.LastActionById, pc.OpenedById)
    FROM dbo.ProcedureCases pc
    WHERE pc.IsDeleted = 0
      AND pc.EntityType = N'LegalPropertyAgreementReview'
      AND pc.Status NOT IN (N'Completed', N'Closed', N'Cancelled', N'Canceled')
      AND (@TenantId IS NULL OR pc.TenantId = @TenantId)
      AND (@LegalCaseId IS NULL OR pc.Id = @LegalCaseId)
      AND (@LegalCaseReference IS NULL OR pc.ReferenceNumber = @LegalCaseReference);

    IF @LegalWorkflowInstanceId IS NULL OR @LegalWorkflowStepId IS NULL
        RAISERROR('The active Legal case has no complete workflow runtime link.', 16, 1);

    IF (SELECT COUNT(*) FROM dbo.ProcedureCaseFields WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND ProcedureCaseId = @LegalCaseId
          AND IsDeleted = 0 AND [Key] = N'sourceProcedureCaseId') <> 1
        RAISERROR('Expected exactly one Estate source link on the Legal case.', 16, 1);

    SELECT @EstateCaseId = TRY_CONVERT(uniqueidentifier, Value)
    FROM dbo.ProcedureCaseFields
    WHERE TenantId = @TenantId AND ProcedureCaseId = @LegalCaseId
      AND IsDeleted = 0 AND [Key] = N'sourceProcedureCaseId';

    SELECT
        @EstateWorkflowInstanceId = pc.WorkflowInstanceId,
        @EstateWorkflowDefinitionId = pc.WorkflowDefinitionId,
        @EstateCurrentStepId = pc.WorkflowStepId,
        @ActorId = COALESCE(pc.LastActionById, pc.OpenedById, @ActorId)
    FROM dbo.ProcedureCases pc WITH (UPDLOCK, HOLDLOCK)
    WHERE pc.Id = @EstateCaseId AND pc.TenantId = @TenantId AND pc.IsDeleted = 0
      AND pc.Module IN (N'Estate', N'PropertyManagement')
      AND pc.Status NOT IN (N'Completed', N'Closed', N'Cancelled', N'Canceled');

    IF @EstateCaseId IS NULL OR @EstateWorkflowInstanceId IS NULL
       OR @EstateWorkflowDefinitionId IS NULL OR @EstateCurrentStepId IS NULL
        RAISERROR('The linked active Estate request or its workflow runtime was not found.', 16, 1);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.WorkflowInstances WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @EstateWorkflowInstanceId AND TenantId = @TenantId AND IsDeleted = 0
          AND WorkflowDefinitionId = @EstateWorkflowDefinitionId
          AND CurrentStepId = @EstateCurrentStepId AND Status IN (1, 6))
        RAISERROR('The Estate case and its workflow runtime are not aligned or active.', 16, 1);

    DECLARE @EstateSteps TABLE
    (
        Id uniqueidentifier PRIMARY KEY,
        StageIndex int,
        [Order] int,
        Name nvarchar(200),
        RequiredRole nvarchar(150),
        StepType int,
        Configuration nvarchar(max)
    );

    INSERT INTO @EstateSteps
    SELECT s.Id,
           ROW_NUMBER() OVER (ORDER BY s.[Order], s.Name) - 1,
           s.[Order], s.Name, s.RequiredRole, s.StepType, s.Configuration
    FROM dbo.WorkflowSteps s WITH (HOLDLOCK)
    WHERE s.TenantId = @TenantId
      AND s.WorkflowDefinitionId = @EstateWorkflowDefinitionId
      AND s.IsDeleted = 0;

    -- Prefer the known Estate stage names. Fall back to the earliest stage that
    -- explicitly contains the agreementSigningLocation workflow form field.
    SELECT TOP (1)
        @EstateTargetStepId = Id,
        @EstateTargetStageIndex = StageIndex,
        @EstateTargetStageName = Name,
        @EstateTargetRole = RequiredRole,
        @EstateTargetStepType = StepType
    FROM @EstateSteps
    WHERE Name IN (N'Estate decision and agreement', N'Management decision')
    ORDER BY CASE Name WHEN N'Estate decision and agreement' THEN 0 ELSE 1 END, [Order];

    IF @EstateTargetStepId IS NULL
    BEGIN
        SELECT TOP (1)
            @EstateTargetStepId = s.Id,
            @EstateTargetStageIndex = s.StageIndex,
            @EstateTargetStageName = s.Name,
            @EstateTargetRole = s.RequiredRole,
            @EstateTargetStepType = s.StepType
        FROM @EstateSteps s
        WHERE ISJSON(s.Configuration) = 1
          AND EXISTS (
              SELECT 1
              FROM OPENJSON(s.Configuration, '$.formFields')
                   WITH (Name nvarchar(100) '$.name') field
              WHERE field.Name = N'agreementSigningLocation')
        ORDER BY s.[Order];
    END;

    IF @EstateTargetStepId IS NULL
        RAISERROR('The Estate workflow has no agreement-signing decision stage.', 16, 1);

    IF @EstateTargetStageIndex > (
        SELECT StageIndex FROM @EstateSteps WHERE Id = @EstateCurrentStepId)
        RAISERROR('The Estate agreement decision stage is after the current Estate stage.', 16, 1);

    IF (SELECT COUNT(*) FROM dbo.WorkflowStepInstances WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowInstanceId = @EstateWorkflowInstanceId
          AND IsDeleted = 0 AND Status IN (0, 1)) <> 1
        RAISERROR('Expected exactly one active Estate workflow step instance.', 16, 1);

    SELECT TOP (1) @EstateTargetStepInstanceId = si.Id
    FROM dbo.WorkflowStepInstances si WITH (UPDLOCK, HOLDLOCK)
    WHERE si.TenantId = @TenantId
      AND si.WorkflowInstanceId = @EstateWorkflowInstanceId
      AND si.WorkflowStepId = @EstateTargetStepId
      AND si.IsDeleted = 0
    ORDER BY si.CreatedDate DESC, si.CreatedAt DESC;

    IF @EstateTargetStepInstanceId IS NULL
        RAISERROR('The Estate workflow has no prior runtime task for its agreement decision stage.', 16, 1);

    IF @EstateTargetStepType = 2 AND NOT EXISTS (
        SELECT 1 FROM dbo.WorkflowApprovals
        WHERE TenantId = @TenantId AND StepInstanceId = @EstateTargetStepInstanceId
          AND IsDeleted = 0)
        RAISERROR('The Estate decision is an approval stage, but its prior approval records were not found.', 16, 1);

    SELECT N'Before' AS Snapshot,
           pc.Id, pc.Module, pc.ReferenceNumber, pc.Status,
           pc.CurrentStageIndex, pc.CurrentStageName, pc.WorkflowInstanceId
    FROM dbo.ProcedureCases pc
    WHERE pc.TenantId = @TenantId AND pc.Id IN (@EstateCaseId, @LegalCaseId);

    SELECT N'Fields before reset' AS Snapshot, ProcedureCaseId, [Key], Value
    FROM dbo.ProcedureCaseFields
    WHERE TenantId = @TenantId AND ProcedureCaseId IN (@EstateCaseId, @LegalCaseId)
      AND IsDeleted = 0
      AND [Key] IN (
          N'agreementSigningLocation', N'customerAcceptanceStatus', N'customerAcceptanceDate',
          N'legalAgreementReviewCaseId', N'legalAgreementReviewReference', N'legalAgreementReviewStatus',
          N'signedAgreementReference', N'agreementExecutionStatus', N'internalApprovalStatus',
          N'internalSignatureStatus', N'finalSignedAgreementReference', N'finalSignedAgreementVersion',
          N'signatureStatus', N'generatedAgreementReference');

    SELECT N'Signed documents to delete' AS Snapshot,
           Id, ProcedureCaseId, Name, FileName, FileUrl
    FROM dbo.ProcedureCaseDocuments
    WHERE TenantId = @TenantId AND ProcedureCaseId IN (@EstateCaseId, @LegalCaseId)
      AND IsDeleted = 0
      AND Name IN (N'Customer signed agreement', N'Signed property agreement', N'Head of Legal signed agreement');

    DELETE FROM dbo.ProcedureCaseDocuments
    WHERE TenantId = @TenantId AND ProcedureCaseId IN (@EstateCaseId, @LegalCaseId)
      AND IsDeleted = 0
      AND Name IN (N'Customer signed agreement', N'Signed property agreement', N'Head of Legal signed agreement');

    -- Retire the old Legal runtime. A fresh Legal case will be created after Estate
    -- saves the new signing location and lodges the agreement again.
    UPDATE a
    SET Status = 4, ProcessedDate = @Now,
        Comments = CONCAT(a.Comments, N' [Expired by Estate test rewind.]'),
        UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.WorkflowApprovals a
    JOIN dbo.WorkflowStepInstances si ON si.Id = a.StepInstanceId AND si.TenantId = a.TenantId
    WHERE a.TenantId = @TenantId AND si.WorkflowInstanceId = @LegalWorkflowInstanceId
      AND si.IsDeleted = 0 AND si.Status IN (0, 1)
      AND a.IsDeleted = 0 AND a.Status IN (0, 5, 6);

    UPDATE dbo.WorkflowStepInstances
    SET Status = 3, CompletedDate = @Now,
        Comments = CONCAT(Comments, N' [Cancelled by Estate test rewind.]'),
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE TenantId = @TenantId AND WorkflowInstanceId = @LegalWorkflowInstanceId
      AND IsDeleted = 0 AND Status IN (0, 1);

    UPDATE dbo.WorkflowInstances
    SET Status = 3, CurrentStepId = NULL, CancelledDate = @Now,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @LegalWorkflowInstanceId AND TenantId = @TenantId AND IsDeleted = 0;

    UPDATE dbo.ProcedureCases
    SET Status = N'Cancelled', CurrentStageOwner = NULL, CurrentAssignedRole = NULL,
        CompletedAt = NULL, IsDeleted = 1, DeletedAt = @Now, DeletedBy = @Script,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @LegalCaseId AND TenantId = @TenantId AND IsDeleted = 0;

    -- Cancel the currently active Estate task and reactivate the original decision task.
    UPDATE dbo.WorkflowStepInstances
    SET Status = 3, CompletedDate = @Now,
        Comments = CONCAT(Comments, N' [Cancelled by rewind to ', @EstateTargetStageName, N'.]'),
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE TenantId = @TenantId AND WorkflowInstanceId = @EstateWorkflowInstanceId
      AND IsDeleted = 0 AND Status IN (0, 1);

    UPDATE dbo.WorkflowStepInstances
    SET Status = 1, AssignedToId = NULL, StartedDate = COALESCE(StartedDate, @Now),
        CompletedDate = NULL, ResultData = NULL,
        Comments = N'Reopened to choose where the customer agreement will be signed.',
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @EstateTargetStepInstanceId AND TenantId = @TenantId;

    IF @EstateTargetStepType = 2
    BEGIN
        DECLARE @FirstApprovalGroup int;
        SELECT @FirstApprovalGroup = MIN(CASE WHEN ApprovalGroup < 1 THEN 1 ELSE ApprovalGroup END)
        FROM dbo.WorkflowApprovals
        WHERE TenantId = @TenantId AND StepInstanceId = @EstateTargetStepInstanceId AND IsDeleted = 0;

        UPDATE dbo.WorkflowApprovals
        SET Status = CASE
                WHEN (CASE WHEN ApprovalGroup < 1 THEN 1 ELSE ApprovalGroup END) = @FirstApprovalGroup THEN 0
                ELSE 6
            END,
            ApproverId = COALESCE(OriginalApproverId, ApproverId),
            DelegationId = NULL, DelegatedById = NULL, DelegatedAt = NULL, DelegationReason = NULL,
            ProcessedDate = NULL, ProcessedById = NULL, Comments = NULL,
            RequestedDate = @Now, UpdatedAt = @Now, UpdatedBy = @Script
        WHERE TenantId = @TenantId AND StepInstanceId = @EstateTargetStepInstanceId AND IsDeleted = 0;
    END;

    UPDATE dbo.WorkflowInstances
    SET CurrentStepId = @EstateTargetStepId, Status = 1,
        CompletedDate = NULL, CancelledDate = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @EstateWorkflowInstanceId AND TenantId = @TenantId AND IsDeleted = 0;

    UPDATE dbo.ProcedureCases
    SET CurrentStageIndex = @EstateTargetStageIndex,
        CurrentStageName = @EstateTargetStageName,
        CurrentStageOwner = @EstateTargetRole,
        CurrentAssignedRole = @EstateTargetRole,
        WorkflowStepId = @EstateTargetStepId,
        Status = N'Open', CompletedAt = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @EstateCaseId AND TenantId = @TenantId AND IsDeleted = 0;

    UPDATE c
    SET StageIndex = s.StageIndex, IsCompleted = 0,
        CompletedById = NULL, CompletedAt = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.ProcedureCaseChecklistItems c
    JOIN @EstateSteps s ON s.Name = c.StageName
    WHERE c.TenantId = @TenantId AND c.ProcedureCaseId = @EstateCaseId
      AND c.IsDeleted = 0 AND s.StageIndex >= @EstateTargetStageIndex;

    DECLARE @ResetFields TABLE
    (
        [Key] nvarchar(100) PRIMARY KEY,
        Label nvarchar(150),
        FieldType nvarchar(50),
        Value nvarchar(max)
    );
    INSERT INTO @ResetFields VALUES
        (N'agreementSigningLocation', N'Agreement signing location', N'select', NULL),
        (N'customerAcceptanceStatus', N'Customer acceptance status', N'select', N'Pending'),
        (N'customerAcceptanceDate', N'Customer acceptance date', N'date', NULL),
        (N'customerNotificationStatus', N'Customer notification status', N'text', N'Awaiting Estate agreement decision'),
        (N'signedAgreementReference', N'Signed agreement upload reference', N'text', NULL),
        (N'agreementExecutionStatus', N'Agreement execution status', N'select', N'Awaiting customer signature'),
        (N'internalApprovalStatus', N'Internal agreement approval status', N'select', N'Not submitted'),
        (N'internalSignatureStatus', N'Internal digital signature status', N'select', N'Blocked - customer signature pending'),
        (N'finalSignedAgreementReference', N'Final signed agreement reference', N'text', NULL),
        (N'finalSignedAgreementVersion', N'Final signed agreement version', N'text', NULL),
        (N'legalAgreementReviewCaseId', N'Legal agreement review case ID', N'text', NULL),
        (N'legalAgreementReviewReference', N'Legal agreement review reference', N'text', NULL),
        (N'legalAgreementReviewStatus', N'Legal agreement review status', N'text', NULL),
        (N'legalLastMatterCaseId', N'Latest linked Legal matter ID', N'text', NULL),
        (N'legalLastMatterReference', N'Latest linked Legal matter reference', N'text', NULL),
        (N'legalLastMatterType', N'Latest linked Legal matter type', N'text', NULL),
        (N'legalLastMatterStatus', N'Latest linked Legal matter status', N'text', NULL),
        (N'applicationStatus', N'Request status', N'select', N'Approved - agreement decision reopened');

    UPDATE f
    SET Label = r.Label, FieldType = r.FieldType, Value = r.Value,
        OptionsJson = CASE
            WHEN r.[Key] = N'agreementSigningLocation' THEN N'["Legal","Estate"]'
            ELSE f.OptionsJson
        END,
        IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.ProcedureCaseFields f
    JOIN @ResetFields r ON r.[Key] = f.[Key]
    WHERE f.TenantId = @TenantId AND f.ProcedureCaseId = @EstateCaseId;

    INSERT INTO dbo.ProcedureCaseFields
        (Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    SELECT NEWID(), @EstateCaseId, r.[Key], r.Label, r.FieldType, r.Value,
           CASE WHEN r.[Key] = N'agreementSigningLocation' THEN N'["Legal","Estate"]' ELSE NULL END,
           @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId
    FROM @ResetFields r
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedureCaseFields f
        WHERE f.TenantId = @TenantId AND f.ProcedureCaseId = @EstateCaseId
          AND f.[Key] = r.[Key]);

    INSERT INTO dbo.ProcedureCaseActivities
        (Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    VALUES
        (NEWID(), @EstateCaseId, N'Database test rewind', @EstateTargetStageName,
         N'Reopened Estate agreement decision to choose Legal or Estate signing. Prior signed evidence was deleted and the old Legal review case was retired.',
         @ActorId, @Now, @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId);

    SELECT N'After (committed only when PreviewOnly = 0)' AS Snapshot,
           pc.Id, pc.Module, pc.ReferenceNumber, pc.Status,
           pc.CurrentStageIndex, pc.CurrentStageName, pc.CurrentAssignedRole,
           pc.WorkflowInstanceId, pc.WorkflowStepId
    FROM dbo.ProcedureCases pc
    WHERE pc.TenantId = @TenantId AND pc.Id IN (@EstateCaseId, @LegalCaseId);

    SELECT N'Estate choice after reset' AS Snapshot, [Key], Value
    FROM dbo.ProcedureCaseFields
    WHERE TenantId = @TenantId AND ProcedureCaseId = @EstateCaseId
      AND IsDeleted = 0
      AND [Key] IN (N'agreementSigningLocation', N'generatedAgreementReference',
                    N'decisionStatus', N'legalAgreementReviewStatus', N'customerAcceptanceStatus');

    IF @PreviewOnly = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'Preview rolled back. Set @PreviewOnly = 0 and rerun to apply.' AS Result;
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Committed. Open the Estate request, select Agreement signing location, save, complete the Estate decision stage, and lodge a fresh Legal agreement review.' AS Result;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @Error nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR('%s', 16, 1, @Error);
END CATCH;
