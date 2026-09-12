using AutoMapper;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class WarehouseInventoryCostTests
{
    [Theory]
    [InlineData(ValuationMethod.WeightedAverage)]
    [InlineData(ValuationMethod.FIFO)]
    [InlineData(ValuationMethod.StandardCost)]
    public async Task Warehouse_picker_uses_current_warehouse_cost_not_standard_or_other_warehouse_average(ValuationMethod method)
    {
        var tenant = Guid.NewGuid();
        var warehouse = new Warehouse { TenantId = tenant, Code = "COST", Name = "Cost warehouse" };
        var item = new InventoryItem { TenantId = tenant, ItemCode = "SKU-001", Name = "PVC Pipe", UnitOfMeasure = "EA",
            ValuationMethod = method, StandardCost = 2000, AverageCost = 1700, LastPurchaseCost = 1800 };
        var quantity = new WarehouseQuantity { TenantId = tenant, WarehouseId = warehouse.Id, InventoryItemId = item.Id,
            InventoryItem = item, CurrentStock = 300, AvailableStock = 290, AllocatedStock = 10, AverageCost = 1918.85m };
        var warehouses = new Mock<IWarehouseRepository>();
        var quantities = new Mock<IWarehouseQuantityRepository>();
        warehouses.Setup(value => value.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        quantities.Setup(value => value.GetItemsWithStockAsync(warehouse.Id, 1)).ReturnsAsync(new[] { quantity });
        var controller = CreateController(warehouses, quantities);

        var response = await controller.GetInventoryByWarehouse(warehouse.Id, 1);
        var result = Assert.Single(Assert.IsAssignableFrom<IEnumerable<WarehouseInventoryDto>>(Assert.IsType<OkObjectResult>(response.Result).Value));

        Assert.Equal(1918.85m, result.UnitCost);
        Assert.Equal(290m, result.AvailableStock);
        Assert.Equal(300m, result.CurrentStock);
        Assert.Equal(10m, result.AllocatedStock);
        Assert.Equal(item.Id, result.InventoryItemId);
        Assert.Equal(2000m, item.StandardCost);
        Assert.Equal(1700m, item.AverageCost);
        quantities.Verify(value => value.GetItemsWithStockAsync(warehouse.Id, 1), Times.Once);
    }

    [Fact]
    public async Task Zero_warehouse_value_does_not_invent_standard_cost_or_borrow_another_warehouse_cost()
    {
        var warehouse = new Warehouse { Code = "ZERO", Name = "Zero cost warehouse" };
        var warehouses = new Mock<IWarehouseRepository>();
        var quantities = new Mock<IWarehouseQuantityRepository>();
        warehouses.Setup(value => value.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        quantities.Setup(value => value.GetItemsWithStockAsync(warehouse.Id, null)).ReturnsAsync(new[] {
            new WarehouseQuantity { WarehouseId = warehouse.Id, AverageCost = 0, CurrentStock = 1,
                InventoryItem = new InventoryItem { ItemCode = "ZERO", Name = "Zero value", StandardCost = 2000, AverageCost = 1918.85m } }
        });

        var response = await CreateController(warehouses, quantities).GetInventoryByWarehouse(warehouse.Id);

        var result = Assert.Single(Assert.IsAssignableFrom<IEnumerable<WarehouseInventoryDto>>(Assert.IsType<OkObjectResult>(response.Result).Value));
        Assert.Equal(0m, result.UnitCost);
    }

    [Fact]
    public async Task Missing_warehouse_does_not_query_or_disclose_inventory()
    {
        var warehouses = new Mock<IWarehouseRepository>();
        var quantities = new Mock<IWarehouseQuantityRepository>(MockBehavior.Strict);
        var response = await CreateController(warehouses, quantities).GetInventoryByWarehouse(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(response.Result);
        quantities.VerifyNoOtherCalls();
    }

    private static InventoryItemsController CreateController(Mock<IWarehouseRepository> warehouses, Mock<IWarehouseQuantityRepository> quantities)
        => new(Mock.Of<ICurrentUserProvider>(), Mock.Of<IInventoryItemRepository>(), Mock.Of<IStockMovementRepository>(),
            Mock.Of<IInventoryLocationRepository>(), Mock.Of<IInventoryAllocationRepository>(), Mock.Of<IWarehouseLocationRepository>(),
            warehouses.Object, quantities.Object, Mock.Of<IInventoryMovementRepository>(), Mock.Of<IInventoryBalanceRepository>(),
            Mock.Of<IItemUnitOfMeasureRepository>(), Mock.Of<IUnitOfMeasureScheduleRepository>(), Mock.Of<IProcurementAccessControlService>(),
            Mock.Of<IMapper>(), NullLogger<InventoryItemsController>.Instance);
}
