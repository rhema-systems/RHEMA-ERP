using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane F2: how a team actually works — what it met about, what it decided, how it did, and the
    /// nightly sweep that chases what is slipping (plan § 1.4, § 6.6).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these tables and a bare <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ THE DEFAULT-VALUE TRAP, FOR THE FOURTH TIME THIS ROUND.</b> EF scaffolded
    /// <c>TeamTaskReminderLeadDays</c> with <c>defaultValue: 0</c>, ignoring the entity's
    /// <c>= 3</c> initialiser, and paired it with an <c>UpdateData</c> that fixes only the ONE
    /// seeded settings row. Every other tenant's row would have been backfilled with zero — a lead
    /// time of nothing, so the sweep would warn on the due date and never before, silently. Written
    /// here as <c>DEFAULT (3)</c> so every existing row gets the real number, exactly as
    /// <c>AddCertificationModel</c> had to do for <c>CertificationExpiryLeadDays</c>.
    /// <b>Read every scaffolded AddColumn default against the entity initialiser.</b>
    /// </para>
    ///
    /// <para>
    /// <b>Two tables the plan did not list</b>, and the reason matters:
    /// <c>TeamReminderRuns</c> and <c>TeamReminderDispatchLogs</c>. Every other HR sweep has them,
    /// and <b>two HR sweeps in this codebase turned out never to have run at all</b> — the host
    /// registration was missing and nothing said so, because there was no run record to be empty.
    /// A sweep that leaves no trace cannot be shown to have fired. The unique
    /// <c>(TenantId, DedupeKey)</c> index is the send-once guarantee, claimed in the same
    /// <c>SaveChanges</c> that records the run, so the nightly host and the run-now button cannot
    /// double-send even if they overlap.
    /// </para>
    ///
    /// <para>
    /// <b>RESTRICT throughout, except where a child is reachable only through one parent.</b> A
    /// meeting's attendees and decisions die with the meeting; a review's lines die with the review;
    /// a dispatch log dies with its run. Everything else — and every tenant foreign key — is NO
    /// ACTION, which is what the model says now that these entities have <c>DbSet</c>s and the
    /// global tenant pass reaches them. <c>TeamReviewLines → TeamObjectives</c> is deliberately
    /// RESTRICT and not Cascade: a review states what was true of an objective at the time, and
    /// deleting the objective must not quietly rewrite last quarter's review.
    /// </para>
    /// </remarks>
    public partial class AddTeamMeetingsAndReviews : Migration
    {
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

        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter = null)
        {
            var where = string.IsNullOrEmpty(filter) ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        /// <remarks>
        /// ⚠ Defaults to NO ACTION, which is what <c>Restrict</c> means in the model. Pass CASCADE
        /// only where the model says Cascade, or a chain-built database and a model-built one will
        /// disagree — and only one of them gets tested.
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The policy setting ────────────────────────────────────────────────
            // ⚠ DEFAULT (3), not the scaffold's 0. Days rather than weeks on purpose: a probation
            // end is a date somebody plans a month around; a committee action item is a thing
            // somebody does on Tuesday, and a month of nagging about it is noise.
            migrationBuilder.Sql(AddColumn(
                "CompanyHrPolicySettings", "TeamTaskReminderLeadDays",
                "int NOT NULL CONSTRAINT [DF_CompanyHrPolicySettings_TeamTaskReminderLeadDays] DEFAULT (3)"));

            // ── 2. The minute book ───────────────────────────────────────────────────
            // ⚠ ScheduledAt and HeldAt are BOTH kept. A committee that met three weeks late still
            // met; overwriting the scheduled date would make the record unable to answer whether
            // the team met when its terms of reference said it should.
            migrationBuilder.Sql(CreateTable("TeamMeetings", @"
    [TeamId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [ScheduledAt] datetime2 NOT NULL,
    [HeldAt] datetime2 NULL,
    [Venue] nvarchar(300) NULL,
    [Agenda] nvarchar(4000) NULL,
    [Minutes] nvarchar(max) NULL,
    [Status] int NOT NULL,
    [ChairMemberId] uniqueidentifier NULL,
    [CancelledReason] nvarchar(1000) NULL,
    [MinutesFileUploadRecordId] uniqueidentifier NULL,
    [MinutesDocumentRecordId] uniqueidentifier NULL,
    [MinutesDocumentVersionId] uniqueidentifier NULL,
    [MinutesFileName] nvarchar(255) NULL,
    [MinutesMimeType] nvarchar(150) NULL,
    [MinutesFileSizeBytes] bigint NULL,"));

            // ⚠ Attended is NULLABLE. Three states are real and a bit holds two: came, did not, and
            // not yet known because the meeting has not happened. A NOT NULL default of 0 would
            // mark every invitee absent the moment the meeting was scheduled.
            migrationBuilder.Sql(CreateTable("TeamMeetingAttendees", @"
    [MeetingId] uniqueidentifier NOT NULL,
    [MemberId] uniqueidentifier NOT NULL,
    [Attended] bit NULL,
    [Apology] bit NOT NULL,
    [Notes] nvarchar(500) NULL,"));

            // ⚠ RaisedTaskId has NO foreign key: TeamTask.SourceMeetingDecisionId already points
            // this way, and a second configured relationship between the same pair is how EF mints
            // a shadow FK column beside one of them. The service keeps both ends consistent.
            migrationBuilder.Sql(CreateTable("TeamMeetingDecisions", @"
    [MeetingId] uniqueidentifier NOT NULL,
    [DisplayOrder] int NOT NULL,
    [Text] nvarchar(2000) NOT NULL,
    [ResponsibleMemberId] uniqueidentifier NULL,
    [DueDate] date NULL,
    [RaisedTaskId] uniqueidentifier NULL,"));

            // ── 3. The reviews ───────────────────────────────────────────────────────
            // ReviewedById is an EMPLOYEE, not a team member: the reviewer is usually NOT on the
            // team — a sponsor, a unit head, HR — and a TeamMember FK would have made the commonest
            // reviewer unrepresentable.
            migrationBuilder.Sql(CreateTable("TeamReviews", @"
    [TeamId] uniqueidentifier NOT NULL,
    [PeriodStart] date NOT NULL,
    [PeriodEnd] date NOT NULL,
    [ReviewedById] uniqueidentifier NULL,
    [OverallRating] int NULL,
    [Summary] nvarchar(4000) NULL,
    [Recommendations] nvarchar(4000) NULL,
    [Status] int NOT NULL,
    [AcknowledgedById] uniqueidentifier NULL,
    [AcknowledgedOn] datetime2 NULL,
    [AcknowledgementNote] nvarchar(2000) NULL,"));

            // ⚠ ProgressAtReview is a SNAPSHOT column, not a join. A review says what was true in
            // March; reading the objective's current figure would rewrite every past review each
            // time somebody ticked a task.
            migrationBuilder.Sql(CreateTable("TeamReviewLines", @"
    [ReviewId] uniqueidentifier NOT NULL,
    [ObjectiveId] uniqueidentifier NOT NULL,
    [ProgressAtReview] int NOT NULL,
    [Rating] int NULL,
    [Comment] nvarchar(2000) NULL,"));

            // ── 4. The sweep's own record ────────────────────────────────────────────
            migrationBuilder.Sql(CreateTable("TeamReminderRuns", @"
    [StartedAt] datetime2 NOT NULL,
    [CompletedAt] datetime2 NULL,
    [Trigger] nvarchar(20) NOT NULL,
    [TriggeredByUserId] uniqueidentifier NULL,
    [RemindersQueued] int NOT NULL,"));

            migrationBuilder.Sql(CreateTable("TeamReminderDispatchLogs", @"
    [RunId] uniqueidentifier NOT NULL,
    [Kind] nvarchar(60) NOT NULL,
    [ItemType] nvarchar(100) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [TeamId] uniqueidentifier NOT NULL,
    [Reference] nvarchar(250) NOT NULL,
    [DueDate] date NULL,
    [DaysRemaining] int NOT NULL,
    [RoutedToEmployeeId] uniqueidentifier NULL,
    [DedupeKey] nvarchar(300) NOT NULL,"));

            // ── 5. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(
                "TeamMeetings", "IX_TeamMeeting_Tenant_Team_Status", "[TenantId], [TeamId], [Status]"));
            // The dashboard's "next meeting", and the sweep's meeting-tomorrow window.
            migrationBuilder.Sql(CreateIndex(
                "TeamMeetings", "IX_TeamMeeting_Tenant_Scheduled", "[TenantId], [ScheduledAt]"));
            migrationBuilder.Sql(CreateIndex("TeamMeetings", "IX_TeamMeetings_TeamId", "[TeamId]"));
            migrationBuilder.Sql(CreateIndex("TeamMeetings", "IX_TeamMeetings_ChairMemberId", "[ChairMemberId]"));

            // ⚠ Filtered on IsDeleted: this store soft-deletes, so without it a member taken off the
            // invitee list and put back would collide with their own tombstone.
            migrationBuilder.Sql(CreateUniqueIndex(
                "TeamMeetingAttendees", "IX_TeamMeetingAttendee_Meeting_Member",
                "[MeetingId], [MemberId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex("TeamMeetingAttendees", "IX_TeamMeetingAttendees_MemberId", "[MemberId]"));
            migrationBuilder.Sql(CreateIndex("TeamMeetingAttendees", "IX_TeamMeetingAttendees_TenantId", "[TenantId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamMeetingDecisions", "IX_TeamMeetingDecision_Meeting_Order", "[MeetingId], [DisplayOrder]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamMeetingDecisions", "IX_TeamMeetingDecision_RaisedTaskId", "[RaisedTaskId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamMeetingDecisions", "IX_TeamMeetingDecisions_ResponsibleMemberId", "[ResponsibleMemberId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamMeetingDecisions", "IX_TeamMeetingDecisions_TenantId", "[TenantId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamReviews", "IX_TeamReview_Tenant_Team_Period", "[TenantId], [TeamId], [PeriodEnd]"));
            migrationBuilder.Sql(CreateIndex("TeamReviews", "IX_TeamReviews_TeamId", "[TeamId]"));
            migrationBuilder.Sql(CreateIndex("TeamReviews", "IX_TeamReviews_ReviewedById", "[ReviewedById]"));
            migrationBuilder.Sql(CreateIndex("TeamReviews", "IX_TeamReviews_AcknowledgedById", "[AcknowledgedById]"));

            migrationBuilder.Sql(CreateUniqueIndex(
                "TeamReviewLines", "IX_TeamReviewLine_Review_Objective",
                "[ReviewId], [ObjectiveId]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex("TeamReviewLines", "IX_TeamReviewLines_ObjectiveId", "[ObjectiveId]"));
            migrationBuilder.Sql(CreateIndex("TeamReviewLines", "IX_TeamReviewLines_TenantId", "[TenantId]"));

            migrationBuilder.Sql(CreateIndex(
                "TeamReminderRuns", "IX_TeamReminderRun_Tenant_Started", "[TenantId], [StartedAt]"));

            // ⚠ THE SEND-ONCE GUARANTEE. Unfiltered on purpose — a soft-deleted dispatch must still
            // hold its key, or purging the log would let the sweep re-send everything it had
            // already sent.
            migrationBuilder.Sql(CreateUniqueIndex(
                "TeamReminderDispatchLogs", "IX_TeamReminderDispatch_Tenant_DedupeKey",
                "[TenantId], [DedupeKey]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamReminderDispatchLogs", "IX_TeamReminderDispatch_Tenant_Team", "[TenantId], [TeamId]"));
            migrationBuilder.Sql(CreateIndex(
                "TeamReminderDispatchLogs", "IX_TeamReminderDispatchLogs_RunId", "[RunId]"));

            // ── 6. Foreign keys ──────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetings", "FK_TeamMeetings_Teams_TeamId", "TeamId", "Teams"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetings", "FK_TeamMeetings_TeamMembers_ChairMemberId", "ChairMemberId", "TeamMembers"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetings", "FK_TeamMeetings_Tenants_TenantId", "TenantId", "Tenants"));

            // CASCADE: an attendee row and a decision row are reachable only through their meeting.
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingAttendees", "FK_TeamMeetingAttendees_TeamMeetings_MeetingId",
                "MeetingId", "TeamMeetings", "CASCADE"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingAttendees", "FK_TeamMeetingAttendees_TeamMembers_MemberId", "MemberId", "TeamMembers"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingAttendees", "FK_TeamMeetingAttendees_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingDecisions", "FK_TeamMeetingDecisions_TeamMeetings_MeetingId",
                "MeetingId", "TeamMeetings", "CASCADE"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingDecisions", "FK_TeamMeetingDecisions_TeamMembers_ResponsibleMemberId",
                "ResponsibleMemberId", "TeamMembers"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamMeetingDecisions", "FK_TeamMeetingDecisions_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamReviews", "FK_TeamReviews_Teams_TeamId", "TeamId", "Teams"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReviews", "FK_TeamReviews_Employees_ReviewedById", "ReviewedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReviews", "FK_TeamReviews_Employees_AcknowledgedById", "AcknowledgedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReviews", "FK_TeamReviews_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamReviewLines", "FK_TeamReviewLines_TeamReviews_ReviewId",
                "ReviewId", "TeamReviews", "CASCADE"));
            // ⚠ RESTRICT, not Cascade. A review states what was true of an objective at the time;
            // deleting the objective must not quietly rewrite last quarter's review.
            migrationBuilder.Sql(AddForeignKey(
                "TeamReviewLines", "FK_TeamReviewLines_TeamObjectives_ObjectiveId",
                "ObjectiveId", "TeamObjectives"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReviewLines", "FK_TeamReviewLines_Tenants_TenantId", "TenantId", "Tenants"));

            migrationBuilder.Sql(AddForeignKey(
                "TeamReminderRuns", "FK_TeamReminderRuns_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReminderDispatchLogs", "FK_TeamReminderDispatchLogs_TeamReminderRuns_RunId",
                "RunId", "TeamReminderRuns", "CASCADE"));
            migrationBuilder.Sql(AddForeignKey(
                "TeamReminderDispatchLogs", "FK_TeamReminderDispatchLogs_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Children before parents.
            migrationBuilder.Sql(DropTable("TeamReminderDispatchLogs"));
            migrationBuilder.Sql(DropTable("TeamReminderRuns"));
            migrationBuilder.Sql(DropTable("TeamReviewLines"));
            migrationBuilder.Sql(DropTable("TeamReviews"));
            migrationBuilder.Sql(DropTable("TeamMeetingDecisions"));
            migrationBuilder.Sql(DropTable("TeamMeetingAttendees"));
            migrationBuilder.Sql(DropTable("TeamMeetings"));

            migrationBuilder.Sql(DropColumn("CompanyHrPolicySettings", "TeamTaskReminderLeadDays"));
        }
    }
}
