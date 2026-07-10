using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowStatusAdapterRegistryTests
{
    [Fact]
    public void Constructor_RejectsDuplicateAliasesAcrossAdapters()
    {
        var action = () => new WorkflowStatusAdapterRegistry(new IWorkflowStatusAdapter[]
        {
            new TestAdapter("SharedEntity"),
            new SecondTestAdapter("sharedentity")
        });

        action.Should().Throw<InvalidOperationException>().WithMessage("*SharedEntity*");
    }

    [Fact]
    public void GetAdapter_ResolvesAliasesCaseInsensitively()
    {
        var adapter = new TestAdapter("PurchaseRequest");
        var registry = new WorkflowStatusAdapterRegistry(new[] { adapter });

        registry.GetAdapter("purchaserequest").Should().BeSameAs(adapter);
    }

    private sealed class TestAdapter : IWorkflowStatusAdapter
    {
        public TestAdapter(params string[] entityTypes) => EntityTypes = entityTypes;
        public IReadOnlyCollection<string> EntityTypes { get; }
        public void ApplySubmitOutcome(object entity, ErpSystem.Core.Enums.WorkflowOutcome outcome, Guid? userId) { }
        public void ApplyApprovalOutcome(object entity, ErpSystem.Core.Enums.WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null) { }
    }

    private sealed class SecondTestAdapter : IWorkflowStatusAdapter
    {
        public SecondTestAdapter(params string[] entityTypes) => EntityTypes = entityTypes;
        public IReadOnlyCollection<string> EntityTypes { get; }
        public void ApplySubmitOutcome(object entity, ErpSystem.Core.Enums.WorkflowOutcome outcome, Guid? userId) { }
        public void ApplyApprovalOutcome(object entity, ErpSystem.Core.Enums.WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null) { }
    }
}
