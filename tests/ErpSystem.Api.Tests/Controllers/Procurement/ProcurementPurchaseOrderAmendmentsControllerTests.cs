using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentsControllerTests
{
    [Fact]
    public void ControllerIsAuthenticatedAndExposesSeparateInternalAndSupplierRoutes()
    {
        var type = typeof(ProcurementPurchaseOrderAmendmentsController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/purchase-order-amendments");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "GetOverview",
                "Create",
                "Submit",
                "Decide",
                "Dispatch",
                "Acknowledge",
                "GetExternalOverview",
                "AcknowledgeExternal"
            ]);
    }

    [Fact]
    public async Task TenantAndLifecycleFailuresMapToStructuredProblems()
    {
        var service = new Mock<IProcurementPurchaseOrderAmendmentService>();
        service.Setup(item => item.GetOverviewAsync(
                Guid.Empty,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderAmendmentNotFoundException(
                "PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND",
                "Missing."));
        service.Setup(item => item.GetExternalOverviewAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderAmendmentAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.SubmitAsync(
                Guid.Empty,
                It.IsAny<ProcurementPurchaseOrderAmendmentLifecycleRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderAmendmentConflictException(
                "PO_AMENDMENT_CONCURRENCY_CONFLICT",
                "Stale."));
        service.Setup(item => item.CreateAsync(
                Guid.Empty,
                It.IsAny<CreateProcurementPurchaseOrderAmendmentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderAmendmentValidationException(
                "PO_AMENDMENT_REASON_REQUIRED",
                "Invalid."));
        var controller = Controller(service);

        AssertProblem(await controller.GetOverview(Guid.Empty, default), 404,
            "PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND");
        AssertProblem(await controller.GetExternalOverview(default), 403,
            "PO_AMENDMENT_ACCESS_FORBIDDEN");
        AssertProblem(await controller.Submit(
                Guid.Empty,
                new ProcurementPurchaseOrderAmendmentLifecycleRequest(),
                default),
            409,
            "PO_AMENDMENT_CONCURRENCY_CONFLICT");
        AssertProblem(await controller.Create(
                Guid.Empty,
                new CreateProcurementPurchaseOrderAmendmentRequest(),
                default),
            422,
            "PO_AMENDMENT_REASON_REQUIRED");
    }

    [Fact]
    public async Task ExternalAcknowledgementUsesSupplierScopedServiceFlag()
    {
        var dispatchId = Guid.NewGuid();
        var request = new AcknowledgeProcurementPurchaseOrderAmendmentRequest
        {
            AcknowledgementChannel = "SupplierPortal",
            AcknowledgementReference = "ACK-0406",
            EvidenceReference = "DMS-ACK-0406",
            IdempotencyKey = "ack-0406"
        };
        var service = new Mock<IProcurementPurchaseOrderAmendmentService>();
        service.Setup(item => item.AcknowledgeAsync(
                dispatchId,
                request,
                "corr-0406",
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementPurchaseOrderAmendmentAcknowledgementDto
            {
                Id = Guid.NewGuid(),
                AcknowledgementReference = "ACK-0406"
            });
        var controller = Controller(service, "corr-0406");

        var result = await controller.AcknowledgeExternal(
            dispatchId,
            request,
            default);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        service.VerifyAll();
    }

    private static ProcurementPurchaseOrderAmendmentsController Controller(
        Mock<IProcurementPurchaseOrderAmendmentService> service,
        string correlation = "test-correlation")
    {
        var controller =
            new ProcurementPurchaseOrderAmendmentsController(service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        controller.Request.Headers["X-Correlation-ID"] = correlation;
        return controller;
    }

    private static void AssertProblem(
        IActionResult result,
        int status,
        string code)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(status);
        var problem = objectResult.Value.Should()
            .BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(code);
    }
}
