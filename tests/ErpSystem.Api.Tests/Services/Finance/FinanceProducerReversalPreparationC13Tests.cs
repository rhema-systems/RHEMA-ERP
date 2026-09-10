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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerReversalPreparationC13Tests
{
    [Fact]
    public async Task Prepare_ReconstructsExactOppositeEconomicsAndDimensions_WithoutCurrentC5()
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);

        var result = await harness.Service.PrepareReversalAsync(request);

        result.AccountingEventId.Should().Be(request.ReversalAccountingEventId);
        result.OriginalAccountingEventId.Should().Be(original.EventId);
        result.Status.Should().Be(AccountingEventStatuses.PendingApproval);
        result.DecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Pending);
        var reversal = await harness.Db.AccountingEvents.AsNoTracking()
            .SingleAsync(item => item.Id == request.ReversalAccountingEventId);
        reversal.EventKind.Should().Be(AccountingEventKinds.Reversal);
        reversal.RootAccountingEventId.Should().Be(original.EventId);
        reversal.Version.Should().Be(2);
        reversal.SupersedesAccountingEventId.Should().Be(original.EventId);
        reversal.ReversesAccountingEventId.Should().Be(original.EventId);
        reversal.SelectionFingerprint.Should().Be(Hash('B'));
        var frozen = JsonSerializer.Deserialize<CreateAccountingEventDto>(reversal.ProducerIntentSnapshotJson!, JsonOptions)!;
        frozen.ExpectedCalculationInputHash.Should().Be(Hash('A'));
        frozen.ExpectedSelectionFingerprint.Should().Be(Hash('B'));
        frozen.PostingRequest.PostingDate.Should().Be(request.ReversalDate.Date);
        frozen.PostingRequest.ReversalReason.Should().Be(request.Reason);
        frozen.PostingRequest.OriginModuleCode.Should().Be("SALES");
        frozen.PostingRequest.SourceDocumentType.Should().Be("AR_CREDIT_NOTE");
        frozen.PostingRequest.SourceDocumentId.Should().Be(original.OwnerId);
        frozen.PostingRequest.PostingAction.Should().Be("POST_CREDIT_NOTE");
        frozen.PostingRequest.AccountingBookCode.Should().BeEmpty();
        frozen.PostingRequest.Lines.Should().HaveCount(2);
        frozen.PostingRequest.Lines[0].DebitAmount.Should().Be(0);
        frozen.PostingRequest.Lines[0].CreditAmount.Should().Be(125m);
        frozen.PostingRequest.Lines[0].TransactionDebitAmount.Should().Be(0);
        frozen.PostingRequest.Lines[0].TransactionCreditAmount.Should().Be(10m);
        frozen.PostingRequest.Lines[0].Dimensions.Should().BeEquivalentTo(original.Request.PostingRequest.Lines[0].Dimensions);
        frozen.PostingRequest.Lines[0].FinanceDimensionSetId.Should()
            .Be(original.Request.PostingRequest.Lines[0].FinanceDimensionSetId);
        frozen.PostingRequest.Lines[1].DebitAmount.Should().Be(125m);
        frozen.PostingRequest.Lines[1].CreditAmount.Should().Be(0);
        harness.Applicability.VerifyNoOtherCalls();

        await FluentActions.Awaiting(() => harness.Service.ApprovePreparedAsync(result.AccountingEventId,
            new DecideProducerAccountingIntentDto { Reason = "Reviewed exact reversal" })).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*checker must differ*");
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        (await harness.Service.ApprovePreparedAsync(result.AccountingEventId,
            new DecideProducerAccountingIntentDto { Reason = "Independent reversal review" }))
            .ProducerDecisionStatus.Should().Be(ProducerIntentDecisionStatuses.Approved);
    }

    [Fact]
    public async Task ExactRetry_IsReadOnly_AndDateReasonOrIdentityConflictFailClosed()
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);

        var first = await harness.Service.PrepareReversalAsync(request);
        harness.Db.ChangeTracker.Clear();
        var retry = await harness.Service.PrepareReversalAsync(request);

        retry.Should().Be(first);
        (await harness.Db.AccountingEvents.CountAsync()).Should().Be(2);
        harness.Audit.Verify(service => service.RecordAsync(It.Is<FinanceAuditEventDto>(item =>
            item.EventType == FinanceAuditEvents.AccountingEventPrepared
            && item.ResourceId == request.ReversalAccountingEventId.ToString()), It.IsAny<CancellationToken>()), Times.Once);

        var changedDate = ReversalRequest(original); changedDate.ReversalDate = changedDate.ReversalDate.AddDays(1);
        var changedReason = ReversalRequest(original); changedReason.Reason = "A different governed reason";
        var changedId = ReversalRequest(original); changedId.ReversalAccountingEventId = Guid.NewGuid();
        var changedKey = ReversalRequest(original); changedKey.IdempotencyKey = "SALES-CREDIT-NOTE-REVERSAL-OTHER";
        foreach (var conflict in new[] { changedDate, changedReason, changedId, changedKey })
        {
            await FluentActions.Awaiting(() => harness.Service.PrepareReversalAsync(conflict)).Should()
                .ThrowAsync<InvalidOperationException>();
        }
        (await harness.Db.AccountingEvents.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("REVERSE")]
    public async Task OriginalEffectFingerprintReplay_FailsBeforeReversalOrAuditMutation(string ownerAction)
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);
        request.ExpectedOwnerEffect.OwnerAction = ownerAction;
        request.ExpectedOwnerEffect.EffectFingerprint = $"  {Hash('c').ToLowerInvariant()}  ";

        await FluentActions.Awaiting(() => harness.Service.PrepareReversalAsync(request)).Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*distinct compensating owner-effect fingerprint*");

        (await harness.Db.AccountingEvents.CountAsync()).Should().Be(1);
        harness.Audit.Verify(service => service.RecordAsync(
            It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IdOnlyExecution_ReconstructsApprovedC13Intent_AndReturnsStableC10Compatibility()
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);
        var prepared = await harness.Service.PrepareReversalAsync(request);
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        await harness.Service.ApprovePreparedAsync(prepared.AccountingEventId,
            new DecideProducerAccountingIntentDto { Reason = "Independent execution approval" });

        var originalEvidenceId = await harness.Db.AccountingEvents.AsNoTracking()
            .Where(item => item.Id == original.EventId)
            .Select(item => item.AccountingBookSelectionEvidenceId).SingleAsync();
        var primaryBookId = await harness.Db.AccountingBookSelectionEvidenceBooks.AsNoTracking()
            .Where(item => item.AccountingBookSelectionEvidenceId == originalEvidenceId)
            .OrderBy(item => item.SelectionOrder).Select(item => item.AccountingBookId).FirstAsync();
        harness.Db.AccountingBooks.Add(new AccountingBook
        {
            Id = primaryBookId, TenantId = harness.TenantId, Code = "IFRS", Name = "IFRS",
            IsDefault = true, IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS"
        });
        await harness.Db.SaveChangesAsync();
        var postingEventId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        var executor = new RecordingExecutor
        {
            Result = new AccountingEventDto
            {
                Id = prepared.AccountingEventId, RequestFingerprint = prepared.AccountingEventRequestFingerprint,
                Status = AccountingEventStatuses.Posted,
                Postings = [new AccountingEventPostingDto
                {
                    AccountingBookId = primaryBookId, AccountingBookCode = "IFRS",
                    Status = AccountingEventStatuses.Posted,
                    FinancePostingEventId = postingEventId, JournalEntryId = journalEntryId
                }]
            }
        };
        var bridge = new FinanceProducerIntentService(harness.Applicability.Object, harness.Events, executor,
            harness.Db, harness.User.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        var receipt = Receipt(harness.TenantId, request.ExpectedOwnerEffect);

        var first = await ((IFinanceProducerApprovedExecutionService)bridge)
            .ExecuteInAmbientTransactionAsync(prepared.AccountingEventId, receipt);
        var retry = await ((IFinanceProducerApprovedExecutionService)bridge)
            .ExecuteInAmbientTransactionAsync(prepared.AccountingEventId, receipt);

        retry.Should().Be(first);
        first.Should().Be(new FinanceProducerApprovedExecutionResultDto(prepared.AccountingEventId,
            prepared.AccountingEventRequestFingerprint, AccountingEventStatuses.Posted,
            postingEventId, journalEntryId));
        executor.Requests.Should().HaveCount(2);
        executor.Requests.Select(item => item.Request.PostingRequest.Lines).Should()
            .OnlyContain(lines => lines[0].CreditAmount == 125m && lines[1].DebitAmount == 125m);
        executor.Receipts.Should().OnlyContain(item => ReferenceEquals(item, receipt));

        var failure = new InvalidOperationException("owner transaction rolled back");
        await ((IFinanceProducerApprovedExecutionService)bridge)
            .RecordFailureAfterRollbackAsync(prepared.AccountingEventId, receipt, failure);
        executor.FailedEventId.Should().Be(prepared.AccountingEventId);
        executor.Failure.Should().BeSameAs(failure);
        executor.FailureRequest!.Request.PostingRequest.Lines[0].CreditAmount.Should().Be(125m);
    }

    [Fact]
    public async Task IdOnlyExecution_RejectsReceiptMismatchAndMissingAmbientTransaction()
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);
        var prepared = await harness.Service.PrepareReversalAsync(request);
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        await harness.Service.ApprovePreparedAsync(prepared.AccountingEventId,
            new DecideProducerAccountingIntentDto { Reason = "Independent execution approval" });
        var receipt = Receipt(harness.TenantId, request.ExpectedOwnerEffect);
        receipt.EffectFingerprint = Hash('A');

        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)harness.Service)
            .ExecuteInAmbientTransactionAsync(prepared.AccountingEventId, receipt)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*receipt differs*");

        receipt.EffectFingerprint = request.ExpectedOwnerEffect.EffectFingerprint;
        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)harness.Service)
            .ExecuteInAmbientTransactionAsync(prepared.AccountingEventId, receipt)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*transaction*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdOnlyExecution_WrongEventOrTenantIsNotDiscoverable(bool wrongTenant)
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);
        var prepared = await harness.Service.PrepareReversalAsync(request);
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        await harness.Service.ApprovePreparedAsync(prepared.AccountingEventId,
            new DecideProducerAccountingIntentDto { Reason = "Independent execution approval" });
        if (wrongTenant)
            harness.User.SetupGet(value => value.TenantId).Returns(Guid.NewGuid());
        var executor = new RecordingExecutor();
        var bridge = new FinanceProducerIntentService(harness.Applicability.Object, harness.Events, executor,
            harness.Db, harness.User.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = true }));

        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)bridge)
            .ExecuteInAmbientTransactionAsync(wrongTenant ? prepared.AccountingEventId : Guid.NewGuid(),
                Receipt(harness.TenantId, request.ExpectedOwnerEffect))).Should()
            .ThrowAsync<KeyNotFoundException>();
        executor.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("snapshot")]
    [InlineData("fingerprint")]
    [InlineData("nonapproved")]
    [InlineData("lineage")]
    public async Task IdOnlyExecution_TamperedOrUnapprovedAuthorityFailsBeforeExecution(string mutation)
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var request = ReversalRequest(original);
        var prepared = await harness.Service.PrepareReversalAsync(request);
        if (mutation != "nonapproved")
        {
            harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
            await harness.Service.ApprovePreparedAsync(prepared.AccountingEventId,
                new DecideProducerAccountingIntentDto { Reason = "Independent execution approval" });
        }
        var row = await harness.Db.AccountingEvents.SingleAsync(item => item.Id == prepared.AccountingEventId);
        switch (mutation)
        {
            case "source": row.SourceDocumentType = "UNTRUSTED_SOURCE"; break;
            case "snapshot": row.ProducerIntentSnapshotJson = row.ProducerIntentSnapshotJson!.Replace("125", "126", StringComparison.Ordinal); break;
            case "fingerprint": row.RequestFingerprint = Hash('A'); break;
            case "lineage": row.RootAccountingEventId = Guid.NewGuid(); break;
        }
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        var executor = new RecordingExecutor();
        var bridge = new FinanceProducerIntentService(harness.Applicability.Object, harness.Events, executor,
            harness.Db, harness.User.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = true }));

        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)bridge)
            .ExecuteInAmbientTransactionAsync(prepared.AccountingEventId,
                Receipt(harness.TenantId, request.ExpectedOwnerEffect))).Should()
            .ThrowAsync<InvalidOperationException>();
        executor.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task IdOnlyExecutionAndFailure_RejectSelfConsistentGenericC7ReversalProvenance()
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var reversalEvidence = ReversalRequest(original);
        var generic = FinanceProducerIntentService.ToProducerIntent(original.Request);
        generic.AccountingEventId = reversalEvidence.ReversalAccountingEventId;
        generic.EventKind = AccountingEventKinds.Reversal;
        generic.SupersedesAccountingEventId = original.EventId;
        generic.ReversesAccountingEventId = original.EventId;
        generic.IdempotencyKey = reversalEvidence.IdempotencyKey;
        generic.ParticipantIdentity = reversalEvidence.ParticipantIdentity;
        generic.ExpectedOwnerEffect = reversalEvidence.ExpectedOwnerEffect;
        generic.PostingRequest.PostingDate = reversalEvidence.ReversalDate;
        generic.PostingRequest.ReversalReason = reversalEvidence.Reason;
        generic.PostingRequest.ReversalType = "Generic self-consistent reversal";
        generic.PostingRequest.Description = "Owner-supplied generic reversal economics";
        generic.PostingRequest.ReturnExistingOnDuplicate = false;
        generic.PostingRequest.PreserveHistoricalExchangeRateSnapshot = false;
        foreach (var line in generic.PostingRequest.Lines)
        {
            (line.DebitAmount, line.CreditAmount) = (line.CreditAmount, line.DebitAmount);
            (line.TransactionDebitAmount, line.TransactionCreditAmount) =
                (line.TransactionCreditAmount, line.TransactionDebitAmount);
        }
        generic.PostingRequest.Lines[0].CreditAmount = 124m;
        generic.PostingRequest.Lines[1].DebitAmount = 124m;
        generic.PostingRequest.Lines[0].Dimensions[0].ValueCode = "ALTERED";

        var prepared = await harness.Service.PrepareAsync(generic);
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        await harness.Service.ApprovePreparedAsync(prepared.Id,
            new DecideProducerAccountingIntentDto { Reason = "Approved generic C7 reversal" });
        var stored = await harness.Db.AccountingEvents.AsNoTracking().SingleAsync(item => item.Id == prepared.Id);
        stored.ProducerIntentSnapshotHash.Should().Be(Sha256(stored.ProducerIntentSnapshotJson!));
        stored.RequestFingerprint.Should().Be(prepared.RequestFingerprint);
        var executor = new RecordingExecutor();
        var bridge = new FinanceProducerIntentService(harness.Applicability.Object, harness.Events, executor,
            harness.Db, harness.User.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        var receipt = Receipt(harness.TenantId, reversalEvidence.ExpectedOwnerEffect);

        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)bridge)
            .ExecuteInAmbientTransactionAsync(prepared.Id, receipt)).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*canonical C13 reconstruction path*");
        await FluentActions.Awaiting(() => ((IFinanceProducerApprovedExecutionService)bridge)
            .RecordFailureAfterRollbackAsync(prepared.Id, receipt,
                new InvalidOperationException("owner rollback"))).Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*canonical C13 reconstruction path*");
        executor.Requests.Should().BeEmpty();
        executor.FailedEventId.Should().BeNull();
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("source")]
    [InlineData("tenant")]
    [InlineData("participant")]
    [InlineData("kind")]
    [InlineData("status")]
    [InlineData("selection")]
    [InlineData("receipt")]
    [InlineData("lineage")]
    public async Task TamperedOrIneligibleOriginalEvidence_FailsBeforePreparation(string mutation)
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var row = await harness.Db.AccountingEvents
            .Include(item => item.AccountingBookSelectionEvidence)!.ThenInclude(item => item!.Books)
            .Include(item => item.ProducerReceipt)
            .SingleAsync(item => item.Id == original.EventId);
        switch (mutation)
        {
            case "snapshot": row.ProducerIntentSnapshotJson = row.ProducerIntentSnapshotJson!.Replace("125", "126", StringComparison.Ordinal); break;
            case "source": row.SourceDocumentType = "UNTRUSTED_SOURCE"; break;
            case "tenant":
            {
                var snapshot = JsonSerializer.Deserialize<CreateAccountingEventDto>(row.ProducerIntentSnapshotJson!, JsonOptions)!;
                snapshot.PostingRequest.SourceDocumentTenantId = Guid.NewGuid();
                row.ProducerIntentSnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
                row.ProducerIntentSnapshotHash = Sha256(row.ProducerIntentSnapshotJson);
                row.RequestFingerprint = Fingerprint(snapshot, row.Id, 1, row.Id);
                break;
            }
            case "participant": row.ProducerParticipantIdentity = "SALES.OTHER.V1"; break;
            case "kind": row.EventKind = AccountingEventKinds.Correction; break;
            case "status": row.Status = AccountingEventStatuses.PendingApproval; break;
            case "selection": row.AccountingBookSelectionEvidence!.Books.First().AuthorityFingerprint = Hash('F'); break;
            case "receipt": row.ProducerReceipt!.EffectFingerprint = Hash('E'); break;
            case "lineage":
                harness.Db.AccountingEvents.Add(new AccountingEvent
                {
                    TenantId = harness.TenantId, RootAccountingEventId = row.Id, Version = 2,
                    EventKind = AccountingEventKinds.Reversal, SupersedesAccountingEventId = row.Id,
                    ReversesAccountingEventId = row.Id, IdempotencyKey = "EXISTING-SUCCESSOR",
                    OriginatingModuleCode = row.OriginatingModuleCode, SourceDocumentType = row.SourceDocumentType,
                    SourceDocumentId = row.SourceDocumentId, PostingAction = row.PostingAction,
                    Status = AccountingEventStatuses.PendingApproval, EventDate = DateTime.UtcNow.Date,
                    RequestedAtUtc = DateTime.UtcNow, RequestedByUserId = harness.Actor,
                    PreparedAtUtc = DateTime.UtcNow, PreparedByUserId = harness.Actor
                });
                break;
        }
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        await FluentActions.Awaiting(() => harness.Service.PrepareReversalAsync(ReversalRequest(original))).Should()
            .ThrowAsync<InvalidOperationException>();
        (await harness.Db.AccountingEvents.CountAsync()).Should().Be(mutation == "lineage" ? 2 : 1);
        harness.Applicability.VerifyNoOtherCalls();
    }

    [Fact]
    public void FinanceService_ImplementsTheCoreFacingReversalContract()
    {
        typeof(FinanceProducerIntentService).GetInterfaces()
            .Should().Contain(typeof(IFinanceProducerReversalPreparationService));
    }

    [Fact]
    public void NoHttpReversalPreparationSurfaceWasAdded()
    {
        typeof(ProducerAccountingIntentsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Should().NotContain(method => method.Name.Contains("Reversal", StringComparison.OrdinalIgnoreCase));
    }

    private static TestHarness Harness()
    {
        var tenantId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.TenantId).Returns(tenantId);
        user.SetupGet(value => value.UserId).Returns(actor.ToString());
        user.SetupGet(value => value.UserName).Returns("c13-maker");
        var applicability = new Mock<IAccountingBookApplicabilityService>(MockBehavior.Strict);
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ErpSystem.Core.Entities.AuditLog());
        var events = new AccountingEventService(db, user.Object, applicability.Object,
            new FinancePostingEngine(db, user.Object,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FinancePostingEngine>.Instance),
            audit.Object, Options.Create(new AccountingEventOptions { Enabled = true }));
        var service = new FinanceProducerIntentService(applicability.Object, events, events, db, user.Object,
            Options.Create(new FinanceProducerIntentOptions { Enabled = true }));
        return new TestHarness(service, events, db, tenantId, actor, user, applicability, audit);
    }

    private static async Task<OriginalEvidence> SeedOriginalAsync(TestHarness harness)
    {
        var eventId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var request = new CreateAccountingEventDto
        {
            AccountingEventId = eventId,
            EventKind = AccountingEventKinds.Original,
            SelectionIdempotencyKey = "SALES-CREDIT-NOTE-ORIGINAL",
            ExpectedCalculationInputHash = Hash('A'),
            ExpectedSelectionFingerprint = Hash('B'),
            ProducerParticipantIdentity = "SALES.CREDIT_NOTE.V1",
            ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
            {
                ParticipantCode = "SALES.CREDIT_NOTE.V1", OwnerEntityType = "SALES_CREDIT_NOTE",
                OwnerEntityId = ownerId, OwnerAction = "POST", EffectFingerprint = Hash('C')
            },
            PostingRequest = new FinancePostingRequestV2Dto
            {
                SourceModule = "SALES", OriginModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE",
                SourceDocumentId = ownerId, SourceDocumentTenantId = harness.TenantId,
                PostingAction = "POST_CREDIT_NOTE", PostingDate = new DateTime(2026, 8, 31),
                Description = "Posted credit note", FunctionalCurrencyCode = "GHS", AccountingBookCode = string.Empty,
                Lines =
                [
                    new FinancePostingLineDto
                    {
                        AccountId = Guid.NewGuid(), SourceDocumentLineId = Guid.NewGuid(), DebitAmount = 125m,
                        TransactionDebitAmount = 10m, TransactionCreditAmount = 0m, TransactionCurrency = "USD",
                        ForeignCurrencyAmount = 10m, ExchangeRate = 12.5m, FinanceDimensionSetId = Guid.NewGuid(),
                        LineNumber = 1, Description = "Receivable", SegmentString = "100-200",
                        Dimensions = [new FinancePostingDimensionValueDto
                        {
                            DimensionCode = "CUSTOMER", ValueCode = "C001", SourceEntityType = "CUSTOMER",
                            SourceEntityId = ownerId
                        }]
                    },
                    new FinancePostingLineDto
                    {
                        AccountId = Guid.NewGuid(), CreditAmount = 125m, TransactionDebitAmount = 0m,
                        TransactionCreditAmount = 10m, TransactionCurrency = "USD", ForeignCurrencyAmount = 10m,
                        ExchangeRate = 12.5m, LineNumber = 2, Description = "Revenue"
                    }
                ]
            }
        };
        var fingerprint = Fingerprint(request, eventId, 1, eventId);
        var snapshot = JsonSerializer.Serialize(request, JsonOptions);
        var evidence = new AccountingBookSelectionEvidence
        {
            Id = Guid.NewGuid(), TenantId = harness.TenantId, EffectiveDate = request.PostingRequest.PostingDate.Date,
            OriginatingModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE", PostingAction = "POST_CREDIT_NOTE",
            IdempotencyKey = request.SelectionIdempotencyKey, CalculationInputHash = Hash('A'),
            SelectionFingerprint = Hash('B'), FrozenByUserId = harness.Actor, FrozenAtUtc = DateTime.UtcNow
        };
        evidence.Books = bookIds.Select((bookId, index) => new AccountingBookSelectionEvidenceBook
        {
            TenantId = harness.TenantId, AccountingBookSelectionEvidenceId = evidence.Id,
            AccountingBookId = bookId, SelectionOrder = index + 1,
            AccountingBookCodeSnapshot = index == 0 ? "IFRS" : "LOCAL",
            AuthorityFingerprint = Hash(index == 0 ? 'D' : 'E')
        }).ToList();
        var item = new AccountingEvent
        {
            Id = eventId, TenantId = harness.TenantId, RootAccountingEventId = eventId, Version = 1,
            EventKind = AccountingEventKinds.Original, IdempotencyKey = request.SelectionIdempotencyKey,
            OriginatingModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE", SourceDocumentId = ownerId,
            PostingAction = "POST_CREDIT_NOTE", EventDate = request.PostingRequest.PostingDate.Date,
            Status = AccountingEventStatuses.Posted, SelectionFingerprint = Hash('B'), RequestFingerprint = fingerprint,
            AccountingBookSelectionEvidenceId = evidence.Id, AccountingBookSelectionEvidence = evidence,
            RequestedAtUtc = DateTime.UtcNow, RequestedByUserId = harness.Actor,
            PreparedAtUtc = DateTime.UtcNow, PreparedByUserId = harness.Actor,
            ReleasedAtUtc = DateTime.UtcNow, ReleasedByUserId = Guid.NewGuid(), ReleaseReason = "Approved",
            CompletedAtUtc = DateTime.UtcNow, ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved,
            ProducerParticipantIdentity = request.ProducerParticipantIdentity,
            ProducerIntentSnapshotJson = snapshot, ProducerIntentSnapshotHash = Sha256(snapshot),
            ProducerDecidedAtUtc = DateTime.UtcNow, ProducerDecidedByUserId = Guid.NewGuid(),
            ProducerDecisionReason = "Approved exact credit note", CreatedAt = DateTime.UtcNow, CreatedBy = "c13-test"
        };
        item.Postings = evidence.Books.Select(book => new AccountingEventPosting
        {
            TenantId = harness.TenantId, AccountingEventId = eventId, EventVersion = 1,
            AccountingBookId = book.AccountingBookId, SelectionOrder = book.SelectionOrder,
            AccountingBookCodeSnapshot = book.AccountingBookCodeSnapshot,
            AuthorityFingerprint = book.AuthorityFingerprint, Status = AccountingEventStatuses.Posted,
            FinancePostingEventId = Guid.NewGuid(), JournalEntryId = Guid.NewGuid(), PostedAtUtc = DateTime.UtcNow
        }).ToList();
        item.ProducerReceipt = new AccountingEventProducerReceipt
        {
            TenantId = harness.TenantId, AccountingEventId = eventId,
            ParticipantCode = request.ExpectedOwnerEffect.ParticipantCode,
            OwnerEntityType = request.ExpectedOwnerEffect.OwnerEntityType, OwnerEntityId = ownerId,
            OwnerAction = request.ExpectedOwnerEffect.OwnerAction,
            EffectFingerprint = request.ExpectedOwnerEffect.EffectFingerprint,
            RequestFingerprint = fingerprint, RecordedAtUtc = DateTime.UtcNow, RecordedByUserId = harness.Actor
        };
        harness.Db.AccountingEvents.Add(item);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        return new OriginalEvidence(eventId, ownerId, request);
    }

    private static PrepareProducerAccountingReversalDto ReversalRequest(OriginalEvidence original) => new()
    {
        OriginalAccountingEventId = original.EventId,
        ReversalAccountingEventId = DeterministicGuid(original.EventId, "reversal"),
        IdempotencyKey = "SALES-CREDIT-NOTE-REVERSAL",
        ReversalDate = new DateTime(2026, 9, 10),
        Reason = "Customer credit note reversed",
        ParticipantIdentity = "SALES.CREDIT_NOTE.V1",
        ExpectedOwnerEffect = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "SALES.CREDIT_NOTE.V1", OwnerEntityType = "SALES_CREDIT_NOTE",
            OwnerEntityId = original.OwnerId, OwnerAction = "REVERSE", EffectFingerprint = Hash('F')
        }
    };

    private static ProducerOwnerEffectReceiptDto Receipt(Guid tenantId, ProducerOwnerEffectIdentityDto effect) => new()
    {
        TenantId = tenantId, ParticipantCode = effect.ParticipantCode, OwnerEntityType = effect.OwnerEntityType,
        OwnerEntityId = effect.OwnerEntityId, OwnerAction = effect.OwnerAction,
        EffectFingerprint = effect.EffectFingerprint
    };

    private static Guid DeterministicGuid(Guid source, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source:N}:{purpose}"));
        return new Guid(bytes[..16]);
    }

    private static string Fingerprint(CreateAccountingEventDto request, Guid id, int version, Guid root)
    {
        request.AccountingEventId = id;
        return (string)typeof(AccountingEventService)
            .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), version, root])!;
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Hash(char value) => new(value, 64);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record OriginalEvidence(Guid EventId, Guid OwnerId, CreateAccountingEventDto Request);
    private sealed record TestHarness(FinanceProducerIntentService Service, AccountingEventService Events,
        ApplicationDbContext Db,
        Guid TenantId, Guid Actor, Mock<ICurrentUserService> User,
        Mock<IAccountingBookApplicabilityService> Applicability,
        Mock<IFinanceAuditService> Audit) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class RecordingExecutor : ITrustedAccountingEventExecutor
    {
        public AccountingEventDto Result { get; set; } = new();
        public List<ReleaseAccountingEventDto> Requests { get; } = [];
        public List<ProducerOwnerEffectReceiptDto> Receipts { get; } = [];
        public Guid? FailedEventId { get; private set; }
        public ReleaseAccountingEventDto? FailureRequest { get; private set; }
        public Exception? Failure { get; private set; }
        public Task<AccountingEventDto> ExecuteApprovedInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, ProducerOwnerEffectReceiptDto receipt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request); Receipts.Add(receipt); return Task.FromResult(Result);
        }
        public Task RecordApprovedFailureAfterRollbackAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
            ProducerOwnerEffectReceiptDto receipt, Exception failure, CancellationToken cancellationToken = default)
        {
            FailedEventId = accountingEventId; FailureRequest = request; Failure = failure;
            Receipts.Add(receipt); return Task.CompletedTask;
        }
        public Task<AccountingEventDto> PrepareGroupMemberInAmbientTransactionAsync(CreateAccountingEventDto request,
            CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task<AccountingEventDto> ValidatePreparedGroupMemberAsync(Guid accountingEventId,
            CreateAccountingEventDto request, CancellationToken cancellationToken = default) => Task.FromResult(Result);
        public Task<AccountingEventDto> ExecuteApprovedGroupMemberInAmbientTransactionAsync(Guid accountingEventId,
            ReleaseAccountingEventDto request, Guid producerIntentGroupId, Guid approvedCheckerId,
            Guid expectedAmbientTransactionId, CancellationToken cancellationToken = default) => Task.FromResult(Result);
    }
}
