using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 9c slice 3. Gives an employee-relations case a document surface for the first time, and
    /// closes FR-HR-181 obligation 9 — the final signed agreement, and the employee's acceptance
    /// of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Before this migration a grievance had no document surface at all.</b> Not a broken one:
    /// none. The complaint as filed on paper, the evidence an investigation gathered, and the signed
    /// agreement the requirement names explicitly all had nowhere to live, which is why obligation 9
    /// scored as absent in the slice-0 census.
    /// </para>
    /// <para>
    /// <b><c>FilePath</c> is a stored location and never a URL.</b> Every row here arrives through
    /// the controlled upload gate — scan, central-DMS registration, row write, rollback if that
    /// write fails — under the <c>hr-grievance-documents</c> category, which is scan-mandatory and
    /// cannot be opted out of by tenant policy. ⚠ The shape this exists to prevent is a create
    /// endpoint accepting <c>fileName</c> and <c>filePath</c> as JSON and storing no file at all;
    /// area 16 had to replace exactly that wholesale. The <c>FileUploadRecordId</c> /
    /// <c>DocumentRecordId</c> / <c>DocumentVersionId</c> triple is how a row proves it went
    /// through the gate rather than around it.
    /// </para>
    /// <para>
    /// <b><c>StepId</c> is <c>NO ACTION</c> while <c>GrievanceId</c> cascades, and that asymmetry is
    /// required rather than chosen.</b> A step already cascades from the case, so cascading the step
    /// leg too would give <c>StaffGrievances</c> two cascade paths into this one table, which SQL
    /// Server refuses outright. Documents still disappear with their case, by the direct leg.
    /// </para>
    /// <para>
    /// <b><c>AgreementSignedDate</c> is supplied, not stamped.</b> The signing happens in a room and
    /// the scan arrives afterwards, so <c>UtcNow</c> would record when somebody got round to
    /// uploading it — which is not what FR-HR-181 asks to be retained.
    /// <c>AgreementAcceptanceComment</c> is its own column for a related reason: appending the
    /// employee's remark to <c>RemedyOrUndertakings</c> would edit part of the frozen decision to
    /// hold something said after it was taken.
    /// </para>
    /// <para>
    /// The scaffold was correct as generated — four <c>AddColumn</c>, one <c>CreateTable</c>, six
    /// indexes and one <c>AddForeignKey</c>, with <c>StepId</c> already <c>Restrict</c>, nothing
    /// inferred and nothing renamed. Rewritten as guarded SQL only to match the surrounding HR
    /// migrations, so it is safe to re-run against a database at either state — including one built
    /// from the EF model rather than from the chain, which is how this repo's <c>rebuild-db</c>
    /// works.
    /// </para>
    /// </remarks>
    public partial class AddGrievanceDocumentsAndSignedAgreement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Obligation 9's stamps, on the resolution ──────────────────────────
            foreach (var (column, definition) in new[]
            {
                ("AgreementSignedDate", "datetime2 NULL"),
                ("AgreementAcceptedDate", "datetime2 NULL"),
                ("AgreementAcceptedById", "uniqueidentifier NULL"),
                ("AgreementAcceptanceComment", "nvarchar(1000) NULL"),
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievanceResolutions', '{column}') IS NULL
    ALTER TABLE [dbo].[StaffGrievanceResolutions] ADD [{column}] {definition};");
            }

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.StaffGrievanceResolutions', 'AgreementAcceptedById') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                   WHERE name = 'FK_StaffGrievanceResolutions_Employees_AgreementAcceptedById')
    ALTER TABLE [dbo].[StaffGrievanceResolutions]
        ADD CONSTRAINT [FK_StaffGrievanceResolutions_Employees_AgreementAcceptedById]
        FOREIGN KEY ([AgreementAcceptedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            // ── The case's paperwork ──────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceDocuments', 'U') IS NULL
CREATE TABLE [dbo].[StaffGrievanceDocuments] (
    [Id]                  uniqueidentifier NOT NULL,
    [GrievanceId]         uniqueidentifier NOT NULL,
    [Scope]               int              NOT NULL,
    [StepId]              uniqueidentifier NULL,
    [FileName]            nvarchar(500)    NOT NULL,
    [FilePath]            nvarchar(1000)   NOT NULL,
    [FileSize]            bigint           NOT NULL,
    [FileUploadRecordId]  uniqueidentifier NULL,
    [DocumentRecordId]    uniqueidentifier NULL,
    [DocumentVersionId]   uniqueidentifier NULL,
    [Description]         nvarchar(500)    NULL,
    [UploadDate]          datetime2        NOT NULL,
    [UploadedById]        uniqueidentifier NOT NULL,
    [CreatedAt]           datetime2        NOT NULL,
    [UpdatedAt]           datetime2        NULL,
    [CreatedBy]           nvarchar(max)    NULL,
    [UpdatedBy]           nvarchar(max)    NULL,
    [CreatedById]         uniqueidentifier NULL,
    [LastModifiedById]    uniqueidentifier NULL,
    [IsDeleted]           bit              NOT NULL,
    [DeletedAt]           datetime2        NULL,
    [DeletedBy]           nvarchar(max)    NULL,
    [TenantId]            uniqueidentifier NOT NULL,
    CONSTRAINT [PK_StaffGrievanceDocuments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StaffGrievanceDocuments_StaffGrievances_GrievanceId] FOREIGN KEY ([GrievanceId])
        REFERENCES [dbo].[StaffGrievances] ([Id]) ON DELETE CASCADE,
    -- NO ACTION, not CASCADE: the step already cascades from the case, and two cascade paths from
    -- StaffGrievances into this table is what SQL Server refuses. See the remarks.
    CONSTRAINT [FK_StaffGrievanceDocuments_StaffGrievanceSteps_StepId] FOREIGN KEY ([StepId])
        REFERENCES [dbo].[StaffGrievanceSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceDocuments_Employees_UploadedById] FOREIGN KEY ([UploadedById])
        REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_StaffGrievanceDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId])
        REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
);");

            // Indexes are created separately from the table so this migration is still correct
            // against a database whose tables were built from the EF model rather than from the
            // chain — the `rebuild-db` case, where the table exists and these may not.
            foreach (var (name, table, columns) in new[]
            {
                ("IX_StaffGrievanceResolutions_AgreementAcceptedById", "StaffGrievanceResolutions", "[AgreementAcceptedById]"),
                ("IX_StaffGrievanceDocuments_GrievanceId", "StaffGrievanceDocuments", "[GrievanceId]"),
                ("IX_StaffGrievanceDocuments_Scope", "StaffGrievanceDocuments", "[Scope]"),
                ("IX_StaffGrievanceDocuments_StepId", "StaffGrievanceDocuments", "[StepId]"),
                ("IX_StaffGrievanceDocuments_UploadedById", "StaffGrievanceDocuments", "[UploadedById]"),
                ("IX_StaffGrievanceDocuments_TenantId", "StaffGrievanceDocuments", "[TenantId]"),
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
            // ⚠ This drops the ROWS, not the files. The documents themselves live outside the web
            // root and stay in the central DMS, so what is destroyed here is the link between a case
            // and its paperwork — including which signed agreement settled it. FR-HR-181 requires
            // that agreement be retained; nothing else in the schema records which file it was.
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.StaffGrievanceDocuments', 'U') IS NOT NULL
    DROP TABLE [dbo].[StaffGrievanceDocuments];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = 'FK_StaffGrievanceResolutions_Employees_AgreementAcceptedById')
    ALTER TABLE [dbo].[StaffGrievanceResolutions]
        DROP CONSTRAINT [FK_StaffGrievanceResolutions_Employees_AgreementAcceptedById];

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_StaffGrievanceResolutions_AgreementAcceptedById'
             AND object_id = OBJECT_ID('dbo.StaffGrievanceResolutions'))
    DROP INDEX [IX_StaffGrievanceResolutions_AgreementAcceptedById] ON [dbo].[StaffGrievanceResolutions];");

            foreach (var column in new[]
            {
                "AgreementSignedDate", "AgreementAcceptedDate",
                "AgreementAcceptedById", "AgreementAcceptanceComment",
            })
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.StaffGrievanceResolutions', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[StaffGrievanceResolutions] DROP COLUMN [{column}];");
            }
        }
    }
}
