using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Narrows <c>IX_Team_Tenant_Code</c> to live rows: <c>WHERE IsDeleted = 0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Slice 4b. <c>Teams</c> soft-deletes, so without the filter a dissolved team holds its code
    /// for ever: <c>TeamService</c>'s duplicate check reads through the soft-delete filter, sees
    /// nothing, approves the write, and SQL then rejects it — so the caller gets an opaque 500
    /// instead of the sentence the service was written to give.
    /// </para>
    /// <para>
    /// This is the third occurrence of one trap in this bundle. Slice 0 measured it on
    /// <c>IX_EmployeeRelievers_EmployeeId_Priority</c> (D-9) and on the external-associate number
    /// generator (D-10). <b>Whenever a store soft-deletes, every uniqueness claim over it — index or
    /// generator — is wrong until proven otherwise.</b>
    /// </para>
    /// <para>
    /// Written as guarded SQL rather than the scaffolded <c>DropIndex</c>/<c>CreateIndex</c> pair so
    /// it is safe to re-run against a database that is already at either state, matching the house
    /// pattern in the surrounding HR migrations.
    /// </para>
    /// </remarks>
    public partial class FilterTeamCodeUniqueIndexOnSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop first, unconditionally-if-present: an "IF NOT EXISTS … CREATE" on its own would
            // find the UNFILTERED index already sitting under the same name and skip the whole
            // migration, leaving the defect in place and the migration recorded as applied.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Team_Tenant_Code' AND object_id = OBJECT_ID('dbo.Teams'))
    DROP INDEX [IX_Team_Tenant_Code] ON [dbo].[Teams];");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Team_Tenant_Code' AND object_id = OBJECT_ID('dbo.Teams'))
    CREATE UNIQUE INDEX [IX_Team_Tenant_Code] ON [dbo].[Teams] ([TenantId], [Code]) WHERE [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Team_Tenant_Code' AND object_id = OBJECT_ID('dbo.Teams'))
    DROP INDEX [IX_Team_Tenant_Code] ON [dbo].[Teams];");

            // ⚠ This can legitimately FAIL, and failing is the right behaviour. Once the filtered
            // index has been in place, soft-deleted rows may share a code with a live one — which is
            // exactly what the filter exists to allow. Rebuilding the unfiltered index then hits a
            // genuine duplicate. Reverting past this migration means reconciling those codes first;
            // silently dropping the uniqueness instead would be the worse answer.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Team_Tenant_Code' AND object_id = OBJECT_ID('dbo.Teams'))
    CREATE UNIQUE INDEX [IX_Team_Tenant_Code] ON [dbo].[Teams] ([TenantId], [Code]);");
        }
    }
}
