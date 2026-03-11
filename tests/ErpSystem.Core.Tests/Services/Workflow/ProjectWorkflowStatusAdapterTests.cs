using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class ProjectWorkflowStatusAdapterTests
{
    private readonly ProjectWorkflowStatusAdapter _adapter = new();

    [Fact]
    public void ApplySubmitOutcome_WhenPending_ShouldSetPendingApproval()
    {
        var project = new Project { Status = ProjectStatuses.Draft };

        _adapter.ApplySubmitOutcome(project, WorkflowOutcome.Pending, Guid.NewGuid());

        project.Status.Should().Be(ProjectStatuses.PendingApproval);
    }

    [Fact]
    public void ApplySubmitOutcome_WhenApprovedImmediately_ShouldSetPlanned()
    {
        var project = new Project { Status = ProjectStatuses.Draft };

        _adapter.ApplySubmitOutcome(project, WorkflowOutcome.Approved, Guid.NewGuid());

        project.Status.Should().Be(ProjectStatuses.Planned);
    }

    [Fact]
    public void ApplyApprovalOutcome_WhenRejected_ShouldReturnToDraftAndStoreReason()
    {
        var project = new Project { Status = ProjectStatuses.PendingApproval };

        _adapter.ApplyApprovalOutcome(project, WorkflowOutcome.Rejected, Guid.NewGuid(), "Missing sponsor");

        project.Status.Should().Be(ProjectStatuses.Draft);
        project.StatusRemarks.Should().Be("Missing sponsor");
    }

    [Fact]
    public void ApplyApprovalOutcome_WhenApproved_ShouldSetPlannedAndApprovedAt()
    {
        var project = new Project { Status = ProjectStatuses.PendingApproval };

        _adapter.ApplyApprovalOutcome(project, WorkflowOutcome.Approved, Guid.NewGuid());

        project.Status.Should().Be(ProjectStatuses.Planned);
        project.ApprovedAt.Should().NotBeNull();
    }
}
