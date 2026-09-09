using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Demo feedback round 2, lane B1 (docs/HR/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md § 5): a <c>Notes</c>
    /// column on the organisation-unit change log — the "any other ones" of O-3b, beside the
    /// reason and the effective dates that became the user's in the same slice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b> — the same rewrite as
    /// <c>20260909004030_AddGuarantorIdTypeAndDocuments</c>. <c>rebuild-db</c> builds from the EF
    /// model rather than the migration chain, so a rebuilt database already has the column and a
    /// bare <c>AddColumn</c> fails on it.
    /// </para>
    /// <para>
    /// Schema only. The dates and reason already existed; nothing is backfilled, and the rows the
    /// system wrote before this slice keep their recorded-on-that-day <c>EffectiveFrom</c> — the
    /// history PUT that arrived with this slice is how a user corrects one.
    /// </para>
    /// </remarks>
    public partial class AddOrganizationUnitHistoryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrganizationUnitHistories', 'Notes') IS NULL
    ALTER TABLE [dbo].[OrganizationUnitHistories] ADD [Notes] nvarchar(2000) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.OrganizationUnitHistories', 'Notes') IS NOT NULL
    ALTER TABLE [dbo].[OrganizationUnitHistories] DROP COLUMN [Notes];");
        }
    }
}
