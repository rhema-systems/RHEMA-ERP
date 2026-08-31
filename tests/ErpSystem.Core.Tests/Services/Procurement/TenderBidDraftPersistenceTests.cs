using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderBidDraftPersistenceTests
{
    [Fact]
    public async Task SaveThenReopenRetainsLotAndEveryEditableLineValueWithoutDuplicates()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var tenderItemId = Guid.NewGuid();
        var bidId = Guid.NewGuid();
        var bidLotId = Guid.NewGuid();
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-DRAFT",
            PartnerName = "Draft Supplier",
            PartnerType = "Supplier",
            UserId = userId,
            IsActive = true,
            ApprovalStatus = "Approved"
        };
        var tender = new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TND-2026-DRAFT",
            Title = "Draft persistence",
            Status = "Published",
            Currency = "GHS",
            SubmissionDeadline = DateTime.UtcNow.AddDays(1)
        };
        var lot = new TenderLot
        {
            Id = lotId,
            TenantId = tenantId,
            TenderId = tenderId,
            LotCode = "LOT-01",
            Title = "Equipment",
            Status = "Active"
        };
        var tenderItem = new TenderItem
        {
            Id = tenderItemId,
            TenantId = tenantId,
            TenderId = tenderId,
            LotId = lotId,
            Description = "Laptop",
            Quantity = 12m,
            UnitOfMeasure = "EA",
            Lot = lot
        };
        lot.Items.Add(tenderItem);
        tender.Lots.Add(lot);
        tender.Items.Add(tenderItem);

        var bid = new TenderBid
        {
            Id = bidId,
            TenantId = tenantId,
            TenderId = tenderId,
            BusinessPartnerId = partner.Id,
            BusinessPartner = partner,
            BidNumber = "BID-2026-DRAFT",
            Status = "Draft",
            Currency = "GHS",
            CreatedAt = DateTime.UtcNow
        };
        var bidLot = new TenderBidLot
        {
            Id = bidLotId,
            TenantId = tenantId,
            TenderBidId = bidId,
            LotId = lotId,
            Lot = lot,
            TenderBid = bid,
            Status = "Draft",
            Currency = "GHS"
        };
        var bidItem = new TenderBidItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderBidId = bidId,
            BidLotId = bidLotId,
            TenderItemId = tenderItemId,
            TenderBid = bid,
            BidLot = bidLot,
            TenderItem = tenderItem,
            OfferedQuantity = 12m,
            UnitPrice = 0m
        };
        bid.BidLots.Add(bidLot);
        bid.Items.Add(bidItem);
        bidLot.Items.Add(bidItem);

        var bids = new Mock<ITenderBidRepository>();
        var tenders = new Mock<ITenderRepository>();
        var bidItems = new Mock<ITenderBidItemRepository>();
        var bidDocuments = new Mock<ITenderBidDocumentRepository>();
        var bidLots = new Mock<ITenderBidLotRepository>();
        var partners = new Mock<IBusinessPartnerRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserProvider>();

        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        bids.Setup(item => item.GetByIdAsync(bidId)).ReturnsAsync(bid);
        bids.Setup(item => item.GetByTenderAndPartnerAsync(tenderId, partner.Id))
            .ReturnsAsync(bid);
        bids.Setup(item => item.UpdateAsync(bid)).ReturnsAsync(bid);
        tenders.Setup(item => item.GetByIdAsync(tenderId)).ReturnsAsync(tender);
        partners.Setup(item => item.GetByUserIdAsync(userId)).ReturnsAsync(partner);
        bidItems.Setup(item => item.GetByBidIdAsync(bidId))
            .ReturnsAsync(() => bid.Items.Where(item => !item.IsDeleted).ToList());
        bidItems.Setup(item => item.UpdateAsync(bidItem)).ReturnsAsync(bidItem);
        bidDocuments.Setup(item => item.GetByBidIdAsync(bidId))
            .ReturnsAsync(Array.Empty<TenderBidDocument>());
        bidLots.Setup(item => item.UpdateAsync(bidLot)).ReturnsAsync(bidLot);
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new TenderBidService(
            bids.Object,
            tenders.Object,
            bidItems.Object,
            bidDocuments.Object,
            Mock.Of<ITenderPaymentRepository>(),
            Mock.Of<ITenderFeeRepository>(),
            Mock.Of<ITenderInterviewRepository>(),
            Mock.Of<ITenderAssignmentRepository>(),
            bidLots.Object,
            Mock.Of<ITenderNotificationService>(),
            partners.Object,
            Mock.Of<IBusinessPartnerUserRepository>(),
            Mock.Of<ISupplierValidationService>(),
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementTenderControlService>(),
            Mock.Of<IProcurementTenderDocumentControlService>(),
            Mock.Of<IProcurementExceptionalSourcingControlService>(),
            Mock.Of<IQuantitySurveyTenderBoqSubmissionService>(),
            Mock.Of<ILogger<TenderBidService>>());

        var update = new UpdateTenderBidDto
        {
            DeliveryDays = 14,
            PaymentTerms = "Thirty days after delivery",
            WarrantyTerms = "Two years",
            TechnicalProposal = "Technical proposal retained",
            CommercialProposal = "Commercial proposal retained",
            AssociationType = "Self",
            AcceptedDeclaration = true,
            SelectedLotIds = [lotId],
            Items =
            [
                new UpdateTenderBidItemDto
                {
                    TenderItemId = tenderItemId,
                    OfferedQuantity = 10m,
                    UnitPrice = 450m,
                    DeliveryDays = 9,
                    Specifications = "16 GB RAM",
                    Brand = "Example Brand",
                    Model = "Model X",
                    TechnicalDetails = "Exact saved technical details"
                }
            ]
        };

        await service.UpdateBidAsync(bidId, update);
        await service.UpdateBidAsync(bidId, update);
        var reopened = await service.GetMyDraftBidByTenderIdAsync(tenderId);

        reopened.Should().NotBeNull();
        reopened!.SelectedLotIds.Should().Equal(lotId);
        reopened.BidLots.Should().ContainSingle(item => item.LotId == lotId);
        reopened.DeliveryDays.Should().Be(14);
        reopened.PaymentTerms.Should().Be("Thirty days after delivery");
        reopened.WarrantyTerms.Should().Be("Two years");
        reopened.TechnicalProposal.Should().Be("Technical proposal retained");
        reopened.CommercialProposal.Should().Be("Commercial proposal retained");
        reopened.Items.Should().ContainSingle(item =>
            item.TenderItemId == tenderItemId &&
            item.OfferedQuantity == 10m &&
            item.UnitPrice == 450m &&
            item.TotalPrice == 4500m &&
            item.DeliveryDays == 9 &&
            item.Specifications == "16 GB RAM" &&
            item.Brand == "Example Brand" &&
            item.Model == "Model X" &&
            item.TechnicalDetails == "Exact saved technical details");
        bidItems.Verify(item => item.CreateAsync(It.IsAny<TenderBidItem>()), Times.Never);
        bidLots.Verify(item => item.CreateAsync(It.IsAny<TenderBidLot>()), Times.Never);
        bid.Items.Should().ContainSingle();
        bid.BidLots.Should().ContainSingle();
    }
}
