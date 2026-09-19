using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 6, decisions D-2 and D-9 — anonymous / whistleblower intake.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ These are the only two tables in the module that must not know who wrote their rows,
    /// and the guarantee is an ABSENCE — which means nothing in the schema defends it.</b> Read the
    /// three points below before altering either table.
    /// </para>
    /// <para>
    /// <b>1. There is no reporter column, and none may be added.</b> Not a nullable one, not a
    /// "just for support purposes" one. <c>TriagedById</c>, <c>ClosedById</c> and
    /// <c>ConvertedCaseId</c> are HR's own attributed actions and are supposed to be here: only the
    /// REPORTER is anonymous, the desk is accountable for what it does with a report.
    /// </para>
    /// <para>
    /// <b>2. <c>CreatedBy</c> and <c>CreatedById</c> DO exist on both tables — inherited from the
    /// base entity — and the guarantee is that they stay EMPTY, not that they are absent.</b> That
    /// holds today only because <c>ApplicationDbContext.UpdateAuditableEntities</c> stamps
    /// timestamps, the tenant and the soft-delete flag and nothing else. If anything ever starts
    /// stamping an actor centrally, this breaks <i>silently</i> and no test in the API harness would
    /// notice — which is why <c>dev-harness/hr-employee-relations/verify-slice6-anonymity.sql</c>
    /// checks the stored rows directly and must be run alongside the harness.
    /// </para>
    /// <para>
    /// <b>3. The retrieval code is never stored.</b> Only a PBKDF2-SHA256 hash (100,000 iterations)
    /// with a per-row salt. HR cannot look a code up or reissue one, and a reporter who loses theirs
    /// has lost their thread — the correct trade, because a recoverable code would have to be
    /// recoverable BY somebody, and that somebody could then read the thread.
    /// </para>
    /// <para>
    /// <b>Anonymous here means unattributed, not unauthenticated (D-9).</b> The endpoints require a
    /// valid internal token and simply never record who called. The honest limit, so nobody
    /// over-promises to staff: request logs and the reverse proxy still see the caller.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — two <c>CreateTable</c>, six foreign keys and nine
    /// indexes, with nothing inferred and nothing renamed. Rewritten as guarded SQL only to match
    /// the surrounding HR migrations, so it is safe to re-run against a database at either state —
    /// including one built from the EF model rather than from the chain, which is how this repo's
    /// <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeRelationsConcerns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeRelationsConcerns', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeRelationsConcerns] (
    [Id]                  uniqueidentifier NOT NULL,
    [ConcernNumber]       nvarchar(50)     NOT NULL,
    [Category]            int              NOT NULL,
    [Subject]             nvarchar(300)    NOT NULL,
    [Statement]           nvarchar(max)    NOT NULL,
    [Status]              int              NOT NULL,
    [ReportedAt]          datetime2        NOT NULL,
    -- PBKDF2-SHA256, 100k iterations, per-row salt. The code itself is never stored anywhere.
    [RetrievalCodeHash]   nvarchar(200)    NOT NULL,
    [RetrievalCodeSalt]   nvarchar(100)    NOT NULL,
    -- HR's side. Attributed on purpose: only the reporter is anonymous.
    [TriageNotes]         nvarchar(4000)   NULL,
    [TriagedAt]           datetime2        NULL,
    [TriagedById]         uniqueidentifier NULL,
    [ClosedAt]            datetime2        NULL,
    [ClosureReason]       nvarchar(1000)   NULL,
    [ClosedById]          uniqueidentifier NULL,
    [ConvertedCaseId]     uniqueidentifier NULL,
    [CreatedAt]           datetime2        NOT NULL,
    [UpdatedAt]           datetime2        NULL,
    -- ⚠ Present because the base entity has them. They MUST stay NULL on every row here; that
    -- emptiness is the anonymity guarantee. See the remarks and verify-slice6-anonymity.sql.
    [CreatedBy]           nvarchar(max)    NULL,
    [UpdatedBy]           nvarchar(max)    NULL,
    [CreatedById]         uniqueidentifier NULL,
    [LastModifiedById]    uniqueidentifier NULL,
    [IsDeleted]           bit              NOT NULL,
    [DeletedAt]           datetime2        NULL,
    [DeletedBy]           nvarchar(max)    NULL,
    [TenantId]            uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EmployeeRelationsConcerns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeRelationsConcerns_Employees_TriagedById] FOREIGN KEY ([TriagedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsConcerns_Employees_ClosedById] FOREIGN KEY ([ClosedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsConcerns_StaffGrievances_ConvertedCaseId] FOREIGN KEY ([ConvertedCaseId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsConcerns_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeRelationsConcernUpdates', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeRelationsConcernUpdates] (
    [Id]                uniqueidentifier NOT NULL,
    [ConcernId]         uniqueidentifier NOT NULL,
    -- ⚠ THIS is what marks a reporter's message — never the absence of an author, which would make
    -- that absence the tell.
    [IsFromReporter]    bit              NOT NULL,
    -- HR's messages only. NULL on every reporter message.
    [AuthorEmployeeId]  uniqueidentifier NULL,
    [Body]              nvarchar(4000)   NOT NULL,
    [PostedAt]          datetime2        NOT NULL,
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
    CONSTRAINT [PK_EmployeeRelationsConcernUpdates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeRelationsConcernUpdates_EmployeeRelationsConcerns_ConcernId]
        FOREIGN KEY ([ConcernId])
        REFERENCES [dbo].[EmployeeRelationsConcerns] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeRelationsConcernUpdates_Employees_AuthorEmployeeId] FOREIGN KEY ([AuthorEmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_EmployeeRelationsConcernUpdates_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, columns, unique) in new[]
            {
                ("IX_EmployeeRelationsConcerns_TenantId_ConcernNumber", "EmployeeRelationsConcerns", "[TenantId], [ConcernNumber]", true),
                ("IX_EmployeeRelationsConcerns_Status", "EmployeeRelationsConcerns", "[Status]", false),
                ("IX_EmployeeRelationsConcerns_Category", "EmployeeRelationsConcerns", "[Category]", false),
                ("IX_EmployeeRelationsConcerns_TriagedById", "EmployeeRelationsConcerns", "[TriagedById]", false),
                ("IX_EmployeeRelationsConcerns_ClosedById", "EmployeeRelationsConcerns", "[ClosedById]", false),
                ("IX_EmployeeRelationsConcerns_ConvertedCaseId", "EmployeeRelationsConcerns", "[ConvertedCaseId]", false),
                ("IX_EmployeeRelationsConcernUpdates_ConcernId", "EmployeeRelationsConcernUpdates", "[ConcernId]", false),
                ("IX_EmployeeRelationsConcernUpdates_AuthorEmployeeId", "EmployeeRelationsConcernUpdates", "[AuthorEmployeeId]", false),
                ("IX_EmployeeRelationsConcernUpdates_TenantId", "EmployeeRelationsConcernUpdates", "[TenantId]", false),
            })
            {
                var kind = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    {kind} [{name}] ON [dbo].[{table}] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This destroys every concern reported in confidence, and every thread on one — and
            // unlike the rest of this module there is no compensating record anywhere, because the
            // whole design point is that these reports exist in one place and are traceable to
            // nobody. A concern deleted here cannot be reconstructed from a case, a log or a person.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeRelationsConcernUpdates', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeRelationsConcernUpdates];

IF OBJECT_ID('dbo.EmployeeRelationsConcerns', 'U') IS NOT NULL
    DROP TABLE [dbo].[EmployeeRelationsConcerns];");
        }
    }
}
