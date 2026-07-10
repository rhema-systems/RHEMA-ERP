using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class WorkflowDelegationPolicyTests
{
    [Fact]
    public void SelectEffective_PrefersScopedOutOfOfficeDelegationWithinAuthority()
    {
        var broad = Delegation(WorkflowDelegationKind.Authority);
        var scoped = Delegation(WorkflowDelegationKind.OutOfOffice, "Procurement", "PurchaseOrder", 50_000m);

        var result = WorkflowDelegationPolicy.SelectEffective([broad, scoped],
            "Procurement", "PurchaseOrder", null, null, 30_000m, "GHS");

        result.Should().BeSameAs(scoped);
    }

    [Fact]
    public void SelectEffective_RejectsDelegationAboveAuthorityLimit()
    {
        var limited = Delegation(WorkflowDelegationKind.Authority, maximumAmount: 100m);
        WorkflowDelegationPolicy.SelectEffective([limited], null, null, null, null, 101m, "GHS").Should().BeNull();
    }

    [Fact]
    public void SelectEffective_UsesWorkflowStepScopeBeforeBroaderWorkflowScope()
    {
        var workflowId = Guid.NewGuid();
        var firstStepId = Guid.NewGuid();
        var secondStepId = Guid.NewGuid();
        var workflowScoped = Delegation(WorkflowDelegationKind.OutOfOffice, workflowDefinitionId: workflowId);
        var stepScoped = Delegation(WorkflowDelegationKind.Authority, workflowDefinitionId: workflowId, workflowStepId: firstStepId);

        WorkflowDelegationPolicy.SelectEffective([workflowScoped, stepScoped], null, null,
                workflowId, firstStepId, null, "GHS")
            .Should().BeSameAs(stepScoped);

        WorkflowDelegationPolicy.SelectEffective([workflowScoped, stepScoped], null, null,
                workflowId, secondStepId, null, "GHS")
            .Should().BeSameAs(workflowScoped);
    }

    [Fact]
    public void CalculateDueDate_SkipsWeekendAndHoliday()
    {
        var friday = new DateTime(2026, 7, 3, 16, 0, 0, DateTimeKind.Utc);
        var holidays = new HashSet<DateOnly> { new(2026, 7, 6) };

        var due = WorkflowDelegationPolicy.CalculateDueDate(friday, 2, TimeZoneInfo.Utc, 31,
            new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0), holidays);

        due.Should().Be(new DateTime(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc));
    }

    private static WorkflowDelegation Delegation(WorkflowDelegationKind kind, string? module = null,
        string? entityType = null, decimal? maximumAmount = null, Guid? workflowDefinitionId = null,
        Guid? workflowStepId = null) => new()
    {
        Kind = kind, Module = module, EntityType = entityType, MaximumAmount = maximumAmount,
        WorkflowDefinitionId = workflowDefinitionId, WorkflowStepId = workflowStepId,
        CurrencyCode = "GHS", EffectiveFrom = DateTime.UtcNow.AddDays(-1), EffectiveTo = DateTime.UtcNow.AddDays(1)
    };
}
