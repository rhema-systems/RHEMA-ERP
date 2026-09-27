using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane J2: the author's PROPOSED salary grade beside the system's suggestion on a job
    /// description (register row J-5; decision D-11) — <c>ProposedSalaryGradeId</c> and a note.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and their keys,
    /// and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> Two nullable columns on <c>JobDescriptions</c>, one index, one
    /// <c>NO ACTION</c> foreign key to <c>SalaryGrades</c> (payroll's projection). No row is rewritten:
    /// existing descriptions have no proposal until their valuation is next stored, when it defaults
    /// to the suggestion. The typed <c>RoleIntrinsicValue</c> column is untouched (D-9 stops WRITING
    /// it; what an author typed stays on the row, reported and no longer counted).
    /// </para>
    /// </remarks>
    public partial class AddJobDescriptionProposedGrade : Migration
    {
        private static string AddColumn(string table, string column, string definition) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

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

        private const string Table = "JobDescriptions";
        private const string Index = "IX_JobDescriptions_ProposedSalaryGradeId";
        private const string Fk = "FK_JobDescriptions_SalaryGrades_ProposedSalaryGradeId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then indexing it in one batch fails with "Invalid column name".
            migrationBuilder.Sql(AddColumn(Table, "ProposedSalaryGradeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ProposedSalaryGradeNote", "nvarchar(500) NULL"));
            migrationBuilder.Sql(CreateIndex(Table, Index, "[ProposedSalaryGradeId]"));
            migrationBuilder.Sql(AddForeignKey(Table, Fk, "ProposedSalaryGradeId", "SalaryGrades"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Table, Fk));
            migrationBuilder.Sql(DropIndex(Table, Index));
            migrationBuilder.Sql(DropColumn(Table, "ProposedSalaryGradeId"));
            migrationBuilder.Sql(DropColumn(Table, "ProposedSalaryGradeNote"));
        }
    }
}
