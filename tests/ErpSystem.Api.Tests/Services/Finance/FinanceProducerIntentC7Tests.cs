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
            typeof(ProducerAccountingIntentsController).GetMethod(name)!.GetCustomAttribute<AuthorizeAttribute>()!
                .Policy.Should().Be(FinancePermissions.OrchestrateAccountingEvents);
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
    public void Registry_RejectsUnregisteredWrongContextExternalAndTransactionOwningHandlers()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var other = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        FluentActions.Invoking(() => new FinanceProducerExecutionRegistry([]).Resolve("INV", db))
            .Should().Throw<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_PARTICIPANT_UNREGISTERED:*");
        FluentActions.Invoking(() => new FinanceProducerExecutionRegistry([new Participant(other, "INV")]).Resolve("INV", db))
            .Should().Throw<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_PARTICIPANT_CONTEXT_CONFLICT:*");
        FluentActions.Invoking(() => new FinanceProducerExecutionRegistry([new Participant(db, "INV", external: true)]).Resolve("INV", db))
            .Should().Throw<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_PARTICIPANT_EXTERNAL_EFFECTS_UNSUPPORTED:*");
        FluentActions.Invoking(() => new FinanceProducerExecutionRegistry([new Participant(db, "INV", managesTransactions: true)]).Resolve("INV", db))
            .Should().Throw<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_PARTICIPANT_TRANSACTION_CONTROL_FORBIDDEN:*");
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
    public async Task ExecuteApproved_BindsExactParticipantIdentity()
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
        var handler = new Participant(harness.Db, "inventory.disposal.v1");
        harness.Registry.Setup(x => x.Resolve("INVENTORY.DISPOSAL.V1", harness.Db)).Returns(handler);
        harness.Executor.Result = new AccountingEventDto { Status = "Posted" };

        (await harness.Service.ExecuteApprovedAsync(Guid.NewGuid(), intent)).Status.Should().Be("Posted");

        harness.Registry.Verify(x => x.Resolve("INVENTORY.DISPOSAL.V1", harness.Db), Times.Once);
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
        var registry = new Mock<IFinanceProducerExecutionRegistry>();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenantId);
        var service = new FinanceProducerIntentService(applicability.Object, events.Object, executor,
            registry.Object, db, user.Object, Options.Create(new FinanceProducerIntentOptions { Enabled = enabled }));
        return new TestHarness(service, db, tenantId, executor, registry);
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

    private static ProducerAccountingIntentDto Intent() => new()
    {
        IdempotencyKey = "inventory-disposal-1", ParticipantIdentity = "inventory.disposal.v1",
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
            PostingRequest = ToFinance(intent.PostingRequest)
        }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
    };

    private static string Fingerprint(CreateAccountingEventDto request, Guid root) => (string)typeof(AccountingEventService)
        .GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [request, request.SelectionIdempotencyKey, 1, root])!;

    private static FinancePostingRequestV2Dto ToFinance(ProducerFinancePostingRequestDto source) => new()
    {
        SourceModule = source.SourceModule, OriginModuleCode = source.OriginModuleCode,
        SourceDocumentType = source.SourceDocumentType, SourceDocumentId = source.SourceDocumentId,
        PostingAction = source.PostingAction, PostingDate = source.PostingDate, Lines = source.Lines,
        AccountingBookCode = string.Empty
    };

    private sealed record TestHarness(FinanceProducerIntentService Service, ApplicationDbContext Db, Guid TenantId,
        StubExecutor Executor, Mock<IFinanceProducerExecutionRegistry> Registry);

    private sealed class StubExecutor : ITrustedAccountingEventExecutor
    {
        public AccountingEventDto Result { get; set; } = new();
        public Task<AccountingEventDto> ExecuteApprovedAsync(Guid accountingEventId, ReleaseAccountingEventDto request,
            IFinanceProducerExecutionHandler handler, CancellationToken cancellationToken = default) => Task.FromResult(Result);
    }

    private sealed class Participant(ApplicationDbContext db, string identity, bool external = false, bool managesTransactions = false)
        : IFinanceProducerExecutionHandler
    {
        public string ParticipantIdentity { get; } = identity;
        public ApplicationDbContext DbContext => db;
        public bool UsesExternalSideEffects => external;
        public bool ManagesTransactions => managesTransactions;
        public Task ExecuteAsync(FinanceProducerExecutionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
