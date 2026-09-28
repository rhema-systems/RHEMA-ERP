using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Merge #11 follow-up: <c>TeamObjectives.TargetValue</c> is decimal(18,4) on every database,
    /// not only on the ones built from the model.
    /// </summary>
    /// <remarks>
    /// <para><b>Why.</b> <c>20260910023951_AddTeamTermsObjectivesAndTasks</c> wrote the column as
    /// decimal(18,2), faithfully copying the entity's <c>[Column(TypeName)]</c>. But
    /// <c>ConfigureDecimalPrecision</c> runs after that attribute and makes the model 18,4, so the
    /// model, the snapshot and every database built from the model have 18,4, while a database built
    /// by the chain has 18,2. Since master archived that migration and replays it from
    /// <c>20260922210000_ReconcilePreBaselineLateMerges</c>, the chain is how fresh databases are now
    /// built: master's from-empty gate, the VPS cutover and <c>New-UatDatabase.ps1</c>. On those, a
    /// target of 12.3456 was stored as 12.35. Found at merge #11 by comparing a chain-built and a
    /// model-built scratch database column for column; it was the only HR difference.</para>
    ///
    /// <para><b>No model change, so the scaffold was empty</b> and the snapshot is untouched. The
    /// entity's attribute now reads 18,4 as well, so the code states the type the model already
    /// had.</para>
    ///
    /// <para><b>Guarded</b>, like every HR migration: it alters the column only where it exists and is
    /// not already decimal(18,4), so it is a no-op on a model-built database. Nothing indexes or
    /// constrains the column (checked), so the ALTER needs nothing dropped first. Moving the scale
    /// from 2 to 4 narrows the integer part from 16 digits to 14; no team target comes near
    /// 10^14.</para>
    ///
    /// <para><b>Down does nothing, on purpose.</b> 18,2 was never the model's type, so rolling back
    /// to it would reintroduce the drift, and on a model-built database it would narrow a column that
    /// was never 18,2 at all. Leaving 18,4 in place loses nothing.</para>
    /// </remarks>
    public partial class AlignTeamObjectiveTargetValuePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1
           FROM sys.columns c
           JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(N'dbo.TeamObjectives')
             AND c.name = N'TargetValue'
             AND NOT (t.name = N'decimal' AND c.precision = 18 AND c.scale = 4))
    ALTER TABLE [dbo].[TeamObjectives] ALTER COLUMN [TargetValue] decimal(18,4) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty; see the remarks.
        }
    }
}
