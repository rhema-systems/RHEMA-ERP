using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceRfqTests
{
    [Fact]
    public void AwardExposureIsComparedWithApprovedBudgetAvailabilityNotPrEstimate()
    {
        var withinBudgetDespiteLowerEstimate = () =>
            ProcurementPurchaseOrderSourceService.EnsureAwardBudgetExposure(
                250_000m,
                4_920m,
                "GHS");
        withinBudgetDespiteLowerEstimate.Should().NotThrow();

        var overBudget = () =>
            ProcurementPurchaseOrderSourceService.EnsureAwardBudgetExposure(
                4_900m,
                4_920m,
                "GHS");
        overBudget.Should().Throw<ProcurementPurchaseOrderSourceValidationException>()
            .Where(exception =>
                exception.Code == "PO_BUDGET_INSUFFICIENT_FOR_AWARD" &&
                exception.Message.Contains("20.00 GHS"));

        var revisedQuote = () =>
            ProcurementPurchaseOrderSourceService.EnsureAwardBudgetExposure(
                4_900m,
                3_990m,
                "GHS");
        revisedQuote.Should().NotThrow();
    }

    [Fact]
    public void AwardAvailabilitySubtractsOtherRequisitionReservationsAndAddsBackOnlyCurrentEnvelope()
    {
        var otherRequisitionReserved =
            ProcurementPurchaseOrderSourceService.CalculateAwardBudgetAvailability(
                allocatedAmount: 100m,
                utilizedAmount: 0m,
                committedAmount: 0m,
                reservedAmount: 70m,
                currentRequisitionReservation: 0m);

        otherRequisitionReserved.Should().Be(30m);
        var blocked = () =>
            ProcurementPurchaseOrderSourceService.EnsureAwardBudgetExposure(
                otherRequisitionReserved,
                requiredExposure: 50m,
                currencyCode: "GHS");
        blocked.Should().Throw<ProcurementPurchaseOrderSourceValidationException>()
            .Where(exception => exception.Code == "PO_BUDGET_INSUFFICIENT_FOR_AWARD");

        var currentEnvelopeReusable =
            ProcurementPurchaseOrderSourceService.CalculateAwardBudgetAvailability(
                allocatedAmount: 100m,
                utilizedAmount: 0m,
                committedAmount: 0m,
                reservedAmount: 70m,
                currentRequisitionReservation: 20m);

        currentEnvelopeReusable.Should().Be(50m);
        Action reusable = () =>
            ProcurementPurchaseOrderSourceService.EnsureAwardBudgetExposure(
                currentEnvelopeReusable,
                requiredExposure: 50m,
                currencyCode: "GHS");
        reusable.Should().NotThrow();
    }

    [Fact]
    public async Task AwardedReleaseOnlyRfqResolvesAndAppliesWithoutSourcingCase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var requisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionNumber = "PR-DIRECT-RFQ-001",
            RequestedById = Guid.NewGuid(),
            Status = "Approved",
            ProcurementCategory = ProcurementCategoryClass.Goods,
            Currency = "GHS"
        };
        var release = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseRequisitionId = requisition.Id,
            AttemptNumber = 1,
            ReleaseReference = "SRL-PR-DIRECT-RFQ-001-A1",
            ReleasedAtUtc = DateTime.UtcNow.AddHours(-2),
            IntegrityHash = new string('b', 64)
        };
        var rfq = new RequestForQuotation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RfqNumber = "RFQ-DIRECT-001",
            Title = "Direct approved requisition RFQ",
            Status = "Awarded",
            Currency = "GHS",
            SourcePurchaseRequisitionId = requisition.Id,
            SourcingReleaseId = release.Id,
            SourcingCaseId = null
        };
        var rfqItem = new RequestForQuotationItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RfqId = rfq.Id,
            LineNumber = 1,
            Description = "Wireless keyboard",
            Quantity = 10,
            UnitOfMeasure = "EA"
        };
        var awardLine = new RequestForQuotationAwardLine
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RfqId = rfq.Id,
            RfqItemId = rfqItem.Id,
            BusinessPartnerId = supplierId,
            QuoteId = Guid.NewGuid(),
            UnitPrice = 490,
            LineTotal = 4_900
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableServiceProviderCaching(false)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        context.AddRange(requisition, release, rfq, rfqItem, awardLine);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);

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
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto());

        var service = new ProcurementPurchaseOrderSourceService(
            unitOfWork,
            currentUser.Object,
            access.Object,
            controlEvents.Object,
            new Mock<IProcurementRequisitionBudgetControlService>().Object,
            new Mock<IProcurementBudgetReservationStore>().Object,
            new Mock<INotificationTopicPublisher>().Object,
            NullLogger<ProcurementPurchaseOrderSourceService>.Instance);

        var source = await service.ResolveAsync(
            ProcurementPurchaseOrderSourceType.RfqAward,
            rfq.Id,
            supplierId,
            "release-only-rfq-award");

        source.PurchaseRequisitionId.Should().Be(requisition.Id);
        source.SourcingReleaseId.Should().Be(release.Id);
        source.SourcingCaseId.Should().Be(Guid.Empty);
        source.AwardReadinessDecisionId.Should().Be(Guid.Empty);
        source.CurrencyCode.Should().Be("GHS");
        source.ApprovedAmount.Should().Be(4_900);
        source.ApprovedLines.Should().ContainSingle(item =>
            item.SourceLineId == awardLine.Id && item.UnitPrice == 490);
        source.SourceIntegrityHash.Should().HaveLength(64);

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = supplierId
        };
        service.Apply(purchaseOrder, source);

        purchaseOrder.SourcingReleaseId.Should().Be(release.Id);
        purchaseOrder.SourcingCaseId.Should().BeNull();
        purchaseOrder.AwardReadinessDecisionId.Should().BeNull();
        purchaseOrder.Currency.Should().Be("GHS");
    }

    [Fact]
    public async Task AwardedReleaseOnlyTenderResolvesAndAppliesWithoutSourcingCase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var requisition = new PurchaseRequisition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequisitionNumber = "PR-DIRECT-TENDER-001",
            RequestedById = Guid.NewGuid(),
            Status = "Approved",
            ProcurementCategory = ProcurementCategoryClass.Goods,
            Currency = "GHS"
        };
        var release = new ProcurementRequisitionSourcingRelease
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PurchaseRequisitionId = requisition.Id,
            AttemptNumber = 1,
            ReleaseReference = "SRL-PR-DIRECT-TENDER-001-A1",
            ReleasedAtUtc = DateTime.UtcNow.AddHours(-2),
            IntegrityHash = new string('c', 64)
        };
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderNumber = "TND-DIRECT-001",
            Title = "Direct approved requisition tender",
            TenderType = "NCT",
            Status = "Awarded",
            Currency = "GHS",
            SourcePurchaseRequisitionId = requisition.Id,
            SourcingReleaseId = release.Id,
            SourcingCaseId = null
        };
        var tenderItem = new TenderItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            LineNumber = 1,
            ItemCode = "KEYBOARD-001",
            Description = "Wireless keyboard",
            Quantity = 10,
            UnitOfMeasure = "EA"
        };
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            BusinessPartnerId = supplierId,
            BidNumber = "BID-DIRECT-001",
            Status = "Accepted",
            Currency = "GHS",
            TotalBidAmount = 4_900
        };
        var bidItem = new TenderBidItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderBidId = bid.Id,
            TenderItemId = tenderItem.Id,
            OfferedQuantity = 10,
            UnitPrice = 490,
            TotalPrice = 4_900
        };
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            TenderBidId = bid.Id,
            BusinessPartnerId = supplierId,
            OriginalBidAmount = 4_900,
            AwardedAmount = 4_900,
            Currency = "GHS",
            Status = "Awarded"
        };
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContractNumber = "CON-DIRECT-TENDER-001",
            ContractTitle = "Direct approved requisition tender contract",
            ContractType = "Goods",
            Status = "Active",
            TenderAwardId = award.Id,
            TenderId = tender.Id,
            TenderBidId = bid.Id,
            BusinessPartnerId = supplierId,
            ContractValue = 4_900,
            Currency = "GHS",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var readiness = new ProcurementAwardReadinessDecision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceType = ProcurementAwardReadinessSourceType.Tender,
            SourceId = tender.Id,
            SourceReference = tender.TenderNumber,
            Method = ProcurementMethodType.NationalCompetitiveTendering,
            DecisionSequence = 1,
            Status = ProcurementAwardReadinessDecisionStatus.Ready,
            RecommendationSubjectType = "TenderBid",
            RecommendedSubjectIdsJson = $"[\"{bid.Id:D}\"]",
            RecommendedBusinessPartnerIdsJson = $"[\"{supplierId:D}\"]",
            SourceIntegrityHash = new string('d', 64),
            IntegrityHash = new string('e', 64),
            IdempotencyKey = "direct-tender-readiness",
            CorrelationId = "direct-tender-readiness",
            EvaluatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EvaluatedByUserId = Guid.NewGuid(),
            EvaluatedByName = "Independent Approver"
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .EnableServiceProviderCaching(false)
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        context.AddRange(
            requisition, release, tender, tenderItem, bid, bidItem, award, contract,
            readiness);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
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
        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto());
        var service = new ProcurementPurchaseOrderSourceService(
            unitOfWork,
            currentUser.Object,
            access.Object,
            controlEvents.Object,
            new Mock<IProcurementRequisitionBudgetControlService>().Object,
            new Mock<IProcurementBudgetReservationStore>().Object,
            new Mock<INotificationTopicPublisher>().Object,
            NullLogger<ProcurementPurchaseOrderSourceService>.Instance);

        var source = await service.ResolveAsync(
            ProcurementPurchaseOrderSourceType.TenderAward,
            award.Id,
            supplierId,
            "release-only-tender-award");

        source.PurchaseRequisitionId.Should().Be(requisition.Id);
        source.SourcingReleaseId.Should().Be(release.Id);
        source.SourcingCaseId.Should().Be(Guid.Empty);
        source.AwardReadinessDecisionId.Should().Be(readiness.Id);
        source.CurrencyCode.Should().Be("GHS");
        source.ApprovedLines.Should().ContainSingle(item =>
            item.SourceLineId == bidItem.Id && item.UnitPrice == 490);

        var purchaseOrder = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = supplierId
        };
        service.Apply(purchaseOrder, source);

        purchaseOrder.SourcingReleaseId.Should().Be(release.Id);
        purchaseOrder.SourcingCaseId.Should().BeNull();
        purchaseOrder.AwardReadinessDecisionId.Should().Be(readiness.Id);
        purchaseOrder.Currency.Should().Be("GHS");

        var contractSource = await service.ResolveAsync(
            ProcurementPurchaseOrderSourceType.Contract,
            contract.Id,
            supplierId,
            "release-only-tender-contract");

        contractSource.PurchaseRequisitionId.Should().Be(requisition.Id);
        contractSource.SourcingReleaseId.Should().Be(release.Id);
        contractSource.SourcingCaseId.Should().Be(Guid.Empty);
        contractSource.AwardReadinessDecisionId.Should().Be(readiness.Id);
        contractSource.SourceId.Should().Be(contract.Id);
        contractSource.ApprovedLines.Should().ContainSingle(item =>
            item.SourceLineId == bidItem.Id && item.LineTotal == 4_900);
    }
}
