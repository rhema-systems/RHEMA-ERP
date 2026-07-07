using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowApprovalGuardValidatorTests
{
    [Fact]
    public void Validate_BlocksWorkflowInitiator_WhenConfigured()
    {
        var userId = Guid.NewGuid();
        var config = new WorkflowApprovalConfigDto { PreventInitiatorApproval = true };

        var errors = WorkflowApprovalGuardValidator.Validate(config, userId, Array.Empty<WorkflowApproval>(), userId);

        errors.Should().ContainSingle().Which.Should().Contain("initiator");
    }

    [Fact]
    public void Validate_BlocksUserWhoAlreadyFilledApprovalSlot_WhenDistinctApproversRequired()
    {
        var userId = Guid.NewGuid();
        var config = new WorkflowApprovalConfigDto { RequireDistinctApprovers = true };
        var approvals = new[]
        {
            new WorkflowApproval
            {
                Status = WorkflowApprovalStatus.Approved,
                ProcessedById = userId
            }
        };

        var errors = WorkflowApprovalGuardValidator.Validate(config, Guid.NewGuid(), approvals, userId);

        errors.Should().ContainSingle().Which.Should().Contain("different user");
    }

    [Fact]
    public void Validate_AllowsApproval_WhenGuardsAreDisabled()
    {
        var userId = Guid.NewGuid();
        var approvals = new[]
        {
            new WorkflowApproval
            {
                Status = WorkflowApprovalStatus.Approved,
                ProcessedById = userId
            }
        };

        var errors = WorkflowApprovalGuardValidator.Validate(new WorkflowApprovalConfigDto(), userId, approvals, userId);

        errors.Should().BeEmpty();
    }
}
