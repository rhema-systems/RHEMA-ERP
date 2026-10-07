-- Read-only Up preconditions for the HR performance final closure:
--   20261001120541_PerformanceClosureBatch2            (data-precondition)
--   20261001172838_PerformanceClosureWorkflowRetrofit  (data-precondition)
-- Only table variables are written. Each check mirrors one THROW in a migration's
-- Up, under the same conditions, and counts what that THROW counts. The Down
-- refusals are not probed: a deployment only applies Up.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'HrPerformanceFinalClosure.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Batch2Applied bit = CASE WHEN EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory
     WHERE MigrationId = N'20261001120541_PerformanceClosureBatch2') THEN 1 ELSE 0 END;
DECLARE @RetrofitApplied bit = CASE WHEN EXISTS
    (SELECT 1 FROM dbo.__EFMigrationsHistory
     WHERE MigrationId = N'20261001172838_PerformanceClosureWorkflowRetrofit') THEN 1 ELSE 0 END;

-- Batch 2 creates four filtered unique indexes, each only while it is absent, and
-- refuses when live rows already hold the key twice. The counts run in dynamic SQL
-- so a missing column is reported instead of failing this probe's compile.
IF @Batch2Applied = 0
BEGIN
    IF OBJECT_ID(N'dbo.AppraisalOutcomeRecommendations', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.AppraisalOutcomeRecommendations')
                         AND name = N'UX_AppraisalOutcomeRecommendations_Appraisal_Type_Open')
    BEGIN
        IF COL_LENGTH(N'dbo.AppraisalOutcomeRecommendations', N'PerformanceAppraisalId') IS NULL
           OR COL_LENGTH(N'dbo.AppraisalOutcomeRecommendations', N'RecommendationType') IS NULL
           OR COL_LENGTH(N'dbo.AppraisalOutcomeRecommendations', N'Status') IS NULL
           OR COL_LENGTH(N'dbo.AppraisalOutcomeRecommendations', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrPerformanceFinalClosure.RequiredColumnMissing:AppraisalOutcomeRecommendations', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrPerformanceFinalClosure.DuplicateOpenRecommendations'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.AppraisalOutcomeRecommendations
                      WHERE IsDeleted = 0 AND Status IN (1, 2, 3)
                      GROUP BY PerformanceAppraisalId, RecommendationType
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    IF OBJECT_ID(N'dbo.PerformanceImprovementPlans', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.PerformanceImprovementPlans')
                         AND name = N'UX_PerformanceImprovementPlans_Tenant_PipNumber')
    BEGIN
        IF COL_LENGTH(N'dbo.PerformanceImprovementPlans', N'TenantId') IS NULL
           OR COL_LENGTH(N'dbo.PerformanceImprovementPlans', N'PipNumber') IS NULL
           OR COL_LENGTH(N'dbo.PerformanceImprovementPlans', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrPerformanceFinalClosure.RequiredColumnMissing:PerformanceImprovementPlans', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrPerformanceFinalClosure.DuplicatePipNumbers'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.PerformanceImprovementPlans
                      WHERE IsDeleted = 0
                      GROUP BY TenantId, PipNumber
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    -- Batch 2 creates ProbationExtensionRequests, so at the previous migration the
    -- table is absent and has nothing to refuse; only a table already there can.
    IF OBJECT_ID(N'dbo.ProbationExtensionRequests', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.ProbationExtensionRequests')
                         AND name = N'UX_ProbationExtensionRequest_OneOpenPerProbation')
    BEGIN
        IF COL_LENGTH(N'dbo.ProbationExtensionRequests', N'ProbationPeriodId') IS NULL
           OR COL_LENGTH(N'dbo.ProbationExtensionRequests', N'Status') IS NULL
           OR COL_LENGTH(N'dbo.ProbationExtensionRequests', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrPerformanceFinalClosure.RequiredColumnMissing:ProbationExtensionRequests', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrPerformanceFinalClosure.DuplicateOpenExtensionRequests'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.ProbationExtensionRequests
                      WHERE Status IN (1, 2) AND IsDeleted = 0
                      GROUP BY ProbationPeriodId
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;

    -- Batch 2 adds ProbationExtensions.ExtensionRequestId empty, so only a column
    -- that already exists can hold a request applied twice.
    IF OBJECT_ID(N'dbo.ProbationExtensions', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProbationExtensions', N'ExtensionRequestId') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.ProbationExtensions')
                         AND name = N'UX_ProbationExtension_ExtensionRequestId')
    BEGIN
        IF COL_LENGTH(N'dbo.ProbationExtensions', N'IsDeleted') IS NULL
            INSERT @Checks VALUES(N'HrPerformanceFinalClosure.RequiredColumnMissing:ProbationExtensions.IsDeleted', 1);
        ELSE
            INSERT @Checks EXEC sys.sp_executesql N'
                SELECT N''HrPerformanceFinalClosure.DuplicateAppliedExtensionRequests'', COUNT_BIG(*)
                FROM (SELECT 1 AS Found FROM dbo.ProbationExtensions
                      WHERE ExtensionRequestId IS NOT NULL AND IsDeleted = 0
                      GROUP BY ExtensionRequestId
                      HAVING COUNT_BIG(*) > 1) duplicates;';
    END;
END;

-- The retrofit takes the HR and TenantAdmin role rules off the approval steps of
-- the two seeded proposal routes, and refuses when a step would be left with no
-- approver. The steps are selected first, as the migration does, so OPENJSON only
-- reads configurations that are valid JSON.
IF @RetrofitApplied = 0
   AND OBJECT_ID(N'dbo.WorkflowSteps', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkflowDefinitions', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkflowEntityTypes', N'U') IS NOT NULL
    INSERT @Checks EXEC sys.sp_executesql N'
        DECLARE @steps TABLE (StepId uniqueidentifier NOT NULL PRIMARY KEY);
        INSERT @steps (StepId)
        SELECT s.Id
        FROM dbo.WorkflowSteps s
        JOIN dbo.WorkflowDefinitions d ON d.Id = s.WorkflowDefinitionId
        JOIN dbo.WorkflowEntityTypes et ON et.Id = d.EntityTypeId
        JOIN (VALUES
                (N''Salary Review Approval'', N''SALARY_REVIEW_PROPOSAL'', N''Salary Review Proposal''),
                (N''Employment Action Approval'', N''EMPLOYMENT_ACTION_PROPOSAL'', N''Employment Action Proposal'')
             ) r (DefinitionName, EntityCode, EntityName)
          ON r.DefinitionName = d.Name AND (et.Code = r.EntityCode OR et.Name = r.EntityName)
        WHERE s.IsDeleted = 0 AND d.IsDeleted = 0 AND s.StepType = 2
          AND ISJSON(s.Configuration) = 1
          AND JSON_QUERY(s.Configuration, ''$.approvalConfig'') IS NOT NULL;

        SELECT N''HrPerformanceFinalClosure.ProposalStepsLeftWithoutApprover'', COUNT_BIG(*)
        FROM @steps st
        JOIN dbo.WorkflowSteps s ON s.Id = st.StepId
        WHERE NOT EXISTS (
            SELECT 1 FROM OPENJSON(s.Configuration, ''$.approvalConfig.approverRules'') ar
            WHERE NOT (JSON_VALUE(ar.[value], ''$.assignmentType'') = N''Role''
                       AND JSON_VALUE(ar.[value], ''$.role'') IN (N''HR'', N''TenantAdmin'')));';

SELECT CheckName, AffectedRows FROM @Checks WHERE AffectedRows > 0 ORDER BY CheckName;
