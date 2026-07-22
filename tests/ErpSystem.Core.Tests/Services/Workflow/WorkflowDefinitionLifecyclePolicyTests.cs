using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowDefinitionLifecyclePolicyTests
{
    [Fact]
    public void Draft_IsEditable_ButPublishedAndRetiredAreNot()
    {
        WorkflowDefinitionLifecyclePolicy.IsEditable(Definition(WorkflowDefinitionLifecycleStatus.Draft)).Should().BeTrue();
        WorkflowDefinitionLifecyclePolicy.IsEditable(Definition(WorkflowDefinitionLifecycleStatus.Published)).Should().BeFalse();
        WorkflowDefinitionLifecyclePolicy.IsEditable(Definition(WorkflowDefinitionLifecycleStatus.Retired)).Should().BeFalse();
    }

    [Fact]
    public void RuntimeEligibility_RequiresPublishedAndActive()
    {
        WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(Definition(WorkflowDefinitionLifecycleStatus.Published, true)).Should().BeTrue();
        WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(Definition(WorkflowDefinitionLifecycleStatus.Published, false)).Should().BeFalse();
        WorkflowDefinitionLifecyclePolicy.IsRuntimeEligible(Definition(WorkflowDefinitionLifecycleStatus.Draft, true)).Should().BeFalse();
    }

    [Fact]
    public void NextVersion_UsesHighestVersionInFamily()
    {
        var versions = new[]
        {
            Definition(WorkflowDefinitionLifecycleStatus.Retired, version: 1),
            Definition(WorkflowDefinitionLifecycleStatus.Published, version: 3),
            Definition(WorkflowDefinitionLifecycleStatus.Retired, version: 2)
        };

        WorkflowDefinitionLifecyclePolicy.GetNextVersion(versions).Should().Be(4);
    }

    [Fact]
    public void EnsureEditable_ExplainsHowToChangeImmutableVersion()
    {
        var action = () => WorkflowDefinitionLifecyclePolicy.EnsureEditable(
            Definition(WorkflowDefinitionLifecycleStatus.Published, version: 2));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*published*Clone it as a new draft*");
    }

    private static WorkflowDefinition Definition(
        WorkflowDefinitionLifecycleStatus status,
        bool isActive = false,
        int version = 1) => new()
    {
        LifecycleStatus = status,
        IsActive = isActive,
        Version = version
    };
}
