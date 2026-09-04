using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
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

public sealed class ProcurementAwardReadinessServiceTests
{
    [Fact]
    public async Task FormalTenderUsesOneAggregateLockedProjectionPerRequiredPhase()
    {
        await using var fixture = new Fixture();
        await fixture.AddFormalControlledEvaluationAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("aggregate-phase-projections"),
            "aggregate-phase-projections");

        var prerequisites = result.PrerequisiteGroups
            .SelectMany(group => group.Items)
            .ToList();
        prerequisites.Should().Contain(item =>
                item.Code == "CURRENT_TECHNICAL_SCORES" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "CURRENT_FINANCIAL_SCORES" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "TENDER_TECHNICAL_FINANCIAL_SCORER_SOD" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed);
        result.Evaluations.Should().OnlyContain(item =>
            item.ScoreAttempts.Count == 1);
    }

    [Fact]
    public async Task FormalTenderBlocksWhenAggregatePhasesWereLockedBySameEvaluator()
    {
        await using var fixture = new Fixture();
        await fixture.AddFormalControlledEvaluationAsync(useSamePhaseEvaluator: true);

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("aggregate-scorer-sod"),
            "aggregate-scorer-sod");

        var prerequisites = result.PrerequisiteGroups
            .SelectMany(group => group.Items)
            .ToList();
        prerequisites.Should().Contain(item =>
                item.Code == "CURRENT_TECHNICAL_SCORES" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "CURRENT_FINANCIAL_SCORES" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "TENDER_TECHNICAL_FINANCIAL_SCORER_SOD" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Failed);
        result.BlockedReasons.Should().Contain(item =>
            item.Contains("TENDER_TECHNICAL_FINANCIAL_SCORER_SOD",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SubmittedLockedLegacyEvaluationProducesWinnerRecommendation()
    {
        await using var fixture = new Fixture();
        fixture.Evaluation.Status = "Submitted";
        await fixture.Context.SaveChangesAsync();
        await fixture.AddLockedScoreAttemptAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("submitted-winner"),
            "submitted-winner");

        result.Recommendation.SubjectIds.Should().ContainSingle()
            .Which.Should().Be(fixture.Bid.Id);
        result.Recommendation.BusinessPartnerIds.Should().ContainSingle()
            .Which.Should().Be(fixture.Partner.Id);
        result.Recommendation.RecommendedByUserId.Should()
            .Be(fixture.Evaluation.TenderEvaluator.UserId);
        result.Evaluations.Should().ContainSingle(item =>
            item.EvaluationId == fixture.Evaluation.Id &&
            item.Status == "Submitted" &&
            item.ScoreAttempts.Count == 1 &&
            item.ScoreAttempts[0].Status == ProcurementEvaluationScoreSheetStatus.Locked);
        result.PrerequisiteGroups.SelectMany(group => group.Items)
            .Should().Contain(item =>
                item.Code == "LEGACY_RECOMMENDATION_UNAMBIGUOUS" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "LEGACY_EVALUATIONS_COMPLETE" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed)
            .And.Contain(item =>
                item.Code == "LEGACY_SCORE_ATTEMPTS_CURRENT" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Passed);
    }

    [Fact]
    public async Task DraftLegacyEvaluationBlocksWinnerDespiteSubmittedRecommendation()
    {
        await using var fixture = new Fixture();
        fixture.Evaluation.Status = "Submitted";
        await fixture.Context.SaveChangesAsync();
        var draft = await fixture.AddDraftEvaluationAsync();
        await fixture.AddLockedScoreAttemptAsync(fixture.Evaluation, draft);
        var request = fixture.Request("draft-incomplete");
        request.ExpectedRecommendedSubjectIds.Clear();
        request.ExpectedBusinessPartnerIds.Clear();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            request,
            "draft-incomplete");

        result.Status.Should().Be(ProcurementAwardReadinessDecisionStatus.Blocked);
        result.Recommendation.SubjectIds.Should().BeEmpty();
        result.Recommendation.BusinessPartnerIds.Should().BeEmpty();
        result.AllowedActions.Should().NotContain("RecordAward");
        result.PrerequisiteGroups.SelectMany(group => group.Items)
            .Should().Contain(item =>
                item.Code == "LEGACY_EVALUATIONS_COMPLETE" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.Failed);
    }

    [Fact]
    public async Task RepeatedLegacyEvaluationIsIdempotentAndLatestHashRemainsCurrent()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request("legacy-stable");

        var first = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            request,
            "legacy-stable");
        var replay = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            request,
            "legacy-stable-replay");
        var latest = await fixture.Service.GetLatestAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id);

        replay.Id.Should().Be(first.Id);
        replay.SourceIntegrityHash.Should().Be(first.SourceIntegrityHash);
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(first.Id);
        latest.IsCurrent.Should().BeTrue();
        var count = await fixture.Context.ProcurementAwardReadinessDecisions
            .CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task ExpectedRecommendationAssertionsCannotReplaceServerDerivedRecommendation()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request("assertion-mismatch");
        request.ExpectedRecommendedSubjectIds = [Guid.NewGuid()];

        await fixture.Service.Invoking(service => service.EvaluateAsync(
                ProcurementAwardReadinessSourceType.Tender,
                fixture.Tender.Id,
                request,
                "assertion-mismatch"))
            .Should().ThrowAsync<ProcurementAwardReadinessConflictException>()
            .Where(exception =>
                exception.Code ==
                "AWARD_READINESS_RECOMMENDATION_SUBJECT_MISMATCH");

        fixture.Context.ProcurementAwardReadinessDecisions.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedRecommendedBidderVerificationProducesRetainedBlockedDecision()
    {
        await using var fixture = new Fixture();
        await fixture.AddFailedVerificationAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("failed-verification"),
            "failed-verification");

        result.Status.Should().Be(ProcurementAwardReadinessDecisionStatus.Blocked);
        result.Verifications.Should().ContainSingle(item =>
            item.TenderBidId == fixture.Bid.Id &&
            item.BusinessPartnerId == fixture.Partner.Id &&
            item.BidderStatus == "Failed");
        result.BlockedReasons.Should().Contain(item =>
            item.Contains("AWARD_VERIFICATION_PASSED",
                StringComparison.Ordinal));
        (await fixture.Context.ProcurementAwardReadinessDecisions
                .SingleAsync())
            .IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CrossTenantReadCannotDiscoverExistingDecision()
    {
        await using var fixture = new Fixture();
        await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("tenant-bound"),
            "tenant-bound");
        fixture.SwitchTenant(Guid.NewGuid());

        await fixture.Service.Invoking(service => service.GetLatestAsync(
                ProcurementAwardReadinessSourceType.Tender,
                fixture.Tender.Id))
            .Should().ThrowAsync<ProcurementAwardReadinessNotFoundException>()
            .Where(exception => exception.Code == "TENDER_NOT_FOUND");
    }

    [Fact]
    public async Task MissingDec011ConfigurationIsAdvisoryAtTenderAwardReadiness()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("optional-dec-011"),
            "optional-dec-011");

        var supplier = result.Suppliers.Should().ContainSingle().Subject;
        supplier.Errors.Should().NotContain(item =>
            item.Contains("DEC-011", StringComparison.OrdinalIgnoreCase));
        supplier.Warnings.Should().ContainSingle(item =>
            item.Contains("No effective DEC-011", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingOptionalAwardVerificationChecklistDoesNotBlockTenderReadiness()
    {
        await using var fixture = new Fixture();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            fixture.Request("optional-award-verification"),
            "optional-award-verification");

        result.PrerequisiteGroups
            .SelectMany(group => group.Items)
            .Should().Contain(item =>
                item.Code == "AWARD_VERIFICATION_NOT_CONFIGURED" &&
                item.Status == ProcurementAwardReadinessPrerequisiteStatus.NotApplicable);
        result.BlockedReasons.Should().NotContain(item =>
            item.Contains("AWARD_VERIFICATION", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SodPreflightRequiresReadPermissionRatherThanAwardApproval()
    {
        await using var fixture = new Fixture();

        _ = await fixture.Service.GetEvaluatorAwardApproverSodStatusAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            "read-only-preflight");

        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.records.read"),
            "read-only-preflight",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.tender.approve"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _current;
        private Guid _tenantId;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            _tenantId = TenantId;
            ActorId = Guid.NewGuid();
            SupplierControllerId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Code = "TDC",
                Name = "TDC",
                Status = TenantStatus.Active
            });
            Partner = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-READY-001",
                PartnerName = "Readiness Supplier",
                PartnerType = "Supplier",
                IsActive = true,
                IsBlacklisted = false,
                ApprovalStatus =
                    BusinessPartnerLifecyclePolicy.ApprovedApprovalStatus,
                RegistrationStatus =
                    BusinessPartnerLifecyclePolicy.ActiveRegistrationStatus,
                ApprovedById = SupplierControllerId
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-LEGACY-READY-001",
                Title = "Legacy award readiness",
                TenderType = "NCT",
                Status = "Closed",
                CreatedById = Guid.NewGuid()
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                BusinessPartnerId = Partner.Id,
                BidNumber = "BID-READY-001",
                Status = "Accepted",
                TotalBidAmount = 1000m
            };
            var evaluator = new TenderEvaluator
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                UserId = Guid.NewGuid(),
                Role = "Evaluator",
                Status = "Completed",
                CompletedDate = DateTime.UtcNow.AddHours(-1)
            };
            Evaluation = new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidId = Bid.Id,
                TenderEvaluatorId = evaluator.Id,
                TenderEvaluator = evaluator,
                TenderBid = Bid,
                EvaluationDate = DateTime.UtcNow.AddHours(-2),
                SubmittedDate = DateTime.UtcNow.AddHours(-1),
                Status = "Approved",
                TotalScore = 90m,
                IsRecommended = true,
                Recommendation = "Highest current evaluated score."
            };
            Context.AddRange(Partner, Tender, Bid, evaluator, Evaluation);
            Context.SaveChanges();

            _unitOfWork = new UnitOfWork(Context);
            _current = new Mock<ICurrentUserProvider>();
            _current.SetupGet(item => item.TenantId).Returns(() => _tenantId);
            _current.SetupGet(item => item.UserId).Returns(ActorId);
            _current.SetupGet(item => item.IsAuthenticated).Returns(true);
            _current.SetupGet(item => item.IsExternalUser).Returns(false);
            _current.SetupGet(item => item.Username).Returns("approver@tdc.test");
            _current.SetupGet(item => item.FullName).Returns("TDC Award Approver");
            _current.SetupGet(item => item.Roles)
                .Returns(["TDC_HEAD_OF_PROCUREMENT"]);
            _current.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns(false);
            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });
            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSodGuardDecisionDto
                {
                    Allowed = true
                });
            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Service = new ProcurementAwardReadinessService(
                _unitOfWork,
                _current.Object,
                Access.Object,
                sod.Object,
                events.Object);
        }

        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid SupplierControllerId { get; }
        public ApplicationDbContext Context { get; }
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public TenderEvaluation Evaluation { get; }
        public BusinessPartner Partner { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementAwardReadinessService Service { get; }

        public EvaluateProcurementAwardReadinessRequest Request(string key) => new()
        {
            IdempotencyKey = key,
            ExpectedRecommendedSubjectIds = [Bid.Id],
            ExpectedBusinessPartnerIds = [Partner.Id]
        };

        public async Task AddFormalControlledEvaluationAsync(
            bool useSamePhaseEvaluator = false)
        {
            var technicalEvaluatorId = Guid.NewGuid();
            var financialEvaluatorId = useSamePhaseEvaluator
                ? technicalEvaluatorId
                : Guid.NewGuid();
            var technicalSnapshot = JsonSerializer.Serialize(new
            {
                evaluatorUserId = technicalEvaluatorId,
                scores = new[] { new { bidId = Bid.Id, score = 90m, qualified = true } }
            });
            var financialSnapshot = JsonSerializer.Serialize(new
            {
                evaluatorUserId = financialEvaluatorId,
                recommendedBidId = Bid.Id,
                recommendationReason = "Best evaluated responsive bid.",
                scores = new[] { new { bidId = Bid.Id, evaluatedAmount = 1000m } }
            });
            var now = DateTime.UtcNow;
            var control = new ProcurementTenderControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                SourcingCaseId = Guid.NewGuid(),
                MethodRuleId = Guid.NewGuid(),
                AuthorityRouteId = Guid.NewGuid(),
                Method = ProcurementMethodType.NationalCompetitiveTendering,
                MethodRuleCode = "METHOD-NCT",
                AuthorityRouteReference = "AUTH-NCT",
                Status = ProcurementTenderControlStatus.Approved,
                AdvertisementReference = "ADV-NCT",
                PublicationChannel = "GHANEPS",
                TenderDocumentReference = "DOC-NCT",
                TenderDocumentVersion = "1",
                AdvertisementEvidenceReference = "evidence://advertisement",
                AdvertisedAtUtc = now.AddDays(-5),
                SubmissionDeadlineUtc = now.AddDays(-2),
                OpeningScheduledAtUtc = now.AddDays(-2),
                OpenedAtUtc = now.AddDays(-1),
                TechnicalEvaluatedAtUtc = now.AddHours(-8),
                TechnicalEvaluationSnapshotJson = technicalSnapshot,
                TechnicalEvaluationIntegrityHash = HashJson(technicalSnapshot),
                TechnicalEvaluationEvidenceReference = "evidence://technical",
                FinancialEvaluatedAtUtc = now.AddHours(-4),
                FinancialEvaluationSnapshotJson = financialSnapshot,
                FinancialEvaluationIntegrityHash = HashJson(financialSnapshot),
                FinancialEvaluationEvidenceReference = "evidence://financial",
                RecommendedBidId = Bid.Id,
                AuthorityApprovalReference = "approval://authority",
                ApprovalActorsJson = JsonSerializer.Serialize(new[] { Guid.NewGuid() }),
                ApprovedAtUtc = now.AddHours(-2),
                ApprovedById = Guid.NewGuid(),
                LifecycleSnapshotJson = "{}",
                IntegrityHash = new string('t', 64),
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var committee = new ProcurementEvaluationCommitteeControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = Tender.Id,
                Version = 1,
                SourceReference = Tender.TenderNumber,
                Purpose = "Controlled aggregate tender evaluation.",
                Status = ProcurementEvaluationCommitteeControlStatus.Active,
                CommitteeTemplateId = Guid.NewGuid(),
                CommitteeCode = "TDC_EVALUATION",
                CommitteeName = "Tender Evaluation Committee",
                RequiredQuorum = 2,
                PolicySetId = Guid.NewGuid(),
                PolicyCode = "TDC-POLICY",
                PolicyVersion = 1,
                MethodRuleId = control.MethodRuleId,
                MethodRuleCode = control.MethodRuleCode,
                EffectiveFromUtc = now.AddDays(-5),
                ActivatedAtUtc = now.AddDays(-5),
                ActivatedByUserId = Guid.NewGuid(),
                ActivationEvidenceReference = "evidence://committee-activation",
                CompositionSnapshotJson = "{}",
                CompositionIntegrityHash = new string('c', 64),
                CreationIdempotencyKey = $"committee-{Guid.NewGuid():N}",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var technicalAppointment = Appointment(
                committee, technicalEvaluatorId, "Technical Evaluator");
            var financialAppointment = useSamePhaseEvaluator
                ? technicalAppointment
                : Appointment(committee, financialEvaluatorId, "Financial Evaluator");
            var additionalVotingAppointment = Appointment(
                committee, Guid.NewGuid(), "Independent Voting Evaluator");
            var technicalMeeting = Meeting(
                committee, ProcurementEvaluationPhase.Technical, 1);
            var financialMeeting = Meeting(
                committee, ProcurementEvaluationPhase.Financial, 2);
            var technicalSheet = ScoreSheet(
                committee,
                technicalMeeting,
                technicalAppointment,
                ProcurementEvaluationPhase.Technical,
                technicalSnapshot);
            var financialSheet = ScoreSheet(
                committee,
                financialMeeting,
                financialAppointment,
                ProcurementEvaluationPhase.Financial,
                financialSnapshot);

            Context.AddRange(
                control,
                committee,
                technicalAppointment,
                additionalVotingAppointment,
                technicalMeeting,
                financialMeeting,
                technicalSheet,
                financialSheet);
            if (!useSamePhaseEvaluator)
                Context.Add(financialAppointment);
            await Context.SaveChangesAsync();
        }

        private ProcurementEvaluationCommitteeAppointment Appointment(
            ProcurementEvaluationCommitteeControl committee,
            Guid userId,
            string roleName) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CommitteeControlId = committee.Id,
            CommitteeControl = committee,
            CommitteeMemberId = Guid.NewGuid(),
            ResponsibilityAssignmentId = Guid.NewGuid(),
            UserId = userId,
            UserDisplayName = roleName,
            RoleName = roleName,
            MemberKind = ProcurementCommitteeMemberKind.VotingMember,
            IsVoting = true,
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-5),
            Status = ProcurementEvaluationAppointmentStatus.Accepted,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-4),
            AcceptanceSignatureReference = "signature://acceptance",
            AcceptanceEvidenceReference = "evidence://acceptance",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        private ProcurementEvaluationMeeting Meeting(
            ProcurementEvaluationCommitteeControl committee,
            ProcurementEvaluationPhase phase,
            int sequence) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CommitteeControlId = committee.Id,
            CommitteeControl = committee,
            Sequence = sequence,
            Phase = phase,
            Status = ProcurementEvaluationMeetingStatus.Closed,
            MeetingMode = "Physical",
            MeetingChannel = "Board room",
            ScheduledAtUtc = DateTime.UtcNow.AddDays(-2),
            StartedAtUtc = DateTime.UtcNow.AddDays(-2),
            ClosedAtUtc = DateTime.UtcNow.AddDays(-2).AddHours(1),
            EligibleVotingMemberCount = 3,
            SignedVotingAttendanceCount = 3,
            ChairPresent = true,
            SecretaryPresent = true,
            QuorumMet = true,
            EvidenceReference = $"evidence://{phase}-meeting",
            QuorumSnapshotJson = "{}",
            QuorumIntegrityHash = new string('q', 64),
            IdempotencyKey = $"meeting-{phase}-{Guid.NewGuid():N}",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        private ProcurementEvaluationScoreSheet ScoreSheet(
            ProcurementEvaluationCommitteeControl committee,
            ProcurementEvaluationMeeting meeting,
            ProcurementEvaluationCommitteeAppointment appointment,
            ProcurementEvaluationPhase phase,
            string snapshot) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CommitteeControlId = committee.Id,
            CommitteeControl = committee,
            MeetingId = meeting.Id,
            Meeting = meeting,
            AppointmentId = appointment.Id,
            Appointment = appointment,
            Phase = phase,
            ScoreSubjectType = "ProcurementTenderControl",
            ScoreSubjectId = Tender.Id,
            Attempt = 1,
            Status = ProcurementEvaluationScoreSheetStatus.Locked,
            SubmittedAtUtc = DateTime.UtcNow.AddHours(-4),
            SubmittedByUserId = appointment.UserId,
            SubmittedByName = appointment.UserDisplayName,
            ScoreSnapshotJson = snapshot,
            SignatureReference = $"signature://{phase}",
            EvidenceReference = $"evidence://{phase}",
            IntegrityHash = HashJson(snapshot),
            IdempotencyKey = $"score-{phase}-{Guid.NewGuid():N}",
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        private static string HashJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            var normalized = JsonSerializer.Serialize(document.RootElement);
            return Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(normalized)))
                .ToLowerInvariant();
        }

        public void SwitchTenant(Guid tenantId) => _tenantId = tenantId;

        public async Task AddFailedVerificationAsync()
        {
            var template = new AwardVerificationChecklistTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Name = "Standard award verification",
                IsActive = true
            };
            var item = new AwardVerificationChecklistItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TemplateId = template.Id,
                ItemText = "Tax clearance",
                IsRequired = true,
                IsActive = true
            };
            var verification = new TenderAwardVerification
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                TemplateId = template.Id,
                Status = "Completed",
                StartedDate = DateTime.UtcNow.AddMinutes(-20),
                CompletedDate = DateTime.UtcNow,
                CompletedById = Guid.NewGuid()
            };
            var bidder = new TenderAwardVerificationBidder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                VerificationId = verification.Id,
                TenderBidId = Bid.Id,
                BusinessPartnerId = Partner.Id,
                Status = "Failed",
                VerifiedDate = DateTime.UtcNow
            };
            var result = new TenderAwardVerificationItemResult
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BidderId = bidder.Id,
                ChecklistItemId = item.Id,
                IsVerified = true,
                Status = "Failed",
                VerifiedDate = DateTime.UtcNow
            };
            Context.AddRange(template, item, verification, bidder, result);
            await Context.SaveChangesAsync();
        }

        public async Task<TenderEvaluation> AddDraftEvaluationAsync()
        {
            var evaluator = new TenderEvaluator
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                UserId = Guid.NewGuid(),
                Role = "Evaluator",
                Status = "Assigned"
            };
            var draft = new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidId = Bid.Id,
                TenderEvaluatorId = evaluator.Id,
                TenderEvaluator = evaluator,
                TenderBid = Bid,
                EvaluationDate = DateTime.UtcNow,
                Status = "Draft",
                TotalScore = 75m,
                IsRecommended = false
            };
            Context.AddRange(evaluator, draft);
            await Context.SaveChangesAsync();
            return draft;
        }

        public async Task AddLockedScoreAttemptAsync(params TenderEvaluation[] evaluations)
        {
            if (evaluations.Length == 0)
                evaluations = [Evaluation];
            var committee = new ProcurementEvaluationCommitteeControl
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = Tender.Id,
                Version = 1,
                SourceReference = Tender.TenderNumber,
                Purpose = "Evaluate tender bids.",
                Status = ProcurementEvaluationCommitteeControlStatus.Active,
                CommitteeTemplateId = Guid.NewGuid(),
                CommitteeCode = "TDC_EVALUATION",
                CommitteeName = "Tender Evaluation Committee",
                RequiredQuorum = 1,
                PolicySetId = Guid.NewGuid(),
                PolicyCode = "TDC-POLICY",
                PolicyVersion = 1,
                MethodRuleId = Guid.NewGuid(),
                MethodRuleCode = "METHOD-NCT",
                EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
                ActivatedAtUtc = DateTime.UtcNow.AddDays(-1),
                ActivatedByUserId = Guid.NewGuid(),
                ActivationEvidenceReference = "evidence://committee-activation",
                CompositionSnapshotJson = "{}",
                CompositionIntegrityHash = new string('c', 64),
                CreationIdempotencyKey = $"committee-{Guid.NewGuid():N}",
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            var scores = evaluations.Select(evaluation =>
                new ProcurementEvaluationScoreSheet
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    CommitteeControlId = committee.Id,
                    CommitteeControl = committee,
                    MeetingId = Guid.NewGuid(),
                    AppointmentId = Guid.NewGuid(),
                    Phase = ProcurementEvaluationPhase.Combined,
                    ScoreSubjectType = "TenderEvaluation",
                    ScoreSubjectId = evaluation.Id,
                    Attempt = 1,
                    Status = ProcurementEvaluationScoreSheetStatus.Locked,
                    SubmittedAtUtc = evaluation.SubmittedDate ?? evaluation.EvaluationDate,
                    SubmittedByUserId = evaluation.TenderEvaluator.UserId,
                    SubmittedByName = "Assigned Tender Evaluator",
                    ScoreSnapshotJson = "{}",
                    SignatureReference = "signature://evaluation",
                    EvidenceReference = "evidence://evaluation",
                    IntegrityHash = new string('s', 64),
                    IdempotencyKey = $"score-{Guid.NewGuid():N}",
                    RowVersion = Guid.NewGuid().ToByteArray()
                })
                .ToList();
            Context.Add(committee);
            Context.AddRange(scores);
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
