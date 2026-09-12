using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Records where a position's establishment came from, and adds the FR-HR-136 enforcement
    /// setting (area 17/18 slice 8).
    /// </summary>
    /// <remarks>
    /// <para>FR-HR-136 — <i>verify the position against the approved establishment before a vacancy
    /// may be approved</i> — cannot be enforced against <c>ExpectedHeadcount</c> alone, because that
    /// column cannot distinguish an authorised number from its own default. Measured on the live
    /// tenant: <b>132 of 146 positions carry <c>ExpectedHeadcount = 1</c></b>, untouched since
    /// creation, while one of them holds over a thousand people. That is why area 8 had to downgrade
    /// FR-HR-173 to advisory and why area 6's establishment classification never gained teeth.</para>
    ///
    /// <para><c>EstablishmentApprovedOn</c> answers the missing question. Null means nobody has ever
    /// authorised a headcount for the post, so it is not establishment-constrained. Set means the
    /// number came from a manpower budget that went the whole way up FR-HR-135's chain, and
    /// exceeding it is worth refusing.</para>
    ///
    /// <para>Hand-written; discovery attributes inline; deliberately NOT listed in
    /// <c>FastBuildMigrationMetadata</c>.</para>
    /// </remarks>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819060000_AddApprovedEstablishmentTracking")]
    public partial class AddApprovedEstablishmentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentApprovedOn' AND object_id = OBJECT_ID('dbo.EmployeePositions'))
    ALTER TABLE [dbo].[EmployeePositions] ADD [EstablishmentApprovedOn] datetime2 NULL;");
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentSourceBudgetId' AND object_id = OBJECT_ID('dbo.EmployeePositions'))
    ALTER TABLE [dbo].[EmployeePositions] ADD [EstablishmentSourceBudgetId] uniqueidentifier NULL;");

            // Default 3 = Block. See CompanyHrPolicySettings.EstablishmentEnforcementMode for why
            // this is stricter than the budget ladder's Warn: it only ever fires for a position
            // whose establishment three people authorised.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentEnforcementMode' AND object_id = OBJECT_ID('dbo.CompanyHrPolicySettings'))
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [EstablishmentEnforcementMode] int NOT NULL DEFAULT 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentEnforcementMode' AND object_id = OBJECT_ID('dbo.CompanyHrPolicySettings'))
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [EstablishmentEnforcementMode];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentSourceBudgetId' AND object_id = OBJECT_ID('dbo.EmployeePositions'))
    ALTER TABLE [dbo].[EmployeePositions] DROP COLUMN [EstablishmentSourceBudgetId];");
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'EstablishmentApprovedOn' AND object_id = OBJECT_ID('dbo.EmployeePositions'))
    ALTER TABLE [dbo].[EmployeePositions] DROP COLUMN [EstablishmentApprovedOn];");
        }
    }
}
