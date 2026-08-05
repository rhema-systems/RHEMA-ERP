using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Snapshot reconciliation for the hrdev ← master merge that brought in procurement frameworks and
    /// call-offs (#20), estate land project-readiness (#21), and the remaining Phase 4 procurement
    /// controls (#22).
    /// </summary>
    /// <remarks>
    /// <para>Unlike the earlier merge markers, this one is NOT a no-op — but its body is deliberately
    /// tiny. Regenerating the snapshot against the combined model produced exactly two operations, both
    /// dropping a redundant single-column <c>TenantId</c> index.</para>
    ///
    /// <para>Why they are redundant, and why this is not a merge regression: both tables are created by
    /// the hand-written <c>20260730171500_TDC0409WorksCloseoutControl</c>, which explicitly creates
    /// <c>IX_..._TenantId</c> AND composite indexes whose LEADING column is already <c>TenantId</c>
    /// (<c>(TenantId, ContractId, Sequence)</c>, <c>(TenantId, ActionId, RequirementKey)</c>, …). EF's
    /// foreign-key index convention does not emit a separate index when an existing index already covers
    /// the FK columns, so the model has never contained these two. Master's snapshot lists them because
    /// the migration and the snapshot entry were authored by hand; master's own model does not produce
    /// them, and <c>has-pending-model-changes</c> on master would propose the same two drops.</para>
    ///
    /// <para>The Tenant foreign key itself is untouched and identical on both branches — only the
    /// duplicate index is removed, so tenant referential integrity and tenant-scoped query plans are
    /// unaffected (SQL Server seeks on the leading column of the composite indexes). Blanking this body
    /// was rejected: it would leave the model and the database permanently out of step and every future
    /// merge would re-propose these drops.</para>
    ///
    /// <para>Both statements are guarded so they are safe on databases built by the migration chain
    /// (indexes present) and on databases built by <c>rebuild-db</c>/EnsureCreated (indexes never
    /// created), matching the defensive style used by the other merge migrations.</para>
    ///
    /// <para>Follow-up for the procurement owner (not this merge's job): reconcile master's snapshot
    /// entry for <c>ProcurementWorksCloseoutAction</c>/<c>ProcurementWorksCloseoutEvidence</c> with what
    /// the model actually produces, or add the single-column indexes to the model deliberately if they
    /// are wanted. See also the still-open Finance debt noted on
    /// <c>20260726181817_MergeFinanceModule</c>.</para>
    /// </remarks>
    public partial class MergeProcurementPhase4AndEstateReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "ProcurementWorksCloseoutEvidence", "IX_ProcurementWorksCloseoutEvidence_TenantId");
            DropIndexIfExists(migrationBuilder, "ProcurementWorksCloseoutActions", "IX_ProcurementWorksCloseoutActions_TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            CreateIndexIfMissing(migrationBuilder, "ProcurementWorksCloseoutEvidence", "IX_ProcurementWorksCloseoutEvidence_TenantId");
            CreateIndexIfMissing(migrationBuilder, "ProcurementWorksCloseoutActions", "IX_ProcurementWorksCloseoutActions_TenantId");
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
