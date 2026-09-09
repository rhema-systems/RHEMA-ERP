using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerIntentC7Tests
{
    [Fact]
    public void PublicContract_HasNoBookSelector_AndHttpSurfaceCannotExecuteOwnerEffects()
    {
        typeof(ProducerFinancePostingRequestDto).GetProperty("AccountingBookCode").Should().BeNull();
        typeof(ProducerAccountingIntentDto).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Book", StringComparison.OrdinalIgnoreCase));
        typeof(ProducerAccountingIntentsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Should().NotContain(method => method.Name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        typeof(ProducerAccountingIntentsController).GetMethod(nameof(ProducerAccountingIntentsController.Prepare))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.PrepareAccountingEvents);
        foreach (var name in new[] { nameof(ProducerAccountingIntentsController.Approve), nameof(ProducerAccountingIntentsController.Reject) })
        {
            typeof(ProducerAccountingIntentsController).GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>()!
                .Policy.Should().Be(FinancePermissions.OrchestrateAccountingEvents);
            typeof(ProducerAccountingIntentsController).GetMethod(name)!.GetParameters()[1].ParameterType
                .Should().Be(typeof(DecideProducerAccountingIntentDto),
                    "a checker supplies only durable identity plus decision evidence");
        }
        typeof(ProducerAccountingIntentsController).GetMethod(nameof(ProducerAccountingIntentsController.Get))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewAccountingEvents);
        typeof(DecideProducerAccountingIntentDto).GetProperties().Select(property => property.Name)
            .Should().Equal(nameof(DecideProducerAccountingIntentDto.Reason));
    }

    [Fact]
    public async Task DisabledByDefault_DeniesBeforeApplicabilityOrC6()
    {
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        var events = new Mock<IAccountingEventService>(MockBehavior.Strict);
        var service = Harness(applicability, events, enabled: false).Service;

        var action = () => service.PrepareAsync(Intent());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_PRODUCER_INTENT_DISABLED*");
        applicability.VerifyNoOtherCalls();
        events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DurableDecision_DisabledByDefault_DeniesBeforeStoredEvidenceLookup()
    {
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        var events = new Mock<IAccountingEventService>(MockBehavior.Strict);
        var harness = Harness(applicability, events, enabled: false);

        await FluentActions.Awaiting(() => harness.Service.ApprovePreparedAsync(Guid.NewGuid(),
            new DecideProducerAccountingIntentDto { Reason = "Reviewed" })).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("FINANCE_PRODUCER_INTENT_DISABLED*");

        harness.Db.ChangeTracker.Entries().Should().BeEmpty();
        applicability.VerifyNoOtherCalls();
        events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CompatibilityExecution_DisabledByDefault_DeniesBeforeC6()
    {
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        var events = new Mock<IAccountingEventService>(MockBehavior.Strict);
        var harness = Harness(applicability, events, enabled: false);

        var action = () => ((IFinanceProducerApprovedExecution)harness.Service)
            .ExecuteWithCompatibilityResultInAmbientTransactionAsync(Guid.NewGuid(), Intent(), new ProducerOwnerEffectReceiptDto());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("FINANCE_PRODUCER_INTENT_DISABLED*");
        events.VerifyNoOtherCalls();
        harness.Executor.Receipt.Should().BeNull();
    }

    [Fact]
    public async Task Prepare_ResolvesInsideFinance_AndPassesOneBookNeutralC6Command()
    {
        var applicability = Applicability();
        CreateAccountingEventDto? captured = null;
        var events = new Mock<IAccountingEventService>();
        events.Setup(x => x.CreateAsync(It.IsAny<CreateAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAccountingEventDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AccountingEventDto { Id = Guid.NewGuid(), ProducerDecisionStatus = "Pending" });
        var service = Harness(applicability, events).Service;

        await service.PrepareAsync(Intent());

        applicability.Verify(x => x.ResolveAsync(It.Is<ResolveAccountingBookApplicabilityDto>(request =>
            request.OriginatingModuleCode == "INV" && request.SourceDocumentType == "INVENTORY_DISPOSAL"
            && request.PostingAction == "DISPOSE"), It.IsAny<CancellationToken>()), Times.Once);
        captured.Should().NotBeNull();
        captured!.ExpectedCalculationInputHash.Should().Be(Hash('A'));
        captured.ExpectedSelectionFingerprint.Should().Be(Hash('B'));
        captured.ProducerParticipantIdentity.Should().Be("INVENTORY.DISPOSAL.V1");
        captured.PostingRequest.AccountingBookCode.Should().BeEmpty("producers cannot select or enumerate books");
        captured.PostingRequest.Lines.Should().HaveCount(2);
        events.Verify(x => x.CreateAsync(It.IsAny<CreateAccountingEventDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Prepare_RejectsUnbalancedEconomicsBeforeResolution()
    {
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        var events = new Mock<IAccountingEventService>(MockBehavior.Strict);
        var intent = Intent();
        intent.PostingRequest.Lines = [new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = 10m }];

        var action = () => Harness(applicability, events).Service.PrepareAsync(intent);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_UNBALANCED*");
        applicability.VerifyNoOtherCalls();
        events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CorrectionPreparation_InheritsTargetFrozenAuthority_WithoutCurrentC5Resolution()
    {
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        CreateAccountingEventDto? captured = null;
        var events = new Mock<IAccountingEventService>();
        events.Setup(x => x.CreateAsync(It.IsAny<CreateAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateAccountingEventDto, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new AccountingEventDto());
        var harness = Harness(applicability, events);
        var targetId = Guid.NewGuid();
        var evidence = new AccountingBookSelectionEvidence
        {
            TenantId = harness.TenantId, CalculationInputHash = Hash('D'), SelectionFingerprint = Hash('E')
        };
        harness.Db.AccountingEvents.Add(new AccountingEvent
        {
            Id = targetId, TenantId = harness.TenantId, RootAccountingEventId = targetId, Version = 1,
            Status = AccountingEventStatuses.Posted, AccountingBookSelectionEvidenceId = evidence.Id,
            AccountingBookSelectionEvidence = evidence, SelectionFingerprint = Hash('E')
        });
        await harness.Db.SaveChangesAsync();
        var intent = Intent();
        intent.EventKind = AccountingEventKinds.Correction;
        intent.CorrectsAccountingEventId = targetId;
        intent.SupersedesAccountingEventId = targetId;

        await harness.Service.PrepareAsync(intent);

        captured!.ExpectedCalculationInputHash.Should().Be(Hash('D'));
        captured.ExpectedSelectionFingerprint.Should().Be(Hash('E'));
        applicability.VerifyNoOtherCalls();
    }

    [Fact]
    public void ApprovedExecutionSurface_IsInternalAndAcceptsReceiptButNoCallbackOrContext()
    {
        typeof(IFinanceProducerIntentService).GetMethods().Should().NotContain(method => method.Name.Contains("Execute", StringComparison.Ordinal));
        var method = typeof(IFinanceProducerApprovedExecution).GetMethod("ExecuteInAmbientTransactionAsync")!;
        method.GetParameters().Should().ContainSingle(parameter => parameter.ParameterType == typeof(ProducerOwnerEffectReceiptDto));
        method.GetParameters().Should().NotContain(parameter => typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
            || parameter.ParameterType == typeof(ApplicationDbContext));
        typeof(AccountingEventService).Assembly.GetTypes().Should().NotContain(type => type.Name.Contains("ExecutionRegistry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApprovalAndRejection_AreDecisionOnlyCalls()
    {
        var applicability = Applicability();
        var events = new Mock<IAccountingEventService>();
        var intent = Intent();
        events.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Prepared(intent));
        events.Setup(x => x.ApproveAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = "Approved" });
        events.Setup(x => x.RejectAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = "Rejected" });
        var service = Harness(applicability, events).Service;

        (await service.ApproveAsync(Guid.NewGuid(), intent, new() { Reason = "Reviewed" })).ProducerDecisionStatus.Should().Be("Approved");
        (await service.RejectAsync(Guid.NewGuid(), intent, new() { Reason = "Invalid source" })).ProducerDecisionStatus.Should().Be("Rejected");

        applicability.Verify(x => x.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>()), Times.Never,
            "approval and rejection must survive current C5 drift or blockers by using prepared evidence");
    }

    [Fact]
    public async Task DurableDecisionById_ReconstructsAndForwardsExactCanonicalRequestWithoutC5Resolution()
    {
        var applicability = Applicability();
        ReleaseAccountingEventDto? capturedApproval = null;
        ReleaseAccountingEventDto? capturedRejection = null;
        var events = new Mock<IAccountingEventService>();
        events.Setup(service => service.ApproveAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ReleaseAccountingEventDto, CancellationToken>((_, release, _) => capturedApproval = release)
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved });
        events.Setup(service => service.RejectAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ReleaseAccountingEventDto, CancellationToken>((_, release, _) => capturedRejection = release)
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = ProducerIntentDecisionStatuses.Rejected });
        var harness = Harness(applicability, events);
        var intent = Intent();
        var (id, frozen) = await SeedPreparedEventAsync(harness, intent);
        applicability.Reset();
        applicability.Setup(service => service.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("current C5 policy is unavailable"));

        (await harness.Service.ApprovePreparedAsync(id,
            new DecideProducerAccountingIntentDto { Reason = "Reviewed durable intent" }))
            .ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
        (await harness.Service.RejectPreparedAsync(id,
            new DecideProducerAccountingIntentDto { Reason = "Rejected durable intent" }))
            .ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Rejected);

        capturedApproval!.Request.Should().BeEquivalentTo(frozen);
        capturedApproval.Reason.Should().Be("Reviewed durable intent");
        capturedRejection!.Request.Should().BeEquivalentTo(frozen);
        capturedRejection.Reason.Should().Be("Rejected durable intent");
        applicability.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DurableDecisionById_UsesExistingMakerCheckerRetryConflictAndAuditAuthority()
    {
        var harness = RealDecisionHarness();
        var (id, _) = await SeedPreparedEventAsync(harness.Service, harness.Db, harness.TenantId, Intent());
        var makerEvidence = await harness.Db.AccountingEvents.SingleAsync(item => item.Id == id);
        makerEvidence.RequestedByUserId = harness.Actor;
        makerEvidence.PreparedByUserId = harness.Actor;
        await harness.Db.SaveChangesAsync();

        await FluentActions.Awaiting(() => harness.Service.ApprovePreparedAsync(id,
            new DecideProducerAccountingIntentDto { Reason = "Reviewed immutable request" })).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");

        harness.Actor = Guid.NewGuid();
        var decision = new DecideProducerAccountingIntentDto { Reason = "Reviewed immutable request" };
        (await harness.Service.ApprovePreparedAsync(id, decision)).ProducerDecisionStatus
            .Should().Be(ProducerIntentDecisionStatuses.Approved);
        (await harness.Service.ApprovePreparedAsync(id, decision)).ProducerDecisionStatus
            .Should().Be(ProducerIntentDecisionStatuses.Approved, "the exact retry appends no new decision");
        harness.Audit.Verify(audit => audit.RecordAsync(It.Is<FinanceAuditEventDto>(item =>
            item.EventType == FinanceAuditEvents.ProducerAccountingIntentApproved
            && item.ResourceId == id.ToString()), It.IsAny<CancellationToken>()), Times.Once);

        harness.Actor = Guid.NewGuid();
        await FluentActions.Awaiting(() => harness.Service.ApprovePreparedAsync(id, decision)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_DECISION_CHECKER_CONFLICT*");
        await FluentActions.Awaiting(() => harness.Service.RejectPreparedAsync(id,
            new DecideProducerAccountingIntentDto { Reason = decision.Reason })).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_DECISION_CONFLICT*");
    }

    [Fact]
    public async Task DurableDecisionById_DeniesMissingTamperedAndSourceConflictingEvidence()
    {
        foreach (var mutate in new Action<AccountingEvent>[]
        {
            item => item.ProducerIntentSnapshotJson = null,
            item => item.ProducerIntentSnapshotHash = Hash('D'),
            item => item.SourceDocumentType = "UNTRUSTED_SOURCE"
        })
        {
            var events = new Mock<IAccountingEventService>(MockBehavior.Strict);
            var harness = Harness(Applicability(), events);
            var (id, _) = await SeedPreparedEventAsync(harness, Intent());
            var row = await harness.Db.AccountingEvents.SingleAsync(item => item.Id == id);
            mutate(row);
            await harness.Db.SaveChangesAsync();

            await FluentActions.Awaiting(() => harness.Service.ApprovePreparedAsync(id,
                new DecideProducerAccountingIntentDto { Reason = "Untrusted evidence" })).Should()
                .ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_SNAPSHOT_*");
            events.VerifyNoOtherCalls();
        }
    }

    [Fact]
    public async Task ExecuteApproved_ForwardsExactReceiptWithoutExecutableCallback()
    {
        var applicability = Applicability();
        var events = new Mock<IAccountingEventService>();
        var intent = Intent();
        events.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingEventDto
        {
            ProducerDecisionStatus = "Approved", Status = "PendingApproval", ProducerDecisionReason = "Reviewed",
            ProducerParticipantIdentity = "INVENTORY.DISPOSAL.V1", ProducerIntentSnapshotJson = Prepared(intent).ProducerIntentSnapshotJson
        });
        var harness = Harness(applicability, events);
        harness.Executor.Result = new AccountingEventDto { Status = "Posted" };
        var receipt = Receipt(harness.TenantId, intent);

        (await ((IFinanceProducerApprovedExecution)harness.Service)
            .ExecuteInAmbientTransactionAsync(Guid.NewGuid(), intent, receipt)).Status.Should().Be("Posted");

        harness.Executor.Receipt.Should().BeSameAs(receipt);
    }

    [Fact]
    public async Task CompatibilityExecution_ReturnsDefaultRepresentation_AndExactRetryIsStable()
    {
        var applicability = Applicability();
        var events = new Mock<IAccountingEventService>();
        var intent = Intent();
        events.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingEventDto
        {
            ProducerDecisionStatus = "Approved", Status = "PendingApproval", ProducerDecisionReason = "Reviewed",
            ProducerParticipantIdentity = "INVENTORY.DISPOSAL.V1", ProducerIntentSnapshotJson = Prepared(intent).ProducerIntentSnapshotJson
        });
        var harness = Harness(applicability, events);
        var primaryBook = new AccountingBook
        {
            TenantId = harness.TenantId, Code = "IFRS", Name = "IFRS", IsDefault = true,
            IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS"
        };
        var secondaryBook = new AccountingBook
        {
            TenantId = harness.TenantId, Code = "LOCAL", Name = "Local", IsDefault = false,
            BookType = ErpSystem.Core.Enums.AccountingBookType.ParallelFull,
            IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS"
        };
        harness.Db.AccountingBooks.AddRange(primaryBook, secondaryBook);
        await harness.Db.SaveChangesAsync();
        var primaryPosting = new AccountingEventPostingDto
        {
            AccountingBookId = primaryBook.Id, AccountingBookCode = primaryBook.Code, SelectionOrder = 2,
            Status = AccountingEventStatuses.Posted, FinancePostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid()
        };
        harness.Executor.Result = new AccountingEventDto
        {
            Id = Guid.NewGuid(), Status = AccountingEventStatuses.Posted, RequestFingerprint = Hash('C'),
            Postings =
            [
                new AccountingEventPostingDto
                {
                    AccountingBookId = secondaryBook.Id, AccountingBookCode = secondaryBook.Code, SelectionOrder = 1,
                    Status = AccountingEventStatuses.Posted, FinancePostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid()
                },
                primaryPosting
            ]
        };
        var receipt = Receipt(harness.TenantId, intent);
        var execution = (IFinanceProducerApprovedExecution)harness.Service;

        var first = await execution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            harness.Executor.Result.Id, intent, receipt);
        var retry = await execution.ExecuteWithCompatibilityResultInAmbientTransactionAsync(
            harness.Executor.Result.Id, intent, receipt);

        first.AccountingEventId.Should().Be(harness.Executor.Result.Id);
        first.AccountingEventRequestFingerprint.Should().Be(harness.Executor.Result.RequestFingerprint);
        first.FinancePostingEventId.Should().Be(primaryPosting.FinancePostingEventId!.Value);
        first.JournalEntryId.Should().Be(primaryPosting.JournalEntryId!.Value);
        retry.Should().Be(first);
    }

    [Fact]
    public void C6FingerprintAndRetry_BindParticipantAndOriginalMaker()
    {
        var first = new CreateAccountingEventDto
        {
            SelectionIdempotencyKey = "EVENT-1", ExpectedCalculationInputHash = Hash('A'),
            ExpectedSelectionFingerprint = Hash('B'), ProducerParticipantIdentity = "INVENTORY.DISPOSAL.V1",
            PostingRequest = ToFinance(Intent().PostingRequest)
        };
        var changed = new CreateAccountingEventDto
        {
            SelectionIdempotencyKey = first.SelectionIdempotencyKey,
            ExpectedCalculationInputHash = first.ExpectedCalculationInputHash,
            ExpectedSelectionFingerprint = first.ExpectedSelectionFingerprint,
            ProducerParticipantIdentity = "INVENTORY.OTHER.V1", PostingRequest = first.PostingRequest
        };
        var root = Guid.NewGuid();
        Fingerprint(first, root).Should().NotBe(Fingerprint(changed, root));

        var maker = Guid.NewGuid();
        var evidence = new AccountingEvent
        {
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Pending,
            PreparedByUserId = maker
        };
        var invocation = FluentActions.Invoking(() => typeof(AccountingEventService)
            .GetMethod("RequireProducerMakerMatch", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [evidence, Guid.NewGuid()])).Should().Throw<TargetInvocationException>().Which;
        invocation.InnerException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().StartWith("ACCOUNTING_EVENT_MAKER_CONFLICT:");
    }

    [Fact]
    public void ReceiptValidation_DeniesEmptyMismatchAndCrossTenant_AndFingerprintBindsExpectation()
    {
        var tenant = Guid.NewGuid();
        var intent = Intent();
        var request = new CreateAccountingEventDto
        {
            SelectionIdempotencyKey = intent.IdempotencyKey, ExpectedCalculationInputHash = Hash('A'),
            ExpectedSelectionFingerprint = Hash('B'), ProducerParticipantIdentity = "INVENTORY.DISPOSAL.V1",
            ExpectedOwnerEffect = intent.ExpectedOwnerEffect, PostingRequest = ToFinance(intent.PostingRequest)
        };
        var receipt = Receipt(tenant, intent);
        var validator = typeof(AccountingEventService).GetMethod("RequireReceiptMatch", BindingFlags.NonPublic | BindingFlags.Static)!;
        validator.Invoke(null, [request, receipt, tenant]);

        var mismatched = Receipt(tenant, intent);
        mismatched.OwnerEntityId = Guid.NewGuid();
        var noOp = Receipt(tenant, intent);
        noOp.EffectFingerprint = new string('0', 64);
        foreach (var invalid in new[] { new ProducerOwnerEffectReceiptDto(), noOp, Receipt(Guid.NewGuid(), intent), mismatched })
        {
            Action action = () => validator.Invoke(null, [request, invalid, tenant]);
            action.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
        }

        var changed = new CreateAccountingEventDto
        {
            SelectionIdempotencyKey = request.SelectionIdempotencyKey,
            ExpectedCalculationInputHash = request.ExpectedCalculationInputHash,
            ExpectedSelectionFingerprint = request.ExpectedSelectionFingerprint,
            ProducerParticipantIdentity = request.ProducerParticipantIdentity,
            ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
            {
                ParticipantCode = request.ExpectedOwnerEffect!.ParticipantCode,
                OwnerEntityType = request.ExpectedOwnerEffect.OwnerEntityType,
                OwnerEntityId = request.ExpectedOwnerEffect.OwnerEntityId,
                OwnerAction = request.ExpectedOwnerEffect.OwnerAction,
                EffectFingerprint = Hash('E')
            },
            PostingRequest = request.PostingRequest
        };
        var root = Guid.NewGuid();
        Fingerprint(request, root).Should().NotBe(Fingerprint(changed, root));
    }

    [Fact]
    public void ImmutableSnapshot_ReconstructsExactRequest_AndRejectsRequestOrEvidenceTamper()
    {
        var intent = Intent();
        intent.IdempotencyKey = "INVENTORY-DISPOSAL-1";
        var request = JsonSerializer.Deserialize<CreateAccountingEventDto>(Prepared(intent).ProducerIntentSnapshotJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var root = Guid.NewGuid();
        request.AccountingEventId = root;
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var item = new AccountingEvent
        {
            RootAccountingEventId = root, Version = 1,
            RequestFingerprint = Fingerprint(request, root),
            ProducerIntentSnapshotJson = json,
            ProducerIntentSnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
        };
        var validator = typeof(AccountingEventService).GetMethod("RequireSnapshotMatch", BindingFlags.NonPublic | BindingFlags.Static)!;

        validator.Invoke(null, [item, request]);

        var changed = JsonSerializer.Deserialize<CreateAccountingEventDto>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        changed.PostingRequest.Lines[0].DebitAmount++;
        Action requestTamper = () => validator.Invoke(null, [item, changed]);
        requestTamper.Should().Throw<TargetInvocationException>().Which.InnerException.Should()
            .BeOfType<InvalidOperationException>().Which.Message.Should().StartWith("ACCOUNTING_EVENT_SNAPSHOT_REQUEST_CONFLICT:");

        item.ProducerIntentSnapshotJson = json.Replace("DISPOSE", "DISPOSE_TAMPERED", StringComparison.Ordinal);
        Action evidenceTamper = () => validator.Invoke(null, [item, request]);
        evidenceTamper.Should().Throw<TargetInvocationException>().Which.InnerException.Should()
            .BeOfType<InvalidOperationException>().Which.Message.Should().StartWith("ACCOUNTING_EVENT_SNAPSHOT_INVALID:");
    }

    private static TestHarness Harness(Mock<IAccountingBookApplicabilityService> applicability,
        Mock<IAccountingEventService> events, bool enabled = true)
    {
        var tenantId = Guid.NewGuid();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var executor = new StubExecutor();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        var service = new FinanceProducerIntentService(applicability.Object, events.Object, executor,
            db, user.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = enabled }));
        return new TestHarness(service, db, tenantId, executor);
    }

    private static Mock<IAccountingBookApplicabilityService> Applicability()
    {
        var mock = new Mock<IAccountingBookApplicabilityService>();
        mock.Setup(x => x.ResolveAsync(It.IsAny<ResolveAccountingBookApplicabilityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingBookSelectionDto
            {
                CalculationInputHash = Hash('A'), SelectionFingerprint = Hash('B'),
                Books = [new AccountingBookSelectionBookDto { AccountingBookId = Guid.NewGuid(), AccountingBookCode = "IFRS", SelectionOrder = 1, AuthorityFingerprint = Hash('C') }]
            });
        return mock;
    }

    private static Task<(Guid Id, CreateAccountingEventDto Frozen)> SeedPreparedEventAsync(
        TestHarness harness, ProducerAccountingIntentDto intent) =>
        SeedPreparedEventAsync(harness.Service, harness.Db, harness.TenantId, intent);

    private static async Task<(Guid Id, CreateAccountingEventDto Frozen)> SeedPreparedEventAsync(
        FinanceProducerIntentService service, ApplicationDbContext db, Guid tenantId,
        ProducerAccountingIntentDto intent)
    {
        var frozen = await service.BuildRequestAsync(intent, CancellationToken.None);
        var id = Guid.NewGuid();
        frozen.AccountingEventId = id;
        var key = frozen.SelectionIdempotencyKey.Trim().ToUpperInvariant();
        var snapshot = JsonSerializer.Serialize(frozen, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var maker = Guid.NewGuid();
        db.AccountingEvents.Add(new AccountingEvent
        {
            Id = id, TenantId = tenantId, RootAccountingEventId = id, Version = 1,
            EventKind = frozen.EventKind, IdempotencyKey = key,
            OriginatingModuleCode = frozen.PostingRequest.OriginModuleCode ?? frozen.PostingRequest.SourceModule,
            SourceDocumentType = frozen.PostingRequest.SourceDocumentType,
            SourceDocumentId = frozen.PostingRequest.SourceDocumentId,
            PostingAction = frozen.PostingRequest.PostingAction,
            EventDate = frozen.PostingRequest.PostingDate.Date,
            Status = AccountingEventStatuses.PendingApproval,
            SelectionFingerprint = frozen.ExpectedSelectionFingerprint,
            RequestFingerprint = Fingerprint(frozen, id),
            RequestedAtUtc = DateTime.UtcNow, RequestedByUserId = maker,
            PreparedAtUtc = DateTime.UtcNow, PreparedByUserId = maker,
            ProducerDecisionStatus = ProducerIntentDecisionStatuses.Pending,
            ProducerParticipantIdentity = frozen.ProducerParticipantIdentity,
            ProducerIntentSnapshotJson = snapshot,
            ProducerIntentSnapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))),
            CreatedAt = DateTime.UtcNow, CreatedBy = "c11-test"
        });
        await db.SaveChangesAsync();
        return (id, frozen);
    }

    private static RealDecisionHarnessState RealDecisionHarness()
    {
        var state = new RealDecisionHarnessState
        {
            TenantId = Guid.NewGuid(), Actor = Guid.NewGuid(),
            Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
        };
        state.User.SetupGet(user => user.TenantId).Returns(() => state.TenantId);
        state.User.SetupGet(user => user.UserId).Returns(() => state.Actor.ToString());
        state.User.SetupGet(user => user.UserName).Returns("c11-checker");
        state.Audit.Setup(audit => audit.RecordAsync(It.IsAny<FinanceAuditEventDto>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new ErpSystem.Core.Entities.AuditLog());
        var accountingEvents = new AccountingEventService(state.Db, state.User.Object, state.Applicability.Object,
            new FinancePostingEngine(state.Db, state.User.Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancePostingEngine>.Instance),
            state.Audit.Object, Options.Create(new AccountingEventOptions { Enabled = true }));
        state.Service = new FinanceProducerIntentService(state.Applicability.Object, accountingEvents,
            accountingEvents, state.Db, state.User.Object,
            Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        return state;
    }

    private static ProducerAccountingIntentDto Intent() => new()
    {
        IdempotencyKey = "inventory-disposal-1", ParticipantIdentity = "inventory.disposal.v1",
        ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "inventory.disposal.v1", OwnerEntityType = "InventoryDisposal",
            OwnerEntityId = Guid.NewGuid(), OwnerAction = "Dispose", EffectFingerprint = Hash('F')
        },
        PostingRequest = new ProducerFinancePostingRequestDto
        {
            SourceModule = "INV", OriginModuleCode = "INV", SourceDocumentType = "INVENTORY_DISPOSAL",
            SourceDocumentId = Guid.NewGuid(), PostingAction = "DISPOSE", PostingDate = new DateTime(2026, 9, 7),
            Lines =
            [
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = 25m },
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), CreditAmount = 25m }
            ]
        }
    };

    private static string Hash(char value) => new(value, 64);

    private static AccountingEventDto Prepared(ProducerAccountingIntentDto intent) => new()
    {
        ProducerIntentSnapshotJson = System.Text.Json.JsonSerializer.Serialize(new CreateAccountingEventDto
        {
            EventKind = intent.EventKind, SelectionIdempotencyKey = intent.IdempotencyKey,
            ExpectedCalculationInputHash = Hash('A'), ExpectedSelectionFingerprint = Hash('B'),
            ProducerParticipantIdentity = intent.ParticipantIdentity.Trim().ToUpperInvariant(),
            ExpectedOwnerEffect = intent.ExpectedOwnerEffect,
            PostingRequest = ToFinance(intent.PostingRequest)
        }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
    };

    private static string Fingerprint(CreateAccountingEventDto request, Guid root) => (string)typeof(AccountingEventService)
        .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), 1, root])!;

    private static FinancePostingRequestV2Dto ToFinance(ProducerFinancePostingRequestDto source) => new()
    {
        SourceModule = source.SourceModule, OriginModuleCode = source.OriginModuleCode,
        SourceDocumentType = source.SourceDocumentType, SourceDocumentId = source.SourceDocumentId,
        PostingAction = source.PostingAction, PostingDate = source.PostingDate, Lines = source.Lines,
        AccountingBookCode = string.Empty
    };

    private static ProducerOwnerEffectReceiptDto Receipt(Guid tenantId, ProducerAccountingIntentDto intent) => new()
    {
        TenantId = tenantId, ParticipantCode = intent.ExpectedOwnerEffect.ParticipantCode,
        OwnerEntityType = intent.ExpectedOwnerEffect.OwnerEntityType, OwnerEntityId = intent.ExpectedOwnerEffect.OwnerEntityId,
        OwnerAction = intent.ExpectedOwnerEffect.OwnerAction, EffectFingerprint = intent.ExpectedOwnerEffect.EffectFingerprint
    };

    private sealed record TestHarness(FinanceProducerIntentService Service, ApplicationDbContext Db, Guid TenantId,
        StubExecutor Executor);

    private sealed class RealDecisionHarnessState
    {
        public Guid TenantId { get; set; }
        public Guid Actor { get; set; }
        public ApplicationDbContext Db { get; set; } = null!;
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IAccountingBookApplicabilityService> Applicability { get; } =
            FinanceProducerIntentC7Tests.Applicability();
        public Mock<IFinanceAuditService> Audit { get; } = new();
        public FinanceProducerIntentService Service { get; set; } = null!;
    }

    private sealed class StubExecutor : ITrustedAccountingEventExecutor
    {
        public AccountingEventDto Result { get; set; } = new();
        public ProducerOwnerEffectReceiptDto? Receipt { get; private set; }
        public Task<AccountingEventDto> PrepareGroupMemberInAmbientTransactionAsync(CreateAccountingEventDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task<AccountingEventDto> ValidatePreparedGroupMemberAsync(Guid accountingEventId,
            CreateAccountingEventDto request, CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task<AccountingEventDto> ExecuteApprovedGroupMemberInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, Guid producerIntentGroupId, Guid approvedCheckerId,
            Guid expectedAmbientTransactionId, CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task<AccountingEventDto> ExecuteApprovedInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt, CancellationToken cancellationToken = default)
        { Receipt = receipt; return Task.FromResult(Result); }
        public Task RecordApprovedFailureAfterRollbackAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
            ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
