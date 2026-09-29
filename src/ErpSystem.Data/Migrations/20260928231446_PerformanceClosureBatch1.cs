using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Performance closure, migration batch 1 (the start of lane A): the schema every scoring,
    /// appeal, goal-row and lifecycle lane builds on, and the data repairs that go with it.
    /// </summary>
    /// <remarks>
    /// <para><b>Dropped</b> (their values never reached a score or a decision):
    /// <c>AppraisalSettings.IsManagerAuthoritative</c> and <c>RequireDevelopmentPlanUpdate</c>,
    /// <c>EvaluatorEvaluations.IsAuthoritative</c>, the HR review's <c>AdjustedOverallScore</c> and
    /// <c>AdjustmentReason</c>, and <c>AppraisalAppealItems.RevisedScore</c> (D-11).</para>
    ///
    /// <para><b>Added:</b> the tenant's default settings profile (<c>IsDefault</c>, at most one per
    /// tenant); the calibrated overall score; withdrawal (reason, actor, date; status 7); the score an
    /// appeal started from; a stored status on PIP meetings; a kind on template sections (1 = Fixed);
    /// the rating-history table <c>AppraisalScoreChanges</c>; and on the criterion snapshot the frozen
    /// section weight (A0) and the goal-row fields (lane L). The template item becomes optional on the
    /// criterion snapshot, the criterion score and the remand snapshot, and every row that pointed at a
    /// criterion by template item gains <c>CriterionConfigId</c>. Six filtered unique indexes: one
    /// default profile per tenant; one open appeal per appraisal; one snapshot row per template item and
    /// per goal; one score per snapshot row per evaluation; one assessment per goal per appraisal.</para>
    ///
    /// <para><b>Backfilled</b>, each exactly when its column is added: the default profile is the one
    /// the tenant has appraised the most people with (then the latest cycle year, then the oldest), so
    /// a profile a test run minted never wins; the calibrated overall is the newest overall adjustment
    /// in the session that calibrated the appraisal; the section and its weight come from the template
    /// as it stands; <c>CriterionConfigId</c> is matched by appraisal and template item; an adjustment
    /// with no template item is the overall; a PIP meeting whose date has passed is Held, which is what
    /// the screens inferred.</para>
    ///
    /// <para><b>Repaired:</b> appraisals at the legacy status 0 become Active; cycles at InProgress
    /// become Open (D-14); a remanded appraisal sitting at Active returns to Appealed (C3); a goal with
    /// the lock flag set becomes Locked when the workflow's own lock would have taken it (Approved,
    /// InProgress, AtRisk, OnTrack, Completed), while a flag on a goal never approved is cleared; a
    /// Locked goal without the flag returns to InProgress when it has progress, else Approved (E5); and
    /// an appraisal marked calibrated with no session and no logged advance gets its calibration waiver
    /// written, with no actor (H3).</para>
    ///
    /// <para>⚠ <b>The scaffold RENAMED <c>RequireDevelopmentPlanUpdate</c> to <c>IsDefault</c></b> — two
    /// bit columns left the table and one arrived. Applied, every profile whose old switch was on would
    /// have become the default, and the unique index could not have been built. Here the old column is
    /// dropped and <c>IsDefault</c> is a new column, off everywhere until the backfill picks one.</para>
    ///
    /// <para>⚠ <b>A unique index refuses rather than choosing:</b> where duplicates already exist, the
    /// migration stops with a message saying which, instead of deciding which appeal, score or snapshot
    /// row to lose.</para>
    ///
    /// <para>⚠ <b>Down refuses what the previous shape cannot hold</b> — goal rows (no template item), an
    /// advance with no actor that this migration did not write, a withdrawn appraisal, a goal section —
    /// and does not undo the repairs: which rows carried the old statuses was not kept.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so
    /// a database built from the model skips what it already has. Statements that name a column added
    /// in the same batch run as dynamic SQL, because SQL Server compiles a batch before running it.</para>
    /// </remarks>
    public partial class PerformanceClosureBatch1 : Migration
    {
        private const string Settings = "AppraisalSettings";
        private const string Appraisals = "PerformanceAppraisals";
        private const string Configs = "PerformanceAppraisalCriterionConfigs";
        private const string Scores = "CriterionScores";
        private const string RemandScores = "AppraisalCriterionScoreSnapshots";
        private const string Adjustments = "CalibrationRatingAdjustments";
        private const string Appeals = "AppraisalAppeals";
        private const string AppealItems = "AppraisalAppealItems";
        private const string AdvanceLogs = "AppraisalManualAdvanceLogs";
        private const string ScoreChanges = "AppraisalScoreChanges";

        /// <summary>Marks the calibration waivers this migration writes, so Down can take them back out.</summary>
        private const string WaiverAuthor = "migration:PerformanceClosureBatch1";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The settings profile ────────────────────────────────────────────────────────────
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "IsManagerAuthoritative"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "RequireDevelopmentPlanUpdate"));

            // The default profile (S9): the one the tenant has appraised the most people with, then the
            // latest cycle year, then the oldest — never "the newest profile", which a test run's own
            // profile would win. Only for a tenant that has no default yet.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Settings}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Settings}', 'IsDefault') IS NULL
BEGIN
    ALTER TABLE [dbo].[{Settings}] ADD [IsDefault] bit NOT NULL DEFAULT (0);
    EXEC sp_executesql N'
WITH candidates AS (
    SELECT s.[Id], s.[TenantId], s.[CreatedAt],
           (SELECT COUNT(*) FROM [dbo].[AppraisalCycles] c
              JOIN [dbo].[{Appraisals}] a ON a.[AppraisalCycleId] = c.[Id]
             WHERE c.[AppraisalSettingsId] = s.[Id] AND c.[IsDeleted] = 0 AND a.[IsDeleted] = 0) AS [Appraisals],
           (SELECT MAX(c.[Year]) FROM [dbo].[AppraisalCycles] c
             WHERE c.[AppraisalSettingsId] = s.[Id] AND c.[IsDeleted] = 0) AS [LatestYear]
    FROM [dbo].[{Settings}] s
    WHERE s.[IsDeleted] = 0
),
ranked AS (
    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [TenantId]
        ORDER BY [Appraisals] DESC, [LatestYear] DESC, [CreatedAt] ASC, [Id] ASC) AS [Rank]
    FROM candidates
)
UPDATE s SET s.[IsDefault] = 1
FROM [dbo].[{Settings}] s
JOIN ranked r ON r.[Id] = s.[Id]
WHERE r.[Rank] = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[{Settings}] d
                  WHERE d.[TenantId] = s.[TenantId] AND d.[IsDefault] = 1 AND d.[IsDeleted] = 0);';
END");

            // EF counts the new filtered unique index as covering the tenant foreign key, so the model
            // no longer carries the unfiltered one.
            migrationBuilder.Sql(DropIndexSql(Settings, $"IX_{Settings}_TenantId"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Settings, $"UX_{Settings}_Tenant_Default",
                "[TenantId]", "[IsDefault] = 1 AND [IsDeleted] = 0",
                "tenant(s) have more than one default appraisal settings profile. Leave one default per tenant"));

            // ── 2. Dead columns ────────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(DropColumnWithDefaultSql("EvaluatorEvaluations", "IsAuthoritative"));
            migrationBuilder.Sql(DropColumnWithDefaultSql("AppraisalHRReviews", "AdjustedOverallScore"));
            migrationBuilder.Sql(DropColumnWithDefaultSql("AppraisalHRReviews", "AdjustmentReason"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(AppealItems, "RevisedScore"));

            // ── 3. Template sections: what fills them ──────────────────────────────────────────────
            migrationBuilder.Sql(AddColumnSql("AppraisalTemplateSections", "Kind", "int NOT NULL DEFAULT (1)"));

            // ── 4. The appraisal: calibrated overall and withdrawal ────────────────────────────────
            //
            // The calibrated overall is what the commit last wrote as the overall: the newest overall
            // adjustment in the session that calibrated the appraisal. The stored OverallScore is not
            // touched — it changes only when something re-settles the appraisal (A15, D-13).
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Appraisals}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Appraisals}', 'CalibratedOverallScore') IS NULL
BEGIN
    ALTER TABLE [dbo].[{Appraisals}] ADD [CalibratedOverallScore] decimal(18,4) NULL;
    EXEC sp_executesql N'
UPDATE a SET a.[CalibratedOverallScore] = x.[AdjustedScore]
FROM [dbo].[{Appraisals}] a
CROSS APPLY (
    SELECT TOP (1) r.[AdjustedScore]
    FROM [dbo].[{Adjustments}] r
    WHERE r.[PerformanceAppraisalId] = a.[Id]
      AND r.[CalibrationSessionId] = a.[CalibrationSessionId]
      AND r.[TemplateItemId] IS NULL
      AND r.[AdjustedScore] IS NOT NULL
      AND r.[IsDeleted] = 0
    ORDER BY r.[AdjustmentDate] DESC, r.[CreatedAt] DESC) x
WHERE a.[IsCalibrated] = 1 AND a.[CalibrationSessionId] IS NOT NULL;';
END");

            migrationBuilder.Sql(AddColumnSql(Appraisals, "WithdrawnReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(Appraisals, "WithdrawnById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Appraisals, "WithdrawnDate", "datetime2 NULL"));
            migrationBuilder.Sql(CreateIndexSql(Appraisals, $"IX_{Appraisals}_WithdrawnById", "[WithdrawnById]"));
            migrationBuilder.Sql(AddForeignKeySql(Appraisals, $"FK_{Appraisals}_Employees_WithdrawnById", "WithdrawnById", "Employees"));

            // ── 5. Appeals: where the score started, and one open appeal at a time ─────────────────
            migrationBuilder.Sql(AddColumnSql(Appeals, "OriginalOverallScore", "decimal(18,4) NULL"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Appeals, "UX_AppraisalAppeal_OneOpenPerAppraisal",
                "[PerformanceAppraisalId]", "[Status] IN (1, 2, 3) AND [IsDeleted] = 0",
                "appraisal(s) have more than one open appeal (Submitted, UnderReview or Remanded). Resolve all but one"));

            // ── 6. The criterion snapshot ──────────────────────────────────────────────────────────
            migrationBuilder.Sql(MakeNullableSql(Configs, "TemplateItemId", $"IX_{Configs}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(Configs, $"IX_{Configs}_TemplateItemId", "[TemplateItemId]"));

            // A0: the section each row belongs to, and that section's weight, frozen from the template as
            // it stands. A section or item deleted since keeps the weight it carried.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Configs}', 'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.{Configs}', 'SectionWeightUsed') IS NULL
        ALTER TABLE [dbo].[{Configs}] ADD [SectionWeightUsed] int NULL;
    IF COL_LENGTH('dbo.{Configs}', 'AppraisalTemplateSectionId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[{Configs}] ADD [AppraisalTemplateSectionId] uniqueidentifier NULL;
        EXEC sp_executesql N'
UPDATE c SET c.[AppraisalTemplateSectionId] = i.[AppraisalTemplateSectionId], c.[SectionWeightUsed] = s.[Weight]
FROM [dbo].[{Configs}] c
JOIN [dbo].[AppraisalTemplateItems] i ON i.[Id] = c.[TemplateItemId]
JOIN [dbo].[AppraisalTemplateSections] s ON s.[Id] = i.[AppraisalTemplateSectionId]
WHERE c.[AppraisalTemplateSectionId] IS NULL;';
    END
END");

            // Lane L's goal rows: nothing writes these until then.
            migrationBuilder.Sql(AddColumnSql(Configs, "EmployeeGoalId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Configs, "ItemLabel", "nvarchar(300) NULL"));
            migrationBuilder.Sql(AddColumnSql(Configs, "ScoringMethod", "int NULL"));
            migrationBuilder.Sql(AddColumnSql(Configs, "MeasurementType", "int NULL"));
            migrationBuilder.Sql(AddColumnSql(Configs, "Unit", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumnSql(Configs, "DisplayOrder", "int NULL"));

            migrationBuilder.Sql(CreateIndexSql(Configs, $"IX_{Configs}_AppraisalTemplateSectionId", "[AppraisalTemplateSectionId]"));
            migrationBuilder.Sql(CreateIndexSql(Configs, $"IX_{Configs}_EmployeeGoalId", "[EmployeeGoalId]"));
            migrationBuilder.Sql(AddForeignKeySql(Configs, $"FK_{Configs}_AppraisalTemplateSections_AppraisalTemplateSectionId",
                "AppraisalTemplateSectionId", "AppraisalTemplateSections"));
            migrationBuilder.Sql(AddForeignKeySql(Configs, $"FK_{Configs}_EmployeeGoals_EmployeeGoalId", "EmployeeGoalId", "EmployeeGoals"));

            // Before any CriterionConfigId is matched below: one snapshot row per template item is what
            // makes that match unambiguous.
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Configs, "UX_PerformanceAppraisalCriterionConfig_Appraisal_TemplateItem",
                "[PerformanceAppraisalId], [TemplateItemId]", "[TemplateItemId] IS NOT NULL AND [IsDeleted] = 0",
                "appraisal(s) hold two criterion snapshot rows for the same template item. Soft-delete the extra rows"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Configs, "UX_PerformanceAppraisalCriterionConfig_Appraisal_Goal",
                "[PerformanceAppraisalId], [EmployeeGoalId]", "[EmployeeGoalId] IS NOT NULL AND [IsDeleted] = 0",
                "appraisal(s) hold two criterion snapshot rows for the same goal. Soft-delete the extra rows"));

            // ── 7. Criterion scores ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(MakeNullableSql(Scores, "TemplateItemId", $"IX_{Scores}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(Scores, $"IX_{Scores}_TemplateItemId", "[TemplateItemId]"));
            migrationBuilder.Sql(AddColumnWithBackfillSql(Scores, "CriterionConfigId", "uniqueidentifier NULL", $@"
UPDATE cs SET cs.[CriterionConfigId] = c.[Id]
FROM [dbo].[{Scores}] cs
JOIN [dbo].[EvaluatorEvaluations] e ON e.[Id] = cs.[EvaluatorEvaluationId]
JOIN [dbo].[{Configs}] c ON c.[PerformanceAppraisalId] = e.[AppraisalId]
                        AND c.[TemplateItemId] = cs.[TemplateItemId]
                        AND c.[IsDeleted] = 0
WHERE cs.[CriterionConfigId] IS NULL;"));
            migrationBuilder.Sql(CreateIndexSql(Scores, $"IX_{Scores}_CriterionConfigId", "[CriterionConfigId]"));
            migrationBuilder.Sql(AddForeignKeySql(Scores, $"FK_{Scores}_{Configs}_CriterionConfigId", "CriterionConfigId", Configs));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Scores, "UX_CriterionScore_Evaluation_CriterionConfig",
                "[EvaluatorEvaluationId], [CriterionConfigId]", "[CriterionConfigId] IS NOT NULL AND [IsDeleted] = 0",
                "evaluation(s) score the same criterion twice. Soft-delete the extra score rows"));

            // ── 8. The remand snapshot's scores ────────────────────────────────────────────────────
            migrationBuilder.Sql(MakeNullableSql(RemandScores, "TemplateItemId", $"IX_{RemandScores}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(RemandScores, $"IX_{RemandScores}_TemplateItemId", "[TemplateItemId]"));
            migrationBuilder.Sql(AddColumnSql(RemandScores, "ActualValue", "decimal(18,4) NULL"));
            migrationBuilder.Sql(AddColumnWithBackfillSql(RemandScores, "CriterionConfigId", "uniqueidentifier NULL", $@"
UPDATE x SET x.[CriterionConfigId] = c.[Id]
FROM [dbo].[{RemandScores}] x
JOIN [dbo].[AppraisalEvaluationSnapshots] s ON s.[Id] = x.[AppraisalEvaluationSnapshotId]
JOIN [dbo].[{Configs}] c ON c.[PerformanceAppraisalId] = s.[AppraisalId]
                        AND c.[TemplateItemId] = x.[TemplateItemId]
                        AND c.[IsDeleted] = 0
WHERE x.[CriterionConfigId] IS NULL;"));
            migrationBuilder.Sql(CreateIndexSql(RemandScores, $"IX_{RemandScores}_CriterionConfigId", "[CriterionConfigId]"));
            migrationBuilder.Sql(AddForeignKeySql(RemandScores, $"FK_{RemandScores}_{Configs}_CriterionConfigId", "CriterionConfigId", Configs));

            // ── 9. Calibration adjustments ─────────────────────────────────────────────────────────
            migrationBuilder.Sql(AddColumnWithBackfillSql(Adjustments, "CriterionConfigId", "uniqueidentifier NULL", $@"
UPDATE r SET r.[CriterionConfigId] = c.[Id]
FROM [dbo].[{Adjustments}] r
JOIN [dbo].[{Configs}] c ON c.[PerformanceAppraisalId] = r.[PerformanceAppraisalId]
                        AND c.[TemplateItemId] = r.[TemplateItemId]
                        AND c.[IsDeleted] = 0
WHERE r.[CriterionConfigId] IS NULL AND r.[TemplateItemId] IS NOT NULL;"));
            migrationBuilder.Sql(AddColumnWithBackfillSql(Adjustments, "IsOverall", "bit NOT NULL DEFAULT (0)", $@"
UPDATE [dbo].[{Adjustments}] SET [IsOverall] = 1 WHERE [TemplateItemId] IS NULL;"));
            migrationBuilder.Sql(CreateIndexSql(Adjustments, $"IX_{Adjustments}_CriterionConfigId", "[CriterionConfigId]"));
            migrationBuilder.Sql(AddForeignKeySql(Adjustments, $"FK_{Adjustments}_{Configs}_CriterionConfigId", "CriterionConfigId", Configs));

            // ── 10. Appeal items ───────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(AddColumnWithBackfillSql(AppealItems, "CriterionConfigId", "uniqueidentifier NULL", $@"
UPDATE i SET i.[CriterionConfigId] = c.[Id]
FROM [dbo].[{AppealItems}] i
JOIN [dbo].[{Appeals}] p ON p.[Id] = i.[AppraisalAppealId]
JOIN [dbo].[{Configs}] c ON c.[PerformanceAppraisalId] = p.[PerformanceAppraisalId]
                        AND c.[TemplateItemId] = i.[TemplateItemId]
                        AND c.[IsDeleted] = 0
WHERE i.[CriterionConfigId] IS NULL AND i.[TemplateItemId] IS NOT NULL;"));
            migrationBuilder.Sql(CreateIndexSql(AppealItems, $"IX_{AppealItems}_CriterionConfigId", "[CriterionConfigId]"));
            migrationBuilder.Sql(AddForeignKeySql(AppealItems, $"FK_{AppealItems}_{Configs}_CriterionConfigId", "CriterionConfigId", Configs));

            // ── 11. One year-end assessment per goal per appraisal ─────────────────────────────────
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql("EmployeeGoalAppraisalAssessments", "UX_EmployeeGoalAppraisalAssessment_Goal_Appraisal",
                "[EmployeeGoalId], [PerformanceAppraisalId]", "[IsDeleted] = 0",
                "goal(s) carry two assessments in the same appraisal. Soft-delete the extra rows"));

            // ── 12. An advance with no person behind it ────────────────────────────────────────────
            //
            // The actor becomes optional (the nightly sweep, a waiver written by a repair), and its key
            // stops cascading: deleting an employee must not delete the record of what they advanced.
            migrationBuilder.Sql(DropForeignKeySql(AdvanceLogs, $"FK_{AdvanceLogs}_Employees_AdvancedByEmployeeId"));
            migrationBuilder.Sql(MakeNullableSql(AdvanceLogs, "AdvancedByEmployeeId", $"IX_{AdvanceLogs}_AdvancedByEmployeeId"));
            migrationBuilder.Sql(CreateIndexSql(AdvanceLogs, $"IX_{AdvanceLogs}_AdvancedByEmployeeId", "[AdvancedByEmployeeId]"));
            migrationBuilder.Sql(AddForeignKeySql(AdvanceLogs, $"FK_{AdvanceLogs}_Employees_AdvancedByEmployeeId", "AdvancedByEmployeeId", "Employees"));

            // ── 13. PIP meetings: a stored status, starting from what the screens inferred ─────────
            migrationBuilder.Sql(AddColumnWithBackfillSql("PipReviewMeetings", "Status", "int NOT NULL DEFAULT (1)", @"
UPDATE [dbo].[PipReviewMeetings] SET [Status] = 2 WHERE [MeetingDate] <= SYSUTCDATETIME();"));

            // ── 14. The rating history ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{ScoreChanges}', 'U') IS NULL
CREATE TABLE [dbo].[{ScoreChanges}] (
    [Id]                     uniqueidentifier NOT NULL,
    [PerformanceAppraisalId] uniqueidentifier NOT NULL,
    [FromScore]              decimal(18,4)    NULL,
    [ToScore]                decimal(18,4)    NULL,
    [FromGradeDefinitionId]  uniqueidentifier NULL,
    [ToGradeDefinitionId]    uniqueidentifier NULL,
    [Source]                 int              NOT NULL,
    [ChangedById]            uniqueidentifier NULL,
    [Reason]                 nvarchar(1000)   NULL,
    [ChangedDate]            datetime2        NOT NULL,
    [CreatedAt]              datetime2        NOT NULL,
    [UpdatedAt]              datetime2        NULL,
    [CreatedBy]              nvarchar(max)    NULL,
    [UpdatedBy]              nvarchar(max)    NULL,
    [CreatedById]            uniqueidentifier NULL,
    [LastModifiedById]       uniqueidentifier NULL,
    [IsDeleted]              bit              NOT NULL,
    [DeletedAt]              datetime2        NULL,
    [DeletedBy]              nvarchar(max)    NULL,
    [TenantId]               uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{ScoreChanges}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{ScoreChanges}_AppraisalGradeDefinitions_FromGradeDefinitionId] FOREIGN KEY ([FromGradeDefinitionId])
        REFERENCES [dbo].[AppraisalGradeDefinitions] ([Id]),
    CONSTRAINT [FK_{ScoreChanges}_AppraisalGradeDefinitions_ToGradeDefinitionId] FOREIGN KEY ([ToGradeDefinitionId])
        REFERENCES [dbo].[AppraisalGradeDefinitions] ([Id]),
    CONSTRAINT [FK_{ScoreChanges}_Employees_ChangedById] FOREIGN KEY ([ChangedById])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_{ScoreChanges}_{Appraisals}_PerformanceAppraisalId] FOREIGN KEY ([PerformanceAppraisalId])
        REFERENCES [dbo].[{Appraisals}] ([Id]),
    CONSTRAINT [FK_{ScoreChanges}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_ChangedById", "[ChangedById]"));
            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_ChangedDate", "[ChangedDate]"));
            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_FromGradeDefinitionId", "[FromGradeDefinitionId]"));
            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_PerformanceAppraisalId", "[PerformanceAppraisalId]"));
            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndexSql(ScoreChanges, $"IX_{ScoreChanges}_ToGradeDefinitionId", "[ToGradeDefinitionId]"));

            // ── 15. Status repairs ─────────────────────────────────────────────────────────────────
            //
            // Appraisal status 0 is the retained RHEMA model's "Open", which no pipeline step reads; a
            // cycle's InProgress is written by nothing but the demo seeder, and the live-cycle checks
            // already treat it as Open (D-14); a remand keeps the appraisal Appealed (C3).
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Appraisals}', 'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[{Appraisals}] SET [Status] = 2 WHERE [Status] = 0;
    UPDATE [dbo].[{Appraisals}] SET [Status] = 4 WHERE [Status] = 2 AND [CurrentAppealStatus] = 3;
END
IF OBJECT_ID('dbo.AppraisalCycles', 'U') IS NOT NULL
    UPDATE [dbo].[AppraisalCycles] SET [Status] = 2 WHERE [Status] = 3;");

            // Goals (E5). The lock flag and the Locked status are one fact written two ways: the
            // workflow's lock sets both, the older lock path set only the flag, and the older unlock
            // cleared only the flag. A flagged goal becomes Locked where the workflow's lock would have
            // taken it (Approved 3, InProgress 6, AtRisk 7, OnTrack 8, Completed 9); a flag on a goal
            // never approved (Draft 1, PendingApproval 2, Rejected 4) is a lock that lock would refuse,
            // so the flag goes. A Locked goal without the flag was unlocked, and resumes as InProgress
            // when it has progress, else Approved.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeGoals', 'U') IS NOT NULL
BEGIN
    UPDATE [dbo].[EmployeeGoals] SET [Status] = 5
    WHERE [IsLocked] = 1 AND [Status] IN (3, 6, 7, 8, 9);

    UPDATE [dbo].[EmployeeGoals] SET [IsLocked] = 0, [LockedDate] = NULL
    WHERE [IsLocked] = 1 AND [Status] IN (1, 2, 4);

    UPDATE g SET g.[Status] =
        CASE WHEN g.[ProgressPercent] > 0
                  OR EXISTS (SELECT 1 FROM [dbo].[GoalProgressEntries] e
                             WHERE e.[EmployeeGoalId] = g.[Id] AND e.[IsDeleted] = 0)
             THEN 6 ELSE 3 END
    FROM [dbo].[EmployeeGoals] g
    WHERE g.[Status] = 5 AND g.[IsLocked] = 0;
END");

            // ── 16. Calibration waivers (H3) ───────────────────────────────────────────────────────
            //
            // HR's advance past calibration marks the appraisal calibrated with no session and logs the
            // advance; that log row is the waiver. An appraisal marked so with no such row gets one here,
            // with no actor, so the gate that will read waivers sees the same fact the flag records.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{AdvanceLogs}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{Appraisals}', 'U') IS NOT NULL
INSERT INTO [dbo].[{AdvanceLogs}] (
    [Id], [PerformanceAppraisalId], [AdvancedByEmployeeId], [AdvancedDate], [FromSubStatus], [ToSubStatus],
    [FromMajorStatus], [ToMajorStatus], [Reason], [ActionsPerformedJson],
    [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [CreatedById], [LastModifiedById],
    [IsDeleted], [DeletedAt], [DeletedBy], [TenantId])
SELECT
    NEWID(), a.[Id], NULL, SYSUTCDATETIME(), N'PendingCalibration', N'CalibrationWaived',
    NULL, NULL,
    N'Recorded by a data repair: the appraisal was marked calibrated without a calibration session, and no advance past calibration was logged. This row is that calibration''s waiver.',
    N'[""Recorded the calibration waiver the appraisal already carried.""]',
    SYSUTCDATETIME(), NULL, N'{WaiverAuthor}', NULL, NULL, NULL,
    0, NULL, NULL, a.[TenantId]
FROM [dbo].[{Appraisals}] a
WHERE a.[IsCalibrated] = 1
  AND a.[CalibrationSessionId] IS NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[{AdvanceLogs}] l
                  WHERE l.[PerformanceAppraisalId] = a.[Id] AND l.[IsDeleted] = 0
                    AND l.[FromSubStatus] IN (N'PendingCalibration', N'CalibrationInProgress'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Refuse what the previous shape cannot hold ──────────────────────────────────────
            migrationBuilder.Sql(RefuseDownIfAnySql(Configs, "[TemplateItemId] IS NULL",
                "criterion snapshot row(s) are goal rows, with no template item"));
            migrationBuilder.Sql(RefuseDownIfAnySql(Scores, "[TemplateItemId] IS NULL",
                "criterion score(s) belong to goal rows, with no template item"));
            migrationBuilder.Sql(RefuseDownIfAnySql(RemandScores, "[TemplateItemId] IS NULL",
                "remand snapshot score(s) belong to goal rows, with no template item"));
            migrationBuilder.Sql(RefuseDownIfAnySql(AdvanceLogs,
                $"[AdvancedByEmployeeId] IS NULL AND ([CreatedBy] IS NULL OR [CreatedBy] <> N'{WaiverAuthor}')",
                "pipeline advance(s) were made with no person behind them"));
            migrationBuilder.Sql(RefuseDownIfAnySql(Appraisals, "[Status] = 7",
                "appraisal(s) are withdrawn, a status the previous shape does not have"));
            migrationBuilder.Sql(RefuseDownIfAnySql("AppraisalTemplateSections", "[Kind] = 2",
                "template section(s) are goal sections, which the previous shape does not have"));

            // ── 2. The rating history goes ─────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{ScoreChanges}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{ScoreChanges}];");

            // ── 3. The advance's actor is required again, and cascades as it did ──────────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{AdvanceLogs}', 'CreatedBy') IS NOT NULL
    DELETE FROM [dbo].[{AdvanceLogs}] WHERE [AdvancedByEmployeeId] IS NULL AND [CreatedBy] = N'{WaiverAuthor}';");
            migrationBuilder.Sql(DropForeignKeySql(AdvanceLogs, $"FK_{AdvanceLogs}_Employees_AdvancedByEmployeeId"));
            migrationBuilder.Sql(MakeNotNullableSql(AdvanceLogs, "AdvancedByEmployeeId", $"IX_{AdvanceLogs}_AdvancedByEmployeeId"));
            migrationBuilder.Sql(CreateIndexSql(AdvanceLogs, $"IX_{AdvanceLogs}_AdvancedByEmployeeId", "[AdvancedByEmployeeId]"));
            migrationBuilder.Sql(AddForeignKeySql(AdvanceLogs, $"FK_{AdvanceLogs}_Employees_AdvancedByEmployeeId",
                "AdvancedByEmployeeId", "Employees", cascade: true));

            // ── 4. The unique indexes, the new keys and their indexes go ───────────────────────────
            migrationBuilder.Sql(DropIndexSql("EmployeeGoalAppraisalAssessments", "UX_EmployeeGoalAppraisalAssessment_Goal_Appraisal"));
            migrationBuilder.Sql(DropIndexSql(Appeals, "UX_AppraisalAppeal_OneOpenPerAppraisal"));
            migrationBuilder.Sql(DropIndexSql(Settings, $"UX_{Settings}_Tenant_Default"));

            migrationBuilder.Sql(DropForeignKeySql(AppealItems, $"FK_{AppealItems}_{Configs}_CriterionConfigId"));
            migrationBuilder.Sql(DropIndexSql(AppealItems, $"IX_{AppealItems}_CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(AppealItems, "CriterionConfigId"));

            migrationBuilder.Sql(DropForeignKeySql(Adjustments, $"FK_{Adjustments}_{Configs}_CriterionConfigId"));
            migrationBuilder.Sql(DropIndexSql(Adjustments, $"IX_{Adjustments}_CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Adjustments, "CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Adjustments, "IsOverall"));

            migrationBuilder.Sql(DropForeignKeySql(RemandScores, $"FK_{RemandScores}_{Configs}_CriterionConfigId"));
            migrationBuilder.Sql(DropIndexSql(RemandScores, $"IX_{RemandScores}_CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(RemandScores, "CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(RemandScores, "ActualValue"));

            migrationBuilder.Sql(DropIndexSql(Scores, "UX_CriterionScore_Evaluation_CriterionConfig"));
            migrationBuilder.Sql(DropForeignKeySql(Scores, $"FK_{Scores}_{Configs}_CriterionConfigId"));
            migrationBuilder.Sql(DropIndexSql(Scores, $"IX_{Scores}_CriterionConfigId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Scores, "CriterionConfigId"));

            migrationBuilder.Sql(DropIndexSql(Configs, "UX_PerformanceAppraisalCriterionConfig_Appraisal_Goal"));
            migrationBuilder.Sql(DropIndexSql(Configs, "UX_PerformanceAppraisalCriterionConfig_Appraisal_TemplateItem"));
            migrationBuilder.Sql(DropForeignKeySql(Configs, $"FK_{Configs}_EmployeeGoals_EmployeeGoalId"));
            migrationBuilder.Sql(DropForeignKeySql(Configs, $"FK_{Configs}_AppraisalTemplateSections_AppraisalTemplateSectionId"));
            migrationBuilder.Sql(DropIndexSql(Configs, $"IX_{Configs}_EmployeeGoalId"));
            migrationBuilder.Sql(DropIndexSql(Configs, $"IX_{Configs}_AppraisalTemplateSectionId"));
            foreach (var column in new[] { "EmployeeGoalId", "ItemLabel", "ScoringMethod", "MeasurementType", "Unit", "DisplayOrder",
                                           "AppraisalTemplateSectionId", "SectionWeightUsed" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Configs, column));

            migrationBuilder.Sql(DropForeignKeySql(Appraisals, $"FK_{Appraisals}_Employees_WithdrawnById"));
            migrationBuilder.Sql(DropIndexSql(Appraisals, $"IX_{Appraisals}_WithdrawnById"));
            foreach (var column in new[] { "WithdrawnById", "WithdrawnDate", "WithdrawnReason", "CalibratedOverallScore" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Appraisals, column));

            migrationBuilder.Sql(DropColumnWithDefaultSql(Appeals, "OriginalOverallScore"));
            migrationBuilder.Sql(DropColumnWithDefaultSql("PipReviewMeetings", "Status"));
            migrationBuilder.Sql(DropColumnWithDefaultSql("AppraisalTemplateSections", "Kind"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Settings, "IsDefault"));

            // ── 5. The template item is required again ─────────────────────────────────────────────
            migrationBuilder.Sql(MakeNotNullableSql(Configs, "TemplateItemId", $"IX_{Configs}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(Configs, $"IX_{Configs}_TemplateItemId", "[TemplateItemId]"));
            migrationBuilder.Sql(MakeNotNullableSql(Scores, "TemplateItemId", $"IX_{Scores}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(Scores, $"IX_{Scores}_TemplateItemId", "[TemplateItemId]"));
            migrationBuilder.Sql(MakeNotNullableSql(RemandScores, "TemplateItemId", $"IX_{RemandScores}_TemplateItemId"));
            migrationBuilder.Sql(CreateIndexSql(RemandScores, $"IX_{RemandScores}_TemplateItemId", "[TemplateItemId]"));

            // ── 6. The dropped columns come back empty; the flags with the values they defaulted to ─
            //
            // The two settings defaulted to on, and under that default the manager's evaluation was
            // the authoritative one. The flags had no default constraint, so none is left behind.
            migrationBuilder.Sql(AddRequiredColumnSql(Settings, "IsManagerAuthoritative", "bit", "1"));
            migrationBuilder.Sql(AddRequiredColumnSql(Settings, "RequireDevelopmentPlanUpdate", "bit", "1"));
            migrationBuilder.Sql(AddRequiredColumnSql("EvaluatorEvaluations", "IsAuthoritative", "bit",
                "CASE WHEN [EvaluatorRole] = 2 THEN 1 ELSE 0 END"));
            migrationBuilder.Sql(AddColumnSql("AppraisalHRReviews", "AdjustedOverallScore", "decimal(18,4) NULL"));
            migrationBuilder.Sql(AddColumnSql("AppraisalHRReviews", "AdjustmentReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumnSql(AppealItems, "RevisedScore", "decimal(18,4) NULL"));
            migrationBuilder.Sql(CreateIndexSql(Settings, $"IX_{Settings}_TenantId", "[TenantId]"));
        }

        private static string AddColumnSql(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <summary>
        /// Adds a column and fills it in the same step, so the fill runs exactly once — when the column
        /// arrives — and never on a database that already had it. The fill is dynamic because it names
        /// the column being added in this batch.
        /// </summary>
        private static string AddColumnWithBackfillSql(string table, string column, string definition, string backfill) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};
    EXEC sp_executesql N'{backfill.Replace("'", "''")}';
END";

        /// <summary>
        /// Adds a required column without leaving a default constraint: added optional, filled, then
        /// made required. The fill and the second ALTER are dynamic because they name the new column.
        /// </summary>
        private static string AddRequiredColumnSql(string table, string column, string type, string fill) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{table}] ADD [{column}] {type} NULL;
    EXEC sp_executesql N'UPDATE [dbo].[{table}] SET [{column}] = {fill.Replace("'", "''")};';
    EXEC sp_executesql N'ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] {type} NOT NULL;';
END";

        private static string CreateIndexSql(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        /// <summary>
        /// Creates a filtered unique index, first counting the rows it would refuse. Duplicates stop
        /// the migration with a message, because choosing which row survives is not a schema change.
        /// </summary>
        private static string CreateUniqueIndexRefusingDuplicatesSql(string table, string index, string columns, string filter, string duplicatesAre) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
BEGIN
    DECLARE @duplicates int = (
        SELECT COUNT(*) FROM (
            SELECT 1 AS [Found] FROM [dbo].[{table}] WHERE {filter} GROUP BY {columns} HAVING COUNT(*) > 1) d);
    IF @duplicates > 0
    BEGIN
        DECLARE @message nvarchar(1000) = CONCAT(@duplicates,
            N' {duplicatesAre} ({table}: {columns} where {filter}) before the unique index {index} can be created.');
        THROW 50001, @message, 1;
    END
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}) WHERE {filter};
END";

        private static string DropIndexSql(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKeySql(string table, string name, string column, string principal, bool cascade = false) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{name}', 'F') IS NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principal}] ([Id]){(cascade ? " ON DELETE CASCADE" : string.Empty)};";

        private static string DropForeignKeySql(string table, string name) => $@"
IF OBJECT_ID('dbo.{name}', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{name}];";

        /// <summary>
        /// Makes a uniqueidentifier column optional. Its single-column index is dropped first, as EF's
        /// own ALTER COLUMN would, and recreated by the caller.
        /// </summary>
        private static string MakeNullableSql(string table, string column, string index) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('dbo.{table}'), '{column}', 'AllowsNull') = 0
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
        DROP INDEX [{index}] ON [dbo].[{table}];
    ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] uniqueidentifier NULL;
END";

        private static string MakeNotNullableSql(string table, string column, string index) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND COLUMNPROPERTY(OBJECT_ID('dbo.{table}'), '{column}', 'AllowsNull') = 1
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
        DROP INDEX [{index}] ON [dbo].[{table}];
    ALTER TABLE [dbo].[{table}] ALTER COLUMN [{column}] uniqueidentifier NOT NULL;
END";

        /// <summary>Stops a Down that would have to drop or invent something the previous shape cannot hold.</summary>
        private static string RefuseDownIfAnySql(string table, string condition, string rowsAre) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
BEGIN
    DECLARE @unrepresentable int;
    EXEC sp_executesql N'SELECT @n = COUNT(*) FROM [dbo].[{table}] WHERE {condition.Replace("'", "''")};',
        N'@n int OUTPUT', @n = @unrepresentable OUTPUT;
    IF @unrepresentable > 0
    BEGIN
        DECLARE @message nvarchar(600) = CONCAT(@unrepresentable,
            N' {rowsAre}. The previous shape cannot hold them, so this migration cannot be reverted without losing them.');
        THROW 50001, @message, 1;
    END
END";

        /// <summary>
        /// Drops a column that may carry an unnamed default: the default constraint first (SQL Server
        /// named it, or there is none on a model-built database), then the column.
        /// </summary>
        private static string DropColumnWithDefaultSql(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @default sysname;
    SELECT @default = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @default IS NOT NULL
    BEGIN
        DECLARE @drop nvarchar(400) = N'ALTER TABLE [dbo].[{table}] DROP CONSTRAINT ' + QUOTENAME(@default);
        EXEC sp_executesql @drop;
    END
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";
    }
}
