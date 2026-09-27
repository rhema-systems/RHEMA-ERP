using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class InventoryGlobalSearchControllerTests
{
    [Fact]
    public async Task Completing_with_outstanding_receipts_returns_actionable_conflict_instead_of_server_error()
    {
        var requisitionId = Guid.NewGuid();
        var service = new Mock<IInventoryRequisitionService>();
        service.Setup(value => value.CompleteAsync(requisitionId)).ThrowsAsync(
            new InventoryIssueControlException("INV_ISSUE_RECEIPT_OUTSTANDING",
                "Receive all outstanding issue voucher quantities before completing this requisition."));
        var controller = new InventoryRequisitionsController(service.Object, Mock.Of<IWorkflowService>(),
            Mock.Of<IDocumentOutputService>(), Mock.Of<IInventoryReturnControlService>(),
            NullLogger<InventoryRequisitionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "receipt-completion-check" } }
        };

        var result = (await controller.Complete(requisitionId)).Should().BeOfType<ObjectResult>().Which;

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var body = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        body.GetProperty("code").GetString().Should().Be("INV_ISSUE_RECEIPT_OUTSTANDING");
        body.GetProperty("message").GetString().Should().Contain("Receive all outstanding");
        body.GetProperty("correlationId").GetString().Should().Be("receipt-completion-check");
        service.Verify(value => value.CompleteAsync(requisitionId), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Warehouse_search_filters_before_limit_and_never_exposes_other_tenants_or_deleted_records()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        Warehouse Warehouse(string code, string name, Guid? owner = null, bool deleted = false) => new()
        {
            Id = Guid.NewGuid(), TenantId = owner ?? tenant, Code = code, Name = name, IsDeleted = deleted
        };
        var match = Warehouse("ZZ", "Needle warehouse");
        db.AddRange(Enumerable.Range(0, 70).Select(index => Warehouse($"A{index:000}", "Other warehouse")));
        db.AddRange(match, Warehouse("AA-FOREIGN", "Needle foreign", Guid.NewGuid()),
            Warehouse("AA-DELETED", "Needle deleted", deleted: true));
        await db.SaveChangesAsync();
        using var unit = new UnitOfWork(db);
        var actor = new Mock<ICurrentUserProvider>(); actor.SetupGet(value => value.TenantId).Returns(tenant);
        var controller = new WarehousesController(new WarehouseRepository(db), unit, actor.Object,
            NullLogger<WarehousesController>.Instance, defaultLocations: Mock.Of<IWarehouseDefaultLocationService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.Search(" Needle ", 1);

        var rows = response.Result.Should().BeOfType<OkObjectResult>().Which.Value
            .Should().BeAssignableTo<IEnumerable<WarehouseDto>>().Subject;
        rows.Should().ContainSingle().Which.Id.Should().Be(match.Id);
        typeof(WarehousesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();
        typeof(WarehousesController).GetMethod(nameof(WarehousesController.Search))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Should().BeEmpty();
        actor.SetupGet(value => value.TenantId).Returns(Guid.Empty);
        (await controller.Search("Needle", 1)).Result.Should().BeOfType<UnauthorizedResult>();
    }
}
