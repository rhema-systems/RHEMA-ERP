using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
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
    public async Task Return_of_a_serial_that_is_still_on_hand_is_rejected()
    {
        var category = Category("SERIAL-RETURN", configure: value => value.DefaultSerialTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        var receipt = Event(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt,
            "serial-return-receipt", null, null, 1m);
        receipt.SerialNumber = "SER-RETURN-001";
        await _context.AddRangeAsync(category, item, warehouse, receipt);
        await _context.SaveChangesAsync();

        var action = () => _service.StageEventAsync(Request(
            item.Id, warehouse.Id, InventoryTrackingDirection.Return,
            eventKey: "serial-return-duplicate", serial: "SER-RETURN-001"));

        await action.Should().ThrowAsync<InventoryTrackingControlException>()
            .Where(value => value.Code == "INV_TRACKING_SERIAL_RETURN_INVALID");
        _context.Set<InventoryTraceabilityEvent>().Local.Should().ContainSingle();
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
    public async Task Date_only_tracking_reuses_exact_location_history_after_query_narrowing()
    {
        var category = Category("DATE-ONLY", configure: value => value.DefaultManufactureDateTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = warehouse.Id,
            Warehouse = warehouse, LocationCode = "DATE-BIN", Name = "Date bin", IsActive = true
        };
        await _context.AddRangeAsync(category, item, warehouse, location);
        await _context.SaveChangesAsync();
        var manufactureDate = DateTime.UtcNow.Date.AddDays(-5);
        var receipt = Request(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt, "date-only-receipt");
        receipt.LocationId = location.Id;
        receipt.ManufactureDate = manufactureDate;

        await _service.StageEventAsync(receipt);
        await _unitOfWork.SaveChangesAsync();
        _service = NewService();
        var issue = Request(item.Id, warehouse.Id, InventoryTrackingDirection.Issue, "date-only-issue");
        issue.LocationId = location.Id;
        issue.ManufactureDate = manufactureDate;

        await _service.StageEventAsync(issue);

        _context.Set<InventoryTraceabilityEvent>().Local.Should().Contain(value =>
            value.EventKey == "date-only-issue" && value.Direction == InventoryTrackingDirection.Issue);
    }

    [Fact]
    public async Task Date_only_tracking_reuses_pending_location_history_before_the_unit_of_work_saves()
    {
        var category = Category("DATE-PENDING", configure: value => value.DefaultManufactureDateTracking = true);
        var item = Item(category.Id);
        var warehouse = Warehouse();
        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = warehouse.Id,
            Warehouse = warehouse, LocationCode = "DATE-PENDING-BIN", Name = "Date pending bin", IsActive = true
        };
        await _context.AddRangeAsync(category, item, warehouse, location);
        await _context.SaveChangesAsync();
        var manufactureDate = DateTime.UtcNow.Date.AddDays(-3);
        var receipt = Request(item.Id, warehouse.Id, InventoryTrackingDirection.Receipt, "date-pending-receipt");
        receipt.LocationId = location.Id;
        receipt.ManufactureDate = manufactureDate;
        var issue = Request(item.Id, warehouse.Id, InventoryTrackingDirection.Issue, "date-pending-issue");
        issue.LocationId = location.Id;
        issue.ManufactureDate = manufactureDate;

        await _service.StageEventAsync(receipt);
        await _service.StageEventAsync(issue);

        _context.Set<InventoryTraceabilityEvent>().Local.Should().HaveCount(2);
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

    [Fact]
    public void Tracking_exception_workflow_must_contain_the_exact_canonical_payload_hash()
    {
        var request = new RegisterInventoryTrackingExceptionRequest
        {
            InventoryItemId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), LocationId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(), ReferenceLineId = Guid.NewGuid(), ReferenceType = "InventoryIssue",
            ReferenceNumber = "ISS-001", Reason = "Approved expiry exception", LotNumber = " lot-01 ",
            WorkflowInstanceId = Guid.NewGuid(), WorkflowEvidenceDocumentId = Guid.NewGuid()
        };
        var codes = new List<string> { "INV_TRACKING_EXPIRED" };
        var expires = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        var payloadHashMethod = typeof(InventoryTrackingControlService).GetMethod(
            "TrackingExceptionPayloadHash", BindingFlags.Static | BindingFlags.NonPublic)!;
        var guardMethod = typeof(InventoryTrackingControlService).GetMethod(
            "RequireWorkflowPayloadHash", BindingFlags.Static | BindingFlags.NonPublic)!;
        var expectedHash = (string)payloadHashMethod.Invoke(null, new object[] { request, codes, expires })!;
        var workflow = new WorkflowInstance
        {
            DataContext = JsonSerializer.Serialize(new { inventoryTrackingExceptionPayloadHash = new string('0', 64) })
        };

        var rejected = Assert.Throws<TargetInvocationException>(() =>
            guardMethod.Invoke(null, new object[] { workflow, expectedHash }));
        rejected.InnerException.Should().BeOfType<InventoryTrackingControlException>()
            .Which.Code.Should().Be("INV_TRACKING_EXCEPTION_WORKFLOW_PAYLOAD_INVALID");

        workflow.DataContext = JsonSerializer.Serialize(new { inventoryTrackingExceptionPayloadHash = expectedHash });
        guardMethod.Invoking(method => method.Invoke(null, new object[] { workflow, expectedHash }))
            .Should().NotThrow();
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
