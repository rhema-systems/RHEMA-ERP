using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementReceiptSourceEvidenceControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndUsesReceiptScopedRoutes()
    {
        var type = typeof(ProcurementReceiptSourceEvidenceController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be(
            "api/procurement/purchase-order-receipts/{receiptId:guid}/source-evidence");
        Template(type, nameof(ProcurementReceiptSourceEvidenceController.Get)).Should().BeNull();
        Template(type, nameof(ProcurementReceiptSourceEvidenceController.Upload)).Should().BeNull();
        Template(type, nameof(ProcurementReceiptSourceEvidenceController.Download))
            .Should().Be("{evidenceId:guid}/download");
    }

    [Fact]
    public async Task OverviewDelegatesTenantAndPermissionEnforcementToService()
    {
        var receiptId = Guid.NewGuid();
        var expected = new ProcurementReceiptSourceEvidenceOverviewDto
        {
            ReceiptId = receiptId,
            WaybillReady = true
        };
        var service = new Mock<IProcurementReceiptSourceEvidenceService>();
        service.Setup(item => item.GetOverviewAsync(receiptId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var response = await Controller(service.Object).Get(receiptId);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task UploadForwardsTypedMultipartValuesAndPreservesStableForbiddenResponse()
    {
        var receiptId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var service = new Mock<IProcurementReceiptSourceEvidenceService>();
        service.Setup(item => item.UploadAsync(
                receiptId,
                It.Is<ProcurementReceiptSourceEvidenceUploadCommand>(command =>
                    command.EvidenceKind == ProcurementReceiptSourceEvidenceKind.Waybill &&
                    command.ReferenceNumber == "WB-1001" &&
                    command.ClientRequestId == requestId &&
                    command.Content.SequenceEqual(new byte[] { 1, 2, 3 })),
                "trace-inv-fu-001",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementReceiptSourceEvidenceAuthorizationException(
                "The current user is not permitted to administer receipt evidence."));
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "waybill.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var response = await Controller(service.Object).Upload(receiptId, new()
        {
            EvidenceKind = ProcurementReceiptSourceEvidenceKind.Waybill,
            ReferenceNumber = "WB-1001",
            DocumentDate = new DateTime(2026, 8, 12),
            ClientRequestId = requestId
        }, file);

        var forbidden = response.Result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        JsonSerializer.Serialize(forbidden.Value).Should().Contain("RCV_SOURCE_EVIDENCE_FORBIDDEN");
        service.VerifyAll();
    }

    private static ProcurementReceiptSourceEvidenceController Controller(
        IProcurementReceiptSourceEvidenceService service) => new(
        service,
        NullLogger<ProcurementReceiptSourceEvidenceController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-inv-fu-001" }
        }
    };

    private static string? Template(Type type, string method) =>
        type.GetMethod(method)!.GetCustomAttributes<HttpMethodAttribute>().Single().Template;
}
