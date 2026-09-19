using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// <c>AwardAttachments</c> and <c>AwardNominationAttachments</c> gain the controlled-upload
    /// identifiers every other gated HR document table already carries — <c>FileUploadRecordId</c>,
    /// <c>DocumentRecordId</c>, <c>DocumentVersionId</c> — plus <c>FileSizeBytes</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The sixth instance of the caller-supplied file-location sink (ledger D-39), after
    /// the three medical families (D-10) and succession/NHIS (D-14).
    /// <c>CreateAwardAttachmentDto</c> and <c>CreateAwardNominationAttachmentDto</c> both took
    /// <c>FileName</c> and <c>FilePath</c> as JSON and stored them verbatim, so an "attachment" was
    /// a string somebody typed and the list rendered it beautifully. Neither family had an upload
    /// route or a download route, and <c>grep -ri attachment</c> over the awards screens returned
    /// nothing at all: the citation and the letter of support that make the CASE for an award have
    /// never been attachable. These columns are what let the uploads go through the scanning and
    /// DMS boundary instead.
    /// </para>
    /// <para>
    /// <b>The legacy path columns are deliberately left in place.</b> Existing rows keep whatever
    /// they hold; the download helper prefers the DMS ids, falls back to a bare upload record, and
    /// only then reads the legacy string. No backfill here — streaming each file through the virus
    /// scanner is the HR legacy-file migration utility's job, not a schema migration's. Measured
    /// before writing this: <b>both tables are empty on the DEFAULT tenant</b>, so there is nothing
    /// to convert in practice.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds from the EF model rather than from the migration chain, so a database rebuilt after
    /// the entity change already has all eight columns and a bare <c>AddColumn</c> fails with
    /// "Column names in each table must be unique in each table". Each step is guarded on
    /// <c>COL_LENGTH</c>, so this is a no-op against a database already in the target shape and
    /// still does the work on one that is not. Same rewrite, same reason, as
    /// <c>20260801224538_HrControlledDocumentLinks</c>,
    /// <c>20260829072853_AddMedicalInsuranceProviderDocumentDmsColumns</c> and
    /// <c>20260829215518_AddSuccessionAndNhisDocumentDmsColumns</c>. The generated
    /// <c>.Designer.cs</c> target model is retained unchanged.
    /// </para>
    /// <para>
    /// <b>No composite index.</b> <c>HrControlledDocumentLinks</c> added
    /// <c>IX_&lt;table&gt;_TenantId_FileUploadRecordId</c> to its six tables because the model
    /// configures one for each; neither entity here does, so proposing one in the migration alone
    /// would leave the database permanently ahead of the model and make every future
    /// <c>migrations add</c> re-propose the drop. Both are small child tables read by parent id.
    /// </para>
    /// </remarks>
    public partial class AddAwardAttachmentDmsColumns : Migration
    {
        private static readonly string[] Tables =
        {
            "AwardAttachments",
            "AwardNominationAttachments",
        };

        /// <summary>The gate's three identifiers, all nullable — a legacy row has none of them.</summary>
        private static readonly string[] GuidColumns =
        {
            "FileUploadRecordId",
            "DocumentRecordId",
            "DocumentVersionId",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var column in GuidColumns)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] uniqueidentifier NULL;");
                }

                // Nullable rather than defaulted to zero: an unknown size is not a size of nothing.
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'FileSizeBytes') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [FileSizeBytes] bigint NULL;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var column in GuidColumns)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
                }

                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', 'FileSizeBytes') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [FileSizeBytes];");
            }
        }
    }
}
