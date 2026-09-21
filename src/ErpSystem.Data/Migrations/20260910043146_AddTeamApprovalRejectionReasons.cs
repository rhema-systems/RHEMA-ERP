using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Lane F3: why an approver said no — on a committee's charter, and on a team objective
    /// (plan § 1.4, § 6.6, Q-8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>⚠ The scaffolded body was replaced with guarded SQL</b>, as on every HR migration since
    /// <c>20260907003611_AddSheChecklistBuilder</c>: <c>rebuild-db</c> builds from the EF model, so
    /// a rebuilt database already has these columns and a bare <c>AddColumn</c> stops the chain.
    /// </para>
    ///
    /// <para>
    /// <b>Why two columns exist at all, when the plan said F3 needed no migration.</b> Putting the
    /// approval on the engine means a refusal is now a real outcome, and a refusal that keeps no
    /// reason leaves its author knowing only that somebody said no. That is not hypothetical here:
    /// <c>ManpowerBudget.RejectAsync</c> shipped taking a reason, setting the status and discarding
    /// it, and had to be repaired — a budget holder could see they had been refused with no way to
    /// find out why. Both records go back to <c>Draft</c> on a rejection, so the reason is the only
    /// thing that says what to change.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ Deliberately NOT folded into <c>TeamObjective.CancelledReason</c>.</b> An objective
    /// refused before it began and one the team chose to stop pursuing are different facts. One
    /// column cannot tell them apart a year later, and the two are read by different people for
    /// different reasons — the first by the team, to rework it; the second by whoever asks what
    /// happened to the year's plan.
    /// </para>
    ///
    /// <para>
    /// <b>Nullable, with no default and no backfill</b>, and that is correct rather than lazy: NULL
    /// means "never refused", which is true of every row that exists when this runs. This is the
    /// opposite case from <c>TeamTaskReminderLeadDays</c> and <c>CertificationExpiryLeadDays</c>,
    /// where a scaffolded <c>defaultValue: 0</c> would have written a real and wrong number into
    /// every existing row. Both traps are the same question asked of the entity initialiser — here
    /// there is no initialiser, so there is nothing to preserve.
    /// </para>
    /// </remarks>
    public partial class AddTeamApprovalRejectionReasons : Migration
    {
        // ⚠ The table guard is NOT redundant. COL_LENGTH returns NULL both for "no such column" and
        // for "no such table", so the column check alone would fall through to an ALTER against a
        // table that does not exist — the one shape that turns a guarded migration back into an
        // unguarded one.
        private static string AddColumn(string table, string column, string definition) => $@"
IF OBJECT_ID('dbo.{table}', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.{table}', '{column}') IS NULL
    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};";

        private static string DropColumn(string table, string column) => $@"
IF COL_LENGTH('dbo.{table}', '{column}') IS NOT NULL
    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both tables come from 20260910023951 earlier in the chain; a rebuilt database has them
            // from the model. The guards make either ordering safe.
            migrationBuilder.Sql(AddColumn("TeamTermsOfReferences", "RejectionReason", "nvarchar(1000) NULL"));
            migrationBuilder.Sql(AddColumn("TeamObjectives", "RejectionReason", "nvarchar(1000) NULL"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropColumn("TeamObjectives", "RejectionReason"));
            migrationBuilder.Sql(DropColumn("TeamTermsOfReferences", "RejectionReason"));
        }
    }
}
