/*
    Adds the Legal / Estate agreement signing choice to the current Estate
    Management decision stage on an existing database.

    Run with @PreviewOnly = 1 first. Set it to 0 and rerun to commit.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @PreviewOnly bit = 1;
DECLARE @TenantId uniqueidentifier = NULL;
DECLARE @EstateCaseId uniqueidentifier = NULL;
DECLARE @EstateCaseReference nvarchar(80) = NULL;

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
      AND Module IN (N'Estate', N'PropertyManagement')
      AND EntityType = N'EstatePropertyManagementListingApplication'
      AND CurrentStageName IN (N'Management decision', N'Estate decision and agreement')
      AND Status NOT IN (N'Completed', N'Closed', N'Cancelled', N'Canceled')
      AND (@TenantId IS NULL OR TenantId = @TenantId)
      AND (@EstateCaseId IS NULL OR Id = @EstateCaseId)
      AND (@EstateCaseReference IS NULL OR ReferenceNumber = @EstateCaseReference);

    IF @MatchCount <> 1
        RAISERROR('Expected exactly one active Estate property request at Management decision.', 16, 1);

    DECLARE @WorkflowStepId uniqueidentifier;
    DECLARE @Configuration nvarchar(max);
    DECLARE @Now datetime2 = SYSUTCDATETIME();
    DECLARE @Script nvarchar(150) = N'Add-AgreementSigningLocation-To-ManagementDecision.sql';

    SELECT TOP (1)
        @EstateCaseId = pc.Id,
        @TenantId = pc.TenantId,
        @WorkflowStepId = pc.WorkflowStepId
    FROM dbo.ProcedureCases pc
    WHERE pc.IsDeleted = 0
      AND pc.Module IN (N'Estate', N'PropertyManagement')
      AND pc.EntityType = N'EstatePropertyManagementListingApplication'
      AND pc.CurrentStageName IN (N'Management decision', N'Estate decision and agreement')
      AND pc.Status NOT IN (N'Completed', N'Closed', N'Cancelled', N'Canceled')
      AND (@TenantId IS NULL OR pc.TenantId = @TenantId)
      AND (@EstateCaseId IS NULL OR pc.Id = @EstateCaseId)
      AND (@EstateCaseReference IS NULL OR pc.ReferenceNumber = @EstateCaseReference);

    IF @WorkflowStepId IS NULL
        RAISERROR('The Estate case is not linked to a workflow step.', 16, 1);

    SELECT @Configuration = Configuration
    FROM dbo.WorkflowSteps WITH (UPDLOCK, HOLDLOCK)
    WHERE Id = @WorkflowStepId AND TenantId = @TenantId AND IsDeleted = 0;

    IF @Configuration IS NULL OR ISJSON(@Configuration) <> 1
        RAISERROR('The current Management decision workflow step has no valid JSON configuration.', 16, 1);

    SELECT N'Before' AS Snapshot,
           pc.Id, pc.ReferenceNumber, pc.CurrentStageName,
           pc.WorkflowDefinitionId, pc.WorkflowStepId,
           f.Value AS AgreementSigningLocation,
           @Configuration AS StepConfiguration
    FROM dbo.ProcedureCases pc
    LEFT JOIN dbo.ProcedureCaseFields f
      ON f.TenantId = pc.TenantId AND f.ProcedureCaseId = pc.Id
     AND f.[Key] = N'agreementSigningLocation' AND f.IsDeleted = 0
    WHERE pc.Id = @EstateCaseId AND pc.TenantId = @TenantId;

    IF JSON_QUERY(@Configuration, '$.formFields') IS NULL
        SET @Configuration = JSON_MODIFY(@Configuration, '$.formFields', JSON_QUERY(N'[]'));

    IF NOT EXISTS (
        SELECT 1
        FROM OPENJSON(@Configuration, '$.formFields')
             WITH (Name nvarchar(100) '$.name') field
        WHERE field.Name = N'agreementSigningLocation')
    BEGIN
        SET @Configuration = JSON_MODIFY(
            @Configuration,
            'append $.formFields',
            JSON_QUERY(N'{"name":"agreementSigningLocation","label":"Agreement signing location","fieldType":"Select","isRequired":true,"options":["Legal","Estate"]}'));
    END;

    UPDATE dbo.WorkflowSteps
    SET Configuration = @Configuration, UpdatedAt = @Now, UpdatedBy = @Script
    WHERE Id = @WorkflowStepId AND TenantId = @TenantId AND IsDeleted = 0;

    UPDATE dbo.ProcedureCaseFields
    SET Label = N'Agreement signing location', FieldType = N'select',
        OptionsJson = N'["Legal","Estate"]',
        IsDeleted = 0, DeletedAt = NULL, DeletedBy = NULL,
        UpdatedAt = @Now, UpdatedBy = @Script
    WHERE TenantId = @TenantId AND ProcedureCaseId = @EstateCaseId
      AND [Key] = N'agreementSigningLocation';

    IF NOT EXISTS (
        SELECT 1 FROM dbo.ProcedureCaseFields
        WHERE TenantId = @TenantId AND ProcedureCaseId = @EstateCaseId
          AND [Key] = N'agreementSigningLocation')
    BEGIN
        INSERT INTO dbo.ProcedureCaseFields
            (Id, ProcedureCaseId, [Key], Label, FieldType, Value, OptionsJson,
             CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, CreatedById, LastModifiedById,
             IsDeleted, DeletedAt, DeletedBy, TenantId)
        VALUES
            (NEWID(), @EstateCaseId, N'agreementSigningLocation', N'Agreement signing location',
             N'select', NULL, N'["Legal","Estate"]',
             @Now, NULL, @Script, NULL, NULL, NULL, 0, NULL, NULL, @TenantId);
    END;

    SELECT N'After (committed only when PreviewOnly = 0)' AS Snapshot,
           pc.Id, pc.ReferenceNumber, pc.CurrentStageName,
           f.Label, f.FieldType, f.Value, f.OptionsJson,
           ws.Configuration AS StepConfiguration
    FROM dbo.ProcedureCases pc
    JOIN dbo.WorkflowSteps ws ON ws.Id = pc.WorkflowStepId AND ws.TenantId = pc.TenantId
    LEFT JOIN dbo.ProcedureCaseFields f
      ON f.TenantId = pc.TenantId AND f.ProcedureCaseId = pc.Id
     AND f.[Key] = N'agreementSigningLocation' AND f.IsDeleted = 0
    WHERE pc.Id = @EstateCaseId AND pc.TenantId = @TenantId;

    IF @PreviewOnly = 1
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'Preview rolled back. Set @PreviewOnly = 0 and rerun to apply.' AS Result;
    END
    ELSE
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Committed. Refresh the Estate request; Agreement signing location should now show Legal and Estate.' AS Result;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    DECLARE @Error nvarchar(2048) = ERROR_MESSAGE();
    RAISERROR('%s', 16, 1, @Error);
END CATCH;
