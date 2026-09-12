using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed partial class AllocationRunBatchServiceTests
{
    [Fact]
    public async Task ActiveWorkflowThatCompletesDuringSubmission_ReadiesBatchWithoutInventingHumanApproval()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var workflowId = Guid.NewGuid();
        workflow.Setup(item => item.StartApprovalWorkflowAsync("AllocationRunBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = workflowId });
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var submitted = await service.SubmitRunBatchAsync(draft.Id);
        submitted.Status.Should().Be("Approved");
        submitted.ApprovalRequired.Should().BeTrue();
        submitted.WorkflowInstanceId.Should().Be(workflowId);
        submitted.ApprovedAt.Should().BeNull();
        submitted.ApprovedByName.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyImmediateExecutionCannotBypassConfiguredApprovalOrAnExistingPeriodBatch(bool active)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(active);
        var service = CreateService(db, tenantId, workflow.Object);
        if (!active) await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var run = () => service.RunAllocationAsync(new RunAllocationDto(fixture.Rule.Id, fixture.Period.Id, fixture.Period.EndDate, null));
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Use the allocation run batch*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NoActiveWorkflow_ReadiesSavedCalculationAndNormalPostCreatesExactlyOneBalancedJournal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(false);
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var submitted = await service.SubmitRunBatchAsync(draft.Id);
        submitted.Status.Should().Be("ReadyToPost");
        submitted.ApprovalRequired.Should().BeFalse();
        submitted.WorkflowInstanceId.Should().BeNull();
        submitted.ApprovedAt.Should().BeNull();
        submitted.ApprovedByName.Should().BeNull();
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        workflow.Verify(item => item.StartApprovalWorkflowAsync("AllocationRunBatch", draft.Id), Times.Never);

        var first = await service.PostRunBatchAsync(draft.Id);
        var repeat = await service.PostRunBatchAsync(draft.Id);
        first.Status.Should().Be("Posted");
        repeat.JournalEntryId.Should().Be(first.JournalEntryId);
        first.ApprovedAt.Should().BeNull();
        first.ApprovedByName.Should().BeNull();
        (await db.FinancePostingEvents.CountAsync(e => e.SourceDocumentId == draft.Id)).Should().Be(1);
        var journal = await db.JournalEntries.SingleAsync(j => j.Id == first.JournalEntryId);
        journal.TotalDebitAmount.Should().Be(1000m);
        journal.TotalCreditAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task RetiredDefinitionDoesNotAbandonAnExistingApprovalInstance()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(false);
        workflow.Setup(item => item.HasActiveApprovalInstanceAsync("AllocationRunBatch", It.IsAny<Guid>())).ReturnsAsync(true);
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var submitted = await service.SubmitRunBatchAsync(draft.Id);
        submitted.Status.Should().Be("PendingApproval");
        submitted.ApprovalRequired.Should().BeTrue();
        var post = () => service.PostRunBatchAsync(draft.Id);
        await post.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LookupFailureDoesNotFinalizeOrPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch"))
            .ThrowsAsync(new InvalidOperationException("Policy unavailable"));
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        var submit = () => service.SubmitRunBatchAsync(draft.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("Policy unavailable");
        (await db.AllocationRunBatches.SingleAsync(b => b.Id == draft.Id)).Status.Should().Be(AllocationRunBatchStatus.Draft);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DirectReadyFlagDoesNotExposeHumanApprovalActions()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(false);
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        await service.SubmitRunBatchAsync(draft.Id);
        var approve = () => service.ApproveRunBatchAsync(draft.Id);
        var reject = () => service.RejectRunBatchAsync(draft.Id, "Rejected");
        await approve.Should().ThrowAsync<InvalidOperationException>();
        await reject.Should().ThrowAsync<InvalidOperationException>();
        workflow.Verify(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DirectSubmissionStillRequiresAnOpenPeriod()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = SeedAllocationFixture(db, tenantId);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(item => item.HasActiveApprovalWorkflowAsync("AllocationRunBatch")).ReturnsAsync(false);
        var service = CreateService(db, tenantId, workflow.Object);
        var draft = await service.CreateRunBatchAsync(CreateRunRequest(fixture));
        fixture.Period.IsClosed = true;
        await db.SaveChangesAsync();
        var submit = () => service.SubmitRunBatchAsync(draft.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not open*");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
    }
}
