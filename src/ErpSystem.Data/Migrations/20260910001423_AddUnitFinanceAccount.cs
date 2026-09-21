using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane B2: an organisation unit's and a team's account code becomes a real reference into
    /// Finance's chart of accounts, instead of free text (plan § 1.1, § 6.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>No default-value trap.</b> Both columns are nullable, so EF scaffolded no default and
    /// there is nothing to repair — unlike the three migrations earlier in this round, where a
    /// non-nullable enum or bool added to an existing table got a silent <c>defaultValue: 0</c>.
    /// </para>
    ///
    /// <para>
    /// <b>The existing code columns are untouched.</b> <c>OrganizationUnits.AccountCode</c> and
    /// <c>Teams.CostCenterCode</c> stay exactly as they are and keep every value they hold. They
    /// become SNAPSHOTS: once a unit names an account, the service rewrites the code from it on
    /// every save. A unit that never names one keeps its free text, which is how every row that
    /// predates this lane goes on working, and why there is no data migration here.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ <c>Restrict</c>, on purpose.</b> Deleting an account a unit is charged to fails at the
    /// database rather than quietly orphaning the charge. Finance building its own delete guard is
    /// an ask (plan § 7.2); until it exists, the loud failure is the right one.
    /// </para>
    /// </remarks>
    public partial class AddUnitFinanceAccount : Migration
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
        /// ⚠ The default constraint is looked up rather than named: on a model-built database
        /// (<c>rebuild-db</c>) the name is server-generated, so guessing it would leave the column
        /// undroppable and the <c>Down</c> broken.
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn("OrganizationUnits", "FinanceAccountId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex("OrganizationUnits", "IX_OrganizationUnits_FinanceAccountId", "[FinanceAccountId]"));
            migrationBuilder.Sql(AddForeignKey("OrganizationUnits", "FK_OrganizationUnits_Accounts_FinanceAccountId",
                "FinanceAccountId", "Accounts"));

            migrationBuilder.Sql(AddColumn("Teams", "FinanceAccountId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(CreateIndex("Teams", "IX_Teams_FinanceAccountId", "[FinanceAccountId]"));
            migrationBuilder.Sql(AddForeignKey("Teams", "FK_Teams_Accounts_FinanceAccountId",
                "FinanceAccountId", "Accounts"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey("Teams", "FK_Teams_Accounts_FinanceAccountId"));
            migrationBuilder.Sql(DropIndex("Teams", "IX_Teams_FinanceAccountId"));
            migrationBuilder.Sql(DropColumn("Teams", "FinanceAccountId"));

            migrationBuilder.Sql(DropForeignKey("OrganizationUnits", "FK_OrganizationUnits_Accounts_FinanceAccountId"));
            migrationBuilder.Sql(DropIndex("OrganizationUnits", "IX_OrganizationUnits_FinanceAccountId"));
            migrationBuilder.Sql(DropColumn("OrganizationUnits", "FinanceAccountId"));
        }
    }
}
