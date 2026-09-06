using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.Settings;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookApplicabilityC5Tests
{
    [Fact]
    public void Controller_SeparatesConfigurationResolutionAndApprovalPermissions_AndHasNoDelete()
    {
        var methods = typeof(AccountingBookApplicabilityController).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        methods.Single(item => item.Name == nameof(AccountingBookApplicabilityController.GetEligibleBooks)).GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(FinancePermissions.ViewAccountingBookApplicabilityPolicy);
        methods.Single(item => item.Name == nameof(AccountingBookApplicabilityController.CreateDraft)).GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(FinancePermissions.ManageAccountingBookApplicabilityPolicy);
        methods.Single(item => item.Name == nameof(AccountingBookApplicabilityController.Approve)).GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(FinancePermissions.ApproveAccountingBookApplicabilityPolicy);
        methods.Single(item => item.Name == nameof(AccountingBookApplicabilityController.Resolve)).GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be(FinancePermissions.ResolveAccountingBookApplicability);
        methods.Should().NotContain(item => item.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NoRule_DefaultsToExactlyPrimary_AndReturnsReadinessBlockersWithoutDroppingIt()
    {
        await using var db = Context(); var state = Seed(db, ready: false); await db.SaveChangesAsync();
        var result = await Service(db, state.TenantId).ResolveAsync(Input());
        result.UsedPrimaryOnlyFallback.Should().BeTrue();
        result.Books.Should().ContainSingle().Which.AccountingBookId.Should().Be(state.Primary.Id);
        result.Books.Should().NotContain(item => item.AccountingBookId == state.Parallel.Id);
        result.Blockers.Should().Contain(item => item.AccountingBookId == state.Primary.Id && item.Code == "BOOK_INITIALIZATION_NOT_RECONCILED");
    }

    [Fact]
    public async Task ExplicitRule_SelectsOrderedFullBooks_AndRetryFingerprintIsStable()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        AddApprovedPolicy(db, state, state.Parallel.Id, state.Primary.Id); await db.SaveChangesAsync();
        var service = Service(db, state.TenantId);
        var first = await service.ResolveAsync(Input()); var retry = await service.ResolveAsync(Input());
        first.UsedPrimaryOnlyFallback.Should().BeFalse();
        first.Blockers.Should().BeEmpty();
        first.Books.Select(item => item.AccountingBookId).Should().Equal(state.Parallel.Id, state.Primary.Id);
        retry.CalculationInputHash.Should().Be(first.CalculationInputHash);
        retry.SelectionFingerprint.Should().Be(first.SelectionFingerprint);
    }

    [Fact]
    public async Task EqualHighestPriorityRulesFailClosed_AndDeltaTargetsAreRejectedAtDraftBoundary()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        AddApprovedPolicy(db, state, state.Primary.Id); AddApprovedPolicy(db, state, state.Parallel.Id); await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId).ResolveAsync(Input()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("AMBIGUOUS_ACCOUNTING_BOOK_APPLICABILITY:*");

        var delta = new AccountingBook { TenantId = state.TenantId, Code = "DELTA", Name = "Delta", Purpose = "Adjustment",
            BookType = AccountingBookType.Delta, LifecycleStatus = AccountingBookLifecycleStatus.Configuring, BaseAccountingBookId = state.Primary.Id,
            IsActive = false, AllowsPosting = false };
        db.AccountingBooks.Add(delta); await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId).CreateDraftAsync(Draft(delta.Id)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("DELTA_BOOK_AUTOMATIC_APPLICABILITY_FORBIDDEN:*");
    }

    [Fact]
    public async Task FrozenRetryIsIdempotent_ButChangedSelectionEvidenceConflicts()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var service = Service(db, state.TenantId);
        var preview = await service.ResolveAsync(Input());
        var request = new FreezeAccountingBookSelectionDto { EffectiveDate = state.Date, OriginatingModuleCode = " inv ",
            SourceDocumentType = "goods.receipt", PostingAction = "post", IdempotencyKey = "source-1" };
        request.ExpectedCalculationInputHash = preview.CalculationInputHash; request.ExpectedSelectionFingerprint = preview.SelectionFingerprint;
        var first = await service.FreezeAsync(request); var retry = await service.FreezeAsync(request);
        retry.SelectionFingerprint.Should().Be(first.SelectionFingerprint);
        db.AccountingBookSelectionEvidence.Should().ContainSingle();
        request.SourceDocumentType = "OTHER.DOCUMENT";
        await FluentActions.Awaiting(() => service.FreezeAsync(request)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_SELECTION_IDEMPOTENCY_CONFLICT:*");
        request.SourceDocumentType = "goods.receipt";
        request.ExpectedSelectionFingerprint = new string('C', 64);
        await FluentActions.Awaiting(() => service.FreezeAsync(request)).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_SELECTION_IDEMPOTENCY_CONFLICT:*");
    }

    [Fact]
    public async Task ApprovalCheckerMustDifferFromLastDraftEditor()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var creator = Guid.NewGuid(); var editor = Guid.NewGuid();
        var created = await Service(db, state.TenantId, creator).CreateDraftAsync(Draft(state.Primary.Id));
        var entity = db.AccountingBookApplicabilityPolicies.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();
        var changed = Draft(state.Primary.Id); changed.Name = "Edited policy"; changed.RowVersion = Convert.ToBase64String([1]);
        await Service(db, state.TenantId, editor).UpdateDraftAsync(created.Id, changed);
        entity.RowVersion = [2]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, editor).SubmitAsync(created.Id, new() { Reason = "Submit", RowVersion = Convert.ToBase64String([2]) });
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId, editor).ApproveAsync(created.Id,
            new() { Reason = "Self approval", RowVersion = Convert.ToBase64String([3]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*last substantive Draft editor*");
    }

    [Fact]
    public async Task StaleDraftUpdateIsRejectedWithoutChangingGovernedEvidence()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var created = await Service(db, state.TenantId).CreateDraftAsync(Draft(state.Primary.Id));
        var entity = db.AccountingBookApplicabilityPolicies.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();
        var stale = Draft(state.Primary.Id); stale.Name = "Stale edit"; stale.RowVersion = Convert.ToBase64String([2]);

        await FluentActions.Awaiting(() => Service(db, state.TenantId).UpdateDraftAsync(created.Id, stale))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
        db.AccountingBookApplicabilityPolicies.Single().Name.Should().Be("Inventory policy");
    }

    [Fact]
    public async Task ReadsDoNotWrite_AndEligibleBooksExcludeDelta()
    {
        await using var db = Context(); var state = Seed(db, ready: true);
        db.AccountingBooks.Add(new AccountingBook { TenantId = state.TenantId, Code = "DELTA", Name = "Delta", BookType = AccountingBookType.Delta,
            Purpose = "Adjustment", BaseAccountingBookId = state.Primary.Id, LifecycleStatus = AccountingBookLifecycleStatus.Configuring, IsActive = false, AllowsPosting = false });
        await db.SaveChangesAsync(); var before = db.ChangeTracker.Entries().Count();
        var service = Service(db, state.TenantId); (await service.GetPoliciesAsync()).Should().BeEmpty();
        (await service.GetEligibleBooksAsync()).Should().HaveCount(2).And.OnlyContain(item => item.BookType != "Delta");
        db.AccountingBookApplicabilityPolicies.Should().BeEmpty(); db.AccountingBookSelectionEvidence.Should().BeEmpty();
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("ALL")]
    public async Task ResolveRejectsUnknownOrPseudoModule(string module)
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var input = Input(); input.OriginatingModuleCode = module;
        await FluentActions.Awaiting(() => Service(db, state.TenantId).ResolveAsync(input)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FreezeRequiresCanonicalPreviewHashes()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var request = new FreezeAccountingBookSelectionDto { EffectiveDate = state.Date, OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", IdempotencyKey = "missing-preview" };
        await FluentActions.Awaiting(() => Service(db, state.TenantId).FreezeAsync(request)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*preview calculation input hash*");
        request.ExpectedCalculationInputHash = "abc"; request.ExpectedSelectionFingerprint = new string('A', 64);
        await FluentActions.Awaiting(() => Service(db, state.TenantId).FreezeAsync(request)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*preview calculation input hash*");
        db.AccountingBookSelectionEvidence.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirementRejectCanBeFollowedByNewMakerAndIndependentApproval()
    {
        await using var db = Context(); var state = Seed(db, ready: true); AddApprovedPolicy(db, state, state.Primary.Id); await db.SaveChangesAsync();
        var entity = db.AccountingBookApplicabilityPolicies.Single(); var firstMaker = Guid.NewGuid(); var checker = Guid.NewGuid();
        entity.RowVersion = [1]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, firstMaker).RetireAsync(entity.Id, new() { Reason = "First request", RowVersion = Convert.ToBase64String([1]) });
        entity.RowVersion = [2]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId, firstMaker).RejectRetirementAsync(entity.Id, new() { Reason = "Self", RowVersion = Convert.ToBase64String([2]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");
        await Service(db, state.TenantId, checker).RejectRetirementAsync(entity.Id, new() { Reason = "Reject", RowVersion = Convert.ToBase64String([2]) });
        entity.RetirementDecisionStatus.Should().Be("Rejected"); entity.RowVersion = [3]; await db.SaveChangesAsync();
        var secondMaker = Guid.NewGuid(); await Service(db, state.TenantId, secondMaker).RetireAsync(entity.Id, new() { Reason = "Re-request", RowVersion = Convert.ToBase64String([3]) });
        entity.RetirementDecisionStatus.Should().Be("Pending"); entity.RetirementDecidedByUserId.Should().BeNull(); entity.RowVersion = [4]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, checker).ApproveRetirementAsync(entity.Id, new() { Reason = "Approve", RowVersion = Convert.ToBase64String([4]) });
        entity.PolicyStatus.Should().Be(AccountingBookApplicabilityPolicyStatus.Retired); entity.RetirementDecisionStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task ApprovedSuccessorShadowsOpenPredecessorAtInclusiveBoundaryWithoutMutatingHistory()
    {
        await using var db = Context(); var state = Seed(db, ready: true);
        AddApprovedPolicy(db, state, [state.Primary.Id], policyCode: "REPLACEMENT", effectiveFrom: new DateTime(2026, 9, 1));
        await db.SaveChangesAsync(); var predecessor = db.AccountingBookApplicabilityPolicies.Single();
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid(); var draft = Draft(state.Parallel.Id);
        draft.PolicyCode = "REPLACEMENT"; draft.EffectiveFrom = new DateTime(2026, 9, 6);
        var successor = await Service(db, state.TenantId, maker).CreateDraftAsync(draft);
        var successorEntity = db.AccountingBookApplicabilityPolicies.Single(item => item.Id == successor.Id);
        successorEntity.RowVersion = [1]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, maker).SubmitAsync(successor.Id, new() { Reason = "Submit replacement", RowVersion = Convert.ToBase64String([1]) });
        successorEntity.RowVersion = [2]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, checker).ApproveAsync(successor.Id, new() { Reason = "Approve replacement", RowVersion = Convert.ToBase64String([2]) });

        var before = Input(); before.EffectiveDate = new DateTime(2026, 9, 5);
        var at = Input(); at.EffectiveDate = new DateTime(2026, 9, 6);
        var after = Input(); after.EffectiveDate = new DateTime(2026, 9, 7);
        (await Service(db, state.TenantId).ResolveAsync(before)).Books.Single().AccountingBookId.Should().Be(state.Primary.Id);
        (await Service(db, state.TenantId).ResolveAsync(at)).Books.Single().AccountingBookId.Should().Be(state.Parallel.Id);
        (await Service(db, state.TenantId).ResolveAsync(after)).Books.Single().AccountingBookId.Should().Be(state.Parallel.Id);
        predecessor.EffectiveTo.Should().BeNull();
        db.AccountingBookSelectionEvidence.Should().BeEmpty();
    }

    [Fact]
    public async Task CorruptExplicitZeroBookRuleFailsClosedAndFreezeWritesNothing()
    {
        await using var db = Context(); var state = Seed(db, ready: true);
        AddApprovedPolicy(db, state, [], policyCode: "EMPTY", effectiveFrom: new DateTime(2026, 1, 1)); await db.SaveChangesAsync();
        var service = Service(db, state.TenantId);
        await FluentActions.Awaiting(() => service.ResolveAsync(Input())).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("ACCOUNTING_BOOK_APPLICABILITY_EMPTY_SELECTION:*");
        db.AccountingBookSelectionEvidence.Should().BeEmpty();
    }

    [Fact]
    public async Task ClassificationTypeDriftChangesFingerprintBlocksFreezeAndCompatibleRecoverySucceeds()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var service = Service(db, state.TenantId); var original = await service.ResolveAsync(Input());
        var classification = db.AccountClassifications.Single(item => item.AccountingBookId == state.Primary.Id);
        classification.CoreAccountType = AccountType.Liability; await db.SaveChangesAsync();
        var drifted = await service.ResolveAsync(Input());
        drifted.Blockers.Should().ContainSingle(item => item.Code == "BOOK_MAPPING_CLASSIFICATION_NOT_READY");
        drifted.SelectionFingerprint.Should().NotBe(original.SelectionFingerprint);
        await FluentActions.Awaiting(() => service.FreezeAsync(new() { EffectiveDate = state.Date, OriginatingModuleCode = "INV",
            SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", IdempotencyKey = "type-drift",
            ExpectedCalculationInputHash = original.CalculationInputHash, ExpectedSelectionFingerprint = original.SelectionFingerprint }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_BOOK_SELECTION_EVIDENCE_CONFLICT:*");
        db.AccountingBookSelectionEvidence.Should().BeEmpty();
        classification.CoreAccountType = AccountType.Asset; await db.SaveChangesAsync();
        var recovered = await service.ResolveAsync(Input()); recovered.Blockers.Should().BeEmpty();
        var frozen = await service.FreezeAsync(new() { EffectiveDate = state.Date, OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT",
            PostingAction = "POST", IdempotencyKey = "type-drift", ExpectedCalculationInputHash = recovered.CalculationInputHash,
            ExpectedSelectionFingerprint = recovered.SelectionFingerprint });
        frozen.Books.Should().ContainSingle(); db.AccountingBookSelectionEvidence.Should().ContainSingle();
    }

    [Fact]
    public async Task PolicyAuditContainsReconstructibleRuleBookAndSupersessionEvidence()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        FinanceAuditEventDto? recorded = null; var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinanceAuditEventDto, CancellationToken>((item, _) => recorded = item).ReturnsAsync(new AuditLog());
        var request = Draft(state.Primary.Id); request.Description = "Auditable policy description";
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid(); var retirementMaker = Guid.NewGuid(); var retirementChecker = Guid.NewGuid();
        var created = await Service(db, state.TenantId, maker, audit.Object).CreateDraftAsync(request);
        var entity = db.AccountingBookApplicabilityPolicies.Single(); entity.RowVersion = [1]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, maker, audit.Object).SubmitAsync(created.Id,
            new() { Reason = "Submit audit policy", RowVersion = Convert.ToBase64String([1]) });
        entity.RowVersion = [2]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, checker, audit.Object).ApproveAsync(created.Id,
            new() { Reason = "Approve audit policy", RowVersion = Convert.ToBase64String([2]) });
        entity.RowVersion = [3]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, retirementMaker, audit.Object).RetireAsync(created.Id,
            new() { Reason = "Request retirement", RowVersion = Convert.ToBase64String([3]) });
        entity.RowVersion = [4]; await db.SaveChangesAsync();
        await Service(db, state.TenantId, retirementChecker, audit.Object).ApproveRetirementAsync(created.Id,
            new() { Reason = "Approve retirement", RowVersion = Convert.ToBase64String([4]) });
        var json = JsonSerializer.Serialize(recorded!.AfterValues);
        json.Should().Contain("INVENTORY_POLICY").And.Contain("GRN_POST").And.Contain("GOODS.RECEIPT")
            .And.Contain(state.Primary.Id.ToString()).And.Contain("IFRS").And.Contain("Auditable policy description")
            .And.Contain("PreparedAtUtc").And.Contain("ApprovedAtUtc").And.Contain("RetirementWorkflowInstanceId")
            .And.Contain("RowVersion").And.Contain("SelectionOrder").And.Contain(maker.ToString()).And.Contain(checker.ToString())
            .And.Contain(retirementMaker.ToString()).And.Contain(retirementChecker.ToString());
    }

    [Fact]
    public async Task RetirementPreservesEarlierBound_AndOpenIntervalClosesInclusivelyWithoutExpandingHistory()
    {
        var today = DateTime.UtcNow.Date;
        await using var boundedDb = Context(); var boundedState = Seed(boundedDb, ready: true);
        AddApprovedPolicy(boundedDb, boundedState, [boundedState.Parallel.Id], "BOUNDED", today.AddDays(-10));
        await boundedDb.SaveChangesAsync();
        var bounded = boundedDb.AccountingBookApplicabilityPolicies.Single(); bounded.EffectiveTo = today.AddDays(-5);
        await boundedDb.SaveChangesAsync(); bounded.RowVersion = [1]; await boundedDb.SaveChangesAsync();
        var maker = Guid.NewGuid(); var checker = Guid.NewGuid();
        await Service(boundedDb, boundedState.TenantId, maker).RetireAsync(bounded.Id, new() { Reason = "Retire expired policy", RowVersion = Convert.ToBase64String([1]) });
        bounded.RowVersion = [2]; await boundedDb.SaveChangesAsync();
        await Service(boundedDb, boundedState.TenantId, checker).ApproveRetirementAsync(bounded.Id, new() { Reason = "Approve retirement", RowVersion = Convert.ToBase64String([2]) });
        bounded.EffectiveTo.Should().Be(today.AddDays(-5));
        var before = Input(); before.EffectiveDate = today.AddDays(-6);
        var at = Input(); at.EffectiveDate = today.AddDays(-5);
        var after = Input(); after.EffectiveDate = today.AddDays(-4);
        (await Service(boundedDb, boundedState.TenantId).ResolveAsync(before)).Books.Single().AccountingBookId.Should().Be(boundedState.Parallel.Id);
        (await Service(boundedDb, boundedState.TenantId).ResolveAsync(at)).Books.Single().AccountingBookId.Should().Be(boundedState.Parallel.Id);
        (await Service(boundedDb, boundedState.TenantId).ResolveAsync(after)).Books.Single().AccountingBookId.Should().Be(boundedState.Primary.Id);

        await using var openDb = Context(); var openState = Seed(openDb, ready: true);
        AddApprovedPolicy(openDb, openState, [openState.Parallel.Id], "OPEN", today.AddDays(-10)); await openDb.SaveChangesAsync();
        var open = openDb.AccountingBookApplicabilityPolicies.Single(); open.RowVersion = [1]; await openDb.SaveChangesAsync();
        await Service(openDb, openState.TenantId, maker).RetireAsync(open.Id, new() { Reason = "Close current interval", RowVersion = Convert.ToBase64String([1]) });
        open.RowVersion = [2]; await openDb.SaveChangesAsync();
        await Service(openDb, openState.TenantId, checker).ApproveRetirementAsync(open.Id, new() { Reason = "Approve interval close", RowVersion = Convert.ToBase64String([2]) });
        open.EffectiveTo.Should().Be(today);
        var openAt = Input(); openAt.EffectiveDate = today;
        var openAfter = Input(); openAfter.EffectiveDate = today.AddDays(1);
        (await Service(openDb, openState.TenantId).ResolveAsync(openAt)).Books.Single().AccountingBookId.Should().Be(openState.Parallel.Id);
        (await Service(openDb, openState.TenantId).ResolveAsync(openAfter)).Books.Single().AccountingBookId.Should().Be(openState.Primary.Id);
    }

    [Fact]
    public async Task FutureEffectiveRetirementFailsBeforeWorkflowOrAuditMutation()
    {
        await using var db = Context(); var state = Seed(db, ready: true);
        AddApprovedPolicy(db, state, [state.Parallel.Id], "FUTURE", DateTime.UtcNow.Date.AddDays(2)); await db.SaveChangesAsync();
        var policy = db.AccountingBookApplicabilityPolicies.Single(); policy.RowVersion = [1]; await db.SaveChangesAsync();
        var audit = new Mock<IFinanceAuditService>();
        await FluentActions.Awaiting(() => Service(db, state.TenantId, audit: audit.Object).RetireAsync(policy.Id,
            new() { Reason = "Cancel future authority", RowVersion = Convert.ToBase64String([1]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_BOOK_APPLICABILITY_FUTURE_RETIREMENT_FORBIDDEN:*");
        policy.PolicyStatus.Should().Be(AccountingBookApplicabilityPolicyStatus.Approved);
        policy.EffectiveTo.Should().BeNull(); policy.RetirementDecisionStatus.Should().BeNull();
        audit.Verify(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MalformedVersionLineageCannotBeSubmittedOrWinResolution()
    {
        await using var db = Context(); var state = Seed(db, ready: true);
        AddApprovedPolicy(db, state, [state.Parallel.Id], "MALFORMED", new DateTime(2026, 1, 1), version: 3);
        await db.SaveChangesAsync();
        var malformed = db.AccountingBookApplicabilityPolicies.Single();
        await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId).ResolveAsync(Input()))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID:*");

        malformed.PolicyStatus = AccountingBookApplicabilityPolicyStatus.Draft; malformed.RowVersion = [1]; await db.SaveChangesAsync();
        await FluentActions.Awaiting(() => Service(db, state.TenantId).SubmitAsync(malformed.Id,
            new() { Reason = "Submit malformed version", RowVersion = Convert.ToBase64String([1]) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_BOOK_APPLICABILITY_VERSION_LINEAGE_INVALID:*");
    }

    [Fact]
    public async Task FrozenSelectionAuditIsComplete_AndValidationFailureWritesNoAudit()
    {
        await using var db = Context(); var state = Seed(db, ready: true); await db.SaveChangesAsync();
        var events = new List<FinanceAuditEventDto>(); var audit = new Mock<IFinanceAuditService>();
        audit.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinanceAuditEventDto, CancellationToken>((item, _) => events.Add(item)).ReturnsAsync(new AuditLog());
        var service = Service(db, state.TenantId, audit: audit.Object); var preview = await service.ResolveAsync(Input());
        await service.FreezeAsync(new() { EffectiveDate = state.Date, OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT",
            PostingAction = "POST", IdempotencyKey = "audit-freeze", ExpectedCalculationInputHash = preview.CalculationInputHash,
            ExpectedSelectionFingerprint = preview.SelectionFingerprint });
        var frozen = events.Single(item => item.EventType == FinanceAuditEvents.AccountingBookSelectionFrozen);
        var json = JsonSerializer.Serialize(frozen.AfterValues);
        frozen.IdempotencyKey.Should().Be("AUDIT-FREEZE");
        json.Should().Contain("FrozenByUserId").And.Contain("FrozenAtUtc").And.Contain("AccountingBookApplicabilityPolicyId")
            .And.Contain("AccountingBookApplicabilityRuleId").And.Contain("PolicyVersion").And.Contain("CalculationInputHash")
            .And.Contain("SelectionFingerprint").And.Contain("AuthorityFingerprint").And.Contain("SelectionOrder");

        var invalid = Draft();
        await FluentActions.Awaiting(() => service.CreateDraftAsync(invalid)).Should().ThrowAsync<InvalidOperationException>();
        events.Should().ContainSingle();
    }

    private static ResolveAccountingBookApplicabilityDto Input() => new() { EffectiveDate = new DateTime(2026, 9, 6),
        OriginatingModuleCode = "inv", SourceDocumentType = "goods.receipt", PostingAction = "post" };
    private static SaveAccountingBookApplicabilityPolicyDto Draft(params Guid[] books) => new() { PolicyCode = "INVENTORY_POLICY", Name = "Inventory policy",
        EffectiveFrom = new DateTime(2026, 1, 1), Reason = "Govern representations", Rules = [new() { RuleCode = "GRN_POST", Priority = 100,
            OriginatingModuleCode = "INV", SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", AccountingBookIds = books }] };
    private static void AddApprovedPolicy(ApplicationDbContext db, State state, Guid[] books, string? policyCode = null,
        DateTime? effectiveFrom = null, int version = 1, Guid? supersedesPolicyId = null)
    {
        var policy = new AccountingBookApplicabilityPolicy { TenantId = state.TenantId, PolicyCode = policyCode ?? $"P{Guid.NewGuid():N}"[..20].ToUpperInvariant(), Version = version,
            SupersedesPolicyId = supersedesPolicyId,
            Name = "Approved", EffectiveFrom = effectiveFrom ?? new DateTime(2026, 1, 1), PolicyStatus = AccountingBookApplicabilityPolicyStatus.Approved,
            Reason = "Approved", CreatedByUserId = Guid.NewGuid(), PreparedByUserId = Guid.NewGuid(), PreparedAtUtc = DateTime.UtcNow, ApprovedByUserId = Guid.NewGuid(), ApprovedAtUtc = DateTime.UtcNow };
        var rule = new AccountingBookApplicabilityRule { TenantId = state.TenantId, RuleCode = "RULE", Priority = 100, OriginatingModuleCode = "INV",
            SourceDocumentType = "GOODS.RECEIPT", PostingAction = "POST", SortOrder = 0 };
        var order = 0;
        foreach (var id in books) { var book = id == state.Primary.Id ? state.Primary : state.Parallel; rule.SelectedBooks.Add(new()
            { TenantId = state.TenantId, AccountingBookId = id, AccountingBookCodeSnapshot = book.Code, SelectionOrder = order++ }); }
        policy.Rules.Add(rule); db.AccountingBookApplicabilityPolicies.Add(policy);
    }
    private static void AddApprovedPolicy(ApplicationDbContext db, State state, params Guid[] books) => AddApprovedPolicy(db, state, books, null, null);
    private static State Seed(ApplicationDbContext db, bool ready)
    {
        var tenant = Guid.NewGuid(); var date = new DateTime(2026, 9, 6);
        var primary = Book(tenant, "IFRS", AccountingBookType.PrimaryFull, true); var parallel = Book(tenant, "LOCAL", AccountingBookType.ParallelFull, false);
        db.AccountingBooks.AddRange(primary, parallel);
        var year = new FiscalYear { TenantId = tenant, FiscalYearName = "FY26", FiscalYearCode = "2026", Year = 2026, FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), TotalDays = 365, NumberOfPeriods = 12, Status = "Open", IsActive = true };
        var period = new FiscalPeriod { TenantId = tenant, FiscalYear = year, PeriodName = "September", PeriodCode = "2026-09", PeriodNumber = 9,
            StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 9, 30), PeriodDays = 30, PeriodStatus = "Open", IsOpen = true };
        db.FiscalPeriods.Add(period);
        db.ModuleDefinitions.Add(new ModuleDefinition { TenantId = tenant, ModuleCode = "INV", ModuleName = "Inventory", IsActive = true });
        foreach (var book in new[] { primary, parallel })
        {
            if (ready) db.AccountingBookInitializations.Add(new() { TenantId = tenant, AccountingBookId = book.Id, Version = 1,
                InitializationStatus = AccountingBookInitializationStatus.Approved, CutoffDate = new DateTime(2026, 8, 31), CutoffFiscalPeriodId = period.Id,
                IdempotencyKey = $"init-{book.Code}", Reason = "Ready", EvidenceFingerprint = new string('A', 64), ReconciliationFingerprint = new string('B', 64), PreparedByUserId = Guid.NewGuid() });
            db.AccountingBookPeriods.Add(new() { TenantId = tenant, AccountingBookId = book.Id, FiscalPeriodId = period.Id, PeriodStatus = AccountingBookPeriodStatus.Open });
            var account = new Account { TenantId = tenant, AccountCode = $"A-{book.Code}", AccountNumber = $"A-{book.Code}", AccountName = "Account", AccountType = AccountType.Asset, CurrencyCode = "GHS", Status = AccountStatus.Active };
            var classification = new AccountClassification { TenantId = tenant, AccountingBookId = book.Id, Code = $"C_{book.Code}", Name = "Class", CoreAccountType = AccountType.Asset, Status = AccountClassificationStatus.Active, IsPostingClassification = true };
            db.Accounts.Add(account); db.AccountClassifications.Add(classification); db.AccountAccountingBooks.Add(new() { TenantId = tenant, AccountingBookId = book.Id, AccountId = account.Id, AccountClassificationId = classification.Id, IsEnabled = true });
        }
        return new(tenant, primary, parallel, date);
    }
    private static AccountingBook Book(Guid tenant, string code, AccountingBookType type, bool primary) => new() { TenantId = tenant, Code = code, Name = code,
        Purpose = "Reporting", BookType = type, LifecycleStatus = AccountingBookLifecycleStatus.Active, FunctionalCurrencyCode = "GHS", IsDefault = primary,
        IsActive = true, AllowsPosting = true };
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"c5-{Guid.NewGuid():N}").Options);
    private static IAccountingBookApplicabilityService Service(ApplicationDbContext db, Guid tenant, Guid? actor = null, IFinanceAuditService? audit = null)
    {
        var actorId = actor ?? Guid.NewGuid(); var user = new Mock<ICurrentUserService>(); user.SetupGet(item => item.TenantId).Returns(tenant); user.SetupGet(item => item.UserId).Returns(actorId.ToString()); user.SetupGet(item => item.UserName).Returns("c5.test");
        var workflow = new Mock<IWorkflowService>(); workflow.Setup(item => item.HasActiveApprovalWorkflowAsync(It.IsAny<string>())).ReturnsAsync(true);
        workflow.Setup(item => item.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>())).ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = Guid.NewGuid() });
        workflow.Setup(item => item.CanUserApproveAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(true);
        workflow.Setup(item => item.ProcessApprovalStepAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        if (audit == null) { var auditMock = new Mock<IFinanceAuditService>(); auditMock.Setup(item => item.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AuditLog()); audit = auditMock.Object; }
        var initialization = new Mock<IAccountingBookInitializationService>(); initialization.Setup(item => item.ValidateCurrentApprovedEvidenceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new AccountingBookInitializationEvidenceValidationDto { IsValid = db.AccountingBookInitializations.Any(item => item.AccountingBookId == id),
                InitializationId = id, Version = 1, EvidenceFingerprint = new string('A', 64), ReconciliationFingerprint = new string('B', 64), Blocker = "Missing current approved initialization." });
        return new AccountingBookApplicabilityService(db, user.Object, workflow.Object, audit, initialization.Object);
    }
    private sealed record State(Guid TenantId, AccountingBook Primary, AccountingBook Parallel, DateTime Date);
}
