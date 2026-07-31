using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
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

public sealed class PurchaseOrderReceiptInspectionControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedLifecycleRoutes()
    {
        var type = typeof(PurchaseOrderReceiptsController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/[controller]");
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .ToDictionary(method => method.Name);
        methods.Keys.Should().Contain([
            "GetInspectionControl", "GetSupplierInspectionControl", "InitializeInspection",
            "SaveInspection", "SubmitInspection", "DecideInspection",
            "AcknowledgeInspection", "ResolveInspection", "CloseInspection"]);
        HttpTemplate(methods["GetSupplierInspectionControl"]).Should()
            .Be("inspection-control/supplier");
        HttpTemplate(methods["AcknowledgeInspection"]).Should()
            .Be("inspection-control/{caseId:guid}/supplier-acknowledgement");
    }

    [Fact]
    public void InternalAndSupplierMutationsAreSeparatedByDedicatedEndpoints()
    {
        var type = typeof(PurchaseOrderReceiptsController);

        HttpTemplate(type.GetMethod("SaveInspection")!).Should()
            .Be("{id:guid}/inspection-control");
        HttpTemplate(type.GetMethod("DecideInspection")!).Should()
            .Be("inspection-control/{caseId:guid}/decision");
        HttpTemplate(type.GetMethod("ResolveInspection")!).Should()
            .Be("inspection-control/{caseId:guid}/resolution");
        HttpTemplate(type.GetMethod("CloseInspection")!).Should()
            .Be("inspection-control/{caseId:guid}/close");
    }

    [Fact]
    public async Task InspectionMutationReturnsStructuredForbiddenForCreator()
    {
        var receiptId = Guid.NewGuid();
        var inspection = new Mock<IProcurementReceiptInspectionService>();
        var readiness = new ProcurementPurchaseOrderSodReadinessDto
        {
            PurchaseOrderId = Guid.NewGuid(),
            CanReceive = false,
            Code = "PO_SOD_RECEIPT_BLOCKED",
            Message = "The PO creator cannot initialize receipt inspection.",
            ReceiptActionCoverage = ["InitializeReceiptInspection"]
        };
        inspection.Setup(item => item.InitializeAsync(
                receiptId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementPurchaseOrderSodBlockedException(
                readiness.Code,
                readiness.Message,
                readiness));
        var controller = new PurchaseOrderReceiptsController(
            null!,
            null!,
            null!,
            inspection.Object,
            NullLogger<PurchaseOrderReceiptsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.InitializeInspection(receiptId);

        var forbidden = result.Result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = forbidden.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("PO_SOD_RECEIPT_BLOCKED");
        problem.Extensions["readiness"].Should().BeSameAs(readiness);
    }

    private static string? HttpTemplate(MethodInfo method) =>
        method.GetCustomAttributes<HttpMethodAttribute>().Single().Template;
}
