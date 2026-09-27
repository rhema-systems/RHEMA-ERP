using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane K: the accepted values of a shortlisting criterion as rows — a catalogue
    /// reference (skill, qualification, certification, language), a gender member, or typed text,
    /// each with the label mirrored (plan § 5.4; register row R-8; decision D-7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has this table, and a bare
    /// <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> One new table with no rows to default and two foreign keys. The
    /// parent's <c>RequiredValue</c> column is untouched and keeps every old row's comma-separated
    /// text; the engine reads these rows first and that text second, so rows written before this
    /// lane keep scoring exactly as they did. Nothing here reads or rewrites a value a person typed.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> The only non-nullable bool (<c>IsDeleted</c>) is in the NEW
    /// table, which has no rows to default.
    /// </para>
    /// </remarks>
    public partial class AddCriteriaCatalogueValues : Migration
    {
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

        private const string Table = "JobShortlistingCriteriaValues";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The table ─────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.JobShortlistingCriteriaValues', 'U') IS NULL
CREATE TABLE [dbo].[JobShortlistingCriteriaValues] (
    [Id] uniqueidentifier NOT NULL,
    [JobShortlistingCriteriaId] uniqueidentifier NOT NULL,
    [Kind] int NOT NULL,
    [ReferenceId] uniqueidentifier NULL,
    [Label] nvarchar(200) NOT NULL,
    [SortOrder] int NOT NULL,
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
    CONSTRAINT [PK_JobShortlistingCriteriaValues] PRIMARY KEY ([Id])
);");

            // ── 2. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex(Table, "IX_JobShortlistingCriteriaValues_TenantId", "[TenantId]"));
            migrationBuilder.Sql(CreateIndex(Table, "IX_ShortlistingCriteriaValue_CriteriaId", "[JobShortlistingCriteriaId]"));

            // ── 3. Foreign keys ──────────────────────────────────────────────────────
            migrationBuilder.Sql(AddForeignKey(
                Table, "FK_JobShortlistingCriteriaValues_JobShortlistingCriterias_JobShortlistingCriteriaId",
                "JobShortlistingCriteriaId", "JobShortlistingCriterias"));
            migrationBuilder.Sql(AddForeignKey(
                Table, "FK_JobShortlistingCriteriaValues_Tenants_TenantId", "TenantId", "Tenants"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.JobShortlistingCriteriaValues', 'U') IS NOT NULL
    DROP TABLE [dbo].[JobShortlistingCriteriaValues];");
        }
    }
}
