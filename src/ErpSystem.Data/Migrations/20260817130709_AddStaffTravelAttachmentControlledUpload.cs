using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR area 12 slice 1a — puts staff travel attachments behind the controlled-upload gate.
    ///
    /// The attachment endpoint accepted a caller-supplied FileUrl, which let anyone with travel
    /// write access point an attachment at arbitrary bytes on disk — including another tenant's.
    /// That is the same path-injection sink EmployeeMedicalExamDocument and MedicalExpenseDocument
    /// were both fixed for, and a travel attachment is typically a passport scan or a visa letter,
    /// so it is squarely in scope. This gives StaffTravelRequestAttachment the same three nullable
    /// columns that back a scanned, DMS-registered upload: FileUploadRecordId, DocumentRecordId,
    /// DocumentVersionId. All nullable, so rows written before the gate existed keep working off
    /// their legacy FileUrl.
    ///
    /// No index is added, unlike the medical equivalent. That one mirrored an existing composite on
    /// EmployeeMedicalExamDocument; here nothing queries attachments BY FileUploadRecordId — the
    /// download path resolves FileUploadRecords through its own primary key — so a composite index
    /// would carry cost for no read. IX_StaffTravelRequestAttachments_TenantId is therefore left
    /// alone.
    ///
    /// The scaffolded operations are replaced with guarded SQL (repo convention): local dev DBs are
    /// built from the EF model by rebuild-db, so a database can already carry these columns without
    /// this migration being stamped — every operation checks before it acts. Each statement is its
    /// own Sql() call. The generated Designer and the regenerated snapshot are kept as scaffolded.
    /// </summary>
    public partial class AddStaffTravelAttachmentControlledUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'FileUploadRecordId') IS NULL
    ALTER TABLE [StaffTravelRequestAttachments] ADD [FileUploadRecordId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'DocumentRecordId') IS NULL
    ALTER TABLE [StaffTravelRequestAttachments] ADD [DocumentRecordId] uniqueidentifier NULL;
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'DocumentVersionId') IS NULL
    ALTER TABLE [StaffTravelRequestAttachments] ADD [DocumentVersionId] uniqueidentifier NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'DocumentVersionId') IS NOT NULL
    ALTER TABLE [StaffTravelRequestAttachments] DROP COLUMN [DocumentVersionId];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'DocumentRecordId') IS NOT NULL
    ALTER TABLE [StaffTravelRequestAttachments] DROP COLUMN [DocumentRecordId];
");

            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StaffTravelRequestAttachments]', N'FileUploadRecordId') IS NOT NULL
    ALTER TABLE [StaffTravelRequestAttachments] DROP COLUMN [FileUploadRecordId];
");
        }
    }
}
