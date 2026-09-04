using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class WorkflowEvidenceReviewAuthorizationTests
{
    private const string ProcurementRole = "TDC_HEAD_OF_PROCUREMENT";

    [Theory]
    [InlineData(WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending)]
    [InlineData(WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.InProgress)]
    [InlineData(WorkflowInstanceStatus.Waiting, WorkflowStepInstanceStatus.InProgress)]
    public void CurrentConfiguredTdcRoleCanReview(
        WorkflowInstanceStatus instanceStatus,
        WorkflowStepInstanceStatus stepStatus)
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = instanceStatus;
        approval.StepInstance.Status = stepStatus;

        Assert.True(CanReview(instance, approval, actor, " tdc_head_of_procurement "));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("InternalAudit")]
    [InlineData("Finance Manager")]
    [InlineData("Financial Controller")]
    [InlineData("Chief Accountant")]
    [InlineData("Managing Director")]
    public void BusinessJobTitleAloneDoesNotGrantReviewAuthority(string role)
    {
        var (instance, approval, actor) = CreateAssignment();

        Assert.False(CanReview(instance, approval, actor, role));
    }

    [Fact]
    public void CurrentDirectAssigneeCanReviewWithoutAnExtraRole()
    {
        var (instance, approval, actor) = CreateAssignment();
        approval.ApproverId = actor;
        approval.ApproverRole = null;

        Assert.True(CanReview(instance, approval, actor));
    }

    [Fact]
    public void DelegationDoesNotLeaveThePrincipalOrRoleHolderAuthorized()
    {
        var (instance, approval, actor) = CreateAssignment();
        var delegateId = Guid.NewGuid();
        approval.ApproverId = delegateId;
        approval.OriginalApproverId = actor;
        approval.DelegatedById = actor;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
        Assert.True(CanReview(instance, approval, delegateId));
    }

    [Theory]
    [InlineData(WorkflowApprovalStatus.Queued)]
    [InlineData(WorkflowApprovalStatus.Rejected)]
    [InlineData(WorkflowApprovalStatus.Expired)]
    [InlineData(WorkflowApprovalStatus.Delegated)]
    [InlineData(WorkflowApprovalStatus.MoreInfoRequested)]
    public void NonActionableApprovalDoesNotGrantReviewAuthority(WorkflowApprovalStatus status)
    {
        var (instance, approval, actor) = CreateAssignment();
        approval.Status = status;
        approval.ApproverId = actor;
        approval.ProcessedById = actor;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Fact]
    public void FutureOrPreviousPendingStepDoesNotGrantReviewAuthority()
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.CurrentStepId = Guid.NewGuid();

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Theory]
    [InlineData(WorkflowStepInstanceStatus.Completed)]
    [InlineData(WorkflowStepInstanceStatus.Cancelled)]
    [InlineData(WorkflowStepInstanceStatus.Failed)]
    public void InactiveStepCannotGrantPendingReviewAuthority(WorkflowStepInstanceStatus status)
    {
        var (instance, approval, actor) = CreateAssignment();
        approval.StepInstance.Status = status;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.InProgress)]
    [InlineData(WorkflowInstanceStatus.Waiting)]
    [InlineData(WorkflowInstanceStatus.Completed)]
    public void ActualApproverRetainsExactWorkflowReviewAuthority(WorkflowInstanceStatus status)
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = status;
        instance.CurrentStepId = null;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ProcessedById = actor;
        approval.StepInstance.Status = WorkflowStepInstanceStatus.Completed;

        Assert.True(CanReview(instance, approval, actor, ProcurementRole));
        Assert.False(CanReview(instance, approval, Guid.NewGuid(), ProcurementRole));
    }

    [Fact]
    public void CompletedRoleBasedReviewerLosesAuthorityWhenRoleIsRevoked()
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = WorkflowInstanceStatus.Completed;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ProcessedById = actor;
        approval.StepInstance.Status = WorkflowStepInstanceStatus.Completed;

        Assert.False(CanReview(instance, approval, actor));
        Assert.False(CanReview(instance, approval, actor, "Manager"));
        Assert.True(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Fact]
    public void CompletedDirectReviewerRetainsItsExplicitAssignmentWithoutAnExtraRole()
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = WorkflowInstanceStatus.Completed;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ApproverId = actor;
        approval.ApproverRole = null;
        approval.ProcessedById = actor;
        approval.StepInstance.Status = WorkflowStepInstanceStatus.Completed;

        Assert.True(CanReview(instance, approval, actor));
    }

    [Fact]
    public void CompletedDelegationRetainsOnlyTheActualAssignedDelegate()
    {
        var (instance, approval, principal) = CreateAssignment();
        var delegateId = Guid.NewGuid();
        instance.Status = WorkflowInstanceStatus.Completed;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ApproverId = delegateId;
        approval.OriginalApproverId = principal;
        approval.DelegatedById = principal;
        approval.ProcessedById = delegateId;
        approval.StepInstance.Status = WorkflowStepInstanceStatus.Completed;

        Assert.True(CanReview(instance, approval, delegateId));
        Assert.False(CanReview(instance, approval, principal, ProcurementRole));
        Assert.False(CanReview(instance, approval, Guid.NewGuid(), ProcurementRole));
        approval.ApproverId = Guid.NewGuid();
        Assert.False(CanReview(instance, approval, delegateId, ProcurementRole));
    }

    [Fact]
    public void UnusedAssigneeDoesNotInheritTheActualApproversAuthority()
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = WorkflowInstanceStatus.Completed;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ApproverId = actor;
        approval.ProcessedById = Guid.NewGuid();
        approval.StepInstance.Status = WorkflowStepInstanceStatus.Completed;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Fact]
    public void CompletedWorkflowDoesNotLeavePendingRoleSlotsAuthorized()
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = WorkflowInstanceStatus.Completed;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("WorkflowAdmin")]
    [InlineData("SuperAdmin")]
    [InlineData("TenantAdmin")]
    [InlineData("workflowadmin")]
    public void ExistingAdministrativeGovernanceIsPreserved(string role)
    {
        var (instance, _, actor) = CreateAssignment();

        Assert.True(WorkflowEvidenceReviewAuthorization.CanReview(instance, [], actor, [role]));
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Created)]
    [InlineData(WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Failed)]
    [InlineData(WorkflowInstanceStatus.Suspended)]
    [InlineData((WorkflowInstanceStatus)99)]
    public void NonLiveWorkflowDeniesEvenAnAdministrator(WorkflowInstanceStatus status)
    {
        var (instance, approval, actor) = CreateAssignment();
        instance.Status = status;
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ProcessedById = actor;

        Assert.False(CanReview(instance, approval, actor, ProcurementRole, "SystemAdmin"));
    }

    [Theory]
    [InlineData("foreign-approval-tenant")]
    [InlineData("foreign-step-tenant")]
    [InlineData("other-workflow")]
    [InlineData("mismatched-step-id")]
    [InlineData("deleted-approval")]
    [InlineData("deleted-step")]
    [InlineData("missing-step")]
    [InlineData("empty-approval-id")]
    [InlineData("empty-step-id")]
    public void InvalidApprovalLineageCannotGrantPendingOrCompletedAuthority(string invalidity)
    {
        var (instance, approval, actor) = CreateAssignment();
        switch (invalidity)
        {
            case "foreign-approval-tenant": approval.TenantId = Guid.NewGuid(); break;
            case "foreign-step-tenant": approval.StepInstance.TenantId = Guid.NewGuid(); break;
            case "other-workflow": approval.StepInstance.WorkflowInstanceId = Guid.NewGuid(); break;
            case "mismatched-step-id": approval.StepInstanceId = Guid.NewGuid(); break;
            case "deleted-approval": approval.IsDeleted = true; break;
            case "deleted-step": approval.StepInstance.IsDeleted = true; break;
            case "missing-step": approval.StepInstance = null!; break;
            case "empty-approval-id": approval.Id = Guid.Empty; break;
            case "empty-step-id": approval.StepInstance.Id = Guid.Empty; break;
        }

        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
        approval.Status = WorkflowApprovalStatus.Approved;
        approval.ProcessedById = actor;
        Assert.False(CanReview(instance, approval, actor, ProcurementRole));
    }

    [Theory]
    [InlineData("empty-user")]
    [InlineData("empty-instance")]
    [InlineData("empty-tenant")]
    [InlineData("deleted-instance")]
    public void InvalidContextDeniesEvenAnAdministrator(string invalidity)
    {
        var (instance, approval, actor) = CreateAssignment();
        switch (invalidity)
        {
            case "empty-user": actor = Guid.Empty; break;
            case "empty-instance": instance.Id = Guid.Empty; break;
            case "empty-tenant": instance.TenantId = Guid.Empty; break;
            case "deleted-instance": instance.IsDeleted = true; break;
        }

        Assert.False(CanReview(instance, approval, actor, "SystemAdmin"));
    }

    private static bool CanReview(
        WorkflowInstance instance,
        WorkflowApproval approval,
        Guid actor,
        params string[] roles) => WorkflowEvidenceReviewAuthorization.CanReview(instance, [approval], actor, roles);

    private static (WorkflowInstance Instance, WorkflowApproval Approval, Guid Actor) CreateAssignment()
    {
        var tenant = Guid.NewGuid();
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            CurrentStepId = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.InProgress
        };
        var step = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = instance.CurrentStepId.Value,
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            StepInstanceId = step.Id,
            StepInstance = step,
            ApproverRole = ProcurementRole,
            Status = WorkflowApprovalStatus.Pending
        };
        return (instance, approval, Guid.NewGuid());
    }
}
