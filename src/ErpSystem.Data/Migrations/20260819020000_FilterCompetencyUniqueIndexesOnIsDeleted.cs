using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds <c>WHERE [IsDeleted] = 0</c> to the three competency unique indexes (area 17 slice 5).
    /// </summary>
    /// <remarks>
    /// <para>All three were unfiltered, and <c>DeleteAsync</c> is a soft delete everywhere in this
    /// codebase — so a deleted row went on holding its slot forever:</para>
    ///
    /// <list type="bullet">
    ///   <item><c>IX_Competency_Tenant_Code</c> — delete a competency and its code can never be
    ///         used again, permanently.</item>
    ///   <item><c>IX_PositionCompetency_Tenant_Position_Competency</c> — delete a position's
    ///         requirement and it can never be re-added. This one also breaks
    ///         <c>BulkReplaceForPositionAsync</c>, which soft-deletes the current set and inserts
    ///         the new one in a single <c>SaveChanges</c>: a unique index rejects that mid
    ///         transaction, so re-saving a position's competencies with any of them unchanged is a
    ///         500.</item>
    ///   <item><c>IX_EmployeeCompetency_Tenant_Employee_Competency</c> — delete an assessment and
    ///         that employee can never be re-assessed on that competency.</item>
    /// </list>
    ///
    /// <para>None had ever fired, because all three tables were empty. They were 500s waiting for
    /// the first user. Same shape as the succession fix in area 13 — <b>a soft delete does not
    /// release a unique index</b> — and the same remedy: teach the index about the flag.</para>
    ///
    /// <para>⚠ Written by hand rather than scaffolded, so the <c>[DbContext]</c> and
    /// <c>[Migration]</c> attributes are carried <b>inline here</b> and this migration is
    /// <b>not</b> listed in <c>FastBuildMigrationMetadata</c>. The two are mutually exclusive:
    /// that file supplies the attributes for migrations whose generated <c>.Designer.cs</c> is
    /// excluded from fast builds, so declaring both makes the partial classes merge and the
    /// attributes duplicate. 99 of this project's 285 migrations are hand-written this way; none of
    /// them appears in that file.</para>
    /// </remarks>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819020000_FilterCompetencyUniqueIndexesOnIsDeleted")]
    public partial class FilterCompetencyUniqueIndexesOnIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Competency_Tenant_Code' AND object_id = OBJECT_ID('dbo.Competencies'))
    DROP INDEX [IX_Competency_Tenant_Code] ON [dbo].[Competencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_Competency_Tenant_Code] ON [dbo].[Competencies] ([TenantId], [Code]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PositionCompetency_Tenant_Position_Competency' AND object_id = OBJECT_ID('dbo.PositionCompetencies'))
    DROP INDEX [IX_PositionCompetency_Tenant_Position_Competency] ON [dbo].[PositionCompetencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_PositionCompetency_Tenant_Position_Competency] ON [dbo].[PositionCompetencies] ([TenantId], [PositionId], [CompetencyId]) WHERE [IsDeleted] = 0;");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeCompetency_Tenant_Employee_Competency' AND object_id = OBJECT_ID('dbo.EmployeeCompetencies'))
    DROP INDEX [IX_EmployeeCompetency_Tenant_Employee_Competency] ON [dbo].[EmployeeCompetencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_EmployeeCompetency_Tenant_Employee_Competency] ON [dbo].[EmployeeCompetencies] ([TenantId], [EmployeeId], [CompetencyId]) WHERE [IsDeleted] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Competency_Tenant_Code' AND object_id = OBJECT_ID('dbo.Competencies'))
    DROP INDEX [IX_Competency_Tenant_Code] ON [dbo].[Competencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_Competency_Tenant_Code] ON [dbo].[Competencies] ([TenantId], [Code]);");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PositionCompetency_Tenant_Position_Competency' AND object_id = OBJECT_ID('dbo.PositionCompetencies'))
    DROP INDEX [IX_PositionCompetency_Tenant_Position_Competency] ON [dbo].[PositionCompetencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_PositionCompetency_Tenant_Position_Competency] ON [dbo].[PositionCompetencies] ([TenantId], [PositionId], [CompetencyId]);");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeCompetency_Tenant_Employee_Competency' AND object_id = OBJECT_ID('dbo.EmployeeCompetencies'))
    DROP INDEX [IX_EmployeeCompetency_Tenant_Employee_Competency] ON [dbo].[EmployeeCompetencies];");
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX [IX_EmployeeCompetency_Tenant_Employee_Competency] ON [dbo].[EmployeeCompetencies] ([TenantId], [EmployeeId], [CompetencyId]);");
        }
    }
}
