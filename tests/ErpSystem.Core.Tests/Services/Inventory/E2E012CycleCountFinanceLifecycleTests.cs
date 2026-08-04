using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
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
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class E2E012CycleCountFinanceLifecycleTests : IAsyncLifetime
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
    private readonly PhysicalCountService _counts;
    private readonly Mock<IStockAdjustmentService> _adjustments = new();
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

        var access = new Mock<IProcurementAccessControlService>();
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
            NullLogger<PhysicalCountService>.Instance);
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
        await _context.AddRangeAsync(
            User(_initiatorId, "cycle.initiator"),
            User(_counterId, "cycle.counter"),
            User(_recountUserId, "cycle.recounter"),
            User(_storesApproverId, "stores.approver"),
            User(_financeApproverId, "finance.approver"),
            User(_auditUserId, "audit.attestor"),
            category,
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
                IsPickingLocation = true
            },
            new InventoryItem
            {
                Id = _itemId,
                TenantId = _tenantId,
                ItemCode = "ABC-A-001",
                Name = "Representative ABC item",
                CategoryId = category.Id,
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
            });
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
        await _context.AddRangeAsync(
            new InventoryLocation
            {
                TenantId = _tenantId, InventoryItemId = _itemId, LocationId = _locationId,
                Quantity = 4m, AvailableQuantity = 4m, AverageCost = 23m
            },
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
        await _context.AddAsync(new InventoryLocation
        {
            TenantId = _tenantId, InventoryItemId = _itemId, LocationId = _locationId,
            Quantity = 4m, AvailableQuantity = 4m, AverageCost = 10m
        });
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
            Mock.Of<IProcurementControlEventService>(), NullLogger<PhysicalCountService>.Instance);

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
    public async Task Abc_blind_count_recount_dual_approval_and_audit_attestation_reconcile_to_finance_posting()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            CountType = CountType.CycleCount,
            ABCClass = "A",
            Notes = "Representative E2E-012 ABC count."
        }, _initiatorId);
        created.FreezeInventory.Should().BeTrue();
        created.BlindCount.Should().BeTrue();

        await SetRowVersionsAsync(created.Id);
        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var started = await LoadCountAsync(created.Id);
        started.Status.Should().Be("InProgress");
        started.FreezeStartedAtUtc.Should().NotBeNull();
        started.BlindCount.Should().BeTrue();
        var line = started.Items.Single();

        await _counts.RecordCountItemAsync(new RecordCountItemDto
        {
            PhysicalCountItemId = line.Id,
            CountedQuantity = 7m,
            RowVersion = Convert.ToBase64String(_lineRowVersion),
            IdempotencyKey = "e2e-012-first-count",
            Notes = "Blind first count found a shortage."
        }, _counterId);
        var blindDetail = await _counts.GetByIdAsync(created.Id);
        blindDetail!.ItemsWithVariance.Should().Be(0);
        blindDetail.TotalVarianceValue.Should().Be(0m);
        blindDetail.SystemQuantityVisible.Should().BeFalse();
        var blindList = (await _counts.GetAllAsync()).Single(value => value.Id == created.Id);
        blindList.ItemsWithVariance.Should().Be(0);
        blindList.TotalVarianceValue.Should().Be(0m);
        await _counts.CompleteCountAsync(created.Id, _counterId);
        var recountRequired = await LoadCountAsync(created.Id);
        recountRequired.Status.Should().Be("RecountRequired");
        recountRequired.Items.Single().Should().Match<PhysicalCountItem>(value =>
            value.FirstCountQuantity == 7m && value.VarianceQuantity == -3m && value.RequiresRecount);

        _currentUser.Switch(_recountUserId, "cycle.recounter");
        var recountRequest = new RecordPhysicalCountRecountRequest
        {
            PhysicalCountItemId = line.Id,
            RecountedQuantity = 8m,
            InvestigationNotes = "Independent recount confirmed two units missing.",
            RowVersion = Convert.ToBase64String(_countRowVersion),
            ItemRowVersion = Convert.ToBase64String(_lineRowVersion),
            IdempotencyKey = "e2e-012-recount",
            CorrelationId = "e2e-012",
            Comment = "Submit confirmed variance for independent decisions."
        };
        await _counts.RecordRecountAsync(created.Id, _recountUserId, recountRequest);
        var recounted = await LoadCountAsync(created.Id);
        recounted.Status.Should().Be("PendingStoresApproval");
        recounted.TotalVarianceQuantity.Should().Be(-2m);
        recounted.TotalVarianceValue.Should().Be(-20m);
        recounted.StockAdjustmentId.Should().Be(_adjustment!.Id);
        _createdAdjustment.Should().NotBeNull();
        _createdAdjustment!.ReasonCode.Should().Be(StockAdjustmentReasonCodes.CycleCount);
        _createdAdjustment.Items.Should().ContainSingle().Which.Should().Match<CreateStockAdjustmentItemDto>(value =>
            value.InventoryItemId == _itemId && value.LocationId == _locationId &&
            value.AdjustmentQuantity == -2m && value.UnitCost == 10m);

        recounted.RowVersion = [20, 21, 22, 23];
        recounted.Items.Single().RowVersion = [24, 25, 26, 27];
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        (await _counts.RecordRecountAsync(created.Id, _recountUserId, recountRequest)).Should().BeTrue(
            "an identical retry must replay before evaluating its now-stale row versions");
        var changedReplay = async () => await _counts.RecordRecountAsync(created.Id, _recountUserId,
            new RecordPhysicalCountRecountRequest
            {
                PhysicalCountItemId = line.Id,
                RecountedQuantity = 9m,
                InvestigationNotes = recountRequest.InvestigationNotes,
                RowVersion = recountRequest.RowVersion,
                ItemRowVersion = recountRequest.ItemRowVersion,
                IdempotencyKey = recountRequest.IdempotencyKey,
                CorrelationId = recountRequest.CorrelationId,
                Comment = recountRequest.Comment
            });
        await changedReplay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*idempotency key*payload or actor*");
        var restored = await LoadCountAsync(created.Id);
        restored.RowVersion = _countRowVersion;
        restored.Items.Single().RowVersion = _lineRowVersion;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _currentUser.Switch(_storesApproverId, "stores.approver", "TDC_STORES_MANAGER");
        await _counts.ApproveStoresAsync(created.Id, _storesApproverId, Decision("e2e-012-stores"));
        (await LoadCountAsync(created.Id)).Status.Should().Be("PendingFinanceApproval");

        _currentUser.Switch(_financeApproverId, "finance.approver", "TDC_FINANCE_REVIEWER");
        await _counts.ApproveFinanceAsync(created.Id, _financeApproverId, Decision("e2e-012-finance"));
        (await LoadCountAsync(created.Id)).Status.Should().Be("PendingAuditAttestation");

        _currentUser.Switch(_auditUserId, "audit.attestor", ProcurementAccessControlRegistry.InternalAuditRole);
        await _counts.AttestAuditAsync(created.Id, _auditUserId, Decision("e2e-012-audit"));
        (await LoadCountAsync(created.Id)).Status.Should().Be("ReadyToPost");

        _currentUser.Switch(_financeApproverId, "finance.approver", "TDC_FINANCE_REVIEWER");
        await _counts.PostControlledAdjustmentsAsync(created.Id, _financeApproverId, new PhysicalCountMutationRequest
        {
            RowVersion = Convert.ToBase64String(_countRowVersion),
            IdempotencyKey = "e2e-012-post",
            CorrelationId = "e2e-012",
            Comment = "Post the approved variance through the authoritative Finance adapter."
        });

        var posted = await LoadCountAsync(created.Id);
        posted.Status.Should().Be("Posted");
        posted.PostedById.Should().Be(_financeApproverId);
        posted.FreezeReleasedAtUtc.Should().NotBeNull();
        _adjustment!.Status.Should().Be("Posted");
        _adjustment.FinancePostingEventId.Should().Be(_postingEventId);
        _adjustment.FinanceJournalEntryId.Should().Be(_journalEntryId);

        var actions = await _context.Set<PhysicalCountAction>()
            .Where(value => value.PhysicalCountId == created.Id)
            .OrderBy(value => value.Sequence)
            .ToListAsync();
        actions.Select(value => value.ActionType).Should().ContainInOrder(
            PhysicalCountActionType.Created,
            PhysicalCountActionType.Started,
            PhysicalCountActionType.CountRecorded,
            PhysicalCountActionType.RecountRequired,
            PhysicalCountActionType.RecountRecorded,
            PhysicalCountActionType.StoresApproved,
            PhysicalCountActionType.FinanceApproved,
            PhysicalCountActionType.AuditAttested,
            PhysicalCountActionType.Posted);
        var postedAction = actions.Single(value => value.ActionType == PhysicalCountActionType.Posted);
        postedAction.SnapshotJson.Should().Contain(_postingEventId.ToString());
        postedAction.SnapshotJson.Should().Contain(_journalEntryId.ToString());
        postedAction.IntegrityHash.Should().HaveLength(64);
        _controlEvents.Select(value => value.Action).Should().Contain(["Complete", "Recount", "StoresApprove", "FinanceApprove", "AuditAttest", "Post"]);

        _adjustments.Verify(service => service.CreateAsync(It.IsAny<CreateStockAdjustmentDto>(), _recountUserId), Times.Once);
        _adjustments.Verify(service => service.SubmitAsync(It.IsAny<Guid>(), _recountUserId, It.IsAny<StockAdjustmentActionRequest>()), Times.Once);
        _adjustments.Verify(service => service.DecideAsync(It.IsAny<Guid>(), _storesApproverId, It.IsAny<DecideStockAdjustmentRequest>()), Times.Once);
        _adjustments.Verify(service => service.PostAsync(It.IsAny<Guid>(), _financeApproverId, It.IsAny<StockAdjustmentActionRequest>()), Times.Once);
    }

    [Fact]
    [Trait("Batch", "E2E-012")]
    public async Task Finance_rejection_retires_the_approved_adjustment_and_clears_prior_recount_markers()
    {
        var created = await _counts.CreateAsync(new CreatePhysicalCountDto
        {
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            CountType = CountType.CycleCount,
            ABCClass = "A",
            Notes = "Finance rejection lifecycle."
        }, _initiatorId);
        await SetRowVersionsAsync(created.Id);

        _currentUser.Switch(_counterId, "cycle.counter");
        await _counts.StartCountAsync(created.Id, _counterId);
        var line = (await LoadCountAsync(created.Id)).Items.Single();
        await _counts.RecordCountItemAsync(new RecordCountItemDto
        {
            PhysicalCountItemId = line.Id,
            CountedQuantity = 7m,
            RowVersion = Convert.ToBase64String(_lineRowVersion),
            IdempotencyKey = "finance-reject-first-count"
        }, _counterId);
        var concealedDetail = await _counts.GetByIdAsync(created.Id);
        concealedDetail!.ItemsWithVariance.Should().Be(0);
        concealedDetail.TotalVarianceValue.Should().Be(0m);
        concealedDetail.SystemQuantityVisible.Should().BeFalse();
        var concealedList = (await _counts.GetAllAsync()).Single(value => value.Id == created.Id);
        concealedList.ItemsWithVariance.Should().Be(0);
        concealedList.TotalVarianceValue.Should().Be(0m);
        await _counts.CompleteCountAsync(created.Id, _counterId);

        _currentUser.Switch(_recountUserId, "cycle.recounter");
        await _counts.RecordRecountAsync(created.Id, _recountUserId,
            new RecordPhysicalCountRecountRequest
            {
                PhysicalCountItemId = line.Id,
                RecountedQuantity = 8m,
                InvestigationNotes = "Independent recount confirmed the shortage.",
                RowVersion = Convert.ToBase64String(_countRowVersion),
                ItemRowVersion = Convert.ToBase64String(_lineRowVersion),
                IdempotencyKey = "finance-reject-recount",
                CorrelationId = "finance-reject"
            });

        _currentUser.Switch(_storesApproverId, "stores.approver", "TDC_STORES_MANAGER");
        await _counts.ApproveStoresAsync(created.Id, _storesApproverId,
            Decision("finance-reject-stores"));
        var awaitingFinance = await LoadCountAsync(created.Id);
        awaitingFinance.StockAdjustmentId.Should().Be(_adjustment!.Id);
        awaitingFinance.Items.Single().RecountedQuantity.Should().Be(8m);
        _adjustment.Status.Should().Be("Approved");

        _currentUser.Switch(_financeApproverId, "finance.approver", "TDC_FINANCE_REVIEWER");
        await _counts.ApproveFinanceAsync(created.Id, _financeApproverId,
            new PhysicalCountDecisionRequest
            {
                Approved = false,
                Reason = "The valuation evidence requires a fresh independent recount.",
                RowVersion = Convert.ToBase64String(_countRowVersion),
                IdempotencyKey = "finance-reject-decision",
                CorrelationId = "finance-reject"
            });

        var returned = await LoadCountAsync(created.Id);
        returned.Status.Should().Be("RecountRequired");
        returned.StockAdjustmentId.Should().BeNull();
        returned.Items.Single().Should().Match<PhysicalCountItem>(value =>
            value.RequiresRecount && value.RecountedQuantity == null &&
            value.RecountedAtUtc == null && value.RecountedById == null);
        _adjustment!.Status.Should().Be("Cancelled");
        _adjustments.Verify(service => service.RetireApprovedForRecountAsync(
            It.IsAny<Guid>(), _financeApproverId,
            It.Is<StockAdjustmentActionRequest>(request =>
                request.IdempotencyKey.Contains("finance-reject-decision"))), Times.Once);
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
        await _counts.CompleteCountAsync(created.Id, _counterId);
        (await LoadCountAsync(created.Id)).TotalVarianceQuantity.Should().Be(0m);

        _currentUser.Switch(_recountUserId, "cycle.recounter");
        foreach (var (line, quantity, key) in new[]
                 {
                     (lines[0], 15m, "zero-net-recount-plus"),
                     (lines[1], 5m, "zero-net-recount-minus")
                 })
        {
            await _counts.RecordRecountAsync(created.Id, _recountUserId,
                new RecordPhysicalCountRecountRequest
                {
                    PhysicalCountItemId = line.Id,
                    RecountedQuantity = quantity,
                    InvestigationNotes = "Independent recount confirmed this line variance.",
                    RowVersion = Convert.ToBase64String(_countRowVersion),
                    ItemRowVersion = Convert.ToBase64String(_lineRowVersion),
                    IdempotencyKey = key,
                    CorrelationId = "zero-net-lines"
                });
        }

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
            .ReturnsAsync((CreateStockAdjustmentDto request, Guid _) =>
            {
                _createdAdjustment = request;
                _adjustment = Adjustment("Draft");
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

    private PhysicalCountDecisionRequest Decision(string key) => new()
    {
        Approved = true,
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

    private sealed class MutableCurrentUser(Guid tenantId, Guid userId, string username) : ICurrentUserService
    {
        public Guid ActorId { get; private set; } = userId;
        public string? UserId => ActorId.ToString();
        public string? UserName { get; private set; } = username;
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
