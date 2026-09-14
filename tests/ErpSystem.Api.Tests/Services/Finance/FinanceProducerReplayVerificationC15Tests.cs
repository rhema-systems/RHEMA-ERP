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
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceProducerReplayVerificationC15Tests
{
    [Fact]
    public async Task PostedReplay_ReturnsOnlyExactDurableBookBlindAuthority()
    {
        await using var harness = Harness();
        var evidence = await SeedOriginalAsync(harness);

        var result = await Verify(harness, evidence.EventId, Request(harness, evidence));

        result.Should().Be(new FinanceProducerReplayVerificationResultDto(
            evidence.EventId, evidence.RequestFingerprint, evidence.OwnerEffect.EffectFingerprint,
            AccountingEventStatuses.Posted,
            evidence.PrimaryPostingEventId, evidence.PrimaryJournalEntryId));
        harness.Db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("source")]
    [InlineData("participant")]
    [InlineData("receipt")]
    [InlineData("requestFingerprint")]
    [InlineData("snapshotFingerprint")]
    [InlineData("status")]
    [InlineData("event")]
    [InlineData("posting")]
    [InlineData("journal")]
    [InlineData("defaultRepresentation")]
    public async Task ForgedOrStaleCoordinates_FailClosedWithoutMutation(string mutation)
    {
        await using var harness = Harness();
        var evidence = await SeedOriginalAsync(harness);
        var request = Request(harness, evidence);
        var eventId = evidence.EventId;

        switch (mutation)
        {
            case "tenant":
                harness.User.SetupGet(user => user.TenantId).Returns(Guid.NewGuid());
                break;
            case "source": request.SourceDocumentType = "AR_INVOICE"; break;
            case "participant": request.ParticipantIdentity = "SALES.OTHER.V1"; break;
            case "receipt": request.OwnerEffectReceipt.OwnerAction = "REVERSE"; break;
            case "requestFingerprint": request.AccountingEventRequestFingerprint = Hash('9'); break;
            case "snapshotFingerprint":
                (await harness.Db.AccountingEvents.SingleAsync(item => item.Id == eventId))
                    .ProducerIntentSnapshotHash = Hash('8');
                await harness.Db.SaveChangesAsync();
                harness.Db.ChangeTracker.Clear();
                break;
            case "status":
                (await harness.Db.AccountingEvents.SingleAsync(item => item.Id == eventId)).Status =
                    AccountingEventStatuses.PendingApproval;
                await harness.Db.SaveChangesAsync();
                harness.Db.ChangeTracker.Clear();
                break;
            case "event": eventId = Guid.NewGuid(); break;
            case "posting": request.FinancePostingEventId = Guid.NewGuid(); break;
            case "journal": request.JournalEntryId = Guid.NewGuid(); break;
            case "defaultRepresentation":
                var books = await harness.Db.AccountingBooks.OrderBy(book => book.Code).ToListAsync();
                books.Single(book => book.Id == evidence.PrimaryBookId).IsDefault = false;
                books.Single(book => book.Id != evidence.PrimaryBookId).IsDefault = true;
                await harness.Db.SaveChangesAsync();
                harness.Db.ChangeTracker.Clear();
                break;
        }

        var eventCount = await harness.Db.AccountingEvents.CountAsync();
        var receiptCount = await harness.Db.AccountingEventProducerReceipts.CountAsync();
        await FluentActions.Awaiting(() => Verify(harness, eventId, request)).Should()
            .ThrowAsync<Exception>();
        (await harness.Db.AccountingEvents.CountAsync()).Should().Be(eventCount);
        (await harness.Db.AccountingEventProducerReceipts.CountAsync()).Should().Be(receiptCount);
    }

    [Theory]
    [InlineData("reversalLineage")]
    [InlineData("originalReceipt")]
    public async Task ReversalReplay_RequiresCanonicalC13LineageAndOriginalAuthority(string mutation)
    {
        await using var harness = Harness();
        var original = await SeedOriginalAsync(harness);
        var reversal = await SeedPostedC13ReversalAsync(harness, original);
        var request = Request(harness, reversal);

        if (mutation == "reversalLineage")
        {
            var row = await harness.Db.AccountingEvents.SingleAsync(item => item.Id == reversal.EventId);
            row.RootAccountingEventId = Guid.NewGuid();
        }
        else
        {
            var receipt = await harness.Db.AccountingEventProducerReceipts
                .SingleAsync(item => item.AccountingEventId == original.EventId);
            receipt.OwnerAction = "ALTERED";
        }
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();

        await FluentActions.Awaiting(() => Verify(harness, reversal.EventId, request)).Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Contract_IsRegisteredOnFinanceService_WithoutHttpSurface()
    {
        typeof(FinanceProducerIntentService).GetInterfaces()
            .Should().Contain(typeof(IFinanceProducerReplayVerificationService));
        typeof(ProducerAccountingIntentsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Should().NotContain(method => method.Name.Contains("Replay", StringComparison.OrdinalIgnoreCase));
    }

    private static Task<FinanceProducerReplayVerificationResultDto> Verify(
        TestHarness harness, Guid eventId, FinanceProducerReplayVerificationRequestDto request) =>
        ((IFinanceProducerReplayVerificationService)harness.Service).VerifyPostedAsync(eventId, request);

    private static FinanceProducerReplayVerificationRequestDto Request(TestHarness harness, EventEvidence evidence) => new()
    {
        AccountingEventRequestFingerprint = evidence.RequestFingerprint,
        OriginatingModuleCode = "SALES",
        SourceDocumentType = "AR_CREDIT_NOTE",
        SourceDocumentId = evidence.OwnerId,
        PostingAction = "POST_CREDIT_NOTE",
        ParticipantIdentity = evidence.OwnerEffect.ParticipantCode,
        OwnerEffectReceipt = new ProducerOwnerEffectReceiptDto
        {
            TenantId = harness.TenantId,
            ParticipantCode = evidence.OwnerEffect.ParticipantCode,
            OwnerEntityType = evidence.OwnerEffect.OwnerEntityType,
            OwnerEntityId = evidence.OwnerEffect.OwnerEntityId,
            OwnerAction = evidence.OwnerEffect.OwnerAction,
            EffectFingerprint = evidence.OwnerEffect.EffectFingerprint
        },
        FinancePostingEventId = evidence.PrimaryPostingEventId,
        JournalEntryId = evidence.PrimaryJournalEntryId
    };

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
        user.SetupGet(value => value.UserName).Returns("c15-maker");
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
        return new TestHarness(service, db, tenantId, actor, user);
    }

    private static async Task<EventEvidence> SeedOriginalAsync(TestHarness harness)
    {
        var eventId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var primaryBookId = Guid.NewGuid();
        var localBookId = Guid.NewGuid();
        var ownerEffect = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = "SALES.CREDIT_NOTE.V1", OwnerEntityType = "SALES_CREDIT_NOTE",
            OwnerEntityId = ownerId, OwnerAction = "POST", EffectFingerprint = Hash('C')
        };
        var request = BaseRequest(harness.TenantId, eventId, ownerId, ownerEffect);
        var snapshot = JsonSerializer.Serialize(request, JsonOptions);
        var fingerprint = Fingerprint(request, eventId, 1, eventId);
        var evidenceId = Guid.NewGuid();
        var selection = new AccountingBookSelectionEvidence
        {
            Id = evidenceId, TenantId = harness.TenantId, EffectiveDate = request.PostingRequest.PostingDate.Date,
            OriginatingModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE", PostingAction = "POST_CREDIT_NOTE",
            IdempotencyKey = request.SelectionIdempotencyKey, CalculationInputHash = Hash('A'),
            SelectionFingerprint = Hash('B'), FrozenByUserId = harness.Actor, FrozenAtUtc = DateTime.UtcNow,
            Books =
            [
                SelectionBook(harness.TenantId, evidenceId, primaryBookId, 1, "IFRS", 'D'),
                SelectionBook(harness.TenantId, evidenceId, localBookId, 2, "LOCAL", 'E')
            ]
        };
        var primaryPostingEventId = Guid.NewGuid();
        var primaryJournalId = Guid.NewGuid();
        var item = Event(harness, request, eventId, eventId, 1, fingerprint, snapshot, selection);
        item.Postings =
        [
            Posting(harness.TenantId, eventId, 1, primaryBookId, 1, "IFRS", 'D', primaryPostingEventId, primaryJournalId),
            Posting(harness.TenantId, eventId, 1, localBookId, 2, "LOCAL", 'E', Guid.NewGuid(), Guid.NewGuid())
        ];
        item.ProducerReceipt = Receipt(harness, eventId, ownerEffect, fingerprint);
        harness.Db.AccountingBooks.AddRange(
            new AccountingBook { Id = primaryBookId, TenantId = harness.TenantId, Code = "IFRS", Name = "IFRS", IsDefault = true, IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS" },
            new AccountingBook { Id = localBookId, TenantId = harness.TenantId, Code = "LOCAL", Name = "Local", IsDefault = false, IsActive = true, AllowsPosting = true, FunctionalCurrencyCode = "GHS" });
        harness.Db.AccountingEvents.Add(item);
        await harness.Db.SaveChangesAsync();
        harness.Db.ChangeTracker.Clear();
        return new EventEvidence(eventId, ownerId, request, fingerprint, Sha256(snapshot), ownerEffect,
            primaryBookId, primaryPostingEventId, primaryJournalId);
    }

    private static async Task<EventEvidence> SeedPostedC13ReversalAsync(TestHarness harness, EventEvidence original)
    {
        var reversalId = DeterministicGuid(original.EventId, "reversal");
        var reversalEffect = new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = original.OwnerEffect.ParticipantCode,
            OwnerEntityType = original.OwnerEffect.OwnerEntityType,
            OwnerEntityId = original.OwnerId, OwnerAction = "REVERSE", EffectFingerprint = Hash('F')
        };
        var prepared = await harness.Service.PrepareReversalAsync(new PrepareProducerAccountingReversalDto
        {
            OriginalAccountingEventId = original.EventId, ReversalAccountingEventId = reversalId,
            IdempotencyKey = "SALES-CREDIT-NOTE-REVERSAL", ReversalDate = new DateTime(2026, 9, 10),
            Reason = "Customer credit note reversed", ParticipantIdentity = original.OwnerEffect.ParticipantCode,
            ExpectedOwnerEffect = reversalEffect
        });
        harness.User.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        await harness.Service.ApprovePreparedAsync(reversalId,
            new DecideProducerAccountingIntentDto { Reason = "Independent reversal approval" });
        var row = await harness.Db.AccountingEvents.Include(item => item.Postings)
            .SingleAsync(item => item.Id == reversalId);
        row.Status = AccountingEventStatuses.Posted;
        row.CompletedAtUtc = DateTime.UtcNow;
        var selected = await harness.Db.AccountingBookSelectionEvidenceBooks.AsNoTracking()
            .Where(item => item.AccountingBookSelectionEvidenceId == row.AccountingBookSelectionEvidenceId)
            .OrderBy(item => item.SelectionOrder).ToListAsync();
        var primaryPostingEventId = Guid.NewGuid();
        var primaryJournalId = Guid.NewGuid();
        row.Postings.Should().HaveCount(selected.Count);
        foreach (var posting in row.Postings)
        {
            posting.Status = AccountingEventStatuses.Posted;
            posting.FinancePostingEventId = posting.SelectionOrder == 1
                ? primaryPostingEventId : Guid.NewGuid();
            posting.JournalEntryId = posting.SelectionOrder == 1
                ? primaryJournalId : Guid.NewGuid();
            posting.PostedAtUtc = DateTime.UtcNow;
            posting.FailureMessage = null;
        }
        harness.Db.AccountingEventProducerReceipts.Add(
            Receipt(harness, reversalId, reversalEffect, row.RequestFingerprint));
        await harness.Db.SaveChangesAsync();
        var evidence = new EventEvidence(reversalId, original.OwnerId,
            JsonSerializer.Deserialize<CreateAccountingEventDto>(row.ProducerIntentSnapshotJson!, JsonOptions)!,
            row.RequestFingerprint, row.ProducerIntentSnapshotHash!, reversalEffect,
            original.PrimaryBookId, primaryPostingEventId, primaryJournalId);
        harness.Db.ChangeTracker.Clear();
        return evidence;
    }

    private static CreateAccountingEventDto BaseRequest(Guid tenantId, Guid eventId, Guid ownerId,
        ProducerOwnerEffectIdentityDto ownerEffect) => new()
    {
        AccountingEventId = eventId, EventKind = AccountingEventKinds.Original,
        SelectionIdempotencyKey = "SALES-CREDIT-NOTE-ORIGINAL", ExpectedCalculationInputHash = Hash('A'),
        ExpectedSelectionFingerprint = Hash('B'), ProducerParticipantIdentity = ownerEffect.ParticipantCode,
        ExpectedOwnerEffect = ownerEffect,
        PostingRequest = new FinancePostingRequestV2Dto
        {
            SourceModule = "SALES", OriginModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE",
            SourceDocumentId = ownerId, SourceDocumentTenantId = tenantId, PostingAction = "POST_CREDIT_NOTE",
            PostingDate = new DateTime(2026, 8, 31), Description = "Posted credit note",
            FunctionalCurrencyCode = "GHS", AccountingBookCode = string.Empty,
            Lines =
            [
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), DebitAmount = 125m, LineNumber = 1, Description = "Receivable" },
                new FinancePostingLineDto { AccountId = Guid.NewGuid(), CreditAmount = 125m, LineNumber = 2, Description = "Revenue" }
            ]
        }
    };

    private static AccountingEvent Event(TestHarness harness, CreateAccountingEventDto request, Guid id,
        Guid rootId, int version, string fingerprint, string snapshot, AccountingBookSelectionEvidence selection) => new()
    {
        Id = id, TenantId = harness.TenantId, RootAccountingEventId = rootId, Version = version,
        EventKind = request.EventKind, IdempotencyKey = request.SelectionIdempotencyKey,
        OriginatingModuleCode = "SALES", SourceDocumentType = "AR_CREDIT_NOTE",
        SourceDocumentId = request.PostingRequest.SourceDocumentId, PostingAction = "POST_CREDIT_NOTE",
        EventDate = request.PostingRequest.PostingDate.Date, Status = AccountingEventStatuses.Posted,
        SelectionFingerprint = request.ExpectedSelectionFingerprint, RequestFingerprint = fingerprint,
        AccountingBookSelectionEvidenceId = selection.Id, AccountingBookSelectionEvidence = selection,
        RequestedAtUtc = DateTime.UtcNow, RequestedByUserId = harness.Actor,
        PreparedAtUtc = DateTime.UtcNow, PreparedByUserId = harness.Actor,
        ReleasedAtUtc = DateTime.UtcNow, ReleasedByUserId = Guid.NewGuid(), ReleaseReason = "Approved",
        CompletedAtUtc = DateTime.UtcNow, ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved,
        ProducerParticipantIdentity = request.ProducerParticipantIdentity,
        ProducerIntentSnapshotJson = snapshot, ProducerIntentSnapshotHash = Sha256(snapshot),
        ProducerDecidedAtUtc = DateTime.UtcNow, ProducerDecidedByUserId = Guid.NewGuid(),
        ProducerDecisionReason = "Independent approval", CreatedAt = DateTime.UtcNow, CreatedBy = "c15-test"
    };

    private static AccountingBookSelectionEvidenceBook SelectionBook(Guid tenantId, Guid evidenceId,
        Guid bookId, int order, string code, char authority) => new()
    {
        TenantId = tenantId, AccountingBookSelectionEvidenceId = evidenceId, AccountingBookId = bookId,
        SelectionOrder = order, AccountingBookCodeSnapshot = code, AuthorityFingerprint = Hash(authority)
    };

    private static AccountingEventPosting Posting(Guid tenantId, Guid eventId, int version, Guid bookId,
        int order, string code, char authority, Guid postingEventId, Guid journalId) => new()
    {
        TenantId = tenantId, AccountingEventId = eventId, EventVersion = version, AccountingBookId = bookId,
        SelectionOrder = order, AccountingBookCodeSnapshot = code, AuthorityFingerprint = Hash(authority),
        Status = AccountingEventStatuses.Posted, FinancePostingEventId = postingEventId,
        JournalEntryId = journalId, PostedAtUtc = DateTime.UtcNow
    };

    private static AccountingEventProducerReceipt Receipt(TestHarness harness, Guid eventId,
        ProducerOwnerEffectIdentityDto effect, string fingerprint) => new()
    {
        TenantId = harness.TenantId, AccountingEventId = eventId, ParticipantCode = effect.ParticipantCode,
        OwnerEntityType = effect.OwnerEntityType, OwnerEntityId = effect.OwnerEntityId,
        OwnerAction = effect.OwnerAction, EffectFingerprint = effect.EffectFingerprint,
        RequestFingerprint = fingerprint, RecordedAtUtc = DateTime.UtcNow, RecordedByUserId = harness.Actor
    };

    private static string Fingerprint(CreateAccountingEventDto request, Guid id, int version, Guid root)
    {
        request.AccountingEventId = id;
        return (string)typeof(AccountingEventService)
            .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [request, request.SelectionIdempotencyKey.Trim().ToUpperInvariant(), version, root])!;
    }

    private static Guid DeterministicGuid(Guid source, string purpose)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source:N}:{purpose}"));
        return new Guid(bytes[..16]);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Hash(char value) => new(value, 64);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record EventEvidence(Guid EventId, Guid OwnerId, CreateAccountingEventDto Request,
        string RequestFingerprint, string SnapshotHash, ProducerOwnerEffectIdentityDto OwnerEffect,
        Guid PrimaryBookId, Guid PrimaryPostingEventId, Guid PrimaryJournalEntryId);
    private sealed record TestHarness(FinanceProducerIntentService Service, ApplicationDbContext Db,
        Guid TenantId, Guid Actor, Mock<ICurrentUserService> User) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
