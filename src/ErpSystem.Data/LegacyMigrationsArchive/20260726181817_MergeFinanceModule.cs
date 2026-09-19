using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev ← master merge that brought in the Finance module
    /// integration foundation (#8).
    /// </summary>
    /// <remarks>
    /// <para>Deliberately a no-op. Neither branch's <c>ApplicationDbContextModelSnapshot</c> described
    /// the other's entities (hrdev has the HR port, master has Finance), so after the merge the snapshot
    /// was regenerated against the combined model. Scaffolding this migration is what regenerated it —
    /// that regenerated snapshot is the real payload here.</para>
    ///
    /// <para>The scaffolded body was large (≈40 CreateTable, ≈191 AddColumn, plus the
    /// CustomerId → BusinessPartnerId renames and the Finance index rework). Every operation is one of:
    /// (a) already performed by a Finance migration that came across in the merge — e.g. the renames by
    /// <c>20260717090000_UseBusinessPartnersForSalesReturnAccounting</c>; or (b) a table that has no
    /// CreateTable migration on master either. The latter is a pre-existing condition on master: its
    /// snapshot lists ~21 Finance tables (AllocationRules, AccountingBooks, UnitTypes,
    /// RecurringJournalTemplates, UnitAccounts, …, introduced by "finance core updated (#3)") whose
    /// CreateTable migration was never authored, so the Finance schema is materialised via
    /// <c>rebuild-db</c> (EnsureCreated from the model), not by the migration chain. Applying the
    /// scaffolded body would therefore both re-create objects that already exist and diverge from how
    /// master builds. The body was removed and the regenerated snapshot kept.</para>
    ///
    /// <para>Follow-up (not this merge's job): master's model is ahead of its migrations for those ~21
    /// Finance tables — worth a dedicated migration on the Finance side so a <c>Database.Migrate()</c>
    /// build is complete. See also the equivalent no-ops
    /// <c>20260722202144_MergeHrPortWithMasterPayroll</c> and <c>20260723234640_MergeProcurementPhase0</c>.</para>
    /// </remarks>
    public partial class MergeFinanceModule : Migration
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
