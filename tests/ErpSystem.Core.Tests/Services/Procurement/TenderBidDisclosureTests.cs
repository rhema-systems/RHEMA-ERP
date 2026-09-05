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

public sealed class TenderBidDisclosureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TenderWideSummaryRequiresRecordedOpeningNotStatusOrDeadline(bool opened)
    {
        var bid = new TenderBid { Id = Guid.NewGuid(), Status = "Evaluated", TotalBidAmount = 52000,
            Currency = "GHS", TechnicalScore = 90, FinancialScore = 88,
            OpenedDate = opened ? DateTime.UtcNow.AddMinutes(-1) : null };
        var result = TenderService.MapBidToSummaryDto(bid);
        result.IsSealed.Should().Be(!opened);
        result.TotalBidAmount.Should().Be(opened ? 52000 : 0);
        if (!opened) { result.Currency.Should().BeNull(); result.TechnicalScore.Should().BeNull(); }
        bid.TotalBidAmount.Should().Be(52000, "read redaction never alters persisted bid data");
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    public async Task AllBidReadSurfacesPreserveOwnerAndQualityEnvelopeBoundaries(bool external, bool opened, bool qualityFinancialSealed)
    {
        var bid = new TenderBid { Id = Guid.NewGuid(), TenderId = Guid.NewGuid(), BusinessPartnerId = Guid.NewGuid(),
            BidNumber = "BID-TEST", Status = "Submitted", TotalBidAmount = 52000, Currency = "GHS",
            TechnicalProposal = "technical secret", CommercialProposal = "commercial secret",
            OpenedDate = opened ? DateTime.UtcNow.AddMinutes(-1) : null };
        var lot = new TenderBidLot { Id = Guid.NewGuid(), TenderBidId = bid.Id, TotalLotAmount = 52000,
            CommercialProposal = "lot commercial", Notes = "lot price notes" };
        bid.BidLots.Add(lot);
        var item = new TenderBidItem { Id = Guid.NewGuid(), TenderBidId = bid.Id, UnitPrice = 2600, TotalPrice = 52000 };
        lot.Items.Add(item);
        var documents = new[] {
            new TenderBidDocument { Id = Guid.NewGuid(), TenderBidId = bid.Id, DocumentType = "TechnicalProposal", DocumentName = "technical.pdf" },
            new TenderBidDocument { Id = Guid.NewGuid(), TenderBidId = bid.Id, DocumentType = "CommercialProposal", DocumentName = "commercial.pdf" }
        };
        var bids = new Mock<ITenderBidRepository>();
        bids.Setup(r => r.GetByIdAsync(bid.Id)).ReturnsAsync(bid);
        bids.Setup(r => r.GetWithAllRelatedDataAsync(bid.Id)).ReturnsAsync(bid);
        bids.Setup(r => r.GetByBidNumberAsync(bid.BidNumber)).ReturnsAsync(bid);
        bids.Setup(r => r.GetByTenderIdAsync(bid.TenderId)).ReturnsAsync(new[] { bid });
        var tenders = new Mock<ITenderRepository>();
        tenders.Setup(r => r.GetByIdAsync(bid.TenderId)).ReturnsAsync(new Tender { Id = bid.TenderId, Status = "Published" });
        var items = new Mock<ITenderBidItemRepository>();
        items.Setup(r => r.GetByBidIdAsync(bid.Id)).ReturnsAsync(new[] { item });
        var docs = new Mock<ITenderBidDocumentRepository>();
        docs.Setup(r => r.GetByBidIdAsync(bid.Id)).ReturnsAsync(documents);
        var lots = new Mock<ITenderBidLotRepository>();
        lots.Setup(r => r.GetByBidIdAsync(bid.Id)).ReturnsAsync(new[] { lot });
        lots.Setup(r => r.GetByIdWithItemsAsync(lot.Id)).ReturnsAsync(lot);
        var fees = new Mock<ITenderFeeRepository>();
        fees.Setup(r => r.GetByTenderIdAsync(bid.TenderId)).ReturnsAsync(Array.Empty<TenderFee>());
        var payments = new Mock<ITenderPaymentRepository>();
        payments.Setup(r => r.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId)).ReturnsAsync(Array.Empty<TenderPayment>());
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(r => r.IsExternalUser).Returns(external);
        var controls = new Mock<IProcurementTenderControlService>();
        controls.Setup(r => r.ShouldConcealFinancialProposalAsync(bid.TenderId, bid.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityFinancialSealed);
        var service = new TenderBidService(bids.Object, tenders.Object, items.Object, docs.Object, payments.Object,
            fees.Object, Mock.Of<ITenderInterviewRepository>(), Mock.Of<ITenderAssignmentRepository>(), lots.Object,
            Mock.Of<ITenderNotificationService>(), Mock.Of<IBusinessPartnerRepository>(), Mock.Of<IBusinessPartnerUserRepository>(),
            Mock.Of<ISupplierValidationService>(), Mock.Of<IUnitOfWork>(), actor.Object, Mock.Of<IAppEventBus>(),
            controls.Object, Mock.Of<IProcurementTenderDocumentControlService>(), Mock.Of<IProcurementExceptionalSourcingControlService>(),
            Mock.Of<IQuantitySurveyTenderBoqSubmissionService>(), Mock.Of<ILogger<TenderBidService>>());
        var sealedBid = !external && !opened;
        var financialVisible = external || (opened && !qualityFinancialSealed);
        var expectedFiles = sealedBid ? 0 : financialVisible ? 2 : 1;
        foreach (var result in new[] { await service.GetBidByIdAsync(bid.Id), await service.GetBidByNumberAsync(bid.BidNumber) })
        {
            result!.IsSealed.Should().Be(sealedBid);
            result.TotalBidAmount.Should().Be(financialVisible ? 52000 : 0);
            result.Documents.Should().HaveCount(expectedFiles);
            if (sealedBid) { result.TechnicalProposal.Should().BeNull(); result.Items.Should().BeEmpty(); result.BidLots.Should().BeEmpty(); }
            if (!financialVisible) result.CommercialProposal.Should().BeNull();
            if (!sealedBid && !financialVisible) result.BidLots.Single().TotalLotAmount.Should().Be(0);
        }
        var summary = (await service.GetBidsByTenderIdAsync(bid.TenderId)).Single();
        summary.IsSealed.Should().Be(sealedBid);
        summary.TotalBidAmount.Should().Be(financialVisible ? 52000 : 0);
        (await service.GetBidDocumentsAsync(bid.Id)).Should().HaveCount(expectedFiles);
        (await service.GetBidLotsAsync(bid.Id)).Should().HaveCount(sealedBid ? 0 : 1);
        var lotResult = await service.GetBidLotByIdAsync(lot.Id);
        if (sealedBid) lotResult.Should().BeNull();
        else lotResult!.TotalLotAmount.Should().Be(financialVisible ? 52000 : 0);
        bid.TotalBidAmount.Should().Be(52000);
        lot.TotalLotAmount.Should().Be(52000);
    }
}
