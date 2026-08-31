using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderEvaluationServiceCommitteeTests
{
    [Fact]
    public async Task CreateRejectsSecondDraftForSameBidAndEvaluator()
    {
        var fixture = new Fixture();
        var existing = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(repository => repository.GetByBidIdAsync(fixture.Bids[0].Id))
            .ReturnsAsync([existing]);

        await fixture.Service.Invoking(service => service.CreateEvaluationAsync(
                new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_DRAFT_ALREADY_EXISTS");
        fixture.Evaluations.Verify(
            repository => repository.CreateAsync(It.IsAny<TenderEvaluation>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsolidationRequiresLockedSheetCoverageForEveryBidAndEvaluator()
    {
        var fixture = new Fixture();
        var first = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Submitted");
        var second = fixture.Evaluation(fixture.Bids[1], fixture.Evaluator, "Submitted");
        fixture.Evaluations.Setup(repository => repository.GetByBidIdAsync(fixture.Bids[0].Id))
            .ReturnsAsync([first]);
        fixture.Evaluations.Setup(repository => repository.GetByBidIdAsync(fixture.Bids[1].Id))
            .ReturnsAsync([second]);
        fixture.Committee.Setup(service => service.GetAsync(
                ProcurementEvaluationSourceType.Tender,
                fixture.TenderId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.CommitteeWithSheet(first));

        await fixture.Service.Invoking(service =>
                service.CalculateQCBSScoresAsync(fixture.TenderId))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_SCORE_PROJECTION_INCOMPLETE");
    }

    [Fact]
    public async Task CreateEvaluationRequiresAnOpenedBid()
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Submitted";

        await fixture.Service.Invoking(service => service.CreateEvaluationAsync(
                new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only an opened bid*");
        fixture.Evaluations.Verify(
            repository => repository.CreateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
    }

    [Fact]
    public async Task EvaluatorCannotUpdateAnotherEvaluatorsDraft()
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "UnderEvaluation";
        var other = new TenderEvaluator
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = fixture.TenderId,
            UserId = Guid.NewGuid()
        };
        var evaluation = fixture.Evaluation(fixture.Bids[0], other, "Draft");
        fixture.Evaluations.Setup(repository => repository.GetByIdAsync(evaluation.Id))
            .ReturnsAsync(evaluation);

        await fixture.Service.Invoking(service => service.UpdateEvaluationAsync(
                evaluation.Id, new UpdateEvaluationDto()))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeAuthorizationException>();
        fixture.Evaluations.Verify(
            repository => repository.UpdateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            TenderId = Guid.NewGuid();
            Evaluator = new TenderEvaluator
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = TenderId,
                UserId = UserId,
                Status = "Accepted",
                User = new ApplicationUser { Id = UserId, UserName = "evaluator@tdc.test" }
            };
            Bids =
            [
                new TenderBid
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TenderId = TenderId,
                    BidNumber = "BID-001", Status = "Opened"
                },
                new TenderBid
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TenderId = TenderId,
                    BidNumber = "BID-002", Status = "Opened"
                }
            ];

            CurrentUser.SetupGet(item => item.UserId).Returns(UserId);
            CurrentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            BidsRepository.Setup(repository => repository.GetByIdAsync(Bids[0].Id))
                .ReturnsAsync(Bids[0]);
            BidsRepository.Setup(repository => repository.GetByTenderIdAsync(TenderId))
                .ReturnsAsync(Bids);
            Evaluators.Setup(repository => repository.GetByUserIdAsync(UserId))
                .ReturnsAsync([Evaluator]);
            TenderControl.Setup(service => service.IsControlledTenderMethodAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            ExceptionalControl.Setup(service => service.IsExceptionalAsync(
                    It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            Committee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                    ProcurementEvaluationSourceType.Tender,
                    TenderId,
                    ProcurementEvaluationPhase.Combined,
                    "TenderEvaluation",
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
                {
                    Allowed = true,
                    SourceType = ProcurementEvaluationSourceType.Tender,
                    SourceId = TenderId,
                    Phase = ProcurementEvaluationPhase.Combined,
                    ActorUserId = UserId,
                    AppointmentId = Guid.NewGuid(),
                    MeetingId = Guid.NewGuid(),
                    AuthorizedAttempt = 1
                });

            Service = new TenderEvaluationService(
                Evaluations.Object,
                BidsRepository.Object,
                Evaluators.Object,
                Tenders.Object,
                Notifications.Object,
                Criteria.Object,
                UnitOfWork.Object,
                CurrentUser.Object,
                Events.Object,
                TenderControl.Object,
                ExceptionalControl.Object,
                Committee.Object,
                NullLogger<TenderEvaluationService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid TenderId { get; }
        public TenderEvaluator Evaluator { get; }
        public List<TenderBid> Bids { get; }
        public TenderEvaluationService Service { get; }
        public Mock<ITenderEvaluationRepository> Evaluations { get; } = new();
        public Mock<ITenderBidRepository> BidsRepository { get; } = new();
        public Mock<ITenderEvaluatorRepository> Evaluators { get; } = new();
        public Mock<ITenderRepository> Tenders { get; } = new();
        public Mock<ITenderNotificationService> Notifications { get; } = new();
        public Mock<IEvaluationCriterionRepository> Criteria { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IAppEventBus> Events { get; } = new();
        public Mock<IProcurementTenderControlService> TenderControl { get; } = new();
        public Mock<IProcurementExceptionalSourcingControlService> ExceptionalControl { get; } = new();
        public Mock<IProcurementEvaluationCommitteeControlService> Committee { get; } = new();

        public TenderEvaluation Evaluation(
            TenderBid bid,
            TenderEvaluator evaluator,
            string status)
        {
            var submittedAt = DateTime.UtcNow;
            return new TenderEvaluation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidId = bid.Id,
                TenderEvaluatorId = evaluator.Id,
                TenderBid = bid,
                TenderEvaluator = evaluator,
                Status = status,
                EvaluationDate = submittedAt,
                SubmittedDate = status == "Submitted" ? submittedAt : null,
                PriceScore = 80m,
                QualityScore = 75m,
                TotalScore = 77.5m,
                CreatedAt = submittedAt
            };
        }

        public ProcurementEvaluationCommitteeDto CommitteeWithSheet(
            TenderEvaluation evaluation)
        {
            var appointmentId = Guid.NewGuid();
            var submittedAt = evaluation.SubmittedDate!.Value;
            return new ProcurementEvaluationCommitteeDto
            {
                Id = Guid.NewGuid(),
                SourceType = ProcurementEvaluationSourceType.Tender,
                SourceId = TenderId,
                Status = ProcurementEvaluationCommitteeControlStatus.Active,
                CompositionReady = true,
                QuorumMet = true,
                ScoreSheets =
                [
                    new ProcurementEvaluationScoreSheetDto
                    {
                        Id = Guid.NewGuid(),
                        AppointmentId = appointmentId,
                        MeetingId = Guid.NewGuid(),
                        Phase = ProcurementEvaluationPhase.Combined,
                        ScoreSubjectType = "TenderEvaluation",
                        ScoreSubjectId = evaluation.TenderBidId,
                        Attempt = 1,
                        Status = ProcurementEvaluationScoreSheetStatus.Locked,
                        ScoreSnapshotJson = Snapshot(evaluation, submittedAt),
                        SubmittedAtUtc = submittedAt
                    }
                ]
            };
        }

        private static string Snapshot(TenderEvaluation evaluation, DateTime submittedAtUtc) =>
            JsonSerializer.Serialize(new
            {
                schemaVersion = "tdc.legacy-tender-score-sheet.v1",
                evaluationId = evaluation.Id,
                evaluation.TenderBidId,
                evaluation.TenderEvaluatorId,
                status = "Submitted",
                submittedAtUtc,
                evaluatorUserId = evaluation.TenderEvaluator.UserId,
                evaluation.PriceScore,
                evaluation.QualityScore,
                evaluation.DeliveryScore,
                evaluation.ExperienceScore,
                evaluation.TechnicalScore,
                evaluation.ComplianceScore,
                evaluation.TotalScore,
                evaluation.EvaluationCriteriaJson,
                evaluation.TechnicalComments,
                evaluation.CommercialComments,
                evaluation.OverallComments,
                evaluation.IsRecommended,
                evaluation.Recommendation
            });
    }
}
