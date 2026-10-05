using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Performance closure D-14 (lane E, slice E-c): the appraisal cycle status <c>InProgress</c> (3) is
    /// retired, and every cycle at it moves to Open (2).
    /// </summary>
    /// <remarks>
    /// <para>Only the demo seeder ever wrote InProgress. The checks that ask whether a cycle is running
    /// listed it beside Open — except the HR review's <c>IsCycleActive</c>, which no screen reads. Batch 1
    /// made this repair once; the rebuild that followed seeded the demo cycle at InProgress again, so it is
    /// made again here, now that the enum member is gone and the seeder writes Open. On a database built
    /// from empty the table is empty when this runs.</para>
    ///
    /// <para>Data only: the model does not change — the status is an int column.</para>
    ///
    /// <para>⚠ <b>The scaffold carried eleven operations of Estate's</b> (two tables, five columns, four
    /// indexes: #266 hand-wrote four migrations and never updated the snapshot). They are not this
    /// migration's and are left out; Estate's own migrations create those objects, and the regenerated
    /// snapshot now records them. The snapshot also moved <c>JournalBatch.AccountingBookId</c> into
    /// alphabetical order — no change to the model.</para>
    ///
    /// <para>Down changes nothing: which cycles were InProgress was not kept, and the code before this
    /// migration reads an Open cycle as running wherever it read an InProgress one.</para>
    ///
    /// <para>Guarded SQL, as on every HR migration: it checks for the table it changes, so a database that
    /// lacks it skips the statement.</para>
    /// </remarks>
    public partial class PerformanceClosureRetireCycleInProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID('dbo.AppraisalCycles', 'U') IS NOT NULL
    UPDATE [dbo].[AppraisalCycles] SET [Status] = 2 WHERE [Status] = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: see the remarks.
        }
    }
}
