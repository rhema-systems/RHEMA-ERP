using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane F1: what a team or committee is chartered to do, what it has undertaken, and who is
    /// doing it — terms of reference, objectives, tasks, a tick list and task files
    /// (plan § 1.4, § 6.6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these five tables and a bare <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ THE DbSets FOR THESE FIVE ENTITIES ARE LOAD-BEARING.</b> Written down here because the
    /// symptom is remote from the cause and it cost three scaffolds to find. An entity EF first
    /// discovers inside <c>ConfigureHrModule</c> is discovered AFTER
    /// <c>ConfigureGlobalTenantRelationships</c> has run — the pass whose whole job is forcing every
    /// tenant foreign key to <c>Restrict</c> "to avoid multiple cascade paths in SQL Server". Its
    /// tenant FK is then minted by convention afterwards and keeps the convention's CASCADE. For a
    /// table that ALSO cascades from a parent — a checklist item from its task — that is two cascade
    /// paths to <c>Tenants</c>, and SQL Server rejects the constraint outright:
    /// <c>Introducing FOREIGN KEY constraint 'FK_TeamTaskChecklistItems_Tenants_TenantId' … may
    /// cause cycles or multiple cascade paths.</c> Declaring a <c>DbSet</c> makes EF discover the
    /// entity before <c>OnModelCreating</c>'s body runs, so the global pass reaches it. The same
    /// omission is why the first scaffold named these tables in the SINGULAR — after the entity
    /// rather than after the set. Two symptoms, one cause. Remove a DbSet here and both come back.
    /// </para>
    ///
    /// <para>
    /// <b>RESTRICT throughout, except a task's own children.</b> This store SOFT-deletes, so a
    /// cascade would rarely fire anyway; and <c>TeamTasks</c> is reachable from <c>Teams</c> both
    /// directly and through <c>TeamObjectives</c>, so cascading either path would give SQL Server
    /// two routes to the same row. The services do the cleaning up and say so when they will not —
    /// deleting an objective that still has tasks is refused with a count. The exception is a
    /// checklist item and an attachment, each reachable ONLY through its task, so there is exactly
    /// one path to each and neither is worth keeping without it.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> EF scaffolds <c>defaultValue: 0</c> for a non-nullable enum or
    /// bool added to an EXISTING table, ignoring the property initialiser — the trap
    /// <c>AddEmployeePayBasis</c>, <c>AddProbationSourceAndContractKind</c> and
    /// <c>AddSalaryStructurePolicy</c> each hit in this round. Every column here is in a NEW table
    /// with no rows to default. Nothing to repair.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Two tables get an <c>IX_*_TenantId</c> and two do not, and that is correct.</b>
    /// <c>TeamObjectives</c> and <c>TeamTasks</c> carry composites that LEAD with <c>TenantId</c>,
    /// so EF's foreign-key index convention skips the single-column one as redundant.
    /// <c>TeamTaskChecklistItems</c> and <c>TeamTaskAttachments</c> lead theirs with <c>TaskId</c>,
    /// so they still need it. Do not "tidy" the asymmetry — it would put the migration and the model
    /// out of step and the next scaffold would produce a diff.
    /// </para>
    /// </remarks>
    public partial class AddTeamTermsObjectivesAndTasks : Migration
    {
        /// <summary>The ten audit columns every <c>TenantEntity</c> carries, identically.</summary>
        private const string AuditColumns = @"
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,";

        private static string CreateTable(string table, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NULL
CREATE TABLE [dbo].[{table}] (
    [Id] uniqueidentifier NOT NULL,{columns}{AuditColumns}
    CONSTRAINT [PK_{table}] PRIMARY KEY ([Id])
);";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter)
        {
            var where = string.IsNullOrEmpty(filter) ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        /// <remarks>
        /// ⚠ <paramref name="onDelete"/> defaults to NO ACTION, which is what <c>Restrict</c> means
        /// in the model. Pass CASCADE only where the model says Cascade, or a chain-built database
        /// and a model-built one will disagree — and only one of them gets tested.
        /// </remarks>
        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropTable(string table) =>
            $"IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL DROP TABLE [dbo].[{table}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. What the team is chartered to do ──────────────────────────────────
            // Versioned, and an approved version is immutable — see the entity. The signed charter
            // arrives through the controlled gate, so the six document columns are ids and metadata,
            // never a path.
            migrationBuilder.Sql(CreateTable("TeamTermsOfReferences", @"
    [TeamId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [PreviousVersionId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [Purpose] nvarchar(4000) NOT NULL,
    [Scope] nvarchar(4000) NULL,
    [Authority] nvarchar(4000) NULL,
    [MembershipRules] nvarchar(4000) NULL,
    [MeetingCadence] nvarchar(500) NULL,
    [ReportingLine] nvarchar(500) NULL,
    [Deliverables] nvarchar(4000) NULL,
    [EffectiveFrom] date NOT NULL,
    [EffectiveTo] date NULL,
    [ApprovedById] uniqueidentifier NULL,
    [ApprovedOn] datetime2 NULL,
    [DocumentFileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId] uniqueidentifier NULL,
    [DocumentVersionId] uniqueidentifier NULL,
    [DocumentFileName] nvarchar(255) NULL,
    [DocumentMimeType] nvarchar(150) NULL,
    [DocumentFileSizeBytes] bigint NULL,
    [Notes] nvarchar(2000) NULL,"));

            // ── 2. What the team has undertaken ──────────────────────────────────────
            migrationBuilder.Sql(CreateTable("TeamObjectives", @"
    [TeamId] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NULL,
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [Measure] nvarchar(1000) NULL,
    [TargetValue] decimal(18,2) NULL,
    [Unit] nvarchar(50) NULL,
    [Weight] int NULL,
    [StartDate] date NOT NULL,
    [DueDate] date NULL,
    [OwnerMemberId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [ProgressPercent] int NOT NULL,
    [ProgressMode] int NOT NULL,
    [OutcomeSummary] nvarchar(4000) NULL,
    [CompletedOn] date NULL,
    [CancelledReason] nvarchar(1000) NULL,"));

            // ── 3. Who is doing it ───────────────────────────────────────────────────
            // ⚠ SourceMeetingDecisionId is a bare provenance id with NO foreign key: the table it
            // points at arrives in slice F2, and F1 must not depend forward on it. Same shape as
            // EmployeeBenefitEnrollment.SourceBenefitGroupId.
            migrationBuilder.Sql(CreateTable("TeamTasks", @"
    [TeamId] uniqueidentifier NOT NULL,
    [ObjectiveId] uniqueidentifier NULL,
    [Title] nvarchar(300) NOT NULL,
    [Description] nvarchar(4000) NULL,
    [AssigneeMemberId] uniqueidentifier NULL,
    [Priority] int NOT NULL,
    [StartDate] date NULL,
    [DueDate] date NULL,
    [Status] int NOT NULL,
    [BlockedReason] nvarchar(1000) NULL,
    [CompletedOn] date NULL,
    [CompletionNotes] nvarchar(2000) NULL,
    [SourceMeetingDecisionId] uniqueidentifier NULL,"));

            migrationBuilder.Sql(CreateTable("TeamTaskChecklistItems", @"
    [TaskId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Text] nvarchar(500) NOT NULL,
    [IsDone] bit NOT NULL,
    [DoneById] uniqueidentifier NULL,
    [DoneAt] datetime2 NULL,"));

            migrationBuilder.Sql(CreateTable("TeamTaskAttachments", @"
    [TaskId] uniqueidentifier NOT NULL,
    [Title] nvarchar(300) NULL,
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId] uniqueidentifier NULL,
    [DocumentVersionId] uniqueidentifier NULL,
    [FileName] nvarchar(255) NULL,
    [MimeType] nvarchar(150) NULL,
    [FileSizeBytes] bigint NULL,
    [UploadedById] uniqueidentifier NULL,"));

            // ── 4. Indexes ───────────────────────────────────────────────────────────
            // ⚠ Both unique indexes are filtered on IsDeleted. This store soft-deletes, so without
            // the filter a discarded draft would hold version 2 for ever and the next "new version"
            // would fail with an opaque 500 — D-9 and D-10 from slice 0, and the reason the Teams
            // code index carries the same filter.
            migrationBuilder.Sql(CreateUniqueIndex(
                "TeamTermsOfReferences", "IX_TeamTor_Tenant_Team_Version",
                "[TenantId], [TeamId], [Version]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTermsOfReferences", "IX_TeamTor_Tenant_Team_Status", "[TenantId], [TeamId], [Status]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTermsOfReferences", "IX_TeamTermsOfReferences_TeamId", "[TeamId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTermsOfReferences", "IX_TeamTermsOfReferences_ApprovedById", "[ApprovedById]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTermsOfReferences", "IX_TeamTermsOfReferences_PreviousVersionId", "[PreviousVersionId]"));

            // ⚠ Filtered twice: on IsDeleted, and on Code IS NOT NULL because the code is optional
            // and an unfiltered unique index would let exactly one objective per team go without one.
            migrationBuilder.Sql(CreateUniqueIndex(
                "TeamObjectives", "IX_TeamObjective_Tenant_Team_Code",
                "[TenantId], [TeamId], [Code]", "[IsDeleted] = 0 AND [Code] IS NOT NULL"));
            migrationBuilder.Sql(CreateIndex(
                "TeamObjectives", "IX_TeamObjective_Tenant_Team_Status", "[TenantId], [TeamId], [Status]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamObjectives", "IX_TeamObjective_Tenant_Due", "[TenantId], [DueDate]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamObjectives", "IX_TeamObjectives_TeamId", "[TeamId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamObjectives", "IX_TeamObjectives_OwnerMemberId", "[OwnerMemberId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTask_Tenant_Team_Status", "[TenantId], [TeamId], [Status]"));
            // "My tasks" on /me/teams, and the nightly sweep's per-assignee window.
            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTask_Tenant_Assignee_Status", "[TenantId], [AssigneeMemberId], [Status]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTask_Tenant_Due", "[TenantId], [DueDate]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTask_ObjectiveId", "[ObjectiveId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTasks_TeamId", "[TeamId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTasks", "IX_TeamTasks_AssigneeMemberId", "[AssigneeMemberId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamTaskChecklistItems", "IX_TeamTaskChecklistItem_Task_Order", "[TaskId], [DisplayOrder]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTaskChecklistItems", "IX_TeamTaskChecklistItems_DoneById", "[DoneById]"));
            // ⚠ These two DO need a single-column tenant index — their composites lead with TaskId,
            // so EF's foreign-key convention does not consider TenantId covered. See the remarks.
            migrationBuilder.Sql(CreateIndex(
                "TeamTaskChecklistItems", "IX_TeamTaskChecklistItems_TenantId", "[TenantId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamTaskAttachments", "IX_TeamTaskAttachment_TaskId", "[TaskId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTaskAttachments", "IX_TeamTaskAttachments_UploadedById", "[UploadedById]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamTaskAttachments", "IX_TeamTaskAttachments_TenantId", "[TenantId]"));

            // ── 5. Foreign keys ──────────────────────────────────────────────────────
            // ⚠ Every tenant FK is NO ACTION, matching what ConfigureGlobalTenantRelationships puts
            // in the model. Making any of them CASCADE is what broke the first attempt at this
            // migration — see the remarks.
            migrationBuilder.Sql(AddForeignKey(
                "TeamTermsOfReferences", "FK_TeamTermsOfReferences_Teams_TeamId", "TeamId", "Teams"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTermsOfReferences", "FK_TeamTermsOfReferences_Employees_ApprovedById", "ApprovedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTermsOfReferences", "FK_TeamTermsOfReferences_TeamTermsOfReferences_PreviousVersionId",
                "PreviousVersionId", "TeamTermsOfReferences"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTermsOfReferences", "FK_TeamTermsOfReferences_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamObjectives", "FK_TeamObjectives_Teams_TeamId", "TeamId", "Teams"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamObjectives", "FK_TeamObjectives_TeamMembers_OwnerMemberId", "OwnerMemberId", "TeamMembers"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamObjectives", "FK_TeamObjectives_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamTasks", "FK_TeamTasks_Teams_TeamId", "TeamId", "Teams"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTasks", "FK_TeamTasks_TeamObjectives_ObjectiveId", "ObjectiveId", "TeamObjectives"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTasks", "FK_TeamTasks_TeamMembers_AssigneeMemberId", "AssigneeMemberId", "TeamMembers"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTasks", "FK_TeamTasks_Tenants_TenantId", "TenantId", "Tenants"));

            // ⚠ CASCADE here and only here: a checklist item and an attachment are reachable ONLY
            // through their task, so there is exactly one path to each.
            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskChecklistItems", "FK_TeamTaskChecklistItems_TeamTasks_TaskId", "TaskId", "TeamTasks", "CASCADE"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskChecklistItems", "FK_TeamTaskChecklistItems_Employees_DoneById", "DoneById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskChecklistItems", "FK_TeamTaskChecklistItems_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskAttachments", "FK_TeamTaskAttachments_TeamTasks_TaskId", "TaskId", "TeamTasks", "CASCADE"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskAttachments", "FK_TeamTaskAttachments_Employees_UploadedById", "UploadedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamTaskAttachments", "FK_TeamTaskAttachments_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Children before parents: TeamTasks is referenced by both leaf tables, and
            // TeamObjectives by TeamTasks.
            migrationBuilder.Sql(DropTable("TeamTaskAttachments"));
            migrationBuilder.Sql(DropTable("TeamTaskChecklistItems"));
            migrationBuilder.Sql(DropTable("TeamTasks"));
            migrationBuilder.Sql(DropTable("TeamObjectives"));
            migrationBuilder.Sql(DropTable("TeamTermsOfReferences"));
        }
    }
}
