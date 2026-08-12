using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderBidsOwnershipControllerTests
{
    [Fact]
    public async Task GetBidDoesNotExposeAnotherSuppliersBidThroughTenderAssignment()
    {
        var fixture = new Fixture();
        var bidId = Guid.NewGuid();
        var currentPartnerId = Guid.NewGuid();
        fixture.CurrentUser.SetupGet(item => item.IsExternalUser).Returns(true);
        fixture.CurrentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        fixture.BusinessPartners.Setup(item => item.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BusinessPartner { Id = currentPartnerId });
        fixture.Bids.Setup(item => item.GetBidByIdAsync(bidId))
            .ReturnsAsync(new TenderBidDetailDto
            {
                Id = bidId,
                TenderId = Guid.NewGuid(),
                BusinessPartnerId = Guid.NewGuid()
            });

        var result = await fixture.Controller.GetBid(bidId);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Contains(bidId.ToString(), Assert.IsType<string>(notFound.Value));
        fixture.Assignments.Verify(
            item => item.GetByBusinessPartnerIdAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task GetBidReturnsExternalSuppliersOwnBid()
    {
        var fixture = new Fixture();
        var bidId = Guid.NewGuid();
        var currentPartnerId = Guid.NewGuid();
        var bid = new TenderBidDetailDto
        {
            Id = bidId,
            TenderId = Guid.NewGuid(),
            BusinessPartnerId = currentPartnerId
        };
        fixture.CurrentUser.SetupGet(item => item.IsExternalUser).Returns(true);
        fixture.CurrentUser.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
        fixture.BusinessPartners.Setup(item => item.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BusinessPartner { Id = currentPartnerId });
        fixture.Bids.Setup(item => item.GetBidByIdAsync(bidId)).ReturnsAsync(bid);

        var result = await fixture.Controller.GetBid(bidId);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(bid, ok.Value);
    }

    private sealed class Fixture
    {
        public Mock<ITenderBidService> Bids { get; } = new();
        public Mock<IBusinessPartnerRepository> BusinessPartners { get; } = new();
        public Mock<ITenderAssignmentRepository> Assignments { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public TenderBidsController Controller { get; }

        public Fixture()
        {
            Controller = new TenderBidsController(
                Bids.Object,
                BusinessPartners.Object,
                new Mock<IBusinessPartnerUserRepository>().Object,
                Assignments.Object,
                new Mock<ITenderPaymentRepository>().Object,
                CurrentUser.Object,
                new Mock<IControlledFileUploadService>().Object,
                new Mock<ICentralDocumentRepositoryFileService>().Object,
                new Mock<ILogger<TenderBidsController>>().Object);
        }
    }
}
