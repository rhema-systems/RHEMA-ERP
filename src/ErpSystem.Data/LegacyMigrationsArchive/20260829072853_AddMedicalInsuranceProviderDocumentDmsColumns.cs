using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// <c>MedicalInsuranceProviderDocuments</c> gains the three controlled-upload identifiers every
    /// other gated HR document table already carries: <c>FileUploadRecordId</c>,
    /// <c>DocumentRecordId</c> and <c>DocumentVersionId</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why.</b> The only way to create a provider document was <c>POST provider-documents</c>
    /// with a caller-supplied <c>FilePath</c>, stored verbatim — a path-injection sink, and the
    /// third instance of that defect in the medical module after <c>MedicalExpenseDocument</c> and
    /// <c>EmployeeMedicalExamDocument</c> were both fixed for it. There was no upload endpoint and
    /// no download route either, so a row was unreadable even when the path was honest. These three
    /// nullable columns are what let the upload go through the scanning + DMS boundary instead.
    /// <c>FilePath</c> is deliberately left in place and non-nullable: existing rows keep whatever
    /// they hold, and new rows leave it empty.
    /// </para>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced because it was not idempotent.</b> <c>rebuild-db</c>
    /// builds from the EF model rather than from the migration chain, so a database rebuilt after
    /// the entity change already has all three columns and a bare <c>AddColumn</c> fails with
    /// "Column names in each table must be unique". Each step is guarded on <c>COL_LENGTH</c>, so
    /// the migration is a no-op against a database that is already in the target shape and still
    /// does the work on one that is not.
    /// </para>
    /// </remarks>
    public partial class AddMedicalInsuranceProviderDocumentDmsColumns : Migration
    {
        /// <summary>The three identifiers, all nullable — a legacy row has none of them.</summary>
        private static readonly string[] Columns =
        {
            "FileUploadRecordId",
            "DocumentRecordId",
            "DocumentVersionId",
        };

        private const string Table = "MedicalInsuranceProviderDocuments";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{Table}] ADD [{column}] uniqueidentifier NULL;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{Table}] DROP COLUMN [{column}];");
            }
        }
    }
}
