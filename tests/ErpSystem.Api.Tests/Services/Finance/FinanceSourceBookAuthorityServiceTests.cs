using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceBookAuthorityServiceTests
{
    [Fact]
    public async Task WorkflowType_MayDifferFromPostingType_ButMismatchIsRejectedAndRetained()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var workflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();
        var service = Service(db, state);
        var request = Request(state, workflowId: workflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted,
            sourceDocumentType: "CustomerInvoice", workflowEntityType: "Invoice");

        var frozen = await service.FreezeInitialPrimaryAsync(request);
        frozen.SourceDocumentType.Should().Be("CUSTOMERINVOICE");
        frozen.SourceWorkflowEntityType.Should().Be("INVOICE");
        workflow.Status = WorkflowInstanceStatus.Completed;
        workflow.CompletedDate = DateTime.UtcNow;
        AddWorkflowApprovalEvidence(db, state, workflow, Guid.NewGuid(), workflow.CompletedDate.Value.AddSeconds(-1));
        await db.SaveChangesAsync();
        (await service.RequireForPostingAsync(request)).AuthorityId.Should().Be(frozen.AuthorityId);

        var mismatch = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted,
            sourceDocumentType: "CustomerInvoice", workflowEntityType: "Payment"));
        await mismatch.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(AccountingBookType.ParallelFull, false)]
    [InlineData(AccountingBookType.Delta, true)]
    public async Task RetainedOriginal_AllowsActiveParallelAndDeltaCoordinates(
        AccountingBookType type, bool usesBaseBook)
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var retainedBook = Book(state.TenantId,
            type == AccountingBookType.ParallelFull ? "PAR_USD" : "DELTA_USD", false);
        retainedBook.BookType = type;
        retainedBook.FunctionalCurrencyCode = usesBaseBook ? null : "USD";
        if (usesBaseBook)
        {
            var baseBook = Book(state.TenantId, "PAR_BASE", false);
            baseBook.BookType = AccountingBookType.ParallelFull;
            baseBook.FunctionalCurrencyCode = "USD";
            retainedBook.BaseAccountingBookId = baseBook.Id;
            db.AccountingBooks.Add(baseBook);
        }
        db.AccountingBooks.Add(retainedBook);
        await db.SaveChangesAsync();
        state = state with { Book = retainedBook };
        var posting = await AddPostingAsync(db, state, state.SourceId, functionalCurrency: "USD");
        var service = Service(db, state);

        var retained = await service.RetainExistingPostedOriginalAsync(
            Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted),
            posting.JournalId, posting.EventId);

        retained.AccountingBookId.Should().Be(state.Book.Id);
        retained.FunctionalCurrencyCode.Should().Be("USD");
        (await service.RequireBoundOriginalAsync(retained.AuthorityId)).AuthorityId.Should().Be(retained.AuthorityId);
    }

    [Fact]
    public async Task RetainedOriginal_RejectsInactiveNonPostableAndCurrencyTamperedBooks()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var posting = await AddPostingAsync(db, state, state.SourceId);
        var service = Service(db, state);
        var retained = await service.RetainExistingPostedOriginalAsync(
            Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted),
            posting.JournalId, posting.EventId);

        state.Book.IsActive = false;
        await db.SaveChangesAsync();
        Func<Task> inactive = () => service.RequireBoundOriginalAsync(retained.AuthorityId);
        await inactive.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BOOK_INVALID*");
        state.Book.IsActive = true;
        state.Book.AllowsPosting = false;
        await db.SaveChangesAsync();
        Func<Task> nonPostable = () => service.RequireBoundOriginalAsync(retained.AuthorityId);
        await nonPostable.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BOOK_INVALID*");
        state.Book.AllowsPosting = true;
        state.Book.FunctionalCurrencyCode = "USD";
        await db.SaveChangesAsync();
        Func<Task> currencyTampered = () => service.RequireBoundOriginalAsync(retained.AuthorityId);
        await currencyTampered.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InitialFreeze_UsesPerpetualDefaultPrimary_IgnoresRetiredDesignation_AndRetriesExactly()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var retiredTarget = Book(state.TenantId, "OLD_PRIMARY", isDefault: false);
        db.AccountingBooks.Add(retiredTarget);
        db.AccountingBookPrimaryDesignations.Add(new AccountingBookPrimaryDesignation
        {
            TenantId = state.TenantId, PreviousPrimaryBookId = state.Book.Id,
            NewPrimaryBookId = retiredTarget.Id, EffectiveFrom = state.Date.AddDays(-1),
            RequestReason = "retired model evidence", DecisionReason = "retired model evidence",
            RequestedByUserId = state.ActorId, RequestedAtUtc = DateTime.UtcNow,
            ApprovedByUserId = state.ActorId, ApprovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = Service(db, state);

        var first = await service.FreezeInitialPrimaryAsync(Request(state));
        var retry = await service.FreezeInitialPrimaryAsync(Request(state));

        first.AuthorityId.Should().Be(retry.AuthorityId);
        first.AccountingBookId.Should().Be(state.Book.Id);
        first.AccountingBookCode.Should().Be("BASE");
        first.SelectionBasis.Should().Be(FinanceSourceBookAuthoritySelectionBases.DefaultPrimary);
        first.OriginModuleCode.Should().Be("FIN");
        first.SourceDocumentType.Should().Be("INVOICE");
        (await db.FinanceSourceBookAuthorities.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(true, false, "GHS")]
    [InlineData(false, true, "GHS")]
    [InlineData(false, false, "USD")]
    public async Task InitialFreeze_RejectsNonPerpetualOrCurrencyMismatchedPrimary(
        bool hasStart, bool hasEnd, string bookCurrency)
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        state.Book.EffectiveFromUtc = hasStart ? state.Date.AddDays(-1) : null;
        state.Book.EffectiveToUtc = hasEnd ? state.Date.AddDays(1) : null;
        state.Book.FunctionalCurrencyCode = bookCurrency;
        await db.SaveChangesAsync();

        var act = () => Service(db, state).FreezeInitialPrimaryAsync(Request(state));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PRIMARY_INVALID*");
    }

    [Fact]
    public async Task Resubmission_RequiresRejectedPredecessorWorkflow_AndCreatesImmutableVersion()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var oldWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(oldWorkflow);
        await db.SaveChangesAsync();
        var service = Service(db, state);
        var first = await service.FreezeInitialPrimaryAsync(Request(state, workflowId: oldWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        oldWorkflow.Status = WorkflowInstanceStatus.Cancelled;
        var replacement = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(replacement);
        await db.SaveChangesAsync();

        var second = await service.FreezeResubmissionAsync(Request(state, workflowId: replacement.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), first.AuthorityId);

        second.AuthorityVersion.Should().Be(2);
        second.SourceWorkflowInstanceId.Should().Be(replacement.Id);
        (await db.FinanceSourceBookAuthorities.SingleAsync(item => item.Id == second.AuthorityId))
            .SupersedesAuthorityId.Should().Be(first.AuthorityId);
    }

    [Fact]
    public async Task Binding_IsOneWay_Idempotent_AndRequiresExactNonReplicaEvidence()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var authority = await service.FreezeInitialPrimaryAsync(Request(state));
        var original = await AddPostingAsync(db, state, state.SourceId);

        var bound = await service.BindOriginalPostingAsync(authority.AuthorityId, original.EventId, original.JournalId);
        var retry = await service.BindOriginalPostingAsync(authority.AuthorityId, original.EventId, original.JournalId);
        bound.OriginalJournalEntryId.Should().Be(original.JournalId);
        retry.Should().Be(bound);

        var different = await AddPostingAsync(db, state, state.SourceId);
        var act = () => service.BindOriginalPostingAsync(authority.AuthorityId, different.EventId, different.JournalId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*BINDING_IMMUTABLE*");
    }

    [Fact]
    public async Task LegacyRetention_RejectsReplica_AndNeverGuessesAnotherEvent()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var replica = await AddPostingAsync(db, state, state.SourceId, replica: true);
        var service = Service(db, state);
        var request = Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted);

        var act = () => service.RetainExistingPostedOriginalAsync(request, replica.JournalId, replica.EventId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*LEGACY_JOURNAL_INVALID*");
        (await db.FinanceSourceBookAuthorities.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task LegacyRetention_DerivesOnlyExactPostedEvidence_WithoutInventingWorkflowOrApprover()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var posting = await AddPostingAsync(db, state, state.SourceId);
        var service = Service(db, state);

        var retained = await service.RetainExistingPostedOriginalAsync(
            Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted),
            posting.JournalId, posting.EventId);
        var required = await service.RequireBoundOriginalAsync(retained.AuthorityId);

        required.OriginalFinancePostingEventId.Should().Be(posting.EventId);
        required.OriginalJournalEntryId.Should().Be(posting.JournalId);
        required.SelectionBasis.Should().Be(FinanceSourceBookAuthoritySelectionBases.RetainedPostedOriginal);
        var entity = await db.FinanceSourceBookAuthorities.AsNoTracking().SingleAsync(item => item.Id == retained.AuthorityId);
        entity.SourceWorkflowInstanceId.Should().BeNull();
        entity.FrozenByUserId.Should().BeNull();
        entity.BoundByUserId.Should().BeNull();
    }

    [Fact]
    public async Task LegacyRetention_RetryRejectsJournalWithRetainedReversalLink()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var posting = await AddPostingAsync(db, state, state.SourceId);
        var service = Service(db, state);
        var request = Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted);
        await service.RetainExistingPostedOriginalAsync(request, posting.JournalId, posting.EventId);
        var journal = await db.JournalEntries.SingleAsync(item => item.Id == posting.JournalId);
        journal.ReversalJournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var retry = () => service.RetainExistingPostedOriginalAsync(request, posting.JournalId, posting.EventId);
        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ORIGINAL_JOURNAL_INVALID*");
    }

    [Fact]
    public async Task InheritedFreeze_RequiresBoundOriginsAndRetainsTheirExactEvidence()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var firstSource = state.SourceId;
        var secondSource = Guid.NewGuid();
        var first = await FreezeAndBindAsync(db, service, state, firstSource);
        var second = await FreezeAndBindAsync(db, service, state, secondSource);
        var settlement = Guid.NewGuid();

        var result = await service.FreezeInheritedAsync(Request(state, sourceId: settlement, currency: "USD"),
        [
            new() { OriginAuthorityId = first.AuthorityId, Role = "INVOICE" },
            new() { OriginAuthorityId = second.AuthorityId, Role = "ADVANCE" }
        ]);

        result.AccountingBookId.Should().Be(state.Book.Id);
        result.TransactionCurrencyCode.Should().Be("USD");
        result.SelectionBasis.Should().Be(FinanceSourceBookAuthoritySelectionBases.InheritedOriginal);
        var origins = await db.FinanceSourceBookAuthorityOrigins.Where(item =>
            item.FinanceSourceBookAuthorityId == result.AuthorityId).ToListAsync();
        origins.Should().HaveCount(2);
        origins.Should().OnlyContain(item => item.OriginalFinancePostingEventId != Guid.Empty && item.OriginalJournalEntryId != Guid.Empty);
    }

    [Fact]
    public async Task InitialFreeze_RejectsCompletedWorkflowAtSubmittedStage_AndPostingRequiresCompletedOutcome()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var completed = Workflow(state, WorkflowInstanceStatus.Completed);
        db.WorkflowInstances.Add(completed);
        await db.SaveChangesAsync();
        var service = Service(db, state);

        var stale = () => service.FreezeInitialPrimaryAsync(Request(state, workflowId: completed.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_STAGE_INVALID*");

        var pending = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(pending);
        await db.SaveChangesAsync();
        var authority = await service.FreezeInitialPrimaryAsync(Request(state, workflowId: pending.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        var beforeApproval = () => service.RequireForPostingAsync(Request(state, workflowId: pending.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.PrePost));
        await beforeApproval.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_NOT_APPROVED*");
        pending.Status = WorkflowInstanceStatus.Completed;
        pending.CompletedDate = DateTime.UtcNow;
        AddWorkflowApprovalEvidence(db, state, pending, Guid.NewGuid(),
            pending.CompletedDate.Value.AddSeconds(-1));
        await db.SaveChangesAsync();
        (await service.RequireForPostingAsync(Request(state, workflowId: pending.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.PrePost))).AuthorityId.Should().Be(authority.AuthorityId);
    }

    [Fact]
    public async Task CompletedWorkflow_RequiresUniqueTimelyApprovedCheckerEvidence()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var workflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();
        var service = Service(db, state);
        await service.FreezeInitialPrimaryAsync(Request(state, workflowId: workflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        workflow.Status = WorkflowInstanceStatus.Completed;
        workflow.CompletedDate = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var absent = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id));
        await absent.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_APPROVAL_REQUIRED*");

        var completion = workflow.CompletedDate.Value;
        var evidence = AddWorkflowApprovalEvidence(db, state, workflow, Guid.NewGuid(),
            completion.AddSeconds(-1), WorkflowApprovalStatus.Rejected);
        await db.SaveChangesAsync();
        var rejected = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id));
        await rejected.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_APPROVAL_REQUIRED*");

        evidence.Approval.Status = WorkflowApprovalStatus.Approved;
        evidence.Approval.ProcessedDate = completion.AddSeconds(1);
        evidence.Step.CompletedDate = completion.AddSeconds(2);
        await db.SaveChangesAsync();
        var afterCompletion = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id));
        await afterCompletion.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_APPROVAL_REQUIRED*");

        var validProcessedAt = completion.AddSeconds(-1);
        evidence.Approval.ProcessedDate = validProcessedAt;
        evidence.Approval.ProcessedById = workflow.InitiatedById;
        evidence.Step.CompletedDate = completion;
        await db.SaveChangesAsync();
        var sameActor = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id));
        await sameActor.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_MAKER_CHECKER_REQUIRED*");

        evidence.Approval.ProcessedById = Guid.NewGuid();
        AddWorkflowApprovalEvidence(db, state, workflow, Guid.NewGuid(), validProcessedAt);
        await db.SaveChangesAsync();
        var tiedLatest = () => service.RequireForPostingAsync(Request(state, workflowId: workflow.Id));
        await tiedLatest.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_APPROVAL_AMBIGUOUS*");
    }

    [Fact]
    public async Task Resubmission_RejectsWrongEntityType_AndBoundPredecessor()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var oldWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(oldWorkflow);
        await db.SaveChangesAsync();
        var service = Service(db, state);
        var first = await service.FreezeInitialPrimaryAsync(Request(state, workflowId: oldWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        oldWorkflow.Status = WorkflowInstanceStatus.Cancelled;
        var wrongTypeId = Guid.NewGuid();
        db.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = wrongTypeId, TenantId = state.TenantId, Code = "PAYMENT", Name = "Payment", IsActive = true
        });
        var wrongType = Workflow(state, WorkflowInstanceStatus.InProgress, wrongTypeId);
        db.WorkflowInstances.Add(wrongType);
        await db.SaveChangesAsync();

        var wrong = () => service.FreezeResubmissionAsync(Request(state, workflowId: wrongType.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), first.AuthorityId);
        await wrong.Should().ThrowAsync<InvalidOperationException>().WithMessage("*WORKFLOW_INVALID*");

        oldWorkflow.Status = WorkflowInstanceStatus.Completed;
        oldWorkflow.CompletedDate = DateTime.UtcNow;
        AddWorkflowApprovalEvidence(db, state, oldWorkflow, Guid.NewGuid(),
            oldWorkflow.CompletedDate.Value.AddSeconds(-1));
        await db.SaveChangesAsync();
        var posting = await AddPostingAsync(db, state, state.SourceId);
        await service.BindOriginalPostingAsync(first.AuthorityId, posting.EventId, posting.JournalId);
        oldWorkflow.Status = WorkflowInstanceStatus.Cancelled;
        oldWorkflow.CompletedDate = null;
        var replacement = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(replacement);
        await db.SaveChangesAsync();
        var bound = () => service.FreezeResubmissionAsync(Request(state, workflowId: replacement.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), first.AuthorityId);
        await bound.Should().ThrowAsync<InvalidOperationException>().WithMessage("*POSTED_IMMUTABLE*");
    }

    [Fact]
    public async Task ThreeVersionHistory_OnlyRetriesLatestWorkflow()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var firstWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(firstWorkflow);
        await db.SaveChangesAsync();
        var first = await service.FreezeInitialPrimaryAsync(Request(state, workflowId: firstWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        firstWorkflow.Status = WorkflowInstanceStatus.Cancelled;
        var secondWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(secondWorkflow);
        await db.SaveChangesAsync();
        var second = await service.FreezeResubmissionAsync(Request(state, workflowId: secondWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), first.AuthorityId);
        var stalePosting = await AddPostingAsync(db, state, state.SourceId);
        var staleBind = () => service.BindOriginalPostingAsync(first.AuthorityId, stalePosting.EventId, stalePosting.JournalId);
        await staleBind.Should().ThrowAsync<InvalidOperationException>().WithMessage("*STALE*");
        secondWorkflow.Status = WorkflowInstanceStatus.Failed;
        var thirdWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress);
        db.WorkflowInstances.Add(thirdWorkflow);
        await db.SaveChangesAsync();
        var third = await service.FreezeResubmissionAsync(Request(state, workflowId: thirdWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), second.AuthorityId);

        secondWorkflow.Status = WorkflowInstanceStatus.InProgress;
        secondWorkflow.CompletedDate = null;
        await db.SaveChangesAsync();
        var staleSecondRetry = () => service.FreezeResubmissionAsync(Request(state, workflowId: secondWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), first.AuthorityId);
        await staleSecondRetry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*STALE*");

        var stale = () => service.FreezeInitialPrimaryAsync(Request(state, workflowId: firstWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        await stale.Should().ThrowAsync<InvalidOperationException>().WithMessage("*VERSION_REQUIRED*");
        var retry = await service.FreezeInitialPrimaryAsync(Request(state, workflowId: thirdWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted));
        retry.AuthorityId.Should().Be(third.AuthorityId);
        retry.AuthorityVersion.Should().Be(3);
    }

    [Fact]
    public async Task InheritedResubmission_CreatesNewVersionWithoutSwitchingBookAuthority()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var origin = await FreezeAndBindAsync(db, service, state, Guid.NewGuid());
        var settlement = Guid.NewGuid();
        var firstWorkflow = Workflow(state, WorkflowInstanceStatus.InProgress, sourceId: settlement);
        db.WorkflowInstances.Add(firstWorkflow);
        await db.SaveChangesAsync();
        var originRequest = new[] { new FinanceSourceBookAuthorityOriginRequest { OriginAuthorityId = origin.AuthorityId, Role = "INVOICE" } };
        var first = await service.FreezeInheritedAsync(Request(state, settlement, workflowId: firstWorkflow.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), originRequest);
        firstWorkflow.Status = WorkflowInstanceStatus.Cancelled;
        var replacement = Workflow(state, WorkflowInstanceStatus.InProgress, sourceId: settlement);
        db.WorkflowInstances.Add(replacement);
        await db.SaveChangesAsync();

        var second = await service.FreezeInheritedAsync(Request(state, settlement, workflowId: replacement.Id,
            stage: FinanceSourceBookAuthorityFreezeStages.Submitted), originRequest);

        second.AuthorityVersion.Should().Be(2);
        second.AccountingBookId.Should().Be(first.AccountingBookId);
        (await db.FinanceSourceBookAuthorities.SingleAsync(item => item.Id == second.AuthorityId))
            .SupersedesAuthorityId.Should().Be(first.AuthorityId);
    }

    [Theory]
    [InlineData(1, "AR")]
    [InlineData(0, "SALES")]
    public async Task Binding_RejectsWrongEffectiveDateOrJournalOrigin(int dateOffsetDays, string journalModule)
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var authority = await service.FreezeInitialPrimaryAsync(Request(state));
        var posting = await AddPostingAsync(db, state, state.SourceId, dateOffsetDays: dateOffsetDays,
            journalSourceModule: journalModule);

        var act = () => service.BindOriginalPostingAsync(authority.AuthorityId, posting.EventId, posting.JournalId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*POSTING_MISMATCH*");
    }

    [Fact]
    public async Task BindingRetry_RevalidatesFingerprintAndUnreversedOriginal()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var service = Service(db, state);
        var authority = await service.FreezeInitialPrimaryAsync(Request(state));
        var posting = await AddPostingAsync(db, state, state.SourceId);
        await service.BindOriginalPostingAsync(authority.AuthorityId, posting.EventId, posting.JournalId);
        var journal = await db.JournalEntries.SingleAsync(item => item.Id == posting.JournalId);
        journal.ReversalJournalEntryId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var reversed = () => service.BindOriginalPostingAsync(authority.AuthorityId, posting.EventId, posting.JournalId);
        await reversed.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ORIGINAL_JOURNAL_INVALID*");
        journal.ReversalJournalEntryId = null;
        await db.SaveChangesAsync();
        var entity = await db.FinanceSourceBookAuthorities.SingleAsync(item => item.Id == authority.AuthorityId);
        entity.AuthorityFingerprint = "TAMPERED";
        var tampered = () => service.BindOriginalPostingAsync(authority.AuthorityId, posting.EventId, posting.JournalId);
        await tampered.Should().ThrowAsync<InvalidOperationException>().WithMessage("*FINGERPRINT_INVALID*");
    }

    [Fact]
    public async Task Freeze_RejectsDefaultDateAndUnauthenticatedActor()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var invalidDate = Request(state, effectiveDate: DateTime.MinValue);
        var dateAct = () => Service(db, state).FreezeInitialPrimaryAsync(invalidDate);
        await dateAct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Effective date*");

        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.TenantId).Returns(state.TenantId);
        current.SetupGet(item => item.UserId).Returns(state.ActorId.ToString());
        current.SetupGet(item => item.IsAuthenticated).Returns(false);
        var actorAct = () => new FinanceSourceBookAuthorityService(db, current.Object)
            .FreezeInitialPrimaryAsync(Request(state));
        await actorAct.Should().ThrowAsync<UnauthorizedAccessException>();

        var posting = await AddPostingAsync(db, state, state.SourceId);
        var legacyAct = () => new FinanceSourceBookAuthorityService(db, current.Object)
            .RetainExistingPostedOriginalAsync(
                Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted),
                posting.JournalId, posting.EventId);
        await legacyAct.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task LegacyRetention_FiltersExplicitEventBeforeFullCardinalityCheck()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var posting = await AddPostingAsync(db, state, state.SourceId);
        FinancePostingEvent? fourth = null;
        for (var index = 0; index < 3; index++)
        {
            fourth = MatchingPostingEvent(state, state.SourceId, posting.JournalId);
            db.FinancePostingEvents.Add(fourth);
        }
        await db.SaveChangesAsync();
        var service = Service(db, state);
        var request = Request(state, stage: FinanceSourceBookAuthorityFreezeStages.LegacyPosted);

        var ambiguous = () => service.RetainExistingPostedOriginalAsync(request, posting.JournalId);
        await ambiguous.Should().ThrowAsync<InvalidOperationException>().WithMessage("*LEGACY_EVENT_AMBIGUOUS*");
        var retained = await service.RetainExistingPostedOriginalAsync(request, posting.JournalId, fourth!.Id);
        retained.OriginalFinancePostingEventId.Should().Be(fourth.Id);
    }

    [Fact]
    public async Task DbContext_RejectsMutationBeyondFirstPostingBinding()
    {
        await using var db = Context();
        var state = await SeedAsync(db);
        var authority = await Service(db, state).FreezeInitialPrimaryAsync(Request(state));
        var entity = await db.FinanceSourceBookAuthorities.SingleAsync(item => item.Id == authority.AuthorityId);
        entity.AccountingBookCode = "TAMPERED";

        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*immutable except*");
    }

    private static async Task<FinanceSourceBookAuthorityResult> FreezeAndBindAsync(ApplicationDbContext db,
        FinanceSourceBookAuthorityService service, State state, Guid sourceId)
    {
        var authority = await service.FreezeInitialPrimaryAsync(Request(state, sourceId));
        var posting = await AddPostingAsync(db, state, sourceId);
        return await service.BindOriginalPostingAsync(authority.AuthorityId, posting.EventId, posting.JournalId);
    }

    private static async Task<(Guid EventId, Guid JournalId)> AddPostingAsync(ApplicationDbContext db,
        State state, Guid sourceId, bool replica = false, int dateOffsetDays = 0, string journalSourceModule = "AR",
        bool reversed = false, string? functionalCurrency = null)
    {
        var postingDate = state.Date.AddDays(dateOffsetDays);
        functionalCurrency ??= state.Book.FunctionalCurrencyCode ?? "GHS";
        var journal = new JournalEntry
        {
            TenantId = state.TenantId, JournalEntryNumber = $"JE-{Guid.NewGuid():N}", JournalType = "AR",
            EntryDate = postingDate, Description = "authority test", SourceModule = journalSourceModule, SourceDocumentType = "Invoice",
            SourceDocumentId = sourceId, BookClassification = state.Book.Code, AccountingBookId = state.Book.Id,
            ReplicatedFromJournalEntryId = replica ? Guid.NewGuid() : null, FiscalPeriodId = Guid.NewGuid(),
            PostingStatus = "Posted", PostingDate = postingDate, IsBalanced = true,
            IsReversed = reversed, ReversalJournalEntryId = reversed ? Guid.NewGuid() : null
        };
        var postingEvent = new FinancePostingEvent
        {
            TenantId = state.TenantId, SourceModule = "AR", SourceDocumentType = "Invoice", SourceDocumentId = sourceId,
            PostingAction = "Post", JournalEntryId = journal.Id, PostingStatus = "Posted", PostingDate = postingDate,
            PostedAt = DateTime.UtcNow, FunctionalCurrencyCode = functionalCurrency, PrimaryTransactionCurrencyCode = "GHS",
            BookClassification = state.Book.Code, AccountingBookId = state.Book.Id
        };
        db.JournalEntries.Add(journal);
        db.FinancePostingEvents.Add(postingEvent);
        await db.SaveChangesAsync();
        return (postingEvent.Id, journal.Id);
    }

    private static FinancePostingEvent MatchingPostingEvent(State state, Guid sourceId, Guid journalId) => new()
    {
        TenantId = state.TenantId, SourceModule = "AR", SourceDocumentType = "Invoice", SourceDocumentId = sourceId,
        PostingAction = "Post", JournalEntryId = journalId, PostingStatus = "Posted", PostingDate = state.Date,
        PostedAt = DateTime.UtcNow, FunctionalCurrencyCode = "GHS", PrimaryTransactionCurrencyCode = "GHS",
        BookClassification = state.Book.Code, AccountingBookId = state.Book.Id
    };

    private static (WorkflowStepInstance Step, WorkflowApproval Approval) AddWorkflowApprovalEvidence(
        ApplicationDbContext db, State state, WorkflowInstance workflow, Guid processorId,
        DateTime processedAt, WorkflowApprovalStatus status = WorkflowApprovalStatus.Approved)
    {
        var step = new WorkflowStepInstance
        {
            TenantId = state.TenantId, WorkflowInstanceId = workflow.Id, WorkflowStepId = Guid.NewGuid(),
            Status = WorkflowStepInstanceStatus.Completed, CompletedDate = processedAt
        };
        var approval = new WorkflowApproval
        {
            TenantId = state.TenantId, StepInstanceId = step.Id, ApproverId = processorId,
            Status = status, RequestedDate = processedAt.AddMinutes(-1), ProcessedDate = processedAt,
            ProcessedById = processorId
        };
        db.WorkflowStepInstances.Add(step);
        db.WorkflowApprovals.Add(approval);
        return (step, approval);
    }

    private static WorkflowInstance Workflow(State state, WorkflowInstanceStatus status,
        Guid? typeId = null, Guid? sourceId = null) => new()
    {
        TenantId = state.TenantId, WorkflowDefinitionId = Guid.NewGuid(), EntityId = sourceId ?? state.SourceId,
        EntityTypeId = typeId ?? state.WorkflowTypeId, Status = status, InitiatedById = state.ActorId,
        CompletedDate = status == WorkflowInstanceStatus.Completed ? DateTime.UtcNow : null
    };

    private static FinanceSourceBookAuthorityFreezeRequest Request(State state, Guid? sourceId = null,
        string currency = "GHS", Guid? workflowId = null,
        string stage = FinanceSourceBookAuthorityFreezeStages.PrePost, DateTime? effectiveDate = null,
        string sourceDocumentType = "Invoice", string? workflowEntityType = null) => new()
    {
        OriginModuleCode = "FIN", SourceDocumentType = sourceDocumentType, SourceDocumentId = sourceId ?? state.SourceId,
        PostingAction = "Post", EffectiveDate = effectiveDate ?? state.Date, TransactionCurrencyCode = currency,
        FreezeStage = stage, SourceWorkflowInstanceId = workflowId,
        SourceWorkflowEntityType = workflowEntityType
    };

    private static FinanceSourceBookAuthorityService Service(ApplicationDbContext db, State state)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(item => item.TenantId).Returns(state.TenantId);
        current.SetupGet(item => item.UserId).Returns(state.ActorId.ToString());
        current.SetupGet(item => item.UserName).Returns("source-authority.test");
        current.SetupGet(item => item.IsAuthenticated).Returns(true);
        return new FinanceSourceBookAuthorityService(db, current.Object);
    }

    private static async Task<State> SeedAsync(ApplicationDbContext db)
    {
        var state = new State(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 9, 29), null!);
        var book = Book(state.TenantId, "BASE", true);
        state = state with { Book = book };
        db.Tenants.Add(new Tenant { Id = state.TenantId, Name = "Tenant", Code = "TEN", BaseCurrency = "GHS" });
        db.FinanceSettings.Add(new FinanceSettings { TenantId = state.TenantId, BaseCurrency = "GHS" });
        db.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = state.WorkflowTypeId, TenantId = state.TenantId, Code = "INVOICE", Name = "Invoice", IsActive = true
        });
        db.AccountingBooks.Add(book);
        await db.SaveChangesAsync();
        return state;
    }

    private static AccountingBook Book(Guid tenantId, string code, bool isDefault) => new()
    {
        TenantId = tenantId, Code = code, Name = code, BookType = AccountingBookType.PrimaryFull,
        LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS",
        IsDefault = isDefault, IsActive = true, AllowsPosting = true
    };

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"finance-source-book-{Guid.NewGuid():N}").Options);
    private sealed record State(Guid TenantId, Guid ActorId, Guid SourceId, Guid WorkflowTypeId,
        DateTime Date, AccountingBook Book);
}
