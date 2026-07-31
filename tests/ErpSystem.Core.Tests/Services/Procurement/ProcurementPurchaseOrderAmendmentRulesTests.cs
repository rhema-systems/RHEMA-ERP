using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentRulesTests
{
    [Theory]
    [InlineData("Approved")]
    [InlineData("Sent")]
    [InlineData("Acknowledged")]
    public void ApprovedUnreceivedOrderWithoutOpenAmendmentCanBeAmended(
        string status)
    {
        ProcurementPurchaseOrderAmendmentRules.CanCreate(
                status,
                hasReceipts: false,
                hasOpenAmendment: false)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("Draft", false, false)]
    [InlineData("Pending Approval", false, false)]
    [InlineData("Approved", true, false)]
    [InlineData("Approved", false, true)]
    [InlineData("Received", false, false)]
    public void IneligibleLifecycleCannotBeAmended(
        string status,
        bool hasReceipts,
        bool hasOpenAmendment)
    {
        ProcurementPurchaseOrderAmendmentRules.CanCreate(
                status,
                hasReceipts,
                hasOpenAmendment)
            .Should().BeFalse();
    }

    [Fact]
    public void CommitmentDeltaUsesFinancialRounding()
    {
        ProcurementPurchaseOrderAmendmentRules.CalculateTotal(
                subTotal: 100.005m,
                taxAmount: 5.005m,
                shippingCost: 1m,
                miscellaneousCost: 0m,
                discountAmount: 0.001m)
            .Should().Be(106.01m);
    }

    [Fact]
    public void EmptyItemsAreRejected()
    {
        var action = () =>
            ProcurementPurchaseOrderAmendmentRules.ValidateItems([]);

        action.Should()
            .Throw<ProcurementPurchaseOrderAmendmentValidationException>()
            .Where(exception =>
                exception.Code == "PO_AMENDMENT_ITEMS_REQUIRED");
    }

    [Fact]
    public void DuplicateExistingLineageIsRejected()
    {
        var lineId = Guid.NewGuid();
        var items = new[]
        {
            ValidItem(lineId),
            ValidItem(lineId)
        };

        var action = () =>
            ProcurementPurchaseOrderAmendmentRules.ValidateItems(items);

        action.Should()
            .Throw<ProcurementPurchaseOrderAmendmentValidationException>()
            .Where(exception =>
                exception.Code == "PO_AMENDMENT_ITEM_DUPLICATE");
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Rejected, true)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Cancelled, true)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Acknowledged, true)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Draft, false)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.PendingApproval, false)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Applied, false)]
    [InlineData(ProcurementPurchaseOrderAmendmentStatus.Dispatched, false)]
    public void TerminalLifecycleIsExplicit(
        ProcurementPurchaseOrderAmendmentStatus status,
        bool expected)
    {
        ProcurementPurchaseOrderAmendmentRules.IsTerminal(status)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(WorkflowOutcome.Pending, false)]
    [InlineData(WorkflowOutcome.Approved, true)]
    [InlineData(WorkflowOutcome.Rejected, false)]
    [InlineData(WorkflowOutcome.Recalled, false)]
    public void OnlyApprovedWorkflowOutcomeIsAutomaticApproval(
        WorkflowOutcome outcome,
        bool expected)
    {
        ProcurementPurchaseOrderAmendmentRules
            .IsAutomaticApproval(outcome)
            .Should().Be(expected);
    }

    private static ProcurementPurchaseOrderAmendmentItemRequest ValidItem(
        Guid? purchaseOrderItemId = null) =>
        new()
        {
            PurchaseOrderItemId = purchaseOrderItemId,
            InventoryItemId = Guid.NewGuid(),
            ItemDescription = "Controlled line",
            OrderedQuantity = 1m,
            UnitOfMeasure = "EA",
            UnitPrice = 10m
        };
}
