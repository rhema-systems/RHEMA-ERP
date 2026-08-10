using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Links the three recruitment attachment tables to the shared controlled-upload boundary and
    /// the central DMS, finishing the job <c>20260801224538_HrControlledDocumentLinks</c> started
    /// for the other six HR document tables.
    /// </summary>
    /// <remarks>
    /// <para>Requisition, vacancy and posting attachments each accepted a caller-supplied
    /// <c>filePath</c> on a JSON endpoint: nothing was scanned, nothing was stored, and the row
    /// recorded a path to a file the server had never received. Routing them through
    /// <c>IControlledFileUploadService</c> and <c>ICentralDocumentRepositoryFileService</c> requires
    /// each owning row to remember which controlled upload and which DMS record/version backs it —
    /// that is all these columns are. <c>FileSizeBytes</c> comes along because the gate reports it
    /// and the listing shows it.</para>
    ///
    /// <para>Every added column is nullable and the existing <c>FilePath</c> column is left
    /// untouched, so pre-migration rows keep resolving: the download helper prefers the DMS ids,
    /// falls back to a bare upload record, and only then reads the legacy string. No backfill —
    /// there is nothing to back-fill, because those rows never referred to a stored file.</para>
    ///
    /// <para><b>No indexes.</b> The earlier migration added <c>IX_&lt;table&gt;_TenantId_FileUploadRecordId</c>
    /// composites and dropped the superseded single-column ones, but those came from index
    /// declarations in the EF model. These three entities declare plain properties, so the
    /// scaffolded body is twelve <c>AddColumn</c> calls and nothing else. Adding index SQL by hand
    /// would put the migration permanently out of step with the snapshot and make every future
    /// <c>migrations add</c> re-propose it.</para>
    ///
    /// <para>No foreign keys to <c>FileUploadRecords</c> or the DMS tables, for the reasons set out
    /// at length on <c>HrControlledDocumentLinks</c>: controlled uploads are soft-deleted,
    /// <c>FileUploadRecords</c> carries a delete-guard trigger, and this database is rebuilt from
    /// the EF model rather than the migration chain.</para>
    ///
    /// <para>The scaffolded body has been rewritten as guarded SQL, matching the defensive style of
    /// its predecessor, so this is safe on databases built by the migration chain, on databases
    /// built by <c>rebuild-db</c>/EnsureCreated where the model already contains all of this, and on
    /// a re-run. It is also safe on a dev box where the four <c>StaffRequisitionAttachments</c>
    /// columns were already applied by hand ahead of this migration — which is exactly the case the
    /// guards were needed for here. The generated <c>.Designer.cs</c> target model is retained
    /// unchanged.</para>
    /// </remarks>
    public partial class HrRecruitmentAttachmentDocumentLinks : Migration
    {
        /// <summary>Recruitment attachment tables gaining the upload + DMS link triple and a size.</summary>
        private static readonly string[] AttachmentTables =
        [
            "StaffRequisitionAttachments",
            "JobVacancyAttachments",
            "JobPostingAttachments"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AttachmentTables)
            {
                AddGuidColumnIfMissing(migrationBuilder, table, "FileUploadRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentVersionId");
                AddBigIntColumnIfMissing(migrationBuilder, table, "FileSizeBytes");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AttachmentTables)
            {
                DropColumnIfExists(migrationBuilder, table, "FileSizeBytes");
                DropColumnIfExists(migrationBuilder, table, "DocumentVersionId");
                DropColumnIfExists(migrationBuilder, table, "DocumentRecordId");
                DropColumnIfExists(migrationBuilder, table, "FileUploadRecordId");
            }
        }

        private static void AddGuidColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "uniqueidentifier NULL");

        private static void AddBigIntColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "bigint NULL");

        private static void AddColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};");
        }

        private static void DropColumnIfExists(
            MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
   AND COL_LENGTH(N'[dbo].[{table}]', N'{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
        }
    }
}
