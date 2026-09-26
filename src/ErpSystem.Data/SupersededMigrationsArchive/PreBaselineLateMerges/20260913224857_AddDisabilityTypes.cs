using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane P2: the disability catalogue (<c>DisabilityTypes</c>; register row E-5) and the
    /// <c>DisabilityTypeId</c> the employee and the dependant each name from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has this table, these columns and
    /// their keys, and a bare <c>CreateTable</c> or <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> One new table with no rows to default (the seeder fills it under
    /// <c>seed-hr-all</c>), one nullable column on each of two existing tables, five indexes, three
    /// foreign keys. The two <c>HasDisability</c> / <c>DisabilityDescription</c> pairs are untouched:
    /// the description becomes the notes beside the type, and every old row keeps what it said.
    /// </para>
    ///
    /// <para>
    /// <b>The unique indexes match the model:</b> Tenant+Name unfiltered; Tenant+Code filtered on
    /// <c>[Code] IS NOT NULL</c>, because the code is optional and an unfiltered unique index would
    /// let exactly one row go without one.
    /// </para>
    /// </remarks>
    public partial class AddDisabilityTypes : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string CreateUniqueIndex(string table, string index, string columns, string filter = null)
        {
            var where = string.IsNullOrEmpty(filter) ? string.Empty : " WHERE " + filter;
            return $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE UNIQUE INDEX [{index}] ON [dbo].[{table}] ({columns}){where};";
        }

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(
            string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

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

        private const string Types = "DisabilityTypes";
        private const string Employees = "Employees";
        private const string Dependents = "EmployeeDependents";
        private const string EmployeeFk = "FK_Employees_DisabilityTypes_DisabilityTypeId";
        private const string DependentFk = "FK_EmployeeDependents_DisabilityTypes_DisabilityTypeId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The catalogue ─────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.DisabilityTypes', 'U') IS NULL
CREATE TABLE [dbo].[DisabilityTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NULL,
    [Category] int NOT NULL,
    [Description] nvarchar(500) NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
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
    CONSTRAINT [PK_DisabilityTypes] PRIMARY KEY ([Id])
);");

            // ── 2. The nullable columns — each in its own batch ──────────────────────
            migrationBuilder.Sql(AddColumn(Employees, "DisabilityTypeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Dependents, "DisabilityTypeId", "uniqueidentifier NULL"));

            // ── 3. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateUniqueIndex(Types, "IX_DisabilityType_Tenant_Name", "[TenantId], [Name]"));
            migrationBuilder.Sql(CreateUniqueIndex(Types, "IX_DisabilityType_Tenant_Code", "[TenantId], [Code]", "[Code] IS NOT NULL"));
            migrationBuilder.Sql(CreateIndex(Types, "IX_DisabilityType_Tenant_Category_Active", "[TenantId], [Category], [IsActive]"));
            migrationBuilder.Sql(CreateIndex(Employees, "IX_Employees_DisabilityTypeId", "[DisabilityTypeId]"));
            migrationBuilder.Sql(CreateIndex(Dependents, "IX_EmployeeDependents_DisabilityTypeId", "[DisabilityTypeId]"));

            // ── 4. Foreign keys ──────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(Types, "FK_DisabilityTypes_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(AddForeignKey(Employees, EmployeeFk, "DisabilityTypeId", Types));
            migrationBuilder.Sql(AddForeignKey(Dependents, DependentFk, "DisabilityTypeId", Types));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Employees, EmployeeFk));
            migrationBuilder.Sql(DropForeignKey(Dependents, DependentFk));
            migrationBuilder.Sql(DropIndex(Employees, "IX_Employees_DisabilityTypeId"));
            migrationBuilder.Sql(DropIndex(Dependents, "IX_EmployeeDependents_DisabilityTypeId"));
            migrationBuilder.Sql(DropColumn(Employees, "DisabilityTypeId"));
            migrationBuilder.Sql(DropColumn(Dependents, "DisabilityTypeId"));
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.DisabilityTypes', 'U') IS NOT NULL
    DROP TABLE [dbo].[DisabilityTypes];");
        }
    }
}
