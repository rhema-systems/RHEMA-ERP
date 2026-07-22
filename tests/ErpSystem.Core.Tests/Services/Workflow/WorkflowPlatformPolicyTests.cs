using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class WorkflowPlatformPolicyTests
{
    [Fact]
    public void Template_graph_rejects_duplicate_and_unknown_step_references()
    {
        var step = Guid.NewGuid();
        var errors = WorkflowPlatformPolicy.ValidateTemplateGraph([step, step], [(step, Guid.NewGuid())]);
        errors.Should().Contain(message => message.Contains("unique"));
        errors.Should().Contain(message => message.Contains("included step"));
    }

    [Theory]
    [InlineData("approve", "approve")]
    [InlineData(" Reject ", "reject")]
    public void Offline_actions_are_restricted_to_explicit_decisions(string input, string expected) =>
        WorkflowPlatformPolicy.NormalizeOfflineAction(input).Should().Be(expected);

    [Fact]
    public void Unknown_offline_action_is_rejected() =>
        FluentActions.Invoking(() => WorkflowPlatformPolicy.NormalizeOfflineAction("delete"))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Retry_policy_uses_exponential_delay_then_dead_letters()
    {
        var now = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);
        WorkflowPlatformPolicy.GetRetryDecision(2, 5, now).Should().Be(
            new WorkflowIntegrationRetryDecision(WorkflowExecutionQueueStatus.Failed, now.AddMinutes(4)));
        WorkflowPlatformPolicy.GetRetryDecision(5, 5, now).Should().Be(
            new WorkflowIntegrationRetryDecision(WorkflowExecutionQueueStatus.DeadLetter, null));
    }

    [Fact]
    public void Archive_hash_is_deterministic_and_analytics_range_is_bounded()
    {
        WorkflowPlatformPolicy.ComputeArchiveHash("[{\"id\":1}]")
            .Should().Be(WorkflowPlatformPolicy.ComputeArchiveHash("[{\"id\":1}]"));
        WorkflowPlatformPolicy.NormalizeAnalyticsDays(0).Should().Be(1);
        WorkflowPlatformPolicy.NormalizeAnalyticsDays(900).Should().Be(365);
    }
}
