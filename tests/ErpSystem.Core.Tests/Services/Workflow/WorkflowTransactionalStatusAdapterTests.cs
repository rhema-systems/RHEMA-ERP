using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowTransactionalStatusAdapterTests
{
    [Fact]
    public void Transactional_default_catalog_aliases_have_concrete_adapters()
    {
        var adapters = typeof(WorkflowStatusAdapterRegistry).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(IWorkflowStatusAdapter).IsAssignableFrom(type))
            .Select(type => (IWorkflowStatusAdapter)Activator.CreateInstance(type)!)
            .ToList();
        var registry = new WorkflowStatusAdapterRegistry(adapters);
        var expectedAliases = new[]
        {
            "WorkOrder", "JobCard", "FleetTrip", "FleetTripInspection", "PurchaseOrder",
            "ProcurementPlan", "ProcurementBudget", "PurchaseRequisition", "Tender", "RFQ", "SupplierQuote", "Bid",
            "Evaluation", "InventoryTransfer", "InventoryRequisition", "PayrollRun", "Project",
            "ProjectDeliverable", "ProjectClosure", "Customer", "SalesOrder", "SalesAgreement",
            "SalesAllocation", "Refund", "CreditNote", "BusinessPartner", "Vendor", "ServiceRequest",
            "EhcTicket", "EHC_TICKET"
        };

        var unsupportedAliases = expectedAliases
            .Where(alias => !registry.TryGetAdapter(alias, out _))
            .ToList();

        unsupportedAliases.Should().BeEmpty();
    }

    [Fact]
    public void Work_order_adapter_preserves_approval_metadata()
    {
        var userId = Guid.NewGuid();
        var entity = new WorkOrder();
        var adapter = new WorkOrderWorkflowStatusAdapter();

        adapter.ApplySubmitOutcome(entity, WorkflowOutcome.Pending, userId);
        entity.Status.Should().Be("PendingApproval");
        entity.ApprovedById.Should().BeNull();

        adapter.ApplyApprovalOutcome(entity, WorkflowOutcome.Approved, userId);
        entity.Status.Should().Be("Approved");
        entity.ApprovedById.Should().Be(userId);
        entity.ApprovedAt.Should().NotBeNull();

        adapter.ApplyRecallOutcome(entity, userId);
        entity.Status.Should().Be("Draft");
        entity.ApprovedById.Should().BeNull();
    }

    [Fact]
    public void Procurement_transaction_adapters_apply_native_statuses()
    {
        var rfq = new RequestForQuotation();
        var quote = new RequestForQuotationQuote();
        var bid = new TenderBid();
        var evaluation = new TenderEvaluation();

        new RequestForQuotationWorkflowStatusAdapter().ApplyApprovalOutcome(rfq, WorkflowOutcome.Approved, null);
        new SupplierQuoteWorkflowStatusAdapter().ApplyApprovalOutcome(quote, WorkflowOutcome.Approved, null);
        new TenderBidWorkflowStatusAdapter().ApplyApprovalOutcome(bid, WorkflowOutcome.Rejected, null);
        new TenderEvaluationWorkflowStatusAdapter().ApplySubmitOutcome(evaluation, WorkflowOutcome.Pending, null);

        rfq.Status.Should().Be("Approved");
        quote.Status.Should().Be("Accepted");
        bid.Status.Should().Be("Rejected");
        evaluation.Status.Should().Be("Submitted");
    }

    [Fact]
    public void Procurement_budget_adapter_requires_workflow_outcomes_for_its_lifecycle()
    {
        var budget = new ProcurementBudget { Status = "Draft" };
        var approverId = Guid.NewGuid();
        var adapter = new ProcurementBudgetWorkflowStatusAdapter();

        adapter.ApplySubmitOutcome(budget, WorkflowOutcome.Pending, Guid.NewGuid());
        budget.Status.Should().Be("Submitted");
        budget.ApprovedById.Should().BeNull();

        adapter.ApplyApprovalOutcome(budget, WorkflowOutcome.Approved, approverId);
        budget.Status.Should().Be("Approved");
        budget.ApprovedById.Should().Be(approverId);
        budget.ApprovedDate.Should().NotBeNull();

        adapter.ApplyRecallOutcome(budget, approverId);
        budget.Status.Should().Be("Draft");
        budget.ApprovedById.Should().BeNull();
    }

    [Fact]
    public void Customer_adapter_controls_activation_with_approval()
    {
        var entity = new Customer();
        var adapter = new CustomerWorkflowStatusAdapter();

        adapter.ApplySubmitOutcome(entity, WorkflowOutcome.Pending, null);
        entity.Status.Should().Be("PendingApproval");
        entity.IsActive.Should().BeFalse();

        adapter.ApplyApprovalOutcome(entity, WorkflowOutcome.Approved, Guid.NewGuid());
        entity.Status.Should().Be("Active");
        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Ehc_ticket_adapter_maps_workflow_outcomes_to_ticket_statuses()
    {
        var entity = new EhcTicket();
        var adapter = new EhcTicketWorkflowStatusAdapter();

        adapter.ApplySubmitOutcome(entity, WorkflowOutcome.Pending, Guid.NewGuid());
        entity.Status.Should().Be(EhcTicketStatus.Acknowledged);

        adapter.ApplyApprovalOutcome(entity, WorkflowOutcome.Approved, Guid.NewGuid());
        entity.Status.Should().Be(EhcTicketStatus.InProgress);
        entity.FirstRespondedAt.Should().NotBeNull();

        adapter.ApplyApprovalOutcome(entity, WorkflowOutcome.Rejected, Guid.NewGuid());
        entity.Status.Should().Be(EhcTicketStatus.New);

        adapter.ApplyRecallOutcome(entity, Guid.NewGuid());
        entity.Status.Should().Be(EhcTicketStatus.New);
    }
}
