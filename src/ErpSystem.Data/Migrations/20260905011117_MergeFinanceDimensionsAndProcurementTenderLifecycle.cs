using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev ← master merge that brought in the finance dimension and
    /// budget-control work, the governed procurement tender lifecycle, the inventory/quantity-survey/
    /// civil assurance sweeps and estate property and facilities planning (PRs #122–#168).
    /// </summary>
    /// <remarks>
    /// <para>Like <c>20260731143111_MergeProcurementPhase4AndEstateReadiness</c>, this marker is NOT a
    /// no-op, and for the same reason. Regenerating the snapshot against the combined model produced
    /// exactly one operation: dropping a redundant single-column <c>TenantId</c> index.</para>
    ///
    /// <para>Why it is redundant, and why this is not a merge regression:
    /// <c>BusinessPartnerBankAccounts</c> is created by the hand-written
    /// <c>20260903120000_AddBusinessPartnerBankAccounts</c>, which creates
    /// <c>IX_BusinessPartnerBankAccounts_TenantId</c> alongside composite indexes whose LEADING column
    /// is already <c>TenantId</c> — <c>(TenantId, BusinessPartnerId)</c> and the filtered unique
    /// <c>(TenantId, BusinessPartnerId, IsPrimary)</c>. EF's foreign-key index convention does not emit
    /// a separate index when an existing one already covers the FK columns, so the model has never
    /// contained it. Master's snapshot lists it only because the migration and the snapshot entry were
    /// authored by hand; master's own model does not produce it, and <c>has-pending-model-changes</c>
    /// on master alone would propose this same drop.</para>
    ///
    /// <para>The Tenant foreign key itself is untouched — only the duplicate index is removed, so tenant
    /// referential integrity and tenant-scoped query plans are unaffected (SQL Server seeks on the
    /// leading column of the composite indexes). Blanking this body was rejected for the reason recorded
    /// on merge #5: it would leave the model and the database permanently out of step, and every future
    /// merge would re-propose the drop.</para>
    ///
    /// <para>Both statements are guarded so they are safe on databases built by the migration chain
    /// (index present) and on databases built by <c>rebuild-db</c>/EnsureCreated (index never created),
    /// matching the defensive style of the other merge markers.</para>
    ///
    /// <para>Follow-up for the procurement owner (not this merge's job): either drop
    /// <c>IX_BusinessPartnerBankAccounts_TenantId</c> from
    /// <c>20260903120000_AddBusinessPartnerBankAccounts</c> and its snapshot entry, or declare the
    /// single-column index on the model deliberately if it is wanted. See also the still-open Finance
    /// debt noted on <c>20260726181817_MergeFinanceModule</c>.</para>
    ///
    /// <para>Separately, and the reason the snapshot had to be regenerated rather than trusted: git's
    /// textual auto-merge of <c>ApplicationDbContextModelSnapshot.cs</c> had silently replaced master's
    /// <c>b.ToTable("TenderEvaluations", null, t =&gt; t.HasTrigger("TR_TenderEvaluations_CommitteeScoreProjection"))</c>
    /// and its <c>SqlServer:UseSqlOutputClause = false</c> annotation with a plain
    /// <c>b.ToTable("TenderEvaluations")</c>. Without those, EF emits an OUTPUT clause against a table
    /// that carries a trigger and every save to <c>TenderEvaluations</c> throws. Regeneration restored
    /// both; the entity set is otherwise identical on either side of the regeneration (1,603 entities,
    /// none added, none removed).</para>
    /// </remarks>
    public partial class MergeFinanceDimensionsAndProcurementTenderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "BusinessPartnerBankAccounts", "IX_BusinessPartnerBankAccounts_TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            CreateIndexIfMissing(migrationBuilder, "BusinessPartnerBankAccounts", "IX_BusinessPartnerBankAccounts_TenantId");
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql($@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    DROP INDEX [{index}] ON [dbo].[{table}];");
        }

        private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[dbo].[{table}]') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{index}' AND object_id = OBJECT_ID(N'[dbo].[{table}]'))
    CREATE INDEX [{index}] ON [dbo].[{table}] ([TenantId]);");
        }
    }
}
