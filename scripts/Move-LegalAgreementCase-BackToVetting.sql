/*
    Rewind one Legal property agreement case for a portal-release test.
    Run against the server ERP database while this case is not being edited.
    Run with @PreviewOnly = 1 first; set it to 0 and rerun to commit.
    Optional filters disambiguate the case. No workflow definitions are changed.
    Set @DeleteExistingSignatureEvidence = 1 only when deliberately resetting
    this test case. Signed document rows are permanently deleted from the database.
    The generated unsigned agreement and completed workflow history are retained.
    After committing, submit Agreement Vetting through the application to test
    release into Customer Signature Return. This script does not send the agreement.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @DeleteExistingSignatureEvidence bit = 1;
DECLARE @TenantId uniqueidentifier = NULL;
DECLARE @CaseId uniqueidentifier = NULL;
DECLARE @CaseReference nvarchar(80) = NULL;

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run this script outside an existing transaction.', 16, 1);
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Matches int;
    SELECT @Matches = COUNT(*)
    FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
    WHERE IsDeleted = 0 AND EntityType = N'LegalPropertyAgreementReview'
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled')
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@CaseId IS NULL OR Id = @CaseId)
      AND (@CaseReference IS NULL OR ReferenceNumber = @CaseReference);

    IF @Matches <> 1
        RAISERROR('Expected exactly one active Legal agreement case. Set @TenantId and @CaseId or @CaseReference.', 16, 1);

    DECLARE @InstanceId uniqueidentifier, @DefinitionId uniqueidentifier,
            @OldStepId uniqueidentifier, @OldStage nvarchar(120),
            @ActorId uniqueidentifier, @SourceCaseId uniqueidentifier;
    SELECT @CaseId = Id, @TenantId = TenantId,
           @InstanceId = WorkflowInstanceId, @DefinitionId = WorkflowDefinitionId,
           @OldStepId = WorkflowStepId, @OldStage = CurrentStageName,
           @ActorId = COALESCE(LastActionById, OpenedById)
    FROM dbo.ProcedureCases
    WHERE IsDeleted = 0 AND EntityType = N'LegalPropertyAgreementReview'
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled')
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@CaseId IS NULL OR Id = @CaseId)
      AND (@CaseReference IS NULL OR ReferenceNumber = @CaseReference);

    IF @OldStage NOT IN (N'Customer Signature Return', N'Head of Legal Signature')
        RAISERROR('Case must currently be at Customer Signature Return or Head of Legal Signature. No rewind performed.', 16, 1);

    IF @InstanceId IS NULL OR @DefinitionId IS NULL OR @OldStepId IS NULL
        RAISERROR('Case has no complete workflow runtime link. No changes made.', 16, 1);

    IF NOT EXISTS (
        SELECT 1 FROM dbo.WorkflowInstances WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @InstanceId AND TenantId = @TenantId AND IsDeleted = 0
          AND WorkflowDefinitionId = @DefinitionId AND CurrentStepId = @OldStepId
          AND Status IN (1, 6)
    )
        RAISERROR('Case and workflow runtime are not aligned or the runtime is not active.', 16, 1);

    IF (SELECT COUNT(*) FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowInstanceId = @InstanceId AND IsDeleted = 0) <> 1
        RAISERROR('Workflow runtime is shared by multiple cases. No changes made.', 16, 1);

    DECLARE @TargetStepId uniqueidentifier, @CustomerStepId uniqueidentifier,
            @TargetIndex int, @TargetRole nvarchar(150), @TargetConfig nvarchar(max);
    IF (SELECT COUNT(*) FROM dbo.WorkflowSteps WITH (HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowDefinitionId = @DefinitionId AND IsDeleted = 0
          AND Name = N'Agreement Vetting') <> 1
        RAISERROR('The case workflow must contain exactly one Agreement Vetting stage.', 16, 1);
    IF (SELECT COUNT(*) FROM dbo.WorkflowSteps WITH (HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowDefinitionId = @DefinitionId AND IsDeleted = 0
          AND Name = N'Customer Signature Return') <> 1
        RAISERROR('The case workflow must contain exactly one Customer Signature Return stage.', 16, 1);

    DECLARE @Stages TABLE (Id uniqueidentifier PRIMARY KEY, StageIndex int, Name nvarchar(200));
    INSERT INTO @Stages
    SELECT Id, ROW_NUMBER() OVER (ORDER BY [Order], Name) - 1, Name
    FROM dbo.WorkflowSteps
    WHERE TenantId = @TenantId AND WorkflowDefinitionId = @DefinitionId AND IsDeleted = 0;

    SELECT @TargetStepId = s.Id, @TargetIndex = i.StageIndex,
           @TargetRole = s.RequiredRole, @TargetConfig = s.Configuration
    FROM dbo.WorkflowSteps s JOIN @Stages i ON i.Id = s.Id
    WHERE s.Name = N'Agreement Vetting';
    SELECT @CustomerStepId = Id FROM @Stages WHERE Name = N'Customer Signature Return';

    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowSteps
        WHERE Id = @TargetStepId AND StepType = 0 AND AssignmentType = N'Role'
          AND NULLIF(LTRIM(RTRIM(RequiredRole)), N'') IS NOT NULL)
        RAISERROR('Agreement Vetting is not a role-assigned task on this server. This script cannot recreate its assignment.', 16, 1);

    -- Reject an unexpected route rather than replacing the server workflow.
    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowTransitions WITH (HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowDefinitionId = @DefinitionId AND IsDeleted = 0
          AND FromStepId = @TargetStepId AND ToStepId = @CustomerStepId
          AND NULLIF(LTRIM(RTRIM(Condition)), N'') IS NULL)
       OR EXISTS (SELECT 1 FROM dbo.WorkflowTransitions
        WHERE TenantId = @TenantId AND WorkflowDefinitionId = @DefinitionId AND IsDeleted = 0
          AND FromStepId = @TargetStepId AND ToStepId <> @CustomerStepId)
        RAISERROR('Agreement Vetting does not route exclusively to Customer Signature Return. Inspect the server workflow before rewinding.', 16, 1);

    IF (SELECT COUNT(*) FROM dbo.ProcedureCaseFields WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND ProcedureCaseId = @CaseId AND IsDeleted = 0
          AND [Key] = N'sourceProcedureCaseId') <> 1
        RAISERROR('Expected exactly one linked Estate source case field.', 16, 1);
    SELECT @SourceCaseId = TRY_CONVERT(uniqueidentifier, Value)
    FROM dbo.ProcedureCaseFields
    WHERE TenantId = @TenantId AND ProcedureCaseId = @CaseId
      AND IsDeleted = 0 AND [Key] = N'sourceProcedureCaseId';
    IF @SourceCaseId IS NULL OR @SourceCaseId = @CaseId OR NOT EXISTS (
        SELECT 1 FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @SourceCaseId AND TenantId = @TenantId AND IsDeleted = 0
          AND Module IN (N'Estate', N'PropertyManagement')
          AND Status NOT IN (N'Completed', N'Closed', N'Cancelled'))
        RAISERROR('The linked active Estate request was not found in the same tenant.', 16, 1);

    IF EXISTS (SELECT 1 FROM dbo.ProcedureCaseFields WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId)
          AND IsDeleted = 0 AND [Key] = N'agreementSigningLocation' AND Value LIKE N'%estate%')
        RAISERROR('This case is configured for signing in Estate. Its setting will not be overwritten.', 16, 1);

    IF @DeleteExistingSignatureEvidence = 0 AND (EXISTS (SELECT 1 FROM dbo.ProcedureCaseFields
        WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId) AND IsDeleted = 0
          AND (([Key] IN (N'signedAgreementReference', N'finalSignedAgreementReference')
                AND NULLIF(LTRIM(RTRIM(Value)), N'') IS NOT NULL)
            OR ([Key] IN (N'signatureStatus', N'legalAgreementReviewStatus')
                AND ((Value LIKE N'%Head of Legal%' AND Value LIKE N'%signed%') OR Value LIKE N'%fully signed%'))
            OR ([Key] = N'agreementExecutionStatus' AND Value = N'Fully executed')))
       OR EXISTS (SELECT 1 FROM dbo.ProcedureCaseDocuments WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId) AND IsDeleted = 0
          AND Name IN (N'Customer signed agreement', N'Signed property agreement', N'Head of Legal signed agreement')
          AND NULLIF(LTRIM(RTRIM(FileUrl)), N'') IS NOT NULL))
        RAISERROR('A signed agreement already exists. Set @DeleteExistingSignatureEvidence = 1 to permanently delete its case document rows and reset the customer signing state.', 16, 1);

    IF (SELECT COUNT(*) FROM dbo.WorkflowStepInstances WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId AND WorkflowInstanceId = @InstanceId AND IsDeleted = 0
          AND Status IN (0, 1)) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.WorkflowStepInstances
        WHERE TenantId = @TenantId AND WorkflowInstanceId = @InstanceId AND IsDeleted = 0
          AND WorkflowStepId = @OldStepId AND Status IN (0, 1))
        RAISERROR('Expected one active step instance matching the case current stage.', 16, 1);

    IF @DeleteExistingSignatureEvidence = 0 AND EXISTS (
        SELECT 1 FROM dbo.WorkflowApprovals a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.WorkflowStepInstances si ON si.Id = a.StepInstanceId AND si.TenantId = a.TenantId
        WHERE a.TenantId = @TenantId AND si.WorkflowInstanceId = @InstanceId
          AND si.WorkflowStepId = @OldStepId AND si.IsDeleted = 0 AND si.Status IN (0, 1)
          AND a.IsDeleted = 0 AND a.Status = 1)
        RAISERROR('The active stage already has an approval. This script will not rewind approved work.', 16, 1);

    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @Script nvarchar(150) = N'Move-LegalAgreementCase-BackToVetting.sql';
    DECLARE @Details nvarchar(max) = N'Database test rewind from ' + @OldStage
        + N' to Agreement Vetting. Prior signed evidence and customer response reset for retesting; generated unsigned agreement retained. Submit vetting in the application to test portal release.';

    SELECT N'Before' AS Snapshot, Id, ReferenceNumber, CurrentStageName, WorkflowDefinitionId, WorkflowInstanceId
    FROM dbo.ProcedureCases WHERE Id IN (@CaseId, @SourceCaseId) AND TenantId = @TenantId;
    SELECT N'Before' AS Snapshot, ProcedureCaseId, [Key], Value
    FROM dbo.ProcedureCaseFields
    WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId) AND IsDeleted = 0
      AND [Key] IN (N'agreementSigningLocation', N'legalAgreementReviewStatus', N'customerNotificationStatus',
                    N'customerAcceptanceStatus', N'generatedAgreementReference', N'decisionStatus', N'moveInDate');

    SELECT N'Signed evidence before reset' AS Snapshot, Id, ProcedureCaseId, Name, FileName, FileUrl
    FROM dbo.ProcedureCaseDocuments
    WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId) AND IsDeleted = 0
      AND Name IN (N'Customer signed agreement', N'Signed property agreement', N'Head of Legal signed agreement');

    IF @DeleteExistingSignatureEvidence = 1
    BEGIN
        -- Delete signed evidence rows only. Keep the generated unsigned agreement used for the retest.
        DELETE FROM dbo.ProcedureCaseDocuments
        WHERE TenantId = @TenantId AND ProcedureCaseId IN (@CaseId, @SourceCaseId) AND IsDeleted = 0
          AND Name IN (N'Customer signed agreement', N'Signed property agreement', N'Head of Legal signed agreement');

        DECLARE @ResetFields TABLE
        (
            ProcedureCaseId uniqueidentifier,
            [Key] nvarchar(100),
            Label nvarchar(150),
            FieldType nvarchar(50),
            Value nvarchar(max)
        );
        INSERT INTO @ResetFields VALUES
            (@CaseId, N'legalVettingStatus', N'Legal vetting status', N'select', N'Under review'),
            (@CaseId, N'signatureStatus', N'Signature status', N'select', NULL),
            (@CaseId, N'sealStatus', N'Seal status', N'select', NULL),
            (@CaseId, N'dispatchStatus', N'Dispatch status', N'select', NULL),
            (@CaseId, N'estateReturnStatus', N'Estate return status', N'select', NULL),
            (@SourceCaseId, N'customerAcceptanceStatus', N'Customer acceptance status', N'select', N'Pending'),
            (@SourceCaseId, N'customerAcceptanceDate', N'Customer acceptance date', N'date', NULL),
            (@SourceCaseId, N'signedAgreementReference', N'Signed agreement upload reference', N'text', NULL),
            (@SourceCaseId, N'agreementExecutionStatus', N'Agreement execution status', N'select', N'Awaiting customer signature'),
            (@SourceCaseId, N'internalApprovalStatus', N'Internal agreement approval status', N'select', N'Not submitted'),
            (@SourceCaseId, N'internalSignatureStatus', N'Internal digital signature status', N'select', N'Blocked - customer signature pending'),
            (@SourceCaseId, N'finalSignedAgreementReference', N'Final signed agreement reference', N'text', NULL),
            (@SourceCaseId, N'finalSignedAgreementVersion', N'Final signed agreement version', N'text', NULL),
            (@SourceCaseId, N'applicationStatus', N'Request status', N'select', N'Approved - awaiting customer response');

        UPDATE f
        SET Value = r.Value, UpdatedAt = @Now, UpdatedBy = @Script
        FROM dbo.ProcedureCaseFields f
        JOIN @ResetFields r ON r.ProcedureCaseId = f.ProcedureCaseId AND r.[Key] = f.[Key]
        WHERE f.TenantId = @TenantId AND f.IsDeleted = 0;

        INSERT INTO dbo.ProcedureCaseFields
            (Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
             CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
             IsDeleted, DeletedAt, DeletedBy, TenantId)
        SELECT NEWID(), r.ProcedureCaseId, r.[Key], r.Label, r.FieldType, r.Value, NULL,
               @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId
        FROM @ResetFields r
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.ProcedureCaseFields f
            WHERE f.TenantId = @TenantId AND f.ProcedureCaseId = r.ProcedureCaseId
              AND f.[Key] = r.[Key] AND f.IsDeleted = 0);
    END;

    -- Expire outstanding approvals but retain them and all completed step history.
    UPDATE a SET Status = 4, ProcessedDate = @Now,
        Comments = CONCAT(a.Comments, N' [Expired by ', @Script, N' for test rewind.]'),
        UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.WorkflowApprovals a
    JOIN dbo.WorkflowStepInstances si ON si.Id = a.StepInstanceId AND si.TenantId = a.TenantId
    WHERE a.TenantId = @TenantId AND si.WorkflowInstanceId = @InstanceId
      AND si.IsDeleted = 0 AND si.Status IN (0, 1) AND a.IsDeleted = 0 AND a.Status IN (0, 5, 6);

    UPDATE dbo.WorkflowStepInstances
    SET Status = 3, CompletedDate = @Now, Comments = CONCAT(Comments, N' [', @Details, N']'),
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE TenantId = @TenantId AND WorkflowInstanceId = @InstanceId AND IsDeleted = 0 AND Status IN (0, 1);

    INSERT INTO dbo.WorkflowStepInstances
        (Id, WorkflowInstanceId, WorkflowStepId, Status, AssignedToId,
         CreatedDate, StartedDate, CompletedDate, DueDate, ResultData, Comments, RetryCount,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    VALUES
        (NEWID(), @InstanceId, @TargetStepId, 0, NULL,
         @Now, NULL, NULL, NULL, NULL, @Details, 0,
         @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId);

    UPDATE dbo.WorkflowInstances
    SET CurrentStepId = @TargetStepId, Status = 1, CompletedDate = NULL, CancelledDate = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @InstanceId AND TenantId = @TenantId;
    UPDATE dbo.ProcedureCases
    SET CurrentStageIndex = @TargetIndex, CurrentStageName = N'Agreement Vetting',
        CurrentStageOwner = @TargetRole, CurrentAssignedRole = @TargetRole,
        WorkflowStepId = @TargetStepId, Status = N'Open', CompletedAt = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @CaseId AND TenantId = @TenantId;

    UPDATE c SET StageIndex = s.StageIndex, IsCompleted = 0, CompletedAt = NULL, CompletedById = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.ProcedureCaseChecklistItems c JOIN @Stages s ON s.Name = c.StageName
    WHERE c.TenantId = @TenantId AND c.ProcedureCaseId = @CaseId AND c.IsDeleted = 0
      AND s.StageIndex >= @TargetIndex;

    -- Supply any vetting checks missing from a case created before the server workflow was revised.
    IF ISJSON(@TargetConfig) = 1
    BEGIN
        INSERT INTO dbo.ProcedureCaseChecklistItems
            (Id, ProcedureCaseId, StageIndex, StageName, Text, IsCompleted, CompletedById, CompletedAt,
             CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
             IsDeleted, DeletedAt, DeletedBy, TenantId)
        SELECT NEWID(), @CaseId, @TargetIndex, N'Agreement Vetting', q.Name, 0, NULL, NULL,
               @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId
        FROM (SELECT DISTINCT LTRIM(RTRIM(Name)) AS Name
              FROM OPENJSON(@TargetConfig, '$.qualityConfig.qualityChecks')
              WITH (Name nvarchar(500) '$.name', IsRequired bit '$.isRequired')
              WHERE IsRequired = 1) q
        WHERE NULLIF(LTRIM(RTRIM(q.Name)), N'') IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM dbo.ProcedureCaseChecklistItems c
            WHERE c.TenantId = @TenantId AND c.ProcedureCaseId = @CaseId AND c.IsDeleted = 0
              AND c.StageName = N'Agreement Vetting' AND c.Text = q.Name);
    END;

    DECLARE @SourceFields TABLE ([Key] nvarchar(100), Label nvarchar(150), Value nvarchar(max));
    INSERT INTO @SourceFields VALUES
        (N'legalAgreementReviewStatus', N'Legal agreement review status', N'Under Legal review - Agreement Vetting'),
        (N'customerNotificationStatus', N'Customer notification status', N'Awaiting Legal agreement review');
    UPDATE f SET Value = s.Value, UpdatedAt = @Now, UpdatedBy = @Script
    FROM dbo.ProcedureCaseFields f JOIN @SourceFields s ON s.[Key] = f.[Key]
    WHERE f.TenantId = @TenantId AND f.ProcedureCaseId = @SourceCaseId AND f.IsDeleted = 0;
    INSERT INTO dbo.ProcedureCaseFields
        (Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    SELECT NEWID(), @SourceCaseId, s.[Key], s.Label, N'text', s.Value, NULL,
           @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId
    FROM @SourceFields s WHERE NOT EXISTS (
        SELECT 1 FROM dbo.ProcedureCaseFields f WHERE f.TenantId = @TenantId
          AND f.ProcedureCaseId = @SourceCaseId AND f.IsDeleted = 0 AND f.[Key] = s.[Key]);

    INSERT INTO dbo.ProcedureCaseActivities
        (Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    VALUES
        (NEWID(), @CaseId, N'Database test rewind', N'Agreement Vetting', @Details, @ActorId, @Now,
         @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId);

    SELECT N'After (committed only when PreviewOnly = 0)' AS Snapshot,
           pc.Id, pc.ReferenceNumber, pc.CurrentStageName, pc.CurrentAssignedRole,
           wi.CurrentStepId, @SourceCaseId AS LinkedEstateCaseId
    FROM dbo.ProcedureCases pc JOIN dbo.WorkflowInstances wi ON wi.Id = pc.WorkflowInstanceId
    WHERE pc.Id = @CaseId AND pc.TenantId = @TenantId;
    SELECT N'Portal fields after rewind' AS Snapshot, [Key], Value
    FROM dbo.ProcedureCaseFields WHERE ProcedureCaseId = @SourceCaseId AND TenantId = @TenantId
      AND IsDeleted = 0 AND [Key] IN (N'legalAgreementReviewStatus', N'customerNotificationStatus',
        N'customerAcceptanceStatus', N'generatedAgreementReference', N'decisionStatus', N'moveInDate');

    IF @PreviewOnly = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'Preview rolled back. Set @PreviewOnly = 0 and rerun to apply.' AS Result;
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Committed. Refresh Legal, complete Agreement Vetting, and submit to Customer Signature Return. Then refresh My Property Requests.' AS Result;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @Error nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR('%s', 16, 1, @Error);
END CATCH;
