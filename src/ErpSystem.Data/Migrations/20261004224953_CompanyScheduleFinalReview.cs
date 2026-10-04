using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Company-schedule final closure, lane 0 — the one schema batch for the whole closure
    /// (<c>docs/HR/areas/company-schedule/HR-COMPANY-SCHEDULE-FINAL-CLOSURE-PLAN.md</c> § 4 lane 0, § 6).
    /// </summary>
    /// <remarks>
    /// <para><b>What it adds.</b> On <c>CompanyEvents</c>: <c>RecurrenceSeriesId</c> and
    /// <c>OccurrenceNumber</c> (D-2, D-12: the occurrences ARE the series — there is no series table),
    /// <c>OrganizationUnitId</c> (D-5), and <c>SourceEntityType</c> / <c>SourceEntityId</c> (C-51, the
    /// SHE drill's event). On <c>BusinessClosures</c>: <c>OrganizationUnitId</c> (D-1, D-5) and
    /// <c>RecursAnnually</c> (C-38). On <c>EventAttachments</c>: the upload-gate columns (D-3). The new
    /// <c>CompanyMilestoneDocuments</c> table (D-3). Unique filtered indexes so one employee cannot be
    /// on one event twice, as a guest or in the attendance register (F-45). No milestone-link
    /// columns: D-17 dropped the link.</para>
    ///
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration:
    /// <c>rebuild-db</c> builds from the EF model, so a rebuilt database already has every object
    /// below, and a bare <c>AddColumn</c> or <c>CreateTable</c> stops the chain.</para>
    ///
    /// <para><b>⚠ The scaffold carried two operations of Estate's</b> — <c>ExternalPremiumChargeAmount</c>
    /// on <c>EstateManagedAssets</c> and <c>EstateLandDemarcations</c>. Master's
    /// <c>20261004103000_AddEstateListingPremiumChargeAmount</c> is hand-written and never updated the
    /// snapshot. They are not this migration's and are left out; that migration creates the columns,
    /// and the regenerated snapshot now records them. The snapshot also moved
    /// <c>AuthenticationSettings.LoginPageStyle</c> into alphabetical order — no change to the model.</para>
    ///
    /// <para><b>The two <c>IX_*_TenantId</c> indexes are dropped by EF's own convention</b>: a
    /// foreign-key column that leads another index gets no index of its own, and each new unique
    /// index leads with <c>TenantId</c>. Kept, guarded, so a migrated database matches a model-built
    /// one. The new indexes are FILTERED (live rows; on participants, internal guests only), so they
    /// do not cover a soft-deleted row or an external guest — which nothing reads by tenant alone:
    /// every read goes by event (<c>IX_*_EventId</c> stays), and the only other user of a
    /// <c>TenantId</c> index is the foreign key's check when a tenant is deleted.</para>
    ///
    /// <para><b>⚠ Duplicates are refused, not repaired (F-45).</b> A unique index cannot be built over
    /// a pair that is already doubled, and SQL Server's own failure names the index rather than the
    /// rows. The <c>Up</c> checks first and stops with a sentence naming each event and employee.
    /// Which row stands — the answer someone gave, the attendance mark someone relies on — is a
    /// decision for whoever knows the event. <c>ErpSystemDB_UAT</c> had none, measured read-only on
    /// 2026-10-04 before this was written.</para>
    ///
    /// <para><b>No data steps (decisions L0-1, L0-2).</b> The organisation unit replaces the
    /// department outright (D-5), and no event or closure was ever saved against a department —
    /// none on UAT, the dev database or the test-data database, live or deleted — so nothing is
    /// carried across. <c>DepartmentId</c> stays only while today's code still reads it; a later
    /// migration drops it once lanes 1 and 2 have moved every read to the unit. No closure
    /// contradicts D-1 either (UAT's one is a whole-company Full closure), and lane 1 refuses a
    /// contradictory save from now on.</para>
    ///
    /// <para><b>No default-value trap.</b> <c>RecursAnnually</c> is the one non-nullable column added
    /// to an existing table, and false is its true default for every existing closure — none of them
    /// was typed as recurring.</para>
    ///
    /// <para><b>⚠ Down refuses once the new features hold data.</b> It cannot put a unit closure
    /// back into a department, a series back together, or an uploaded file back on its record, so
    /// it stops with a sentence counting what would be lost rather than choosing what to lose.
    /// Straight after <c>Up</c>, with none of them used, it runs.</para>
    /// </remarks>
    public partial class CompanyScheduleFinalReview : Migration
    {
        private const string Events = "CompanyEvents";
        private const string Closures = "BusinessClosures";
        private const string Attachments = "EventAttachments";
        private const string Participants = "EventParticipants";
        private const string Attendance = "EventAttendances";
        private const string MilestoneDocuments = "CompanyMilestoneDocuments";

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database the
        /// name is server-generated, so guessing it would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}) WHERE {filter};";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE NO ACTION;";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

        /// <summary>
        /// Stops the migration, naming each event and employee, if one employee is already on one event
        /// twice in <paramref name="table"/> (F-45).
        /// </summary>
        /// <remarks>
        /// ⚠ It REFUSES; it does not repair — see the class remarks. <paramref name="filter"/> is the
        /// unique index's own filter, so the check and the index agree on what counts.
        /// </remarks>
        private static string RefuseOnDuplicates(string table, string filter, string what, int error) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[{table}] WHERE {filter}
                GROUP BY [TenantId], [EventId], [EmployeeId] HAVING COUNT(*) > 1)
BEGIN
    DECLARE @dupes_{table} nvarchar(max);
    SELECT @dupes_{table} = STRING_AGG(CONVERT(nvarchar(max), CONCAT(
               N'event ', COALESCE(ev.[EventNumber], CONVERT(nvarchar(36), d.[EventId])),
               N' / employee ', COALESCE(em.[EmployeeNumber], CONVERT(nvarchar(36), d.[EmployeeId])),
               N' (', d.[n], N' rows)')), N', ')
      FROM (SELECT [EventId], [EmployeeId], COUNT(*) AS n FROM [dbo].[{table}] WHERE {filter}
             GROUP BY [TenantId], [EventId], [EmployeeId] HAVING COUNT(*) > 1) d
      LEFT JOIN [dbo].[CompanyEvents] ev ON ev.[Id] = d.[EventId]
      LEFT JOIN [dbo].[Employees] em ON em.[Id] = d.[EmployeeId];

    DECLARE @msg_{table} nvarchar(2048) = LEFT(CONCAT(
        N'{table} holds the same employee more than once on one event as {what}: ', @dupes_{table},
        N'. A unique index cannot be created over them. Decide which row stands for each pair, soft-delete the others (IsDeleted = 1), then run this migration again.'),
        2048);
    THROW {error}, @msg_{table}, 1;
END";

        /// <summary>
        /// One line of the Down's refusal: counts the rows of <paramref name="table"/> matching
        /// <paramref name="where"/>, when the column exists, and adds them to <c>@used</c>.
        /// </summary>
        /// <remarks>
        /// Dynamic SQL, because a column this migration added may be absent on a database it never
        /// reached, and a static reference to it would fail the whole batch at compile time.
        /// </remarks>
        private static string CountUse(string table, string column, string where, string label) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    SET @n = 0;
    EXEC sp_executesql N'SELECT @n = COUNT(*) FROM [dbo].[{table}] WHERE {where}', N'@n int OUTPUT', @n = @n OUTPUT;
    IF @n > 0 SET @used = CONCAT(@used, CASE WHEN @used = N'' THEN N'' ELSE N'; ' END, @n, N' {label}');
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Columns ───────────────────────────────────────────────────────────
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then using it in one batch fails with "Invalid column name".

            // D-2, D-12: the series is the occurrences sharing an id — no series table.
            migrationBuilder.Sql(AddColumn(Events, "RecurrenceSeriesId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Events, "OccurrenceNumber", "int NULL"));
            // D-5: the unit replaces the legacy department.
            migrationBuilder.Sql(AddColumn(Events, "OrganizationUnitId", "uniqueidentifier NULL"));
            // C-51: the record that created the event — today the SHE emergency drill. No FK: the
            // source may be in any module.
            migrationBuilder.Sql(AddColumn(Events, "SourceEntityType", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumn(Events, "SourceEntityId", "uniqueidentifier NULL"));

            // D-1, D-5: an organisation-unit closure covers the unit and everything beneath it.
            migrationBuilder.Sql(AddColumn(Closures, "OrganizationUnitId", "uniqueidentifier NULL"));
            // C-38. Named default, so every existing closure reads "does not recur" and Down can find it.
            migrationBuilder.Sql(AddColumn(Closures, "RecursAnnually",
                "bit NOT NULL CONSTRAINT [DF_BusinessClosures_RecursAnnually] DEFAULT (0)"));

            // D-3: the upload gate. All nullable — the four rows UAT holds are path-only and stay so.
            migrationBuilder.Sql(AddColumn(Attachments, "UploadedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Attachments, "FileSizeBytes", "bigint NULL"));
            migrationBuilder.Sql(AddColumn(Attachments, "FileUploadRecordId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Attachments, "DocumentRecordId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Attachments, "DocumentVersionId", "uniqueidentifier NULL"));

            // ── 2. Milestone documents (D-3) ─────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CompanyMilestoneDocuments', 'U') IS NULL
CREATE TABLE [dbo].[CompanyMilestoneDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [MilestoneId] uniqueidentifier NOT NULL,
    [FileName] nvarchar(200) NOT NULL,
    [FilePath] nvarchar(500) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [UploadDate] datetime2 NOT NULL,
    [UploadedById] uniqueidentifier NOT NULL,
    [FileSizeBytes] bigint NULL,
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId] uniqueidentifier NULL,
    [DocumentVersionId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CompanyMilestoneDocuments] PRIMARY KEY ([Id])
);");

            // ── 3. One employee once per event (F-45): check, build, then drop the old ──
            // ⚠ The check comes FIRST and stops the migration; it does not choose which row to lose.
            migrationBuilder.Sql(RefuseOnDuplicates(Participants,
                "[EmployeeId] IS NOT NULL AND [IsDeleted] = 0", "a guest", 51520));
            migrationBuilder.Sql(RefuseOnDuplicates(Attendance,
                "[IsDeleted] = 0", "an attendance mark", 51521));

            migrationBuilder.Sql(CreateUniqueIndex(Participants, "UX_EventParticipant_Tenant_Event_Employee",
                "[TenantId], [EventId], [EmployeeId]", "[EmployeeId] IS NOT NULL AND [IsDeleted] = 0"));
            migrationBuilder.Sql(CreateUniqueIndex(Attendance, "UX_EventAttendance_Tenant_Event_Employee",
                "[TenantId], [EventId], [EmployeeId]", "[IsDeleted] = 0"));

            // EF's convention (see the class remarks). Dropped only AFTER the unique indexes exist,
            // so the tenant column is never left unindexed between two statements.
            migrationBuilder.Sql(DropIndex(Participants, "IX_EventParticipants_TenantId"));
            migrationBuilder.Sql(DropIndex(Attendance, "IX_EventAttendances_TenantId"));

            // ── 4. Other indexes ─────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvents_OrganizationUnitId", "[OrganizationUnitId]"));
            // D-12: a series action ("this and following", "the whole series") is a query on this.
            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvents_RecurrenceSeriesId", "[RecurrenceSeriesId]"));
            // C-51: the drill finds the event it scheduled, to move or cancel it.
            migrationBuilder.Sql(CreateIndex(Events, "IX_CompanyEvents_SourceEntityType_SourceEntityId",
                "[SourceEntityType], [SourceEntityId]"));
            migrationBuilder.Sql(CreateIndex(Closures, "IX_BusinessClosures_OrganizationUnitId", "[OrganizationUnitId]"));
            migrationBuilder.Sql(CreateIndex(Attachments, "IX_EventAttachments_UploadedById", "[UploadedById]"));
            migrationBuilder.Sql(CreateIndex(MilestoneDocuments, "IX_CompanyMilestoneDocuments_MilestoneId", "[MilestoneId]"));
            migrationBuilder.Sql(CreateIndex(MilestoneDocuments, "IX_CompanyMilestoneDocuments_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(MilestoneDocuments, "IX_CompanyMilestoneDocuments_UploadedById", "[UploadedById]"));

            // ── 5. Foreign keys ──────────────────────────────────────────────────────
            // NO ACTION on all of them (the model's Restrict): a unit, an employee or a milestone that
            // a record stands on cannot be deleted out from under it.
            migrationBuilder.Sql(AddForeignKey(Events,
                "FK_CompanyEvents_OrganizationUnits_OrganizationUnitId", "OrganizationUnitId", "OrganizationUnits"));
            migrationBuilder.Sql(AddForeignKey(Closures,
                "FK_BusinessClosures_OrganizationUnits_OrganizationUnitId", "OrganizationUnitId", "OrganizationUnits"));
            migrationBuilder.Sql(AddForeignKey(Attachments,
                "FK_EventAttachments_Employees_UploadedById", "UploadedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(MilestoneDocuments,
                "FK_CompanyMilestoneDocuments_CompanyMilestones_MilestoneId", "MilestoneId", "CompanyMilestones"));
            migrationBuilder.Sql(AddForeignKey(MilestoneDocuments,
                "FK_CompanyMilestoneDocuments_Employees_UploadedById", "UploadedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(MilestoneDocuments,
                "FK_CompanyMilestoneDocuments_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ First: refuse if anything would be lost (see the class remarks). One batch, so the
            // counts and the refusal share @used.
            migrationBuilder.Sql($@"
DECLARE @used nvarchar(max) = N'', @n int;
{CountUse(Events, "OrganizationUnitId", "[OrganizationUnitId] IS NOT NULL", "events scoped to an organisation unit")}
{CountUse(Closures, "OrganizationUnitId", "[OrganizationUnitId] IS NOT NULL", "closures scoped to an organisation unit")}
{CountUse(Events, "RecurrenceSeriesId", "[RecurrenceSeriesId] IS NOT NULL", "events in a recurring series")}
{CountUse(Events, "SourceEntityId", "[SourceEntityId] IS NOT NULL", "events created by another record")}
{CountUse(Closures, "RecursAnnually", "[RecursAnnually] = 1", "closures that recur every year")}
{CountUse(Attachments, "FileUploadRecordId", "[FileUploadRecordId] IS NOT NULL", "event attachments stored as files")}
{CountUse(MilestoneDocuments, "Id", "1 = 1", "milestone documents")}
IF @used <> N''
BEGIN
    DECLARE @msg nvarchar(2048) = LEFT(CONCAT(
        N'CompanyScheduleFinalReview cannot be rolled back without losing data: ', @used,
        N'. Down would drop the columns and the table that hold them. Remove or re-scope those records first, or restore a backup taken before the migration.'),
        2048);
    THROW 51522, @msg, 1;
END");

            migrationBuilder.Sql(DropForeignKey(Events, "FK_CompanyEvents_OrganizationUnits_OrganizationUnitId"));
            migrationBuilder.Sql(DropForeignKey(Closures, "FK_BusinessClosures_OrganizationUnits_OrganizationUnitId"));
            migrationBuilder.Sql(DropForeignKey(Attachments, "FK_EventAttachments_Employees_UploadedById"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.CompanyMilestoneDocuments', 'U') IS NOT NULL DROP TABLE [dbo].[CompanyMilestoneDocuments];");

            // Restored BEFORE the unique indexes go, so the tenant column is never left unindexed.
            migrationBuilder.Sql(CreateIndex(Participants, "IX_EventParticipants_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Attendance, "IX_EventAttendances_TenantId", "[TenantId]"));
            migrationBuilder.Sql(DropIndex(Participants, "UX_EventParticipant_Tenant_Event_Employee"));
            migrationBuilder.Sql(DropIndex(Attendance, "UX_EventAttendance_Tenant_Event_Employee"));

            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvents_OrganizationUnitId"));
            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvents_RecurrenceSeriesId"));
            migrationBuilder.Sql(DropIndex(Events, "IX_CompanyEvents_SourceEntityType_SourceEntityId"));
            migrationBuilder.Sql(DropIndex(Closures, "IX_BusinessClosures_OrganizationUnitId"));
            migrationBuilder.Sql(DropIndex(Attachments, "IX_EventAttachments_UploadedById"));

            migrationBuilder.Sql(DropColumn(Attachments, "DocumentVersionId"));
            migrationBuilder.Sql(DropColumn(Attachments, "DocumentRecordId"));
            migrationBuilder.Sql(DropColumn(Attachments, "FileUploadRecordId"));
            migrationBuilder.Sql(DropColumn(Attachments, "FileSizeBytes"));
            migrationBuilder.Sql(DropColumn(Attachments, "UploadedById"));

            migrationBuilder.Sql(DropColumn(Closures, "RecursAnnually"));
            migrationBuilder.Sql(DropColumn(Closures, "OrganizationUnitId"));

            migrationBuilder.Sql(DropColumn(Events, "SourceEntityId"));
            migrationBuilder.Sql(DropColumn(Events, "SourceEntityType"));
            migrationBuilder.Sql(DropColumn(Events, "OrganizationUnitId"));
            migrationBuilder.Sql(DropColumn(Events, "OccurrenceNumber"));
            migrationBuilder.Sql(DropColumn(Events, "RecurrenceSeriesId"));
        }
    }
}
