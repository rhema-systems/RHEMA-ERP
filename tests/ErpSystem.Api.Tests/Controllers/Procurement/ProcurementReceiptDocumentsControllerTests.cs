using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementReceiptDocumentsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedTenantSafeLifecycleRoutes()
    {
        var type = typeof(ProcurementReceiptDocumentsController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/ProcurementReceiptDocuments");

        Route(type, nameof(ProcurementReceiptDocumentsController.GetOverview)).Should().Be("receipt/{receiptId:guid}");
        Route(type, nameof(ProcurementReceiptDocumentsController.Ensure)).Should().Be("receipt/{receiptId:guid}/ensure");
        Route(type, nameof(ProcurementReceiptDocumentsController.Reconcile)).Should().Be("receipt/{receiptId:guid}/reconcile");
        Route(type, nameof(ProcurementReceiptDocumentsController.Sign)).Should().Be("{documentId:guid}/sign");
        Route(type, nameof(ProcurementReceiptDocumentsController.Issue)).Should().Be("{documentId:guid}/issue");
        Route(type, nameof(ProcurementReceiptDocumentsController.Cancel)).Should().Be("{documentId:guid}/cancel");
        Route(type, nameof(ProcurementReceiptDocumentsController.Download)).Should().Be("{documentId:guid}/download");
    }

    [Fact]
    public async Task OverviewDelegatesOnlyToTenantEnforcingService()
    {
        var receiptId = Guid.NewGuid();
        var expected = new ProcurementReceiptDocumentOverviewDto { PurchaseOrderReceiptId = receiptId };
        var service = new Mock<IProcurementReceiptDocumentService>();
        service.Setup(item => item.GetOverviewAsync(receiptId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = Controller(service.Object);

        var response = await controller.GetOverview(receiptId);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task AuthorizationAndStableConcurrencyCodesAreReturnedWithoutMutationFallback()
    {
        var documentId = Guid.NewGuid();
        var service = new Mock<IProcurementReceiptDocumentService>();
        service.Setup(item => item.IssueAsync(documentId, It.IsAny<IssueProcurementReceiptDocumentRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementReceiptDocumentAuthorizationException("Cross-tenant access denied."));
        var forbiddenController = Controller(service.Object);

        var forbidden = await forbiddenController.Issue(documentId, new IssueProcurementReceiptDocumentRequest());
        forbidden.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        service.Reset();
        service.Setup(item => item.SignAsync(documentId, It.IsAny<SignProcurementReceiptDocumentRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var conflictController = Controller(service.Object);

        var conflict = await conflictController.Sign(documentId, new SignProcurementReceiptDocumentRequest());
        var result = conflict.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        JsonSerializer.Serialize(result.Value).Should().Contain("RCV_DOCUMENT_ROW_VERSION_CONFLICT");
    }

    [Fact]
    public async Task DomainConflictPreservesItsMachineReadableCode()
    {
        var documentId = Guid.NewGuid();
        var service = new Mock<IProcurementReceiptDocumentService>();
        service.Setup(item => item.CancelAsync(documentId, It.IsAny<CancelProcurementReceiptDocumentRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementReceiptDocumentConflictException(
                "RCV_DOCUMENT_CONCURRENT_CREATE",
                "The register was created concurrently."));
        var controller = Controller(service.Object);

        var response = await controller.Cancel(documentId, new CancelProcurementReceiptDocumentRequest());

        var conflict = response.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        JsonSerializer.Serialize(conflict.Value).Should().Contain("RCV_DOCUMENT_CONCURRENT_CREATE");
    }

    [Fact]
    public async Task DownloadReturnsOnlyTheServiceAuthorizedCentralDmsRendition()
    {
        var documentId = Guid.NewGuid();
        var service = new Mock<IProcurementReceiptDocumentService>();
        service.Setup(item => item.DownloadAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementReceiptDocumentFileDto
            {
                Content = [1, 2, 3], ContentType = "application/pdf", FileName = "GRN-1.pdf"
            });
        var controller = Controller(service.Object);

        var response = await controller.Download(documentId);

        var file = response.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("GRN-1.pdf");
    }

    private static ProcurementReceiptDocumentsController Controller(IProcurementReceiptDocumentService service) => new(
        service,
        NullLogger<ProcurementReceiptDocumentsController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-0509" }
        }
    };

    private static string? Route(Type type, string method) =>
        type.GetMethod(method)!.GetCustomAttributes<HttpMethodAttribute>().Single().Template;
}
