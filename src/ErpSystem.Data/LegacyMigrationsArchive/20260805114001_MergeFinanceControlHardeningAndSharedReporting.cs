using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev ← master merge that brought in the TDC Finance control
    /// hardening and shared reporting integration (#26).
    /// </summary>
    /// <remarks>
    /// <para>Deliberately a no-op, following <c>20260726181817_MergeFinanceModule</c>. The model
    /// snapshot conflicted textually for the first time in this merge series — both branches inserted
    /// entity blocks at the same alphabetical boundary — so rather than hand-merge a 180k-line generated
    /// file, the conflict was cleared to hrdev's side and the snapshot regenerated against the combined
    /// model by scaffolding this migration. <b>That regenerated snapshot is the real payload here.</b></para>
    ///
    /// <para>The scaffolded body was ~5,000 lines: 39 CreateTable, 97 AddColumn, 217 CreateIndex,
    /// 23 AddForeignKey, 5 DropIndex and 1 AlterColumn. It was removed because every operation in it is
    /// redundant or belongs to another module's drift:</para>
    /// <list type="bullet">
    ///   <item><description>All 39 tables are already created by Finance migrations that came across in
    ///   this merge. Two of them — <c>AllocationRunBatches</c> and <c>AllocationRunBatchLines</c> — are
    ///   created by <c>20260728170400_AddAllocationRunBatches</c> through raw
    ///   <c>IF OBJECT_ID(...) IS NULL CREATE TABLE</c> SQL rather than a <c>CreateTable</c> call, which
    ///   is why a naive scan reports them as new.</description></item>
    ///   <item><description>The columns and indexes are master's long-standing "model ahead of its own
    ///   migrations" condition for the Finance schema, documented on <c>MergeFinanceModule</c>. Master
    ///   is actively working it — <c>20260731140309_RepairMissingFinanceSettingsColumns</c> and
    ///   <c>20260801160552_ReconcileFinanceMasterModelSnapshot</c> both landed in #26.</description></item>
    ///   <item><description>The 5 index drops (two composite ones on <c>ExchangeRates</c>, three
    ///   single-column <c>TenantId</c> indexes on the budgeting tables) and the
    ///   <c>CashTransaction.ExchangeRate</c> precision change are master's snapshot-vs-model drift, on
    ///   Finance tables only.</description></item>
    /// </list>
    ///
    /// <para>Every one of the 499 identifiers the body referenced is a Finance object; not one is an HR
    /// table. Keeping the body would have had an HR merge silently re-create objects that already exist
    /// and reshape another team's schema — including dropping two composite <c>ExchangeRates</c> indexes
    /// that look deliberate for FX lookups. That is the Finance owner's call, not this merge's.</para>
    ///
    /// <para>Follow-up for the Finance owner, unchanged from <c>MergeFinanceModule</c>: master's model
    /// remains ahead of its migrations, so a <c>Database.Migrate()</c> build of the Finance schema is
    /// still incomplete and only <c>rebuild-db</c> (EnsureCreated from the model) produces it in full.
    /// Separately, <c>20260719120000_FixFinanceWorkflowConformance</c> carries no <c>[Migration]</c>
    /// attribute, has no generated designer, and is absent from
    /// <c>Migrations/FastBuildMigrationMetadata.cs</c>, so EF cannot discover it in any build
    /// configuration and its repair has never run — on master either.</para>
    /// </remarks>
    public partial class MergeFinanceControlHardeningAndSharedReporting : Migration
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
