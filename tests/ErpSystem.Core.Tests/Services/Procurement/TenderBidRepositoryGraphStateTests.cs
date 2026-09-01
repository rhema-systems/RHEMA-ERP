using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderBidRepositoryGraphStateTests
{
    [Fact]
    public async Task UpdatingBidDraftPreservesNewSelectedLotAsAdded()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);

        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = Guid.NewGuid(),
            BidNumber = "BID-STEP-ONE",
            Status = "Draft",
            Currency = "GHS"
        };
        context.Attach(bid);

        var selectedLot = new TenderBidLot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderBidId = bid.Id,
            LotId = Guid.NewGuid(),
            TenderBid = bid,
            Status = "Draft",
            Currency = "GHS"
        };
        bid.BidLots.Add(selectedLot);

        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        var bids = new TenderBidRepository(context, currentUser.Object);
        var bidLots = new TenderBidLotRepository(context);

        await bidLots.CreateAsync(selectedLot);
        context.Entry(selectedLot).State.Should().Be(EntityState.Added);

        await bids.UpdateAsync(bid);
        await bidLots.UpdateAsync(selectedLot);

        context.Entry(bid).State.Should().Be(EntityState.Modified);
        context.Entry(selectedLot).State.Should().Be(EntityState.Added,
            "the selected lot must be inserted before bid items reference it");
    }
}
