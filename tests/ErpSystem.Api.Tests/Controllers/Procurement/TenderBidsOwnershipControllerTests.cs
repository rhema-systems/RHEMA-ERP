using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderBidsOwnershipControllerTests
{
    [Fact]
    public async Task FilteredSealedDocumentCannotReachTheFileDownloadPath()
    {
        var fixture = new Fixture();
        var bidId = Guid.NewGuid();
        fixture.Bids.Setup(service => service.GetBidByIdAsync(bidId))
            .ReturnsAsync(new TenderBidDetailDto { Id = bidId, IsSealed = true });
        fixture.Bids.Setup(service => service.GetBidDocumentsAsync(bidId))
            .ReturnsAsync(Array.Empty<TenderBidDocumentDto>());
        Assert.IsType<NotFoundObjectResult>(await fixture.Controller.DownloadDocument(bidId, Guid.NewGuid()));
    }

    [Fact]
    public async Task ExternalSupplierCannotReadAnotherSuppliersLotCollection()
    {
        var fixture = new Fixture();
        var bidId = Guid.NewGuid();
        fixture.CurrentUser.SetupGet(user => user.IsExternalUser).Returns(true);
        fixture.BusinessPartners.Setup(repo => repo.GetByUserIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BusinessPartner { Id = Guid.NewGuid() });
        fixture.Bids.Setup(service => service.GetBidByIdAsync(bidId))
            .ReturnsAsync(new TenderBidDetailDto { Id = bidId, BusinessPartnerId = Guid.NewGuid() });
        var result = await fixture.Controller.GetBidLots(bidId);
        Assert.IsType<NotFoundResult>(result.Result);
        fixture.Bids.Verify(service => service.GetBidLotsAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetBidsForwardsExactTenderFilter()
    {
        var fixture = new Fixture();
        var tenderId = Guid.NewGuid();
        var expected = new ErpSystem.Core.DTOs.Common.PagedResult<TenderBidSummaryDto>
        {
            Items = [], TotalCount = 0, Page = 1, PageSize = 10
        };
        fixture.Bids.Setup(service => service.GetBidsAsync(
                1, 10, null, "Evaluated", tenderId))
            .ReturnsAsync(expected);

        var response = await fixture.Controller.GetBids(1, 10, "Evaluated", tenderId);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(expected, ok.Value);
        fixture.Bids.VerifyAll();
    }

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

    [Fact]
    public async Task CreateBidReturnsStructuredForbiddenWhenSupplierEvidenceCannotBeRead()
    {
        var fixture = new Fixture();
        var tenderId = Guid.NewGuid();
        fixture.Bids.Setup(item => item.CreateBidAsync(It.Is<CreateTenderBidDto>(dto =>
                dto.TenderId == tenderId)))
            .ThrowsAsync(new ProcurementSupplierEvidencePackAuthorizationException(
                "Supplier evidence is not linked to the current supplier account."));

        var result = await fixture.Controller.CreateBid(new CreateTenderBidDto
        {
            TenderId = tenderId,
            Items =
            [
                new CreateTenderBidItemDto
                {
                    TenderItemId = Guid.NewGuid(),
                    OfferedQuantity = 1m,
                    UnitPrice = 0m
                }
            ]
        });

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(forbidden.Value);
        Assert.Equal("SUPPLIER_BID_EVIDENCE_ACCESS_FORBIDDEN", problem.Extensions["code"]);
        Assert.Equal("trace-tender-bid", problem.Extensions["correlationId"]);
        Assert.Equal("/api/procurement/TenderBids", problem.Instance);
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
            Controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-tender-bid"
                }
            };
            Controller.HttpContext.Request.Path = "/api/procurement/TenderBids";
        }
    }
}
