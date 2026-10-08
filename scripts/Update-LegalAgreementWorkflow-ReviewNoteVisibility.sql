/*
    Emergency database workaround for the deployed Legal workspace.

    The deployed frontend hides the exact generic document name
    "Legal review note" for Legal-specific cases. This script renames that
    requirement to "Agreement vetting note" in:

      1. The Agreement Vetting step of the workflow used by LEG-20261008-0001.
      2. The workflow designer JSON, when that matching node is present.
      3. The existing document row for LEG-20261008-0001.

    Run with @PreviewOnly = 1 first. If the snapshots identify the intended
    workflow, step, and case, set @PreviewOnly = 0 and rerun to commit.

    No roles, routes, signatures, checklists, files, or other cases are changed.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @CaseReference nvarchar(80) = N'LEG-20261008-0001';
DECLARE @OldDocumentName nvarchar(180) = N'Legal review note';
DECLARE @NewDocumentName nvarchar(180) = N'Agreement vetting note';
DECLARE @Script nvarchar(100) = N'DB emergency workflow visibility fix';

IF @@TRANCOUNT <> 0
BEGIN
    RAISERROR('Run this script outside an existing transaction.', 16, 1);
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @CaseId uniqueidentifier;
    DECLARE @TenantId uniqueidentifier;
    DECLARE @WorkflowDefinitionId uniqueidentifier;
    DECLARE @AgreementStepId uniqueidentifier;
    DECLARE @ActorId uniqueidentifier;
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @CaseMatches int;

    SELECT @CaseMatches = COUNT(*)
    FROM dbo.ProcedureCases WITH (UPDLOCK, HOLDLOCK)
    WHERE IsDeleted = 0
      AND EntityType = N'LegalPropertyAgreementReview'
      AND ReferenceNumber = @CaseReference
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled');

    IF @CaseMatches <> 1
        RAISERROR('Expected exactly one active Legal property agreement case with reference %s.', 16, 1, @CaseReference);

    SELECT
        @CaseId = Id,
        @TenantId = TenantId,
        @WorkflowDefinitionId = WorkflowDefinitionId,
        @ActorId = COALESCE(LastActionById, OpenedById, CreatedById)
    FROM dbo.ProcedureCases
    WHERE IsDeleted = 0
      AND EntityType = N'LegalPropertyAgreementReview'
      AND ReferenceNumber = @CaseReference
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled');

    IF @WorkflowDefinitionId IS NULL
        RAISERROR('The selected case has no workflow definition. No change was made.', 16, 1);

    IF @ActorId IS NULL
        RAISERROR('The selected case has no user available for the audit activity. No change was made.', 16, 1);

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.ProcedureCases
        WHERE Id = @CaseId
          AND TenantId = @TenantId
          AND CurrentStageName = N'Agreement Vetting'
          AND IsDeleted = 0)
        RAISERROR('The selected case is not currently at Agreement Vetting. No change was made.', 16, 1);

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.WorkflowDefinitions wd WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.WorkflowEntityTypes et
          ON et.Id = wd.EntityTypeId
         AND et.TenantId = wd.TenantId
         AND et.IsDeleted = 0
        WHERE wd.Id = @WorkflowDefinitionId
          AND wd.TenantId = @TenantId
          AND wd.IsDeleted = 0
          AND wd.IsActive = 1
          AND wd.LifecycleStatus = 1
          AND (et.Code = N'LegalPropertyAgreementReview'
               OR et.Name = N'LegalPropertyAgreementReview'))
        RAISERROR('The case is not linked to an active published LegalPropertyAgreementReview workflow.', 16, 1);

    IF (SELECT COUNT(*)
        FROM dbo.WorkflowSteps WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId
          AND WorkflowDefinitionId = @WorkflowDefinitionId
          AND IsDeleted = 0
          AND Name = N'Agreement Vetting') <> 1
        RAISERROR('Expected exactly one Agreement Vetting step in the linked workflow.', 16, 1);

    SELECT @AgreementStepId = Id
    FROM dbo.WorkflowSteps
    WHERE TenantId = @TenantId
      AND WorkflowDefinitionId = @WorkflowDefinitionId
      AND IsDeleted = 0
      AND Name = N'Agreement Vetting';

    DECLARE @StepConfiguration nvarchar(max);
    SELECT @StepConfiguration = Configuration
    FROM dbo.WorkflowSteps
    WHERE Id = @AgreementStepId;

    IF ISJSON(@StepConfiguration) <> 1
        RAISERROR('Agreement Vetting has no valid JSON configuration.', 16, 1);

    DECLARE @StepRequirementMatches int;
    DECLARE @StepRequirementIndex int;

    SELECT
        @StepRequirementMatches = COUNT(*),
        @StepRequirementIndex = MIN(TRY_CONVERT(int, requirement.[key]))
    FROM OPENJSON(@StepConfiguration, '$.taskConfig.documentRequirements') requirement
    WHERE JSON_VALUE(requirement.value, '$.documentName') = @OldDocumentName;

    IF @StepRequirementMatches <> 1 OR @StepRequirementIndex IS NULL
        RAISERROR('Expected exactly one Legal review note requirement in Agreement Vetting.', 16, 1);

    IF EXISTS (
        SELECT 1
        FROM OPENJSON(@StepConfiguration, '$.taskConfig.documentRequirements') requirement
        WHERE JSON_VALUE(requirement.value, '$.documentName') = @NewDocumentName)
        RAISERROR('Agreement vetting note already exists in Agreement Vetting.', 16, 1);

    IF (SELECT COUNT(*)
        FROM dbo.ProcedureCaseDocuments WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId
          AND ProcedureCaseId = @CaseId
          AND IsDeleted = 0
          AND Name = @OldDocumentName
          AND RequiredFrom = N'Agreement Vetting') <> 1
        RAISERROR('Expected exactly one active Legal review note document on the selected case.', 16, 1);

    IF EXISTS (
        SELECT 1
        FROM dbo.ProcedureCaseDocuments
        WHERE TenantId = @TenantId
          AND ProcedureCaseId = @CaseId
          AND IsDeleted = 0
          AND Name = @NewDocumentName)
        RAISERROR('Agreement vetting note already exists on the selected case.', 16, 1);

    SELECT
        N'Before - workflow step' AS Snapshot,
        wd.Id AS WorkflowDefinitionId,
        wd.Name AS WorkflowName,
        wd.Version,
        ws.Id AS WorkflowStepId,
        ws.Name AS WorkflowStepName,
        JSON_VALUE(requirement.value, '$.documentName') AS DocumentName,
        JSON_VALUE(requirement.value, '$.documentType') AS DocumentType,
        JSON_VALUE(requirement.value, '$.isRequired') AS IsRequired
    FROM dbo.WorkflowDefinitions wd
    JOIN dbo.WorkflowSteps ws ON ws.WorkflowDefinitionId = wd.Id
    CROSS APPLY OPENJSON(ws.Configuration, '$.taskConfig.documentRequirements') requirement
    WHERE wd.Id = @WorkflowDefinitionId
      AND ws.Id = @AgreementStepId
      AND JSON_VALUE(requirement.value, '$.documentName') = @OldDocumentName;

    SELECT
        N'Before - current case' AS Snapshot,
        pc.ReferenceNumber,
        pc.CurrentStageName,
        d.Id AS DocumentId,
        d.Name,
        d.RequiredFrom,
        d.ProvidedBy,
        d.IsMandatory,
        d.FileName,
        d.FileUrl
    FROM dbo.ProcedureCases pc
    JOIN dbo.ProcedureCaseDocuments d
      ON d.ProcedureCaseId = pc.Id
     AND d.TenantId = pc.TenantId
     AND d.IsDeleted = 0
    WHERE pc.Id = @CaseId
      AND pc.TenantId = @TenantId
      AND d.Name = @OldDocumentName;

    DECLARE @StepDocumentPath nvarchar(300) =
        N'$.taskConfig.documentRequirements['
        + CONVERT(nvarchar(20), @StepRequirementIndex)
        + N'].documentName';
    SET @StepConfiguration = JSON_MODIFY(
        @StepConfiguration,
        @StepDocumentPath,
        @NewDocumentName);

    UPDATE dbo.WorkflowSteps
    SET Configuration = @StepConfiguration,
        UpdatedAt = @Now,
        UpdatedBy = @Script,
        LastModifiedById = @ActorId
    WHERE Id = @AgreementStepId
      AND TenantId = @TenantId
      AND WorkflowDefinitionId = @WorkflowDefinitionId;

    IF @@ROWCOUNT <> 1
        RAISERROR('Agreement Vetting workflow configuration was not updated exactly once.', 16, 1);

    DECLARE @DefinitionConfiguration nvarchar(max);
    SELECT @DefinitionConfiguration = Configuration
    FROM dbo.WorkflowDefinitions
    WHERE Id = @WorkflowDefinitionId;

    DECLARE @DesignerRequirementMatches int = 0;
    DECLARE @DesignerNodeIndex int;
    DECLARE @DesignerRequirementIndex int;

    IF ISJSON(@DefinitionConfiguration) = 1
    BEGIN
        SELECT
            @DesignerRequirementMatches = COUNT(*),
            @DesignerNodeIndex = MIN(TRY_CONVERT(int, node.[key])),
            @DesignerRequirementIndex = MIN(TRY_CONVERT(int, requirement.[key]))
        FROM OPENJSON(@DefinitionConfiguration, '$.designer.nodes') node
        CROSS APPLY OPENJSON(node.value, '$.data.documentRequirements') requirement
        WHERE JSON_VALUE(node.value, '$.data.label') = N'Agreement Vetting'
          AND JSON_VALUE(requirement.value, '$.documentName') = @OldDocumentName;

        IF @DesignerRequirementMatches > 1
            RAISERROR('Multiple Legal review note requirements were found in the workflow designer JSON.', 16, 1);

        IF @DesignerRequirementMatches = 1
        BEGIN
            DECLARE @DesignerDocumentPath nvarchar(400) =
                N'$.designer.nodes['
                + CONVERT(nvarchar(20), @DesignerNodeIndex)
                + N'].data.documentRequirements['
                + CONVERT(nvarchar(20), @DesignerRequirementIndex)
                + N'].documentName';
            SET @DefinitionConfiguration = JSON_MODIFY(
                @DefinitionConfiguration,
                @DesignerDocumentPath,
                @NewDocumentName);
        END;
    END;

    UPDATE dbo.WorkflowDefinitions
    SET Configuration = @DefinitionConfiguration,
        UpdatedAt = @Now,
        UpdatedBy = @Script,
        LastModifiedById = @ActorId
    WHERE Id = @WorkflowDefinitionId
      AND TenantId = @TenantId;

    IF @@ROWCOUNT <> 1
        RAISERROR('Workflow definition was not updated exactly once.', 16, 1);

    UPDATE dbo.ProcedureCaseDocuments
    SET Name = @NewDocumentName,
        UpdatedAt = @Now,
        UpdatedBy = @Script,
        LastModifiedById = @ActorId
    WHERE TenantId = @TenantId
      AND ProcedureCaseId = @CaseId
      AND IsDeleted = 0
      AND Name = @OldDocumentName
      AND RequiredFrom = N'Agreement Vetting';

    IF @@ROWCOUNT <> 1
        RAISERROR('The current case document was not updated exactly once.', 16, 1);

    INSERT INTO dbo.ProcedureCaseActivities
        (Id, ProcedureCaseId, Action, StageName, Details, PerformedById, PerformedAt,
         CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
         IsDeleted, DeletedAt, DeletedBy, TenantId)
    VALUES
        (NEWID(), @CaseId, N'Workflow document visibility workaround', N'Agreement Vetting',
         N'Renamed the Agreement Vetting Legal review note requirement to Agreement vetting note in the linked workflow and current case so the deployed Legal workspace displays its upload control.',
         @ActorId, @Now, @Now, NULL, @Script, NULL, NULL, NULL,
         0, NULL, NULL, @TenantId);

    SELECT
        N'After - workflow step (committed only when PreviewOnly = 0)' AS Snapshot,
        wd.Id AS WorkflowDefinitionId,
        wd.Name AS WorkflowName,
        wd.Version,
        ws.Id AS WorkflowStepId,
        ws.Name AS WorkflowStepName,
        JSON_VALUE(requirement.value, '$.documentName') AS DocumentName,
        JSON_VALUE(requirement.value, '$.documentType') AS DocumentType,
        JSON_VALUE(requirement.value, '$.isRequired') AS IsRequired,
        @DesignerRequirementMatches AS DesignerRequirementsUpdated
    FROM dbo.WorkflowDefinitions wd
    JOIN dbo.WorkflowSteps ws ON ws.WorkflowDefinitionId = wd.Id
    CROSS APPLY OPENJSON(ws.Configuration, '$.taskConfig.documentRequirements') requirement
    WHERE wd.Id = @WorkflowDefinitionId
      AND ws.Id = @AgreementStepId
      AND JSON_VALUE(requirement.value, '$.documentName') = @NewDocumentName;

    SELECT
        N'After - current case (committed only when PreviewOnly = 0)' AS Snapshot,
        pc.ReferenceNumber,
        pc.CurrentStageName,
        d.Id AS DocumentId,
        d.Name,
        d.RequiredFrom,
        d.ProvidedBy,
        d.IsMandatory,
        d.FileName,
        d.FileUrl
    FROM dbo.ProcedureCases pc
    JOIN dbo.ProcedureCaseDocuments d
      ON d.ProcedureCaseId = pc.Id
     AND d.TenantId = pc.TenantId
     AND d.IsDeleted = 0
    WHERE pc.Id = @CaseId
      AND pc.TenantId = @TenantId
      AND d.Name = @NewDocumentName;

    IF @PreviewOnly = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'Preview rolled back. Set @PreviewOnly = 0 and rerun to update the workflow and current case.' AS Result;
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Committed. Refresh LEG-20261008-0001 and upload Agreement vetting note from the Documents tab.' AS Result;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @Error nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR('%s', 16, 1, @Error);
END CATCH;
