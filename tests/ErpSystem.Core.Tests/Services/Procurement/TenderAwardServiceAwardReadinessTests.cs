using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderAwardServiceAwardReadinessTests
{
    [Fact]
    public async Task LegacyAwardCannotMutateWhenSelectedBidContradictsReadinessRecommendation()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderNumber = "LEGACY-001",
            Title = "Legacy tender"
        };
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            BusinessPartnerId = Guid.NewGuid(),
            BidNumber = "BID-001",
            TotalBidAmount = 150m
        };
        var awardRepository = new Mock<ITenderAwardRepository>();
        var tenderRepository = new Mock<ITenderRepository>();
        var bidRepository = new Mock<ITenderBidRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserProvider>();
        var tenderControl = new Mock<IProcurementTenderControlService>();
        var exceptionalControl =
            new Mock<IProcurementExceptionalSourcingControlService>();
        var readiness = new Mock<IProcurementAwardReadinessService>();
        var blockedDecision = new ProcurementAwardReadinessDto
        {
            Id = Guid.NewGuid(),
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = tender.Id,
            Status = ProcurementAwardReadinessDecisionStatus.Blocked,
            BlockedReasons =
                ["The selected bid contradicts the retained recommendation."]
        };

        currentUser.SetupGet(provider => provider.TenantId).Returns(tenantId);
        currentUser.SetupGet(provider => provider.UserId).Returns(userId);
        tenderControl.Setup(service => service.IsNctOrIctAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        exceptionalControl.Setup(service => service.IsExceptionalAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        tenderRepository.Setup(repository => repository.GetByIdAsync(tender.Id))
            .ReturnsAsync(tender);
        bidRepository.Setup(repository => repository.GetByIdAsync(bid.Id))
            .ReturnsAsync(bid);
        awardRepository.Setup(repository => repository.GetByTenderIdAsync(tender.Id))
            .ReturnsAsync((TenderAward?)null);
        readiness.Setup(service => service.EnsureAwardReadyAsync(
                ProcurementAwardReadinessSourceType.Tender,
                tender.Id,
                It.Is<EvaluateProcurementAwardReadinessRequest>(request =>
                    request.IdempotencyKey.StartsWith("award-gate:1:") &&
                    request.ExpectedRecommendedSubjectIds.SequenceEqual(new[] { bid.Id }) &&
                    request.ExpectedBusinessPartnerIds.SequenceEqual(
                        new[] { bid.BusinessPartnerId }) &&
                    request.ExpectedSourceIntegrityHash == null),
                "award-correlation",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAwardReadinessBlockedException(
                "AWARD_READINESS_RECOMMENDATION_MISMATCH",
                "The selected bid does not match the retained recommendation.",
                blockedDecision));

        var service = new TenderAwardService(
            awardRepository.Object,
            tenderRepository.Object,
            bidRepository.Object,
            new Mock<ITenderBidItemRepository>().Object,
            new Mock<ITenderEvaluationRepository>().Object,
            new Mock<ITenderEvaluatorRepository>().Object,
            new Mock<ITenderNotificationService>().Object,
            new Mock<IPurchaseOrderRepository>().Object,
            new Mock<IPurchaseOrderItemRepository>().Object,
            new Mock<ITenderNegotiationRepository>().Object,
            unitOfWork.Object,
            currentUser.Object,
            tenderControl.Object,
            exceptionalControl.Object,
            readiness.Object,
            new Mock<IProcurementPurchaseOrderSourceService>().Object,
            new Mock<IProcurementPurchaseOrderSodService>().Object,
            new Mock<ILogger<TenderAwardService>>().Object);

        var action = () => service.CreateAwardAsync(
            tender.Id,
            new CreateAwardDto
            {
                TenderId = tender.Id,
                TenderBidId = bid.Id,
                AwardedAmount = 150m,
                Currency = "GHS"
            },
            "award-correlation",
            default);

        await action.Should().ThrowAsync<ProcurementAwardReadinessBlockedException>()
            .Where(exception => exception.Decision == blockedDecision);
        awardRepository.Verify(
            repository => repository.CreateAsync(It.IsAny<TenderAward>()),
            Times.Never);
        tenderRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<Tender>()),
            Times.Never);
        bidRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<TenderBid>()),
            Times.Never);
        unitOfWork.Verify(
            work => work.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
        readiness.Verify(service => service.EnsureAwardReadyAsync(
            ProcurementAwardReadinessSourceType.Tender,
            tender.Id,
            It.IsAny<EvaluateProcurementAwardReadinessRequest>(),
            "award-correlation",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyAwardRejectsBidFromAnotherTenderBeforeReadinessEvaluation()
    {
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenderNumber = "LEGACY-002",
            Title = "Legacy tender"
        };
        var foreignBid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenderId = Guid.NewGuid(),
            BidNumber = "FOREIGN-BID"
        };
        var tenderRepository = new Mock<ITenderRepository>();
        var bidRepository = new Mock<ITenderBidRepository>();
        var tenderControl = new Mock<IProcurementTenderControlService>();
        var exceptionalControl =
            new Mock<IProcurementExceptionalSourcingControlService>();
        var readiness = new Mock<IProcurementAwardReadinessService>();

        tenderControl.Setup(service => service.IsNctOrIctAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        exceptionalControl.Setup(service => service.IsExceptionalAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        tenderRepository.Setup(repository => repository.GetByIdAsync(tender.Id))
            .ReturnsAsync(tender);
        bidRepository.Setup(repository => repository.GetByIdAsync(foreignBid.Id))
            .ReturnsAsync(foreignBid);

        var service = new TenderAwardService(
            new Mock<ITenderAwardRepository>().Object,
            tenderRepository.Object,
            bidRepository.Object,
            new Mock<ITenderBidItemRepository>().Object,
            new Mock<ITenderEvaluationRepository>().Object,
            new Mock<ITenderEvaluatorRepository>().Object,
            new Mock<ITenderNotificationService>().Object,
            new Mock<IPurchaseOrderRepository>().Object,
            new Mock<IPurchaseOrderItemRepository>().Object,
            new Mock<ITenderNegotiationRepository>().Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<ICurrentUserProvider>().Object,
            tenderControl.Object,
            exceptionalControl.Object,
            readiness.Object,
            new Mock<IProcurementPurchaseOrderSourceService>().Object,
            new Mock<IProcurementPurchaseOrderSodService>().Object,
            new Mock<ILogger<TenderAwardService>>().Object);

        var action = () => service.CreateAwardAsync(
            tender.Id,
            new CreateAwardDto
            {
                TenderId = tender.Id,
                TenderBidId = foreignBid.Id,
                AwardedAmount = 100m
            },
            "foreign-bid",
            default);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong*");
        readiness.VerifyNoOtherCalls();
    }
}
