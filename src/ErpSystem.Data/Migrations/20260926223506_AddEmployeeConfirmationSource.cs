using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR finish plan lane 11: where an employee's confirmation date came from.
    /// </summary>
    /// <remarks>
    /// <para><b>One new column</b>, <c>Employees.ConfirmationSource</c> (nullable int —
    /// <c>ConfirmationSource</c>: Probation 1, Imported 2, Derived 3), so a confirmation date worked out
    /// from the hire date and the probation term can be told apart from one somebody supplied — the
    /// user's condition for deriving them at all (2026-09-25).</para>
    ///
    /// <para>⚠ <b>No data is written here.</b> The dates already on the table stay unlabelled: nobody
    /// recorded how they arrived, and a default would claim a provenance the data does not have (the
    /// same call as <c>ProbationSource</c>, lane D1). Confirming the imported workforce is the
    /// probation repair's job, run by an administrator after a dry run — never a side effect of
    /// starting the API. Guarded SQL, as on every HR migration.</para>
    /// </remarks>
    public partial class AddEmployeeConfirmationSource : Migration
    {
        private const string Employees = "Employees";
        private const string Column = "ConfirmationSource";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Employees}', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.{Employees}', '{Column}') IS NULL
    ALTER TABLE [dbo].[{Employees}] ADD [{Column}] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Loses which confirmation dates were derived: after a Down, a worked-out date and a
            // supplied one look the same again.
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.{Employees}', '{Column}') IS NOT NULL
    ALTER TABLE [dbo].[{Employees}] DROP COLUMN [{Column}];");
        }
    }
}
