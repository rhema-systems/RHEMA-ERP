using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 3, lane V: ownership and intent on a candidate talent segment (register row R-1;
    /// decision D-6) — who works it, why it exists, and the role it feeds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> and the UAT builder create the
    /// schema from the EF model, so a rebuilt database already has these columns and their keys, and
    /// a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Purely additive.</b> Four nullable columns on <c>CandidateTalentSegments</c>, three indexes
    /// and three <c>NO ACTION</c> foreign keys — to <c>Employees</c> (the owning recruiter),
    /// <c>EmployeePositions</c> (the target position) and <c>JobFamilies</c>. No row is rewritten:
    /// every segment that existed before this lane keeps working with all four empty, which is why
    /// none of them is required. The scaffold defaulted nothing, so there is no default constraint
    /// to reconcile against the entity initialiser.
    /// </para>
    ///
    /// <para>
    /// <b>Restrict, not cascade, on all three.</b> Retiring a recruiter, a position or a job family
    /// must not take a talent segment — and with it every candidate's membership — along with it.
    /// </para>
    /// </remarks>
    public partial class AddTalentSegmentOwnership : Migration
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
        /// <c>Down</c> broken. None of these four columns has one, but the helper is the same one
        /// every HR migration uses and a later edit must not have to remember to add the lookup.
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

        private const string Table = "CandidateTalentSegments";

        private const string IxOwner    = "IX_CandidateTalentSegment_OwnerEmployeeId";
        private const string IxPosition = "IX_CandidateTalentSegment_TargetPositionId";
        private const string IxFamily   = "IX_CandidateTalentSegment_JobFamilyId";

        private const string FkOwner    = "FK_CandidateTalentSegments_Employees_OwnerEmployeeId";
        private const string FkPosition = "FK_CandidateTalentSegments_EmployeePositions_TargetPositionId";
        private const string FkFamily   = "FK_CandidateTalentSegments_JobFamilies_JobFamilyId";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each in its own batch: SQL Server compiles a batch before running it, so adding a
            // column and then indexing it in one batch fails with "Invalid column name".
            migrationBuilder.Sql(AddColumn(Table, "OwnerEmployeeId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "Purpose", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumn(Table, "TargetPositionId", "uniqueidentifier NULL"));
            migrationBuilder.Sql(AddColumn(Table, "JobFamilyId", "uniqueidentifier NULL"));

            migrationBuilder.Sql(CreateIndex(Table, IxOwner, "[OwnerEmployeeId]"));
            migrationBuilder.Sql(CreateIndex(Table, IxPosition, "[TargetPositionId]"));
            migrationBuilder.Sql(CreateIndex(Table, IxFamily, "[JobFamilyId]"));

            migrationBuilder.Sql(AddForeignKey(Table, FkOwner, "OwnerEmployeeId", "Employees"));
            migrationBuilder.Sql(AddForeignKey(Table, FkPosition, "TargetPositionId", "EmployeePositions"));
            migrationBuilder.Sql(AddForeignKey(Table, FkFamily, "JobFamilyId", "JobFamilies"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropForeignKey(Table, FkOwner));
            migrationBuilder.Sql(DropForeignKey(Table, FkPosition));
            migrationBuilder.Sql(DropForeignKey(Table, FkFamily));

            migrationBuilder.Sql(DropIndex(Table, IxOwner));
            migrationBuilder.Sql(DropIndex(Table, IxPosition));
            migrationBuilder.Sql(DropIndex(Table, IxFamily));

            migrationBuilder.Sql(DropColumn(Table, "OwnerEmployeeId"));
            migrationBuilder.Sql(DropColumn(Table, "Purpose"));
            migrationBuilder.Sql(DropColumn(Table, "TargetPositionId"));
            migrationBuilder.Sql(DropColumn(Table, "JobFamilyId"));
        }
    }
}
