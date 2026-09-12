using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912113000_AllowOptionalProcurementApproval")]
public sealed class AllowOptionalProcurementApproval : Migration
{
    private static readonly (string Table, string Entity, string Initial, string ApprovedAt)[] Targets =
    [
        ("PurchaseRequisitions", "PurchaseRequisition", "Draft", "ApprovedAt"),
        ("PurchaseOrders", "PurchaseOrder", "Draft", "ApprovedAt"),
        ("ProcurementBudgets", "ProcurementBudget", "Draft", "ApprovedDate"),
        ("ProcurementPlans", "ProcurementPlan", "Draft", "ApprovedDate"),
        ("ProcurementBudgetRevisions", "ProcurementBudgetRevision", "Pending", "ApprovedDate"),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var target in Targets)
            migrationBuilder.AddColumn<bool>("ApprovalRequired", target.Table, type: "bit", nullable: false, defaultValue: true);

        foreach (var target in Targets)
            migrationBuilder.Sql(PolicyGuard(target.Table, target.Entity, target.Initial, target.ApprovedAt));

        PatchTrigger(migrationBuilder, "TR_ProcurementRequisitionSourcingReleases_TenantGuard",
            "OR pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL",
            "OR (pr.[ApprovalRequired] = 1 AND (pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL))");
        PatchTrigger(migrationBuilder, "TR_PurchaseOrders_GovernedCommitment",
            "OR b.ApprovedById IS NULL OR b.ApprovedDate IS NULL",
            "OR (b.ApprovalRequired = 1 AND (b.ApprovedById IS NULL OR b.ApprovedDate IS NULL))");
        PatchTrigger(migrationBuilder, "TR_PurchaseOrders_SodHardStop",
            "WHERE purchaseOrder.IsDeleted = 0",
            "WHERE purchaseOrder.IsDeleted = 0 AND purchaseOrder.ApprovalRequired = 1");
    }

    private static string PolicyGuard(string table, string entity, string initial, string approvedAt) => $$"""
        CREATE OR ALTER TRIGGER [dbo].[TR_{{table}}_ApprovalPolicy]
        ON [dbo].[{{table}}] AFTER INSERT, UPDATE
        AS
        BEGIN
            SET NOCOUNT ON;
            -- Only the direct finalization transition captures false. Draft DTOs cannot preselect it,
            -- historical/pending approvals cannot be reclassified, and later edits cannot rewrite it.
            IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                WHERE (d.Id IS NULL AND i.ApprovalRequired = 0)
                   OR (d.Id IS NOT NULL AND i.ApprovalRequired <> d.ApprovalRequired
                       AND NOT (d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                           AND d.Status = N'{{initial}}' AND i.Status = N'Approved'
                           AND d.ApprovedById IS NULL AND d.[{{approvedAt}}] IS NULL)))
                THROW 52530, 'PROCUREMENT_APPROVAL_POLICY_IMMUTABLE: the submission approval requirement cannot be rewritten.', 1;

            IF EXISTS (SELECT 1 FROM inserted i WHERE i.ApprovalRequired = 0
                AND (i.ApprovedById IS NOT NULL OR i.[{{approvedAt}}] IS NOT NULL))
                THROW 52531, 'PROCUREMENT_DIRECT_APPROVAL_METADATA: direct completion must not claim a human approval.', 1;

            IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE d.ApprovalRequired = 1 AND i.ApprovalRequired = 0
                  AND dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId, N'{{entity}}', i.Id) = 1)
                THROW 52532, 'PROCUREMENT_APPROVAL_STILL_REQUIRED: an active approval route or existing instance must be completed.', 1;
        END;
        """;

    private static void PatchTrigger(MigrationBuilder builder, string trigger, string before, string after)
    {
        var oldSql = before.Replace("'", "''");
        var newSql = after.Replace("'", "''");
        builder.Sql($$"""
            DECLARE @definition nvarchar(max) = OBJECT_DEFINITION(OBJECT_ID(N'dbo.{{trigger}}'));
            IF @definition IS NULL OR CHARINDEX(N'{{oldSql}}', @definition) = 0
                THROW 52533, 'PROCUREMENT_APPROVAL_TRIGGER_BASELINE: expected protected trigger was not found; no guards were changed.', 1;
            SET @definition = REPLACE(@definition, N'{{oldSql}}', N'{{newSql}}');
            DECLARE @start int = CHARINDEX(N'TRIGGER', UPPER(@definition));
            IF @start = 0 THROW 52533, 'PROCUREMENT_APPROVAL_TRIGGER_BASELINE: invalid trigger declaration.', 1;
            SET @definition = N'ALTER ' + SUBSTRING(@definition, @start, LEN(@definition));
            EXEC sys.sp_executesql @definition;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A downgrade cannot manufacture missing approvers for direct-finalized records.
        foreach (var target in Targets)
            migrationBuilder.Sql($"IF EXISTS (SELECT 1 FROM dbo.[{target.Table}] WHERE ApprovalRequired = 0) THROW 52534, 'Direct-finalized procurement records prevent this approval-policy downgrade.', 1;");

        PatchTrigger(migrationBuilder, "TR_ProcurementRequisitionSourcingReleases_TenantGuard",
            "OR (pr.[ApprovalRequired] = 1 AND (pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL))",
            "OR pr.[ApprovedAt] IS NULL OR pr.[ApprovedById] IS NULL");
        PatchTrigger(migrationBuilder, "TR_PurchaseOrders_GovernedCommitment",
            "OR (b.ApprovalRequired = 1 AND (b.ApprovedById IS NULL OR b.ApprovedDate IS NULL))",
            "OR b.ApprovedById IS NULL OR b.ApprovedDate IS NULL");
        PatchTrigger(migrationBuilder, "TR_PurchaseOrders_SodHardStop",
            "WHERE purchaseOrder.IsDeleted = 0 AND purchaseOrder.ApprovalRequired = 1",
            "WHERE purchaseOrder.IsDeleted = 0");
        foreach (var target in Targets)
        {
            migrationBuilder.Sql($"DROP TRIGGER [dbo].[TR_{target.Table}_ApprovalPolicy];");
            migrationBuilder.DropColumn("ApprovalRequired", target.Table);
        }
    }
}
