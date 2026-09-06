using System.Collections;
using System.Linq.Expressions;
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
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderEvaluationServiceCommitteeTests
{
    [Fact]
    public async Task CommitteeProjectionAllowsFirstDraftWithoutStandaloneAssignment()
    {
        var fixture = new Fixture();
        fixture.Evaluators.Setup(item => item.GetByUserIdAsync(fixture.UserId)).ReturnsAsync([]);
        fixture.Committee.Setup(item => item.EnsureTenderEvaluatorAsync(fixture.TenderId,
                It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(fixture.Evaluator);
        var draft = await fixture.Service.CreateEvaluationAsync(new CreateEvaluationDto
            { TenderBidId = fixture.Bids[0].Id, TechnicalScore = 90 });
        draft.Status.Should().Be("Draft");
        fixture.Evaluations.Verify(item => item.CreateAsync(It.Is<TenderEvaluation>(evaluation =>
            evaluation.TenderEvaluatorId == fixture.Evaluator.Id)), Times.Once);
        fixture.Bids[0].Status.Should().Be("UnderEvaluation");
        fixture.Evaluators.Verify(item => item.GetByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("submit")]
    public async Task StandaloneAssignmentCannotBypassCommitteeMembership(string stage)
    {
        var fixture = new Fixture();
        fixture.Committee.Setup(item => item.EnsureTenderEvaluatorAsync(fixture.TenderId,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementEvaluationCommitteeAuthorizationException("Not appointed."));
        var evaluation = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(item => item.GetByIdAsync(evaluation.Id)).ReturnsAsync(evaluation);
        Func<Task> action = stage switch
        {
            "create" => () => fixture.Service.CreateEvaluationAsync(new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }),
            "update" => () => fixture.Service.UpdateEvaluationAsync(evaluation.Id, new UpdateEvaluationDto()),
            _ => () => fixture.Service.SubmitEvaluationAsync(evaluation.Id, new SubmitEvaluationDto { ConfirmSubmission = true })
        };
        await action.Should().ThrowAsync<ProcurementEvaluationCommitteeAuthorizationException>();
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportWaitsForAllCommitteeMembersEvenWhenTheirProjectionIsMissing(bool projectOtherMembers)
    {
        var fixture = new Fixture();
        var second = new TenderEvaluator { Id = Guid.NewGuid(), TenantId = fixture.TenantId, UserId = Guid.NewGuid() };
        var third = new TenderEvaluator { Id = Guid.NewGuid(), TenantId = fixture.TenantId, UserId = Guid.NewGuid() };
        fixture.Committee.Setup(item => item.GetTenderScoringUserIdsAsync(fixture.TenderId,
                It.IsAny<CancellationToken>())).ReturnsAsync(new[] { fixture.UserId, second.UserId, third.UserId });
        fixture.Evaluators.Setup(item => item.GetByTenderIdAsync(fixture.TenderId))
            .ReturnsAsync(projectOtherMembers ? [fixture.Evaluator, second, third] : [fixture.Evaluator]);
        fixture.Evaluations.Setup(item => item.GetByBidIdAsync(fixture.Bids[0].Id))
            .ReturnsAsync([fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Submitted")]);
        fixture.Evaluations.Setup(item => item.GetByBidIdAsync(fixture.Bids[1].Id)).ReturnsAsync([]);
        var report = await fixture.Service.GetTenderEvaluationReportAsync(fixture.TenderId);
        report.EvaluatedBids.Should().Be(0);
        report.BidEvaluations.Single(item => item.BidId == fixture.Bids[0].Id).BidStatus.Should().NotBe("Evaluated");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("submit")]
    public async Task MismatchBlocksDraftAndFinalScoringBeforeAnyWrite(string stage)
    {
        var fixture = new Fixture();
        var template = new EvaluationTemplate { Id = Guid.NewGuid(), TenantId = fixture.TenantId, ScoringMethod = "QCBS" };
        fixture.Tenders.Setup(item => item.GetByIdAsync(fixture.TenderId)).ReturnsAsync(new Tender
            { Id = fixture.TenderId, TenantId = fixture.TenantId, EvaluationTemplateId = template.Id, UseQCBSEvaluation = false });
        var templates = new Mock<IGenericRepository<EvaluationTemplate>>();
        templates.Setup(item => item.GetByIdAsync(template.Id)).ReturnsAsync(template);
        fixture.UnitOfWork.Setup(item => item.Repository<EvaluationTemplate>()).Returns(templates.Object);
        var evaluation = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(item => item.GetByIdAsync(evaluation.Id)).ReturnsAsync(evaluation);
        Func<Task> action = stage switch
        {
            "create" => () => fixture.Service.CreateEvaluationAsync(new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }),
            "update" => () => fixture.Service.UpdateEvaluationAsync(evaluation.Id, new UpdateEvaluationDto { TechnicalScore = 90 }),
            _ => () => fixture.Service.SubmitEvaluationAsync(evaluation.Id, new SubmitEvaluationDto { ConfirmSubmission = true })
        };
        (await action.Should().ThrowAsync<TenderEvaluationConfigurationException>())
            .Which.Code.Should().Be("TENDER_EVALUATION_METHOD_MISMATCH");
        fixture.Bids[0].Status.Should().Be("Opened");
        evaluation.Status.Should().Be("Draft");
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignedEvaluatorCanCreateDraftBeforeCommitteeIsActive()
    {
        var fixture = new Fixture();
        fixture.Committee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                It.IsAny<ProcurementEvaluationSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<ProcurementEvaluationPhase>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
            {
                Allowed = false,
                BlockedReasons = ["No active evaluation committee control exists."]
            });

        var result = await fixture.Service.CreateEvaluationAsync(
            new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id });

        result.Status.Should().Be("Draft");
        fixture.Evaluations.Verify(
            repository => repository.CreateAsync(It.Is<TenderEvaluation>(item =>
                item.TenderBidId == fixture.Bids[0].Id &&
                item.TenderEvaluatorId == fixture.Evaluator.Id &&
                item.Status == "Draft")),
            Times.Once);
        fixture.Committee.Verify(service => service.EnsureScoreSubjectEligibleAsync(
            It.IsAny<ProcurementEvaluationSourceType>(),
            It.IsAny<Guid>(),
            It.IsAny<ProcurementEvaluationPhase>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignedEvaluatorCanUpdateDraftBeforeCommitteeIsActive()
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "UnderEvaluation";
        var evaluation = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(repository => repository.GetByIdAsync(evaluation.Id))
            .ReturnsAsync(evaluation);
        fixture.Committee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                It.IsAny<ProcurementEvaluationSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<ProcurementEvaluationPhase>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
            {
                Allowed = false,
                BlockedReasons = ["No active evaluation committee control exists."]
            });

        var result = await fixture.Service.UpdateEvaluationAsync(
            evaluation.Id,
            new UpdateEvaluationDto { QualityScore = 82m, OverallComments = "Draft review" });

        result.Status.Should().Be("Draft");
        result.QualityScore.Should().Be(82m);
        result.OverallComments.Should().Be("Draft review");
        fixture.Evaluations.Verify(
            repository => repository.UpdateAsync(It.Is<TenderEvaluation>(item =>
                item.Id == evaluation.Id && item.Status == "Draft")),
            Times.Once);
        fixture.Committee.Verify(service => service.EnsureScoreSubjectEligibleAsync(
            It.IsAny<ProcurementEvaluationSourceType>(),
            It.IsAny<Guid>(),
            It.IsAny<ProcurementEvaluationPhase>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignedEvaluatorCanDeleteDraftBeforeCommitteeIsActive()
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "UnderEvaluation";
        var evaluation = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(repository => repository.GetByIdAsync(evaluation.Id))
            .ReturnsAsync(evaluation);

        await fixture.Service.DeleteEvaluationAsync(evaluation.Id);

        fixture.Evaluations.Verify(
            repository => repository.DeleteAsync(evaluation.Id), Times.Once);
        fixture.Committee.Verify(service => service.EnsureScoreSubjectEligibleAsync(
            It.IsAny<ProcurementEvaluationSourceType>(),
            It.IsAny<Guid>(),
            It.IsAny<ProcurementEvaluationPhase>(),
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitStillRequiresActiveCommitteeScorerControl()
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "UnderEvaluation";
        var evaluation = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(repository => repository.GetByIdAsync(evaluation.Id))
            .ReturnsAsync(evaluation);
        fixture.Committee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender,
                fixture.TenderId,
                ProcurementEvaluationPhase.Combined,
                "TenderEvaluation",
                fixture.Bids[0].Id,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
            {
                Allowed = false,
                BlockedReasons = ["No active evaluation committee control exists."]
            });

        await fixture.Service.Invoking(service => service.SubmitEvaluationAsync(
                evaluation.Id,
                new SubmitEvaluationDto
                {
                    ConfirmSubmission = true,
                    SignatureReference = "SIG-001",
                    EvidenceReference = "DMS-001",
                    IdempotencyKey = "submit-001"
                }))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_SCORER_INELIGIBLE");

        fixture.Evaluations.Verify(
            repository => repository.UpdateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
        fixture.UnitOfWork.Verify(
            unitOfWork => unitOfWork.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

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
            Tenders.Setup(repository => repository.GetByIdAsync(TenderId))
                .ReturnsAsync(new Tender { Id = TenderId, TenantId = TenantId });
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
            TenderFees.Setup(repository => repository.GetQueryable(
                    It.IsAny<Expression<Func<TenderFee, bool>>>()))
                .Returns((Expression<Func<TenderFee, bool>> predicate) =>
                    Array.Empty<TenderFee>().Where(predicate.Compile()).AsAsyncQueryable());
            UnitOfWork.Setup(unitOfWork => unitOfWork.Repository<TenderFee>())
                .Returns(TenderFees.Object);
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
        public Mock<IGenericRepository<TenderFee>> TenderFees { get; } = new();

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

internal static class TenderEvaluationAsyncQueryableExtensions
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source) =>
        new TestAsyncEnumerable<T>(source);

    private sealed class TestAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) =>
            new TestAsyncEnumerable<TEntity>(expression);

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(expression);

        public object? Execute(Expression expression) => inner.Execute(expression);

        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

        public TResult ExecuteAsync<TResult>(
            Expression expression,
            CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments().Single();
            var result = typeof(IQueryProvider)
                .GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression]);

            return (TResult)typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result])!;
        }
    }

    private sealed class TestAsyncEnumerable<T> :
        EnumerableQuery<T>,
        IAsyncEnumerable<T>,
        IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable)
        {
        }

        public TestAsyncEnumerable(Expression expression) : base(expression)
        {
        }

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;

        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
