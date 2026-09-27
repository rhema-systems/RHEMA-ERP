using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryGlobalSearchTests
{
    [Fact]
    public async Task Counts_search_finds_old_authorized_match_after_denied_matches_and_before_limiting()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var allowed = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "A", Name = "Allowed" };
        var denied = new Warehouse { Id = Guid.NewGuid(), TenantId = tenant, Code = "B", Name = "Denied" };
        var location = Guid.NewGuid();
        PhysicalCount Count(string number, Warehouse warehouse, Guid? bin = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenant, CountNumber = number, Warehouse = warehouse,
            WarehouseId = warehouse.Id, LocationId = bin, CountDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, RowVersion = [1]
        };
        var match = Count("FIND-OLD", allowed, location);
        match.CountDate = DateTime.UtcNow.AddYears(-2);
        match.CreatedAt = match.CountDate;
        var foreign = Count("FIND-FOREIGN", allowed, location); foreign.TenantId = Guid.NewGuid();
        var deleted = Count("FIND-DELETED", allowed, location); deleted.IsDeleted = true;
        db.AddRange(allowed, denied, match, foreign, deleted);
        db.AddRange(Enumerable.Range(0, 55).Select(index => Count($"FIND-DENIED-{index}", denied)));
        db.AddRange(Enumerable.Range(0, 60).Select(index => Count($"OTHER-{index}", allowed, location)));
        await db.SaveChangesAsync();
        // Keep the target outside both the normal three-month list and the first search batch.
        match.CreatedAt = DateTime.UtcNow.AddYears(-2);
        await db.SaveChangesAsync();
        using var unit = new UnitOfWork(db);
        var actor = new Mock<ICurrentUserService>(); actor.SetupGet(value => value.TenantId).Returns(tenant);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = request.WarehouseId == allowed.Id && request.LocationId == location
                });
        var service = new PhysicalCountService(new PhysicalCountRepository(db),
            Mock.Of<IPhysicalCountItemRepository>(), Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(),
            Mock.Of<IWarehouseQuantityRepository>(), unit, actor.Object, access.Object,
            Mock.Of<IStockAdjustmentService>(), Mock.Of<IProcurementControlEventService>(),
            NullLogger<PhysicalCountService>.Instance, Mock.Of<IWarehouseDefaultLocationService>());

        var results = await service.SearchAsync(" FIND ", 1);

        results.Should().ContainSingle().Which.Id.Should().Be(match.Id);
        access.Verify(value => value.CheckCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" && request.RequireLocationScope),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(56));
        (await service.SearchAsync("F", 8)).Should().BeEmpty();
        (await service.SearchAsync(new string('x', 101), 8)).Should().BeEmpty();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var abandoned = () => service.SearchAsync("FIND", 1, cancelled.Token);
        await abandoned.Should().ThrowAsync<OperationCanceledException>();
    }
}
