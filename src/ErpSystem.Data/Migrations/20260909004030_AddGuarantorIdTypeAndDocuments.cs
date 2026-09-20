using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Demo feedback round 2, lane A (docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 5): the guarantor's
    /// national-ID kind as a foreign key to the identification-type catalogue (E-13), and a
    /// documents table for the papers that pertain to a guarantor — the signed form, an ID scan,
    /// a payslip — through the controlled upload gate (E-12).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> — the same rewrite as
    /// <c>20260907003611_AddSheChecklistBuilder</c>. <c>rebuild-db</c> builds from the EF model rather
    /// than the migration chain, so a rebuilt database already has the column, the table, the
    /// indexes and the foreign keys below, and a bare <c>CreateTable</c> / <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// <b>No data step.</b> The free-text <c>NationalIdType</c> column stays beside the new key
    /// (the lane-3b idiom for the certifying body): existing rows hold text nobody has mapped, and
    /// guessing a catalogue row from it would be wrong more often than it was right. The screen
    /// offers the lookup and shows the text only where the key is null.
    /// <c>GuarantorFormPath</c> is untouched — read-only from here on, never written.
    /// </para>
    /// <para>
    /// <b>NO ACTION on every foreign key except the guarantor's own</b>, as the scaffold chose: the
    /// context defaults relationships to Restrict, and the one cascade (document → guarantor) is
    /// the model's deliberate choice, since a guarantor's papers have no meaning without them.
    /// </para>
    /// </remarks>
    public partial class AddGuarantorIdTypeAndDocuments : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df IS NOT NULL EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The guarantor's national-ID kind, from the catalogue ──
            migrationBuilder.Sql(AddColumn("EmployeeGuarantors", "NationalIdTypeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantors", "IX_EmployeeGuarantors_NationalIdTypeId", "[NationalIdTypeId]"));
            migrationBuilder.Sql(AddForeignKey("EmployeeGuarantors", "FK_EmployeeGuarantors_IdentificationTypes_NationalIdTypeId", "NationalIdTypeId", "IdentificationTypes"));

            // ── 2. Documents that pertain to a guarantor ──
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.EmployeeGuarantorDocuments', 'U') IS NULL
CREATE TABLE [dbo].[EmployeeGuarantorDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [GuarantorId] uniqueidentifier NOT NULL,
    [DocumentTypeId] uniqueidentifier NOT NULL,
    [Title] nvarchar(250) NULL,
    [Description] nvarchar(1000) NULL,
    [IssuedOn] date NULL,
    [ExpiresOn] date NULL,
    [FileUploadRecordId] uniqueidentifier NULL,
    [DocumentRecordId] uniqueidentifier NULL,
    [DocumentVersionId] uniqueidentifier NULL,
    [FileName] nvarchar(255) NULL,
    [MimeType] nvarchar(150) NULL,
    [FileSizeBytes] bigint NULL,
    [UploadedById] uniqueidentifier NULL,
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
    CONSTRAINT [PK_EmployeeGuarantorDocuments] PRIMARY KEY ([Id])
);");

            migrationBuilder.Sql(CreateIndex("EmployeeGuarantorDocuments", "IX_EmployeeGuarantorDocuments_DocumentTypeId", "[DocumentTypeId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantorDocuments", "IX_EmployeeGuarantorDocuments_GuarantorId", "[GuarantorId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantorDocuments", "IX_EmployeeGuarantorDocuments_TenantId_ExpiresOn", "[TenantId], [ExpiresOn]"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantorDocuments", "IX_EmployeeGuarantorDocuments_TenantId_GuarantorId", "[TenantId], [GuarantorId]"));
            migrationBuilder.Sql(CreateIndex("EmployeeGuarantorDocuments", "IX_EmployeeGuarantorDocuments_UploadedById", "[UploadedById]"));

            migrationBuilder.Sql(AddForeignKey("EmployeeGuarantorDocuments", "FK_EmployeeGuarantorDocuments_EmployeeDocumentTypes_DocumentTypeId", "DocumentTypeId", "EmployeeDocumentTypes"));
            migrationBuilder.Sql(AddForeignKey("EmployeeGuarantorDocuments", "FK_EmployeeGuarantorDocuments_EmployeeGuarantors_GuarantorId", "GuarantorId", "EmployeeGuarantors", onDelete: "CASCADE"));
            migrationBuilder.Sql(AddForeignKey("EmployeeGuarantorDocuments", "FK_EmployeeGuarantorDocuments_Employees_UploadedById", "UploadedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey("EmployeeGuarantorDocuments", "FK_EmployeeGuarantorDocuments_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible for the schema; what is lost is every guarantor document row (the files
            // themselves stay in the store) and which catalogue kind each guarantor's ID was.
            migrationBuilder.Sql("IF OBJECT_ID('dbo.EmployeeGuarantorDocuments', 'U') IS NOT NULL DROP TABLE [dbo].[EmployeeGuarantorDocuments];");

            migrationBuilder.Sql(DropForeignKey("EmployeeGuarantors", "FK_EmployeeGuarantors_IdentificationTypes_NationalIdTypeId"));
            migrationBuilder.Sql(DropIndex("EmployeeGuarantors", "IX_EmployeeGuarantors_NationalIdTypeId"));
            migrationBuilder.Sql(DropColumn("EmployeeGuarantors", "NationalIdTypeId"));
        }
    }
}
