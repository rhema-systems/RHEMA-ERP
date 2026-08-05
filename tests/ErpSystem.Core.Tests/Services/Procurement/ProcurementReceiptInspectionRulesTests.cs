using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionRulesTests
{
    [Theory]
    [InlineData(10, 10, 0, 0, ProcurementReceiptDisposition.Accepted)]
    [InlineData(10, 0, 10, 0, ProcurementReceiptDisposition.Rejected)]
    [InlineData(10, 6, 4, 0, ProcurementReceiptDisposition.PartiallyAccepted)]
    [InlineData(10, 6, 0, 4, ProcurementReceiptDisposition.Pending)]
    public void DispositionIsDerivedFromAuthoritativeQuantities(
        decimal received,
        decimal accepted,
        decimal rejected,
        decimal pending,
        ProcurementReceiptDisposition disposition)
    {
        ProcurementReceiptInspectionRules.Evaluate(received, accepted, rejected)
            .Should().Be((accepted, rejected, pending, disposition));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, -1, 0)]
    [InlineData(10, 0, -1)]
    [InlineData(10, 8, 3)]
    public void InvalidOrOverAcceptedQuantitiesFailClosed(
        decimal received,
        decimal accepted,
        decimal rejected)
    {
        var action = () => ProcurementReceiptInspectionRules.Evaluate(
            received, accepted, rejected);

        action.Should().Throw<ProcurementReceiptInspectionValidationException>()
            .Which.Code.Should().Be("RCV_INSPECTION_QUANTITY_INVALID");
    }

    [Fact]
    public void OnlyDraftCanBeEditedOrSubmitted()
    {
        ProcurementReceiptInspectionRules.CanEdit(
            ProcurementReceiptInspectionStatus.Draft).Should().BeTrue();
        ProcurementReceiptInspectionRules.CanSubmit(
            ProcurementReceiptInspectionStatus.Draft).Should().BeTrue();
        ProcurementReceiptInspectionRules.CanEdit(
            ProcurementReceiptInspectionStatus.Rejected).Should().BeFalse();
        ProcurementReceiptInspectionRules.CanSubmit(
            ProcurementReceiptInspectionStatus.PendingApproval).Should().BeFalse();
    }

    [Theory]
    [InlineData(10, 5, 10, 15, 10)]
    [InlineData(4, 12.5, 6, 7.5, 9.5)]
    [InlineData(0, 0, 8, 3.25, 3.25)]
    public void WarehouseAverageCostUsesExistingAndReceivedValue(
        decimal existingQuantity,
        decimal existingAverageCost,
        decimal receivedQuantity,
        decimal receivedUnitCost,
        decimal expected)
    {
        ProcurementReceiptInspectionRules.CalculateWeightedAverageCost(
                existingQuantity,
                existingAverageCost,
                receivedQuantity,
                receivedUnitCost)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptInspectionStatus.QualityHold, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.ReturnPending, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.PendingApproval, 0, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Rejected, 0, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 1, 4, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 0, false)]
    public void ApEligibilityUsesOnlyGovernedAcceptedQuantity(
        ProcurementReceiptInspectionStatus status,
        decimal pending,
        decimal eligible,
        bool expected)
    {
        ProcurementReceiptInspectionRules.IsApEligible(status, pending, eligible)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 0, true)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 0, 4, true)]
    [InlineData(ProcurementReceiptInspectionStatus.QualityHold, 0, 0, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Rejected, 0, 0, false)]
    [InlineData(ProcurementReceiptInspectionStatus.Closed, 1, 0, false)]
    public void ApMatchingTreatsOnlyClosedZeroEligibleRejectionsAsResolved(
        ProcurementReceiptInspectionStatus status,
        decimal pending,
        decimal eligible,
        bool expected)
    {
        ProcurementReceiptInspectionRules.IsApMatchingResolved(status, pending, eligible)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.Dispatched, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.ReplacementReceived, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, ProcurementReceiptResolutionStatus.Dispatched, false)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, ProcurementReceiptResolutionStatus.Authorized, false)]
    public void ClosureRequiresSupplierAcknowledgementAndCompletedResolution(
        ProcurementReceiptSupplierAcknowledgementStatus acknowledgement,
        ProcurementReceiptResolutionStatus resolution,
        bool expected)
    {
        ProcurementReceiptInspectionRules.CanClose(acknowledgement, resolution)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, true, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Pending, false, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Disputed, true, true)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Disputed, false, false)]
    [InlineData(ProcurementReceiptSupplierAcknowledgementStatus.Acknowledged, true, false)]
    public void SupplierCanAcknowledgeAPreviouslyDisputedRejectionNote(
        ProcurementReceiptSupplierAcknowledgementStatus status,
        bool acknowledged,
        bool expected)
    {
        ProcurementReceiptInspectionRules.CanSupplierRespond(status, acknowledged)
            .Should().Be(expected);
    }
}
