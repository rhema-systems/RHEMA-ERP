using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderNegotiationLifecycleTests
{
    [Fact]
    public async Task CompletingApprovedAwardCommercialNegotiationRequiresIndependentReapproval()
    {
        var fixture = new Fixture();

        var result = await fixture.Service.CompleteNegotiationAsync(
            fixture.Negotiation.Id,
            new CompleteNegotiationDto
            {
                Items =
                [
                    new UpdateNegotiationItemDto
                    {
                        ItemId = fixture.Item.Id,
                        NegotiatedUnitPrice = 80m
                    }
                ],
                Notes = "Supplier accepted the negotiated price."
            });

        result.Status.Should().Be("Completed");
        fixture.Award.AwardedAmount.Should().Be(80m);
        fixture.Award.Status.Should().Be("PendingApproval");
        fixture.Award.CreatedById.Should().Be(fixture.NegotiatorId);
        fixture.Award.AwardedById.Should().BeNull();
        fixture.Tender.Status.Should().Be("Evaluated");
        fixture.Bid.Status.Should().Be("Evaluated");
        fixture.Award.Notes.Should().Contain("independent award reapproval");
        fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NegotiationCannotChangeAwardAfterContractHandoff()
    {
        var fixture = new Fixture();
        fixture.Contracts.Setup(repository => repository.GetByAwardIdAsync(fixture.Award.Id))
            .ReturnsAsync(new Contract
            {
                Id = Guid.NewGuid(),
                TenantId = fixture.TenantId,
                TenderAwardId = fixture.Award.Id
            });

        await fixture.Service.Invoking(service => service.CompleteNegotiationAsync(
                fixture.Negotiation.Id, new CompleteNegotiationDto()))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*purchase order or contract*");
        fixture.Negotiations.Verify(repository => repository.UpdateItemAsync(
            It.IsAny<TenderNegotiationItem>()), Times.Never);
        fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateNegotiationRejectsCrossTenderOrCrossTenantBid(bool crossTender)
    {
        var fixture = new Fixture();
        if (crossTender)
            fixture.Bid.TenderId = Guid.NewGuid();
        else
            fixture.Bid.TenantId = Guid.NewGuid();
        fixture.Negotiations.Setup(repository => repository.GetByTenderBidAndLotAsync(
                fixture.Tender.Id, fixture.Bid.Id, null))
            .ReturnsAsync((TenderNegotiation?)null);

        await fixture.Service.Invoking(service => service.CreateNegotiationAsync(
                new CreateNegotiationDto
                {
                    TenderId = fixture.Tender.Id,
                    TenderBidId = fixture.Bid.Id
                }))
            .Should().ThrowAsync<Exception>();
        fixture.Negotiations.Verify(repository => repository.CreateAsync(
            It.IsAny<TenderNegotiation>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderNumber = "TND-NEG-001",
                Title = "Negotiated award",
                Status = "Awarded"
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                BusinessPartnerId = Guid.NewGuid(),
                BidNumber = "BID-NEG-001",
                Status = "Awarded",
                Currency = "GHS"
            };
            Award = new TenderAward
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                TenderBidId = Bid.Id,
                BusinessPartnerId = Bid.BusinessPartnerId,
                OriginalBidAmount = 100m,
                AwardedAmount = 100m,
                Currency = "GHS",
                Status = "Awarded",
                AwardedById = Guid.NewGuid(),
                CreatedById = Guid.NewGuid()
            };
            Item = new TenderNegotiationItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderBidItemId = Guid.NewGuid(),
                Quantity = 1m,
                OriginalUnitPrice = 100m,
                OriginalTotalPrice = 100m
            };
            Negotiation = new TenderNegotiation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = Tender.Id,
                TenderBidId = Bid.Id,
                BusinessPartnerId = Bid.BusinessPartnerId,
                Status = "InProgress",
                OriginalAmount = 100m,
                Currency = "GHS",
                Items = [Item]
            };
            Item.NegotiationId = Negotiation.Id;

            CurrentUser.SetupGet(service => service.TenantId).Returns(TenantId);
            CurrentUser.SetupGet(service => service.UserId).Returns(NegotiatorId.ToString());
            Negotiations.Setup(repository => repository.GetByIdWithItemsAsync(Negotiation.Id))
                .ReturnsAsync(Negotiation);
            Negotiations.Setup(repository => repository.UpdateAsync(It.IsAny<TenderNegotiation>()))
                .ReturnsAsync((TenderNegotiation value) => value);
            Negotiations.Setup(repository => repository.UpdateItemAsync(It.IsAny<TenderNegotiationItem>()))
                .ReturnsAsync((TenderNegotiationItem value) => value);
            Awards.Setup(repository => repository.GetByTenderIdAsync(Tender.Id)).ReturnsAsync(Award);
            Awards.Setup(repository => repository.UpdateAsync(Award)).ReturnsAsync(Award);
            Tenders.Setup(repository => repository.GetByIdAsync(Tender.Id)).ReturnsAsync(Tender);
            Tenders.Setup(repository => repository.UpdateAsync(Tender)).ReturnsAsync(Tender);
            Bids.Setup(repository => repository.GetByIdAsync(Bid.Id)).ReturnsAsync(Bid);
            Bids.Setup(repository => repository.UpdateAsync(Bid)).ReturnsAsync(Bid);
            Contracts.Setup(repository => repository.GetByAwardIdAsync(Award.Id))
                .ReturnsAsync((Contract?)null);

            Service = new TenderNegotiationService(
                Negotiations.Object, Bids.Object, Mock.Of<ITenderBidItemRepository>(),
                Awards.Object, Tenders.Object, Contracts.Object, UnitOfWork.Object,
                CurrentUser.Object, Mock.Of<IProcurementExceptionalSourcingControlService>(),
                NullLogger<TenderNegotiationService>.Instance);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid NegotiatorId { get; } = Guid.NewGuid();
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public TenderAward Award { get; }
        public TenderNegotiation Negotiation { get; }
        public TenderNegotiationItem Item { get; }
        public Mock<ITenderNegotiationRepository> Negotiations { get; } = new();
        public Mock<ITenderBidRepository> Bids { get; } = new();
        public Mock<ITenderAwardRepository> Awards { get; } = new();
        public Mock<ITenderRepository> Tenders { get; } = new();
        public Mock<IContractRepository> Contracts { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public TenderNegotiationService Service { get; }
    }
}
