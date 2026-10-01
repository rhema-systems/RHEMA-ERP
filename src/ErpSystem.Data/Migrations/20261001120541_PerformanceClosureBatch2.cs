using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Performance closure, migration batch 2 (the start of lane F, slice F-a): the columns, tables and indexes lane F
    /// builds on, and the probation extension request that D-99 routes to the confirming authority.
    /// </summary>
    /// <remarks>
    /// <para><b>Added:</b> who rejected or dismissed a recommendation, and when (D-45); who submitted a salary review or
    /// employment action proposal and when, and who marked it applied (F3, F5); the PIP that raised an employment action,
    /// and the date the action takes effect (D-96, D-19); a PIP's author (F3, D-94); the KPI tolerance a criterion row was
    /// snapshotted with (D-32); the performance sweep's run and dispatch tables (F7, lane H); and for probation (D-99) the
    /// extension request table, the request an extension applied, and on the probation whom its confirmation was sent to,
    /// when, and who decided it.</para>
    ///
    /// <para><b>Indexes:</b> a PIP number is unique per tenant among live plans (D-75), replacing the global non-unique
    /// index — EF counts the new index as covering the tenant foreign key, so the unfiltered tenant index goes too; one open
    /// recommendation of a type per appraisal (D-93); one open extension request per probation; one extension per request;
    /// the sweep's send-once key.</para>
    ///
    /// <para><b>Moved, not copied:</b> a rejected or dismissed recommendation's approver columns hold whoever closed it —
    /// the close and the withdrawal's dismissal stamped them, over an earlier approval when there was one. They move to
    /// <c>DecidedById</c> and <c>DecidedDate</c> and are cleared, so an approver column never names a decider. An earlier
    /// approval those closes overwrote cannot be recovered.</para>
    ///
    /// <para><b>Left empty:</b> the proposal submitter, the one who marked a proposal applied, a PIP's author and the KPI
    /// tolerance. Nothing recorded the first three; filling the tolerance from today's KPI definitions would restate scores
    /// already given, and a row with none scores as before (D-32).</para>
    ///
    /// <para>⚠ <b>A unique index refuses rather than choosing</b> (D-91): where duplicates exist the migration stops with
    /// the count, as batch 1's did.</para>
    ///
    /// <para>⚠ <b>Down refuses while any probation extension request exists</b> — the previous shape has nowhere to keep a
    /// pending decision or an extension's provenance. Otherwise it moves the deciders back to the approver columns, as the
    /// previous code wrote them, and drops the rest; what was written to the dropped columns goes with them.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: each statement checks for the object it changes, so a database built
    /// from the model skips what it already has. Statements that name a column added in the same batch run as dynamic SQL,
    /// because SQL Server compiles a batch before running it.</para>
    /// </remarks>
    public partial class PerformanceClosureBatch2 : Migration
    {
        private const string Recommendations = "AppraisalOutcomeRecommendations";
        private const string SalaryProposals = "SalaryReviewProposals";
        private const string ActionProposals = "EmploymentActionProposals";
        private const string Pips = "PerformanceImprovementPlans";
        private const string Configs = "PerformanceAppraisalCriterionConfigs";
        private const string Probations = "ProbationPeriods";
        private const string Extensions = "ProbationExtensions";
        private const string ExtensionRequests = "ProbationExtensionRequests";
        private const string SweepRuns = "PerformanceSweepRuns";
        private const string SweepDispatches = "PerformanceSweepDispatches";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Recommendations: the decider, and one open recommendation of a type ─────────────────
            migrationBuilder.Sql(AddColumnSql(Recommendations, "DecidedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnWithBackfillSql(Recommendations, "DecidedById", "uniqueidentifier NULL", $@"
UPDATE [dbo].[{Recommendations}]
SET [DecidedById] = [ApprovedById], [DecidedDate] = [ApprovedDate], [ApprovedById] = NULL, [ApprovedDate] = NULL
WHERE [Status] IN (4, 5);"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Recommendations, "UX_AppraisalOutcomeRecommendations_Appraisal_Type_Open",
                "[PerformanceAppraisalId], [RecommendationType]", "[IsDeleted] = 0 AND [Status] IN (1, 2, 3)",
                "appraisal(s) hold more than one open recommendation of the same type. Reject or dismiss the extra ones"));

            // ── 2. Salary review proposals: the submitter and the one who marked it applied ───────────
            migrationBuilder.Sql(AddColumnSql(SalaryProposals, "SubmittedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(SalaryProposals, "SubmittedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(SalaryProposals, "ActionedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(SalaryProposals, "ActionedDate", "datetime2 NULL"));
            migrationBuilder.Sql(CreateIndexSql(SalaryProposals, $"IX_{SalaryProposals}_SubmittedById", "[SubmittedById]"));
            migrationBuilder.Sql(CreateIndexSql(SalaryProposals, $"IX_{SalaryProposals}_ActionedById", "[ActionedById]"));
            migrationBuilder.Sql(AddForeignKeySql(SalaryProposals, $"FK_{SalaryProposals}_Employees_SubmittedById", "SubmittedById", "Employees"));
            migrationBuilder.Sql(AddForeignKeySql(SalaryProposals, $"FK_{SalaryProposals}_Employees_ActionedById", "ActionedById", "Employees"));

            // ── 3. Employment action proposals: the same, the plan that raised it, when it takes effect ─
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "SubmittedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "SubmittedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "ActionedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "ActionedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "SourcePipId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(ActionProposals, "EffectiveDate", "date NULL"));
            migrationBuilder.Sql(CreateIndexSql(ActionProposals, $"IX_{ActionProposals}_SubmittedById", "[SubmittedById]"));
            migrationBuilder.Sql(CreateIndexSql(ActionProposals, $"IX_{ActionProposals}_ActionedById", "[ActionedById]"));
            migrationBuilder.Sql(CreateIndexSql(ActionProposals, $"IX_{ActionProposals}_SourcePipId", "[SourcePipId]"));
            migrationBuilder.Sql(AddForeignKeySql(ActionProposals, $"FK_{ActionProposals}_Employees_SubmittedById", "SubmittedById", "Employees"));
            migrationBuilder.Sql(AddForeignKeySql(ActionProposals, $"FK_{ActionProposals}_Employees_ActionedById", "ActionedById", "Employees"));
            migrationBuilder.Sql(AddForeignKeySql(ActionProposals, $"FK_{ActionProposals}_{Pips}_SourcePipId", "SourcePipId", Pips));

            // ── 4. PIPs: the author, and a number unique per tenant ───────────────────────────────────
            migrationBuilder.Sql(AddColumnSql(Pips, "AuthoredById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndexSql(Pips, $"IX_{Pips}_AuthoredById", "[AuthoredById]"));
            migrationBuilder.Sql(AddForeignKeySql(Pips, $"FK_{Pips}_Employees_AuthoredById", "AuthoredById", "Employees"));
            migrationBuilder.Sql(DropIndexSql(Pips, $"IX_{Pips}_PipNumber"));
            migrationBuilder.Sql(DropIndexSql(Pips, $"IX_{Pips}_TenantId"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Pips, "UX_PerformanceImprovementPlans_Tenant_PipNumber",
                "[TenantId], [PipNumber]", "[IsDeleted] = 0",
                "PIP number(s) are held by more than one live plan in a tenant. Renumber the extra plans"));

            // ── 5. The criterion snapshot's KPI tolerance ─────────────────────────────────────────────
            migrationBuilder.Sql(AddColumnSql(Configs, "KpiTolerancePercent", "decimal(18,4) NULL"));

            // ── 6. Probation: the confirming authority's decision on the probation ───────────────────
            migrationBuilder.Sql(AddColumnSql(Probations, "ConfirmationAuthorityEmployeeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Probations, "ConfirmationSubmittedDate", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumnSql(Probations, "ConfirmationDecidedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumnSql(Probations, "ConfirmationDecidedDate", "datetime2 NULL"));
            migrationBuilder.Sql(CreateIndexSql(Probations, $"IX_{Probations}_ConfirmationAuthorityEmployeeId", "[ConfirmationAuthorityEmployeeId]"));
            migrationBuilder.Sql(CreateIndexSql(Probations, $"IX_{Probations}_ConfirmationDecidedById", "[ConfirmationDecidedById]"));
            migrationBuilder.Sql(AddForeignKeySql(Probations, $"FK_{Probations}_Employees_ConfirmationAuthorityEmployeeId",
                "ConfirmationAuthorityEmployeeId", "Employees"));
            migrationBuilder.Sql(AddForeignKeySql(Probations, $"FK_{Probations}_Employees_ConfirmationDecidedById",
                "ConfirmationDecidedById", "Employees"));

            // ── 7. Probation: the extension request (D-99) ────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{ExtensionRequests}', 'U') IS NULL
CREATE TABLE [dbo].[{ExtensionRequests}] (
    [Id]                      uniqueidentifier NOT NULL,
    [ProbationPeriodId]       uniqueidentifier NOT NULL,
    [Status]                  int              NOT NULL,
    [ExtensionMonths]         int              NOT NULL,
    [EndDateWhenRaised]       date             NOT NULL,
    [ProposedEndDate]         date             NOT NULL,
    [Reason]                  nvarchar(2000)   NOT NULL,
    [Comments]                nvarchar(2000)   NULL,
    [RequestedByUserId]       uniqueidentifier NOT NULL,
    [RequestedDate]           datetime2        NOT NULL,
    [AuthorityEmployeeId]     uniqueidentifier NOT NULL,
    [DecidedById]             uniqueidentifier NULL,
    [DecidedDate]             datetime2        NULL,
    [DecisionNotes]           nvarchar(2000)   NULL,
    [SourceRecommendationId]  uniqueidentifier NULL,
    [SourceProbationReviewId] uniqueidentifier NULL,
    [CreatedAt]               datetime2        NOT NULL,
    [UpdatedAt]               datetime2        NULL,
    [CreatedBy]               nvarchar(max)    NULL,
    [UpdatedBy]               nvarchar(max)    NULL,
    [CreatedById]             uniqueidentifier NULL,
    [LastModifiedById]        uniqueidentifier NULL,
    [IsDeleted]               bit              NOT NULL,
    [DeletedAt]               datetime2        NULL,
    [DeletedBy]               nvarchar(max)    NULL,
    [TenantId]                uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{ExtensionRequests}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_{Recommendations}_SourceRecommendationId] FOREIGN KEY ([SourceRecommendationId])
        REFERENCES [dbo].[{Recommendations}] ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_Employees_AuthorityEmployeeId] FOREIGN KEY ([AuthorityEmployeeId])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_Employees_DecidedById] FOREIGN KEY ([DecidedById])
        REFERENCES [dbo].[Employees] ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_{Probations}_ProbationPeriodId] FOREIGN KEY ([ProbationPeriodId])
        REFERENCES [dbo].[{Probations}] ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_ProbationReviews_SourceProbationReviewId] FOREIGN KEY ([SourceProbationReviewId])
        REFERENCES [dbo].[ProbationReviews] ([Id]),
    CONSTRAINT [FK_{ExtensionRequests}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, "IX_ProbationExtensionRequest_ProbationId", "[ProbationPeriodId]"));
            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, "IX_ProbationExtensionRequest_AuthorityEmployeeId", "[AuthorityEmployeeId]"));
            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, $"IX_{ExtensionRequests}_DecidedById", "[DecidedById]"));
            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, $"IX_{ExtensionRequests}_SourceProbationReviewId", "[SourceProbationReviewId]"));
            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, $"IX_{ExtensionRequests}_SourceRecommendationId", "[SourceRecommendationId]"));
            migrationBuilder.Sql(CreateIndexSql(ExtensionRequests, $"IX_{ExtensionRequests}_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(ExtensionRequests, "UX_ProbationExtensionRequest_OneOpenPerProbation",
                "[ProbationPeriodId]", "[Status] IN (1, 2) AND [IsDeleted] = 0",
                "probation(s) hold more than one open extension request"));

            migrationBuilder.Sql(AddColumnSql(Extensions, "ExtensionRequestId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateUniqueIndexRefusingDuplicatesSql(Extensions, "UX_ProbationExtension_ExtensionRequestId",
                "[ExtensionRequestId]", "[ExtensionRequestId] IS NOT NULL AND [IsDeleted] = 0",
                "extension request(s) were applied more than once"));
            migrationBuilder.Sql(AddForeignKeySql(Extensions, $"FK_{Extensions}_{ExtensionRequests}_ExtensionRequestId",
                "ExtensionRequestId", ExtensionRequests));

            // ── 8. The performance sweep's run and dispatch log (F7, lane H) ─────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{SweepRuns}', 'U') IS NULL
CREATE TABLE [dbo].[{SweepRuns}] (
    [Id]                uniqueidentifier NOT NULL,
    [StartedAt]         datetime2        NOT NULL,
    [CompletedAt]       datetime2        NULL,
    [Trigger]           nvarchar(20)     NOT NULL,
    [Sweep]             nvarchar(40)     NOT NULL,
    [TriggeredByUserId] uniqueidentifier NULL,
    [ItemsDispatched]   int              NOT NULL,
    [CreatedAt]         datetime2        NOT NULL,
    [UpdatedAt]         datetime2        NULL,
    [CreatedBy]         nvarchar(max)    NULL,
    [UpdatedBy]         nvarchar(max)    NULL,
    [CreatedById]       uniqueidentifier NULL,
    [LastModifiedById]  uniqueidentifier NULL,
    [IsDeleted]         bit              NOT NULL,
    [DeletedAt]         datetime2        NULL,
    [DeletedBy]         nvarchar(max)    NULL,
    [TenantId]          uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{SweepRuns}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{SweepRuns}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{SweepDispatches}', 'U') IS NULL
CREATE TABLE [dbo].[{SweepDispatches}] (
    [Id]               uniqueidentifier NOT NULL,
    [RunId]            uniqueidentifier NOT NULL,
    [Kind]             nvarchar(60)     NOT NULL,
    [ItemType]         nvarchar(100)    NOT NULL,
    [EntityId]         uniqueidentifier NOT NULL,
    [EmployeeId]       uniqueidentifier NULL,
    [Reference]        nvarchar(250)    NOT NULL,
    [DueDate]          datetime2        NULL,
    [DaysRemaining]    int              NOT NULL,
    [EscalationTier]   int              NOT NULL,
    [DedupeKey]        nvarchar(300)    NOT NULL,
    [CreatedAt]        datetime2        NOT NULL,
    [UpdatedAt]        datetime2        NULL,
    [CreatedBy]        nvarchar(max)    NULL,
    [UpdatedBy]        nvarchar(max)    NULL,
    [CreatedById]      uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted]        bit              NOT NULL,
    [DeletedAt]        datetime2        NULL,
    [DeletedBy]        nvarchar(max)    NULL,
    [TenantId]         uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{SweepDispatches}] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_{SweepDispatches}_{SweepRuns}_RunId] FOREIGN KEY ([RunId])
        REFERENCES [dbo].[{SweepRuns}] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_{SweepDispatches}_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id])
);");

            migrationBuilder.Sql(CreateIndexSql(SweepRuns, $"IX_{SweepRuns}_TenantId_StartedAt", "[TenantId], [StartedAt]"));
            migrationBuilder.Sql(CreateIndexSql(SweepDispatches, $"IX_{SweepDispatches}_RunId", "[RunId]"));
            migrationBuilder.Sql(CreateIndexSql(SweepDispatches, $"IX_{SweepDispatches}_TenantId_CreatedAt", "[TenantId], [CreatedAt]"));
            migrationBuilder.Sql(CreateIndexSql(SweepDispatches, $"IX_{SweepDispatches}_TenantId_EmployeeId", "[TenantId], [EmployeeId]"));
            migrationBuilder.Sql(CreateUniqueIndexSql(SweepDispatches, $"IX_{SweepDispatches}_TenantId_DedupeKey", "[TenantId], [DedupeKey]"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── 1. Refuse what the previous shape cannot hold ─────────────────────────────────────────
            migrationBuilder.Sql(RefuseDownIfAnySql(ExtensionRequests, "1 = 1",
                "probation extension request(s) exist — pending decisions, or the provenance of extensions already applied"));

            // ── 2. The deciders return to the approver columns, where the previous code wrote them ───
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Recommendations}', 'DecidedById') IS NOT NULL
    EXEC sp_executesql N'
UPDATE [dbo].[{Recommendations}]
SET [ApprovedById] = [DecidedById], [ApprovedDate] = [DecidedDate]
WHERE [Status] IN (4, 5) AND ([DecidedById] IS NOT NULL OR [DecidedDate] IS NOT NULL);';");

            // ── 3. The sweep log goes ─────────────────────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{SweepDispatches}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{SweepDispatches}];
IF OBJECT_ID('dbo.{SweepRuns}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{SweepRuns}];");

            // ── 4. Probation: the extension request and the confirmation's authority go ──────────────
            migrationBuilder.Sql(DropForeignKeySql(Extensions, $"FK_{Extensions}_{ExtensionRequests}_ExtensionRequestId"));
            migrationBuilder.Sql(DropIndexSql(Extensions, "UX_ProbationExtension_ExtensionRequestId"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Extensions, "ExtensionRequestId"));
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{ExtensionRequests}', 'U') IS NOT NULL
    DROP TABLE [dbo].[{ExtensionRequests}];");

            migrationBuilder.Sql(DropForeignKeySql(Probations, $"FK_{Probations}_Employees_ConfirmationAuthorityEmployeeId"));
            migrationBuilder.Sql(DropForeignKeySql(Probations, $"FK_{Probations}_Employees_ConfirmationDecidedById"));
            migrationBuilder.Sql(DropIndexSql(Probations, $"IX_{Probations}_ConfirmationAuthorityEmployeeId"));
            migrationBuilder.Sql(DropIndexSql(Probations, $"IX_{Probations}_ConfirmationDecidedById"));
            foreach (var column in new[] { "ConfirmationAuthorityEmployeeId", "ConfirmationSubmittedDate", "ConfirmationDecidedById", "ConfirmationDecidedDate" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(Probations, column));

            // ── 5. The KPI tolerance ──────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(DropColumnWithDefaultSql(Configs, "KpiTolerancePercent"));

            // ── 6. PIPs: the author goes, and the number index is global and non-unique again ─────────
            migrationBuilder.Sql(DropIndexSql(Pips, "UX_PerformanceImprovementPlans_Tenant_PipNumber"));
            migrationBuilder.Sql(DropForeignKeySql(Pips, $"FK_{Pips}_Employees_AuthoredById"));
            migrationBuilder.Sql(DropIndexSql(Pips, $"IX_{Pips}_AuthoredById"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Pips, "AuthoredById"));
            migrationBuilder.Sql(CreateIndexSql(Pips, $"IX_{Pips}_PipNumber", "[PipNumber]"));
            migrationBuilder.Sql(CreateIndexSql(Pips, $"IX_{Pips}_TenantId", "[TenantId]"));

            // ── 7. The proposals' new columns ─────────────────────────────────────────────────────────
            migrationBuilder.Sql(DropForeignKeySql(ActionProposals, $"FK_{ActionProposals}_Employees_SubmittedById"));
            migrationBuilder.Sql(DropForeignKeySql(ActionProposals, $"FK_{ActionProposals}_Employees_ActionedById"));
            migrationBuilder.Sql(DropForeignKeySql(ActionProposals, $"FK_{ActionProposals}_{Pips}_SourcePipId"));
            migrationBuilder.Sql(DropIndexSql(ActionProposals, $"IX_{ActionProposals}_SubmittedById"));
            migrationBuilder.Sql(DropIndexSql(ActionProposals, $"IX_{ActionProposals}_ActionedById"));
            migrationBuilder.Sql(DropIndexSql(ActionProposals, $"IX_{ActionProposals}_SourcePipId"));
            foreach (var column in new[] { "SubmittedById", "SubmittedDate", "ActionedById", "ActionedDate", "SourcePipId", "EffectiveDate" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(ActionProposals, column));

            migrationBuilder.Sql(DropForeignKeySql(SalaryProposals, $"FK_{SalaryProposals}_Employees_SubmittedById"));
            migrationBuilder.Sql(DropForeignKeySql(SalaryProposals, $"FK_{SalaryProposals}_Employees_ActionedById"));
            migrationBuilder.Sql(DropIndexSql(SalaryProposals, $"IX_{SalaryProposals}_SubmittedById"));
            migrationBuilder.Sql(DropIndexSql(SalaryProposals, $"IX_{SalaryProposals}_ActionedById"));
            foreach (var column in new[] { "SubmittedById", "SubmittedDate", "ActionedById", "ActionedDate" })
                migrationBuilder.Sql(DropColumnWithDefaultSql(SalaryProposals, column));

            // ── 8. Recommendations: the open-type index and the decider columns ──────────────────────
            migrationBuilder.Sql(DropIndexSql(Recommendations, "UX_AppraisalOutcomeRecommendations_Appraisal_Type_Open"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Recommendations, "DecidedById"));
            migrationBuilder.Sql(DropColumnWithDefaultSql(Recommendations, "DecidedDate"));
        }

        private static string AddColumnSql(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <summary>
        /// Adds a column and fills it in the same step, so the fill runs exactly once — when the column arrives — and
        /// never on a database that already had it. The fill is dynamic because it names the column being added.
        /// </summary>
        private static string AddColumnWithBackfillSql(string table, string column, string definition, string backfill) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
BEGIN
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};
    EXEC sp_executesql N'{backfill.Replace("'", "''")}';
END";

        private static string CreateIndexSql(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        /// <summary>An unfiltered unique index on a table this migration creates, so it has no rows to refuse.</summary>
        private static string CreateUniqueIndexSql(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        /// <summary>
        /// Creates a filtered unique index, first counting the rows it would refuse. Duplicates stop the migration with a
        /// message, because choosing which row survives is not a schema change.
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

        private static string AddForeignKeySql(string table, string name, string column, string principal) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL AND OBJECT_ID('dbo.{name}', 'F') IS NULL
    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principal}] ([Id]);";

        private static string DropForeignKeySql(string table, string name) => $@"
IF OBJECT_ID('dbo.{name}', 'F') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{name}];";

        /// <summary>Stops a Down that would have to drop something the previous shape cannot hold.</summary>
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
        /// Drops a column that may carry an unnamed default: the default constraint first (SQL Server named it, or there
        /// is none on a model-built database), then the column.
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
