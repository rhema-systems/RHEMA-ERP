using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane G: which Procurement suppliers provide which pre-employment checks, and the
    /// supplier behind a check item's and a template item's provider name (register row R-7;
    /// decision D-14).
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
    /// <b>Purely additive.</b> One new table with no rows to default, two nullable columns on
    /// existing tables, five indexes and four foreign keys. The provider NAME columns
    /// (<c>ServiceProviderName</c>, <c>DefaultServiceProvider</c>) are untouched and keep every old
    /// row's text; the new supplier ids are null on all of them. Nothing here reads or rewrites a
    /// value a person typed.
    /// </para>
    ///
    /// <para>
    /// <b>The unique index on the provider table is filtered on live rows</b> (<c>[IsDeleted] = 0</c>):
    /// a removed pairing is a soft delete, and an unfiltered unique index would keep it reserved for
    /// ever — the relationship-type lesson.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> The only non-nullable bools (<c>IsActive</c>, <c>IsDeleted</c>)
    /// are in the NEW table, which has no rows to default; both columns added to existing tables
    /// are nullable.
    /// </para>
    /// </remarks>
    public partial class AddPreEmploymentCheckProviders : Migration
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

        private const string Providers = "PreEmploymentCheckProviderServices";
        private const string Items = "PreEmploymentCheckItems";
        private const string TemplateItems = "PreEmploymentCheckTemplateItems";
        private const string ItemFk = "FK_PreEmploymentCheckItems_Suppliers_ServiceProviderSupplierId";
        private const string TemplateItemFk = "FK_PreEmploymentCheckTemplateItems_Suppliers_DefaultServiceProviderSupplierId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The provider table ────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.PreEmploymentCheckProviderServices', 'U') IS NULL
CREATE TABLE [dbo].[PreEmploymentCheckProviderServices] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [CheckType] int NOT NULL,
    [Notes] nvarchar(500) NULL,
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
    CONSTRAINT [PK_PreEmploymentCheckProviderServices] PRIMARY KEY ([Id])
);");

            // ── 2. The nullable columns ──────────────────────────────────────────────
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then indexing it in one batch fails with "Invalid column name".
            migrationBuilder.Sql(AddColumn(Items, "ServiceProviderSupplierId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(TemplateItems, "DefaultServiceProviderSupplierId", "uniqueidentifier NULL"));

            // ── 3. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateUniqueIndex(
                Providers, "IX_PreEmpCheckProvider_Tenant_Supplier_Type", "[TenantId], [SupplierId], [CheckType]", "[IsDeleted] = 0"));
            migrationBuilder.Sql(CreateIndex(Providers, "IX_PreEmpCheckProvider_Tenant_Type", "[TenantId], [CheckType]"));
            migrationBuilder.Sql(CreateIndex(Providers, "IX_PreEmploymentCheckProviderServices_SupplierId", "[SupplierId]"));
            migrationBuilder.Sql(CreateIndex(Items, "IX_PreEmpCheckItem_SupplierId", "[ServiceProviderSupplierId]"));
            migrationBuilder.Sql(CreateIndex(TemplateItems, "IX_PreEmpCheckTemplateItem_SupplierId", "[DefaultServiceProviderSupplierId]"));

            // ── 4. Foreign keys ──────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(
                Providers, "FK_PreEmploymentCheckProviderServices_Suppliers_SupplierId", "SupplierId", "Suppliers"));
            migrationBuilder.Sql(AddForeignKey(
                Providers, "FK_PreEmploymentCheckProviderServices_Tenants_TenantId", "TenantId", "Tenants"));
            migrationBuilder.Sql(AddForeignKey(Items, ItemFk, "ServiceProviderSupplierId", "Suppliers"));
            migrationBuilder.Sql(AddForeignKey(TemplateItems, TemplateItemFk, "DefaultServiceProviderSupplierId", "Suppliers"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Items, ItemFk));
            migrationBuilder.Sql(DropForeignKey(TemplateItems, TemplateItemFk));
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.PreEmploymentCheckProviderServices', 'U') IS NOT NULL
    DROP TABLE [dbo].[PreEmploymentCheckProviderServices];");
            migrationBuilder.Sql(DropIndex(Items, "IX_PreEmpCheckItem_SupplierId"));
            migrationBuilder.Sql(DropIndex(TemplateItems, "IX_PreEmpCheckTemplateItem_SupplierId"));
            migrationBuilder.Sql(DropColumn(Items, "ServiceProviderSupplierId"));
            migrationBuilder.Sql(DropColumn(TemplateItems, "DefaultServiceProviderSupplierId"));
        }
    }
}
