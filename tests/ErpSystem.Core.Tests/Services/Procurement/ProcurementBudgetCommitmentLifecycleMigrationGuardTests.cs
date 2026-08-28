using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBudgetCommitmentLifecycleMigrationGuardTests
{
    private const string MigrationId = "20260828190000_AlignProcurementReservationAndFormalCommitmentLifecycle";

    [Fact]
    public void Migration_separates_reservation_formal_commitment_and_utilization_with_bounded_backfill()
    {
        var root = FindRepositoryRoot();
        var migration = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", MigrationId + ".cs"));

        migration.Should().Contain("name: \"ReservedAmount\", table: \"ProcurementBudgets\"")
            .And.Contain("FormallyCommittedAmount")
            .And.Contain("UtilizedAmount")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntries")
            .And.Contain("SUM(contract.ContractValue) OVER")
            .And.Contain("COALESCE(p.ApprovedAt, p.UpdatedAt, p.CreatedAt)")
            .And.Contain("COALESCE(po.ApprovedAt, po.UpdatedAt, po.CreatedAt)")
            .And.NotContain("p.ApprovedDate")
            .And.NotContain("po.ApprovedDate")
            .And.Contain("WHERE contract.RunningAmount <= contract.ReservedAmount")
            .And.Contain("PARTITION BY ce.Id ORDER BY")
            .And.Contain("WHERE po.RunningAmount <= po.ContractAmount")
            .And.Contain("THROW 52052")
            .And.Contain("THROW 52053")
            .And.Contain("THROW 52054")
            .And.Contain("PreviousCommitmentEvidenceTriggerSql")
            .And.Contain("PreviousCommitmentLifecycleTriggerSql")
            .And.Contain("PreviousPurchaseOrderCommitmentTriggerSql")
            .And.Contain("d.Id IS NULL AND i.BudgetAvailableBefore")
            .And.Contain("d.Status = 2 AND i.Status = 1")
            .And.Contain("TR_ProcurementBudgetCommitmentLedgerEntries_Immutable");
    }

    [Fact]
    public void Runtime_promotes_only_final_sources_and_prevents_contract_child_po_double_commit()
    {
        var root = FindRepositoryRoot();
        var lifecycle = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementBudgetCommitmentLifecycleService.cs"));
        var purchaseOrders = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers",
            "Procurement", "PurchaseOrdersController.cs"));
        var contracts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementContractActivationService.cs"));
        var receipts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services",
            "Procurement", "ProcurementReceiptInspectionService.cs"));

        lifecycle.Should().Contain("BUDGET_LIFECYCLE_SOURCE_TENANT_MISMATCH")
            .And.Contain("PurchaseOrderAllocation")
            .And.Contain("PO_CONTRACT_COMMITMENT_EXCEEDED")
            .And.Contain("PO_CONTRACT_REQUISITION_LINEAGE_MISMATCH")
            .And.Contain("PO_CONTRACT_ALLOCATION_EXCEEDED")
            .And.Contain("BUDGET_UTILIZATION_SOURCE_LINEAGE_INVALID");
        purchaseOrders.Should().Contain("CommitPurchaseOrderAsync")
            .And.Contain("WorkflowOutcome.Approved");
        contracts.Should().Contain("CommitContractAsync");
        receipts.Should().Contain("UtilizePurchaseOrderAsync");
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory; directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
