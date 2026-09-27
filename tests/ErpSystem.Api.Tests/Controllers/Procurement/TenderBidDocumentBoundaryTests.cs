using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class TenderBidDocumentBoundaryTests
{
    [Fact]
    public async Task ItemRemovalCannotUseAnItemFromADifferentRouteBid()
    {
        var bidId = Guid.NewGuid();
        var service = new Mock<ITenderBidService>();
        service.Setup(value => value.GetBidByIdAsync(bidId)).ReturnsAsync(new TenderBidDetailDto { Id = bidId });
        var controller = Create(service.Object, Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>());
        (await controller.DeleteBidItem(bidId, Guid.NewGuid())).Should().BeOfType<NotFoundResult>();
        service.Verify(value => value.DeleteBidItemAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ItemDocumentConflictDuringDraftUpdateRetainsItsProblemCode()
    {
        var bidId = Guid.NewGuid();
        var service = new Mock<ITenderBidService>();
        service.Setup(value => value.GetBidByIdAsync(bidId)).ReturnsAsync(new TenderBidDetailDto { Id = bidId });
        service.Setup(value => value.UpdateBidAsync(bidId, It.IsAny<UpdateTenderBidDto>()))
            .ThrowsAsync(new TenderBidInitiationValidationException("BID_ITEM_HAS_DOCUMENTS", "Remove supporting documents first."));
        var controller = Create(service.Object, Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>());
        var result = await controller.UpdateBid(bidId, new UpdateTenderBidDto());
        result.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value.Should().BeOfType<ProblemDetails>()
            .Subject.Extensions["code"].Should().Be("BID_ITEM_HAS_DOCUMENTS");
    }

    [Fact]
    public async Task DeadlineRejectionPrecedesFileStorageAndReturnsProblemCode()
    {
        var bidId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var service = new Mock<ITenderBidService>();
        service.Setup(value => value.GetBidByIdAsync(bidId)).ReturnsAsync(new TenderBidDetailDto { Id = bidId });
        service.Setup(value => value.ValidateBidDocumentChangeAsync(bidId, itemId))
            .ThrowsAsync(new TenderBidInitiationValidationException("BID_DOCUMENT_CUTOFF", "Submission deadline reached."));
        var files = new Mock<IControlledFileUploadService>(MockBehavior.Strict);
        var central = new Mock<ICentralDocumentRepositoryFileService>(MockBehavior.Strict);
        var controller = Create(service.Object, files.Object, central.Object);
        using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "spec.pdf");
        var result = await controller.UploadDocument(bidId, file, "Other", null, itemId);
        var problem = result.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("BID_DOCUMENT_CUTOFF");
        problem.Detail.Should().Be("Submission deadline reached.");
        files.VerifyNoOtherCalls();
        central.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeletePassesRouteBidToTheServiceAndPreservesAuthorizationError()
    {
        var bidId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var service = new Mock<ITenderBidService>();
        service.Setup(value => value.GetBidByIdAsync(bidId)).ReturnsAsync(new TenderBidDetailDto { Id = bidId });
        service.Setup(value => value.DeleteBidDocumentAsync(documentId, bidId)).ThrowsAsync(new UnauthorizedAccessException("Not your bid."));
        var controller = Create(service.Object, Mock.Of<IControlledFileUploadService>(), Mock.Of<ICentralDocumentRepositoryFileService>());
        var result = (await controller.DeleteDocument(bidId, documentId)).Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        result.Value.Should().BeOfType<ProblemDetails>().Subject.Extensions["code"].Should().Be("BID_DOCUMENT_FORBIDDEN");
        service.Verify(value => value.DeleteBidDocumentAsync(documentId, bidId), Times.Once);
        service.Verify(value => value.DeleteBidDocumentAsync(documentId), Times.Never);
    }

    private static TenderBidsController Create(ITenderBidService service, IControlledFileUploadService files, ICentralDocumentRepositoryFileService central)
        => new(service, Mock.Of<IBusinessPartnerRepository>(), Mock.Of<IBusinessPartnerUserRepository>(),
            Mock.Of<ITenderAssignmentRepository>(), Mock.Of<ITenderPaymentRepository>(), Mock.Of<ICurrentUserProvider>(),
            files, central, NullLogger<TenderBidsController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
}
