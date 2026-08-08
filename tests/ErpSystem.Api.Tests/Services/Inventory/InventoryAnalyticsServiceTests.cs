using ErpSystem.Api.Services.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Inventory;

public sealed class InventoryAnalyticsServiceTests
{
    [Fact, Trait("Batch", "TDC-0614")]
    public async Task Analytics_reconciles_ageing_expiry_movement_and_existing_replenishment_by_location()
    {
        await using var fixture = await Fixture.CreateAsync(allowAccess: true);

        var result = await fixture.Service.GetAsync(null, null, 90, 180, 90, 500);

        result.Summary.ItemLocationCount.Should().Be(2);
        result.Summary.InventoryValue.Should().Be(100m);
        result.Summary.ExpiredQuantity.Should().Be(10m);
        result.Summary.NonMovingCount.Should().Be(1);
        result.Summary.StockoutCount.Should().Be(1);
        result.AgeingBands.Single(value => value.Key == "D181_365").Value.Should().Be(100m);
        var obsolete = result.Items.Single(value => value.ItemCode == "OLD-001");
        obsolete.ActivityClassification.Should().Be(InventoryActivityClassification.NonMoving);
        obsolete.DisposalCandidate.Should().BeTrue();
        obsolete.RecommendedActionCode.Should().Be("DISPOSAL_REVIEW");
        obsolete.LocationCode.Should().Be("A-01");
        var stockout = result.Items.Single(value => value.ItemCode == "FAST-001");
        stockout.ActivityClassification.Should().Be(InventoryActivityClassification.Stockout);
        stockout.AverageDailyDemand.Should().Be(0.1m);
        stockout.ReplenishmentRecommendationNumber.Should().Be("IRR-001");
        stockout.RecommendedActionCode.Should().Be("FOLLOW_REPLENISHMENT");
    }

    [Fact, Trait("Batch", "TDC-0614")]
    public async Task Analytics_uses_traceability_balances_for_non_fifo_expiry_exposure()
    {
        await using var fixture = await Fixture.CreateAsync(allowAccess: true);
        var tenantId = fixture.Db.InventoryBalances.Select(value => value.TenantId).First();
        var categoryId = fixture.Db.InventoryCategories.Select(value => value.Id).First();
        var warehouse = await fixture.Db.Warehouses.SingleAsync();
        var location = await fixture.Db.WarehouseLocations.SingleAsync();
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId,
            ItemCode = "WA-EXP-001", Name = "Weighted tracked item", UnitOfMeasure = "EA",
            Status = ItemStatus.Active, ValuationMethod = ValuationMethod.WeightedAverage
        };
        fixture.Db.InventoryItems.Add(item);
        fixture.Db.InventoryBalances.Add(new InventoryBalance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = item.Id,
            WarehouseId = warehouse.Id, LocationId = location.Id, QuantityOnHand = 6m,
            QuantityAvailable = 6m, TotalValue = 30m, AverageUnitCost = 5m,
            LastReceiptDate = DateTime.UtcNow.AddDays(-30), InventoryItem = item,
            Warehouse = warehouse, Location = location
        });
        fixture.Db.InventoryTraceabilityEvents.AddRange(
            Fixture.TraceEvent(tenantId, item.Id, warehouse.Id, location.Id,
                InventoryTrackingDirection.Receipt, 10m, "WA-LOT-001", DateTime.UtcNow.AddDays(-2), "wa-receipt"),
            Fixture.TraceEvent(tenantId, item.Id, warehouse.Id, location.Id,
                InventoryTrackingDirection.Issue, 4m, "wa-lot-001", null, "wa-issue"));
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(null, null, 90, 180, 90, 500);

        var row = result.Items.Single(value => value.ItemCode == item.ItemCode);
        row.ExpiredQuantity.Should().Be(6m);
        row.ExpiredValue.Should().Be(30m);
        row.DisposalCandidate.Should().BeTrue();
        row.RecommendedActionCode.Should().Be("DISPOSAL_REVIEW");
    }

    [Fact, Trait("Batch", "E2E-023")]
    public async Task Stockout_report_updates_to_the_draft_pr_created_from_the_governed_recommendation()
    {
        await using var fixture = await Fixture.CreateAsync(allowAccess: true);
        var recommendation = await fixture.Db.InventoryReplenishmentRecommendations.SingleAsync();
        var requisitionId = Guid.NewGuid();
        recommendation.Status = InventoryReplenishmentRecommendationStatus.ConvertedToRequisition;
        recommendation.PurchaseRequisitionId = requisitionId;
        recommendation.PurchaseRequisitionNumber = "PR-REP-001";
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.GetAsync(null, null, 90, 180, 90, 500);

        var stockout = result.Items.Single(value => value.ItemCode == "FAST-001");
        stockout.ReplenishmentRecommendationNumber.Should().Be("IRR-001");
        stockout.ReplenishmentRecommendationStatus.Should().Be("ConvertedToRequisition");
        stockout.ReplenishmentPurchaseRequisitionId.Should().Be(requisitionId);
        stockout.ReplenishmentPurchaseRequisitionNumber.Should().Be("PR-REP-001");
        stockout.RecommendedActionCode.Should().Be("FOLLOW_REQUISITION");
        stockout.RecommendedAction.Should().Contain("Draft Stock Replenishment PR PR-REP-001");
    }

    [Fact, Trait("Batch", "TDC-0614")]
    public async Task Analytics_fails_closed_when_no_item_location_scope_is_readable()
    {
        await using var fixture = await Fixture.CreateAsync(allowAccess: false);

        var action = async () => await fixture.Service.GetAsync(null, null, 90, 180, 90, 500);

        var error = await action.Should().ThrowAsync<InventoryAnalyticsAuthorizationException>();
        error.Which.Code.Should().Be("INV_ANALYTICS_FORBIDDEN");
    }

    [Theory, Trait("Batch", "TDC-0614")]
    [InlineData(0, 180, 90)]
    [InlineData(90, 90, 90)]
    [InlineData(90, 180, 0)]
    public async Task Analytics_rejects_invalid_classification_thresholds(
        int slowDays, int nonMovingDays, int expiryDays)
    {
        await using var fixture = await Fixture.CreateAsync(allowAccess: true);

        var action = async () => await fixture.Service.GetAsync(
            null, null, slowDays, nonMovingDays, expiryDays, 500);

        var error = await action.Should().ThrowAsync<InventoryAnalyticsException>();
        error.Which.Code.Should().Be("INV_ANALYTICS_THRESHOLDS_INVALID");
    }

    [Fact, Trait("Batch", "TDC-0614")]
    public void Analytics_indexes_related_inputs_before_projecting_each_balance()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
        var source = File.ReadAllText(Path.Combine(root,
            "src", "ErpSystem.Api", "Services", "Inventory", "InventoryAnalyticsService.cs"));
        var loopStart = source.IndexOf("foreach (var balance in balances)", StringComparison.Ordinal);
        var loopEnd = source.IndexOf("var overallBands", loopStart, StringComparison.Ordinal);
        var loop = source[loopStart..loopEnd];

        source.Should().Contain("layersByScope");
        source.Should().Contain("traceabilityByScope");
        source.Should().Contain("outboundByScope");
        source.Should().Contain("replenishmentByItemWarehouse");
        loop.Should().NotContain("layers.Where");
        loop.Should().NotContain("traceabilityEvents.Where");
        loop.Should().NotContain("movements.Where");
        loop.Should().NotContain("replenishments.FirstOrDefault");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, InventoryAnalyticsService service)
        {
            Db = db;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public InventoryAnalyticsService Service { get; }

        public static async Task<Fixture> CreateAsync(bool allowAccess)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tdc0614-analytics-{Guid.NewGuid():N}").Options;
            var db = new ApplicationDbContext(options);
            var category = new InventoryCategory
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Materials", Code = "MAT"
            };
            var warehouse = new Warehouse
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "WH-1", Name = "Main Warehouse"
            };
            var location = new WarehouseLocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WarehouseId = warehouse.Id,
                LocationCode = "A-01", Name = "Aisle 1"
            };
            var oldItem = Item(tenantId, category.Id, "OLD-001", 5m);
            var fastItem = Item(tenantId, category.Id, "FAST-001", 5m);
            var now = DateTime.UtcNow;
            db.InventoryCategories.Add(category);
            db.Warehouses.Add(warehouse);
            db.WarehouseLocations.Add(location);
            db.InventoryItems.AddRange(oldItem, fastItem);
            db.InventoryBalances.AddRange(
                new InventoryBalance
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = oldItem.Id,
                    WarehouseId = warehouse.Id, LocationId = location.Id, QuantityOnHand = 10m,
                    QuantityAvailable = 10m, TotalValue = 100m, AverageUnitCost = 10m,
                    LastReceiptDate = now.AddDays(-220), LastMovementDate = now.AddDays(-200),
                    LastIssueDate = now.AddDays(-200), InventoryItem = oldItem,
                    Warehouse = warehouse, Location = location
                },
                new InventoryBalance
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = fastItem.Id,
                    WarehouseId = warehouse.Id, LocationId = location.Id, QuantityOnHand = 0m,
                    QuantityAvailable = 0m, TotalValue = 0m, AverageUnitCost = 4m,
                    LastMovementDate = now.AddDays(-5), LastIssueDate = now.AddDays(-5),
                    InventoryItem = fastItem, Warehouse = warehouse, Location = location
                });
            db.InventoryLayers.Add(new InventoryLayer
            {
                Id = Guid.NewGuid(), TenantId = tenantId, LayerNumber = "L-OLD",
                InventoryItemId = oldItem.Id, WarehouseId = warehouse.Id, LocationId = location.Id,
                LayerDate = now.AddDays(-220), OriginalQuantity = 10m, RemainingQuantity = 10m,
                UnitCost = 10m, RemainingValue = 100m, ExpirationDate = now.AddDays(-2),
                InventoryItem = oldItem, Warehouse = warehouse, Location = location
            });
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = fastItem.Id,
                WarehouseId = warehouse.Id, LocationId = location.Id, MovementType = "Issue",
                Quantity = -9m, UnitCost = 4m, TotalValue = -36m, MovementDate = now.AddDays(-5),
                ReferenceType = ReferenceType.Requisition, ReferenceNumber = "REQ-CANONICAL",
                InventoryItem = fastItem, Warehouse = warehouse, Location = location
            });
            db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = fastItem.Id,
                WarehouseId = warehouse.Id, LocationId = location.Id, MovementType = "IssueReversal",
                Quantity = 90m, UnitCost = 4m, TotalValue = 360m, MovementDate = now.AddDays(-4),
                ReferenceType = ReferenceType.Requisition, ReferenceNumber = "REQ-REVERSAL",
                InventoryItem = fastItem, Warehouse = warehouse, Location = location
            });
            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MovementNumber = "IMV-FAST",
                InventoryItemId = fastItem.Id, WarehouseId = warehouse.Id, LocationId = location.Id,
                MovementType = InventoryMovementType.RequisitionIssue, Direction = MovementDirection.Out,
                Quantity = 900m, UnitCost = 4m, TotalValue = 3600m, MovementDate = now.AddDays(-5),
                PostingDate = now.AddDays(-5), ReferenceType = ReferenceType.Requisition,
                IsPosted = true, InventoryItem = fastItem, Warehouse = warehouse, Location = location
            });
            db.InventoryReplenishmentRecommendations.Add(new InventoryReplenishmentRecommendation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, RecommendationNumber = "IRR-001",
                WarehouseQuantityId = Guid.NewGuid(), WarehouseId = warehouse.Id,
                InventoryItemId = fastItem.Id, Status = InventoryReplenishmentRecommendationStatus.PendingApproval,
                Explanation = "Test", CalculationSnapshotJson = "{}", CalculationHash = new string('A', 64),
                GeneratedById = userId, GeneratedAtUtc = now, IdempotencyKey = "irr-test",
                PayloadHash = new string('B', 64), CorrelationId = "tdc0614-test",
                Warehouse = warehouse, InventoryItem = fastItem
            });
            await db.SaveChangesAsync();

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(service => service.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                    new ProcurementAccessCapabilityDecisionDto
                    {
                        Allowed = allowAccess, PermissionCode = request.PermissionCode,
                        WarehouseId = request.WarehouseId, LocationId = request.LocationId,
                        ActorUserId = userId, TenantId = tenantId, CorrelationId = correlation
                    });
            var service = new InventoryAnalyticsService(
                db, new TestCurrentUser(userId, tenantId), access.Object);
            return new Fixture(db, service);
        }

        private static InventoryItem Item(Guid tenantId, Guid categoryId, string code, decimal reorder) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId,
            ItemCode = code, Name = code, UnitOfMeasure = "EA", Status = ItemStatus.Active,
            ReorderLevel = reorder
        };

        internal static InventoryTraceabilityEvent TraceEvent(
            Guid tenantId,
            Guid inventoryItemId,
            Guid warehouseId,
            Guid locationId,
            InventoryTrackingDirection direction,
            decimal quantity,
            string lotNumber,
            DateTime? expiryDate,
            string eventKey) => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, InventoryItemId = inventoryItemId,
            WarehouseId = warehouseId, LocationId = locationId, Direction = direction,
            Quantity = quantity, ReferenceType = "AnalyticsTest", ReferenceNumber = eventKey,
            ReferenceId = Guid.NewGuid(), EventKey = eventKey, LotNumber = lotNumber,
            ExpiryDate = expiryDate, ActorUserId = Guid.NewGuid(), OccurredAtUtc = DateTime.UtcNow,
            CorrelationId = eventKey, PayloadHash = new string('A', 64)
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser(Guid userId, Guid tenantId) : ICurrentUserProvider
    {
        public Guid UserId { get; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username => "tdc0614.test";
        public string FullName => "TDC 0614 Test";
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC Stores Manager"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims => new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Test";
    }
}
