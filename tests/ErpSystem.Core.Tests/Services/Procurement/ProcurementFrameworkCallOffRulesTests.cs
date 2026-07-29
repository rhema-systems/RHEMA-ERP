using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkCallOffRulesTests
{
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
