using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryReadScopeRegressionTests
{
    [Fact]
    public async Task Replenishment_reads_filter_lists_and_deny_direct_out_of_scope_access()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedWarehouse = Warehouse(tenantId, "ALLOWED");
        var deniedWarehouse = Warehouse(tenantId, "DENIED");
        var actor = User(tenantId, userId);
        var allowed = Recommendation(tenantId, allowedWarehouse, actor, "IRR-ALLOWED");
        var denied = Recommendation(tenantId, deniedWarehouse, actor, "IRR-DENIED");
        context.AddRange(actor, allowedWarehouse, deniedWarehouse, allowed.WarehouseQuantity,
            denied.WarehouseQuantity, allowed.InventoryItem, denied.InventoryItem, allowed, denied);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(allowedWarehouse.Id);
        var service = new InventoryReplenishmentService(unitOfWork, current.Object, access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationService>(),
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementRequisitionLinkageService>(),
            Mock.Of<IPurchaseRequisitionRepository>(), Mock.Of<IPurchaseRequisitionItemRepository>(),
            NullLogger<InventoryReplenishmentService>.Instance);

        var visible = await service.GetAsync(null, null, 10);
        var direct = () => service.GetByIdAsync(denied.Id);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
        await direct.Should().ThrowAsync<InventoryReplenishmentAuthorizationException>();
    }

    [Fact]
    public async Task Project_reservation_reads_filter_by_the_exact_warehouse_and_location_scope()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actor = User(tenantId, userId);
        var allowedWarehouse = Warehouse(tenantId, "PROJECT-A");
        var deniedWarehouse = Warehouse(tenantId, "PROJECT-B");
        var allowedLocation = Location(tenantId, allowedWarehouse, "A-01");
        var deniedLocation = Location(tenantId, deniedWarehouse, "B-01");
        var allowedItem = Item(tenantId, "PROJECT-ITEM-A");
        var deniedItem = Item(tenantId, "PROJECT-ITEM-B");
        var allowed = Allocation(tenantId, actor, allowedWarehouse, allowedLocation, allowedItem, "RES-A");
        var denied = Allocation(tenantId, actor, deniedWarehouse, deniedLocation, deniedItem, "RES-B");
        context.AddRange(actor, allowedWarehouse, deniedWarehouse, allowedLocation, deniedLocation,
            allowedItem, deniedItem, allowed, denied);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(allowedWarehouse.Id, allowedLocation.Id);
        var userManager = UserManager(context);
        var service = new InventoryProjectReservationService(unitOfWork, current.Object, access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationService>(), userManager.Object,
            NullLogger<InventoryProjectReservationService>.Instance);

        var visible = await service.GetAsync(take: 10);
        var direct = () => service.GetByIdAsync(denied.Id);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
        await direct.Should().ThrowAsync<InventoryProjectReservationAuthorizationException>();
    }

    [Fact]
    public async Task Negative_stock_history_is_visible_only_to_read_or_override_actors_in_scope()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedWarehouse = Warehouse(tenantId, "NEG-A");
        var deniedWarehouse = Warehouse(tenantId, "NEG-B");
        var allowedLocation = Location(tenantId, allowedWarehouse, "NEG-A-01");
        var deniedLocation = Location(tenantId, deniedWarehouse, "NEG-B-01");
        var allowedItem = Item(tenantId, "NEG-ITEM-A");
        var deniedItem = Item(tenantId, "NEG-ITEM-B");
        var allowed = Override(tenantId, allowedWarehouse, allowedLocation, allowedItem, "NEG-OVR-A");
        var denied = Override(tenantId, deniedWarehouse, deniedLocation, deniedItem, "NEG-OVR-B");
        context.AddRange(allowedWarehouse, deniedWarehouse, allowedLocation, deniedLocation,
            allowedItem, deniedItem, allowed, denied);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(allowedWarehouse.Id, allowedLocation.Id);
        var service = new InventoryNegativeStockControlService(unitOfWork, current.Object,
            Mock.Of<IProcurementConfigurationService>(), access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<IInventoryNegativeStockMutationStore>());

        var visible = await service.GetOverridesAsync(10);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
        access.Verify(value => value.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.inventory.read" ||
                request.PermissionCode == "Inventory.EmergencyOverride"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static Mock<ICurrentUserProvider> CurrentUser(Guid tenantId, Guid userId)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(userId);
        current.SetupGet(value => value.Username).Returns("stores.reader@tenant.test");
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        current.SetupGet(value => value.IsExternalUser).Returns(false);
        return current;
    }

    private static Mock<IProcurementAccessControlService> ScopedAccess(Guid warehouseId, Guid? locationId = null)
    {
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = request.WarehouseId == warehouseId &&
                        (!locationId.HasValue || request.LocationId == locationId),
                    PermissionCode = request.PermissionCode,
                    WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId,
                    CorrelationId = correlation
                });
        return access;
    }

    private static Mock<UserManager<ApplicationUser>> UserManager(ApplicationDbContext context)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var manager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!,
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null!, null!, null!, null!);
        manager.SetupGet(value => value.Users).Returns(context.Users);
        return manager;
    }

    private static ApplicationUser User(Guid tenantId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        UserName = "stores.reader@tenant.test",
        FirstName = "Stores",
        LastName = "Reader",
        IsActive = true
    };

    private static Warehouse Warehouse(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = code, IsActive = true
    };

    private static WarehouseLocation Location(Guid tenantId, Warehouse warehouse, string code) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id, Warehouse = warehouse,
        LocationCode = code, Name = code, IsActive = true
    };

    private static InventoryItem Item(Guid tenantId, string code) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ItemCode = code, Name = code, UnitOfMeasure = "EA"
    };

    private static InventoryReplenishmentRecommendation Recommendation(
        Guid tenantId, Warehouse warehouse, ApplicationUser actor, string number)
    {
        var item = Item(tenantId, $"ITEM-{number}");
        var balance = new WarehouseQuantity
        {
            Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
            InventoryItemId = item.Id, Warehouse = warehouse, InventoryItem = item
        };
        return new InventoryReplenishmentRecommendation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RecommendationNumber = number,
            WarehouseQuantityId = balance.Id, WarehouseQuantity = balance,
            WarehouseId = warehouse.Id, Warehouse = warehouse, InventoryItemId = item.Id, InventoryItem = item,
            DemandWindowDays = 30, DemandFromUtc = DateTime.UtcNow.AddDays(-30), DemandToUtc = DateTime.UtcNow,
            RequiredDateUtc = DateTime.UtcNow.AddDays(1), ValidUntilUtc = DateTime.UtcNow.AddDays(1),
            Explanation = "Scope regression", CalculationSnapshotJson = "{}", CalculationHash = new string('A', 64),
            GeneratedById = actor.Id, GeneratedBy = actor, GeneratedAtUtc = DateTime.UtcNow,
            IdempotencyKey = number, PayloadHash = new string('B', 64), CorrelationId = number,
            RowVersion = [1]
        };
    }

    private static InventoryAllocation Allocation(
        Guid tenantId, ApplicationUser actor, Warehouse warehouse, WarehouseLocation location,
        InventoryItem item, string reference) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id, InventoryItem = item,
        WarehouseId = warehouse.Id, Warehouse = warehouse, LocationId = location.Id, Location = location,
        AllocationType = "ProjectRequisition", ReferenceNumber = reference,
        InventoryRequisitionId = Guid.NewGuid(), InventoryRequisitionItemId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(), DepartmentId = Guid.NewGuid(), AllocatedQuantity = 5,
        RemainingQuantity = 5, AllocationDate = DateTime.UtcNow, ExpirationDate = DateTime.UtcNow.AddDays(1),
        Status = "Active", AllocatedById = actor.Id, AllocatedBy = actor,
        IdempotencyKey = reference, PayloadHash = new string('C', 64), CorrelationId = reference,
        RowVersion = [1]
    };

    private static InventoryNegativeStockOverride Override(
        Guid tenantId, Warehouse warehouse, WarehouseLocation location, InventoryItem item, string reference) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id, InventoryItem = item,
        WarehouseId = warehouse.Id, Warehouse = warehouse, LocationId = location.Id, Location = location,
        ReferenceId = Guid.NewGuid(), ReferenceType = "StockAdjustment", ReferenceNumber = reference,
        Reason = "Independent approved emergency", AuthorizedQuantity = 1,
        ConfigurationProfileId = Guid.NewGuid(), ConfigurationDecisionId = Guid.NewGuid(),
        ConfigurationProfileVersion = 1, DecisionSnapshotHash = new string('D', 64),
        WorkflowInstanceId = Guid.NewGuid(), CentralDocumentVersionId = Guid.NewGuid(),
        FileUploadRecordId = Guid.NewGuid(), EvidenceReference = "DMS-EVIDENCE",
        RequestedById = Guid.NewGuid(), ApprovedById = Guid.NewGuid(), ApprovedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddHours(1), IntegrityHash = new string('E', 64), RowVersion = [1]
    };
}
