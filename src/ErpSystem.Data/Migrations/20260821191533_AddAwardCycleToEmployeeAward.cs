using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// A conferred award records the run it was conferred in (area 14 slice 8).
    /// </summary>
    /// <remarks>
    /// <para><b>Why the award needs this and the nomination is not enough.</b> An award conferred
    /// from a nomination can already reach its cycle through <c>AwardNominationId</c>. An award
    /// taken by <b>direct management selection</b> has no nomination at all — that is the whole
    /// point of AWD-07 — so without this column a "Chairman's Award 2026 Q3" could not say which run
    /// it belonged to, and a cycle could not list its own outcome.</para>
    ///
    /// <para><b>Nullable, and that is the back-fill answer.</b> Awards conferred before cycles
    /// existed belong to no run, which is the truth about them rather than a gap to guess at. TDC
    /// may also confer an ad-hoc award outside any cycle.</para>
    ///
    /// <para>Scaffolded by the user, rewritten here into guarded SQL so it is safe on a model-built
    /// database as well as a migrated one, and listed in <c>FastBuildMigrationMetadata.cs</c>.</para>
    /// </remarks>
    public partial class AddAwardCycleToEmployeeAward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AwardCycleId' AND object_id = OBJECT_ID('dbo.EmployeeAwards'))
    ALTER TABLE [dbo].[EmployeeAwards] ADD [AwardCycleId] uniqueidentifier NULL;");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeAwards_AwardCycleId' AND object_id = OBJECT_ID('dbo.EmployeeAwards'))
    CREATE INDEX [IX_EmployeeAwards_AwardCycleId] ON [dbo].[EmployeeAwards] ([AwardCycleId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeAwards_AwardCycles_AwardCycleId')
    ALTER TABLE [dbo].[EmployeeAwards] ADD CONSTRAINT [FK_EmployeeAwards_AwardCycles_AwardCycleId]
        FOREIGN KEY ([AwardCycleId]) REFERENCES [dbo].[AwardCycles] ([Id]) ON DELETE NO ACTION;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeAwards_AwardCycles_AwardCycleId')
    ALTER TABLE [dbo].[EmployeeAwards] DROP CONSTRAINT [FK_EmployeeAwards_AwardCycles_AwardCycleId];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeAwards_AwardCycleId' AND object_id = OBJECT_ID('dbo.EmployeeAwards'))
    DROP INDEX [IX_EmployeeAwards_AwardCycleId] ON [dbo].[EmployeeAwards];");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'AwardCycleId' AND object_id = OBJECT_ID('dbo.EmployeeAwards'))
    ALTER TABLE [dbo].[EmployeeAwards] DROP COLUMN [AwardCycleId];");
        }
    }
}
