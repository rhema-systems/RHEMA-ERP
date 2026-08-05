using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev (HR module port) ← master merge.
    /// </summary>
    /// <remarks>
    /// <para>Deliberately a no-op. Both branches added migrations after the merge base: master added the
    /// Oracle payroll set plus workflow / procurement / fleet-inspection changes, while hrdev added the
    /// HR module port. <c>ApplicationDbContextModelSnapshot</c> could only be taken from one side during
    /// the merge (hrdev's was kept), so it did not describe master's entities.</para>
    ///
    /// <para>Scaffolding this migration regenerated the snapshot against the merged model, which is the
    /// only reason it exists. Its scaffolded body was 66 CreateTable + 62 AddColumn operations, every one
    /// of which is already produced by a migration that came across in the merge — the payroll tables by
    /// <c>AddHrPayrollLegacySetupSlices</c>, <c>AddHrPayrollBackpaySetup</c> and
    /// <c>AddHrPayrollBudgetAnalysisRows</c>; the columns by <c>AddWorkflowGovernanceOperations</c>,
    /// <c>AddWorkflowDefinitionLifecycle</c>, <c>AddInspectionTemplateFleetMobileAssignments</c> and
    /// <c>AddMaintenanceAssetMovementAndOfflineFleetInspections</c>. Applying it would have attempted to
    /// recreate objects that already exist, so the body was removed and the regenerated snapshot kept.</para>
    ///
    /// <para>Note that the supported way to build a dev database here is
    /// <c>dotnet run --project src/ErpSystem.Api rebuild-db</c>, which calls <c>EnsureCreated</c> against
    /// the current model and then stamps every known migration as applied — it does not execute migrations.</para>
    /// </remarks>
    public partial class MergeHrPortWithMasterPayroll : Migration
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
