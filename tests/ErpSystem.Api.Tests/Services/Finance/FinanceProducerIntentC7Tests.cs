using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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
        var service = Service(applicability, events, enabled: false);

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
        var service = Service(applicability, events);

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

        var action = () => Service(applicability, events).PrepareAsync(intent);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("PRODUCER_INTENT_UNBALANCED*");
        applicability.VerifyNoOtherCalls();
        events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApprovalAndRejection_AreDecisionOnlyCalls()
    {
        var applicability = Applicability();
        var events = new Mock<IAccountingEventService>();
        events.Setup(x => x.ApproveAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = "Approved" });
        events.Setup(x => x.RejectAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingEventDto { ProducerDecisionStatus = "Rejected" });
        var service = Service(applicability, events);

        (await service.ApproveAsync(Guid.NewGuid(), Intent(), new() { Reason = "Reviewed" })).ProducerDecisionStatus.Should().Be("Approved");
        (await service.RejectAsync(Guid.NewGuid(), Intent(), new() { Reason = "Invalid source" })).ProducerDecisionStatus.Should().Be("Rejected");

        events.Verify(x => x.ExecuteApprovedAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(),
            It.IsAny<IFinanceProducerExecutionParticipant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteApproved_BindsExactParticipantIdentity()
    {
        var applicability = Applicability();
        var events = new Mock<IAccountingEventService>();
        events.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AccountingEventDto
        { ProducerDecisionStatus = "Approved", Status = "PendingApproval", ReleaseReason = null, ProducerDecisionReason = "Reviewed" });
        events.Setup(x => x.ExecuteApprovedAsync(It.IsAny<Guid>(), It.IsAny<ReleaseAccountingEventDto>(),
                It.IsAny<IFinanceProducerExecutionParticipant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountingEventDto { Status = "Posted" });
        var service = Service(applicability, events);

        (await service.ExecuteApprovedAsync(Guid.NewGuid(), Intent(), new Participant("inventory.disposal.v1"))).Status.Should().Be("Posted");

        var conflict = () => service.ExecuteApprovedAsync(Guid.NewGuid(), Intent(), new Participant("inventory.other"));
        await conflict.Should().ThrowAsync<InvalidOperationException>().WithMessage("ACCOUNTING_EVENT_PARTICIPANT_CONFLICT*");
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

    private static FinanceProducerIntentService Service(Mock<IAccountingBookApplicabilityService> applicability,
        Mock<IAccountingEventService> events, bool enabled = true) => new(applicability.Object, events.Object,
        Options.Create(new FinanceProducerIntentOptions { Enabled = enabled }));

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

    private sealed class Participant(string identity) : IFinanceProducerExecutionParticipant
    {
        public string ParticipantIdentity { get; } = identity;
        public Task ExecuteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
