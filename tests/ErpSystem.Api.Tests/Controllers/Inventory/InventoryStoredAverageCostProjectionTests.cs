using System.Reflection;
using AutoMapper;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Api.Mapping;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryStoredAverageCostProjectionTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void Catalogue_and_detail_map_the_stored_item_average_without_recalculation()
    {
        var mapper = new MapperConfiguration(config => config.AddProfile<InventoryMappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var item = Item(1907.09m);
        item.StandardCost = 123m;
        mapper.Map<InventoryItemDto>(item).AverageCost.Should().Be(1907.09m);
        mapper.Map<InventoryItemDetailDto>(item).AverageCost.Should().Be(1907.09m);
    }

    [Fact]
    public void Count_returns_saved_valuation_and_current_item_average_as_distinct_fields()
    {
        var item = Item(1907.09m);
        var line = Line(item);
        var result = MapCount(line, true);
        result.CountUnitCost.Should().Be(1918.85m);
        result.ItemAverageCost.Should().Be(1907.09m);
        item.AverageCost = 1800m;
        MapCount(line, true).ItemAverageCost.Should().Be(1800m);
        line.UnitCost.Should().Be(1918.85m, "displaying a reference cost must not rewrite the count snapshot");
    }

    [Fact]
    public void Blind_count_redacts_both_costs_server_side()
    {
        var result = MapCount(Line(Item(1907.09m)), false);
        result.CountUnitCost.Should().BeNull();
        result.ItemAverageCost.Should().BeNull();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-tenant")]
    [InlineData("wrong-item")]
    [InlineData("deleted")]
    public void Count_and_warehouse_do_not_expose_an_unavailable_or_unrelated_item_average(string condition)
    {
        var item = Item(1907.09m);
        var line = Line(item);
        var warehouse = Warehouse(item);
        if (condition == "missing") { line.InventoryItem = null!; warehouse.InventoryItem = null!; }
        if (condition == "foreign-tenant") item.TenantId = Guid.NewGuid();
        if (condition == "wrong-item") item.Id = Guid.NewGuid();
        if (condition == "deleted") item.IsDeleted = true;
        MapCount(line, true).ItemAverageCost.Should().BeNull();
        MapWarehouse(warehouse).ItemAverageCost.Should().BeNull();
    }

    [Fact]
    public void Warehouse_retains_its_own_cost_and_does_not_replace_a_zero_item_average()
    {
        var item = Item(0m);
        var warehouse = Warehouse(item);
        var result = MapWarehouse(warehouse);
        result.AverageCost.Should().Be(1918.85m);
        result.ItemAverageCost.Should().Be(0m);
        MapCount(Line(item), true).ItemAverageCost.Should().Be(0m);
    }

    private static InventoryItem Item(decimal average) => new() { Id = Guid.NewGuid(), TenantId = Tenant, AverageCost = average };
    private static PhysicalCountItem Line(InventoryItem item) => new() { TenantId = Tenant, InventoryItemId = item.Id, InventoryItem = item, UnitCost = 1918.85m };
    private static WarehouseQuantity Warehouse(InventoryItem item) => new() { TenantId = Tenant, InventoryItemId = item.Id, InventoryItem = item, AverageCost = 1918.85m };
    private static PhysicalCountItemDto MapCount(PhysicalCountItem line, bool visible) => (PhysicalCountItemDto)typeof(PhysicalCountService)
        .GetMethod("MapItemToDto", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object?[] { line, visible, null })!;
    private static WarehouseItemDto MapWarehouse(WarehouseQuantity warehouse) => (WarehouseItemDto)typeof(WarehouseItemsController)
        .GetMethod("MapToDto", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { warehouse })!;
}
