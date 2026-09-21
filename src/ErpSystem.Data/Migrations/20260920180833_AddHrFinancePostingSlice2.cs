using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// HR finish plan lane 8, slice 2 (employee payables): the per-event settlement route on a
    /// posting rule, and the amount actually paid on an employee award.
    /// </summary>
    /// <remarks>
    /// <para><b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration
    /// since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has these columns and a
    /// bare <c>AddColumn</c> stops the chain.</para>
    ///
    /// <para><b>Both columns are nullable on purpose.</b> A null <c>SettlementRoute</c> means "the
    /// catalogue's default for the event" — leave and long-service default to payroll, award and
    /// benefit payments to direct — so an existing rule row keeps behaving exactly as the catalogue
    /// says until an administrator chooses. A null <c>AmountPaid</c> on an award paid before this
    /// slice means "the conferred value", which is what the payment code assumed then.</para>
    /// </remarks>
    public partial class AddHrFinancePostingSlice2 : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn("HrFinancePostingRules", "SettlementRoute", "int NULL"));
            migrationBuilder.Sql(AddColumn("EmployeeAwards", "AmountPaid", "decimal(18,2) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn("EmployeeAwards", "AmountPaid"));
            migrationBuilder.Sql(DropColumn("HrFinancePostingRules", "SettlementRoute"));
        }
    }
}
