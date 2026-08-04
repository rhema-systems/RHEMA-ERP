using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryValuationReconciliationsControllerSecurityTests
{
    [Fact, Trait("Batch", "TDC-0613")]
    public void Controller_and_routes_use_internal_and_existing_finance_permissions()
    {
        var type = typeof(InventoryValuationReconciliationsController);
        type.GetCustomAttribute<AuthorizeAttribute>(true)!.Policy.Should().Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/inventory/valuation-reconciliations");
        type.GetMethod(nameof(InventoryValuationReconciliationsController.Get))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewFinance);
        type.GetMethod(nameof(InventoryValuationReconciliationsController.Generate))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.RunFinanceReports);
        type.GetMethod(nameof(InventoryValuationReconciliationsController.Freeze))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.CloseAccountingPeriods);
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Generate_propagates_correlation_and_delegates_to_tenant_service()
    {
        var service = new Mock<IInventoryValuationReconciliationService>();
        var expected = new InventoryValuationReconciliationDto
        { Id = Guid.NewGuid(), ReconciliationNumber = "IVR-TEST-001" };
        service.Setup(value => value.GenerateAsync(
                It.Is<GenerateInventoryValuationReconciliationRequest>(request =>
                    request.CorrelationId == "tdc0613-api"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Create(service.Object);
        controller.Request.Headers["X-Correlation-ID"] = "tdc0613-api";

        var result = await controller.Generate(new GenerateInventoryValuationReconciliationRequest
        {
            FiscalPeriodId = Guid.NewGuid(), ToleranceAmount = 0.01m, IdempotencyKey = "generate"
        }, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(expected);
        service.VerifyAll();
    }

    [Fact, Trait("Batch", "TDC-0613")]
    public async Task Unknown_or_cross_tenant_snapshot_returns_structured_404()
    {
        var service = new Mock<IInventoryValuationReconciliationService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryValuationReconciliationNotFoundException("Not found."));
        var result = await Create(service.Object).GetById(Guid.NewGuid(), CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Extensions["code"].Should().Be("INV_VALUATION_RECONCILIATION_NOT_FOUND");
    }

    [Theory, Trait("Batch", "TDC-0613")]
    [InlineData("INV_VALUATION_RECONCILIATION_CONCURRENCY_CONFLICT", 409)]
    [InlineData("INV_VALUATION_RECONCILIATION_SOD", 403)]
    [InlineData("INV_VALUATION_RECONCILIATION_NOT_CLEAN", 422)]
    public async Task Freeze_returns_structured_control_errors(string code, int status)
    {
        var service = new Mock<IInventoryValuationReconciliationService>();
        service.Setup(value => value.FreezeAsync(It.IsAny<Guid>(),
                It.IsAny<FreezeInventoryValuationReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryValuationReconciliationException(code, "Controlled rejection."));
        var result = await Create(service.Object).Freeze(Guid.NewGuid(),
            new FreezeInventoryValuationReconciliationRequest
            { RowVersion = "AQ==", Reason = "Review", IdempotencyKey = "freeze" }, CancellationToken.None);

        var response = result.Result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(status);
        response.Value.Should().BeOfType<ProblemDetails>().Which.Extensions["code"].Should().Be(code);
    }

    private static InventoryValuationReconciliationsController Create(
        IInventoryValuationReconciliationService service) => new(service,
        NullLogger<InventoryValuationReconciliationsController>.Instance)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
}
