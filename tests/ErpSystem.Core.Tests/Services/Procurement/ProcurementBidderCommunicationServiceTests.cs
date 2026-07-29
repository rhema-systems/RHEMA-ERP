using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBidderCommunicationServiceTests
{
    [Fact]
    public async Task InitializeDerivesSuccessfulAndUnsuccessfulRfqRecipientsAndReplaysIdempotently()
    {
        await using var fixture = new Fixture("rfq");

        var first = await fixture.InitializeAsync("init-rfq");
        var replay = await fixture.InitializeAsync("init-rfq");

        replay.Id.Should().Be(first.Id);
        first.AwardFamily.Should().Be(
            ProcurementBidderCommunicationAwardFamily.RequestForQuotation);
        first.Recipients.Should().HaveCount(2);
        first.Recipients.Should().ContainSingle(item =>
            item.BusinessPartnerId == fixture.Winner.Id &&
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful &&
            item.BidOrQuoteIds.SequenceEqual(new[] { fixture.WinnerSubjectId }));
        first.Recipients.Should().ContainSingle(item =>
            item.BusinessPartnerId == fixture.Loser.Id &&
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful &&
            item.BidOrQuoteIds.SequenceEqual(new[] { fixture.LoserSubjectId }));
        first.AwardReadinessDecisionId.Should().Be(fixture.Readiness.Id);
        first.IntegrityHash.Should().HaveLength(64);
        (await fixture.Context.Set<ProcurementBidderCommunicationRegister>()
            .CountAsync()).Should().Be(1);
        (await fixture.Context.Set<ProcurementBidderCommunicationRecipient>()
            .CountAsync()).Should().Be(2);
        fixture.Events.Should().ContainSingle(item =>
            item.Action == "RegisterInitialized" &&
            item.DecisionKeys.Count == 14 &&
            item.SourceId == fixture.SourceId);
    }

    [Theory]
    [InlineData("formal", ProcurementBidderCommunicationAwardFamily.FormalTender)]
    [InlineData("exceptional", ProcurementBidderCommunicationAwardFamily.ExceptionalSourcing)]
    [InlineData("legacy", ProcurementBidderCommunicationAwardFamily.LegacyTenderAward)]
    public async Task InitializeSupportsEveryTenderAwardFamily(
        string sourceKind,
        ProcurementBidderCommunicationAwardFamily expectedFamily)
    {
        await using var fixture = new Fixture(sourceKind);

        var result = await fixture.InitializeAsync($"init-{sourceKind}");

        result.AwardFamily.Should().Be(expectedFamily);
        result.Recipients.Should().ContainSingle(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful);
        result.Recipients.Should().ContainSingle(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful);
        result.Recipients.Single(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful)
            .BusinessPartnerId.Should().Be(fixture.Winner.Id);
    }

    [Fact]
    public async Task StaleReadinessFailsBeforeAnyRegisterOrRecipientWrite()
    {
        await using var fixture = new Fixture("rfq");
        fixture.Readiness.IntegrityHash = new string('f', 64);

        var action = () => fixture.InitializeAsync("stale-readiness",
            expectedHash: new string('a', 64));

        await action.Should().ThrowAsync<ProcurementBidderCommunicationConflictException>()
            .Where(exception => exception.Code == "BIDDER_COMMUNICATION_READINESS_STALE");
        fixture.Context.Set<ProcurementBidderCommunicationRegister>().Should().BeEmpty();
        fixture.Context.Set<ProcurementBidderCommunicationRecipient>().Should().BeEmpty();
    }

    [Fact]
    public async Task DuplicateControlledAwardLineageFailsClosed()
    {
        await using var fixture = new Fixture("formal");
        var control = await fixture.Context.Set<ProcurementTenderControl>().SingleAsync();
        fixture.Context.Add(new ProcurementTenderControl
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = control.TenderId,
            SourcingCaseId = Guid.NewGuid(),
            MethodRuleId = Guid.NewGuid(),
            AuthorityRouteId = Guid.NewGuid(),
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            MethodRuleCode = "NCT-DUP",
            AuthorityRouteReference = "AUTH-DUP",
            Status = ProcurementTenderControlStatus.Awarded,
            AdvertisementReference = "ADV-DUP",
            PublicationChannel = "Portal",
            TenderDocumentReference = "DOC-DUP",
            TenderDocumentVersion = "1",
            AdvertisementEvidenceReference = "EVID-DUP",
            AdvertisedAtUtc = fixture.AwardedAtUtc.AddDays(-5),
            SubmissionDeadlineUtc = fixture.AwardedAtUtc.AddDays(-2),
            OpeningScheduledAtUtc = fixture.AwardedAtUtc.AddDays(-1),
            AwardBidId = fixture.WinnerSubjectId,
            AwardReference = "AWARD-DUP",
            AwardEvidenceReference = "AWARD-EVID-DUP",
            AwardedAtUtc = fixture.AwardedAtUtc,
            IntegrityHash = new string('d', 64),
            RowVersion = Guid.NewGuid().ToByteArray()
        });
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.InitializeAsync("duplicate-control");

        await action.Should().ThrowAsync<ProcurementBidderCommunicationConflictException>()
            .Where(exception => exception.Code ==
                "BIDDER_COMMUNICATION_SOURCE_LINEAGE_AMBIGUOUS");
        fixture.Context.Set<ProcurementBidderCommunicationRegister>().Should().BeEmpty();
    }

    [Fact]
    public async Task ApprovedLetterDispatchDeliveryAndAcknowledgementAreImmutableAndAudited()
    {
        await using var fixture = new Fixture("rfq");
        var register = await fixture.InitializeAsync("history-init");
        var recipient = register.Recipients.Single(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Unsuccessful);

        var letter = await fixture.ApproveLetterAsync(
            recipient, register, "letter-approved");
        var dispatch = await fixture.DispatchAsync(
            letter, recipient, "letter-dispatched");
        var delivery = await fixture.Service.RecordDeliveryAsync(
            dispatch.Id,
            new RecordProcurementBidderCommunicationDeliveryRequest
            {
                Outcome = ProcurementBidderCommunicationDeliveryOutcome.Delivered,
                OccurredAtUtc = DateTime.UtcNow,
                ProviderReference = "EMAIL-PROVIDER-001",
                EvidenceReference = "DELIVERY-EVID-001",
                IdempotencyKey = "delivery-recorded"
            },
            "delivery-recorded");
        var acknowledgement = await fixture.Service.RecordAcknowledgementAsync(
            dispatch.Id,
            new RecordProcurementBidderCommunicationAcknowledgementRequest
            {
                Outcome = ProcurementBidderCommunicationAcknowledgementOutcome.Received,
                AcknowledgementChannel = "Email",
                AcknowledgementReference = "ACK-001",
                EvidenceReference = "ACK-EVID-001",
                IdempotencyKey = "ack-recorded"
            },
            "ack-recorded");
        var replay = await fixture.Service.RecordAcknowledgementAsync(
            dispatch.Id,
            new RecordProcurementBidderCommunicationAcknowledgementRequest
            {
                Outcome = ProcurementBidderCommunicationAcknowledgementOutcome.Received,
                AcknowledgementChannel = "Email",
                AcknowledgementReference = "ACK-001",
                EvidenceReference = "ACK-EVID-001",
                IdempotencyKey = "ack-recorded"
            },
            "ack-replay");

        letter.Version.Should().Be(1);
        letter.TemplateChecksumSha256.Should().HaveLength(64);
        dispatch.Sequence.Should().Be(1);
        delivery.Sequence.Should().Be(1);
        acknowledgement.IntegrityHash.Should().HaveLength(64);
        replay.Id.Should().Be(acknowledgement.Id);
        (await fixture.Context.Set<ProcurementBidderCommunicationAcknowledgement>()
            .CountAsync()).Should().Be(1);
        fixture.Events.Select(item => item.Action).Should().Contain(
            ["LetterApproved", "LetterDispatched", "DeliveryRecorded",
                "AcknowledgementRecorded"]);
        fixture.Notifications.Select(item => item.TopicKey).Should().Contain(
            ["procurement.bidder-communication.dispatched",
                "procurement.bidder-communication.delivery",
                "procurement.bidder-communication.acknowledged"]);
    }

    [Fact]
    public async Task SodDenialPreventsApprovedLetterWrite()
    {
        await using var fixture = new Fixture("rfq");
        var register = await fixture.InitializeAsync("sod-init");
        var recipient = register.Recipients.Single(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful);
        fixture.DenySod();

        var action = () => fixture.ApproveLetterAsync(
            recipient, register, "sod-denied");

        await action.Should().ThrowAsync<ProcurementBidderCommunicationAuthorizationException>();
        fixture.Context.Set<ProcurementBidderCommunicationLetterVersion>().Should().BeEmpty();
    }

    [Fact]
    public async Task ExternalOverviewAcknowledgementAndAppealAreSupplierScoped()
    {
        await using var fixture = new Fixture("rfq");
        var register = await fixture.InitializeAsync("external-init");
        var loser = register.Recipients.Single(item =>
            item.BusinessPartnerId == fixture.Loser.Id);
        var winner = register.Recipients.Single(item =>
            item.BusinessPartnerId == fixture.Winner.Id);
        var loserLetter = await fixture.ApproveLetterAsync(
            loser, register, "external-loser-letter");
        var loserDispatch = await fixture.DispatchAsync(
            loserLetter, loser, "external-loser-dispatch");
        var winnerLetter = await fixture.ApproveLetterAsync(
            winner, register, "external-winner-letter");
        var winnerDispatch = await fixture.DispatchAsync(
            winnerLetter, winner, "external-winner-dispatch");
        await fixture.SwitchToExternalAsync(fixture.Loser.Id);

        var overview = await fixture.Service.GetExternalOverviewAsync(
            fixture.SourceType, fixture.SourceId);
        var acknowledgement = await fixture.Service.RecordExternalAcknowledgementAsync(
            loserDispatch.Id,
            new RecordProcurementBidderCommunicationAcknowledgementRequest
            {
                Outcome = ProcurementBidderCommunicationAcknowledgementOutcome.Received,
                AcknowledgementChannel = "SupplierPortal",
                AcknowledgementReference = "EXT-ACK-001",
                EvidenceReference = "EXT-ACK-EVID-001",
                IdempotencyKey = "external-ack"
            },
            "external-ack");
        var appeal = await fixture.Service.FileExternalAppealAsync(
            loser.Id,
            new FileProcurementBidderAppealRequest
            {
                Grounds = "The scoring outcome requires review.",
                EvidenceReference = "EXT-APPEAL-EVID-001",
                IdempotencyKey = "external-appeal"
            },
            "external-appeal");
        var foreignAck = () => fixture.Service.RecordExternalAcknowledgementAsync(
            winnerDispatch.Id,
            new RecordProcurementBidderCommunicationAcknowledgementRequest
            {
                Outcome = ProcurementBidderCommunicationAcknowledgementOutcome.Received,
                AcknowledgementChannel = "SupplierPortal",
                AcknowledgementReference = "FORGED-ACK",
                EvidenceReference = "FORGED-EVID",
                IdempotencyKey = "forged-ack"
            },
            "forged-ack");

        overview.Recipients.Should().ContainSingle().Which.BusinessPartnerId
            .Should().Be(fixture.Loser.Id);
        acknowledgement.AcknowledgedByBusinessPartnerId.Should().Be(fixture.Loser.Id);
        appeal.FiledByBusinessPartnerId.Should().Be(fixture.Loser.Id);
        await foreignAck.Should().ThrowAsync<ProcurementBidderCommunicationAuthorizationException>();
        (await fixture.Context.Set<ProcurementBidderCommunicationAcknowledgement>()
            .CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SecurityActionRequiresElapsedWindowsResolvedAppealsWorkflowAndOutcome()
    {
        await using var fixture = new Fixture("rfq", elapsedWindows: true);
        var register = await fixture.InitializeAsync("security-init");
        var recipient = register.Recipients.Single(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful);
        var letter = await fixture.ApproveLetterAsync(
            recipient, register, "security-letter");
        _ = await fixture.DispatchAsync(letter, recipient, "security-dispatch");
        var security = await fixture.Service.RegisterSecurityAsync(
            recipient.Id,
            new RegisterProcurementTenderSecurityRequest
            {
                RequestForQuotationQuoteId = fixture.WinnerSubjectId,
                InstrumentType = ProcurementTenderSecurityInstrumentType.BankGuarantee,
                InstrumentReference = "SEC-001",
                IssuerName = "TDC Bank",
                Amount = 1000m,
                CurrencyCode = "GHS",
                IssuedAtUtc = fixture.AwardedAtUtc.AddDays(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                EvidenceReference = "SEC-EVID-001",
                IdempotencyKey = "security-register"
            },
            "security-register");
        var workflow = await fixture.AddCompletedWorkflowAsync(security.Id);

        var action = await fixture.Service.RecordSecurityActionAsync(
            security.Id,
            new RecordProcurementTenderSecurityActionRequest
            {
                ActionType = ProcurementTenderSecurityActionType.Released,
                WorkflowInstanceId = workflow.Id,
                ActionReference = "SEC-REL-001",
                Reason = "Standstill and appeal windows elapsed without an appeal.",
                EvidenceReference = "SEC-REL-EVID-001",
                IdempotencyKey = "security-release"
            },
            "security-release");
        var duplicate = () => fixture.Service.RecordSecurityActionAsync(
            security.Id,
            new RecordProcurementTenderSecurityActionRequest
            {
                ActionType = ProcurementTenderSecurityActionType.Released,
                WorkflowInstanceId = workflow.Id,
                ActionReference = "SEC-REL-002",
                Reason = "Duplicate release attempt.",
                EvidenceReference = "SEC-REL-EVID-002",
                IdempotencyKey = "security-release-second"
            },
            "security-release-second");

        action.ActionType.Should().Be(ProcurementTenderSecurityActionType.Released);
        action.IntegrityHash.Should().HaveLength(64);
        await duplicate.Should().ThrowAsync<ProcurementBidderCommunicationConflictException>()
            .Where(exception => exception.Code == "TENDER_SECURITY_ALREADY_ACTIONED");
        fixture.Events.Should().Contain(item =>
            item.Action == "SecurityActionRecorded" &&
            item.DecisionKeys.Count == 14);
    }

    [Fact]
    public async Task SecurityActionBeforeConfiguredWindowsFailsWithoutPartialAction()
    {
        await using var fixture = new Fixture("rfq");
        var register = await fixture.InitializeAsync("early-security-init");
        var recipient = register.Recipients.Single(item =>
            item.Outcome == ProcurementBidderCommunicationRecipientOutcome.Successful);
        var letter = await fixture.ApproveLetterAsync(
            recipient, register, "early-security-letter");
        _ = await fixture.DispatchAsync(letter, recipient, "early-security-dispatch");
        var security = await fixture.Service.RegisterSecurityAsync(
            recipient.Id,
            new RegisterProcurementTenderSecurityRequest
            {
                RequestForQuotationQuoteId = fixture.WinnerSubjectId,
                InstrumentType = ProcurementTenderSecurityInstrumentType.BidBond,
                InstrumentReference = "SEC-EARLY",
                IssuerName = "TDC Bank",
                Amount = 500m,
                CurrencyCode = "GHS",
                IssuedAtUtc = fixture.AwardedAtUtc.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
                EvidenceReference = "SEC-EARLY-EVID",
                IdempotencyKey = "early-security-register"
            },
            "early-security-register");
        var workflow = await fixture.AddCompletedWorkflowAsync(security.Id);

        var action = () => fixture.Service.RecordSecurityActionAsync(
            security.Id,
            new RecordProcurementTenderSecurityActionRequest
            {
                ActionType = ProcurementTenderSecurityActionType.Released,
                WorkflowInstanceId = workflow.Id,
                ActionReference = "SEC-EARLY-REL",
                Reason = "Premature attempt.",
                EvidenceReference = "SEC-EARLY-REL-EVID",
                IdempotencyKey = "early-security-release"
            },
            "early-security-release");

        await action.Should().ThrowAsync<ProcurementBidderCommunicationConflictException>()
            .Where(exception => exception.Code == "TENDER_SECURITY_WINDOW_OPEN");
        fixture.Context.Set<ProcurementTenderSecurityAction>().Should().BeEmpty();
    }

    [Fact]
    public async Task CrossTenantOverviewCannotDiscoverExistingRegister()
    {
        await using var fixture = new Fixture("rfq");
        _ = await fixture.InitializeAsync("tenant-init");
        fixture.SwitchTenant(Guid.NewGuid());

        var action = () => fixture.Service.GetOverviewAsync(
            fixture.SourceType, fixture.SourceId);

        await action.Should().ThrowAsync<ProcurementBidderCommunicationNotFoundException>()
            .Where(exception => exception.Code ==
                "BIDDER_COMMUNICATION_REGISTER_NOT_FOUND");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current = new();
        private readonly Mock<IProcurementSodGuardService> _sod = new();
        private readonly Mock<IProcurementAwardReadinessService> _readiness = new();
        private Guid _currentTenantId;
        private Guid _currentUserId;
        private bool _external;
        private readonly DateTime _standstillEndsAtUtc;
        private readonly DateTime _appealWindowEndsAtUtc;

        public Fixture(string sourceKind, bool elapsedWindows = false)
        {
            TenantId = Guid.NewGuid();
            _currentTenantId = TenantId;
            _currentUserId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            AwardedAtUtc = elapsedWindows
                ? now.AddDays(-4)
                : now.AddHours(-2);
            _standstillEndsAtUtc = elapsedWindows
                ? now.AddDays(-2)
                : now.AddHours(1);
            _appealWindowEndsAtUtc = elapsedWindows
                ? now.AddDays(-1)
                : now.AddHours(2);
            Winner = Partner("SUP-WIN", "Successful Supplier", "winner@tdc.test");
            Loser = Partner("SUP-LOSE", "Unsuccessful Supplier", "loser@tdc.test");
            WinnerSubjectId = Guid.NewGuid();
            LoserSubjectId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Context.AddRange(Winner, Loser);
            switch (sourceKind)
            {
                case "rfq":
                    SeedRfq();
                    break;
                case "formal":
                    SeedTender(formal: true, exceptional: false, legacy: false);
                    break;
                case "exceptional":
                    SeedTender(formal: false, exceptional: true, legacy: false);
                    break;
                case "legacy":
                    SeedTender(formal: false, exceptional: false, legacy: true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceKind));
            }
            Context.SaveChanges();
            Readiness = new ProcurementAwardReadinessDto
            {
                Id = Guid.NewGuid(),
                DecisionSequence = 1,
                SourceType = SourceType,
                SourceId = SourceId,
                SourceReference = SourceReference,
                Status = ProcurementAwardReadinessDecisionStatus.Ready,
                IsCurrent = true,
                SourceIntegrityHash = new string('b', 64),
                IntegrityHash = new string('a', 64),
                Recommendation = new ProcurementAwardReadinessRecommendationDto
                {
                    SubjectType = SourceType ==
                                  ProcurementAwardReadinessSourceType.RequestForQuotation
                        ? "RfqQuote"
                        : "TenderBid",
                    SubjectIds = [WinnerSubjectId],
                    BusinessPartnerIds = [Winner.Id]
                }
            };
            _readiness.Setup(item => item.GetLatestAsync(
                    SourceType, SourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Readiness);

            _current.SetupGet(item => item.TenantId).Returns(() => _currentTenantId);
            _current.SetupGet(item => item.UserId).Returns(() => _currentUserId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(() => _external);
            _current.SetupGet(item => item.Username).Returns("operator@tdc.test");
            _current.SetupGet(item => item.FullName).Returns("TDC Operator");
            _current.SetupGet(item => item.Roles).Returns(() =>
                _external ? ["Supplier"] : ["Administrator"]);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => !_external &&
                    string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase));

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            _sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var controlEvents = new Mock<IProcurementControlEventService>();
            controlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>(
                    (request, _) => Events.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());
            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NotificationTopicEvent, CancellationToken>(
                    (notification, _) => Notifications.Add(notification))
                .Returns(Task.CompletedTask);

            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementBidderCommunicationService(
                _unitOfWork,
                _current.Object,
                access.Object,
                _sod.Object,
                controlEvents.Object,
                _readiness.Object,
                notifications.Object);
        }

        public Guid TenantId { get; }
        public Guid SourceId { get; private set; }
        public string SourceReference { get; private set; } = string.Empty;
        public ProcurementAwardReadinessSourceType SourceType { get; private set; }
        public DateTime AwardedAtUtc { get; }
        public Guid WinnerSubjectId { get; }
        public Guid LoserSubjectId { get; }
        public BusinessPartner Winner { get; }
        public BusinessPartner Loser { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementAwardReadinessDto Readiness { get; }
        public ProcurementBidderCommunicationService Service { get; }
        public List<ProcurementControlEventWriteRequest> Events { get; } = [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        public async Task<ProcurementBidderCommunicationOverviewDto> InitializeAsync(
            string key,
            string? expectedHash = null)
        {
            return await Service.InitializeAsync(
                new InitializeProcurementBidderCommunicationRegisterRequest
                {
                    SourceType = SourceType,
                    SourceId = SourceId,
                    StandstillEndsAtUtc = _standstillEndsAtUtc,
                    AppealWindowEndsAtUtc = _appealWindowEndsAtUtc,
                    StandstillAuthorityReference = "TDC-CONFIG-001",
                    ExpectedAwardReadinessDecisionId = Readiness.Id,
                    ExpectedAwardReadinessIntegrityHash =
                        expectedHash ?? Readiness.IntegrityHash,
                    IdempotencyKey = key
                },
                key);
        }

        public async Task<ProcurementBidderCommunicationLetterVersionDto> ApproveLetterAsync(
            ProcurementBidderCommunicationRecipientDto recipient,
            ProcurementBidderCommunicationOverviewDto register,
            string key)
        {
            var template = new ProcurementTenderDocumentTemplateVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TemplateKey = Guid.NewGuid(),
                TemplateCode = recipient.Outcome ==
                               ProcurementBidderCommunicationRecipientOutcome.Successful
                    ? "AWARD-SUCCESS"
                    : "AWARD-UNSUCCESSFUL",
                Name = "Approved bidder letter",
                DocumentTypeCode = recipient.Outcome ==
                                   ProcurementBidderCommunicationRecipientOutcome.Successful
                    ? "SUCCESSFUL_BIDDER_LETTER"
                    : "UNSUCCESSFUL_BIDDER_LETTER",
                Version = 1,
                Status = ProcurementTenderDocumentTemplateStatus.Published,
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                PolicySetId = Guid.NewGuid(),
                PolicySetCode = "POLICY-001",
                PolicySetVersion = 1,
                SourceConfigurationProfileId = Guid.NewGuid(),
                ContentReference = $"template:{key}",
                ContentChecksumSha256 = new string('c', 64),
                WorkflowDefinitionId = Guid.NewGuid(),
                ApprovalEvidenceReference = $"template-approval:{key}",
                PublishedAtUtc = DateTime.UtcNow.AddHours(-1),
                PublishedById = Guid.NewGuid(),
                PublishedByName = "Template Approver",
                LifecycleSnapshotJson = "{}",
                IntegrityHash = new string('d', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            Context.Add(template);
            var workflow = await AddCompletedWorkflowAsync(recipient.Id);
            await Context.SaveChangesAsync();
            return await Service.ApproveLetterAsync(
                recipient.Id,
                new ApproveProcurementBidderCommunicationLetterRequest
                {
                    TemplateVersionId = template.Id,
                    ContentReference = $"letter:{key}",
                    ContentChecksumSha256 = new string('e', 64),
                    WorkflowInstanceId = workflow.Id,
                    ApprovalReference = $"APP-{key}",
                    ApprovalEvidenceReference = $"APP-EVID-{key}",
                    ExpectedRegisterIntegrityHash = register.IntegrityHash,
                    IdempotencyKey = key
                },
                key);
        }

        public Task<ProcurementBidderCommunicationDispatchDto> DispatchAsync(
            ProcurementBidderCommunicationLetterVersionDto letter,
            ProcurementBidderCommunicationRecipientDto recipient,
            string key) =>
            Service.DispatchLetterAsync(
                letter.Id,
                new DispatchProcurementBidderCommunicationLetterRequest
                {
                    Channel = ProcurementBidderCommunicationDispatchChannel.Email,
                    Destination = recipient.RecipientEmail!,
                    DispatchReference = $"DSP-{key}",
                    DispatchEvidenceReference = $"DSP-EVID-{key}",
                    IdempotencyKey = key
                },
                key);

        public async Task<WorkflowInstance> AddCompletedWorkflowAsync(Guid entityId)
        {
            var workflow = new WorkflowInstance
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                WorkflowDefinitionId = Guid.NewGuid(),
                EntityId = entityId,
                EntityTypeId = Guid.NewGuid(),
                Status = WorkflowInstanceStatus.Completed,
                InitiatedById = Guid.NewGuid(),
                CreatedDate = DateTime.UtcNow.AddHours(-1),
                CompletedDate = DateTime.UtcNow.AddMinutes(-1)
            };
            Context.Add(workflow);
            await Context.SaveChangesAsync();
            return workflow;
        }

        public void DenySod()
        {
            _sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = false,
                    Message = "SOD conflict"
                });
        }

        public async Task SwitchToExternalAsync(Guid businessPartnerId)
        {
            _external = true;
            _currentUserId = Guid.NewGuid();
            Context.Add(new BusinessPartnerUser
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerId = businessPartnerId,
                UserId = _currentUserId,
                Role = "User",
                IsActive = true
            });
            await Context.SaveChangesAsync();
        }

        public void SwitchTenant(Guid tenantId) => _currentTenantId = tenantId;

        private BusinessPartner Partner(string code, string name, string email) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            PartnerCode = code,
            PartnerName = name,
            PartnerType = "Supplier",
            PrimaryEmail = email,
            PrimaryPhone = $"+23320{Random.Shared.Next(1000000, 9999999)}",
            IsActive = true,
            IsBlacklisted = false,
            ApprovalStatus =
                BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
            RegistrationStatus =
                BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus
        };

        private void SeedRfq()
        {
            SourceType = ProcurementAwardReadinessSourceType.RequestForQuotation;
            SourceId = Guid.NewGuid();
            SourceReference = "RFQ-COMMS-001";
            var itemId = Guid.NewGuid();
            Context.AddRange(
                new RequestForQuotation
                {
                    Id = SourceId,
                    TenantId = TenantId,
                    RfqNumber = SourceReference,
                    Title = "Bidder communication RFQ",
                    Status = "Awarded",
                    AwardedBusinessPartnerId = Winner.Id,
                    AwardedAt = AwardedAtUtc
                },
                new RequestForQuotationItem
                {
                    Id = itemId,
                    TenantId = TenantId,
                    RfqId = SourceId,
                    LineNumber = 1,
                    Description = "Controlled item",
                    Quantity = 1,
                    UnitOfMeasure = "EA"
                },
                new RequestForQuotationQuote
                {
                    Id = WinnerSubjectId,
                    TenantId = TenantId,
                    RfqId = SourceId,
                    BusinessPartnerId = Winner.Id,
                    Status = "Submitted",
                    SubmittedAt = AwardedAtUtc.AddDays(-1)
                },
                new RequestForQuotationQuote
                {
                    Id = LoserSubjectId,
                    TenantId = TenantId,
                    RfqId = SourceId,
                    BusinessPartnerId = Loser.Id,
                    Status = "Submitted",
                    SubmittedAt = AwardedAtUtc.AddDays(-1)
                },
                new RequestForQuotationAwardLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    RfqId = SourceId,
                    RfqItemId = itemId,
                    BusinessPartnerId = Winner.Id,
                    QuoteId = WinnerSubjectId,
                    UnitPrice = 100,
                    LineTotal = 100
                });
        }

        private void SeedTender(bool formal, bool exceptional, bool legacy)
        {
            SourceType = exceptional
                ? ProcurementAwardReadinessSourceType.ExceptionalSourcing
                : ProcurementAwardReadinessSourceType.Tender;
            SourceId = Guid.NewGuid();
            SourceReference = $"TND-COMMS-{(formal ? "FORMAL" : exceptional ? "EXC" : "LEGACY")}";
            Context.AddRange(
                new Tender
                {
                    Id = SourceId,
                    TenantId = TenantId,
                    TenderNumber = SourceReference,
                    Title = "Bidder communication tender",
                    TenderType = formal ? "NCT" : "Restricted",
                    Status = "Awarded",
                    AwardDate = AwardedAtUtc
                },
                new TenderBid
                {
                    Id = WinnerSubjectId,
                    TenantId = TenantId,
                    TenderId = SourceId,
                    BusinessPartnerId = Winner.Id,
                    BidNumber = "BID-WIN",
                    Status = "Accepted",
                    TotalBidAmount = 1000
                },
                new TenderBid
                {
                    Id = LoserSubjectId,
                    TenantId = TenantId,
                    TenderId = SourceId,
                    BusinessPartnerId = Loser.Id,
                    BidNumber = "BID-LOSE",
                    Status = "Rejected",
                    TotalBidAmount = 1100
                });
            if (formal)
            {
                var controlId = Guid.NewGuid();
                Context.AddRange(
                    new ProcurementTenderControl
                    {
                        Id = controlId,
                        TenantId = TenantId,
                        TenderId = SourceId,
                        SourcingCaseId = Guid.NewGuid(),
                        MethodRuleId = Guid.NewGuid(),
                        AuthorityRouteId = Guid.NewGuid(),
                        Method = ProcurementMethodType.NationalCompetitiveTendering,
                        MethodRuleCode = "NCT-001",
                        AuthorityRouteReference = "AUTH-001",
                        Status = ProcurementTenderControlStatus.Awarded,
                        AdvertisementReference = "ADV-001",
                        PublicationChannel = "Portal",
                        TenderDocumentReference = "DOC-001",
                        TenderDocumentVersion = "1",
                        AdvertisementEvidenceReference = "ADV-EVID-001",
                        AdvertisedAtUtc = AwardedAtUtc.AddDays(-5),
                        SubmissionDeadlineUtc = AwardedAtUtc.AddDays(-2),
                        OpeningScheduledAtUtc = AwardedAtUtc.AddDays(-1),
                        AwardBidId = WinnerSubjectId,
                        AwardReference = "FORMAL-AWARD-001",
                        AwardEvidenceReference = "FORMAL-AWARD-EVID-001",
                        AwardedAtUtc = AwardedAtUtc,
                        IntegrityHash = new string('1', 64),
                        RowVersion = Guid.NewGuid().ToByteArray()
                    },
                    Receipt(controlId, WinnerSubjectId, Winner.Id, "REC-WIN"),
                    Receipt(controlId, LoserSubjectId, Loser.Id, "REC-LOSE"));
            }
            else if (exceptional)
            {
                Context.Add(new ProcurementExceptionalSourcingControl
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TenderId = SourceId,
                    SourcingCaseId = Guid.NewGuid(),
                    MethodRuleId = Guid.NewGuid(),
                    ExceptionRuleId = Guid.NewGuid(),
                    AuthorityRouteId = Guid.NewGuid(),
                    Method = ProcurementMethodType.RestrictedTendering,
                    MethodRuleCode = "REST-001",
                    ExceptionRuleCode = "EXC-001",
                    AuthorityRouteReference = "AUTH-EXC-001",
                    Status = ProcurementExceptionalSourcingControlStatus.Awarded,
                    Justification = "Approved exception",
                    JustificationEvidenceReference = "JUST-EVID-001",
                    SupplierSelectionEvidenceReference = "SUP-EVID-001",
                    PreparedAtUtc = AwardedAtUtc.AddDays(-3),
                    PreparedById = Guid.NewGuid(),
                    WorkflowDefinitionId = Guid.NewGuid(),
                    AwardBidId = WinnerSubjectId,
                    AwardReference = "EXC-AWARD-001",
                    AwardEvidenceReference = "EXC-AWARD-EVID-001",
                    AwardedAtUtc = AwardedAtUtc,
                    IntegrityHash = new string('2', 64),
                    RowVersion = Guid.NewGuid().ToByteArray()
                });
            }
            else if (legacy)
            {
                Context.Add(new TenderAward
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TenderId = SourceId,
                    TenderBidId = WinnerSubjectId,
                    BusinessPartnerId = Winner.Id,
                    AwardDate = AwardedAtUtc,
                    OriginalBidAmount = 1000,
                    AwardedAmount = 1000,
                    Currency = "GHS",
                    Status = "Awarded"
                });
            }
        }

        private ProcurementTenderSubmissionReceipt Receipt(
            Guid controlId,
            Guid bidId,
            Guid partnerId,
            string number) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TenderControlId = controlId,
            TenderBidId = bidId,
            BusinessPartnerId = partnerId,
            ReceiptNumber = number,
            ReceivedAtUtc = AwardedAtUtc.AddDays(-2),
            SubmissionDeadlineUtc = AwardedAtUtc.AddDays(-2),
            Disposition = ProcurementTenderSubmissionDisposition.OnTimeAccepted,
            SealedSnapshotJson = "{}",
            IntegrityHash = new string('3', 64)
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
