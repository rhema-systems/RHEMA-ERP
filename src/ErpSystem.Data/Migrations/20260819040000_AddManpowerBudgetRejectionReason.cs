using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds <c>ManpowerBudgets.RejectionReason</c> (area 17/18 slice 7).
    /// </summary>
    /// <remarks>
    /// <para><c>ManpowerBudgetService.RejectAsync(budgetId, reason)</c> took a reason, set the
    /// status to <c>Rejected</c>, and <b>discarded the reason entirely</b> — it reached a log line
    /// and nothing else. A budget holder opening a refused budget could see that it had been
    /// refused and had no way to find out why, which makes the rejection unactionable and the
    /// resubmission guesswork.</para>
    ///
    /// <para>Hand-written, so the discovery attributes are inline and this migration is deliberately
    /// <b>not</b> listed in <c>FastBuildMigrationMetadata</c> — see
    /// <c>20260819020000_FilterCompetencyUniqueIndexesOnIsDeleted</c> for why the two are mutually
    /// exclusive.</para>
    /// </remarks>
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260819040000_AddManpowerBudgetRejectionReason")]
    public partial class AddManpowerBudgetRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectionReason' AND object_id = OBJECT_ID('dbo.ManpowerBudgets'))
    ALTER TABLE [dbo].[ManpowerBudgets] ADD [RejectionReason] nvarchar(1000) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE name = 'RejectionReason' AND object_id = OBJECT_ID('dbo.ManpowerBudgets'))
    ALTER TABLE [dbo].[ManpowerBudgets] DROP COLUMN [RejectionReason];");
        }
    }
}
