using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task Replenishment_aggregate_reads_and_mutations_require_all_location_scope()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var warehouse = Warehouse(tenantId, "RESTRICTED-BIN");
        var actor = User(tenantId, userId);
        var recommendation = Recommendation(tenantId, warehouse, actor, "IRR-RESTRICTED-BIN");
        context.AddRange(actor, warehouse, recommendation.WarehouseQuantity,
            recommendation.InventoryItem, recommendation);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(warehouse.Id, Guid.NewGuid());
        var service = new InventoryReplenishmentService(unitOfWork, current.Object, access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationService>(),
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementRequisitionLinkageService>(),
            Mock.Of<IPurchaseRequisitionRepository>(), Mock.Of<IPurchaseRequisitionItemRepository>(),
            NullLogger<InventoryReplenishmentService>.Instance);

        var visible = await service.GetAsync(null, null, 10);
        var direct = () => service.GetByIdAsync(recommendation.Id);
        var generate = () => service.GenerateAsync(new GenerateInventoryReplenishmentRequest
        {
            WarehouseId = warehouse.Id,
            DemandWindowDays = 30,
            IdempotencyKey = "restricted-location-generate",
            CorrelationId = "restricted-location-generate"
        });

        visible.Should().BeEmpty();
        await direct.Should().ThrowAsync<InventoryReplenishmentAuthorizationException>();
        await generate.Should().ThrowAsync<InventoryReplenishmentAuthorizationException>();
        access.Verify(value => value.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.WarehouseId == warehouse.Id &&
                request.RequireLocationScope && request.LocationId == null),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
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
    public async Task Project_reservation_reads_page_past_denied_history_to_fill_the_authorized_limit()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actor = User(tenantId, userId);
        var allowedWarehouse = Warehouse(tenantId, "PROJECT-PAGED-A");
        var deniedWarehouse = Warehouse(tenantId, "PROJECT-PAGED-B");
        var allowedLocation = Location(tenantId, allowedWarehouse, "PA-01");
        var deniedLocation = Location(tenantId, deniedWarehouse, "PB-01");
        var allowedItem = Item(tenantId, "PROJECT-PAGED-ITEM-A");
        var deniedItem = Item(tenantId, "PROJECT-PAGED-ITEM-B");
        var now = DateTime.UtcNow;
        var denied = Enumerable.Range(1, 50).Select(index =>
        {
            var value = Allocation(tenantId, actor, deniedWarehouse, deniedLocation, deniedItem, $"RES-DENIED-{index:000}");
            value.AllocationDate = now.AddMinutes(-index);
            return value;
        }).ToList();
        var allowed = Allocation(tenantId, actor, allowedWarehouse, allowedLocation, allowedItem, "RES-ALLOWED-OLDER");
        allowed.AllocationDate = now.AddDays(-2);
        context.AddRange(actor, allowedWarehouse, deniedWarehouse, allowedLocation, deniedLocation, allowedItem, deniedItem);
        context.AddRange(denied);
        context.Add(allowed);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(allowedWarehouse.Id, allowedLocation.Id);
        var userManager = UserManager(context);
        var service = new InventoryProjectReservationService(unitOfWork, current.Object, access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationService>(), userManager.Object,
            NullLogger<InventoryProjectReservationService>.Instance);

        var visible = await service.GetAsync(take: 1);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
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

    [Fact]
    public async Task Negative_stock_history_pages_past_denied_rows_to_fill_the_authorized_limit()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var allowedWarehouse = Warehouse(tenantId, "NEG-PAGED-A");
        var deniedWarehouse = Warehouse(tenantId, "NEG-PAGED-B");
        var allowedLocation = Location(tenantId, allowedWarehouse, "NEG-PA-01");
        var deniedLocation = Location(tenantId, deniedWarehouse, "NEG-PB-01");
        var allowedItem = Item(tenantId, "NEG-PAGED-ITEM-A");
        var deniedItem = Item(tenantId, "NEG-PAGED-ITEM-B");
        var now = DateTime.UtcNow;
        var denied = Enumerable.Range(1, 50).Select(index =>
        {
            var value = Override(tenantId, deniedWarehouse, deniedLocation, deniedItem, $"NEG-DENIED-{index:000}");
            value.ApprovedAtUtc = now.AddMinutes(-index);
            return value;
        }).ToList();
        var allowed = Override(tenantId, allowedWarehouse, allowedLocation, allowedItem, "NEG-ALLOWED-OLDER");
        allowed.ApprovedAtUtc = now.AddDays(-2);
        context.AddRange(allowedWarehouse, deniedWarehouse, allowedLocation, deniedLocation, allowedItem, deniedItem);
        context.AddRange(denied);
        context.Add(allowed);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(allowedWarehouse.Id, allowedLocation.Id);
        var service = new InventoryNegativeStockControlService(unitOfWork, current.Object,
            Mock.Of<IProcurementConfigurationService>(), access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<IInventoryNegativeStockMutationStore>());

        var visible = await service.GetOverridesAsync(1);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
    }

    [Fact]
    public async Task Project_reservation_release_replays_before_stale_row_version_and_verifies_payload_actor()
    {
        await using var context = Context();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actor = User(tenantId, userId);
        var warehouse = Warehouse(tenantId, "PROJECT-REPLAY");
        var location = Location(tenantId, warehouse, "REPLAY-01");
        var item = Item(tenantId, "PROJECT-REPLAY-ITEM");
        var allocation = Allocation(tenantId, actor, warehouse, location, item, "RES-REPLAY");
        allocation.RowVersion = [2];
        var request = new ReleaseInventoryProjectReservationRequest
        {
            Quantity = 1m,
            Reason = "Release unused project stock",
            IdempotencyKey = "project-release-replay",
            CorrelationId = "project-release-replay",
            RowVersion = Convert.ToBase64String([1])
        };
        var hashMethod = typeof(InventoryProjectReservationService).GetMethod(
            "Hash", BindingFlags.Static | BindingFlags.NonPublic)!;
        var payloadHash = (string)hashMethod.Invoke(null, new object[]
        {
            new { id = allocation.Id, request.Quantity, reason = request.Reason }
        })!;
        var action = new InventoryProjectReservationAction
        {
            TenantId = tenantId,
            InventoryAllocationId = allocation.Id,
            InventoryAllocation = allocation,
            Sequence = 1,
            ActionType = InventoryProjectReservationActionType.Released,
            PreviousStatus = InventoryProjectReservationStatus.Reserved,
            NewStatus = InventoryProjectReservationStatus.Reserved,
            Quantity = request.Quantity,
            ActorUserId = userId,
            ActorUser = actor,
            OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = request.IdempotencyKey,
            PayloadHash = payloadHash,
            CorrelationId = request.CorrelationId,
            Reason = request.Reason,
            IntegrityHash = new string('F', 64)
        };
        context.AddRange(actor, warehouse, location, item, allocation, action);
        await context.SaveChangesAsync();
        using var unitOfWork = new UnitOfWork(context);
        var current = CurrentUser(tenantId, userId);
        var access = ScopedAccess(warehouse.Id, location.Id);
        var service = new InventoryProjectReservationService(unitOfWork, current.Object, access.Object,
            Mock.Of<IProcurementControlEventService>(), Mock.Of<INotificationService>(), UserManager(context).Object,
            NullLogger<InventoryProjectReservationService>.Instance);

        var replay = await service.ReleaseAsync(allocation.Id, request);
        var changedPayload = () => service.ReleaseAsync(allocation.Id, new ReleaseInventoryProjectReservationRequest
        {
            Quantity = 2m,
            Reason = request.Reason,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = request.CorrelationId,
            RowVersion = request.RowVersion
        });

        replay.Id.Should().Be(allocation.Id);
        replay.RemainingQuantity.Should().Be(5m);
        await changedPayload.Should().ThrowAsync<InventoryProjectReservationControlException>()
            .Where(error => error.Code == "INV_PROJECT_RESERVATION_IDEMPOTENCY_CONFLICT");
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

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
        ProcurementAccessCapabilityDecisionDto Decide(
            ProcurementAccessCapabilityRequest request,
            string correlation) => new()
        {
            Allowed = request.WarehouseId == warehouseId &&
                (!locationId.HasValue || request.LocationId == locationId || !request.RequireLocationScope),
            PermissionCode = request.PermissionCode,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            CorrelationId = correlation
        };
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                Decide(request, correlation));
        access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                Decide(request, correlation));
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
