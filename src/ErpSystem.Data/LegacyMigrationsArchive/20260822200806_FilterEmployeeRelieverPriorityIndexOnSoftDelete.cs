using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Narrows <c>IX_EmployeeRelievers_EmployeeId_Priority</c> to live rows: <c>WHERE IsDeleted = 0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slice 7, and the migration this bundle has owed since slice 0 measured D-9.
    /// <c>EmployeeRelievers</c> soft-deletes, so without the filter a removed reliever holds their
    /// priority slot for ever: <c>EmployeeRelieverService</c>'s duplicate check reads live rows,
    /// sees nothing, approves the write, and SQL then rejects it — so the caller gets an opaque 500
    /// instead of the sentence the service was written to give.
    /// </para>
    /// <para>
    /// Reproduced exactly on 2026-08-22 before this migration: create a priority-1 row, delete it,
    /// create priority 1 again — <b>500</b>, no body. ⚠ The reproduction has to recreate the
    /// identical <c>(EmployeeId, Priority)</c> pair; slice 0's first attempt changed the value
    /// before deleting, so nothing clashed and it reported the defect absent.
    /// </para>
    /// <para>
    /// <b>Fourth occurrence of one trap in this bundle</b>, after D-10 (the external-associate number
    /// generator) and D-29 (<c>IX_Team_Tenant_Code</c>, slice 4b). Whenever a store soft-deletes,
    /// every uniqueness claim over it — index or generator — is wrong until proven otherwise.
    /// </para>
    /// <para>
    /// Written as guarded SQL rather than the scaffolded <c>DropIndex</c>/<c>CreateIndex</c> pair so
    /// it is safe to re-run against a database already at either state, matching slice 4b and the
    /// surrounding HR migrations.
    /// </para>
    /// </remarks>
    public partial class FilterEmployeeRelieverPriorityIndexOnSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop first, unconditionally-if-present. An "IF NOT EXISTS … CREATE" on its own would
            // find the UNFILTERED index already sitting under the same name, skip, and record the
            // migration as applied — leaving the defect in place and looking fixed.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeRelievers_EmployeeId_Priority' AND object_id = OBJECT_ID('dbo.EmployeeRelievers'))
    DROP INDEX [IX_EmployeeRelievers_EmployeeId_Priority] ON [dbo].[EmployeeRelievers];");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeRelievers_EmployeeId_Priority' AND object_id = OBJECT_ID('dbo.EmployeeRelievers'))
    CREATE UNIQUE INDEX [IX_EmployeeRelievers_EmployeeId_Priority] ON [dbo].[EmployeeRelievers] ([EmployeeId], [Priority]) WHERE [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeRelievers_EmployeeId_Priority' AND object_id = OBJECT_ID('dbo.EmployeeRelievers'))
    DROP INDEX [IX_EmployeeRelievers_EmployeeId_Priority] ON [dbo].[EmployeeRelievers];");

            // ⚠ This can legitimately FAIL, and failing is the right behaviour. Once the filtered
            // index has been live, a soft-deleted row may share (EmployeeId, Priority) with a live
            // one — which is exactly what the filter exists to allow. Rebuilding the unfiltered
            // index then hits a genuine duplicate. Reverting past this migration means reconciling
            // those rows first; silently dropping the uniqueness instead would be the worse answer.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeRelievers_EmployeeId_Priority' AND object_id = OBJECT_ID('dbo.EmployeeRelievers'))
    CREATE UNIQUE INDEX [IX_EmployeeRelievers_EmployeeId_Priority] ON [dbo].[EmployeeRelievers] ([EmployeeId], [Priority]);");
        }
    }
}
