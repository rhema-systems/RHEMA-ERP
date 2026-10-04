/*
Restart the single Estate property-management case below on the imported,
published workflow while preserving its submitted fields, documents, and
procedure-case activity history.

Run this entire file in SSMS against the API database. Set @Apply = 0 for a
rollback rehearsal. Re-running after a successful migration is idempotent.
*/
GO
USE [RhemaERP_VpsTest_20260926_173800];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @CaseId uniqueidentifier = '19FC5CF0-9EAA-47A8-B443-4E0F9D1109F4';
DECLARE @ExpectedReference nvarchar(80) = N'EST-PM-20261004-0001';
DECLARE @ExpectedOldInstanceId uniqueidentifier = '7D576531-3A5F-4FAD-856F-CAA17C08C062';
DECLARE @ExpectedOldDefinitionId uniqueidentifier = 'B6E97F15-7B95-4E1C-B0A0-601590D7791B';
DECLARE @ImportMarker nvarchar(500) = N'Local Estate export 80B5A54E-04A2-4C0C-BCB1-69A5D2019B28 v6';
DECLARE @Apply bit = 1;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @TenantId uniqueidentifier;
DECLARE @ActorId uniqueidentifier;
DECLARE @CurrentInstanceId uniqueidentifier;
DECLARE @CurrentDefinitionId uniqueidentifier;
DECLARE @TargetDefinitionId uniqueidentifier;
DECLARE @TargetEntityTypeId uniqueidentifier;
DECLARE @StartStepId uniqueidentifier;
DECLARE @StartStepName nvarchar(120);
DECLARE @StartRole nvarchar(150);
DECLARE @NewInstanceId uniqueidentifier = NEWID();
DECLARE @NewStepInstanceId uniqueidentifier = NEWID();
DECLARE @OldEntityId uniqueidentifier;
DECLARE @OldInitiatedById uniqueidentifier;
DECLARE @OldPriority int;
DECLARE @OldDataContext nvarchar(max);
DECLARE @OldData nvarchar(max);
DECLARE @OwnTransaction bit = CASE WHEN @@TRANCOUNT = 0 THEN 1 ELSE 0 END;

SELECT
    @TenantId = c.TenantId,
    @ActorId = COALESCE(c.LastActionById, c.OpenedById),
    @CurrentInstanceId = c.WorkflowInstanceId,
    @CurrentDefinitionId = c.WorkflowDefinitionId
FROM dbo.ProcedureCases c
WHERE c.Id = @CaseId
  AND c.ReferenceNumber = @ExpectedReference
  AND c.IsDeleted = 0;

IF @TenantId IS NULL
    THROW 51100, 'The exact Estate case/reference was not found in this database.', 1;

SELECT TOP (1)
    @TargetDefinitionId = d.Id,
    @TargetEntityTypeId = d.EntityTypeId
FROM dbo.WorkflowDefinitions d
JOIN dbo.WorkflowEntityTypes e ON e.Id = d.EntityTypeId
WHERE d.TenantId = @TenantId
  AND e.TenantId = @TenantId
  AND e.Code = N'EstatePropertyManagementListingApplication'
  AND d.ChangeSummary = @ImportMarker
  AND d.IsActive = 1
  AND d.LifecycleStatus = 1
  AND d.IsDeleted = 0
  AND e.IsDeleted = 0
ORDER BY d.Version DESC, d.PublishedAt DESC;

IF @TargetDefinitionId IS NULL
    THROW 51101, 'The imported published Estate workflow is missing. Run Import-LocalEstateListingWorkflow.sql first.', 1;

SELECT TOP (1)
    @StartStepId = s.Id,
    @StartStepName = s.Name,
    @StartRole = s.RequiredRole
FROM dbo.WorkflowSteps s
WHERE s.WorkflowDefinitionId = @TargetDefinitionId
  AND s.TenantId = @TenantId
  AND s.IsStartStep = 1
  AND s.IsDeleted = 0
ORDER BY s.[Order];

IF @StartStepId IS NULL
    THROW 51102, 'The imported Estate workflow has no active start step.', 1;

IF @CurrentDefinitionId = @TargetDefinitionId
BEGIN
    SELECT N'Already migrated; no changes made.' AS Result,
        c.ReferenceNumber, c.CurrentStageIndex, c.CurrentStageName,
        c.WorkflowDefinitionId, c.WorkflowInstanceId,
        (SELECT COUNT(*) FROM dbo.ProcedureCaseChecklistItems x
         WHERE x.ProcedureCaseId = c.Id AND x.IsDeleted = 0) AS ChecklistItems
    FROM dbo.ProcedureCases c WHERE c.Id = @CaseId;
    RETURN;
END;

IF @CurrentInstanceId <> @ExpectedOldInstanceId OR @CurrentDefinitionId <> @ExpectedOldDefinitionId
    THROW 51103, 'The case workflow changed after inspection. Migration stopped without changes.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.WorkflowStepInstances
    WHERE WorkflowInstanceId = @CurrentInstanceId
      AND TenantId = @TenantId
      AND Status = 2
      AND IsDeleted = 0)
    THROW 51104, 'The old workflow has completed steps. Review it manually before migration.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.ProcedureCaseChecklistItems
    WHERE ProcedureCaseId = @CaseId
      AND TenantId = @TenantId
      AND IsCompleted = 1
      AND IsDeleted = 0)
    THROW 51105, 'The case has completed checklist evidence. Review it manually before migration.', 1;

SELECT
    @OldEntityId = i.EntityId,
    @OldInitiatedById = i.InitiatedById,
    @OldPriority = i.Priority,
    @OldDataContext = i.DataContext,
    @OldData = i.Data
FROM dbo.WorkflowInstances i
WHERE i.Id = @CurrentInstanceId
  AND i.TenantId = @TenantId
  AND i.WorkflowDefinitionId = @CurrentDefinitionId
  AND i.Status IN (0, 1)
  AND i.IsDeleted = 0;

IF @OldEntityId IS NULL
    THROW 51106, 'The old active workflow instance was not found in the expected state.', 1;

IF @ActorId IS NULL SET @ActorId = @OldInitiatedById;
IF @ActorId IS NULL
    THROW 51107, 'No valid audit actor could be resolved for this case.', 1;

IF @OwnTransaction = 1 BEGIN TRANSACTION;
ELSE SAVE TRANSACTION EstateCaseWorkflowMigration;

BEGIN TRY
    -- Retain the old workflow as an auditable cancelled instance.
    UPDATE dbo.WorkflowApprovals
    SET Status = 4,
        ProcessedDate = @Now,
        ProcessedById = @ActorId,
        Comments = CONCAT(COALESCE(Comments + N' ', N''),
            N'Expired because the case was restarted on the corrected published Estate workflow.'),
        UpdatedAt = @Now,
        UpdatedBy = N'EstateCaseWorkflowMigration',
        LastModifiedById = @ActorId
    WHERE TenantId = @TenantId
      AND StepInstanceId IN (
          SELECT Id FROM dbo.WorkflowStepInstances
          WHERE WorkflowInstanceId = @CurrentInstanceId AND IsDeleted = 0)
      AND Status IN (0, 6)
      AND IsDeleted = 0;

    UPDATE dbo.WorkflowStepInstances
    SET Status = 3,
        CompletedDate = COALESCE(CompletedDate, @Now),
        Comments = CONCAT(COALESCE(Comments + N' ', N''),
            N'Cancelled because the case was restarted on the corrected published Estate workflow.'),
        UpdatedAt = @Now,
        UpdatedBy = N'EstateCaseWorkflowMigration',
        LastModifiedById = @ActorId
    WHERE WorkflowInstanceId = @CurrentInstanceId
      AND TenantId = @TenantId
      AND Status IN (0, 1)
      AND IsDeleted = 0;

    UPDATE dbo.WorkflowInstances
    SET Status = 3,
        CompletedDate = COALESCE(CompletedDate, @Now),
        CancelledDate = COALESCE(CancelledDate, @Now),
        Notes = CONCAT(COALESCE(Notes + N' ', N''),
            N'Replaced by workflow instance ', CONVERT(nvarchar(36), @NewInstanceId),
            N' for corrected Estate intake processing.'),
        UpdatedAt = @Now,
        UpdatedBy = N'EstateCaseWorkflowMigration',
        LastModifiedById = @ActorId
    WHERE Id = @CurrentInstanceId
      AND TenantId = @TenantId
      AND Status IN (0, 1)
      AND IsDeleted = 0;

    IF @@ROWCOUNT <> 1
        THROW 51108, 'The old workflow instance changed concurrently. Migration rolled back.', 1;

    INSERT INTO dbo.WorkflowActivityLogs
        (Id, WorkflowInstanceId, StepInstanceId, ActivityType, Title, Description,
         PerformedById, ActivityDate, CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    VALUES
        (NEWID(), @CurrentInstanceId, NULL, 3, N'Workflow cancelled for corrected restart',
         CONCAT(N'Case ', @ExpectedReference, N' moved to corrected imported Estate workflow instance ',
                CONVERT(nvarchar(36), @NewInstanceId), N'.'),
         @ActorId, @Now, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId);

    INSERT INTO dbo.WorkflowInstances
        (Id, WorkflowDefinitionId, EntityId, EntityTypeId, Status, Priority,
         InitiatedById, StartedById, CreatedDate, StartedDate, DataContext, Data, Notes,
         CurrentStepId, CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    VALUES
        (@NewInstanceId, @TargetDefinitionId, @OldEntityId, @TargetEntityTypeId, 1,
         COALESCE(@OldPriority, 0), COALESCE(@OldInitiatedById, @ActorId), @ActorId,
         @Now, @Now, @OldDataContext, @OldData,
         CONCAT(N'Restarted from workflow instance ', CONVERT(nvarchar(36), @CurrentInstanceId),
                N' with submitted case data and history preserved.'),
         @StartStepId, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId);

    INSERT INTO dbo.WorkflowStepInstances
        (Id, WorkflowInstanceId, WorkflowStepId, Status, CreatedDate, StartedDate,
         RetryCount, CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    VALUES
        (@NewStepInstanceId, @NewInstanceId, @StartStepId, 0, @Now, @Now,
         0, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId);

    INSERT INTO dbo.WorkflowActivityLogs
        (Id, WorkflowInstanceId, StepInstanceId, ActivityType, Title, Description,
         PerformedById, ActivityDate, CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    VALUES
        (NEWID(), @NewInstanceId, @NewStepInstanceId, 1, N'Workflow started',
         CONCAT(N'Corrected workflow started for case ', @ExpectedReference,
                N' at ', @StartStepName, N'.'),
         @ActorId, @Now, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId),
        (NEWID(), @NewInstanceId, @NewStepInstanceId, 4, N'Step started',
         @StartStepName, @ActorId, @Now, @Now,
         N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId);

    -- The imported workflow owns the checklist. Existing business fields and documents are untouched.
    INSERT INTO dbo.ProcedureCaseChecklistItems
        (Id, ProcedureCaseId, StageIndex, StageName, Text, IsCompleted,
         CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    SELECT NEWID(), @CaseId, s.[Order] - 1, s.Name,
        COALESCE(NULLIF(JSON_VALUE(j.value, '$.name'), N''), JSON_VALUE(j.value, '$.description')),
        0, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId
    FROM dbo.WorkflowSteps s
    CROSS APPLY OPENJSON(s.Configuration, '$.qualityConfig.qualityChecks') j
    WHERE s.WorkflowDefinitionId = @TargetDefinitionId
      AND s.TenantId = @TenantId
      AND s.IsDeleted = 0
      AND JSON_VALUE(j.value, '$.isRequired') = N'true'
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ProcedureCaseChecklistItems x
          WHERE x.ProcedureCaseId = @CaseId
            AND x.TenantId = @TenantId
            AND x.StageIndex = s.[Order] - 1
            AND x.StageName = s.Name
            AND x.Text = COALESCE(NULLIF(JSON_VALUE(j.value, '$.name'), N''), JSON_VALUE(j.value, '$.description'))
            AND x.IsDeleted = 0);

    IF (SELECT COUNT(*) FROM dbo.ProcedureCaseChecklistItems
        WHERE ProcedureCaseId = @CaseId AND TenantId = @TenantId AND IsDeleted = 0) <> 11
        THROW 51109, 'Expected eleven unchecked checklist items. Migration rolled back.', 1;

    UPDATE dbo.ProcedureCases
    SET Status = N'Open',
        CurrentStageIndex = 0,
        CurrentStageName = @StartStepName,
        CurrentStageOwner = @StartRole,
        CurrentAssignedRole = CONCAT(@StartRole, N' / ', @StartStepName),
        WorkflowDefinitionId = @TargetDefinitionId,
        WorkflowInstanceId = @NewInstanceId,
        WorkflowStepId = @StartStepId,
        LastActionById = @ActorId,
        CompletedAt = NULL,
        UpdatedAt = @Now,
        UpdatedBy = N'EstateCaseWorkflowMigration',
        LastModifiedById = @ActorId
    WHERE Id = @CaseId
      AND TenantId = @TenantId
      AND WorkflowInstanceId = @CurrentInstanceId
      AND WorkflowDefinitionId = @CurrentDefinitionId
      AND IsDeleted = 0;

    IF @@ROWCOUNT <> 1
        THROW 51110, 'The case changed concurrently. Migration rolled back.', 1;

    INSERT INTO dbo.ProcedureCaseActivities
        (Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
         CreatedAt, CreatedBy, CreatedById, IsDeleted, TenantId)
    VALUES
        (NEWID(), @CaseId, N'Workflow migrated to intake', @StartStepName,
         CONCAT(N'Preserved submitted fields, documents, and history. Replaced workflow instance ',
                CONVERT(nvarchar(36), @CurrentInstanceId), N' with ',
                CONVERT(nvarchar(36), @NewInstanceId), N'.'),
         @ActorId, @Now, @Now, N'EstateCaseWorkflowMigration', @ActorId, 0, @TenantId);

    SELECT DB_NAME() AS DatabaseName, c.ReferenceNumber, c.Status,
        c.CurrentStageIndex, c.CurrentStageName, c.CurrentStageOwner,
        c.WorkflowDefinitionId, c.WorkflowInstanceId,
        (SELECT COUNT(*) FROM dbo.ProcedureCaseChecklistItems x
         WHERE x.ProcedureCaseId = c.Id AND x.IsDeleted = 0) AS ChecklistItems,
        (SELECT COUNT(*) FROM dbo.ProcedureCaseChecklistItems x
         WHERE x.ProcedureCaseId = c.Id AND x.IsDeleted = 0 AND x.IsCompleted = 1) AS CompletedChecklistItems,
        @Apply AS ApplyRequested
    FROM dbo.ProcedureCases c WHERE c.Id = @CaseId;

    IF @Apply = 0
    BEGIN
        IF @OwnTransaction = 1 ROLLBACK TRANSACTION;
        ELSE ROLLBACK TRANSACTION EstateCaseWorkflowMigration;
        PRINT 'Rehearsal passed; all changes rolled back.';
    END
    ELSE
    BEGIN
        IF @OwnTransaction = 1 COMMIT TRANSACTION;
        PRINT 'Case migrated to the imported Estate workflow intake.';
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
    BEGIN
        IF @OwnTransaction = 1 ROLLBACK TRANSACTION;
        ELSE IF XACT_STATE() = 1 ROLLBACK TRANSACTION EstateCaseWorkflowMigration;
    END;
    THROW;
END CATCH;
GO
