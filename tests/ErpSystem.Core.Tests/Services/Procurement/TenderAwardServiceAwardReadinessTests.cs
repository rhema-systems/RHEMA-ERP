using System.Data;
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
    public async Task AwardApprovalCannotMutateWhenSelectedBidContradictsReadinessRecommendation()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderNumber = "LEGACY-001",
            Title = "Legacy tender",
            Status = "Evaluated",
            SourcePurchaseRequisitionId = requisitionId,
            SourcingReleaseId = releaseId,
            SourcingCaseId = caseId
        };
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            BusinessPartnerId = Guid.NewGuid(),
            BidNumber = "BID-001",
            TotalBidAmount = 150m,
            Status = "Evaluated"
        };
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            TenderBidId = bid.Id,
            BusinessPartnerId = bid.BusinessPartnerId,
            AwardedAmount = 150m,
            OriginalBidAmount = 150m,
            Currency = "GHS",
            Status = "PendingApproval",
            CreatedById = Guid.NewGuid()
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
        var sourcingCases = new Mock<IProcurementSourcingCaseService>();
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
        tenderControl.Setup(service => service.IsControlledTenderMethodAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        exceptionalControl.Setup(service => service.IsExceptionalAsync(
                tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        sourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                requisitionId,
                releaseId,
                tender.Id,
                tender.TenderNumber,
                "award-correlation",
                ProcurementTenderSourceRecoveryBoundary.AwardApproval,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
            {
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
            });
        tenderRepository.Setup(repository => repository.GetByIdAsync(tender.Id))
            .ReturnsAsync(tender);
        bidRepository.Setup(repository => repository.GetByIdAsync(bid.Id))
            .ReturnsAsync(bid);
        awardRepository.Setup(repository => repository.GetByIdAsync(award.Id))
            .ReturnsAsync(award);
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
            sourcingCases.Object,
            readiness.Object,
            new Mock<IProcurementPurchaseOrderSourceService>().Object,
            new Mock<IProcurementPurchaseOrderSodService>().Object,
            new Mock<ILogger<TenderAwardService>>().Object);

        var action = () => service.ApproveAwardAsync(
            award.Id,
            new ApproveAwardDto(),
            "award-correlation",
            default);

        await action.Should().ThrowAsync<ProcurementAwardReadinessBlockedException>()
            .Where(exception => exception.Decision == blockedDecision);
        awardRepository.Verify(
            repository => repository.UpdateAsync(It.IsAny<TenderAward>()),
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
        sourcingCases.VerifyAll();
    }

    [Fact]
    public async Task LegacyAwardRejectsBidFromAnotherTenderBeforeReadinessEvaluation()
    {
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TenderNumber = "LEGACY-002",
            Title = "Legacy tender",
            Status = "Evaluated"
        };
        var foreignBid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tender.TenantId,
            TenderId = Guid.NewGuid(),
            BidNumber = "FOREIGN-BID",
            Status = "Evaluated"
        };
        var tenderRepository = new Mock<ITenderRepository>();
        var bidRepository = new Mock<ITenderBidRepository>();
        var tenderControl = new Mock<IProcurementTenderControlService>();
        var exceptionalControl =
            new Mock<IProcurementExceptionalSourcingControlService>();
        var readiness = new Mock<IProcurementAwardReadinessService>();
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(provider => provider.TenantId).Returns(tender.TenantId);

        tenderControl.Setup(service => service.IsControlledTenderMethodAsync(
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
            currentUser.Object,
            tenderControl.Object,
            exceptionalControl.Object,
            new Mock<IProcurementSourcingCaseService>().Object,
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

    [Fact]
    public async Task CreateAwardCreatesPendingRecommendationWithoutFinalizingTenderOrBids()
    {
        var fixture = new AwardFixture();

        var result = await fixture.Service.CreateAwardAsync(
            fixture.Tender.Id,
            new CreateAwardDto
            {
                TenderId = fixture.Tender.Id,
                TenderBidId = fixture.Bid.Id,
                AwardedAmount = fixture.Bid.TotalBidAmount,
                Currency = "GHS",
                AwardJustification = "Highest retained evaluated recommendation"
            });

        result.Status.Should().Be("PendingApproval");
        result.CreatedById.Should().Be(fixture.UserId);
        result.AwardedById.Should().BeNull();
        fixture.Tenders.Verify(repository => repository.UpdateAsync(
            It.IsAny<Tender>()), Times.Never);
        fixture.Bids.Verify(repository => repository.UpdateAsync(
            It.IsAny<TenderBid>()), Times.Never);
        fixture.Readiness.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AwardRecommendationRepairsExactPrTenderLineageWithAdministerPermission()
    {
        var fixture = new AwardFixture();
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        fixture.Tender.SourcePurchaseRequisitionId = requisitionId;
        fixture.Tender.SourcingReleaseId = releaseId;
        fixture.SourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                requisitionId,
                releaseId,
                fixture.Tender.Id,
                fixture.Tender.TenderNumber,
                "award-lineage",
                ProcurementTenderSourceRecoveryBoundary.AwardAdministration,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
            {
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
            });

        var result = await fixture.Service.CreateAwardAsync(
            fixture.Tender.Id,
            new CreateAwardDto
            {
                TenderId = fixture.Tender.Id,
                TenderBidId = fixture.Bid.Id,
                AwardedAmount = fixture.Bid.TotalBidAmount,
                Currency = "GHS",
                AwardJustification = "Retained evaluated recommendation"
            },
            "award-lineage");

        result.Status.Should().Be("PendingApproval");
        fixture.Tender.SourcingReleaseId.Should().Be(releaseId);
        fixture.Tender.SourcingCaseId.Should().Be(caseId);
        fixture.Tenders.Verify(repository => repository.UpdateAsync(fixture.Tender), Times.Once);
        fixture.SourcingCases.VerifyAll();
    }

    [Fact]
    public async Task AwardRecommendationRejectsAConflictingRetainedSourcingCase()
    {
        var fixture = new AwardFixture();
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        fixture.Tender.SourcePurchaseRequisitionId = requisitionId;
        fixture.Tender.SourcingReleaseId = releaseId;
        fixture.Tender.SourcingCaseId = Guid.NewGuid();
        fixture.SourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                requisitionId,
                releaseId,
                fixture.Tender.Id,
                fixture.Tender.TenderNumber,
                It.IsAny<string>(),
                ProcurementTenderSourceRecoveryBoundary.AwardAdministration,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
            {
                SourcingReleaseId = releaseId,
                SourcingCaseId = Guid.NewGuid(),
                SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
            });

        await fixture.Service.Invoking(service => service.CreateAwardAsync(
                fixture.Tender.Id,
                new CreateAwardDto
                {
                    TenderId = fixture.Tender.Id,
                    TenderBidId = fixture.Bid.Id,
                    AwardedAmount = fixture.Bid.TotalBidAmount
                }))
            .Should().ThrowAsync<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "SOURCING_CASE_LINEAGE_MISMATCH");
        fixture.Awards.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderAward>()), Times.Never);
    }

    [Fact]
    public async Task AwardToPurchaseOrderRepairsLineageBeforeApprovedSourceResolution()
    {
        var fixture = new AwardFixture();
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        fixture.Tender.SourcePurchaseRequisitionId = requisitionId;
        fixture.Tender.SourcingReleaseId = releaseId;
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id,
            TenderBidId = fixture.Bid.Id,
            BusinessPartnerId = fixture.Bid.BusinessPartnerId,
            AwardedAmount = fixture.Bid.TotalBidAmount,
            Currency = "GHS",
            Status = "Awarded"
        };
        fixture.Awards.Setup(repository => repository.GetByIdAsync(award.Id))
            .ReturnsAsync(award);
        fixture.SourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                requisitionId,
                releaseId,
                fixture.Tender.Id,
                fixture.Tender.TenderNumber,
                It.IsAny<string>(),
                ProcurementTenderSourceRecoveryBoundary.PurchaseOrderCreation,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
            {
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
            });
        fixture.PurchaseOrderSources.Setup(service => service.ResolveAsync(
                ProcurementPurchaseOrderSourceType.TenderAward,
                award.Id,
                award.BusinessPartnerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderSourceValidationException(
                "PO_TEST_STOP",
                "Stop after lineage recovery."));

        await fixture.Service.Invoking(service => service.CreatePurchaseOrderFromAwardAsync(
                new CreatePurchaseOrderFromAwardDto
                {
                    TenderAwardId = award.Id,
                    RequiredDate = DateTime.UtcNow.AddDays(14)
                }))
            .Should().ThrowAsync<ProcurementPurchaseOrderSourceValidationException>()
            .Where(exception => exception.Code == "PO_TEST_STOP");

        fixture.Tender.SourcingCaseId.Should().Be(caseId);
        fixture.SourcingCases.VerifyAll();
        fixture.PurchaseOrderSources.VerifyAll();
    }

    [Fact]
    public async Task RecommendationCreatorCannotApproveOwnAward()
    {
        var fixture = new AwardFixture();
        var pendingAward = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id,
            TenderBidId = fixture.Bid.Id,
            BusinessPartnerId = fixture.Bid.BusinessPartnerId,
            Status = "PendingApproval",
            CreatedById = fixture.UserId
        };
        fixture.Awards.Setup(repository => repository.GetByIdAsync(pendingAward.Id))
            .ReturnsAsync(pendingAward);

        await fixture.Service.Invoking(service => service.ApproveAwardAsync(
                pendingAward.Id, new ApproveAwardDto()))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*creator cannot approve*");
        fixture.Readiness.VerifyNoOtherCalls();
        fixture.Awards.Verify(repository => repository.UpdateAsync(
            It.IsAny<TenderAward>()), Times.Never);
    }

    [Fact]
    public async Task RejectedRecommendationCanBeCorrectedAndResubmitted()
    {
        var fixture = new AwardFixture();
        var rejected = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id,
            TenderBidId = Guid.NewGuid(),
            BusinessPartnerId = Guid.NewGuid(),
            AwardedAmount = 90m,
            OriginalBidAmount = 90m,
            Status = "Rejected",
            Notes = "Rejected: recommendation did not match retained evaluation."
        };
        fixture.Awards.Setup(repository => repository.GetByTenderIdAsync(fixture.Tender.Id))
            .ReturnsAsync(rejected);
        fixture.Awards.Setup(repository => repository.UpdateAsync(rejected)).ReturnsAsync(rejected);

        var result = await fixture.Service.CreateAwardAsync(
            fixture.Tender.Id,
            new CreateAwardDto
            {
                TenderId = fixture.Tender.Id,
                TenderBidId = fixture.Bid.Id,
                AwardedAmount = 100m,
                Currency = "GHS",
                AwardJustification = "Corrected retained recommendation"
            });

        result.Id.Should().Be(rejected.Id);
        result.Status.Should().Be("PendingApproval");
        result.TenderBidId.Should().Be(fixture.Bid.Id);
        result.Notes.Should().Contain("Rejected:").And.Contain("Resubmitted:");
        fixture.Awards.Verify(repository => repository.UpdateAsync(rejected), Times.Once);
        fixture.Awards.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderAward>()), Times.Never);
    }

    [Fact]
    public async Task AwardNotificationRejectsMismatchedTenderRoute()
    {
        var fixture = new AwardFixture();
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = fixture.Tender.Id,
            TenderBidId = fixture.Bid.Id,
            BusinessPartnerId = fixture.Bid.BusinessPartnerId,
            Status = "Awarded"
        };
        fixture.Awards.Setup(repository => repository.GetByIdAsync(award.Id))
            .ReturnsAsync(award);

        await fixture.Service.Invoking(service => service.SendAwardNotificationsAsync(
                Guid.NewGuid(), new AwardNotificationDto { AwardId = award.Id }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not belong to the selected tender*");
        fixture.Notifications.Verify(service => service.SendAwardNotificationAsync(
            It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseOrderRunsReadsAndSerializableClaimInsideExecutionStrategy()
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();

        var result = await fixture.Service.CreatePurchaseOrderFromAwardAsync(request);

        result.Status.Should().Be("Draft");
        result.TotalAmount.Should().Be(100m);
        result.ItemCount.Should().Be(1);
        fixture.CreatedOrder!.Currency.Should().Be("GHS");
        fixture.Events.Should().Equal("strategy", "read", "begin", "reserve", "order", "item", "claim", "bound", "commit");
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(
            IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("reserve")]
    [InlineData("item")]
    [InlineData("claim")]
    [InlineData("bound")]
    public async Task PurchaseOrderFailureRollsBackOwnedTransaction(string failedStep)
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();
        fixture.FailAt = failedStep;

        await fixture.Service.Invoking(service => service.CreatePurchaseOrderFromAwardAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("injected failure");

        fixture.Events.Last().Should().Be("rollback");
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurchaseOrderRetryReloadsAwardAfterRollbackAndCommitsOnlySuccessfulAttempt()
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();
        fixture.FailAt = "claim";
        fixture.UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<PurchaseOrderFromAwardResponseDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<PurchaseOrderFromAwardResponseDto>> operation, CancellationToken _) =>
            {
                fixture.InStrategy = true;
                try { return await operation(); }
                catch (InvalidOperationException exception) when (exception.Message == "injected failure")
                {
                    fixture.FailAt = null;
                    return await operation();
                }
            });

        await fixture.Service.CreatePurchaseOrderFromAwardAsync(request);

        fixture.Events.Count(item => item == "read").Should().Be(2);
        fixture.Events.Should().ContainInOrder("rollback", "clear", "read", "begin");
        fixture.UnitOfWork.Verify(unit => unit.ClearTrackedChanges(), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurchaseOrderRetryAfterUncertainCommitCannotCreateSecondOrder()
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();
        fixture.UnitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                // Simulate SQL committing the claim but the connection losing
                // its acknowledgement. The next attempt must see that claim.
                fixture.PurchaseOrderAward!.PurchaseOrderId = fixture.CreatedOrder!.Id;
                fixture.ActiveTransaction = false;
                throw new TimeoutException("Commit acknowledgement lost");
            });
        fixture.UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<PurchaseOrderFromAwardResponseDto>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<PurchaseOrderFromAwardResponseDto>> operation, CancellationToken _) =>
            {
                fixture.InStrategy = true;
                try { return await operation(); }
                catch (TimeoutException) { return await operation(); }
            });

        await fixture.Service.Invoking(service => service.CreatePurchaseOrderFromAwardAsync(request))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("Purchase order already exists*");

        fixture.UnitOfWork.Verify(unit => unit.ClearTrackedChanges(), Times.Once);
        fixture.Orders.Verify(repository => repository.CreatePurchaseOrderAsync(It.IsAny<PurchaseOrder>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(
            IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PurchaseOrderLeavesCallerOwnedTransactionToCaller(bool fail)
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();
        fixture.ActiveTransaction = true;
        fixture.InStrategy = true; // The caller owns the complete retry boundary.
        fixture.FailAt = fail ? "claim" : null;

        if (fail)
            await fixture.Service.Invoking(service => service.CreatePurchaseOrderFromAwardAsync(request))
                .Should().ThrowAsync<InvalidOperationException>();
        else
            await fixture.Service.CreatePurchaseOrderFromAwardAsync(request);

        fixture.UnitOfWork.Verify(unit => unit.ExecuteInStrategyAsync(
            It.IsAny<Func<Task<PurchaseOrderFromAwardResponseDto>>>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unapproved")]
    [InlineData("tenant")]
    [InlineData("auto-approve")]
    public async Task PurchaseOrderStillRejectsInvalidAwardOrApprovalBypassBeforeMutation(string invalid)
    {
        var fixture = new AwardFixture();
        var request = fixture.PreparePurchaseOrder();
        if (invalid == "duplicate") fixture.PurchaseOrderAward!.PurchaseOrderId = Guid.NewGuid();
        if (invalid == "unapproved") fixture.PurchaseOrderAward!.Status = "PendingApproval";
        if (invalid == "tenant") fixture.PurchaseOrderAward!.TenantId = Guid.NewGuid();
        if (invalid == "auto-approve")
        {
            request.AutoApprove = true;
            fixture.PurchaseOrderSod.Setup(service => service.RejectApprovalBypassAsync(
                It.IsAny<PurchaseOrder>(), "AwardAutoApprove", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Approval bypass denied"));
        }

        await fixture.Service.Invoking(service => service.CreatePurchaseOrderFromAwardAsync(request))
            .Should().ThrowAsync<InvalidOperationException>();

        fixture.Orders.Verify(repository => repository.CreatePurchaseOrderAsync(It.IsAny<PurchaseOrder>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.BeginTransactionAsync(
            It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class AwardFixture
    {
        public AwardFixture()
        {
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-MAKER-CHECKER",
                Title = "Maker-checker tender",
                Status = "Evaluated",
                Currency = "GHS"
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                BusinessPartnerId = Guid.NewGuid(),
                BidNumber = "BID-MAKER-CHECKER",
                TotalBidAmount = 100m,
                Status = "Evaluated"
            };
            CurrentUser.SetupGet(provider => provider.TenantId).Returns(TenantId);
            CurrentUser.SetupGet(provider => provider.UserId).Returns(UserId);
            UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task<PurchaseOrderFromAwardResponseDto>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task<PurchaseOrderFromAwardResponseDto>> operation,
                    CancellationToken _) => operation());
            TenderControls.Setup(service => service.IsControlledTenderMethodAsync(
                    Tender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            ExceptionalControls.Setup(service => service.IsExceptionalAsync(
                    Tender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            Tenders.Setup(repository => repository.GetByIdAsync(Tender.Id)).ReturnsAsync(Tender);
            Bids.Setup(repository => repository.GetByIdAsync(Bid.Id)).ReturnsAsync(Bid);
            Awards.Setup(repository => repository.GetByTenderIdAsync(Tender.Id))
                .ReturnsAsync((TenderAward?)null);
            Awards.Setup(repository => repository.CreateAsync(It.IsAny<TenderAward>()))
                .ReturnsAsync((TenderAward value) => value);

            Service = new TenderAwardService(
                Awards.Object, Tenders.Object, Bids.Object,
                BidItems.Object, Mock.Of<ITenderEvaluationRepository>(),
                Mock.Of<ITenderEvaluatorRepository>(), Notifications.Object,
                Orders.Object, OrderItems.Object,
                Mock.Of<ITenderNegotiationRepository>(), UnitOfWork.Object, CurrentUser.Object,
                TenderControls.Object, ExceptionalControls.Object, SourcingCases.Object,
                Readiness.Object,
                PurchaseOrderSources.Object,
                PurchaseOrderSod.Object,
                Mock.Of<ILogger<TenderAwardService>>());
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public Mock<ITenderAwardRepository> Awards { get; } = new();
        public Mock<ITenderRepository> Tenders { get; } = new();
        public Mock<ITenderBidRepository> Bids { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IProcurementTenderControlService> TenderControls { get; } = new();
        public Mock<IProcurementExceptionalSourcingControlService> ExceptionalControls { get; } = new();
        public Mock<IProcurementAwardReadinessService> Readiness { get; } = new();
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public Mock<IProcurementPurchaseOrderSourceService> PurchaseOrderSources { get; } = new();
        public Mock<ITenderNotificationService> Notifications { get; } = new();
        public TenderAwardService Service { get; }
        public Mock<ITenderBidItemRepository> BidItems { get; } = new();
        public Mock<IPurchaseOrderRepository> Orders { get; } = new();
        public Mock<IPurchaseOrderItemRepository> OrderItems { get; } = new();
        public Mock<IProcurementPurchaseOrderSodService> PurchaseOrderSod { get; } = new();
        public List<string> Events { get; } = [];
        public bool ActiveTransaction { get; set; }
        public bool InStrategy { get; set; }
        public string? FailAt { get; set; }
        public TenderAward? PurchaseOrderAward { get; private set; }
        public PurchaseOrder? CreatedOrder { get; private set; }

        private void Step(string name)
        {
            InStrategy.Should().BeTrue("database operations must execute inside the retry strategy");
            Events.Add(name);
            if (FailAt == name) throw new InvalidOperationException("injected failure");
        }

        public CreatePurchaseOrderFromAwardDto PreparePurchaseOrder()
        {
            PurchaseOrderAward = new TenderAward
            {
                Id = Guid.NewGuid(), TenantId = TenantId, TenderId = Tender.Id,
                TenderBidId = Bid.Id, BusinessPartnerId = Bid.BusinessPartnerId,
                Status = "Awarded", Currency = "GHS", AwardedAmount = 100m
            };
            Awards.Setup(repository => repository.GetByIdAsync(PurchaseOrderAward.Id))
                .Callback(() => Step("read")).ReturnsAsync(() => PurchaseOrderAward);
            var source = new ProcurementPurchaseOrderSourceResolution
            {
                SourceType = ProcurementPurchaseOrderSourceType.TenderAward,
                SourceId = PurchaseOrderAward.Id, CurrencyCode = "GHS",
                PurchaseRequisitionRequestedById = Guid.NewGuid()
            };
            PurchaseOrderSources.Setup(service => service.ResolveAsync(
                source.SourceType, source.SourceId, Bid.BusinessPartnerId,
                It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(source);
            BidItems.Setup(repository => repository.GetByBidIdAsync(Bid.Id))
                .ReturnsAsync(new[] { new TenderBidItem
                {
                    Id = Guid.NewGuid(), OfferedQuantity = 2m, UnitPrice = 50m, TotalPrice = 100m,
                    TenderItem = new TenderItem { Description = "Goods", UnitOfMeasure = "EA" }
                } });
            Orders.Setup(repository => repository.GenerateOrderNumberAsync()).ReturnsAsync("PO-TEST-001");
            Orders.Setup(repository => repository.CreatePurchaseOrderAsync(It.IsAny<PurchaseOrder>()))
                .Callback<PurchaseOrder>(order => { Step("order"); CreatedOrder = order; })
                .ReturnsAsync((PurchaseOrder order) => order);
            OrderItems.Setup(repository => repository.CreateItemAsync(It.IsAny<PurchaseOrderItem>()))
                .Callback(() => Step("item")).ReturnsAsync((PurchaseOrderItem item) => item);
            PurchaseOrderSources.Setup(service => service.ReserveAsync(source,
                It.IsAny<IReadOnlyCollection<ProcurementPurchaseOrderSourceOrderLine>>(),
                100m, "GHS", It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => { ActiveTransaction.Should().BeTrue(); Step("reserve"); });
            PurchaseOrderSources.Setup(service => service.ClaimTenderAwardAsync(
                PurchaseOrderAward.Id, It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()))
                .Callback(() => { ActiveTransaction.Should().BeTrue(); Step("claim"); });
            PurchaseOrderSources.Setup(service => service.RecordBoundAsync(
                It.IsAny<PurchaseOrder>(), "TenderAwardPurchaseOrderCreated",
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback(() => { ActiveTransaction.Should().BeTrue(); Step("bound"); });
            UnitOfWork.SetupGet(unit => unit.HasActiveTransaction).Returns(() => ActiveTransaction);
            UnitOfWork.Setup(unit => unit.BeginTransactionAsync(
                IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .Callback(() => { Step("begin"); ActiveTransaction = true; });
            UnitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
                .Callback(() => { Step("commit"); ActiveTransaction = false; });
            UnitOfWork.Setup(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()))
                .Callback(() => { Step("rollback"); ActiveTransaction = false; });
            UnitOfWork.Setup(unit => unit.ClearTrackedChanges()).Callback(() => Events.Add("clear"));
            UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task<PurchaseOrderFromAwardResponseDto>>>(), It.IsAny<CancellationToken>()))
                .Returns(async (Func<Task<PurchaseOrderFromAwardResponseDto>> operation, CancellationToken _) =>
                {
                    Events.Add("strategy"); InStrategy = true;
                    try { return await operation(); }
                    finally { InStrategy = false; }
                });
            return new CreatePurchaseOrderFromAwardDto { TenderAwardId = PurchaseOrderAward.Id };
        }
    }
}
