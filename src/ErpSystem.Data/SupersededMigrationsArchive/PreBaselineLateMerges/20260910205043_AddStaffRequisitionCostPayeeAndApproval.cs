using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 2b (recruitment feedback), lane R7, decision D-10: a recruitment cost names its payee
    /// (a Procurement supplier, or a person), keeps the base-currency amount Finance's rate gave it
    /// on the cost date, and carries HR's own approval of the spend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para><b>Three scaffolded defaults were wrong, and the DATA half of this migration matters
    /// more than the schema half:</b></para>
    /// <list type="bullet">
    /// <item><c>Status</c> scaffolded <c>defaultValue: 0</c> — not a member. Every cost that exists
    /// when this runs was recorded and never decided: <b>1 = Recorded</b>.</item>
    /// <item><c>CostDate</c> scaffolded <c>0001-01-01</c>. The day the money moved is, for an
    /// existing row, the day it was recorded: backfilled from <c>RecordedDate</c>.</item>
    /// <item><c>AmountBaseCurrency</c> scaffolded <c>0</c>. Existing rows carry the rate their
    /// recorder typed — the best figure available for them — so the base amount is backfilled as
    /// <c>Amount × ExchangeRate</c>; from here on the service writes it from Finance's rate.</item>
    /// </list>
    ///
    /// <para><c>SupplierId</c>/<c>PayeeName</c> are left NULL on existing rows: nobody knows who
    /// they were paid to, and the service only demands a payee when a cost is next edited.</para>
    /// </remarks>
    public partial class AddStaffRequisitionCostPayeeAndApproval : Migration
    {
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

        private const string Table = "StaffRequisitionCosts";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1 = Recorded. NOT the scaffolded 0, which is not a member.
            migrationBuilder.Sql(AddColumn(Table, "Status", "int NOT NULL DEFAULT 1"));
            migrationBuilder.Sql(AddColumn(Table, "CostDate", "date NOT NULL DEFAULT ('1900-01-01')"));
            migrationBuilder.Sql(AddColumn(Table, "AmountBaseCurrency", "decimal(18,2) NOT NULL DEFAULT 0"));
            migrationBuilder.Sql(AddColumn(Table, "SupplierId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "PayeeName", "nvarchar(200) NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ApprovedById", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ApprovedOn", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ApprovalNote", "nvarchar(500) NULL"));

            // The data half: existing rows get the day they were recorded and the base amount
            // their typed rate implies. Guarded on the sentinel default, so a re-run changes nothing.
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Table}', 'CostDate') IS NOT NULL
    UPDATE [dbo].[{Table}] SET [CostDate] = CAST([RecordedDate] AS date) WHERE [CostDate] = '1900-01-01';");
            migrationBuilder.Sql($@"
IF OBJECT_ID('dbo.{Table}', 'U') IS NOT NULL AND COL_LENGTH('dbo.{Table}', 'AmountBaseCurrency') IS NOT NULL
    UPDATE [dbo].[{Table}] SET [AmountBaseCurrency] = ROUND([Amount] * [ExchangeRate], 2) WHERE [AmountBaseCurrency] = 0 AND [Amount] <> 0;");

            migrationBuilder.Sql(CreateIndex(Table, "IX_StaffRequisitionCosts_SupplierId", "[SupplierId]"));
            migrationBuilder.Sql(CreateIndex(Table, "IX_StaffRequisitionCosts_Status", "[Status]"));
            migrationBuilder.Sql(CreateIndex(Table, "IX_StaffRequisitionCosts_ApprovedById", "[ApprovedById]"));
            migrationBuilder.Sql(AddForeignKey(Table, "FK_StaffRequisitionCosts_Suppliers_SupplierId", "SupplierId", "Suppliers"));
            migrationBuilder.Sql(AddForeignKey(Table, "FK_StaffRequisitionCosts_Employees_ApprovedById", "ApprovedById", "Employees"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Table, "FK_StaffRequisitionCosts_Employees_ApprovedById"));
            migrationBuilder.Sql(DropForeignKey(Table, "FK_StaffRequisitionCosts_Suppliers_SupplierId"));
            migrationBuilder.Sql(DropIndex(Table, "IX_StaffRequisitionCosts_ApprovedById"));
            migrationBuilder.Sql(DropIndex(Table, "IX_StaffRequisitionCosts_Status"));
            migrationBuilder.Sql(DropIndex(Table, "IX_StaffRequisitionCosts_SupplierId"));

            migrationBuilder.Sql(DropColumn(Table, "ApprovalNote"));
            migrationBuilder.Sql(DropColumn(Table, "ApprovedOn"));
            migrationBuilder.Sql(DropColumn(Table, "ApprovedById"));
            migrationBuilder.Sql(DropColumn(Table, "PayeeName"));
            migrationBuilder.Sql(DropColumn(Table, "SupplierId"));
            migrationBuilder.Sql(DropColumn(Table, "AmountBaseCurrency"));
            migrationBuilder.Sql(DropColumn(Table, "CostDate"));
            migrationBuilder.Sql(DropColumn(Table, "Status"));
        }
    }
}
