using ErpSystem.Api.Controllers.Estate;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class EstateRentPenaltyStatusTests
{
    private static readonly Guid AssetId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly DateTime DueDate = new(2026, 8, 20);

    [Fact]
    public void PaidInvoice_IsNeverEligible()
    {
        var result = Status(today: DueDate.AddDays(20), paidAmount: 4500m);

        result.IsOverdue.Should().BeFalse();
        result.CanAssessPenalty.Should().BeFalse();
        result.OutstandingAmount.Should().Be(0m);
    }

    [Fact]
    public void UnpaidInvoice_OnDueDate_IsNotOverdue()
    {
        var result = Status(today: DueDate);

        result.IsOverdue.Should().BeFalse();
        result.CanAssessPenalty.Should().BeFalse();
    }

    [Fact]
    public void OverdueInvoice_WithinManagerGracePeriod_IsNotEligible()
    {
        var result = Status(today: DueDate.AddDays(5), gracePeriodDays: 10);

        result.IsOverdue.Should().BeTrue();
        result.CanAssessPenalty.Should().BeFalse();
    }

    [Fact]
    public void OverdueInvoice_AfterManagerGracePeriod_IsEligibleOnce()
    {
        var eligible = Status(today: DueDate.AddDays(11), gracePeriodDays: 10);
        var alreadyAssessed = Status(
            today: DueDate.AddDays(11),
            gracePeriodDays: 10,
            lastPenaltySourceInvoiceId: InvoiceId);

        eligible.IsOverdue.Should().BeTrue();
        eligible.CanAssessPenalty.Should().BeTrue();
        alreadyAssessed.CanAssessPenalty.Should().BeFalse();
    }

    private static EstateRentPenaltyStatusResult Status(
        DateTime today,
        decimal paidAmount = 0m,
        int gracePeriodDays = 0,
        Guid? lastPenaltySourceInvoiceId = null)
    {
        return PropertyManagementArBillingController.CalculateRentPenaltyStatus(
            AssetId,
            InvoiceId,
            DueDate.AddDays(-30),
            DueDate,
            4500m,
            paidAmount,
            0m,
            gracePeriodDays,
            "Fixed",
            100m,
            lastPenaltySourceInvoiceId,
            today);
    }
}
