using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 11 slice 3a — puts medical expense claim documents behind the controlled-upload gate.
    ///
    /// The claim-document endpoint accepted a caller-supplied FilePath, which let anyone with medical
    /// write access attach arbitrary bytes on disk — including another tenant's — to a claim. That is
    /// the same defect EmployeeMedicalExamDocument was fixed for, so this gives MedicalExpenseDocument
    /// the same three nullable columns that back a scanned, DMS-registered upload:
    /// FileUploadRecordId, DocumentRecordId, DocumentVersionId. All nullable, so rows written before
    /// the gate existed keep working off their legacy FilePath.
    ///
    /// IX_MedicalExpenseDocuments_TenantId is dropped in favour of the composite
    /// (TenantId, FileUploadRecordId), mirroring EmployeeMedicalExamDocument. Nothing is lost: a
    /// composite index serves TenantId-only lookups through its leftmost column, so the standalone
    /// one was redundant once the composite existed.
    ///
    /// The scaffolded operations are replaced with guarded SQL (repo convention): local dev DBs are
    /// built from the EF model by rebuild-db, so a database can already carry these columns and
    /// indexes without this migration being stamped — every operation checks before it acts. Each
    /// statement is its own Sql() call so the index is created in a later batch than the column it
    /// covers. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddMedicalClaimDocumentControlledUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'IX_MedicalExpenseDocuments_TenantId'
             AND object_id = OBJECT_ID(N'[MedicalExpenseDocuments]'))
    DROP INDEX [IX_MedicalExpenseDocuments_TenantId] ON [MedicalExpenseDocuments];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'FileUploadRecordId') IS NULL
    ALTER TABLE [MedicalExpenseDocuments] ADD [FileUploadRecordId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'DocumentRecordId') IS NULL
    ALTER TABLE [MedicalExpenseDocuments] ADD [DocumentRecordId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'DocumentVersionId') IS NULL
    ALTER TABLE [MedicalExpenseDocuments] ADD [DocumentVersionId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MedicalExpenseDocuments_TenantId_FileUploadRecordId'
                 AND object_id = OBJECT_ID(N'[MedicalExpenseDocuments]'))
    CREATE INDEX [IX_MedicalExpenseDocuments_TenantId_FileUploadRecordId]
        ON [MedicalExpenseDocuments] ([TenantId], [FileUploadRecordId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = N'IX_MedicalExpenseDocuments_TenantId_FileUploadRecordId'
             AND object_id = OBJECT_ID(N'[MedicalExpenseDocuments]'))
    DROP INDEX [IX_MedicalExpenseDocuments_TenantId_FileUploadRecordId] ON [MedicalExpenseDocuments];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'DocumentVersionId') IS NOT NULL
    ALTER TABLE [MedicalExpenseDocuments] DROP COLUMN [DocumentVersionId];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'DocumentRecordId') IS NOT NULL
    ALTER TABLE [MedicalExpenseDocuments] DROP COLUMN [DocumentRecordId];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MedicalExpenseDocuments]', N'FileUploadRecordId') IS NOT NULL
    ALTER TABLE [MedicalExpenseDocuments] DROP COLUMN [FileUploadRecordId];
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_MedicalExpenseDocuments_TenantId'
                 AND object_id = OBJECT_ID(N'[MedicalExpenseDocuments]'))
    CREATE INDEX [IX_MedicalExpenseDocuments_TenantId]
        ON [MedicalExpenseDocuments] ([TenantId]);
");
        }
    }
}
