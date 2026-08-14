using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryWorkOrderReservationControlTests : IDisposable
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid actorId = Guid.NewGuid();
    private readonly Guid workOrderId = Guid.NewGuid();
    private readonly Guid warehouseId = Guid.NewGuid();
    private readonly Guid locationId = Guid.NewGuid();
    private readonly Guid itemId = Guid.NewGuid();
    private readonly ApplicationDbContext context;
    private readonly UnitOfWork unitOfWork;
    private readonly MutableCurrentUser currentUser;
    private readonly List<ProcurementControlEventWriteRequest> controlEvents = [];
    private readonly InventoryWorkOrderReservationService service;
    private bool accessAllowed = true;

    public InventoryWorkOrderReservationControlTests()
    {
        context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"inv-work-order-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        unitOfWork = new UnitOfWork(context);
        currentUser = new MutableCurrentUser(tenantId, actorId);

        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                Decision(request, correlation));
        access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                Decision(request, correlation));

        var events = new Mock<IProcurementControlEventService>();
        events.Setup(value => value.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
            {
                controlEvents.Add(request);
                return new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, EventKey = request.EventKey,
                    EventType = request.EventType, Action = request.Action, Result = request.Result,
                    SourceType = request.SourceType, SourceId = request.SourceId,
                    SourceReference = request.SourceReference, CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc, RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                };
            });

        var negativeStock = new Mock<IInventoryNegativeStockControlService>();
        negativeStock.Setup(value => value.PrepareDecreaseAsync(
                It.IsAny<InventoryStockDecreaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryStockDecreaseAuthorization());
        negativeStock.Setup(value => value.ClearMutationContextAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        service = new InventoryWorkOrderReservationService(
            unitOfWork, currentUser, access.Object, events.Object, negativeStock.Object);
        SeedAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Reserve_consume_reschedule_and_return_are_atomic_idempotent_and_audited()
    {
        var request = new CreateWorkOrderPartDto
        {
            WorkOrderId = workOrderId, InventoryItemId = itemId, WarehouseId = warehouseId,
            WarehouseLocationId = locationId, QuantityRequired = 5m, UnitCost = 12m,
            Notes = "Planned maintenance material"
        };

        var created = await service.CreateAndReserveAsync(request, "reserve-1", "corr-reserve");
        var replay = await service.CreateAndReserveAsync(request, "reserve-1", "corr-reserve");
        replay.Id.Should().Be(created.Id);
        (await StockAsync()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 10m && value.AllocatedStock == 5m && value.AvailableStock == 5m);
        (await LocationStockAsync()).Should().Match<InventoryLocation>(value =>
            value.Quantity == 10m && value.AllocatedQuantity == 5m && value.AvailableQuantity == 5m);

        var consumed = await service.UpdateAsync(created.Id, new UpdateWorkOrderPartDto
        {
            QuantityRequired = 5m, QuantityUsed = 2m, QuantityReturned = 0m,
            UnitCost = 12m, WarehouseLocationId = locationId, Status = created.Status,
            Notes = "Two units consumed"
        }, "consume-1", "corr-consume");
        consumed.QuantityUsed.Should().Be(2m);
        consumed.Status.Should().Be("Partial");
        (await StockAsync()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 8m && value.AllocatedStock == 3m && value.AvailableStock == 5m);

        var requiredDate = DateTime.UtcNow.AddDays(4);
        var rescheduled = await service.RescheduleAsync(
            workOrderId, requiredDate, "Maintenance window moved.", "reschedule-1", "corr-reschedule");
        rescheduled.AffectedParts.Should().Be(1);

        var returned = await service.ReturnUnusedAsync(created.Id, "return-1", "corr-return");
        returned.Status.Should().Be("Returned");
        returned.QuantityReturned.Should().Be(3m);
        (await StockAsync()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 8m && value.AllocatedStock == 0m && value.AvailableStock == 8m);
        (await LocationStockAsync()).Should().Match<InventoryLocation>(value =>
            value.Quantity == 8m && value.AllocatedQuantity == 0m && value.AvailableQuantity == 8m);

        context.ChangeTracker.Clear();
        var allocation = await context.Set<InventoryAllocation>().SingleAsync();
        allocation.Status.Should().Be("Cancelled");
        allocation.ConsumedQuantity.Should().Be(2m);
        allocation.RemainingQuantity.Should().Be(0m);
        var actions = await context.Set<InventoryWorkOrderReservationAction>()
            .OrderBy(value => value.Sequence).ToListAsync();
        actions.Select(value => value.ActionType).Should().Equal("Reserve", "Update", "Reschedule", "Return");
        actions.Select(value => value.Sequence).Should().Equal(1, 2, 3, 4);
        actions.Skip(1).Should().OnlyContain(value => value.PreviousHash != null);
        actions.Should().OnlyContain(value => value.IntegrityHash.Length == 64);
        (await context.Set<AuditLog>().CountAsync(value =>
            value.Resource == "InventoryAllocation" && value.ResourceId == allocation.Id.ToString())).Should().Be(4);
        controlEvents.Should().HaveCount(4).And.OnlyContain(value => value.RuleCode == "INV-REQ-FU-002");
    }

    [Fact]
    public async Task Denied_or_cross_tenant_requests_do_not_mutate_stock()
    {
        accessAllowed = false;
        var denied = () => service.CreateAndReserveAsync(new CreateWorkOrderPartDto
        {
            WorkOrderId = workOrderId, InventoryItemId = itemId, WarehouseId = warehouseId,
            WarehouseLocationId = locationId, QuantityRequired = 2m, UnitCost = 1m
        }, "denied-1", "corr-denied");
        await denied.Should().ThrowAsync<InventoryWorkOrderReservationAuthorizationException>();
        (await context.Set<InventoryAllocation>().CountAsync()).Should().Be(0);
        (await StockAsync()).AvailableStock.Should().Be(10m);

        accessAllowed = true;
        var created = await service.CreateAndReserveAsync(new CreateWorkOrderPartDto
        {
            WorkOrderId = workOrderId, InventoryItemId = itemId, WarehouseId = warehouseId,
            WarehouseLocationId = locationId, QuantityRequired = 2m, UnitCost = 1m
        }, "allowed-1", "corr-allowed");
        currentUser.TenantId = Guid.NewGuid();
        var crossTenant = () => service.UpdateAsync(created.Id, new UpdateWorkOrderPartDto
        {
            QuantityRequired = 2m, QuantityUsed = 1m, QuantityReturned = 0m,
            UnitCost = 1m, WarehouseLocationId = locationId, Status = created.Status
        }, "cross-tenant", "corr-cross-tenant");
        await crossTenant.Should().ThrowAsync<InventoryWorkOrderReservationNotFoundException>();
        currentUser.TenantId = tenantId;
        (await StockAsync()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 10m && value.AllocatedStock == 2m && value.AvailableStock == 8m);
    }

    [Fact]
    public void Relational_model_keeps_inventory_allocation_as_owner_with_exact_governance_indexes()
    {
        var model = context.GetService<IDesignTimeModel>().Model;
        var allocation = model.FindEntityType(typeof(InventoryAllocation))!;
        allocation.GetTableName().Should().Be("InventoryAllocations");
        allocation.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.GetDatabaseName() == "IX_InventoryAllocations_TenantId_WorkOrderIdempotencyKey" &&
            index.GetFilter()!.Contains("WorkOrder", StringComparison.Ordinal));
        allocation.GetCheckConstraints().Select(value => value.Name).Should().Contain(new[]
        {
            "CK_InventoryAllocations_WorkOrderLineage",
            "CK_InventoryAllocations_WorkOrderHashes",
            "CK_InventoryAllocations_WorkOrderQuantities"
        });

        var action = model.FindEntityType(typeof(InventoryWorkOrderReservationAction))!;
        action.GetTableName().Should().Be("InventoryWorkOrderReservationActions");
        action.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "TenantId", "InventoryAllocationId", "IdempotencyKey" }));

        var inventoryLocation = model.FindEntityType(typeof(InventoryLocation))!;
        inventoryLocation.GetDeclaredTriggers().Select(trigger => trigger.ModelName)
            .Should().Contain("TR_InventoryLocations_NegativeStockGuard");
    }

    [Fact]
    public void Migration_is_bounded_and_installs_append_only_transition_and_lineage_hard_stops()
    {
        var migration = new INVREQFU002MaintenanceReservationLifecycle();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<CreateTableOperation>().Select(value => value.Name)
            .Should().Equal("InventoryWorkOrderReservationActions");
        builder.Operations.OfType<DropTableOperation>().Should().BeEmpty();
        builder.Operations.OfType<DropColumnOperation>().Should().BeEmpty();
        var sql = string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(value => value.Sql));
        sql.Should().Contain("TR_InventoryWorkOrderReservationActions_Immutable");
        sql.Should().Contain("AFTER INSERT, UPDATE, DELETE");
        sql.Should().Contain("TR_InventoryAllocations_WorkOrderReservationGuard");
        sql.Should().Contain("TR_WorkOrderParts_ReservationLineageGuard");
        sql.Should().Contain("INV_WORK_ORDER_RESERVATION_ACTION_LINEAGE_INVALID");
        sql.Should().Contain("INV_WORK_ORDER_RESERVATION_TRANSITION_INVALID");
        sql.Should().Contain("INV_WORK_ORDER_RESERVATION_GOVERNANCE_REQUIRED");
        sql.Should().Contain("INV_WORK_ORDER_PART_LINEAGE_INVALID");
        sql.Should().Contain("ConsignmentWarehouseId");
        sql.Should().NotContain("location.[InventoryWarehouseId]");
    }

    [Fact]
    public void Maintenance_routes_and_scheduler_delegate_to_the_governed_inventory_owner()
    {
        var root = FindRepositoryRoot();
        var parts = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Maintenance",
            "WorkOrderPartService.cs"));
        parts.Should().Contain("IInventoryWorkOrderReservationService");
        parts.Should().NotContain("AvailableStock -=");
        parts.Should().NotContain("Repository<InventoryAllocation>().AddAsync");

        var scheduler = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Maintenance",
            "WorkOrderSchedulingService.cs"));
        scheduler.Should().Contain("ReserveForScheduleAsync");
        scheduler.Should().Contain("RescheduleAsync");
        scheduler.Should().NotContain("Need to update reservation");

        var legacyController = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers",
            "Maintenance", "ResourceAllocationController.cs"));
        legacyController.Should().Contain("INV_WORK_ORDER_RESERVATION_GOVERNED_ROUTE_REQUIRED");
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "maintenance", "work-orders",
            "page.tsx"));
        page.Should().Contain("warehouse-location-select");
        page.Should().Contain("workOrderPartService.updatePart");
    }

    [Fact]
    public void Current_relational_model_matches_the_compiled_snapshot()
    {
        using var sqlContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=InvReqFu002ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
        var snapshot = sqlContext.GetService<IMigrationsAssembly>().ModelSnapshot;
        if (snapshot is null) return;
        var differ = sqlContext.GetService<IMigrationsModelDiffer>();
        var initializer = sqlContext.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(snapshot!.Model, designTime: true);
        var current = sqlContext.GetService<IDesignTimeModel>().Model;
        differ.GetDifferences(snapshotModel.GetRelationalModel(), current.GetRelationalModel()).Should().BeEmpty();
    }

    private async Task SeedAsync()
    {
        var categoryId = Guid.NewGuid();
        await context.AddRangeAsync(
            new InventoryCategory { Id = categoryId, TenantId = tenantId, Code = "MAINT", Name = "Maintenance" },
            new Warehouse { Id = warehouseId, TenantId = tenantId, Code = "MAIN", Name = "Main", IsActive = true },
            new WarehouseLocation
            {
                Id = locationId, TenantId = tenantId, WarehouseId = warehouseId,
                LocationCode = "BIN-A-01", Name = "Bin A-01", IsActive = true
            },
            new InventoryItem
            {
                Id = itemId, TenantId = tenantId, CategoryId = categoryId,
                ItemCode = "SPARE-001", Name = "Maintenance spare", Status = ItemStatus.Active,
                AverageCost = 12m, CurrentStock = 10m, AvailableStock = 10m
            },
            new WarehouseQuantity
            {
                TenantId = tenantId, WarehouseId = warehouseId, InventoryItemId = itemId,
                CurrentStock = 10m, AvailableStock = 10m, AverageCost = 12m
            },
            new InventoryLocation
            {
                TenantId = tenantId, LocationId = locationId, InventoryItemId = itemId,
                Quantity = 10m, AvailableQuantity = 10m, AverageCost = 12m
            },
            new WorkOrder
            {
                Id = workOrderId, TenantId = tenantId, WorkOrderNumber = "WO-INV-002",
                Title = "Representative maintenance", AssetId = Guid.NewGuid(),
                WorkOrderTypeId = Guid.NewGuid(), MaintenanceTypeId = Guid.NewGuid(),
                PriorityLevelId = Guid.NewGuid(), Status = "Approved",
                RequestedStartDate = DateTime.UtcNow.AddDays(1)
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private ProcurementAccessCapabilityDecisionDto Decision(
        ProcurementAccessCapabilityRequest request, string correlation) => new()
    {
        Allowed = accessAllowed, Code = accessAllowed ? "ALLOWED" : "DENIED",
        Message = accessAllowed ? "Actor is in the exact warehouse scope." : "Actor is outside the exact warehouse scope.",
        ActorUserId = actorId, TenantId = currentUser.TenantId, PermissionCode = request.PermissionCode,
        WarehouseId = request.WarehouseId, LocationId = request.LocationId,
        CorrelationId = correlation, EvaluatedAtUtc = DateTime.UtcNow
    };

    private async Task<WarehouseQuantity> StockAsync()
    {
        context.ChangeTracker.Clear();
        return await context.Set<WarehouseQuantity>().SingleAsync(value =>
            value.TenantId == tenantId && value.WarehouseId == warehouseId && value.InventoryItemId == itemId);
    }

    private async Task<InventoryLocation> LocationStockAsync()
    {
        context.ChangeTracker.Clear();
        return await context.Set<InventoryLocation>().SingleAsync(value =>
            value.TenantId == tenantId && value.LocationId == locationId && value.InventoryItemId == itemId);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public void Dispose() => unitOfWork.Dispose();

    private sealed class MutableCurrentUser(Guid tenantId, Guid userId) : ICurrentUserProvider
    {
        public Guid UserId { get; } = userId;
        public Guid TenantId { get; set; } = tenantId;
        public string Username => "maintenance.stores";
        public string FullName => "Maintenance Stores";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC_STORES_MANAGER"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims { get; } = new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Local";
    }
}
