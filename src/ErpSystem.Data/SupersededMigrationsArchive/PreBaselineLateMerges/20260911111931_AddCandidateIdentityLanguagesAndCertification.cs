using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane C1: the candidate's national-identity trio, the HR language catalogue with a
    /// link from the candidate language row, certificate details on a candidate skill, and a
    /// description on a candidate document (plan § 5.6; register rows R-3a, R-3d, R-3e, R-3f;
    /// decisions D-15, D-16, D-17).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has every column, index and
    /// constraint below, and a bare <c>AddColumn</c> or <c>CreateTable</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> One new table with no rows to default, eight nullable columns on
    /// existing tables, three foreign keys, and one index rename. Nothing here reads or rewrites a
    /// value a person typed. <c>JobCandidateLanguage.LanguageName</c> stays NOT NULL and keeps
    /// every old row's text; the new <c>LanguageId</c> is null on all of them, and the services
    /// mirror the catalogue name into the text column on each save from now on.
    /// </para>
    ///
    /// <para>
    /// <b>Why the candidate-language foreign key is dropped and re-added.</b> Until this lane
    /// <c>JobCandidateLanguage</c> had no explicit configuration, so EF's convention gave its
    /// candidate foreign key <c>ON DELETE CASCADE</c> and the index the conventional name. The
    /// explicit block added in lane C1 makes it <c>NO ACTION</c>, like every other candidate child,
    /// and names the index by the house pattern. Both changes are guarded: a model-built database
    /// already has the restricted key and the new name.
    /// </para>
    ///
    /// <para>
    /// <b>Both unique indexes on the catalogue are filtered on live rows</b> (<c>[IsDeleted] = 0</c>).
    /// A catalogue delete is a soft delete, and an unfiltered unique index would keep a deleted
    /// name reserved for ever — the relationship-type lesson.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> The only non-nullable bool (<c>Languages.IsActive</c>) is in
    /// the NEW table, which has no rows to default; every column added to an existing table is
    /// nullable.
    /// </para>
    /// </remarks>
    public partial class AddCandidateIdentityLanguagesAndCertification : Migration
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

        private static string RenameIndex(string table, string from, string to) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{from}' AND object_id = OBJECT_ID('dbo.{table}'))
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{to}' AND object_id = OBJECT_ID('dbo.{table}'))
    EXEC sp_rename N'dbo.{table}.{from}', N'{to}', N'INDEX';";

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

        /// <summary>Drops the key only while it still carries the conventional CASCADE, so the re-add below is a no-op elsewhere.</summary>
        private static string DropCascadingForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}') AND delete_referential_action <> 0)
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

        private const string CandidateLanguageFk = "FK_JobCandidateLanguages_JobCandidates_JobCandidateId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. The language catalogue ────────────────────────────────────────────
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.Languages', 'U') IS NULL
CREATE TABLE [dbo].[Languages] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(10) NULL,
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
    CONSTRAINT [PK_Languages] PRIMARY KEY ([Id])
);");

            migrationBuilder.Sql(CreateUniqueIndex(
                "Languages", "IX_Language_Tenant_Name", "[TenantId], [Name]", "[IsDeleted] = 0"));
            // ⚠ Also filtered on Code IS NOT NULL: the code is optional, and an unfiltered unique
            // index would allow exactly one row without one.
            migrationBuilder.Sql(CreateUniqueIndex(
                "Languages", "IX_Language_Tenant_Code", "[TenantId], [Code]", "[Code] IS NOT NULL AND [IsDeleted] = 0"));
            migrationBuilder.Sql(AddForeignKey(
                "Languages", "FK_Languages_Tenants_TenantId", "TenantId", "Tenants"));

            // ── 2. The nullable columns ──────────────────────────────────────────────
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then indexing it in one batch fails with "Invalid column name".
            migrationBuilder.Sql(AddColumn("JobCandidates", "NationalIdTypeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn("JobCandidates", "NationalIdNumber", "nvarchar(50) NULL"));
            migrationBuilder.Sql(AddColumn("JobCandidates", "NationalIdExpiryDate", "datetime2 NULL"));

            migrationBuilder.Sql(AddColumn("JobCandidateSkills", "CertificationNumber", "nvarchar(100) NULL"));
            migrationBuilder.Sql(AddColumn("JobCandidateSkills", "CertifyingBody", "nvarchar(200) NULL"));
            migrationBuilder.Sql(AddColumn("JobCandidateSkills", "CertificationExpiryDate", "datetime2 NULL"));

            migrationBuilder.Sql(AddColumn("JobCandidateLanguages", "LanguageId", "uniqueidentifier NULL"));

            migrationBuilder.Sql(AddColumn("JobCandidateDocuments", "Description", "nvarchar(500) NULL"));

            // ── 3. Indexes ───────────────────────────────────────────────────────────
            migrationBuilder.Sql(CreateIndex("JobCandidates", "IX_JobCandidates_NationalIdTypeId", "[NationalIdTypeId]"));
            migrationBuilder.Sql(CreateIndex("JobCandidateLanguages", "IX_CandidateLanguage_LanguageId", "[LanguageId]"));
            // The conventional name gives way to the house pattern; a model-built database already
            // carries the new one, and a database that somehow has neither gets it created.
            migrationBuilder.Sql(RenameIndex("JobCandidateLanguages", "IX_JobCandidateLanguages_JobCandidateId", "IX_CandidateLanguage_CandidateId"));
            migrationBuilder.Sql(CreateIndex("JobCandidateLanguages", "IX_CandidateLanguage_CandidateId", "[JobCandidateId]"));

            // ── 4. Foreign keys ──────────────────────────────────────────────────────
            // NO ACTION on all of them: a language or an identification type that a record stands
            // on cannot be deleted out from under it. The services refuse first, with a count.
            migrationBuilder.Sql(DropCascadingForeignKey("JobCandidateLanguages", CandidateLanguageFk));
            migrationBuilder.Sql(AddForeignKey("JobCandidateLanguages", CandidateLanguageFk, "JobCandidateId", "JobCandidates"));
            migrationBuilder.Sql(AddForeignKey(
                "JobCandidateLanguages", "FK_JobCandidateLanguages_Languages_LanguageId", "LanguageId", "Languages"));
            migrationBuilder.Sql(AddForeignKey(
                "JobCandidates", "FK_JobCandidates_IdentificationTypes_NationalIdTypeId", "NationalIdTypeId", "IdentificationTypes"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reversible without losing anything a person typed: the language NAME stays on every
            // candidate row because the services mirrored it there on each save. What is lost is
            // the catalogue link, the identity trio, the certificate details and the descriptions.
            migrationBuilder.Sql(DropForeignKey("JobCandidates", "FK_JobCandidates_IdentificationTypes_NationalIdTypeId"));
            migrationBuilder.Sql(DropForeignKey("JobCandidateLanguages", "FK_JobCandidateLanguages_Languages_LanguageId"));

            // Back to the conventional CASCADE the table had before it was configured explicitly.
            migrationBuilder.Sql(DropForeignKey("JobCandidateLanguages", CandidateLanguageFk));
            migrationBuilder.Sql(AddForeignKey("JobCandidateLanguages", CandidateLanguageFk, "JobCandidateId", "JobCandidates", "CASCADE"));

            migrationBuilder.Sql(DropIndex("JobCandidateLanguages", "IX_CandidateLanguage_LanguageId"));
            migrationBuilder.Sql(DropIndex("JobCandidates", "IX_JobCandidates_NationalIdTypeId"));
            migrationBuilder.Sql(RenameIndex("JobCandidateLanguages", "IX_CandidateLanguage_CandidateId", "IX_JobCandidateLanguages_JobCandidateId"));

            migrationBuilder.Sql(DropColumn("JobCandidateDocuments", "Description"));
            migrationBuilder.Sql(DropColumn("JobCandidateLanguages", "LanguageId"));
            migrationBuilder.Sql(DropColumn("JobCandidateSkills", "CertificationExpiryDate"));
            migrationBuilder.Sql(DropColumn("JobCandidateSkills", "CertifyingBody"));
            migrationBuilder.Sql(DropColumn("JobCandidateSkills", "CertificationNumber"));
            migrationBuilder.Sql(DropColumn("JobCandidates", "NationalIdExpiryDate"));
            migrationBuilder.Sql(DropColumn("JobCandidates", "NationalIdNumber"));
            migrationBuilder.Sql(DropColumn("JobCandidates", "NationalIdTypeId"));

            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.Languages', 'U') IS NOT NULL DROP TABLE [dbo].[Languages];");
        }
    }
}
