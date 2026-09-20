using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderAwardRecommendationProjectionTests
{
    [Fact]
    public async Task ReplacementUsesThreeCurrentVotersWithoutDeletingFourHistoricalProjections()
    {
        var fixture = new PreviewFixture();
        var original = fixture.AddEvaluation(0, 30m);
        fixture.AddEvaluation(1, 90m);
        fixture.AddEvaluation(2, 60m);
        var replacement = fixture.AddEvaluation(0, 90m, offsetMinutes: 1);

        var result = await fixture.Service.GenerateAwardRecommendationAsync(fixture.Tender.Id);

        var bid = result.BidRecommendations.Should().ContainSingle().Subject;
        bid.EvaluationCount.Should().Be(3);
        bid.RecommendationCount.Should().Be(2);
        bid.TotalEvaluators.Should().Be(3);
        bid.AverageScore.Should().Be(80m);
        result.RecommendedScore.Should().Be(80m);
        result.EvaluatedBids.Should().Be(1);
        fixture.Evaluations.Should().HaveCount(4).And.Contain(original).And.Contain(replacement);
        original.TotalScore.Should().Be(30m);
        original.Status.Should().Be("Submitted");
        fixture.Readiness.Invocations.Should().BeEmpty("preview does not introduce a readiness prerequisite");
        fixture.UnitOfWork.Invocations.Should().BeEmpty("preview does not mutate retained history");
    }

    [Fact]
    public async Task NewerDraftSuppressesTheSameVotersPreviouslySubmittedScore()
    {
        var fixture = new PreviewFixture();
        fixture.AddEvaluation(0, 100m);
        fixture.AddEvaluation(1, 80m);
        fixture.AddEvaluation(2, 60m);
        fixture.AddEvaluation(0, 0m, "Draft", 1);

        var result = await fixture.Service.GenerateAwardRecommendationAsync(fixture.Tender.Id);

        var bid = result.BidRecommendations.Should().ContainSingle().Subject;
        bid.EvaluationCount.Should().Be(2);
        bid.TotalEvaluators.Should().Be(3);
        bid.AverageScore.Should().Be(70m);
        bid.RecommendationCount.Should().Be(1);
        fixture.Evaluations.Should().HaveCount(4);
    }

    [Fact]
    public async Task OnlyNewDraftMeansNoSubmittedRecommendationDespiteHistoricalSubmission()
    {
        var fixture = new PreviewFixture();
        fixture.AddEvaluation(0, 100m);
        fixture.AddEvaluation(0, 0m, "Draft", 1);

        var result = await fixture.Service.GenerateAwardRecommendationAsync(fixture.Tender.Id);

        result.BidRecommendations.Should().BeEmpty();
        result.RecommendedBidId.Should().BeNull();
        result.EvaluatedBids.Should().Be(0);
        fixture.Evaluations.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("other-bid")]
    [InlineData("deleted")]
    public async Task OutOfScopeProjectionCannotSuppressOrInflateCurrentBid(string scope)
    {
        var fixture = new PreviewFixture();
        fixture.AddEvaluation(0, 80m);
        var invalid = fixture.AddEvaluation(0, 100m, offsetMinutes: 1);
        if (scope == "foreign-tenant") invalid.TenantId = Guid.NewGuid();
        if (scope == "other-bid") invalid.TenderBidId = Guid.NewGuid();
        if (scope == "deleted") invalid.IsDeleted = true;

        var result = await fixture.Service.GenerateAwardRecommendationAsync(fixture.Tender.Id);

        var bid = result.BidRecommendations.Should().ContainSingle().Subject;
        bid.EvaluationCount.Should().Be(1);
        bid.AverageScore.Should().Be(80m);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("other-tender")]
    [InlineData("deleted")]
    public async Task OutOfScopeBidAndEvaluatorDoNotEnterPreviewTotals(string scope)
    {
        var fixture = new PreviewFixture();
        fixture.AddEvaluation(0, 80m);
        var invalidBid = new TenderBid
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id, Status = "Evaluated"
        };
        var invalidEvaluator = new TenderEvaluator
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id, UserId = Guid.NewGuid()
        };
        if (scope == "foreign-tenant")
        {
            invalidBid.TenantId = Guid.NewGuid();
            invalidEvaluator.TenantId = Guid.NewGuid();
        }
        if (scope == "other-tender")
        {
            invalidBid.TenderId = Guid.NewGuid();
            invalidEvaluator.TenderId = Guid.NewGuid();
        }
        if (scope == "deleted")
        {
            invalidBid.IsDeleted = true;
            invalidEvaluator.IsDeleted = true;
        }
        fixture.BidRows.Add(invalidBid);
        fixture.EvaluatorRows.Add(invalidEvaluator);

        var result = await fixture.Service.GenerateAwardRecommendationAsync(fixture.Tender.Id);

        result.TotalBids.Should().Be(1);
        result.BidRecommendations.Should().ContainSingle().Which.TotalEvaluators.Should().Be(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewRejectsForeignOrDeletedTenderBeforeReadingBids(bool deleted)
    {
        var fixture = new PreviewFixture();
        if (deleted) fixture.Tender.IsDeleted = true;
        else fixture.Tender.TenantId = Guid.NewGuid();

        await fixture.Service.Invoking(service => service.GenerateAwardRecommendationAsync(fixture.Tender.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        fixture.Bids.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void SharedSelectionKeepsDifferentBidsAndUsesDeterministicTiesWithoutMutatingHistory()
    {
        var fixture = new PreviewFixture();
        var first = fixture.AddEvaluation(0, 40m);
        first.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var laterCreated = fixture.AddEvaluation(0, 60m);
        laterCreated.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        laterCreated.CreatedAt = first.CreatedAt.AddMinutes(1);
        var higherId = fixture.AddEvaluation(0, 80m);
        higherId.Id = Guid.Parse("00000000-0000-0000-0000-000000000003");
        higherId.CreatedAt = laterCreated.CreatedAt;
        var differentBid = fixture.AddEvaluation(0, 90m);
        differentBid.TenderBidId = Guid.NewGuid();

        var current = ProcurementTenderEvaluationProjectionPolicy.SelectCurrent(fixture.Evaluations);
        var reversed = ProcurementTenderEvaluationProjectionPolicy.SelectCurrent(fixture.Evaluations.AsEnumerable().Reverse());

        current.Should().BeEquivalentTo(new[] { higherId, differentBid });
        reversed.Should().BeEquivalentTo(current);
        fixture.Evaluations.Should().HaveCount(4);
    }

    private sealed class PreviewFixture
    {
        private static readonly DateTime EvaluatedAt = new(2026, 9, 6, 1, 0, 0, DateTimeKind.Utc);

        public PreviewFixture()
        {
            Tender = new Tender
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TenderNumber = "TND-PREVIEW",
                Title = "Current evaluator preview", Status = "Evaluated"
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id,
                BidNumber = "BID-PREVIEW", BusinessPartnerId = Guid.NewGuid(),
                TotalBidAmount = 52000m, Status = "Evaluated"
            };
            BidRows.Add(Bid);
            for (var index = 0; index < 3; index++)
                EvaluatorRows.Add(new TenderEvaluator
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id,
                    UserId = Guid.NewGuid(), Status = "Completed"
                });
            var tenders = new Mock<ITenderRepository>();
            tenders.Setup(repository => repository.GetByIdAsync(Tender.Id)).ReturnsAsync(Tender);
            Bids.Setup(repository => repository.GetByTenderIdAsync(Tender.Id))
                .ReturnsAsync(() => BidRows);
            var evaluators = new Mock<ITenderEvaluatorRepository>();
            evaluators.Setup(repository => repository.GetByTenderIdAsync(Tender.Id))
                .ReturnsAsync(() => EvaluatorRows);
            var evaluations = new Mock<ITenderEvaluationRepository>(MockBehavior.Strict);
            evaluations.Setup(repository => repository.GetByBidIdAsync(Bid.Id))
                .ReturnsAsync(() => Evaluations);
            var currentUser = new Mock<ICurrentUserProvider>();
            currentUser.SetupGet(provider => provider.TenantId).Returns(TenantId);
            currentUser.SetupGet(provider => provider.UserId).Returns(Guid.NewGuid());
            Service = new TenderAwardService(
                Mock.Of<ITenderAwardRepository>(), tenders.Object, Bids.Object,
                Mock.Of<ITenderBidItemRepository>(), evaluations.Object, evaluators.Object,
                Mock.Of<ITenderNotificationService>(), Mock.Of<IPurchaseOrderRepository>(),
                Mock.Of<IPurchaseOrderItemRepository>(), Mock.Of<ITenderNegotiationRepository>(),
                UnitOfWork.Object, currentUser.Object, Mock.Of<IProcurementTenderControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                Mock.Of<IProcurementSourcingCaseService>(), Readiness.Object,
                Mock.Of<IProcurementPurchaseOrderSourceService>(),
                Mock.Of<IProcurementPurchaseOrderSodService>(), Mock.Of<ILogger<TenderAwardService>>());
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public List<TenderBid> BidRows { get; } = [];
        public List<TenderEvaluator> EvaluatorRows { get; } = [];
        public List<TenderEvaluation> Evaluations { get; } = [];
        public Mock<ITenderBidRepository> Bids { get; } = new();
        public Mock<IProcurementAwardReadinessService> Readiness { get; } = new(MockBehavior.Strict);
        public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Strict);
        public TenderAwardService Service { get; }

        public TenderEvaluation AddEvaluation(int voter, decimal score, string status = "Submitted", int offsetMinutes = 0)
        {
            var timestamp = EvaluatedAt.AddMinutes(offsetMinutes);
            var evaluation = new TenderEvaluation
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TenderBidId = Bid.Id,
                TenderEvaluatorId = EvaluatorRows[voter].Id, Status = status,
                TotalScore = score, IsRecommended = score >= 70m, CreatedAt = timestamp,
                EvaluationDate = timestamp, UpdatedAt = timestamp,
                SubmittedDate = status == "Submitted" ? timestamp : null
            };
            Evaluations.Add(evaluation);
            return evaluation;
        }
    }
}
