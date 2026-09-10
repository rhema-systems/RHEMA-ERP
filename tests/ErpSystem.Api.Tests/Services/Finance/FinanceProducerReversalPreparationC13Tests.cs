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
        return new TestHarness(service, db, tenantId, actor, user, applicability, audit);
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
    private sealed record TestHarness(FinanceProducerIntentService Service, ApplicationDbContext Db,
        Guid TenantId, Guid Actor, Mock<ICurrentUserService> User,
        Mock<IAccountingBookApplicabilityService> Applicability,
        Mock<IFinanceAuditService> Audit) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
