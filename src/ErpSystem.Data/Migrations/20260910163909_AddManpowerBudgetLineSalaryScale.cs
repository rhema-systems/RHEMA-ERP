using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 2b (recruitment feedback), lane R3, decision D-1: a manpower budget line records the
    /// place on the salary scale its planned average salary was read from — grade, level, notch —
    /// and where the figure came from (<c>PlannedSalarySource</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ <c>PlannedSalarySource</c> defaults to 4 (<c>Manual</c>), not the scaffolded 0.</b>
    /// The enum has no zero member (<c>Notch = 1 … Manual = 4</c>), and every line that exists when
    /// this runs had its figure typed by hand — the only true statement about it. A scaffolded
    /// <c>defaultValue: 0</c> would have written a value that is not a member into every existing
    /// row (the fourth time this round: D1's bool, E1's <c>PayBasis</c>, G's enum, now this).
    /// </para>
    ///
    /// <para>
    /// <b>Restrict on all three foreign keys.</b> A grade, level or notch a budget line points at
    /// cannot be deleted from under it; the structure's own rule is to retire, not delete, and
    /// this keeps a budget's stated basis readable for as long as the budget exists.
    /// </para>
    /// </remarks>
    public partial class AddManpowerBudgetLineSalaryScale : Migration
    {
        // ⚠ The table guard is NOT redundant. COL_LENGTH returns NULL both for "no such column" and
        // for "no such table", so the column check alone would fall through to an ALTER against a
        // table that does not exist.
        private static string AddColumn(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string CreateIndex(string table, string index, string columns) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ({columns});";

        private static string DropIndex(string table, string index) => $@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('dbo.{table}'))
    DROP INDEX [{index}] ON [dbo].[{table}];";

        private static string AddForeignKey(string table, string fk, string column, string principalTable, string onDelete = "NO ACTION") => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
   AND OBJECT_ID('dbo.{principalTable}', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] WITH CHECK
        ADD CONSTRAINT [{fk}] FOREIGN KEY ([{column}]) REFERENCES [dbo].[{principalTable}] ([Id]) ON DELETE {onDelete};";

        private static string DropForeignKey(string table, string fk) => $@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = '{fk}' AND parent_object_id = OBJECT_ID('dbo.{table}'))
    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{fk}];";

        /// <remarks>
        /// The default constraint is looked up rather than named: on a model-built database the
        /// name is server-generated, so guessing it would leave the column undroppable.
        /// </remarks>
        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
BEGIN
    DECLARE @df_{table}_{column} sysname;
    SELECT @df_{table}_{column} = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON c.default_object_id = dc.object_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.{table}') AND c.name = '{column}';
    IF @df_{table}_{column} IS NOT NULL EXEC('ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [' + @df_{table}_{column} + ']');
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
END";

        private const string Table = "ManpowerBudgetLines";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 4 = Manual: every existing line was typed. NOT the scaffolded 0 (not a member).
            migrationBuilder.Sql(AddColumn(Table, "PlannedSalarySource", "int NOT NULL DEFAULT 4"));

            migrationBuilder.Sql(AddColumn(Table, "SalaryGradeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "SalaryLevelId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "SalaryNotchId", "uniqueidentifier NULL"));

            migrationBuilder.Sql(CreateIndex(Table, "IX_ManpowerBudgetLines_SalaryGradeId", "[SalaryGradeId]"));
            migrationBuilder.Sql(CreateIndex(Table, "IX_ManpowerBudgetLines_SalaryLevelId", "[SalaryLevelId]"));
            migrationBuilder.Sql(CreateIndex(Table, "IX_ManpowerBudgetLines_SalaryNotchId", "[SalaryNotchId]"));

            migrationBuilder.Sql(AddForeignKey(Table, "FK_ManpowerBudgetLines_SalaryGrades_SalaryGradeId", "SalaryGradeId", "SalaryGrades"));
            migrationBuilder.Sql(AddForeignKey(Table, "FK_ManpowerBudgetLines_SalaryLevels_SalaryLevelId", "SalaryLevelId", "SalaryLevels"));
            migrationBuilder.Sql(AddForeignKey(Table, "FK_ManpowerBudgetLines_SalaryNotches_SalaryNotchId", "SalaryNotchId", "SalaryNotches"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Table, "FK_ManpowerBudgetLines_SalaryNotches_SalaryNotchId"));
            migrationBuilder.Sql(DropForeignKey(Table, "FK_ManpowerBudgetLines_SalaryLevels_SalaryLevelId"));
            migrationBuilder.Sql(DropForeignKey(Table, "FK_ManpowerBudgetLines_SalaryGrades_SalaryGradeId"));

            migrationBuilder.Sql(DropIndex(Table, "IX_ManpowerBudgetLines_SalaryNotchId"));
            migrationBuilder.Sql(DropIndex(Table, "IX_ManpowerBudgetLines_SalaryLevelId"));
            migrationBuilder.Sql(DropIndex(Table, "IX_ManpowerBudgetLines_SalaryGradeId"));

            migrationBuilder.Sql(DropColumn(Table, "SalaryNotchId"));
            migrationBuilder.Sql(DropColumn(Table, "SalaryLevelId"));
            migrationBuilder.Sql(DropColumn(Table, "SalaryGradeId"));
            migrationBuilder.Sql(DropColumn(Table, "PlannedSalarySource"));
        }
    }
}
