using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErpSystem.Core.Services.Inventory;

public partial class PhysicalCountService
{
    private async Task<WarehouseLocation?> ValidateCountLocationAsync(PhysicalCount count, Guid? locationId)
    {
        if (!locationId.HasValue) return null; // A whole-warehouse count has no header location.
        return await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
            x.Id == locationId.Value && x.TenantId == count.TenantId &&
            ((!x.IsConsignmentBin && x.WarehouseId == count.WarehouseId) ||
             (x.IsConsignmentBin && x.ConsignmentWarehouseId == count.WarehouseId)) &&
            x.IsActive && !x.IsDeleted).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Select an active location belonging to the selected warehouse.");
    }

    // The header controls scope, but each physical stock row keeps its actual bin.
    // A warehouse total must never be copied into every bin's system quantity.
    private async Task<List<PhysicalCountItem>> BuildScopedCountItemsAsync(
        PhysicalCount count, Guid? itemId = null, Guid? exactLocationId = null)
    {
        var locationId = exactLocationId ?? count.LocationId;
        await ValidateCountLocationAsync(count, locationId);
        // Fill only the unlocated warehouse remainder. Existing assigned bins and
        // warehouse totals are preserved by the central default-bin service.
        var warehouseItemIds = await _unitOfWork.Repository<WarehouseQuantity>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.WarehouseId == count.WarehouseId && !x.IsDeleted &&
            (!itemId.HasValue || x.InventoryItemId == itemId.Value)).Select(x => x.InventoryItemId).Distinct().ToListAsync();
        var defaultCandidates = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x =>
            x.TenantId == count.TenantId && !x.IsDeleted && warehouseItemIds.Contains(x.Id) &&
            (!count.CategoryId.HasValue || x.CategoryId == count.CategoryId.Value)).AsNoTracking().ToListAsync();
        foreach (var candidate in defaultCandidates.Where(x => count.CountType != CountType.CycleCount ||
                     string.Equals(x.ABCClass, count.ABCClass, StringComparison.OrdinalIgnoreCase)))
            await _defaultLocations.EnsureItemAssignmentAsync(count.WarehouseId, candidate.Id,
                Guid.Parse(_currentUserService.UserId!));
        var locations = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
            x.TenantId == count.TenantId && !x.IsDeleted &&
            ((!x.IsConsignmentBin && x.WarehouseId == count.WarehouseId) ||
             (x.IsConsignmentBin && x.ConsignmentWarehouseId == count.WarehouseId)) &&
            (!locationId.HasValue || x.Id == locationId.Value)).AsNoTracking().ToDictionaryAsync(x => x.Id);
        var locationIds = locations.Keys.ToList();
        var quantities = await _unitOfWork.Repository<InventoryLocation>().GetQueryable(x =>
            x.TenantId == count.TenantId && !x.IsDeleted && locationIds.Contains(x.LocationId) &&
            (!itemId.HasValue || x.InventoryItemId == itemId.Value)).AsNoTracking().ToListAsync();
        var warehouseQuantities = await _unitOfWork.Repository<WarehouseQuantity>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.WarehouseId == count.WarehouseId && !x.IsDeleted &&
            (!itemId.HasValue || x.InventoryItemId == itemId.Value)).AsNoTracking().ToListAsync();
        var itemIds = quantities.Select(x => x.InventoryItemId)
            .Concat(locationId.HasValue ? [] : warehouseQuantities.Select(x => x.InventoryItemId)).Distinct().ToList();
        var candidates = await _unitOfWork.Repository<InventoryItem>().GetQueryable(x =>
            x.TenantId == count.TenantId && !x.IsDeleted && itemIds.Contains(x.Id) &&
            (!count.CategoryId.HasValue || x.CategoryId == count.CategoryId.Value)).AsNoTracking().ToListAsync();
        var items = candidates.Where(x => count.CountType != CountType.CycleCount ||
            string.Equals(x.ABCClass, count.ABCClass, StringComparison.OrdinalIgnoreCase)).ToDictionary(x => x.Id);
        var balances = await _unitOfWork.Repository<InventoryBalance>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.WarehouseId == count.WarehouseId && !x.IsDeleted &&
            itemIds.Contains(x.InventoryItemId)).AsNoTracking().ToListAsync();
        var layers = await _unitOfWork.Repository<InventoryLayer>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.WarehouseId == count.WarehouseId && !x.IsDeleted && x.IsActive &&
            !x.IsFullyConsumed && x.RemainingQuantity > 0 && itemIds.Contains(x.InventoryItemId)).AsNoTracking().ToListAsync();
        var rows = new List<PhysicalCountItem>();
        foreach (var item in items.Values.OrderBy(x => x.ItemCode))
        {
            var itemQuantities = quantities.Where(x => x.InventoryItemId == item.Id).ToList();
            foreach (var group in itemQuantities.GroupBy(x => x.LocationId).OrderBy(x => locations[x.Key].LocationCode))
            {
                if (!locations[group.Key].IsActive)
                    throw new InvalidOperationException($"{item.ItemCode} has stock assigned to inactive location {locations[group.Key].LocationCode}. Resolve that location before creating the count.");
                var quantity = new InventoryLocation { InventoryItemId = item.Id, LocationId = group.Key,
                    Quantity = group.Sum(x => x.Quantity), AverageCost = group.First().AverageCost };
                if (quantity.Quantity < 0)
                    throw new InvalidOperationException($"Reconcile the negative stock balance for {item.ItemCode} at {locations[group.Key].LocationCode} before creating the count.");
                var itemLayers = layers.Where(x => x.InventoryItemId == item.Id && x.LocationId == group.Key).ToList();
                var fifoCost = itemLayers.Count == 0 ? 0 : itemLayers.Sum(x => x.RemainingQuantity * x.UnitCost) / itemLayers.Sum(x => x.RemainingQuantity);
                var unitCost = ResolveCycleCountUnitCost(item, quantity,
                    balances.FirstOrDefault(x => x.InventoryItemId == item.Id && x.LocationId == group.Key), fifoCost);
                rows.Add(ScopeLine(count, item, group.Key, quantity.Quantity, unitCost));
            }
            if (!locationId.HasValue)
            {
                var warehouseQuantity = warehouseQuantities.Where(x => x.InventoryItemId == item.Id).ToList();
                if (warehouseQuantity.Count > 0)
                {
                    var residual = warehouseQuantity.Sum(x => x.CurrentStock) - itemQuantities.Sum(x => x.Quantity);
                    if (residual < 0)
                        throw new InvalidOperationException($"Reconcile {item.ItemCode}: its location balances exceed the warehouse balance. The count was not created.");
                    // Default assignment must fully reconcile each warehouse remainder.
                    if (residual != 0 || itemQuantities.Count == 0)
                        throw new InvalidOperationException($"The default-bin assignment for {item.ItemCode} could not be reconciled. No count was created.");
                }
            }
        }
        return rows;
    }

    private static PhysicalCountItem ScopeLine(PhysicalCount count, InventoryItem item,
        Guid? locationId, decimal quantity, decimal cost) => new()
    {
        TenantId = count.TenantId, PhysicalCountId = count.Id, InventoryItemId = item.Id,
        LocationId = locationId, ItemCode = item.ItemCode, ItemName = item.Name,
        UnitOfMeasure = item.UnitOfMeasure, SystemQuantity = quantity, UnitCost = cost
    };

    private async Task ValidateSavedCountScopeAsync(PhysicalCount count, Guid userId)
    {
        await ValidateCountLocationAsync(count, count.LocationId);
        var lines = count.Items.Where(x => !x.IsDeleted).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("Add at least one item before starting or submitting the count.");
        var resolved = await ResolveLegacyCountLocationsAsync(count, userId);
        if (count.LocationId.HasValue && lines.Any(x => EffectiveCountLocation(x, resolved) != count.LocationId))
            throw new InvalidOperationException("This location count contains items outside the selected location. Prepare a new count with the correct scope.");
        var ids = lines.Select(x => EffectiveCountLocation(x, resolved) ?? Guid.Empty).Distinct().ToList();
        var validIds = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.IsActive && !x.IsDeleted && ids.Contains(x.Id) &&
            ((!x.IsConsignmentBin && x.WarehouseId == count.WarehouseId) ||
             (x.IsConsignmentBin && x.ConsignmentWarehouseId == count.WarehouseId)))
            .Select(x => x.Id).ToListAsync();
        if (validIds.Count != ids.Count)
            throw new InvalidOperationException("Every count line must belong to an active location in the selected warehouse.");
        if (lines.GroupBy(x => new { x.InventoryItemId, LocationId = EffectiveCountLocation(x, resolved) }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Count each item once per location. Remove duplicate item/location lines before starting.");
    }

    private sealed record ResolvedCountLocation(Guid LineId, Guid LocationId, string LocationCode);

    private static Dictionary<Guid, ResolvedCountLocation> ReadResolvedCountLocations(PhysicalCount count)
    {
        var result = new Dictionary<Guid, ResolvedCountLocation>();
        foreach (var action in count.Actions.Where(x => !x.IsDeleted && x.ActionType == PhysicalCountActionType.DefaultLocationsResolved).OrderBy(x => x.Sequence))
        {
            using var snapshot = JsonDocument.Parse(action.SnapshotJson);
            if (!snapshot.RootElement.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("resolvedLocations", out var locations)) continue;
            foreach (var location in locations.EnumerateArray())
            {
                var value = new ResolvedCountLocation(location.GetProperty("lineId").GetGuid(),
                    location.GetProperty("locationId").GetGuid(), location.GetProperty("locationCode").GetString() ?? string.Empty);
                if (result.TryGetValue(value.LineId, out var previous) && previous != value)
                    throw new InvalidOperationException("The count contains conflicting recorded default locations.");
                result[value.LineId] = value;
            }
        }
        return result;
    }

    private static Guid? EffectiveCountLocation(PhysicalCountItem line, IReadOnlyDictionary<Guid, ResolvedCountLocation> resolved)
        => line.LocationId is { } locationId && locationId != Guid.Empty ? locationId : resolved.GetValueOrDefault(line.Id)?.LocationId;

    private async Task<Dictionary<Guid, ResolvedCountLocation>> ResolveLegacyCountLocationsAsync(PhysicalCount count, Guid userId)
    {
        // Old snapshots are immutable. Retain a separate audited mapping rather
        // than rewriting their LocationId, counted quantities, baseline or cost.
        var storedActions = await _unitOfWork.Repository<PhysicalCountAction>().GetQueryable(x =>
            x.TenantId == count.TenantId && x.PhysicalCountId == count.Id && !x.IsDeleted).AsNoTracking().ToListAsync();
        var readOnlyCount = new PhysicalCount { Actions = storedActions };
        var resolved = ReadResolvedCountLocations(readOnlyCount);
        var missing = count.Items.Where(x => !x.IsDeleted && (!x.LocationId.HasValue || x.LocationId == Guid.Empty) && !resolved.ContainsKey(x.Id)).ToList();
        if (missing.Count == 0) return resolved;
        var defaultLocation = await _defaultLocations.GetOrCreateAsync(count.WarehouseId, userId);
        if (count.LocationId.HasValue && count.LocationId != defaultLocation.Id)
            throw new InvalidOperationException("This old location count contains an unlocated item outside the warehouse default. Create a new count for the correct location.");
        var warehouseLocationIds = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(x =>
            x.TenantId == count.TenantId && !x.IsDeleted &&
            ((!x.IsConsignmentBin && x.WarehouseId == count.WarehouseId) ||
             (x.IsConsignmentBin && x.ConsignmentWarehouseId == count.WarehouseId)))
            .Select(x => x.Id).ToListAsync();
        var additions = new List<ResolvedCountLocation>();
        foreach (var line in missing)
        {
            if (await _unitOfWork.Repository<InventoryLocation>().GetQueryable(x =>
                x.TenantId == count.TenantId && !x.IsDeleted && x.InventoryItemId == line.InventoryItemId &&
                warehouseLocationIds.Contains(x.LocationId) && x.LocationId != defaultLocation.Id &&
                (x.Quantity != 0 || x.AllocatedQuantity != 0)).AnyAsync())
                throw new InvalidOperationException($"{line.ItemCode} was counted without a bin but already has stock in another location. A warehouse total cannot be assigned entirely to the default bin. Create a new scoped count for this item; saved quantities are unchanged.");
            await _defaultLocations.EnsureItemAssignmentAsync(count.WarehouseId, line.InventoryItemId, userId);
            var value = new ResolvedCountLocation(line.Id, defaultLocation.Id, defaultLocation.LocationCode);
            resolved[line.Id] = value;
            additions.Add(value);
        }
        await AddCountActionAsync(count, PhysicalCountActionType.DefaultLocationsResolved, userId,
            $"default-bin:{count.Id:N}:{Hash(string.Join(',', additions.Select(x => x.LineId).Order()))[..16]}",
            "Missing count locations resolved to the warehouse default; original quantities and snapshots retained.",
            new { ResolvedLocations = additions }, "Counter");
        await _unitOfWork.SaveChangesAsync();
        return resolved;
    }
}
