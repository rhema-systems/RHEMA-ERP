using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTrackingControlServiceTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private InventoryTrackingControlService _service;

    public InventoryTrackingControlServiceTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        _unitOfWork = new UnitOfWork(_context);
        _currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(value => value.UserId).Returns(_userId);
        _currentUser.SetupGet(value => value.Username).Returns("stores.officer@tenant.test");
        _currentUser.SetupGet(value => value.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(value => value.IsExternalUser).Returns(false);
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        _service = NewService();
    }

    [Fact]
    public async Task Requirements_combine_item_and_category_ancestry_fail_closed()
    {
        var parent = Category("PARENT", configure: value =>
        {
            value.DefaultLotTracking = true;
            value.DefaultManufactureDateTracking = true;
            value.MinimumShelfLifeDays = 30;
        });
        var child = Category("CHILD", parent.Id, value =>
        {
            value.DefaultBatchTracking = true;
            value.DefaultExpirationTracking = true;
            value.EnforceFifoIssue = true;
        });
        var item = Item(child.Id, value =>
        {
            value.IsSerialTracked = true;
            value.ShelfLifeDays = 45;
            value.ValuationMethod = ValuationMethod.FIFO;
        });
        await _context.AddRangeAsync(parent, child, item);
        await _context.SaveChangesAsync();

        var result = await _service.GetRequirementsAsync(item.Id);

        result.Should().BeEquivalentTo(new
        {
            RequiresLot = true,
            RequiresBatch = true,
            RequiresSerial = true,
            RequiresManufactureDate = true,
            RequiresExpiryDate = true,
            EnforcesFifoIssue = true,
            MinimumShelfLifeDays = 45
        });
        result.CategoryLineage.Should().Equal(child.Id, parent.Id);
    }

    [Fact]
    public async Task Required_tracking_cannot_be_omitted_and_no_event_is_staged()
    {
        var category = Category("CONTROLLED", configure: value =>
        {
            value.DefaultLotTracking = true;
            value.DefaultBatchTracking = true;
            value.DefaultManufactureDateTracking = true;
            value.DefaultExpirationTracking = true;
        });
        var item = Item(category.Id);
        var warehouse = Warehouse();
        await _context.AddRangeAsync(category, item, warehouse);
        await _context.SaveChangesAsync();

        var act = () => _service.StageEventAsync(Request(item.Id, warehouse.Id,
            InventoryTrackingDirection.Receipt, lot: "LOT-01"));

        await act.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_BATCH_REQUIRED");
        _context.Set<InventoryTraceabilityEvent>().Local.Should().BeEmpty();
    }

    [Fact]
    public async Task Expired_lot_is_blocked_without_an_exact_approved_exception()
    {
        var category = Category("EXPIRY", configure: value => value.DefaultLotTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        await _context.AddRangeAsync(category, item, warehouse, Event(item.Id, warehouse.Id,
            InventoryTrackingDirection.Receipt, "old-receipt", "LOT-OLD", DateTime.UtcNow.AddDays(-1), 10));
        await _context.SaveChangesAsync();

        var act = () => _service.StageEventAsync(Request(item.Id, warehouse.Id,
            InventoryTrackingDirection.Issue, lot: "LOT-OLD"));

        await act.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_EXPIRED");
    }

    [Fact]
    public async Task Fifo_category_blocks_a_newer_open_lot()
    {
        var category = Category("FIFO", configure: value =>
        {
            value.DefaultLotTracking = true;
            value.EnforceFifoIssue = true;
        });
        var item = Item(category.Id, value => value.ValuationMethod = ValuationMethod.FIFO);
        var warehouse = Warehouse();
        var older = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt, "receipt-1", "LOT-001", DateTime.UtcNow.AddDays(60), 5);
        older.OccurredAtUtc = DateTime.UtcNow.AddDays(-2);
        var newer = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt, "receipt-2", "LOT-002", DateTime.UtcNow.AddDays(60), 5);
        newer.OccurredAtUtc = DateTime.UtcNow.AddDays(-1);
        await _context.AddRangeAsync(category, item, warehouse, older, newer);
        await _context.SaveChangesAsync();

        var act = () => _service.StageEventAsync(Request(item.Id, warehouse.Id,
            InventoryTrackingDirection.Issue, lot: "LOT-002"));

        await act.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_FIFO_VIOLATION");
    }

    [Fact]
    public async Task Duplicate_serial_receipt_is_blocked_but_identical_event_replay_is_idempotent()
    {
        var category = Category("SERIAL", configure: value => value.DefaultSerialTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        await _context.AddRangeAsync(category, item, warehouse);
        await _context.SaveChangesAsync();
        var first = Request(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt, serial: "SER-001");

        await _service.StageEventAsync(first);
        await _service.StageEventAsync(first);

        _context.Set<InventoryTraceabilityEvent>().Local.Should().ContainSingle();
        await _unitOfWork.SaveChangesAsync();
        _service = NewService();
        var duplicate = () => _service.StageEventAsync(Request(item.Id, warehouse.Id,
            InventoryTrackingDirection.Receipt, eventKey: "receipt-second", serial: "SER-001"));
        await duplicate.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_SERIAL_DUPLICATE");
    }

    [Fact]
    public async Task Legacy_untracked_stock_can_issue_without_synthetic_inbound_history()
    {
        var category = Category("LEGACY-UNTRACKED");
        var item = Item(category.Id);
        var warehouse = Warehouse();
        await _context.AddRangeAsync(category, item, warehouse);
        await _context.SaveChangesAsync();

        await _service.StageEventAsync(Request(
            item.Id,
            warehouse.Id,
            InventoryTrackingDirection.Issue,
            eventKey: "legacy-untracked-issue"));

        _context.Set<InventoryTraceabilityEvent>().Local.Should().ContainSingle(value =>
            value.EventKey == "legacy-untracked-issue" &&
            value.Direction == InventoryTrackingDirection.Issue);
    }

    [Fact]
    public async Task Availability_validation_does_not_aggregate_other_lots_in_the_same_bin()
    {
        var category = Category("LOT-AVAILABILITY", configure: value => value.DefaultLotTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = warehouse.Id,
            Warehouse = warehouse, LocationCode = "LOT-BIN", Name = "Lot bin", IsActive = true
        };
        var selected = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt,
            "lot-selected", "LOT-A", null, 2m);
        selected.LocationId = location.Id;
        var other = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt,
            "lot-other", "LOT-B", null, 10m);
        other.LocationId = location.Id;
        await _context.AddRangeAsync(category, item, warehouse, location, selected, other);
        await _context.SaveChangesAsync();

        var action = () => _service.ValidateAvailabilityAsync(
            item.Id, warehouse.Id, location.Id, 3m, lotNumber: "lot-a");

        await action.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_LOT_INSUFFICIENT");
    }

    [Fact]
    public async Task Reads_are_tenant_scoped_and_external_actors_are_denied()
    {
        var category = Category("TENANT");
        var item = Item(category.Id);
        var warehouse = Warehouse();
        var foreignEvent = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt,
            "foreign", null, null, 1);
        foreignEvent.TenantId = Guid.NewGuid();
        await _context.AddRangeAsync(category, item, warehouse, foreignEvent);
        await _context.SaveChangesAsync();

        (await _service.GetEventsAsync()).Should().BeEmpty();
        _currentUser.SetupGet(value => value.IsExternalUser).Returns(true);
        _currentUser.Object.IsExternalUser.Should().BeTrue();
        _service = NewService();
        var act = () => _service.GetEventsAsync();
        await act.Should().ThrowAsync<InventoryTrackingAuthorizationException>();
    }

    [Fact]
    public async Task Trace_register_returns_only_warehouses_in_the_actors_effective_scope()
    {
        var category = Category("SCOPED-READ");
        var item = Item(category.Id);
        var allowedWarehouse = Warehouse();
        var deniedWarehouse = Warehouse();
        var allowedEvent = Event(item.Id, allowedWarehouse.Id, InventoryTrackingDirection.Receipt,
            "allowed-event", null, null, 1);
        var deniedEvent = Event(item.Id, deniedWarehouse.Id, InventoryTrackingDirection.Receipt,
            "denied-event", null, null, 1);
        await _context.AddRangeAsync(category, item, allowedWarehouse, deniedWarehouse, allowedEvent, deniedEvent);
        await _context.SaveChangesAsync();
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken __) =>
                new ProcurementAccessCapabilityDecisionDto { Allowed = request.WarehouseId == allowedWarehouse.Id });

        var result = await _service.GetEventsAsync();

        result.Should().ContainSingle().Which.Id.Should().Be(allowedEvent.Id);
    }

    [Fact]
    public async Task Warehouse_wide_tracking_registers_require_an_all_locations_assignment()
    {
        var warehouse = Warehouse();
        await _context.AddAsync(warehouse);
        await _context.SaveChangesAsync();
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken __) =>
                new ProcurementAccessCapabilityDecisionDto { Allowed = !request.RequireLocationScope });

        var events = () => _service.GetEventsAsync();
        var exceptions = () => _service.GetExceptionsAsync();

        await events.Should().ThrowAsync<InventoryTrackingAuthorizationException>();
        await exceptions.Should().ThrowAsync<InventoryTrackingAuthorizationException>();
        _access.Verify(value => value.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.WarehouseId == warehouse.Id && request.RequireLocationScope),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public void Ef_model_has_tenant_idempotency_exception_lineage_and_quantity_guards()
    {
        var exception = _context.Model.FindEntityType(typeof(InventoryTrackingException))!;
        exception.GetIndexes().Should().Contain(value => value.IsUnique && value.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "WorkflowInstanceId" }));
        exception.FindProperty(nameof(InventoryTrackingException.RowVersion))!.IsConcurrencyToken.Should().BeTrue();

        var sqlServerContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Tdc0603ModelOnly;Trusted_Connection=True").Options);
        var trace = sqlServerContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(InventoryTraceabilityEvent))!;
        trace.GetIndexes().Should().Contain(value => value.IsUnique && value.Properties.Select(property => property.Name)
            .SequenceEqual(new[] { "TenantId", "EventKey" }));
        trace.FindProperty(nameof(InventoryTraceabilityEvent.Quantity))!.GetColumnType().Should().Be("decimal(18,4)");
        trace.GetCheckConstraints().Select(value => value.Name).Should().Contain("CK_InventoryTraceabilityEvents_Quantity");
    }

    private InventoryTrackingControlService NewService() => new(_unitOfWork, _currentUser.Object, _access.Object);

    private InventoryCategory Category(string code, Guid? parentId = null, Action<InventoryCategory>? configure = null)
    {
        var value = new InventoryCategory
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = code, Name = code, ParentCategoryId = parentId, IsActive = true
        };
        configure?.Invoke(value);
        return value;
    }

    private InventoryItem Item(Guid categoryId, Action<InventoryItem>? configure = null)
    {
        var value = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CategoryId = categoryId,
            ItemCode = ($"ITEM-{Guid.NewGuid():N}")[..20], Name = "Controlled item", UnitOfMeasure = "EA"
        };
        configure?.Invoke(value);
        return value;
    }

    private Warehouse Warehouse() => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, Code = ($"WH-{Guid.NewGuid():N}")[..12], Name = "Controlled warehouse", IsActive = true
    };

    private InventoryTraceabilityEvent Event(
        Guid itemId,
        Guid warehouseId,
        InventoryTrackingDirection direction,
        string key,
        string? lot,
        DateTime? expiry,
        decimal quantity) => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, InventoryItemId = itemId, WarehouseId = warehouseId,
        Direction = direction, Quantity = quantity, ReferenceType = "Test", ReferenceNumber = key,
        ReferenceId = Guid.NewGuid(), EventKey = key, LotNumber = lot, ExpiryDate = expiry,
        ActorUserId = _userId, OccurredAtUtc = DateTime.UtcNow, CorrelationId = key,
        PayloadHash = new string('a', 64)
    };

    private InventoryTrackingMutationRequest Request(
        Guid itemId,
        Guid warehouseId,
        InventoryTrackingDirection direction,
        string? eventKey = null,
        string? lot = null,
        string? serial = null) => new()
    {
        InventoryItemId = itemId, WarehouseId = warehouseId, Direction = direction, Quantity = 1,
        ReferenceType = "TestTransaction", ReferenceNumber = "TEST-001", ReferenceId = Guid.NewGuid(),
        ReferenceLineId = Guid.NewGuid(), EventKey = eventKey ?? Guid.NewGuid().ToString("N"),
        LotNumber = lot, SerialNumber = serial, CorrelationId = "tracking-test"
    };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _unitOfWork.Dispose();
        await _context.DisposeAsync();
    }
}
