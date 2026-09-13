using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane P3 (register row E-4; decision D-5): the three leave columns leave the contract —
    /// <c>AnnualLeaveEntitlementDays</c>, <c>VacationDaysPerYear</c>, <c>SickDaysPerYear</c> on
    /// <c>EmployeeContractDetails</c>. Leave entitlement is the leave module's (LeaveType /
    /// LeaveCategoryAllocation / LeaveBalance); the contract's copy disagreed with it by default and
    /// was read by nothing but its own tab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database has no such columns, and a bare
    /// <c>DropColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ DESTRUCTIVE, deliberately.</b> This is the first HR migration in the round that removes
    /// data. It is safe because the closure ledger's probe (2026-09-01) found <b>0 of 24 rows</b> with
    /// any of the three off its default — there is nothing on any row a person typed. The reserved
    /// trio (<c>EffectiveDate</c>, <c>ContractEndDate</c>, <c>IsCurrent</c>) is untouched; see
    /// HR-CLOSURE-LEDGER § F, whose banner still stands for those three.
    /// </para>
    ///
    /// <para>
    /// <b>Each column's default constraint is looked up, not named.</b> On a model-built database
    /// the default's name is server-generated; guessing it would leave the column undroppable.
    /// <c>Down</c> restores the three with the entity's OLD defaults (20 / 15 / 10, not the
    /// scaffold's 0) so a rolled-back database reads as it did.
    /// </para>
    /// </remarks>
    public partial class DropContractLeaveColumns : Migration
    {
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL
        EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private static string AddColumnWithDefault(string table, string column, string definition, string defaultValue) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition} NOT NULL CONSTRAINT [DF_{table}_{column}] DEFAULT {defaultValue};";

        private const string Table = "EmployeeContractDetails";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn(Table, "AnnualLeaveEntitlementDays"));
            migrationBuilder.Sql(DropColumn(Table, "VacationDaysPerYear"));
            migrationBuilder.Sql(DropColumn(Table, "SickDaysPerYear"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumnWithDefault(Table, "AnnualLeaveEntitlementDays", "int", "20"));
            migrationBuilder.Sql(AddColumnWithDefault(Table, "VacationDaysPerYear", "int", "15"));
            migrationBuilder.Sql(AddColumnWithDefault(Table, "SickDaysPerYear", "int", "10"));
        }
    }
}
