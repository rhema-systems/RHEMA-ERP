using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Round 5, lane L2b: FR-HR-152's cap on the days of annual leave a leaver's settlement pays —
    /// a company setting, where it was a constant (56) in <c>SeparationService</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>One column</b>, <c>CompanyHrPolicySettings.SettlementLeaveDaysCap</c> (int, nullable).
    /// Empty means no cap, so the column carries no default constraint: a default would make "no cap"
    /// impossible to tell from "never set".</para>
    ///
    /// <para>⚠ <b>Every existing tenant row gets 56 — the real default, never the scaffold's NULL</b>,
    /// which would have meant "no cap" and quietly changed what every existing tenant pays. The
    /// backfill runs only in the same step that adds the column (through dynamic SQL, so the batch
    /// compiles before the column exists): on a re-run, or on a database the model built, the column
    /// is already there, and a tenant that has since cleared its cap keeps its choice.</para>
    ///
    /// <para>⚠ <b>The scaffold's <c>UpdateData</c> is not here.</b> It also switched the seeded demo
    /// tenant's <c>AllowInServiceEncashment</c> off, because the seed changed (lane L1). A migration
    /// that overwrites a tenant's own setting on every database it reaches is a data decision, not a
    /// schema change: the demo database is switched through the API instead, and a database built
    /// from the model takes the new seed. <c>UpdateData</c> cannot run under the fast EF build either
    /// (<c>migration-ownership-and-chain</c>).</para>
    ///
    /// <para>Proven on a scratch database before it was applied: Up twice, Down twice, Up again.</para>
    /// </remarks>
    public partial class AddSettlementLeaveDaysCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SettlementLeaveDaysCap') IS NULL
BEGIN
    ALTER TABLE [dbo].[CompanyHrPolicySettings] ADD [SettlementLeaveDaysCap] int NULL;
    EXEC(N'UPDATE [dbo].[CompanyHrPolicySettings] SET [SettlementLeaveDaysCap] = 56;');
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CompanyHrPolicySettings', 'SettlementLeaveDaysCap') IS NOT NULL
    ALTER TABLE [dbo].[CompanyHrPolicySettings] DROP COLUMN [SettlementLeaveDaysCap];");
        }
    }
}
