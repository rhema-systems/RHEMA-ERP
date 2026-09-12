using System.Reflection;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionOptionalApprovalTests
{
    [Fact]
    public void Historical_entity_and_DTO_default_to_required_approval()
    {
        new ProcurementReceiptInspectionCase().ApprovalRequired.Should().BeTrue();
        new ProcurementReceiptInspectionDto().ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public void Confirmed_direct_result_has_no_workflow_instance_or_human_approval()
    {
        var direct = Result(false, null, WorkflowOutcome.Approved, WorkflowInstanceStatus.Completed);
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(false, direct);
        accept.Should().NotThrow();
        direct.ExecutionResult.WorkflowInstanceId.Should().BeNull();
        ProcurementReceiptInspectionActionType.Completed.Should().NotBe(ProcurementReceiptInspectionActionType.Approved);
    }

    [Fact]
    public void Configured_approval_keeps_the_real_pending_instance()
    {
        var required = Result(true, Guid.NewGuid(), WorkflowOutcome.Pending, WorkflowInstanceStatus.InProgress);
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(true, required);
        accept.Should().NotThrow();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Configuration_change_between_staging_and_central_submission_fails_closed(bool staged, bool actual)
    {
        var result = Result(actual, actual ? Guid.NewGuid() : null,
            actual ? WorkflowOutcome.Pending : WorkflowOutcome.Approved,
            actual ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed);
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(staged, result);
        accept.Should().Throw<ProcurementReceiptInspectionConflictException>()
            .Where(error => error.Code == "RCV_INSPECTION_WORKFLOW_CHANGED");
    }

    [Theory]
    [InlineData(true, false, WorkflowOutcome.Pending, WorkflowInstanceStatus.InProgress)]
    [InlineData(false, true, WorkflowOutcome.Approved, WorkflowInstanceStatus.Completed)]
    [InlineData(false, false, WorkflowOutcome.Pending, WorkflowInstanceStatus.Completed)]
    [InlineData(false, false, WorkflowOutcome.Rejected, WorkflowInstanceStatus.Completed)]
    [InlineData(false, false, WorkflowOutcome.Approved, WorkflowInstanceStatus.Waiting)]
    public void Incomplete_or_fabricated_workflow_result_cannot_post_stock(bool required, bool hasInstance,
        WorkflowOutcome outcome, WorkflowInstanceStatus status)
    {
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(required,
            Result(required, hasInstance ? Guid.NewGuid() : null, outcome, status));
        accept.Should().Throw<ProcurementReceiptInspectionConflictException>()
            .Where(error => error.Code == "RCV_INSPECTION_WORKFLOW_CHANGED");
    }

    [Fact]
    public void Empty_instance_identifier_is_not_a_configured_approval_instance()
    {
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(true,
            Result(true, Guid.Empty, WorkflowOutcome.Pending, WorkflowInstanceStatus.InProgress));
        accept.Should().Throw<ProcurementReceiptInspectionConflictException>();
    }

    [Fact]
    public void Central_failure_is_not_treated_as_absent_approval()
    {
        var failed = new WorkflowIntegrationResult(new WorkflowExecutionResult
            { Success = false, Message = "No eligible active approver." }, WorkflowOutcome.Pending);
        var accept = () => ProcurementReceiptInspectionService.EnsureSubmissionWorkflowResult(true, failed);
        accept.Should().Throw<ProcurementReceiptInspectionConflictException>()
            .Where(error => error.Code == "RCV_INSPECTION_WORKFLOW_START_FAILED");
    }

    [Fact]
    public async Task Confirmed_absence_uses_central_policy_without_inventing_a_definition()
    {
        var workflow = new Mock<IWorkflowIntegrationService>(MockBehavior.Strict);
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("PROCUREMENT_RECEIPT_INSPECTION"))
            .ReturnsAsync(false);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var service = Create(unitOfWork.Object, workflow.Object);
        var result = await ResolveDefinition(service);
        result.Should().BeNull();
        workflow.VerifyAll();
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Central_policy_failure_is_propagated_without_fallback_or_repository_writes()
    {
        var workflow = new Mock<IWorkflowIntegrationService>(MockBehavior.Strict);
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("PROCUREMENT_RECEIPT_INSPECTION"))
            .ThrowsAsync(new UnauthorizedAccessException("Authenticated tenant required."));
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var service = Create(unitOfWork.Object, workflow.Object);
        var resolve = async () => await ResolveDefinition(service);
        await resolve.Should().ThrowAsync<UnauthorizedAccessException>();
        unitOfWork.VerifyNoOtherCalls();
    }

    [Fact]
    public void DTO_projects_direct_mode_without_fabricated_workflow_metadata()
    {
        var inspection = new ProcurementReceiptInspectionCase
            { ApprovalRequired = false, Status = ProcurementReceiptInspectionStatus.Closed };
        var map = typeof(ProcurementReceiptInspectionService).GetMethod("Map", BindingFlags.Static | BindingFlags.NonPublic)!;
        var dto = (ProcurementReceiptInspectionDto)map.Invoke(null, [inspection])!;
        dto.ApprovalRequired.Should().BeFalse();
        dto.WorkflowDefinitionId.Should().BeNull();
        dto.WorkflowInstanceId.Should().BeNull();
        dto.Status.Should().Be(ProcurementReceiptInspectionStatus.Closed);
        inspection.DecidedByUserId.Should().BeNull();
        inspection.DecidedAtUtc.Should().BeNull();
    }

    private static WorkflowIntegrationResult Result(bool required, Guid? instance,
        WorkflowOutcome outcome, WorkflowInstanceStatus status) => new(new WorkflowExecutionResult
        { Success = true, WorkflowInstanceId = instance, Status = status }, outcome, required);

    private static Task<Guid?> ResolveDefinition(ProcurementReceiptInspectionService service) =>
        (Task<Guid?>)typeof(ProcurementReceiptInspectionService)
            .GetMethod("ResolveReceiptWorkflowDefinitionIdAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(service, [CancellationToken.None])!;

    private static ProcurementReceiptInspectionService Create(IUnitOfWork unitOfWork, IWorkflowIntegrationService workflow) => new(
        unitOfWork, Mock.Of<ICurrentUserProvider>(), Mock.Of<IProcurementAccessControlService>(),
        Mock.Of<IProcurementConfigurationService>(), Mock.Of<IProcurementComplianceDecisionService>(), workflow,
        Mock.Of<IProcurementSodGuardService>(), Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationTopicPublisher>(),
        Mock.Of<IProcurementPurchaseOrderSodService>(), Mock.Of<IProcurementReceiptSourceControlService>(),
        Mock.Of<IProcurementReceiptSourceEvidenceReadinessService>(), Mock.Of<IProcurementReceiptInspectionStore>(),
        Mock.Of<IInventoryValuationService>(), Mock.Of<IInventoryReceiptFinancePostingService>(),
        Mock.Of<IProcurementBudgetCommitmentLifecycleService>(), NullLogger<ProcurementReceiptInspectionService>.Instance);
}
