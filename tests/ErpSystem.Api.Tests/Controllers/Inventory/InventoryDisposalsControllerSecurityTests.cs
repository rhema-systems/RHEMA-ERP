using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

[Trait("Batch", "TDC-0615")]
public sealed class InventoryDisposalsControllerSecurityTests
{
    [Fact]
    public void Register_is_internal_only_and_exposes_authorized_document_reads_and_mutations()
    {
        var type = typeof(InventoryDisposalsController);
        type.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Policy.Should().Be("InternalOnly");
        type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/inventory/disposals");

        var actions = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        actions.Count(value => value.GetCustomAttribute<HttpGetAttribute>() is not null).Should().Be(4);
        actions.Count(value => value.GetCustomAttribute<HttpPostAttribute>() is not null).Should().Be(10);
        actions.Count(value => value.GetCustomAttribute<HttpPutAttribute>() is not null).Should().Be(1);
        foreach (var action in actions)
            action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public async Task Get_delegates_tenant_filters_and_take_to_the_service()
    {
        var warehouseId = Guid.NewGuid();
        var expected = new List<InventoryDisposalDto>
        {
            new() { Id = Guid.NewGuid(), DisposalNumber = "IDP-TEST-001" }
        };
        var service = new Mock<IInventoryDisposalService>(MockBehavior.Strict);
        service.Setup(value => value.GetAsync(InventoryDisposalStatus.AuditVerified, warehouseId, 75,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Create(service.Object);

        var result = await controller.Get(InventoryDisposalStatus.AuditVerified, warehouseId, 75, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task Create_propagates_the_request_correlation_header()
    {
        var expected = new InventoryDisposalDto { Id = Guid.NewGuid(), DisposalNumber = "IDP-TEST-002" };
        var service = new Mock<IInventoryDisposalService>(MockBehavior.Strict);
        service.Setup(value => value.CreateAsync(
                It.Is<CreateInventoryDisposalRequest>(request => request.CorrelationId == "tdc0615-api"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Create(service.Object);
        controller.Request.Headers["X-Correlation-ID"] = "tdc0615-api";

        var result = await controller.Create(new CreateInventoryDisposalRequest
        {
            WarehouseId = Guid.NewGuid(), Method = InventoryDisposalMethod.WriteOff,
            Reason = "test", IdentificationDetails = "test", IdempotencyKey = "create-test"
        }, CancellationToken.None);

        result.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeEquivalentTo(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task Cross_tenant_or_unknown_case_is_a_structured_404()
    {
        var service = new Mock<IInventoryDisposalService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryDisposalNotFoundException("Not found in the current tenant."));
        var controller = Create(service.Object);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("INV_DISPOSAL_CONCURRENCY_CONFLICT", StatusCodes.Status409Conflict)]
    [InlineData("INV_DISPOSAL_STATE_INVALID", StatusCodes.Status409Conflict)]
    [InlineData("INV_DISPOSAL_EVIDENCE_INVALID", StatusCodes.Status422UnprocessableEntity)]
    public async Task Domain_failures_are_mapped_without_leaking_as_500(string code, int expectedStatus)
    {
        var service = new Mock<IInventoryDisposalService>();
        service.Setup(value => value.SubmitAsync(It.IsAny<Guid>(), It.IsAny<SubmitInventoryDisposalRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryDisposalException(code, "Controlled failure."));
        var controller = Create(service.Object);

        var result = await controller.Submit(Guid.NewGuid(), new SubmitInventoryDisposalRequest
        {
            Comment = "submit", IdempotencyKey = "submit-test", RowVersion = "AQ=="
        }, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData(InventoryDisposalStatus.Identified, InventoryDisposalMethod.WriteOff)]
    [InlineData(InventoryDisposalStatus.PendingApproval, InventoryDisposalMethod.Auction)]
    [InlineData(InventoryDisposalStatus.Cancelled, InventoryDisposalMethod.Donation)]
    [InlineData(InventoryDisposalStatus.Completed, InventoryDisposalMethod.Sale)]
    public async Task Waybill_rejects_unreleased_cases_and_sales(InventoryDisposalStatus status, InventoryDisposalMethod method)
    {
        var service = new Mock<IInventoryDisposalService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryDisposalDto { Status = status, Method = method });
        var result = await Create(service.Object).Waybill(Guid.NewGuid(), CancellationToken.None);
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Waybill_preserves_warehouse_authorization_failure()
    {
        var service = new Mock<IInventoryDisposalService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryDisposalAuthorizationException("Warehouse access denied."));
        var result = await Create(service.Object).Waybill(Guid.NewGuid(), CancellationToken.None);
        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Completed_non_sales_case_generates_a_downloadable_pdf()
    {
        var service = new Mock<IInventoryDisposalService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryDisposalDto
            {
                DisposalNumber = "DISP-TEST-001", Method = InventoryDisposalMethod.Donation,
                Status = InventoryDisposalStatus.Completed, ApprovalRequired = false,
                RequestedByName = "Test preparer", WarehouseCode = "DEMO-PM", WarehouseName = "Test warehouse",
                RequestedAtUtc = new DateTime(2026, 9, 27), BuyerOrRecipient = "Test recipient",
                Lines = [new() { ItemCode = "TEST-1", ItemName = "Test item", Quantity = 2, UnitOfMeasure = "EA", LocationCode = "BIN-01" }]
            });
        var controller = Create(service.Object);
        var result = await controller.Waybill(Guid.NewGuid(), CancellationToken.None);
        var file = result.Result.Should().BeOfType<FileContentResult>().Which;
        file.ContentType.Should().Be("application/pdf");
        file.FileDownloadName.Should().Be("DISP-TEST-001-waybill.pdf");
        System.Text.Encoding.ASCII.GetString(file.FileContents[..5]).Should().Be("%PDF-");
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        var previewPath = Environment.GetEnvironmentVariable("RHEMA_TEST_WAYBILL_PDF");
        if (!string.IsNullOrWhiteSpace(previewPath))
            await File.WriteAllBytesAsync(previewPath, file.FileContents);
    }

    private static InventoryDisposalsController Create(IInventoryDisposalService service)
    {
        var controller = new InventoryDisposalsController(service, NullLogger<InventoryDisposalsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.TraceIdentifier = "tdc0615-controller-test";
        return controller;
    }
}
