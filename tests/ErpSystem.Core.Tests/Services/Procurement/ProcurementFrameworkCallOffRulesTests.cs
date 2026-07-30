using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkCallOffRulesTests
{
    [Fact]
    public void LineTotalUsesThePersistedFourDecimalQuantity()
    {
        var normalized =
            ProcurementFrameworkCallOffCommercialRules.NormalizeLine(
                1.23456m,
                1000m);

        normalized.Quantity.Should().Be(1.2346m);
        normalized.LineTotal.Should().Be(1234.60m);
    }

    [Fact]
    public void RevisionCannotReserveMoreThanAgreementFamilyBalance()
    {
        var rejected =
            ProcurementFrameworkCallOffCommercialRules.EvaluateFamilyCapacity(
                1000m,
                600m,
                500m);
        var allowed =
            ProcurementFrameworkCallOffCommercialRules.EvaluateFamilyCapacity(
                1000m,
                600m,
                400m);

        rejected.CommittedAmount.Should().Be(600m);
        rejected.AvailableAmount.Should().Be(400m);
        rejected.CanReserve.Should().BeFalse();
        allowed.CanReserve.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcurementFrameworkCallOffStatus.Draft, "submit")]
    [InlineData(ProcurementFrameworkCallOffStatus.Draft, "cancel")]
    [InlineData(ProcurementFrameworkCallOffStatus.PendingApproval, "approve")]
    [InlineData(ProcurementFrameworkCallOffStatus.PendingApproval, "reject")]
    [InlineData(ProcurementFrameworkCallOffStatus.Approved, "issue")]
    [InlineData(ProcurementFrameworkCallOffStatus.Approved, "cancel")]
    public void AllowedActionsAreExplicitAndServerOwned(
        ProcurementFrameworkCallOffStatus status,
        string expectedAction)
    {
        var callOff = new ProcurementFrameworkCallOff { Status = status };

        ProcurementFrameworkCallOffRules.AllowedActions(callOff)
            .Should().Contain(expectedAction);
    }

    [Theory]
    [InlineData(ProcurementFrameworkCallOffStatus.Issued)]
    [InlineData(ProcurementFrameworkCallOffStatus.Rejected)]
    [InlineData(ProcurementFrameworkCallOffStatus.Cancelled)]
    public void TerminalOutcomesExposeNoMutationActions(
        ProcurementFrameworkCallOffStatus status)
    {
        var callOff = new ProcurementFrameworkCallOff { Status = status };

        ProcurementFrameworkCallOffRules.AllowedActions(callOff)
            .Should().BeEmpty();
    }
}
