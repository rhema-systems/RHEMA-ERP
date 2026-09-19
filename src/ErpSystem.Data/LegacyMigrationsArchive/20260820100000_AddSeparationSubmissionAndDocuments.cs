using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Submission tracking on a separation, and the file it carries through its lifecycle
    /// (area 9b slice 2).
    /// </summary>
    /// <remarks>
    /// <para><c>SubmittedOn</c> / <c>SubmittedById</c> record the moment a separation leaves Draft
    /// and enters the FR-HR-092 approval queue — the point from which its notice facts stop being
    /// editable, and therefore the point the FR-HR-184 settlement can rely on them.</para>
    ///
    /// <para><c>EmployeeSeparationDocuments</c> is one table for the whole lifecycle rather than a
    /// path column per stage: a resignation letter now, a signed clearance form in slice 3, a
    /// settlement statement in slice 5, an exit interview record in slice 8. The alternative ends
    /// as a wide row of nullable paths that cannot hold two versions of anything and cannot say who
    /// attached what. Every file arrives through the controlled-upload gate under the new
    /// <c>hr-separation-documents</c> category, which is in the always-scan set.</para>
    ///
    /// <para>Scaffolded by the user, then rewritten here into guarded SQL so it is safe on a
    /// database built from the EF model as well as one built by replaying migrations. Discovery
    /// attributes stay in the generated <c>.Designer.cs</c>; this migration is listed in
    /// <c>FastBuildMigrationMetadata.cs</c> because fast Debug builds strip that designer.</para>
    /// </remarks>
    public partial class AddSeparationSubmissionAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SubmittedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [SubmittedById] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SubmittedOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] ADD [SubmittedOn] datetime2 NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_SubmittedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    CREATE INDEX [IX_EmployeeSeparations_SubmittedById] ON [dbo].[EmployeeSeparations] ([SubmittedById]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_SubmittedById')
    ALTER TABLE [dbo].[EmployeeSeparations] ADD CONSTRAINT [FK_EmployeeSeparations_Employees_SubmittedById]
        FOREIGN KEY ([SubmittedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
BEGIN
    CREATE TABLE [dbo].[EmployeeSeparationDocuments] (
        [Id]                    uniqueidentifier NOT NULL,
        [SeparationId]          uniqueidentifier NOT NULL,
        [Category]              int              NOT NULL,
        [FileName]              nvarchar(500)    NOT NULL,
        [FilePath]              nvarchar(1000)   NOT NULL,
        [FileUploadRecordId]    uniqueidentifier NULL,
        [DocumentRecordId]      uniqueidentifier NULL,
        [DocumentVersionId]     uniqueidentifier NULL,
        [Description]           nvarchar(1000)   NULL,
        [UploadedOn]            datetime2        NOT NULL,
        [UploadedById]          uniqueidentifier NULL,
        [CreatedAt]             datetime2        NOT NULL,
        [UpdatedAt]             datetime2        NULL,
        [CreatedBy]             nvarchar(max)    NULL,
        [UpdatedBy]             nvarchar(max)    NULL,
        [CreatedById]           uniqueidentifier NULL,
        [LastModifiedById]      uniqueidentifier NULL,
        [IsDeleted]             bit              NOT NULL CONSTRAINT [DF_EmployeeSeparationDocuments_IsDeleted] DEFAULT (0),
        [DeletedAt]             datetime2        NULL,
        [DeletedBy]             nvarchar(max)    NULL,
        [TenantId]              uniqueidentifier NOT NULL,
        CONSTRAINT [PK_EmployeeSeparationDocuments] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparationDocuments_EmployeeSeparations_SeparationId')
    ALTER TABLE [dbo].[EmployeeSeparationDocuments] ADD CONSTRAINT [FK_EmployeeSeparationDocuments_EmployeeSeparations_SeparationId]
        FOREIGN KEY ([SeparationId]) REFERENCES [dbo].[EmployeeSeparations] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparationDocuments_Employees_UploadedById')
    ALTER TABLE [dbo].[EmployeeSeparationDocuments] ADD CONSTRAINT [FK_EmployeeSeparationDocuments_Employees_UploadedById]
        FOREIGN KEY ([UploadedById]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparationDocuments_Tenants_TenantId')
    ALTER TABLE [dbo].[EmployeeSeparationDocuments] ADD CONSTRAINT [FK_EmployeeSeparationDocuments_Tenants_TenantId]
        FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparationDocument_Category' AND object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
    CREATE INDEX [IX_EmployeeSeparationDocument_Category] ON [dbo].[EmployeeSeparationDocuments] ([Category]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparationDocument_SeparationId' AND object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
    CREATE INDEX [IX_EmployeeSeparationDocument_SeparationId] ON [dbo].[EmployeeSeparationDocuments] ([SeparationId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparationDocuments_TenantId' AND object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
    CREATE INDEX [IX_EmployeeSeparationDocuments_TenantId] ON [dbo].[EmployeeSeparationDocuments] ([TenantId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparationDocuments_UploadedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
    CREATE INDEX [IX_EmployeeSeparationDocuments_UploadedById] ON [dbo].[EmployeeSeparationDocuments] ([UploadedById]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.EmployeeSeparationDocuments'))
    DROP TABLE [dbo].[EmployeeSeparationDocuments];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeSeparations_Employees_SubmittedById')
    ALTER TABLE [dbo].[EmployeeSeparations] DROP CONSTRAINT [FK_EmployeeSeparations_Employees_SubmittedById];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeSeparations_SubmittedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    DROP INDEX [IX_EmployeeSeparations_SubmittedById] ON [dbo].[EmployeeSeparations];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SubmittedOn' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [SubmittedOn];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'SubmittedById' AND object_id = OBJECT_ID('dbo.EmployeeSeparations'))
    ALTER TABLE [dbo].[EmployeeSeparations] DROP COLUMN [SubmittedById];");
        }
    }
}
