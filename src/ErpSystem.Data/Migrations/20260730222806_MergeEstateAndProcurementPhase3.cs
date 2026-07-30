using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Merge marker for the hrdev ← master merge that brought in estate management and document
    /// workflows (#12) and procurement supplier controls through Phase 3 (#16).
    /// </summary>
    /// <remarks>
    /// <para>Deliberately a no-op, and — unlike the earlier merge no-ops — a no-op in a stronger sense.
    /// Scaffolding it produced an empty body AND left
    /// <c>ApplicationDbContextModelSnapshot</c> byte-for-byte unchanged, so this merge needed no snapshot
    /// reconciliation at all: the textual merge of the snapshot already described the combined model
    /// exactly. <c>migrations has-pending-model-changes</c> reports "No changes"; because that check reads
    /// the snapshot this migration left untouched, its verdict is unaffected by this migration's presence.
    /// It is kept only as a legible marker of the merge point in the chain.</para>
    ///
    /// <para>Why no reconciliation was needed here: the 35 migrations master contributed (estate, central
    /// DMS, procurement supplier controls) are additive and carried their own snapshot entries across the
    /// merge, and the places where the two snapshots disagreed were HR entities whose master-side shape
    /// the HR port had already superseded — so the merged text was the correct one.</para>
    ///
    /// <para>Compare the earlier merge no-ops, where the scaffolded body WAS large and the regenerated
    /// snapshot was the real payload: <c>20260722202144_MergeHrPortWithMasterPayroll</c>,
    /// <c>20260723234640_MergeProcurementPhase0</c>, <c>20260726181817_MergeFinanceModule</c>. The
    /// pre-existing Finance debt documented on that last one is unchanged by this merge and still open:
    /// master's model remains ahead of its migrations for ~21 Finance tables.</para>
    /// </remarks>
    public partial class MergeEstateAndProcurementPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see the remarks on this class.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see the remarks on this class.
        }
    }
}
