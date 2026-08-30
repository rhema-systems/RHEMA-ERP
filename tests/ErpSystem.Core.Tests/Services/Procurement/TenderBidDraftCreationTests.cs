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

public sealed class TenderBidDraftCreationTests
{
    [Fact]
    public async Task SelectedLotItemsCanCreateInitialDraftWithZeroPrice()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenderId = Guid.NewGuid();
        var tenderItemId = Guid.NewGuid();
        var tenderLotId = Guid.NewGuid();
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-STEP-ONE",
            PartnerName = "Step One Supplier",
            PartnerType = "Supplier",
            UserId = userId,
            IsActive = true,
            ApprovalStatus = "Approved"
        };
        var tender = new Tender
        {
            Id = tenderId,
            TenantId = tenantId,
            TenderNumber = "TND-2026-TEST",
            Title = "Step one draft test",
            Status = "Published",
            Currency = "GHS",
            SubmissionDeadline = DateTime.UtcNow.AddDays(1)
        };
        var tenderLot = new TenderLot
        {
            Id = tenderLotId,
            TenantId = tenantId,
            TenderId = tenderId,
            LotCode = "LOT-01",
            Title = "Selected lot",
            Status = "Active"
        };
        var tenderItem = new TenderItem
        {
            Id = tenderItemId,
            TenantId = tenantId,
            TenderId = tenderId,
            LotId = tenderLotId,
            Description = "Selected item",
            Quantity = 10m,
            UnitOfMeasure = "EA",
            Lot = tenderLot
        };
        tenderLot.Items.Add(tenderItem);
        tender.Lots.Add(tenderLot);
        tender.Items.Add(tenderItem);

        var bids = new Mock<ITenderBidRepository>();
        var tenders = new Mock<ITenderRepository>();
        var bidItems = new Mock<ITenderBidItemRepository>();
        var partners = new Mock<IBusinessPartnerRepository>();
        var supplierValidation = new Mock<ISupplierValidationService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserProvider>();
        var tenderControl = new Mock<IProcurementTenderControlService>();
        var bidLots = new Mock<ITenderBidLotRepository>();
        TenderBid? savedBid = null;
        TenderBidItem? savedItem = null;
        TenderBidLot? savedBidLot = null;

        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        tenders.Setup(item => item.GetByIdAsync(tenderId)).ReturnsAsync(tender);
        partners.Setup(item => item.GetByUserIdAsync(userId)).ReturnsAsync(partner);
        tenderControl.Setup(item => item.IsControlledTenderMethodAsync(
                tenderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        supplierValidation.Setup(item => item.ValidateForTenderAsync(
                partner.Id, false, null))
            .ReturnsAsync(new SupplierValidationResult
            {
                IsValid = true,
                BusinessPartnerId = partner.Id,
                TenantId = tenantId
            });
        bids.Setup(item => item.GenerateBidNumberAsync()).ReturnsAsync("BID-2026-9999");
        bids.Setup(item => item.CreateAsync(It.IsAny<TenderBid>()))
            .Callback<TenderBid>(item => savedBid = item)
            .ReturnsAsync((TenderBid item) => item);
        bids.Setup(item => item.UpdateAsync(It.IsAny<TenderBid>()))
            .ReturnsAsync((TenderBid item) => item);
        bidItems.Setup(item => item.CreateAsync(It.IsAny<TenderBidItem>()))
            .Callback<TenderBidItem>(item => savedItem = item)
            .ReturnsAsync((TenderBidItem item) => item);
        bidLots.Setup(item => item.CreateAsync(It.IsAny<TenderBidLot>()))
            .Callback<TenderBidLot>(item => savedBidLot = item)
            .ReturnsAsync((TenderBidLot item) => item);
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new TenderBidService(
            bids.Object,
            tenders.Object,
            bidItems.Object,
            Mock.Of<ITenderBidDocumentRepository>(),
            Mock.Of<ITenderPaymentRepository>(),
            Mock.Of<ITenderInterviewRepository>(),
            Mock.Of<ITenderAssignmentRepository>(),
            bidLots.Object,
            Mock.Of<ITenderNotificationService>(),
            partners.Object,
            Mock.Of<IBusinessPartnerUserRepository>(),
            supplierValidation.Object,
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IAppEventBus>(),
            tenderControl.Object,
            Mock.Of<IProcurementTenderDocumentControlService>(),
            Mock.Of<IProcurementExceptionalSourcingControlService>(),
            Mock.Of<IQuantitySurveyTenderBoqSubmissionService>(),
            Mock.Of<ILogger<TenderBidService>>());

        var result = await service.CreateBidAsync(new CreateTenderBidDto
        {
            TenderId = tenderId,
            AssociationType = "Self",
            AcceptedDeclaration = true,
            SelectedLotIds = [tenderLotId],
            Items =
            [
                new CreateTenderBidItemDto
                {
                    TenderItemId = tenderItemId,
                    OfferedQuantity = 10m,
                    UnitPrice = 0m
                }
            ]
        });

        savedBid.Should().NotBeNull();
        savedBid!.Status.Should().Be("Draft");
        savedBid.Currency.Should().Be("GHS");
        savedBid.TotalBidAmount.Should().Be(0m);
        savedBid.AssociationType.Should().Be("Self");
        savedBid.AcceptedDeclaration.Should().BeTrue();
        savedBid.DeclarationAcceptedAt.Should().NotBeNull();
        savedItem.Should().NotBeNull();
        savedBidLot.Should().NotBeNull();
        savedBidLot!.LotId.Should().Be(tenderLotId);
        savedBidLot.TotalLotAmount.Should().Be(0m);
        savedItem!.TenderItemId.Should().Be(tenderItemId);
        savedItem.BidLotId.Should().Be(savedBidLot.Id);
        savedItem.OfferedQuantity.Should().Be(10m);
        savedItem.UnitPrice.Should().Be(0m);
        savedItem.TotalPrice.Should().Be(0m);
        result.Status.Should().Be("Draft");
        result.TotalBidAmount.Should().Be(0m);
        result.AssociationType.Should().Be("Self");
        result.AcceptedDeclaration.Should().BeTrue();
        result.BidLots.Should().ContainSingle(lot =>
            lot.LotId == tenderLotId &&
            lot.LotCode == "LOT-01" &&
            lot.TotalLotAmount == 0m &&
            lot.ItemCount == 1);
        result.BidLotCount.Should().Be(1);
        result.Items.Should().ContainSingle(item =>
            item.TenderItemId == tenderItemId &&
            item.UnitPrice == 0m &&
            item.TotalPrice == 0m);
        unitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
