using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// <c>SuccessionDocuments</c> and <c>NHISClaimDocuments</c> gain the three controlled-upload
    /// identifiers every other gated HR document table already carries: <c>FileUploadRecordId</c>,
    /// <c>DocumentRecordId</c> and <c>DocumentVersionId</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> Both families were still on a caller-supplied file location — the fourth and
    /// fifth instances of the sink <c>MedicalExpenseDocument</c>,
    /// <c>EmployeeMedicalExamDocument</c> and <c>MedicalInsuranceProviderDocument</c> were each
    /// fixed for (D-10). <c>CreateSuccessionDocumentDto.DocumentUrl</c> and
    /// <c>CreateNHISClaimDocumentDto.FilePath</c> were both <c>[Required]</c> and stored verbatim,
    /// and neither family had an upload route or a download route, so a row could only ever name a
    /// file the server had never received — unreadable even when the path was honest. These six
    /// nullable columns are what let the uploads go through the scanning + DMS boundary instead
    /// (D-14). One DTO serves all three succession doors, which is why one table change unblocks
    /// <c>succession-plans</c>, <c>succession-candidates</c> and <c>talent-pools</c> together.
    /// </para>
    /// <para>
    /// <b>The legacy path columns are deliberately left in place and non-nullable.</b> Existing
    /// rows keep whatever they hold and new rows leave them empty; the download helper prefers the
    /// DMS ids, falls back to a bare upload record, and only then reads the legacy string. Backfill
    /// is NOT done here — it has to stream each file through the virus scanner, which is the HR
    /// legacy-file migration utility's job, not a schema migration's.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds from the EF model rather than from the migration chain, so a database rebuilt after
    /// the entity change already has all six columns and a bare <c>AddColumn</c> fails with
    /// "Column names in each table must be unique in each table". Each step is guarded on
    /// <c>COL_LENGTH</c>, so the migration is a no-op against a database already in the target
    /// shape and still does the work on one that is not. Same rewrite, same reason, as
    /// <c>20260801224538_HrControlledDocumentLinks</c> and
    /// <c>20260829072853_AddMedicalInsuranceProviderDocumentDmsColumns</c>. The generated
    /// <c>.Designer.cs</c> target model is retained unchanged.
    /// </para>
    /// <para>
    /// <b>No composite index, unlike <c>HrControlledDocumentLinks</c>.</b> That migration added
    /// <c>IX_&lt;table&gt;_TenantId_FileUploadRecordId</c> to its six tables because the model
    /// configures one for each. Neither entity here does, so proposing one in the migration alone
    /// would leave the database permanently ahead of the model and make every future
    /// <c>migrations add</c> re-propose the drop. Both are small child tables read by parent id.
    /// </para>
    /// </remarks>
    public partial class AddSuccessionAndNhisDocumentDmsColumns : Migration
    {
        /// <summary>The three identifiers, all nullable — a legacy row has none of them.</summary>
        private static readonly string[] Columns =
        {
            "FileUploadRecordId",
            "DocumentRecordId",
            "DocumentVersionId",
        };

        private static readonly string[] Tables =
        {
            "SuccessionDocuments",
            "NHISClaimDocuments",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var column in Columns)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] uniqueidentifier NULL;");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                foreach (var column in Columns)
                {
                    migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];");
                }
            }
        }
    }
}
