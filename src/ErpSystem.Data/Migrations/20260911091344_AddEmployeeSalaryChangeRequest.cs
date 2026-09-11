using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane S (decision D-1): a change of pay becomes a request — <c>EmployeeSalaryChangeRequest</c>
    /// on the workflow engine, applied to HR and to payroll on approval — and the policy switch that
    /// turns the three direct pay doors into "raise a request".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> (converted 2026-09-11, the same day it
    /// was applied, when lane C1's review caught that it had been left in builder form), as on every HR
    /// migration since <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder
    /// create the schema from the EF model, so a rebuilt database already has every column, index and
    /// constraint below, and a bare <c>AddColumn</c> or <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>The one default that matters.</b> The scaffold wrote <c>defaultValue: false</c> for
    /// <c>SalaryChangeRequiresApproval</c>; the entity initialiser says <c>true</c>, and the policy is ON
    /// by default for every tenant that already has a settings row. The column is added with a NAMED
    /// default of 1, so existing rows come up ON, and the <c>UPDATE</c> that follows is a belt-and-braces
    /// for a model-built database, whose column has no default and whose seeded row would otherwise sit
    /// at 0. Both statements are idempotent.
    /// </para>
    ///
    /// <para>
    /// <b>Why <c>UpdateData</c> became <c>Sql</c>.</b> <c>UpdateData</c> needs the migration's model to
    /// infer column types, and the fast Debug build strips migration models, so applying failed with
    /// "no entity type mapped to the table 'CompanyHrPolicySettings'".
    /// </para>
    ///
    /// <para>
    /// <b>Delete behaviour.</b> Every reference is <c>NO ACTION</c>: an employee, a grade, a level or a
    /// notch that a request names cannot be deleted out from under it.
    /// </para>
    /// </remarks>
    public partial class AddEmployeeSalaryChangeRequest : Migration
    {
        private const string Table = "EmployeeSalaryChangeRequests";
        private const string PolicyDefault = "DF_CompanyHrPolicySettings_SalaryChangeRequiresApproval";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        /// <remarks>
        /// ⚠ The default constraint is looked up rather than named: on a model-built database the
        /// name is server-generated, so guessing it would leave the column undroppable and the
        /// <c>Down</c> broken.
        /// </remarks>
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The policy switch, ON for every existing row ─────────────────────
            migrationBuilder.Sql($@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryChangeRequiresApproval') IS NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings]
        ADD [SalaryChangeRequiresApproval] bit NOT NULL CONSTRAINT [{PolicyDefault}] DEFAULT 1;");

            // The seeded default-tenant row, in case the column pre-existed without a default (a
            // model-built database). Its own batch: the column must be compiled before it is named.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SalaryChangeRequiresApproval') IS NOT NULL
    UPDATE [dbo].[CompanyHrPolicySettings] SET [SalaryChangeRequiresApproval] = 1
    WHERE [Id] = 'b2c3d4e5-0000-0000-0000-000000000001' AND [SalaryChangeRequiresApproval] = 0;");

            // ── 2. The request table ────────────────────────────────────────────────
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Table}', 'U') IS NULL
CREATE TABLE [dbo].[{Table}] (
    [Id] uniqueidentifier NOT NULL,
    [EmployeeId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [Status] int NOT NULL,
    [CurrentPayBasis] int NOT NULL,
    [CurrentGradeId] uniqueidentifier NULL,
    [CurrentLevelId] uniqueidentifier NULL,
    [CurrentNotchId] uniqueidentifier NULL,
    [CurrentAmount] decimal(18,2) NULL,
    [ProposedPayBasis] int NULL,
    [ProposedGradeId] uniqueidentifier NULL,
    [ProposedLevelId] uniqueidentifier NULL,
    [ProposedNotchId] uniqueidentifier NULL,
    [ProposedAmount] decimal(18,2) NULL,
    [ProposedCurrencyCode] nvarchar(10) NULL,
    [EffectiveDate] datetime2 NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [RequestedById] uniqueidentifier NOT NULL,
    [SourceProposalId] uniqueidentifier NULL,
    [RejectionReason] nvarchar(1000) NULL,
    [HrAppliedOn] datetime2 NULL,
    [AppliedOn] datetime2 NULL,
    [AppliedByUserId] uniqueidentifier NULL,
    [AppliedPlacementId] uniqueidentifier NULL,
    [ApplyFailure] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [CreatedById] uniqueidentifier NULL,
    [LastModifiedById] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAt] datetime2 NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_{Table}] PRIMARY KEY ([Id])
);");

            // ── 3. Indexes ──────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_EmployeeId", "[EmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_EmployeeId_Status", "[EmployeeId], [Status]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_ProposedGradeId", "[ProposedGradeId]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_ProposedLevelId", "[ProposedLevelId]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_ProposedNotchId", "[ProposedNotchId]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_RequestedById", "[RequestedById]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_Status", "[Status]"));
            migrationBuilder.Sql(CreateIndex(Table, $"IX_{Table}_TenantId", "[TenantId]"));

            // ── 4. Foreign keys ─────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_Employees_EmployeeId", "EmployeeId", "Employees"));
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_Employees_RequestedById", "RequestedById", "Employees"));
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_SalaryGrades_ProposedGradeId", "ProposedGradeId", "SalaryGrades"));
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_SalaryLevels_ProposedLevelId", "ProposedLevelId", "SalaryLevels"));
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_SalaryNotches_ProposedNotchId", "ProposedNotchId", "SalaryNotches"));
            migrationBuilder.Sql(AddForeignKey(Table, $"FK_{Table}_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ⚠ Loses every request ever raised, applied or not. The pay itself is untouched: an
            // applied request already wrote the placement, the pay basis and payroll's basic, and
            // none of those live here.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Table}', 'U') IS NOT NULL DROP TABLE [dbo].[{Table}];");

            migrationBuilder.Sql(DropColumn("CompanyHrPolicySettings", "SalaryChangeRequiresApproval"));
        }
    }
}
