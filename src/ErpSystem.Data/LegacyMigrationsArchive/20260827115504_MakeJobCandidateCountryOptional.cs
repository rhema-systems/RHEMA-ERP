using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Area 25 slice 13b. Makes <c>JobCandidates.CountryId</c> nullable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This one column made the internal job board unusable.</b> Applying for an internal
    /// vacancy mints a shadow <c>JobCandidate</c> from the employee's record, and this FK was
    /// required while <c>Employees.CountryId</c> is optional — so writing the shadow row would
    /// have taken an FK 547 on <c>Guid.Empty</c>. Both internal write paths therefore refused
    /// outright, with "This employee has no country set on their profile, which is required to
    /// apply. Please complete the employee record first."
    /// </para>
    /// <para>
    /// Measured on DEFAULT 2026-08-27: <b>8,072 of 8,077</b> live employees have no country. So
    /// the board refused 99.94% of the workforce — and told them to correct a field they cannot
    /// correct, because country is one of the HR-approved fields in the slice-12a change-request
    /// set (<c>EmployeeProfileField.CountryId</c>), not one of the contact fields an employee
    /// edits directly. The advice was as unavailable as the feature.
    /// </para>
    /// <para>
    /// <b>The country was a requirement the foreign key invented, not one the business asked
    /// for</b>, so the key gives way rather than the feature. It stays REQUIRED on the external
    /// path: the public application form asks the candidate directly and they can answer, which
    /// is exactly the asymmetry — an external applicant knows their own country, while an
    /// internal one is being asked for a field on a personnel record they do not control.
    /// </para>
    /// <para>
    /// Nothing needs backfilling: widening a column to nullable leaves every existing value in
    /// place, and no existing candidate row is affected. Written as guarded SQL like the rest of
    /// the HR chain so it is safe to re-run against a database at either state — including one
    /// built from the EF model rather than from the chain, which is how this repo's
    /// <c>rebuild-db</c> works. The scaffold was correct as generated; only the guard is added.
    /// </para>
    /// <para>
    /// SQL Server allows <c>ALTER COLUMN</c> to widen NOT NULL → NULL with the foreign key in
    /// place, so the constraint is neither dropped nor recreated here.
    /// </para>
    /// </remarks>
    public partial class MakeJobCandidateCountryOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JobCandidates'
      AND COLUMN_NAME = 'CountryId'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    ALTER TABLE [JobCandidates] ALTER COLUMN [CountryId] uniqueidentifier NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Narrowing back is only safe once every row has a country. Rows written while the
            // column was optional — internal applicants — legitimately have none, so this stamps
            // them with Guid.Empty exactly as the scaffolded Down would have. That value does not
            // satisfy the foreign key, which is why the down path drops and does not restore it:
            // reversing this migration on a database that has taken internal applications is a
            // deliberate act, not a routine one.
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'JobCandidates'
      AND COLUMN_NAME = 'CountryId'
      AND IS_NULLABLE = 'YES'
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_JobCandidates_Countries_CountryId')
        ALTER TABLE [JobCandidates] DROP CONSTRAINT [FK_JobCandidates_Countries_CountryId];

    UPDATE [JobCandidates] SET [CountryId] = '00000000-0000-0000-0000-000000000000'
    WHERE [CountryId] IS NULL;

    ALTER TABLE [JobCandidates] ALTER COLUMN [CountryId] uniqueidentifier NOT NULL;
END
");
        }
    }
}
