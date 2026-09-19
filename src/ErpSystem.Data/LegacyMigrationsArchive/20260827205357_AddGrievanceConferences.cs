using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 4, decision D-8. Meetings convened on an employee-relations case — and with
    /// them FR-HR-181's <b>last outstanding obligation</b>, number 6: union consultation notes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One table for three kinds of meeting.</b> A case conference, a mediation and a union
    /// consultation are the same shape — convened on a date, at a place, chaired by somebody,
    /// attended by named people, producing notes and an outcome. What differs is why it was called,
    /// which is <c>ConferenceType</c>. Three tables would have been these columns three times.
    /// </para>
    /// <para>
    /// <b><c>Notes</c> is <c>nvarchar(max)</c>, and that is EF's doing rather than a choice.</b>
    /// 6,000 characters is 12,000 bytes, past <c>nvarchar</c>'s 4,000-character ceiling, so it
    /// becomes MAX and is stored off-row when it needs to be. That happens to relieve the row-size
    /// pressure that made slice 3's read fail — but the case file is read with
    /// <c>AsSplitQuery()</c> regardless, and nothing here should be taken as making that optional.
    /// </para>
    /// <para>
    /// <b><c>Notes</c> is also the one column in this module that is redacted on read</b>, to HR and
    /// the chair. It records what the other party said in a room they were promised was private, and
    /// the case's read rule admits the complainant — so without that, filing a grievance would be a
    /// route to the respondent's position verbatim. The rule lives in
    /// <c>StaffGrievanceService.MaySeeConferenceNotes</c>; nothing in the schema enforces it, and
    /// anything new that reads this column must apply it too.
    /// </para>
    /// <para>
    /// <b><c>DidAttend</c> is nullable on purpose.</b> Null means the meeting has not happened or
    /// attendance was never recorded; <c>false</c> means asked and did not come. Those are different
    /// facts, and the difference matters when a grievance turns on whether somebody was heard. A
    /// <c>NOT NULL DEFAULT 0</c> would quietly assert the second whenever the first is true.
    /// </para>
    /// <para>
    /// <b><c>StaffGrievanceDocuments.ConferenceId</c> is <c>NO ACTION</c></b>, like its
    /// <c>StepId</c> neighbour and for the same reason: the conference already cascades from the
    /// case, so cascading here would give <c>StaffGrievances</c> a second path into that table,
    /// which SQL Server refuses. Documents still go when the case does, by the direct leg.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — one <c>AddColumn</c>, two <c>CreateTable</c>, eleven
    /// indexes and one <c>AddForeignKey</c>, with nothing inferred and nothing renamed. Rewritten as
    /// guarded SQL only to match the surrounding HR migrations, so it is safe to re-run against a
    /// database at either state — including one built from the EF model rather than from the chain,
    /// which is how this repo's <c>rebuild-db</c> works.
    /// </para>
    /// </remarks>
    public partial class AddGrievanceConferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceConferences', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceConferences] (
    [Id]                         uniqueidentifier NOT NULL,
    [GrievanceId]                uniqueidentifier NOT NULL,
    [ConferenceType]             int              NOT NULL,
    [Status]                     int              NOT NULL,
    [ScheduledFor]               datetime2        NOT NULL,
    [Venue]                      nvarchar(300)    NULL,
    [ChairId]                    uniqueidentifier NULL,
    [ExternalChairName]          nvarchar(200)    NULL,
    [ExternalChairOrganisation]  nvarchar(200)    NULL,
    [UnionId]                    uniqueidentifier NULL,
    [Purpose]                    nvarchar(1000)   NULL,
    -- MAX because 6,000 characters exceeds nvarchar's ceiling. ⚠ Redacted on read — see remarks.
    [Notes]                      nvarchar(max)    NULL,
    [Outcome]                    nvarchar(4000)   NULL,
    [HeldDate]                   datetime2        NULL,
    [CancelledDate]              datetime2        NULL,
    [CancellationReason]         nvarchar(500)    NULL,
    [ConvenedById]               uniqueidentifier NULL,
    [CreatedAt]                  datetime2        NOT NULL,
    [UpdatedAt]                  datetime2        NULL,
    [CreatedBy]                  nvarchar(max)    NULL,
    [UpdatedBy]                  nvarchar(max)    NULL,
    [CreatedById]                uniqueidentifier NULL,
    [LastModifiedById]           uniqueidentifier NULL,
    [IsDeleted]                  bit              NOT NULL,
    [DeletedAt]                  datetime2        NULL,
    [DeletedBy]                  nvarchar(max)    NULL,
    [TenantId]                   uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StaffGrievanceConferences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceConferences_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StaffGrievanceConferences_Employees_ChairId] FOREIGN KEY ([ChairId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceConferences_Employees_ConvenedById] FOREIGN KEY ([ConvenedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceConferences_Unions_UnionId] FOREIGN KEY ([UnionId])
        REFERENCES [dbo].[Unions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceConferences_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceConferenceAttendees', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceConferenceAttendees] (
    [Id]                    uniqueidentifier NOT NULL,
    [ConferenceId]          uniqueidentifier NOT NULL,
    [EmployeeId]            uniqueidentifier NULL,
    [ExternalName]          nvarchar(200)    NULL,
    [ExternalOrganisation]  nvarchar(200)    NULL,
    [Capacity]              nvarchar(200)    NULL,
    -- ⚠ NULLABLE. Null = not recorded; 0 = asked and did not come. Different facts. See remarks.
    [DidAttend]             bit              NULL,
    [ApologyReason]         nvarchar(500)    NULL,
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
    CONSTRAINT [PK_StaffGrievanceConferenceAttendees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceConferenceAttendees_StaffGrievanceConferences_ConferenceId]
        FOREIGN KEY ([ConferenceId])
        REFERENCES [dbo].[StaffGrievanceConferences] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_StaffGrievanceConferenceAttendees_Employees_EmployeeId] FOREIGN KEY ([EmployeeId])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceConferenceAttendees_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Slice 3 promised this scope member and left the column out, because an FK to a table
            // that did not exist would have had nowhere to point.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffGrievanceDocuments', 'ConferenceId') IS NULL
    ALTER TABLE [dbo].[StaffGrievanceDocuments] ADD [ConferenceId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffGrievanceDocuments', 'ConferenceId') IS NOT NULL
   AND OBJECT_ID('dbo.StaffGrievanceConferences', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = 'FK_StaffGrievanceDocuments_StaffGrievanceConferences_ConferenceId')
    ALTER TABLE [dbo].[StaffGrievanceDocuments]
        ADD CONSTRAINT [FK_StaffGrievanceDocuments_StaffGrievanceConferences_ConferenceId]
        FOREIGN KEY ([ConferenceId])
        REFERENCES [dbo].[StaffGrievanceConferences] ([Id]) ON DELETE NO ACTION;");

            // Indexes are created separately from the tables so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the tables exist and these may not.
            foreach (var (name, table, columns) in new[]
            {
                ("IX_StaffGrievanceConferences_GrievanceId", "StaffGrievanceConferences", "[GrievanceId]"),
                ("IX_StaffGrievanceConferences_ConferenceType", "StaffGrievanceConferences", "[ConferenceType]"),
                ("IX_StaffGrievanceConferences_Status", "StaffGrievanceConferences", "[Status]"),
                // The slice-7 reminder sweep chases meetings by when they are due.
                ("IX_StaffGrievanceConferences_ScheduledFor", "StaffGrievanceConferences", "[ScheduledFor]"),
                ("IX_StaffGrievanceConferences_ChairId", "StaffGrievanceConferences", "[ChairId]"),
                ("IX_StaffGrievanceConferences_ConvenedById", "StaffGrievanceConferences", "[ConvenedById]"),
                ("IX_StaffGrievanceConferences_UnionId", "StaffGrievanceConferences", "[UnionId]"),
                ("IX_StaffGrievanceConferences_TenantId", "StaffGrievanceConferences", "[TenantId]"),
                ("IX_StaffGrievanceConferenceAttendees_ConferenceId", "StaffGrievanceConferenceAttendees", "[ConferenceId]"),
                ("IX_StaffGrievanceConferenceAttendees_EmployeeId", "StaffGrievanceConferenceAttendees", "[EmployeeId]"),
                ("IX_StaffGrievanceConferenceAttendees_TenantId", "StaffGrievanceConferenceAttendees", "[TenantId]"),
                ("IX_StaffGrievanceDocuments_ConferenceId", "StaffGrievanceDocuments", "[ConferenceId]"),
            })
            {
                migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{name}] ON [dbo].[{table}] ({columns});");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ This discards every meeting held on an employee-relations case: what was convened,
            // who was asked, who came, what was decided, and FR-HR-181's union consultation notes.
            // Nothing else in the schema keeps any of it, and the notes in particular exist nowhere
            // outside this table.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_StaffGrievanceDocuments_StaffGrievanceConferences_ConferenceId')
    ALTER TABLE [dbo].[StaffGrievanceDocuments]
        DROP CONSTRAINT [FK_StaffGrievanceDocuments_StaffGrievanceConferences_ConferenceId];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_StaffGrievanceDocuments_ConferenceId'
             AND object_id = OBJECT_ID('dbo.StaffGrievanceDocuments'))
    DROP INDEX [IX_StaffGrievanceDocuments_ConferenceId] ON [dbo].[StaffGrievanceDocuments];

IF COL_LENGTH('dbo.StaffGrievanceDocuments', 'ConferenceId') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievanceDocuments] DROP COLUMN [ConferenceId];");

            // Attendees first: they reference the meeting they were asked to.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceConferenceAttendees', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceConferenceAttendees];

IF OBJECT_ID('dbo.StaffGrievanceConferences', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceConferences];");
        }
    }
}
