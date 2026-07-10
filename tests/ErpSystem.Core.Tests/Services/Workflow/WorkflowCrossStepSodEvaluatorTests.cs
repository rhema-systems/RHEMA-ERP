using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowCrossStepSodEvaluatorTests
{
    [Fact]
    public void Validate_BlocksPreviousStepActor()
    {
        var userId = Guid.NewGuid();
        var step = CompletedStep("Compliance Review", userId);
        var config = Config(WorkflowApprovalActorSource.PreviousStepActor);

        var errors = WorkflowCrossStepSodEvaluator.Validate(
            config,
            new[] { step },
            EmptyApprovals(),
            new Dictionary<string, object>(),
            userId);

        errors.Should().ContainSingle().Which.Should().Contain("previous workflow step");
    }

    [Fact]
    public void Validate_BlocksAnyPriorApprover()
    {
        var userId = Guid.NewGuid();
        var step = CompletedStep("Department Approval");
        var approvals = new Dictionary<Guid, IReadOnlyCollection<WorkflowApproval>>
        {
            [step.Id] = new[] { new WorkflowApproval { ProcessedById = userId, Status = WorkflowApprovalStatus.Approved } }
        };

        var errors = WorkflowCrossStepSodEvaluator.Validate(
            Config(WorkflowApprovalActorSource.AnyPreviousApprover),
            new[] { step },
            approvals,
            new Dictionary<string, object>(),
            userId);

        errors.Should().ContainSingle().Which.Should().Contain("earlier workflow step");
    }

    [Fact]
    public void Validate_BlocksActorFromNamedStepOnly()
    {
        var userId = Guid.NewGuid();
        var selectedStep = CompletedStep("Evaluation", userId);
        var otherStep = CompletedStep("Budget Review", Guid.NewGuid());
        var config = Config(WorkflowApprovalActorSource.SpecificStepActor);
        config.ConflictRules[0].SourceStepName = "Evaluation";

        var errors = WorkflowCrossStepSodEvaluator.Validate(
            config,
            new[] { selectedStep, otherStep },
            EmptyApprovals(),
            new Dictionary<string, object>(),
            userId);

        errors.Should().ContainSingle().Which.Should().Contain("Evaluation");
    }

    [Fact]
    public void Validate_BlocksUserResolvedFromWorkflowContext()
    {
        var userId = Guid.NewGuid();
        var config = Config(WorkflowApprovalActorSource.ContextUser);
        config.ConflictRules[0].ContextField = "requestedById";

        var errors = WorkflowCrossStepSodEvaluator.Validate(
            config,
            Array.Empty<WorkflowStepInstance>(),
            EmptyApprovals(),
            new Dictionary<string, object> { ["requestedById"] = userId.ToString() },
            userId);

        errors.Should().ContainSingle().Which.Should().Contain("requestedById");
    }

    private static WorkflowApprovalConfigDto Config(WorkflowApprovalActorSource source)
    {
        return new WorkflowApprovalConfigDto
        {
            ConflictRules = new List<WorkflowApprovalConflictRuleDto>
            {
                new()
                {
                    Id = "rule-1",
                    Name = "Test rule",
                    IsEnabled = true,
                    ActorSource = source
                }
            }
        };
    }

    private static WorkflowStepInstance CompletedStep(string name, Guid? assignedToId = null)
    {
        return new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.Completed,
            AssignedToId = assignedToId,
            CreatedDate = DateTime.UtcNow.AddHours(-2),
            CompletedDate = DateTime.UtcNow.AddHours(-1),
            WorkflowStep = new WorkflowStep { Name = name }
        };
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyCollection<WorkflowApproval>> EmptyApprovals()
        => new Dictionary<Guid, IReadOnlyCollection<WorkflowApproval>>();
}
