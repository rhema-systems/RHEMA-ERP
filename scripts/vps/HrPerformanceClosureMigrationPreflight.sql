-- Read-only Up preconditions for 20260928231446_PerformanceClosureBatch1.
-- Only table variables are written. These checks mirror the duplicate-refusal
-- guards and reject partial or unexpected pending schema.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'HrPerformanceClosure.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Applied bit = 0;
EXEC sys.sp_executesql
    N'SELECT @Applied = CASE WHEN EXISTS
      (SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N''20260928231446_PerformanceClosureBatch1'')
      THEN 1 ELSE 0 END;',
    N'@Applied bit OUTPUT', @Applied = @Applied OUTPUT;

IF @Applied = 0
BEGIN
    DECLARE @RequiredTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @RequiredTables VALUES
        (N'AppraisalSettings'),
        (N'AppraisalAppeals'),
        (N'PerformanceAppraisalCriterionConfigs'),
        (N'CriterionScores'),
        (N'EmployeeGoalAppraisalAssessments'),
        (N'EvaluatorEvaluations');

    INSERT @Checks
    SELECT N'HrPerformanceClosure.RequiredTableMissing:' + TableName, 1
    FROM @RequiredTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    IF COL_LENGTH(N'dbo.AppraisalSettings', N'IsDefault') IS NOT NULL
        INSERT @Checks VALUES(N'HrPerformanceClosure.UnexpectedPendingSchema:AppraisalSettings.IsDefault', 1);
    IF COL_LENGTH(N'dbo.PerformanceAppraisalCriterionConfigs', N'EmployeeGoalId') IS NOT NULL
        INSERT @Checks VALUES(N'HrPerformanceClosure.UnexpectedPendingSchema:PerformanceAppraisalCriterionConfigs.EmployeeGoalId', 1);
    IF COL_LENGTH(N'dbo.CriterionScores', N'CriterionConfigId') IS NOT NULL
        INSERT @Checks VALUES(N'HrPerformanceClosure.UnexpectedPendingSchema:CriterionScores.CriterionConfigId', 1);
    IF OBJECT_ID(N'dbo.AppraisalScoreChanges', N'U') IS NOT NULL
        INSERT @Checks VALUES(N'HrPerformanceClosure.UnexpectedPendingSchema:AppraisalScoreChanges', 1);

    IF OBJECT_ID(N'dbo.AppraisalAppeals', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.AppraisalAppeals', N'PerformanceAppraisalId') IS NOT NULL
       AND COL_LENGTH(N'dbo.AppraisalAppeals', N'Status') IS NOT NULL
       AND COL_LENGTH(N'dbo.AppraisalAppeals', N'IsDeleted') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''HrPerformanceClosure.DuplicateOpenAppeals'', COUNT_BIG(*)
            FROM (SELECT PerformanceAppraisalId FROM dbo.AppraisalAppeals
                  WHERE Status IN (1,2,3) AND IsDeleted=0
                  GROUP BY PerformanceAppraisalId HAVING COUNT_BIG(*)>1) duplicates;';

    IF OBJECT_ID(N'dbo.PerformanceAppraisalCriterionConfigs', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.PerformanceAppraisalCriterionConfigs', N'PerformanceAppraisalId') IS NOT NULL
       AND COL_LENGTH(N'dbo.PerformanceAppraisalCriterionConfigs', N'TemplateItemId') IS NOT NULL
       AND COL_LENGTH(N'dbo.PerformanceAppraisalCriterionConfigs', N'IsDeleted') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''HrPerformanceClosure.DuplicateCriterionConfigs'', COUNT_BIG(*)
            FROM (SELECT PerformanceAppraisalId,TemplateItemId
                  FROM dbo.PerformanceAppraisalCriterionConfigs
                  WHERE TemplateItemId IS NOT NULL AND IsDeleted=0
                  GROUP BY PerformanceAppraisalId,TemplateItemId HAVING COUNT_BIG(*)>1) duplicates;';

    -- CriterionConfigId is backfilled from TemplateItemId. Duplicate legacy
    -- scores for one evaluation/template would collide after that backfill.
    IF OBJECT_ID(N'dbo.CriterionScores', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.CriterionScores', N'EvaluatorEvaluationId') IS NOT NULL
       AND COL_LENGTH(N'dbo.CriterionScores', N'TemplateItemId') IS NOT NULL
       AND COL_LENGTH(N'dbo.CriterionScores', N'IsDeleted') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''HrPerformanceClosure.DuplicateCriterionScores'', COUNT_BIG(*)
            FROM (SELECT EvaluatorEvaluationId,TemplateItemId FROM dbo.CriterionScores
                  WHERE TemplateItemId IS NOT NULL AND IsDeleted=0
                  GROUP BY EvaluatorEvaluationId,TemplateItemId HAVING COUNT_BIG(*)>1) duplicates;';

    IF OBJECT_ID(N'dbo.EmployeeGoalAppraisalAssessments', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.EmployeeGoalAppraisalAssessments', N'EmployeeGoalId') IS NOT NULL
       AND COL_LENGTH(N'dbo.EmployeeGoalAppraisalAssessments', N'PerformanceAppraisalId') IS NOT NULL
       AND COL_LENGTH(N'dbo.EmployeeGoalAppraisalAssessments', N'IsDeleted') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''HrPerformanceClosure.DuplicateGoalAssessments'', COUNT_BIG(*)
            FROM (SELECT EmployeeGoalId,PerformanceAppraisalId
                  FROM dbo.EmployeeGoalAppraisalAssessments WHERE IsDeleted=0
                  GROUP BY EmployeeGoalId,PerformanceAppraisalId HAVING COUNT_BIG(*)>1) duplicates;';
END;

SELECT CheckName,AffectedRows FROM @Checks WHERE AffectedRows>0 ORDER BY CheckName;
