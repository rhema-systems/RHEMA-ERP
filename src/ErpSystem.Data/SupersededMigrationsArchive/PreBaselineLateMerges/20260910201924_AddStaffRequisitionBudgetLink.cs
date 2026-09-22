using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 2b (recruitment feedback), lane R5: a staff requisition names the approved manpower
    /// budget line it draws down from, carries the exception justification decision D-4 asks for,
    /// and keeps the establishment as it stood at submit (decision D-2) for the approver.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Every column is nullable with no default and no backfill</b>, and that is correct rather
    /// than lazy: NULL on the link means "not raised against a budget", which is true of every
    /// requisition that exists when this runs — <c>IsBudgeted</c> was a self-declared checkbox and
    /// nothing can say which of them a budget really covered; NULL on the snapshot means "not yet
    /// submitted since R5", which the detail page renders as such. Nothing here has an entity
    /// initialiser, so the scaffolded-default trap of R3 does not arise.
    /// </para>
    ///
    /// <para>
    /// <b>Restrict on the line.</b> A budget line with requisitions drawing down from it cannot be
    /// deleted from under them; the budget's own delete is Draft-only and Admin-tier anyway.
    /// </para>
    /// </remarks>
    public partial class AddStaffRequisitionBudgetLink : Migration
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
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";

        private const string Table = "StaffRequisitions";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(AddColumn(Table, "ManpowerBudgetLineId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "ExceptionJustification", "nvarchar(2000) NULL"));
            migrationBuilder.Sql(AddColumn(Table, "EstablishmentSnapshotOn", "datetime2 NULL"));
            migrationBuilder.Sql(AddColumn(Table, "EstablishmentSnapshotIsEstablished", "bit NULL"));
            migrationBuilder.Sql(AddColumn(Table, "EstablishmentSnapshotExpected", "int NULL"));
            migrationBuilder.Sql(AddColumn(Table, "EstablishmentSnapshotFilled", "int NULL"));
            migrationBuilder.Sql(AddColumn(Table, "EstablishmentSnapshotSourceBudgetNumber", "nvarchar(50) NULL"));

            migrationBuilder.Sql(CreateIndex(Table, "IX_StaffRequisitions_ManpowerBudgetLineId", "[ManpowerBudgetLineId]"));
            migrationBuilder.Sql(AddForeignKey(Table, "FK_StaffRequisitions_ManpowerBudgetLines_ManpowerBudgetLineId",
                "ManpowerBudgetLineId", "ManpowerBudgetLines"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Table, "FK_StaffRequisitions_ManpowerBudgetLines_ManpowerBudgetLineId"));
            migrationBuilder.Sql(DropIndex(Table, "IX_StaffRequisitions_ManpowerBudgetLineId"));

            migrationBuilder.Sql(DropColumn(Table, "EstablishmentSnapshotSourceBudgetNumber"));
            migrationBuilder.Sql(DropColumn(Table, "EstablishmentSnapshotFilled"));
            migrationBuilder.Sql(DropColumn(Table, "EstablishmentSnapshotExpected"));
            migrationBuilder.Sql(DropColumn(Table, "EstablishmentSnapshotIsEstablished"));
            migrationBuilder.Sql(DropColumn(Table, "EstablishmentSnapshotOn"));
            migrationBuilder.Sql(DropColumn(Table, "ExceptionJustification"));
            migrationBuilder.Sql(DropColumn(Table, "ManpowerBudgetLineId"));
        }
    }
}
