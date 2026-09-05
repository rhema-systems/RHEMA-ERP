using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Payroll membership on the employee record (finish plan, lane 3f): not every employee is paid
    /// through the payroll run, so HR states which are (<c>IsOnPayroll</c>) and, for the rest, how
    /// they are paid instead (<c>OffPayrollReason</c>, an <c>OffPayrollReason</c> enum value, and a
    /// free-text <c>OffPayrollNote</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffold said <c>defaultValue: false</c>; the column defaults to 1.</b> EF scaffolds
    /// a <c>bool</c> column's default from the CLR default, not from the entity's initialiser
    /// (<c>= true</c>), and <c>false</c> here would take every existing employee off payroll in one
    /// statement — the salary block would vanish from every record and every emolument would read
    /// zero. "Paid through the run" is what an unset flag has always meant for the people on this
    /// table, so existing rows get 1.
    /// </para>
    /// <para>
    /// <b>The backfill is the one narrow exception:</b> a Consultant or Freelance employee with no
    /// salary on record and no payroll profile has never been in the run and is marked off payroll
    /// with the reason payroll would give (paid by invoice). Nobody else is touched; anyone the
    /// backfill gets wrong is corrected on the employee form, and the reconciliation read lists the
    /// disagreements either way. This is raw SQL, not <c>UpdateData</c>: the model-typed data
    /// operations resolve column types from the target model, which the fast EF build strips (the
    /// trap <c>20260901001749_AddEmployeeDocuments</c> records).
    /// </para>
    /// <para>
    /// <b>The scaffolded body was replaced with guarded SQL</b>, as in the five migrations before it:
    /// <c>rebuild-db</c> builds from the EF model, so a rebuilt database already has the three columns
    /// and a bare <c>AddColumn</c> fails. Every step is a no-op against a database already in the
    /// target shape — including the backfill, which is skipped unless this run added the column
    /// (a rebuilt database was populated by code that already writes the flag).
    /// </para>
    /// </remarks>
    public partial class AddEmployeePayrollMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @added bit = 0;

IF COL_LENGTH('dbo.Employees', 'IsOnPayroll') IS NULL
BEGIN
    ALTER TABLE [dbo].[Employees] ADD [IsOnPayroll] bit NOT NULL
        CONSTRAINT [DF_Employees_IsOnPayroll] DEFAULT (1);
    SET @added = 1;
END;

IF COL_LENGTH('dbo.Employees', 'OffPayrollReason') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [OffPayrollReason] int NULL;

IF COL_LENGTH('dbo.Employees', 'OffPayrollNote') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [OffPayrollNote] nvarchar(500) NULL;

-- Only when THIS run added the flag: on a rebuilt database the rows were written by code that
-- already sets it, and re-deriving it would overwrite HR's actual statements.
IF @added = 1
BEGIN
    -- EmploymentType 8 = Consultant, 9 = Freelance; OffPayrollReason 1 = PaidByInvoice.
    EXEC sp_executesql N'
        UPDATE e
        SET    e.IsOnPayroll = 0,
               e.OffPayrollReason = 1,
               e.OffPayrollNote = N''Set by migration: engaged as a consultant/freelancer with no salary on record and no payroll profile.''
        FROM   [dbo].[Employees] e
        WHERE  e.IsDeleted = 0
          AND  e.EmploymentType IN (8, 9)
          AND  (e.Salary IS NULL OR e.Salary <= 0)
          AND  NOT EXISTS (SELECT 1 FROM [dbo].[PayrollEmployeeProfiles] p
                           WHERE p.EmployeeId = e.Id AND p.IsDeleted = 0);';
END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'OffPayrollNote') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [OffPayrollNote];

IF COL_LENGTH('dbo.Employees', 'OffPayrollReason') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [OffPayrollReason];

IF COL_LENGTH('dbo.Employees', 'IsOnPayroll') IS NOT NULL
BEGIN
    IF OBJECT_ID('dbo.DF_Employees_IsOnPayroll', 'D') IS NOT NULL
        ALTER TABLE [dbo].[Employees] DROP CONSTRAINT [DF_Employees_IsOnPayroll];
    ALTER TABLE [dbo].[Employees] DROP COLUMN [IsOnPayroll];
END;");
        }
    }
}
