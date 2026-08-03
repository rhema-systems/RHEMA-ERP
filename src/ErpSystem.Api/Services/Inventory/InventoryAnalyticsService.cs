using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Inventory;

public sealed class InventoryAnalyticsService : IInventoryAnalyticsService
{
    private const int DemandWindowDays = 90;
    private static readonly BandDefinition[] Bands =
    [
        new("D000_030", "0-30 days", 0, 30),
        new("D031_060", "31-60 days", 31, 60),
        new("D061_090", "61-90 days", 61, 90),
        new("D091_180", "91-180 days", 91, 180),
        new("D181_365", "181-365 days", 181, 365),
        new("D366_PLUS", "366+ days", 366, null)
    ];

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;

    public InventoryAnalyticsService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access)
    {
        _db = db;
        _currentUser = currentUser;
        _access = access;
    }

    public async Task<InventoryAnalyticsDto> GetAsync(
        Guid? warehouseId,
        Guid? categoryId,
        int slowMovingDays,
        int nonMovingDays,
        int expiryWarningDays,
        int take,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (slowMovingDays is < 1 or > 3650 || nonMovingDays <= slowMovingDays || nonMovingDays > 3650 ||
            expiryWarningDays is < 1 or > 730)
            throw Error("INV_ANALYTICS_THRESHOLDS_INVALID",
                "Slow-moving days must be 1-3650, non-moving days must be greater and at most 3650, and expiry warning days must be 1-730.");
        take = Math.Clamp(take, 1, 2000);
        var now = DateTime.UtcNow;

        var balanceQuery = _db.InventoryBalances.AsNoTracking()
            .Include(value => value.InventoryItem).ThenInclude(value => value.Category)
            .Include(value => value.Warehouse)
            .Include(value => value.Location)
            .Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                            !value.InventoryItem.IsDeleted && !value.Warehouse.IsDeleted &&
                            value.InventoryItem.Status == ItemStatus.Active);
        if (warehouseId.HasValue) balanceQuery = balanceQuery.Where(value => value.WarehouseId == warehouseId.Value);
        if (categoryId.HasValue) balanceQuery = balanceQuery.Where(value => value.InventoryItem.CategoryId == categoryId.Value);
        var candidates = await balanceQuery.OrderBy(value => value.InventoryItem.ItemCode)
            .ThenBy(value => value.Warehouse.Code).ThenBy(value => value.LocationId)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0) return Empty(now, slowMovingDays, nonMovingDays, expiryWarningDays);

        var readableScopes = new HashSet<ScopeKey>();
        foreach (var scope in candidates.Select(value => new ScopeKey(value.WarehouseId, value.LocationId)).Distinct())
        {
            try
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = scope.WarehouseId,
                    LocationId = scope.LocationId,
                    RequireLocationScope = scope.LocationId.HasValue,
                    SourceType = "InventoryAnalytics",
                    SourceReference = $"analytics:{scope.WarehouseId:N}:{scope.LocationId?.ToString("N") ?? "warehouse"}"
                }, $"inventory-analytics-{_currentUser.UserId:N}", cancellationToken);
                if (decision.Allowed) readableScopes.Add(scope);
            }
            catch (ProcurementAccessAuthorizationException) { }
            catch (ProcurementAccessValidationException) { }
        }
        var balances = candidates.Where(value => readableScopes.Contains(new ScopeKey(value.WarehouseId, value.LocationId))).ToList();
        if (balances.Count == 0)
            throw new InventoryAnalyticsAuthorizationException(
                "The current actor has no assigned warehouse or location granting inventory read access.");

        var warehouseIds = balances.Select(value => value.WarehouseId).Distinct().ToList();
        var itemIds = balances.Select(value => value.InventoryItemId).Distinct().ToList();
        var layers = await _db.InventoryLayers.AsNoTracking().Where(value =>
                value.TenantId == _currentUser.TenantId && !value.IsDeleted && value.IsActive &&
                !value.IsFullyConsumed && value.RemainingQuantity > 0m &&
                warehouseIds.Contains(value.WarehouseId) && itemIds.Contains(value.InventoryItemId))
            .ToListAsync(cancellationToken);
        var traceabilityEvents = await _db.InventoryTraceabilityEvents.AsNoTracking().Where(value =>
                value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                warehouseIds.Contains(value.WarehouseId) && itemIds.Contains(value.InventoryItemId) &&
                (value.ExpiryDate.HasValue || value.LotNumber != null || value.BatchNumber != null ||
                 value.SerialNumber != null))
            .Select(value => new TraceabilityExpiryEvent(
                value.InventoryItemId, value.WarehouseId, value.LocationId, value.Direction,
                value.Quantity, value.LotNumber, value.BatchNumber, value.SerialNumber,
                value.ExpiryDate, value.OccurredAtUtc))
            .ToListAsync(cancellationToken);
        var demandFrom = now.Date.AddDays(-DemandWindowDays);
        var movements = await _db.InventoryMovements.AsNoTracking().Where(value =>
                value.TenantId == _currentUser.TenantId && !value.IsDeleted && value.IsPosted &&
                value.MovementDate >= demandFrom && value.MovementDate <= now &&
                warehouseIds.Contains(value.WarehouseId) && itemIds.Contains(value.InventoryItemId))
            .Select(value => new { value.InventoryItemId, value.WarehouseId, value.LocationId,
                value.Direction, value.Quantity })
            .ToListAsync(cancellationToken);
        var replenishments = await _db.InventoryReplenishmentRecommendations.AsNoTracking().Where(value =>
                value.TenantId == _currentUser.TenantId && !value.IsDeleted &&
                warehouseIds.Contains(value.WarehouseId) && itemIds.Contains(value.InventoryItemId) &&
                (value.Status == InventoryReplenishmentRecommendationStatus.Draft ||
                 value.Status == InventoryReplenishmentRecommendationStatus.PendingApproval ||
                 value.Status == InventoryReplenishmentRecommendationStatus.Approved ||
                 value.Status == InventoryReplenishmentRecommendationStatus.ConvertedToRequisition))
            .OrderByDescending(value => value.GeneratedAtUtc).ToListAsync(cancellationToken);

        var rows = new List<InventoryItemLocationAnalyticsDto>(balances.Count);
        foreach (var balance in balances)
        {
            var key = new ScopeItemKey(balance.InventoryItemId, balance.WarehouseId, balance.LocationId);
            var rowLayers = layers.Where(value => Key(value) == key).ToList();
            var expiryExposures = BuildExpiryExposures(balance, rowLayers,
                traceabilityEvents.Where(value => Key(value) == key).ToList());
            var fragments = BuildFragments(balance, rowLayers, now);
            var rowBands = BuildBands(fragments);
            var outbound = movements.Where(value => value.InventoryItemId == key.InventoryItemId &&
                    value.WarehouseId == key.WarehouseId && value.LocationId == key.LocationId &&
                    value.Direction == MovementDirection.Out)
                .Sum(value => Math.Abs(value.Quantity));
            var averageDailyDemand = Round(outbound / DemandWindowDays);
            var anchor = balance.LastIssueDate ?? balance.LastMovementDate ?? balance.LastReceiptDate ?? balance.CreatedAt;
            var daysSinceActivity = Days(now, anchor);
            var isStockout = balance.QuantityAvailable <= 0m &&
                             (averageDailyDemand > 0m || balance.InventoryItem.ReorderLevel > 0m);
            var classification = isStockout
                ? InventoryActivityClassification.Stockout
                : balance.QuantityOnHand > 0m && daysSinceActivity >= nonMovingDays
                    ? InventoryActivityClassification.NonMoving
                    : balance.QuantityOnHand > 0m && daysSinceActivity >= slowMovingDays
                        ? InventoryActivityClassification.SlowMoving
                        : InventoryActivityClassification.Active;
            var expiryCutoff = now.Date.AddDays(expiryWarningDays + 1).AddTicks(-1);
            var expired = expiryExposures.Where(value => value.ExpiryDate < now.Date).ToList();
            var expiring = expiryExposures.Where(value => value.ExpiryDate >= now.Date &&
                value.ExpiryDate <= expiryCutoff).ToList();
            var recommendation = replenishments.FirstOrDefault(value =>
                value.InventoryItemId == key.InventoryItemId && value.WarehouseId == key.WarehouseId);
            var disposal = expired.Count != 0 ||
                           (classification == InventoryActivityClassification.NonMoving && balance.QuantityOnHand > 0m);
            var replenishment = classification == InventoryActivityClassification.Stockout;
            var (actionCode, action) = Action(classification, expired.Count != 0, expiring.Count != 0, recommendation);
            var oldestAge = fragments.Count == 0 ? 0 : fragments.Max(value => value.AgeDays);
            rows.Add(new InventoryItemLocationAnalyticsDto
            {
                InventoryItemId = balance.InventoryItemId, ItemCode = balance.InventoryItem.ItemCode,
                ItemName = balance.InventoryItem.Name, CategoryName = balance.InventoryItem.Category?.Name ?? "Unclassified",
                UnitOfMeasure = balance.InventoryItem.UnitOfMeasure, WarehouseId = balance.WarehouseId,
                WarehouseCode = balance.Warehouse.Code, WarehouseName = balance.Warehouse.Name,
                LocationId = balance.LocationId, LocationCode = balance.Location?.LocationCode,
                LocationName = balance.Location?.Name, QuantityOnHand = balance.QuantityOnHand,
                QuantityAllocated = balance.QuantityAllocated, QuantityAvailable = balance.QuantityAvailable,
                QuantityOnOrder = balance.QuantityOnOrder, InventoryValue = Round(balance.TotalValue),
                AverageUnitCost = Round(balance.AverageUnitCost), ReorderLevel = balance.InventoryItem.ReorderLevel,
                LastMovementDateUtc = balance.LastMovementDate, LastReceiptDateUtc = balance.LastReceiptDate,
                LastIssueDateUtc = balance.LastIssueDate, DaysSinceActivity = daysSinceActivity,
                ActivityClassification = classification,
                CurrentStockoutDays = isStockout ? Days(now, balance.LastIssueDate ?? balance.LastMovementDate ?? anchor) : null,
                AverageDailyDemand = averageDailyDemand,
                EstimatedDaysOfCover = averageDailyDemand > 0m && balance.QuantityAvailable > 0m
                    ? Round(balance.QuantityAvailable / averageDailyDemand) : null,
                OldestStockAgeDays = oldestAge, OldestAgeingBand = Band(oldestAge).Label,
                ExpiredQuantity = Round(expired.Sum(value => value.Quantity)),
                ExpiredValue = Round(expired.Sum(value => value.Value)),
                ExpiringQuantity = Round(expiring.Sum(value => value.Quantity)),
                ExpiringValue = Round(expiring.Sum(value => value.Value)),
                DisposalCandidate = disposal, ReplenishmentCandidate = replenishment,
                ReplenishmentRecommendationId = recommendation?.Id,
                ReplenishmentRecommendationNumber = recommendation?.RecommendationNumber,
                ReplenishmentRecommendationStatus = recommendation?.Status.ToString(),
                ReplenishmentPurchaseRequisitionId = recommendation?.PurchaseRequisitionId,
                ReplenishmentPurchaseRequisitionNumber = recommendation?.PurchaseRequisitionNumber,
                RecommendedActionCode = actionCode, RecommendedAction = action,
                AgeingBands = rowBands
            });
        }

        var overallBands = Bands.Select(definition => new InventoryAgeingBandDto
        {
            Key = definition.Key, Label = definition.Label, FromDays = definition.FromDays,
            ToDays = definition.ToDays,
            Quantity = Round(rows.Sum(value => value.AgeingBands.Single(band => band.Key == definition.Key).Quantity)),
            Value = Round(rows.Sum(value => value.AgeingBands.Single(band => band.Key == definition.Key).Value)),
            ItemLocationCount = rows.Count(value =>
                value.AgeingBands.Single(band => band.Key == definition.Key).Quantity > 0m)
        }).ToList();
        var ordered = rows.OrderByDescending(value => value.ExpiredQuantity > 0m)
            .ThenByDescending(value => value.ActivityClassification)
            .ThenByDescending(value => value.InventoryValue)
            .ThenBy(value => value.ItemCode).ThenBy(value => value.LocationCode).Take(take).ToList();
        return new InventoryAnalyticsDto
        {
            AsOfUtc = now, SlowMovingDays = slowMovingDays, NonMovingDays = nonMovingDays,
            ExpiryWarningDays = expiryWarningDays, AgeingBands = overallBands, Items = ordered,
            Summary = new InventoryAnalyticsSummaryDto
            {
                ItemLocationCount = rows.Count, QuantityOnHand = Round(rows.Sum(value => value.QuantityOnHand)),
                InventoryValue = Round(rows.Sum(value => value.InventoryValue)),
                SlowMovingCount = rows.Count(value => value.ActivityClassification == InventoryActivityClassification.SlowMoving),
                SlowMovingValue = Round(rows.Where(value => value.ActivityClassification == InventoryActivityClassification.SlowMoving).Sum(value => value.InventoryValue)),
                NonMovingCount = rows.Count(value => value.ActivityClassification == InventoryActivityClassification.NonMoving),
                NonMovingValue = Round(rows.Where(value => value.ActivityClassification == InventoryActivityClassification.NonMoving).Sum(value => value.InventoryValue)),
                StockoutCount = rows.Count(value => value.ActivityClassification == InventoryActivityClassification.Stockout),
                DisposalCandidateCount = rows.Count(value => value.DisposalCandidate),
                ReplenishmentCandidateCount = rows.Count(value => value.ReplenishmentCandidate),
                ExpiredQuantity = Round(rows.Sum(value => value.ExpiredQuantity)),
                ExpiredValue = Round(rows.Sum(value => value.ExpiredValue)),
                ExpiringQuantity = Round(rows.Sum(value => value.ExpiringQuantity)),
                ExpiringValue = Round(rows.Sum(value => value.ExpiringValue))
            }
        };
    }

    private static List<AgeFragment> BuildFragments(InventoryBalance balance,
        IReadOnlyList<InventoryLayer> layers, DateTime now)
    {
        if (balance.QuantityOnHand <= 0m) return [];
        var fragments = layers.Select(value => new AgeFragment(
            Days(now, value.LayerDate), value.RemainingQuantity, value.RemainingValue)).ToList();
        var layeredQuantity = fragments.Sum(value => value.Quantity);
        if (layeredQuantity < balance.QuantityOnHand)
        {
            var quantity = balance.QuantityOnHand - layeredQuantity;
            var date = balance.LastReceiptDate ?? balance.LastMovementDate ?? balance.CreatedAt;
            fragments.Add(new AgeFragment(Days(now, date), quantity, quantity * balance.AverageUnitCost));
        }
        else if (layeredQuantity > balance.QuantityOnHand && layeredQuantity > 0m)
        {
            var factor = balance.QuantityOnHand / layeredQuantity;
            fragments = fragments.Select(value => value with
            {
                Quantity = value.Quantity * factor,
                Value = value.Value * factor
            }).ToList();
        }
        var valueTotal = fragments.Sum(value => value.Value);
        if (valueTotal != 0m && Math.Abs(valueTotal - balance.TotalValue) > 0.0001m)
        {
            var factor = balance.TotalValue / valueTotal;
            fragments = fragments.Select(value => value with { Value = value.Value * factor }).ToList();
        }
        return fragments;
    }

    private static IReadOnlyList<InventoryAgeingBandDto> BuildBands(IReadOnlyList<AgeFragment> fragments) =>
        Bands.Select(definition =>
        {
            var values = fragments.Where(value => definition.Includes(value.AgeDays)).ToList();
            return new InventoryAgeingBandDto
            {
                Key = definition.Key, Label = definition.Label, FromDays = definition.FromDays,
                ToDays = definition.ToDays, Quantity = Round(values.Sum(value => value.Quantity)),
                Value = Round(values.Sum(value => value.Value)), ItemLocationCount = values.Count == 0 ? 0 : 1
            };
        }).ToList();

    private static IReadOnlyList<ExpiryExposure> BuildExpiryExposures(
        InventoryBalance balance,
        IReadOnlyList<InventoryLayer> layers,
        IReadOnlyList<TraceabilityExpiryEvent> events)
    {
        var tracked = events
            .GroupBy(value => new TraceabilityKey(
                NormalizeTracking(value.LotNumber),
                NormalizeTracking(value.BatchNumber),
                NormalizeTracking(value.SerialNumber),
                HasTrackingIdentity(value) ? null : value.ExpiryDate?.Date))
            .Select(group => new
            {
                ExpiryDate = group.Where(value => value.ExpiryDate.HasValue)
                    .OrderByDescending(value => value.OccurredAtUtc)
                    .Select(value => value.ExpiryDate!.Value.Date)
                    .FirstOrDefault(),
                Quantity = group.Sum(value => SignedTrackingQuantity(value.Direction, value.Quantity))
            })
            .Where(value => value.ExpiryDate != default && value.Quantity > 0m)
            .ToList();

        if (tracked.Count != 0)
        {
            var trackedQuantity = tracked.Sum(value => value.Quantity);
            var quantityFactor = balance.QuantityOnHand > 0m && trackedQuantity > balance.QuantityOnHand
                ? balance.QuantityOnHand / trackedQuantity
                : 1m;
            var unitValue = balance.QuantityOnHand > 0m
                ? balance.TotalValue / balance.QuantityOnHand
                : balance.AverageUnitCost;
            return tracked.Select(value => new ExpiryExposure(
                value.ExpiryDate,
                value.Quantity * quantityFactor,
                value.Quantity * quantityFactor * unitValue)).ToList();
        }

        return layers.Where(value => value.ExpirationDate.HasValue && value.RemainingQuantity > 0m)
            .Select(value => new ExpiryExposure(
                value.ExpirationDate!.Value.Date,
                value.RemainingQuantity,
                value.RemainingValue)).ToList();
    }

    private static decimal SignedTrackingQuantity(InventoryTrackingDirection direction, decimal quantity) =>
        direction is InventoryTrackingDirection.Receipt or InventoryTrackingDirection.Return or
            InventoryTrackingDirection.TransferIn or InventoryTrackingDirection.AdjustmentIn
            ? Math.Abs(quantity)
            : -Math.Abs(quantity);

    private static bool HasTrackingIdentity(TraceabilityExpiryEvent value) =>
        !string.IsNullOrWhiteSpace(value.LotNumber) || !string.IsNullOrWhiteSpace(value.BatchNumber) ||
        !string.IsNullOrWhiteSpace(value.SerialNumber);

    private static string? NormalizeTracking(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static (string Code, string Text) Action(InventoryActivityClassification classification,
        bool expired, bool expiring, InventoryReplenishmentRecommendation? recommendation)
    {
        if (expired) return ("DISPOSAL_REVIEW", "Quarantine expired stock and start a TDC-0615 disposal review.");
        if (expiring) return ("FEFO_REVIEW", "Prioritize FEFO issue or transfer and review residual stock for disposal.");
        if (classification == InventoryActivityClassification.Stockout &&
            recommendation?.Status == InventoryReplenishmentRecommendationStatus.ConvertedToRequisition &&
            recommendation.PurchaseRequisitionId.HasValue)
            return ("FOLLOW_REQUISITION",
                $"Follow Draft Stock Replenishment PR {recommendation.PurchaseRequisitionNumber} generated from governed replenishment {recommendation.RecommendationNumber}.");
        if (classification == InventoryActivityClassification.Stockout && recommendation is not null)
            return ("FOLLOW_REPLENISHMENT", $"Follow governed replenishment {recommendation.RecommendationNumber} ({recommendation.Status}).");
        if (classification == InventoryActivityClassification.Stockout)
            return ("GENERATE_REPLENISHMENT", "Generate a governed TDC-0612 replenishment recommendation.");
        if (classification == InventoryActivityClassification.NonMoving)
            return ("DISPOSAL_REVIEW", "Review redeployment, operational need and TDC-0615 disposal eligibility.");
        if (classification == InventoryActivityClassification.SlowMoving)
            return ("REDEPLOY_OR_CONSUME", "Review demand, transfer opportunities and planned consumption.");
        return ("MONITOR", "No exception action; continue normal stock monitoring.");
    }

    private static ScopeItemKey Key(InventoryLayer value) =>
        new(value.InventoryItemId, value.WarehouseId, value.LocationId);
    private static ScopeItemKey Key(TraceabilityExpiryEvent value) =>
        new(value.InventoryItemId, value.WarehouseId, value.LocationId);
    private static BandDefinition Band(int ageDays) => Bands.First(value => value.Includes(ageDays));
    private static int Days(DateTime now, DateTime value) => Math.Max(0, (now.Date - value.Date).Days);
    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    private static InventoryAnalyticsException Error(string code, string message) => new(code, message);
    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new InventoryAnalyticsAuthorizationException("An authenticated tenant actor is required.");
    }
    private static InventoryAnalyticsDto Empty(DateTime now, int slow, int nonMoving, int expiry) => new()
    {
        AsOfUtc = now, SlowMovingDays = slow, NonMovingDays = nonMoving, ExpiryWarningDays = expiry,
        AgeingBands = Bands.Select(value => new InventoryAgeingBandDto
        {
            Key = value.Key, Label = value.Label, FromDays = value.FromDays, ToDays = value.ToDays
        }).ToList()
    };

    private readonly record struct ScopeKey(Guid WarehouseId, Guid? LocationId);
    private readonly record struct ScopeItemKey(Guid InventoryItemId, Guid WarehouseId, Guid? LocationId);
    private readonly record struct TraceabilityKey(
        string? LotNumber,
        string? BatchNumber,
        string? SerialNumber,
        DateTime? UnidentifiedExpiryDate);
    private sealed record TraceabilityExpiryEvent(
        Guid InventoryItemId,
        Guid WarehouseId,
        Guid? LocationId,
        InventoryTrackingDirection Direction,
        decimal Quantity,
        string? LotNumber,
        string? BatchNumber,
        string? SerialNumber,
        DateTime? ExpiryDate,
        DateTime OccurredAtUtc);
    private sealed record ExpiryExposure(DateTime ExpiryDate, decimal Quantity, decimal Value);
    private sealed record BandDefinition(string Key, string Label, int FromDays, int? ToDays)
    {
        public bool Includes(int ageDays) => ageDays >= FromDays && (!ToDays.HasValue || ageDays <= ToDays.Value);
    }
    private sealed record AgeFragment(int AgeDays, decimal Quantity, decimal Value);
}
