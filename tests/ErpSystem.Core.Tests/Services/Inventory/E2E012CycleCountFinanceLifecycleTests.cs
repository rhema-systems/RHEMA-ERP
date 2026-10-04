using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed partial class E2E012CycleCountFinanceLifecycleTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _initiatorId = Guid.NewGuid();
    private readonly Guid _counterId = Guid.NewGuid();
    private readonly Guid _recountUserId = Guid.NewGuid();
    private readonly Guid _storesApproverId = Guid.NewGuid();
    private readonly Guid _financeApproverId = Guid.NewGuid();
    private readonly Guid _auditUserId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _postingEventId = Guid.NewGuid();
    private readonly Guid _journalEntryId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly MutableCurrentUser _currentUser;
    private readonly IWarehouseDefaultLocationService _warehouseDefaults;
    private readonly PhysicalCountService _counts;
    private readonly Mock<IStockAdjustmentService> _adjustments = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private readonly List<ProcurementControlEventWriteRequest> _controlEvents = [];
    private readonly byte[] _countRowVersion = [1, 2, 3, 4];
    private readonly byte[] _lineRowVersion = [5, 6, 7, 8];
    private StockAdjustmentDetailDto? _adjustment;
    private CreateStockAdjustmentDto? _createdAdjustment;

    public E2E012CycleCountFinanceLifecycleTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"e2e-012-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        _unitOfWork = new UnitOfWork(_context);
        _currentUser = new MutableCurrentUser(_tenantId, _initiatorId, "cycle.initiator");
        var warehouseUser = new Mock<ICurrentUserProvider>();
        warehouseUser.SetupGet(x => x.UserId).Returns(() => _currentUser.ActorId);
        warehouseUser.SetupGet(x => x.TenantId).Returns(_tenantId);
        warehouseUser.SetupGet(x => x.Username).Returns(() => _currentUser.UserName ?? "cycle.counter");
        warehouseUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _warehouseDefaults = new WarehouseDefaultLocationService(_unitOfWork, warehouseUser.Object);

        var access = _access;
        access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                Allowed(request, correlationId));
        access.Setup(service => service.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                Allowed(request, correlationId));

        var events = new Mock<IProcurementControlEventService>();
        events.Setup(service => service.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
            {
                _controlEvents.Add(request);
                return new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    EventKey = request.EventKey,
                    EventType = request.EventType,
                    Action = request.Action,
                    Result = request.Result,
                    SourceType = request.SourceType,
                    SourceId = request.SourceId,
                    SourceReference = request.SourceReference,
                    CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                };
            });

        ConfigureAdjustmentOwner();
        _counts = new PhysicalCountService(
            new PhysicalCountRepository(_context),
            new PhysicalCountItemRepository(_context),
            new InventoryItemRepository(_context),
            new WarehouseRepository(_context),
            new WarehouseQuantityRepository(_context),
            _unitOfWork,
            _currentUser,
            access.Object,
            _adjustments.Object,
            events.Object,
            NullLogger<PhysicalCountService>.Instance,
            _warehouseDefaults,
            counterNotificationConfiguration: new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["FrontendUrl"] = "https://erp.example.test" }).Build());
    }

    public async Task InitializeAsync()
    {
        var category = new InventoryCategory
        {
            TenantId = _tenantId,
            Code = "CYCLE",
            Name = "Cycle-count items",
            IsActive = true
        };
        var eachUnit = new UnitOfMeasure
        {
            TenantId = _tenantId,
            Code = "EA",
            Name = "Each",
            DecimalPlaces = 0,
            RoundingIncrement = 1m,
            IsActive = true
        };
        await _context.AddRangeAsync(
            User(_initiatorId, "cycle.initiator"),
            User(_counterId, "cycle.counter"),
            User(_recountUserId, "cycle.recounter"),
            User(_storesApproverId, "stores.approver"),
            User(_financeApproverId, "finance.approver"),
            User(_auditUserId, "audit.attestor"),
            category,
            eachUnit,
            new Warehouse
            {
                Id = _warehouseId,
                TenantId = _tenantId,
                Code = "MAIN",
                Name = "Main Stores",
                IsActive = true
            },
            new WarehouseLocation
            {
                Id = _locationId,
                TenantId = _tenantId,
                WarehouseId = _warehouseId,
                LocationCode = "A-01",
                Name = "Cycle bin A-01",
                IsActive = true,
                IsDefault = true,
                IsPickingLocation = true
            },
            new InventoryItem
            {
                Id = _itemId,
                TenantId = _tenantId,
                ItemCode = "ABC-A-001",
                Name = "Representative ABC item",
                CategoryId = category.Id,
                UnitOfMeasureId = eachUnit.Id,
                UnitOfMeasure = "EA",
                Status = ItemStatus.Active,
                ABCClass = "A",
                StandardCost = 10m,
                AverageCost = 10m,
                CurrentStock = 10m,
                AvailableStock = 10m
            },
            new WarehouseQuantity
            {
                TenantId = _tenantId,
                WarehouseId = _warehouseId,
                InventoryItemId = _itemId,
                CurrentStock = 10m,
                AvailableStock = 10m,
                AverageCost = 10m
            },
            new InventoryLocation
            {
                TenantId = _tenantId,
                InventoryItemId = _itemId,
                LocationId = _locationId,
                Quantity = 10m,
                AvailableQuantity = 10m,
                AverageCost = 10m
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("B", "B")]
    [InlineData("C", "C")]
    [InlineData(" a ", "A")]
    [InlineData(null, "C")]
    public async Task Manual_cycle_count_populates_only_the_selected_abc_class(string? selection, string expected)
    {
        await SeedOtherAbcClassesAsync();
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.CycleCount, ABCClass = selection
        }, _initiatorId);

        var saved = await _context.Set<PhysicalCount>().AsNoTracking().Include(x => x.Items).SingleAsync();
        created.TotalItems.Should().Be(1);
        saved.ABCClass.Should().Be(expected);
        saved.Status.Should().Be("Draft");
        saved.Items.Should().ContainSingle().Which.ItemCode.Should().Be($"ABC-{expected}-001");
        saved.Items.Single().IsCounted.Should().BeFalse();
        saved.StockAdjustmentId.Should().BeNull();
    }

    [Fact]
    public async Task Manual_full_count_does_not_filter_by_a_leftover_cycle_class()
    {
        await SeedOtherAbcClassesAsync();
        await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount, ABCClass = "C"
        }, _initiatorId);
        var saved = await _context.Set<PhysicalCount>().AsNoTracking().Include(x => x.Items).SingleAsync();
        saved.TotalItems.Should().Be(3);
        saved.Items.Select(x => x.ItemCode).Should().BeEquivalentTo("ABC-A-001", "ABC-B-001", "ABC-C-001");
    }

    [Fact]
    public async Task Manual_cycle_count_with_no_matching_items_remains_an_empty_draft()
    {
        await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.CycleCount, ABCClass = "B"
        }, _initiatorId);
        var saved = await _context.Set<PhysicalCount>().AsNoTracking().Include(x => x.Items).SingleAsync();
        saved.Status.Should().Be("Draft");
        saved.TotalItems.Should().Be(0);
        saved.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Warehouse_wide_count_snapshots_each_item_location_and_exports_each_location()
    {
        var secondLocationId = await SeedSecondLocationAsync();

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.LocationId.Should().BeNull();
        saved.TotalItems.Should().Be(2);
        saved.Items.Should().ContainSingle(x => x.InventoryItemId == _itemId &&
            x.LocationId == _locationId && x.SystemQuantity == 4m);
        saved.Items.Should().ContainSingle(x => x.InventoryItemId == _itemId &&
            x.LocationId == secondLocationId && x.SystemQuantity == 6m);
        saved.Items.Sum(x => x.SystemQuantity).Should().Be(10m);

        _context.ChangeTracker.Clear();
        var export = await _counts.ExportCountSheetAsync(created.Id);
        export.Items.Should().HaveCount(2);
        export.Items.Select(x => x.LocationName).Should().BeEquivalentTo("A-01", "B-02");
        export.Items.Should().OnlyContain(x => x.ItemCode == "ABC-A-001");
    }

    [Fact]
    public async Task Location_count_includes_only_that_location_items_and_exact_location_balances()
    {
        var secondLocationId = await SeedSecondLocationAsync();
        await SeedOtherAbcClassesAsync();
        // These items exist in the same warehouse, but only in the other bin.
        var otherStocks = await _context.Set<InventoryLocation>()
            .Where(x => x.InventoryItemId != _itemId).ToListAsync();
        foreach (var stock in otherStocks) stock.LocationId = secondLocationId;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.LocationId.Should().Be(_locationId);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.InventoryItemId == _itemId && x.LocationId == _locationId && x.SystemQuantity == 4m);
        saved.TotalItems.Should().Be(1);
        _context.ChangeTracker.Clear();
        var export = await _counts.ExportCountSheetAsync(created.Id);
        export.Items.Should().ContainSingle().Which.LocationName.Should().Be("A-01");
        export.Items.Single().ItemCode.Should().Be("ABC-A-001");
    }

    [Fact]
    public async Task Main_warehouse_count_excludes_stock_owned_by_a_consignment_warehouse()
    {
        var consignmentWarehouseId = Guid.NewGuid();
        var consignmentLocationId = Guid.NewGuid();
        await _context.AddRangeAsync(new Warehouse
        {
            Id = consignmentWarehouseId, TenantId = _tenantId, Code = "CONSIGNMENT",
            Name = "Consignment owner", IsActive = true, IsConsignmentWarehouse = true
        }, new WarehouseLocation
        {
            Id = consignmentLocationId, TenantId = _tenantId, WarehouseId = _warehouseId,
            LocationCode = "CONSIGNMENT-BIN", Name = "Consignment stock in main warehouse",
            IsActive = true, IsConsignmentBin = true, ConsignmentWarehouseId = consignmentWarehouseId
        }, new InventoryLocation
        {
            TenantId = _tenantId, InventoryItemId = _itemId, LocationId = consignmentLocationId,
            Quantity = 7m, AvailableQuantity = 7m, AverageCost = 10m
        }, new WarehouseQuantity
        {
            TenantId = _tenantId, WarehouseId = consignmentWarehouseId, InventoryItemId = _itemId,
            CurrentStock = 7m, AvailableStock = 7m, AverageCost = 10m
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.LocationId == _locationId && x.SystemQuantity == 10m);
        _context.ChangeTracker.Clear();
        (await _counts.ExportCountSheetAsync(created.Id)).Items.Should().ContainSingle().Which
            .LocationName.Should().Be("A-01");
        (await _context.Set<InventoryLocation>().SingleAsync(x => x.LocationId == consignmentLocationId))
            .Quantity.Should().Be(7m);
        (await _context.Set<WarehouseQuantity>().SingleAsync(x => x.WarehouseId == _warehouseId))
            .CurrentStock.Should().Be(10m);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("other-warehouse")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task Invalid_selected_location_is_rejected_before_creating_a_count(string invalidScope)
    {
        var selectedId = _locationId;
        var location = await _context.Set<WarehouseLocation>().SingleAsync();
        switch (invalidScope)
        {
            case "foreign-tenant": location.TenantId = Guid.NewGuid(); break;
            case "other-warehouse": location.WarehouseId = Guid.NewGuid(); break;
            case "inactive": location.IsActive = false; break;
            case "missing": selectedId = Guid.NewGuid(); break;
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var create = () => _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = selectedId, CountType = CountType.FullCount
        }, _initiatorId);

        await create.Should().ThrowAsync<Exception>().WithMessage("*location*");
        (await _context.Set<PhysicalCount>().CountAsync()).Should().Be(0);
        (await _context.Set<PhysicalCountItem>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Foreign_or_inactive_warehouse_is_rejected_before_creating_a_count(bool inactive)
    {
        var warehouse = await _context.Set<Warehouse>().SingleAsync();
        if (inactive) warehouse.IsActive = false;
        else warehouse.TenantId = Guid.NewGuid();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var create = () => _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);

        await create.Should().ThrowAsync<Exception>().WithMessage("*warehouse*");
        (await _context.Set<PhysicalCount>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Warehouse_wide_count_assigns_only_the_unlocated_residual_to_default_without_duplicate_totals()
    {
        var secondLocationId = await SeedSecondLocationAsync(secondQuantity: 3m);
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);
        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().HaveCount(2);
        saved.Items.Should().ContainSingle(x => x.LocationId == _locationId && x.SystemQuantity == 7m);
        saved.Items.Should().ContainSingle(x => x.LocationId == secondLocationId && x.SystemQuantity == 3m);
        saved.Items.Sum(x => x.SystemQuantity).Should().Be(10m);
        (await _context.Set<InventoryLocation>().Where(x => x.InventoryItemId == _itemId)
            .SumAsync(x => x.Quantity)).Should().Be(10m);
        (await _context.Set<WarehouseQuantity>().SingleAsync()).CurrentStock.Should().Be(10m);

        var repeated = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);
        var repeatedRows = (await LoadCountAsync(repeated.Id)).Items;
        repeatedRows.Should().HaveCount(2);
        repeatedRows.Sum(x => x.SystemQuantity).Should().Be(10m);
        repeatedRows.Should().ContainSingle(x => x.LocationId == _locationId && x.SystemQuantity == 7m);
        (await _context.Set<InventoryLocation>().Where(x => x.InventoryItemId == _itemId)
            .SumAsync(x => x.Quantity)).Should().Be(10m);
    }

    [Fact]
    public async Task Warehouse_wide_count_with_no_location_balances_assigns_stock_to_warehouse_default()
    {
        _context.RemoveRange(await _context.Set<InventoryLocation>().ToListAsync());
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.InventoryItemId == _itemId && x.LocationId == _locationId && x.SystemQuantity == 10m);
        (await _context.Set<InventoryLocation>().SingleAsync()).Quantity.Should().Be(10m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Selected_default_or_other_location_counts_only_its_actual_quantity_after_residual_assignment(bool selectDefault)
    {
        var otherLocationId = await SeedSecondLocationAsync(secondQuantity: 3m);
        var selectedLocationId = selectDefault ? _locationId : otherLocationId;

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = selectedLocationId, CountType = CountType.FullCount
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.LocationId == selectedLocationId && x.SystemQuantity == (selectDefault ? 7m : 3m));
        _context.ChangeTracker.Clear();
        (await _counts.ExportCountSheetAsync(created.Id)).Items.Should().ContainSingle().Which
            .LocationName.Should().Be(selectDefault ? "A-01" : "B-02");
        (await _context.Set<InventoryLocation>().Where(x => x.InventoryItemId == _itemId)
            .SumAsync(x => x.Quantity)).Should().Be(10m);
        (await _context.Set<WarehouseQuantity>().SingleAsync()).CurrentStock.Should().Be(10m);
    }

    [Fact]
    public async Task Legacy_draft_uses_pinned_default_resolution_without_rewriting_its_original_snapshot()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        var legacy = await LoadCountAsync(created.Id);
        var originalLine = legacy.Items.Single();
        originalLine.LocationId = null;
        var originalLineId = originalLine.Id;
        var originalSystemQuantity = originalLine.SystemQuantity;
        var originalCost = originalLine.UnitCost;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);

        var started = await LoadCountAsync(created.Id);
        started.Status.Should().Be("InProgress");
        started.LocationId.Should().BeNull();
        started.Items.Single().LocationId.Should().BeNull("the immutable source snapshot must remain intact");
        started.Items.Single().SystemQuantity.Should().Be(originalSystemQuantity);
        started.Items.Single().UnitCost.Should().Be(originalCost);
        (await _context.Set<PhysicalCountAction>().CountAsync(x => x.PhysicalCountId == created.Id &&
            x.ActionType == PhysicalCountActionType.DefaultLocationsResolved)).Should().Be(1);
        _context.ChangeTracker.Clear();
        (await _counts.GetByIdAsync(created.Id))!.Items.Single().LocationId.Should().Be(_locationId);
        (await _counts.ExportCountSheetAsync(created.Id)).Items.Single().LocationName.Should().Be("A-01");

        await SaveQuantityAsync(originalLineId, 8m, "legacy-default-quantity");
        await AttachCleanCountEvidenceAsync(created.Id);
        await _counts.ReviewCountAsync(created.Id, _counterId, Mutation("legacy-default-review"));
        // A later setup change must not redirect this count's already-resolved adjustment.
        var previousDefault = await _context.Set<WarehouseLocation>().SingleAsync(x => x.Id == _locationId);
        previousDefault.IsDefault = false;
        await _context.AddAsync(new WarehouseLocation
        {
            TenantId = _tenantId, WarehouseId = _warehouseId, LocationCode = "NEW-DEFAULT",
            Name = "Later warehouse default", IsDefault = true, IsActive = true, IsPickingLocation = true
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        (await _counts.SubmitReviewedCountAsync(created.Id, _counterId, Mutation("legacy-default-submit")))
            .Should().BeTrue();

        var submitted = await LoadCountAsync(created.Id);
        submitted.Status.Should().Be("PendingStoresApproval");
        submitted.Items.Single().Id.Should().Be(originalLineId);
        submitted.Items.Single().LocationId.Should().BeNull();
        submitted.Items.Single().SystemQuantity.Should().Be(originalSystemQuantity);
        submitted.Items.Single().UnitCost.Should().Be(originalCost);
        submitted.Items.Single().CountedQuantity.Should().Be(8m);
        _createdAdjustment!.Items.Should().ContainSingle().Which.LocationId.Should().Be(_locationId);
        _createdAdjustment.Items.Single().AdjustmentQuantity.Should().Be(-2m);
        (await _context.Set<PhysicalCountAction>().CountAsync(x => x.PhysicalCountId == created.Id &&
            x.ActionType == PhysicalCountActionType.DefaultLocationsResolved)).Should().Be(1);
    }

    [Fact]
    public async Task Warehouse_total_below_location_balances_requires_reconciliation_before_any_count_insert()
    {
        await SeedSecondLocationAsync();
        var warehouseStock = await _context.Set<WarehouseQuantity>().SingleAsync();
        warehouseStock.CurrentStock = warehouseStock.AvailableStock = 9m;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var create = () => _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*reconcile*");
        (await _context.Set<PhysicalCount>().CountAsync()).Should().Be(0);
        (await _context.Set<PhysicalCountItem>().CountAsync()).Should().Be(0);
        (await _context.Set<PhysicalCountAction>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Legacy_item_code_only_import_rejects_multiple_bins_before_changing_any_count_row()
    {
        await SeedSecondLocationAsync();
        await SeedOtherAbcClassesAsync();
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var before = (await LoadCountAsync(created.Id)).Items.Select(x => new
        {
            x.Id, x.LocationId, x.SystemQuantity, x.CountedQuantity, x.IsCounted, x.CountAttempts
        }).ToList();

        var import = () => _counts.ImportCountSheetAsync(created.Id, new[]
        {
            // Even a valid first row must not be applied before discovering the ambiguous SKU.
            new ImportCountItemDto { ItemCode = "ABC-B-001", CountedQuantity = 2m, RowVersion = Convert.ToBase64String(_lineRowVersion), IdempotencyKey = "import-valid" },
            new ImportCountItemDto { ItemCode = "ABC-A-001", CountedQuantity = 8m, RowVersion = Convert.ToBase64String(_lineRowVersion), IdempotencyKey = "import-ambiguous" }
        }, _counterId);

        await import.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not identify one saved line*");
        var saved = await LoadCountAsync(created.Id);
        saved.Items.Select(x => new
        {
            x.Id, x.LocationId, x.SystemQuantity, x.CountedQuantity, x.IsCounted, x.CountAttempts
        }).Should().BeEquivalentTo(before);
        saved.CountedItems.Should().Be(0);
        saved.StockAdjustmentId.Should().BeNull();
    }

    [Fact]
    public async Task Adding_a_draft_item_uses_location_balance_instead_of_caller_system_quantity()
    {
        await SeedSecondLocationAsync();
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);
        var originalLine = (await LoadCountAsync(created.Id)).Items.Single();
        await _counts.RemoveCountItemAsync(originalLine.Id, _initiatorId);
        _context.ChangeTracker.Clear();

        await _counts.AddCountItemAsync(created.Id, new AddCountItemDto
        {
            InventoryItemId = _itemId, LocationId = _locationId, SystemQuantity = 999m
        }, _initiatorId);

        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.LocationId == _locationId && x.SystemQuantity == 4m);
        saved.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Different_authorized_actor_can_add_items_to_another_initiators_draft()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);
        var originalLine = (await LoadCountAsync(created.Id)).Items.Single();
        await _counts.RemoveCountItemAsync(originalLine.Id, _initiatorId);
        _context.ChangeTracker.Clear();
        _currentUser.Switch(_counterId, "cycle.counter");

        await _counts.AddCountItemAsync(created.Id, new AddCountItemDto
        {
            InventoryItemId = _itemId, LocationId = _locationId, SystemQuantity = 999m
        }, _counterId);

        var saved = await LoadCountAsync(created.Id);
        saved.InitiatedById.Should().Be(_initiatorId);
        saved.Items.Should().ContainSingle().Which.Should().Match<PhysicalCountItem>(x =>
            x.LocationId == _locationId && x.SystemQuantity == 10m);
        saved.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Adding_the_same_item_and_location_twice_is_rejected_without_duplicate_count_rows()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);

        var add = () => _counts.AddCountItemAsync(created.Id, new AddCountItemDto
        {
            InventoryItemId = _itemId, LocationId = _locationId
        }, _initiatorId);

        await add.Should().ThrowAsync<InvalidOperationException>();
        var saved = await LoadCountAsync(created.Id);
        saved.Items.Should().ContainSingle();
        saved.TotalItems.Should().Be(1);
    }

    [Fact]
    public async Task Adding_an_item_from_a_different_bin_to_a_location_count_is_rejected()
    {
        var secondLocationId = await SeedSecondLocationAsync();
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);

        var add = () => _counts.AddCountItemAsync(created.Id, new AddCountItemDto
        {
            InventoryItemId = _itemId, LocationId = secondLocationId
        }, _initiatorId);

        await add.Should().ThrowAsync<InvalidOperationException>().WithMessage("*location*");
        (await LoadCountAsync(created.Id)).Items.Should().ContainSingle().Which.LocationId.Should().Be(_locationId);
    }

    [Fact]
    public async Task Legacy_unlocated_count_spanning_multiple_bins_cannot_submit_or_create_an_adjustment()
    {
        await SeedSecondLocationAsync();
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.FullCount
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await SaveQuantityAsync(line.Id, 8m, "location-scope-count");
        await AttachCleanCountEvidenceAsync(created.Id);
        await _counts.ReviewCountAsync(created.Id, _counterId, Mutation("location-scope-review"));
        // Represent an older saved warehouse-wide count that never retained exact bins.
        var legacy = await LoadCountAsync(created.Id);
        legacy.LocationId = null;
        legacy.Items.Single().LocationId = null;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var submit = () => _counts.SubmitReviewedCountAsync(created.Id, _counterId, Mutation("location-scope-submit"));

        await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*location*");
        var saved = await LoadCountAsync(created.Id);
        saved.Status.Should().Be("UnderReview");
        saved.StockAdjustmentId.Should().BeNull();
        _createdAdjustment.Should().BeNull();
    }

    [Fact]
    public async Task Legacy_under_review_count_resolves_default_on_submit_without_changing_saved_quantities_or_scope()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, CountType = CountType.FullCount
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var lineId = (await LoadCountAsync(created.Id)).Items.Single().Id;
        await SaveQuantityAsync(lineId, 8m, "legacy-review-quantity");
        await AttachCleanCountEvidenceAsync(created.Id);
        await _counts.ReviewCountAsync(created.Id, _counterId, Mutation("legacy-review"));
        // Simulate the pre-fix record already saved by the counter, with its original null bin.
        var legacy = await LoadCountAsync(created.Id);
        legacy.Items.Single().LocationId = null;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var earlierActions = await _context.Set<PhysicalCountAction>().AsNoTracking()
            .Where(x => x.PhysicalCountId == created.Id).Select(x => new { x.Id, x.SnapshotJson }).ToListAsync();

        (await _counts.SubmitReviewedCountAsync(created.Id, _counterId, Mutation("legacy-review-submit")))
            .Should().BeTrue();

        var saved = await LoadCountAsync(created.Id);
        saved.LocationId.Should().BeNull();
        saved.Items.Single().LocationId.Should().BeNull();
        saved.Items.Single().SystemQuantity.Should().Be(10m);
        saved.Items.Single().CountedQuantity.Should().Be(8m);
        saved.Items.Single().UnitCost.Should().Be(10m);
        saved.Status.Should().Be("PendingStoresApproval");
        _createdAdjustment!.Items.Single().LocationId.Should().Be(_locationId);
        _createdAdjustment.Items.Single().AdjustmentQuantity.Should().Be(-2m);
        var earlierIds = earlierActions.Select(x => x.Id).ToList();
        (await _context.Set<PhysicalCountAction>().AsNoTracking().Where(x => earlierIds.Contains(x.Id))
            .Select(x => new { x.Id, x.SnapshotJson }).ToListAsync()).Should().BeEquivalentTo(earlierActions);
    }

    private async Task<Guid> SeedSecondLocationAsync(decimal secondQuantity = 6m)
    {
        var secondLocationId = Guid.NewGuid();
        var firstStock = await _context.Set<InventoryLocation>().SingleAsync(x =>
            x.InventoryItemId == _itemId && x.LocationId == _locationId);
        firstStock.Quantity = firstStock.AvailableQuantity = 4m;
        await _context.AddRangeAsync(new WarehouseLocation
        {
            Id = secondLocationId, TenantId = _tenantId, WarehouseId = _warehouseId,
            LocationCode = "B-02", Name = "Cycle bin B-02", IsActive = true, IsPickingLocation = true
        }, new InventoryLocation
        {
            TenantId = _tenantId, InventoryItemId = _itemId, LocationId = secondLocationId,
            Quantity = secondQuantity, AvailableQuantity = secondQuantity, AverageCost = 10m
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return secondLocationId;
    }

    private async Task SeedOtherAbcClassesAsync()
    {
        var categoryId = await _context.Set<InventoryItem>().Where(x => x.Id == _itemId).Select(x => x.CategoryId).SingleAsync();
        foreach (var abc in new[] { "B", "C" })
        {
            var item = new InventoryItem
            {
                TenantId = _tenantId, ItemCode = $"ABC-{abc}-001", Name = $"Class {abc} item",
                CategoryId = categoryId, UnitOfMeasure = "EA", Status = ItemStatus.Active,
                ABCClass = abc, StandardCost = 10m
            };
            await _context.AddRangeAsync(item, new WarehouseQuantity
            {
                TenantId = _tenantId, WarehouseId = _warehouseId, InventoryItemId = item.Id,
                CurrentStock = 5m, AvailableStock = 5m, AverageCost = 10m
            }, new InventoryLocation
            {
                TenantId = _tenantId, InventoryItemId = item.Id, LocationId = _locationId,
                Quantity = 5m, AvailableQuantity = 5m, AverageCost = 10m
            });
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Scheduled_count_snapshots_exact_bin_quantity_and_fifo_layer_value()
    {
        var now = DateTime.UtcNow;
        var schedule = await SeedDueScheduleAsync(now);
        var item = await _context.Set<InventoryItem>().SingleAsync(value => value.Id == _itemId);
        item.ValuationMethod = ValuationMethod.FIFO;
        item.StandardCost = 1m;
        item.AverageCost = 22m;
        var locationStock = await _context.Set<InventoryLocation>().SingleAsync();
        locationStock.Quantity = locationStock.AvailableQuantity = 4m;
        locationStock.AverageCost = 23m;
        await _context.AddRangeAsync(
            new InventoryBalance
            {
                TenantId = _tenantId, InventoryItemId = _itemId, WarehouseId = _warehouseId,
                LocationId = _locationId, QuantityOnHand = 4m, QuantityAvailable = 4m,
                TotalValue = 100m, AverageUnitCost = 25m
            },
            new InventoryLayer
            {
                TenantId = _tenantId, InventoryItemId = _itemId, WarehouseId = _warehouseId,
                LocationId = _locationId, LayerDate = now.AddDays(-2), OriginalQuantity = 1m,
                RemainingQuantity = 1m, UnitCost = 10m, RemainingValue = 10m
            },
            new InventoryLayer
            {
                TenantId = _tenantId, InventoryItemId = _itemId, WarehouseId = _warehouseId,
                LocationId = _locationId, LayerDate = now.AddDays(-1), OriginalQuantity = 3m,
                RemainingQuantity = 3m, UnitCost = 30m, RemainingValue = 90m
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _counts.GenerateDueCycleCountsAsync(_tenantId, now);

        result.DueSchedules.Should().Be(1);
        result.CountsCreated.Should().Be(1);
        var count = await _context.Set<PhysicalCount>().AsNoTracking()
            .Include(value => value.Items).Include(value => value.Actions).SingleAsync();
        count.LocationId.Should().Be(_locationId);
        count.TotalItems.Should().Be(1);
        count.Items.Single().Should().Match<PhysicalCountItem>(value =>
            value.LocationId == _locationId && value.SystemQuantity == 4m && value.UnitCost == 25m);
        count.Actions.Should().ContainSingle(value => value.ActionType == PhysicalCountActionType.Scheduled);
        var advanced = await _context.Set<InventoryCycleCountSchedule>().AsNoTracking()
            .SingleAsync(value => value.Id == schedule.Id);
        advanced.LastPhysicalCountId.Should().Be(count.Id);
        advanced.LastGeneratedAtUtc.Should().Be(now);
        advanced.NextDueAtUtc.Should().Be(schedule.NextDueAtUtc.AddDays(schedule.FrequencyDays));
    }

    [Fact]
    public async Task Scheduled_count_failure_leaves_no_partial_header_and_does_not_advance_schedule()
    {
        var now = DateTime.UtcNow;
        var schedule = await SeedDueScheduleAsync(now);
        var locationStock = await _context.Set<InventoryLocation>().SingleAsync();
        locationStock.Quantity = locationStock.AvailableQuantity = 4m;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var failingItems = new Mock<IPhysicalCountItemRepository>();
        failingItems.Setup(value => value.AddAsync(It.IsAny<PhysicalCountItem>()))
            .ThrowsAsync(new InvalidOperationException("Injected line population failure."));
        var service = new PhysicalCountService(
            new PhysicalCountRepository(_context), failingItems.Object,
            new InventoryItemRepository(_context), new WarehouseRepository(_context),
            new WarehouseQuantityRepository(_context), _unitOfWork, _currentUser,
            Mock.Of<IProcurementAccessControlService>(), _adjustments.Object,
            Mock.Of<IProcurementControlEventService>(), NullLogger<PhysicalCountService>.Instance,
            _warehouseDefaults);

        var generate = () => service.GenerateDueCycleCountsAsync(_tenantId, now);

        await generate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Injected line population failure.");
        (await _context.Set<PhysicalCount>().AsNoTracking().CountAsync()).Should().Be(0);
        var unchanged = await _context.Set<InventoryCycleCountSchedule>().AsNoTracking()
            .SingleAsync(value => value.Id == schedule.Id);
        unchanged.NextDueAtUtc.Should().Be(schedule.NextDueAtUtc);
        unchanged.LastGeneratedAtUtc.Should().BeNull();
        unchanged.LastPhysicalCountId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task First_count_replay_hash_covers_lot_and_serial_metadata()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            CountType = CountType.CycleCount,
            ABCClass = "A",
            Notes = "Tracking replay boundary."
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        var request = new RecordCountItemDto
        {
            PhysicalCountItemId = line.Id,
            CountedQuantity = 7m,
            LotNumber = " lot-a ",
            SerialNumber = " serial-a ",
            RowVersion = Convert.ToBase64String(_lineRowVersion),
            IdempotencyKey = "e2e-012-tracking-replay",
            Notes = "Tracked first count."
        };

        (await _counts.RecordCountItemAsync(request, _counterId)).Should().BeTrue();
        (await _counts.RecordCountItemAsync(request, _counterId)).Should().BeTrue();
        var changedReplay = async () => await _counts.RecordCountItemAsync(new RecordCountItemDto
        {
            PhysicalCountItemId = request.PhysicalCountItemId,
            CountedQuantity = request.CountedQuantity,
            LotNumber = "LOT-B",
            SerialNumber = request.SerialNumber,
            RowVersion = request.RowVersion,
            IdempotencyKey = request.IdempotencyKey,
            Notes = request.Notes
        }, _counterId);

        await changedReplay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different action payload or actor*");
        var saved = (await LoadCountAsync(created.Id)).Items.Single();
        saved.LotNumber.Should().Be("lot-a");
        saved.SerialNumber.Should().Be("serial-a");
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Completion_requires_tenant_owned_current_published_clean_stock_taking_evidence()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            CountType = CountType.CycleCount,
            ABCClass = "A",
            Notes = "Central-DMS evidence boundary."
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await _counts.RecordCountItemAsync(new RecordCountItemDto
        {
            PhysicalCountItemId = line.Id,
            CountedQuantity = 10m,
            RowVersion = Convert.ToBase64String(_lineRowVersion),
            IdempotencyKey = "count-evidence-line"
        }, _counterId);

        await AttachCountEvidenceAsync(created.Id, Guid.NewGuid(), FileVirusScanStatus.Clean);
        var crossTenant = async () => await ReviewAndSubmitForTestAsync(created.Id, _counterId);
        await crossTenant.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current published, clean central-DMS stock-taking evidence*");

        var evidence = await AttachCountEvidenceAsync(created.Id, _tenantId, FileVirusScanStatus.Infected);
        var infected = async () => await ReviewAndSubmitForTestAsync(created.Id, _counterId);
        await infected.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*current published, clean central-DMS stock-taking evidence*");

        var upload = await _context.Set<FileUploadRecord>().SingleAsync(value => value.Id == evidence.UploadId);
        upload.VirusScanStatus = FileVirusScanStatus.Clean;
        upload.ScannedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        (await ReviewAndSubmitForTestAsync(created.Id, _counterId)).Should().BeTrue();
        var detail = await _counts.GetByIdAsync(created.Id);
        detail!.Status.Should().Be("PendingStoresApproval");
        detail.Evidence.Should().ContainSingle(value =>
            value.CentralDocumentRecordId == evidence.RecordId &&
            value.CentralDocumentVersionId == evidence.VersionId &&
            value.FileUploadRecordId == evidence.UploadId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Batch", "E2E-012")]
    public async Task Manual_corrections_after_import_use_saved_quantities_without_reupload_but_keep_evidence_checks(bool cleanEvidence)
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto { WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.CycleCount, ABCClass = "A" }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await SaveQuantityAsync(line.Id, 7m, "imported-quantity");
        var evidence = await AttachCountEvidenceAsync(created.Id, _tenantId, cleanEvidence ? FileVirusScanStatus.Clean : FileVirusScanStatus.Infected);
        await _counts.RetainImportedCountSheetAsync(created.Id, new PhysicalCountSheetBinding(evidence.VersionId, "test-import", 1, 0), _counterId, "import-sheet");
        await _counts.ReviewCountAsync(created.Id, _counterId, Mutation("manual-review"));
        await SaveQuantityAsync(line.Id, 8m, "manual-correction");
        var actions = await _context.Set<PhysicalCountAction>().AsNoTracking().Where(a => a.PhysicalCountId == created.Id).ToListAsync();
        PhysicalCountSheetLineage.Current(actions).Should().BeNull();
        actions.Should().Contain(a => PhysicalCountSheetLineage.Read(a) != null);
        var submit = () => _counts.SubmitReviewedCountAsync(created.Id, _counterId, Mutation("submit-manual-correction"));
        if (!cleanEvidence)
        {
            await submit.Should().ThrowAsync<InvalidOperationException>().WithMessage("*current published, clean central-DMS stock-taking evidence*");
            (await LoadCountAsync(created.Id)).Status.Should().Be("UnderReview");
            return;
        }
        (await submit()).Should().BeTrue();
        var saved = await LoadCountAsync(created.Id);
        saved.Status.Should().Be("PendingStoresApproval");
        saved.Items.Single().CountedQuantity.Should().Be(8m);
        _createdAdjustment!.Items.Single().AdjustmentQuantity.Should().Be(-2m);
        (await _counts.GetByIdAsync(created.Id))!.Evidence.Should().Contain(e => e.CentralDocumentVersionId == evidence.VersionId);
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Counter_reviews_corrects_then_submits_for_independent_approvals_and_finance_posting()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto { WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.CycleCount, ABCClass = "A" }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await SaveQuantityAsync(line.Id, 7, "first");
        var detail = await _counts.GetByIdAsync(created.Id);
        detail!.SystemQuantityVisible.Should().BeFalse();
        detail.Items.Single().CountedQuantity.Should().Be(7);
        var tooEarly = () => _counts.SubmitReviewedCountAsync(created.Id, _counterId, Mutation("early"));
        await tooEarly.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review*before submitting*");
        await _counts.ReviewCountAsync(created.Id, _counterId, Mutation("review"));
        detail = await _counts.GetByIdAsync(created.Id);
        detail!.SystemQuantityVisible.Should().BeTrue();
        detail.Items.Single().VarianceQuantity.Should().Be(-3);
        detail.Items.Single().RequiresRecount.Should().BeFalse();
        foreach (var qty in new[] { 6m, 9m, 8m }) await SaveQuantityAsync(line.Id, qty, "correct-" + qty);
        var corrected = await LoadCountAsync(created.Id);
        corrected.Items.Single().FirstCountQuantity.Should().Be(7);
        corrected.Items.Single().CountedQuantity.Should().Be(8);
        corrected.Items.Single().CountAttempts.Should().Be(1);
        _adjustment.Should().BeNull();
        await AttachCleanCountEvidenceAsync(created.Id);
        var submit = Mutation("submit");
        await _counts.SubmitReviewedCountAsync(created.Id, _counterId, submit);
        await _counts.SubmitReviewedCountAsync(created.Id, _counterId, submit);
        (await LoadCountAsync(created.Id)).Status.Should().Be("PendingStoresApproval");
        _createdAdjustment!.Items.Single().AdjustmentQuantity.Should().Be(-2);
        _adjustments.Verify(x => x.CreateAsync(It.IsAny<CreateStockAdjustmentDto>(), _counterId), Times.Once);
        _currentUser.Switch(_counterId, "cycle.counter", "TDC_STORES_MANAGER");
        var selfApproval = () => _counts.ApproveStoresAsync(created.Id, _counterId, Decision("self-approval"));
        await selfApproval.Should().ThrowAsync<InvalidOperationException>().WithMessage("*independent actor*");
        _currentUser.Switch(_storesApproverId, "stores.approver", "TDC_STORES_MANAGER");
        await _counts.ApproveStoresAsync(created.Id, _storesApproverId, Decision("stores"));
        _currentUser.Switch(_financeApproverId, "finance.approver", "TDC_FINANCE_REVIEWER");
        await _counts.ApproveFinanceAsync(created.Id, _financeApproverId, Decision("finance"));
        _currentUser.Switch(_auditUserId, "audit", ProcurementAccessControlRegistry.InternalAuditRole);
        await _counts.AttestAuditAsync(created.Id, _auditUserId, Decision("audit"));
        _currentUser.Switch(_financeApproverId, "finance.approver", "TDC_FINANCE_REVIEWER");
        await _counts.PostControlledAdjustmentsAsync(created.Id, _financeApproverId, Mutation("post"));
        var posted = await LoadCountAsync(created.Id);
        posted.Status.Should().Be("Posted");
        posted.FreezeReleasedAtUtc.Should().NotBeNull();
        _adjustment!.FinancePostingEventId.Should().Be(_postingEventId);
        var actions = await _context.Set<PhysicalCountAction>().Where(a => a.PhysicalCountId == created.Id).ToListAsync();
        actions.Should().NotContain(a => a.ActionType == PhysicalCountActionType.RecountRequired);
        actions.Where(a => a.ActionType == PhysicalCountActionType.StoresApproved).Single().SnapshotJson.Should().Contain("ApproveAdjustment");
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Independent_approval_pipeline_accepts_blank_comments_and_replays_each_stage_once()
    {
        var id = await PrepareForApprovalStageAsync("stores");
        var submitted = await LoadCountAsync(id);
        submitted.TotalVarianceQuantity.Should().Be(-2m);
        submitted.TotalVarianceValue.Should().Be(-20m);
        _createdAdjustment!.WarehouseId.Should().Be(_warehouseId);
        _createdAdjustment.Items.Should().ContainSingle().Which.Should().Match<CreateStockAdjustmentItemDto>(line =>
            line.InventoryItemId == _itemId && line.LocationId == _locationId && line.AdjustmentQuantity == -2m);
        var ownerPostedWithinCountTransaction = false;
        _adjustments.Setup(service => service.PostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StockAdjustmentActionRequest>()))
            .ReturnsAsync(() =>
            {
                ownerPostedWithinCountTransaction = _unitOfWork.HasActiveTransaction;
                return _adjustment = Adjustment("Posted", _postingEventId, _journalEntryId);
            });

        foreach (var stage in new[] { "stores", "finance", "audit", "post" })
        {
            SwitchToStageActor(stage);
            await ExecuteStageAsync(stage, id, _currentUser.ActorId, "replay-" + stage);
            await ExecuteStageAsync(stage, id, _currentUser.ActorId, "replay-" + stage);
        }

        var posted = await LoadCountAsync(id);
        posted.Status.Should().Be("Posted");
        posted.StoresApprovedById.Should().Be(_storesApproverId);
        posted.FinanceApprovedById.Should().Be(_financeApproverId);
        posted.AuditAttestedById.Should().Be(_auditUserId);
        posted.PostedById.Should().Be(_financeApproverId);
        posted.FreezeReleasedAtUtc.Should().NotBeNull();
        ownerPostedWithinCountTransaction.Should().BeTrue("stock/Finance and count history form one retriable transaction");
        _unitOfWork.HasActiveTransaction.Should().BeFalse("the count operation closes the transaction it owns");
        _adjustments.Verify(service => service.DecideAsync(It.IsAny<Guid>(), _storesApproverId,
            It.Is<DecideStockAdjustmentRequest>(request => request.Approved && request.Comment == null)), Times.Once);
        _adjustments.Verify(service => service.PostAsync(It.IsAny<Guid>(), _financeApproverId,
            It.IsAny<StockAdjustmentActionRequest>()), Times.Once);
        var actions = await _context.Set<PhysicalCountAction>().Where(action => action.PhysicalCountId == id).ToListAsync();
        foreach (var type in new[] { PhysicalCountActionType.StoresApproved, PhysicalCountActionType.FinanceApproved,
                     PhysicalCountActionType.AuditAttested, PhysicalCountActionType.Posted })
            actions.Where(action => action.ActionType == type).Should().ContainSingle().Which.Comment.Should().BeNull();
        actions.Single(action => action.ActionType == PhysicalCountActionType.Posted).SnapshotJson
            .Should().Contain(_postingEventId.ToString()).And.Contain(_journalEntryId.ToString());
        // The count service delegates all physical and financial mutations to the
        // adjustment owner. The mock does not substitute for SQL/GL stock checks.
        (await _context.Set<InventoryLocation>().SingleAsync()).Quantity.Should().Be(10m);
        _controlEvents.Count(entry => entry.Action == "Post").Should().Be(1);
    }

    [Theory]
    [InlineData("stores")]
    [InlineData("finance")]
    [InlineData("audit")]
    [InlineData("post")]
    [Trait("Batch", "E2E-012")]
    public async Task Each_control_stage_rejects_an_actor_without_its_required_role(string stage)
    {
        var id = await PrepareForApprovalStageAsync(stage);
        var before = (await LoadCountAsync(id)).Status;
        _currentUser.Switch(StageActor(stage), "wrong-role");

        var action = () => ExecuteStageAsync(stage, id, _currentUser.ActorId, "no-role");

        await action.Should().ThrowAsync<ProcurementAccessAuthorizationException>();
        (await LoadCountAsync(id)).Status.Should().Be(before);
    }

    [Theory]
    [InlineData("stores")]
    [InlineData("finance")]
    [InlineData("audit")]
    [InlineData("post")]
    [Trait("Batch", "E2E-012")]
    public async Task Required_role_does_not_allow_a_prior_actor_to_approve_or_post_the_count(string stage)
    {
        var id = await PrepareForApprovalStageAsync(stage);
        var before = (await LoadCountAsync(id)).Status;
        var prohibitedActor = stage switch
        {
            "stores" => _counterId, "finance" => _storesApproverId,
            "audit" => _financeApproverId, _ => _auditUserId
        };
        _currentUser.Switch(prohibitedActor, "prior-actor", StageRole(stage));

        var action = () => ExecuteStageAsync(stage, id, prohibitedActor, "prior-actor");

        await action.Should().ThrowAsync<InvalidOperationException>();
        (await LoadCountAsync(id)).Status.Should().Be(before);
    }

    [Theory]
    [InlineData("stores")]
    [InlineData("finance")]
    [InlineData("audit")]
    [InlineData("post")]
    [Trait("Batch", "E2E-012")]
    public async Task Approval_role_still_requires_exact_warehouse_location_permission(string stage)
    {
        var id = await PrepareForApprovalStageAsync(stage);
        var before = (await LoadCountAsync(id)).Status;
        SwitchToStageActor(stage);
        _access.Invocations.Clear();
        _access.Setup(service => service.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false, Message = "Location is outside the actor's assignment." });

        var action = () => ExecuteStageAsync(stage, id, _currentUser.ActorId, "out-of-scope");

        await action.Should().ThrowAsync<ProcurementAccessAuthorizationException>().WithMessage("*outside*");
        (await LoadCountAsync(id)).Status.Should().Be(before);
        var permission = stage == "audit" ? "procurement.inventory.read" : "procurement.inventory.adjust.approve";
        _access.Verify(service => service.EnforceCapabilityAsync(It.Is<ProcurementAccessCapabilityRequest>(request =>
            request.PermissionCode == permission && request.WarehouseId == _warehouseId && request.LocationId == _locationId &&
            request.RequireLocationScope && request.SourceType == "PhysicalCount"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Finance_role_cannot_post_a_count_from_another_tenant()
    {
        var id = await PrepareForApprovalStageAsync("post");
        var count = await LoadCountAsync(id);
        count.TenantId = Guid.NewGuid();
        await _context.SaveChangesAsync();
        SwitchToStageActor("post");

        var action = () => ExecuteStageAsync("post", id, _financeApproverId, "foreign-tenant");

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*not found*");
        _adjustments.Verify(service => service.PostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<StockAdjustmentActionRequest>()), Times.Never);
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Failed_authoritative_post_does_not_complete_the_count_or_release_its_freeze()
    {
        var id = await PrepareForApprovalStageAsync("post");
        SwitchToStageActor("post");
        var ownerSawTransaction = false;
        _adjustments.Setup(service => service.PostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StockAdjustmentActionRequest>()))
            .Returns(() =>
            {
                ownerSawTransaction = _unitOfWork.HasActiveTransaction;
                return Task.FromException<StockAdjustmentDetailDto>(new InvalidOperationException("Finance posting failed."));
            });

        var action = () => ExecuteStageAsync("post", id, _financeApproverId, "failed-post");

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Finance posting failed.");
        ownerSawTransaction.Should().BeTrue();
        _unitOfWork.HasActiveTransaction.Should().BeFalse();
        var retained = await LoadCountAsync(id);
        retained.Status.Should().Be("ReadyToPost");
        retained.PostedById.Should().BeNull();
        retained.FreezeReleasedAtUtc.Should().BeNull();
        (await _context.Set<PhysicalCountAction>().AnyAsync(record => record.PhysicalCountId == id &&
            record.ActionType == PhysicalCountActionType.Posted)).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Batch", "E2E-012")]
    public async Task Adjustment_posting_joins_an_existing_count_transaction_on_failure_and_replay(bool replay)
    {
        SwitchToStageActor("post");
        var adjustment = new StockAdjustment
        {
            TenantId = _tenantId, WarehouseId = _warehouseId, AdjustmentNumber = "TRANSACTION-POST",
            RequestedById = _counterId, Status = replay ? "Posted" : "Approved", RowVersion = [1, 2, 3],
            ReasonCode = StockAdjustmentReasonCodes.PhysicalCount
        };
        _context.Add(adjustment);
        if (replay) _context.Add(new StockAdjustmentAction
        {
            TenantId = _tenantId, StockAdjustmentId = adjustment.Id, ActionType = "Posted", ActorUserId = _financeApproverId,
            Sequence = 1, IdempotencyKey = "joined-post", OccurredAtUtc = DateTime.UtcNow,
            SnapshotJson = "{}", IntegrityHash = new string('0', 64)
        });
        await _context.SaveChangesAsync();
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(current => current.UserId).Returns(_financeApproverId);
        actor.SetupGet(current => current.TenantId).Returns(_tenantId);
        actor.SetupGet(current => current.IsAuthenticated).Returns(true);
        var finance = new Mock<IInventoryAdjustmentFinancePostingService>();
        finance.Setup(service => service.PostAsync(It.IsAny<StockAdjustment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Finance fixture failed before mutation."));
        var service = new StockAdjustmentService(Mock.Of<IStockAdjustmentRepository>(), Mock.Of<IInventoryItemRepository>(),
            Mock.Of<IStockMovementRepository>(), Mock.Of<IWarehouseQuantityRepository>(), Mock.Of<IWarehouseLocationRepository>(),
            Mock.Of<IWarehouseRepository>(), Mock.Of<IConsignmentSettlementService>(), actor.Object, _unitOfWork,
            Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(), _access.Object,
            Mock.Of<IProcurementSodGuardService>(), Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementControlEventService>(),
            finance.Object, Mock.Of<IInventoryValuationService>(), NullLogger<StockAdjustmentService>.Instance);
        await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var request = new StockAdjustmentActionRequest { IdempotencyKey = "joined-post", RowVersion = Convert.ToBase64String(adjustment.RowVersion) };
        if (replay)
            (await service.PostAsync(adjustment.Id, _financeApproverId, request)).Status.Should().Be("Posted");
        else
        {
            var action = () => service.PostAsync(adjustment.Id, _financeApproverId, request);
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Finance fixture failed before mutation.");
        }
        _unitOfWork.HasActiveTransaction.Should().BeTrue("only the enclosing count may close its transaction");
        await _unitOfWork.RollbackAsync();
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Investigation_retires_adjustment_and_recovers_legacy_committee_into_retained_child_recount()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto { WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.CycleCount, ABCClass = "A" }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await SaveQuantityAsync(line.Id, 7, "first");
        await AttachCleanCountEvidenceAsync(created.Id);
        await ReviewAndSubmitForTestAsync(created.Id, _counterId);
        _currentUser.Switch(_storesApproverId, "stores", "TDC_STORES_MANAGER");
        await _counts.ApproveStoresAsync(created.Id, _storesApproverId, Decision("stores"));
        _currentUser.Switch(_financeApproverId, "finance", "TDC_FINANCE_REVIEWER");
        var decision = Decision("investigate");
        decision.Approved = false;
        decision.DecisionCode = "INVESTIGATE";
        decision.Reason = "Check the delivery count entry.";
        await _counts.ApproveFinanceAsync(created.Id, _financeApproverId, decision);
        var returned = await LoadCountAsync(created.Id);
        returned.Status.Should().Be("UnderInvestigation");
        returned.StockAdjustmentId.Should().BeNull();
        returned.Items.Single().RequiresRecount.Should().BeFalse();
        returned.Items.Single().FirstCountQuantity.Should().Be(7);
        _adjustment!.Status.Should().Be("Cancelled");
        var post = () => _counts.PostControlledAdjustmentsAsync(created.Id, _financeApproverId, Mutation("blocked"));
        await post.Should().ThrowAsync<InvalidOperationException>();
        _currentUser.Switch(_counterId, "cycle.counter");
        var correction = () => SaveQuantityAsync(line.Id, 8, "locked");
        await correction.Should().ThrowAsync<InvalidOperationException>().WithMessage("*locked*");
        await RecoverLegacyRecountAndAssertAsync(created.Id, line.Id);
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Opposing_line_variances_with_zero_net_total_still_create_a_governed_adjustment()
    {
        var secondItemId = Guid.NewGuid();
        var categoryId = await _context.Set<InventoryItem>()
            .Where(value => value.Id == _itemId)
            .Select(value => value.CategoryId)
            .SingleAsync();
        await _context.AddRangeAsync(
            new InventoryItem
            {
                Id = secondItemId,
                TenantId = _tenantId,
                ItemCode = "ABC-A-002",
                Name = "Offsetting ABC item",
                CategoryId = categoryId,
                UnitOfMeasure = "EA",
                Status = ItemStatus.Active,
                ABCClass = "A",
                StandardCost = 10m,
                AverageCost = 10m,
                CurrentStock = 10m,
                AvailableStock = 10m
            },
            new WarehouseQuantity
            {
                TenantId = _tenantId,
                WarehouseId = _warehouseId,
                InventoryItemId = secondItemId,
                CurrentStock = 10m,
                AvailableStock = 10m,
                AverageCost = 10m
            },
            new InventoryLocation
            {
                TenantId = _tenantId,
                InventoryItemId = secondItemId,
                LocationId = _locationId,
                Quantity = 10m,
                AvailableQuantity = 10m,
                AverageCost = 10m
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            CountType = CountType.CycleCount,
            ABCClass = "A",
            Notes = "Zero-net, non-zero line variances."
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);

        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var lines = (await LoadCountAsync(created.Id)).Items
            .OrderBy(value => value.ItemCode)
            .ToList();
        foreach (var (line, quantity, key) in new[]
                 {
                     (lines[0], 15m, "zero-net-first-plus"),
                     (lines[1], 5m, "zero-net-first-minus")
                 })
        {
            await _counts.RecordCountItemAsync(new RecordCountItemDto
            {
                PhysicalCountItemId = line.Id,
                CountedQuantity = quantity,
                RowVersion = Convert.ToBase64String(_lineRowVersion),
                IdempotencyKey = key
            }, _counterId);
        }
        await AttachCleanCountEvidenceAsync(created.Id);
        await ReviewAndSubmitForTestAsync(created.Id, _counterId);
        (await LoadCountAsync(created.Id)).TotalVarianceQuantity.Should().Be(0m);

        var submitted = await LoadCountAsync(created.Id);
        submitted.Status.Should().Be("PendingStoresApproval");
        submitted.TotalVarianceQuantity.Should().Be(0m);
        submitted.StockAdjustmentId.Should().Be(_adjustment!.Id);
        _createdAdjustment.Should().NotBeNull();
        _createdAdjustment!.Items.Should().HaveCount(2);
        _createdAdjustment.Items.Select(value => value.AdjustmentQuantity)
            .Should().BeEquivalentTo([5m, -5m]);
    }

    public Task DisposeAsync()
    {
        _unitOfWork.Dispose();
        return Task.CompletedTask;
    }

    private void ConfigureAdjustmentOwner()
    {
        _adjustments.Setup(service => service.CreateAsync(It.IsAny<CreateStockAdjustmentDto>(), It.IsAny<Guid>()))
            .Returns(async (CreateStockAdjustmentDto request, Guid actor) =>
            {
                _createdAdjustment = request;
                _adjustment = Adjustment("Draft");
                await _context.AddAsync(new StockAdjustment {
                    Id = _adjustment.Id, TenantId = _tenantId, WarehouseId = request.WarehouseId,
                    AdjustmentNumber = _adjustment.AdjustmentNumber, ReasonCode = request.ReasonCode,
                    Reference = request.Reference ?? string.Empty, RequestedById = actor, IdempotencyKey = request.IdempotencyKey,
                    Items = request.Items.Select(item => new StockAdjustmentItem {
                        Id = Guid.NewGuid(), TenantId = _tenantId, AdjustmentId = _adjustment.Id,
                        InventoryItemId = item.InventoryItemId, LocationId = item.LocationId,
                        AdjustmentQuantity = item.AdjustmentQuantity, UnitCost = item.UnitCost ?? 0m,
                        LotNumber = item.LotNumber, SerialNumber = item.SerialNumber
                    }).ToList()
                });
                await _context.SaveChangesAsync();
                return _adjustment;
            });
        _adjustments.Setup(service => service.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => _adjustment);
        _adjustments.Setup(service => service.SubmitAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StockAdjustmentActionRequest>()))
            .ReturnsAsync(() => _adjustment = Adjustment("PendingApproval"));
        _adjustments.Setup(service => service.DecideAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DecideStockAdjustmentRequest>()))
            .ReturnsAsync(() => _adjustment = Adjustment("Approved"));
        _adjustments.Setup(service => service.PostAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StockAdjustmentActionRequest>()))
            .ReturnsAsync(() => _adjustment = Adjustment("Posted", _postingEventId, _journalEntryId));
        _adjustments.Setup(service => service.RetireApprovedForRecountAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<StockAdjustmentActionRequest>()))
            .ReturnsAsync(() => _adjustment = Adjustment("Cancelled"));
    }

    private async Task<InventoryCycleCountSchedule> SeedDueScheduleAsync(DateTime now)
    {
        var occurrence = new ProcurementCalendarOccurrence
        {
            TenantId = _tenantId,
            OccurrenceKey = $"cycle-count-{Guid.NewGuid():N}",
            ProfileId = Guid.NewGuid(),
            ProfileKey = Guid.NewGuid(),
            RuleId = Guid.NewGuid(),
            RuleKey = Guid.NewGuid(),
            ProfileVersion = 1,
            EventType = ProcurementCalendarEventType.CycleCount,
            CalendarYear = now.Year,
            Title = "Scheduled cycle count",
            DueAtUtc = now,
            DueLocal = now,
            TimeZoneId = "UTC",
            OwnerUserId = _initiatorId,
            OwnerName = "Cycle Initiator",
            OwnerRoleName = "TDC_STORES_MANAGER",
            EscalationUserId = _storesApproverId,
            EscalationOwnerName = "Stores Approver",
            StatutoryReference = "TDC-0609",
            Status = ProcurementCalendarOccurrenceStatus.Due,
            GeneratedAtUtc = now.AddDays(-1)
        };
        var schedule = new InventoryCycleCountSchedule
        {
            TenantId = _tenantId,
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            ABCClass = "A",
            FrequencyDays = 30,
            NextDueAtUtc = now.AddMinutes(-1),
            CalendarOccurrenceId = occurrence.Id,
            CutoffOccurrenceId = Guid.NewGuid(),
            CutoffAtUtc = now.AddDays(90),
            FreezeInventory = true,
            BlindCount = true,
            RecountQuantityThreshold = 1m,
            RecountValueThreshold = 10m,
            IsActive = true
        };
        await _context.AddRangeAsync(occurrence, schedule);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return schedule;
    }

    private StockAdjustmentDetailDto Adjustment(string status, Guid? postingEventId = null, Guid? journalEntryId = null) => new()
    {
        Id = _adjustment?.Id ?? Guid.NewGuid(),
        AdjustmentNumber = "ADJ-E2E-012",
        AdjustmentDate = DateTime.UtcNow,
        WarehouseId = _warehouseId,
        ReasonCode = StockAdjustmentReasonCodes.CycleCount,
        Reference = "E2E-012",
        Status = status,
        RequestedById = _recountUserId,
        FinancePostingEventId = postingEventId,
        FinanceJournalEntryId = journalEntryId,
        RowVersion = Convert.ToBase64String([9, 10, 11, 12])
    };

    private PhysicalCountMutationRequest Mutation(string key) => new() { RowVersion = Convert.ToBase64String(_countRowVersion), IdempotencyKey = key };
    private Task<bool> SaveQuantityAsync(Guid lineId, decimal qty, string key) => _counts.RecordCountItemAsync(new RecordCountItemDto {
        PhysicalCountItemId = lineId, CountedQuantity = qty, RowVersion = Convert.ToBase64String(_lineRowVersion), IdempotencyKey = key
    }, _counterId);
    private async Task<bool> ReviewAndSubmitForTestAsync(Guid id, Guid actor)
    {
        if ((await LoadCountAsync(id)).Status == "InProgress") await _counts.ReviewCountAsync(id, actor, Mutation("review-" + id));
        return await _counts.SubmitReviewedCountAsync(id, actor, Mutation("submit-" + id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Batch", "E2E-012")]
    public async Task Count_transaction_preserves_original_failure_when_commit_already_disposed_the_transaction(bool commitFailed)
    {
        var active = false;
        var original = new InvalidOperationException(commitFailed ? "Original commit failure." : "Original operation failure.");
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(value => value.HasActiveTransaction).Returns(() => active);
        unit.Setup(value => value.ExecuteInStrategyAsync(It.IsAny<Func<Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<bool>> operation, CancellationToken _) => operation());
        unit.Setup(value => value.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
            .Callback(() => active = true).Returns(Task.CompletedTask);
        unit.Setup(value => value.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unit.Setup(value => value.CommitAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            active = false; // UnitOfWork.CommitAsync disposes/clears in its finally block.
            return Task.FromException(original);
        });
        unit.Setup(value => value.RollbackAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            if (!active) return Task.FromException(new InvalidOperationException("No transaction to rollback"));
            active = false;
            return Task.CompletedTask;
        });
        var service = new PhysicalCountService(new PhysicalCountRepository(_context), Mock.Of<IPhysicalCountItemRepository>(),
            Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), Mock.Of<IWarehouseQuantityRepository>(),
            unit.Object, _currentUser, _access.Object, _adjustments.Object, Mock.Of<IProcurementControlEventService>(),
            NullLogger<PhysicalCountService>.Instance, _warehouseDefaults);
        Func<Task<bool>> operation = () => commitFailed ? Task.FromResult(true) : Task.FromException<bool>(original);
        var method = typeof(PhysicalCountService).GetMethod("InCountTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var action = async () => await (Task<bool>)method.Invoke(service, [Guid.NewGuid(), operation])!;

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(original);
        unit.Verify(value => value.RollbackAsync(It.IsAny<CancellationToken>()), commitFailed ? Times.Never() : Times.Once());
        unit.Verify(value => value.ClearTrackedChanges(), Times.Exactly(2));
        active.Should().BeFalse();
    }

    private async Task<Guid> PrepareForApprovalStageAsync(string stage)
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId, LocationId = _locationId, CountType = CountType.CycleCount, ABCClass = "A"
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        await SaveQuantityAsync((await LoadCountAsync(created.Id)).Items.Single().Id, 8m, "pipeline-count");
        await AttachCleanCountEvidenceAsync(created.Id);
        await ReviewAndSubmitForTestAsync(created.Id, _counterId);
        foreach (var prior in new[] { "stores", "finance", "audit" })
        {
            if (prior == stage) return created.Id;
            SwitchToStageActor(prior);
            await ExecuteStageAsync(prior, created.Id, _currentUser.ActorId, "prepare-" + prior);
        }
        return created.Id;
    }

    private Guid StageActor(string stage) => stage switch
    {
        "stores" => _storesApproverId, "audit" => _auditUserId, _ => _financeApproverId
    };

    private static string StageRole(string stage) => stage switch
    {
        "stores" => "TDC_STORES_MANAGER", "audit" => ProcurementAccessControlRegistry.InternalAuditRole,
        _ => "TDC_FINANCE_REVIEWER"
    };

    private void SwitchToStageActor(string stage) =>
        _currentUser.Switch(StageActor(stage), stage + ".reviewer", StageRole(stage));

    private Task<bool> ExecuteStageAsync(string stage, Guid id, Guid actorId, string key)
    {
        var request = Decision(key);
        request.Comment = null;
        return stage switch
        {
            "stores" => _counts.ApproveStoresAsync(id, actorId, request),
            "finance" => _counts.ApproveFinanceAsync(id, actorId, request),
            "audit" => _counts.AttestAuditAsync(id, actorId, request),
            _ => _counts.PostControlledAdjustmentsAsync(id, actorId, Mutation(key))
        };
    }

    private PhysicalCountDecisionRequest Decision(string key) => new()
    {
        Approved = true,
        DecisionCode = "APPROVE",
        DecisionRevision = PhysicalCountDecisionPolicy.Read(null).Revision,
        RowVersion = Convert.ToBase64String(_countRowVersion),
        IdempotencyKey = key,
        CorrelationId = "e2e-012",
        Comment = "Independent representative approval."
    };

    private async Task SetRowVersionsAsync(Guid countId)
    {
        var count = await _context.Set<PhysicalCount>().Include(value => value.Items)
            .SingleAsync(value => value.Id == countId);
        count.RowVersion = _countRowVersion;
        foreach (var item in count.Items) item.RowVersion = _lineRowVersion;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private async Task<PhysicalCount> LoadCountAsync(Guid countId)
    {
        _context.ChangeTracker.Clear();
        return await _context.Set<PhysicalCount>()
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == countId);
    }

    private Task<CountEvidenceIds> AttachCleanCountEvidenceAsync(Guid countId) =>
        AttachCountEvidenceAsync(countId, _tenantId, FileVirusScanStatus.Clean);

    private async Task<CountEvidenceIds> AttachCountEvidenceAsync(
        Guid countId,
        Guid tenantId,
        FileVirusScanStatus scanStatus)
    {
        var recordId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();
        var documentReference = $"COUNT-EVIDENCE-{recordId:N}";
        await _context.AddRangeAsync(
            new FileUploadRecord
            {
                Id = uploadId,
                TenantId = tenantId,
                Category = "inventory-stock-taking-evidence",
                FilePath = $"protected/{uploadId:N}.pdf",
                StoredFileName = $"{uploadId:N}.pdf",
                OriginalFileName = "signed-stock-count-sheet.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                StorageProvider = "TestDms",
                UploadedByUserId = _counterId,
                VirusScanStatus = scanStatus,
                ScannedAtUtc = DateTime.UtcNow
            },
            new CentralDocumentRecord
            {
                Id = recordId,
                TenantId = tenantId,
                DocumentReference = documentReference,
                Title = "Signed physical stock count sheet",
                SourceModule = "Inventory",
                SourceLabel = "Physical stock-taking evidence",
                SourceEntityType = "PhysicalCount",
                SourceRecordId = countId,
                SourceRecordReference = countId.ToString(),
                RepositoryStatus = "Linked",
                CurrentVersion = "v1.0",
                VersionStatus = "Published",
                LifecycleStatus = "Active",
                PublishedAt = DateTime.UtcNow,
                PublishedById = _counterId
            },
            new CentralDocumentVersion
            {
                Id = versionId,
                TenantId = tenantId,
                DocumentRecordId = recordId,
                VersionNumber = "v1.0",
                Status = "Published",
                FileName = "signed-stock-count-sheet.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                FileUploadRecordId = uploadId,
                CreatedByUserId = _counterId,
                PublishedAt = DateTime.UtcNow,
                PublishedById = _counterId
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return new CountEvidenceIds(recordId, versionId, uploadId);
    }

    private ProcurementAccessCapabilityDecisionDto Allowed(ProcurementAccessCapabilityRequest request, string correlationId) => new()
    {
        Allowed = true,
        Code = "ALLOWED",
        Message = "Representative E2E actor is in scope.",
        ActorUserId = _currentUser.ActorId,
        TenantId = _tenantId,
        PermissionCode = request.PermissionCode,
        WarehouseId = request.WarehouseId,
        LocationId = request.LocationId,
        CorrelationId = correlationId,
        EvaluatedAtUtc = DateTime.UtcNow
    };

    private ApplicationUser User(Guid id, string username) => new()
    {
        Id = id,
        TenantId = _tenantId,
        UserName = username,
        NormalizedUserName = username.ToUpperInvariant(),
        Email = $"{username}@e2e.local",
        NormalizedEmail = $"{username}@e2e.local".ToUpperInvariant(),
        FirstName = username,
        LastName = "E2E",
        IsActive = true,
        EmailConfirmed = true
    };

    private sealed record CountEvidenceIds(Guid RecordId, Guid VersionId, Guid UploadId);

    private sealed class MutableCurrentUser(Guid tenantId, Guid userId, string username) : ICurrentUserService
    {
        public Guid ActorId { get; private set; } = userId;
        public string? UserId => ActorId.ToString();
        public string? UserName { get; private set; } = username;
        public string FullName => UserName ?? username;
        public string? Email => $"{UserName}@e2e.local";
        public Guid? TenantId { get; } = tenantId;
        public Guid? EmployeeId => null;
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles { get; private set; } = [];
        public IDictionary<string, string> Claims { get; } = new Dictionary<string, string>();
        public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "E2E-012";

        public void Switch(Guid nextUserId, string nextUsername, params string[] roles)
        {
            ActorId = nextUserId;
            UserName = nextUsername;
            Roles = roles;
        }
    }
}
