using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptSourceRulesTests
{
    [Theory]
    [InlineData("Approved", false, true)]
    [InlineData("Partially Received", false, true)]
    [InlineData("PartiallyReceived", false, true)]
    [InlineData("Received", false, false)]
    [InlineData("Received", true, true)]
    [InlineData("Draft", true, false)]
    public void OnlyGovernedReceiptStatusesAreReceivable(
        string status,
        bool existingReceipt,
        bool expected)
    {
        ProcurementReceiptSourceRules.IsReceivableStatus(
                status,
                existingReceipt)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void CapacityIncludesToleranceAndPriorReceipts()
    {
        var capacity = ProcurementReceiptSourceRules.CalculateCapacity(
            orderedQuantity: 100m,
            previouslyReceiptedQuantity: 94.5m,
            tolerancePercent: 5m);

        capacity.ToleranceQuantity.Should().Be(5m);
        capacity.MaximumQuantity.Should().Be(105m);
        capacity.RemainingQuantity.Should().Be(10.5m);
    }

    [Theory]
    [InlineData(0, 10, false, "RCV_QUANTITY_REQUIRED")]
    [InlineData(-1, 10, false, "RCV_QUANTITY_REQUIRED")]
    [InlineData(11, 10, false, "RCV_REMAINING_QUANTITY_EXCEEDED")]
    [InlineData(10, 10, true, "RCV_LINE_READY")]
    public void RequestedQuantityIsFailClosedAgainstRemainingCapacity(
        decimal requested,
        decimal remaining,
        bool allowed,
        string code)
    {
        var decision = ProcurementReceiptSourceRules.EvaluateLine(
            requested,
            remaining);

        decision.Allowed.Should().Be(allowed);
        decision.Code.Should().Be(code);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void InvalidToleranceIsRejected(decimal tolerance)
    {
        var action = () =>
            ProcurementReceiptSourceRules.NormalizeTolerance(tolerance);

        action.Should()
            .Throw<ProcurementReceiptSourceValidationException>()
            .Which.Code.Should()
            .Be("RCV_TOLERANCE_INVALID");
    }

    [Fact]
    public void IntegrityHashIsStableButInputSensitive()
    {
        var first = ProcurementReceiptSourceRules.Hash(
            "PO-0501",
            100m,
            5m);
        var repeat = ProcurementReceiptSourceRules.Hash(
            "PO-0501",
            100m,
            5m);
        var changed = ProcurementReceiptSourceRules.Hash(
            "PO-0501",
            101m,
            5m);

        first.Should().HaveLength(64);
        repeat.Should().Be(first);
        changed.Should().NotBe(first);
    }
}
