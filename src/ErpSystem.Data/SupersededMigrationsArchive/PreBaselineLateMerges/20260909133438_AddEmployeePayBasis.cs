using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Demo feedback round 2, lane E1 (docs/HR/programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 6.5.2): how an
    /// employee's basic pay is arrived at — the salary scale, or an amount agreed for the person —
    /// and the note that says why when it is the latter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as for every migration in this
    /// round: <c>rebuild-db</c> builds from the EF model, so a rebuilt database already has the
    /// columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    /// <para>
    /// <b>⚠ EF scaffolded <c>defaultValue: 0</c>, and 0 is not a member of <c>PayBasis</c>.</b>
    /// It reads the CLR default of a non-nullable int, not the property's
    /// <c>= PayBasis.SalaryScale</c> initialiser — the same trap that would have retired every
    /// contract kind in D1. Left as scaffolded, every existing employee would carry a pay basis no
    /// enum member names, and the reconciliation's mismatch rule (<c>PayBasis == SalaryScale</c>)
    /// would be false for all of them. It is <c>DEFAULT (1)</c> below — the scale, which is what
    /// every existing row was implicitly: a placement on a grade was the only pay basis HR could
    /// record before this lane.
    /// </para>
    /// </remarks>
    public partial class AddEmployeePayBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'PayBasis') IS NULL
    ALTER TABLE [dbo].[Employees]
        ADD [PayBasis] int NOT NULL
        CONSTRAINT [DF_Employees_PayBasis] DEFAULT (1);");

            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'PayBasisNote') IS NULL
    ALTER TABLE [dbo].[Employees] ADD [PayBasisNote] nvarchar(500) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Employees', 'PayBasisNote') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [PayBasisNote];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Employees_PayBasis')
    ALTER TABLE [dbo].[Employees] DROP CONSTRAINT [DF_Employees_PayBasis];
IF COL_LENGTH('dbo.Employees', 'PayBasis') IS NOT NULL
    ALTER TABLE [dbo].[Employees] DROP COLUMN [PayBasis];");
        }
    }
}
