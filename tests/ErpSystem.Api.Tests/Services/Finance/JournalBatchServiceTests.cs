using System.Diagnostics;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class JournalBatchServiceTests
{
    [Fact]
    public async Task SubmitWithoutWorkflow_PreparesEntriesWithoutHumanApprovalOrPosting()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false);
        var journalService = CreateJournalService(db, tenant);
        var service = CreateService(db, tenant, journalService.Object, workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);

        var result = await service.SubmitAsync(batch.Id);

        result.ApprovalRequired.Should().BeFalse();
        result.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.ReadyToPost);
        result.PostingStatus.Should().Be(JournalBatchPostingStatus.Ready);
        result.DisplayStatus.Should().Be("Ready to Post");
        result.CanPostAny.Should().BeTrue();
        result.CanReview.Should().BeFalse();
        result.WorkflowInstanceId.Should().BeNull();
        result.ApprovedByUserId.Should().BeNull();
        result.ApprovedAt.Should().BeNull();
        result.ReviewCompletedAt.Should().BeNull();
        result.ApprovedEntryCount.Should().Be(0);
        result.Items.Should().OnlyContain(x => x.ReviewStatus == JournalBatchItemReviewStatus.NotRequired &&
            x.PostingStatus == JournalBatchItemPostingStatus.Ready && x.FinalReviewedByUserId == null && x.FinalReviewedAt == null && x.Reviews.Count == 0);
        var savedJournals = await db.JournalEntries.AsNoTracking().ToListAsync();
        savedJournals.Should().OnlyContain(x => x.PostingStatus == "Approved" && x.ApprovalStatus == "Not Required" &&
            !x.RequiresApproval && x.ApprovedByUserId == null && x.ApprovedDate == null && x.ApprovalWorkflowId == null);
        workflow.Verify(x => x.StartApprovalWorkflowAsync("JournalBatch", It.IsAny<Guid>()), Times.Never);
        journalService.Verify(x => x.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.JournalBatchItemReviews.CountAsync()).Should().Be(0);
        (await db.JournalBatchPostingRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DirectBatch_PostsSelectedEntriesThroughCanonicalOwnerAndRemainsIdempotent()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false);
        var journalService = CreateJournalService(db, tenant);
        var service = CreateService(db, tenant, journalService.Object, workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        batch = await service.SubmitAsync(batch.Id);
        db.ChangeTracker.Clear();
        // A later workflow configuration change cannot rewrite the saved submission mode.
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(true);
        var request = new CreateJournalBatchPostingRunDto { JournalBatchItemIds = [batch.Items[0].Id], IdempotencyKey = "direct-run-1" };
        var first = await service.PostAsync(batch.Id, request);
        var duplicate = await service.PostAsync(batch.Id, request);
        duplicate.Id.Should().Be(first.Id);
        var partial = await service.GetByIdAsync(batch.Id);
        partial!.PostingStatus.Should().Be(JournalBatchPostingStatus.PartiallyPosted);
        partial.PostedDebitTotal.Should().Be(100m);
        partial.CanPostAny.Should().BeTrue();
        await service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto { JournalBatchItemIds = [batch.Items[1].Id], IdempotencyKey = "direct-run-2" });
        var posted = await service.GetByIdAsync(batch.Id);
        posted!.PostingStatus.Should().Be(JournalBatchPostingStatus.Posted);
        posted.PostedDebitTotal.Should().Be(300m);
        posted.CanPostAny.Should().BeFalse();
        foreach (var journal in journals)
            journalService.Verify(x => x.PostJournalEntryForBatchAsync(journal.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetiredWorkflow_WithExistingInstance_StillUsesApproval()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false);
        workflow.Setup(x => x.HasActiveApprovalInstanceAsync("JournalBatch", It.IsAny<Guid>())).ReturnsAsync(true);
        var service = CreateService(db, tenant, workflow: workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        batch = await service.SubmitAsync(batch.Id);
        batch.ApprovalRequired.Should().BeTrue();
        batch.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.PendingApproval);
        batch.WorkflowInstanceId.Should().NotBeNull();
        batch.CanPostAny.Should().BeFalse();
        var post = () => service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto { JournalBatchItemIds = [batch.Items[0].Id], IdempotencyKey = "blocked" });
        await post.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not ready*");
        var resubmit = () => service.SubmitAsync(batch.Id);
        await resubmit.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("changed-content")]
    public async Task DirectPosting_DoesNotBypassRetainedApprovalOrContentGuards(string invalidState)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false);
        var journalService = CreateJournalService(db, tenant);
        var service = CreateService(db, tenant, journalService.Object, workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        batch = await service.SubmitAsync(batch.Id);
        var journal = await db.JournalEntries.FirstAsync(x => x.Id == journals[0].Id);
        if (invalidState == "metadata") journal.ApprovedByUserId = Guid.NewGuid();
        else journal.Description = "Changed after submission";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var post = () => service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto { JournalBatchItemIds = [batch.Items[0].Id], IdempotencyKey = "invalid" });
        await post.Should().ThrowAsync<InvalidOperationException>();
        journalService.Verify(x => x.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoWorkflow_SubmissionStillValidatesControlTotalsBeforeReadiness()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false);
        var service = CreateService(db, tenant, workflow: workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        await service.UpdateAsync(batch.Id, new UpdateJournalBatchDto { Description = batch.Description, ExpectedDebitTotal = 301m, RowVersion = batch.RowVersion });
        var submit = () => service.SubmitAsync(batch.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>();
        var unchanged = await service.GetByIdAsync(batch.Id);
        unchanged!.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.Draft);
        unchanged.ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public async Task CompletedWorkflowResponse_WithoutRetainedProofCannotReleaseBatch()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("JournalBatch", It.IsAny<Guid>())).ReturnsAsync(new WorkflowExecutionResult
            { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = Guid.NewGuid() });
        var service = CreateService(db, tenant, workflow: workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        var submit = () => service.SubmitAsync(batch.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*retained completed workflow*");
        (await db.JournalBatchPostingRuns.CountAsync()).Should().Be(0);
    }

    private static async Task<JournalBatchDetailDto> CreateDraftBatchAsync(JournalBatchService service, Guid periodId, IReadOnlyList<JournalEntry> journals)
    {
        var batch = await service.CreateAsync(new CreateJournalBatchDto { Description = "Optional approval test", FiscalPeriodId = periodId,
            BookClassification = "IFRS", ControlCurrencyCode = "GHS", ExpectedDebitTotal = 300m, ExpectedJournalCount = 2 });
        foreach (var journal in journals) batch = await service.AddExistingJournalAsync(batch.Id, journal.Id);
        return batch;
    }

    [Fact]
    public async Task ConfirmedCompletedWorkflow_PreparesWithoutFabricatingAReviewerOrPosting()
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        var type = new ErpSystem.Core.Entities.Workflow.WorkflowEntityType { TenantId = tenant, Code = "JournalBatch", Name = "JournalBatch" };
        db.WorkflowEntityTypes.Add(type);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        workflow.Setup(x => x.StartApprovalWorkflowAsync("JournalBatch", It.IsAny<Guid>())).Returns(async (string _, Guid batchId) =>
        {
            var instance = new ErpSystem.Core.Entities.Workflow.WorkflowInstance { TenantId = tenant, EntityId = batchId,
                EntityTypeId = type.Id, EntityType = type, WorkflowDefinitionId = Guid.NewGuid(),
                Status = WorkflowInstanceStatus.Completed, CompletedDate = DateTime.UtcNow, InitiatedById = Guid.NewGuid() };
            db.WorkflowInstances.Add(instance);
            await db.SaveChangesAsync();
            return new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = instance.Id };
        });
        var journalService = CreateJournalService(db, tenant);
        var service = CreateService(db, tenant, journalService.Object, workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        batch = await service.SubmitAsync(batch.Id);
        batch.ApprovalRequired.Should().BeTrue();
        batch.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.Approved);
        batch.WorkflowInstanceId.Should().NotBeNull();
        batch.ApprovedByUserId.Should().BeNull();
        batch.Items.Should().OnlyContain(x => x.FinalReviewedByUserId == null && x.ReviewStatus == JournalBatchItemReviewStatus.Approved);
        batch.CanPostAny.Should().BeTrue();
        journalService.Verify(x => x.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("changed")]
    public async Task ApprovalPolicyFailures_DoNotBecomeDirectReadiness(string failure)
    {
        var tenant = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenant);
        var journals = SeedJournals(db, tenant, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        if (failure == "unavailable")
            workflow.Setup(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ThrowsAsync(new InvalidOperationException("Policy unavailable"));
        else workflow.SetupSequence(x => x.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(false).ReturnsAsync(true);
        var service = CreateService(db, tenant, workflow: workflow.Object);
        var batch = await CreateDraftBatchAsync(service, period.Id, journals);
        var submit = () => service.SubmitAsync(batch.Id);
        await submit.Should().ThrowAsync<InvalidOperationException>();
        var saved = await service.GetByIdAsync(batch.Id);
        saved!.ApprovalRequired.Should().BeTrue();
        saved.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.Draft);
        saved.CanPostAny.Should().BeFalse();
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "BookGovernance")]
    public async Task EligibleBooks_ShouldUseExactPeriodAuthorityAndExcludeParallelBooks()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var deltaBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS_ADJUSTMENTS",
            Name = "IFRS Adjustments",
            Purpose = "IFRS reporting adjustments",
            BookType = AccountingBookType.Delta,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            IsActive = true,
            AllowsPosting = true,
            SortOrder = 10
        };
        db.AccountingBooks.Add(deltaBook);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, deltaBook.Code);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, "TAX");
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var result = await service.GetEligibleBooksAsync(period.Id);

        result.Select(book => book.Code).Should().Equal("IFRS", "IFRS_ADJUSTMENTS");
        result.Should().NotContain(book => book.BookType == AccountingBookType.ParallelFull);

        var parallelBookId = db.AccountingBooks.Single(book => book.TenantId == tenantId && book.Code == "TAX").Id;
        var createParallel = () => service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Parallel book must remain replication governed",
            FiscalPeriodId = period.Id,
            AccountingBookId = parallelBookId,
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 100m
        });
        await createParallel.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unavailable for direct manual posting*");
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Controls")]
    public async Task GetEligibleDraftJournalsAsync_ShouldReturnOnlyAttachableDraftsForBatch()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var book = db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == "IFRS" && !item.IsDeleted);
        var journals = SeedJournals(db, tenantId, period.Id);
        var activeWorkflowJournal = NewDraftJournal(db, tenantId, period.Id, "JE-2026-000003", "Workflow draft", "GL");
        var subledgerJournal = NewDraftJournal(db, tenantId, period.Id, "JE-2026-000004", "AP generated draft", "AP");
        var otherBookJournal = NewDraftJournal(db, tenantId, period.Id, "JE-2026-000005", "Tax book draft", "GL", "TAX");
        db.JournalEntries.AddRange(activeWorkflowJournal, subledgerJournal, otherBookJournal);

        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "JournalEntry",
            Name = "Journal Entry"
        };
        db.WorkflowEntityTypes.Add(entityType);
        db.WorkflowInstances.Add(new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            EntityTypeId = entityType.Id,
            EntityType = entityType,
            EntityId = activeWorkflowJournal.Id,
            InitiatedById = Guid.NewGuid(),
            Status = WorkflowInstanceStatus.InProgress
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Eligible draft selector",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 100m
        });
        await service.AddExistingJournalAsync(batch.Id, journals[1].Id);

        var result = await service.GetEligibleDraftJournalsAsync(batch.Id, null);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(journals[0].Id);
        result[0].JournalEntryNumber.Should().Be("JE-2026-000001");
        result[0].TotalDebit.Should().Be(100m);
        result[0].LineCount.Should().Be(2);

        (await service.GetEligibleDraftJournalsAsync(batch.Id, "REF-1"))
            .Should().ContainSingle(item => item.Id == journals[0].Id);
        (await service.GetEligibleDraftJournalsAsync(batch.Id, "does-not-exist"))
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Controls")]
    public async Task ValidateAsync_ShouldEnforceIndependentBatchTotalAndJournalCount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        var batch = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Month-end manual journals",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 300m,
            ExpectedJournalCount = 2
        });
        await service.AddExistingJournalAsync(batch.Id, journals[0].Id);
        await service.AddExistingJournalAsync(batch.Id, journals[1].Id);

        var valid = await service.ValidateAsync(batch.Id);
        valid.IsValid.Should().BeTrue();
        valid.ActualDebitTotal.Should().Be(300m);
        valid.EntryCount.Should().Be(2);
        valid.LineCount.Should().Be(4);

        await service.UpdateAsync(batch.Id, new UpdateJournalBatchDto
        {
            Description = batch.Description,
            ExpectedDebitTotal = 301m,
            ExpectedJournalCount = 2,
            RowVersion = batch.RowVersion
        });
        var invalid = await service.ValidateAsync(batch.Id);
        invalid.IsValid.Should().BeFalse();
        invalid.Issues.Should().Contain(issue => issue.Code == "CONTROL_TOTAL_MISMATCH");
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Workflow")]
    public async Task ReviewAndPost_ShouldSupportPerEntryRejectionAndPartialPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(service => service.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        journalService
            .Setup(service => service.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid journalId, CancellationToken _) => new JournalEntryDto { Id = journalId, Status = "Posted" });
        var service = CreateService(db, tenantId, journalService.Object, workflow.Object);

        var batch = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Selective approval batch",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 300m,
            ExpectedJournalCount = 2
        });
        batch = await service.AddExistingJournalAsync(batch.Id, journals[0].Id);
        batch = await service.AddExistingJournalAsync(batch.Id, journals[1].Id);
        batch = await service.SubmitAsync(batch.Id);
        db.ChangeTracker.Clear(); // Each controller request resolves a fresh scoped DbContext in production.

        var reviewed = await service.ReviewStageAsync(batch.Id, new JournalBatchReviewStageDto
        {
            Decisions =
            [
                new()
                {
                    JournalBatchItemId = batch.Items[0].Id,
                    Decision = JournalBatchReviewDecision.Approved
                },
                new()
                {
                    JournalBatchItemId = batch.Items[1].Id,
                    Decision = JournalBatchReviewDecision.Rejected,
                    Comment = "Supporting schedule is missing."
                }
            ]
        });

        reviewed.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.PartiallyApproved);
        reviewed.ApprovedEntryCount.Should().Be(1);
        reviewed.RejectedEntryCount.Should().Be(1);
        reviewed.Items.Single(item => item.ReviewStatus == JournalBatchItemReviewStatus.Rejected)
            .FinalRejectionReason.Should().Be("Supporting schedule is missing.");

        var approvedItem = reviewed.Items.Single(item => item.ReviewStatus == JournalBatchItemReviewStatus.Approved);
        db.ChangeTracker.Clear();
        var run = await service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = [approvedItem.Id],
            IdempotencyKey = "partial-post-1"
        });
        var posted = await service.GetByIdAsync(batch.Id);

        run.Status.Should().Be(JournalBatchPostingRunStatus.Posted);
        run.SelectedEntryCount.Should().Be(1);
        posted!.PostingStatus.Should().Be(JournalBatchPostingStatus.Posted);
        posted.PostedEntryCount.Should().Be(1);
        journalService.Verify(
            service => service.PostJournalEntryForBatchAsync(approvedItem.JournalEntryId, It.IsAny<CancellationToken>()),
            Times.Once);
        journalService.Verify(
            service => service.NotifyJournalPostedAsync(approvedItem.JournalEntryId, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Posting")]
    public async Task PostAsync_ShouldPostApprovedEntriesAcrossMultipleIdempotentRuns()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(item => item.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        journalService
            .Setup(item => item.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid journalId, CancellationToken _) => new JournalEntryDto { Id = journalId, Status = "Posted" });
        var service = CreateService(db, tenantId, journalService.Object, workflow.Object);

        var batch = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Two-run posting batch",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 300m,
            ExpectedJournalCount = 2
        });
        batch = await service.AddExistingJournalAsync(batch.Id, journals[0].Id);
        batch = await service.AddExistingJournalAsync(batch.Id, journals[1].Id);
        batch = await service.SubmitAsync(batch.Id);
        db.ChangeTracker.Clear();
        batch = await service.ReviewStageAsync(batch.Id, new JournalBatchReviewStageDto
        {
            Decisions = batch.Items.Select(item => new JournalBatchReviewDecisionDto
            {
                JournalBatchItemId = item.Id,
                Decision = JournalBatchReviewDecision.Approved
            }).ToList()
        });

        db.ChangeTracker.Clear();
        var firstRunRequest = new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = [batch.Items[0].Id],
            IdempotencyKey = "posting-run-1"
        };
        var firstRun = await service.PostAsync(batch.Id, firstRunRequest);
        (await service.GetByIdAsync(batch.Id))!.PostingStatus
            .Should().Be(JournalBatchPostingStatus.PartiallyPosted);

        db.ChangeTracker.Clear();
        var replay = await service.PostAsync(batch.Id, firstRunRequest);
        replay.Id.Should().Be(firstRun.Id);
        journalService.Verify(
            item => item.PostJournalEntryForBatchAsync(batch.Items[0].JournalEntryId, It.IsAny<CancellationToken>()),
            Times.Once);

        db.ChangeTracker.Clear();
        await service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = [batch.Items[1].Id],
            IdempotencyKey = "posting-run-2"
        });
        var completed = await service.GetByIdAsync(batch.Id);

        completed!.PostingStatus.Should().Be(JournalBatchPostingStatus.Posted);
        completed.PostedEntryCount.Should().Be(2);
        completed.PostingRuns.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "PostingFailureRecovery")]
    public async Task PostAsync_WhenOneJournalFails_ShouldRollBackRunAndNeverNotify()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(item => item.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        journalService
            .SetupSequence(item => item.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JournalEntryDto { Id = journals[0].Id, Status = "Posted" })
            .ThrowsAsync(new InvalidOperationException("Simulated posting failure."));
        var service = CreateService(db, tenantId, journalService.Object, workflow.Object);

        var batch = await CreateApprovedBatchAsync(service, period.Id, journals);
        db.ChangeTracker.Clear();

        var act = () => service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = batch.Items.Select(item => item.Id).ToList(),
            IdempotencyKey = "posting-failure"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated posting failure.");
        db.ChangeTracker.Clear();
        var recovered = await service.GetByIdAsync(batch.Id);

        recovered!.PostingStatus.Should().Be(JournalBatchPostingStatus.Ready);
        recovered.PostedEntryCount.Should().Be(0);
        recovered.Items.Should().OnlyContain(item =>
            item.PostingStatus == JournalBatchItemPostingStatus.Ready &&
            item.PostingClaimRunId == null);
        recovered.PostingRuns.Should().ContainSingle()
            .Which.Status.Should().Be(JournalBatchPostingRunStatus.Failed);
        journalService.Verify(
            item => item.NotifyJournalPostedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "PostingFailureRecovery")]
    public async Task PostAsync_WhenLaterRunFails_ShouldPreservePartiallyPostedProgress()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(item => item.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        journalService
            .SetupSequence(item => item.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JournalEntryDto { Id = journals[0].Id, Status = "Posted" })
            .ThrowsAsync(new InvalidOperationException("Later posting run failed."));
        var service = CreateService(db, tenantId, journalService.Object, CreateWorkflow().Object);
        var batch = await CreateApprovedBatchAsync(service, period.Id, journals);
        db.ChangeTracker.Clear();

        await service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = [batch.Items[0].Id],
            IdempotencyKey = "successful-first-run"
        });
        db.ChangeTracker.Clear();

        var act = () => service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = [batch.Items[1].Id],
            IdempotencyKey = "failed-second-run"
        });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Later posting run failed.");

        db.ChangeTracker.Clear();
        var recovered = await service.GetByIdAsync(batch.Id);
        recovered!.PostingStatus.Should().Be(JournalBatchPostingStatus.PartiallyPosted);
        recovered.PostedEntryCount.Should().Be(1);
        recovered.Items.Single(item => item.Id == batch.Items[1].Id).PostingStatus
            .Should().Be(JournalBatchItemPostingStatus.Ready);
        recovered.PostingRuns.OrderBy(run => run.RunNumber).Select(run => run.Status)
            .Should().Equal(JournalBatchPostingRunStatus.Posted, JournalBatchPostingRunStatus.Failed);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "PostCommitNotification")]
    public async Task PostAsync_WhenPostCommitNotificationFails_ShouldKeepCommittedPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var journalService = new Mock<IJournalEntryService>();
        journalService
            .Setup(item => item.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        journalService
            .Setup(item => item.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid journalId, CancellationToken _) => new JournalEntryDto { Id = journalId, Status = "Posted" });
        journalService
            .Setup(item => item.NotifyJournalPostedAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("SignalR is unavailable."));
        var service = CreateService(db, tenantId, journalService.Object, workflow.Object);

        var batch = await CreateApprovedBatchAsync(service, period.Id, [journals[0]]);
        db.ChangeTracker.Clear();

        var run = await service.PostAsync(batch.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = batch.Items.Select(item => item.Id).ToList(),
            IdempotencyKey = "notification-failure"
        });
        var committed = await service.GetByIdAsync(batch.Id);

        run.Status.Should().Be(JournalBatchPostingRunStatus.Posted);
        committed!.PostingStatus.Should().Be(JournalBatchPostingStatus.Posted);
        committed.PostedEntryCount.Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "TenantIsolation")]
    public async Task BatchQueriesAndMutations_ShouldFailClosedForAnotherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other Tenant",
            Code = $"OTHER-{otherTenantId:N}"[..12],
            BaseCurrency = "GHS"
        });
        var foreignBatch = new JournalBatch
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            BatchNumber = "JB-OTHER-00001",
            Description = "Must remain isolated",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 100m
        };
        db.JournalBatches.Add(foreignBatch);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId);

        (await service.GetByIdAsync(foreignBatch.Id)).Should().BeNull();
        var act = () => service.UpdateAsync(foreignBatch.Id, new UpdateJournalBatchDto
        {
            Description = "Cross-tenant mutation attempt",
            ExpectedDebitTotal = 100m,
            RowVersion = Convert.ToBase64String(foreignBatch.RowVersion)
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found for this tenant*");
        (await db.JournalBatches.AsNoTracking().SingleAsync(item => item.Id == foreignBatch.Id))
            .Description.Should().Be("Must remain isolated");
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Performance")]
    public async Task GetByIdAsync_ShouldLoadOneThousandEntryBatchWithinReleaseBudget()
    {
        const int entryCount = 1_000;
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var book = db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == "IFRS" && !item.IsDeleted);
        var batch = new JournalBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = "JB-PERF-00001",
            Description = "One-thousand-entry release gate",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = entryCount,
            ExpectedJournalCount = entryCount
        };
        for (var index = 1; index <= entryCount; index++)
        {
            var journalId = Guid.NewGuid();
            batch.Items.Add(new JournalBatchItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SequenceNumber = index,
                JournalEntryId = journalId,
                JournalEntry = new JournalEntry
                {
                    Id = journalId,
                    TenantId = tenantId,
                    JournalEntryNumber = $"JE-PERF-{index:000000}",
                    JournalType = "General",
                    EntryDate = new DateTime(2026, 7, 15),
                    Description = $"Performance journal {index}",
                    SourceModule = "GL",
                    TotalDebitAmount = 1m,
                    TotalCreditAmount = 1m,
                    IsBalanced = true,
                    FiscalPeriodId = period.Id,
                    AccountingBookId = book.Id,
                    BookClassification = book.Code,
                    PostingStatus = "Draft",
                    ApprovalStatus = "Draft",
                    Transactions =
                    [
                        new AccountTransaction
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            JournalEntryId = journalId,
                            AccountingBookId = book.Id,
                            BookClassification = book.Code,
                            AccountId = Guid.NewGuid(),
                            TransactionDate = new DateTime(2026, 7, 15),
                            DebitAmount = 1m,
                            FunctionalCurrencyCode = "GHS",
                            LineNumber = 1
                        },
                        new AccountTransaction
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            JournalEntryId = journalId,
                            AccountingBookId = book.Id,
                            BookClassification = book.Code,
                            AccountId = Guid.NewGuid(),
                            TransactionDate = new DateTime(2026, 7, 15),
                            CreditAmount = 1m,
                            FunctionalCurrencyCode = "GHS",
                            LineNumber = 2
                        }
                    ]
                }
            });
        }
        db.JournalBatches.Add(batch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = CreateService(db, tenantId);

        var stopwatch = Stopwatch.StartNew();
        var detail = await service.GetByIdAsync(batch.Id);
        stopwatch.Stop();

        detail.Should().NotBeNull();
        detail!.EntryCount.Should().Be(entryCount);
        detail.LineCount.Should().Be(entryCount * 2);
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(20),
            "a 1,000-entry batch must remain operationally reviewable");
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Reversal")]
    public async Task FullReversal_ShouldCreateApproveAndPostOneLinkedReversalForEveryPostedEntry()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var journalService = CreateJournalService(db, tenantId);
        var service = CreateService(db, tenantId, journalService.Object, workflow.Object);

        var source = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Posted source batch",
            FiscalPeriodId = period.Id,
            AccountingBookId = BookId(db, tenantId),
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 300m,
            ExpectedJournalCount = 2
        });
        source = await service.AddExistingJournalAsync(source.Id, journals[0].Id);
        source = await service.AddExistingJournalAsync(source.Id, journals[1].Id);
        source = await service.SubmitAsync(source.Id);
        db.ChangeTracker.Clear();
        source = await service.ReviewStageAsync(source.Id, new JournalBatchReviewStageDto
        {
            Decisions = source.Items.Select(item => new JournalBatchReviewDecisionDto
            {
                JournalBatchItemId = item.Id,
                Decision = JournalBatchReviewDecision.Approved
            }).ToList()
        });
        db.ChangeTracker.Clear();
        await service.PostAsync(source.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = source.Items.Select(item => item.Id).ToList(),
            IdempotencyKey = "source-post"
        });
        db.ChangeTracker.Clear();

        var reversal = await service.CreateReversalBatchAsync(source.Id, new CreateJournalBatchReversalDto
        {
            Reason = "Correct the month-end source schedule.",
            ReversalDate = new DateTime(2026, 7, 20)
        });

        reversal.BatchType.Should().Be(JournalBatchType.Reversal);
        reversal.ReversalOfJournalBatchId.Should().Be(source.Id);
        reversal.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.PendingApproval);
        reversal.EntryCount.Should().Be(2);
        reversal.Items.Should().OnlyContain(item => item.ReviewStatus == JournalBatchItemReviewStatus.Pending);
        (await service.GetByIdAsync(source.Id))!.ReversalStatus.Should().Be(JournalBatchReversalStatus.ReversalPending);

        db.ChangeTracker.Clear();
        reversal = await service.ReviewStageAsync(reversal.Id, new JournalBatchReviewStageDto
        {
            Decisions = reversal.Items.Select(item => new JournalBatchReviewDecisionDto
            {
                JournalBatchItemId = item.Id,
                Decision = JournalBatchReviewDecision.Approved
            }).ToList()
        });
        db.ChangeTracker.Clear();
        await service.PostAsync(reversal.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = reversal.Items.Select(item => item.Id).ToList(),
            IdempotencyKey = "reversal-post"
        });

        var completedSource = await service.GetByIdAsync(source.Id);
        var completedReversal = await service.GetByIdAsync(reversal.Id);
        completedSource!.ReversalStatus.Should().Be(JournalBatchReversalStatus.Reversed);
        completedReversal!.PostingStatus.Should().Be(JournalBatchPostingStatus.Posted);
        completedReversal.ReversalStatus.Should().Be(JournalBatchReversalStatus.Reversed);
        (await db.JournalEntries.Where(entry => journals.Select(item => item.Id).Contains(entry.Id)).ToListAsync())
            .Should().OnlyContain(entry => entry.IsReversed && entry.ReversalJournalEntryId.HasValue);
    }

    [Fact]
    [Trait("Batch", "GeneralLedger")]
    [Trait("Category", "Reversal")]
    public async Task WithdrawReversal_ShouldVoidAttemptReleaseSourceAndAllowReplacement()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var period = SeedPeriod(db, tenantId);
        var journals = SeedJournals(db, tenantId, period.Id);
        await db.SaveChangesAsync();
        var workflow = CreateWorkflow();
        var service = CreateService(db, tenantId, CreateJournalService(db, tenantId).Object, workflow.Object);
        var source = await CreateApprovedBatchAsync(service, period.Id, journals);
        db.ChangeTracker.Clear();
        await service.PostAsync(source.Id, new CreateJournalBatchPostingRunDto
        {
            JournalBatchItemIds = source.Items.Select(item => item.Id).ToList(),
            IdempotencyKey = "reversal-withdraw-source-post"
        });
        db.ChangeTracker.Clear();

        var firstAttempt = await service.CreateReversalBatchAsync(source.Id, new CreateJournalBatchReversalDto
        {
            Reason = "First reversal attempt.",
            ReversalDate = new DateTime(2026, 7, 20)
        });
        db.ChangeTracker.Clear();
        var voided = await service.WithdrawAsync(firstAttempt.Id, "Supporting evidence must be corrected.");
        db.ChangeTracker.Clear();
        var releasedSource = await service.GetByIdAsync(source.Id);

        voided.IsVoided.Should().BeTrue();
        voided.ApprovalStatus.Should().Be(JournalBatchApprovalStatus.Cancelled);
        voided.DisplayStatus.Should().Be("Voided");
        releasedSource!.ReversalStatus.Should().Be(JournalBatchReversalStatus.NotReversed);
        releasedSource.ReversalBatchId.Should().BeNull();

        var replacement = await service.CreateReversalBatchAsync(source.Id, new CreateJournalBatchReversalDto
        {
            Reason = "Replacement reversal attempt.",
            ReversalDate = new DateTime(2026, 7, 21)
        });
        replacement.Id.Should().NotBe(firstAttempt.Id);
        replacement.IsVoided.Should().BeFalse();
        (await db.JournalBatches.CountAsync(batch => batch.ReversalOfJournalBatchId == source.Id))
            .Should().Be(2);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"journal-batches-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static JournalBatchService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IJournalEntryService? journalEntries = null,
        IWorkflowService? workflow = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(service => service.UserName).Returns("journal.batch.tester");
        currentUser.SetupGet(service => service.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(service => service.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(service => service.UserAgent).Returns("journal-batch-tests");

        var numbering = new Mock<IDocumentNumberingService>();
        var next = 0;
        numbering
            .Setup(service => service.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.JournalBatch,
                tenantId,
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"JB-2026-{Interlocked.Increment(ref next):00000}");

        var journalService = journalEntries ?? Mock.Of<IJournalEntryService>();
        Mock.Get(journalService)
            .Setup(service => service.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new JournalBatchService(
            db,
            currentUser.Object,
            journalService,
            numbering.Object,
            workflow ?? CreateWorkflow().Object,
            Mock.Of<ILogger<JournalBatchService>>());
    }

    private static Mock<IJournalEntryService> CreateJournalService(ApplicationDbContext db, Guid tenantId)
    {
        var service = new Mock<IJournalEntryService>();
        service
            .Setup(item => item.ValidateJournalEntryReadyForSubmissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        service
            .Setup(item => item.PostJournalEntryForBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid journalId, CancellationToken _) => new JournalEntryDto { Id = journalId, Status = "Posted" });
        var next = 100;
        service
            .Setup(item => item.CreateJournalEntryAsync(It.IsAny<CreateJournalEntryDto>(), It.IsAny<CancellationToken>()))
            .Returns(async (CreateJournalEntryDto dto, CancellationToken cancellationToken) =>
            {
                var id = Guid.NewGuid();
                var bookCode = dto.BookClassification ?? "IFRS";
                var book = db.AccountingBooks.Single(item =>
                    item.TenantId == tenantId && item.Code == bookCode && !item.IsDeleted);
                var transactions = dto.Transactions.Select(line => new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    JournalEntryId = id,
                    AccountingBookId = book.Id,
                    BookClassification = book.Code,
                    AccountId = line.AccountId,
                    TransactionDate = dto.TransactionDate,
                    DebitAmount = line.TransactionType.Equals("Debit", StringComparison.OrdinalIgnoreCase) ? line.Amount : 0,
                    CreditAmount = line.TransactionType.Equals("Credit", StringComparison.OrdinalIgnoreCase) ? line.Amount : 0,
                    FunctionalCurrencyCode = "GHS",
                    TransactionCurrency = line.CurrencyCode,
                    ForeignCurrencyAmount = line.ForeignAmount,
                    ExchangeRate = line.ExchangeRate,
                    LineNumber = line.LineNumber,
                    Description = line.Description,
                    SourceReferenceNumber = line.Reference
                }).ToList();
                var journal = new JournalEntry
                {
                    Id = id,
                    TenantId = tenantId,
                    JournalEntryNumber = $"JE-2026-{Interlocked.Increment(ref next):000000}",
                    JournalType = dto.JournalType ?? "General",
                    EntryDate = dto.TransactionDate,
                    Description = dto.Description ?? string.Empty,
                    ReferenceNumber = dto.Reference,
                    SourceModule = dto.SourceModule,
                    SourceDocumentId = dto.SourceDocumentId,
                    SourceDocumentType = dto.SourceDocumentType,
                    TotalDebitAmount = transactions.Sum(line => line.DebitAmount),
                    TotalCreditAmount = transactions.Sum(line => line.CreditAmount),
                    IsBalanced = true,
                    FiscalPeriodId = dto.FiscalPeriodId!.Value,
                    AccountingBookId = book.Id,
                    BookClassification = book.Code,
                    PostingStatus = "Draft",
                    ApprovalStatus = "Draft",
                    Transactions = transactions
                };
                db.JournalEntries.Add(journal);
                await db.SaveChangesAsync(cancellationToken);
                return new JournalEntryDto
                {
                    Id = id,
                    JournalNumber = journal.JournalEntryNumber,
                    TransactionDate = journal.EntryDate,
                    Status = journal.PostingStatus
                };
            });
        return service;
    }

    private static Mock<IWorkflowService> CreateWorkflow()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(service => service.HasActiveApprovalWorkflowAsync("JournalBatch")).ReturnsAsync(true);
        workflow.Setup(service => service.StartApprovalWorkflowAsync("JournalBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        workflow.Setup(service => service.CanUserApproveAsync("JournalBatch", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(service => service.GetCurrentWorkflowStepAsync("JournalBatch", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowStepInfo
            {
                Id = Guid.NewGuid(),
                StepName = "Finance Manager Approval",
                StepOrder = 1
            });
        workflow.Setup(service => service.ProcessApprovalStepAsync(
                "JournalBatch",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed
            });
        workflow.Setup(service => service.CancelWorkflowAsync(
                "JournalBatch",
                It.IsAny<Guid>(),
                It.IsAny<string>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Cancelled
            });
        return workflow;
    }

    private static async Task<JournalBatchDetailDto> CreateApprovedBatchAsync(
        JournalBatchService service,
        Guid fiscalPeriodId,
        IReadOnlyCollection<JournalEntry> journals)
    {
        var batch = await service.CreateAsync(new CreateJournalBatchDto
        {
            Description = "Approved posting test batch",
            FiscalPeriodId = fiscalPeriodId,
            AccountingBookId = journals.First().AccountingBookId,
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = journals.Sum(item => item.TotalDebitAmount),
            ExpectedJournalCount = journals.Count
        });
        foreach (var journal in journals)
            batch = await service.AddExistingJournalAsync(batch.Id, journal.Id);

        batch = await service.SubmitAsync(batch.Id);
        return await service.ReviewStageAsync(batch.Id, new JournalBatchReviewStageDto
        {
            Decisions = batch.Items.Select(item => new JournalBatchReviewDecisionDto
            {
                JournalBatchItemId = item.Id,
                Decision = JournalBatchReviewDecision.Approved
            }).ToList()
        });
    }

    private static FiscalPeriod SeedPeriod(ApplicationDbContext db, Guid tenantId)
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Journal Batch Tenant",
            Code = $"JBT-{tenantId:N}"[..12],
            BaseCurrency = "GHS"
        });
        var primaryBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            Purpose = "Primary",
            BookType = AccountingBookType.PrimaryFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
            IsDefault = true,
            IsActive = true,
            AllowsPosting = true
        };
        var taxBook = new AccountingBook
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "TAX",
            Name = "Tax Book",
            Purpose = "Tax",
            BookType = AccountingBookType.ParallelFull,
            LifecycleStatus = AccountingBookLifecycleStatus.Active,
            FunctionalCurrencyCode = "GHS",
            IsDefault = false,
            IsActive = true,
            AllowsPosting = true
        };
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = $"FY26-{tenantId.ToString("N")[..4]}",
            Year = 2026,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 12,
            Status = "Open",
            IsActive = true
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Open",
            IsOpen = true,
            IsClosed = false,
            IsLocked = false
        };
        db.AccountingBooks.AddRange(primaryBook, taxBook);
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        FinancePostingAuthorityFixture.SeedExactBookPeriod(db, tenantId, period, primaryBook.Code);
        return period;
    }

    private static Guid BookId(ApplicationDbContext db, Guid tenantId) =>
        db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == "IFRS" && !item.IsDeleted).Id;

    private static List<JournalEntry> SeedJournals(ApplicationDbContext db, Guid tenantId, Guid periodId)
    {
        var book = db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == "IFRS" && !item.IsDeleted);
        var debitAccount = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1000", AccountNumber = "1000",
            AccountName = "Cash", AccountType = AccountType.Asset, CurrencyCode = "GHS",
            Status = AccountStatus.Active, AllowDirectPosting = true
        };
        var creditAccount = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "2000", AccountNumber = "2000",
            AccountName = "Accrual", AccountType = AccountType.Liability, CurrencyCode = "GHS",
            Status = AccountStatus.Active, AllowDirectPosting = true
        };
        db.Accounts.AddRange(debitAccount, creditAccount);
        db.AccountAccountingBooks.AddRange(
            new AccountAccountingBook
            {
                TenantId = tenantId, AccountId = debitAccount.Id, AccountingBookId = book.Id, IsEnabled = true
            },
            new AccountAccountingBook
            {
                TenantId = tenantId, AccountId = creditAccount.Id, AccountingBookId = book.Id, IsEnabled = true
            });
        var amounts = new[] { 100m, 200m };
        var journals = amounts.Select((amount, index) =>
        {
            var journalId = Guid.NewGuid();
            return new JournalEntry
            {
                Id = journalId,
                TenantId = tenantId,
                JournalEntryNumber = $"JE-2026-{index + 1:000000}",
                JournalType = "General",
                EntryDate = new DateTime(2026, 7, 15),
                Description = $"Journal {index + 1}",
                ReferenceNumber = $"REF-{index + 1}",
                SourceModule = "GL",
                TotalDebitAmount = amount,
                TotalCreditAmount = amount,
                IsBalanced = true,
                BalanceDifference = 0,
                FiscalPeriodId = periodId,
                AccountingBookId = book.Id,
                BookClassification = book.Code,
                PostingStatus = "Draft",
                ApprovalStatus = "Draft",
                Transactions =
                [
                    new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        JournalEntryId = journalId,
                        AccountingBookId = book.Id,
                        BookClassification = book.Code,
                        AccountId = debitAccount.Id,
                        TransactionDate = new DateTime(2026, 7, 15),
                        DebitAmount = amount,
                        CreditAmount = 0,
                        FunctionalCurrencyCode = "GHS",
                        LineNumber = 1
                    },
                    new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        JournalEntryId = journalId,
                        AccountingBookId = book.Id,
                        BookClassification = book.Code,
                        AccountId = creditAccount.Id,
                        TransactionDate = new DateTime(2026, 7, 15),
                        DebitAmount = 0,
                        CreditAmount = amount,
                        FunctionalCurrencyCode = "GHS",
                        LineNumber = 2
                    }
                ]
            };
        }).ToList();
        db.JournalEntries.AddRange(journals);
        return journals;
    }

    private static JournalEntry NewDraftJournal(
        ApplicationDbContext db,
        Guid tenantId,
        Guid periodId,
        string number,
        string description,
        string sourceModule,
        string bookClassification = "IFRS")
    {
        var book = db.AccountingBooks.Local.Single(item =>
            item.TenantId == tenantId && item.Code == bookClassification && !item.IsDeleted);
        return new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = number,
            JournalType = "General",
            EntryDate = new DateTime(2026, 7, 16),
            Description = description,
            ReferenceNumber = $"REF-{number[^1]}",
            SourceModule = sourceModule,
            TotalDebitAmount = 50m,
            TotalCreditAmount = 50m,
            IsBalanced = true,
            BalanceDifference = 0m,
            FiscalPeriodId = periodId,
            AccountingBookId = book.Id,
            BookClassification = book.Code,
            PostingStatus = "Draft",
            ApprovalStatus = "Draft",
            Transactions = []
        };
    }
}
