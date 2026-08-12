using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Links pre-employment check evidence to the shared controlled-upload boundary and the central
    /// DMS, extending to recruitment's verification records what
    /// <c>20260810121459_HrRecruitmentAttachmentDocumentLinks</c> did for its attachment tables.
    /// </summary>
    /// <remarks>
    /// <para><c>PreEmploymentCheckItem.DocumentPath</c> and <c>ReferenceCheckResponse.DocumentPath</c>
    /// were caller-supplied strings set straight from a JSON payload: nothing was scanned, nothing
    /// was stored, and the row recorded a path to a file the server had never received. Routing them
    /// through <c>IControlledFileUploadService</c> and <c>ICentralDocumentRepositoryFileService</c>
    /// requires each owning row to remember which controlled upload and which DMS record/version
    /// backs it — that is all these columns are. <c>DocumentFileName</c> comes along so a download
    /// can be served under the name the file was uploaded with.</para>
    ///
    /// <para>These are the most sensitive documents recruitment holds — police clearances, medical
    /// reports, candid written references — which is why they get their own storage category
    /// (<c>hr-pre-employment-documents</c>) rather than sharing the candidate-document one, and why
    /// that category is in <c>SystemCleanScanRequired</c>.</para>
    ///
    /// <para>Every added column is nullable and the existing <c>DocumentPath</c> columns are left
    /// untouched, so pre-migration rows keep resolving: the download helper prefers the DMS ids,
    /// falls back to a bare upload record, and only then reads the legacy string. No backfill —
    /// there is nothing to back-fill, because those rows never referred to a stored file.</para>
    ///
    /// <para><b>No indexes and no foreign keys</b>, for the reasons set out on the two predecessors:
    /// these entities declare plain properties, so adding index SQL by hand would put the migration
    /// permanently out of step with the snapshot and make every future <c>migrations add</c>
    /// re-propose it; and controlled uploads are soft-deleted behind a delete-guard trigger while
    /// this database is rebuilt from the EF model rather than the migration chain.</para>
    ///
    /// <para>The scaffolded body has been rewritten as guarded SQL, matching its predecessors, so
    /// this is safe on databases built by the migration chain, on databases built by
    /// <c>rebuild-db</c>/EnsureCreated where the model already contains all of this, and on a re-run.
    /// The generated <c>.Designer.cs</c> target model is retained unchanged.</para>
    /// </remarks>
    public partial class HrPreEmploymentDocumentLinks : Migration
    {
        /// <summary>Pre-employment tables gaining the upload + DMS link triple and a file name.</summary>
        private static readonly string[] EvidenceTables =
        [
            "PreEmploymentCheckItems",
            "ReferenceCheckResponses"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in EvidenceTables)
            {
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentFileUploadRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentRecordId");
                AddGuidColumnIfMissing(migrationBuilder, table, "DocumentVersionId");
                AddFileNameColumnIfMissing(migrationBuilder, table, "DocumentFileName");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in EvidenceTables)
            {
                DropColumnIfExists(migrationBuilder, table, "DocumentFileName");
                DropColumnIfExists(migrationBuilder, table, "DocumentVersionId");
                DropColumnIfExists(migrationBuilder, table, "DocumentRecordId");
                DropColumnIfExists(migrationBuilder, table, "DocumentFileUploadRecordId");
            }
        }

        private static void AddGuidColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "uniqueidentifier NULL");

        private static void AddFileNameColumnIfMissing(
            MigrationBuilder migrationBuilder, string table, string column)
            => AddColumnIfMissing(migrationBuilder, table, column, "nvarchar(255) NULL");

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
