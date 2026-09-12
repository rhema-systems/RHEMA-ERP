using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class WarehouseDefaultLocationControllerTests
{
    [Fact]
    public async Task Current_default_cannot_be_deleted_without_a_replacement()
    {
        var f = new Fixture();
        var result = await f.Controller.Delete(f.Location.Id);
        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(
            "Select another default bin for this warehouse before deleting the current default.");
        f.Location.IsDeleted.Should().BeFalse();
        f.Unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Current_default_cannot_be_cleared_or_deactivated(bool isDefault, bool isActive)
    {
        var f = new Fixture();
        var result = await f.Controller.Update(f.Location.Id, new UpdateWarehouseLocationDto
        {
            WarehouseId = f.Warehouse.Id, LocationCode = f.Location.LocationCode, IsDefault = isDefault, IsActive = isActive
        });
        result.Result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(
            "Select another default bin for this warehouse before clearing, moving or deactivating the current default.");
        f.Location.IsDefault.Should().BeTrue(); f.Location.IsActive.Should().BeTrue();
        f.Unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Selected_default_is_exposed_in_location_details()
    {
        var f = new Fixture();
        var result = await f.Controller.GetById(f.Location.Id);
        var dto = result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<WarehouseLocationDto>().Which;
        dto.IsDefault.Should().BeTrue(); dto.WarehouseId.Should().Be(f.Warehouse.Id);
    }

    [Theory]
    [InlineData("Zone", false)]
    [InlineData("Bin", true)]
    public async Task Creating_a_special_default_is_rejected_before_saving(string locationType, bool consignment)
    {
        var f = new Fixture();
        var result = await f.Controller.Create(new CreateWarehouseLocationDto
        {
            WarehouseId = f.Warehouse.Id, LocationCode = "Invalid", IsDefault = true,
            LocationType = locationType, IsConsignmentBin = consignment
        });
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        f.Unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Warehouse Warehouse { get; }
        public WarehouseLocation Location { get; }
        public Mock<IUnitOfWork> Unit { get; } = new();
        public WarehouseLocationsController Controller { get; }
        public Fixture()
        {
            var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
            Warehouse = new Warehouse { TenantId = tenant, Code = "TEST", Name = "Warehouse", IsActive = true };
            Location = new WarehouseLocation { TenantId = tenant, WarehouseId = Warehouse.Id, LocationCode = "DEFAULT", IsDefault = true, IsActive = true };
            var locations = new Mock<IWarehouseLocationRepository>(); locations.Setup(x => x.GetByIdAsync(Location.Id)).ReturnsAsync(Location);
            var warehouses = new Mock<IWarehouseRepository>(); warehouses.Setup(x => x.GetByIdAsync(Warehouse.Id)).ReturnsAsync(Warehouse);
            var user = new Mock<ICurrentUserProvider>(); user.SetupGet(x => x.TenantId).Returns(tenant); user.SetupGet(x => x.UserId).Returns(actor);
            Controller = new WarehouseLocationsController(locations.Object, warehouses.Object, Unit.Object, user.Object,
                NullLogger<WarehouseLocationsController>.Instance, Mock.Of<IInventoryNegativeStockControlService>(),
                defaultLocations: Mock.Of<IWarehouseDefaultLocationService>())
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        }
    }
}
