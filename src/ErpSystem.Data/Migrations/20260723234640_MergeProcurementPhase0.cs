using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev ← master merge that brought in procurement Phase 0 (#13).
    /// </summary>
    /// <remarks>
    /// <para>Deliberately a no-op. Merging master into hrdev auto-merged
    /// <c>ApplicationDbContextModelSnapshot</c> textually, and that merge silently dropped two
    /// <c>HasIndex</c> entries on <c>WorkflowDelegations</c> (<c>WorkflowDefinitionId</c> and
    /// <c>WorkflowStepId</c>). Scaffolding this migration regenerated the snapshot against the merged
    /// model, restoring both — which is the only reason it exists.</para>
    ///
    /// <para>Its scaffolded body was two <c>CreateIndex</c> calls for exactly those indexes, both of
    /// which are already created by <c>20260707170000_AddWorkflowDelegationScopedWorkflowSteps</c>, a
    /// migration already present in this branch's history. Applying them would fail against a database
    /// where that migration has run, so the body was removed and the regenerated snapshot kept.</para>
    ///
    /// <para>See also <c>20260722202144_MergeHrPortWithMasterPayroll</c>, the equivalent no-op from the
    /// previous master merge. As noted there, <c>rebuild-db</c> builds the schema from the model via
    /// <c>EnsureCreated</c> and stamps migrations as applied rather than executing them.</para>
    /// </remarks>
    public partial class MergeProcurementPhase0 : Migration
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
