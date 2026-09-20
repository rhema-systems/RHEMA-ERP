using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 2. Closes three of FR-HR-181's four absent obligations — HR interpretation (5),
    /// the investigation report (7) and the resolution decision (8) — and gives
    /// <c>GrievanceStatus.Closed</c> the columns its first writer needs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why <c>StaffGrievanceResolutions</c> is a table and not three more columns.</b> Before this
    /// migration a resolution was <c>StaffGrievances.ResolutionSummary</c>, filled by
    /// <c>RespondAsync</c> with a verbatim copy of whatever the last responder typed. The system
    /// could not tell an ANSWER from a DECISION: no decider distinct from the responder, no decision
    /// date of its own, no rung it was taken at, no outcome, no remedy. FR-HR-181 names the
    /// "resolution decision" as a retained artefact, and an artefact with five attributes of its own
    /// is a row. <c>ResolutionSummary</c> survives as a denormalised mirror of
    /// <c>Decision</c> because the portal screen renders it.
    /// </para>
    /// <para>
    /// <b>Both unique indexes are FILTERED on the soft delete</b>
    /// (<c>WHERE [IsDeleted] = 0</c>), and that is not decoration. A soft delete does NOT release an
    /// unfiltered unique index — the lesson that cost five separate faces in area 13 — so an
    /// unfiltered index here would make "delete this investigation and open a correct one"
    /// permanently impossible, with a duplicate-key error that names neither the cause nor the fix.
    /// </para>
    /// <para>
    /// <b>Every Employee leg is <c>NO ACTION</c>.</b> Six of them across the two new tables and the
    /// two new columns on <c>StaffGrievances</c>; SQL Server refuses multiple cascade paths to one
    /// table, and nobody may be deleted out from under a case file in any event. Only the legs to
    /// the case itself cascade — an investigation report and a decision have no meaning apart from
    /// the case they belong to.
    /// </para>
    /// <para>
    /// <b>No back-fill, and none is possible.</b> Cases already resolved through the old shorthand
    /// have no resolution row: the information to build one — what was actually decided, as opposed
    /// to what the responder wrote — was never captured, and inventing it here would be worse than
    /// leaving the gap. New resolutions taken through that path get a row marked
    /// <c>Outcome = NotRecorded</c> (enum member 1), which the service can fill in later. Older ones
    /// keep <c>ResolutionSummary</c> and nothing more, which is exactly as much as was ever known.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — six <c>AddColumn</c>, two <c>CreateTable</c>, ten
    /// indexes (both filtered uniques present) and two <c>AddForeignKey</c>, with nothing inferred
    /// and nothing renamed. Rewritten as guarded SQL only to match the surrounding HR migrations,
    /// so it is safe to re-run against a database at either state — including one built from the EF
    /// model rather than from the chain, which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddGrievanceInterpretationInvestigationAndResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Obligation 5, and the closure stamps, on the case itself ──────────
            foreach (var (column, definition) in new[]
            {
                ("HrInterpretation", "nvarchar(4000) NULL"),
                ("HrInterpretationById", "uniqueidentifier NULL"),
                ("HrInterpretationDate", "datetime2 NULL"),
                ("ClosedDate", "datetime2 NULL"),
                ("ClosureReason", "nvarchar(1000) NULL"),
                ("ClosedById", "uniqueidentifier NULL"),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievances', '{column}') IS NULL
    ALTER TABLE [dbo].[StaffGrievances] ADD [{column}] {definition};");
            }

            // ── Obligation 7 — the investigation report ───────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceInvestigations', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceInvestigations] (
    [Id]                                uniqueidentifier NOT NULL,
    [GrievanceId]                       uniqueidentifier NOT NULL,
    [InvestigatorId]                    uniqueidentifier NULL,
    [ExternalInvestigatorName]          nvarchar(200)    NULL,
    [ExternalInvestigatorOrganisation]  nvarchar(200)    NULL,
    [StartedDate]                       datetime2        NOT NULL,
    [TargetDate]                        datetime2        NULL,
    [CompletedDate]                     datetime2        NULL,
    [Findings]                          nvarchar(4000)   NULL,
    [EvidenceCollected]                 nvarchar(4000)   NULL,
    [Recommendation]                    nvarchar(4000)   NULL,
    [OpenedById]                        uniqueidentifier NULL,
    [CreatedAt]                         datetime2        NOT NULL,
    [UpdatedAt]                         datetime2        NULL,
    [CreatedBy]                         nvarchar(max)    NULL,
    [UpdatedBy]                         nvarchar(max)    NULL,
    [CreatedById]                       uniqueidentifier NULL,
    [LastModifiedById]                  uniqueidentifier NULL,
    [IsDeleted]                         bit              NOT NULL,
    [DeletedAt]                         datetime2        NULL,
    [DeletedBy]                         nvarchar(max)    NULL,
    [TenantId]                          uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StaffGrievanceInvestigations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceInvestigations_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StaffGrievanceInvestigations_Employees_InvestigatorId] FOREIGN KEY ([InvestigatorId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceInvestigations_Employees_OpenedById] FOREIGN KEY ([OpenedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceInvestigations_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // ── Obligation 8 — the resolution decision ────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceResolutions', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceResolutions] (
    [Id]                    uniqueidentifier NOT NULL,
    [GrievanceId]           uniqueidentifier NOT NULL,
    [Outcome]               int              NOT NULL,
    [Decision]              nvarchar(4000)   NOT NULL,
    [RemedyOrUndertakings]  nvarchar(4000)   NULL,
    [DecidedById]           uniqueidentifier NOT NULL,
    [DecidedDate]           datetime2        NOT NULL,
    [DecidedAtLevel]        int              NOT NULL,
    [OutcomeRecordedDate]   datetime2        NULL,
    [OutcomeRecordedById]   uniqueidentifier NULL,
    [CreatedAt]             datetime2        NOT NULL,
    [UpdatedAt]             datetime2        NULL,
    [CreatedBy]             nvarchar(max)    NULL,
    [UpdatedBy]             nvarchar(max)    NULL,
    [CreatedById]           uniqueidentifier NULL,
    [LastModifiedById]      uniqueidentifier NULL,
    [IsDeleted]             bit              NOT NULL,
    [DeletedAt]             datetime2        NULL,
    [DeletedBy]             nvarchar(max)    NULL,
    [TenantId]              uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StaffGrievanceResolutions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceResolutions_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StaffGrievanceResolutions_Employees_DecidedById] FOREIGN KEY ([DecidedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceResolutions_Employees_OutcomeRecordedById] FOREIGN KEY ([OutcomeRecordedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceResolutions_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // The two attribution legs on the case itself, added after their columns exist.
            foreach (var (name, column) in new[]
            {
                ("FK_StaffGrievances_Employees_HrInterpretationById", "HrInterpretationById"),
                ("FK_StaffGrievances_Employees_ClosedById", "ClosedById"),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievances', '{column}') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{name}')
    ALTER TABLE [dbo].[StaffGrievances] ADD CONSTRAINT [{name}]
        FOREIGN KEY ([{column}]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");
            }

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            //
            // ⚠ The two UNIQUE ones are FILTERED. See the remarks: an unfiltered unique index is not
            // released by a soft delete, and would make replacing an investigation impossible.
            foreach (var (name, table, columns, unique) in new[]
            {
                ("IX_StaffGrievances_HrInterpretationById", "StaffGrievances", "[HrInterpretationById]", false),
                ("IX_StaffGrievances_ClosedById", "StaffGrievances", "[ClosedById]", false),
                ("IX_StaffGrievanceInvestigations_GrievanceId", "StaffGrievanceInvestigations", "[GrievanceId]", true),
                ("IX_StaffGrievanceInvestigations_InvestigatorId", "StaffGrievanceInvestigations", "[InvestigatorId]", false),
                ("IX_StaffGrievanceInvestigations_OpenedById", "StaffGrievanceInvestigations", "[OpenedById]", false),
                ("IX_StaffGrievanceInvestigations_TenantId", "StaffGrievanceInvestigations", "[TenantId]", false),
                ("IX_StaffGrievanceResolutions_GrievanceId", "StaffGrievanceResolutions", "[GrievanceId]", true),
                ("IX_StaffGrievanceResolutions_DecidedById", "StaffGrievanceResolutions", "[DecidedById]", false),
                ("IX_StaffGrievanceResolutions_OutcomeRecordedById", "StaffGrievanceResolutions", "[OutcomeRecordedById]", false),
                ("IX_StaffGrievanceResolutions_Outcome", "StaffGrievanceResolutions", "[Outcome]", false),
                ("IX_StaffGrievanceResolutions_TenantId", "StaffGrievanceResolutions", "[TenantId]", false),
            })
            {
                var kind = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
                var filter = unique ? " WHERE [IsDeleted] = 0" : string.Empty;
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    {kind} [{name}] ON [dbo].[{table}] ({columns}){filter};");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This discards every investigation report and every resolution decision — what was
            // investigated, what was found, what was decided, by whom and at which rung. FR-HR-181
            // requires those be retained, and nothing else in the schema keeps them: the most that
            // survives is ResolutionSummary, which is the free text this slice existed to stop
            // treating as a decision.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceInvestigations', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceInvestigations];

IF OBJECT_ID('dbo.StaffGrievanceResolutions', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceResolutions];");

            foreach (var name in new[]
            {
                "FK_StaffGrievances_Employees_HrInterpretationById",
                "FK_StaffGrievances_Employees_ClosedById",
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{name}')
    ALTER TABLE [dbo].[StaffGrievances] DROP CONSTRAINT [{name}];");
            }

            foreach (var name in new[]
            {
                "IX_StaffGrievances_HrInterpretationById",
                "IX_StaffGrievances_ClosedById",
            })
            {
                migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.StaffGrievances'))
    DROP INDEX [{name}] ON [dbo].[StaffGrievances];");
            }

            // Dropping ClosureReason also makes every case closed as unresolved indistinguishable
            // from one still under review — the rows survive and start reading as something they
            // are not, which is the same silent-corruption shape as slice 1's CaseType.
            foreach (var column in new[]
            {
                "HrInterpretation", "HrInterpretationById", "HrInterpretationDate",
                "ClosedDate", "ClosureReason", "ClosedById",
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievances', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievances] DROP COLUMN [{column}];");
            }
        }
    }
}
