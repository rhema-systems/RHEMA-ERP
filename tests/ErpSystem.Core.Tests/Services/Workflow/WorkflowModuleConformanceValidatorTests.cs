using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowModuleConformanceValidatorTests
{
    [Fact]
    public void Evaluate_ReportsEveryActiveEntityTypeWithoutAnAdapter()
    {
        var registry = new WorkflowStatusAdapterRegistry(new IWorkflowStatusAdapter[]
        {
            new TestAdapter("PurchaseOrder", "PURCHASE_ORDER")
        });
        var entityTypes = new[]
        {
            EntityType("PurchaseOrder", "PURCHASE_ORDER"),
            EntityType("WorkOrder", "WORK_ORDER"),
            EntityType("InactiveType", "INACTIVE_TYPE", isActive: false)
        };

        var report = WorkflowModuleConformanceValidator.Evaluate(entityTypes, registry);

        report.IsConformant.Should().BeFalse();
        report.ActiveEntityTypeCount.Should().Be(2);
        report.SupportedEntityTypes.Should().ContainSingle().Which.Should().Be("PurchaseOrder");
        report.MissingStatusAdapters.Should().ContainSingle().Which.Should().Be("WorkOrder (WORK_ORDER)");
    }

    [Fact]
    public void Evaluate_AcceptsCodeAlias()
    {
        var registry = new WorkflowStatusAdapterRegistry(new IWorkflowStatusAdapter[]
        {
            new TestAdapter("PURCHASE_ORDER")
        });

        var report = WorkflowModuleConformanceValidator.Evaluate(
            new[] { EntityType("PurchaseOrder", "PURCHASE_ORDER") },
            registry);

        report.IsConformant.Should().BeTrue();
    }

    private static WorkflowEntityType EntityType(
        string name,
        string code,
        bool isActive = true) => new()
    {
        Name = name,
        Code = code,
        IsActive = isActive
    };

    private sealed class TestAdapter(params string[] entityTypes) : IWorkflowStatusAdapter
    {
        public IReadOnlyCollection<string> EntityTypes { get; } = entityTypes;
        public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId) { }
        public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null) { }
    }
}
