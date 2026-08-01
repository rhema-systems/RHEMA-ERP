using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReviewRegressionTests
{
    [Fact]
    public async Task Generic_repository_preserves_preassigned_aggregate_identity()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = new GenericRepository<Warehouse>(context);
        var expectedId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            Id = expectedId,
            TenantId = tenantId,
            Code = "ID-001",
            Name = "Identity regression warehouse"
        };

        await repository.AddAsync(warehouse);

        warehouse.Id.Should().Be(expectedId);
        context.Entry(warehouse).Property(item => item.Id).CurrentValue
            .Should().Be(expectedId);
    }

    [Fact]
    public async Task Generic_repository_generates_identity_only_when_explicitly_empty()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = new GenericRepository<Warehouse>(context);
        var preservedId = Guid.NewGuid();
        var notifications = new[]
        {
            NewWarehouse(tenantId, preservedId),
            NewWarehouse(tenantId, Guid.Empty)
        };

        await repository.AddRangeAsync(notifications);

        notifications[0].Id.Should().Be(preservedId);
        notifications[1].Id.Should().NotBe(Guid.Empty);
        notifications[1].Id.Should().NotBe(preservedId);
    }

    [Fact]
    public void Purchase_order_source_binding_is_guarded_by_a_transactional_outbox()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementPurchaseOrderSourceService.cs");

        source.Should().Contain("PO_SOURCE_BOUND_TRANSACTION_REQUIRED");
        source.Should().Contain("NotificationTopicPublisher only appends durable Notification queue rows");
        source.Should().Contain("await PublishNotificationAsync(");
    }

    [Fact]
    public void Plan_item_purchase_order_conversion_maps_source_authorization_to_forbidden()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Controllers", "Procurement",
            "ProcurementPlansController.cs");

        source.Should().Contain(
            "catch (ProcurementPurchaseOrderSourceAuthorizationException ex)");
        source.Should().Contain(
            "catch (ProcurementAccessAuthorizationException ex)");
        source.Should().Contain("StatusCodes.Status403Forbidden");
        source.Should().Contain("PO_SOURCE_FORBIDDEN");
    }

    [Fact]
    public void Final_receipt_inspection_workflow_decision_is_inside_atomic_outcome_scope()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var decideStart = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> DecideAsync",
            StringComparison.Ordinal);
        var executeStart = source.IndexOf(
            "await ExecuteAsync(async () =>",
            decideStart,
            StringComparison.Ordinal);
        var workflowDecision = source.IndexOf(
            "_workflow.ProcessApprovalAsync",
            decideStart,
            StringComparison.Ordinal);
        var outcomeApplication = source.IndexOf(
            "ApplyAcceptedQuantitiesAndStockAsync",
            decideStart,
            StringComparison.Ordinal);
        var decideEnd = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> AcknowledgeAsync",
            decideStart,
            StringComparison.Ordinal);

        decideStart.Should().BeGreaterThanOrEqualTo(0);
        executeStart.Should().BeGreaterThan(decideStart);
        workflowDecision.Should().BeGreaterThan(executeStart,
            "the shared-workflow decision must be consumed inside the serializable transaction");
        outcomeApplication.Should().BeGreaterThan(workflowDecision,
            "stock and inspection outcome application must follow workflow processing in the same transaction");
        outcomeApplication.Should().BeLessThan(decideEnd);
        source[decideStart..executeStart].Should().NotContain("_workflow.ProcessApprovalAsync");
    }

    [Fact]
    public void Receipt_inspection_evidence_is_source_bound_and_revalidated_before_stock()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        source.Should().Contain("workflow.EntityId == inspection.Id");
        source.Should().Contain("workflow.EntityId == inspection.PurchaseOrderReceiptId");
        source.Should().Contain(
            "item.DocumentRecord.SourceRecordId == inspection.PurchaseOrderReceiptId");
        source.Should().Contain("private async Task RevalidateEvidenceAsync(");
        source.Should().Contain("RCV_EVIDENCE_STALE");

        var decisionStart = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> DecideAsync",
            StringComparison.Ordinal);
        var evidenceRevalidation = source.IndexOf(
            "await RevalidateEvidenceAsync(inspection, cancellationToken);",
            decisionStart,
            StringComparison.Ordinal);
        var stockPosting = source.IndexOf(
            "await ApplyAcceptedQuantitiesAndStockAsync(inspection, cancellationToken);",
            decisionStart,
            StringComparison.Ordinal);

        evidenceRevalidation.Should().BeGreaterThan(decisionStart);
        stockPosting.Should().BeGreaterThan(evidenceRevalidation,
            "current evidence state and hashes must be checked inside final approval before stock posting");
    }

    [Fact]
    public void Receipt_source_pending_quantities_are_scoped_to_each_strategy_attempt()
    {
        var contract = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Interfaces", "Procurement",
            "IProcurementReceiptSourceControlService.cs");
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptSourceControlService.cs");
        var inspection = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        contract.Should().Contain("void ResetInventoryPostingAttempt();");
        source.Should().Contain("_pendingInventoryPostingQuantities.Clear();");
        var strategyStart = inspection.IndexOf(
            "await _unitOfWork.ExecuteInStrategyAsync(async () =>",
            StringComparison.Ordinal);
        var reset = inspection.IndexOf(
            "_sourceControl.ResetInventoryPostingAttempt();",
            strategyStart,
            StringComparison.Ordinal);
        var transaction = inspection.IndexOf(
            "await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable",
            strategyStart,
            StringComparison.Ordinal);

        reset.Should().BeGreaterThan(strategyStart);
        reset.Should().BeLessThan(transaction,
            "a retry must discard quantities accumulated by its rolled-back predecessor");
    }

    [Fact]
    public void Procurement_finance_reconciliation_reconstructs_invoice_state_at_cutoff()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "ApReportsService.cs");

        source.Should().Contain("item.SubmittedDate.Value < cutoffExclusive");
        source.Should().Contain("item.ApprovedDate.Value < cutoffExclusive");
        source.Should().Contain("FinanceAuditEvents.ApInvoiceVoided");
        source.Should().Contain("FinanceAuditEvents.ApInvoiceReversed");
        source.Should().Contain("reversedInvoiceIdsAsOf");
    }

    [Fact]
    public void Receipt_document_actions_are_actor_aware_and_require_complete_signatures()
    {
        var service = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");
        var component = ReadRepositoryFile(
            "frontend", "src", "components", "procurement",
            "ReceiptDocumentControl.tsx");

        service.Should().Contain("_access.CheckCapabilityAsync");
        service.Should().Contain("AllowedSignatureRoles = allowedSignatureRoles");
        service.Should().Contain("AllowedActions(item, manageAllowed, issueAllowed");
        component.Should().Contain("overview.allowedActions.includes('ensure')");
        component.Should().Contain("document.allowedSignatureRoles.filter");
        component.Should().Contain("allowedActions.has('issue')");
        component.Should().Contain("roles.length > 0");
    }

    private static Warehouse NewWarehouse(Guid tenantId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        Code = $"ID-{Guid.NewGuid():N}"[..12],
        Name = "Identity regression warehouse"
    };

    private static string ReadRepositoryFile(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root should be discoverable");
        return File.ReadAllText(Path.Combine([directory!.FullName, .. path]));
    }
}
